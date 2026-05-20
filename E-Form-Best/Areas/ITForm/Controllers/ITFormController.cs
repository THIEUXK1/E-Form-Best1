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

            // 2. Kiểm tra tài khoản trong Database
            var user = _context.Users
                .Include(u => u.UserBoPhans)
                    .ThenInclude(ub => ub.IdBoPhanNavigation)
                .Include(u => u.UserQuyens)
                    .ThenInclude(uq => uq.IdQuyenNavigation)
                .FirstOrDefault(u => u.Tk == email && u.MatKhau == matKhau && u.TrangThai != "Đã nghỉ");

            if (user == null)
            {
                ViewBag.Error = "Tài khoản hoặc mật khẩu không đúng.";
                return View();
            }

            // --- MỚI: ĐẢM BẢO CÓ SECURITY STAMP ---
            if (string.IsNullOrEmpty(user.SecurityStamp))
            {
                user.SecurityStamp = Guid.NewGuid().ToString();
                _context.Users.Update(user);
                await _context.SaveChangesAsync();
            }

            // 3. XỬ LÝ ĐỊNH DANH THIẾT BỊ (LAPTOP/PHONE) VÀ TÊN MÁY TÍNH
            string userAgent = Request.Headers["User-Agent"].ToString();
            var remoteIpAddress = HttpContext.Connection.RemoteIpAddress;
            string ipAddress = remoteIpAddress?.ToString();

            // --- MỚI: LẤY TÊN MÁY TÍNH TỪ ĐỊA CHỈ IP (Cho mạng nội bộ Intranet) ---
            string resolvedComputerName = deviceName;

            if (string.IsNullOrEmpty(resolvedComputerName) || resolvedComputerName == "Thiết bị không xác định")
            {
                try
                {
                    if (remoteIpAddress != null)
                    {
                        var hostEntry = System.Net.Dns.GetHostEntry(remoteIpAddress);
                        resolvedComputerName = hostEntry.HostName;
                        if (resolvedComputerName.Contains("."))
                        {
                            resolvedComputerName = resolvedComputerName.Split('.')[0];
                        }
                    }
                }
                catch (Exception)
                {
                    resolvedComputerName = "Không thể xác định tên máy";
                }
            }

            var device = _context.UserDevices
                .FirstOrDefault(d => d.IdNguoiDung == user.IdNguoiDung && d.DeviceFingerprint == userAgent);

            if (device == null)
            {
                var newDevice = new UserDevice
                {
                    IdNguoiDung = user.IdNguoiDung,
                    DeviceFingerprint = userAgent,
                    DeviceName = resolvedComputerName,
                    LastLogin = DateTime.Now,
                    IsTrusted = true
                };
                _context.UserDevices.Add(newDevice);
            }
            else
            {
                device.LastLogin = DateTime.Now;
                device.DeviceName = resolvedComputerName;
                _context.UserDevices.Update(device);
            }

            // --- MỚI: LƯU LỊCH SỬ ĐĂNG NHẬP ---
            var lichSu = new LichSuTruyCap
            {
                IdNguoiDung = user.IdNguoiDung,
                ThoiGianDangNhap = DateTime.Now,
                TenMayTinh = resolvedComputerName,
                DiaChiIp = ipAddress,
                TrinhDuyet = userAgent,
                TrangThai = "Đang hoạt động"
            };
            _context.LichSuTruyCaps.Add(lichSu);

            await _context.SaveChangesAsync();

            // 4. THIẾT LẬP COOKIE AUTHENTICATION
            var claims = new List<Claim>
    {
        new Claim(ClaimTypes.NameIdentifier, user.IdNguoiDung.ToString()),
        new Claim(ClaimTypes.Name, user.HoTen ?? ""),
        new Claim(ClaimTypes.Email, user.Tk ?? ""),
        // THÊM CLAIM MaNv ĐỂ ĐỒNG BỘ VỚI LOGIC PHÂN QUYỀN B2
        new Claim("MaNv", user.Tk ?? ""),
        new Claim("UserRole", user.VaiTro ?? ""),
        new Claim("PhongBan", user.PhongBan ?? ""),
        new Claim("TenCongTy", user.TenCongTy ?? ""),
        new Claim("AnhDaiDien", user.AnhDaiDien ?? "/images/default-avatar.png"),
        new Claim("SecurityStamp", user.SecurityStamp ?? "")
    };

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

            if (user.UserQuyens != null && user.UserQuyens.Any())
            {
                var dsQuyen = user.UserQuyens
                    .Where(uq => uq.IdQuyenNavigation != null)
                    .Select(uq => uq.IdQuyenNavigation.TenQuyen);

                foreach (var tenQuyen in dsQuyen)
                {
                    if (!string.IsNullOrEmpty(tenQuyen))
                    {
                        claims.Add(new Claim(ClaimTypes.Role, tenQuyen));
                    }
                }
            }

            if (matKhau == "abc12345")
            {
                claims.Add(new Claim("IsDefaultPassword", "true"));
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
            // --- MỚI: CẬP NHẬT LỊCH SỬ ĐĂNG XUẤT ---
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (int.TryParse(userIdClaim, out int userId))
            {
                // Tìm bản ghi đăng nhập gần nhất của user này mà chưa có thời gian đăng xuất
                var lastSession = await _context.LichSuTruyCaps
                    .Where(ls => ls.IdNguoiDung == userId && ls.ThoiGianDangXuat == null)
                    .OrderByDescending(ls => ls.ThoiGianDangNhap)
                    .FirstOrDefaultAsync();

                if (lastSession != null)
                {
                    lastSession.ThoiGianDangXuat = DateTime.Now;
                    lastSession.TrangThai = "Đã đăng xuất";
                    _context.LichSuTruyCaps.Update(lastSession);
                    await _context.SaveChangesAsync();
                }
            }

            // 1. Đăng xuất khỏi hệ thống Authentication
            await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);

            // 2. Xóa sạch Session
            HttpContext.Session.Clear();

            // 3. Thông báo
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

            // Lấy danh sách công việc (Chế độ 1)
            ViewBag.CongViecList = await _context.CongViecIts
                                            .OrderBy(x => x.Ten)
                                            .ToListAsync();

            // Lấy danh sách người hỗ trợ IT (Chế độ 2) - BẠN THAY TÊN BẢNG NẾU KHÁC NHÉ
            ViewBag.NguoiHoTroList = await _context.ItNguoiHoTros
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
                TenForm = "Yêu cầu sửa chữa/Hỗ trợ kỹ thuật",
                // THÊM MẶC ĐỊNH NGƯỜI DUYỆT
                IdNguoiDuyet = 8,
                TenNguoiDuyet = "Hệ thống E-form",
                TimeNguoiDuyet = DateTime.Now
            };

            return View(model);
        }

        [HttpPost("/FormIT/TaoIT_Order")]
        [ValidateAntiForgeryToken]
        // Bổ sung thêm tham số modeHoTro và selectedNguoiHoTroId, đổi selectedCongViecId thành int? để linh hoạt
        public async Task<IActionResult> TaoIT_Order(FormIt form, [FromForm] ItOrderIt2 itOrder, string modeHoTro, int? selectedCongViecId, int? selectedNguoiHoTroId)
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
                    // --- 1. TRUY VẤN CÔNG VIỆC & NGƯỜI HỖ TRỢ THEO CHẾ ĐỘ ---
                    CongViecIt? congViec = null;
                    dynamic? nguoiHoTro = null;

                    if (modeHoTro == "Mode1" && selectedCongViecId.HasValue)
                    {
                        congViec = await _context.CongViecIts
                            .Include(c => c.IdItNguoiHoTroNavigation)
                            .FirstOrDefaultAsync(c => c.Id == selectedCongViecId.Value);
                    }
                    else if (modeHoTro == "Mode2" && selectedNguoiHoTroId.HasValue)
                    {
                        // Truy vấn để lấy tên người hỗ trợ ghi vào log
                        nguoiHoTro = await _context.ItNguoiHoTros.FindAsync(selectedNguoiHoTroId.Value);
                    }

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
                    form.IdForm = "IT_OrderIT_2";
                    form.TenForm = "Yêu cầu sửa chữa/Hỗ trợ kỹ thuật";

                    // THÊM MẶC ĐỊNH NGƯỜI DUYỆT
                    form.IdNguoiDuyet = 8;
                    form.TenNguoiDuyet = "Hệ thống E-form";
                    form.TimeNguoiDuyet = DateTime.Now;

                    // Xử lý lưu Danhmuc theo chế độ
                    if (modeHoTro == "Mode1" && congViec != null)
                    {
                        form.Danhmuc = congViec.Ten;
                    }
                    else if (modeHoTro == "Mode2")
                    {
                        form.Danhmuc = "Chỉ định người hỗ trợ";
                    }

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
                        if (string.IsNullOrEmpty(itOrder.Ten) && congViec != null && modeHoTro == "Mode1")
                        {
                            itOrder.Ten = congViec.Ten;
                        }
                        else if (string.IsNullOrEmpty(itOrder.Ten) && modeHoTro == "Mode2")
                        {
                            itOrder.Ten = "Chỉ định người hỗ trợ trực tiếp";
                        }

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

                    // --- 5. LƯU NGƯỜI HỖ TRỢ TÙY THEO CHẾ ĐỘ ---
                    string supporterLog = "Chưa gán người hỗ trợ";

                    if (modeHoTro == "Mode1" && congViec != null && congViec.IdItNguoiHoTro.HasValue)
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
                    else if (modeHoTro == "Mode2" && selectedNguoiHoTroId.HasValue)
                    {
                        var ctNguoiHoTro = new ItCtNguoiHoTro
                        {
                            IdFormIt = form.Id,
                            IdItNguoiHoTro = selectedNguoiHoTroId.Value,
                            Stt = 1
                        };
                        _context.ItCtNguoiHoTros.Add(ctNguoiHoTro);
                        supporterLog = $"Đã chỉ định trực tiếp: {nguoiHoTro?.Ten ?? "Nhân viên IT"}";
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
                    ViewBag.NguoiHoTroList = await _context.ItNguoiHoTros.OrderBy(x => x.Ten).ToListAsync(); // Nạp lại đề phòng lỗi
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
            var tenCongTy = User.FindFirst("TenCongTy")?.Value ?? "";

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
                TenCongTy = tenCongTy,
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
        public async Task<IActionResult> TaoIT_Wifi(FormIt form, [FromForm] List<ItDangKiSuDungWifi3> itWifiList, int selectedCongViecId)
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

                    // --- 3. XỬ LÝ FILE ĐÍNH KÈM (Dùng chung cho cả đơn) ---
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

                    // --- 4. CHI TIẾT WIFI (List<ItDangKiSuDungWifi3>) & XỬ LÝ ẢNH ---
                    string imgFileName = null;
                    var anhFile = Request.Form.Files["Anh"];
                    if (anhFile != null && anhFile.Length > 0)
                    {
                        string imgExtension = Path.GetExtension(anhFile.FileName);
                        if (string.IsNullOrEmpty(imgExtension)) imgExtension = ".jpg";
                        imgFileName = $"AnhWifi_ID{form.Id}_{safeName}_{timeStamp}{imgExtension}";
                        string imgFullPath = Path.Combine(networkPath, imgFileName);

                        using (var imgStream = new FileStream(imgFullPath, FileMode.Create))
                        {
                            await anhFile.CopyToAsync(imgStream);
                        }
                    }

                    if (itWifiList != null && itWifiList.Any())
                    {
                        foreach (var itWifi in itWifiList)
                        {
                            itWifi.IdFormIt = form.Id;
                            if (!string.IsNullOrEmpty(imgFileName))
                            {
                                itWifi.DuongDanAnh = imgFileName;
                                itWifi.Anh = null;
                            }
                        }
                        _context.ItDangKiSuDungWifi3s.AddRange(itWifiList);
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
                        supporterLog = $"Đã tự động gán: {congViec.IdItNguoiHoTroNavigation?.Ten ?? "Nhân viên IT"}";
                    }

                    // --- 6. LƯU LỊCH SỬ CHI TIẾT ---
                    string anhLog = string.IsNullOrEmpty(imgFileName) ? "Không có ảnh" : $"Ảnh: {imgFileName}";
                    var lichSu = new LichSuFormIt
                    {
                        IdFormIt = form.Id,
                        TieuDe = "Khởi tạo đơn Wifi",
                        Mota = $"Người tạo: {userName} | Số lượng thiết bị: {itWifiList?.Count}. {supporterLog}. {fileLog}. {anhLog}",
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
                .Where(x => x.Ten.Contains("tài khoản hệ thống") || x.Ten.Contains("Account"))
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
        public async Task<IActionResult> DonTaiKhoanHeThong(FormIt form, [FromForm] List<ItDangKiTaiKhoanHeThong5> itTaiKhoanList, int SelectedCongViecId)
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

                    // --- BƯỚC 3: LƯU CHI TIẾT (List<ItDangKiTaiKhoanHeThong5>) & XỬ LÝ ẢNH ---
                    string imgFileName = null;
                    var anhFile = Request.Form.Files["Anh"];

                    // Xử lý lưu ảnh 1 lần nếu có
                    if (anhFile != null && anhFile.Length > 0)
                    {
                        string imgExtension = Path.GetExtension(anhFile.FileName);
                        if (string.IsNullOrEmpty(imgExtension)) imgExtension = ".jpg";

                        imgFileName = $"AnhTKHT_ID{form.Id}_{safeName}_{timeStamp}{imgExtension}";
                        string imgFullPath = Path.Combine(networkPath, imgFileName);

                        using (var imgStream = new FileStream(imgFullPath, FileMode.Create))
                        {
                            await anhFile.CopyToAsync(imgStream);
                        }
                    }

                    if (itTaiKhoanList != null && itTaiKhoanList.Any())
                    {
                        foreach (var itTaiKhoan in itTaiKhoanList)
                        {
                            itTaiKhoan.IdFormIt = form.Id;

                            // Gán chung đường dẫn ảnh cho tất cả người/hệ thống được đăng ký
                            if (!string.IsNullOrEmpty(imgFileName))
                            {
                                itTaiKhoan.DuongDanAnh = imgFileName;
                                itTaiKhoan.Anh = null;
                            }
                        }

                        _context.ItDangKiTaiKhoanHeThong5s.AddRange(itTaiKhoanList);
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
                    string anhLog = string.IsNullOrEmpty(imgFileName) ? "Không có ảnh" : $"Ảnh: {imgFileName}";
                    var soLuongDangKy = itTaiKhoanList?.Count ?? 0;
                    var lichSu = new LichSuFormIt
                    {
                        IdFormIt = form.Id,
                        TieuDe = "Khởi tạo đơn Tài khoản Hệ thống",
                        Mota = $"Người thao tác: {userName} | Đăng ký cho {soLuongDangKy} tài khoản/hệ thống. {supporterLog}. {fileLog}. {anhLog}",
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

        #region Don Lap Dat Thiet Bi (Form IT 7)

        [HttpGet("/FormIT/DonLapDatThietBi")]
        public IActionResult DonLapDatThietBi()
        {
            if (User == null || !User.Identity.IsAuthenticated)
                return Redirect("/DonXetDuyet/DangNhap");

            // Lọc danh sách nhân viên IT và lấy những công việc có tên "Lắp đặt thiết bị"
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
                    CongViecIts = x.CongViecIts.Where(cv => cv.Ten == "Lắp đặt thiết bị").ToList()
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

        [HttpPost("/FormIT/DonLapDatThietBi")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DonLapDatThietBi(FormIt form, [FromForm] ItDonLapDatThietBi7 itEquipment, int[] SelectedCongViecIds)
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
                    form.IdForm = "IT_DonLapDatThietBi_7"; // Định danh form mới
                    form.TenForm = "Đơn yêu cầu lắp đặt thiết bị";
                    form.Danhmuc = "Lắp đặt thiết bị";

                    _context.FormIts.Add(form);
                    await _context.SaveChangesAsync();

                    // --- CẤU HÌNH ĐƯỜNG DẪN LƯU FILE ---
                    string networkPath = @"\\10.0.60.30\BPVN-Fileserver\Public\IT-Information Technology Dept\5.E-Form\DonIT";
                    if (!Directory.Exists(networkPath)) Directory.CreateDirectory(networkPath);

                    string safeName = RemoveSign4VietnameseString(userName).Replace(" ", "");
                    string timeStamp = DateTime.Now.ToString("ddMMyy_HHmmss");

                    // --- BƯỚC 2: XỬ LÝ FILE ĐÍNH KÈM & ẢNH ---
                    var uploadFile = Request.Form.Files["UploadFile"];
                    var anhFile = Request.Form.Files["Anh"];

                    if (uploadFile != null && uploadFile.Length > 0)
                    {
                        string extension = Path.GetExtension(uploadFile.FileName);
                        string fileName = $"DonLapDat_ID{form.Id}_{safeName}_{timeStamp}{extension}";
                        string fullPath = Path.Combine(networkPath, fileName);

                        using (var fileStream = new FileStream(fullPath, FileMode.Create))
                        {
                            await uploadFile.CopyToAsync(fileStream);
                        }

                        form.FileDinhKem = fileName;
                        _context.Entry(form).Property(x => x.FileDinhKem).IsModified = true;
                    }

                    if (anhFile != null && anhFile.Length > 0)
                    {
                        string imgExtension = Path.GetExtension(anhFile.FileName);
                        if (string.IsNullOrEmpty(imgExtension)) imgExtension = ".jpg";

                        string imgFileName = $"AnhLapDat_ID{form.Id}_{safeName}_{timeStamp}{imgExtension}";
                        string imgFullPath = Path.Combine(networkPath, imgFileName);

                        using (var imgStream = new FileStream(imgFullPath, FileMode.Create))
                        {
                            await anhFile.CopyToAsync(imgStream);
                        }

                        // Gán thẳng tên file ảnh vào model số 7
                        if (itEquipment != null)
                        {
                            itEquipment.DuongDanAnh = imgFileName;
                        }
                    }

                    await _context.SaveChangesAsync();

                    // --- BƯỚC 3: LƯU CHI TIẾT LẮP ĐẶT ---
                    if (itEquipment != null)
                    {
                        itEquipment.IdFormIt = form.Id;
                        _context.ItDonLapDatThietBi7s.Add(itEquipment);
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
                            .Where(cv => cv.Ten == "Lắp đặt thiết bị" && cv.IdItNguoiHoTro != null)
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
                    string moTaChiTiet = $"[Khởi tạo đơn] Lắp đặt thiết bị\n" +
                                         $"- Tên thiết bị: {itEquipment?.TenThietBi}\n" +
                                         $"- Vị trí: {itEquipment?.ViTri}\n" +
                                         $"- Mục đích: {itEquipment?.MucDich}\n" +
                                         $"- Ảnh đính kèm: {(string.IsNullOrEmpty(itEquipment?.DuongDanAnh) ? "Không có" : itEquipment.DuongDanAnh)}\n" +
                                         $"- File đính kèm: {(string.IsNullOrEmpty(form.FileDinhKem) ? "Không có" : form.FileDinhKem)}\n" +
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

                    TempData["Success"] = "Gửi đơn yêu cầu lắp đặt thiết bị thành công!";
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
                            CongViecIts = x.CongViecIts.Where(cv => cv.Ten == "Lắp đặt thiết bị").ToList()
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
            if (string.IsNullOrEmpty(userIdStr))
            {
                return Redirect("/DonXetDuyet/DangNhap");
            }

            int userId = int.Parse(userIdStr);

            // Lấy các thông tin từ Claims để check quyền
            var userEmail = User.FindFirst(System.Security.Claims.ClaimTypes.Email)?.Value ?? "";
            var tenCongTyUser = User.FindFirst("TenCongTy")?.Value?.Trim() ?? "";
            var boPhanUser = User.FindFirst("TenBoPhan")?.Value ?? "";
            // Lưu ý: TenBoPhan có thể chứa chuỗi gộp "IT, HR, Kế toán", ta sẽ check chứa chuỗi (contains)

            // 2. Lấy dữ liệu đơn (Giữ nguyên toàn bộ các Include hiện có và THÊM FORM 7)
            var don = await _context.FormIts
                .Include(f => f.ItMail1s)
                .Include(f => f.ItOrderIt2s)
                .Include(f => f.ItDangKiSuDungWifi3s)
                .Include(f => f.ItDangKiSuDungDtban4s)
                .Include(f => f.ItDangKiTaiKhoanHeThong5s)
                .Include(f => f.ItDangkiTaiKhoanMayTinh6s)
                .Include(f => f.ItDonLapDatThietBi7s) // <--- ĐÃ BỔ SUNG ĐỂ LẤY DỮ LIỆU FORM 7
                .Include(f => f.ItCtNguoiHoTros)
                    .ThenInclude(ct => ct.IdItNguoiHoTroNavigation)
                .Include(f => f.LichSuFormIts)
                .Include(f => f.DanhGiaFormIts)
                .Include(f => f.BinhLuanFormIts)
                .FirstOrDefaultAsync(m => m.Id == id);

            if (don == null)
            {
                TempData["Error"] = "⚠️ Không tìm thấy đơn yêu cầu IT!";
                return RedirectToAction("LichSuIT");
            }

            // 3. KIỂM TRA QUYỀN XEM (LOGIC CỘNG DỒN)
            bool isAllowed = false;

            // Điều kiện 1: Là người tạo đơn
            if (don.IdNguoiTao == userId)
            {
                isAllowed = true;
            }
            // Điều kiện 2: Có quyền cao nhất (AdminIT hoặc All)
            else if (User.IsInRole("AdminIT") || User.IsInRole("All"))
            {
                isAllowed = true;
            }
            // Điều kiện 3: Có quyền Quản lý duyệt đơn (QuanLyDuyetDonIT)
            // Phải thỏa mãn: Cùng Công ty AND Cùng Bộ phận
            else if (User.IsInRole("QuanLyDuyetDonIT"))
            {
                bool isSameCompany = string.Equals(don.TenCongTy?.Trim(), tenCongTyUser, StringComparison.OrdinalIgnoreCase);

                // 1. Lấy danh sách nhiều bộ phận và bộ phận đơn lẻ từ Claims
                string listBoPhan = User.FindFirst("TenBoPhan")?.Value ?? "";
                string phongBanDon = User.FindFirst("PhongBan")?.Value ?? "";

                bool isSameDepartment = false;

                if (!string.IsNullOrEmpty(don.BoPhan))
                {
                    if (!string.IsNullOrEmpty(listBoPhan))
                    {
                        // Nếu có list bộ phận (nhiều bộ phận), check xem bộ phận của đơn có nằm trong list không
                        isSameDepartment = listBoPhan.Contains(don.BoPhan);
                    }
                    else
                    {
                        // Nếu list rỗng, check theo claim PhongBan đơn lẻ
                        isSameDepartment = string.Equals(don.BoPhan.Trim(), phongBanDon.Trim(), StringComparison.OrdinalIgnoreCase);
                    }
                }

                if (isSameCompany && isSameDepartment)
                {
                    isAllowed = true;
                }
            }

            // Nếu không thỏa mãn bất kỳ điều kiện nào ở trên
            if (!isAllowed)
            {
                return Forbid();
            }


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

        #region Xuất file Excel, Word, PDF chuyên nghiệp cho IT

        // ============================================================
        // ACTION XUẤT BIỂU MẪU IT (EXCEL, WORD, PDF)
        // ============================================================

        [HttpGet("/FormIT/ExportExcel/{id}")]
        public async Task<IActionResult> ExportExcel(int id)
        {
            var don = await _context.FormIts
                .Include(f => f.ItMail1s)
                .Include(f => f.ItOrderIt2s)
                .Include(f => f.ItDangKiSuDungWifi3s)
                .Include(f => f.ItDangKiSuDungDtban4s)
                .Include(f => f.ItDangKiTaiKhoanHeThong5s)
                .Include(f => f.ItDangkiTaiKhoanMayTinh6s)
                .Include(f => f.ItCtNguoiHoTros).ThenInclude(ct => ct.IdItNguoiHoTroNavigation)
                .Include(f => f.LichSuFormIts)
                .FirstOrDefaultAsync(m => m.Id == id);

            if (don == null) return NotFound("Không tìm thấy đơn IT!");

            using (var workbook = new ClosedXML.Excel.XLWorkbook())
            {
                var worksheet = workbook.Worksheets.Add("ChiTietDonIT");

                // Header tiêu đề hệ thống
                worksheet.Cell(1, 1).Value = "HỆ THỐNG E-FORM IT - BEST PACIFIC VIETNAM";
                worksheet.Range("A1:E1").Merge().Style.Font.SetBold().Font.FontSize = 15;
                worksheet.Range("A1:E1").Style.Alignment.SetHorizontal(ClosedXML.Excel.XLAlignmentHorizontalValues.Center);

                // THÔNG TIN CHUNG
                worksheet.Cell(3, 1).Value = "Mã số phiếu:"; worksheet.Cell(3, 2).Value = $"#{don.Id}";
                worksheet.Cell(4, 1).Value = "Tên loại đơn:"; worksheet.Cell(4, 2).Value = don.TenForm;
                worksheet.Cell(5, 1).Value = "Nhân viên yêu cầu:"; worksheet.Cell(5, 2).Value = don.TenNguoiNv;
                worksheet.Cell(6, 1).Value = "Mã nhân viên:"; worksheet.Cell(6, 2).Value = don.SoNhanVien;
                worksheet.Cell(7, 1).Value = "Bộ phận:"; worksheet.Cell(7, 2).Value = don.BoPhan;
                worksheet.Cell(8, 1).Value = "Công ty:"; worksheet.Cell(8, 2).Value = don.TenCongTy;
                worksheet.Cell(9, 1).Value = "Ngày tạo:"; worksheet.Cell(9, 2).Value = don.TimeNguoiTao?.ToString("dd/MM/yyyy HH:mm");
                worksheet.Cell(10, 1).Value = "Trạng thái:"; worksheet.Cell(10, 2).Value = "HOÀN TẤT";

                var rangeChung = worksheet.Range("A3:B10");
                rangeChung.Style.Border.SetOutsideBorder(ClosedXML.Excel.XLBorderStyleValues.Thin);
                rangeChung.Style.Border.SetInsideBorder(ClosedXML.Excel.XLBorderStyleValues.Thin);
                worksheet.Range("A3:A10").Style.Font.SetBold().Fill.SetBackgroundColor(ClosedXML.Excel.XLColor.AliceBlue);

                int currentRow = 12;

                // XỬ LÝ 6 LOẠI CHI TIẾT
                if (don.ItMail1s.Any())
                {
                    var ct = don.ItMail1s.First();
                    worksheet.Cell(currentRow, 1).Value = "NỘI DUNG CHI TIẾT: ĐĂNG KÝ/SỬA ĐỔI EMAIL";
                    worksheet.Range(currentRow, 1, currentRow, 2).Merge().Style.Font.SetBold().Fill.SetBackgroundColor(ClosedXML.Excel.XLColor.LightBlue);
                    currentRow++;
                    worksheet.Cell(currentRow, 1).Value = "Loại yêu cầu:"; worksheet.Cell(currentRow, 2).Value = ct.LoaiYeuCau; currentRow++;
                    worksheet.Cell(currentRow, 1).Value = "Người sử dụng:"; worksheet.Cell(currentRow, 2).Value = ct.NguoiSuDung; currentRow++;
                    worksheet.Cell(currentRow, 1).Value = "Địa chỉ Email:"; worksheet.Cell(currentRow, 2).Value = ct.Email; currentRow++;
                    worksheet.Cell(currentRow, 1).Value = "Vị trí/Chức vụ:"; worksheet.Cell(currentRow, 2).Value = ct.ViTri; currentRow++;
                    worksheet.Cell(currentRow, 1).Value = "Số nội bộ:"; worksheet.Cell(currentRow, 2).Value = ct.SoNoiBo; currentRow++;
                    worksheet.Cell(currentRow, 1).Value = "Số điện thoại:"; worksheet.Cell(currentRow, 2).Value = ct.SoDienThoai; currentRow++;
                    worksheet.Cell(currentRow, 1).Value = "Gửi nhận quốc tế:"; worksheet.Cell(currentRow, 2).Value = ct.GuiRaNgoai; currentRow++;
                    worksheet.Cell(currentRow, 1).Value = "Dùng trên Mobile:"; worksheet.Cell(currentRow, 2).Value = ct.SuDungTrenDienThoai; currentRow++;
                    worksheet.Cell(currentRow, 1).Value = "Webmail:"; worksheet.Cell(currentRow, 2).Value = ct.SuDungWebMail; currentRow++;
                    worksheet.Cell(currentRow, 1).Value = "Mục đích:"; worksheet.Cell(currentRow, 2).Value = ct.MucDich; currentRow++;
                }
                else if (don.ItOrderIt2s.Any())
                {
                    var ct = don.ItOrderIt2s.First();
                    worksheet.Cell(currentRow, 1).Value = "NỘI DUNG CHI TIẾT: SỬA CHỮA / CẤP THIẾT BỊ";
                    worksheet.Range(currentRow, 1, currentRow, 2).Merge().Style.Font.SetBold().Fill.SetBackgroundColor(ClosedXML.Excel.XLColor.LightCoral);
                    currentRow++;
                    worksheet.Cell(currentRow, 1).Value = "Tên thiết bị/Sự cố:"; worksheet.Cell(currentRow, 2).Value = ct.Ten; currentRow++;
                    worksheet.Cell(currentRow, 1).Value = "Mô tả lỗi/Yêu cầu:"; worksheet.Cell(currentRow, 2).Value = ct.GhiChu; currentRow++;
                }
                else if (don.ItDangKiSuDungWifi3s.Any())
                {
                    worksheet.Cell(currentRow, 1).Value = "NỘI DUNG CHI TIẾT: ĐĂNG KÝ SỬ DỤNG WIFI";
                    worksheet.Range(currentRow, 1, currentRow, 5).Merge().Style.Font.SetBold().Fill.SetBackgroundColor(ClosedXML.Excel.XLColor.LightGreen);
                    currentRow++;
                    worksheet.Cell(currentRow, 1).Value = "Loại thiết bị"; worksheet.Cell(currentRow, 2).Value = "Mã thiết bị"; worksheet.Cell(currentRow, 3).Value = "Địa chỉ MAC"; worksheet.Cell(currentRow, 4).Value = "Thời gian"; worksheet.Cell(currentRow, 5).Value = "Lý do";
                    worksheet.Range(currentRow, 1, currentRow, 5).Style.Font.SetBold().Fill.SetBackgroundColor(ClosedXML.Excel.XLColor.LightGray);
                    foreach (var w in don.ItDangKiSuDungWifi3s)
                    {
                        currentRow++;
                        worksheet.Cell(currentRow, 1).Value = w.LoaiThietBi;
                        worksheet.Cell(currentRow, 2).Value = w.MaThietBi;
                        worksheet.Cell(currentRow, 3).Value = w.MacTb;
                        worksheet.Cell(currentRow, 4).Value = $"{w.ThoiGianBatDau:dd/MM} - {w.ThoiGianKetThuc:dd/MM/yyyy}";
                        worksheet.Cell(currentRow, 5).Value = w.LyDo;
                    }
                }
                else if (don.ItDangKiSuDungDtban4s.Any())
                {
                    var ct = don.ItDangKiSuDungDtban4s.First();
                    worksheet.Cell(currentRow, 1).Value = "NỘI DUNG CHI TIẾT: ĐIỆN THOẠI BÀN";
                    worksheet.Range(currentRow, 1, currentRow, 2).Merge().Style.Font.SetBold().Fill.SetBackgroundColor(ClosedXML.Excel.XLColor.LightYellow);
                    currentRow++;
                    worksheet.Cell(currentRow, 1).Value = "Thông tin:"; worksheet.Cell(currentRow, 2).Value = ct.ThongTin; currentRow++;
                    worksheet.Cell(currentRow, 1).Value = "Mục đích:"; worksheet.Cell(currentRow, 2).Value = ct.MucDich; currentRow++;
                }
                else if (don.ItDangKiTaiKhoanHeThong5s.Any())
                {
                    var ct = don.ItDangKiTaiKhoanHeThong5s.First();
                    worksheet.Cell(currentRow, 1).Value = "NỘI DUNG CHI TIẾT: ĐĂNG KÝ TÀI KHOẢN HỆ THỐNG";
                    worksheet.Range(currentRow, 1, currentRow, 2).Merge().Style.Font.SetBold().Fill.SetBackgroundColor(ClosedXML.Excel.XLColor.LightPink);
                    currentRow++;
                    worksheet.Cell(currentRow, 1).Value = "Hệ thống:"; worksheet.Cell(currentRow, 2).Value = ct.HeThongNao; currentRow++;
                    worksheet.Cell(currentRow, 1).Value = "Loại đơn:"; worksheet.Cell(currentRow, 2).Value = ct.LoaiDon; currentRow++;
                    worksheet.Cell(currentRow, 1).Value = "Đăng ký cho:"; worksheet.Cell(currentRow, 2).Value = ct.DangKiChoAi; currentRow++;
                    worksheet.Cell(currentRow, 1).Value = "Sử dụng tại máy:"; worksheet.Cell(currentRow, 2).Value = ct.DungTrenMayNao; currentRow++;
                    worksheet.Cell(currentRow, 1).Value = "Quyền giống:"; worksheet.Cell(currentRow, 2).Value = ct.CapQuyenGiongAi; currentRow++;
                    worksheet.Cell(currentRow, 1).Value = "Mô tả chi tiết:"; worksheet.Cell(currentRow, 2).Value = ct.MoTaChiTiet; currentRow++;
                    worksheet.Cell(currentRow, 1).Value = "Mục đích:"; worksheet.Cell(currentRow, 2).Value = ct.MucDich; currentRow++;
                }
                else if (don.ItDangkiTaiKhoanMayTinh6s.Any())
                {
                    var ct = don.ItDangkiTaiKhoanMayTinh6s.First();
                    worksheet.Cell(currentRow, 1).Value = "NỘI DUNG CHI TIẾT: TÀI KHOẢN MÁY TÍNH / Ổ CHUNG";
                    worksheet.Range(currentRow, 1, currentRow, 2).Merge().Style.Font.SetBold().Fill.SetBackgroundColor(ClosedXML.Excel.XLColor.Cyan);
                    currentRow++;
                    worksheet.Cell(currentRow, 1).Value = "Loại yêu cầu:"; worksheet.Cell(currentRow, 2).Value = ct.Loai; currentRow++;
                    worksheet.Cell(currentRow, 1).Value = "Người sử dụng:"; worksheet.Cell(currentRow, 2).Value = ct.HoTenVaMaNhanVienNguoiSuDung; currentRow++;
                    worksheet.Cell(currentRow, 1).Value = "Chức vụ:"; worksheet.Cell(currentRow, 2).Value = ct.ChucVu; currentRow++;
                    worksheet.Cell(currentRow, 1).Value = "SĐT / Nội bộ:"; worksheet.Cell(currentRow, 2).Value = $"{ct.SoDienThoai} (Ext: {ct.SoNoiBo})"; currentRow++;
                    worksheet.Cell(currentRow, 1).Value = "Tên máy tính:"; worksheet.Cell(currentRow, 2).Value = ct.TenMayCanCai; currentRow++;
                    worksheet.Cell(currentRow, 1).Value = "Quyền ổ chung:"; worksheet.Cell(currentRow, 2).Value = ct.TenOchungCanAddQuyen; currentRow++;
                    worksheet.Cell(currentRow, 1).Value = "Mục đích:"; worksheet.Cell(currentRow, 2).Value = ct.MucDich; currentRow++;
                    worksheet.Cell(currentRow, 1).Value = "Ghi chú:"; worksheet.Cell(currentRow, 2).Value = ct.GhiChu; currentRow++;
                }

                currentRow += 2;

                // ĐỘI NGŨ HỖ TRỢ
                if (don.ItCtNguoiHoTros.Any())
                {
                    worksheet.Cell(currentRow, 1).Value = "DANH SÁCH IT HỖ TRỢ";
                    worksheet.Range(currentRow, 1, currentRow, 3).Merge().Style.Font.SetBold().Fill.SetBackgroundColor(ClosedXML.Excel.XLColor.Orange);
                    currentRow++;
                    worksheet.Cell(currentRow, 1).Value = "STT"; worksheet.Cell(currentRow, 2).Value = "Họ Tên"; worksheet.Cell(currentRow, 3).Value = "Mã NV";
                    int stt = 1;
                    foreach (var ht in don.ItCtNguoiHoTros)
                    {
                        currentRow++;
                        worksheet.Cell(currentRow, 1).Value = stt++;
                        worksheet.Cell(currentRow, 2).Value = ht.IdItNguoiHoTroNavigation?.Ten;
                        worksheet.Cell(currentRow, 3).Value = ht.IdItNguoiHoTroNavigation?.MaNv;
                    }
                }

                worksheet.Columns().AdjustToContents();
                using (var stream = new System.IO.MemoryStream())
                {
                    workbook.SaveAs(stream);
                    return File(stream.ToArray(), "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", $"DonIT_{id}.xlsx");
                }
            }
        }

        [HttpGet("/FormIT/ExportWord/{id}")]
        public async Task<IActionResult> ExportWord(int id)
        {
            var don = await GetFullDonIT(id);
            if (don == null) return NotFound();
            string htmlContent = BuildHtmlContentIT(don, isForWord: true);
            byte[] byteArray = System.Text.Encoding.UTF8.GetBytes(htmlContent);
            return File(byteArray, "application/msword", $"DonIT_{id}.doc");
        }

        [HttpGet("/FormIT/ExportPDF/{id}")]
        public async Task<IActionResult> ExportPDF(int id)
        {
            var don = await GetFullDonIT(id);
            if (don == null) return NotFound();
            string htmlContent = BuildHtmlContentIT(don, isForWord: false);
            return Content(htmlContent, "text/html", System.Text.Encoding.UTF8);
        }

        private async Task<FormIt?> GetFullDonIT(int id)
        {
            return await _context.FormIts
                .Include(f => f.ItMail1s).Include(f => f.ItOrderIt2s).Include(f => f.ItDangKiSuDungWifi3s)
                .Include(f => f.ItDangKiSuDungDtban4s).Include(f => f.ItDangKiTaiKhoanHeThong5s).Include(f => f.ItDangkiTaiKhoanMayTinh6s)
                .Include(f => f.ItCtNguoiHoTros).ThenInclude(ct => ct.IdItNguoiHoTroNavigation)
                .Include(f => f.LichSuFormIts)
                .FirstOrDefaultAsync(m => m.Id == id);
        }

        private string BuildHtmlContentIT(FormIt don, bool isForWord = false)
        {
            var sb = new System.Text.StringBuilder();
            sb.Append("<html><head><meta charset='utf-8'/>");
            sb.Append("<style>");
            sb.Append(@"
                body { font-family: 'Times New Roman', serif; line-height: 1.5; color: #000; }
                .container { max-width: 850px; margin: 0 auto; padding: 30px; }
                .header-table { width: 100%; border: none; margin-bottom: 20px; }
                .company-info { font-weight: bold; text-transform: uppercase; font-size: 14pt; }
                .form-title { font-size: 20pt; font-weight: bold; text-align: center; text-transform: uppercase; margin: 30px 0 10px; }
                .form-subtitle { text-align: center; font-style: italic; margin-bottom: 30px; }
                .section-header { font-size: 13pt; font-weight: bold; text-transform: uppercase; border-bottom: 2px solid #1e40af; color: #1e40af; margin-top: 25px; margin-bottom: 10px; padding-bottom: 5px; }
                .data-table { width: 100%; border-collapse: collapse; margin-bottom: 20px; }
                .data-table th, .data-table td { border: 1px solid #333; padding: 10px; font-size: 12pt; text-align: left; }
                .data-table th { background: #f3f4f6; width: 35%; font-weight: bold; }
                .signature-table { width: 100%; text-align: center; margin-top: 50px; border: none; }
                .signature-table td { border: none; width: 33%; vertical-align: top; }
                .log-table { width: 100%; border-collapse: collapse; margin-top: 10px; }
                .log-table td { border: 1px solid #ddd; padding: 6px; font-size: 10pt; }
            ");
            if (!isForWord) sb.Append("@page { size: A4; margin: 20mm; } script { window.onload = function() { window.print(); } } ");
            sb.Append("</style></head><body><div class='container'>");

            // Header
            sb.Append("<table class='header-table'><tr>");
            sb.Append("<td style='width:50%'><div class='company-info'>BEST PACIFIC VIETNAM</div><div>IT DEPARTMENT - SERVICE FORM</div></td>");
            sb.Append("<td style='width:50%; text-align:right;'>CỘNG HÒA XÃ HỘI CHỦ NGHĨA VIỆT NAM<br/>Độc lập - Tự do - Hạnh phúc</td>");
            sb.Append("</tr></table>");

            sb.Append($"<div class='form-title'>{don.TenForm}</div>");
            sb.Append($"<div class='form-subtitle'>Mã phiếu: #{don.Id} | Ngày tạo: {don.TimeNguoiTao:dd/MM/yyyy HH:mm}</div>");

            // I. THÔNG TIN NGƯỜI YÊU CẦU
            sb.Append("<div class='section-header'>I. THÔNG TIN NGƯỜI YÊU CẦU</div>");
            sb.Append("<table class='data-table'>");
            sb.Append($"<tr><th>Họ và tên</th><td>{don.TenNguoiNv}</td></tr>");
            sb.Append($"<tr><th>Mã nhân viên</th><td>{don.SoNhanVien}</td></tr>");
            sb.Append($"<tr><th>Bộ phận / Phòng ban</th><td>{don.BoPhan}</td></tr>");
            sb.Append($"<tr><th>Công ty</th><td>{don.TenCongTy}</td></tr>");
            sb.Append($"<tr><th>Danh mục yêu cầu</th><td>{don.Danhmuc}</td></tr>");
            sb.Append("</table>");

            // II. CHI TIẾT KỸ THUẬT
            sb.Append("<div class='section-header'>II. NỘI DUNG CHI TIẾT</div>");
            sb.Append("<table class='data-table'>");
            if (don.ItMail1s.Any())
            {
                var ct = don.ItMail1s.First();
                sb.Append($"<tr><th>Loại đơn</th><td>Đăng ký/Sửa đổi Email</td></tr>");
                sb.Append($"<tr><th>Người sử dụng</th><td>{ct.NguoiSuDung}</td></tr>");
                sb.Append($"<tr><th>Địa chỉ Email</th><td>{ct.Email}</td></tr>");
                sb.Append($"<tr><th>Số nội bộ / SĐT</th><td>{ct.SoNoiBo} / {ct.SoDienThoai}</td></tr>");
                sb.Append($"<tr><th>Cấu hình</th><td>Quốc tế: {ct.GuiRaNgoai} | Mobile: {ct.SuDungTrenDienThoai} | Webmail: {ct.SuDungWebMail}</td></tr>");
                sb.Append($"<tr><th>Mục đích</th><td>{ct.MucDich}</td></tr>");
            }
            else if (don.ItOrderIt2s.Any())
            {
                var ct = don.ItOrderIt2s.First();
                sb.Append($"<tr><th>Loại đơn</th><td>Sửa chữa / Cấp thiết bị</td></tr>");
                sb.Append($"<tr><th>Thiết bị/Sự cố</th><td>{ct.Ten}</td></tr>");
                sb.Append($"<tr><th>Mô tả chi tiết</th><td>{ct.GhiChu}</td></tr>");
            }
            else if (don.ItDangKiSuDungWifi3s.Any())
            {
                sb.Append($"<tr><th>Loại đơn</th><td>Đăng ký sử dụng Wifi</td></tr>");
                foreach (var w in don.ItDangKiSuDungWifi3s)
                {
                    sb.Append($"<tr><th>Thiết bị: {w.LoaiThietBi}</th><td>MAC: {w.MacTb} | Lý do: {w.LyDo}</td></tr>");
                }
            }
            else if (don.ItDangKiTaiKhoanHeThong5s.Any())
            {
                var ct = don.ItDangKiTaiKhoanHeThong5s.First();
                sb.Append($"<tr><th>Loại đơn</th><td>Đăng ký tài khoản hệ thống</td></tr>");
                sb.Append($"<tr><th>Hệ thống</th><td>{ct.HeThongNao}</td></tr>");
                sb.Append($"<tr><th>Đăng ký cho / Tại máy</th><td>{ct.DangKiChoAi} / {ct.DungTrenMayNao}</td></tr>");
                sb.Append($"<tr><th>Quyền giống nhân viên</th><td>{ct.CapQuyenGiongAi}</td></tr>");
                sb.Append($"<tr><th>Mô tả chi tiết</th><td>{ct.MoTaChiTiet}</td></tr>");
                sb.Append($"<tr><th>Mục đích</th><td>{ct.MucDich}</td></tr>");
            }
            else if (don.ItDangkiTaiKhoanMayTinh6s.Any())
            {
                var ct = don.ItDangkiTaiKhoanMayTinh6s.First();
                sb.Append($"<tr><th>Loại đơn</th><td>Tài khoản máy tính / Ổ chung</td></tr>");
                sb.Append($"<tr><th>Người sử dụng</th><td>{ct.HoTenVaMaNhanVienNguoiSuDung}</td></tr>");
                sb.Append($"<tr><th>Liên hệ</th><td>SĐT: {ct.SoDienThoai} | Ext: {ct.SoNoiBo}</td></tr>");
                sb.Append($"<tr><th>Tên máy tính / Ổ chung</th><td>Máy: {ct.TenMayCanCai} | Ổ: {ct.TenOchungCanAddQuyen}</td></tr>");
                sb.Append($"<tr><th>Mục đích</th><td>{ct.MucDich}</td></tr>");
            }
            sb.Append("</table>");

            // III. ĐỘI NGŨ IT HỖ TRỢ
            if (don.ItCtNguoiHoTros.Any())
            {
                sb.Append("<div class='section-header'>III. NHÂN VIÊN IT THỰC HIỆN</div>");
                sb.Append("<table class='data-table'><tr><th style='width:10%'>STT</th><th style='width:50%'>Họ và tên IT</th><th>Mã nhân viên</th></tr>");
                int stt = 1;
                foreach (var ht in don.ItCtNguoiHoTros)
                {
                    sb.Append($"<tr><td style='text-align:center;'>{stt++}</td><td>{ht.IdItNguoiHoTroNavigation?.Ten}</td><td>{ht.IdItNguoiHoTroNavigation?.MaNv}</td></tr>");
                }
                sb.Append("</table>");
            }

            // IV. NHẬT KÝ THAO TÁC
            if (don.LichSuFormIts.Any())
            {
                sb.Append("<div class='section-header'>IV. NHẬT KÝ XỬ LÝ (LOGS)</div>");
                sb.Append("<table class='log-table'>");
                foreach (var ls in don.LichSuFormIts.OrderBy(x => x.Time))
                {
                    sb.Append($"<tr><td style='width:20%'>{ls.Time:dd/MM/yyyy HH:mm}</td><td style='width:25%'><b>{ls.TieuDe}</b></td><td>{ls.Mota}</td></tr>");
                }
                sb.Append("</table>");
            }

            // V. CHỮ KÝ
            sb.Append("<table class='signature-table'><tr>");
            sb.Append($"<td><strong>NGƯỜI YÊU CẦU</strong><br/><span style='font-size:10pt;'>(Ký, ghi rõ họ tên)</span><br/><br/><br/><br/><strong>{don.TenNguoiNv}</strong></td>");
            sb.Append($"<td><strong>QUẢN LÝ DUYỆT</strong><br/><span style='font-size:10pt;'>(Ký, ghi rõ họ tên)</span><br/><br/><br/><br/><strong>{don.TenNguoiDuyet}</strong></td>");
            sb.Append($"<td><strong>PHÊ DUYỆT IT</strong><br/><span style='font-size:10pt;'>(Ký, ghi rõ họ tên)</span><br/><br/><br/><br/><strong>{don.TenAdmin}</strong></td>");
            sb.Append("</tr></table>");

            sb.Append("</div></body></html>");
            return sb.ToString();
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

        // 1. Chỉ trả về View giao diện
        [HttpGet("/FormIT/DonCho")]
        public IActionResult DonCho()
        {
            if (User == null || !User.Identity.IsAuthenticated)
            {
                return Redirect("/DonXetDuyet/DangNhap");
            }
            return View();
        }

        [HttpGet("/FormIT/GetDonChoData")]
        public async Task<IActionResult> GetDonChoData()
        {
            // 1. LẤY THÔNG TIN TỪ CLAIMS
            var userIdStr = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userIdStr)) return Unauthorized();

            int userId = int.Parse(userIdStr);

            // 2. TRUY VẤN DỮ LIỆU
            var danhSachDon = await _context.FormIts
                .AsNoTracking() // TỐI ƯU 1: Không theo dõi thay đổi, giảm tải bộ nhớ đáng kể
                .Where(f => f.IdNguoiTao == userId)
                // TỐI ƯU 2: Loại bỏ .Include(). Khi dùng .Select(), EF Core sẽ tự động sinh câu lệnh JOIN 
                // chính xác vào bảng cần lấy, giúp tránh lấy dư thừa các cột không dùng tới.
                .OrderByDescending(f => f.Id)
                .Select(item => new {
                    Id = item.Id,
                    TenForm = item.TenForm ?? "",
                    Danhmuc = item.Danhmuc ?? "N/A",
                    // TỐI ƯU 3: Giữ nguyên logic format ngày tháng
                    Ngay = item.Ngay.HasValue ? item.Ngay.Value.ToString("dd/MM/yyyy") : "",
                    IdNguoiDuyet = item.IdNguoiDuyet,
                    IdAdmin = item.IdAdmin,

                    // TỐI ƯU 4: Dùng .Any() trực tiếp trên navigation property 
                    DaDanhGia = item.DanhGiaFormIts.Any(),

                    // TỐI ƯU 5: Truy vấn sâu xuống chỉ lấy cột 'Ten' thay vì lấy cả Object navigation
                    TenNguoiHoTro = item.ItCtNguoiHoTros
                                        .OrderByDescending(x => x.Stt)
                                        .Select(x => x.IdItNguoiHoTroNavigation.Ten)
                                        .FirstOrDefault() ?? "Chưa có"
                })
                .ToListAsync();

            return Json(danhSachDon);
        }

        #endregion

        #region XỬ LÝ ĐƠN IT (Duyệt / Hủy / Hoàn tất) - PHÂN QUYỀN MỚI 2026

        // 1. HÀM TRẢ VỀ VIEW (Giao diện trống để JS tự load data)
        [HttpGet("/FormIT/QuanLyXetDuyet")]
        public IActionResult QuanLyXetDuyet()
        {
            var userIdStr = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userIdStr)) return Redirect("/DonXetDuyet/DangNhap");

            return View();
        }

        // 2. HÀM API TRẢ VỀ DỮ LIỆU JSON CHO JAVASCRIPT
        [HttpGet("/FormIT/GetQuanLyXetDuyetData")]
        public async Task<IActionResult> GetQuanLyXetDuyetData()
        {
            // --- 1. LẤY THÔNG TIN TỪ CLAIMS ---
            var userIdStr = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userIdStr)) return Unauthorized();
            int userId = int.Parse(userIdStr);

            var userRoles = User.FindAll(System.Security.Claims.ClaimTypes.Role).Select(c => c.Value).ToList();
            var phongBan = User.FindFirst("PhongBan")?.Value?.Trim() ?? "";
            var tenCongTy = User.FindFirst("TenCongTy")?.Value?.Trim() ?? "";
            var listTenBoPhanStr = User.FindFirst("TenBoPhan")?.Value ?? "";

            // Xử lý list bộ phận sẵn ở bộ nhớ để so sánh nhanh hơn
            var listTenBoPhan = listTenBoPhanStr.Split(',')
                                                .Select(s => s.Trim())
                                                .Where(s => !string.IsNullOrEmpty(s))
                                                .ToList();

            if (userRoles.Contains("BaoVe")) return Forbid();

            // --- 2. TRUY VẤN DỮ LIỆU ---
            // TỐI ƯU 1: Loại bỏ .Include() và .ThenInclude(). 
            // Khi dùng .Select(), EF Core sẽ tự động JOIN những bảng cần thiết. 
            // Việc Include dư thừa sẽ làm tăng tải cho SQL Server vì phải lấy toàn bộ các cột của bảng liên quan.
            IQueryable<FormIt> query = _context.FormIts.AsNoTracking();

            // --- 3. LOGIC PHÂN QUYỀN ---
            if (!userRoles.Contains("All") && !string.IsNullOrEmpty(tenCongTy))
            {
                query = query.Where(f => f.TenCongTy == tenCongTy);
            }

            if (userRoles.Contains("All"))
            {
                // View all
            }
            else if (userRoles.Contains("QuanLyDuyetDonIT"))
            {
                // TỐI ƯU 2: Hạn chế dùng .Trim().ToLower() bên trong .Where() nếu Database đã không phân biệt hoa thường (Case-insensitive).
                // Việc dùng hàm trên cột (f.BoPhan) sẽ làm SQL không sử dụng được Index (Non-SARGable).
                if (listTenBoPhan.Any())
                {
                    query = query.Where(f => f.BoPhan != null && listTenBoPhan.Contains(f.BoPhan));
                }
                else if (!string.IsNullOrEmpty(phongBan))
                {
                    query = query.Where(f => f.BoPhan == phongBan);
                }
                else
                {
                    query = query.Where(f => f.IdNguoiTao == userId);
                }
            }
            else
            {
                query = query.Where(f => f.IdNguoiTao == userId);
            }

            // --- 4. PROJECT DATA ---
            var danhSachDon = await query
                .OrderByDescending(f => f.Id)
                .Select(item => new {
                    Id = item.Id,
                    TenNguoiNv = item.TenNguoiNv ?? "",
                    SoNhanVien = item.SoNhanVien ?? "",
                    BoPhan = item.BoPhan ?? "",
                    Danhmuc = string.IsNullOrEmpty(item.Danhmuc) ? "Phổ thông" : item.Danhmuc,
                    TenForm = item.TenForm ?? "",
                    // TỐI ƯU 3: Việc định dạng ngày tháng nên để Client làm hoặc xử lý sau khi lấy dữ liệu 
                    // để SQL Server chỉ tập trung lấy dữ liệu thô. Tuy nhiên tôi giữ nguyên theo yêu cầu của bạn.
                    TimeNguoiTao = item.TimeNguoiTao.HasValue ? item.TimeNguoiTao.Value.ToString("dd/MM/yyyy") : "",
                    IdNguoiDuyet = item.IdNguoiDuyet,
                    IdAdmin = item.IdAdmin,
                    DaDanhGia = item.DanhGiaFormIts.Any(),
                    // TỐI ƯU 4: Subquery lấy người hỗ trợ mới nhất
                    TenNguoiHoTro = item.ItCtNguoiHoTros
                                        .OrderByDescending(x => x.Stt)
                                        .Select(x => x.IdItNguoiHoTroNavigation.Ten)
                                        .FirstOrDefault() ?? "Chưa gán IT"
                })
                .ToListAsync();

            return Json(danhSachDon);
        }


        // 3. HÀM XỬ LÝ POST (Giữ nguyên 100% của bạn)
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
            var userEmail = User.FindFirst(System.Security.Claims.ClaimTypes.Email)?.Value ?? "N/A";

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
                        moTaChiTiet = $"Người duyệt: {userName}({userEmail}). Bộ phận: {phongBan}.";
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
                        moTaChiTiet = $"Người hủy: {userName}({userEmail}). Lý do: {request.Reason}.";
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
                        moTaChiTiet = $"Đơn đã được xử lý xong bởi: {userName}({userEmail}).";
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

        // 1. CHỈ TRẢ VỀ VIEW (Giao diện)
        [HttpGet("/FormIT/HoanTatDon")]
        public IActionResult HoanTatDon()
        {
            var userIdStr = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userIdStr))
                return Redirect("/DonXetDuyet/DangNhap");

            return View();
        }

        // 2. API TRẢ VỀ DỮ LIỆU JSON CHO JAVASCRIPT
        [HttpGet("/FormIT/GetHoanTatDonData")]
        public async Task<IActionResult> GetHoanTatDonData()
        {
            // --- 1. LẤY THÔNG TIN TỪ CLAIMS (Giữ nguyên) ---
            var userIdStr = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userIdStr)) return Unauthorized();

            int userId = int.Parse(userIdStr);
            var userRoles = User.FindAll(System.Security.Claims.ClaimTypes.Role).Select(c => c.Value).ToList();
            var phongBanSession = User.FindFirst("PhongBan")?.Value?.Trim() ?? "";

            // Tối ưu: Chuẩn hóa dữ liệu so sánh ở bộ nhớ trước khi đưa vào Query
            var listTenBoPhanStr = User.FindFirst("TenBoPhan")?.Value ?? "";
            var listTenBoPhan = listTenBoPhanStr.Split(',')
                                                .Select(s => s.Trim())
                                                .Where(s => !string.IsNullOrEmpty(s))
                                                .ToList();

            // --- 2. KHỞI TẠO QUERY (Dùng AsNoTracking ngay từ đầu) ---
            // Không dùng .Include() vì .Select() bên dưới sẽ tự thực hiện Join tối ưu hơn
            var query = _context.FormIts.AsNoTracking();

            // --- 3. PHÂN QUYỀN LỌC DỮ LIỆU (Tối ưu SARGable) ---

            if (userRoles.Any(r => r == "All" || r == "AdminIT"))
            {
                query = query.Where(f => f.IdNguoiDuyet != null);
            }
            else if (userRoles.Contains("QuanLyDuyetDonIT"))
            {
                if (listTenBoPhan.Any())
                {
                    // Tránh dùng .ToLower() trên f.BoPhan để tận dụng Index (Database SQL Server mặc định thường Case-Insensitive)
                    query = query.Where(f => listTenBoPhan.Contains(f.BoPhan));
                }
                else if (!string.IsNullOrEmpty(phongBanSession))
                {
                    query = query.Where(f => f.BoPhan == phongBanSession);
                }
                else
                {
                    query = query.Where(f => f.IdNguoiTao == userId);
                }
            }
            else
            {
                query = query.Where(f => f.IdNguoiTao == userId);
            }

            // --- 4. THỰC THI TRUY VẤN VỚI PROJECTION (Tốc độ tối đa) ---
            var danhSachDon = await query
                .OrderByDescending(f => f.Id)
                .Select(item => new {
                    Id = item.Id,
                    TenNguoiNv = item.TenNguoiNv ?? "",
                    SoNhanVien = item.SoNhanVien ?? "",
                    BoPhan = item.BoPhan ?? "",
                    Danhmuc = (item.Danhmuc == null || item.Danhmuc == "") ? "Yêu cầu IT" : item.Danhmuc,
                    TenForm = item.TenForm ?? "",
                    // Format ngày tháng ngay trên SQL nếu cần hoặc trả về DateTime để JS format (trả về string chậm hơn một chút)
                    TimeNguoiTao = item.TimeNguoiTao.HasValue ? item.TimeNguoiTao.Value.ToString("dd/MM/yyyy") : "",
                    IdNguoiDuyet = item.IdNguoiDuyet,
                    IdAdmin = item.IdAdmin,
                    // Kiểm tra tồn tại nhanh hơn lấy cả list
                    DaDanhGia = item.DanhGiaFormIts.Any(),
                    // Join thủ công trong Select giúp SQL tối ưu hóa cột cần lấy
                    NguoiHoTros = item.ItCtNguoiHoTros.Select(ct => ct.IdItNguoiHoTroNavigation.Ten ?? "N/A").ToList()
                })
                .ToListAsync();

            return Json(danhSachDon);
        }

        // Nút bấm dành cho Đội IT - Xác nhận đã sửa xong/cấp xong thiết bị (GIỮ NGUYÊN 100%)
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

        #region Thống kê

        #region BÁO CÁO THỐNG KÊ FORM IT

        // 1. Action này chỉ trả về giao diện (View)
        [HttpGet("/FormIT/BaoCaoThongKe")]
        public IActionResult BaoCaoThongKe()
        {
            return View();
        }

        [HttpGet("/FormIT/GetDataThongKe")]
        public async Task<IActionResult> GetDataThongKe(DateTime? fromDate, DateTime? toDate)
        {
            try
            {
                var query = _context.FormIts.AsNoTracking();

                // Lọc theo khoảng thời gian nếu có chọn (Sử dụng trường TimeNguoiDuyet làm mốc)
                if (fromDate.HasValue)
                {
                    query = query.Where(x => x.TimeNguoiDuyet >= fromDate.Value);
                }
                if (toDate.HasValue)
                {
                    var endOfToDate = toDate.Value.Date.AddDays(1).AddTicks(-1);
                    query = query.Where(x => x.TimeNguoiDuyet <= endOfToDate);
                }

                // Sử dụng Select trực tiếp để EF Core sinh ra câu lệnh SQL tinh gọn nhất
                var data = await query
                    .Select(x => new {
                        x.Id,
                        x.IdForm,   // Mã Form (IT-01, IT-02...)
                        x.Danhmuc,  // Danh mục (Phần mềm, Phần cứng...)
                        x.BoPhan,
                        x.IdNguoiDuyet,
                        x.IdAdmin,
                        x.TenForm,
                        IsRated = x.DanhGiaFormIts.Any()
                    })
                    .ToListAsync();

                return Json(data);
            }
            catch (Exception ex)
            {
                return BadRequest(ex.Message);
            }
        }

        [HttpGet("/FormIT/GetDataNguoiHoTro")]
        public async Task<IActionResult> GetDataNguoiHoTro(DateTime? fromDate, DateTime? toDate)
        {
            try
            {
                var query = _context.ItCtNguoiHoTros.AsNoTracking();

                // Lọc theo khoảng thời gian nếu có chọn trước khi Select
                if (fromDate.HasValue)
                {
                    query = query.Where(x => x.IdFormItNavigation.TimeNguoiDuyet >= fromDate.Value);
                }
                if (toDate.HasValue)
                {
                    var endOfToDate = toDate.Value.Date.AddDays(1).AddTicks(-1);
                    query = query.Where(x => x.IdFormItNavigation.TimeNguoiDuyet <= endOfToDate);
                }

                // Lấy dữ liệu từ bảng chi tiết hỗ trợ
                var rawData = await query
                    .Select(x => new
                    {
                        x.IdFormIt,
                        x.Stt,
                        TenNguoiHoTro = x.IdItNguoiHoTroNavigation != null ? x.IdItNguoiHoTroNavigation.Ten : "Chưa xác định",
                        DanhMuc = x.IdFormItNavigation != null ? x.IdFormItNavigation.Danhmuc : "N/A",
                        TenForm = x.IdFormItNavigation != null ? x.IdFormItNavigation.TenForm : "",
                        IdAdmin = x.IdFormItNavigation != null ? x.IdFormItNavigation.IdAdmin : null,
                        IdNguoiDuyet = x.IdFormItNavigation != null ? x.IdFormItNavigation.IdNguoiDuyet : null,
                        TimeAdmin = x.IdFormItNavigation != null ? x.IdFormItNavigation.TimeAdmin : null,
                        TimeNguoiDuyet = x.IdFormItNavigation != null ? x.IdFormItNavigation.TimeNguoiDuyet : null,
                        // Kiểm tra xem đơn này đã có đánh giá chưa
                        HasRating = x.IdFormItNavigation != null && x.IdFormItNavigation.DanhGiaFormIts.Any()
                    })
                    .ToListAsync();

                // Sau khi lấy dữ liệu thô về RAM (đã lọc bớt cột), ta mới GroupBy để lấy Stt cao nhất
                // Việc này đảm bảo LINQ không bị lỗi khi dịch sang SQL mà vẫn chạy rất nhanh
                var filteredData = rawData
                    .GroupBy(x => x.IdFormIt)
                    .Select(group => {
                        var top = group.OrderByDescending(x => x.Stt).First();

                        double? minutes = null;
                        if (top.TimeAdmin.HasValue && top.TimeNguoiDuyet.HasValue)
                        {
                            minutes = (top.TimeAdmin.Value - top.TimeNguoiDuyet.Value).TotalMinutes;
                        }

                        return new
                        {
                            IdFormIt = top.IdFormIt, // Đã bổ sung cột này để JS có thể Join dữ liệu lọc chéo
                            TenNguoiHoTro = top.TenNguoiHoTro,
                            DanhMuc = top.DanhMuc,
                            TrangThai = (top.TenForm ?? "").Contains("[ĐÃ HỦY]") ? "HỦY" :
                                        (top.IdAdmin != null && top.HasRating) ? "HOÀN TẤT" :
                                        (top.IdAdmin != null) ? "ĐÁNH GIÁ" :
                                        (top.IdNguoiDuyet != null) ? "ĐANG XỬ LÝ" : "CHỜ QL",
                            PhutXuLy = minutes
                        };
                    })
                    .ToList();

                return Json(filteredData);
            }
            catch (Exception ex)
            {
                // Nếu có lỗi, trả về lỗi chi tiết để debug thay vì file HTML lỗi
                return BadRequest(ex.Message);
            }
        }

        #endregion

        #region THỐNG KÊ MAC WIFI THIẾT BỊ

        // 1. Action này trả về giao diện View mới
        [HttpGet("/FormIT/ThongKeMacWifi")]
        public IActionResult ThongKeMacWifi()
        {
            return View();
        }

        // 2. Action này lấy dữ liệu thống kê cho biểu đồ
        [HttpGet("/FormIT/GetDataThongKeMac")]
        public async Task<IActionResult> GetDataThongKeMac()
        {
            try
            {
                var data = await _context.ItDangKiSuDungWifi3s
                    .AsNoTracking()
                    .Where(x => !string.IsNullOrEmpty(x.MacTb))
                    .GroupBy(x => x.LoaiThietBi ?? "Khác")
                    .Select(g => new
                    {
                        LoaiThietBi = g.Key,
                        SoLuongMacTong = g.Count(),
                        SoLuongMacDuyNhat = g.Select(x => x.MacTb).Distinct().Count()
                    })
                    .ToListAsync();

                return Json(data);
            }
            catch (Exception ex)
            {
                return BadRequest(ex.Message);
            }
        }

        // 3. Action lấy danh sách chi tiết có bộ lọc, phân trang & SẮP XẾP
        [HttpGet("/FormIT/GetDanhSachMacWifi")]
        public async Task<IActionResult> GetDanhSachMacWifi(
            int page = 1, int pageSize = 50,
            string? idForm = null, string? tenNguoiNv = null, string? boPhan = null,
            string? loaiThietBi = null, string? macTb = null, string? maThietBi = null,
            DateTime? fromDate = null, DateTime? toDate = null,
            string sortColumn = "TimeAdmin", string sortDir = "desc") // Thêm tham số sắp xếp
        {
            try
            {
                var query = _context.ItDangKiSuDungWifi3s
                    .Include(x => x.IdFormItNavigation)
                    .AsNoTracking()
                    .Where(x => !string.IsNullOrEmpty(x.MacTb) &&
                                x.IdFormItNavigation != null &&
                                x.IdFormItNavigation.IdAdmin != null &&
                                x.IdFormItNavigation.TenAdmin != null &&
                                x.IdFormItNavigation.TimeAdmin != null);

                // Áp dụng các bộ lọc
                if (!string.IsNullOrEmpty(idForm)) query = query.Where(x => x.IdFormItNavigation.IdForm.Contains(idForm));
                if (!string.IsNullOrEmpty(tenNguoiNv)) query = query.Where(x => x.IdFormItNavigation.TenNguoiNv.Contains(tenNguoiNv));
                if (!string.IsNullOrEmpty(boPhan)) query = query.Where(x => x.IdFormItNavigation.BoPhan.Contains(boPhan));
                if (!string.IsNullOrEmpty(loaiThietBi)) query = query.Where(x => x.LoaiThietBi.Contains(loaiThietBi));
                if (!string.IsNullOrEmpty(macTb)) query = query.Where(x => x.MacTb.Contains(macTb));
                if (!string.IsNullOrEmpty(maThietBi)) query = query.Where(x => x.MaThietBi.Contains(maThietBi));

                if (fromDate.HasValue) query = query.Where(x => x.IdFormItNavigation.TimeAdmin >= fromDate.Value);
                if (toDate.HasValue)
                {
                    var toDateEnd = toDate.Value.AddDays(1).AddTicks(-1);
                    query = query.Where(x => x.IdFormItNavigation.TimeAdmin <= toDateEnd);
                }

                // Áp dụng Sắp Xếp Động
                switch (sortColumn)
                {
                    case "IdForm": query = sortDir == "asc" ? query.OrderBy(x => x.IdFormItNavigation.IdForm) : query.OrderByDescending(x => x.IdFormItNavigation.IdForm); break;
                    case "TenNguoiNv": query = sortDir == "asc" ? query.OrderBy(x => x.IdFormItNavigation.TenNguoiNv) : query.OrderByDescending(x => x.IdFormItNavigation.TenNguoiNv); break;
                    case "BoPhan": query = sortDir == "asc" ? query.OrderBy(x => x.IdFormItNavigation.BoPhan) : query.OrderByDescending(x => x.IdFormItNavigation.BoPhan); break;
                    case "MaThietBi": query = sortDir == "asc" ? query.OrderBy(x => x.MaThietBi) : query.OrderByDescending(x => x.MaThietBi); break;
                    case "LoaiThietBi": query = sortDir == "asc" ? query.OrderBy(x => x.LoaiThietBi) : query.OrderByDescending(x => x.LoaiThietBi); break;
                    case "MacTb": query = sortDir == "asc" ? query.OrderBy(x => x.MacTb) : query.OrderByDescending(x => x.MacTb); break;
                    case "ThoiGianBatDau": query = sortDir == "asc" ? query.OrderBy(x => x.ThoiGianBatDau) : query.OrderByDescending(x => x.ThoiGianBatDau); break;
                    case "ThoiGianKetThuc": query = sortDir == "asc" ? query.OrderBy(x => x.ThoiGianKetThuc) : query.OrderByDescending(x => x.ThoiGianKetThuc); break;
                    case "TenAdmin": query = sortDir == "asc" ? query.OrderBy(x => x.IdFormItNavigation.TenAdmin) : query.OrderByDescending(x => x.IdFormItNavigation.TenAdmin); break;
                    case "TimeAdmin": query = sortDir == "asc" ? query.OrderBy(x => x.IdFormItNavigation.TimeAdmin) : query.OrderByDescending(x => x.IdFormItNavigation.TimeAdmin); break;
                    default: query = query.OrderByDescending(x => x.IdFormItNavigation.TimeAdmin); break;
                }

                var totalRecords = await query.CountAsync();
                var totalPages = (int)Math.Ceiling(totalRecords / (double)pageSize);

                var data = await query
                    .Skip((page - 1) * pageSize)
                    .Take(pageSize)
                    .Select(x => new
                    {
                        FormId = x.IdFormItNavigation.Id,
                        IdForm = x.IdFormItNavigation.IdForm,
                        TenNguoiNv = x.IdFormItNavigation.TenNguoiNv,
                        BoPhan = x.IdFormItNavigation.BoPhan,
                        LoaiThietBi = x.LoaiThietBi,
                        MaThietBi = x.MaThietBi,
                        MacTb = x.MacTb,
                        ThoiGianBatDau = x.ThoiGianBatDau,
                        ThoiGianKetThuc = x.ThoiGianKetThuc,
                        TenAdmin = x.IdFormItNavigation.TenAdmin,
                        TimeAdmin = x.IdFormItNavigation.TimeAdmin
                    })
                    .ToListAsync();

                return Json(new { data, totalRecords, totalPages, currentPage = page, pageSize });
            }
            catch (Exception ex)
            {
                return BadRequest(ex.Message);
            }
        }

        // 4. Action Xuất file Excel
        [HttpGet("/FormIT/ExportExcelMacWifi")]
        public async Task<IActionResult> ExportExcelMacWifi(
            string? idForm = null, string? tenNguoiNv = null, string? boPhan = null,
            string? loaiThietBi = null, string? macTb = null, string? maThietBi = null,
            DateTime? fromDate = null, DateTime? toDate = null,
            string sortColumn = "TimeAdmin", string sortDir = "desc") // Áp dụng sắp xếp cho xuất Excel
        {
            try
            {
                var query = _context.ItDangKiSuDungWifi3s
                    .Include(x => x.IdFormItNavigation)
                    .AsNoTracking()
                    .Where(x => !string.IsNullOrEmpty(x.MacTb) &&
                                x.IdFormItNavigation != null &&
                                x.IdFormItNavigation.IdAdmin != null);

                // Áp dụng bộ lọc
                if (!string.IsNullOrEmpty(idForm)) query = query.Where(x => x.IdFormItNavigation.IdForm.Contains(idForm));
                if (!string.IsNullOrEmpty(tenNguoiNv)) query = query.Where(x => x.IdFormItNavigation.TenNguoiNv.Contains(tenNguoiNv));
                if (!string.IsNullOrEmpty(boPhan)) query = query.Where(x => x.IdFormItNavigation.BoPhan.Contains(boPhan));
                if (!string.IsNullOrEmpty(loaiThietBi)) query = query.Where(x => x.LoaiThietBi.Contains(loaiThietBi));
                if (!string.IsNullOrEmpty(macTb)) query = query.Where(x => x.MacTb.Contains(macTb));
                if (!string.IsNullOrEmpty(maThietBi)) query = query.Where(x => x.MaThietBi.Contains(maThietBi));
                if (fromDate.HasValue) query = query.Where(x => x.IdFormItNavigation.TimeAdmin >= fromDate.Value);
                if (toDate.HasValue)
                {
                    var toDateEnd = toDate.Value.AddDays(1).AddTicks(-1);
                    query = query.Where(x => x.IdFormItNavigation.TimeAdmin <= toDateEnd);
                }

                // Áp dụng Sắp Xếp Động cho Excel
                switch (sortColumn)
                {
                    case "IdForm": query = sortDir == "asc" ? query.OrderBy(x => x.IdFormItNavigation.IdForm) : query.OrderByDescending(x => x.IdFormItNavigation.IdForm); break;
                    case "TenNguoiNv": query = sortDir == "asc" ? query.OrderBy(x => x.IdFormItNavigation.TenNguoiNv) : query.OrderByDescending(x => x.IdFormItNavigation.TenNguoiNv); break;
                    case "BoPhan": query = sortDir == "asc" ? query.OrderBy(x => x.IdFormItNavigation.BoPhan) : query.OrderByDescending(x => x.IdFormItNavigation.BoPhan); break;
                    case "MaThietBi": query = sortDir == "asc" ? query.OrderBy(x => x.MaThietBi) : query.OrderByDescending(x => x.MaThietBi); break;
                    case "LoaiThietBi": query = sortDir == "asc" ? query.OrderBy(x => x.LoaiThietBi) : query.OrderByDescending(x => x.LoaiThietBi); break;
                    case "MacTb": query = sortDir == "asc" ? query.OrderBy(x => x.MacTb) : query.OrderByDescending(x => x.MacTb); break;
                    case "ThoiGianBatDau": query = sortDir == "asc" ? query.OrderBy(x => x.ThoiGianBatDau) : query.OrderByDescending(x => x.ThoiGianBatDau); break;
                    case "ThoiGianKetThuc": query = sortDir == "asc" ? query.OrderBy(x => x.ThoiGianKetThuc) : query.OrderByDescending(x => x.ThoiGianKetThuc); break;
                    case "TenAdmin": query = sortDir == "asc" ? query.OrderBy(x => x.IdFormItNavigation.TenAdmin) : query.OrderByDescending(x => x.IdFormItNavigation.TenAdmin); break;
                    case "TimeAdmin": query = sortDir == "asc" ? query.OrderBy(x => x.IdFormItNavigation.TimeAdmin) : query.OrderByDescending(x => x.IdFormItNavigation.TimeAdmin); break;
                    default: query = query.OrderByDescending(x => x.IdFormItNavigation.TimeAdmin); break;
                }

                var data = await query
                    .Select(x => new
                    {
                        IdForm = x.IdFormItNavigation.IdForm,
                        TenNguoiNv = x.IdFormItNavigation.TenNguoiNv,
                        BoPhan = x.IdFormItNavigation.BoPhan,
                        MaThietBi = x.MaThietBi,
                        LoaiThietBi = x.LoaiThietBi,
                        MacTb = x.MacTb,
                        ThoiGianBatDau = x.ThoiGianBatDau,
                        ThoiGianKetThuc = x.ThoiGianKetThuc,
                        TenAdmin = x.IdFormItNavigation.TenAdmin,
                        TimeAdmin = x.IdFormItNavigation.TimeAdmin
                    })
                    .ToListAsync();

                var builder = new System.Text.StringBuilder();
                builder.AppendLine("Mã Form,Người Yêu Cầu,Bộ Phận,Mã Thiết Bị,Loại Thiết Bị,Địa Chỉ MAC,Bắt Đầu,Kết Thúc,IT Xử Lý,Thời Gian Xử Lý");

                foreach (var item in data)
                {
                    var form = $"\"{item.IdForm}\"";
                    var nq = $"\"{item.TenNguoiNv}\"";
                    var bp = $"\"{item.BoPhan}\"";
                    var matb = $"\"{item.MaThietBi}\"";
                    var loai = $"\"{item.LoaiThietBi}\"";
                    var mac = $"\"{item.MacTb}\"";
                    var bd = $"\"{item.ThoiGianBatDau?.ToString("dd/MM/yyyy HH:mm")}\"";
                    var kt = $"\"{item.ThoiGianKetThuc?.ToString("dd/MM/yyyy HH:mm")}\"";
                    var it = $"\"{item.TenAdmin}\"";
                    var time = $"\"{item.TimeAdmin?.ToString("dd/MM/yyyy HH:mm")}\"";

                    builder.AppendLine($"{form},{nq},{bp},{matb},{loai},{mac},{bd},{kt},{it},{time}");
                }

                var bytes = System.Text.Encoding.UTF8.GetPreamble().Concat(System.Text.Encoding.UTF8.GetBytes(builder.ToString())).ToArray();
                return File(bytes, "text/csv", $"ThongKeMacWifi_{DateTime.Now:yyyyMMdd_HHmm}.csv");
            }
            catch (Exception ex)
            {
                return BadRequest(ex.Message);
            }
        }

        #endregion

        #endregion


        #region LỊCH SỬ VÀ THÔNG BÁO FORM IT (Tối ưu truy vấn - Đầy đủ logic)

        // 1. TRẢ VỀ VIEW RỖNG
        [HttpGet("/FormIT/LichSuIT")]
        public IActionResult LogLichSuIT()
        {
            var userIdStr = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userIdStr)) return RedirectToAction("DangNhap", "DonXetDuyet");
            return View();
        }

        // 2. API TRẢ VỀ JSON - TỐI ƯU TRUY VẤN TẠI SERVER SQL
        [HttpGet("/FormIT/GetLichSuITData")]
        public async Task<IActionResult> GetLichSuITData()
        {
            var userIdStr = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userIdStr)) return Unauthorized();

            int userId = int.Parse(userIdStr);
            var userEmail = User.FindFirst(System.Security.Claims.ClaimTypes.Email)?.Value ?? "";
            var tenCongTy = User.FindFirst("TenCongTy")?.Value?.Trim() ?? "";

            // Bước 1: Xây dựng Query Filter (Chưa thực thi SQL)
            var query = _context.LichSuFormIts.AsNoTracking()
                .Where(l => l.IdFormItNavigation.TenCongTy == tenCongTy);

            // Phân quyền
            if (User.IsInRole("All")) { /* Xem toàn bộ */ }
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
                query = query.Where(l => l.IdFormItNavigation.IdNguoiTao == userId || l.IdFormItNavigation.ItCtNguoiHoTros.Any(ct => ct.IdItNguoiHoTroNavigation.MaNv == userEmail));
            }

            // Bước 2: Select trực tiếp (Projections) - SQL chỉ trả về đúng các cột này, cực nhanh
            var rawData = await query
                .OrderByDescending(l => l.Time)
                .Select(l => new
                {
                    l.IdFormIt,
                    l.Time,
                    l.TieuDe,
                    l.Mota,
                    f = l.IdFormItNavigation,
                    // Lấy supporter mới nhất
                    CurrentSupporterTen = l.IdFormItNavigation.ItCtNguoiHoTros
                        .OrderByDescending(x => x.Stt)
                        .Select(x => x.IdItNguoiHoTroNavigation.Ten)
                        .FirstOrDefault(),
                    // Lấy thông tin đánh giá
                    DanhGia = l.IdFormItNavigation.DanhGiaFormIts.Select(d => new { d.TimeNguoiDanhGia, d.MucDo }).FirstOrDefault()
                })
                .ToListAsync();

            // Bước 3: Map logic màu sắc và text (Xử lý trên RAM sau khi đã lọc dữ liệu tinh gọn)
            var result = rawData.Select(item =>
            {
                var f = item.f;
                bool isCanceled = f?.TrangThai == "DaHuy" || (f?.TenForm != null && f.TenForm.Contains("[ĐÃ HỦY]"));

                string statusText = "Chờ duyệt";
                string statusColor = "#f59e0b";

                if (isCanceled) { statusText = "Đã hủy đơn"; statusColor = "#ef4444"; }
                else if (f != null)
                {
                    if (f.TimeNguoiDuyet == null) { statusText = "Chờ phê duyệt"; statusColor = "#f97316"; }
                    else if (f.TimeAdmin == null) { statusText = "Đang xử lý"; statusColor = "#3b82f6"; }
                    else if (item.DanhGia == null) { statusText = "Chờ đánh giá"; statusColor = "#db2777"; }
                    else { statusText = "Hoàn tất 100%"; statusColor = "#10b981"; }
                }

                string actionColor = "#6366f1";
                var tieuDeUpper = item.TieuDe?.ToUpper() ?? "";
                if (tieuDeUpper.Contains("DUYỆT")) actionColor = "#10b981";
                else if (tieuDeUpper.Contains("HỦY") || tieuDeUpper.Contains("TỪ CHỐI")) actionColor = "#ef4444";
                else if (tieuDeUpper.Contains("HỖ TRỢ")) actionColor = "#f59e0b";

                string timeProcessing = "";
                string timeTotal = "";

                if ((statusText == "Hoàn tất 100%" || statusText == "Chờ đánh giá") && f?.TimeNguoiDuyet != null && f?.TimeAdmin != null)
                {
                    var spanProc = f.TimeAdmin.Value - f.TimeNguoiDuyet.Value;
                    timeProcessing = FormatTimeSpan(spanProc);

                    if (statusText == "Hoàn tất 100%" && item.DanhGia?.TimeNguoiDanhGia != null)
                    {
                        var spanTotal = item.DanhGia.TimeNguoiDanhGia.Value - f.TimeNguoiDuyet.Value;
                        timeTotal = FormatTimeSpan(spanTotal);
                    }
                }

                return new
                {
                    item.IdFormIt,
                    TimeHHmm = item.Time?.ToString("HH:mm") ?? "",
                    TimeDDMM = item.Time?.ToString("dd/MM/yyyy") ?? "",
                    TieuDe = item.TieuDe ?? "",
                    Mota = item.Mota ?? "",
                    IsCanceled = isCanceled,
                    StatusText = statusText,
                    StatusColor = statusColor,
                    ActionColor = actionColor,
                    TenForm = f?.TenForm ?? "N/A",
                    TenAdmin = f?.TenAdmin ?? "Chưa tiếp nhận",
                    KyThuatTen = item.CurrentSupporterTen ?? "",
                    TimeProcessing = timeProcessing,
                    TimeTotal = timeTotal,
                    HasDanhGia = item.DanhGia != null,
                    DanhGiaMucDo = item.DanhGia?.MucDo ?? 0
                };
            });

            return Json(result);
        }

        // Hàm bổ trợ Format thời gian để code sạch hơn
        private static string FormatTimeSpan(TimeSpan span)
        {
            if (span.TotalDays >= 1) return $"{(int)span.TotalDays}n {span.Hours}g";
            if (span.TotalHours >= 1) return $"{(int)span.TotalHours}g {span.Minutes}p";
            return $"{(int)span.TotalMinutes}p";
        }

        // 3. GET NOTIFICATIONS - GIỮ NGUYÊN VÀ TỐI ƯU SELECT
        [HttpGet("/FormIT/GetNotifications")]
        public async Task<IActionResult> GetNotifications(int skip = 0, int take = 20)
        {
            var userIdStr = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userIdStr)) return Unauthorized();

            int userId = int.Parse(userIdStr);
            var userEmail = User.FindFirst(System.Security.Claims.ClaimTypes.Email)?.Value ?? "";
            var tenCongTy = User.FindFirst("TenCongTy")?.Value?.Trim() ?? "";

            var query = _context.LichSuFormIts.AsNoTracking()
                .Where(l => l.IdFormItNavigation.TenCongTy == tenCongTy);

            if (User.IsInRole("All")) { }
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
                                  .ToListAsync();

            return Ok(new { dataList = logs, unreadCount });
        }

        #endregion

        #region THỐNG KÊ TỔNG QUAN KIỂM KÊ VÀ LẤY DANH SÁCH

        [HttpGet("/QLKiemKe/ThongKe")]
        public IActionResult ThongKeKiemKe()
        {
            return View("ThongKeKiemKe");
        }

        // 1. HÀM NÀY DÙNG ĐỂ VẼ BIỂU ĐỒ (Giữ nguyên logic cực chuẩn của bạn)
        [HttpGet("/QLKiemKe/GetDuLieuThongKe")]
        public IActionResult GetDuLieuThongKe()
        {
            try
            {
                // Lấy toàn bộ thiết bị đang hoạt động (Bỏ qua các thiết bị nằm trong thùng rác/có NgayXoa)
                var thietBis = _context.KkThietBis
                    .Include(x => x.IdcongTyNavigation)
                    .Include(x => x.IdboPhanNavigation)
                    .Include(x => x.IdTrangThaiNavigation)
                    .Where(x => x.NgayXoa == null && (x.IdTrangThaiNavigation == null || x.IdTrangThaiNavigation.TenTrangThai.ToLower() != "xóa"))
                    .ToList();

                // 1. Thống kê theo Công ty
                var thongKeCongTy = thietBis
                    .GroupBy(x => x.IdcongTyNavigation != null ? x.IdcongTyNavigation.TenCongTy : "Chưa gắn")
                    .Select(g => new { Label = g.Key, Value = g.Count() })
                    .OrderByDescending(x => x.Value)
                    .ToList();

                // 2. Thống kê theo Bộ phận
                var thongKeBoPhan = thietBis
                    .GroupBy(x => x.IdboPhanNavigation != null ? x.IdboPhanNavigation.TenBoPhan : "Chưa gắn")
                    .Select(g => new { Label = g.Key, Value = g.Count() })
                    .OrderByDescending(x => x.Value)
                    .ToList();

                // 3. Thống kê theo Trạng thái
                var thongKeTrangThai = thietBis
                    .GroupBy(x => x.IdTrangThaiNavigation != null ? x.IdTrangThaiNavigation.TenTrangThai : "Chưa rõ")
                    .Select(g => new { Label = g.Key, Value = g.Count() })
                    .OrderByDescending(x => x.Value)
                    .ToList();

                // 4. Thống kê theo Loại thiết bị
                var thongKeLoai = thietBis
                    .GroupBy(x => !string.IsNullOrEmpty(x.LoaiThietBi) ? x.LoaiThietBi : "Khác")
                    .Select(g => new { Label = g.Key, Value = g.Count() })
                    .OrderByDescending(x => x.Value)
                    .ToList();

                // 5. Thống kê Tiến độ kiểm kê (CẬP NHẬT LOGIC 4 THÁNG)
                var fourMonthsAgo = DateTime.Now.AddMonths(-4);

                // Đã check: Phải có thời gian check VÀ thời gian đó phải lớn hơn hoặc bằng 4 tháng trước
                int daCheck = thietBis.Count(x => x.ThoiGianCheck != null && x.ThoiGianCheck >= fourMonthsAgo);

                // Chưa check: Chưa check bao giờ HOẶC thời gian check nhỏ hơn 4 tháng trước (đã quá hạn)
                int chuaCheck = thietBis.Count(x => x.ThoiGianCheck == null || x.ThoiGianCheck < fourMonthsAgo);

                return Json(new
                {
                    success = true,
                    tongSo = thietBis.Count,
                    congTy = thongKeCongTy,
                    boPhan = thongKeBoPhan,
                    trangThai = thongKeTrangThai,
                    loaiThietBi = thongKeLoai,
                    tienDo = new
                    {
                        DaKiemKe = daCheck,
                        ChuaKiemKe = chuaCheck
                    }
                });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, msg = ex.Message });
            }
        }

        // 2. HÀM NÀY DÙNG ĐỂ ĐỔ DỮ LIỆU VÀO BẢNG & XUẤT EXCEL (Đã bổ sung trường TK)
        [HttpGet("/QLKiemKe/GetKkThietBiss")]
        public IActionResult GetKkThietBiss()
        {
            try
            {
                var data = _context.KkThietBis
                    .Include(x => x.IdcongTyNavigation)
                    .Include(x => x.IdboPhanNavigation)
                    .Include(x => x.IdTrangThaiNavigation)
                    .Include(x => x.IdNguoiDungNavigation) // Bắt buộc Include bảng User
                    .OrderByDescending(x => x.IdThietBi) // <--- DESCENDING ĐỂ MỚI NHẤT LÊN ĐẦU
                    .Select(x => new
                    {
                        idThietBi = x.IdThietBi,
                        tenThietBi = x.TenThietBi,
                        tenMayTinh = x.TenMayTinh,
                        loaiThietBi = x.LoaiThietBi,
                        tenDangNhap = x.TenDangNhap,

                        // Lấy thông tin từ bảng User
                        tenNguoiDung = x.IdNguoiDungNavigation != null ? x.IdNguoiDungNavigation.HoTen : null,
                        tk = x.IdNguoiDungNavigation != null ? x.IdNguoiDungNavigation.Tk : null,

                        tenCongTy = x.IdcongTyNavigation != null ? x.IdcongTyNavigation.TenCongTy : null,
                        tenBoPhan = x.IdboPhanNavigation != null ? x.IdboPhanNavigation.TenBoPhan : null,
                        tenTrangThai = x.IdTrangThaiNavigation != null ? x.IdTrangThaiNavigation.TenTrangThai : null,
                        ghiChu = x.GhiChu,
                        duongDanAnh = x.DuongDanAnh,
                        thoiGianCheck = x.ThoiGianCheck,
                        ngayCapNhat = x.NgayCapNhat,
                        ngayXoa = x.NgayXoa
                    })
                    .ToList();

                return Json(new { success = true, data = data });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, msg = ex.Message });
            }
        }

        // 3. HÀM NÀY DÙNG ĐỂ LẤY LỊCH SỬ THAO TÁC CỦA THIẾT BỊ THEO ID
        [HttpGet("/QLKiemKe/GetLichSuThietBi")]
        public IActionResult GetLichSuThietBi(int idThietBi)
        {
            try
            {
                // Giả định DoiTuong là "Thiết bị" hoặc bạn quản lý thẳng bằng IdDoiTuong
                var lichSuData = _context.KkLichSuThaoTacs
                    .Where(x => x.IdDoiTuong == idThietBi)
                    .OrderByDescending(x => x.ThoiGian)
                    .Select(x => new
                    {
                        x.IdLichSu,
                        x.HanhDong,
                        x.DoiTuong,
                        x.ChiTiet,
                        x.ThoiGian,
                        x.NguoiThaoTac
                    })
                    .ToList();

                return Json(new { success = true, data = lichSuData });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, msg = ex.Message });
            }
        }

        #endregion


        // HÀM HỖ TRỢ DÙNG CHUNG CHO CẢ 2 REGION
        // =================================================================================
        private void GhiLichSu(string hanhDong, string doiTuong, int idDoiTuong, string chiTiet)
        {
            try
            {
                var userName = User.Identity?.Name ?? "Hệ thống";
                var history = new KkLichSuThaoTac
                {
                    HanhDong = hanhDong,
                    DoiTuong = doiTuong,
                    IdDoiTuong = idDoiTuong,
                    ChiTiet = chiTiet,
                    ThoiGian = DateTime.Now,
                    NguoiThaoTac = userName
                };
                _context.KkLichSuThaoTacs.Add(history);
                _context.SaveChanges();
            }
            catch { /* Bỏ qua lỗi ghi log để không làm gián đoạn luồng chính */ }
        }


        #region 1. QL KIỂM KÊ: DANH MỤC & CẤU HÌNH (Công Ty, Bộ Phận, Loại Thiết Bị, Trạng Thái, Lịch Sử)

        // View dành cho Danh mục
        [HttpGet("/QLKiemKe")]
        public IActionResult IndexKiemKe()
        {
            return View("IndexKiemKe");
        }

        // ==================== LỊCH SỬ THAO TÁC ====================
        [HttpGet("/QLKiemKe/GetLichSu")]
        public IActionResult GetLichSu()
        {
            try
            {
                var data = _context.KkLichSuThaoTacs
                    .OrderByDescending(x => x.ThoiGian)
                    .Take(500) // Lấy 500 lịch sử gần nhất để tối ưu hiệu suất
                    .ToList();
                return Json(new { success = true, data = data });
            }
            catch (Exception ex) { return Json(new { success = false, msg = ex.Message }); }
        }

        // ==================== TRẠNG THÁI ====================
        [HttpGet("/QLKiemKe/GetKkTrangThais")]
        public IActionResult GetKkTrangThais()
        {
            try
            {
                var data = _context.KkTrangThais.OrderByDescending(x => x.IdTrangThai).ToList();
                return Json(new { success = true, data = data });
            }
            catch (Exception ex) { return Json(new { success = false, msg = ex.Message }); }
        }

        [HttpPost("/QLKiemKe/SaveKkTrangThai")]
        public IActionResult SaveKkTrangThai(KkTrangThai model)
        {
            try
            {
                string action = model.IdTrangThai == 0 ? "Thêm mới" : "Cập nhật";
                int objId = model.IdTrangThai;

                if (model.IdTrangThai == 0)
                {
                    _context.KkTrangThais.Add(model);
                }
                else
                {
                    var existing = _context.KkTrangThais.Find(model.IdTrangThai);
                    if (existing != null)
                    {
                        existing.TenTrangThai = model.TenTrangThai;
                        existing.MoTa = model.MoTa;
                    }
                }
                _context.SaveChanges();

                if (objId == 0) objId = model.IdTrangThai;
                GhiLichSu(action, "Trạng Thái", objId, $"Tên trạng thái: {model.TenTrangThai}");

                return Json(new { success = true, msg = "Lưu trạng thái thành công!" });
            }
            catch (Exception ex) { return Json(new { success = false, msg = ex.Message }); }
        }

        [HttpPost("/QLKiemKe/DeleteKkTrangThai")]
        public IActionResult DeleteKkTrangThai(int id)
        {
            try
            {
                var item = _context.KkTrangThais.Find(id);
                if (item != null)
                {
                    if (item.TenTrangThai.ToLower() == "xóa")
                        return Json(new { success = false, msg = "Không thể xóa trạng thái mặc định của hệ thống!" });

                    string tenTT = item.TenTrangThai;
                    _context.KkTrangThais.Remove(item);
                    _context.SaveChanges();

                    GhiLichSu("Xóa", "Trạng Thái", id, $"Đã xóa trạng thái: {tenTT}");
                }
                return Json(new { success = true, msg = "Đã xóa trạng thái!" });
            }
            catch (Exception) { return Json(new { success = false, msg = "Không thể xóa vì trạng thái này đang được gán cho thiết bị." }); }
        }

        // ==================== CÔNG TY ====================
        [HttpGet("/QLKiemKe/GetKkCongTys")]
        public IActionResult GetKkCongTys()
        {
            try
            {
                var data = _context.KkCongTies.OrderByDescending(x => x.IdcongTy).ToList();
                return Json(new { success = true, data = data });
            }
            catch (Exception ex) { return Json(new { success = false, msg = ex.Message }); }
        }

        [HttpPost("/QLKiemKe/SaveKkCongTy")]
        public IActionResult SaveKkCongTy(KkCongTy model)
        {
            try
            {
                string action = model.IdcongTy == 0 ? "Thêm mới" : "Cập nhật";
                int objId = model.IdcongTy;

                if (model.IdcongTy == 0)
                {
                    model.NgayTao = DateTime.Now;
                    model.TrangThai = true;
                    _context.KkCongTies.Add(model);
                }
                else
                {
                    var existing = _context.KkCongTies.Find(model.IdcongTy);
                    if (existing != null)
                    {
                        existing.TenCongTy = model.TenCongTy;
                        existing.GhiChu = model.GhiChu;
                        existing.TrangThai = model.TrangThai;
                    }
                }

                _context.SaveChanges();

                if (objId == 0) objId = model.IdcongTy;
                GhiLichSu(action, "Công Ty", objId, $"Tên công ty: {model.TenCongTy}");

                return Json(new { success = true, msg = "Lưu công ty thành công!" });
            }
            catch (Exception ex) { return Json(new { success = false, msg = ex.Message }); }
        }

        [HttpPost("/QLKiemKe/DeleteKkCongTy")]
        public IActionResult DeleteKkCongTy(int id)
        {
            try
            {
                var item = _context.KkCongTies.Find(id);
                if (item != null)
                {
                    string tenCT = item.TenCongTy;
                    _context.KkCongTies.Remove(item);
                    _context.SaveChanges();

                    GhiLichSu("Xóa", "Công Ty", id, $"Đã xóa công ty: {tenCT}");
                }
                return Json(new { success = true, msg = "Đã xóa công ty!" });
            }
            catch (Exception) { return Json(new { success = false, msg = "Không thể xóa vì công ty này đang được sử dụng." }); }
        }

        // ==================== BỘ PHẬN ====================
        [HttpGet("/QLKiemKe/GetKkBoPhans")]
        public IActionResult GetKkBoPhans()
        {
            try
            {
                var data = _context.KkBoPhans
                    .Select(x => new
                    {
                        x.IdboPhan,
                        x.TenBoPhan,
                        x.IdcongTy,
                        TenCongTy = x.IdcongTyNavigation != null ? x.IdcongTyNavigation.TenCongTy : "",
                        x.GhiChu,
                        x.NgayTao,
                        x.TrangThai
                    })
                    .OrderByDescending(x => x.IdboPhan).ToList();
                return Json(new { success = true, data = data });
            }
            catch (Exception ex) { return Json(new { success = false, msg = ex.Message }); }
        }

        [HttpPost("/QLKiemKe/SaveKkBoPhan")]
        public IActionResult SaveKkBoPhan(KkBoPhan model)
        {
            try
            {
                string action = model.IdboPhan == 0 ? "Thêm mới" : "Cập nhật";
                int objId = model.IdboPhan;

                if (model.IdboPhan == 0)
                {
                    model.NgayTao = DateTime.Now;
                    model.TrangThai = true;
                    _context.KkBoPhans.Add(model);
                }
                else
                {
                    var existing = _context.KkBoPhans.Find(model.IdboPhan);
                    if (existing != null)
                    {
                        existing.TenBoPhan = model.TenBoPhan;
                        existing.IdcongTy = model.IdcongTy;
                        existing.GhiChu = model.GhiChu;
                        existing.TrangThai = model.TrangThai;
                    }
                }

                _context.SaveChanges();

                if (objId == 0) objId = model.IdboPhan;
                GhiLichSu(action, "Bộ Phận", objId, $"Tên bộ phận: {model.TenBoPhan}");

                return Json(new { success = true, msg = "Lưu bộ phận thành công!" });
            }
            catch (Exception ex) { return Json(new { success = false, msg = ex.Message }); }
        }

        [HttpPost("/QLKiemKe/DeleteKkBoPhan")]
        public IActionResult DeleteKkBoPhan(int id)
        {
            try
            {
                var item = _context.KkBoPhans.Find(id);
                if (item != null)
                {
                    string tenBP = item.TenBoPhan;
                    _context.KkBoPhans.Remove(item);
                    _context.SaveChanges();

                    GhiLichSu("Xóa", "Bộ Phận", id, $"Đã xóa bộ phận: {tenBP}");
                }
                return Json(new { success = true, msg = "Đã xóa bộ phận!" });
            }
            catch (Exception) { return Json(new { success = false, msg = "Không thể xóa vì bộ phận này đang được sử dụng." }); }
        }

        // ==================== LOẠI THIẾT BỊ ====================
        [HttpGet("/QLKiemKe/GetKkLoaiThietBis")]
        public IActionResult GetKkLoaiThietBis()
        {
            try
            {
                var data = _context.KkLoaiThietBis.OrderByDescending(x => x.IdLoai).ToList();
                return Json(new { success = true, data = data });
            }
            catch (Exception ex) { return Json(new { success = false, msg = ex.Message }); }
        }

        [HttpPost("/QLKiemKe/SaveKkLoaiThietBi")]
        public IActionResult SaveKkLoaiThietBi(KkLoaiThietBi model)
        {
            try
            {
                string action = model.IdLoai == 0 ? "Thêm mới" : "Cập nhật";
                int objId = model.IdLoai;

                if (model.IdLoai == 0)
                {
                    model.NgayTao = DateTime.Now;
                    _context.KkLoaiThietBis.Add(model);
                }
                else
                {
                    var existing = _context.KkLoaiThietBis.Find(model.IdLoai);
                    if (existing != null)
                    {
                        existing.TenLoai = model.TenLoai;
                        existing.GhiChu = model.GhiChu;
                    }
                }

                _context.SaveChanges();

                if (objId == 0) objId = model.IdLoai;
                GhiLichSu(action, "Loại Thiết Bị", objId, $"Tên loại: {model.TenLoai}");

                return Json(new { success = true, msg = "Lưu loại thiết bị thành công!" });
            }
            catch (Exception ex) { return Json(new { success = false, msg = ex.Message }); }
        }

        [HttpPost("/QLKiemKe/DeleteKkLoaiThietBi")]
        public IActionResult DeleteKkLoaiThietBi(int id)
        {
            try
            {
                var item = _context.KkLoaiThietBis.Find(id);
                if (item != null)
                {
                    string tenLoai = item.TenLoai;
                    _context.KkLoaiThietBis.Remove(item);
                    _context.SaveChanges();

                    GhiLichSu("Xóa", "Loại Thiết Bị", id, $"Đã xóa loại: {tenLoai}");
                }
                return Json(new { success = true, msg = "Đã xóa loại thiết bị!" });
            }
            catch (Exception) { return Json(new { success = false, msg = "Không thể xóa vì loại thiết bị này đang được gán cho thiết bị." }); }
        }

        #endregion


        #region 2. QL KIỂM KÊ: THIẾT BỊ (Xử lý Thiết Bị & Hình Ảnh)

        // View chuyên biệt quản lý Thiết Bị
        [HttpGet("/QLKiemKe/ThietBi")]
        public IActionResult IndexThietBi()
        {
            // Truyền User list sang View để đổ vào Dropdown Người Dùng
            ViewBag.Users = _context.Users.OrderBy(u => u.HoTen).ToList();
            return View("IndexThietBi"); // Bạn cần tạo file IndexThietBi.cshtml trong thư mục Views
        }

        [HttpGet("/QLKiemKe/GetKkThietBis")]
        public IActionResult GetKkThietBis()
        {
            try
            {
                // DỌN DẸP THIẾT BỊ ĐÃ XÓA QUÁ 1 THÁNG (Dựa vào NgayXoa)
                var oneMonthAgo = DateTime.Now.AddMonths(-1);
                var itemsToDelete = _context.KkThietBis
                    .Include(x => x.IdTrangThaiNavigation)
                    .Where(x => x.IdTrangThaiNavigation != null
                             && x.IdTrangThaiNavigation.TenTrangThai.ToLower() == "xóa"
                             && x.NgayXoa.HasValue
                             && x.NgayXoa.Value <= oneMonthAgo)
                    .ToList();

                if (itemsToDelete.Any())
                {
                    foreach (var del in itemsToDelete)
                    {
                        GhiLichSu("Xóa vĩnh viễn", "Thiết Bị", del.IdThietBi, $"Hệ thống tự động hủy thiết bị (Hostname: {del.TenMayTinh}) do nằm trong thùng rác quá 1 tháng.");
                    }
                    _context.KkThietBis.RemoveRange(itemsToDelete);
                    _context.SaveChanges();
                }

                // Trả về kèm NgayXoa, LyDoXoa, DuongDanAnh và ThoiGianCheck
                var data = _context.KkThietBis
                    .Select(x => new
                    {
                        x.IdThietBi,
                        x.TenThietBi,
                        x.TenMayTinh,
                        x.TenDangNhap,
                        x.LoaiThietBi,
                        x.GhiChu,
                        x.IdNguoiDung,
                        TenNguoiDung = x.IdNguoiDungNavigation != null ? x.IdNguoiDungNavigation.HoTen : "",

                        // ---> CẬP NHẬT Ở ĐÂY: Thêm trường lấy Mã TK
                        Tk = x.IdNguoiDungNavigation != null ? x.IdNguoiDungNavigation.Tk : "",

                        x.IdcongTy,
                        TenCongTy = x.IdcongTyNavigation != null ? x.IdcongTyNavigation.TenCongTy : "",
                        x.IdboPhan,
                        TenBoPhan = x.IdboPhanNavigation != null ? x.IdboPhanNavigation.TenBoPhan : "",
                        x.IdTrangThai,
                        TenTrangThai = x.IdTrangThaiNavigation != null ? x.IdTrangThaiNavigation.TenTrangThai : "",
                        x.NgayTao,
                        x.NgayCapNhat,
                        NgayXoa = x.NgayXoa,
                        LyDoXoa = x.LyDoXoa,
                        x.DuongDanAnh, // Trả về đường dẫn ảnh
                        x.ThoiGianCheck // Trả về thời gian check
                    })
                    .OrderByDescending(x => x.IdThietBi).ToList();
                return Json(new { success = true, data = data });
            }
            catch (Exception ex) { return Json(new { success = false, msg = ex.Message }); }
        }

        // HÀM LƯU THIẾT BỊ (BỔ SUNG UPLOAD ẢNH BẤT ĐỒNG BỘ VÀ LÀM MỚI TIME CHECK)
        [HttpPost("/QLKiemKe/SaveKkThietBi")]
        public async Task<IActionResult> SaveKkThietBi([FromForm] KkThietBi model, IFormFile? AnhThietBi)
        {
            try
            {
                bool isDuplicate = false;
                if (!string.IsNullOrWhiteSpace(model.TenMayTinh))
                {
                    if (model.IdThietBi == 0)
                        isDuplicate = _context.KkThietBis.Any(x => x.TenMayTinh.ToLower().Trim() == model.TenMayTinh.ToLower().Trim());
                    else
                        isDuplicate = _context.KkThietBis.Any(x => x.TenMayTinh.ToLower().Trim() == model.TenMayTinh.ToLower().Trim() && x.IdThietBi != model.IdThietBi);
                }

                if (isDuplicate)
                    return Json(new { success = false, msg = $"Tên máy tính (Hostname) '{model.TenMayTinh}' đã tồn tại trong hệ thống. Vui lòng kiểm tra lại!" });

                if (string.IsNullOrWhiteSpace(model.TenThietBi)) model.TenThietBi = "";

                string action = model.IdThietBi == 0 ? "Thêm mới" : "Cập nhật";
                int objId = model.IdThietBi;

                var statusCheck = _context.KkTrangThais.Find(model.IdTrangThai);
                string statusName = statusCheck != null ? statusCheck.TenTrangThai : "Chưa rõ";

                // --- XỬ LÝ UPLOAD VÀ LƯU ẢNH LÊN THƯ MỤC MẠNG ---
                string newImageFileName = null;
                if (AnhThietBi != null && AnhThietBi.Length > 0)
                {
                    string networkPath = @"\\10.0.60.30\BPVN-Fileserver\Public\IT-Information Technology Dept\5.E-Form\AnhKiemKe";

                    // Tạo folder nếu chưa tồn tại
                    if (!Directory.Exists(networkPath))
                    {
                        Directory.CreateDirectory(networkPath);
                    }

                    string imgExtension = Path.GetExtension(AnhThietBi.FileName);
                    if (string.IsNullOrEmpty(imgExtension)) imgExtension = ".jpg";

                    string timeStamp = DateTime.Now.ToString("yyyyMMddHHmmss");
                    // Xóa ký tự đặc biệt khỏi Tên thiết bị để làm tên file
                    string safeName = string.Join("_", model.TenThietBi.Split(Path.GetInvalidFileNameChars()));
                    if (string.IsNullOrEmpty(safeName)) safeName = "TB";

                    newImageFileName = $"AnhTB_{safeName}_{timeStamp}{imgExtension}";
                    string imgFullPath = Path.Combine(networkPath, newImageFileName);

                    // Copy file vào thư mục mạng
                    using (var imgStream = new FileStream(imgFullPath, FileMode.Create))
                    {
                        await AnhThietBi.CopyToAsync(imgStream);
                    }
                }

                if (model.IdThietBi == 0)
                {
                    model.NgayTao = DateTime.Now;
                    model.NgayCapNhat = DateTime.Now;
                    model.ThoiGianCheck = DateTime.Now; // Làm mới thời gian check khi thêm mới
                    model.DuongDanAnh = newImageFileName; // Gán tên ảnh mới tạo
                    _context.KkThietBis.Add(model);
                }
                else
                {
                    var existing = _context.KkThietBis.Find(model.IdThietBi);
                    if (existing != null)
                    {
                        existing.TenThietBi = model.TenThietBi;
                        existing.TenMayTinh = model.TenMayTinh;
                        existing.TenDangNhap = model.TenDangNhap;
                        existing.LoaiThietBi = model.LoaiThietBi;
                        existing.GhiChu = model.GhiChu;
                        existing.IdNguoiDung = model.IdNguoiDung;
                        existing.IdcongTy = model.IdcongTy;
                        existing.IdboPhan = model.IdboPhan;
                        existing.IdTrangThai = model.IdTrangThai;
                        existing.NgayCapNhat = DateTime.Now;
                        existing.ThoiGianCheck = DateTime.Now; // Làm mới thời gian check khi cập nhật

                        // Chỉ cập nhật DuongDanAnh nếu có ảnh mới upload lên
                        if (newImageFileName != null)
                        {
                            existing.DuongDanAnh = newImageFileName;
                        }

                        // Nếu cập nhật thoát khỏi trạng thái Xóa thì clear ngày xóa & lý do xóa
                        if (statusName.ToLower() != "xóa")
                        {
                            existing.NgayXoa = null;
                            existing.LyDoXoa = null;
                        }
                    }
                }

                // Đã chuyển thành hàm Async nên dùng SaveChangesAsync
                await _context.SaveChangesAsync();

                if (objId == 0) objId = model.IdThietBi;

                string tenNguoiDung = "Chưa cấp phát";
                if (model.IdNguoiDung.HasValue)
                {
                    var user = _context.Users.Find(model.IdNguoiDung.Value);
                    if (user != null) tenNguoiDung = user.HoTen;
                }

                string tenBoPhan = "Chưa gắn";
                if (model.IdboPhan.HasValue)
                {
                    var bp = _context.KkBoPhans.Find(model.IdboPhan.Value);
                    if (bp != null) tenBoPhan = bp.TenBoPhan;
                }

                string chiTietLog = $"Máy tính: {model.TenMayTinh} | Trạng thái: {statusName} | Account: {model.TenDangNhap} | N.Dùng: {tenNguoiDung} | B.Phận: {tenBoPhan} | Loại: {model.LoaiThietBi}";
                GhiLichSu(action, "Thiết Bị", objId, chiTietLog);

                return Json(new { success = true, msg = "Lưu thiết bị thành công!" });
            }
            catch (Exception ex) { return Json(new { success = false, msg = ex.Message }); }
        }

        // HÀM XUẤT ẢNH TỪ FILE SERVER RA TRÌNH DUYỆT
        [HttpGet("/QLKiemKe/GetImage")]
        public IActionResult GetImage(string fileName)
        {
            if (string.IsNullOrEmpty(fileName)) return NotFound();

            string networkPath = @"\\10.0.60.30\BPVN-Fileserver\Public\IT-Information Technology Dept\5.E-Form\AnhKiemKe";
            string filePath = Path.Combine(networkPath, fileName);

            if (!System.IO.File.Exists(filePath)) return NotFound();

            var ext = Path.GetExtension(filePath).ToLowerInvariant();
            string mimeType = "image/jpeg"; // Mặc định

            if (ext == ".png") mimeType = "image/png";
            else if (ext == ".gif") mimeType = "image/gif";
            else if (ext == ".webp") mimeType = "image/webp";

            return PhysicalFile(filePath, mimeType);
        }

        // HÀM XÁC NHẬN CHECK THIẾT BỊ
        [HttpPost("/QLKiemKe/XacNhanCheck")]
        public async Task<IActionResult> XacNhanCheck(int idThietBi)
        {
            try
            {
                var thietBi = await _context.KkThietBis.FindAsync(idThietBi);
                if (thietBi == null) return Json(new { success = false, msg = "Không tìm thấy thiết bị trong hệ thống." });

                thietBi.ThoiGianCheck = DateTime.Now;
                _context.Update(thietBi);
                await _context.SaveChangesAsync();

                return Json(new { success = true, msg = "Cập nhật thời gian kiểm tra thành công." });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, msg = ex.Message });
            }
        }

        // NÚT XÓA: Chuyển vào thùng rác
        [HttpPost("/QLKiemKe/DeleteKkThietBi")]
        public IActionResult DeleteKkThietBi(int id, string lyDo)
        {
            try
            {
                var item = _context.KkThietBis.Find(id);
                if (item != null)
                {
                    var statusXoa = _context.KkTrangThais.FirstOrDefault(x => x.TenTrangThai.ToLower() == "xóa");
                    if (statusXoa == null)
                    {
                        statusXoa = new KkTrangThai { TenTrangThai = "Xóa", MoTa = "Đã xóa (Chờ hủy 30 ngày)" };
                        _context.KkTrangThais.Add(statusXoa);
                        _context.SaveChanges();
                    }

                    item.IdTrangThai = statusXoa.IdTrangThai;
                    item.NgayCapNhat = DateTime.Now;

                    // Lưu dữ liệu vào 2 cột riêng biệt
                    item.NgayXoa = DateTime.Now;
                    item.LyDoXoa = lyDo;

                    _context.SaveChanges();

                    string chiTietLog = $"Chuyển vào thùng rác. Tên máy tính: {item.TenMayTinh} | Lý do: {lyDo} | Chờ hủy sau 1 tháng.";
                    GhiLichSu("Xóa (Tạm)", "Thiết Bị", id, chiTietLog);
                }
                return Json(new { success = true, msg = "Đã chuyển thiết bị vào Thùng Rác!" });
            }
            catch (Exception ex) { return Json(new { success = false, msg = ex.Message }); }
        }

        // NÚT KHÔI PHỤC: Lấy lại từ thùng rác
        [HttpPost("/QLKiemKe/RestoreKkThietBi")]
        public IActionResult RestoreKkThietBi(int id)
        {
            try
            {
                var item = _context.KkThietBis.Find(id);
                if (item != null)
                {
                    // Lấy trạng thái mặc định (ví dụ: Đang hoạt động) hoặc bỏ trống
                    var statusHoatDong = _context.KkTrangThais.FirstOrDefault(x => x.TenTrangThai.ToLower() == "đang hoạt động");

                    item.IdTrangThai = statusHoatDong?.IdTrangThai;
                    item.NgayXoa = null;
                    item.LyDoXoa = null;
                    item.NgayCapNhat = DateTime.Now;

                    _context.SaveChanges();

                    GhiLichSu("Khôi phục", "Thiết Bị", id, $"Đã khôi phục thiết bị: {item.TenMayTinh}");
                }
                return Json(new { success = true, msg = "Khôi phục thiết bị thành công!" });
            }
            catch (Exception ex) { return Json(new { success = false, msg = ex.Message }); }
        }

        // NÚT XÓA VĨNH VIỄN: Hủy hoàn toàn khỏi CSDL
        [HttpPost("/QLKiemKe/HardDeleteKkThietBi")]
        public IActionResult HardDeleteKkThietBi(int id)
        {
            try
            {
                var item = _context.KkThietBis.Find(id);
                if (item != null)
                {
                    string tenMay = item.TenMayTinh ?? item.TenThietBi ?? "Không rõ";
                    _context.KkThietBis.Remove(item);
                    _context.SaveChanges();

                    GhiLichSu("Xóa vĩnh viễn", "Thiết Bị", id, $"Đã xóa vĩnh viễn thiết bị (Hostname/Mã: {tenMay}) khỏi thùng rác.");
                }
                return Json(new { success = true, msg = "Đã xóa vĩnh viễn thiết bị khỏi hệ thống!" });
            }
            catch (Exception ex) { return Json(new { success = false, msg = "Lỗi: Không thể xóa vì ràng buộc dữ liệu hoặc lỗi hệ thống. Chi tiết: " + ex.Message }); }
        }

        #endregion
        
    }
}
