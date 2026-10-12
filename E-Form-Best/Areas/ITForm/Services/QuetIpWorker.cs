using System.Collections.Concurrent;
using System.Net;
using System.Net.NetworkInformation;
using System.Text.RegularExpressions;
using E_Form_Best.Context;
using E_Form_Best.Models.ITForm;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace E_Form_Best.Areas.ITForm.Services
{
    /// <summary>
    /// Job nền quét dải IP máy chủ (tab "Máy chủ" trên /QLSwitch/{công ty}): mỗi chu kỳ (QuetIp:ChuKyGiay,
    /// mặc định 300 giây) ping .1–.254 của từng dải trong QuetIp:DaiMang (mặc định "PFVN=10.0.193").
    ///  - IP lạ vừa trả lời: thêm 1 dòng KK_QuetIp (kèm tên DNS ngược) rồi theo dõi từ đó.
    ///  - IP đã biết: đổi UP ⇄ DOWN thì ghi KK_QuetIpLichSu (append-only), cùng cách SwitchPingWorker.
    /// Dải không có danh mục nào để đối chiếu nên "từng trả lời ping" là định nghĩa máy cần theo dõi.
    ///
    /// Công ty máy chủ web không thông mạng thì bỏ khỏi Switch:CongTyPing (dùng chung với Switch),
    /// không thì cả dải bị báo mất kết nối oan. Tắt hẳn: QuetIp:Bat=false (mặc định theo Switch:GhiLichSu).
    /// Chạy song song 2 máy chủ: thêm IP chốt bằng unique (IDCongTy, dia_chi_ip), đổi trạng thái là UPDATE
    /// có điều kiện theo trạng thái cũ, unique sự kiện là chốt cuối.
    /// </summary>
    public class QuetIpWorker : BackgroundService
    {
        private const string DaiMacDinh = "PFVN=10.0.193";

        private readonly IServiceScopeFactory _scopeFactory;
        private readonly IConfiguration _configuration;

        public QuetIpWorker(IServiceScopeFactory scopeFactory, IConfiguration configuration)
        {
            _scopeFactory = scopeFactory;
            _configuration = configuration;
        }

        public static int ChuKyGiay(IConfiguration cfg) => Math.Clamp(cfg.GetValue<int?>("QuetIp:ChuKyGiay") ?? 300, 120, 3600);

        /// <summary>
        /// Dải quét theo công ty từ QuetIp:DaiMang = "PFVN=10.0.193;BPVN=10.0.60" (3 octet đầu).
        /// Mục sai cú pháp / công ty lạ bị bỏ qua; mỗi công ty 1 dải.
        /// </summary>
        public static Dictionary<string, string> DsDaiQuet(IConfiguration cfg)
        {
            var cauHinh = cfg["QuetIp:DaiMang"];
            if (string.IsNullOrWhiteSpace(cauHinh)) cauHinh = DaiMacDinh;

            var kq = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (var muc in cauHinh.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            {
                var p = muc.Split('=', 2, StringSplitOptions.TrimEntries);
                if (p.Length != 2) continue;
                var cty = SwitchPingWorker.DsCongTy.FirstOrDefault(c => c.Equals(p[0], StringComparison.OrdinalIgnoreCase));
                if (cty == null || !Regex.IsMatch(p[1], @"^(\d{1,3})\.(\d{1,3})\.(\d{1,3})$")) continue;
                if (p[1].Split('.').Any(x => int.Parse(x) > 255)) continue;
                kq.TryAdd(cty, p[1]);
            }
            return kq;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            var macDinhBat = _configuration.GetValue<bool?>("Switch:GhiLichSu") ?? true;
            if (!(_configuration.GetValue<bool?>("QuetIp:Bat") ?? macDinhBat)) return;

            var chuKy = ChuKyGiay(_configuration);

            // Lệch giờ khởi động với các job ping khác cho đỡ dồn ICMP cùng lúc
            try { await Task.Delay(TimeSpan.FromSeconds(60), stoppingToken); }
            catch (TaskCanceledException) { return; }

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    var (soMoi, soSuKien) = await QuetAsync(stoppingToken);
                    if (soMoi > 0 || soSuKien > 0)
                        Console.WriteLine($"[QuetIp] {soMoi} IP mới, {soSuKien} sự kiện đổi trạng thái lúc {DateTime.Now:dd/MM/yyyy HH:mm}");
                }
                catch (TaskCanceledException) { return; }
                catch (Exception ex)
                {
                    Console.WriteLine($"[QuetIp Error]: {ex.Message}");
                }

                try { await Task.Delay(TimeSpan.FromSeconds(chuKy), stoppingToken); }
                catch (TaskCanceledException) { return; }
            }
        }

        /// <summary>Công ty máy này thông mạng — dùng chung Switch:CongTyPing, trống = cả 3.</summary>
        private HashSet<string> DsCongTyPing()
        {
            var cauHinh = _configuration["Switch:CongTyPing"];
            if (string.IsNullOrWhiteSpace(cauHinh)) return SwitchPingWorker.DsCongTy.ToHashSet(StringComparer.OrdinalIgnoreCase);
            return cauHinh.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);
        }

        private async Task<(int soMoi, int soSuKien)> QuetAsync(CancellationToken ct)
        {
            var dsPing = DsCongTyPing();
            var dsDai = DsDaiQuet(_configuration).Where(x => dsPing.Contains(x.Key)).ToList();
            if (dsDai.Count == 0) return (0, 0);

            int soMoi = 0, soSuKien = 0;
            foreach (var (cty, dai) in dsDai)
            {
                var (moi, suKien) = await QuetDaiAsync(cty, dai, ct);
                soMoi += moi;
                soSuKien += suKien;
            }
            return (soMoi, soSuKien);
        }

        private async Task<(int soMoi, int soSuKien)> QuetDaiAsync(string cty, string dai, CancellationToken ct)
        {
            using var scope = _scopeFactory.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<ITFormContext>();

            var idCongTy = await db.KkCongTies.Where(x => x.TenCongTy == cty).Select(x => (int?)x.IdcongTy).FirstOrDefaultAsync(ct);
            if (idCongTy == null) return (0, 0);

            var daBiet = await db.KkQuetIps
                .Where(x => x.IdcongTy == idCongTy && x.DaiMang == dai)
                .Select(x => new { x.IdQuetIp, x.DiaChiIp, x.TenMay, x.TrangThaiKetNoi, x.DoiTrangThaiLuc })
                .ToListAsync(ct);
            var theoIp = daBiet.ToDictionary(x => x.DiaChiIp);

            // IP đã theo dõi ping kỹ 3 lần (tránh DOWN giả); IP lạ chỉ dò 1 lần cho nhanh, lượt sau dò lại
            var song = new ConcurrentDictionary<string, bool>();
            await Parallel.ForEachAsync(
                Enumerable.Range(1, 254).Select(i => dai + "." + i),
                new ParallelOptions { MaxDegreeOfParallelism = 32, CancellationToken = ct },
                async (ip, token) => { song[ip] = await PingAsync(ip, theoIp.ContainsKey(ip) ? 3 : 1, token); });

            var bayGio = DateTime.Now;

            // ---- IP lạ vừa trả lời: thêm mới ----
            var soMoi = 0;
            foreach (var ip in song.Where(x => x.Value && !theoIp.ContainsKey(x.Key)).Select(x => x.Key).OrderBy(SoCuoi))
            {
                var dong = new KkQuetIp
                {
                    IdcongTy = idCongTy.Value,
                    DaiMang = dai,
                    DiaChiIp = ip,
                    TenMay = Cat(await TenDnsAsync(ip, ct), 255),
                    TrangThaiKetNoi = "UP",
                    DoiTrangThaiLuc = bayGio,
                    KiemTraLuc = bayGio,
                    LanDauThay = bayGio,
                    LanCuoiSong = bayGio
                };
                db.KkQuetIps.Add(dong);
                try
                {
                    await db.SaveChangesAsync(ct);
                    soMoi++;
                }
                catch (DbUpdateException ex) when (ex.InnerException is SqlException sql && (sql.Number == 2601 || sql.Number == 2627))
                {
                    // Máy chủ kia vừa thêm đúng IP này
                    db.Entry(dong).State = EntityState.Detached;
                }
            }

            if (daBiet.Count == 0) return (soMoi, 0);

            var dsId = daBiet.Select(x => x.IdQuetIp).ToList();
            await db.KkQuetIps.Where(x => dsId.Contains(x.IdQuetIp))
                .ExecuteUpdateAsync(s => s.SetProperty(x => x.KiemTraLuc, bayGio), ct);
            var dsIdSong = daBiet.Where(x => song.TryGetValue(x.DiaChiIp, out var len) && len).Select(x => x.IdQuetIp).ToList();
            if (dsIdSong.Count > 0)
                await db.KkQuetIps.Where(x => dsIdSong.Contains(x.IdQuetIp))
                    .ExecuteUpdateAsync(s => s.SetProperty(x => x.LanCuoiSong, bayGio), ct);

            // ---- IP đã biết: đổi trạng thái + bù tên DNS còn thiếu ----
            var soSuKien = 0;
            foreach (var may in daBiet)
            {
                var moi = song.TryGetValue(may.DiaChiIp, out var len) && len ? "UP" : "DOWN";

                var tenMay = may.TenMay;
                if (tenMay == null && moi == "UP")
                {
                    tenMay = Cat(await TenDnsAsync(may.DiaChiIp, ct), 255);
                    if (tenMay != null)
                        await db.KkQuetIps.Where(x => x.IdQuetIp == may.IdQuetIp && x.TenMay == null)
                            .ExecuteUpdateAsync(s => s.SetProperty(x => x.TenMay, tenMay), ct);
                }

                var cu = may.TrangThaiKetNoi;
                if (cu == moi) continue;

                var soDong = await db.KkQuetIps
                    .Where(x => x.IdQuetIp == may.IdQuetIp && x.TrangThaiKetNoi == cu)
                    .ExecuteUpdateAsync(s => s
                        .SetProperty(x => x.TrangThaiKetNoi, moi)
                        .SetProperty(x => x.DoiTrangThaiLuc, bayGio), ct);
                if (soDong == 0 || cu == null) continue;

                int? thoiLuong = null;
                if (cu == "DOWN" && may.DoiTrangThaiLuc.HasValue && bayGio > may.DoiTrangThaiLuc.Value)
                    thoiLuong = (int)Math.Min(int.MaxValue, (bayGio - may.DoiTrangThaiLuc.Value).TotalSeconds);

                var sk = new KkQuetIpLichSu
                {
                    IdQuetIp = may.IdQuetIp,
                    IdcongTy = idCongTy.Value,
                    ThoiGian = bayGio,
                    DiaChiIp = may.DiaChiIp,
                    TenMay = tenMay,
                    TuTrangThai = cu,
                    SangTrangThai = moi,
                    ThoiLuongGiay = thoiLuong,
                    GhiNhanLuc = bayGio
                };
                db.KkQuetIpLichSus.Add(sk);
                try
                {
                    await db.SaveChangesAsync(ct);
                    soSuKien++;
                }
                catch (DbUpdateException ex) when (ex.InnerException is SqlException sql && (sql.Number == 2601 || sql.Number == 2627))
                {
                    db.Entry(sk).State = EntityState.Detached;
                }
            }
            return (soMoi, soSuKien);
        }

        private static int SoCuoi(string ip) => int.TryParse(ip[(ip.LastIndexOf('.') + 1)..], out var n) ? n : 0;

        private static async Task<bool> PingAsync(string ip, int soLan, CancellationToken ct)
        {
            for (var lan = 0; lan < soLan; lan++)
            {
                try
                {
                    using var ping = new Ping();
                    var traLoi = await ping.SendPingAsync(ip, TimeSpan.FromMilliseconds(lan == 0 ? 1000 : 1500), cancellationToken: ct);
                    if (traLoi.Status == IPStatus.Success) return true;
                }
                catch (OperationCanceledException) { throw; }
                catch (PingException) { }
            }
            return false;
        }

        /// <summary>Tên DNS ngược (PTR); DNS chậm / không có bản ghi thì trả null, không chặn cả lượt quét.</summary>
        private static async Task<string?> TenDnsAsync(string ip, CancellationToken ct)
        {
            try
            {
                var entry = await Dns.GetHostEntryAsync(ip, ct).WaitAsync(TimeSpan.FromSeconds(2), ct);
                // Không có PTR thì Windows trả lại chính IP — coi như chưa có tên
                return string.IsNullOrWhiteSpace(entry.HostName) || entry.HostName == ip ? null : entry.HostName;
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested) { throw; }
            catch { return null; }
        }

        private static string? Cat(string? s, int toiDa)
            => s == null ? null : (s.Length > toiDa ? s[..toiDa] : s);
    }
}
