using System.Collections.Concurrent;
using System.Net.NetworkInformation;
using E_Form_Best.Context;
using E_Form_Best.Models.ITForm;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace E_Form_Best.Areas.ITForm.Services
{
    /// <summary>
    /// Job nền theo dõi Server và lưu trữ 3 công ty (/QLServer): mỗi chu kỳ (Server:ChuKyGiay, mặc định 120 giây)
    /// ping toàn bộ thiết bị đang dùng có khai IP, ghi trạng thái gần nhất vào KK_Server và khi đổi
    /// UP ⇄ DOWN thì ghi một dòng KK_ServerLichSu (append-only).
    ///
    /// Cùng khuôn SwitchPingWorker. Server:CongTyPing / Server:GhiLichSu bỏ trống thì theo cấu hình Switch
    /// của máy đó — cùng mạng nội bộ nên máy nào ping được switch công ty nào thì cũng ping được server công ty đó.
    ///
    /// Idempotent / chạy song song 2 máy chủ: đổi trạng thái là UPDATE có điều kiện theo trạng thái cũ,
    /// chỉ máy nào đổi được dòng đó mới ghi sự kiện; unique (id_server, thoi_gian, sang_trang_thai) là chốt cuối.
    /// </summary>
    public class ServerPingWorker : BackgroundService
    {
        public static readonly string[] DsCongTy = SwitchPingWorker.DsCongTy;

        /// <summary>Thiết bị chưa lắp / đã ngưng thì không ping, không tính vào online/offline.</summary>
        public static readonly string[] DsTinhTrangKhongTheoDoi = { "Ngừng sử dụng", "Trong kho" };

        private readonly IServiceScopeFactory _scopeFactory;
        private readonly IConfiguration _configuration;

        public ServerPingWorker(IServiceScopeFactory scopeFactory, IConfiguration configuration)
        {
            _scopeFactory = scopeFactory;
            _configuration = configuration;
        }

        public static int ChuKyGiay(IConfiguration cfg)
            => Math.Clamp(cfg.GetValue<int?>("Server:ChuKyGiay") ?? 120, 60, 3600);

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            var bat = _configuration.GetValue<bool?>("Server:GhiLichSu") ?? _configuration.GetValue<bool?>("Switch:GhiLichSu") ?? true;
            if (!bat) return;

            var chuKyGiay = ChuKyGiay(_configuration);

            try { await Task.Delay(TimeSpan.FromSeconds(45), stoppingToken); }
            catch (TaskCanceledException) { return; }

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    var soSuKien = await QuetAsync(stoppingToken);
                    if (soSuKien > 0)
                        Console.WriteLine($"[Server] Ghi {soSuKien} sự kiện đổi trạng thái lúc {DateTime.Now:dd/MM/yyyy HH:mm}");
                }
                catch (TaskCanceledException) { return; }
                catch (Exception ex)
                {
                    // Mất kết nối DB / bảng chưa tạo: bỏ lượt này, lượt sau chạy tiếp
                    Console.WriteLine($"[Server Error]: {ex.Message}");
                }

                try { await Task.Delay(TimeSpan.FromSeconds(chuKyGiay), stoppingToken); }
                catch (TaskCanceledException) { return; }
            }
        }

        /// <summary>Công ty máy này được ping: Server:CongTyPing, trống thì theo Switch:CongTyPing, trống nữa thì cả 3.</summary>
        private List<string> DsCongTyPing() => DsCongTyTheoDoi(_configuration);

        /// <summary>
        /// Dải IP máy này không tới được (Server:BoQuaDaiIp = "10.0.63.,10.0.5.") thì bỏ qua, không ping / đọc SNMP —
        /// để máy chủ tới được ghi trạng thái, tránh ghi DOWN giả vào lịch sử (append-only, không xoá được).
        /// Cùng 1 công ty nhưng khác dải (BPVN 10.0.60 vs 10.0.63) nên không dùng được Server:CongTyPing.
        /// </summary>
        public static bool BoQuaIp(IConfiguration cfg, string? ip)
        {
            var cauHinh = cfg["Server:BoQuaDaiIp"];
            if (string.IsNullOrWhiteSpace(cauHinh) || string.IsNullOrWhiteSpace(ip)) return false;
            return cauHinh.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Any(dai => ip.Trim().StartsWith(dai, StringComparison.Ordinal));
        }

        /// <summary>Dùng chung cho ServerSnmpWorker: máy nào ping được công ty nào thì đọc SNMP công ty đó.</summary>
        public static List<string> DsCongTyTheoDoi(IConfiguration cfg)
        {
            var cauHinh = cfg["Server:CongTyPing"];
            if (string.IsNullOrWhiteSpace(cauHinh)) cauHinh = cfg["Switch:CongTyPing"];
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

            var ds = await db.KkServers
                .Where(x => x.NgayXoa == null && x.DiaChiIp != null
                            && (x.TinhTrang == null || !DsTinhTrangKhongTheoDoi.Contains(x.TinhTrang))
                            && x.IdcongTyNavigation != null && dsCongTy.Contains(x.IdcongTyNavigation.TenCongTy))
                .Select(x => new { x.IdServer, x.IdcongTy, x.TenServer, x.DiaChiIp, x.TrangThaiKetNoi, x.DoiTrangThaiLuc })
                .ToListAsync(ct);
            ds = ds.Where(x => !BoQuaIp(_configuration, x.DiaChiIp)).ToList();
            if (ds.Count == 0) return 0;

            var song = new ConcurrentDictionary<string, bool>();
            await Parallel.ForEachAsync(
                ds.Select(x => x.DiaChiIp!).Distinct(),
                new ParallelOptions { MaxDegreeOfParallelism = 32, CancellationToken = ct },
                async (ip, token) => { song[ip] = await PingAsync(ip, token); });

            var bayGio = DateTime.Now;
            var dsId = ds.Select(x => x.IdServer).ToList();
            await db.KkServers.Where(x => dsId.Contains(x.IdServer))
                .ExecuteUpdateAsync(s => s.SetProperty(x => x.KiemTraLuc, bayGio), ct);

            var daGhi = 0;
            foreach (var sv in ds)
            {
                var moi = song.TryGetValue(sv.DiaChiIp!, out var len) && len ? "UP" : "DOWN";
                var cu = sv.TrangThaiKetNoi;
                if (cu == moi) continue;

                // Chỉ đổi khi trạng thái + IP vẫn y như lúc đọc (máy chủ kia đổi trước / người dùng vừa sửa IP thì 0 dòng)
                var soDong = await db.KkServers
                    .Where(x => x.IdServer == sv.IdServer && x.TrangThaiKetNoi == cu && x.DiaChiIp == sv.DiaChiIp && x.NgayXoa == null)
                    .ExecuteUpdateAsync(s => s
                        .SetProperty(x => x.TrangThaiKetNoi, moi)
                        .SetProperty(x => x.DoiTrangThaiLuc, bayGio), ct);

                // Lần đầu kiểm: chỉ ghi trạng thái nền, chưa có "đổi" nào để báo
                if (soDong == 0 || cu == null) continue;

                int? thoiLuong = null;
                if (cu == "DOWN" && sv.DoiTrangThaiLuc.HasValue && bayGio > sv.DoiTrangThaiLuc.Value)
                    thoiLuong = (int)Math.Min(int.MaxValue, (bayGio - sv.DoiTrangThaiLuc.Value).TotalSeconds);

                var sk = new KkServerLichSu
                {
                    IdServer = sv.IdServer,
                    IdcongTy = sv.IdcongTy,
                    ThoiGian = bayGio,
                    TenServer = sv.TenServer.Length > 255 ? sv.TenServer[..255] : sv.TenServer,
                    DiaChiIp = sv.DiaChiIp,
                    TuTrangThai = cu,
                    SangTrangThai = moi,
                    ThoiLuongGiay = thoiLuong,
                    GhiNhanLuc = bayGio
                };
                db.KkServerLichSus.Add(sk);
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

        /// <summary>Chỉ coi là mất kết nối khi cả 3 lần ping đều hụt (tránh sự kiện DOWN/UP giả do rớt gói).</summary>
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
    }
}
