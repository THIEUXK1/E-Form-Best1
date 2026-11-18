namespace E_Form_Best.Areas.QAForm.Services
{
    /// <summary>
    /// Job đồng bộ file khiếu nại khách hàng (thư mục QA trên file server) vào QA_KhieuNai.
    /// Lượt đầu (sau khi app lên 2 phút) đọc mọi file; các lượt sau chỉ đọc file đã sửa (QaKhieuNaiService.FileDaDoc).
    /// Nhập idempotent (khoá SHA-256 từng dòng) nên 2 máy chủ cùng chạy không tạo trùng; dòng đã có chỉ điền ô trống.
    /// Máy không vào được share (tài khoản app pool không có quyền) thì chỉ ghi log cảnh báo.
    /// Cấu hình: QaKhieuNai__DongBoBat (mặc định true), QaKhieuNai__ThuMucDongBo, QaKhieuNai__PhutDongBo (30).
    /// </summary>
    public class KhieuNaiDongBoWorker : BackgroundService
    {
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly IConfiguration _configuration;
        private readonly ILogger<KhieuNaiDongBoWorker> _logger;

        public KhieuNaiDongBoWorker(IServiceScopeFactory scopeFactory, IConfiguration configuration, ILogger<KhieuNaiDongBoWorker> logger)
        {
            _scopeFactory = scopeFactory;
            _configuration = configuration;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            if (!(_configuration.GetValue<bool?>("QaKhieuNai:DongBoBat") ?? true)) return;
            var phut = Math.Clamp(_configuration.GetValue<int?>("QaKhieuNai:PhutDongBo") ?? 30, 5, 1440);
            var thuMuc = QaKhieuNaiService.ThuMucDongBo(_configuration);

            try { await Task.Delay(TimeSpan.FromMinutes(2), stoppingToken); }
            catch (TaskCanceledException) { return; }

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    using var scope = _scopeFactory.CreateScope();
                    var svc = scope.ServiceProvider.GetRequiredService<QaKhieuNaiService>();
                    var kq = await svc.DongBoThuMucAsync(thuMuc, tatCa: false, "Đồng bộ thư mục QA", stoppingToken);
                    if (kq.ThanhCong) _logger.LogInformation("Đồng bộ khiếu nại QA: {KetQua}", kq.ThongBao);
                    else _logger.LogWarning("Đồng bộ khiếu nại QA chưa xong: {KetQua}", kq.ThongBao);
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    _logger.LogError(ex, "Lỗi job đồng bộ khiếu nại QA");
                }
                try { await Task.Delay(TimeSpan.FromMinutes(phut), stoppingToken); }
                catch (TaskCanceledException) { return; }
            }
        }
    }
}
