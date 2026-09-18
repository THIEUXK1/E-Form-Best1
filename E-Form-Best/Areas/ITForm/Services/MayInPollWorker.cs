using E_Form_Best.Context;
using E_Form_Best.Models.ITForm;
using Microsoft.EntityFrameworkCore;

namespace E_Form_Best.Areas.ITForm.Services
{
    /// <summary>
    /// Job nền tự đọc chỉ số máy in theo chu kỳ (mặc định mỗi giờ, đặt ở MayIn:ChuKyDocPhut),
    /// không cần ai bấm nút. Ngoài ra mỗi ngày một lần nạp nhật ký lỗi của máy để lấp những ngày
    /// máy tắt không đọc được (MayIn:GioNapLichSu).
    ///
    /// Idempotent: mỗi máy mỗi ngày chỉ có một dòng trong MayIn_ChiSo, nên chạy lại bao nhiêu lần
    /// trong ngày cũng chỉ cập nhật dòng của ngày hôm đó — số cuối cùng trong ngày là số chốt.
    /// Nhờ vậy app restart giữa chừng cũng không mất hay nhân bản dữ liệu, và không cần cơ chế
    /// "chốt bù" riêng như trước: lượt chạy kế tiếp tự lo.
    /// </summary>
    public class MayInPollWorker : BackgroundService
    {
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly IConfiguration _configuration;

        /// <summary>Ngày đã nạp nhật ký lỗi gần nhất, để mỗi ngày chỉ nạp một lần.</summary>
        private DateOnly? _ngayDaNapLichSu;

        public MayInPollWorker(IServiceScopeFactory scopeFactory, IConfiguration configuration)
        {
            _scopeFactory = scopeFactory;
            _configuration = configuration;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            var chuKyPhut = _configuration.GetValue<int?>("MayIn:ChuKyDocPhut") ?? 60;
            if (chuKyPhut < 5) chuKyPhut = 5;      // dưới 5 phút là quấy máy in vô ích
            if (chuKyPhut > 1440) chuKyPhut = 1440;

            var gioNapLichSu = _configuration.GetValue<int?>("MayIn:GioNapLichSu") ?? 5;

            // Nhường app khởi động xong (nạp cấu hình, mở kết nối) rồi mới đi đọc gần trăm máy
            try
            {
                await Task.Delay(TimeSpan.FromSeconds(30), stoppingToken);
            }
            catch (TaskCanceledException)
            {
                return;
            }

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    var (tong, thanhCong) = await ChotChiSoAsync(stoppingToken);
                    Console.WriteLine($"[MayIn] Đọc {thanhCong}/{tong} máy lúc {DateTime.Now:dd/MM/yyyy HH:mm}");

                    await NapLichSuHangNgayAsync(gioNapLichSu, stoppingToken);
                }
                catch (TaskCanceledException)
                {
                    return; // app đang tắt
                }
                catch (Exception ex)
                {
                    // Một lượt hỏng không được làm chết job nền, lượt sau chạy tiếp
                    Console.WriteLine($"[MayIn Error]: {ex.Message}");
                }

                var lanSau = DateTime.Now.AddMinutes(chuKyPhut);
                Console.WriteLine($"[MayIn] Lượt đọc kế tiếp lúc {lanSau:dd/MM/yyyy HH:mm}");

                try
                {
                    await Task.Delay(TimeSpan.FromMinutes(chuKyPhut), stoppingToken);
                }
                catch (TaskCanceledException)
                {
                    return;
                }
            }
        }

        /// <summary>
        /// Mỗi ngày một lần, sau giờ đã hẹn, đọc nhật ký lỗi của máy để lấp những ngày trong quá khứ
        /// chưa có chỉ số (máy tắt, mạng đứt, app dừng). Chỉ thêm ngày còn trống nên chạy lại vô hại.
        /// </summary>
        private async Task NapLichSuHangNgayAsync(int gioNapLichSu, CancellationToken ct)
        {
            var homNay = DateOnly.FromDateTime(DateTime.Now);
            if (_ngayDaNapLichSu == homNay) return;
            if (DateTime.Now.Hour < gioNapLichSu) return;

            var (tong, soNgayThem) = await NapLichSuAsync(ct);
            _ngayDaNapLichSu = homNay;

            Console.WriteLine($"[MayIn] Nạp nhật ký lỗi: thêm {soNgayThem} ngày chỉ số từ {tong} máy");
        }

        /// <summary>Đọc toàn bộ máy đang bật theo dõi. Trả về (số máy đã thử, số máy đọc được).</summary>
        public async Task<(int Tong, int ThanhCong)> ChotChiSoAsync(CancellationToken ct)
        {
            var dsMay = await LayDanhSachMayAsync(ct);
            var thanhCong = 0;

            // Chia lô thay vì bắn 100 request cùng lúc: mạng nhà máy và Kestrel đều không cần chịu tải đó.
            foreach (var lo in ChiaLo(dsMay, LaySoSongSong()))
            {
                // Mỗi máy một scope riêng vì DbContext không an toàn khi dùng song song
                var ketQua = await Task.WhenAll(lo.Select(m => DocMotMayAsync(m.IdMayIn, ct)));
                thanhCong += ketQua.Count(x => x);
            }

            return (dsMay.Count, thanhCong);
        }

        /// <summary>Nạp nhật ký lỗi toàn bộ máy. Trả về (số máy đã thử, tổng số ngày thêm mới).</summary>
        public async Task<(int Tong, int SoNgayThem)> NapLichSuAsync(CancellationToken ct)
        {
            var dsMay = await LayDanhSachMayAsync(ct);
            var soNgayThem = 0;

            foreach (var lo in ChiaLo(dsMay, LaySoSongSong()))
            {
                var ketQua = await Task.WhenAll(lo.Select(m => NapLichSuMotMayAsync(m.IdMayIn, ct)));
                soNgayThem += ketQua.Sum();
            }

            return (dsMay.Count, soNgayThem);
        }

        private async Task<List<MayIn>> LayDanhSachMayAsync(CancellationToken ct)
        {
            using var scope = _scopeFactory.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<ITFormContext>();

            return await context.MayIns
                .Where(m => m.TheoDoiTuDong && m.DiaChiIp != null && m.TrangThai == "HoatDong")
                .ToListAsync(ct);
        }

        private int LaySoSongSong()
        {
            var so = _configuration.GetValue<int?>("MayIn:SoMayDocSongSong") ?? 8;
            return so < 1 ? 1 : so;
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

        private async Task<int> NapLichSuMotMayAsync(int idMayIn, CancellationToken ct)
        {
            using var scope = _scopeFactory.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<ITFormContext>();
            var apiService = scope.ServiceProvider.GetRequiredService<MayInApiService>();

            var may = await context.MayIns.FirstOrDefaultAsync(m => m.IdMayIn == idMayIn, ct);
            if (may is null) return 0;

            try
            {
                var ketQua = await apiService.NapLichSuTuMayAsync(may, ct);
                return ketQua.SoNgayThemMoi;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[MayIn] Lỗi nạp lịch sử máy {idMayIn}: {ex.Message}");
                return 0;
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
