using E_Form_Best.Context;
using E_Form_Best.Models.ITForm;
using Microsoft.EntityFrameworkCore;

namespace E_Form_Best.Areas.ITForm.Services
{
    /// <summary>
    /// Ghi một lần đăng nhập thất bại vào LichSuDangNhapLoi. Gọi SAU khi đã lưu FailedAttempts/LockoutEnd:
    /// lưu riêng một nhịp SaveChanges để lỗi ghi log (mất kết nối, bảng chưa có) không kéo theo
    /// mất bộ đếm khoá — nếu gộp chung, ghi log hỏng là kẻ dò mật khẩu né được cơ chế khoá.
    /// Ghi log hỏng thì chỉ cảnh báo vào log app, không chặn phản hồi đăng nhập.
    /// </summary>
    public static class NhatKyDangNhapLoi
    {
        public const string KhongTonTai = "KhongTonTai";
        public const string SaiMatKhau = "SaiMatKhau";
        public const string DangKhoa = "DangKhoa";
        public const string VuaKhoa = "VuaKhoa";
        public const string TokenLoi = "TokenLoi";

        public static async Task GhiAsync(ITFormContext db, HttpContext http, string? taiKhoanNhap,
            int? idNguoiDung, string lyDo, string noiGoi)
        {
            var ip = http.Connection.RemoteIpAddress;
            if (ip != null && ip.IsIPv4MappedToIPv6) ip = ip.MapToIPv4();

            var entry = db.LichSuDangNhapLois.Add(new LichSuDangNhapLoi
            {
                ThoiGian = DateTime.Now,
                TaiKhoanNhap = Cat(taiKhoanNhap?.Trim(), 100),
                IdNguoiDung = idNguoiDung,
                DiaChiIp = ip?.ToString(),
                TrinhDuyet = Cat(http.Request.Headers.UserAgent.ToString(), 400),
                LyDo = lyDo,
                NoiGoi = Cat(noiGoi, 30) ?? ""
            });

            try
            {
                await db.SaveChangesAsync();
            }
            catch (Exception ex)
            {
                // Gỡ entity hỏng khỏi context để lần SaveChanges sau của request không thử lại nó
                entry.State = EntityState.Detached;
                http.RequestServices.GetService<ILoggerFactory>()?
                    .CreateLogger(typeof(NhatKyDangNhapLoi))
                    .LogWarning(ex, "Không ghi được LichSuDangNhapLoi ({LyDo}, {NoiGoi})", lyDo, noiGoi);
            }
        }

        private static string? Cat(string? s, int max) =>
            string.IsNullOrEmpty(s) || s.Length <= max ? s : s[..max];
    }
}
