using System.Globalization;
using System.Text.Json;

namespace E_Form_Best.Areas.ITForm.Services
{
    /// <summary>
    /// Job nền chụp "ảnh lưu sẵn" cho mọi camera BPVN vào thư mục CameraNvr:ThuMucAnhLuu
    /// (file "{nvrIp}_{kênh}.jpg", ghi đè), để /QLCamera xem được ảnh ngay cả khi máy chủ đang xem
    /// không gọi thẳng được đầu ghi (xem CameraXemTrucTiepService.LayAnhLuuAsync).
    ///
    /// Chỉ bật ở máy GỌI ĐƯỢC đầu ghi: CameraNvr:ChupAnhLuu=true (mặc định tắt).
    /// Giờ chụp: CameraNvr:GioChupAnhLuu, nhiều mốc cách nhau dấu phẩy (mặc định "08:30").
    /// Chụp bù: khởi động mà lượt chụp gần nhất cũ hơn mốc giờ đã qua gần nhất (hoặc chưa từng chụp)
    /// thì chụp ngay — app tắt đúng giờ chụp cũng không mất lượt.
    /// Mốc lượt chụp cuối lưu ở file "_lan_chup_cuoi.txt" trong cùng thư mục, nên nhiều máy cùng
    /// bật job sẽ thấy nhau và không chụp trùng lượt.
    /// </summary>
    public class CameraAnhLuuWorker : BackgroundService
    {
        private const string TenFileMoc = "_lan_chup_cuoi.txt";

        private readonly IServiceScopeFactory _scopeFactory;
        private readonly IConfiguration _configuration;

        public CameraAnhLuuWorker(IServiceScopeFactory scopeFactory, IConfiguration configuration)
        {
            _scopeFactory = scopeFactory;
            _configuration = configuration;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            if (!(_configuration.GetValue<bool?>("CameraNvr:ChupAnhLuu") ?? false)) return;

            var thuMuc = _configuration["CameraNvr:ThuMucAnhLuu"];
            if (string.IsNullOrWhiteSpace(thuMuc))
            {
                Console.WriteLine("[CameraAnhLuu] Bật chụp nhưng chưa cấu hình CameraNvr:ThuMucAnhLuu — bỏ qua.");
                return;
            }

            var dsGio = DocGioChup(_configuration["CameraNvr:GioChupAnhLuu"] ?? "08:30");

            try { await Task.Delay(TimeSpan.FromSeconds(40), stoppingToken); }
            catch (TaskCanceledException) { return; }

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    var mocGanNhat = MocDaQuaGanNhat(dsGio, DateTime.Now);
                    var lanCuoi = DocMoc(thuMuc);
                    if (lanCuoi == null || lanCuoi < mocGanNhat)
                    {
                        var (tong, duoc) = await ChupMotLuotAsync(thuMuc, stoppingToken);
                        GhiMoc(thuMuc, tong, duoc);
                        Console.WriteLine($"[CameraAnhLuu] Chụp {duoc}/{tong} camera vào {thuMuc} lúc {DateTime.Now:dd/MM/yyyy HH:mm}");
                    }
                }
                catch (TaskCanceledException) { return; }
                catch (Exception ex)
                {
                    // Mất mạng tới ISAPI / thư mục chia sẻ: lượt kiểm tra sau thử lại
                    Console.WriteLine($"[CameraAnhLuu Error]: {ex.Message}");
                }

                // Ngủ tới mốc kế tiếp, nhưng tối đa 30 phút để còn kịp nhận ra thư mục/mạng đã sống lại
                var cho = MocKeTiep(dsGio, DateTime.Now) - DateTime.Now;
                if (cho > TimeSpan.FromMinutes(30)) cho = TimeSpan.FromMinutes(30);
                if (cho < TimeSpan.FromSeconds(30)) cho = TimeSpan.FromSeconds(30);
                try { await Task.Delay(cho, stoppingToken); }
                catch (TaskCanceledException) { return; }
            }
        }

        private async Task<(int tong, int duoc)> ChupMotLuotAsync(string thuMuc, CancellationToken ct)
        {
            using var scope = _scopeFactory.CreateScope();
            var giamSat = scope.ServiceProvider.GetRequiredService<CameraGiamSatService>();
            var xem = scope.ServiceProvider.GetRequiredService<CameraXemTrucTiepService>();

            var duLieu = await giamSat.DanhSachCameraAsync(null, null, null, true, false, ct);
            if (!duLieu.TryGetProperty("cameras", out var cameras) || cameras.ValueKind != JsonValueKind.Array)
                return (0, 0);

            // Một kênh có thể xuất hiện 2 lần (khoá ISAPI kèm IP camera) -> bỏ trùng theo đầu ghi + kênh
            var dsKenh = new HashSet<(string nvr, int kenh)>();
            foreach (var c in cameras.EnumerateArray())
            {
                if (c.TryGetProperty("nvr_ip", out var n) && n.GetString() is { } nvr
                    && c.TryGetProperty("cam_id", out var k) && int.TryParse(k.ToString(), out var kenh)
                    && System.Net.IPAddress.TryParse(nvr, out var ip)
                    && ip.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork)
                {
                    dsKenh.Add((ip.ToString(), kenh));
                }
            }

            Directory.CreateDirectory(thuMuc);
            var duoc = 0;

            // 6 ảnh song song: đủ nhanh (~319 ảnh trong vài chục giây) mà không dồn ép một đầu ghi
            await Parallel.ForEachAsync(dsKenh, new ParallelOptions { MaxDegreeOfParallelism = 6, CancellationToken = ct },
                async (muc, token) =>
                {
                    try
                    {
                        var anh = await xem.LayAnhChupAsync(muc.nvr, muc.kenh, token);
                        if (anh == null || anh.Length == 0) return;

                        // Ghi ra file tạm rồi đổi tên đè: người xem không bao giờ đọc phải ảnh ghi dở
                        var dich = Path.Combine(thuMuc, $"{muc.nvr}_{muc.kenh}.jpg");
                        var tam = dich + ".tmp";
                        await File.WriteAllBytesAsync(tam, anh, token);
                        File.Move(tam, dich, true);
                        Interlocked.Increment(ref duoc);
                    }
                    catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or IOException)
                    {
                        // Camera mất kết nối / đầu ghi chậm: giữ ảnh cũ của kênh đó
                        if (ct.IsCancellationRequested) throw;
                    }
                });

            return (dsKenh.Count, duoc);
        }

        private static List<TimeSpan> DocGioChup(string cauHinh)
        {
            var ds = cauHinh.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Select(s => TimeSpan.TryParseExact(s, @"hh\:mm", CultureInfo.InvariantCulture, out var t) ? t : (TimeSpan?)null)
                .Where(t => t.HasValue && t.Value < TimeSpan.FromDays(1))
                .Select(t => t!.Value)
                .Distinct()
                .OrderBy(t => t)
                .ToList();
            return ds.Count > 0 ? ds : new List<TimeSpan> { new(8, 30, 0) };
        }

        private static DateTime MocDaQuaGanNhat(List<TimeSpan> dsGio, DateTime bayGio)
        {
            var homNay = dsGio.Select(g => bayGio.Date + g).Where(m => m <= bayGio).ToList();
            return homNay.Count > 0 ? homNay.Max() : bayGio.Date.AddDays(-1) + dsGio.Max();
        }

        private static DateTime MocKeTiep(List<TimeSpan> dsGio, DateTime bayGio)
        {
            var homNay = dsGio.Select(g => bayGio.Date + g).Where(m => m > bayGio).ToList();
            return homNay.Count > 0 ? homNay.Min() : bayGio.Date.AddDays(1) + dsGio.Min();
        }

        private static DateTime? DocMoc(string thuMuc)
        {
            try
            {
                var f = Path.Combine(thuMuc, TenFileMoc);
                if (!File.Exists(f)) return null;
                var dong = File.ReadLines(f).FirstOrDefault();
                return DateTime.TryParseExact(dong, "yyyy-MM-ddTHH:mm:ss", CultureInfo.InvariantCulture, DateTimeStyles.None, out var d)
                    ? d : null;
            }
            catch (IOException) { return null; }
        }

        private static void GhiMoc(string thuMuc, int tong, int duoc)
        {
            File.WriteAllText(Path.Combine(thuMuc, TenFileMoc),
                $"{DateTime.Now:yyyy-MM-ddTHH:mm:ss}\nChụp được {duoc}/{tong} camera\nMáy: {Environment.MachineName}\n");
        }
    }
}
