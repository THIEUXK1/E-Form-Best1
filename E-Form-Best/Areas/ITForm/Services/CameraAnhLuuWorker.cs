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
            var chupBpvn = _configuration.GetValue<bool?>("CameraNvr:ChupAnhLuu") ?? false;
            // Công ty có đầu ghi máy chủ gọi thẳng được (MEGA) thì máy nào cũng tự chụp vào thư mục của mình,
            // không phụ thuộc cờ ChupAnhLuu của BPVN
            var dsCongTy = CameraXemTrucTiepService.DsCongTyAnhLuu
                .Where(c => CameraXemTrucTiepService.CoDauGhiTrucTiepCongTy(_configuration, c)).ToList();
            if (!chupBpvn && dsCongTy.Count == 0) return;

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
                var mocGanNhat = MocDaQuaGanNhat(dsGio, DateTime.Now);
                if (chupBpvn)
                {
                    try
                    {
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
                }

                // Mốc lượt chụp từng công ty nằm trong thư mục con của công ty đó
                foreach (var congTy in dsCongTy)
                {
                    var thuMucCty = Path.Combine(thuMuc, congTy);
                    try
                    {
                        var lanCuoi = DocMoc(thuMucCty);
                        if (lanCuoi == null || lanCuoi < mocGanNhat)
                        {
                            using var scope = _scopeFactory.CreateScope();
                            var (tong, duoc) = await scope.ServiceProvider.GetRequiredService<CameraXemTrucTiepService>()
                                .ChupAnhLuuTrucTiepAsync(congTy, stoppingToken);
                            Directory.CreateDirectory(thuMucCty);
                            GhiMoc(thuMucCty, tong, duoc);
                            Console.WriteLine($"[CameraAnhLuu {congTy}] Chụp {duoc}/{tong} camera lúc {DateTime.Now:dd/MM/yyyy HH:mm}");
                        }
                    }
                    catch (TaskCanceledException) { return; }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"[CameraAnhLuu {congTy} Error]: {ex.Message}");
                    }
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
            var chuaDuoc = new System.Collections.Concurrent.ConcurrentBag<(string nvr, int kenh)>();

            // 6 ảnh song song: đủ nhanh (~319 ảnh trong vài chục giây) mà không dồn ép một đầu ghi
            await Parallel.ForEachAsync(dsKenh, new ParallelOptions { MaxDegreeOfParallelism = 6, CancellationToken = ct },
                async (muc, token) =>
                {
                    var dich = Path.Combine(thuMuc, $"{muc.nvr}_{muc.kenh}.jpg");
                    try
                    {
                        var anh = await xem.LayAnhChupAsync(muc.nvr, muc.kenh, token);
                        if (anh == null || anh.Length == 0)
                        {
                            if (await LayAnhTuPlaybackAsync(xem, muc.nvr, muc.kenh, dich, token)) Interlocked.Increment(ref duoc);
                            else chuaDuoc.Add(muc);
                            return;
                        }

                        await GhiAnhAsync(dich, anh, null, token);
                        Interlocked.Increment(ref duoc);
                    }
                    catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or IOException or InvalidOperationException)
                    {
                        // Camera mất kết nối / đầu ghi chậm / thiếu ffmpeg: giữ ảnh cũ của kênh đó
                        if (ct.IsCancellationRequested) throw;
                        chuaDuoc.Add(muc);
                    }
                });

            // Lỗi lượt song song phần lớn do đầu ghi đang bị gọi dồn (10.0.21.251, 10.0.29.254 trả 403/luồng hỏng):
            // lấy lại từ playback lần lượt từng kênh. Kênh không có bản ghi chỉ tốn thêm vài lượt tìm kiếm.
            foreach (var muc in chuaDuoc)
            {
                try
                {
                    if (await LayAnhTuPlaybackAsync(xem, muc.nvr, muc.kenh, Path.Combine(thuMuc, $"{muc.nvr}_{muc.kenh}.jpg"), ct))
                        duoc++;
                }
                catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or IOException or InvalidOperationException)
                {
                    if (ct.IsCancellationRequested) throw;
                }
            }

            return (dsKenh.Count, duoc);
        }

        /// <summary>
        /// Chụp trực tiếp không được (camera mất kết nối, đầu ghi trả 403): lấy khung hình cuối của đoạn
        /// ghi cuối trên đầu ghi làm ảnh lưu. Giờ file đặt đúng giờ khung hình để /QLCamera ghi
        /// "Ảnh lưu lúc ..." đúng thời điểm; ảnh lưu đã mới bằng/hơn thì bỏ qua, khỏi chạy ffmpeg lại.
        /// </summary>
        private static async Task<bool> LayAnhTuPlaybackAsync(CameraXemTrucTiepService xem, string nvr, int kenh,
            string dich, CancellationToken ct)
        {
            var doan = await xem.TimGhiHinhCuoiAsync(nvr, kenh, ct);
            if (doan == null) return false;

            var gioKhung = CameraXemTrucTiepService.GioKhungHinhPlayback(doan.Value.batDau, doan.Value.ketThuc);
            if (File.Exists(dich) && File.GetLastWriteTime(dich) >= gioKhung) return false;

            var khung = await xem.ChupKhungHinhPlaybackAsync(nvr, kenh, doan.Value.batDau, doan.Value.ketThuc, ct);
            if (khung == null) return false;

            await GhiAnhAsync(dich, khung.Value.anh, khung.Value.gio, ct);
            return true;
        }

        // Ghi ra file tạm rồi đổi tên đè: người xem không bao giờ đọc phải ảnh ghi dở
        private static async Task GhiAnhAsync(string dich, byte[] anh, DateTime? gioFile, CancellationToken ct)
        {
            var tam = dich + ".tmp";
            await File.WriteAllBytesAsync(tam, anh, ct);
            if (gioFile != null) File.SetLastWriteTime(tam, gioFile.Value);
            File.Move(tam, dich, true);
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
