namespace E_Form_Best.Areas.ITForm.Services
{
    /// <summary>
    /// Job nền quét danh sách MAC trên mọi WLC đã khai (WlcExec__{CÔNG TY}__...) mỗi WlcExec:Quet:ChuKyPhut (mặc định 30),
    /// ghi vào KK_WlcQuet / KK_WlcMac để nút "+" ở trang đơn Wi-Fi được đánh dấu sẵn.
    ///
    /// SSH bằng tài khoản dịch vụ WlcExec:Quet:TaiKhoan / MatKhau (chỉ cần quyền đọc trên WLC); bỏ trống thì không chạy.
    /// Tắt bằng WlcExec__Quet__Bat=false. Sai mật khẩu thì dừng ngay và nghỉ 6 giờ để không khoá tài khoản AD.
    /// </summary>
    public class WlcMacQuetWorker : BackgroundService
    {
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly IConfiguration _configuration;

        public WlcMacQuetWorker(IServiceScopeFactory scopeFactory, IConfiguration configuration)
        {
            _scopeFactory = scopeFactory;
            _configuration = configuration;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            var taiKhoan = _configuration["WlcExec:Quet:TaiKhoan"];
            var matKhau = _configuration["WlcExec:Quet:MatKhau"];
            if (!(_configuration.GetValue<bool?>("WlcExec:Quet:Bat") ?? true)
                || string.IsNullOrWhiteSpace(taiKhoan) || string.IsNullOrEmpty(matKhau)) return;

            var chuKy = TimeSpan.FromMinutes(Math.Clamp(_configuration.GetValue<int?>("WlcExec:Quet:ChuKyPhut") ?? 30, 10, 1440));

            try { await Task.Delay(TimeSpan.FromSeconds(60), stoppingToken); }
            catch (TaskCanceledException) { return; }

            while (!stoppingToken.IsCancellationRequested)
            {
                var nghi = chuKy;
                try
                {
                    if (!await QuetAsync(taiKhoan!.Trim(), matKhau!, stoppingToken))
                    {
                        Console.WriteLine($"[WLC quét] Tài khoản {taiKhoan} bị WLC từ chối mật khẩu — nghỉ 6 giờ, kiểm tra WlcExec__Quet__MatKhau.");
                        nghi = TimeSpan.FromHours(6);
                    }
                }
                catch (TaskCanceledException) { return; }
                catch (Exception ex)
                {
                    // Mất kết nối DB / bảng chưa tạo: bỏ lượt này, lượt sau chạy tiếp
                    Console.WriteLine($"[WLC quét Error]: {ex.Message}");
                }

                try { await Task.Delay(nghi, stoppingToken); }
                catch (TaskCanceledException) { return; }
            }
        }

        /// <summary>Quét lần lượt từng WLC; trả false nếu gặp sai mật khẩu (dừng luôn, không thử WLC khác).</summary>
        private async Task<bool> QuetAsync(string taiKhoan, string matKhau, CancellationToken ct)
        {
            using var scope = _scopeFactory.CreateScope();
            var wlcService = scope.ServiceProvider.GetRequiredService<WlcMacService>();
            var quetService = scope.ServiceProvider.GetRequiredService<WlcMacQuetService>();

            foreach (var congTy in wlcService.DsCongTyCoCauHinh())
            {
                foreach (var wlc in wlcService.DsWlc(congTy))
                {
                    var (ds, loi) = await wlcService.LayDsMacAsync(wlc, taiKhoan, matKhau, ct);
                    if (WlcMacService.LaLoiSaiMatKhau(loi))
                    {
                        await quetService.GhiKetQuaQuetAsync(congTy, wlc, null, loi, ct);
                        return false;
                    }
                    await quetService.GhiKetQuaQuetAsync(congTy, wlc, ds, loi, ct);
                }
            }
            return true;
        }
    }
}
