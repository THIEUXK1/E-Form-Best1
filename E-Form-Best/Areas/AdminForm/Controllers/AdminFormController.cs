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
        [HttpGet("/MenuA")] // Hoặc đường dẫn tương ứng của bạn
        [Authorize]
        public async Task<IActionResult> MenuA()
        {
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (!string.IsNullOrEmpty(userIdClaim))
            {
                int userId = int.Parse(userIdClaim);
                var user = await _context.Users.FirstOrDefaultAsync(u => u.IdNguoiDung == userId);
                if (user != null)
                {
                    // Truyền ảnh mới nhất ra giao diện
                    ViewBag.AnhDaiDien = user.AnhDaiDien;
                }
            }

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
            var user = await _context.Users.FirstOrDefaultAsync(u => u.IdNguoiDung == userId);

            if (user == null) return NotFound();

            // [FIX 1] Gán trực tiếp ảnh từ DB vào ViewBag.
            // Điều này giúp View nhận được ảnh mới nhất thay vì dùng Claim cũ chưa được update.
            ViewBag.AnhDaiDien = user.AnhDaiDien;

            return View(user);
        }

        [HttpPost("/DonXetDuyet/CapNhatAnh")]
        [Authorize]
        public async Task<IActionResult> CapNhatAnh(IFormFile fileAnh)
        {
            // 1. Kiểm tra file hợp lệ và xác thực người dùng
            if (fileAnh == null || fileAnh.Length == 0)
                return Json(new { success = false, message = "File không hợp lệ." });

            if (User?.Identity == null || !User.Identity.IsAuthenticated)
                return Json(new { success = false, message = "Người dùng chưa đăng nhập." });

            try
            {
                var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                if (string.IsNullOrEmpty(userIdClaim))
                    return Json(new { success = false, message = "Không tìm thấy thông tin người dùng." });

                int userId = int.Parse(userIdClaim);
                var user = await _context.Users.FindAsync(userId);

                if (user == null)
                    return Json(new { success = false, message = "Người dùng không tồn tại." });

                // 2. Xóa ảnh cũ nếu có
                if (!string.IsNullOrEmpty(user.AnhDaiDien) && System.IO.File.Exists(user.AnhDaiDien))
                {
                    try { System.IO.File.Delete(user.AnhDaiDien); } catch { /* Bỏ qua nếu ko xóa đc ảnh cũ */ }
                }

                // 3. Lưu ảnh mới vào File Server
                string folderPath = @"\\10.0.60.30\BPVN-Fileserver\Public\IT-Information Technology Dept\5.E-Form\User";

                if (!Directory.Exists(folderPath))
                {
                    Directory.CreateDirectory(folderPath);
                }

                string fileName = $"Avatar_{userId}_{DateTime.Now:yyyyMMddHHmmss}{Path.GetExtension(fileAnh.FileName)}";
                string fullPath = Path.Combine(folderPath, fileName);

                using (var stream = new FileStream(fullPath, FileMode.Create))
                {
                    await fileAnh.CopyToAsync(stream);
                }

                // 4. Lưu đường dẫn mới vào DB
                user.AnhDaiDien = fullPath;
                _context.Users.Update(user);
                await _context.SaveChangesAsync();

                return Json(new { success = true });
            }
            catch (Exception ex)
            {
                // Trả về thông báo lỗi chi tiết để debug
                return Json(new { success = false, message = "Lỗi Server: " + ex.Message });
            }
        }

        [HttpPost("/DonXetDuyet/XoaAnhDaiDien")]
        [Authorize]
        public async Task<IActionResult> XoaAnhDaiDien()
        {
            // Kiểm tra xác thực người dùng
            if (User?.Identity == null || !User.Identity.IsAuthenticated)
                return Json(new { success = false, message = "Người dùng chưa đăng nhập." });

            try
            {
                var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                if (string.IsNullOrEmpty(userIdClaim) || !int.TryParse(userIdClaim, out int userId))
                    return Json(new { success = false, message = "Không tìm thấy thông tin định danh người dùng." });

                var user = await _context.Users.FindAsync(userId);

                // Kiểm tra user tồn tại trước khi thao tác
                if (user == null)
                    return Json(new { success = false, message = "Người dùng không tồn tại." });

                // Xóa file ảnh vật lý nếu có đường dẫn hợp lệ và file thực sự tồn tại
                if (!string.IsNullOrEmpty(user.AnhDaiDien) && System.IO.File.Exists(user.AnhDaiDien))
                {
                    try
                    {
                        System.IO.File.Delete(user.AnhDaiDien);
                    }
                    catch
                    {
                        // Bỏ qua nếu không xóa được ảnh cũ (ví dụ: file đang bị lock hoặc đã bị xóa thủ công)
                    }
                }

                // Cập nhật Database
                user.AnhDaiDien = null;
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
                // [FIX 2] Kiểm tra tồn tại file mặc định trước khi đọc để tránh sập (Lỗi 500).
                // Nếu không có, trả về NotFound() để View kích hoạt hàm "onerror" và hiện ảnh thay thế.
                var defaultPath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "images", "default-avatar.png");
                if (System.IO.File.Exists(defaultPath))
                {
                    return File(System.IO.File.OpenRead(defaultPath), "image/png");
                }
                return NotFound();
            }

            var image = System.IO.File.OpenRead(path);

            // [FIX 3] Trả về đúng định dạng extension của ảnh thay vì fix cứng jpeg
            string ext = Path.GetExtension(path).ToLower();
            string contentType = ext switch
            {
                ".png" => "image/png",
                ".gif" => "image/gif",
                ".webp" => "image/webp",
                _ => "image/jpeg"
            };

            return File(image, contentType);
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

                if (user.MatKhau != oldPassword)
                    return Json(new { success = false, message = "Mật khẩu hiện tại không đúng." });

                if (newPassword != confirmPassword)
                    return Json(new { success = false, message = "Mật khẩu xác nhận không khớp." });

                user.MatKhau = newPassword;
                user.NgayCapNhat = DateTime.Now;

                _context.Users.Update(user);
                await _context.SaveChangesAsync();

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
