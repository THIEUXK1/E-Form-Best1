using E_Form_Best.Context;
using E_Form_Best.Models.ITForm;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace E_Form_Best.Areas.ITForm.Controllers
{
    [Area("ITform")]
    public class ITFormController : Controller
    {
        public ITFormContext _context;
        public ITFormController()
        {
            _context = new ITFormContext();
        }

        #region logo
        [HttpGet("/Logo")]
        public IActionResult Logo()
        {
            return View();
        }
        #endregion

        #region Trang đăng nhập

        [HttpGet("/DonXetDuyet/DangNhap")]
        public IActionResult DangNhap()
        {
            // 1. Nếu người dùng đã đăng nhập (Cookie còn hạn), cho vào trang chủ luôn
            if (User.Identity.IsAuthenticated)
            {
                return Redirect("/menuA");
            }

            // 2. Kiểm tra xem trình duyệt có đang lưu Cookie email để điền sẵn không
            ViewBag.IsRemembered = Request.Cookies.ContainsKey("RememberedEmail");

            return View();
        }

        [HttpPost("/DonXetDuyet/DangNhap")]
        public async Task<IActionResult> DangNhap(string email, string matKhau, string deviceName, bool rememberMe = false)
        {
            // 1. Kiểm tra nhập liệu
            if (string.IsNullOrEmpty(email) || string.IsNullOrEmpty(matKhau))
            {
                ViewBag.Error = "Vui lòng nhập đầy đủ thông tin.";
                return View();
            }

            // 2. Kiểm tra tài khoản trong Database
            var user = _context.Users
                .FirstOrDefault(u => u.Tk == email && u.MatKhau == matKhau && u.TrangThai != "Khóa");

            if (user == null)
            {
                ViewBag.Error = "Tài khoản hoặc mật khẩu không đúng.";
                return View();
            }

            // 3. XỬ LÝ ĐỊNH DANH THIẾT BỊ (LAPTOP/PHONE)
            string userAgent = Request.Headers["User-Agent"].ToString();

            // Tìm thiết bị dựa trên ID người dùng và mã nhận diện trình duyệt (userAgent)
            var device = _context.UserDevices
                .FirstOrDefault(d => d.IdNguoiDung == user.IdNguoiDung && d.DeviceFingerprint == userAgent);

            if (device == null)
            {
                // Nếu là thiết bị/trình duyệt mới, lưu vào bảng UserDevices
                var newDevice = new UserDevice
                {
                    IdNguoiDung = user.IdNguoiDung,
                    DeviceFingerprint = userAgent,
                    DeviceName = deviceName ?? "Thiết bị không xác định",
                    LastLogin = DateTime.Now,
                    IsTrusted = true
                };
                _context.UserDevices.Add(newDevice);
            }
            else
            {
                // Nếu thiết bị cũ quay lại, cập nhật thời gian truy cập mới nhất
                device.LastLogin = DateTime.Now;
                if (!string.IsNullOrEmpty(deviceName)) device.DeviceName = deviceName;
                _context.UserDevices.Update(device);
            }
            await _context.SaveChangesAsync();

            // 4. THIẾT LẬP COOKIE AUTHENTICATION (ĐĂNG NHẬP VĨNH VIỄN)
            var claims = new List<Claim>
    {
        new Claim(ClaimTypes.NameIdentifier, user.IdNguoiDung.ToString()),
        new Claim(ClaimTypes.Name, user.HoTen ?? ""),
        new Claim(ClaimTypes.Email, user.Tk ?? ""),
        new Claim("UserRole", user.VaiTro ?? ""),
        new Claim("PhongBan", user.PhongBan ?? "")
    };

            var claimsIdentity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);

            var authProperties = new AuthenticationProperties
            {
                // QUAN TRỌNG: Nếu tích 'Ghi nhớ', Cookie sẽ lưu trên ổ cứng (Persistent)
                IsPersistent = rememberMe,
                ExpiresUtc = rememberMe ? DateTimeOffset.UtcNow.AddDays(365) : null,
                AllowRefresh = true
            };

            // Đăng nhập hệ thống (Lưu Cookie)
            await HttpContext.SignInAsync(
                CookieAuthenticationDefaults.AuthenticationScheme,
                new ClaimsPrincipal(claimsIdentity),
                authProperties);

            // 5. Lưu email vào Cookie thường để hiện thị lần sau (nếu cần)
            if (rememberMe)
            {
                Response.Cookies.Append("RememberedEmail", email, new CookieOptions
                {
                    Expires = DateTime.Now.AddDays(30),
                    HttpOnly = true
                });
            }
            else
            {
                Response.Cookies.Delete("RememberedEmail");
            }

            // 6. Chuyển hướng thành công
            return Redirect("/menuA");
        }

        [HttpGet("/DonXetDuyet/DangXuat")]
        public async Task<IActionResult> DangXuat()
        {
            // Xóa Cookie xác thực
            await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);

            // Xóa sạch Session (nếu có dùng các biến khác)
            HttpContext.Session.Clear();

            return Redirect("/DonXetDuyet/DangNhap");
        }

        #endregion

        #region Phan quyen

        private bool HasAccess(params string[] allowedRoles)
        {
            // 1. Kiểm tra xem User đã đăng nhập (Authenticated) hay chưa thông qua Cookie
            if (User == null || !User.Identity.IsAuthenticated)
            {
                return false;
            }

            // 2. Lấy Role của người dùng từ Claims
            // Lưu ý: "UserRole" phải khớp với tên Claim bạn đặt lúc thực hiện lệnh SignIn
            var userRole = User.FindFirst("UserRole")?.Value ?? "";

            // 3. Nếu User có quyền "Admin" hoặc "All", thường thì sẽ cho phép truy cập mọi thứ
            if (userRole == "Admin" || userRole == "All")
            {
                return true;
            }

            // 4. Kiểm tra xem Role hiện tại có nằm trong danh sách được phép không
            return allowedRoles.Contains(userRole);
        }

        #endregion

        #region MAIL

        #region Mail - Tạo Mail
        [HttpGet("/Mail/TaoMail")]
        public IActionResult TaoMail()
        {
            // Kiểm tra quyền truy cập thông qua Claims
            if (!HasAccess("NhanVien", "QuanLy", "Admin", "All"))
            {
                TempData["Error"] = "Bạn không có quyền truy cập chức năng này.";
                return Redirect("/DonXetDuyet/DangNhap");
            }

            return View();
        }

        [HttpPost("/Mail/TaoMail")]
        public IActionResult TaoMail([FromBody] MailViewModel model)
        {
            // --- THAY ĐỔI: Lấy thông tin từ User Claims (Cookie) thay vì Session ---
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            string userName = User.Identity.Name ?? "";
            string PhongBan = User.FindFirst("PhongBan")?.Value ?? "";

            if (string.IsNullOrEmpty(userIdClaim))
                return Json(new { success = false, message = "Bạn chưa đăng nhập." });

            int userId = int.Parse(userIdClaim);

            // Kiểm tra quyền truy cập
            if (!HasAccess("NhanVien", "QuanLy", "Admin", "All"))
                return Json(new { success = false, message = "Bạn không có quyền truy cập." });

            // Kiểm tra dữ liệu rỗng
            if (string.IsNullOrWhiteSpace(model.TenForm) ||
                string.IsNullOrWhiteSpace(model.BoPhan) ||
                string.IsNullOrWhiteSpace(model.SoNhanVien) ||
                string.IsNullOrWhiteSpace(model.HoVaTen) ||
                string.IsNullOrWhiteSpace(model.Email) ||
                string.IsNullOrWhiteSpace(model.ViTri))
            {
                return Json(new { success = false, message = "Vui lòng điền đầy đủ thông tin bắt buộc." });
            }

            // Kiểm tra định dạng email
            if (!IsValidEmail(model.Email))
            {
                return Json(new { success = false, message = "Email không đúng định dạng." });
            }

            try
            {
                var mail = new Mail
                {
                    TenForm = model.TenForm,
                    Ngay = DateOnly.FromDateTime(DateTime.Now),
                    BoPhan = PhongBan,
                    SoNhanVien = model.SoNhanVien,
                    HoVaTen = model.HoVaTen,
                    Email = model.Email,
                    ViTri = model.ViTri,
                    SoNoiBo = model.SoNoiBo,
                    GuiRaNgoai = model.GuiRaNgoai,
                    SuDungTrenDienThoai = model.SuDungTrenDienThoai,
                    SuDungWedMail = model.SuDungWedMail,
                    MucDichSuDung = model.MucDichSuDung,
                    IdNguoiTao = userId,
                    TenNguoiTao = userName,
                    TimeTao = DateTime.Now
                };

                _context.Mail.Add(mail);
                _context.SaveChanges();

                return Json(new { success = true, message = "Tạo Mail thành công!", id = mail.Id });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = ex.Message });
            }
        }

        // Hàm kiểm tra định dạng email
        private bool IsValidEmail(string email)
        {
            if (string.IsNullOrWhiteSpace(email)) return false;
            try
            {
                var addr = new System.Net.Mail.MailAddress(email);
                return addr.Address == email;
            }
            catch
            {
                return false;
            }
        }

        public class MailViewModel
        {
            public string TenForm { get; set; }
            public string BoPhan { get; set; }
            public string SoNhanVien { get; set; }
            public string HoVaTen { get; set; }
            public string Email { get; set; }
            public string ViTri { get; set; }
            public string SoNoiBo { get; set; }
            public bool GuiRaNgoai { get; set; }
            public bool SuDungTrenDienThoai { get; set; }
            public bool SuDungWedMail { get; set; }
            public string MucDichSuDung { get; set; }
        }

        #endregion

        #region Admin Quan Ly Mail

        [HttpGet("/Mail/QuanLyMail")]
        public IActionResult QuanLyMail()
        {
            if (!HasAccess("Admin", "All"))
                return Redirect("/DonXetDuyet/KhongDuQuyen");

            var mails = _context.Mail
                .Where(m => m.IdNguoiDuyet.HasValue
                         && !string.IsNullOrEmpty(m.TenNguoiDuyet)
                         && !m.TenNguoiDuyet.Contains("Đơn đã hủy:")
                         && m.TimeNguoiDuyet.HasValue)
                .OrderByDescending(m => m.Id)
                .ToList();

            return View(mails);
        }

        [HttpGet("/Mail/QuanLyMail/ChiTiet/{id}")]
        public IActionResult QuanLyMailChiTiet(int id)
        {
            if (!HasAccess("Admin", "All"))
                return Redirect("/DonXetDuyet/KhongDuQuyen");

            // Admin xem được tất cả, không lọc theo phòng ban của admin
            var mail = _context.Mail.FirstOrDefault(m => m.Id == id);

            if (mail == null)
                return NotFound();

            return View(mail);
        }

        // POST Admin Hủy bỏ Mail
        [HttpPost("/Mail/A/HuyBoMail/{id}")]
        public IActionResult AHuyBoMail(int id)
        {
            if (!HasAccess("Admin", "All"))
                return Redirect("/DonXetDuyet/KhongDuQuyen");

            var mail = _context.Mail.FirstOrDefault(m => m.Id == id);
            if (mail == null)
                return NotFound();

            if (mail.IdAdmin != null)
            {
                TempData["Error"] = "Mail này đã được xử lý, không thể hủy bỏ lại!";
                return Redirect($"/Mail/QuanLyMail/ChiTiet/{id}");
            }

            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            string userName = User.Identity.Name ?? "";

            if (string.IsNullOrEmpty(userIdClaim))
                return Redirect("/DonXetDuyet/DangNhap");

            mail.IdAdmin = int.Parse(userIdClaim);
            mail.TenAdmin = "Đơn không đạt: " + userName;
            mail.TimeAdmin = DateTime.Now;

            _context.SaveChanges();

            TempData["Success"] = "Mail đã được hủy bỏ thành công!";
            return Redirect($"/Mail/QuanLyMail/ChiTiet/{id}");
        }

        // POST Admin Xác nhận Mail
        [HttpPost("/Mail/A/XacNhanMail/{id}")]
        public IActionResult AXacNhanMail(int id)
        {
            if (!HasAccess("Admin", "All"))
                return Redirect("/DonXetDuyet/KhongDuQuyen");

            var mail = _context.Mail.FirstOrDefault(m => m.Id == id);
            if (mail == null)
                return NotFound();

            if (mail.IdAdmin != null)
            {
                TempData["Error"] = "Mail này đã được xử lý, không thể xác nhận lại!";
                return Redirect($"/Mail/QuanLyMail/ChiTiet/{id}");
            }

            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            string userName = User.Identity.Name ?? "";

            if (string.IsNullOrEmpty(userIdClaim))
                return Redirect("/DonXetDuyet/DangNhap");

            mail.IdAdmin = int.Parse(userIdClaim);
            mail.TenAdmin = userName;
            mail.TimeAdmin = DateTime.Now;

            _context.SaveChanges();

            TempData["Success"] = "Mail đã được xác nhận thành công!";
            return Redirect($"/Mail/QuanLyMail/ChiTiet/{id}");
        }
        #endregion

        #region Mail đã xong

        [HttpGet("/Mail/DaHoanThanhMail")]
        public IActionResult DaHoanThanhMail()
        {
            if (!HasAccess("NhanVien", "QuanLy", "Admin", "All"))
                return Redirect("/DonXetDuyet/KhongDuQuyen");

            string phongBanHienTai = User.FindFirst("PhongBan")?.Value ?? "";

            var mails = _context.Mail
                .Where(m => m.IdNguoiDuyet.HasValue
                         && !string.IsNullOrEmpty(m.TenAdmin)
                         && !m.TenAdmin.Contains("Đơn không đạt:")
                         && m.TimeAdmin.HasValue
                         && m.BoPhan == phongBanHienTai)
                .OrderByDescending(m => m.Id)
                .ToList();

            return View(mails);
        }

        #endregion

        #endregion

        #region xử lý ảnh
        private string RemoveSign4VietnameseString(string str)
        {
            if (string.IsNullOrEmpty(str)) return "";
            string[] VietnameseSigns = new string[]
            {
        "aAeEoOuUiIdDyY",
        "áàạảãâấầậẩẫăắằặẳẵ",
        "ÁÀẠẢÃÂẤẦẬẨẪĂẮẰẶẲẴ",
        "éèẹẻẽêếềệểễ",
        "ÉÈẸẺẼÊẾỀỆỂỄ",
        "óòọỏõôốồộổỗơớờợởỡ",
        "ÓÒỌỎÕÔỐỒỘỔỖƠỚỜỢỞỠ",
        "úùụủũưứừựửữ",
        "ÚÙỤỦŨƯỨỪỰỬỮ",
        "íìịỉĩ",
        "ÍÌỊỈĨ",
        "đ",
        "Đ",
        "ýỳỵỷỹ",
        "ÝỲỴỶỸ"
            };
            for (int i = 1; i < VietnameseSigns.Length; i++)
            {
                for (int j = 0; j < VietnameseSigns[i].Length; j++)
                    str = str.Replace(VietnameseSigns[i][j], VietnameseSigns[0][i - 1]);
            }
            return str;
        }

        // HÀM HELPER - Xử lý chuyển đổi file sang byte[] (Dùng cho cột kiểu varbinary/image trong DB)
        private async Task<byte[]> GetFileBytesAsync2(string inputName)
        {
            try
            {
                // Kiểm tra xem trong request có file đính kèm với name tương ứng không
                if (Request.Form.Files.Count > 0)
                {
                    var file = Request.Form.Files[inputName];
                    if (file != null && file.Length > 0)
                    {
                        using var ms = new MemoryStream();
                        await file.CopyToAsync(ms);
                        return ms.ToArray();
                    }
                }
            }
            catch
            {
                return null;
            }
            return null;
        }
        #endregion

        #region Don Mail (Form IT)

        [HttpGet("/FormIT/DonMail")]
        public IActionResult DonMail()
        {
            if (User == null || !User.Identity.IsAuthenticated)
                return Redirect("/DonXetDuyet/DangNhap");

            // Lọc danh sách nhân viên IT và CHỈ lấy những công việc có tên "Đăng kí mail"
            ViewBag.ListNguoiHoTro = _context.ItNguoiHoTros
                .Include(x => x.CongViecs)
                .Where(x => x.BoPhan == "IT")
                .Select(x => new E_Form_Best.Models.ITForm.ItNguoiHoTro
                {
                    Id = x.Id,
                    MaNv = x.MaNv,
                    Ten = x.Ten,
                    BoPhan = x.BoPhan,
                    GhiChu = x.GhiChu,
                    // Chỉ nạp các công việc có tên chính xác là "Đăng kí mail"
                    CongViecs = x.CongViecs.Where(cv => cv.Ten == "Đăng kí mail").ToList()
                })
                .Where(x => x.CongViecs.Any()) // Chỉ lấy những người có công việc này
                .ToList();

            var userIdStr = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
            int? userId = !string.IsNullOrEmpty(userIdStr) ? int.Parse(userIdStr) : null;

            string userName = User.Identity.Name ?? "";
            string phongBan = User.FindFirst("PhongBan")?.Value ?? "";
            string userRole = User.FindFirst("UserRole")?.Value ?? "";
            string userEmail = User.FindFirst(System.Security.Claims.ClaimTypes.Email)?.Value ?? "";

            var model = new FormIt
            {
                TenNguoiNv = userName,
                BoPhan = phongBan,
                ViTri = userRole,
                SoNhanVien = userEmail,
                Ngay = DateOnly.FromDateTime(DateTime.Now),
                IdNguoiTao = userId,
                TenNguoiTao = userName,
                TimeNguoiTao = DateTime.Now,
                TrangThai = "ChoDuyet"
            };

            return View(model);
        }

        [HttpPost("/FormIT/DonMail")]
        public async Task<IActionResult> DonMail(FormIt form, [FromForm] ItMail1 itMail, int[] SelectedNguoiHoTroIds)
        {
            // Kiểm tra đăng nhập
            if (User == null || !User.Identity.IsAuthenticated)
                return Redirect("/DonXetDuyet/DangNhap");

            // Lấy thông tin User (sử dụng TryParse để an toàn hơn)
            var userIdStr = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
            int.TryParse(userIdStr, out int userId);

            var userName = User.Identity.Name ?? "";
            var phongBan = User.FindFirst("PhongBan")?.Value ?? "";
            var viTri = User.FindFirst("UserRole")?.Value ?? "";
            var userEmail = User.FindFirst(System.Security.Claims.ClaimTypes.Email)?.Value ?? "";

            using (var transaction = await _context.Database.BeginTransactionAsync())
            {
                try
                {
                    // --- BƯỚC 1: LƯU BẢNG CHÍNH (FORMIT) ---
                    form.Ngay = DateOnly.FromDateTime(DateTime.Now);
                    form.IdNguoiTao = userId;
                    form.TenNguoiTao = userName;
                    form.TimeNguoiTao = DateTime.Now;
                    form.TenNguoiNv = userName;
                    form.BoPhan = phongBan;
                    form.ViTri = viTri;
                    form.SoNhanVien = userEmail;
                    form.TrangThai = "ChoDuyet";
                    form.IdForm = "IT_MAIL_1";
                    form.TenForm = "Đơn đăng ký/sửa đổi Mail";

                    // Thêm danh mục theo yêu cầu riêng của bạn
                    form.Danhmuc = "Đăng kí mail";

                    _context.FormIts.Add(form);
                    await _context.SaveChangesAsync();

                    // --- BƯỚC 2: FILE ĐÍNH KÈM ---
                    var uploadFile = Request.Form.Files["UploadFile"];
                    if (uploadFile != null && uploadFile.Length > 0)
                    {
                        string folderName = "FileIT";
                        string wwwRootPath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", folderName);
                        if (!Directory.Exists(wwwRootPath)) Directory.CreateDirectory(wwwRootPath);

                        string extension = Path.GetExtension(uploadFile.FileName);
                        string safeName = RemoveSign4VietnameseString(userName).Replace(" ", "");
                        string timeStamp = DateTime.Now.ToString("ddMMyy_HHmmss");
                        string fileName = $"DonMail_ID{form.Id}_{safeName}_{timeStamp}{extension}";

                        string fullPath = Path.Combine(wwwRootPath, fileName);
                        using (var fileStream = new FileStream(fullPath, FileMode.Create))
                        {
                            await uploadFile.CopyToAsync(fileStream);
                        }
                        form.FileDinhKem = fileName;
                        _context.Entry(form).Property(x => x.FileDinhKem).IsModified = true;
                        await _context.SaveChangesAsync();
                    }

                    // --- BƯỚC 3: CHI TIẾT CẤU HÌNH MAIL ---
                    if (itMail != null)
                    {
                        itMail.IdFormIt = form.Id;
                        itMail.Anh = await GetFileBytesAsync2("Anh");
                        _context.ItMail1s.Add(itMail);
                        await _context.SaveChangesAsync();
                    }
                    // --- BƯỚC 4: LƯU NGƯỜI HỖ TRỢ ---
                    string danhSachTenHoTro = "Chưa chọn";

                    // PHẦN THÊM MỚI: Nếu là "Đăng kí mail", tự động tìm SelectedNguoiHoTroIds từ bảng CongViec
                    if (form.Danhmuc == "Đăng kí mail")
                    {
                        var idsTuCongViec = await _context.CongViecs
                            .Where(cv => cv.Ten == "Đăng kí mail" && cv.IdItNguoiHoTro != null)
                            .Select(cv => cv.IdItNguoiHoTro.Value)
                            .Distinct()
                            .ToArrayAsync();

                        // Gán lại cho SelectedNguoiHoTroIds để các bước sau xử lý như bình thường
                        if (idsTuCongViec.Any())
                        {
                            SelectedNguoiHoTroIds = idsTuCongViec;
                        }
                    }

                    // Giữ nguyên logic xử lý của bạn bên dưới
                    if (SelectedNguoiHoTroIds != null && SelectedNguoiHoTroIds.Length > 0)
                    {
                        // Lấy danh sách tên để ghi vào lịch sử cho chi tiết
                        var listHoTro = _context.ItNguoiHoTros
                            .Where(x => SelectedNguoiHoTroIds.Contains(x.Id))
                            .Select(x => x.Ten)
                            .ToList();
                        danhSachTenHoTro = string.Join(", ", listHoTro);

                        int stt = 1;
                        foreach (var idHoTro in SelectedNguoiHoTroIds)
                        {
                            var chiTietHoTro = new ItCtNguoiHoTro
                            {
                                IdFormIt = form.Id,
                                IdItNguoiHoTro = idHoTro,
                                Stt = stt++
                            };
                            _context.ItCtNguoiHoTros.Add(chiTietHoTro);
                        }
                        await _context.SaveChangesAsync();
                    }

                    // --- BƯỚC 5: LƯU LỊCH SỬ THAY ĐỔI (CHI TIẾT TỐI ĐA) ---
                    string emailDeXuat = itMail?.Email ?? "N/A";

                    string moTaChiTiet = $"[Khởi tạo đơn] Người tạo: {userName} (ID: {userId}) | Bộ phận: {phongBan}\n" +
                                         $"- Danh mục: {form.Danhmuc}\n" +
                                         $"- Email đề xuất: {emailDeXuat}\n" +
                                         $"- File đính kèm: {(string.IsNullOrEmpty(form.FileDinhKem) ? "Không có" : form.FileDinhKem)}\n" +
                                         $"- Người hỗ trợ chỉ định: {danhSachTenHoTro}.";

                    var lichSu = new LichSu
                    {
                        IdFormIt = form.Id,
                        TieuDe = "Khởi tạo đơn",
                        Mota = moTaChiTiet,
                        Time = DateTime.Now
                    };
                    _context.LichSus.Add(lichSu);
                    await _context.SaveChangesAsync();

                    await transaction.CommitAsync();
                    TempData["Success"] = "Gửi đơn đăng ký Mail thành công!";
                    return Redirect("/FormIT/DonCho");
                }
                catch (Exception ex)
                {
                    await transaction.RollbackAsync();
                    // Đảm bảo khi lỗi vẫn load lại được dữ liệu cho View
                    ViewBag.ListNguoiHoTro = _context.ItNguoiHoTros.Where(x => x.BoPhan == "IT").ToList();
                    ModelState.AddModelError("", "Lỗi trong quá trình lưu: " + ex.Message);
                    return View(form);
                }
            }
        }
        #endregion

        #region IT Order - Sửa chữa/Yêu cầu thiết bị (Form IT) - CẬP NHẬT LỊCH SỬ & NGƯỜI HỖ TRỢ

        [HttpGet("/FormIT/TaoIT_Order")]
        public async Task<IActionResult> TaoIT_Order()
        {
            // --- LẤY THÔNG TIN TỪ CLAIMS ---
            var userIdClaim = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userIdClaim)) return Redirect("/DonXetDuyet/DangNhap");

            int userId = int.Parse(userIdClaim);
            var userName = User.Identity.Name ?? "";
            var phongBan = User.FindFirst("PhongBan")?.Value ?? "";
            var userRole = User.FindFirst(System.Security.Claims.ClaimTypes.Role)?.Value
                            ?? User.FindFirst("UserRole")?.Value ?? "";
            var userEmail = User.FindFirst(System.Security.Claims.ClaimTypes.Email)?.Value ?? "";

            // CẬP NHẬT: Lấy danh sách công việc thay vì người hỗ trợ
            ViewBag.CongViecList = await _context.CongViecs
                                            .OrderBy(x => x.Ten)
                                            .ToListAsync();

            var model = new FormIt
            {
                TenNguoiNv = userName,
                BoPhan = phongBan,
                ViTri = userRole,
                SoNhanVien = userEmail,
                Ngay = DateOnly.FromDateTime(DateTime.Now),
                IdNguoiTao = userId,
                TenNguoiTao = userName,
                TimeNguoiTao = DateTime.Now,
                TrangThai = "ChoDuyet",
                IdForm = "IT_ORDER_2",
                TenForm = "Yêu cầu sửa chữa/Hỗ trợ kỹ thuật"
            };

            return View(model);
        }

        [HttpPost("/FormIT/TaoIT_Order")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> TaoIT_Order(FormIt form, [FromForm] ItOrderIt2 itOrder, int selectedCongViecId)
        {
            var userIdClaim = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userIdClaim)) return Redirect("/DonXetDuyet/DangNhap");

            int userId = int.Parse(userIdClaim);
            var userName = User.Identity.Name ?? "";
            var userEmail = User.FindFirst(System.Security.Claims.ClaimTypes.Email)?.Value ?? ""; var phongBan = User.FindFirst("PhongBan")?.Value ?? "";
            var viTri = User.FindFirst(System.Security.Claims.ClaimTypes.Role)?.Value
                        ?? User.FindFirst("UserRole")?.Value ?? "";

            using (var transaction = await _context.Database.BeginTransactionAsync())
            {
                try
                {
                    // --- 1. TRUY VẤN NGƯỜI HỖ TRỢ VÀ THÔNG TIN CÔNG VIỆC ---
                    var congViec = await _context.CongViecs
                        .Include(c => c.IdItNguoiHoTroNavigation)
                        .FirstOrDefaultAsync(c => c.Id == selectedCongViecId);

                    // --- 2. LƯU BẢNG CHÍNH (FormIt) ---
                    form.Ngay = DateOnly.FromDateTime(DateTime.Now);
                    form.IdNguoiTao = userId;
                    form.TenNguoiTao = userName;
                    form.TimeNguoiTao = DateTime.Now;
                    form.TenNguoiNv = userName;
                    form.BoPhan = phongBan;
                    form.ViTri = viTri;
                    form.SoNhanVien = userEmail;
                    form.TrangThai = "ChoDuyet";
                    form.IdForm = "IT_ORDER_2";
                    form.TenForm = "Yêu cầu sửa chữa/Hỗ trợ kỹ thuật";

                    // GÁN TÊN LOẠI CÔNG VIỆC VÀO TRƯỜNG DANHMUC NHƯ BẠN MUỐN
                    if (congViec != null)
                    {
                        form.Danhmuc = congViec.Ten;
                    }

                    _context.FormIts.Add(form);
                    await _context.SaveChangesAsync(); // Lưu để lấy form.Id cho các bảng sau

                    // --- 3. XỬ LÝ CHI TIẾT LỖI (ItOrderIt2) ---
                    if (itOrder != null)
                    {
                        itOrder.IdFormIt = form.Id;
                        // Nếu người dùng chọn công việc, ta có thể gán tên công việc vào trường Ten nếu nó đang trống
                        if (string.IsNullOrEmpty(itOrder.Ten) && congViec != null)
                        {
                            itOrder.Ten = congViec.Ten;
                        }
                        itOrder.Anh = await GetFileBytesAsync2("Anh");

                        _context.ItOrderIt2s.Add(itOrder);
                    }

                    // --- 4. XỬ LÝ FILE ĐÍNH KÈM ---
                    var uploadFile = Request.Form.Files["UploadFile"];
                    string fileLog = "Không có tệp đính kèm";

                    if (uploadFile != null && uploadFile.Length > 0)
                    {
                        string folderName = "FileIT";
                        string wwwRootPath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", folderName);
                        if (!Directory.Exists(wwwRootPath)) Directory.CreateDirectory(wwwRootPath);

                        string extension = Path.GetExtension(uploadFile.FileName);
                        string safeName = RemoveSign4VietnameseString(userName).Replace(" ", "");
                        string timeStamp = DateTime.Now.ToString("ddMMyy_HHmmss");
                        string fileName = $"DonOrder_ID{form.Id}_{safeName}_{timeStamp}{extension}";

                        string fullPath = Path.Combine(wwwRootPath, fileName);
                        using (var fileStream = new FileStream(fullPath, FileMode.Create))
                        {
                            await uploadFile.CopyToAsync(fileStream);
                        }

                        form.FileDinhKem = fileName;
                        _context.Entry(form).Property(x => x.FileDinhKem).IsModified = true;
                        fileLog = $"Đính kèm tệp: {fileName}";
                    }

                    // --- 5. LƯU NGƯỜI HỖ TRỢ (Truy vấn ngược từ bảng Công Việc) ---
                    string supporterLog = "Chưa gán người hỗ trợ";
                    if (congViec != null && congViec.IdItNguoiHoTro.HasValue)
                    {
                        var ctNguoiHoTro = new ItCtNguoiHoTro
                        {
                            IdFormIt = form.Id,
                            IdItNguoiHoTro = congViec.IdItNguoiHoTro.Value,
                            Stt = 1
                        };
                        _context.ItCtNguoiHoTros.Add(ctNguoiHoTro);

                        supporterLog = $"Đã tự động gán: {congViec.IdItNguoiHoTroNavigation?.Ten ?? "Nhân viên IT"} (Dựa trên loại công việc: {congViec.Ten})";
                    }

                    // --- 6. LƯU LỊCH SỬ CHI TIẾT ---
                    string tenThietBi = itOrder != null && !string.IsNullOrEmpty(itOrder.Ten) ? $" | Loại CV: {itOrder.Ten}" : "";

                    var lichSu = new LichSu
                    {
                        IdFormIt = form.Id,
                        TieuDe = "Khởi tạo yêu cầu",
                        Mota = $"Người tạo: {userName} (ID: {userId}){tenThietBi}. " +
                                $"Nội dung: Gửi yêu cầu hỗ trợ. {supporterLog}. {fileLog}.",
                        Time = DateTime.Now
                    };
                    _context.LichSus.Add(lichSu);

                    await _context.SaveChangesAsync();
                    await transaction.CommitAsync();

                    TempData["Success"] = "Gửi yêu cầu hỗ trợ IT thành công!";
                    return Redirect("/FormIT/DonCho");
                }
                catch (Exception ex)
                {
                    await transaction.RollbackAsync();
                    // Load lại danh sách công việc nếu lỗi để View không bị trống dropdown
                    ViewBag.CongViecList = await _context.CongViecs.OrderBy(x => x.Ten).ToListAsync();
                    ModelState.AddModelError("", "Hệ thống gặp lỗi: " + ex.Message);
                    return View(form);
                }
            }
        }
        #endregion

        #region IT Wifi - Đăng ký sử dụng Wifi (Form IT)

        [HttpGet("/FormIT/TaoIT_Wifi")]
        public async Task<IActionResult> TaoIT_Wifi()
        {
            var userIdClaim = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userIdClaim)) return Redirect("/DonXetDuyet/DangNhap");

            int userId = int.Parse(userIdClaim);
            var userName = User.Identity.Name ?? "";
            var phongBan = User.FindFirst("PhongBan")?.Value ?? "";
            var userRole = User.FindFirst(System.Security.Claims.ClaimTypes.Role)?.Value
                           ?? User.FindFirst("UserRole")?.Value ?? "";
            var userEmail = User.FindFirst(System.Security.Claims.ClaimTypes.Email)?.Value ?? "";
            // --- CẬP NHẬT: LẤY DANH SÁCH CÔNG VIỆC THAY VÌ NGƯỜI HỖ TRỢ ---
            ViewBag.CongViecList = await _context.CongViecs
                .Where(x => x.Ten.Contains("Đăng kí sử dụng wifi")) // Thêm điều kiện lọc theo tên
                .OrderBy(x => x.Ten)
                .ToListAsync();

            var model = new FormIt
            {
                TenNguoiNv = userName,
                BoPhan = phongBan,
                ViTri = userRole,
                SoNhanVien = userEmail,
                Ngay = DateOnly.FromDateTime(DateTime.Now),
                IdNguoiTao = userId,
                TenNguoiTao = userName,
                TimeNguoiTao = DateTime.Now,
                TrangThai = "ChoDuyet",
                IdForm = "IT_WIFI_3",
                TenForm = "Đăng ký sử dụng Wifi"
            };

            return View(model);
        }

        [HttpPost("/FormIT/TaoIT_Wifi")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> TaoIT_Wifi(FormIt form, [FromForm] ItDangKiSuDungWifi3 itWifi, int selectedCongViecId)
        {
            var userIdClaim = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userIdClaim)) return Redirect("/DonXetDuyet/DangNhap");

            int userId = int.Parse(userIdClaim);
            var userName = User.Identity.Name ?? "";
            var phongBan = User.FindFirst("PhongBan")?.Value ?? "";
            var viTri = User.FindFirst(System.Security.Claims.ClaimTypes.Role)?.Value
                        ?? User.FindFirst("UserRole")?.Value ?? "";
            var userEmail = User.FindFirst(System.Security.Claims.ClaimTypes.Email)?.Value ?? "";

            using (var transaction = await _context.Database.BeginTransactionAsync())
            {
                try
                {
                    // --- 1. TRUY VẤN NGƯỜI HỖ TRỢ VÀ THÔNG TIN CÔNG VIỆC ---
                    var congViec = await _context.CongViecs
                        .Include(c => c.IdItNguoiHoTroNavigation)
                        .FirstOrDefaultAsync(c => c.Id == selectedCongViecId);

                    // --- 2. LƯU BẢNG CHÍNH (FormIt) ---
                    form.Ngay = DateOnly.FromDateTime(DateTime.Now);
                    form.IdNguoiTao = userId;
                    form.TenNguoiTao = userName;
                    form.TimeNguoiTao = DateTime.Now;
                    form.TenNguoiNv = userName;
                    form.BoPhan = phongBan;
                    form.ViTri = viTri;
                    form.SoNhanVien = userEmail;
                    form.TrangThai = "ChoDuyet";
                    form.IdForm = "IT_WIFI_3";
                    form.TenForm = "Đăng ký sử dụng Wifi";

                    if (congViec != null)
                    {
                        form.Danhmuc = congViec.Ten; // Gán tên loại công việc vào danh mục
                    }

                    _context.FormIts.Add(form);
                    await _context.SaveChangesAsync();

                    // --- 3. XỬ LÝ FILE ĐÍNH KÈM (UploadFile) ---
                    var uploadFile = Request.Form.Files["UploadFile"];
                    string fileLog = "Không có tệp đính kèm";
                    if (uploadFile != null && uploadFile.Length > 0)
                    {
                        string folderName = "FileIT";
                        string wwwRootPath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", folderName);
                        if (!Directory.Exists(wwwRootPath)) Directory.CreateDirectory(wwwRootPath);

                        string extension = Path.GetExtension(uploadFile.FileName);
                        string safeName = RemoveSign4VietnameseString(userName).Replace(" ", "");
                        string timeStamp = DateTime.Now.ToString("ddMMyy_HHmmss");
                        string fileName = $"DonWifi_ID{form.Id}_{safeName}_{timeStamp}{extension}";

                        string fullPath = Path.Combine(wwwRootPath, fileName);
                        using (var fileStream = new FileStream(fullPath, FileMode.Create))
                        {
                            await uploadFile.CopyToAsync(fileStream);
                        }
                        form.FileDinhKem = fileName;
                        _context.Entry(form).Property(x => x.FileDinhKem).IsModified = true;
                        await _context.SaveChangesAsync();
                        fileLog = $"Đính kèm tệp: {fileName}";
                    }

                    // --- 4. CHI TIẾT WIFI (ItDangKiSuDungWifi3) ---
                    if (itWifi != null)
                    {
                        itWifi.IdFormIt = form.Id;
                        itWifi.Anh = await GetFileBytesAsync2("Anh"); // Ảnh MAC Address từ input "Anh"
                        _context.ItDangKiSuDungWifi3s.Add(itWifi);
                        await _context.SaveChangesAsync();
                    }

                    // --- 5. LƯU NGƯỜI HỖ TRỢ (Tự động gán từ Công Việc) ---
                    string supporterLog = "Chưa gán người hỗ trợ";
                    if (congViec != null && congViec.IdItNguoiHoTro.HasValue)
                    {
                        var ctNguoiHoTro = new ItCtNguoiHoTro
                        {
                            IdFormIt = form.Id,
                            IdItNguoiHoTro = congViec.IdItNguoiHoTro.Value,
                            Stt = 1
                        };
                        _context.ItCtNguoiHoTros.Add(ctNguoiHoTro);
                        supporterLog = $"Đã tự động gán: {congViec.IdItNguoiHoTroNavigation?.Ten ?? "Nhân viên IT"} (Dựa trên loại CV: {congViec.Ten})";
                    }

                    // --- 6. LƯU LỊCH SỬ CHI TIẾT ---
                    var lichSu = new LichSu
                    {
                        IdFormIt = form.Id,
                        TieuDe = "Khởi tạo đơn Wifi",
                        Mota = $"Người tạo: {userName} (ID: {userId}) | Thiết bị: {itWifi?.LoaiThietBi} | MAC: {itWifi?.MacTb}. " +
                                $"{supporterLog}. {fileLog}.",
                        Time = DateTime.Now
                    };
                    _context.LichSus.Add(lichSu);

                    await _context.SaveChangesAsync();
                    await transaction.CommitAsync();

                    TempData["Success"] = "Gửi yêu cầu đăng ký Wifi thành công!";
                    return Redirect("/FormIT/DonCho");
                }
                catch (Exception ex)
                {
                    await transaction.RollbackAsync();
                    // Load lại danh sách công việc để View không lỗi dropdown
                    ViewBag.CongViecList = await _context.CongViecs.OrderBy(x => x.Ten).ToListAsync();
                    ModelState.AddModelError("", "Lỗi hệ thống: " + ex.Message);
                    return View(form);
                }
            }
        }

        #endregion

        #region Đơn Đăng ký Điện thoại bàn (Form IT 4) - SỬ DỤNG CK

        [HttpGet("/FormIT/DonDienThoaiBan")]
        public async Task<IActionResult> DonDienThoaiBan()
        {
            // --- LẤY THÔNG TIN TỪ CLAIMS (CK) ---
            var userIdClaim = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userIdClaim)) return Redirect("/DonXetDuyet/DangNhap");

            int userId = int.Parse(userIdClaim);
            var userName = User.Identity.Name ?? "";
            var phongBan = User.FindFirst("PhongBan")?.Value ?? "";
            var userRole = User.FindFirst(System.Security.Claims.ClaimTypes.Role)?.Value
                           ?? User.FindFirst("UserRole")?.Value ?? "";
            var userEmail = User.FindFirst(System.Security.Claims.ClaimTypes.Email)?.Value ?? "";

            // --- LẤY DANH SÁCH CÔNG VIỆC (Đổi từ chọn người hỗ trợ sang chọn công việc) ---
            ViewBag.CongViecList = await _context.CongViecs
                .Where(x => x.Ten.Contains("Đăng kí sử dụng điện thoại"))
                .OrderBy(x => x.Ten)
                .ToListAsync();

            // Khởi tạo Model bảng chính
            var model = new FormIt
            {
                TenNguoiNv = userName,
                BoPhan = phongBan,
                ViTri = userRole,
                SoNhanVien = userEmail,
                Ngay = DateOnly.FromDateTime(DateTime.Now),
                IdNguoiTao = userId,
                TenNguoiTao = userName,
                TimeNguoiTao = DateTime.Now,
                TrangThai = "ChoDuyet"
            };

            return View(model);
        }

        [HttpPost("/FormIT/DonDienThoaiBan")]
        public async Task<IActionResult> DonDienThoaiBan(FormIt form, [FromForm] ItDangKiSuDungDtban4 itDtBan, int SelectedCongViecId)
        {
            // --- LẤY THÔNG TIN TỪ CLAIMS (CK) ---
            var userIdClaim = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userIdClaim)) return Redirect("/DonXetDuyet/DangNhap");

            int userId = int.Parse(userIdClaim);
            var userName = User.Identity.Name ?? "";
            var phongBan = User.FindFirst("PhongBan")?.Value ?? "";
            var viTri = User.FindFirst(System.Security.Claims.ClaimTypes.Role)?.Value
                        ?? User.FindFirst("UserRole")?.Value ?? "";
            var userEmail = User.FindFirst(System.Security.Claims.ClaimTypes.Email)?.Value ?? "";

            using (var transaction = await _context.Database.BeginTransactionAsync())
            {
                try
                {
                    // --- TRUY VẤN THÔNG TIN CÔNG VIỆC VÀ NGƯỜI HỖ TRỢ LIÊN QUAN ---
                    var congViec = await _context.CongViecs
                        .Include(c => c.IdItNguoiHoTroNavigation)
                        .FirstOrDefaultAsync(c => c.Id == SelectedCongViecId);

                    // --- BƯỚC 1: LƯU BẢNG CHÍNH FormIt ---
                    form.Ngay = DateOnly.FromDateTime(DateTime.Now);
                    form.IdNguoiTao = userId;
                    form.TenNguoiTao = userName;
                    form.TimeNguoiTao = DateTime.Now;
                    form.TenNguoiNv = userName;
                    form.BoPhan = phongBan;
                    form.ViTri = viTri;
                    form.SoNhanVien = userEmail;
                    form.TrangThai = "ChoDuyet";
                    form.IdForm = "IT_DT_BAN_4";
                    form.TenForm = "Đơn đăng ký sử dụng điện thoại bàn";

                    // Gán tên công việc vào trường Danhmuc
                    if (congViec != null)
                    {
                        form.Danhmuc = congViec.Ten;
                    }

                    _context.FormIts.Add(form);
                    await _context.SaveChangesAsync();

                    // --- BƯỚC 2: XỬ LÝ FILE ĐÍNH KÈM (Giữ nguyên logic của bạn) ---
                    var uploadFile = Request.Form.Files["UploadFile"];
                    if (uploadFile != null && uploadFile.Length > 0)
                    {
                        string folderName = "FileIT";
                        string wwwRootPath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", folderName);
                        if (!Directory.Exists(wwwRootPath)) Directory.CreateDirectory(wwwRootPath);

                        string extension = Path.GetExtension(uploadFile.FileName);
                        string safeName = RemoveSign4VietnameseString(userName).Replace(" ", "");
                        string timeStamp = DateTime.Now.ToString("ddMMyy_HHmmss");

                        string fileName = $"DonDTBan_ID{form.Id}_{safeName}_{timeStamp}{extension}";
                        string fullPath = Path.Combine(wwwRootPath, fileName);

                        using (var fileStream = new FileStream(fullPath, FileMode.Create))
                        {
                            await uploadFile.CopyToAsync(fileStream);
                        }

                        form.FileDinhKem = fileName;
                        _context.Entry(form).Property(x => x.FileDinhKem).IsModified = true;
                        await _context.SaveChangesAsync();
                    }

                    // --- BƯỚC 3: LƯU CHI TIẾT (ItDangKiSuDungDtban4) ---
                    if (itDtBan != null)
                    {
                        itDtBan.IdFormIt = form.Id;
                        itDtBan.Anh = await GetFileBytesAsync2("Anh");

                        _context.ItDangKiSuDungDtban4s.Add(itDtBan);
                        await _context.SaveChangesAsync();
                    }

                    // --- BƯỚC 4: LƯU NGƯỜI HỖ TRỢ (Tự động lưu từ công việc đã chọn) ---
                    if (congViec != null && congViec.IdItNguoiHoTroNavigation != null)
                    {
                        var chiTietHoTro = new ItCtNguoiHoTro
                        {
                            IdFormIt = form.Id,
                            IdItNguoiHoTro = congViec.IdItNguoiHoTroNavigation.Id,
                            Stt = 1
                        };
                        _context.ItCtNguoiHoTros.Add(chiTietHoTro);
                        await _context.SaveChangesAsync();
                    }

                    // --- BƯỚC 5: LƯU LỊCH SỬ ---
                    var lichSu = new LichSu
                    {
                        IdFormIt = form.Id,
                        TieuDe = "Khởi tạo đơn Điện thoại bàn",
                        Mota = $"Người thao tác: {userName} ({userEmail}). " +
                               $"Công việc: {form.Danhmuc}. " +
                               $"Nội dung: {itDtBan?.ThongTin}. " +
                               $"Trạng thái: Khởi tạo đơn (Chờ duyệt).",
                        Time = DateTime.Now
                    };
                    _context.LichSus.Add(lichSu);
                    await _context.SaveChangesAsync();

                    await transaction.CommitAsync();
                    TempData["Success"] = "Gửi đơn đăng ký điện thoại bàn thành công!";
                    return Redirect("/FormIT/DonCho");
                }
                catch (Exception ex)
                {
                    await transaction.RollbackAsync();
                    // Load lại danh sách công việc nếu lỗi
                    ViewBag.CongViecList = await _context.CongViecs
                        .Where(x => x.Ten.Contains("Đăng kí sử dụng điện thoại"))
                        .ToListAsync();
                    ModelState.AddModelError("", "Lỗi hệ thống: " + ex.Message);
                    return View(form);
                }
            }
        }

        #endregion

        #region CHI TIẾT ĐƠN FORM IT (TẤT CẢ LOẠI ĐƠN)
        [HttpGet("/FormIT/ChiTiet/{id}")]
        public async Task<IActionResult> ChiTiet(int id)
        {
            if (User == null || !User.Identity.IsAuthenticated)
            {
                return RedirectToAction("DangNhap", "DonXetDuyet");
            }

            var userIdStr = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userIdStr))
            {
                return RedirectToAction("DangNhap", "DonXetDuyet");
            }
            int userId = int.Parse(userIdStr);

            var don = await _context.FormIts
                .Include(f => f.ItMail1s)
                .Include(f => f.ItOrderIt2s)
                .Include(f => f.ItDangKiSuDungWifi3s)
                .Include(f => f.ItDangKiSuDungDtban4s)
                .Include(f => f.ItCtNguoiHoTros)
                    .ThenInclude(ct => ct.IdItNguoiHoTroNavigation)
                .Include(f => f.LichSus)
                .Include(f => f.DanhGia) // 🔴 THÊM DÒNG NÀY
                .FirstOrDefaultAsync(m => m.Id == id);

            if (don == null)
            {
                TempData["Error"] = "⚠️ Không tìm thấy đơn yêu cầu IT!";
                return RedirectToAction("DonCho");
            }

            var userRole = User.FindFirst(System.Security.Claims.ClaimTypes.Role)?.Value
                           ?? User.FindFirst("UserRole")?.Value ?? "";

            var userPhongBan = User.FindFirst("PhongBan")?.Value
                               ?? User.FindFirst(System.Security.Claims.ClaimTypes.Locality)?.Value ?? "";

            bool isOwner = don.IdNguoiTao == userId;
            bool isAssignedSupporter = don.ItCtNguoiHoTros.Any(x => x.IdItNguoiHoTroNavigation?.MaNv == User.Identity.Name);
            bool isPrivilegedUser = userRole == "Admin" || userRole == "QuanLy" || userRole == "All" || isAssignedSupporter;

            if (!isOwner && !isPrivilegedUser) return Forbid();

            if (don.LichSus != null)
            {
                don.LichSus = don.LichSus.OrderByDescending(x => x.Time).ToList();
            }

            ViewBag.ListNguoiHoTro = await _context.ItNguoiHoTros
                                             .Where(x => x.BoPhan == "IT")
                                             .ToListAsync();

            ViewBag.UserPhongBan = userPhongBan;
            ViewBag.CurrentUserId = userId; // 🔴 QUAN TRỌNG

            return View(don);
        }

        [HttpPost("/FormIT/ThemNguoiHoTro")]
        public async Task<IActionResult> ThemNguoiHoTro([FromBody] System.Text.Json.JsonElement data)
        {
            try
            {
                // Sử dụng GetProperty để đọc dữ liệu từ JsonElement, tránh lỗi RuntimeBinderException
                int idForm = data.GetProperty("idFormIt").GetInt32();
                string maNvMoi = data.GetProperty("maNv").GetString();

                // 1. Kiểm tra nhân viên IT mới có tồn tại không
                var nvIt = await _context.ItNguoiHoTros.FirstOrDefaultAsync(x => x.MaNv == maNvMoi);
                if (nvIt == null)
                {
                    return Json(new { success = false, message = "Mã nhân viên IT không tồn tại!" });
                }

                // 2. Lấy người hỗ trợ cuối cùng hiện tại (người có STT lớn nhất)
                var hienTai = await _context.ItCtNguoiHoTros
                    .Include(x => x.IdItNguoiHoTroNavigation)
                    .Where(x => x.IdFormIt == idForm)
                    .OrderByDescending(x => x.Stt)
                    .FirstOrDefaultAsync();

                // 3. Logic chặn: Kiểm tra nếu trùng với người cuối cùng được thêm
                if (hienTai != null && hienTai.IdItNguoiHoTroNavigation?.MaNv == maNvMoi)
                {
                    return Json(new
                    {
                        success = false,
                        message = $"Nhân viên {nvIt.Ten} hiện đang là người xử lý cuối cùng. Vui lòng chọn người khác!"
                    });
                }

                int sttMoi = (hienTai?.Stt ?? 0) + 1;

                // 4. Lưu thông tin mới (Giữ nguyên toàn bộ các phần hiện có)
                var ctMoi = new ItCtNguoiHoTro
                {
                    IdFormIt = idForm,
                    IdItNguoiHoTro = nvIt.Id,
                    Stt = sttMoi
                };
                _context.ItCtNguoiHoTros.Add(ctMoi);

                var lichSu = new LichSu
                {
                    IdFormIt = idForm,
                    TieuDe = "CHỈ ĐỊNH NGƯỜI HỖ TRỢ MỚI",
                    Mota = $"Nhân viên {User.Identity.Name} đã thay đổi người hỗ trợ sang: {nvIt.Ten} ({nvIt.MaNv}).",
                    Time = DateTime.Now
                };
                _context.LichSus.Add(lichSu);

                await _context.SaveChangesAsync();
                return Json(new { success = true, message = "Đã cập nhật người hỗ trợ mới!" });
            }
            catch (Exception ex) { return Json(new { success = false, message = "Lỗi: " + ex.Message }); }
        }

        #endregion

        #region ĐƠN CHỜ XÉT DUYỆT (cho nhân viên tạo form)

        // GET: /FormIT/DonCho
        [HttpGet("/FormIT/DonCho")]
        public async Task<IActionResult> DonCho()
        {
            // --- BƯỚC 1: KIỂM TRA ĐĂNG NHẬP QUA COOKIE (CK) ---
            if (User == null || !User.Identity.IsAuthenticated)
            {
                return Redirect("/DonXetDuyet/DangNhap");
            }

            // --- BƯỚC 2: LẤY USERID TỪ CLAIMS ---
            var userIdStr = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;

            if (string.IsNullOrEmpty(userIdStr))
            {
                return Redirect("/DonXetDuyet/DangNhap");
            }

            int userId = int.Parse(userIdStr);

            // --- BƯỚC 3: TRUY VẤN DỮ LIỆU ---
            // Cần: using Microsoft.EntityFrameworkCore;
            var danhSachDon = await _context.FormIts
                .Where(f => f.IdNguoiTao == userId)
                .Include(f => f.ItMail1s)
                // Lấy danh sách chi tiết người hỗ trợ và thông tin nhân viên từ bảng liên kết (Navigation)
                .Include(f => f.ItCtNguoiHoTros)
                    .ThenInclude(ct => ct.IdItNguoiHoTroNavigation)
                .OrderByDescending(f => f.Id)
                .ToListAsync();

            // Trả về View cùng danh sách đơn của chính người dùng đó
            return View(danhSachDon);
        }

        #endregion

        #region XỬ LÝ ĐƠN IT (Duyệt / Hủy / Hoàn tất) - SỬ DỤNG COOKIE AUTH (CK)

        [HttpGet("/FormIT/QuanLyXetDuyet")]
        public async Task<IActionResult> QuanLyXetDuyet()
        {
            // --- 1. LẤY THÔNG TIN TỪ CLAIMS ---
            var userIdStr = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userIdStr)) return Redirect("/DonXetDuyet/DangNhap");
            int userId = int.Parse(userIdStr);

            var userRole = User.FindFirst(System.Security.Claims.ClaimTypes.Role)?.Value
                           ?? User.FindFirst("UserRole")?.Value ?? "";
            var phongBan = User.FindFirst("PhongBan")?.Value?.Trim() ?? "";

            // Bảo vệ: Nếu là Bảo vệ thì không có quyền vào trang xét duyệt
            if (userRole == "BaoVe") return Forbid();

            // --- 2. TRUY VẤN DỮ LIỆU KÈM NGƯỜI HỖ TRỢ ---
            IQueryable<FormIt> query = _context.FormIts
                .Include(f => f.ItCtNguoiHoTros)
                    .ThenInclude(ct => ct.IdItNguoiHoTroNavigation); // Lấy Navigation để có tên IT

            // --- 3. LOGIC PHÂN QUYỀN TRUY VẤN (Giữ nguyên của bạn) ---
            if (userRole == "All")
            {
                // Xem toàn bộ đơn IT
            }
            else if (userRole == "Admin" || userRole == "QuanLy")
            {
                if (!string.IsNullOrEmpty(phongBan))
                {
                    query = query.Where(f => f.BoPhan != null && f.BoPhan.Trim().ToLower() == phongBan.ToLower());
                }
                else
                {
                    query = query.Where(f => false);
                }
            }
            else
            {
                query = query.Where(f => f.IdNguoiTao == userId);
            }

            var danhSachDon = await query
                .OrderByDescending(f => f.Id)
                .AsNoTracking()
                .ToListAsync();

            return View(danhSachDon);
        }

        [HttpPost("/FormIT/XuLyDon")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> XuLyDon([FromBody] ITApprovalRequest request)
        {
            // 1. Kiểm tra đầu vào cơ bản
            if (request == null || request.Id <= 0)
            {
                return Json(new { success = false, message = "Dữ liệu yêu cầu không hợp lệ." });
            }

            // 2. Lấy thông tin chi tiết người thao tác từ Claims
            var userIdStr = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userIdStr))
            {
                return Json(new { success = false, message = "Phiên đăng nhập đã hết hạn. Vui lòng đăng nhập lại." });
            }

            int userId = int.Parse(userIdStr);
            var userName = User.Identity.Name ?? "N/A";
            var userEmail = User.FindFirst(System.Security.Claims.ClaimTypes.Email)?.Value ?? "N/A";
            var userRole = User.FindFirst(System.Security.Claims.ClaimTypes.Role)?.Value
                           ?? User.FindFirst("UserRole")?.Value ?? "N/A";
            var phongBan = User.FindFirst("PhongBan")?.Value ?? "N/A";

            // 3. Tìm đơn trong cơ sở dữ liệu
            var form = await _context.FormIts.FindAsync(request.Id);
            if (form == null)
            {
                return Json(new { success = false, message = "Không tìm thấy đơn yêu cầu trong hệ thống." });
            }

            // 4. Kiểm tra logic trạng thái (Đơn đã hoàn thành hoặc đã hủy thì không cho sửa)
            bool isAlreadyCancelled = form.TenForm != null && form.TenForm.Contains("[ĐÃ HỦY]");
            bool isFinished = form.IdAdmin != null || form.TrangThai == "HoanTat";

            if (isAlreadyCancelled || isFinished)
            {
                return Json(new { success = false, message = "Đơn này đã kết thúc xử lý hoặc đã hủy trước đó." });
            }

            // 5. Thực hiện xử lý trong Transaction để đảm bảo tính toàn vẹn
            using (var transaction = await _context.Database.BeginTransactionAsync())
            {
                try
                {
                    DateTime now = DateTime.Now;
                    string tieuDeLichSu = "";
                    string moTaChiTiet = "";

                    // --- TRƯỜNG HỢP DUYỆT ĐƠN ---
                    if (request.Action == "Duyet")
                    {
                        // Kiểm tra quyền (Chỉ Manager hoặc IT Admin mới được duyệt bước này)
                        if (userRole != "QuanLy" && userRole != "Admin" && userRole != "All")
                        {
                            return Json(new { success = false, message = "Bạn không có quyền phê duyệt đơn này." });
                        }

                        form.IdNguoiDuyet = userId;
                        form.TenNguoiDuyet = userName;
                        form.TimeNguoiDuyet = now;
                        form.TrangThai = "DaDuyet";

                        tieuDeLichSu = "Phê duyệt đơn IT";
                        moTaChiTiet = $"Người duyệt: {userName} ({userEmail}). " +
                                      $"Bộ phận: {phongBan}. " +
                                      $"Trạng thái: Đã duyệt, chờ IT tiếp nhận xử lý.";
                    }
                    // --- TRƯỜNG HỢP HỦY ĐƠN ---
                    else if (request.Action == "Huy")
                    {
                        // Cập nhật trạng thái và đánh dấu tiêu đề
                        form.TrangThai = "DaHuy";
                        if (form.TenForm != null && !form.TenForm.StartsWith("[ĐÃ HỦY]"))
                        {
                            form.TenForm = "[ĐÃ HỦY] " + form.TenForm;
                        }

                        tieuDeLichSu = "Hủy đơn IT";
                        // Lưu đầy đủ thông tin người hủy vào phần mô tả lịch sử
                        moTaChiTiet = $"Người hủy: {userName} ({userEmail}). " +
                                      $"Bộ phận: {phongBan}. " +
                                      $"Quyền hạn: {userRole}. " +
                                      $"Lý do hủy: {request.Reason ?? "Không có lý do cụ thể"}.";
                    }
                    else
                    {
                        return Json(new { success = false, message = "Hành động yêu cầu không hợp lệ." });
                    }

                    // 6. Ghi nhật ký vào bảng LichSu
                    var lichSu = new LichSu
                    {
                        IdFormIt = form.Id,
                        TieuDe = tieuDeLichSu,
                        Mota = moTaChiTiet,
                        Time = now
                    };

                    _context.LichSus.Add(lichSu);

                    // 7. Lưu thay đổi và kết thúc Transaction
                    await _context.SaveChangesAsync();
                    await transaction.CommitAsync();

                    return Json(new { success = true, message = $"Đã {(request.Action == "Duyet" ? "phê duyệt" : "hủy")} đơn thành công." });
                }
                catch (Exception ex)
                {
                    await transaction.RollbackAsync();
                    return Json(new { success = false, message = "Lỗi hệ thống khi xử lý: " + ex.Message });
                }
            }
        }

        #endregion

        #region QUẢN LÝ XÉT DUYỆT IT (Admin IT, All, Quản lý bộ phận) - SỬ DỤNG COOKIE AUTH (CK)

        [HttpGet("/FormIT/HoanTatDon")]
        public async Task<IActionResult> HoanTatDon()
        {
            // --- 1. LẤY THÔNG TIN TỪ CLAIMS (CK) ---
            var userIdStr = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userIdStr))
                return Redirect("/DonXetDuyet/DangNhap");

            int userId = int.Parse(userIdStr);
            var userRole = User.FindFirst(System.Security.Claims.ClaimTypes.Role)?.Value
                           ?? User.FindFirst("UserRole")?.Value ?? "";
            var phongBanSession = User.FindFirst("PhongBan")?.Value?.Trim() ?? "";

            // --- 2. KHỞI TẠO QUERY ---
            // Giữ nguyên logic: Chỉ lấy những đơn đã có Người Duyệt (IdNguoiDuyet != null)
            // Thêm Include để lấy đầy đủ thông tin danh sách người hỗ trợ và Navigation liên quan
            IQueryable<FormIt> query = _context.FormIts
                .Include(f => f.ItCtNguoiHoTros)
                    .ThenInclude(ct => ct.IdItNguoiHoTroNavigation)
                .Where(f => f.IdNguoiDuyet != null && f.TenNguoiDuyet != null);

            // --- 3. PHÂN QUYỀN LỌC DỮ LIỆU (Giữ nguyên tuyệt đối logic của bạn) ---
            if (userRole == "All" || userRole == "Admin")
            {
                // Quyền Admin/All: Thấy đơn của phòng IT hoặc các đơn đã có Admin xử lý (hoàn tất)
                string IT_Dept_Name = "Phòng thông tin 资讯科技部";
                query = query.Where(f => f.BoPhan == IT_Dept_Name || f.IdAdmin != null);
            }
            else if (userRole == "QuanLy")
            {
                // Quyền Quản Lý: Chỉ thấy đơn thuộc bộ phận mình quản lý
                if (!string.IsNullOrEmpty(phongBanSession))
                {
                    query = query.Where(f => f.BoPhan != null &&
                                            f.BoPhan.Trim().ToLower() == phongBanSession.ToLower());
                }
                else
                {
                    // Nếu không có thông tin phòng ban trong Session/Cookie thì không trả về dữ liệu
                    query = query.Where(f => false);
                }
            }
            else
            {
                // Quyền Nhân Viên: Chỉ thấy đơn do chính mình tạo ra
                query = query.Where(f => f.IdNguoiTao == userId);
            }

            // --- 4. THỰC THI TRUY VẤN ---
            // Sắp xếp theo thời gian duyệt mới nhất lên đầu
            var danhSachDon = await query
                .OrderByDescending(f => f.TimeNguoiDuyet)
                .AsNoTracking() // Tăng hiệu năng cho tác vụ chỉ đọc
                .ToListAsync();

            // Trả về View kèm theo danh sách dữ liệu
            return View(danhSachDon);
        }
        // Các hàm XacNhanHoanThanh và class Request giữ nguyên như bạn đã viết

        // Nút bấm dành cho Đội IT - Xác nhận đã sửa xong/cấp xong thiết bị
        [HttpPost("/FormIT/XacNhanHoanThanh")]
        [ValidateAntiForgeryToken] // Thêm bảo mật CSRF
        public async Task<IActionResult> XacNhanHoanThanh([FromBody] ITApprovalRequest request)
        {
            // 1. Kiểm tra đầu vào cơ bản
            if (request == null || request.Id <= 0)
            {
                return Json(new { success = false, message = "Dữ liệu yêu cầu không hợp lệ." });
            }

            // 2. Lấy thông tin đầy đủ của người thao tác (IT Admin) từ Claims
            var userRole = User.FindFirst(System.Security.Claims.ClaimTypes.Role)?.Value
                          ?? User.FindFirst("UserRole")?.Value ?? "";
            var userIdStr = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
            var userName = User.Identity.Name ?? "N/A";
            var userEmail = User.FindFirst(System.Security.Claims.ClaimTypes.Email)?.Value ?? "N/A";
            var phongBan = User.FindFirst("PhongBan")?.Value ?? "N/A";

            // 3. Kiểm tra quyền hạn và phiên đăng nhập
            if (string.IsNullOrEmpty(userIdStr))
            {
                return Json(new { success = false, message = "Phiên đăng nhập đã hết hạn. Vui lòng đăng nhập lại." });
            }

            // Chỉ IT Admin hoặc Role "All" mới được chốt đơn
            if (userRole != "Admin" && userRole != "All")
            {
                return Json(new { success = false, message = "Chỉ bộ phận IT mới có quyền xác nhận hoàn thành đơn này." });
            }

            // 4. Tìm đơn IT trong cơ sở dữ liệu
            var form = await _context.FormIts.FindAsync(request.Id);
            if (form == null)
            {
                return Json(new { success = false, message = "Không tìm thấy đơn IT trong hệ thống." });
            }

            // Kiểm tra nếu đơn đã hủy hoặc đã hoàn thành trước đó để tránh lặp thao tác
            if (form.TrangThai == "HoanTat" || (form.TenForm != null && form.TenForm.Contains("[ĐÃ HỦY]")))
            {
                return Json(new { success = false, message = "Đơn này đã được xử lý xong hoặc đã hủy, không thể thao tác lại." });
            }

            // 5. Bắt đầu Transaction để đảm bảo an toàn dữ liệu
            using (var transaction = await _context.Database.BeginTransactionAsync())
            {
                try
                {
                    DateTime now = DateTime.Now;

                    // 6. Cập nhật thông tin IT xử lý vào bảng chính (FormIts)
                    form.IdAdmin = int.Parse(userIdStr);
                    form.TenAdmin = userName;
                    form.TimeAdmin = now;
                    form.TrangThai = "HoanTat"; // Trạng thái cuối cùng của quy trình

                    // 7. Lưu Lịch sử thao tác (LichSu)
                    var lichSu = new LichSu
                    {
                        IdFormIt = form.Id,
                        TieuDe = "IT Xác nhận Hoàn tất",
                        Mota = $"Người thực hiện: {userName} ({userEmail}). " +
                               $"Bộ phận: {phongBan}. " +
                               $"Quyền hạn: {userRole}. " +
                               $"Nội dung: Đã xử lý xong yêu cầu và đóng đơn.",
                        Time = now
                    };

                    _context.LichSus.Add(lichSu);

                    // 8. Lưu các thay đổi vào DB
                    await _context.SaveChangesAsync();

                    // Commit Transaction thành công
                    await transaction.CommitAsync();

                    return Json(new { success = true, message = "Xác nhận hoàn tất đơn IT thành công!" });
                }
                catch (Exception ex)
                {
                    // Rollback nếu có lỗi xảy ra
                    await transaction.RollbackAsync();

                    // Bạn có thể log ex ở đây nếu cần (Serilog, NLog...)
                    return Json(new { success = false, message = "Lỗi hệ thống khi lưu dữ liệu: " + ex.Message });
                }
            }
        }
        public class ITApprovalRequest
        {
            public int Id { get; set; }
            public string Action { get; set; } // "Duyet" hoặc "Huy"
            public string Reason { get; set; }
        }

        public class ITCompleteRequest
        {
            public int Id { get; set; }
        }
        #endregion

        #region Xác nhận chưa hoàn thành 
        [HttpPost("/FormIT/XacNhanChuaHoanThanh")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> XacNhanChuaHoanThanh([FromBody] ITNotCompleteRequest request)
        {
            if (request == null || request.Id <= 0 || string.IsNullOrWhiteSpace(request.Reason))
            {
                return Json(new { success = false, message = "Vui lòng nhập đầy đủ thông tin và lý do." });
            }

            var userIdStr = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            var userName = User.Identity.Name ?? "N/A";
            var userEmail = User.FindFirst(ClaimTypes.Email)?.Value ?? "N/A";

            if (string.IsNullOrEmpty(userIdStr))
            {
                return Json(new { success = false, message = "Phiên đăng nhập đã hết hạn. Vui lòng đăng nhập lại." });
            }

            int userId = int.Parse(userIdStr);

            // 🔴 QUAN TRỌNG: Include DanhGia để kiểm tra
            var form = await _context.FormIts
                .Include(f => f.DanhGia)
                .FirstOrDefaultAsync(x => x.Id == request.Id);

            if (form == null)
            {
                return Json(new { success = false, message = "Không tìm thấy đơn yêu cầu trong hệ thống." });
            }

            // CHỈ NGƯỜI TẠO ĐƠN
            if (form.IdNguoiTao != userId)
            {
                return Json(new { success = false, message = "Bạn không có quyền thao tác đơn này." });
            }

            // CHỈ ÁP DỤNG KHI ĐÃ HOÀN TẤT
            if (form.TrangThai != "HoanTat")
            {
                return Json(new { success = false, message = "Chỉ có thể hoàn tác các đơn đã được IT xác nhận hoàn tất." });
            }

            // 🔴 CHẶN NẾU ĐÃ ĐÁNH GIÁ
            if (form.DanhGia != null && form.DanhGia.Any())
            {
                return Json(new
                {
                    success = false,
                    message = "Bạn đã đánh giá đơn này rồi! Không thể hoàn tác sau khi đánh giá. Đơn đã được xác nhận hoàn thành 100%."
                });
            }

            // Kiểm tra nếu đơn đã bị hủy
            if (form.TenForm != null && form.TenForm.Contains("[ĐÃ HỦY]"))
            {
                return Json(new { success = false, message = "Đơn đã bị hủy, không thể thao tác." });
            }

            using (var transaction = await _context.Database.BeginTransactionAsync())
            {
                try
                {
                    DateTime now = DateTime.Now;

                    string itDaXuLy = form.TenAdmin ?? "N/A";
                    DateTime? timeItXuLy = form.TimeAdmin;

                    // RESET TRẠNG THÁI IT
                    form.IdAdmin = null;
                    form.TenAdmin = null;
                    form.TimeAdmin = null;
                    form.TrangThai = "DaDuyet";

                    // LƯU LỊCH SỬ CHI TIẾT
                    var lichSu = new LichSu
                    {
                        IdFormIt = form.Id,
                        TieuDe = "NGƯỜI TẠO XÁC NHẬN CHƯA HOÀN THÀNH",
                        Mota = $"Người tạo đơn: {userName} ({userEmail})\n" +
                               $"Đã yêu cầu IT xử lý lại do chưa hoàn thành.\n" +
                               $"IT đã xử lý trước đó: {itDaXuLy} (lúc {timeItXuLy?.ToString("HH:mm dd/MM/yyyy")})\n" +
                               $"Lý do phản hồi: {request.Reason.Trim()}",
                        Time = now
                    };

                    _context.LichSus.Add(lichSu);

                    await _context.SaveChangesAsync();
                    await transaction.CommitAsync();

                    return Json(new
                    {
                        success = true,
                        message = "Đã ghi nhận phản hồi của bạn. Đơn được chuyển về cho bộ phận IT xử lý lại."
                    });
                }
                catch (Exception ex)
                {
                    await transaction.RollbackAsync();
                    return Json(new { success = false, message = "Lỗi hệ thống: " + ex.Message });
                }
            }
        }
        public class ITNotCompleteRequest
        {
            public int Id { get; set; }
            public string Reason { get; set; }
        }
        #endregion

        #region Đánh giá đơn IT
        [HttpPost("/FormIT/DanhGiaDon")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DanhGiaDon([FromBody] DanhGiaRequest request)
        {
            // 1. Kiểm tra đầu vào
            if (request == null || request.Id <= 0 || request.MucDo < 1 || request.MucDo > 5)
            {
                return Json(new { success = false, message = "Dữ liệu không hợp lệ. Mức độ đánh giá từ 1-5 sao." });
            }

            // 2. Lấy thông tin người dùng
            var userIdStr = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            var userName = User.Identity.Name ?? "N/A";
            var userEmail = User.FindFirst(ClaimTypes.Email)?.Value ?? "N/A";

            if (string.IsNullOrEmpty(userIdStr))
            {
                return Json(new { success = false, message = "Phiên đăng nhập đã hết hạn. Vui lòng đăng nhập lại." });
            }

            int userId = int.Parse(userIdStr);

            // 3. Tìm đơn
            var form = await _context.FormIts
                .Include(f => f.DanhGia)
                .FirstOrDefaultAsync(x => x.Id == request.Id);

            if (form == null)
            {
                return Json(new { success = false, message = "Không tìm thấy đơn yêu cầu." });
            }

            // 4. CHỈ NGƯỜI TẠO ĐƠN
            if (form.IdNguoiTao != userId)
            {
                return Json(new { success = false, message = "Bạn không có quyền đánh giá đơn này." });
            }

            // 5. CHỈ ĐÁNH GIÁ KHI HOÀN TẤT
            if (form.TrangThai != "HoanTat")
            {
                return Json(new { success = false, message = "Chỉ có thể đánh giá các đơn đã hoàn thành." });
            }

            // 6. KIỂM TRA ĐÃ ĐÁNH GIÁ CHƯA
            if (form.DanhGia != null && form.DanhGia.Any())
            {
                return Json(new { success = false, message = "Bạn đã đánh giá đơn này rồi!" });
            }

            // 7. KIỂM TRA ĐƠN ĐÃ HỦY
            if (form.TenForm != null && form.TenForm.Contains("[ĐÃ HỦY]"))
            {
                return Json(new { success = false, message = "Đơn đã bị hủy, không thể đánh giá." });
            }

            // 8. Transaction
            using (var transaction = await _context.Database.BeginTransactionAsync())
            {
                try
                {
                    DateTime now = DateTime.Now;

                    // 9. TẠO BẢN GHI ĐÁNH GIÁ
                    var danhGia = new DanhGium
                    {
                        IdFormIt = form.Id,
                        IdNguoiDanhGia = userId,
                        TenNguoiDanhGia = userName,
                        TimeNguoiDanhGia = now,
                        MucDo = request.MucDo
                    };

                    _context.DanhGia.Add(danhGia);

                    // 10. LƯU LỊCH SỬ
                    string stars = new string('⭐', request.MucDo);
                    var lichSu = new LichSu
                    {
                        IdFormIt = form.Id,
                        TieuDe = "NGƯỜI TẠO ĐƠN ĐÁNH GIÁ",
                        Mota = $"Người tạo đơn: {userName} ({userEmail})\n" +
                               $"Đã đánh giá mức độ hài lòng: {stars} ({request.MucDo}/5 sao)\n" +
                               $"{(string.IsNullOrWhiteSpace(request.NhanXet) ? "" : $"Nhận xét: {request.NhanXet.Trim()}")}",
                        Time = now
                    };

                    _context.LichSus.Add(lichSu);

                    // 11. Lưu thay đổi
                    await _context.SaveChangesAsync();
                    await transaction.CommitAsync();

                    return Json(new
                    {
                        success = true,
                        message = $"Cảm ơn bạn đã đánh giá {request.MucDo} sao! Đơn đã được xác nhận hoàn thành 100%."
                    });
                }
                catch (Exception ex)
                {
                    await transaction.RollbackAsync();
                    return Json(new { success = false, message = "Lỗi hệ thống: " + ex.Message });
                }
            }
        }

        public class DanhGiaRequest
        {
            public int Id { get; set; }
            public int MucDo { get; set; } // 1-5 sao
            public string NhanXet { get; set; } // Tùy chọn
        }
        #endregion

        #region BÁO CÁO THỐNG KÊ FORM IT
        [HttpGet("/FormIT/BaoCaoThongKe")]
        public async Task<IActionResult> BaoCaoThongKe()
        {
            // 1. KIỂM TRA QUYỀN
            var userRole = User.FindFirst("UserRole")?.Value ?? "";
            if (userRole != "Admin" && userRole != "All" && userRole != "IT")
                return Redirect("/");

            // 2. LẤY TOÀN BỘ FORM (kèm navigation cần thiết)
            var allForms = await _context.FormIts
                .AsNoTracking()
                .Include(f => f.DanhGia)
                .Include(f => f.ItCtNguoiHoTros)
                    .ThenInclude(ct => ct.IdItNguoiHoTroNavigation)
                .OrderByDescending(f => f.TimeNguoiTao)
                .ToListAsync();

            // 3. THỐNG KÊ THEO LOẠI FORM
            var typeStats = allForms
                .GroupBy(f => f.IdForm ?? "")
                .Select(g => new
                {
                    Label = GetShortNameIT(g.Key),
                    Value = g.Count()
                })
                .ToList();

            ViewBag.TypeLabels = typeStats.Select(x => x.Label).ToList();
            ViewBag.TypeCounts = typeStats.Select(x => x.Value).ToList();

            // 4. THỐNG KÊ THEO DANH MỤC
            var danhMucStats = allForms
                .Where(f => !string.IsNullOrEmpty(f.Danhmuc))
                .GroupBy(f => f.Danhmuc)
                .Select(g => new
                {
                    Label = g.Key,
                    Value = g.Count()
                })
                .ToList();

            ViewBag.DanhMucLabels = danhMucStats.Select(x => x.Label).ToList();
            ViewBag.DanhMucCounts = danhMucStats.Select(x => x.Value).ToList();

            // 5. THỐNG KÊ TRẠNG THÁI FORM
            bool IsHuy(FormIt f) =>
                (f.TenForm ?? "").Contains("[ĐÃ HỦY]") || f.TrangThai == "TuChoi";

            int countHuy = allForms.Count(IsHuy);

            int countHoanTat = allForms.Count(f =>
                !IsHuy(f) &&
                f.IdAdmin != null &&
                f.TimeAdmin != null &&
                f.DanhGia != null &&
                f.DanhGia.Any()
            );

            int countChoDanhGia = allForms.Count(f =>
                !IsHuy(f) &&
                f.IdAdmin != null &&
                f.TimeAdmin != null &&
                (f.DanhGia == null || !f.DanhGia.Any())
            );

            int countDangXuLy = allForms.Count(f =>
                !IsHuy(f) &&
                f.IdNguoiDuyet != null &&
                f.TimeNguoiDuyet != null &&
                f.IdAdmin == null
            );

            int countChoDuyet = allForms.Count - countHuy - countHoanTat - countChoDanhGia - countDangXuLy;

            ViewBag.StatusLabels = new List<string>
    {
        "Hoàn tất", "Chờ đánh giá", "Đang xử lý", "Chờ duyệt", "Đã hủy"
    };

            ViewBag.StatusCounts = new List<int>
    {
        countHoanTat,
        countChoDanhGia,
        countDangXuLy,
        countChoDuyet,
        countHuy
    };

            // 6. THỜI GIAN XỬ LÝ TRUNG BÌNH
            var completedForms = allForms
                .Where(f => !IsHuy(f) && f.TimeAdmin != null && f.TimeNguoiDuyet != null)
                .ToList();

            if (completedForms.Any())
            {
                var avgMinutes = completedForms
                    .Average(f => (f.TimeAdmin.Value - f.TimeNguoiDuyet.Value).TotalMinutes);

                var t = TimeSpan.FromMinutes(avgMinutes);
                ViewBag.AvgProcessingTime = $"{(int)t.TotalHours:D2}h {t.Minutes:D2}m";
            }
            else
            {
                ViewBag.AvgProcessingTime = "0h 0m";
            }

            // 7. THỐNG KÊ THEO NGƯỜI HỖ TRỢ
            // - Mỗi đơn chỉ tính cho 1 người (stt cao nhất)
            // - Nếu không có tên (navigation) thì dùng "Không xác định"
            var formWithMainHandler = allForms
                .Select(f => new
                {
                    Form = f,
                    Handler = f.ItCtNguoiHoTros?
                                .OrderByDescending(h => h.Stt)
                                .FirstOrDefault()
                })
                .ToList();

            // Lọc những dòng có handler và handler có Id
            var withValidHandler = formWithMainHandler
                .Where(x => x.Handler != null && x.Handler.IdItNguoiHoTro != null)
                .ToList();

            var supportStats = withValidHandler
                .GroupBy(x => new
                {
                    IdIt = x.Handler.IdItNguoiHoTro.Value,
                    Ten = x.Handler.IdItNguoiHoTroNavigation?.Ten ?? "Không xác định"
                })
                .Select(g => new ItSupportStatisticVM
                {
                    IdIt = g.Key.IdIt,
                    TenIt = g.Key.Ten,

                    HoanTat = g.Count(x =>
                        !IsHuy(x.Form) &&
                        x.Form.IdAdmin != null &&
                        x.Form.TimeAdmin != null &&
                        x.Form.DanhGia != null &&
                        x.Form.DanhGia.Any()),

                    ChoDanhGia = g.Count(x =>
                        !IsHuy(x.Form) &&
                        x.Form.IdAdmin != null &&
                        x.Form.TimeAdmin != null &&
                        (x.Form.DanhGia == null || !x.Form.DanhGia.Any())),

                    DangXuLy = g.Count(x =>
                        !IsHuy(x.Form) &&
                        x.Form.IdNguoiDuyet != null &&
                        x.Form.TimeNguoiDuyet != null &&
                        x.Form.IdAdmin == null),

                    ChoDuyet = g.Count(x =>
                        !IsHuy(x.Form) &&
                        x.Form.IdNguoiDuyet == null),

                    DaHuy = g.Count(x => IsHuy(x.Form))
                })
                .ToList();

            // Nếu cần, đảm bảo Tong được tính đúng (VM có thuộc tính tính toán)
            if (supportStats != null && supportStats.Any())
            {
                // Order by tổng giảm dần
                supportStats = supportStats.OrderByDescending(s => s.Tong).ToList();
            }
            else
            {
                supportStats = new List<ItSupportStatisticVM>();
            }

            ViewBag.SupportStats = supportStats;

            // 8. THỐNG KÊ: THỜI GIAN HOÀN THÀNH TRUNG BÌNH THEO DANHMUC CHO MỖI NGƯỜI HỖ TRỢ
            //    (sử dụng các form đã hoàn thành: TimeAdmin != null && TimeNguoiDuyet != null)
            var avgByHandlerDanhMuc = withValidHandler
                .Where(x => x.Form.TimeAdmin != null && x.Form.TimeNguoiDuyet != null)
                .GroupBy(x => new
                {
                    IdIt = x.Handler.IdItNguoiHoTro.Value,
                    TenIt = x.Handler.IdItNguoiHoTroNavigation?.Ten ?? "Không xác định",
                    Danhmuc = string.IsNullOrWhiteSpace(x.Form.Danhmuc) ? "Không xác định" : x.Form.Danhmuc
                })
                .Select(g => new ItSupportCategoryAvgVM
                {
                    IdIt = g.Key.IdIt,
                    TenIt = g.Key.TenIt,
                    Danhmuc = g.Key.Danhmuc,
                    AvgMinutes = g.Average(x => (x.Form.TimeAdmin.Value - x.Form.TimeNguoiDuyet.Value).TotalMinutes),
                    Count = g.Count()
                })
                .ToList();

            // format string for display
            foreach (var item in avgByHandlerDanhMuc)
            {
                var ts = TimeSpan.FromMinutes(item.AvgMinutes);
                if (ts.TotalDays >= 1)
                    item.AvgTimeFormatted = $"{(int)ts.TotalDays}d {ts.Hours}h {ts.Minutes}m";
                else if (ts.TotalHours >= 1)
                    item.AvgTimeFormatted = $"{(int)ts.TotalHours}h {ts.Minutes}m";
                else
                    item.AvgTimeFormatted = $"{(int)ts.TotalMinutes}m";
            }

            ViewBag.AvgByHandlerDanhMuc = avgByHandlerDanhMuc;

            return View(allForms);
        }

        /* HÀM TIỆN ÍCH */
        private string GetShortNameIT(string id) => id switch
        {
            "IT_MAIL_1" => "Đăng ký Email",
            "IT_ORDER_2" => "Sửa chữa / Yêu cầu",
            "IT_WIFI_3" => "Sử dụng Wifi",
            "IT_DTBAN_4" => "Điện thoại bàn",
            _ => "Khác"
        };

        /* VIEW MODEL */
        public class ItSupportStatisticVM
        {
            public int IdIt { get; set; }
            public string TenIt { get; set; }

            public int HoanTat { get; set; }
            public int ChoDanhGia { get; set; }
            public int DangXuLy { get; set; }
            public int ChoDuyet { get; set; }
            public int DaHuy { get; set; }

            // Tổng số đơn (tính tại runtime)
            public int Tong =>
                HoanTat + ChoDanhGia + DangXuLy + ChoDuyet + DaHuy;
        }

        /* NEW VIEW MODEL: trung bình thời gian hoàn thành theo (Người hỗ trợ, Danh mục) */
        public class ItSupportCategoryAvgVM
        {
            public int IdIt { get; set; }
            public string TenIt { get; set; }
            public string Danhmuc { get; set; }
            public double AvgMinutes { get; set; }
            public string AvgTimeFormatted { get; set; }
            public int Count { get; set; }
        }
        #endregion

        #region LỊCH SỬ VÀ THÔNG BÁO FORM IT

        [HttpGet("/FormIT/LichSuIT")]
        public async Task<IActionResult> LogLichSuIT()
        {
            var userIdStr = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userIdStr)) return RedirectToAction("DangNhap", "DonXetDuyet");

            int userId = int.Parse(userIdStr);
            var userRole = User.FindFirst("UserRole")?.Value ?? "";
            var userMaNv = User.Identity.Name;

            var query = _context.LichSus
                .Include(l => l.IdFormItNavigation)
                    .ThenInclude(f => f.ItCtNguoiHoTros)
                        .ThenInclude(ct => ct.IdItNguoiHoTroNavigation)
                .Include(l => l.IdFormItNavigation)
                    .ThenInclude(f => f.DanhGia)
                .AsQueryable();

            // Logic phân quyền (Giữ nguyên hoàn toàn)
            if (userRole != "Admin" && userRole != "All")
            {
                query = query.Where(l =>
                    l.IdFormItNavigation.IdNguoiTao == userId ||
                    l.IdFormItNavigation.IdNguoiDuyet == userId ||
                    l.IdFormItNavigation.IdAdmin == userId ||
                    l.IdFormItNavigation.ItCtNguoiHoTros.Any(ct => ct.IdItNguoiHoTroNavigation.MaNv == userMaNv)
                );
            }

            // Không dùng AsNoTracking ở đây nếu bạn muốn cập nhật IsRead ngay trong action này (tùy chọn)
            var logs = await query.OrderByDescending(l => l.Time).ToListAsync();
            return View(logs);
        }

        [HttpGet("/FormIT/GetNotifications")]
        public async Task<IActionResult> GetNotifications(int skip = 0, int take = 20)
        {
            var userIdStr = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userIdStr)) return Unauthorized();

            int userId = int.Parse(userIdStr);
            var userRole = User.FindFirst("UserRole")?.Value ?? "";
            var userMaNv = User.Identity.Name;

            var query = _context.LichSus
                .Include(l => l.IdFormItNavigation)
                    .ThenInclude(f => f.ItCtNguoiHoTros)
                        .ThenInclude(ct => ct.IdItNguoiHoTroNavigation)
                .AsQueryable();

            // Logic phân quyền (Giữ nguyên hoàn toàn của bạn)
            if (userRole != "Admin" && userRole != "All")
            {
                query = query.Where(l =>
                    l.IdFormItNavigation.IdNguoiTao == userId ||
                    l.IdFormItNavigation.IdNguoiDuyet == userId ||
                    l.IdFormItNavigation.IdAdmin == userId ||
                    l.IdFormItNavigation.ItCtNguoiHoTros.Any(ct => ct.IdItNguoiHoTroNavigation.MaNv == userMaNv)
                );
            }

            // Tính tổng số chưa đọc (không bị ảnh hưởng bởi skip/take)
            var unreadCount = await query.CountAsync(l => l.IsRead != true);

            // Lấy dữ liệu phân trang
            var logs = await query.OrderByDescending(l => l.Time)
                                  .Skip(skip)
                                  .Take(take)
                                  .Select(l => new {
                                      l.Id,
                                      l.IdFormIt,
                                      l.TieuDe,
                                      l.Mota,
                                      Time = l.Time.HasValue ? l.Time.Value.ToString("dd/MM HH:mm") : "",
                                      IsRead = l.IsRead ?? false
                                  })
                                  .AsNoTracking()
                                  .ToListAsync();

            return Ok(new { dataList = logs, unreadCount });
        }

        // ACTION MỚI: Đánh dấu một thông báo là đã đọc
        [HttpPost("/FormIT/MarkAsRead/{id}")]
        public async Task<IActionResult> MarkAsRead(int id)
        {
            var log = await _context.LichSus.FindAsync(id);
            if (log == null) return NotFound();

            if (log.IsRead != true)
            {
                log.IsRead = true;
                await _context.SaveChangesAsync();
            }
            return Ok();
        }

        // ACTION MỚI: Đánh dấu tất cả là đã đọc
        [HttpPost("/FormIT/MarkAllAsRead")]
        public async Task<IActionResult> MarkAllAsRead()
        {
            var userIdStr = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userIdStr)) return Unauthorized();

            int userId = int.Parse(userIdStr);
            var userRole = User.FindFirst("UserRole")?.Value ?? "";
            var userMaNv = User.Identity.Name;

            // Lấy tất cả thông báo chưa đọc dựa trên phân quyền
            var query = _context.LichSus.Where(l => l.IsRead != true);

            if (userRole != "Admin" && userRole != "All")
            {
                query = query.Where(l =>
                    l.IdFormItNavigation.IdNguoiTao == userId ||
                    l.IdFormItNavigation.IdNguoiDuyet == userId ||
                    l.IdFormItNavigation.IdAdmin == userId ||
                    l.IdFormItNavigation.ItCtNguoiHoTros.Any(ct => ct.IdItNguoiHoTroNavigation.MaNv == userMaNv)
                );
            }

            var unreadLogs = await query.ToListAsync();
            foreach (var log in unreadLogs)
            {
                log.IsRead = true;
            }

            await _context.SaveChangesAsync();
            return Ok();
        }

        #endregion
    }
    }
