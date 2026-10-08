using System.Collections.Concurrent;
using System.Net.NetworkInformation;
using E_Form_Best.Context;
using E_Form_Best.Models.ITForm;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace E_Form_Best.Areas.ITForm.Services
{
    /// <summary>
    /// Job nền theo dõi AP Wi-Fi 3 công ty (/QLAP): mỗi chu kỳ (AccessPoint:ChuKyGiay, mặc định 120 giây)
    /// ping toàn bộ AP đang dùng có khai IP, ghi trạng thái gần nhất vào KK_AccessPoint và khi đổi
    /// UP ⇄ DOWN thì ghi một dòng KK_AccessPointLichSu (append-only) cho tab Lịch sử + báo cáo theo kỳ.
    ///
    /// Công ty có cấu hình AP controller (AccessPointController__{CÔNG TY}__Url) thì đồng bộ danh sách AP và lấy
    /// trạng thái từ controller; AP không có trên controller / công ty không có controller thì máy chủ tự ping.
    /// Công ty nào máy chủ không thông mạng thì bỏ khỏi AccessPoint:CongTyPing của máy đó (.env), không thì AP
    /// bị báo mất kết nối oan.
    ///
    /// Idempotent / chạy song song 2 máy chủ: đổi trạng thái là UPDATE có điều kiện theo trạng thái cũ,
    /// chỉ máy nào đổi được dòng đó mới ghi sự kiện; unique (id_ap, thoi_gian, sang_trang_thai) là chốt cuối.
    /// </summary>
    public class AccessPointPingWorker : BackgroundService
    {
        public static readonly string[] DsCongTy = { "BPVN", "PFVN", "MEGA" };

        /// <summary>AP chưa lắp / đã ngưng thì không ping, không tính vào % hoạt động.</summary>
        public static readonly string[] DsTinhTrangKhongTheoDoi = { "Ngừng sử dụng", "Trong kho" };

        private readonly IServiceScopeFactory _scopeFactory;
        private readonly IConfiguration _configuration;
        private readonly AccessPointControllerService _controller;

        public AccessPointPingWorker(IServiceScopeFactory scopeFactory, IConfiguration configuration, AccessPointControllerService controller)
        {
            _scopeFactory = scopeFactory;
            _configuration = configuration;
            _controller = controller;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            if (!(_configuration.GetValue<bool?>("AccessPoint:GhiLichSu") ?? true)) return;

            var chuKyGiay = Math.Clamp(_configuration.GetValue<int?>("AccessPoint:ChuKyGiay") ?? 120, 60, 3600);

            try { await Task.Delay(TimeSpan.FromSeconds(30), stoppingToken); }
            catch (TaskCanceledException) { return; }

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    var soSuKien = await QuetAsync(stoppingToken);
                    if (soSuKien > 0)
                        Console.WriteLine($"[AccessPoint] Ghi {soSuKien} sự kiện đổi trạng thái lúc {DateTime.Now:dd/MM/yyyy HH:mm}");
                }
                catch (TaskCanceledException) { return; }
                catch (Exception ex)
                {
                    // Mất kết nối DB / lỗi tạm thời: bỏ lượt này, lượt sau chạy tiếp
                    Console.WriteLine($"[AccessPoint Error]: {ex.Message}");
                }

                try { await Task.Delay(TimeSpan.FromSeconds(chuKyGiay), stoppingToken); }
                catch (TaskCanceledException) { return; }
            }
        }

        /// <summary>Công ty máy này được ping, mặc định cả 3 (AccessPoint:CongTyPing = "BPVN,PFVN,MEGA").</summary>
        private List<string> DsCongTyPing()
        {
            var cauHinh = _configuration["AccessPoint:CongTyPing"];
            if (string.IsNullOrWhiteSpace(cauHinh)) return DsCongTy.ToList();
            return cauHinh.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Select(x => DsCongTy.FirstOrDefault(c => c.Equals(x, StringComparison.OrdinalIgnoreCase)))
                .Where(x => x != null).Select(x => x!).Distinct().ToList();
        }

        private async Task<int> QuetAsync(CancellationToken ct)
        {
            var dsCongTy = DsCongTyPing();
            if (dsCongTy.Count == 0) return 0;

            using var scope = _scopeFactory.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<ITFormContext>();

            // Công ty có AP controller: đồng bộ danh sách + lấy trạng thái từ controller (chính xác hơn ping,
            // AP chặn ICMP vẫn đúng). Đọc controller lỗi thì lượt này lùi về ping như công ty không có controller.
            var trangThaiAc = new Dictionary<string, bool>(StringComparer.OrdinalIgnoreCase);
            foreach (var cty in dsCongTy.Where(_controller.CoController))
            {
                try
                {
                    var dsAc = await _controller.DanhSachAsync(cty, ct);
                    var kq = await _controller.DongBoAsync(db, cty, dsAc, ct);
                    if (kq.ThemMoi > 0)
                        Console.WriteLine($"[AccessPoint] {cty}: thêm {kq.ThemMoi} AP mới từ controller");
                    foreach (var ap in dsAc) trangThaiAc[ap.Mac] = ap.Online;
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    db.ChangeTracker.Clear();
                    Console.WriteLine($"[AccessPoint Error] Đọc controller {cty}: {ex.Message}");
                }
            }

            var ds = await db.KkAccessPoints
                .Where(x => x.NgayXoa == null && x.DiaChiIp != null
                            && (x.TinhTrang == null || !DsTinhTrangKhongTheoDoi.Contains(x.TinhTrang))
                            && x.IdcongTyNavigation != null && dsCongTy.Contains(x.IdcongTyNavigation.TenCongTy))
                .Select(x => new { x.IdAp, x.IdcongTy, x.TenAp, x.DiaChiIp, x.DiaChiMac, x.TrangThaiKetNoi, x.DoiTrangThaiLuc })
                .ToListAsync(ct);
            if (ds.Count == 0) return 0;

            bool TuController(string? mac) => mac != null && trangThaiAc.ContainsKey(mac);

            var song = new ConcurrentDictionary<string, bool>();
            await Parallel.ForEachAsync(
                ds.Where(x => !TuController(x.DiaChiMac)).Select(x => x.DiaChiIp!).Distinct(),
                new ParallelOptions { MaxDegreeOfParallelism = 32, CancellationToken = ct },
                async (ip, token) => { song[ip] = await PingAsync(ip, token); });

            var bayGio = DateTime.Now;
            var dsIdAc = ds.Where(x => TuController(x.DiaChiMac)).Select(x => x.IdAp).ToList();
            var dsIdPing = ds.Where(x => !TuController(x.DiaChiMac)).Select(x => x.IdAp).ToList();
            if (dsIdAc.Count > 0)
                await db.KkAccessPoints.Where(x => dsIdAc.Contains(x.IdAp))
                    .ExecuteUpdateAsync(s => s.SetProperty(x => x.KiemTraLuc, bayGio).SetProperty(x => x.NguonTrangThai, "AC"), ct);
            if (dsIdPing.Count > 0)
                await db.KkAccessPoints.Where(x => dsIdPing.Contains(x.IdAp))
                    .ExecuteUpdateAsync(s => s.SetProperty(x => x.KiemTraLuc, bayGio).SetProperty(x => x.NguonTrangThai, "PING"), ct);

            var daGhi = 0;
            foreach (var ap in ds)
            {
                var len = TuController(ap.DiaChiMac)
                    ? trangThaiAc[ap.DiaChiMac!]
                    : song.TryGetValue(ap.DiaChiIp!, out var traLoi) && traLoi;
                var moi = len ? "UP" : "DOWN";
                var cu = ap.TrangThaiKetNoi;
                if (cu == moi) continue;

                // Chỉ đổi khi trạng thái + IP vẫn y như lúc đọc: máy chủ kia đổi trước, hoặc người dùng vừa sửa IP
                // (Lưu đã đặt lại trạng thái) thì 0 dòng -> không ghi sự kiện trùng / sai
                var soDong = await db.KkAccessPoints
                    .Where(x => x.IdAp == ap.IdAp && x.TrangThaiKetNoi == cu && x.DiaChiIp == ap.DiaChiIp && x.NgayXoa == null)
                    .ExecuteUpdateAsync(s => s
                        .SetProperty(x => x.TrangThaiKetNoi, moi)
                        .SetProperty(x => x.DoiTrangThaiLuc, bayGio), ct);

                // Lần đầu kiểm AP này: chỉ ghi trạng thái nền, chưa có "đổi" nào để báo
                if (soDong == 0 || cu == null) continue;

                int? thoiLuong = null;
                // UP lại: đã mất kết nối bao lâu tính từ lúc chuyển sang DOWN
                if (cu == "DOWN" && ap.DoiTrangThaiLuc.HasValue && bayGio > ap.DoiTrangThaiLuc.Value)
                    thoiLuong = (int)Math.Min(int.MaxValue, (bayGio - ap.DoiTrangThaiLuc.Value).TotalSeconds);

                var sk = new KkAccessPointLichSu
                {
                    IdAp = ap.IdAp,
                    IdcongTy = ap.IdcongTy,
                    ThoiGian = bayGio,
                    TenAp = Cat(ap.TenAp, 255),
                    DiaChiIp = ap.DiaChiIp,
                    TuTrangThai = cu,
                    SangTrangThai = moi,
                    ThoiLuongGiay = thoiLuong,
                    GhiNhanLuc = bayGio
                };
                db.KkAccessPointLichSus.Add(sk);
                try
                {
                    await db.SaveChangesAsync(ct);
                    daGhi++;
                }
                catch (DbUpdateException ex) when (ex.InnerException is SqlException sql && (sql.Number == 2601 || sql.Number == 2627))
                {
                    db.Entry(sk).State = EntityState.Detached;
                }
            }
            return daGhi;
        }

        /// <summary>
        /// Rớt 1 gói ICMP trên Wi-Fi/đường truyền chi nhánh là chuyện thường: chỉ coi là mất kết nối khi
        /// cả 3 lần ping đều hụt, tránh lịch sử đầy sự kiện DOWN/UP giả.
        /// </summary>
        private static async Task<bool> PingAsync(string ip, CancellationToken ct)
        {
            for (var lan = 0; lan < 3; lan++)
            {
                try
                {
                    using var ping = new Ping();
                    var traLoi = await ping.SendPingAsync(ip, TimeSpan.FromMilliseconds(lan == 0 ? 1000 : 1500), cancellationToken: ct);
                    if (traLoi.Status == IPStatus.Success) return true;
                }
                catch (OperationCanceledException) { throw; }
                catch (PingException)
                {
                    // IP không hợp lệ / mạng chặn ICMP: coi như lần này không tới được
                }
            }
            return false;
        }

        private static string? Cat(string? s, int toiDa)
            => s == null ? null : (s.Length > toiDa ? s[..toiDa] : s);
    }
}
