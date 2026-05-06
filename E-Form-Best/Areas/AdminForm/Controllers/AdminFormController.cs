using E_Form_Best.Context;
using E_Form_Best.Models.ITForm;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace E_Form_Best.Areas.AdminForm.Controllers
{
    [Area("AdminForm")]
    public class AdminFormController : Controller
    {
        public ITFormContext _context;
        public AdminFormController()
        {
            _context = new ITFormContext();
        }
        #region MenuA
        [HttpGet("/MenuA")]
        public IActionResult MenuA()
        {
            return View();
        }
        #endregion

        #region Thông Tin Cá Nhân & Ảnh Đại Diện

        [HttpGet("/ThongTinTaiKhoan")]
        [Authorize]
        public async Task<IActionResult> ThongTinTaiKhoan()
        {
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userIdClaim)) return RedirectToAction("Login");

            int userId = int.Parse(userIdClaim);
            var user = _context.Users.FirstOrDefault(u => u.IdNguoiDung == userId);

            // Truyền trực tiếp đường dẫn ảnh từ Database vào ViewBag để View hiển thị
            ViewBag.AnhDaiDien = user?.AnhDaiDien;

            return View();
        }

        [HttpPost("/DonXetDuyet/CapNhatAnh")]
        [Authorize]
        public async Task<IActionResult> CapNhatAnh(IFormFile fileAnh)
        {
            if (fileAnh == null || fileAnh.Length == 0) return Json(new { success = false, message = "File không hợp lệ." });

            try
            {
                var userId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier).Value);
                var user = await _context.Users.FindAsync(userId);

                // 1. Xóa ảnh cũ nếu có (để dọn dẹp bộ nhớ File Server)
                if (!string.IsNullOrEmpty(user.AnhDaiDien) && System.IO.File.Exists(user.AnhDaiDien))
                {
                    System.IO.File.Delete(user.AnhDaiDien);
                }

                // 2. Lưu ảnh mới vào File Server
                string folderPath = @"\\10.0.60.30\BPVN-Fileserver\Public\IT-Information Technology Dept\5.E-Form\User";
                if (!Directory.Exists(folderPath)) Directory.CreateDirectory(folderPath);

                string fileName = $"Avatar_{userId}_{DateTime.Now:yyyyMMddHHmmss}{Path.GetExtension(fileAnh.FileName)}";
                string fullPath = Path.Combine(folderPath, fileName);

                using (var stream = new FileStream(fullPath, FileMode.Create))
                {
                    await fileAnh.CopyToAsync(stream);
                }

                // 3. Lưu đường dẫn mới vào DB
                user.AnhDaiDien = fullPath;
                _context.Users.Update(user);
                await _context.SaveChangesAsync();

                return Json(new { success = true });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = ex.Message });
            }
        }

        [HttpPost("/DonXetDuyet/XoaAnhDaiDien")]
        [Authorize]
        public async Task<IActionResult> XoaAnhDaiDien()
        {
            try
            {
                var userId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier).Value);
                var user = await _context.Users.FindAsync(userId);

                if (!string.IsNullOrEmpty(user.AnhDaiDien) && System.IO.File.Exists(user.AnhDaiDien))
                {
                    System.IO.File.Delete(user.AnhDaiDien); // Xóa file vật lý trong thư mục
                }

                user.AnhDaiDien = null; // Gán lại null trong DB
                _context.Users.Update(user);
                await _context.SaveChangesAsync();

                return Json(new { success = true });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = ex.Message });
            }
        }

        [HttpGet("/DonXetDuyet/GetAvatar")]
        [Authorize]
        public IActionResult GetAvatar(string path)
        {
            if (string.IsNullOrEmpty(path) || !System.IO.File.Exists(path))
            {
                return NotFound();
            }
            var image = System.IO.File.OpenRead(path);
            return File(image, "image/jpeg"); // Trả về FileStream cho thẻ <img>
        }

        [HttpPost("/DonXetDuyet/DoiMatKhau")]
        [Authorize]
        public async Task<IActionResult> DoiMatKhau(string oldPassword, string newPassword, string confirmPassword)
        {
            try
            {
                var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                if (string.IsNullOrEmpty(userIdClaim))
                    return Json(new { success = false, message = "Phiên làm việc hết hạn." });

                int userId = int.Parse(userIdClaim);
                var user = await _context.Users.FindAsync(userId);

                if (user == null)
                    return Json(new { success = false, message = "Người dùng không tồn tại." });

                // 1. Kiểm tra mật khẩu cũ
                if (user.MatKhau != oldPassword)
                {
                    return Json(new { success = false, message = "Mật khẩu hiện tại không đúng." });
                }

                // 2. Kiểm tra khớp mật khẩu mới
                if (newPassword != confirmPassword)
                {
                    return Json(new { success = false, message = "Mật khẩu xác nhận không khớp." });
                }

                // 3. Cập nhật mật khẩu
                user.MatKhau = newPassword;
                user.NgayCapNhat = DateTime.Now;

                _context.Users.Update(user);
                await _context.SaveChangesAsync();

                // --- MỚI: Đăng xuất ngay lập tức sau khi đổi mật khẩu thành công ---
                await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);

                return Json(new { success = true, message = "Đổi mật khẩu thành công! Hệ thống sẽ yêu cầu đăng nhập lại." });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = "Lỗi: " + ex.Message });
            }
        }
        #endregion

    }

}
