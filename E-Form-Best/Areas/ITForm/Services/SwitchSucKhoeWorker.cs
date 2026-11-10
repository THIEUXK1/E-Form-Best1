using System.Text.Json;
using E_Form_Best.Context;
using E_Form_Best.Models.ITForm;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace E_Form_Best.Areas.ITForm.Services
{
    /// <summary>
    /// Job nền đọc sức khoẻ switch lõi / WLC qua SNMP (SwitchSucKhoe:ChuKyGiay, mặc định 300 giây) cho các switch
    /// có dòng trong KK_SwitchSucKhoe: ghi đè ảnh chụp, đổi trạng thái thì ghi KK_SwitchSucKhoeLichSu (append-only).
    ///
    /// Community trong .env (không hardcode), dùng chung với nút "Kiểm tra kết nối": SwitchSnmp__{CÔNG TY}__Community,
    /// SwitchSnmp__Community; trống thì ServerSnmp__Community__LuuTru (thiết bị mạng/lưu trữ BPVN cùng community).
    /// Công ty đọc theo Switch:CongTyPing như job ping — 10.0.63.x BPVN chỉ máy chủ 60.52 tới được,
    /// máy không tới mà vẫn đọc sẽ ghi "Không đọc được" giả vào nhật ký.
    /// </summary>
    public class SwitchSucKhoeWorker : BackgroundService
    {
        private static readonly JsonSerializerOptions JsonOpt = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

        private readonly IServiceScopeFactory _scopeFactory;
        private readonly IConfiguration _configuration;

        public SwitchSucKhoeWorker(IServiceScopeFactory scopeFactory, IConfiguration configuration)
        {
            _scopeFactory = scopeFactory;
            _configuration = configuration;
        }

        public static int ChuKyGiay(IConfiguration cfg)
            => Math.Clamp(cfg.GetValue<int?>("SwitchSucKhoe:ChuKyGiay") ?? 300, 60, 86400);

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            var bat = _configuration.GetValue<bool?>("SwitchSucKhoe:Bat")
                      ?? _configuration.GetValue<bool?>("Switch:GhiLichSu") ?? true;
            if (!bat) return;

            try { await Task.Delay(TimeSpan.FromSeconds(45), stoppingToken); }
            catch (TaskCanceledException) { return; }

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    var soSuKien = await QuetAsync(stoppingToken);
                    if (soSuKien > 0)
                        Console.WriteLine($"[SwitchSucKhoe] {soSuKien} switch đổi trạng thái sức khoẻ lúc {DateTime.Now:dd/MM/yyyy HH:mm}");
                }
                catch (TaskCanceledException) { return; }
                catch (Exception ex)
                {
                    // Bảng chưa tạo (trước khi chạy script) cũng rơi vào đây — chỉ log, không làm sập app
                    Console.WriteLine($"[SwitchSucKhoe Error]: {ex.Message}");
                }

                try { await Task.Delay(TimeSpan.FromSeconds(ChuKyGiay(_configuration)), stoppingToken); }
                catch (TaskCanceledException) { return; }
            }
        }

        /// <summary>Công ty máy này đọc: Switch:CongTyPing, trống thì cả 3 (giống SwitchPingWorker).</summary>
        public static List<string> DsCongTyTheoDoi(IConfiguration cfg)
        {
            var cauHinh = cfg["Switch:CongTyPing"];
            if (string.IsNullOrWhiteSpace(cauHinh)) return SwitchPingWorker.DsCongTy.ToList();
            return cauHinh.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Select(x => SwitchPingWorker.DsCongTy.FirstOrDefault(c => c.Equals(x, StringComparison.OrdinalIgnoreCase)))
                .Where(x => x != null).Select(x => x!).Distinct().ToList();
        }

        public static string? Community(IConfiguration cfg, string congTy)
        {
            foreach (var khoa in new[] { $"SwitchSnmp:{congTy}:Community", "SwitchSnmp:Community", "ServerSnmp:Community:LuuTru" })
                if (!string.IsNullOrWhiteSpace(cfg[khoa])) return cfg[khoa];
            return null;
        }

        private async Task<int> QuetAsync(CancellationToken ct)
        {
            var dsCongTy = DsCongTyTheoDoi(_configuration);
            if (dsCongTy.Count == 0) return 0;

            List<int> dsId;
            using (var scope = _scopeFactory.CreateScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<ITFormContext>();
                var ds = await db.KkSwitchSucKhoes
                    .Where(x => x.IdSwitchNavigation != null && x.IdSwitchNavigation.NgayXoa == null
                                && x.IdSwitchNavigation.DiaChiIp != null
                                && x.IdSwitchNavigation.IdcongTyNavigation != null
                                && dsCongTy.Contains(x.IdSwitchNavigation.IdcongTyNavigation.TenCongTy))
                    .Select(x => new { x.IdSwitch, CongTy = x.IdSwitchNavigation!.IdcongTyNavigation!.TenCongTy, Ip = x.IdSwitchNavigation.DiaChiIp })
                    .ToListAsync(ct);
                dsId = ds.Where(x => !ServerPingWorker.BoQuaIp(_configuration, x.Ip) && !string.IsNullOrWhiteSpace(Community(_configuration, x.CongTy)))
                    .Select(x => x.IdSwitch).ToList();
            }

            var doi = 0;
            await Parallel.ForEachAsync(dsId, new ParallelOptions { MaxDegreeOfParallelism = 4, CancellationToken = ct }, async (id, token) =>
            {
                // Mỗi thiết bị 1 scope: DbContext không dùng chung giữa các luồng
                using var scope = _scopeFactory.CreateScope();
                var db = scope.ServiceProvider.GetRequiredService<ITFormContext>();
                try
                {
                    if ((await DocVaLuuAsync(db, _configuration, id, false, token)).DoiTrangThai) Interlocked.Increment(ref doi);
                }
                catch (OperationCanceledException) when (token.IsCancellationRequested) { throw; }
                catch (Exception ex) { Console.WriteLine($"[SwitchSucKhoe] id {id}: {ex.Message}"); }
            });
            return doi;
        }

        public record KetQuaLuu(bool DaLuu, bool DoiTrangThai, string? Loi);

        /// <summary>
        /// Đọc SNMP 1 switch và lưu ảnh chụp. Dùng cho cả job nền và nút "Đọc ngay" trên trang.
        /// khongLuuKhiLoi: nút "Đọc ngay" chạy trên máy chủ web đang phục vụ — máy đó có thể không thông mạng tới switch
        /// (10.0.63.x chỉ 60.52 tới), đọc lỗi thì chỉ báo lại, không ghi "Không đọc được" giả vào nhật ký.
        /// </summary>
        public static async Task<KetQuaLuu> DocVaLuuAsync(ITFormContext db, IConfiguration cfg, int idSwitch, bool khongLuuKhiLoi, CancellationToken ct)
        {
            var sw = await db.KkSwitches
                .Where(x => x.IdSwitch == idSwitch && x.NgayXoa == null)
                .Select(x => new { x.IdSwitch, x.IdcongTy, CongTy = x.IdcongTyNavigation!.TenCongTy, x.TenSwitch, x.DiaChiIp })
                .FirstOrDefaultAsync(ct);
            var cu = await db.KkSwitchSucKhoes.AsNoTracking().Where(x => x.IdSwitch == idSwitch)
                .Select(x => new { x.TrangThai, x.DocDuocLuc, x.IpSnmp, x.TenHeThong, x.MoTa, x.DuLieu }).FirstOrDefaultAsync(ct);
            if (sw == null || cu == null) return new KetQuaLuu(false, false, "Switch không còn trong danh sách theo dõi.");

            var com = Community(cfg, sw.CongTy);
            SwitchSucKhoeService.KetQua? kq = null;
            string? loi = null;
            if (string.IsNullOrWhiteSpace(com))
                loi = "Chưa cấu hình community SNMP (SwitchSnmp__Community).";
            else if (string.IsNullOrWhiteSpace(sw.DiaChiIp))
                loi = "Switch chưa có địa chỉ IP.";
            else
            {
                var ip = await SnmpClient.PhanGiaiAsync(sw.DiaChiIp, ct);
                kq = ip == null ? null : await SwitchSucKhoeService.DocAsync(ip, com, ct);
                if (kq == null) loi = $"Không đọc được SNMP từ {sw.DiaChiIp} (community sai, chưa bật SNMP hoặc máy chủ này không thông mạng tới).";
            }

            if (kq == null && khongLuuKhiLoi) return new KetQuaLuu(false, false, loi);

            var bayGio = DateTime.Now;
            if (kq != null && cu.DocDuocLuc != null) TinhTangLoi(kq, cu.DuLieu);

            var trangThai = kq == null ? SwitchSucKhoeService.KhongDocDuoc : SwitchSucKhoeService.TrangThaiTong(kq.DsMuc);
            var tomTat = kq == null ? loi
                : Cat(string.Join("; ", kq.DsMuc.Where(x => x.TrangThai is SwitchSucKhoeService.Loi or SwitchSucKhoeService.CanhBao)
                    .Select(x => $"{x.Nhom} {x.Ten}: {x.GiaTri}")), 1000);
            if (string.IsNullOrWhiteSpace(tomTat)) tomTat = null;
            var duLieu = kq == null ? null : JsonSerializer.Serialize(new
            {
                kq.ThoiGianChay, dsMuc = kq.DsMuc,
                cong = new { tong = kq.SoCong, up = kq.SoCongUp, down = kq.SoCongDown, tat = kq.SoCongTat },
                dsCongLoi = kq.DsCongLoi, kq.SoAp, kq.SoClient
            }, JsonOpt);

            // Lượt đọc lỗi giữ số liệu lần đọc được gần nhất để trang vẫn có cái xem, chỉ đổi trạng thái + lỗi.
            // Điều kiện trạng thái cũ trong WHERE: máy chủ kia vừa ghi giữa chừng thì lượt này không ghi nhật ký trùng.
            var soDong = await db.KkSwitchSucKhoes
                .Where(x => x.IdSwitch == idSwitch && x.TrangThai == cu.TrangThai)
                .ExecuteUpdateAsync(s => s
                    .SetProperty(x => x.KiemTraLuc, bayGio)
                    .SetProperty(x => x.DocDuocLuc, kq != null ? (DateTime?)bayGio : cu.DocDuocLuc)
                    .SetProperty(x => x.IpSnmp, kq != null ? Cat(kq.Ip, 100) : cu.IpSnmp)
                    .SetProperty(x => x.TenHeThong, kq != null ? Cat(kq.TenHeThong, 255) : cu.TenHeThong)
                    .SetProperty(x => x.MoTa, kq != null ? Cat(kq.MoTa, 500) : cu.MoTa)
                    .SetProperty(x => x.TrangThai, trangThai)
                    .SetProperty(x => x.TomTat, tomTat)
                    .SetProperty(x => x.DuLieu, kq != null ? duLieu : cu.DuLieu)
                    .SetProperty(x => x.Loi, Cat(loi, 500)), ct);

            // Lần đọc đầu (CHUA_DOC) không phải sự kiện đổi trạng thái
            var doi = soDong == 1 && cu.TrangThai != trangThai && cu.TrangThai != SwitchSucKhoeService.ChuaDoc;
            if (doi)
            {
                var sk = new KkSwitchSucKhoeLichSu
                {
                    IdSwitch = idSwitch, IdcongTy = sw.IdcongTy, ThoiGian = bayGio, TenSwitch = Cat(sw.TenSwitch, 255),
                    TuTrangThai = cu.TrangThai, SangTrangThai = trangThai, TomTat = tomTat, GhiNhanLuc = bayGio
                };
                db.KkSwitchSucKhoeLichSus.Add(sk);
                try { await db.SaveChangesAsync(ct); }
                catch (DbUpdateException ex) when (ex.InnerException is SqlException sql && (sql.Number == 2601 || sql.Number == 2627))
                {
                    db.Entry(sk).State = EntityState.Detached;
                }
            }
            return new KetQuaLuu(soDong == 1, doi, loi);
        }

        /// <summary>
        /// Lỗi cổng là bộ đếm cộng dồn từ lúc khởi động — số lớn chưa chắc đang lỗi. So với lần đọc trước để biết
        /// cổng nào lỗi đang TĂNG; bộ đếm nhỏ đi (thiết bị khởi động lại / clear counters) thì bỏ qua.
        /// </summary>
        private static void TinhTangLoi(SwitchSucKhoeService.KetQua kq, string? duLieuCu)
        {
            if (string.IsNullOrWhiteSpace(duLieuCu) || kq.DsCongLoi.Count == 0) return;
            Dictionary<int, (long vao, long ra)> truoc;
            try
            {
                using var doc = JsonDocument.Parse(duLieuCu);
                if (!doc.RootElement.TryGetProperty("dsCongLoi", out var mang) || mang.ValueKind != JsonValueKind.Array) return;
                truoc = mang.EnumerateArray()
                    .GroupBy(x => x.GetProperty("ifIndex").GetInt32())
                    .ToDictionary(g => g.Key, g => (g.First().GetProperty("loiVao").GetInt64(), g.First().GetProperty("loiRa").GetInt64()));
            }
            catch (Exception ex) when (ex is JsonException or InvalidOperationException or KeyNotFoundException or FormatException) { return; }

            kq.DsCongLoi = kq.DsCongLoi.Select(c =>
            {
                // Cổng lần trước chưa có lỗi (không nằm trong danh sách) = lần trước 0
                var (v, r) = truoc.TryGetValue(c.IfIndex, out var t) ? t : (0L, 0L);
                return c.LoiVao >= v && c.LoiRa >= r ? c with { TangVao = c.LoiVao - v, TangRa = c.LoiRa - r } : c;
            }).OrderByDescending(c => (c.TangVao ?? 0) + (c.TangRa ?? 0)).ThenByDescending(c => c.LoiVao + c.LoiRa).ToList();
        }

        private static string? Cat(string? s, int toiDa) => s == null ? null : s.Length > toiDa ? s[..toiDa] : s;
    }
}
