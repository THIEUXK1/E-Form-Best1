using E_Form_Best.Context;
using E_Form_Best.Models.ITForm;
using Microsoft.EntityFrameworkCore;

namespace E_Form_Best.Areas.ITForm.Services
{
    /// <summary>
    /// Job nền chốt chỉ số máy in mỗi ngày một lần (giờ đặt ở appsettings: MayIn:GioChotHangNgay).
    ///
    /// Idempotent: mỗi máy mỗi ngày chỉ có một dòng trong MayIn_ChiSo, nên app khởi động lại
    /// giữa chừng hay chạy lại trong ngày đều chỉ cập nhật dòng của ngày hôm đó.
    /// </summary>
    public class MayInPollWorker : BackgroundService
    {
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly IConfiguration _configuration;

        public MayInPollWorker(IServiceScopeFactory scopeFactory, IConfiguration configuration)
        {
            _scopeFactory = scopeFactory;
            _configuration = configuration;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            var gioChot = _configuration.GetValue<int?>("MayIn:GioChotHangNgay") ?? 23;

            await ChotBuNeuThieuAsync(gioChot, stoppingToken);

            while (!stoppingToken.IsCancellationRequested)
            {
                var now = DateTime.Now;
                var lanChay = now.Date.AddHours(gioChot);
                if (lanChay <= now) lanChay = lanChay.AddDays(1);

                Console.WriteLine($"[MayIn] Chờ {(lanChay - now).TotalHours:F2} giờ, chốt chỉ số lúc {lanChay:dd/MM/yyyy HH:mm}");

                try
                {
                    await Task.Delay(lanChay - now, stoppingToken);
                }
                catch (TaskCanceledException)
                {
                    return; // app đang tắt
                }

                try
                {
                    var (tong, thanhCong) = await ChotChiSoAsync(stoppingToken);
                    Console.WriteLine($"[MayIn] Đã chốt {thanhCong}/{tong} máy lúc {DateTime.Now:dd/MM/yyyy HH:mm}");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[MayIn Error]: {ex.Message}");
                }
            }
        }


        /// <summary>
        /// Chốt bù ngay sau khi app khởi động, cho trường hợp máy chủ tắt/restart đúng lúc giờ chốt
        /// và cả ngày hôm đó không ai ghi được chỉ số nào. Bỏ một ngày là biểu đồ trang in gãy một
        /// đoạn và không suy lại được, nên thà đọc muộn còn hơn mất.
        ///
        /// Chỉ chạy khi đã qua giờ chốt và hôm nay chưa có bản đọc tự động nào — mốc nhập tay hay
        /// mốc nạp từ Excel không tính là đã chốt.
        /// </summary>
        private async Task ChotBuNeuThieuAsync(int gioChot, CancellationToken ct)
        {
            if (DateTime.Now.Hour < gioChot) return;

            try
            {
                // Nhường app khởi động xong (nạp cấu hình, mở kết nối) rồi mới đi đọc gần trăm máy
                await Task.Delay(TimeSpan.FromSeconds(30), ct);

                using var scope = _scopeFactory.CreateScope();
                var context = scope.ServiceProvider.GetRequiredService<ITFormContext>();

                var homNay = DateOnly.FromDateTime(DateTime.Now);
                var daChot = await context.MayInChiSos
                    .AnyAsync(c => c.NgayChot == homNay && c.Nguon != "Excel" && c.Nguon != "NhapTay", ct);

                if (daChot) return;

                Console.WriteLine($"[MayIn] Hôm nay chưa chốt chỉ số, chốt bù lúc {DateTime.Now:dd/MM/yyyy HH:mm}");
                var (tong, thanhCong) = await ChotChiSoAsync(ct);
                Console.WriteLine($"[MayIn] Chốt bù xong {thanhCong}/{tong} máy");
            }
            catch (TaskCanceledException)
            {
                // App tắt giữa chừng
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[MayIn Error] Chốt bù thất bại: {ex.Message}");
            }
        }
        /// <summary>Đọc toàn bộ máy đang bật theo dõi. Trả về (số máy đã thử, số máy đọc được).</summary>
        public async Task<(int Tong, int ThanhCong)> ChotChiSoAsync(CancellationToken ct)
        {
            using var scope = _scopeFactory.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<ITFormContext>();

            var dsMay = await context.MayIns
                .Where(m => m.TheoDoiTuDong && m.DiaChiIp != null && m.TrangThai == "HoatDong")
                .ToListAsync(ct);

            var soSongSong = _configuration.GetValue<int?>("MayIn:SoMayDocSongSong") ?? 8;
            var thanhCong = 0;

            // Chia lô thay vì bắn 100 request cùng lúc: mạng nhà máy và Kestrel đều không cần chịu tải đó.
            foreach (var lo in ChiaLo(dsMay, soSongSong))
            {
                // Mỗi máy một scope riêng vì DbContext không an toàn khi dùng song song
                var congViec = lo.Select(m => DocMotMayAsync(m.IdMayIn, ct)).ToList();
                var ketQua = await Task.WhenAll(congViec);
                thanhCong += ketQua.Count(x => x);
            }

            return (dsMay.Count, thanhCong);
        }

        private async Task<bool> DocMotMayAsync(int idMayIn, CancellationToken ct)
        {
            using var scope = _scopeFactory.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<ITFormContext>();
            var apiService = scope.ServiceProvider.GetRequiredService<MayInApiService>();

            var may = await context.MayIns.FirstOrDefaultAsync(m => m.IdMayIn == idMayIn, ct);
            if (may is null) return false;

            try
            {
                var ketQua = await apiService.DocVaLuuAsync(may, ct);
                return ketQua.ThanhCong;
            }
            catch (Exception ex)
            {
                // Một máy hỏng không được làm hỏng cả lượt chốt
                Console.WriteLine($"[MayIn] Lỗi đọc máy {idMayIn}: {ex.Message}");
                return false;
            }
        }

        private static IEnumerable<List<MayIn>> ChiaLo(List<MayIn> nguon, int kichThuoc)
        {
            for (var i = 0; i < nguon.Count; i += kichThuoc)
            {
                yield return nguon.GetRange(i, Math.Min(kichThuoc, nguon.Count - i));
            }
        }
    }
}
