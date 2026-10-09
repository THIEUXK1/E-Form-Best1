using System.Collections.Concurrent;
using System.Net.NetworkInformation;
using E_Form_Best.Context;
using E_Form_Best.Models.ITForm;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace E_Form_Best.Areas.ITForm.Services
{
    /// <summary>
    /// Job nền theo dõi Switch 3 công ty (/QLSwitch): mỗi chu kỳ (Switch:ChuKyGiay, mặc định 120 giây)
    /// ping toàn bộ switch đang dùng có khai IP, ghi trạng thái gần nhất vào KK_Switch và khi đổi
    /// UP ⇄ DOWN thì ghi một dòng KK_SwitchLichSu (append-only) cho tab Lịch sử + báo cáo theo kỳ.
    ///
    /// Cùng khuôn AccessPointPingWorker nhưng chỉ ping (không có controller). Công ty nào máy chủ không thông
    /// mạng thì bỏ khỏi Switch:CongTyPing của máy đó (.env), không thì switch bị báo mất kết nối oan.
    ///
    /// Idempotent / chạy song song 2 máy chủ: đổi trạng thái là UPDATE có điều kiện theo trạng thái cũ,
    /// chỉ máy nào đổi được dòng đó mới ghi sự kiện; unique (id_switch, thoi_gian, sang_trang_thai) là chốt cuối.
    /// </summary>
    public class SwitchPingWorker : BackgroundService
    {
        public static readonly string[] DsCongTy = { "BPVN", "PFVN", "MEGA" };

        /// <summary>switch chưa lắp / đã ngưng thì không ping, không tính vào % hoạt động.</summary>
        public static readonly string[] DsTinhTrangKhongTheoDoi = { "Ngừng sử dụng", "Trong kho" };

        private readonly IServiceScopeFactory _scopeFactory;
        private readonly IConfiguration _configuration;

        public SwitchPingWorker(IServiceScopeFactory scopeFactory, IConfiguration configuration)
        {
            _scopeFactory = scopeFactory;
            _configuration = configuration;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            if (!(_configuration.GetValue<bool?>("Switch:GhiLichSu") ?? true)) return;

            var chuKyGiay = Math.Clamp(_configuration.GetValue<int?>("Switch:ChuKyGiay") ?? 120, 60, 3600);

            try { await Task.Delay(TimeSpan.FromSeconds(30), stoppingToken); }
            catch (TaskCanceledException) { return; }

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    var soSuKien = await QuetAsync(stoppingToken);
                    if (soSuKien > 0)
                        Console.WriteLine($"[Switch] Ghi {soSuKien} sự kiện đổi trạng thái lúc {DateTime.Now:dd/MM/yyyy HH:mm}");
                }
                catch (TaskCanceledException) { return; }
                catch (Exception ex)
                {
                    // Mất kết nối DB / lỗi tạm thời: bỏ lượt này, lượt sau chạy tiếp
                    Console.WriteLine($"[Switch Error]: {ex.Message}");
                }

                try { await Task.Delay(TimeSpan.FromSeconds(chuKyGiay), stoppingToken); }
                catch (TaskCanceledException) { return; }
            }
        }

        /// <summary>Công ty máy này được ping, mặc định cả 3 (Switch:CongTyPing = "BPVN,PFVN,MEGA").</summary>
        private List<string> DsCongTyPing()
        {
            var cauHinh = _configuration["Switch:CongTyPing"];
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

            var ds = await db.KkSwitches
                .Where(x => x.NgayXoa == null && x.DiaChiIp != null
                            && (x.TinhTrang == null || !DsTinhTrangKhongTheoDoi.Contains(x.TinhTrang))
                            && x.IdcongTyNavigation != null && dsCongTy.Contains(x.IdcongTyNavigation.TenCongTy))
                .Select(x => new { x.IdSwitch, x.IdcongTy, x.TenSwitch, x.DiaChiIp, x.TrangThaiKetNoi, x.DoiTrangThaiLuc })
                .ToListAsync(ct);
            if (ds.Count == 0) return 0;

            var song = new ConcurrentDictionary<string, bool>();
            await Parallel.ForEachAsync(
                ds.Select(x => x.DiaChiIp!).Distinct(),
                new ParallelOptions { MaxDegreeOfParallelism = 32, CancellationToken = ct },
                async (ip, token) => { song[ip] = await PingAsync(ip, token); });

            var bayGio = DateTime.Now;
            var dsId = ds.Select(x => x.IdSwitch).ToList();
            await db.KkSwitches.Where(x => dsId.Contains(x.IdSwitch))
                .ExecuteUpdateAsync(s => s.SetProperty(x => x.KiemTraLuc, bayGio), ct);

            var daGhi = 0;
            foreach (var sw in ds)
            {
                var moi = song.TryGetValue(sw.DiaChiIp!, out var len) && len ? "UP" : "DOWN";
                var cu = sw.TrangThaiKetNoi;
                if (cu == moi) continue;

                // Chỉ đổi khi trạng thái + IP vẫn y như lúc đọc: máy chủ kia đổi trước, hoặc người dùng vừa sửa IP
                // (Lưu đã đặt lại trạng thái) thì 0 dòng -> không ghi sự kiện trùng / sai
                var soDong = await db.KkSwitches
                    .Where(x => x.IdSwitch == sw.IdSwitch && x.TrangThaiKetNoi == cu && x.DiaChiIp == sw.DiaChiIp && x.NgayXoa == null)
                    .ExecuteUpdateAsync(s => s
                        .SetProperty(x => x.TrangThaiKetNoi, moi)
                        .SetProperty(x => x.DoiTrangThaiLuc, bayGio), ct);

                // Lần đầu kiểm switch này: chỉ ghi trạng thái nền, chưa có "đổi" nào để báo
                if (soDong == 0 || cu == null) continue;

                int? thoiLuong = null;
                // UP lại: đã mất kết nối bao lâu tính từ lúc chuyển sang DOWN
                if (cu == "DOWN" && sw.DoiTrangThaiLuc.HasValue && bayGio > sw.DoiTrangThaiLuc.Value)
                    thoiLuong = (int)Math.Min(int.MaxValue, (bayGio - sw.DoiTrangThaiLuc.Value).TotalSeconds);

                var sk = new KkSwitchLichSu
                {
                    IdSwitch = sw.IdSwitch,
                    IdcongTy = sw.IdcongTy,
                    ThoiGian = bayGio,
                    TenSwitch = Cat(sw.TenSwitch, 255),
                    DiaChiIp = sw.DiaChiIp,
                    TuTrangThai = cu,
                    SangTrangThai = moi,
                    ThoiLuongGiay = thoiLuong,
                    GhiNhanLuc = bayGio
                };
                db.KkSwitchLichSus.Add(sk);
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
