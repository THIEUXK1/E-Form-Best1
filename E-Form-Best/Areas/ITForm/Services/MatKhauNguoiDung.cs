using System.Security.Cryptography;
using System.Text;
using E_Form_Best.Models.ITForm;
using Microsoft.AspNetCore.Identity;

namespace E_Form_Best.Areas.ITForm.Services
{
    /// <summary>
    /// Kiểm / đặt mật khẩu DB của bảng User. Trước đây mật khẩu lưu thô ở mat_khau; nay lưu bản băm ở
    /// mat_khau_hash và chuyển dần: tài khoản nào đăng nhập đúng bằng mật khẩu thô thì băm luôn và xoá bản thô.
    /// Admin-E-Form-Best1 (/QLtaiKhoan) có bản sao lớp này — cùng PasswordHasher nên băm bên nào kiểm bên kia được,
    /// sửa một bên phải sửa bên kia.
    /// </summary>
    public static class MatKhauNguoiDung
    {
        // PasswordHasher không giữ trạng thái, dùng chung một thể hiện được
        private static readonly PasswordHasher<User> BoBam = new();

        // Thông báo chung cho mọi lỗi đăng nhập: không phân biệt "không có tài khoản" với "sai mật khẩu",
        // không lộ số lần đã thử / số giây còn khoá, để không dò được danh sách tài khoản tồn tại.
        public const string ThongBaoSai = "Tài khoản hoặc mật khẩu không chính xác.";
        public const string ThongBaoKhoa = "Tài khoản tạm thời bị khóa do nhập sai nhiều lần. Vui lòng thử lại sau hoặc liên hệ quản trị viên.";

        // Bản băm giả để tài khoản không tồn tại cũng tốn đúng một lần PBKDF2 như tài khoản thật —
        // nếu không, phản hồi nhanh hơn hẳn và đo độ trễ là biết tài khoản nào có thật.
        private static readonly User UserGia = new();
        private static readonly string BanBamGia = BoBam.HashPassword(UserGia, Guid.NewGuid().ToString());

        /// <summary>Tốn thời gian như một lần kiểm mật khẩu thật; gọi khi không tìm thấy tài khoản.</summary>
        public static void GiaLapKiemTra(string? matKhau)
        {
            BoBam.VerifyHashedPassword(UserGia, BanBamGia, matKhau ?? "");
        }

        /// <summary>
        /// Kéo mọi phản hồi đăng nhập THẤT BẠI lên tối thiểu ~1 giây (+ nhiễu ngẫu nhiên). Tài khoản liên kết Domain
        /// phải gọi AD nên chậm hơn hẳn tên không tồn tại; không đệm thì đo độ trễ là lọc ra được tài khoản thật.
        /// </summary>
        public static async Task DemThoiGianThatBaiAsync(System.Diagnostics.Stopwatch dongHo)
        {
            int toiThieuMs = 1000 + RandomNumberGenerator.GetInt32(0, 250);
            int conLai = toiThieuMs - (int)dongHo.ElapsedMilliseconds;
            if (conLai > 0) await Task.Delay(conLai);
        }

        /// <summary>
        /// Trả true nếu mật khẩu đúng. Khi đúng mà user vừa được chuyển sang băm (hoặc bản băm cần nâng cấp)
        /// thì entity đã bị sửa — canLuu = true, nơi gọi phải SaveChanges.
        /// </summary>
        public static bool KiemTra(User user, string matKhau, out bool canLuu)
        {
            canLuu = false;
            if (string.IsNullOrEmpty(matKhau)) return false;

            // Có bản băm thì chỉ tin bản băm: mat_khau thô sót lại (vd. app cũ ghi đè) không được mở cửa sau
            if (!string.IsNullOrEmpty(user.MatKhauHash))
            {
                var ketQua = BoBam.VerifyHashedPassword(user, user.MatKhauHash, matKhau);
                if (ketQua == PasswordVerificationResult.Failed) return false;
                if (ketQua == PasswordVerificationResult.SuccessRehashNeeded)
                {
                    Dat(user, matKhau);
                    canLuu = true;
                }
                return true;
            }

            if (string.IsNullOrEmpty(user.MatKhau)) return false;

            // So sánh thời gian cố định để không đoán được mật khẩu qua độ trễ phản hồi
            bool dung = CryptographicOperations.FixedTimeEquals(
                Encoding.UTF8.GetBytes(user.MatKhau), Encoding.UTF8.GetBytes(matKhau));
            if (dung)
            {
                Dat(user, matKhau);
                canLuu = true;
            }
            return dung;
        }

        /// <summary>
        /// Số giây khoá sau lần sai thứ soLanSai (từ 5): 30 * 2^(n-5), trần 15 phút. Có trần để kẻ biết tên
        /// tài khoản không khoá được người khác nhiều ngày; tính bằng long để khỏi tràn số âm (tự mở khoá).
        /// </summary>
        public static int SoGiayKhoa(int soLanSai)
        {
            const long TranGiay = 15 * 60;
            int mu = Math.Clamp(soLanSai - 5, 0, 20);
            return (int)Math.Min(30L << mu, TranGiay);
        }

        /// <summary>Đặt mật khẩu mới: ghi bản băm, xoá bản thô. Nơi gọi tự SaveChanges.</summary>
        public static void Dat(User user, string matKhau)
        {
            user.MatKhauHash = BoBam.HashPassword(user, matKhau);
            user.MatKhau = null;
        }
    }
}
