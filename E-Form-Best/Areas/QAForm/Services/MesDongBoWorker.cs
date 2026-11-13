using E_Form_Best.Context;
using Microsoft.EntityFrameworkCore;

namespace E_Form_Best.Areas.QAForm.Services
{
    /// <summary>
    /// Job đồng bộ dữ liệu đóng gói NH-MES về QA_MesDongGoi theo từng ngày đóng gói (pbDate).
    /// Mỗi lượt: kéo lại MesDongBo:SoNgayQuetLai ngày gần nhất (hàng mới đóng, QC sửa ý kiến),
    /// rồi kéo bù các ngày từ MesDongBo:TuNgay chưa có bản đủ (lần đầu = kéo toàn bộ lịch sử).
    /// 2 máy chủ cùng chạy: ngày nào máy kia vừa đồng bộ trong nửa chu kỳ thì bỏ qua; ghi DB đã có khoá ứng dụng.
    /// Cấu hình: MesDongBo__Bat (mặc định true), MesDongBo__TuNgay (2026-01-01), MesDongBo__SoNgayQuetLai (7),
    /// MesDongBo__PhutMoiLuot (30).
    /// </summary>
    public class MesDongBoWorker : BackgroundService
    {
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly IConfiguration _configuration;
        private readonly ILogger<MesDongBoWorker> _logger;

        public MesDongBoWorker(IServiceScopeFactory scopeFactory, IConfiguration configuration, ILogger<MesDongBoWorker> logger)
        {
            _scopeFactory = scopeFactory;
            _configuration = configuration;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            if (!(_configuration.GetValue<bool?>("MesDongBo:Bat") ?? true)) return;
            var tuNgay = DateOnly.TryParse(_configuration["MesDongBo:TuNgay"], out var tn) ? tn : new DateOnly(2026, 1, 1);
            var soNgayQuetLai = Math.Clamp(_configuration.GetValue<int?>("MesDongBo:SoNgayQuetLai") ?? 7, 1, 60);
            var phut = Math.Clamp(_configuration.GetValue<int?>("MesDongBo:PhutMoiLuot") ?? 30, 5, 1440);

            try { await Task.Delay(TimeSpan.FromSeconds(90), stoppingToken); }
            catch (TaskCanceledException) { return; }

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    await MotLuotAsync(tuNgay, soNgayQuetLai, TimeSpan.FromMinutes(phut / 2.0), stoppingToken);
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { return; }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "[MesDongBo] Lỗi lượt đồng bộ, lượt sau thử lại");
                }

                try { await Task.Delay(TimeSpan.FromMinutes(phut), stoppingToken); }
                catch (TaskCanceledException) { return; }
            }
        }

        private async Task MotLuotAsync(DateOnly tuNgay, int soNgayQuetLai, TimeSpan boQuaNeuMoiHon, CancellationToken ct)
        {
            using var scope = _scopeFactory.CreateScope();
            if (!scope.ServiceProvider.GetRequiredService<MesApiService>().CoCauHinh) return;
            var db = scope.ServiceProvider.GetRequiredService<ITFormContext>();
            var kho = scope.ServiceProvider.GetRequiredService<MesKhoService>();

            var homNay = DateOnly.FromDateTime(DateTime.Now);
            var trangThai = await db.QaMesDongBoNgays.AsNoTracking()
                .Select(x => new { x.Ngay, x.DayDu, x.NgayDongBo }).ToDictionaryAsync(x => x.Ngay, ct);

            // Gần nhất trước (dữ liệu đang dùng), sau đó kéo bù lịch sử từ cũ đến mới
            var dsNgay = new List<DateOnly>();
            for (int i = 0; i < soNgayQuetLai; i++)
            {
                var n = homNay.AddDays(-i);
                if (n >= tuNgay) dsNgay.Add(n);
            }
            for (var n = tuNgay; n < homNay.AddDays(-soNgayQuetLai + 1); n = n.AddDays(1))
                if (!trangThai.TryGetValue(n, out var tt) || !tt.DayDu) dsNgay.Add(n);

            int soNgay = 0, tongDong = 0;
            foreach (var ngay in dsNgay)
            {
                ct.ThrowIfCancellationRequested();
                var moiNhat = await db.QaMesDongBoNgays.AsNoTracking().Where(x => x.Ngay == ngay)
                    .Select(x => (DateTime?)x.NgayDongBo).FirstOrDefaultAsync(ct);
                if (moiNhat != null && DateTime.Now - moiNhat.Value < boQuaNeuMoiHon) continue;
                try
                {
                    var (soDong, dayDu) = await kho.DongBoNgayAsync(ngay, ct);
                    soNgay++;
                    tongDong += soDong;
                    if (!dayDu) _logger.LogWarning("[MesDongBo] Ngày {Ngay:dd/MM/yyyy} vượt trần, mới lấy {SoDong} dòng", ngay, soDong);
                }
                catch (InvalidOperationException ex)
                {
                    // Lỗi MES (đăng nhập, lọc ngày...) — dừng lượt, không gọi dồn các ngày còn lại
                    _logger.LogError("[MesDongBo] Dừng ở ngày {Ngay:dd/MM/yyyy}: {Loi}", ngay, ex.Message);
                    break;
                }
            }
            if (soNgay > 0)
                _logger.LogInformation("[MesDongBo] Đồng bộ {SoNgay} ngày, {TongDong} dòng MES", soNgay, tongDong);
        }
    }
}
