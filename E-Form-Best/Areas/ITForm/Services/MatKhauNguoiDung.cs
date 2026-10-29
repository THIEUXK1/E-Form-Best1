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

        /// <summary>Đặt mật khẩu mới: ghi bản băm, xoá bản thô. Nơi gọi tự SaveChanges.</summary>
        public static void Dat(User user, string matKhau)
        {
            user.MatKhauHash = BoBam.HashPassword(user, matKhau);
            user.MatKhau = null;
        }
    }
}
