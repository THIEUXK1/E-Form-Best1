using E_Form_Best.Context;
using E_Form_Best.Models.ITForm;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace E_Form_Best.Areas.ITForm.Controllers
{
    [Area("ITform")]
    public class ITFormController : Controller
    {
        public ITFormContext _context;

        // Sửa constructor để nhận DI từ hệ thống
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

            // 2. Kiểm tra tài khoản trong Database (Đã thêm Include UserQuyens và IdQuyenNavigation)
            var user = _context.Users
                .Include(u => u.UserBoPhans)
                    .ThenInclude(ub => ub.IdBoPhanNavigation)
                .Include(u => u.UserQuyens) // <-- Thêm Include bảng trung gian
                    .ThenInclude(uq => uq.IdQuyenNavigation) // <-- Thêm Include bảng Quyen
                .FirstOrDefault(u => u.Tk == email && u.MatKhau == matKhau && u.TrangThai != "Khóa");

            if (user == null)
            {
                ViewBag.Error = "Tài khoản hoặc mật khẩu không đúng.";
                return View();
            }

            // 3. XỬ LÝ ĐỊNH DANH THIẾT BỊ (LAPTOP/PHONE)
            string userAgent = Request.Headers["User-Agent"].ToString();

            var device = _context.UserDevices
                .FirstOrDefault(d => d.IdNguoiDung == user.IdNguoiDung && d.DeviceFingerprint == userAgent);

            if (device == null)
            {
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
                device.LastLogin = DateTime.Now;
                if (!string.IsNullOrEmpty(deviceName)) device.DeviceName = deviceName;
                _context.UserDevices.Update(device);
            }
            await _context.SaveChangesAsync();

            // 4. THIẾT LẬP COOKIE AUTHENTICATION
            var claims = new List<Claim>
    {
        new Claim(ClaimTypes.NameIdentifier, user.IdNguoiDung.ToString()),
        new Claim(ClaimTypes.Name, user.HoTen ?? ""),
        new Claim(ClaimTypes.Email, user.Tk ?? ""),
        new Claim("UserRole", user.VaiTro ?? ""),
        new Claim("PhongBan", user.PhongBan ?? ""),
        new Claim("TenCongTy", user.TenCongTy ?? ""),
        new Claim("AnhDaiDien", user.AnhDaiDien ?? "/images/default-avatar.png")
    };

            // --- LẤY TẤT CẢ BỘ PHẬN (GIỮ NGUYÊN) ---
            if (user.UserBoPhans != null && user.UserBoPhans.Any())
            {
                var tatCaTenBoPhan = string.Join(", ", user.UserBoPhans
                    .Where(ub => ub.IdBoPhanNavigation != null)
                    .Select(ub => ub.IdBoPhanNavigation.TenBoPhan));

                var tatCaMoTa = string.Join(" | ", user.UserBoPhans
                    .Where(ub => ub.IdBoPhanNavigation != null)
                    .Select(ub => ub.IdBoPhanNavigation.MoTa));

                claims.Add(new Claim("TenBoPhan", tatCaTenBoPhan));
                claims.Add(new Claim("MoTaBoPhan", tatCaMoTa));
            }

            // --- MỚI: LẤY TẤT CẢ QUYỀN (ROLES) VÀO COOKIE ---
            if (user.UserQuyens != null && user.UserQuyens.Any())
            {
                // Lấy danh sách tên quyền
                var dsQuyen = user.UserQuyens
                    .Where(uq => uq.IdQuyenNavigation != null)
                    .Select(uq => uq.IdQuyenNavigation.TenQuyen);

                foreach (var tenQuyen in dsQuyen)
                {
                    if (!string.IsNullOrEmpty(tenQuyen))
                    {
                        // Thêm từng quyền vào danh sách ClaimTypes.Role để dùng được [Authorize(Roles = "...")]
                        claims.Add(new Claim(ClaimTypes.Role, tenQuyen));
                    }
                }
            }

            var claimsIdentity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);

            var authProperties = new AuthenticationProperties
            {
                IsPersistent = rememberMe,
                ExpiresUtc = rememberMe ? DateTimeOffset.UtcNow.AddDays(365) : null,
                AllowRefresh = true
            };

            await HttpContext.SignInAsync(
                CookieAuthenticationDefaults.AuthenticationScheme,
                new ClaimsPrincipal(claimsIdentity),
                authProperties);

            // 5. Lưu email vào Cookie thường
            if (rememberMe)
            {
                Response.Cookies.Append("RememberedEmail", email, new CookieOptions
                {
                    Expires = DateTime.Now.AddDays(30),
                    HttpOnly = true,
                    IsEssential = true
                });
            }
            else
            {
                Response.Cookies.Delete("RememberedEmail");
            }

            // 6. Chuyển hướng thành công
            TempData["ShowPushPrompt"] = true;
            return Redirect("/menuA");
        }

        [HttpGet("/DonXetDuyet/DangXuat")]
        public async Task<IActionResult> DangXuat()
        {
            // 1. Đăng xuất khỏi hệ thống Authentication (Xóa Cookie định danh .AspNetCore.Cookies)
            await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);

            // 2. Xóa sạch Session (nếu bạn có lưu các biến tạm khác)
            HttpContext.Session.Clear();

            // 3. Tùy chọn: Bạn có thể thêm TempData để thông báo ở trang đăng nhập
            TempData["Success"] = "Bạn đã đăng xuất thành công.";

            // 4. Chuyển hướng về trang đăng nhập
            return Redirect("/DonXetDuyet/DangNhap");
        }

        #endregion

        #region PHÂN QUYỀN TRUY CẬP (Custom Access Control)

        /// <summary>
        /// Kiểm tra quyền truy cập dựa trên danh sách Role được phép.
        /// Hỗ trợ các Role mới: AdminIT, QuanLyDuyetDonIT, IT, All...
        /// </summary>
        /// <param name="allowedRoles">Danh sách các Role cụ thể được phép truy cập</param>
        /// <returns>True nếu có quyền, False nếu không</returns>
        private bool HasAccess(params string[] allowedRoles)
        {
            // 1. Kiểm tra trạng thái đăng nhập
            if (User == null || !User.Identity.IsAuthenticated)
            {
                return false;
            }

            // 2. Lấy Role của người dùng từ Claims (Hỗ trợ cả chuẩn .NET và Custom Claim)
            var userRole = User.FindFirst(System.Security.Claims.ClaimTypes.Role)?.Value
                           ?? User.FindFirst("UserRole")?.Value
                           ?? "";

            if (string.IsNullOrEmpty(userRole)) return false;

            // 3. NHÓM QUYỀN ƯU TIÊN (SUPER ROLES)
            // Nếu là "All" hoặc "AdminIT", mặc định có toàn quyền truy cập các chức năng quản trị IT
            if (userRole == "All" || userRole == "AdminIT")
            {
                return true;
            }

            // 4. KIỂM TRA ROLE CỤ THỂ TRONG DANH SÁCH CHO PHÉP
            // Bao gồm các role như: "QuanLyDuyetDonIT", "IT", "QuanLy", "Admin"...
            if (allowedRoles != null && allowedRoles.Length > 0)
            {
                if (allowedRoles.Contains(userRole))
                {
                    return true;
                }
            }

            // 5. Mặc định từ chối nếu không khớp bất kỳ điều kiện nào
            return false;
        }

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

        #region Don Mail  1Form IT 1)

        [HttpGet("/FormIT/DonMail")]
        public IActionResult DonMail()
        {
            if (User == null || !User.Identity.IsAuthenticated)
                return Redirect("/DonXetDuyet/DangNhap");

            // Lọc danh sách nhân viên IT và CHỈ lấy những công việc có tên "Đăng kí mail"
            ViewBag.ListNguoiHoTro = _context.ItNguoiHoTros
                .Include(x => x.CongViecIts)
                .Where(x => x.BoPhan == "IT")
                .Select(x => new E_Form_Best.Models.ITForm.ItNguoiHoTro
                {
                    Id = x.Id,
                    MaNv = x.MaNv,
                    Ten = x.Ten,
                    BoPhan = x.BoPhan,
                    GhiChu = x.GhiChu,
                    CongViecIts = x.CongViecIts.Where(cv => cv.Ten == "Đăng kí mail").ToList()
                })
                .Where(x => x.CongViecIts.Any())
                .ToList();

            // Lấy thông tin User từ Claims
            var userIdStr = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
            int? userId = !string.IsNullOrEmpty(userIdStr) && int.TryParse(userIdStr, out var tmpId) ? tmpId : null;

            string userName = User.Identity.Name ?? "";
            string phongBan = User.FindFirst("PhongBan")?.Value ?? "";
            string userRole = User.FindFirst("UserRole")?.Value ?? "";
            string userEmail = User.FindFirst(System.Security.Claims.ClaimTypes.Email)?.Value ?? "";

            // MỚI: Lấy Tên Công Ty từ Claim
            string tenCongTy = User.FindFirst("TenCongTy")?.Value ?? "";

            var model = new FormIt
            {
                TenNguoiNv = userName,
                BoPhan = phongBan,
                ViTri = userRole,
                SoNhanVien = userEmail,
                TenCongTy = tenCongTy, // Gán vào model để hiển thị nếu cần
                Ngay = DateOnly.FromDateTime(DateTime.Now),
                IdNguoiTao = userId,
                TenNguoiTao = userName,
                TimeNguoiTao = DateTime.Now,
                TrangThai = "ChoDuyet"
            };

            return View(model);
        }
        [HttpPost("/FormIT/DonMail")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DonMail(FormIt form, [FromForm] ItMail1 itMail, int[] SelectedCongViecIds)
        {
            // 1. Kiểm tra đăng nhập
            if (User == null || !User.Identity.IsAuthenticated)
                return Redirect("/DonXetDuyet/DangNhap");

            // 2. Lấy thông tin User từ Claims
            var userIdStr = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
            int.TryParse(userIdStr, out int userId);

            var userName = User.Identity.Name ?? "";
            var phongBan = User.FindFirst("PhongBan")?.Value ?? "";
            var viTri = User.FindFirst("UserRole")?.Value ?? "";
            var userEmail = User.FindFirst(System.Security.Claims.ClaimTypes.Email)?.Value ?? "";
            var tenCongTy = User.FindFirst("TenCongTy")?.Value ?? "";

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
                    form.TenCongTy = tenCongTy;
                    form.TrangThai = "ChoDuyet";
                    form.IdForm = "IT_Mail_1";
                    form.TenForm = "Đơn đăng ký/sửa đổi Mail";
                    form.Danhmuc = "Đăng kí mail";

                    _context.FormIts.Add(form);
                    await _context.SaveChangesAsync();

                    // --- CẤU HÌNH ĐƯỜNG DẪN LƯU FILE ---
                    string networkPath = @"\\10.0.60.30\BPVN-Fileserver\Public\IT-Information Technology Dept\5.E-Form\DonIT";
                    if (!Directory.Exists(networkPath)) Directory.CreateDirectory(networkPath);

                    string safeName = RemoveSign4VietnameseString(userName).Replace(" ", "");
                    string timeStamp = DateTime.Now.ToString("ddMMyy_HHmmss");

                    // --- BƯỚC 2: XỬ LÝ FILE ĐÍNH KÈM (UploadFile) ---
                    var uploadFile = Request.Form.Files["UploadFile"];
                    if (uploadFile != null && uploadFile.Length > 0)
                    {
                        string extension = Path.GetExtension(uploadFile.FileName);
                        string fileName = $"DonMail_ID{form.Id}_{safeName}_{timeStamp}{extension}";
                        string fullPath = Path.Combine(networkPath, fileName);

                        using (var fileStream = new FileStream(fullPath, FileMode.Create))
                        {
                            await uploadFile.CopyToAsync(fileStream);
                        }

                        form.FileDinhKem = fileName;
                        _context.Entry(form).Property(x => x.FileDinhKem).IsModified = true;
                        await _context.SaveChangesAsync();
                    }

                    // --- BƯỚC 3: CHI TIẾT CẤU HÌNH MAIL & XỬ LÝ ẢNH ---
                    if (itMail != null)
                    {
                        itMail.IdFormIt = form.Id;

                        // Xử lý lưu ảnh vật lý thay vì dùng GetFileBytesAsync2
                        var anhFile = Request.Form.Files["Anh"];
                        if (anhFile != null && anhFile.Length > 0)
                        {
                            string imgExtension = Path.GetExtension(anhFile.FileName);
                            if (string.IsNullOrEmpty(imgExtension)) imgExtension = ".jpg"; // Mặc định cho ảnh paste

                            string imgFileName = $"AnhMail_ID{form.Id}_{safeName}_{timeStamp}{imgExtension}";
                            string imgFullPath = Path.Combine(networkPath, imgFileName);

                            using (var imgStream = new FileStream(imgFullPath, FileMode.Create))
                            {
                                await anhFile.CopyToAsync(imgStream);
                            }

                            // Lưu tên file vào cột DuonDanAnh
                            itMail.DuonDanAnh = imgFileName;
                            // Đảm bảo cột Anh (byte[]) để null để tối ưu database
                            itMail.Anh = null;
                        }

                        _context.ItMail1s.Add(itMail);
                        await _context.SaveChangesAsync();
                    }

                    // --- BƯỚC 4: LƯU NGƯỜI HỖ TRỢ ---
                    string danhSachTenHoTro = "Chưa chọn";
                    List<int> selectedItNguoiHoTroIds = new List<int>();

                    if (SelectedCongViecIds != null && SelectedCongViecIds.Length > 0)
                    {
                        selectedItNguoiHoTroIds = await _context.CongViecIts
                            .Where(cv => SelectedCongViecIds.Contains(cv.Id) && cv.IdItNguoiHoTro != null)
                            .Select(cv => cv.IdItNguoiHoTro!.Value)
                            .Distinct()
                            .ToListAsync();
                    }

                    if (!selectedItNguoiHoTroIds.Any() && form.Danhmuc == "Đăng kí mail")
                    {
                        selectedItNguoiHoTroIds = await _context.CongViecIts
                            .Where(cv => cv.Ten == "Đăng kí mail" && cv.IdItNguoiHoTro != null)
                            .Select(cv => cv.IdItNguoiHoTro!.Value)
                            .Distinct()
                            .ToListAsync();
                    }

                    if (selectedItNguoiHoTroIds.Any())
                    {
                        var listHoTro = await _context.ItNguoiHoTros
                            .Where(x => selectedItNguoiHoTroIds.Contains(x.Id))
                            .Select(x => x.Ten)
                            .ToListAsync();

                        danhSachTenHoTro = string.Join(", ", listHoTro);

                        int stt = 1;
                        foreach (var idHoTro in selectedItNguoiHoTroIds)
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

                    // --- BƯỚC 5: LƯU LỊCH SỬ THAY ĐỔI ---
                    string emailDeXuat = itMail?.Email ?? "N/A";
                    string moTaChiTiet = $"[Khởi tạo đơn] Người tạo: {userName} (ID: {userId}) | Bộ phận: {phongBan} | Công ty: {tenCongTy}\n" +
                                         $"- Danh mục: {form.Danhmuc}\n" +
                                         $"- Email đề xuất: {emailDeXuat}\n" +
                                         $"- Ảnh minh họa: {(string.IsNullOrEmpty(itMail?.DuonDanAnh) ? "Không có" : itMail.DuonDanAnh)}\n" +
                                         $"- File đính kèm: {(string.IsNullOrEmpty(form.FileDinhKem) ? "Không có" : form.FileDinhKem)}\n" +
                                         $"- Người hỗ trợ chỉ định: {danhSachTenHoTro}.";

                    var lichSu = new LichSuFormIt
                    {
                        IdFormIt = form.Id,
                        TieuDe = "Khởi tạo đơn",
                        Mota = moTaChiTiet,
                        Time = DateTime.Now
                    };
                    _context.LichSuFormIts.Add(lichSu);
                    await _context.SaveChangesAsync();

                    await transaction.CommitAsync();

                    TempData["Success"] = "Gửi đơn đăng ký Mail thành công!";
                    return Redirect("/FormIT/DonCho");
                }
                catch (Exception ex)
                {
                    await transaction.RollbackAsync();

                    // Load lại list hỗ trợ khi xảy ra lỗi để trả về View
                    ViewBag.ListNguoiHoTro = _context.ItNguoiHoTros
                        .Include(x => x.CongViecIts)
                        .Where(x => x.BoPhan == "IT")
                        .Select(x => new E_Form_Best.Models.ITForm.ItNguoiHoTro
                        {
                            Id = x.Id,
                            MaNv = x.MaNv,
                            Ten = x.Ten,
                            BoPhan = x.BoPhan,
                            GhiChu = x.GhiChu,
                            CongViecIts = x.CongViecIts.Where(cv => cv.Ten == "Đăng kí mail").ToList()
                        })
                        .Where(x => x.CongViecIts.Any())
                        .ToList();

                    ModelState.AddModelError("", "Lỗi trong quá trình lưu: " + ex.Message);
                    return View(form);
                }
            }
        }
        #endregion

        #region IT Order - Sửa chữa/Yêu cầu thiết bị (Form IT 2) - CẬP NHẬT CÔNG TY & LỊCH SỬ

        [HttpGet("/FormIT/TaoIT_Order")]
        public async Task<IActionResult> TaoIT_Order()
        {
            // --- LẤY THÔNG TIN TỪ CLAIMS ---
            var userIdClaim = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userIdClaim)) return Redirect("/DonXetDuyet/DangNhap");

            int userId = int.Parse(userIdClaim);
            var userName = User.Identity.Name ?? "";
            var phongBan = User.FindFirst("PhongBan")?.Value ?? "";
            var userRole = User.FindFirst(System.Security.Claims.ClaimTypes.Role) ?? User.FindFirst("UserRole");
            var userEmail = User.FindFirst(System.Security.Claims.ClaimTypes.Email)?.Value ?? "";

            // Lấy thông tin Công ty từ Claim
            var tenCongTy = User.FindFirst("TenCongTy")?.Value ?? "";

            ViewBag.CongViecList = await _context.CongViecIts
                                            .OrderBy(x => x.Ten)
                                            .ToListAsync();

            var model = new FormIt
            {
                TenNguoiNv = userName,
                BoPhan = phongBan,
                ViTri = userRole?.Value ?? "",
                SoNhanVien = userEmail,
                TenCongTy = tenCongTy, // Gán thông tin công ty vào model
                Ngay = DateOnly.FromDateTime(DateTime.Now),
                IdNguoiTao = userId,
                TenNguoiTao = userName,
                TimeNguoiTao = DateTime.Now,
                TrangThai = "ChoDuyet",
                IdForm = "IT_OrderIT_2",
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
            var userEmail = User.FindFirst(System.Security.Claims.ClaimTypes.Email)?.Value ?? "";
            var phongBan = User.FindFirst("PhongBan")?.Value ?? "";
            var viTri = User.FindFirst(System.Security.Claims.ClaimTypes.Role)?.Value
                        ?? User.FindFirst("UserRole")?.Value ?? "";
            var tenCongTy = User.FindFirst("TenCongTy")?.Value ?? "";

            using (var transaction = await _context.Database.BeginTransactionAsync())
            {
                try
                {
                    // --- 1. TRUY VẤN CÔNG VIỆC & NGƯỜI HỖ TRỢ ---
                    var congViec = await _context.CongViecIts
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
                    form.TenCongTy = tenCongTy;
                    form.TrangThai = "ChoDuyet";
                    form.IdForm = "IT_ORDER_2";
                    form.TenForm = "Yêu cầu sửa chữa/Hỗ trợ kỹ thuật";

                    if (congViec != null) form.Danhmuc = congViec.Ten;

                    _context.FormIts.Add(form);
                    await _context.SaveChangesAsync();

                    // --- CẤU HÌNH ĐƯỜNG DẪN MẠNG ---
                    string networkPath = @"\\10.0.60.30\BPVN-Fileserver\Public\IT-Information Technology Dept\5.E-Form\DonIT";
                    if (!Directory.Exists(networkPath)) Directory.CreateDirectory(networkPath);

                    string safeName = RemoveSign4VietnameseString(userName).Replace(" ", "");
                    string timeStamp = DateTime.Now.ToString("ddMMyy_HHmmss");

                    // --- 3. XỬ LÝ CHI TIẾT LỖI (ItOrderIt2) & ẢNH PASTE ---
                    if (itOrder != null)
                    {
                        itOrder.IdFormIt = form.Id;
                        if (string.IsNullOrEmpty(itOrder.Ten) && congViec != null) itOrder.Ten = congViec.Ten;

                        // Xử lý lưu ảnh vật lý thay vì GetFileBytesAsync2
                        var anhFile = Request.Form.Files["Anh"];
                        if (anhFile != null && anhFile.Length > 0)
                        {
                            string imgExtension = Path.GetExtension(anhFile.FileName);
                            if (string.IsNullOrEmpty(imgExtension)) imgExtension = ".jpg";

                            string imgFileName = $"AnhOrder_ID{form.Id}_{safeName}_{timeStamp}{imgExtension}";
                            string imgFullPath = Path.Combine(networkPath, imgFileName);

                            using (var imgStream = new FileStream(imgFullPath, FileMode.Create))
                            {
                                await anhFile.CopyToAsync(imgStream);
                            }

                            // Lưu đường dẫn và set byte[] null
                            itOrder.DuongDanAnh = imgFileName;
                            itOrder.Anh = null;
                        }

                        _context.ItOrderIt2s.Add(itOrder);
                    }

                    // --- 4. XỬ LÝ FILE ĐÍNH KÈM (UploadFile) ---
                    var uploadFile = Request.Form.Files["UploadFile"];
                    string fileLog = "Không có tệp đính kèm";

                    if (uploadFile != null && uploadFile.Length > 0)
                    {
                        string extension = Path.GetExtension(uploadFile.FileName);
                        string fileName = $"DonOrder_ID{form.Id}_{safeName}_{timeStamp}{extension}";
                        string fullPath = Path.Combine(networkPath, fileName);

                        using (var fileStream = new FileStream(fullPath, FileMode.Create))
                        {
                            await uploadFile.CopyToAsync(fileStream);
                        }

                        form.FileDinhKem = fileName;
                        _context.Entry(form).Property(x => x.FileDinhKem).IsModified = true;
                        fileLog = $"Đính kèm tệp: {fileName}";
                        await _context.SaveChangesAsync();
                    }

                    // --- 5. LƯU NGƯỜI HỖ TRỢ ---
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
                        supporterLog = $"Đã tự động gán: {congViec.IdItNguoiHoTroNavigation?.Ten ?? "Nhân viên IT"} (Loại CV: {congViec.Ten})";
                    }

                    // --- 6. LƯU LỊCH SỬ ---
                    string anhLog = string.IsNullOrEmpty(itOrder?.DuongDanAnh) ? "Không có ảnh" : $"Ảnh: {itOrder.DuongDanAnh}";
                    var lichSu = new LichSuFormIt
                    {
                        IdFormIt = form.Id,
                        TieuDe = "Khởi tạo yêu cầu",
                        Mota = $"[Cty: {tenCongTy}] Người tạo: {userName}. {supporterLog}. {fileLog}. {anhLog}",
                        Time = DateTime.Now
                    };
                    _context.LichSuFormIts.Add(lichSu);

                    await _context.SaveChangesAsync();
                    await transaction.CommitAsync();


                    TempData["Success"] = "Gửi yêu cầu hỗ trợ IT thành công!";
                    return Redirect("/FormIT/DonCho");
                }
                catch (Exception ex)
                {
                    await transaction.RollbackAsync();
                    ViewBag.CongViecList = await _context.CongViecIts.OrderBy(x => x.Ten).ToListAsync();
                    ModelState.AddModelError("", "Lỗi: " + ex.Message);
                    return View(form);
                }
            }
        }
        #endregion

        #region IT Wifi - Đăng ký sử dụng Wifi (Form IT 3)

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

            // --- LẤY THÊM THÔNG TIN TÊN CÔNG TY TỪ CLAIM ---
            var tenCongTy = User.FindFirst("TenCongTy")?.Value ?? "";

            // --- CẬP NHẬT: LẤY DANH SÁCH CÔNG VIỆC THAY VÌ NGƯỜI HỖ TRỢ ---
            ViewBag.CongViecList = await _context.CongViecIts
                .Where(x => x.Ten.Contains("Đăng kí sử dụng wifi"))
                .OrderBy(x => x.Ten)
                .ToListAsync();

            var model = new FormIt
            {
                TenNguoiNv = userName,
                BoPhan = phongBan,
                ViTri = userRole,
                SoNhanVien = userEmail,
                TenCongTy = tenCongTy, // Gán thông ty vào model
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
            var tenCongTy = User.FindFirst("TenCongTy")?.Value ?? "";

            using (var transaction = await _context.Database.BeginTransactionAsync())
            {
                try
                {
                    // --- 1. TRUY VẤN NGƯỜI HỖ TRỢ VÀ THÔNG TIN CÔNG VIỆC ---
                    var congViec = await _context.CongViecIts
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
                    form.TenCongTy = tenCongTy;
                    form.TrangThai = "ChoDuyet";
                    form.IdForm = "IT_DangKiSuDungWifi_3";
                    form.TenForm = "Đăng ký sử dụng Wifi";

                    if (congViec != null) form.Danhmuc = congViec.Ten;

                    _context.FormIts.Add(form);
                    await _context.SaveChangesAsync();

                    // --- CẤU HÌNH ĐƯỜNG DẪN LƯU FILE ---
                    string networkPath = @"\\10.0.60.30\BPVN-Fileserver\Public\IT-Information Technology Dept\5.E-Form\DonIT";
                    if (!Directory.Exists(networkPath)) Directory.CreateDirectory(networkPath);

                    string safeName = RemoveSign4VietnameseString(userName).Replace(" ", "");
                    string timeStamp = DateTime.Now.ToString("ddMMyy_HHmmss");

                    // --- 3. XỬ LÝ FILE ĐÍNH KÈM (UploadFile) ---
                    var uploadFile = Request.Form.Files["UploadFile"];
                    string fileLog = "Không có tệp đính kèm";

                    if (uploadFile != null && uploadFile.Length > 0)
                    {
                        string extension = Path.GetExtension(uploadFile.FileName);
                        string fileName = $"DonWifi_ID{form.Id}_{safeName}_{timeStamp}{extension}";
                        string fullPath = Path.Combine(networkPath, fileName);

                        using (var fileStream = new FileStream(fullPath, FileMode.Create))
                        {
                            await uploadFile.CopyToAsync(fileStream);
                        }

                        form.FileDinhKem = fileName;
                        _context.Entry(form).Property(x => x.FileDinhKem).IsModified = true;
                        fileLog = $"Đính kèm tệp: {fileName}";
                        await _context.SaveChangesAsync();
                    }

                    // --- 4. CHI TIẾT WIFI (ItDangKiSuDungWifi3) & XỬ LÝ ẢNH ---
                    if (itWifi != null)
                    {
                        itWifi.IdFormIt = form.Id;

                        // Xử lý lưu ảnh vật lý thay vì lưu vào DB
                        var anhFile = Request.Form.Files["Anh"];
                        if (anhFile != null && anhFile.Length > 0)
                        {
                            string imgExtension = Path.GetExtension(anhFile.FileName);
                            if (string.IsNullOrEmpty(imgExtension)) imgExtension = ".jpg";

                            string imgFileName = $"AnhWifi_ID{form.Id}_{safeName}_{timeStamp}{imgExtension}";
                            string imgFullPath = Path.Combine(networkPath, imgFileName);

                            using (var imgStream = new FileStream(imgFullPath, FileMode.Create))
                            {
                                await anhFile.CopyToAsync(imgStream);
                            }

                            // Lưu tên file vào cột DuongDanAnh và để cột Anh null
                            itWifi.DuongDanAnh = imgFileName;
                            itWifi.Anh = null;
                        }

                        _context.ItDangKiSuDungWifi3s.Add(itWifi);
                        await _context.SaveChangesAsync();
                    }

                    // --- 5. LƯU NGƯỜI HỖ TRỢ ---
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
                    string anhLog = string.IsNullOrEmpty(itWifi?.DuongDanAnh) ? "Không có ảnh" : $"Ảnh: {itWifi.DuongDanAnh}";
                    var lichSu = new LichSuFormIt
                    {
                        IdFormIt = form.Id,
                        TieuDe = "Khởi tạo đơn Wifi",
                        Mota = $"Người tạo: {userName} (ID: {userId}) | Công ty: {tenCongTy} | Thiết bị: {itWifi?.LoaiThietBi} | MAC: {itWifi?.MacTb}. " +
                               $"{supporterLog}. {fileLog}. {anhLog}",
                        Time = DateTime.Now
                    };
                    _context.LichSuFormIts.Add(lichSu);

                    await _context.SaveChangesAsync();
                    await transaction.CommitAsync();


                    TempData["Success"] = "Gửi yêu cầu đăng ký Wifi thành công!";
                    return Redirect("/FormIT/DonCho");
                }
                catch (Exception ex)
                {
                    await transaction.RollbackAsync();
                    ViewBag.CongViecList = await _context.CongViecIts.OrderBy(x => x.Ten).ToListAsync();
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

            // --- THÊM: LẤY TÊN CÔNG TY ---
            var tenCongTy = User.FindFirst("TenCongTy")?.Value ?? "";

            // --- LẤY DANH SÁCH CÔNG VIỆC ---
            ViewBag.CongViecList = await _context.CongViecIts
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
                TenCongTy = tenCongTy, // Gán công ty vào model
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
            // --- LẤY THÔNG TIN TỪ CLAIMS ---
            var userIdClaim = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userIdClaim)) return Redirect("/DonXetDuyet/DangNhap");

            int userId = int.Parse(userIdClaim);
            var userName = User.Identity.Name ?? "";
            var phongBan = User.FindFirst("PhongBan")?.Value ?? "";
            var viTri = User.FindFirst(System.Security.Claims.ClaimTypes.Role)?.Value
                        ?? User.FindFirst("UserRole")?.Value ?? "";
            var userEmail = User.FindFirst(System.Security.Claims.ClaimTypes.Email)?.Value ?? "";
            var tenCongTy = User.FindFirst("TenCongTy")?.Value ?? "";

            using (var transaction = await _context.Database.BeginTransactionAsync())
            {
                try
                {
                    // --- TRUY VẤN THÔNG TIN CÔNG VIỆC VÀ NGƯỜI HỖ TRỢ LIÊN QUAN ---
                    var congViec = await _context.CongViecIts
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
                    form.TenCongTy = tenCongTy;
                    form.TrangThai = "ChoDuyet";
                    form.IdForm = "IT_DangKiSuDungDTBan_4";
                    form.TenForm = "Đơn đăng ký sử dụng điện thoại bàn";

                    if (congViec != null) form.Danhmuc = congViec.Ten;

                    _context.FormIts.Add(form);
                    await _context.SaveChangesAsync();

                    // --- CẤU HÌNH ĐƯỜNG DẪN MẠNG ---
                    string networkPath = @"\\10.0.60.30\BPVN-Fileserver\Public\IT-Information Technology Dept\5.E-Form\DonIT";
                    if (!Directory.Exists(networkPath)) Directory.CreateDirectory(networkPath);

                    string safeName = RemoveSign4VietnameseString(userName).Replace(" ", "");
                    string timeStamp = DateTime.Now.ToString("ddMMyy_HHmmss");

                    // --- BƯỚC 2: XỬ LÝ FILE ĐÍNH KÈM (UploadFile) ---
                    var uploadFile = Request.Form.Files["UploadFile"];
                    string fileLog = "Không có tệp đính kèm";

                    if (uploadFile != null && uploadFile.Length > 0)
                    {
                        string extension = Path.GetExtension(uploadFile.FileName);
                        string fileName = $"DonDTBan_ID{form.Id}_{safeName}_{timeStamp}{extension}";
                        string fullPath = Path.Combine(networkPath, fileName);

                        using (var fileStream = new FileStream(fullPath, FileMode.Create))
                        {
                            await uploadFile.CopyToAsync(fileStream);
                        }

                        form.FileDinhKem = fileName;
                        _context.Entry(form).Property(x => x.FileDinhKem).IsModified = true;
                        fileLog = $"Đính kèm tệp: {fileName}";
                        await _context.SaveChangesAsync();
                    }

                    // --- BƯỚC 3: LƯU CHI TIẾT (ItDangKiSuDungDtban4) & XỬ LÝ ẢNH ---
                    if (itDtBan != null)
                    {
                        itDtBan.IdFormIt = form.Id;

                        // Xử lý lưu ảnh vật lý thay vì dùng GetFileBytesAsync2
                        var anhFile = Request.Form.Files["Anh"];
                        if (anhFile != null && anhFile.Length > 0)
                        {
                            string imgExtension = Path.GetExtension(anhFile.FileName);
                            if (string.IsNullOrEmpty(imgExtension)) imgExtension = ".jpg";

                            string imgFileName = $"AnhDTBan_ID{form.Id}_{safeName}_{timeStamp}{imgExtension}";
                            string imgFullPath = Path.Combine(networkPath, imgFileName);

                            using (var imgStream = new FileStream(imgFullPath, FileMode.Create))
                            {
                                await anhFile.CopyToAsync(imgStream);
                            }

                            // Lưu tên file vào cột DuongDanAnh và gán Anh (byte[]) là null
                            itDtBan.DuongDanAnh = imgFileName;
                            itDtBan.Anh = null;
                        }

                        _context.ItDangKiSuDungDtban4s.Add(itDtBan);
                        await _context.SaveChangesAsync();
                    }

                    // --- BƯỚC 4: LƯU NGƯỜI HỖ TRỢ ---
                    string supporterLog = "Chưa gán người hỗ trợ";
                    if (congViec != null && congViec.IdItNguoiHoTroNavigation != null)
                    {
                        var chiTietHoTro = new ItCtNguoiHoTro
                        {
                            IdFormIt = form.Id,
                            IdItNguoiHoTro = congViec.IdItNguoiHoTroNavigation.Id,
                            Stt = 1
                        };
                        _context.ItCtNguoiHoTros.Add(chiTietHoTro);
                        supporterLog = $"Đã gán: {congViec.IdItNguoiHoTroNavigation.Ten}";
                        await _context.SaveChangesAsync();
                    }

                    // --- BƯỚC 5: LƯU LỊCH SỬ ---
                    string anhLog = string.IsNullOrEmpty(itDtBan?.DuongDanAnh) ? "Không có ảnh" : $"Ảnh: {itDtBan.DuongDanAnh}";
                    var lichSu = new LichSuFormIt
                    {
                        IdFormIt = form.Id,
                        TieuDe = "Khởi tạo đơn Điện thoại bàn",
                        Mota = $"Người thao tác: {userName} | Công ty: {tenCongTy} | Công việc: {form.Danhmuc}. " +
                               $"Nội dung: {itDtBan?.ThongTin}. {supporterLog}. {fileLog}. {anhLog}",
                        Time = DateTime.Now
                    };
                    _context.LichSuFormIts.Add(lichSu);
                    await _context.SaveChangesAsync();

                    await transaction.CommitAsync();

                    TempData["Success"] = "Gửi đơn đăng ký điện thoại bàn thành công!";
                    return Redirect("/FormIT/DonCho");
                }
                catch (Exception ex)
                {
                    await transaction.RollbackAsync();
                    ViewBag.CongViecList = await _context.CongViecIts.Where(x => x.Ten.Contains("Đăng kí sử dụng điện thoại")).ToListAsync();
                    ModelState.AddModelError("", "Lỗi hệ thống: " + ex.Message);
                    return View(form);
                }
            }
        }

        #endregion

        #region Đơn Đăng ký Tài khoản Hệ thống (Form IT 5)

        [HttpGet("/FormIT/DonTaiKhoanHeThong")]
        public async Task<IActionResult> DonTaiKhoanHeThong()
        {
            // --- LẤY THÔNG TIN TỪ CLAIMS ---
            var userIdClaim = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userIdClaim)) return Redirect("/DonXetDuyet/DangNhap");

            int userId = int.Parse(userIdClaim);
            var userName = User.Identity.Name ?? "";
            var phongBan = User.FindFirst("PhongBan")?.Value ?? "";
            var userRole = User.FindFirst(System.Security.Claims.ClaimTypes.Role) ?? User.FindFirst("UserRole");
            var roleName = userRole?.Value ?? "";
            var userEmail = User.FindFirst(System.Security.Claims.ClaimTypes.Email)?.Value ?? "";
            var tenCongTy = User.FindFirst("TenCongTy")?.Value ?? "";

            // --- LẤY DANH SÁCH CÔNG VIỆC LIÊN QUAN ĐẾN TÀI KHOẢN ---
            ViewBag.CongViecList = await _context.CongViecIts
                .Where(x => x.Ten.Contains("tài khoản") || x.Ten.Contains("Account"))
                .OrderBy(x => x.Ten)
                .ToListAsync();

            // Khởi tạo Model bảng chính
            var model = new FormIt
            {
                TenNguoiNv = userName,
                BoPhan = phongBan,
                ViTri = roleName,
                SoNhanVien = userEmail,
                TenCongTy = tenCongTy,
                Ngay = DateOnly.FromDateTime(DateTime.Now),
                IdNguoiTao = userId,
                TenNguoiTao = userName,
                TimeNguoiTao = DateTime.Now,
                TrangThai = "ChoDuyet"
            };

            return View(model);
        }

        [HttpPost("/FormIT/DonTaiKhoanHeThong")]
        public async Task<IActionResult> DonTaiKhoanHeThong(FormIt form, [FromForm] ItDangKiTaiKhoanHeThong5 itTaiKhoan, int SelectedCongViecId)
        {
            // --- LẤY THÔNG TIN TỪ CLAIMS ---
            var userIdClaim = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userIdClaim)) return Redirect("/DonXetDuyet/DangNhap");

            int userId = int.Parse(userIdClaim);
            var userName = User.Identity.Name ?? "";
            var phongBan = User.FindFirst("PhongBan")?.Value ?? "";
            var userRole = User.FindFirst(System.Security.Claims.ClaimTypes.Role) ?? User.FindFirst("UserRole");
            var roleName = userRole?.Value ?? "";
            var userEmail = User.FindFirst(System.Security.Claims.ClaimTypes.Email)?.Value ?? "";
            var tenCongTy = User.FindFirst("TenCongTy")?.Value ?? "";

            using (var transaction = await _context.Database.BeginTransactionAsync())
            {
                try
                {
                    // --- TRUY VẤN CÔNG VIỆC VÀ NGƯỜI HỖ TRỢ ---
                    var congViec = await _context.CongViecIts
                        .Include(c => c.IdItNguoiHoTroNavigation)
                        .FirstOrDefaultAsync(c => c.Id == SelectedCongViecId);

                    // --- BƯỚC 1: LƯU BẢNG CHÍNH FormIt ---
                    form.Ngay = DateOnly.FromDateTime(DateTime.Now);
                    form.IdNguoiTao = userId;
                    form.TenNguoiTao = userName;
                    form.TimeNguoiTao = DateTime.Now;
                    form.TenNguoiNv = userName;
                    form.BoPhan = phongBan;
                    form.ViTri = roleName;
                    form.SoNhanVien = userEmail;
                    form.TenCongTy = tenCongTy;
                    form.TrangThai = "ChoDuyet";
                    form.IdForm = "IT_DangKiTaiKhoanHeThong_5";
                    form.TenForm = "Đơn đăng ký tài khoản hệ thống";

                    if (congViec != null) form.Danhmuc = congViec.Ten;

                    _context.FormIts.Add(form);
                    await _context.SaveChangesAsync();

                    // --- CẤU HÌNH ĐƯỜNG DẪN MẠNG ---
                    string networkPath = @"\\10.0.60.30\BPVN-Fileserver\Public\IT-Information Technology Dept\5.E-Form\DonIT";
                    if (!Directory.Exists(networkPath)) Directory.CreateDirectory(networkPath);

                    string safeName = RemoveSign4VietnameseString(userName).Replace(" ", "");
                    string timeStamp = DateTime.Now.ToString("ddMMyy_HHmmss");

                    // --- BƯỚC 2: XỬ LÝ FILE ĐÍNH KÈM (UploadFile) ---
                    var uploadFile = Request.Form.Files["UploadFile"];
                    string fileLog = "Không có tệp đính kèm";

                    if (uploadFile != null && uploadFile.Length > 0)
                    {
                        string extension = Path.GetExtension(uploadFile.FileName);
                        string fileName = $"DonTKHT_ID{form.Id}_{safeName}_{timeStamp}{extension}";
                        string fullPath = Path.Combine(networkPath, fileName);

                        using (var fileStream = new FileStream(fullPath, FileMode.Create))
                        {
                            await uploadFile.CopyToAsync(fileStream);
                        }

                        form.FileDinhKem = fileName;
                        _context.Entry(form).Property(x => x.FileDinhKem).IsModified = true;
                        fileLog = $"Đính kèm tệp: {fileName}";
                        await _context.SaveChangesAsync();
                    }

                    // --- BƯỚC 3: LƯU CHI TIẾT (ItDangKiTaiKhoanHeThong5) & XỬ LÝ ẢNH ---
                    if (itTaiKhoan != null)
                    {
                        itTaiKhoan.IdFormIt = form.Id;

                        var anhFile = Request.Form.Files["Anh"];
                        if (anhFile != null && anhFile.Length > 0)
                        {
                            string imgExtension = Path.GetExtension(anhFile.FileName);
                            if (string.IsNullOrEmpty(imgExtension)) imgExtension = ".jpg";

                            string imgFileName = $"AnhTKHT_ID{form.Id}_{safeName}_{timeStamp}{imgExtension}";
                            string imgFullPath = Path.Combine(networkPath, imgFileName);

                            using (var imgStream = new FileStream(imgFullPath, FileMode.Create))
                            {
                                await anhFile.CopyToAsync(imgStream);
                            }

                            itTaiKhoan.DuongDanAnh = imgFileName;
                            itTaiKhoan.Anh = null; // Đảm bảo không lưu byte[] vào DB nếu đã dùng file
                        }

                        _context.ItDangKiTaiKhoanHeThong5s.Add(itTaiKhoan);
                        await _context.SaveChangesAsync();
                    }

                    // --- BƯỚC 4: LƯU NGƯỜI HỖ TRỢ ---
                    string supporterLog = "Chưa gán người hỗ trợ";
                    if (congViec != null && congViec.IdItNguoiHoTroNavigation != null)
                    {
                        var chiTietHoTro = new ItCtNguoiHoTro
                        {
                            IdFormIt = form.Id,
                            IdItNguoiHoTro = congViec.IdItNguoiHoTroNavigation.Id,
                            Stt = 1
                        };
                        _context.ItCtNguoiHoTros.Add(chiTietHoTro);
                        supporterLog = $"Đã gán: {congViec.IdItNguoiHoTroNavigation.Ten}";
                        await _context.SaveChangesAsync();
                    }

                    // --- BƯỚC 5: LƯU LỊCH SỬ ---
                    string anhLog = string.IsNullOrEmpty(itTaiKhoan?.DuongDanAnh) ? "Không có ảnh" : $"Ảnh: {itTaiKhoan.DuongDanAnh}";
                    var lichSu = new LichSuFormIt
                    {
                        IdFormIt = form.Id,
                        TieuDe = "Khởi tạo đơn Tài khoản Hệ thống",
                        Mota = $"Người thao tác: {userName} | Hệ thống: {itTaiKhoan?.HeThongNao} | Loại đơn: {itTaiKhoan?.LoaiDon}. " +
                               $"Cấp quyền giống: {itTaiKhoan?.CapQuyenGiongAi}. {supporterLog}. {fileLog}. {anhLog}",
                        Time = DateTime.Now
                    };
                    _context.LichSuFormIts.Add(lichSu);
                    await _context.SaveChangesAsync();

                    await transaction.CommitAsync();

                    TempData["Success"] = "Gửi đơn đăng ký tài khoản hệ thống thành công!";
                    return Redirect("/FormIT/DonCho");
                }
                catch (Exception ex)
                {
                    await transaction.RollbackAsync();
                    ViewBag.CongViecList = await _context.CongViecIts.Where(x => x.Ten.Contains("tài khoản")).ToListAsync();
                    ModelState.AddModelError("", "Lỗi hệ thống: " + ex.Message);
                    return View(form);
                }
            }
        }

        #endregion

        #region Don Tai Khoan May Tinh (Form IT 6)

        [HttpGet("/FormIT/DonTaiKhoanMayTinh")]
        public IActionResult DonTaiKhoanMayTinh()
        {
            if (User == null || !User.Identity.IsAuthenticated)
                return Redirect("/DonXetDuyet/DangNhap");

            // Lọc danh sách nhân viên IT và lấy những công việc có tên "Đăng kí tài khoản máy tính"
            ViewBag.ListNguoiHoTro = _context.ItNguoiHoTros
                .Include(x => x.CongViecIts)
                .Where(x => x.BoPhan == "IT")
                .Select(x => new E_Form_Best.Models.ITForm.ItNguoiHoTro
                {
                    Id = x.Id,
                    MaNv = x.MaNv,
                    Ten = x.Ten,
                    BoPhan = x.BoPhan,
                    GhiChu = x.GhiChu,
                    CongViecIts = x.CongViecIts.Where(cv => cv.Ten == "Đăng kí tài khoản máy tính").ToList()
                })
                .Where(x => x.CongViecIts.Any())
                .ToList();

            // Lấy thông tin User từ Claims
            var userIdStr = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
            int? userId = !string.IsNullOrEmpty(userIdStr) && int.TryParse(userIdStr, out var tmpId) ? tmpId : null;

            string userName = User.Identity.Name ?? "";
            string phongBan = User.FindFirst("PhongBan")?.Value ?? "";
            string userRole = User.FindFirst("UserRole")?.Value ?? "";
            string userEmail = User.FindFirst(System.Security.Claims.ClaimTypes.Email)?.Value ?? "";
            string tenCongTy = User.FindFirst("TenCongTy")?.Value ?? "";

            var model = new FormIt
            {
                TenNguoiNv = userName,
                BoPhan = phongBan,
                ViTri = userRole,
                SoNhanVien = userEmail,
                TenCongTy = tenCongTy,
                Ngay = DateOnly.FromDateTime(DateTime.Now),
                IdNguoiTao = userId,
                TenNguoiTao = userName,
                TimeNguoiTao = DateTime.Now,
                TrangThai = "ChoDuyet"
            };

            return View(model);
        }

        [HttpPost("/FormIT/DonTaiKhoanMayTinh")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DonTaiKhoanMayTinh(FormIt form, [FromForm] ItDangkiTaiKhoanMayTinh6 itAccount, int[] SelectedCongViecIds)
        {
            // 1. Kiểm tra đăng nhập
            if (User == null || !User.Identity.IsAuthenticated)
                return Redirect("/DonXetDuyet/DangNhap");

            // 2. Lấy thông tin User từ Claims
            var userIdStr = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
            int.TryParse(userIdStr, out int userId);

            var userName = User.Identity.Name ?? "";
            var phongBan = User.FindFirst("PhongBan")?.Value ?? "";
            var viTri = User.FindFirst("UserRole")?.Value ?? "";
            var userEmail = User.FindFirst(System.Security.Claims.ClaimTypes.Email)?.Value ?? "";
            var tenCongTy = User.FindFirst("TenCongTy")?.Value ?? "";

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
                    form.TenCongTy = tenCongTy;
                    form.TrangThai = "ChoDuyet";
                    form.IdForm = "IT_DangkiTaiKhoanMayTinh_6"; // Định danh form mới
                    form.TenForm = "Đăng ký Tài khoản Máy tính / Ổ chung";
                    form.Danhmuc = "Đăng kí tài khoản máy tính";

                    _context.FormIts.Add(form);
                    await _context.SaveChangesAsync();

                    // --- CẤU HÌNH ĐƯỜNG DẪN LƯU FILE ---
                    string networkPath = @"\\10.0.60.30\BPVN-Fileserver\Public\IT-Information Technology Dept\5.E-Form\DonIT";
                    if (!Directory.Exists(networkPath)) Directory.CreateDirectory(networkPath);

                    string safeName = RemoveSign4VietnameseString(userName).Replace(" ", "");
                    string timeStamp = DateTime.Now.ToString("ddMMyy_HHmmss");

                    // --- BƯỚC 2: XỬ LÝ FILE ĐÍNH KÈM (UploadFile) ---
                    var uploadFile = Request.Form.Files["UploadFile"];
                    if (uploadFile != null && uploadFile.Length > 0)
                    {
                        string extension = Path.GetExtension(uploadFile.FileName);
                        string fileName = $"DonAcc_ID{form.Id}_{safeName}_{timeStamp}{extension}";
                        string fullPath = Path.Combine(networkPath, fileName);

                        using (var fileStream = new FileStream(fullPath, FileMode.Create))
                        {
                            await uploadFile.CopyToAsync(fileStream);
                        }

                        form.FileDinhKem = fileName;
                        _context.Entry(form).Property(x => x.FileDinhKem).IsModified = true;
                        await _context.SaveChangesAsync();
                    }

                    // --- BƯỚC 3: CHI TIẾT TÀI KHOẢN & XỬ LÝ ẢNH ---
                    if (itAccount != null)
                    {
                        itAccount.IdFormIt = form.Id;

                        var anhFile = Request.Form.Files["Anh"];
                        if (anhFile != null && anhFile.Length > 0)
                        {
                            string imgExtension = Path.GetExtension(anhFile.FileName);
                            if (string.IsNullOrEmpty(imgExtension)) imgExtension = ".jpg";

                            string imgFileName = $"AnhAcc_ID{form.Id}_{safeName}_{timeStamp}{imgExtension}";
                            string imgFullPath = Path.Combine(networkPath, imgFileName);

                            using (var imgStream = new FileStream(imgFullPath, FileMode.Create))
                            {
                                await anhFile.CopyToAsync(imgStream);
                            }

                            itAccount.DuongDanAnh = imgFileName;
                        }

                        _context.ItDangkiTaiKhoanMayTinh6s.Add(itAccount);
                        await _context.SaveChangesAsync();
                    }

                    // --- BƯỚC 4: LƯU NGƯỜI HỖ TRỢ ---
                    string danhSachTenHoTro = "Chưa chọn";
                    List<int> selectedItNguoiHoTroIds = new List<int>();

                    if (SelectedCongViecIds != null && SelectedCongViecIds.Length > 0)
                    {
                        selectedItNguoiHoTroIds = await _context.CongViecIts
                            .Where(cv => SelectedCongViecIds.Contains(cv.Id) && cv.IdItNguoiHoTro != null)
                            .Select(cv => cv.IdItNguoiHoTro!.Value)
                            .Distinct()
                            .ToListAsync();
                    }

                    if (!selectedItNguoiHoTroIds.Any()) // Nếu không chọn, mặc định lấy theo danh mục
                    {
                        selectedItNguoiHoTroIds = await _context.CongViecIts
                            .Where(cv => cv.Ten == "Đăng kí tài khoản máy tính" && cv.IdItNguoiHoTro != null)
                            .Select(cv => cv.IdItNguoiHoTro!.Value)
                            .Distinct()
                            .ToListAsync();
                    }

                    if (selectedItNguoiHoTroIds.Any())
                    {
                        var listHoTro = await _context.ItNguoiHoTros
                            .Where(x => selectedItNguoiHoTroIds.Contains(x.Id))
                            .Select(x => x.Ten)
                            .ToListAsync();

                        danhSachTenHoTro = string.Join(", ", listHoTro);

                        int stt = 1;
                        foreach (var idHoTro in selectedItNguoiHoTroIds)
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

                    // --- BƯỚC 5: LƯU LỊCH SỬ THAY ĐỔI ---
                    string moTaChiTiet = $"[Khởi tạo đơn] Loại yêu cầu: {itAccount?.Loai}\n" +
                                         $"- Người sử dụng: {itAccount?.HoTenVaMaNhanVienNguoiSuDung}\n" +
                                         $"- Máy tính: {itAccount?.TenMayCanCai} | Ổ chung: {itAccount?.TenOchungCanAddQuyen}\n" +
                                         $"- Mục đích: {itAccount?.MucDich}\n" +
                                         $"- Người hỗ trợ: {danhSachTenHoTro}.";

                    var lichSu = new LichSuFormIt
                    {
                        IdFormIt = form.Id,
                        TieuDe = "Khởi tạo đơn",
                        Mota = moTaChiTiet,
                        Time = DateTime.Now
                    };
                    _context.LichSuFormIts.Add(lichSu);
                    await _context.SaveChangesAsync();

                    await transaction.CommitAsync();

                    TempData["Success"] = "Gửi đơn đăng ký tài khoản thành công!";
                    return Redirect("/FormIT/DonCho");
                }
                catch (Exception ex)
                {
                    await transaction.RollbackAsync();

                    // Load lại list hỗ trợ khi lỗi
                    ViewBag.ListNguoiHoTro = _context.ItNguoiHoTros
                        .Include(x => x.CongViecIts)
                        .Where(x => x.BoPhan == "IT")
                        .Select(x => new E_Form_Best.Models.ITForm.ItNguoiHoTro
                        {
                            Id = x.Id,
                            MaNv = x.MaNv,
                            Ten = x.Ten,
                            CongViecIts = x.CongViecIts.Where(cv => cv.Ten == "Đăng kí tài khoản máy tính").ToList()
                        })
                        .Where(x => x.CongViecIts.Any())
                        .ToList();

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
            // 1. Kiểm tra đăng nhập
            var userIdStr = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userIdStr)) return RedirectToAction("DangNhap", "DonXetDuyet");

            int userId = int.Parse(userIdStr);

            // Lấy Email và Công ty từ Claims (giống logic đồng bộ bên HR)
            var userEmail = User.FindFirst(System.Security.Claims.ClaimTypes.Email)?.Value ?? "";
            var tenCongTy = User.FindFirst("TenCongTy")?.Value?.Trim() ?? "";

            // 2. Lấy dữ liệu đơn (Giữ nguyên toàn bộ các Include hiện có)
            var don = await _context.FormIts
                .Include(f => f.ItMail1s)
                .Include(f => f.ItOrderIt2s)
                .Include(f => f.ItDangKiSuDungWifi3s)
                .Include(f => f.ItDangKiSuDungDtban4s)
                .Include(f => f.ItDangKiTaiKhoanHeThong5s)
                .Include(f => f.ItDangkiTaiKhoanMayTinh6s)
                .Include(f => f.ItCtNguoiHoTros)
                    .ThenInclude(ct => ct.IdItNguoiHoTroNavigation)
                .Include(f => f.LichSuFormIts)
                .Include(f => f.DanhGiaFormIts)
                .Include(f => f.BinhLuanFormIts)
                .FirstOrDefaultAsync(m => m.Id == id);

            if (don == null)
            {
                TempData["Error"] = "⚠️ Không tìm thấy đơn yêu cầu IT!";
                return RedirectToAction("LichSuIT"); // Hoặc trang danh sách tương ứng
            }

            // 3. KIỂM TRA QUYỀN XEM (Đồng bộ logic phân quyền)

            // Kiểm tra cùng công ty (trừ quyền All)
            if (don.TenCongTy?.Trim() != tenCongTy && !User.IsInRole("All"))
            {
                return Forbid();
            }

            bool hasAccess = false;

            if (User.IsInRole("All"))
            {
                hasAccess = true;
            }
            else if (User.IsInRole("AdminIT"))
            {
                // AdminIT xem được nếu đơn đã duyệt VÀ (là Admin xử lý hoặc có tên trong danh sách hỗ trợ)
                bool isSupporter = don.ItCtNguoiHoTros.Any(ct => ct.IdItNguoiHoTroNavigation?.MaNv == userEmail);
                if (don.IdNguoiDuyet != null && (don.IdAdmin == userId || isSupporter))
                {
                    hasAccess = true;
                }
            }
            else if (User.IsInRole("QuanLyDuyetDonIT"))
            {
                // Quản lý xem được đơn mình tạo hoặc mình duyệt
                if (don.IdNguoiTao == userId || don.IdNguoiDuyet == userId)
                {
                    hasAccess = true;
                }
            }
            else
            {
                // User thường xem được đơn mình tạo hoặc mình là người hỗ trợ
                bool isSupporter = don.ItCtNguoiHoTros.Any(ct => ct.IdItNguoiHoTroNavigation?.MaNv == userEmail);
                if (don.IdNguoiTao == userId || isSupporter)
                {
                    hasAccess = true;
                }
            }

            if (!hasAccess) return Forbid();

            // 4. Xử lý dữ liệu hiển thị
            if (don.LichSuFormIts != null)
            {
                don.LichSuFormIts = don.LichSuFormIts.OrderByDescending(x => x.Time).ToList();
            }

            // Gán danh sách hỗ trợ cho Admin để thực hiện điều phối
            if (User.IsInRole("AdminIT") || User.IsInRole("All"))
            {
                ViewBag.ListNguoiHoTro = await _context.ItNguoiHoTros
                    .Where(x => x.BoPhan == "IT")
                    .AsNoTracking()
                    .ToListAsync();
            }

            ViewBag.CurrentUserId = userId;
            ViewBag.UserEmail = userEmail;

            return View(don);
        }
        // --- ACTION DOWNLOAD / XEM FILE (Giữ nguyên 100%) ---
        [HttpGet("/FormIT/DownloadFile/{fileName}")]
        public async Task<IActionResult> DownloadFile(string fileName)
        {
            if (string.IsNullOrEmpty(fileName)) return NotFound();

            string networkPath = @"\\10.0.60.30\BPVN-Fileserver\Public\IT-Information Technology Dept\5.E-Form\DonIT";
            string fullPath = Path.Combine(networkPath, fileName);

            if (!System.IO.File.Exists(fullPath))
                return NotFound("Tệp tin không tồn tại.");

            var memory = new MemoryStream();
            using (var stream = new FileStream(fullPath, FileMode.Open, FileAccess.Read))
            {
                await stream.CopyToAsync(memory);
            }
            memory.Position = 0;

            string ext = Path.GetExtension(fileName).ToLowerInvariant();
            string contentType = ext switch
            {
                ".jpg" or ".jpeg" => "image/jpeg",
                ".png" => "image/png",
                ".gif" => "image/gif",
                ".webp" => "image/webp",
                ".pdf" => "application/pdf",
                ".xlsx" => "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                _ => "application/octet-stream"
            };

            return contentType.StartsWith("image/")
                ? File(memory, contentType)
                : File(memory, contentType, fileName);
        }

        // --- ACTION CHỈ ĐỊNH / THAY ĐỔI NGƯỜI HỖ TRỢ (Giữ nguyên logic nghiệp vụ) ---
        [HttpPost("/FormIT/ThemNguoiHoTro")]
        public async Task<IActionResult> ThemNguoiHoTro([FromBody] System.Text.Json.JsonElement data)
        {
            // Lưu ý: Mặc dù ai cũng xem được, nhưng thao tác thay đổi người hỗ trợ vẫn nên giữ phân quyền
            var roles = User.FindAll(System.Security.Claims.ClaimTypes.Role).Select(r => r.Value).ToList();
            if (!roles.Any(r => r == "AdminIT" || r == "All"))
            {
                return Json(new { success = false, message = "Bạn không có quyền thực hiện thao tác này!" });
            }

            try
            {
                int idForm = data.GetProperty("idFormIt").GetInt32();
                string maNvMoi = data.GetProperty("maNv").GetString();

                var nvIt = await _context.ItNguoiHoTros.FirstOrDefaultAsync(x => x.MaNv == maNvMoi);
                if (nvIt == null) return Json(new { success = false, message = "Mã nhân viên IT không tồn tại!" });

                var hienTai = await _context.ItCtNguoiHoTros
                    .Include(x => x.IdItNguoiHoTroNavigation)
                    .Where(x => x.IdFormIt == idForm)
                    .OrderByDescending(x => x.Stt)
                    .FirstOrDefaultAsync();

                if (hienTai != null && hienTai.IdItNguoiHoTroNavigation?.MaNv == maNvMoi)
                {
                    return Json(new { success = false, message = $"Nhân viên {nvIt.Ten} đã được chỉ định trước đó!" });
                }

                var ctMoi = new ItCtNguoiHoTro
                {
                    IdFormIt = idForm,
                    IdItNguoiHoTro = nvIt.Id,
                    Stt = (hienTai?.Stt ?? 0) + 1
                };
                _context.ItCtNguoiHoTros.Add(ctMoi);

                _context.LichSuFormIts.Add(new LichSuFormIt
                {
                    IdFormIt = idForm,
                    TieuDe = "CHỈ ĐỊNH NGƯỜI HỖ TRỢ",
                    Mota = $"{User.Identity.Name} đã thay đổi người hỗ trợ sang: {nvIt.Ten}.",
                    Time = DateTime.Now
                });

                await _context.SaveChangesAsync();
                return Json(new { success = true, message = "Đã cập nhật người hỗ trợ mới!" });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = "Lỗi hệ thống: " + ex.Message });
            }
        }

        #endregion

        #region BÌNH LUẬN ĐƠN IT

        [HttpGet("/FormIT/LayBinhLuan/{idForm}")]
        public async Task<IActionResult> LayBinhLuan(int idForm, int skip = 0, int take = 20)
        {
            try
            {
                // 1. Truy vấn lấy dữ liệu từ database
                var binhLuans = await _context.BinhLuanFormIts
                    .Where(bl => bl.IdForm == idForm && bl.TrangThai == "Active")
                    // Sắp xếp giảm dần để lấy những bản ghi MỚI NHẤT trước
                    .OrderByDescending(bl => bl.ThoiGian)
                    .Skip(skip)
                    .Take(take)
                    .Select(bl => new
                    {
                        id = bl.Id,
                        noiDung = bl.NoiDung,
                        tenNguoiBinhLuan = bl.TenNguoiBinhLuan,
                        idNguoiBinhLuan = bl.IdNguoiBinhLuan,
                        ma = bl.Ma,
                        phongBan = bl.PhongBan,
                        tenCongTy = bl.TenCongTy,
                        thoiGian = bl.ThoiGian,
                        fileDinhKem = bl.FileDinhKem,
                        trangThai = bl.TrangThai
                    })
                    .ToListAsync();

                // 2. Đảo ngược danh sách kết quả để cái mới nhất nằm ở cuối cùng của mảng trả về
                // Sử dụng Enumerable.Reverse() hoặc sắp xếp lại theo ThoiGian tăng dần
                var resultData = binhLuans.OrderBy(bl => bl.thoiGian).ToList();

                return Json(new { success = true, data = resultData });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = ex.Message });
            }
        }

        [HttpPost("/FormIT/ThemBinhLuan")]
        public async Task<IActionResult> ThemBinhLuan()
        {
            try
            {
                var idForm = int.Parse(Request.Form["idForm"]);
                var noiDung = Request.Form["noiDung"].ToString();
                var file = Request.Form.Files.GetFile("file");

                // Lấy đầy đủ thông tin từ Claims
                var userIdStr = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                var userName = User.Identity?.Name ?? "Unknown";
                var userMa = User.FindFirst(ClaimTypes.Email)?.Value ?? "";
                var userPhongBan = User.FindFirst("PhongBan")?.Value ?? "";
                var userTenCongTy = User.FindFirst("TenCongTy")?.Value ?? "";

                if (string.IsNullOrEmpty(userIdStr))
                    return Json(new { success = false, message = "Chưa đăng nhập" });

                // Kiểm tra đơn có tồn tại không
                var formIt = await _context.FormIts.FindAsync(idForm);
                if (formIt == null)
                    return Json(new { success = false, message = "Không tìm thấy đơn" });

                string fileName = null;

                // Xử lý file đính kèm
                if (file != null && file.Length > 0)
                {
                    // Kiểm tra kích thước file (max 50MB)
                    if (file.Length > 50 * 1024 * 1024)
                        return Json(new { success = false, message = "File không được vượt quá 50MB" });

                    string networkPath = @"\\10.0.60.30\BPVN-Fileserver\Public\IT-Information Technology Dept\5.E-Form\BinhLuanDonIT";

                    try
                    {
                        if (!Directory.Exists(networkPath))
                            Directory.CreateDirectory(networkPath);
                    }
                    catch (Exception ex)
                    {
                        return Json(new { success = false, message = "Không thể truy cập thư mục lưu trữ: " + ex.Message });
                    }

                    // Tạo tên file unique
                    string safeFileName = Path.GetFileName(file.FileName);
                    fileName = $"{DateTime.Now:yyyyMMdd_HHmmss}_{Guid.NewGuid().ToString().Substring(0, 8)}_{safeFileName}";
                    string fullPath = Path.Combine(networkPath, fileName);

                    try
                    {
                        using (var stream = new FileStream(fullPath, FileMode.Create))
                        {
                            await file.CopyToAsync(stream);
                        }
                    }
                    catch (Exception ex)
                    {
                        return Json(new { success = false, message = "Không thể lưu file: " + ex.Message });
                    }
                }

                // Kiểm tra nội dung không được rỗng nếu không có file
                if (string.IsNullOrWhiteSpace(noiDung) && file == null)
                    return Json(new { success = false, message = "Vui lòng nhập nội dung hoặc đính kèm file" });

                // Lưu bình luận
                var binhLuan = new BinhLuanFormIt
                {
                    IdForm = idForm,
                    NoiDung = noiDung?.Trim(),
                    IdNguoiBinhLuan = userIdStr,
                    TenNguoiBinhLuan = userName,
                    Ma = userMa,
                    PhongBan = userPhongBan,
                    TenCongTy = userTenCongTy,
                    ThoiGian = DateTime.Now,
                    TrangThai = "Active",
                    FileDinhKem = fileName
                };

                _context.BinhLuanFormIts.Add(binhLuan);
                await _context.SaveChangesAsync();

                // Tạo lịch sử
                var lichSu = new LichSuFormIt
                {
                    IdFormIt = idForm,
                    TieuDe = "BÌNH LUẬN MỚI",
                    Mota = $"👤 {userName} ({userMa})\n" +
                           $"🏢 {userPhongBan} - {userTenCongTy}\n" +
                           $"💬 {(string.IsNullOrWhiteSpace(noiDung) ? "[File đính kèm]" : (noiDung.Length > 50 ? noiDung.Substring(0, 50) + "..." : noiDung))}\n" +
                           $"{(fileName != null ? "📎 Có đính kèm file" : "")}",
                    Time = DateTime.Now
                };
                _context.LichSuFormIts.Add(lichSu);
                await _context.SaveChangesAsync();

                return Json(new
                {
                    success = true,
                    data = new
                    {
                        id = binhLuan.Id,
                        noiDung = binhLuan.NoiDung,
                        tenNguoiBinhLuan = binhLuan.TenNguoiBinhLuan,
                        idNguoiBinhLuan = binhLuan.IdNguoiBinhLuan,
                        ma = binhLuan.Ma,
                        phongBan = binhLuan.PhongBan,
                        tenCongTy = binhLuan.TenCongTy,
                        thoiGian = binhLuan.ThoiGian,
                        fileDinhKem = binhLuan.FileDinhKem,
                        trangThai = binhLuan.TrangThai
                    }
                });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = "Lỗi hệ thống: " + ex.Message });
            }
        }

        #region BÌNH LUẬN ĐƠN IT - XÓA BÌNH LUẬN (GIỚI HẠN 2 GIỜ)

        [HttpPost("/FormIT/XoaBinhLuan")]
        public async Task<IActionResult> XoaBinhLuan([FromBody] XoaBinhLuanRequest request)
        {
            try
            {
                // 1. Kiểm tra đầu vào
                if (request == null || request.id <= 0)
                    return Json(new { success = false, message = "Dữ liệu không hợp lệ" });

                // 2. Tìm bình luận trong Database
                var binhLuan = await _context.BinhLuanFormIts.FindAsync(request.id);
                if (binhLuan == null)
                    return Json(new { success = false, message = "Không tìm thấy bình luận" });

                // 3. Lấy thông tin người dùng hiện tại từ Claims
                var userIdStr = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
                var userName = User.Identity?.Name ?? "Unknown";
                var userRole = User.FindFirst("UserRole")?.Value ?? "";

                // 4. Kiểm tra thời gian (Sửa lỗi TimeSpan? an toàn)
                // Dùng toán tử ?. để lấy TotalHours nếu kết quả phép trừ không null, ngược lại mặc định là 999 giờ (hết hạn)
                double soGioDaTroiQua = (DateTime.Now - binhLuan.ThoiGian)?.TotalHours ?? 999;
                bool hetHanXoa = soGioDaTroiQua > 2;

                // 5. Kiểm tra quyền xóa
                bool isOwner = binhLuan.IdNguoiBinhLuan == userIdStr;
                bool isAdmin = userRole == "Admin" || userRole == "All";

                // Logic: Admin/All luôn được xóa. Người dùng thường chỉ được xóa của mình và PHẢI trong vòng 2 giờ.
                if (!isAdmin)
                {
                    if (!isOwner)
                        return Json(new { success = false, message = "Bạn không có quyền xóa bình luận này" });

                    if (hetHanXoa)
                        return Json(new { success = false, message = "Đã quá 2 giờ, bạn không thể xóa bình luận này nữa." });
                }

                // 6. Xử lý xóa file đính kèm trên FileServer (nếu có)
                if (!string.IsNullOrEmpty(binhLuan.FileDinhKem))
                {
                    string networkPath = @"\\10.0.60.30\BPVN-Fileserver\Public\IT-Information Technology Dept\5.E-Form\BinhLuanDonIT";
                    string fullPath = Path.Combine(networkPath, binhLuan.FileDinhKem);

                    try
                    {
                        if (System.IO.File.Exists(fullPath))
                        {
                            System.IO.File.Delete(fullPath);
                        }
                    }
                    catch (Exception fileEx)
                    {
                        // Chỉ log lỗi file, vẫn tiếp tục để xóa bản ghi trong DB tránh tắc nghẽn
                        Console.WriteLine($"Lỗi xóa file vật lý: {fileEx.Message}");
                    }
                }

                // 7. Xóa bản ghi trong Database
                // SỬA LỖI: Chuyển int? sang int bằng cách dùng ?? 0 để tránh lỗi 'Cannot implicitly convert type'
                int idFormTam = binhLuan.IdForm ?? 0;
                string tenNguoiBiXoa = binhLuan.TenNguoiBinhLuan ?? "Người dùng";

                _context.BinhLuanFormIts.Remove(binhLuan);
                await _context.SaveChangesAsync();

                // 8. Tạo lịch sử thao tác
                if (idFormTam > 0)
                {
                    var lichSu = new LichSuFormIt
                    {
                        IdFormIt = idFormTam,
                        TieuDe = "XÓA BÌNH LUẬN",
                        Mota = $"{userName} đã xóa bình luận của {tenNguoiBiXoa}",
                        Time = DateTime.Now
                    };
                    _context.LichSuFormIts.Add(lichSu);
                    await _context.SaveChangesAsync();
                }

                return Json(new { success = true, message = "Đã xóa bình luận thành công" });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = "Lỗi hệ thống: " + ex.Message });
            }
        }

        // Request DTO (Đặt bên ngoài hoặc bên trong Controller tùy cấu trúc của bạn)
        public class XoaBinhLuanRequest
        {
            public int id { get; set; }
        }

        #endregion

        [HttpGet("/FormIT/DownloadBinhLuanFile/{fileName}")]
        public async Task<IActionResult> DownloadBinhLuanFile(string fileName)
        {
            if (string.IsNullOrEmpty(fileName))
                return NotFound();

            string networkPath = @"\\10.0.60.30\BPVN-Fileserver\Public\IT-Information Technology Dept\5.E-Form\BinhLuanDonIT";
            string fullPath = Path.Combine(networkPath, fileName);

            if (!System.IO.File.Exists(fullPath))
                return NotFound("File không tồn tại");

            try
            {
                var memory = new MemoryStream();
                using (var stream = new FileStream(fullPath, FileMode.Open, FileAccess.Read))
                {
                    await stream.CopyToAsync(memory);
                }
                memory.Position = 0;

                // Xác định Content-Type
                string ext = Path.GetExtension(fileName).ToLowerInvariant();
                string contentType = ext switch
                {
                    ".jpg" or ".jpeg" => "image/jpeg",
                    ".png" => "image/png",
                    ".gif" => "image/gif",
                    ".pdf" => "application/pdf",
                    ".xlsx" => "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                    ".docx" => "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
                    ".txt" => "text/plain",
                    ".zip" => "application/zip",
                    _ => "application/octet-stream"
                };

                // Lấy tên file gốc (bỏ phần timestamp và GUID)
                string originalFileName = string.Join("_", fileName.Split('_').Skip(2));

                return File(memory, contentType, originalFileName);
            }
            catch (Exception ex)
            {
                return StatusCode(500, "Lỗi khi tải file: " + ex.Message);
            }
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
            // Đã thêm Include DanhGiaFormIts để phân biệt trạng thái Chờ đánh giá và Hoàn tất
            var danhSachDon = await _context.FormIts
                .Where(f => f.IdNguoiTao == userId)
                .Include(f => f.DanhGiaFormIts)
                .Include(f => f.ItMail1s)
                .Include(f => f.ItCtNguoiHoTros)
                    .ThenInclude(ct => ct.IdItNguoiHoTroNavigation)
                .OrderByDescending(f => f.Id)
                .ToListAsync();

            return View(danhSachDon);
        }

        #endregion

        #region XỬ LÝ ĐƠN IT (Duyệt / Hủy / Hoàn tất) - PHÂN QUYỀN MỚI 2026

        [HttpGet("/FormIT/QuanLyXetDuyet")]
        public async Task<IActionResult> QuanLyXetDuyet()
        {
            // --- 1. LẤY THÔNG TIN TỪ CLAIMS ---
            var userIdStr = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userIdStr)) return Redirect("/DonXetDuyet/DangNhap");
            int userId = int.Parse(userIdStr);

            // Lấy tất cả các Roles từ Claims (vì một User có thể có nhiều quyền)
            var userRoles = User.FindAll(System.Security.Claims.ClaimTypes.Role).Select(c => c.Value).ToList();

            var phongBan = User.FindFirst("PhongBan")?.Value?.Trim() ?? "";
            var tenCongTy = User.FindFirst("TenCongTy")?.Value?.Trim() ?? "";

            // Lấy danh sách bộ phận được quản lý (nếu có)
            var listTenBoPhanStr = User.FindFirst("TenBoPhan")?.Value ?? "";
            var listTenBoPhan = listTenBoPhanStr.Split(',')
                                                .Select(s => s.Trim().ToLower())
                                                .Where(s => !string.IsNullOrEmpty(s))
                                                .ToList();

            // Chặn Bảo vệ (nếu có quyền này)
            if (userRoles.Contains("BaoVe")) return Forbid();

            // --- 2. TRUY VẤN DỮ LIỆU ---
            IQueryable<FormIt> query = _context.FormIts
                .Include(f => f.ItCtNguoiHoTros)
                    .ThenInclude(ct => ct.IdItNguoiHoTroNavigation)
                .Include(f => f.DanhGiaFormIts);

            // --- 3. LOGIC PHÂN QUYỀN THEO HỆ THỐNG MỚI (3 LOẠI QUYỀN CHÍNH) ---

            // A. Lọc theo Công ty (Bắt buộc)
            if (!string.IsNullOrEmpty(tenCongTy))
            {
                query = query.Where(f => f.TenCongTy == tenCongTy);
            }

            // B. Lọc theo Role thực tế trong Cookie
            if (userRoles.Contains("All"))
            {
                // QUYỀN CAO NHẤT: Xem toàn bộ đơn trong công ty
            }
            else if (userRoles.Contains("QuanLyDuyetDonIT"))
            {
                // QUYỀN QUẢN LÝ: Chỉ xem đơn thuộc bộ phận mình quản lý
                if (listTenBoPhan.Any())
                {
                    query = query.Where(f => f.BoPhan != null && listTenBoPhan.Contains(f.BoPhan.Trim().ToLower()));
                }
                else if (!string.IsNullOrEmpty(phongBan))
                {
                    query = query.Where(f => f.BoPhan != null && f.BoPhan.Trim().ToLower() == phongBan.ToLower());
                }
                else
                {
                    query = query.Where(f => f.IdNguoiTao == userId);
                }
            }
            else
            {
                // CÁC ROLE KHÁC HOẶC NHÂN VIÊN: Chỉ xem đơn mình tạo
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
            if (request == null || request.Id <= 0)
                return Json(new { success = false, message = "Dữ liệu không hợp lệ." });

            var userIdStr = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userIdStr))
                return Json(new { success = false, message = "Hết phiên đăng nhập." });

            int userId = int.Parse(userIdStr);
            var userName = User.Identity.Name ?? "N/A";
            var userRoles = User.FindAll(System.Security.Claims.ClaimTypes.Role).Select(c => c.Value).ToList();
            var phongBan = User.FindFirst("PhongBan")?.Value ?? "N/A";

            var form = await _context.FormIts.FindAsync(request.Id);
            if (form == null) return Json(new { success = false, message = "Không tìm thấy đơn." });

            using (var transaction = await _context.Database.BeginTransactionAsync())
            {
                try
                {
                    DateTime now = DateTime.Now;
                    string tieuDeLichSu = "";
                    string moTaChiTiet = "";

                    if (request.Action == "Duyet")
                    {
                        // Kiểm tra quyền duyệt dựa trên 3 Role của bạn
                        if (!userRoles.Any(r => r == "All" || r == "AdminIT" || r == "QuanLyDuyetDonIT"))
                        {
                            return Json(new { success = false, message = "Bạn không có quyền phê duyệt." });
                        }

                        form.IdNguoiDuyet = userId;
                        form.TenNguoiDuyet = userName;
                        form.TimeNguoiDuyet = now;
                        form.TrangThai = "DaDuyet";

                        tieuDeLichSu = "Phê duyệt đơn IT";
                        moTaChiTiet = $"Người duyệt: {userName}. Bộ phận: {phongBan}.";
                    }
                    else if (request.Action == "Huy")
                    {
                        if (!userRoles.Any(r => r == "All" || r == "AdminIT" || r == "QuanLyDuyetDonIT"))
                        {
                            return Json(new { success = false, message = "Bạn không có quyền hủy." });
                        }

                        form.TrangThai = "DaHuy";
                        if (form.TenForm != null && !form.TenForm.StartsWith("[ĐÃ HỦY]"))
                            form.TenForm = "[ĐÃ HỦY] " + form.TenForm;

                        tieuDeLichSu = "Hủy đơn IT";
                        moTaChiTiet = $"Người hủy: {userName}. Lý do: {request.Reason}.";
                    }
                    else if (request.Action == "HoanTat")
                    {
                        // Quyền hoàn tất đơn thường chỉ dành cho IT (AdminIT hoặc All)
                        if (!userRoles.Any(r => r == "All" || r == "AdminIT"))
                        {
                            return Json(new { success = false, message = "Chỉ IT mới có thể hoàn tất đơn này." });
                        }

                        form.TrangThai = "HoanTat";
                        tieuDeLichSu = "Hoàn tất đơn IT";
                        moTaChiTiet = $"Đơn đã được xử lý xong bởi: {userName}.";
                    }

                    var lichSu = new LichSuFormIt
                    {
                        IdFormIt = form.Id,
                        TieuDe = tieuDeLichSu,
                        Mota = moTaChiTiet,
                        Time = now
                    };

                    _context.LichSuFormIts.Add(lichSu);
                    await _context.SaveChangesAsync();
                    await transaction.CommitAsync();

                    return Json(new { success = true, message = tieuDeLichSu });
                }
                catch (Exception ex)
                {
                    await transaction.RollbackAsync();
                    return Json(new { success = false, message = "Lỗi: " + ex.Message });
                }
            }
        }
        #endregion

        #region QUẢN LÝ XÉT DUYỆT IT (Admin IT, All, IT, Quản lý bộ phận) - PHÂN QUYỀN 2026

        [HttpGet("/FormIT/HoanTatDon")]
        public async Task<IActionResult> HoanTatDon()
        {
            // --- 1. LẤY THÔNG TIN TỪ CLAIMS ---
            var userIdStr = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userIdStr))
                return Redirect("/DonXetDuyet/DangNhap");

            int userId = int.Parse(userIdStr);

            // Lấy danh sách Roles thực tế từ Cookie
            var userRoles = User.FindAll(System.Security.Claims.ClaimTypes.Role).Select(c => c.Value).ToList();

            var phongBanSession = User.FindFirst("PhongBan")?.Value?.Trim() ?? "";
            // Vẫn lấy tenCongTy từ Claim nếu bạn cần dùng ở nơi khác, nhưng sẽ không dùng để filter query bên dưới
            var tenCongTy = User.FindFirst("TenCongTy")?.Value?.Trim() ?? "";

            var listTenBoPhanStr = User.FindFirst("TenBoPhan")?.Value ?? "";
            var listTenBoPhan = listTenBoPhanStr.Split(',')
                                                .Select(s => s.Trim().ToLower())
                                                .Where(s => !string.IsNullOrEmpty(s))
                                                .ToList();

            // --- 2. KHỞI TẠO QUERY ---
            IQueryable<FormIt> query = _context.FormIts
                .Include(f => f.ItCtNguoiHoTros)
                    .ThenInclude(ct => ct.IdItNguoiHoTroNavigation)
                .Include(f => f.DanhGiaFormIts);

            // --- 3. PHÂN QUYỀN LỌC DỮ LIỆU ---

            // A. Lọc theo Công ty (ĐÃ LOẠI BỎ THEO YÊU CẦU: XEM TẤT CẢ)
            /* if (!string.IsNullOrEmpty(tenCongTy))
            {
                query = query.Where(f => f.TenCongTy == tenCongTy);
            }
            */

            // B. Lọc theo Role thực tế
            if (userRoles.Contains("All") || userRoles.Contains("AdminIT"))
            {
                // Nhóm quản trị: Xem đơn đã được duyệt (để tiếp nhận xử lý)
                query = query.Where(f => f.IdNguoiDuyet != null);
            }
            else if (userRoles.Contains("QuanLyDuyetDonIT"))
            {
                // Quản lý bộ phận: Xem đơn thuộc phạm vi quản lý
                if (listTenBoPhan.Any())
                {
                    query = query.Where(f => f.BoPhan != null && listTenBoPhan.Contains(f.BoPhan.Trim().ToLower()));
                }
                else if (!string.IsNullOrEmpty(phongBanSession))
                {
                    query = query.Where(f => f.BoPhan != null && f.BoPhan.Trim().ToLower() == phongBanSession.ToLower());
                }
                else
                {
                    query = query.Where(f => f.IdNguoiTao == userId);
                }
            }
            else
            {
                // Nhân viên thường: Chỉ xem đơn của mình
                query = query.Where(f => f.IdNguoiTao == userId);
            }

            // --- 4. THỰC THI TRUY VẤN ---
            var danhSachDon = await query
                .OrderByDescending(f => f.Id)
                .AsNoTracking()
                .ToListAsync();

            // --- 5. GÁN TRẠNG THÁI HIỂN THỊ (Logic View) ---
            foreach (var item in danhSachDon)
            {
                if (item.TenForm != null && item.TenForm.Contains("[ĐÃ HỦY]"))
                {
                    item.TrangThai = "Đã hủy";
                }
                else if (item.IdNguoiDuyet == null)
                {
                    item.TrangThai = "Chờ QL Duyệt";
                }
                else if (item.IdAdmin == null)
                {
                    item.TrangThai = "IT Đang xử lý";
                }
                else if (item.DanhGiaFormIts == null || !item.DanhGiaFormIts.Any())
                {
                    item.TrangThai = "Chờ đánh giá";
                }
                else
                {
                    item.TrangThai = "Hoàn tất";
                }
            }

            return View(danhSachDon);
        }

        // Nút bấm dành cho Đội IT - Xác nhận đã sửa xong/cấp xong thiết bị
        [HttpPost("/FormIT/XacNhanHoanThanh")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> XacNhanHoanThanh([FromBody] ITCompleteRequest request)
        {
            // 1. Kiểm tra đầu vào
            if (request == null || request.Id <= 0)
                return Json(new { success = false, message = "Dữ liệu không hợp lệ." });

            // 2. Thông tin người thao tác
            var userIdStr = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userIdStr))
                return Json(new { success = false, message = "Phiên đăng nhập đã hết hạn." });

            var userRoles = User.FindAll(System.Security.Claims.ClaimTypes.Role).Select(c => c.Value).ToList();
            var userName = User.Identity.Name ?? "N/A";
            var userEmail = User.FindFirst(System.Security.Claims.ClaimTypes.Email)?.Value ?? "N/A";
            var phongBan = User.FindFirst("PhongBan")?.Value ?? "N/A";

            // 3. Kiểm tra quyền (Chỉ IT Admin hoặc Role "All")
            if (!userRoles.Any(r => r == "All" || r == "AdminIT"))
            {
                return Json(new { success = false, message = "Bạn không có quyền xác nhận hoàn tất đơn này." });
            }

            // 4. Tìm đơn IT
            var form = await _context.FormIts.FindAsync(request.Id);
            if (form == null)
                return Json(new { success = false, message = "Không tìm thấy đơn IT." });

            // Kiểm tra trạng thái đơn
            bool isCancelled = (form.TenForm != null && form.TenForm.Contains("[ĐÃ HỦY]"));
            if (form.TrangThai == "HoanTat" || isCancelled)
            {
                return Json(new { success = false, message = "Đơn này đã kết thúc hoặc đã hủy." });
            }

            // 5. Bắt đầu Transaction
            using (var transaction = await _context.Database.BeginTransactionAsync())
            {
                try
                {
                    DateTime now = DateTime.Now;

                    // 6. Cập nhật thông tin IT xử lý
                    form.IdAdmin = int.Parse(userIdStr);
                    form.TenAdmin = userName;
                    form.TimeAdmin = now;
                    form.TrangThai = "HoanTat";

                    // 7. Lưu Lịch sử thao tác
                    var lichSu = new LichSuFormIt
                    {
                        IdFormIt = form.Id,
                        TieuDe = "IT Xác nhận Hoàn tất",
                        Mota = $"Kỹ thuật thực hiện: {userName} ({userEmail}). " +
                               $"Bộ phận: {phongBan}. " +
                               $"Nội dung: Đã xử lý hoàn tất yêu cầu.",
                        Time = now
                    };

                    _context.LichSuFormIts.Add(lichSu);
                    _context.FormIts.Update(form); // Đảm bảo EF theo dõi sự thay đổi

                    // 8. Lưu DB
                    await _context.SaveChangesAsync();
                    await transaction.CommitAsync();

                    return Json(new { success = true, message = "Xác nhận hoàn tất thành công!" });
                }
                catch (Exception ex)
                {
                    await transaction.RollbackAsync();
                    return Json(new { success = false, message = "Lỗi hệ thống: " + ex.Message });
                }
            }
        }

        // --- CLASS REQUEST (Giữ nguyên cấu trúc của bạn) ---
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

        #region Xác nhận chưa hoàn thành - NGƯỜI TẠO PHẢN HỒI

        [HttpPost("/FormIT/XacNhanChuaHoanThanh")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> XacNhanChuaHoanThanh([FromBody] ITNotCompleteRequest request)
        {
            // 1. Kiểm tra đầu vào (Bắt buộc phải có lý do)
            if (request == null || request.Id <= 0 || string.IsNullOrWhiteSpace(request.Reason))
            {
                return Json(new { success = false, message = "Vui lòng nhập đầy đủ thông tin và lý do phản hồi." });
            }

            // 2. Lấy thông tin từ Claims
            var userIdStr = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userIdStr))
            {
                return Json(new { success = false, message = "Phiên đăng nhập đã hết hạn. Vui lòng đăng nhập lại." });
            }

            var userName = User.Identity.Name ?? "N/A";
            var userEmail = User.FindFirst(System.Security.Claims.ClaimTypes.Email)?.Value ?? "N/A";
            int userId = int.Parse(userIdStr);

            // 3. Tìm đơn và nạp kèm bảng Đánh giá
            var form = await _context.FormIts
                .Include(f => f.DanhGiaFormIts)
                .FirstOrDefaultAsync(x => x.Id == request.Id);

            if (form == null)
            {
                return Json(new { success = false, message = "Không tìm thấy đơn yêu cầu trong hệ thống." });
            }

            // --- KIỂM TRA ĐIỀU KIỆN THAO TÁC ---

            // A. Chỉ người tạo đơn mới có quyền này
            if (form.IdNguoiTao != userId)
            {
                return Json(new { success = false, message = "Bạn không có quyền thao tác trên đơn của người khác." });
            }

            // B. Chỉ áp dụng khi đơn đang ở trạng thái Hoàn tất (chờ đánh giá)
            if (form.TrangThai != "HoanTat")
            {
                return Json(new { success = false, message = "Chỉ có thể phản hồi các đơn đã được IT xác nhận hoàn tất." });
            }

            // C. Chặn nếu đơn đã được đánh giá (Đã đánh giá coi như kết thúc 100%)
            if (form.DanhGiaFormIts != null && form.DanhGiaFormIts.Any())
            {
                return Json(new
                {
                    success = false,
                    message = "Bạn đã đánh giá đơn này rồi! Không thể yêu cầu xử lý lại sau khi đã đánh giá."
                });
            }

            // D. Kiểm tra nếu đơn đã bị hủy
            if (form.TenForm != null && form.TenForm.Contains("[ĐÃ HỦY]"))
            {
                return Json(new { success = false, message = "Đơn đã bị hủy, không thể yêu cầu xử lý lại." });
            }

            // 4. Xử lý Transaction để đảm bảo dữ liệu đồng nhất
            using (var transaction = await _context.Database.BeginTransactionAsync())
            {
                try
                {
                    DateTime now = DateTime.Now;

                    // Lưu lại thông tin IT xử lý trước khi reset để ghi lịch sử
                    string itDaXuLy = form.TenAdmin ?? "N/A";
                    DateTime? timeItXuLy = form.TimeAdmin;

                    // --- RESET TRẠNG THÁI ĐỂ IT XỬ LÝ LẠI ---
                    form.IdAdmin = null;
                    form.TenAdmin = null;
                    form.TimeAdmin = null;
                    form.TrangThai = "DaDuyet"; // Chuyển về trạng thái chờ IT tiếp nhận lại

                    // 5. Lưu lịch sử chi tiết cho IT biết tại sao bị trả về
                    var lichSu = new LichSuFormIt
                    {
                        IdFormIt = form.Id,
                        TieuDe = "NGƯỜI TẠO XÁC NHẬN CHƯA HOÀN THÀNH",
                        Mota = $"Người tạo đơn: {userName} ({userEmail})\n" +
                               $"Nội dung: Đã yêu cầu IT xử lý lại do thực tế chưa hoàn thành.\n" +
                               $"IT đã xử lý trước đó: {itDaXuLy} (Lúc {timeItXuLy?.ToString("HH:mm dd/MM/yyyy")})\n" +
                               $"Lý do phản hồi: {request.Reason.Trim()}",
                        Time = now
                    };

                    _context.LichSuFormIts.Add(lichSu);
                    _context.FormIts.Update(form); // Đánh dấu EF cập nhật lại bảng chính

                    await _context.SaveChangesAsync();
                    await transaction.CommitAsync();

                    return Json(new
                    {
                        success = true,
                        message = "Đã gửi phản hồi thành công. Đơn đã được chuyển về cho bộ phận IT xử lý lại."
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

        #region Đánh giá đơn IT (Tích hợp TenCongTy & Lịch sử)

        [HttpPost("/FormIT/DanhGiaFormItsDon")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DanhGiaFormItsDon([FromBody] DanhGiaFormItsRequest request)
        {
            // 1. Kiểm tra đầu vào cơ bản
            if (request == null || request.Id <= 0 || request.MucDo < 1 || request.MucDo > 5)
            {
                return Json(new { success = false, message = "Dữ liệu không hợp lệ. Mức độ đánh giá từ 1-5 sao." });
            }

            // 2. Lấy thông tin người dùng từ Claims
            var userIdStr = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
            var userName = User.Identity?.Name ?? "N/A";
            var userEmail = User.FindFirst(System.Security.Claims.ClaimTypes.Email)?.Value ?? "N/A";
            var tenCongTy = User.FindFirst("TenCongTy")?.Value?.Trim() ?? "";

            if (string.IsNullOrEmpty(userIdStr))
            {
                return Json(new { success = false, message = "Phiên đăng nhập đã hết hạn. Vui lòng đăng nhập lại." });
            }

            int userId = int.Parse(userIdStr);

            // 3. Tìm đơn kèm các bảng liên quan và kiểm tra TenCongTy
            var form = await _context.FormIts
                .Include(f => f.DanhGiaFormIts)
                .FirstOrDefaultAsync(x => x.Id == request.Id && x.TenCongTy == tenCongTy);

            if (form == null)
            {
                return Json(new { success = false, message = "Không tìm thấy đơn yêu cầu hoặc bạn không có quyền truy cập đơn này." });
            }

            // 4. KIỂM TRA QUYỀN: Chỉ người tạo đơn mới được đánh giá
            if (form.IdNguoiTao != userId)
            {
                return Json(new { success = false, message = "Chỉ người tạo đơn mới có quyền thực hiện đánh giá này." });
            }

            // 5. KIỂM TRA TRẠNG THÁI: Chỉ đánh giá khi đơn đã HoanTat
            if (form.TrangThai != "HoanTat")
            {
                return Json(new { success = false, message = "Chỉ có thể đánh giá các đơn đã hoàn thành xử lý." });
            }

            // 6. KIỂM TRA ĐÃ ĐÁNH GIÁ CHƯA: Tránh đánh giá nhiều lần
            if (form.DanhGiaFormIts != null && form.DanhGiaFormIts.Any())
            {
                return Json(new { success = false, message = "Bạn đã gửi đánh giá cho đơn này trước đó rồi." });
            }

            // 7. Transaction để đảm bảo tính toàn vẹn dữ liệu
            using (var transaction = await _context.Database.BeginTransactionAsync())
            {
                try
                {
                    DateTime now = DateTime.Now;

                    // 8. TẠO BẢN GHI ĐÁNH GIÁ CHI TIẾT
                    var danhGiaMoi = new DanhGiaFormIt
                    {
                        IdFormIt = form.Id,
                        IdNguoiDanhGia = userId,
                        TenNguoiDanhGia = userName,
                        TimeNguoiDanhGia = now,
                        MucDo = request.MucDo,
                        // Nếu DB của bạn có cột NhanXet, hãy bỏ comment dòng dưới:
                        // NhanXet = request.NhanXet?.Trim() 
                    };

                    _context.DanhGiaFormIts.Add(danhGiaMoi);

                    // 9. LƯU VÀO LỊCH SỬ FORM (Để hiện thị trong phần Trao đổi/Log)
                    string stars = new string('⭐', request.MucDo);
                    var lichSu = new LichSuFormIt
                    {
                        IdFormIt = form.Id,
                        TieuDe = "NGƯỜI DÙNG ĐÁNH GIÁ DỊCH VỤ",
                        Mota = $"Mức độ: {stars} ({request.MucDo}/5 sao)\n" +
                               $"Người đánh giá: {userName}\n" +
                               $"Nội dung: {(string.IsNullOrWhiteSpace(request.NhanXet) ? "(Không có nhận xét)" : request.NhanXet.Trim())}",
                        Time = now,
                        IsRead = false // Để IT nhận được thông báo về đánh giá mới
                    };

                    _context.LichSuFormIts.Add(lichSu);

                    // 10. Cập nhật lại một số thông tin trên Form (nếu cần)
                    // Ví dụ: Đánh dấu đơn đã đóng hoàn toàn không cho phản hồi thêm nữa
                    // form.GhiChu += $"\n[Đã đánh giá {request.MucDo} sao vào {now:dd/MM/yyyy}]";

                    await _context.SaveChangesAsync();
                    await transaction.CommitAsync();

                    return Json(new
                    {
                        success = true,
                        message = $"Cảm ơn bạn đã đánh giá {request.MucDo} sao! Ý kiến của bạn giúp chúng tôi cải thiện dịch vụ tốt hơn."
                    });
                }
                catch (Exception ex)
                {
                    await transaction.RollbackAsync();
                    return Json(new { success = false, message = "Đã xảy ra lỗi khi lưu đánh giá: " + ex.Message });
                }
            }
        }

        // Request model cho đánh giá
        public class DanhGiaFormItsRequest
        {
            public int Id { get; set; }
            public int MucDo { get; set; } // Giá trị từ 1-5
            public string NhanXet { get; set; } // Nội dung phản hồi thêm
        }

        #endregion

        #region BÁO CÁO THỐNG KÊ FORM IT (AdminIT, All, IT)

        [HttpGet("/FormIT/BaoCaoThongKe")]
        public async Task<IActionResult> BaoCaoThongKe()
        {
            // --- 1. KIỂM TRA QUYỀN TRUY CẬP ---
            // Sử dụng hàm HasAccess để kiểm tra các Role có quyền xem báo cáo
            if (!HasAccess("IT", "AdminIT", "All"))
            {
                return Redirect("/");
            }

            // Lấy thông tin công ty từ Claim (Vẫn giữ để có thông tin nếu cần, nhưng không dùng lọc query)
            var tenCongTy = User.FindFirst("TenCongTy")?.Value?.Trim() ?? "";

            // --- 2. LẤY DỮ LIỆU GỐC (ĐÃ BỎ LỌC THEO CÔNG TY) ---
            var query = _context.FormIts
                .AsNoTracking()
                .Include(f => f.DanhGiaFormIts)
                .Include(f => f.ItCtNguoiHoTros)
                    .ThenInclude(ct => ct.IdItNguoiHoTroNavigation);
            // .Where(f => f.TenCongTy == tenCongTy); // Đã bỏ dòng này để xem tất cả

            var allForms = await query
                .OrderByDescending(f => f.TimeNguoiTao)
                .ToListAsync();

            // Hàm Local Helper kiểm tra trạng thái hủy
            bool IsHuy(FormIt f) => (f.TenForm ?? "").Contains("[ĐÃ HỦY]") || f.TrangThai == "DaHuy" || f.TrangThai == "TuChoi";

            // --- 3. THỐNG KÊ THEO LOẠI FORM (BIỂU ĐỒ TRÒN) ---
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

            // --- 4. THỐNG KÊ THEO DANH MỤC ---
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

            // --- 5. THỐNG KÊ TRẠNG THÁI (STATUS DASHBOARD) ---
            int countHuy = allForms.Count(IsHuy);

            int countHoanTat = allForms.Count(f =>
                !IsHuy(f) && f.TrangThai == "HoanTat" && f.DanhGiaFormIts != null && f.DanhGiaFormIts.Any());

            int countChoDanhGia = allForms.Count(f =>
                !IsHuy(f) && f.TrangThai == "HoanTat" && (f.DanhGiaFormIts == null || !f.DanhGiaFormIts.Any()));

            int countDangXuLy = allForms.Count(f =>
                !IsHuy(f) && f.IdNguoiDuyet != null && f.IdAdmin == null && f.TrangThai != "HoanTat");

            int countChoDuyet = allForms.Count(f =>
                !IsHuy(f) && f.IdNguoiDuyet == null);

            ViewBag.StatusLabels = new List<string> { "Hoàn tất", "Chờ đánh giá", "Đang xử lý", "Chờ duyệt", "Đã hủy" };
            ViewBag.StatusCounts = new List<int> { countHoanTat, countChoDanhGia, countDangXuLy, countChoDuyet, countHuy };

            // --- 6. THỜI GIAN XỬ LÝ TRUNG BÌNH (KPI) ---
            var completedForms = allForms
                .Where(f => !IsHuy(f) && f.TimeAdmin != null && f.TimeNguoiDuyet != null)
                .ToList();

            if (completedForms.Any())
            {
                var avgMinutes = completedForms.Average(f => (f.TimeAdmin.Value - f.TimeNguoiDuyet.Value).TotalMinutes);
                var t = TimeSpan.FromMinutes(avgMinutes);
                ViewBag.AvgProcessingTime = $"{(int)t.TotalHours:D2}h {t.Minutes:D2}m";
            }
            else
            {
                ViewBag.AvgProcessingTime = "0h 0m";
            }

            // --- 7. THỐNG KÊ THEO NGƯỜI HỖ TRỢ (TABLE) ---
            var supportStats = allForms
                .SelectMany(f => f.ItCtNguoiHoTros ?? new List<ItCtNguoiHoTro>())
                .Where(h => h.IdItNguoiHoTro != null)
                .GroupBy(h => new
                {
                    Id = h.IdItNguoiHoTro.Value,
                    Ten = h.IdItNguoiHoTroNavigation?.Ten ?? "Không xác định"
                })
                .Select(g => new ItSupportStatisticVM
                {
                    IdIt = g.Key.Id,
                    TenIt = g.Key.Ten,
                    HoanTat = allForms.Count(f => f.ItCtNguoiHoTros.Any(h => h.IdItNguoiHoTro == g.Key.Id) && f.TrangThai == "HoanTat" && f.DanhGiaFormIts.Any()),
                    ChoDanhGia = allForms.Count(f => f.ItCtNguoiHoTros.Any(h => h.IdItNguoiHoTro == g.Key.Id) && f.TrangThai == "HoanTat" && !f.DanhGiaFormIts.Any()),
                    DangXuLy = allForms.Count(f => f.ItCtNguoiHoTros.Any(h => h.IdItNguoiHoTro == g.Key.Id) && f.IdNguoiDuyet != null && f.IdAdmin == null),
                    ChoDuyet = allForms.Count(f => f.ItCtNguoiHoTros.Any(h => h.IdItNguoiHoTro == g.Key.Id) && f.IdNguoiDuyet == null),
                    DaHuy = allForms.Count(f => f.ItCtNguoiHoTros.Any(h => h.IdItNguoiHoTro == g.Key.Id) && IsHuy(f))
                })
                .OrderByDescending(s => s.Tong)
                .ToList();

            ViewBag.SupportStats = supportStats;

            // --- 8. TRUNG BÌNH THEO DANH MỤC CHO MỖI NGƯỜI (DETAILS) ---
            var avgByHandlerDanhMuc = allForms
                .Where(f => f.TimeAdmin != null && f.TimeNguoiDuyet != null)
                .SelectMany(f => f.ItCtNguoiHoTros.Select(h => new { Form = f, Handler = h }))
                .Where(x => x.Handler.IdItNguoiHoTro != null)
                .GroupBy(x => new
                {
                    IdIt = x.Handler.IdItNguoiHoTro.Value,
                    TenIt = x.Handler.IdItNguoiHoTroNavigation?.Ten ?? "N/A",
                    Danhmuc = string.IsNullOrWhiteSpace(x.Form.Danhmuc) ? "Khác" : x.Form.Danhmuc
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

            foreach (var item in avgByHandlerDanhMuc)
            {
                var ts = TimeSpan.FromMinutes(item.AvgMinutes);
                item.AvgTimeFormatted = ts.TotalDays >= 1 ? $"{(int)ts.TotalDays}d {ts.Hours}h {ts.Minutes}m" :
                                       ts.TotalHours >= 1 ? $"{(int)ts.TotalHours}h {ts.Minutes}m" : $"{(int)ts.TotalMinutes}m";
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
            public int Tong => HoanTat + ChoDanhGia + DangXuLy + ChoDuyet + DaHuy;
        }

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

        #region LỊCH SỬ VÀ THÔNG BÁO FORM IT (Phân quyền mới & TenCongTy)

        [HttpGet("/FormIT/LichSuIT")]
        public async Task<IActionResult> LogLichSuIT()
        {
            // 1. Lấy thông tin User từ Claims
            var userIdStr = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userIdStr)) return RedirectToAction("DangNhap", "DonXetDuyet");

            int userId = int.Parse(userIdStr);
            var userEmail = User.FindFirst(System.Security.Claims.ClaimTypes.Email)?.Value ?? "";
            var tenCongTy = User.FindFirst("TenCongTy")?.Value?.Trim() ?? "";

            // 2. Khởi tạo Query cơ bản (Luôn lọc theo công ty trước để bảo mật dữ liệu)
            var query = _context.LichSuFormIts
                .Include(l => l.IdFormItNavigation)
                    .ThenInclude(f => f.ItCtNguoiHoTros)
                        .ThenInclude(ct => ct.IdItNguoiHoTroNavigation)
                .Include(l => l.IdFormItNavigation)
                    .ThenInclude(f => f.DanhGiaFormIts)
                .Where(l => l.IdFormItNavigation.TenCongTy == tenCongTy)
                .AsQueryable();

            // 3. Logic phân quyền lọc dữ liệu theo 3 loại quyền mới
            if (User.IsInRole("All"))
            {
                // Quyền All: Xem toàn bộ lịch sử của công ty đó
            }
            else if (User.IsInRole("AdminIT"))
            {
                // AdminIT: Xem các đơn đã được duyệt VÀ liên quan đến bộ phận IT (Admin xử lý hoặc người hỗ trợ)
                query = query.Where(l =>
                    l.IdFormItNavigation.IdNguoiDuyet != null &&
                    (l.IdFormItNavigation.IdAdmin == userId ||
                     l.IdFormItNavigation.ItCtNguoiHoTros.Any(ct => ct.IdItNguoiHoTroNavigation.MaNv == userEmail))
                );
            }
            else if (User.IsInRole("QuanLyDuyetDonIT"))
            {
                // QuanLyDuyetDonIT: Xem các đơn mình đã duyệt hoặc đơn mình tạo
                query = query.Where(l =>
                    l.IdFormItNavigation.IdNguoiTao == userId ||
                    l.IdFormItNavigation.IdNguoiDuyet == userId
                );
            }
            else
            {
                // User thường: Chỉ xem đơn mình tạo hoặc mình có tham gia hỗ trợ (nếu có)
                query = query.Where(l =>
                    l.IdFormItNavigation.IdNguoiTao == userId ||
                    l.IdFormItNavigation.ItCtNguoiHoTros.Any(ct => ct.IdItNguoiHoTroNavigation.MaNv == userEmail)
                );
            }

            var logs = await query
                .OrderByDescending(l => l.Time)
                .AsNoTracking()
                .ToListAsync();

            return View(logs);
        }

        [HttpGet("/FormIT/GetNotifications")]
        public async Task<IActionResult> GetNotifications(int skip = 0, int take = 20)
        {
            var userIdStr = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userIdStr)) return Unauthorized();

            int userId = int.Parse(userIdStr);
            var userEmail = User.FindFirst(System.Security.Claims.ClaimTypes.Email)?.Value ?? "";
            var tenCongTy = User.FindFirst("TenCongTy")?.Value?.Trim() ?? "";

            var query = _context.LichSuFormIts
                .Include(l => l.IdFormItNavigation)
                    .ThenInclude(f => f.ItCtNguoiHoTros)
                        .ThenInclude(ct => ct.IdItNguoiHoTroNavigation)
                .Where(l => l.IdFormItNavigation.TenCongTy == tenCongTy)
                .AsQueryable();

            // Logic lọc thông báo (Đồng bộ hoàn toàn với hàm LichSuIT)
            if (User.IsInRole("All")) { /* Xem hết */ }
            else if (User.IsInRole("AdminIT"))
            {
                query = query.Where(l =>
                    l.IdFormItNavigation.IdNguoiDuyet != null &&
                    (l.IdFormItNavigation.IdAdmin == userId ||
                     l.IdFormItNavigation.ItCtNguoiHoTros.Any(ct => ct.IdItNguoiHoTroNavigation.MaNv == userEmail))
                );
            }
            else if (User.IsInRole("QuanLyDuyetDonIT"))
            {
                query = query.Where(l => l.IdFormItNavigation.IdNguoiTao == userId || l.IdFormItNavigation.IdNguoiDuyet == userId);
            }
            else
            {
                query = query.Where(l => l.IdFormItNavigation.IdNguoiTao == userId);
            }

            var unreadCount = await query.CountAsync(l => l.IsRead != true);

            var logs = await query.OrderByDescending(l => l.Time)
                                  .Skip(skip)
                                  .Take(take)
                                  .Select(l => new
                                  {
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


        #endregion 
    }
}
