using System.Text.Json;
using E_Form_Best.Context;
using E_Form_Best.Models.ITForm;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace E_Form_Best.Areas.ITForm.Services
{
    /// <summary>
    /// Job nền đọc sức khoẻ phần cứng Server và lưu trữ qua SNMP (ServerSnmp:ChuKyGiay, mặc định 600 giây):
    /// ghi đè ảnh chụp KK_ServerSucKhoe, đổi trạng thái thì ghi KK_ServerSucKhoeLichSu (append-only),
    /// và cập nhật dung lượng KK_Server khi thiết bị có báo (Synology volume / ổ của host).
    ///
    /// Community theo nhóm thiết bị, để trong .env (không hardcode):
    ///   ServerSnmp__Community__LuuTru  (NAS / SAN)       ServerSnmp__Community__MayChu (Server / Máy ảo)
    ///   ghi đè riêng công ty: ServerSnmp__BPVN__Community__LuuTru ...
    /// Công ty đọc theo Server:CongTyPing (giống job ping) — 10.0.63.x BPVN chỉ máy chủ 60.52 tới được.
    /// </summary>
    public class ServerSnmpWorker : BackgroundService
    {
        private static readonly JsonSerializerOptions JsonOpt = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

        private readonly IServiceScopeFactory _scopeFactory;
        private readonly IConfiguration _configuration;

        public ServerSnmpWorker(IServiceScopeFactory scopeFactory, IConfiguration configuration)
        {
            _scopeFactory = scopeFactory;
            _configuration = configuration;
        }

        public static int ChuKyGiay(IConfiguration cfg)
            => Math.Clamp(cfg.GetValue<int?>("ServerSnmp:ChuKyGiay") ?? 600, 120, 86400);

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            var bat = _configuration.GetValue<bool?>("ServerSnmp:Bat")
                      ?? _configuration.GetValue<bool?>("Server:GhiLichSu")
                      ?? _configuration.GetValue<bool?>("Switch:GhiLichSu") ?? true;
            if (!bat) return;

            try { await Task.Delay(TimeSpan.FromSeconds(60), stoppingToken); }
            catch (TaskCanceledException) { return; }

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    var soSuKien = await QuetAsync(stoppingToken);
                    if (soSuKien > 0)
                        Console.WriteLine($"[ServerSnmp] {soSuKien} thiết bị đổi trạng thái sức khoẻ lúc {DateTime.Now:dd/MM/yyyy HH:mm}");
                }
                catch (TaskCanceledException) { return; }
                catch (Exception ex)
                {
                    Console.WriteLine($"[ServerSnmp Error]: {ex.Message}");
                }

                try { await Task.Delay(TimeSpan.FromSeconds(ChuKyGiay(_configuration)), stoppingToken); }
                catch (TaskCanceledException) { return; }
            }
        }

        private async Task<int> QuetAsync(CancellationToken ct)
        {
            var dsCongTy = ServerPingWorker.DsCongTyTheoDoi(_configuration);
            if (dsCongTy.Count == 0) return 0;

            List<int> dsId;
            using (var scope = _scopeFactory.CreateScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<ITFormContext>();
                var ds = await db.KkServers
                    .Where(x => x.NgayXoa == null && (x.DiaChiIp != null || x.IpQuanTri != null)
                                && (x.TinhTrang == null || !ServerPingWorker.DsTinhTrangKhongTheoDoi.Contains(x.TinhTrang))
                                && x.IdcongTyNavigation != null && dsCongTy.Contains(x.IdcongTyNavigation.TenCongTy))
                    .Select(x => new { x.IdServer, CongTy = x.IdcongTyNavigation!.TenCongTy, x.Loai, x.DiaChiIp, x.IpQuanTri })
                    .ToListAsync(ct);
                // Bỏ thiết bị máy này không tới được (mọi IP đều thuộc dải bỏ qua) hoặc chưa cấu hình community:
                // ghi "Không đọc được" lúc đó là giả, để máy chủ tới được / đã cấu hình ghi
                dsId = ds.Where(x => !(ServerPingWorker.BoQuaIp(_configuration, x.DiaChiIp ?? x.IpQuanTri)
                                       && (x.IpQuanTri == null || ServerPingWorker.BoQuaIp(_configuration, x.IpQuanTri)))
                                     && !string.IsNullOrWhiteSpace(Community(_configuration, x.CongTy, x.Loai)))
                    .Select(x => x.IdServer).ToList();
            }

            var doi = 0;
            await Parallel.ForEachAsync(dsId, new ParallelOptions { MaxDegreeOfParallelism = 6, CancellationToken = ct }, async (id, token) =>
            {
                // Mỗi thiết bị 1 scope: DbContext không dùng chung giữa các luồng
                using var scope = _scopeFactory.CreateScope();
                var db = scope.ServiceProvider.GetRequiredService<ITFormContext>();
                try
                {
                    if (await DocVaLuuAsync(db, _configuration, id, token)) Interlocked.Increment(ref doi);
                }
                catch (OperationCanceledException) when (token.IsCancellationRequested) { throw; }
                catch (Exception ex) { Console.WriteLine($"[ServerSnmp] id {id}: {ex.Message}"); }
            });
            return doi;
        }

        /// <summary>Community cho thiết bị: ưu tiên cấu hình riêng công ty, rồi cấu hình chung.</summary>
        public static string? Community(IConfiguration cfg, string congTy, string loai)
        {
            var nhom = QLServerNhom(loai);
            var rieng = cfg[$"ServerSnmp:{congTy}:Community:{nhom}"];
            return string.IsNullOrWhiteSpace(rieng) ? cfg[$"ServerSnmp:Community:{nhom}"] : rieng;
        }

        private static string QLServerNhom(string loai) => loai is "NAS" or "SAN" ? "LuuTru" : "MayChu";

        /// <summary>
        /// Đọc SNMP 1 thiết bị và lưu ảnh chụp. Trả true nếu trạng thái sức khoẻ vừa đổi (có ghi nhật ký).
        /// Dùng cho cả job nền và nút "Kiểm tra ngay" trên trang.
        /// </summary>
        public static async Task<bool> DocVaLuuAsync(ITFormContext db, IConfiguration cfg, int idServer, CancellationToken ct)
        {
            var sv = await db.KkServers
                .Where(x => x.IdServer == idServer && x.NgayXoa == null)
                .Select(x => new { x.IdServer, x.IdcongTy, CongTy = x.IdcongTyNavigation!.TenCongTy, x.TenServer, x.Loai, x.DiaChiIp, x.IpQuanTri })
                .FirstOrDefaultAsync(ct);
            if (sv == null) return false;

            var com = Community(cfg, sv.CongTy, sv.Loai);
            ServerSnmpService.KetQua? kq = null;
            string? loi = null;

            if (string.IsNullOrWhiteSpace(com))
                loi = $"Chưa cấu hình community SNMP (ServerSnmp__Community__{QLServerNhom(sv.Loai)}).";
            else
            {
                // IP quản trị (iLO / iDRAC) có cảm biến phần cứng; IP chính (hệ điều hành) có dung lượng ổ
                var dsIp = new[] { sv.IpQuanTri, sv.DiaChiIp }.Where(x => !string.IsNullOrWhiteSpace(x)).Select(x => x!.Trim()).Distinct().ToList();
                foreach (var ip in dsIp)
                {
                    kq = await ServerSnmpService.DocAsync(ip, com, ct);
                    if (kq != null) break;
                }

                // Có số liệu phần cứng từ iLO/iDRAC thì thử bổ sung dung lượng ổ từ IP hệ điều hành
                if (kq != null && kq.Nguon is "HPE ProLiant" or "Dell iDRAC")
                {
                    var ipOs = dsIp.FirstOrDefault(x => x != kq.Ip);
                    if (ipOs != null)
                    {
                        try
                        {
                            var nguon = kq.Nguon;
                            await ServerSnmpService.HostAsync(kq, ipOs, com, ct, chiDungLuong: true);
                            kq.Nguon = nguon;
                            kq.Ip += ", " + ipOs;
                        }
                        catch (TimeoutException) { /* hệ điều hành không bật SNMP: bỏ qua phần dung lượng */ }
                    }
                }
                if (kq == null) loi = $"Không đọc được SNMP từ {string.Join(", ", dsIp)} (community sai, chưa bật SNMP hoặc không thông mạng).";
            }

            var bayGio = DateTime.Now;
            var cu = await db.KkServerSucKhoes.AsNoTracking().Where(x => x.IdServer == idServer)
                .Select(x => new { x.TrangThai, x.DocDuocLuc, x.Nguon, x.TenHeThong, x.MoTa, x.DuLieu, x.IpSnmp }).FirstOrDefaultAsync(ct);

            if (kq != null && cu?.DocDuocLuc != null) TinhTocDo(kq, cu.DuLieu, (bayGio - cu.DocDuocLuc.Value).TotalSeconds);

            var trangThai = kq == null ? ServerSnmpService.KhongDocDuoc : ServerSnmpService.TrangThaiTong(kq.DsMuc);
            var tomTat = kq == null ? loi
                : Cat(string.Join("; ", kq.DsMuc.Where(x => x.TrangThai is ServerSnmpService.Loi or ServerSnmpService.CanhBao)
                    .Select(x => $"{x.Nhom} {x.Ten}" + (string.IsNullOrWhiteSpace(x.GiaTri) ? "" : $": {x.GiaTri}"))), 1000);
            if (string.IsNullOrWhiteSpace(tomTat)) tomTat = null;
            // boDem lưu lại để lượt sau tính tốc độ; dsSuKien = nhật ký thiết bị (MSA)
            var duLieu = kq == null ? null : JsonSerializer.Serialize(new
            {
                kq.Nguon, kq.GhiChu, kq.ThoiGianChay, dsMuc = kq.DsMuc, dsSuKien = kq.DsSuKien, boDem = kq.DsBoDem
            }, JsonOpt);

            if (cu == null)
            {
                db.KkServerSucKhoes.Add(new KkServerSucKhoe
                {
                    IdServer = idServer, KiemTraLuc = bayGio, DocDuocLuc = kq != null ? bayGio : null, IpSnmp = Cat(kq?.Ip, 100),
                    Nguon = kq?.Nguon, TenHeThong = Cat(kq?.TenHeThong, 255), MoTa = Cat(kq?.MoTa, 500),
                    TrangThai = trangThai, TomTat = tomTat, DuLieu = duLieu, Loi = Cat(loi, 500)
                });
                try { await db.SaveChangesAsync(ct); }
                catch (DbUpdateException ex) when (ex.InnerException is SqlException sql && (sql.Number == 2601 || sql.Number == 2627))
                {
                    return false; // máy chủ kia vừa tạo dòng này trước
                }
            }
            else
            {
                // Lượt đọc lỗi giữ lại số liệu lần đọc được gần nhất để trang vẫn có cái xem, chỉ đổi trạng thái + lỗi
                var soDong = await db.KkServerSucKhoes
                    .Where(x => x.IdServer == idServer && x.TrangThai == cu.TrangThai)
                    .ExecuteUpdateAsync(s => s
                        .SetProperty(x => x.KiemTraLuc, bayGio)
                        .SetProperty(x => x.DocDuocLuc, kq != null ? (DateTime?)bayGio : cu.DocDuocLuc)
                        .SetProperty(x => x.IpSnmp, kq != null ? Cat(kq.Ip, 100) : cu.IpSnmp)
                        .SetProperty(x => x.Nguon, kq != null ? kq.Nguon : cu.Nguon)
                        .SetProperty(x => x.TenHeThong, kq != null ? Cat(kq.TenHeThong, 255) : cu.TenHeThong)
                        .SetProperty(x => x.MoTa, kq != null ? Cat(kq.MoTa, 500) : cu.MoTa)
                        .SetProperty(x => x.TrangThai, trangThai)
                        .SetProperty(x => x.TomTat, tomTat)
                        .SetProperty(x => x.DuLieu, kq != null ? duLieu : cu.DuLieu)
                        .SetProperty(x => x.Loi, Cat(loi, 500)), ct);

                if (soDong == 1 && cu.TrangThai != trangThai)
                {
                    var sk = new KkServerSucKhoeLichSu
                    {
                        IdServer = idServer, IdcongTy = sv.IdcongTy, ThoiGian = bayGio, TenServer = Cat(sv.TenServer, 255),
                        TuTrangThai = cu.TrangThai, SangTrangThai = trangThai, TomTat = tomTat, GhiNhanLuc = bayGio
                    };
                    db.KkServerSucKhoeLichSus.Add(sk);
                    try { await db.SaveChangesAsync(ct); }
                    catch (DbUpdateException ex) when (ex.InnerException is SqlException sql && (sql.Number == 2601 || sql.Number == 2627))
                    {
                        db.Entry(sk).State = EntityState.Detached;
                    }
                }
            }

            // Thiết bị báo được dung lượng thì cập nhật luôn cột dung lượng (không phải nhập tay nữa)
            if (kq?.DungLuongTb > 0)
                await db.KkServers.Where(x => x.IdServer == idServer)
                    .ExecuteUpdateAsync(s => s
                        .SetProperty(x => x.DungLuongTb, kq.DungLuongTb)
                        .SetProperty(x => x.DaDungTb, kq.DaDungTb), ct);

            return cu != null && cu.TrangThai != trangThai;
        }

        /// <summary>
        /// Tốc độ = (bộ đếm lần này - lần trước) / số giây giữa 2 lần đọc được. Bỏ qua khi khoảng cách quá ngắn
        /// (bấm "Kiểm tra ngay" liền nhau, số không ổn định) hoặc quá dài, và khi bộ đếm bị reset (thiết bị khởi động lại).
        /// </summary>
        private static void TinhTocDo(ServerSnmpService.KetQua kq, string? duLieuCu, double giay)
        {
            if (giay < 30 || giay > 3600 || string.IsNullOrWhiteSpace(duLieuCu) || kq.DsBoDem.Count == 0) return;

            Dictionary<string, (ulong vao, ulong ra)> truoc;
            try
            {
                using var doc = JsonDocument.Parse(duLieuCu);
                if (!doc.RootElement.TryGetProperty("boDem", out var mang) || mang.ValueKind != JsonValueKind.Array) return;
                truoc = mang.EnumerateArray()
                    .Where(x => x.TryGetProperty("khoa", out _))
                    .GroupBy(x => x.GetProperty("khoa").GetString() ?? "")
                    .ToDictionary(g => g.Key, g => (g.First().GetProperty("vao").GetUInt64(), g.First().GetProperty("ra").GetUInt64()));
            }
            catch (Exception ex) when (ex is JsonException or InvalidOperationException or FormatException) { return; }

            var tocDo = new Dictionary<string, (double vao, double ra, string loai)>();
            foreach (var b in kq.DsBoDem)
                if (truoc.TryGetValue(b.Khoa, out var c) && b.Vao >= c.vao && b.Ra >= c.ra)
                    tocDo[b.Khoa] = ((b.Vao - c.vao) / giay, (b.Ra - c.ra) / giay, b.Loai);

            for (var i = 0; i < kq.DsMuc.Count; i++)
            {
                var m = kq.DsMuc[i];
                if (m.Khoa == null || !tocDo.TryGetValue(m.Khoa, out var t)) continue;
                if (t.loai == "mang")
                    kq.DsMuc[i] = m with { GiaTri = $"{m.GiaTri} · ↓ {Bit(t.vao * 8)} ↑ {Bit(t.ra * 8)}" };
                else
                {
                    var ct = new Dictionary<string, string>(m.ChiTiet ?? new()) { ["Đọc"] = Byte(t.vao) + "/s", ["Ghi"] = Byte(t.ra) + "/s" };
                    kq.DsMuc[i] = m with { ChiTiet = ct };
                }
            }
        }

        private static string Bit(double bps) => bps >= 1e9 ? $"{bps / 1e9:0.##} Gbps" : bps >= 1e6 ? $"{bps / 1e6:0.#} Mbps" : $"{bps / 1e3:0} Kbps";
        private static string Byte(double b) => b >= 1048576 ? $"{b / 1048576:0.#} MB" : $"{b / 1024:0} KB";

        private static string? Cat(string? s, int toiDa) => s == null ? null : s.Length > toiDa ? s[..toiDa] : s;
    }
}
