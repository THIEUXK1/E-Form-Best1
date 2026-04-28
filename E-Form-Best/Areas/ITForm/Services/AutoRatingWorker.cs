using E_Form_Best.Context;
using E_Form_Best.Models.ITForm;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace E_Form_Best.Areas.ITForm.Services
{
    public class AutoRatingWorker : BackgroundService
    {
        private readonly IServiceScopeFactory _scopeFactory;

        public AutoRatingWorker(IServiceScopeFactory scopeFactory)
        {
            _scopeFactory = scopeFactory;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                // 1. Tính toán thời gian đến 12h đêm tiếp theo
                var now = DateTime.Now;
                var nextRunTime = now.Date.AddDays(1); // 00:00:00 ngày hôm sau
                var delay = nextRunTime - now;

                // Ghi log để kiểm tra thời gian chờ (Tùy chọn)
                Console.WriteLine($"[AutoRating] Đang chờ {delay.TotalHours:F2} giờ để chạy lúc {nextRunTime:dd/MM/yyyy HH:mm:ss}");

                // 2. Chờ cho đến đúng thời điểm đó
                await Task.Delay(delay, stoppingToken);

                // 3. Thực hiện công việc
                try
                {
                    await DoWork();
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[AutoRating Error]: {ex.Message}");
                }
            }
        }

        private async Task DoWork()
        {
            using (var scope = _scopeFactory.CreateScope())
            {
                var context = scope.ServiceProvider.GetRequiredService<ITFormContext>();
                DateTime oneMonthAgo = DateTime.Now.AddMonths(-1);

                // Logic: Đã xong (IdAdmin), quá 1 tháng (TimeAdmin), chưa hủy, chưa đánh giá
                var formsToAutoRate = await context.FormIts
                    .Where(f => f.IdAdmin != null
                             && f.TimeAdmin != null
                             && f.TimeAdmin <= oneMonthAgo
                             && !(f.TenForm ?? "").Contains("[ĐÃ HỦY]")
                             && !context.DanhGiaFormIts.Any(dg => dg.IdFormIt == f.Id))
                    .ToListAsync();

                if (formsToAutoRate.Any())
                {
                    foreach (var form in formsToAutoRate)
                    {
                        context.DanhGiaFormIts.Add(new DanhGiaFormIt
                        {
                            IdFormIt = form.Id,
                            IdNguoiDanhGia = 0,
                            TenNguoiDanhGia = "Hệ thống (Auto)",
                            TimeNguoiDanhGia = DateTime.Now,
                            MucDo = 5
                        });
                    }
                    await context.SaveChangesAsync();
                    Console.WriteLine($"[AutoRating]: Đã tự động đánh giá cho {formsToAutoRate.Count} đơn.");
                }
            }
        }
    }
}