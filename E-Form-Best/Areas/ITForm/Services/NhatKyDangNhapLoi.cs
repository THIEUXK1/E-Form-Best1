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

        /// <summary>
        /// Khoá GIẢ cho tên tài khoản không tồn tại. Tài khoản thật sai 5 lần thì báo "bị khoá"; tên bịa không bao giờ
        /// bị khoá thì kẻ quét thử 5 lần là biết tài khoản nào có thật → phải khoá giả y hệt công thức thật.
        /// Bộ đếm lấy từ chính bảng này (các lần KhongTonTai 24h gần nhất) chứ không giữ RAM: 2 máy chủ dùng chung DB
        /// nên đổi máy chủ cũng không né được, và tool gửi hàng loạt tên ngẫu nhiên không làm phình bộ nhớ app.
        /// Gọi TRƯỚC GhiAsync của lần hiện tại. Trả dangKhoa = đang trong thời gian khoá (không tăng bộ đếm, như user thật).
        /// </summary>
        public static async Task<(bool dangKhoa, string thongBao)> XetKhoaGiaAsync(ITFormContext db, string? taiKhoanNhap)
        {
            string? tk = Cat(taiKhoanNhap?.Trim(), 100);
            if (string.IsNullOrEmpty(tk)) return (false, MatKhauNguoiDung.ThongBaoSai);

            List<DateTime> cacLan;
            try
            {
                var tu = DateTime.Now.AddDays(-1);
                // So sánh theo collation SQL (không phân biệt hoa thường) — khớp cách tra tài khoản thật lúc đăng nhập
                cacLan = await db.LichSuDangNhapLois.AsNoTracking()
                    .Where(x => x.ThoiGian >= tu && x.LyDo == KhongTonTai && x.TaiKhoanNhap == tk)
                    .OrderByDescending(x => x.ThoiGian)
                    .Take(200)
                    .Select(x => x.ThoiGian)
                    .ToListAsync();
            }
            catch
            {
                return (false, MatKhauNguoiDung.ThongBaoSai); // không đọc được log thì không khoá giả, vẫn trả thông báo chung
            }

            // Phát lại lịch sử theo đúng luật tài khoản thật: lần thử rơi vào lúc đang khoá thì không tính
            cacLan.Reverse();
            int soLan = 0;
            DateTime? khoaDen = null;
            foreach (var t in cacLan)
            {
                if (khoaDen.HasValue && t < khoaDen) continue;
                soLan++;
                if (soLan >= 5) khoaDen = t.AddSeconds(MatKhauNguoiDung.SoGiayKhoa(soLan));
            }

            if (khoaDen.HasValue && khoaDen > DateTime.Now) return (true, MatKhauNguoiDung.ThongBaoKhoa);
            return (false, soLan + 1 >= 5 ? MatKhauNguoiDung.ThongBaoKhoa : MatKhauNguoiDung.ThongBaoSai);
        }

        private static string? Cat(string? s, int max) =>
            string.IsNullOrEmpty(s) || s.Length <= max ? s : s[..max];
    }
}
