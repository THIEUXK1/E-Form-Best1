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
        new Claim("PhongBan", user.PhongBan ?? ""),
        // LƯU THÊM TÊN CÔNG TY VÀO CLAIM
        new Claim("TenCongTy", user.TenCongTy ?? "")
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

            // 5. Lưu email vào Cookie thường để hiển thị lần sau (nếu cần)
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
            await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);

            // Xóa sạch Session (nếu có dùng các biến khác)
            HttpContext.Session.Clear();

            return Redirect("/DonXetDuyet/DangNhap");
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
                    CongViecs = x.CongViecs.Where(cv => cv.Ten == "Đăng kí mail").ToList()
                })
                .Where(x => x.CongViecs.Any())
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
            // Kiểm tra đăng nhập
            if (User == null || !User.Identity.IsAuthenticated)
                return Redirect("/DonXetDuyet/DangNhap");

            // Lấy thông tin User
            var userIdStr = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
            int.TryParse(userIdStr, out int userId);

            var userName = User.Identity.Name ?? "";
            var phongBan = User.FindFirst("PhongBan")?.Value ?? "";
            var viTri = User.FindFirst("UserRole")?.Value ?? "";
            var userEmail = User.FindFirst(System.Security.Claims.ClaimTypes.Email)?.Value ?? "";

            // MỚI: Lấy Tên Công Ty từ Claim khi lưu
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

                    // MỚI: Gán thông tin Công ty vào Entity
                    form.TenCongTy = tenCongTy;

                    form.TrangThai = "ChoDuyet";
                    form.IdForm = "IT_MAIL_1";
                    form.TenForm = "Đơn đăng ký/sửa đổi Mail";
                    form.Danhmuc = "Đăng kí mail";

                    _context.FormIts.Add(form);
                    await _context.SaveChangesAsync();

                    // --- BƯỚC 2: FILE ĐÍNH KÈM ---
                    var uploadFile = Request.Form.Files["UploadFile"];
                    if (uploadFile != null && uploadFile.Length > 0)
                    {
                        string networkPath = @"\\10.0.60.30\BPVN-Fileserver\Public\IT-Information Technology Dept\5.E-Form\DonIT";
                        if (!Directory.Exists(networkPath)) Directory.CreateDirectory(networkPath);

                        string extension = Path.GetExtension(uploadFile.FileName);
                        string safeName = RemoveSign4VietnameseString(userName).Replace(" ", "");
                        string timeStamp = DateTime.Now.ToString("ddMMyy_HHmmss");
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
                    List<int> selectedItNguoiHoTroIds = new List<int>();

                    if (SelectedCongViecIds != null && SelectedCongViecIds.Length > 0)
                    {
                        selectedItNguoiHoTroIds = await _context.CongViecs
                            .Where(cv => SelectedCongViecIds.Contains(cv.Id) && cv.IdItNguoiHoTro != null)
                            .Select(cv => cv.IdItNguoiHoTro!.Value)
                            .Distinct()
                            .ToListAsync();
                    }

                    if (!selectedItNguoiHoTroIds.Any() && form.Danhmuc == "Đăng kí mail")
                    {
                        selectedItNguoiHoTroIds = await _context.CongViecs
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

                    // Load lại list hỗ trợ khi xảy ra lỗi để trả về View
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
                            CongViecs = x.CongViecs.Where(cv => cv.Ten == "Đăng kí mail").ToList()
                        })
                        .Where(x => x.CongViecs.Any())
                        .ToList();

                    ModelState.AddModelError("", "Lỗi trong quá trình lưu: " + ex.Message);
                    return View(form);
                }
            }
        }
        #endregion

        #region IT Order - Sửa chữa/Yêu cầu thiết bị (Form IT) - CẬP NHẬT CÔNG TY & LỊCH SỬ

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

            ViewBag.CongViecList = await _context.CongViecs
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
            var userEmail = User.FindFirst(System.Security.Claims.ClaimTypes.Email)?.Value ?? "";
            var phongBan = User.FindFirst("PhongBan")?.Value ?? "";
            var viTri = User.FindFirst(System.Security.Claims.ClaimTypes.Role)?.Value
                        ?? User.FindFirst("UserRole")?.Value ?? "";
            var tenCongTy = User.FindFirst("TenCongTy")?.Value ?? ""; // Lấy lại claim công ty khi POST

            using (var transaction = await _context.Database.BeginTransactionAsync())
            {
                try
                {
                    // --- 1. TRUY VẤN CÔNG VIỆC & NGƯỜI HỖ TRỢ ---
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
                    form.TenCongTy = tenCongTy; // Lưu tên công ty vào database
                    form.TrangThai = "ChoDuyet";
                    form.IdForm = "IT_ORDER_2";
                    form.TenForm = "Yêu cầu sửa chữa/Hỗ trợ kỹ thuật";

                    if (congViec != null) form.Danhmuc = congViec.Ten;

                    _context.FormIts.Add(form);
                    await _context.SaveChangesAsync();

                    // --- 3. XỬ LÝ CHI TIẾT LỖI (ItOrderIt2) ---
                    if (itOrder != null)
                    {
                        itOrder.IdFormIt = form.Id;
                        if (string.IsNullOrEmpty(itOrder.Ten) && congViec != null) itOrder.Ten = congViec.Ten;
                        itOrder.Anh = await GetFileBytesAsync2("Anh");
                        _context.ItOrderIt2s.Add(itOrder);
                    }

                    // --- 4. XỬ LÝ FILE ĐÍNH KÈM (Network Path) ---
                    var uploadFile = Request.Form.Files["UploadFile"];
                    string fileLog = "Không có tệp đính kèm";

                    if (uploadFile != null && uploadFile.Length > 0)
                    {
                        string networkPath = @"\\10.0.60.30\BPVN-Fileserver\Public\IT-Information Technology Dept\5.E-Form\DonIT";
                        if (!Directory.Exists(networkPath)) Directory.CreateDirectory(networkPath);

                        string extension = Path.GetExtension(uploadFile.FileName);
                        string safeName = RemoveSign4VietnameseString(userName).Replace(" ", "");
                        string fileName = $"DonOrder_ID{form.Id}_{safeName}_{DateTime.Now:ddMMyy_HHmmss}{extension}";
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
                    var lichSu = new LichSu
                    {
                        IdFormIt = form.Id,
                        TieuDe = "Khởi tạo yêu cầu",
                        Mota = $"[Cty: {tenCongTy}] Người tạo: {userName}. {supporterLog}. {fileLog}.",
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
                    ViewBag.CongViecList = await _context.CongViecs.OrderBy(x => x.Ten).ToListAsync();
                    ModelState.AddModelError("", "Lỗi: " + ex.Message);
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

            // --- LẤY THÊM THÔNG TIN TÊN CÔNG TY TỪ CLAIM ---
            var tenCongTy = User.FindFirst("TenCongTy")?.Value ?? "";

            // --- CẬP NHẬT: LẤY DANH SÁCH CÔNG VIỆC THAY VÌ NGƯỜI HỖ TRỢ ---
            ViewBag.CongViecList = await _context.CongViecs
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

            // --- LẤY TÊN CÔNG TY ĐỂ LƯU XUỐNG DB ---
            var tenCongTy = User.FindFirst("TenCongTy")?.Value ?? "";

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
                    form.TenCongTy = tenCongTy; // Lưu tên công ty vào database
                    form.TrangThai = "ChoDuyet";
                    form.IdForm = "IT_WIFI_3";
                    form.TenForm = "Đăng ký sử dụng Wifi";

                    if (congViec != null)
                    {
                        form.Danhmuc = congViec.Ten;
                    }

                    _context.FormIts.Add(form);
                    await _context.SaveChangesAsync();

                    // --- 3. XỬ LÝ FILE ĐÍNH KÈM (UploadFile) ---
                    var uploadFile = Request.Form.Files["UploadFile"];
                    string fileLog = "Không có tệp đính kèm";

                    if (uploadFile != null && uploadFile.Length > 0)
                    {
                        string networkPath = @"\\10.0.60.30\BPVN-Fileserver\Public\IT-Information Technology Dept\5.E-Form\DonIT";

                        if (!Directory.Exists(networkPath))
                        {
                            Directory.CreateDirectory(networkPath);
                        }

                        string extension = Path.GetExtension(uploadFile.FileName);
                        string safeName = RemoveSign4VietnameseString(userName).Replace(" ", "");
                        string timeStamp = DateTime.Now.ToString("ddMMyy_HHmmss");

                        string fileName = $"DonWifi_ID{form.Id}_{safeName}_{timeStamp}{extension}";
                        string fullPath = Path.Combine(networkPath, fileName);

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
                        itWifi.Anh = await GetFileBytesAsync2("Anh");
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
                    var lichSu = new LichSu
                    {
                        IdFormIt = form.Id,
                        TieuDe = "Khởi tạo đơn Wifi",
                        Mota = $"Người tạo: {userName} (ID: {userId}) | Công ty: {tenCongTy} | Thiết bị: {itWifi?.LoaiThietBi} | MAC: {itWifi?.MacTb}. " +
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

            // --- THÊM: LẤY TÊN CÔNG TY ---
            var tenCongTy = User.FindFirst("TenCongTy")?.Value ?? "";

            // --- LẤY DANH SÁCH CÔNG VIỆC ---
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
            // --- LẤY THÔNG TIN TỪ CLAIMS (CK) ---
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
                    form.TenCongTy = tenCongTy; // Lưu tên công ty
                    form.TrangThai = "ChoDuyet";
                    form.IdForm = "IT_DT_BAN_4";
                    form.TenForm = "Đơn đăng ký sử dụng điện thoại bàn";

                    if (congViec != null)
                    {
                        form.Danhmuc = congViec.Ten;
                    }

                    _context.FormIts.Add(form);
                    await _context.SaveChangesAsync();

                    // --- BƯỚC 2: XỬ LÝ FILE ĐÍNH KÈM ---
                    var uploadFile = Request.Form.Files["UploadFile"];
                    if (uploadFile != null && uploadFile.Length > 0)
                    {
                        string networkPath = @"\\10.0.60.30\BPVN-Fileserver\Public\IT-Information Technology Dept\5.E-Form\DonIT";
                        if (!Directory.Exists(networkPath)) Directory.CreateDirectory(networkPath);

                        string extension = Path.GetExtension(uploadFile.FileName);
                        string safeName = RemoveSign4VietnameseString(userName).Replace(" ", "");
                        string timeStamp = DateTime.Now.ToString("ddMMyy_HHmmss");

                        string fileName = $"DonDTBan_ID{form.Id}_{safeName}_{timeStamp}{extension}";
                        string fullPath = Path.Combine(networkPath, fileName);

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

                    // --- BƯỚC 4: LƯU NGƯỜI HỖ TRỢ ---
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
                        Mota = $"Người thao tác: {userName} | Công ty: {tenCongTy} | Công việc: {form.Danhmuc}. Nội dung: {itDtBan?.ThongTin}. Trạng thái: Chờ duyệt.",
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
                    ViewBag.CongViecList = await _context.CongViecs.Where(x => x.Ten.Contains("Đăng kí sử dụng điện thoại")).ToListAsync();
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

        [HttpGet("/FormIT/DownloadFile/{fileName}")]
        public async Task<IActionResult> DownloadFile(string fileName)
        {
            if (string.IsNullOrEmpty(fileName)) return NotFound();

            // Đường dẫn mạng giống hệt bước Upload
            string networkPath = @"\\10.0.60.30\BPVN-Fileserver\Public\IT-Information Technology Dept\5.E-Form\DonIT";
            string fullPath = Path.Combine(networkPath, fileName);

            // Kiểm tra file có tồn tại thực tế trên File Server không
            if (!System.IO.File.Exists(fullPath))
            {
                return NotFound("Tệp tin không tồn tại trên hệ thống lưu trữ.");
            }

            // Đọc dữ liệu file
            var memory = new MemoryStream();
            using (var stream = new FileStream(fullPath, FileMode.Open, FileAccess.Read))
            {
                await stream.CopyToAsync(memory);
            }
            memory.Position = 0;

            // Xác định định dạng file để trình duyệt mở/tải cho đúng
            string contentType = "application/octet-stream"; // Mặc định là tải về

            // Trả file về cho người dùng (người dùng sẽ thấy tên file gốc)
            return File(memory, contentType, fileName);
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
            // Đã thêm Include DanhGia để phân biệt trạng thái Chờ đánh giá và Hoàn tất
            var danhSachDon = await _context.FormIts
                .Where(f => f.IdNguoiTao == userId)
                .Include(f => f.DanhGia)
                .Include(f => f.ItMail1s)
                .Include(f => f.ItCtNguoiHoTros)
                    .ThenInclude(ct => ct.IdItNguoiHoTroNavigation)
                .OrderByDescending(f => f.Id)
                .ToListAsync();

            return View(danhSachDon);
        }

        #endregion

        #region XỬ LÝ ĐƠN IT (Duyệt / Hủy / Hoàn tất) - SỬ DỤNG COOKIE AUTH (CK)

        [HttpGet("/FormIT/QuanLyXetDuyet")]
        public async Task<IActionResult> QuanLyXetDuyet()
        {
            // --- 1. LẤY THÔNG TIN TỪ CLAIMS (Giữ nguyên & Bổ sung TenCongTy) ---
            var userIdStr = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userIdStr)) return Redirect("/DonXetDuyet/DangNhap");
            int userId = int.Parse(userIdStr);

            var userRole = User.FindFirst(System.Security.Claims.ClaimTypes.Role)?.Value
                            ?? User.FindFirst("UserRole")?.Value ?? "";
            var phongBan = User.FindFirst("PhongBan")?.Value?.Trim() ?? "";

            // Lấy thông tin TenCongTy từ Claim
            var tenCongTy = User.FindFirst("TenCongTy")?.Value?.Trim() ?? "";

            if (userRole == "BaoVe") return Forbid();

            // --- 2. TRUY VẤN DỮ LIỆU (Giữ nguyên toàn bộ Include) ---
            IQueryable<FormIt> query = _context.FormIts
                .Include(f => f.ItCtNguoiHoTros)
                    .ThenInclude(ct => ct.IdItNguoiHoTroNavigation)
                .Include(f => f.DanhGia); // Giữ nguyên để kiểm tra đánh giá

            // --- 3. LOGIC PHÂN QUYỀN (Kết hợp TenCongTy & Giữ nguyên logic cũ) ---

            // Ưu tiên lọc theo TenCongTy trước để đảm bảo an toàn dữ liệu giữa các công ty
            if (!string.IsNullOrEmpty(tenCongTy))
            {
                query = query.Where(f => f.TenCongTy == tenCongTy);
            }

            if (userRole == "All")
            {
                /* Xem toàn bộ trong phạm vi công ty đã lọc ở trên */
            }
            else if (userRole == "Admin" || userRole == "QuanLy")
            {
                if (!string.IsNullOrEmpty(phongBan))
                    query = query.Where(f => f.BoPhan != null && f.BoPhan.Trim().ToLower() == phongBan.ToLower());
                else
                    query = query.Where(f => false);
            }
            else
            {
                query = query.Where(f => f.IdNguoiTao == userId);
            }

            // --- 4. THỰC THI TRUY VẤN ---
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
            // Loại bỏ điều kiện IdNguoiDuyet != null ở đây để lấy được cả đơn "Chờ QL Duyệt"
            IQueryable<FormIt> query = _context.FormIts
                .Include(f => f.ItCtNguoiHoTros)
                    .ThenInclude(ct => ct.IdItNguoiHoTroNavigation)
                .Include(f => f.DanhGia); // Include để kiểm tra trạng thái Hoàn tất

            // --- 3. PHÂN QUYỀN LỌC DỮ LIỆU (Giữ nguyên tuyệt đối logic của bạn) ---
            if (userRole == "All" || userRole == "Admin")
            {
                string IT_Dept_Name = "Phòng thông tin 资讯科技部";
                query = query.Where(f => f.BoPhan == IT_Dept_Name || f.IdAdmin != null);
            }
            else if (userRole == "QuanLy")
            {
                if (!string.IsNullOrEmpty(phongBanSession))
                {
                    query = query.Where(f => f.BoPhan != null &&
                                            f.BoPhan.Trim().ToLower() == phongBanSession.ToLower());
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

            // --- 4. THỰC THI TRUY VẤN ---
            var danhSachDon = await query
                .OrderByDescending(f => f.TimeNguoiDuyet)
                .AsNoTracking()
                .ToListAsync();

            // --- 5. GÁN TRẠNG THÁI DỰA TRÊN LOGIC YÊU CẦU ---
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
                else if (item.DanhGia == null || !item.DanhGia.Any())
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

            // 6.1 THỐNG KÊ THEO CÔNG TY (mỗi công ty 1 báo cáo riêng và 1 báo cáo tổng)
            var companies = allForms
                .Select(f => string.IsNullOrWhiteSpace(f.TenCongTy) ? "Không xác định" : f.TenCongTy)
                .Distinct()
                .OrderBy(x => x)
                .ToList();

            // Build a dictionary of reports per company (key = company name)
            var companyReports = new Dictionary<string, object>();

            // Helper local func to build report object for a list of forms
            object BuildReport(IEnumerable<FormIt> formsForCompany)
            {
                var list = formsForCompany.ToList();
                var tStats = list
                    .GroupBy(f => f.IdForm ?? "")
                    .Select(g => new { Label = GetShortNameIT(g.Key), Value = g.Count() })
                    .ToList();

                var dStats = list
                    .Where(f => !string.IsNullOrEmpty(f.Danhmuc))
                    .GroupBy(f => f.Danhmuc)
                    .Select(g => new { Label = g.Key, Value = g.Count() })
                    .ToList();

                bool IsHuyLocal(FormIt f) => (f.TenForm ?? "").Contains("[ĐÃ HỦY]") || f.TrangThai == "TuChoi";

                int h = list.Count(IsHuyLocal);
                int ht = list.Count(f => !IsHuyLocal(f) && f.IdAdmin != null && f.TimeAdmin != null && f.DanhGia != null && f.DanhGia.Any());
                int chdg = list.Count(f => !IsHuyLocal(f) && f.IdAdmin != null && f.TimeAdmin != null && (f.DanhGia == null || !f.DanhGia.Any()));
                int dxl = list.Count(f => !IsHuyLocal(f) && f.IdNguoiDuyet != null && f.TimeNguoiDuyet != null && f.IdAdmin == null);
                int cd = list.Count - h - ht - chdg - dxl;

                return new
                {
                    Total = list.Count,
                    TypeLabels = tStats.Select(x => x.Label).ToList(),
                    TypeCounts = tStats.Select(x => x.Value).ToList(),
                    DanhMucLabels = dStats.Select(x => x.Label).ToList(),
                    DanhMucCounts = dStats.Select(x => x.Value).ToList(),
                    StatusLabels = new List<string> { "Hoàn tất", "Chờ đánh giá", "Đang xử lý", "Chờ duyệt", "Đã hủy" },
                    StatusCounts = new List<int> { ht, chdg, dxl, cd, h }
                };
            }

            // Overall (All companies)
            companyReports["All"] = BuildReport(allForms);

            foreach (var c in companies)
            {
                var forC = allForms.Where(f => (f.TenCongTy ?? "Không xác định") == c);
                companyReports[c] = BuildReport(forC);
            }

            ViewBag.Companies = (new List<string> { "All" }).Concat(companies).ToList();
            ViewBag.CompanyReports = companyReports; // will be serialized in view

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
