using E_Form_Best.Context;
using E_Form_Best.Models.ITForm;
using Microsoft.EntityFrameworkCore;

namespace E_Form_Best.Areas.ITForm.Services
{
    /// <summary>
    /// Job nền chụp số liệu thiết bị mỗi ngày vào KK_ThietBiChotNgay (bản quyền Windows / Office / Cần cài Office /
    /// trạng thái) để "Báo cáo định kì" so được kỳ này với kỳ trước — KK_ThietBi chỉ giữ giá trị hiện tại.
    ///
    /// Chụp 1 lần/ngày từ ThietBiChotNgay:GioChup (mặc định 22h, gần cuối ngày để bản chụp là số cuối ngày).
    /// Bảng còn trống (mới triển khai) thì chụp ngay để có mốc đầu tiên.
    /// Idempotent: đã có bản chụp hôm nay thì bỏ qua; 2 máy chủ chụp cùng lúc thì unique
    /// (ngay, cong_ty, chi_tieu, gia_tri) chặn máy thứ hai, lỗi đó được bỏ qua.
    /// </summary>
    public class ThietBiChotNgayWorker : BackgroundService
    {
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly IConfiguration _configuration;

        public ThietBiChotNgayWorker(IServiceScopeFactory scopeFactory, IConfiguration configuration)
        {
            _scopeFactory = scopeFactory;
            _configuration = configuration;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            if (!(_configuration.GetValue<bool?>("ThietBiChotNgay:Bat") ?? true)) return;
            var gioChup = Math.Clamp(_configuration.GetValue<int?>("ThietBiChotNgay:GioChup") ?? 22, 0, 23);

            try { await Task.Delay(TimeSpan.FromSeconds(60), stoppingToken); }
            catch (TaskCanceledException) { return; }

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    var soDong = await ChupAsync(gioChup, stoppingToken);
                    if (soDong > 0)
                        Console.WriteLine($"[ThietBiChotNgay] Chụp {soDong} dòng số liệu thiết bị lúc {DateTime.Now:dd/MM/yyyy HH:mm}");
                }
                catch (TaskCanceledException) { return; }
                catch (Exception ex)
                {
                    // Chưa có bảng / mất kết nối DB: bỏ lượt này, lượt sau thử lại
                    Console.WriteLine($"[ThietBiChotNgay Error]: {ex.Message}");
                }

                try { await Task.Delay(TimeSpan.FromMinutes(15), stoppingToken); }
                catch (TaskCanceledException) { return; }
            }
        }

        private async Task<int> ChupAsync(int gioChup, CancellationToken ct)
        {
            using var scope = _scopeFactory.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<ITFormContext>();

            var bayGio = DateTime.Now;
            var homNay = DateOnly.FromDateTime(bayGio);
            if (await db.KkThietBiChotNgays.AnyAsync(x => x.Ngay == homNay, ct)) return 0;
            var bangTrong = !await db.KkThietBiChotNgays.AnyAsync(ct);
            if (bayGio.Hour < gioChup && !bangTrong) return 0;

            var baoCao = scope.ServiceProvider.GetRequiredService<BaoCaoDinhKyService>();
            var ds = await baoCao.TinhChotAsync(ct);
            if (ds.Count == 0) return 0;

            db.KkThietBiChotNgays.AddRange(ds.Select(x => new KkThietBiChotNgay
            {
                Ngay = homNay,
                CongTy = x.CongTy.Length > 255 ? x.CongTy[..255] : x.CongTy,
                ChiTieu = x.ChiTieu,
                GiaTri = x.GiaTri,
                SoLuong = x.SoLuong,
                GhiNhanLuc = bayGio
            }));
            try
            {
                // Một lần SaveChanges = một transaction: cả bản chụp vào hoặc không dòng nào
                await db.SaveChangesAsync(ct);
            }
            catch (DbUpdateException)
            {
                // Máy chủ khác vừa chụp xong hôm nay (trùng unique) thì bỏ qua; lỗi khác ném tiếp để log
                db.ChangeTracker.Clear();
                if (await db.KkThietBiChotNgays.AnyAsync(x => x.Ngay == homNay, ct)) return 0;
                throw;
            }
            return ds.Count;
        }
    }
}
