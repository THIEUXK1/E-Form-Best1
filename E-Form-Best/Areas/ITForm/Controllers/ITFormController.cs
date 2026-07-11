using E_Form_Best.Context;
using E_Form_Best.Models.ITForm;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Data.SqlClient;
using System.DirectoryServices.AccountManagement;
using System.Runtime.InteropServices;
using System.Security.Claims;

namespace E_Form_Best.Areas.ITForm.Controllers
{
    [Area("ITform")]
    public class ITFormController : Controller
    {
        public ITFormContext _context;
        private readonly IConfiguration _config;

        // Sửa constructor để nhận DI từ hệ thống
        public ITFormController(IConfiguration config)
        {
            _context = new ITFormContext();
            _config = config;
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
            if (User?.Identity?.IsAuthenticated == true) return Redirect("/menuA");
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

            User? user = null;
            UserDomainAuth? domainAuth = null;
            bool isDomainAuth = false;
            bool isAuthenticated = false;

            // A. Tìm kiếm User và thông tin liên kết Domain
            // LƯU Ý EF CORE: Tại tầng IQueryable này, ta bỏ .ToLower() trên DB field và giá trị truyền vào.
            // SQL Server mặc định không phân biệt hoa thường (Case-Insensitive), giúp EF Core sinh mã SQL sạch và sử dụng Index hiệu quả.
            domainAuth = await _context.UserDomainAuths
                .Include(a => a.IdNguoiDungNavigation)
                    .ThenInclude(u => u!.UserBoPhans).ThenInclude(ub => ub.IdBoPhanNavigation)
                .Include(a => a.IdNguoiDungNavigation)
                    .ThenInclude(u => u!.UserQuyens).ThenInclude(uq => uq.IdQuyenNavigation)
                .FirstOrDefaultAsync(a => a.DomainUsername != null && a.DomainUsername == email);

            if (domainAuth != null)
            {
                user = domainAuth.IdNguoiDungNavigation;
            }
            else
            {
                // Bỏ biến đổi .ToLower() ngầm (nếu có) hoặc các biểu thức phức tạp, giữ so sánh nguyên bản để tối ưu SQL
                user = await _context.Users
                    .Include(u => u.UserBoPhans).ThenInclude(ub => ub.IdBoPhanNavigation)
                    .Include(u => u.UserQuyens).ThenInclude(uq => uq.IdQuyenNavigation)
                    .FirstOrDefaultAsync(u => u.Tk == email && u.TrangThai != "Đã nghỉ");
            }

            // B. Kiểm tra user tồn tại
            if (user == null)
            {
                ViewBag.Error = "Tài khoản hoặc mật khẩu không đúng.";
                return View();
            }

            // C. Kiểm tra khóa tài khoản (Lockout)
            if (user.LockoutEnd.HasValue && user.LockoutEnd > DateTime.Now)
            {
                var remainingSeconds = (int)(user.LockoutEnd.Value - DateTime.Now).TotalSeconds;
                ViewBag.Error = $"Tài khoản bị khóa. Vui lòng thử lại sau {remainingSeconds} giây.";
                return View();
            }

            // D. XỬ LÝ 3 CHẾ ĐỘ ĐĂNG NHẬP (LoginMode)
            if (domainAuth != null)
            {
                // Chế độ 0 (Domain Only) hoặc 2 (Hybrid) -> Thử xác thực qua Domain trước
                if (domainAuth.LoginMode == 0 || domainAuth.LoginMode == 2)
                {
                    if (AuthenticateWithDomain(email, matKhau))
                    {
                        isAuthenticated = true;
                        isDomainAuth = true;
                    }
                }

                // Nếu xác thực Domain chưa thành công VÀ Chế độ là 1 (DB Only) hoặc 2 (Hybrid) -> Thử xác thực qua DB
                if (!isAuthenticated && (domainAuth.LoginMode == 1 || domainAuth.LoginMode == 2))
                {
                    // So sánh mật khẩu phân biệt chữ hoa/thường bằng Ordinal (vì password bắt buộc phải khớp chính xác)
                    if (string.Equals(user.MatKhau, matKhau, StringComparison.Ordinal))
                    {
                        isAuthenticated = true;
                        isDomainAuth = false; // Đánh dấu là đăng nhập bằng Database
                    }
                }
            }
            else
            {
                // User chưa được liên kết Domain -> Chỉ xác thực qua Database
                if (string.Equals(user.MatKhau, matKhau, StringComparison.Ordinal))
                {
                    isAuthenticated = true;
                    isDomainAuth = false;
                }
            }

            // E. Xử lý nếu xác thực thất bại
            if (!isAuthenticated)
            {
                // Tăng số lần sai
                user.FailedAttempts = (user.FailedAttempts ?? 0) + 1;

                if (user.FailedAttempts >= 5)
                {
                    // Tính thời gian khóa: 30 * 2^(n-5)
                    int multiplier = (int)Math.Pow(2, user.FailedAttempts.Value - 5);
                    int seconds = 30 * multiplier;
                    user.LockoutEnd = DateTime.Now.AddSeconds(seconds);
                    ViewBag.Error = $"Bạn đã nhập sai {user.FailedAttempts} lần. Tài khoản bị khóa {seconds} giây.";
                }
                else
                {
                    ViewBag.Error = $"Tài khoản hoặc mật khẩu không đúng. (Lần thử {user.FailedAttempts}/5)";
                }

                _context.Users.Update(user);
                await _context.SaveChangesAsync();
                return View();
            }

            // F. Đăng nhập thành công -> Reset trạng thái khóa
            user.FailedAttempts = 0;
            user.LockoutEnd = null;
            _context.Users.Update(user);
            await _context.SaveChangesAsync();

            // 2. Đảm bảo có Security Stamp
            if (string.IsNullOrEmpty(user.SecurityStamp))
            {
                user.SecurityStamp = Guid.NewGuid().ToString();
                _context.Users.Update(user);
                await _context.SaveChangesAsync();
            }

            // 3. Xử lý định danh thiết bị
            string userAgent = Request.Headers["User-Agent"].ToString() ?? "";
            var remoteIpAddress = HttpContext.Connection.RemoteIpAddress;
            string? ipAddress = remoteIpAddress?.ToString(); // Chuyển thành string? vì gán từ một giá trị có thể null
            string resolvedComputerName = deviceName;

            if (string.IsNullOrEmpty(resolvedComputerName) || resolvedComputerName.Equals("Thiết bị không xác định", StringComparison.OrdinalIgnoreCase))
            {
                try
                {
                    if (remoteIpAddress != null)
                    {
                        var hostEntry = System.Net.Dns.GetHostEntry(remoteIpAddress);
                        resolvedComputerName = hostEntry.HostName;

                        // Thay đổi phương thức kiểm tra và cắt chuỗi an toàn không tạo mảng char ngầm
                        if (resolvedComputerName.Contains('.', StringComparison.Ordinal))
                            resolvedComputerName = resolvedComputerName.Split('.', StringSplitOptions.None)[0];
                    }
                }
                catch { resolvedComputerName = "Không thể xác định tên máy"; }
            }

            // So sánh DeviceFingerprint chạy trên RAM (LINQ to Objects) bằng StringComparison
            var device = _context.UserDevices.AsEnumerable().FirstOrDefault(d => d.IdNguoiDung == user.IdNguoiDung && string.Equals(d.DeviceFingerprint, userAgent, StringComparison.Ordinal));
            if (device == null)
            {
                _context.UserDevices.Add(new UserDevice { IdNguoiDung = user.IdNguoiDung, DeviceFingerprint = userAgent, DeviceName = resolvedComputerName, LastLogin = DateTime.Now, IsTrusted = true });
            }
            else
            {
                device.LastLogin = DateTime.Now;
                device.DeviceName = resolvedComputerName;
                _context.UserDevices.Update(device);
            }

            // Lưu lịch sử truy cập (Sử dụng toán tử ?? để gán chuỗi rỗng nếu ipAddress bị null)
            _context.LichSuTruyCaps.Add(new LichSuTruyCap { IdNguoiDung = user.IdNguoiDung, ThoiGianDangNhap = DateTime.Now, TenMayTinh = resolvedComputerName, DiaChiIp = ipAddress ?? "", TrinhDuyet = userAgent, TrangThai = "Đang hoạt động" });
            await _context.SaveChangesAsync();

            // 4. Thiết lập Claims chính để lưu vào Cookie Authentication
            var claims = new List<Claim>
            {
                new Claim(ClaimTypes.NameIdentifier, user.IdNguoiDung.ToString()),
                new Claim(ClaimTypes.Name, user.HoTen ?? ""),
                new Claim(ClaimTypes.Email, user.Tk ?? ""),
                new Claim("MaNv", user.Tk ?? ""),
                new Claim("UserRole", user.VaiTro ?? ""),
                new Claim("PhongBan", user.PhongBan ?? ""),
                new Claim("TenCongTy", user.TenCongTy ?? ""),
                new Claim("AnhDaiDien", user.AnhDaiDien ?? "/images/default-avatar.png"),
                new Claim("SecurityStamp", user.SecurityStamp ?? ""),
                new Claim("LoginMethod", isDomainAuth ? "Domain" : "Database")
            };

            // Mảng gom toàn bộ chuỗi quyền thu được từ cả 2 phân hệ trước khi nạp vào cookie Role
            var danhSachMaQuyenDeGhanRole = new List<string>();

            // =====================================================================================
            // CƠ CHẾ 1: QUYỀN BỘ PHẬN PHÒNG BAN (MÔ HÌNH MỚI - DANHMUCQUYENBOPHAN)
            // =====================================================================================
            if (user.UserBoPhans != null && user.UserBoPhans.Any(ub => ub.IdBoPhanNavigation != null))
            {
                // Đảm bảo lọc bỏ các phần tử điều hướng bị null trước khi Select dữ liệu
                string danhSachTenBP = string.Join(", ", user.UserBoPhans.Where(ub => ub.IdBoPhanNavigation != null).Select(ub => ub.IdBoPhanNavigation!.TenBoPhan));
                string danhSachMoTaBP = string.Join(" | ", user.UserBoPhans.Where(ub => ub.IdBoPhanNavigation != null).Select(ub => ub.IdBoPhanNavigation!.MoTa));

                // Ghi đè/Cập nhật dữ liệu tên bộ phận chính thức vào danh mục Claim lưu trú
                var existingPhongBan = claims.FirstOrDefault(c => c.Type == "PhongBan");
                if (existingPhongBan != null) claims.Remove(existingPhongBan);
                claims.Add(new Claim("PhongBan", danhSachTenBP));

                claims.Add(new Claim("TenBoPhan", danhSachTenBP));
                claims.Add(new Claim("MoTaBoPhan", danhSachMoTaBP));

                var listIdBoPhan = user.UserBoPhans.Where(ub => ub.IdBoPhanNavigation != null).Select(ub => ub.IdBoPhan).ToList();

                // Lấy các mã quyền MaQuyen tiếng Anh viết liền từ mô hình bộ phận mới
                var quyenBoPhan = await _context.BoPhanQuyenTrungGians
                    .Where(x => listIdBoPhan.Contains(x.IdBoPhan) && x.ChoPhep == true && x.IdQuyenNavigation != null)
                    .Select(x => x.IdQuyenNavigation!.MaQuyen)
                    .Distinct()
                    .ToListAsync();

                danhSachMaQuyenDeGhanRole.AddRange(quyenBoPhan);
            }
            else
            {
                // BỘ LỌC DỰ PHÒNG CHUỖI THÔ: Nếu bảng trung gian trống -> Quét chuỗi văn bản phong_ban từ bảng User sang danh mục bộ phận tương ứng
                string phongBanTho = user.PhongBan?.Trim() ?? "";
                if (!string.IsNullOrEmpty(phongBanTho))
                {
                    var boPhanTuongUng = await _context.BoPhans
                        .FirstOrDefaultAsync(bp => bp.TenBoPhan != null && bp.TenBoPhan == phongBanTho);

                    if (boPhanTuongUng != null)
                    {
                        var quyenBoPhanDuPhong = await _context.BoPhanQuyenTrungGians
                            .Where(x => x.IdBoPhan == boPhanTuongUng.IdBoPhan && x.ChoPhep == true && x.IdQuyenNavigation != null)
                            .Select(x => x.IdQuyenNavigation!.MaQuyen)
                            .Distinct()
                            .ToListAsync();

                        danhSachMaQuyenDeGhanRole.AddRange(quyenBoPhanDuPhong);
                    }
                }
            }

            // =====================================================================================
            // CƠ CHẾ 2: QUYỀN CÁ NHÂN RIÊNG LẺ (MÔ HÌNH CŨ - BẢNG QUYEN & USER_QUYEN)
            // TỰ ĐỘNG CHUYỂN ĐỔI CHUỖI TIẾNG VIỆT CÓ DẤU SANG CHUỖI VIẾT LIỀN KHÔNG DẤU ĐỂ KHỚP VIEW
            // =====================================================================================
            if (user.UserQuyens != null)
            {
                var quyenRiengLeUser = user.UserQuyens
                    .Where(uq => uq.IdQuyenNavigation != null && !string.IsNullOrEmpty(uq.IdQuyenNavigation.TenQuyen))
                    .Select(uq => uq.IdQuyenNavigation!.TenQuyen!)
                    .ToList();

                foreach (var tenQuyenGoc in quyenRiengLeUser)
                {
                    // Chuyển hóa tự động: "Giám đốc HR" -> "GiamDocHR", "Bảo vệ HR" -> "BaoVeHR"
                    string quyenChuyenDoi = ConvertVietnameseToEnglishCode(tenQuyenGoc);
                    if (!string.IsNullOrEmpty(quyenChuyenDoi))
                    {
                        danhSachMaQuyenDeGhanRole.Add(quyenChuyenDoi);
                    }
                }
            }

            // Khử trùng lặp toàn bộ dải quyền hợp lệ thu được từ cả 2 mô hình độc lập và đẩy vào Cookie Role hệ thống
            foreach (var maQuyen in danhSachMaQuyenDeGhanRole.Distinct())
            {
                if (!string.IsNullOrEmpty(maQuyen))
                {
                    // Lưu định dạng chuỗi gốc hoặc không dấu viết liền để User.IsInRole("GiamDocHR") ngoài View chạy chính xác
                    claims.Add(new Claim(ClaimTypes.Role, maQuyen.Trim()));
                }
            }

            if (!isDomainAuth && matKhau.Equals("abc12345", StringComparison.Ordinal)) claims.Add(new Claim("IsDefaultPassword", "true"));

            var claimsIdentity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
            var authProperties = new AuthenticationProperties
            {
                IsPersistent = rememberMe,
                ExpiresUtc = rememberMe ? DateTimeOffset.UtcNow.AddDays(365) : null,
                AllowRefresh = true
            };

            // Mã hóa toàn bộ thông tin Claims thành Token và ghi đè xuống Cookie (ck) Trình duyệt của Client
            await HttpContext.SignInAsync(
                CookieAuthenticationDefaults.AuthenticationScheme,
                new ClaimsPrincipal(claimsIdentity),
                authProperties);

            if (rememberMe)
                Response.Cookies.Append("RememberedEmail", email, new CookieOptions { Expires = DateTime.Now.AddDays(30), HttpOnly = true, IsEssential = true });
            else
                Response.Cookies.Delete("RememberedEmail");

            TempData["ShowPushPrompt"] = true;
            return Redirect("/menuA");
        }

        // Xác thực Hybrid dùng chung với trang đăng nhập chính (DangNhap): ưu tiên Domain theo UserDomainAuth.LoginMode
        // (0 = Domain only, 1 = DB only, 2 = Hybrid - thử Domain trước, không được thì fallback DB). Trả về User nếu xác thực đúng, null nếu sai.
        private async Task<User?> XacThucTaiKhoanHybrid(string taikhoan, string matkhau)
        {
            if (string.IsNullOrEmpty(taikhoan) || string.IsNullOrEmpty(matkhau)) return null;

            var domainAuth = await _context.UserDomainAuths
                .Include(a => a.IdNguoiDungNavigation)
                .FirstOrDefaultAsync(a => a.DomainUsername != null && a.DomainUsername == taikhoan);

            User? user = domainAuth != null
                ? domainAuth.IdNguoiDungNavigation
                : await _context.Users.FirstOrDefaultAsync(u => u.Tk == taikhoan && u.TrangThai != "Đã nghỉ");

            if (user == null) return null;

            bool isAuthenticated = false;

            if (domainAuth != null)
            {
                if (domainAuth.LoginMode == 0 || domainAuth.LoginMode == 2)
                {
                    if (AuthenticateWithDomain(taikhoan, matkhau))
                    {
                        isAuthenticated = true;
                    }
                }

                if (!isAuthenticated && (domainAuth.LoginMode == 1 || domainAuth.LoginMode == 2))
                {
                    if (string.Equals(user.MatKhau, matkhau, StringComparison.Ordinal))
                    {
                        isAuthenticated = true;
                    }
                }
            }
            else
            {
                if (string.Equals(user.MatKhau, matkhau, StringComparison.Ordinal))
                {
                    isAuthenticated = true;
                }
            }

            return isAuthenticated ? user : null;
        }

        private bool AuthenticateWithDomain(string username, string password)
        {
            // Kiểm tra xem ứng dụng có đang chạy trên Windows không
            if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                // Nếu chạy trên Linux/macOS, không thể dùng PrincipalContext
                // Trả về false hoặc log lỗi tùy theo yêu cầu nghiệp vụ
                return false;
            }

            return AuthenticateOnWindows(username, password);
        }

        // Tách riêng logic Windows để trình biên dịch không cảnh báo lỗi CA1416 tại nơi gọi chính
        [System.Runtime.Versioning.SupportedOSPlatform("windows")]
        private bool AuthenticateOnWindows(string username, string password)
        {
            try
            {
                using (var context = new PrincipalContext(ContextType.Domain, "bestpacific.com"))
                {
                    return context.ValidateCredentials(username, password);
                }
            }
            catch
            {
                return false;
            }
        }

        [HttpGet("/DonXetDuyet/DangXuat")]
        public async Task<IActionResult> DangXuat()
        {
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (int.TryParse(userIdClaim, out int userId))
            {
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

            await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
            HttpContext.Session.Clear();
            TempData["Success"] = "Bạn đã đăng xuất thành công.";
            return Redirect("/DonXetDuyet/DangNhap");
        }

        // --- HÀM KHỬ DẤU VÀ KHOẢNG TRẮNG ĐỂ ĐỒNG BỘ QUYỀN RIÊNG LẺ CÁ NHÂN CŨ VÀO HỆ THỐNG MỚI ---
        private static string ConvertVietnameseToEnglishCode(string text)
        {
            if (string.IsNullOrEmpty(text)) return "";

            // Bắt chính xác từ khóa phân hệ HR đặc thù để ép chuỗi khớp tuyệt đối với câu lệnh kiểm tra cứng trên View
            string textCheck = text.Trim().Replace(" ", "").ToLower();
            if (textCheck == "giámđốchr" || textCheck == "giamdochr") return "GiamDocHR";
            if (textCheck == "bảovệhr" || textCheck == "baovehr") return "BaoVeHR";
            if (textCheck == "adminhr") return "AdminHR";
            if (textCheck == "quảnlýduyệtđơnhrb2" || textCheck == "quanlyduyetdonhrb2") return "QuanLyDuyetDonHR_B2";
            if (textCheck == "all" || textCheck == "admin") return "All";

            // Bộ lọc font xử lý chuyển đổi chữ có dấu tự động làm phương án dự phòng
            string[] arr1 = new string[] { "á", "à", "ả", "ã", "ạ", "â", "ấ", "ầ", "ẩ", "ẫ", "ậ", "ă", "ắ", "ằ", "ẳ", "ẵ", "ặ",
                                            "đ",
                                            "é","è","ẻ","ẽ","ẹ","ê","ế","ề","ể","ễ","ệ",
                                            "í","ì","ỉ","ĩ","ị",
                                            "ó","ò","ỏ","õ","ọ","ô","ố","ồ","ổ","ỗ","ộ","ơ","ớ","ờ","ở","ỡ","ợ",
                                            "ú","ù","ủ","ũ","ụ","ư","ứ","ừ","ử","ữ","ự",
                                            "ý","ỳ","ỷ","ỹ","ỵ",
                                            "Á", "À", "Ả", "Ã", "Ạ", "Â", "Ấ", "Ầ", "Ẩ", "Ẫ", "Ậ", "Ă", "Ắ", "ằ", "Ẳ", "Ẵ", "Ặ",
                                            "Đ",
                                            "É","È","Ẻ","Ẽ","Ẹ","Ê","Ế","Ề","Ể","Ễ","Ệ",
                                            "Í","Ì","Ỉ","Ĩ","Ị",
                                            "Ó","Ò","Ỏ","Õ","Ọ","Ô","Ố","Ồ","Ổ","Ỗ","Ộ","Ơ","Ớ","Ờ","Ở","Ỡ","Ợ",
                                            "Ú","Ù","Ủ","Ũ","Ụ","Ư","Ứ","Ừ","Ử","Ữ","Ự",
                                            "Ý","Ỳ","Ỷ","Ỹ","Ỵ" };

            string[] arr2 = new string[] { "a", "a", "a", "a", "a", "a", "a", "a", "a", "a", "a", "a", "a", "a", "a", "a", "a",
                                            "d",
                                            "e","e","e","e","e","e","e","e","e","e","e",
                                            "i","i","i","i","i",
                                            "o","o","o","o","o","o","o","o","o","o","o","o","o","o","o","o","o",
                                            "u","u","u","u","u","u","u","u","u","u","u",
                                            "y","y","y","y","y",
                                            "A", "A", "A", "A", "A", "A", "A", "A", "A", "A", "A", "A", "A", "a", "A", "A", "A",
                                            "D",
                                            "E","E","E","E","E","E","E","E","E","E","E",
                                            "I","I","I","I","I",
                                            "O","O","O","O","O","O","O","O","O","O","O","O","O","O","O","O","O",
                                            "U","U","U","U","U","U","U","U","U","U","U",
                                            "Y","Y","Y","Y","Y" };

            for (int i = 0; i < arr1.Length; i++)
            {
                text = text.Replace(arr1[i], arr2[i]);
            }

            return text.Replace(" ", "").Trim();
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
            if (User == null || User.Identity == null ||   !User.Identity.IsAuthenticated)
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
            if (string.IsNullOrWhiteSpace(str)) return string.Empty;

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
                foreach (char c in VietnameseSigns[i])
                {
                    str = str.Replace(c, VietnameseSigns[0][i - 1]);
                }
            }
            return str;
        }

        // HÀM HELPER - Xử lý chuyển đổi file sang byte[]
        // Chuyển kiểu trả về thành byte[]? để thể hiện rõ khả năng trả về null
        private async Task<byte[]?> GetFileBytesAsync2(string inputName)
        {
            try
            {
                // Kiểm tra Request và FileCollection an toàn
                if (Request?.Form?.Files != null && Request.Form.Files.Count > 0)
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
                // Nếu lỗi xảy ra, trả về null một cách tường minh
                return null;
            }
            return null;
        }
        #endregion

        #region Don Mail  1Form IT 1)

        [HttpGet("/FormIT/DonMail")]
        public IActionResult DonMail()
        {
            if (User == null || User.Identity == null || !User.Identity.IsAuthenticated)
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
            if (User == null || User.Identity == null || !User.Identity.IsAuthenticated)
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
            var userName = User.Identity?.Name ?? "";
            var phongBan = User.FindFirst("PhongBan")?.Value ?? "";
            var userRole = User.FindFirst(System.Security.Claims.ClaimTypes.Role) ?? User.FindFirst("UserRole");
            var userEmail = User.FindFirst(System.Security.Claims.ClaimTypes.Email)?.Value ?? "";

            // Lấy thông tin Công ty từ Claim
            var tenCongTy = User.FindFirst("TenCongTy")?.Value ?? "";

            // Lấy danh sách công việc (Chế độ 1)
            ViewBag.CongViecList = await _context.CongViecIts
                                            .OrderBy(x => x.Ten)
                                            .ToListAsync();

            // Lấy danh sách người hỗ trợ IT (Chế độ 2)
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
        public async Task<IActionResult> TaoIT_Order(FormIt form, [FromForm] ItOrderIt2 itOrder, string modeHoTro, int? selectedCongViecId, List<int> selectedNguoiHoTroIds)
        {
            var userIdClaim = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userIdClaim))
            {
                if (modeHoTro == "Mode2") return Json(new { success = false, message = "Hết phiên đăng nhập." });
                return Redirect("/DonXetDuyet/DangNhap");
            }

            int userId = int.Parse(userIdClaim);
            var userName = User.Identity?.Name ?? "";
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
                    List<ItNguoiHoTro> danhSachNguoiHoTro = new List<ItNguoiHoTro>();

                    if (modeHoTro == "Mode1" && selectedCongViecId.HasValue)
                    {
                        congViec = await _context.CongViecIts
                            .Include(c => c.IdItNguoiHoTroNavigation)
                            .FirstOrDefaultAsync(c => c.Id == selectedCongViecId.Value);
                    }
                    else if (modeHoTro == "Mode2" && selectedNguoiHoTroIds != null && selectedNguoiHoTroIds.Any())
                    {
                        danhSachNguoiHoTro = await _context.ItNguoiHoTros
                            .Where(x => selectedNguoiHoTroIds.Contains(x.Id))
                            .ToListAsync();
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

                    form.IdNguoiDuyet = 8;
                    form.TenNguoiDuyet = "Hệ thống E-form";
                    form.TimeNguoiDuyet = DateTime.Now;

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
                            itOrder.ThoiHanHoanThanh = null; // Chế độ 1 mặc định không dùng thời hạn chọn tay
                        }
                        else if (string.IsNullOrEmpty(itOrder.Ten) && modeHoTro == "Mode2")
                        {
                            itOrder.Ten = "Chỉ định người hỗ trợ trực tiếp";
                            // Thuộc tính itOrder.ThoiHanHoanThanh đã được gán tự động từ view nếu người dùng chọn ngày
                        }

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
                    else if (modeHoTro == "Mode2" && selectedNguoiHoTroIds != null && selectedNguoiHoTroIds.Any())
                    {
                        int indexStt = 1;
                        foreach (var idNguoi in selectedNguoiHoTroIds)
                        {
                            var ctNguoiHoTro = new ItCtNguoiHoTro
                            {
                                IdFormIt = form.Id,
                                IdItNguoiHoTro = idNguoi,
                                Stt = indexStt++
                            };
                            _context.ItCtNguoiHoTros.Add(ctNguoiHoTro);
                        }

                        var names = string.Join(", ", danhSachNguoiHoTro.Select(x => x.Ten));
                        supporterLog = $"Đã chỉ định trực tiếp nhóm: {(!string.IsNullOrEmpty(names) ? names : "Nhân viên IT")}";
                    }

                    // --- 6. LƯU LỊCH SỬ ---
                    string deadlineLog = itOrder?.ThoiHanHoanThanh.HasValue == true
                        ? $"Thời hạn hoàn thành: {itOrder.ThoiHanHoanThanh.Value:dd/MM/yyyy HH:mm}"
                        : "Thời hạn: Không chỉ định";
                    string anhLog = string.IsNullOrEmpty(itOrder?.DuongDanAnh) ? "Không có ảnh" : $"Ảnh: {itOrder.DuongDanAnh}";

                    var lichSu = new LichSuFormIt
                    {
                        IdFormIt = form.Id,
                        TieuDe = "Khởi tạo yêu cầu",
                        Mota = $"[Cty: {tenCongTy}] Người tạo: {userName}. {supporterLog}. {deadlineLog}. {fileLog}. {anhLog}",
                        Time = DateTime.Now
                    };
                    _context.LichSuFormIts.Add(lichSu);

                    await _context.SaveChangesAsync();
                    await transaction.CommitAsync();

                    if (modeHoTro == "Mode2")
                    {
                        return Json(new { success = true, message = "Gửi yêu cầu hỗ trợ đến các nhân viên IT thành công!" });
                    }

                    TempData["Success"] = "Gửi yêu cầu hỗ trợ IT thành công!";
                    return Redirect("/FormIT/DonCho");
                }
                catch (Exception ex)
                {
                    await transaction.RollbackAsync();
                    ViewBag.CongViecList = await _context.CongViecIts.OrderBy(x => x.Ten).ToListAsync();
                    ViewBag.NguoiHoTroList = await _context.ItNguoiHoTros.OrderBy(x => x.Ten).ToListAsync();

                    if (modeHoTro == "Mode2")
                    {
                        return Json(new { success = false, message = "Lỗi hệ thống: " + ex.Message });
                    }

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
            var userName = User.Identity?.Name ?? "";
            var phongBan = User.FindFirst("PhongBan")?.Value ?? "";
            var userRole = User.FindFirst(System.Security.Claims.ClaimTypes.Role)?.Value
                            ?? User.FindFirst("UserRole")?.Value ?? "";
            var userEmail = User.FindFirst(System.Security.Claims.ClaimTypes.Email)?.Value ?? "";
            var tenCongTy = User.FindFirst("TenCongTy")?.Value ?? "";

            // Thêm kiểm tra null cho _context hoặc CongViecIts nếu cần, 
            // hoặc sử dụng toán tử bẻ khóa bang (!) nếu chắc chắn DB không null
            ViewBag.CongViecList = _context.CongViecIts != null
                ? await _context.CongViecIts
                    .Where(x => x.Ten != null && x.Ten.Contains("Đăng kí sử dụng wifi"))
                    .OrderBy(x => x.Ten)
                    .ToListAsync()
                : new List<CongViecIt>();

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
        public async Task<IActionResult> TaoIT_Wifi(FormIt form, [FromForm] ItDangKiSuDungWifi3 chiTiet, int selectedCongViecId, List<string> arrMaThietBi, List<string> arrMacTb)
        {
            var userIdClaim = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userIdClaim)) return Redirect("/DonXetDuyet/DangNhap");

            int userId = int.Parse(userIdClaim);
            var userName = User.Identity?.Name ?? "";
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
                        string extension = Path.GetExtension(uploadFile.FileName) ?? "";
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

                    // --- 4. CHI TIẾT WIFI & XỬ LÝ ẢNH ---
                    string? imgFileName = null; // Khai báo string? vì giá trị ban đầu là null
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

                    // --- BƯỚC GHÉP MẢNG ĐỊNH DANH THIẾT BỊ & ĐỊA CHỈ MAC QUA DẤU NGĂN CÁCH | ---
                    int thietBiCount = 0;
                    if (chiTiet == null) chiTiet = new ItDangKiSuDungWifi3();

                    if (arrMaThietBi != null && arrMaThietBi.Count > 0)
                    {
                        var dsMaThietBi = new List<string>();
                        var dsMacTb = new List<string>();

                        for (int i = 0; i < arrMaThietBi.Count; i++)
                        {
                            if (!string.IsNullOrWhiteSpace(arrMaThietBi[i]))
                            {
                                dsMaThietBi.Add(arrMaThietBi[i].Trim());
                                string macVal = (arrMacTb != null && i < arrMacTb.Count) ? arrMacTb[i].Trim() : "";
                                dsMacTb.Add(macVal);
                                thietBiCount++;
                            }
                        }

                        chiTiet.MaThietBi = string.Join(" | ", dsMaThietBi);
                        chiTiet.MacTb = string.Join(" | ", dsMacTb);
                    }

                    chiTiet.IdFormIt = form.Id;
                    if (!string.IsNullOrEmpty(imgFileName))
                    {
                        chiTiet.DuongDanAnh = imgFileName;
                        chiTiet.Anh = null;
                    }

                    _context.ItDangKiSuDungWifi3s.Add(chiTiet);
                    await _context.SaveChangesAsync();

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
                        Mota = $"Người tạo: {userName} | Số lượng thiết bị: {thietBiCount}. {supporterLog}. {fileLog}. {anhLog}",
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
            var userName = User.Identity?.Name ?? "";
            var phongBan = User.FindFirst("PhongBan")?.Value ?? "";
            var userRole = User.FindFirst(System.Security.Claims.ClaimTypes.Role)?.Value
                            ?? User.FindFirst("UserRole")?.Value ?? "";
            var userEmail = User.FindFirst(System.Security.Claims.ClaimTypes.Email)?.Value ?? "";

            // --- THÊM: LẤY TÊN CÔNG TY ---
            var tenCongTy = User.FindFirst("TenCongTy")?.Value ?? "";

            // --- LẤY DANH SÁCH CÔNG VIỆC ---
            ViewBag.CongViecList = await _context.CongViecIts
                .Where(x => x.Ten != null && x.Ten.Contains("Đăng kí sử dụng điện thoại"))
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
            var userName = User.Identity?.Name ?? "";
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
                    var congViec = _context.CongViecIts != null
                        ? await _context.CongViecIts
                            .Include(c => c.IdItNguoiHoTroNavigation)
                            .FirstOrDefaultAsync(c => c.Id == SelectedCongViecId)
                        : null;

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

                    if (_context.FormIts != null)
                    {
                        _context.FormIts.Add(form);
                        await _context.SaveChangesAsync();
                    }

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
                        string extension = Path.GetExtension(uploadFile.FileName) ?? "";
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

                        if (_context.ItDangKiSuDungDtban4s != null)
                        {
                            _context.ItDangKiSuDungDtban4s.Add(itDtBan);
                            await _context.SaveChangesAsync();
                        }
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

                        // Đã sửa thành ItCtNguoiHoTros để khớp chính xác với DbContext của bạn
                        if (_context.ItCtNguoiHoTros != null)
                        {
                            _context.ItCtNguoiHoTros.Add(chiTietHoTro);
                            supporterLog = $"Đã gán: {congViec.IdItNguoiHoTroNavigation.Ten}";
                            await _context.SaveChangesAsync();
                        }
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

                    if (_context.LichSuFormIts != null)
                    {
                        _context.LichSuFormIts.Add(lichSu);
                        await _context.SaveChangesAsync();
                    }

                    await transaction.CommitAsync();

                    TempData["Success"] = "Gửi đơn đăng ký điện thoại bàn thành công!";
                    return Redirect("/FormIT/DonCho");
                }
                catch (Exception ex)
                {
                    await transaction.RollbackAsync();

                    ViewBag.CongViecList = _context.CongViecIts != null
                        ? await _context.CongViecIts
                            .Where(x => x.Ten != null && x.Ten.Contains("Đăng kí sử dụng điện thoại"))
                            .ToListAsync()
                        : new List<CongViecIt>();

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
            var userName = User.Identity?.Name ?? "";
            var phongBan = User.FindFirst("PhongBan")?.Value ?? "";
            var userRole = User.FindFirst(System.Security.Claims.ClaimTypes.Role) ?? User.FindFirst("UserRole");
            var roleName = userRole?.Value ?? "";
            var userEmail = User.FindFirst(System.Security.Claims.ClaimTypes.Email)?.Value ?? "";
            var tenCongTy = User.FindFirst("TenCongTy")?.Value ?? "";

            // --- LẤY DANH SÁCH CÔNG VIỆC LIÊN QUAN ĐẾN TÀI KHOẢN ---
            ViewBag.CongViecList = _context.CongViecIts != null
                ? await _context.CongViecIts
                    .Where(x => x.Ten != null && (x.Ten.Contains("tài khoản hệ thống") || x.Ten.Contains("Account")))
                    .OrderBy(x => x.Ten)
                    .ToListAsync()
                : new List<CongViecIt>();

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
            var userName = User.Identity?.Name ?? ""; // SỬA: Thêm toán tử điều kiện null ?. phòng trường hợp Identity null
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
                        string extension = Path.GetExtension(uploadFile.FileName) ?? ""; // SỬA: Fallback về chuỗi rỗng nếu Extension null
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
                    string? imgFileName = null; // SỬA: Chuyển thành string? (nullable) vì ban đầu nó được gán bằng null
                    var anhFile = Request.Form.Files["Anh"];

                    // Xử lý lưu ảnh 1 lần nếu có
                    if (anhFile != null && anhFile.Length > 0)
                    {
                        string imgExtension = Path.GetExtension(anhFile.FileName) ?? ""; // SỬA: Fallback về chuỗi rỗng nếu Extension null
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
                    ViewBag.CongViecList = await _context.CongViecIts.Where(x => x.Ten != null && x.Ten.Contains("tài khoản")).ToListAsync();
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
            if (User == null || User.Identity == null || !User.Identity.IsAuthenticated)
                return Redirect("/DonXetDuyet/DangNhap");

            // Lọc danh sách nhân viên IT và lấy những công việc có tên "Đăng kí tài khoản máy tính"
            ViewBag.ListNguoiHoTro = _context.ItNguoiHoTros != null
                ? _context.ItNguoiHoTros
                    .Include(x => x.CongViecIts)
                    .Where(x => x.BoPhan == "IT" && x.CongViecIts.Any(cv => cv.Ten == "Đăng kí tài khoản máy tính"))
                    .ToList() // Đưa về Client evaluation trước để tránh lỗi dịch LINQ dịch toán tử 3 ngôi phức tạp
                    .Select(x => new E_Form_Best.Models.ITForm.ItNguoiHoTro
                    {
                        Id = x.Id,
                        MaNv = x.MaNv,
                        Ten = x.Ten,
                        BoPhan = x.BoPhan,
                        GhiChu = x.GhiChu,
                        CongViecIts = x.CongViecIts != null
                            ? x.CongViecIts.Where(cv => cv.Ten == "Đăng kí tài khoản máy tính").ToList()
                            : new List<CongViecIt>()
                    })
                    .ToList()
                : new List<E_Form_Best.Models.ITForm.ItNguoiHoTro>();

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
            if (User == null || User.Identity == null || !User.Identity.IsAuthenticated)
                return Redirect("/DonXetDuyet/DangNhap");

            // 2. Lấy thông tin User từ Claims
            var userIdStr = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
            int.TryParse(userIdStr, out int userId);

            var userName = User.Identity.Name ?? "";
            var phongBan = User.FindFirst("PhongBan")?.Value ?? "";
            var viTri = User.FindFirst("UserRole")?.Value ?? "";
            var userEmail = User.FindFirst(System.Security.Claims.ClaimTypes.Email)?.Value ?? "";
            var tenCongTy = User.FindFirst("TenCongTy")?.Value ?? "";

            if (_context.Database == null)
            {
                ModelState.AddModelError("", "Kết nối cơ sở dữ liệu không khả dụng.");
                return View(form);
            }

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

                    if (_context.FormIts != null)
                    {
                        _context.FormIts.Add(form);
                        await _context.SaveChangesAsync();
                    }

                    // --- CẤU HÌNH ĐƯỜNG DẪN LƯU FILE ---
                    string networkPath = @"\\10.0.60.30\BPVN-Fileserver\Public\IT-Information Technology Dept\5.E-Form\DonIT";
                    if (!Directory.Exists(networkPath)) Directory.CreateDirectory(networkPath);

                    string safeName = RemoveSign4VietnameseString(userName).Replace(" ", "");
                    string timeStamp = DateTime.Now.ToString("ddMMyy_HHmmss");

                    // --- BƯỚC 2: XỬ LÝ FILE ĐÍNH KÈM (UploadFile) ---
                    var uploadFile = Request.Form.Files["UploadFile"];
                    if (uploadFile != null && uploadFile.Length > 0)
                    {
                        string extension = Path.GetExtension(uploadFile.FileName) ?? "";
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

                        if (_context.ItDangkiTaiKhoanMayTinh6s != null)
                        {
                            _context.ItDangkiTaiKhoanMayTinh6s.Add(itAccount);
                            await _context.SaveChangesAsync();
                        }
                    }

                    // --- BƯỚC 4: LƯU NGƯỜI HỖ TRỢ ---
                    string danhSachTenHoTro = "Chưa chọn";
                    List<int> selectedItNguoiHoTroIds = new List<int>();

                    if (_context.CongViecIts != null)
                    {
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
                    }

                    if (selectedItNguoiHoTroIds.Any() && _context.ItNguoiHoTros != null)
                    {
                        var listHoTro = await _context.ItNguoiHoTros
                            .Where(x => selectedItNguoiHoTroIds.Contains(x.Id) && x.Ten != null)
                            .Select(x => x.Ten!)
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

                            if (_context.ItCtNguoiHoTros != null)
                            {
                                _context.ItCtNguoiHoTros.Add(chiTietHoTro);
                            }
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

                    if (_context.LichSuFormIts != null)
                    {
                        _context.LichSuFormIts.Add(lichSu);
                        await _context.SaveChangesAsync();
                    }

                    await transaction.CommitAsync();

                    TempData["Success"] = "Gửi đơn đăng ký tài khoản thành công!";
                    return Redirect("/FormIT/DonCho");
                }
                catch (Exception ex)
                {
                    await transaction.RollbackAsync();

                    // Load lại list hỗ trợ khi lỗi (Sửa lỗi LINQ tương tự bên trên)
                    ViewBag.ListNguoiHoTro = _context.ItNguoiHoTros != null
                        ? _context.ItNguoiHoTros
                            .Include(x => x.CongViecIts)
                            .Where(x => x.BoPhan == "IT" && x.CongViecIts.Any(cv => cv.Ten == "Đăng kí tài khoản máy tính"))
                            .ToList()
                            .Select(x => new E_Form_Best.Models.ITForm.ItNguoiHoTro
                            {
                                Id = x.Id,
                                MaNv = x.MaNv,
                                Ten = x.Ten,
                                CongViecIts = x.CongViecIts != null
                                    ? x.CongViecIts.Where(cv => cv.Ten == "Đăng kí tài khoản máy tính").ToList()
                                    : new List<CongViecIt>()
                            })
                            .ToList()
                        : new List<E_Form_Best.Models.ITForm.ItNguoiHoTro>();

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
            if (User == null || User.Identity == null || !User.Identity.IsAuthenticated)
                return Redirect("/DonXetDuyet/DangNhap");

            // Lọc danh sách nhân viên IT và lấy những công việc có tên "Lắp đặt thiết bị"
            ViewBag.ListNguoiHoTro = _context.ItNguoiHoTros != null
                ? _context.ItNguoiHoTros
                    .Include(x => x.CongViecIts)
                    .Where(x => x.BoPhan == "IT" && x.CongViecIts.Any(cv => cv.Ten == "Lắp đặt thiết bị"))
                    .ToList() // Chuyển về Client evaluation trước để tránh lỗi dịch toán tử 3 ngôi dưới database
                    .Select(x => new E_Form_Best.Models.ITForm.ItNguoiHoTro
                    {
                        Id = x.Id,
                        MaNv = x.MaNv,
                        Ten = x.Ten,
                        BoPhan = x.BoPhan,
                        GhiChu = x.GhiChu,
                        CongViecIts = x.CongViecIts != null
                            ? x.CongViecIts.Where(cv => cv.Ten == "Lắp đặt thiết bị").ToList()
                            : new List<CongViecIt>()
                    })
                    .ToList()
                : new List<E_Form_Best.Models.ITForm.ItNguoiHoTro>();

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
            if (User == null || User.Identity == null || !User.Identity.IsAuthenticated)
                return Redirect("/DonXetDuyet/DangNhap");

            // 2. Lấy thông tin User từ Claims
            var userIdStr = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
            int.TryParse(userIdStr, out int userId);

            var userName = User.Identity.Name ?? "";
            var phongBan = User.FindFirst("PhongBan")?.Value ?? "";
            var viTri = User.FindFirst("UserRole")?.Value ?? "";
            var userEmail = User.FindFirst(System.Security.Claims.ClaimTypes.Email)?.Value ?? "";
            var tenCongTy = User.FindFirst("TenCongTy")?.Value ?? "";

            if (_context.Database == null)
            {
                ModelState.AddModelError("", "Kết nối cơ sở dữ liệu không khả dụng.");
                return View(form);
            }

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

                    if (_context.FormIts != null)
                    {
                        _context.FormIts.Add(form);
                        await _context.SaveChangesAsync();
                    }

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
                        string extension = Path.GetExtension(uploadFile.FileName) ?? "";
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
                    if (itEquipment != null && _context.ItDonLapDatThietBi7s != null)
                    {
                        itEquipment.IdFormIt = form.Id;
                        _context.ItDonLapDatThietBi7s.Add(itEquipment);
                        await _context.SaveChangesAsync();
                    }

                    // --- BƯỚC 4: LƯU NGƯỜI HỖ TRỢ ---
                    string danhSachTenHoTro = "Chưa chọn";
                    List<int> selectedItNguoiHoTroIds = new List<int>();

                    if (_context.CongViecIts != null)
                    {
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
                    }

                    if (selectedItNguoiHoTroIds.Any() && _context.ItNguoiHoTros != null)
                    {
                        var listHoTro = await _context.ItNguoiHoTros
                            .Where(x => selectedItNguoiHoTroIds.Contains(x.Id) && x.Ten != null)
                            .Select(x => x.Ten!)
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

                            if (_context.ItCtNguoiHoTros != null)
                            {
                                _context.ItCtNguoiHoTros.Add(chiTietHoTro);
                            }
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

                    if (_context.LichSuFormIts != null)
                    {
                        _context.LichSuFormIts.Add(lichSu);
                        await _context.SaveChangesAsync();
                    }

                    await transaction.CommitAsync();

                    TempData["Success"] = "Gửi đơn yêu cầu lắp đặt thiết bị thành công!";
                    return Redirect("/FormIT/DonCho");
                }
                catch (Exception ex)
                {
                    await transaction.RollbackAsync();

                    // Load lại list hỗ trợ khi xảy ra lỗi để trả về View (Sửa lỗi dịch LINQ tương tự bên trên)
                    ViewBag.ListNguoiHoTro = _context.ItNguoiHoTros != null
                        ? _context.ItNguoiHoTros
                            .Include(x => x.CongViecIts)
                            .Where(x => x.BoPhan == "IT" && x.CongViecIts.Any(cv => cv.Ten == "Lắp đặt thiết bị"))
                            .ToList()
                            .Select(x => new E_Form_Best.Models.ITForm.ItNguoiHoTro
                            {
                                Id = x.Id,
                                MaNv = x.MaNv,
                                Ten = x.Ten,
                                BoPhan = x.BoPhan,
                                GhiChu = x.GhiChu,
                                CongViecIts = x.CongViecIts != null
                                    ? x.CongViecIts.Where(cv => cv.Ten == "Lắp đặt thiết bị").ToList()
                                    : new List<CongViecIt>()
                            })
                            .ToList()
                        : new List<E_Form_Best.Models.ITForm.ItNguoiHoTro>();

                    ModelState.AddModelError("", "Lỗi hệ thống: " + ex.Message);
                    return View(form);
                }
            }
        }

        #endregion

        #region Don Cap Quyen O Chung 8
        [HttpGet("/FormIT/DonCapQuyenOChung")]
        public IActionResult DonCapQuyenOChung()
        {
            if (User == null || User.Identity == null || !User.Identity.IsAuthenticated)
                return Redirect("/DonXetDuyet/DangNhap");

            // Lọc danh sách nhân viên IT phụ trách công việc "Cấp quyền ổ chung"
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
                    CongViecIts = x.CongViecIts.Where(cv => cv.Ten == "Cấp quyền ổ chung").ToList()
                })
                .Where(x => x.CongViecIts.Any())
                .ToList();

            // Lấy danh sách kết hợp Tên Công Ty và Phòng Ban duy nhất từ bảng User
            ViewBag.ListPhongBan = _context.Users
                .Where(u => !string.IsNullOrEmpty(u.PhongBan) && !string.IsNullOrEmpty(u.TenCongTy))
                .Select(u => u.TenCongTy + " - " + u.PhongBan)
                .Distinct()
                .OrderBy(x => x)
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

        [HttpPost("/FormIT/DonCapQuyenOChung")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DonCapQuyenOChung(FormIt form, [FromForm] ItCapQuyenOchung8 itOChung, string BoPhanDuocChon, int[] SelectedCongViecIds)
        {
            // 1. Kiểm tra đăng nhập
            if (User == null || User.Identity == null || !User.Identity.IsAuthenticated)
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
                    form.IdForm = "IT_CapQuyenOChung_8";
                    form.TenForm = "Đơn cấp quyền ổ chung";
                    form.Danhmuc = "Cấp quyền ổ chung";

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
                        string fileName = $"DonOChung_ID{form.Id}_{safeName}_{timeStamp}{extension}";
                        string fullPath = Path.Combine(networkPath, fileName);

                        using (var fileStream = new FileStream(fullPath, FileMode.Create))
                        {
                            await uploadFile.CopyToAsync(fileStream);
                        }

                        form.FileDinhKem = fileName;
                        _context.Entry(form).Property(x => x.FileDinhKem).IsModified = true;
                        await _context.SaveChangesAsync();
                    }

                    // --- BƯỚC 3: CHI TIẾT CẤU HÌNH Ổ CHUNG & XỬ LÝ ẢNH ---
                    if (itOChung != null)
                    {
                        itOChung.IdFormIt = form.Id;

                        var anhFile = Request.Form.Files["Anh"];
                        if (anhFile != null && anhFile.Length > 0)
                        {
                            string imgExtension = Path.GetExtension(anhFile.FileName);
                            if (string.IsNullOrEmpty(imgExtension)) imgExtension = ".jpg";

                            string imgFileName = $"AnhOChung_ID{form.Id}_{safeName}_{timeStamp}{imgExtension}";
                            string imgFullPath = Path.Combine(networkPath, imgFileName);

                            using (var imgStream = new FileStream(imgFullPath, FileMode.Create))
                            {
                                await anhFile.CopyToAsync(imgStream);
                            }

                            itOChung.DuongDanAnh = imgFileName;
                        }

                        _context.ItCapQuyenOchung8s.Add(itOChung);
                        await _context.SaveChangesAsync();

                        // --- BƯỚC MỚI: TẠO BẢN GHI CHO BẢNG XÁC NHẬN (Mọi thứ rỗng, chỉ lưu Bộ phận được chọn) ---
                        var xacNhan = new ItXacNhanCapQuyen8
                        {
                            IdCapQuyenOchung8 = itOChung.Id,
                            BoPhan = BoPhanDuocChon, // Lưu bộ phận người dùng chọn từ form
                            TrangThai = null,
                            IdNguoiXacNhan = null,
                            TenNguoiXacNhan = null,
                            TimeXacNhan = null,
                            GhiChu = null
                        };
                        _context.ItXacNhanCapQuyen8s.Add(xacNhan);
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

                    if (!selectedItNguoiHoTroIds.Any() && form.Danhmuc == "Cấp quyền ổ chung")
                    {
                        selectedItNguoiHoTroIds = await _context.CongViecIts
                            .Where(cv => cv.Ten == "Cấp quyền ổ chung" && cv.IdItNguoiHoTro != null)
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
                    string moTaChiTiet = $"[Khởi tạo đơn] Người tạo: {userName} (ID: {userId}) | Bộ phận: {phongBan} | Công ty: {tenCongTy}\n" +
                                         $"- Danh mục: {form.Danhmuc}\n" +
                                         $"- Thư mục ổ chung: {itOChung?.TenThuMucOchung}\n" +
                                         $"- Quyền đề xuất: {itOChung?.LoaiQuyenYeuCau}\n" +
                                         $"- Bộ phận xác nhận được chỉ định: {BoPhanDuocChon}\n" +
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

                    TempData["Success"] = "Gửi đơn đăng ký Cấp quyền ổ chung thành công!";
                    return Redirect("/FormIT/DonCho");
                }
                catch (Exception ex)
                {
                    await transaction.RollbackAsync();

                    // Load lại dữ liệu khi lỗi
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
                            CongViecIts = x.CongViecIts.Where(cv => cv.Ten == "Cấp quyền ổ chung").ToList()
                        })
                        .Where(x => x.CongViecIts.Any())
                        .ToList();

                    ViewBag.ListPhongBan = _context.Users
                        .Where(u => !string.IsNullOrEmpty(u.PhongBan))
                        .Select(u => u.PhongBan)
                        .Distinct()
                        .ToList();

                    ModelState.AddModelError("", "Lỗi trong quá trình lưu: " + ex.Message);
                    return View(form);
                }
            }
        }
        #endregion

        #region CHI TIẾT ĐƠN FORM IT (TẤT CẢ LOẠI ĐƠN)

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
            var phongBanDon = User.FindFirst("PhongBan")?.Value ?? "";
            var listBoPhan = User.FindFirst("TenBoPhan")?.Value ?? ""; // Chuỗi chứa nhiều bộ phận: "IT, HR, Kế toán"

            // 2. Lấy dữ liệu đơn
            var don = await _context.FormIts
                .Include(f => f.ItMail1s)
                .Include(f => f.ItOrderIt2s)
                .Include(f => f.ItDangKiSuDungWifi3s)
                .Include(f => f.ItDangKiSuDungDtban4s)
                .Include(f => f.ItDangKiTaiKhoanHeThong5s)
                .Include(f => f.ItDangkiTaiKhoanMayTinh6s)
                .Include(f => f.ItDonLapDatThietBi7s)
                .Include(f => f.ItCapQuyenOchung8s)
                    .ThenInclude(c => c.ItXacNhanCapQuyen8s)
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
            else if (User.IsInRole("QuanLyDuyetDonIT"))
            {
                // A. Check quyền duyệt của Quản lý trực tiếp người làm đơn
                bool isSameCompany = string.Equals(don.TenCongTy?.Trim(), tenCongTyUser, StringComparison.OrdinalIgnoreCase);
                bool isSameDepartment = false;

                if (!string.IsNullOrEmpty(don.BoPhan))
                {
                    // Lọc chính xác bằng mảng thay vì dùng Contains để tránh lỗi "IT" nằm trong "Kế toán IT"
                    if (!string.IsNullOrEmpty(listBoPhan))
                    {
                        var arrBoPhan = listBoPhan.Split(',').Select(x => x.Trim());
                        isSameDepartment = arrBoPhan.Contains(don.BoPhan.Trim(), StringComparer.OrdinalIgnoreCase);
                    }

                    // Nếu chưa khớp trong bảng UserBoPhan, check lại Phòng Ban chính trong bảng User
                    if (!isSameDepartment)
                    {
                        isSameDepartment = string.Equals(don.BoPhan.Trim(), phongBanDon.Trim(), StringComparison.OrdinalIgnoreCase);
                    }
                }

                if (isSameCompany && isSameDepartment)
                {
                    isAllowed = true;
                }

                // B. Check quyền của Quản lý bộ phận liên đới được gán xác nhận (Dành riêng cho Đơn 8)
                if (!isAllowed && don.IdForm == "IT_CapQuyenOChung_8" && don.ItCapQuyenOchung8s.Any())
                {
                    var xacNhanOChung = don.ItCapQuyenOchung8s.First().ItXacNhanCapQuyen8s.FirstOrDefault();
                    if (xacNhanOChung != null && !string.IsNullOrEmpty(xacNhanOChung.BoPhan))
                    {
                        string targetBoPhan = xacNhanOChung.BoPhan.Trim(); // Dạng: "Công ty A - IT"
                        string primaryBoPhanStr = (tenCongTyUser + " - " + phongBanDon).Trim();

                        // 1. Kiểm tra với Phòng Ban chính
                        bool isMatchXacNhan = string.Equals(targetBoPhan, primaryBoPhanStr, StringComparison.OrdinalIgnoreCase);

                        // 2. Kiểm tra với danh sách Phòng Ban phụ (Bảng UserBoPhan)
                        if (!isMatchXacNhan && !string.IsNullOrEmpty(listBoPhan))
                        {
                            var arrBoPhanPhu = listBoPhan.Split(',').Select(x => x.Trim());
                            foreach (var bp in arrBoPhanPhu)
                            {
                                string phuBoPhanStr = (tenCongTyUser + " - " + bp).Trim();
                                if (string.Equals(targetBoPhan, phuBoPhanStr, StringComparison.OrdinalIgnoreCase))
                                {
                                    isMatchXacNhan = true;
                                    break;
                                }
                            }
                        }

                        if (isMatchXacNhan)
                        {
                            isAllowed = true;
                        }
                    }
                }
            }

            if (!isAllowed)
            {
                return Forbid();
            }

            // 4. Xử lý dữ liệu hiển thị
            if (don.LichSuFormIts != null)
            {
                don.LichSuFormIts = don.LichSuFormIts.OrderByDescending(x => x.Time).ToList();
            }

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

        #endregion

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

        // --- ACTION CHỈ ĐỊNH / THAY ĐỔI NGƯỜI HỖ TRỢ (Giữ nguyên 100%) ---
        [HttpPost("/FormIT/ThemNguoiHoTro")]
        public async Task<IActionResult> ThemNguoiHoTro([FromBody] System.Text.Json.JsonElement data)
        {
            var roles = User.FindAll(System.Security.Claims.ClaimTypes.Role).Select(r => r.Value).ToList();
            if (!roles.Any(r => r == "AdminIT" || r == "All"))
            {
                return Json(new { success = false, message = "Bạn không có quyền thực hiện thao tác này!" });
            }

            try
            {
                int idForm = data.GetProperty("idFormIt").GetInt32();
                string maNvMoi = data.GetProperty("maNv").GetString() ?? "";

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

                // =========================================================================
                // MỚI: KIỂM TRA VÀ XÓA BẢN GHI CŨ CỦA NGƯỜI NÀY TRONG ĐƠN ĐỂ TRÁNH LẶP TÊN
                // =========================================================================
                var banGhiCu = await _context.ItCtNguoiHoTros
                    .FirstOrDefaultAsync(x => x.IdFormIt == idForm && x.IdItNguoiHoTro == nvIt.Id);

                if (banGhiCu != null)
                {
                    _context.ItCtNguoiHoTros.Remove(banGhiCu);
                }
                // =========================================================================

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
                    Mota = $"{(User.Identity?.Name ?? "Hệ thống")} đã thay đổi người hỗ trợ sang: {nvIt.Ten}.",
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

        #region Xuất file Excel, Word, PDF cho hệ thống IT

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
                .Include(f => f.ItDonLapDatThietBi7s)
                .FirstOrDefaultAsync(m => m.Id == id);

            if (don == null || don.IdAdmin == null)
                return NotFound("Đơn chưa hoàn tất xử lý hoặc không tồn tại.");

            using (var workbook = new ClosedXML.Excel.XLWorkbook())
            {
                var worksheet = workbook.Worksheets.Add("ChiTietDonIT");

                // Header chung
                worksheet.Cell(1, 1).Value = "HỆ THỐNG E-FORM IT - PHÒNG CÔNG NGHỆ THÔNG TIN - BEST PACIFIC";
                worksheet.Range("A1:E1").Merge().Style.Font.SetBold().Font.FontSize = 14;
                worksheet.Range("A1:E1").Style.Alignment.SetHorizontal(ClosedXML.Excel.XLAlignmentHorizontalValues.Center);

                worksheet.Cell(3, 1).Value = "Mã Đơn:"; worksheet.Cell(3, 2).Value = don.Id;
                worksheet.Cell(4, 1).Value = "Tên Form:"; worksheet.Cell(4, 2).Value = don.TenForm;
                worksheet.Cell(5, 1).Value = "Mã NV:"; worksheet.Cell(5, 2).Value = don.SoNhanVien;
                worksheet.Cell(6, 1).Value = "Họ Tên:"; worksheet.Cell(6, 2).Value = don.TenNguoiNv;
                worksheet.Cell(7, 1).Value = "Bộ Phận:"; worksheet.Cell(7, 2).Value = don.BoPhan;
                worksheet.Cell(8, 1).Value = "Ngày Tạo:"; worksheet.Cell(8, 2).Value = don.TimeNguoiTao?.ToString("dd/MM/yyyy HH:mm");
                worksheet.Cell(9, 1).Value = "Trạng Thái:"; worksheet.Cell(9, 2).Value = "HOÀN TẤT (IT)";

                var rangeChung = worksheet.Range("A3:B9");
                rangeChung.Style.Border.SetOutsideBorder(ClosedXML.Excel.XLBorderStyleValues.Thin);
                rangeChung.Style.Border.SetInsideBorder(ClosedXML.Excel.XLBorderStyleValues.Thin);
                worksheet.Range("A3:A9").Style.Font.SetBold().Fill.SetBackgroundColor(ClosedXML.Excel.XLColor.LightGray);

                int currentRow = 11;

                // Xử lý nạp dữ liệu chi tiết cho 7 loại đơn hệ thống IT trên Excel
                if (don.ItMail1s.Any())
                {
                    var ct = don.ItMail1s.First();
                    worksheet.Cell(currentRow, 1).Value = "I. CHI TIẾT ĐƠN: ĐĂNG KÝ EMAIL (FORM 1)";
                    worksheet.Range(currentRow, 1, currentRow, 2).Merge().Style.Font.SetBold().Fill.SetBackgroundColor(ClosedXML.Excel.XLColor.LightBlue);
                    currentRow++;
                    worksheet.Cell(currentRow, 1).Value = "Loại yêu cầu:"; worksheet.Cell(currentRow, 2).Value = ct.LoaiYeuCau; currentRow++;
                    worksheet.Cell(currentRow, 1).Value = "Người sử dụng:"; worksheet.Cell(currentRow, 2).Value = ct.NguoiSuDung; currentRow++;
                    worksheet.Cell(currentRow, 1).Value = "Địa chỉ Email:"; worksheet.Cell(currentRow, 2).Value = ct.Email; currentRow++;
                    worksheet.Cell(currentRow, 1).Value = "Vị trí công việc / Số nội bộ:"; worksheet.Cell(currentRow, 2).Value = $"{ct.ViTri} / {ct.SoNoiBo}"; currentRow++;
                    worksheet.Cell(currentRow, 1).Value = "Số điện thoại:"; worksheet.Cell(currentRow, 2).Value = ct.SoDienThoai; currentRow++;
                    worksheet.Cell(currentRow, 1).Value = "Nhóm Email đề xuất:"; worksheet.Cell(currentRow, 2).Value = ct.NhomEmail; currentRow++;
                    worksheet.Cell(currentRow, 1).Value = "Quyền gửi ra ngoài / Trên PC:"; worksheet.Cell(currentRow, 2).Value = $"Ra ngoài: {ct.GuiRaNgoai} / Máy tính: {ct.SuDungTrenMayTinh}"; currentRow++;
                    worksheet.Cell(currentRow, 1).Value = "Sử dụng trên Phone / Webmail:"; worksheet.Cell(currentRow, 2).Value = $"Phone: {ct.SuDungTrenDienThoai} / Webmail: {ct.SuDungWebMail}"; currentRow++;
                    worksheet.Cell(currentRow, 1).Value = "Mục đích sử dụng:"; worksheet.Cell(currentRow, 2).Value = ct.MucDich; currentRow++;
                }
                else if (don.ItOrderIt2s.Any())
                {
                    var ct = don.ItOrderIt2s.First();
                    worksheet.Cell(currentRow, 1).Value = "I. CHI TIẾT ĐƠN: YÊU CẦU THIẾT BỊ / MUA SẮM IT (FORM 2)";
                    worksheet.Range(currentRow, 1, currentRow, 2).Merge().Style.Font.SetBold().Fill.SetBackgroundColor(ClosedXML.Excel.XLColor.LightGreen);
                    currentRow++;
                    worksheet.Cell(currentRow, 1).Value = "Tên thiết bị / Nội dung:"; worksheet.Cell(currentRow, 2).Value = ct.Ten; currentRow++;
                    worksheet.Cell(currentRow, 1).Value = "Ghi chú chi tiết:"; worksheet.Cell(currentRow, 2).Value = ct.GhiChu; currentRow++;
                }
                else if (don.ItDangKiSuDungWifi3s.Any())
                {
                    var ct = don.ItDangKiSuDungWifi3s.First();
                    worksheet.Cell(currentRow, 1).Value = "I. CHI TIẾT ĐƠN: ĐĂNG KÝ SỬ DỤNG WIFI (FORM 3)";
                    worksheet.Range(currentRow, 1, currentRow, 2).Merge().Style.Font.SetBold().Fill.SetBackgroundColor(ClosedXML.Excel.XLColor.LightYellow);
                    currentRow++;
                    worksheet.Cell(currentRow, 1).Value = "Loại đơn / Mã thiết bị:"; worksheet.Cell(currentRow, 2).Value = $"{ct.LoaiDon} / {ct.MaThietBi}"; currentRow++;
                    worksheet.Cell(currentRow, 1).Value = "Loại thiết bị / Địa chỉ MAC:"; worksheet.Cell(currentRow, 2).Value = $"{ct.LoaiThietBi} / {ct.MacTb}"; currentRow++;
                    worksheet.Cell(currentRow, 1).Value = "Loại thời gian đăng ký:"; worksheet.Cell(currentRow, 2).Value = ct.LoaiThoiGian; currentRow++;
                    worksheet.Cell(currentRow, 1).Value = "Thời gian bắt đầu:"; worksheet.Cell(currentRow, 2).Value = ct.ThoiGianBatDau?.ToString("dd/MM/yyyy HH:mm"); currentRow++;
                    worksheet.Cell(currentRow, 1).Value = "Thời gian kết thúc:"; worksheet.Cell(currentRow, 2).Value = ct.ThoiGianKetThuc?.ToString("dd/MM/yyyy HH:mm"); currentRow++;
                    worksheet.Cell(currentRow, 1).Value = "Lý do đề xuất:"; worksheet.Cell(currentRow, 2).Value = ct.LyDo; currentRow++;
                    worksheet.Cell(currentRow, 1).Value = "Ghi chú thêm:"; worksheet.Cell(currentRow, 2).Value = ct.GhiChu; currentRow++;
                }
                else if (don.ItDangKiSuDungDtban4s.Any())
                {
                    var ct = don.ItDangKiSuDungDtban4s.First();
                    worksheet.Cell(currentRow, 1).Value = "I. CHI TIẾT ĐƠN: ĐĂNG KÝ ĐIỆN THOẠI BÀN (FORM 4)";
                    worksheet.Range(currentRow, 1, currentRow, 2).Merge().Style.Font.SetBold().Fill.SetBackgroundColor(ClosedXML.Excel.XLColor.Orange);
                    currentRow++;
                    worksheet.Cell(currentRow, 1).Value = "Thông tin điện thoại bàn:"; worksheet.Cell(currentRow, 2).Value = ct.ThongTin; currentRow++;
                    worksheet.Cell(currentRow, 1).Value = "Mục đích đề xuất:"; worksheet.Cell(currentRow, 2).Value = ct.MucDich; currentRow++;
                }
                else if (don.ItDangKiTaiKhoanHeThong5s.Any())
                {
                    var ct = don.ItDangKiTaiKhoanHeThong5s.First();
                    worksheet.Cell(currentRow, 1).Value = "I. CHI TIẾT ĐƠN: ĐĂNG KÝ TÀI KHOẢN HỆ THỐNG (FORM 5)";
                    worksheet.Range(currentRow, 1, currentRow, 2).Merge().Style.Font.SetBold().Fill.SetBackgroundColor(ClosedXML.Excel.XLColor.Pink);
                    currentRow++;
                    worksheet.Cell(currentRow, 1).Value = "Loại đơn đăng ký:"; worksheet.Cell(currentRow, 2).Value = ct.LoaiDon; currentRow++;
                    worksheet.Cell(currentRow, 1).Value = "Đăng ký cấp cho ai:"; worksheet.Cell(currentRow, 2).Value = ct.DangKiChoAi; currentRow++;
                    worksheet.Cell(currentRow, 1).Value = "Hệ thống yêu cầu:"; worksheet.Cell(currentRow, 2).Value = ct.HeThongNao; currentRow++;
                    worksheet.Cell(currentRow, 1).Value = "Cấp phân quyền giống ai:"; worksheet.Cell(currentRow, 2).Value = ct.CapQuyenGiongAi; currentRow++;
                    worksheet.Cell(currentRow, 1).Value = "Sử dụng trên máy nào:"; worksheet.Cell(currentRow, 2).Value = ct.DungTrenMayNao; currentRow++;
                    worksheet.Cell(currentRow, 1).Value = "Mô tả chi tiết quyền:"; worksheet.Cell(currentRow, 2).Value = ct.MoTaChiTiet; currentRow++;
                    worksheet.Cell(currentRow, 1).Value = "Mục đích sử dụng:"; worksheet.Cell(currentRow, 2).Value = ct.MucDich; currentRow++;
                }
                else if (don.ItDangkiTaiKhoanMayTinh6s.Any())
                {
                    var ct = don.ItDangkiTaiKhoanMayTinh6s.First();
                    worksheet.Cell(currentRow, 1).Value = "I. CHI TIẾT ĐƠN: TÀI KHOẢN MÁY TÍNH / THƯ MỤC CHUNG (FORM 6)";
                    worksheet.Range(currentRow, 1, currentRow, 2).Merge().Style.Font.SetBold().Fill.SetBackgroundColor(ClosedXML.Excel.XLColor.LightSlateGray);
                    currentRow++;
                    worksheet.Cell(currentRow, 1).Value = "Phân loại phân quyền:"; worksheet.Cell(currentRow, 2).Value = ct.Loai; currentRow++;
                    worksheet.Cell(currentRow, 1).Value = "Nhân viên sử dụng (Họ tên - Mã NV):"; worksheet.Cell(currentRow, 2).Value = ct.HoTenVaMaNhanVienNguoiSuDung; currentRow++;
                    worksheet.Cell(currentRow, 1).Value = "Chức vụ / Số nội bộ:"; worksheet.Cell(currentRow, 2).Value = $"{ct.ChucVu} / {ct.SoNoiBo}"; currentRow++;
                    worksheet.Cell(currentRow, 1).Value = "Số điện thoại di động:"; worksheet.Cell(currentRow, 2).Value = ct.SoDienThoai; currentRow++;
                    worksheet.Cell(currentRow, 1).Value = "Tên máy tính cài đặt quyền:"; worksheet.Cell(currentRow, 2).Value = ct.TenMayCanCai; currentRow++;
                    worksheet.Cell(currentRow, 1).Value = "Tên ổ đĩa / Thư mục chung cần add quyền:"; worksheet.Cell(currentRow, 2).Value = ct.TenOchungCanAddQuyen; currentRow++;
                    worksheet.Cell(currentRow, 1).Value = "Mục đích yêu cầu:"; worksheet.Cell(currentRow, 2).Value = ct.MucDich; currentRow++;
                    worksheet.Cell(currentRow, 1).Value = "Ghi chú thêm:"; worksheet.Cell(currentRow, 2).Value = ct.GhiChu; currentRow++;
                }
                else if (don.ItDonLapDatThietBi7s.Any())
                {
                    var ct = don.ItDonLapDatThietBi7s.First();
                    worksheet.Cell(currentRow, 1).Value = "I. CHI TIẾT ĐƠN: ĐĂNG KÝ LẤP ĐẶT THIẾT BỊ IT (FORM 7)";
                    worksheet.Range(currentRow, 1, currentRow, 2).Merge().Style.Font.SetBold().Fill.SetBackgroundColor(ClosedXML.Excel.XLColor.Khaki);
                    currentRow++;
                    worksheet.Cell(currentRow, 1).Value = "Tên thiết bị đề xuất:"; worksheet.Cell(currentRow, 2).Value = ct.TenThietBi; currentRow++;
                    worksheet.Cell(currentRow, 1).Value = "Vị trí lắp đặt chi tiết:"; worksheet.Cell(currentRow, 2).Value = ct.ViTri; currentRow++;
                    worksheet.Cell(currentRow, 1).Value = "Mục đích sử dụng yêu cầu:"; worksheet.Cell(currentRow, 2).Value = ct.MucDich; currentRow++;
                }

                if (currentRow > 11)
                {
                    var rangeChiTiet = worksheet.Range(11, 1, currentRow - 1, 2);
                    rangeChiTiet.Style.Border.SetOutsideBorder(ClosedXML.Excel.XLBorderStyleValues.Thin);
                    rangeChiTiet.Style.Border.SetInsideBorder(ClosedXML.Excel.XLBorderStyleValues.Thin);
                    worksheet.Range(12, 1, currentRow - 1, 1).Style.Font.SetBold().Fill.SetBackgroundColor(ClosedXML.Excel.XLColor.WhiteSmoke);
                }

                worksheet.Columns().AdjustToContents();

                using (var stream = new System.IO.MemoryStream())
                {
                    workbook.SaveAs(stream);
                    return File(stream.ToArray(), "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", $"Don_EFormIT_{id}.xlsx");
                }
            }
        }

        [HttpGet("/FormIT/ExportWord/{id}")]
        public async Task<IActionResult> ExportWord(int id)
        {
            var don = await _context.FormIts
                .Include(f => f.ItMail1s)
                .Include(f => f.ItOrderIt2s)
                .Include(f => f.ItDangKiSuDungWifi3s)
                .Include(f => f.ItDangKiSuDungDtban4s)
                .Include(f => f.ItDangKiTaiKhoanHeThong5s)
                .Include(f => f.ItDangkiTaiKhoanMayTinh6s)
                .Include(f => f.ItDonLapDatThietBi7s)
                .FirstOrDefaultAsync(m => m.Id == id);

            if (don == null || don.IdAdmin == null)
                return NotFound("Đơn chưa hoàn tất hoặc không tồn tại.");

            string htmlContent = BuildHtmlContentIT(don, isForWord: true);

            byte[] byteArray = System.Text.Encoding.UTF8.GetBytes(htmlContent);
            return File(byteArray, "application/msword", $"Don_EFormIT_{id}.doc");
        }

        [HttpGet("/FormIT/ExportPDF/{id}")]
        public async Task<IActionResult> ExportPDF(int id)
        {
            var don = await _context.FormIts
                .Include(f => f.ItMail1s)
                .Include(f => f.ItOrderIt2s)
                .Include(f => f.ItDangKiSuDungWifi3s)
                .Include(f => f.ItDangKiSuDungDtban4s)
                .Include(f => f.ItDangKiTaiKhoanHeThong5s)
                .Include(f => f.ItDangkiTaiKhoanMayTinh6s)
                .Include(f => f.ItDonLapDatThietBi7s)
                .FirstOrDefaultAsync(m => m.Id == id);

            if (don == null || don.IdAdmin == null)
                return NotFound("Đơn chưa hoàn tất hoặc không tồn tại.");

            string htmlContent = BuildHtmlContentIT(don, isForWord: false);
            return Content(htmlContent, "text/html", System.Text.Encoding.UTF8);
        }

        // ============================================================
        // HÀM HỖ TRỢ BUILD HTML CHUYÊN NGHIỆP CHO IT (HIỂN THỊ ĐẦY ĐỦ 7 LOẠI ĐƠN CHẤT LƯỢNG)
        // ============================================================
        private string BuildHtmlContentIT(FormIt don, bool isForWord = false)
        {
            var sb = new System.Text.StringBuilder();

            sb.Append("<html><head><meta charset='utf-8'/>");
            sb.Append("<style>");
            sb.Append(@"
                body { font-family: 'Times New Roman', Times, serif; line-height: 1.5; margin: 0; padding: 0; color: #000; background: #fff; }
                .document-container { max-width: 850px; margin: 0 auto; padding: 40px; background: #fff; }
                .header-table { width: 100%; border: none; margin-bottom: 20px; text-align: center; }
                .header-table td { border: none; padding: 0; }
                .company-name { font-size: 14pt; font-weight: bold; text-transform: uppercase; }
                .company-sub { font-size: 12pt; font-weight: bold; text-decoration: underline; margin-bottom: 10px; }
                .national-title { font-size: 14pt; font-weight: bold; text-transform: uppercase; }
                .national-sub { font-size: 13pt; font-weight: bold; text-decoration: underline; }
                .form-title { font-size: 20pt; font-weight: bold; text-align: center; text-transform: uppercase; margin: 25px 0 5px 0; }
                .form-id { font-size: 12pt; text-align: center; font-style: italic; margin-bottom: 30px; }
                .section-title { font-size: 14pt; font-weight: bold; margin-top: 25px; margin-bottom: 10px; text-transform: uppercase; border-bottom: 2px solid #000; padding-bottom: 5px; }
                .data-table { width: 100%; border-collapse: collapse; margin-bottom: 20px; }
                .data-table th, .data-table td { border: 1px solid #000; padding: 8px 12px; font-size: 12pt; vertical-align: top; }
                .data-table th { background-color: #f2f2f2; font-weight: bold; text-align: left; width: 35%; }
                .signature-table { width: 100%; text-align: center; margin-top: 40px; border: none; table-layout: fixed; page-break-inside: avoid; }
                .signature-table td { vertical-align: top; border: none; font-size: 12pt; padding: 5px; word-wrap: break-word; }

                /* ĐỊNH DẠNG KHUNG CHỮ KÝ ĐIỆN TỬ THEO ẢNH MẪU KHUNG CHUẨN ĐỒNG BỘ VỚI HỆ THỐNG */
                .digital-signature-box { 
                    border: 1px solid #2e7d32; 
                    padding: 8px; 
                    text-align: left; 
                    background-color: #f1f8e9; 
                    margin: 10px auto 0 auto; 
                    display: block; 
                    width: 98%;
                    max-width: 190px;
                    position: relative;
                    box-sizing: border-box;
                }
                .sig-status { 
                    color: #2e7d32; 
                    font-size: 10.5pt; 
                    font-weight: bold; 
                    margin-bottom: 3px;
                }
                .sig-info { 
                    font-size: 9pt; 
                    color: #202124; 
                    line-height: 1.35;
                }
                .sig-check-mark {
                    position: absolute;
                    right: 6px;
                    bottom: 4px;
                    font-size: 18pt;
                    font-weight: bold;
                    color: rgba(46, 125, 50, 0.25);
                }
            ");

            if (!isForWord)
            {
                sb.Append("@page { size: A4; margin: 20mm; } ");
                sb.Append("@media print { .document-container { padding: 0; } } ");
                sb.Append("</style><script>window.onload = function() { window.print(); }</script></head><body>");
            }
            else
            {
                sb.Append("</style></head><body>");
            }

            sb.Append("<div class='document-container'>");

            // Phần Quốc hiệu và Tên bộ phận IT
            sb.Append("<table class='header-table'><tr>");
            sb.Append("<td style='width:45%;'><div class='company-name'>BEST PACIFIC</div><div class='company-sub'>PHÒNG CÔNG NGHỆ THÔNG TIN (IT)</div></td>");
            sb.Append("<td style='width:55%;'><div class='national-title'>CỘNG HÒA XÃ HỘI CHỦ NGHĨA VIỆT NAM</div><div class='national-sub'>Độc lập - Tự do - Hạnh phúc</div></td>");
            sb.Append("</tr></table>");

            // Tên đơn
            sb.Append($"<div class='form-title'>{don.TenForm}</div>");
            sb.Append($"<div class='form-id'>Mã phiếu: #{don.Id} | Trạng thái: ĐÃ HOÀN TẤT</div>");

            // I. THÔNG TIN NGƯỜI TẠO
            sb.Append("<div class='section-title'>I. THÔNG TIN NHÂN VIÊN ĐĂNG KÝ</div>");
            sb.Append("<table class='data-table'>");
            sb.Append($"<tr><th>Họ và tên nhân viên</th><td>{don.TenNguoiNv}</td></tr>");
            sb.Append($"<tr><th>Mã nhân viên</th><td>{don.SoNhanVien}</td></tr>");
            sb.Append($"<tr><th>Bộ phận / Phòng ban</th><td>{don.BoPhan}</td></tr>");
            sb.Append($"<tr><th>Thời gian lập đơn</th><td>{don.TimeNguoiTao?.ToString("dd/MM/yyyy HH:mm")}</td></tr>");
            sb.Append("</table>");

            // II. CHI TIẾT DỮ LIỆU ĐỘNG CỦA 7 LOẠI BIỂU MẪU IT ĐẦY ĐỦ KHÔNG BỎ SÓT
            sb.Append("<div class='section-title'>II. NỘI DUNG CHI TIẾT YÊU CẦU</div>");
            sb.Append("<table class='data-table'>");

            if (don.ItMail1s.Any())
            {
                var ct = don.ItMail1s.First();
                sb.Append($"<tr><th>Loại dịch vụ IT</th><td style='font-weight:bold;'>Đăng ký / Thay đổi tài khoản Email (Form 1)</td></tr>");
                sb.Append($"<tr><th>Loại yêu cầu hệ thống</th><td>{ct.LoaiYeuCau}</td></tr>");
                sb.Append($"<tr><th>Họ tên người sử dụng</th><td>{ct.NguoiSuDung}</td></tr>");
                sb.Append($"<tr><th>Địa chỉ Email</th><td>{ct.Email}</td></tr>");
                sb.Append($"<tr><th>Vị trí công việc / Cấp bậc</th><td>{ct.ViTri}</td></tr>");
                sb.Append($"<tr><th>Số nội bộ (Extension)</th><td>{ct.SoNoiBo}</td></tr>");
                sb.Append($"<tr><th>Số điện thoại liên hệ</th><td>{ct.SoDienThoai}</td></tr>");
                sb.Append($"<tr><th>Nhóm Email đề xuất (Mailing list)</th><td>{ct.NhomEmail}</td></tr>");
                sb.Append($"<tr><th>Quyền nhận gửi mail ra ngoài</th><td>{ct.GuiRaNgoai}</td></tr>");
                sb.Append($"<tr><th>Sử dụng trên máy tính cty</th><td>{ct.SuDungTrenMayTinh}</td></tr>");
                sb.Append($"<tr><th>Sử dụng Outlook di động</th><td>{ct.SuDungTrenDienThoai}</td></tr>");
                sb.Append($"<tr><th>Quyền truy cập Webmail</th><td>{ct.SuDungWebMail}</td></tr>");
                sb.Append($"<tr><th>Mục đích đề xuất chi tiết</th><td>{ct.MucDich}</td></tr>");
            }
            else if (don.ItOrderIt2s.Any())
            {
                var ct = don.ItOrderIt2s.First();
                sb.Append($"<tr><th>Loại dịch vụ IT</th><td style='font-weight:bold;'>Yêu cầu cấp phát vật tư / Linh kiện thiết bị IT (Form 2)</td></tr>");
                sb.Append($"<tr><th>Tên hạng mục thiết bị yêu cầu</th><td>{ct.Ten}</td></tr>");
                sb.Append($"<tr><th>Nội dung ghi chú chi tiết</th><td>{ct.GhiChu}</td></tr>");
            }
            else if (don.ItDangKiSuDungWifi3s.Any())
            {
                var ct = don.ItDangKiSuDungWifi3s.First();
                sb.Append($"<tr><th>Loại dịch vụ IT</th><td style='font-weight:bold;'>Đăng ký truy cập mạng không dây Wifi (Form 3)</td></tr>");
                sb.Append($"<tr><th>Loại đơn / Mã thiết bị</th><td>{ct.LoaiDon} / {ct.MaThietBi}</td></tr>");
                sb.Append($"<tr><th>Tên thiết bị / Địa chỉ vật lý MAC</th><td>{ct.LoaiThietBi} - [MAC: {ct.MacTb}]</td></tr>");
                sb.Append($"<tr><th>Chế độ đăng ký sử dụng</th><td>{ct.LoaiThoiGian}</td></tr>");
                sb.Append($"<tr><th>Thời gian hiệu lực dùng</th><td>Từ: {ct.ThoiGianBatDau?.ToString("dd/MM/yyyy HH:mm")} đến: {ct.ThoiGianKetThuc?.ToString("dd/MM/yyyy HH:mm")}</td></tr>");
                sb.Append($"<tr><th>Lý do đăng ký sử dụng</th><td>{ct.LyDo}</td></tr>");
                sb.Append($"<tr><th>Ghi chú đi kèm</th><td>{ct.GhiChu}</td></tr>");
            }
            else if (don.ItDangKiSuDungDtban4s.Any())
            {
                var ct = don.ItDangKiSuDungDtban4s.First();
                sb.Append($"<tr><th>Loại dịch vụ IT</th><td style='font-weight:bold;'>Đăng ký lắp đặt / Phân quyền điện thoại bàn (Form 4)</td></tr>");
                sb.Append($"<tr><th>Thông tin kỹ thuật thiết bị</th><td>{ct.ThongTin}</td></tr>");
                sb.Append($"<tr><th>Mục đích sử dụng chi tiết</th><td>{ct.MucDich}</td></tr>");
            }
            else if (don.ItDangKiTaiKhoanHeThong5s.Any())
            {
                var ct = don.ItDangKiTaiKhoanHeThong5s.First();
                sb.Append($"<tr><th>Loại dịch vụ IT</th><td style='font-weight:bold;'>Đăng ký cấp quyền tài khoản phần mềm hệ thống (Form 5)</td></tr>");
                sb.Append($"<tr><th>Loại đơn</th><td>{ct.LoaiDon}</td></tr>");
                sb.Append($"<tr><th>Đăng ký sử dụng cho nhân sự</th><td>{ct.DangKiChoAi}</td></tr>");
                sb.Append($"<tr><th>Tên hệ thống / Phần mềm đăng ký</th><td>{ct.HeThongNao}</td></tr>");
                sb.Append($"<tr><th>Cấu hình phân quyền nhân bản giống ai</th><td>{ct.CapQuyenGiongAi}</td></tr>");
                sb.Append($"<tr><th>Sử dụng cố định trên trạm máy tính nào</th><td>{ct.DungTrenMayNao}</td></tr>");
                sb.Append($"<tr><th>Mô tả chi tiết phân quyền yêu cầu</th><td>{ct.MoTaChiTiet}</td></tr>");
                sb.Append($"<tr><th>Mục đích nghiệp vụ</th><td>{ct.MucDich}</td></tr>");
            }
            else if (don.ItDangkiTaiKhoanMayTinh6s.Any())
            {
                var ct = don.ItDangkiTaiKhoanMayTinh6s.First();
                sb.Append($"<tr><th>Loại dịch vụ IT</th><td style='font-weight:bold;'>Đăng ký tài khoản Windows / Cấp quyền thư mục chung Server (Form 6)</td></tr>");
                sb.Append($"<tr><th>Hành động yêu cầu</th><td>{ct.Loai}</td></tr>");
                sb.Append($"<tr><th>Thông tin nhân viên (Tên - Mã số)</th><td>{ct.HoTenVaMaNhanVienNguoiSuDung}</td></tr>");
                sb.Append($"<tr><th>Chức vụ đảm nhiệm</th><td>{ct.ChucVu}</td></tr>");
                sb.Append($"<tr><th>Số điện thoại / Số nội bộ liên lạc</th><td>Phone: {ct.SoDienThoai} / Ext: {ct.SoNoiBo}</td></tr>");
                sb.Append($"<tr><th>Tên máy trạm Windows cần cài đặt</th><td>{ct.TenMayCanCai}</td></tr>");
                sb.Append($"<tr><th>Tên thư mục chung Server cần add quyền</th><td>{ct.TenOchungCanAddQuyen}</td></tr>");
                sb.Append($"<tr><th>Mục đích công việc</th><td>{ct.MucDich}</td></tr>");
                sb.Append($"<tr><th>Ghi chú thêm</th><td>{ct.GhiChu}</td></tr>");
            }
            else if (don.ItDonLapDatThietBi7s.Any())
            {
                var ct = don.ItDonLapDatThietBi7s.First();
                sb.Append($"<tr><th>Loại dịch vụ IT</th><td style='font-weight:bold;'>Đăng ký lắp đặt hạ tầng thiết bị phần cứng IT (Form 7)</td></tr>");
                sb.Append($"<tr><th>Tên thiết bị phần cứng đề xuất</th><td>{ct.TenThietBi}</td></tr>");
                sb.Append($"<tr><th>Vị trí lắp đặt chi tiết</th><td>{ct.ViTri}</td></tr>");
                sb.Append($"<tr><th>Mục đích sử dụng yêu cầu</th><td>{ct.MucDich}</td></tr>");
            }

            sb.Append("</table>");

            // III. KHỐI CHỮ KÝ XÁC NHẬN CỐ ĐỊNH CHO 3 BÊN (Người lập phiếu, Quản lý trực tiếp, Phòng IT xác nhận)
            double colPercent = 100.0 / 3.0;

            sb.Append("<table class='signature-table'><tr>");

            // 1. CỘT NGƯỜI LẬP PHIẾU
            sb.Append($"<td style='width:{colPercent}%;'><strong>NGƯỜI LẬP PHIẾU</strong><br/><span style='font-size:9pt;'>(Chữ ký điện tử)</span><br/>");
            if (don.TimeNguoiTao.HasValue)
            {
                sb.Append("<div class='digital-signature-box'>");
                sb.Append("<div class='sig-status'>Signature Valid</div>");
                sb.Append($"<div class='sig-info'>Ký bởi: {don.TenNguoiTao}<br/>Ký ngày: {don.TimeNguoiTao?.ToString("dd/MM/yyyy")}</div>");
                sb.Append("<div class='sig-check-mark'>✓</div>");
                sb.Append("</div>");
            }
            else
            {
                sb.Append("<br/><br/><br/><br/><strong>" + don.TenNguoiTao + "</strong>");
            }
            sb.Append("</td>");

            // 2. CỘT QUẢN LÝ TRỰC TIẾP
            sb.Append($"<td style='width:{colPercent}%;'><strong>QUẢN LÝ TRỰC TIẾP</strong><br/><span style='font-size:9pt;'>(Chữ ký điện tử)</span><br/>");
            if (don.TimeNguoiDuyet.HasValue)
            {
                sb.Append("<div class='digital-signature-box'>");
                sb.Append("<div class='sig-status'>Signature Valid</div>");
                sb.Append($"<div class='sig-info'>Ký bởi: {don.TenNguoiDuyet}<br/>Ký ngày: {don.TimeNguoiDuyet?.ToString("dd/MM/yyyy")}</div>");
                sb.Append("<div class='sig-check-mark'>✓</div>");
                sb.Append("</div>");
            }
            else
            {
                sb.Append("<br/><br/><br/><br/><strong>" + (don.TenNguoiDuyet ?? "") + "</strong>");
            }
            sb.Append("</td>");

            // 3. CỘT PHÒNG IT XÁC NHẬN
            sb.Append($"<td style='width:{colPercent}%;'><strong>PHÒNG IT XÁC NHẬN</strong><br/><span style='font-size:9pt;'>(Chữ ký điện tử)</span><br/>");
            if (don.TimeAdmin.HasValue)
            {
                sb.Append("<div class='digital-signature-box'>");
                sb.Append("<div class='sig-status'>Signature Valid</div>");
                sb.Append($"<div class='sig-info'>Ký bởi: {don.TenAdmin}<br/>Ký ngày: {don.TimeAdmin?.ToString("dd/MM/yyyy")}</div>");
                sb.Append("<div class='sig-check-mark'>✓</div>");
                sb.Append("</div>");
            }
            else
            {
                sb.Append("<br/><br/><br/><br/><strong>" + (don.TenAdmin ?? "") + "</strong>");
            }
            sb.Append("</td>");

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
                // 1. Kiểm tra ID form an toàn
                if (!int.TryParse(Request.Form["idForm"], out int idForm))
                    return Json(new { success = false, message = "ID đơn không hợp lệ" });

                var noiDung = Request.Form["noiDung"].ToString();
                var file = Request.Form.Files.GetFile("file");

                // 2. Lấy thông tin User an toàn
                var userIdStr = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                if (string.IsNullOrEmpty(userIdStr))
                    return Json(new { success = false, message = "Chưa đăng nhập" });

                var userName = User.Identity?.Name ?? "Unknown";
                var userMa = User.FindFirst(ClaimTypes.Email)?.Value ?? "";
                var userPhongBan = User.FindFirst("PhongBan")?.Value ?? "";
                var userTenCongTy = User.FindFirst("TenCongTy")?.Value ?? "";

                // Kiểm tra đơn có tồn tại không
                var formIt = await _context.FormIts.FindAsync(idForm);
                if (formIt == null)
                    return Json(new { success = false, message = "Không tìm thấy đơn" });

                string? fileName = null;

                // 3. Xử lý file đính kèm
                if (file != null && file.Length > 0)
                {
                    if (file.Length > 50 * 1024 * 1024)
                        return Json(new { success = false, message = "File không được vượt quá 50MB" });

                    string networkPath = @"\\10.0.60.30\BPVN-Fileserver\Public\IT-Information Technology Dept\5.E-Form\BinhLuanDonIT";

                    if (!Directory.Exists(networkPath)) Directory.CreateDirectory(networkPath);

                    fileName = $"{DateTime.Now:yyyyMMdd_HHmmss}_{Guid.NewGuid().ToString().Substring(0, 8)}_{Path.GetFileName(file.FileName)}";
                    string fullPath = Path.Combine(networkPath, fileName);

                    using (var stream = new FileStream(fullPath, FileMode.Create))
                    {
                        await file.CopyToAsync(stream);
                    }
                }

                if (string.IsNullOrWhiteSpace(noiDung) && fileName == null)
                    return Json(new { success = false, message = "Vui lòng nhập nội dung hoặc đính kèm file" });

                // 4. Lưu bình luận
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

                // 5. Tạo lịch sử
                string moTaPreview = string.IsNullOrWhiteSpace(noiDung) ? "[File đính kèm]" :
                                     (noiDung.Length > 50 ? noiDung.Substring(0, 50) + "..." : noiDung);

                var lichSu = new LichSuFormIt
                {
                    IdFormIt = idForm,
                    TieuDe = "BÌNH LUẬN MỚI",
                    Mota = $"👤 {userName} ({userMa})\n🏢 {userPhongBan} - {userTenCongTy}\n💬 {moTaPreview}\n{(fileName != null ? "📎 Có đính kèm file" : "")}",
                    Time = DateTime.Now
                };
                _context.LichSuFormIts.Add(lichSu);

                await _context.SaveChangesAsync();

                return Json(new
                {
                    success = true,
                    data = new
                    {
                        binhLuan.Id,
                        binhLuan.NoiDung,
                        binhLuan.TenNguoiBinhLuan,
                        binhLuan.IdNguoiBinhLuan,
                        binhLuan.Ma,
                        binhLuan.PhongBan,
                        binhLuan.TenCongTy,
                        binhLuan.ThoiGian,
                        binhLuan.FileDinhKem,
                        binhLuan.TrangThai
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
            if (User == null || User.Identity == null || !User.Identity.IsAuthenticated)
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
                .Select(item => new
                {
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
                    // ĐÃ SỬA: Thêm dấu '?' sau IdItNguoiHoTroNavigation để tránh lỗi cảnh báo Null Reference
                    TenNguoiHoTro = item.ItCtNguoiHoTros
                                        .OrderByDescending(x => x.Stt)
                                        .Select(x => x.IdItNguoiHoTroNavigation != null ? x.IdItNguoiHoTroNavigation.Ten : "Chưa có")
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

            if (userRoles.Contains("BaoVe")) return Forbid();

            // Xử lý mảng bộ phận từ Claim
            var listTenBoPhan = listTenBoPhanStr.Split(',')
                                                .Select(s => s.Trim())
                                                .Where(s => !string.IsNullOrEmpty(s))
                                                .ToList();

            // TẠO DANH SÁCH MỤC TIÊU (CÔNG TY - BỘ PHẬN) ĐỂ CHECK CHO ĐƠN 8
            var validTargetBoPhans = new List<string>();
            if (!string.IsNullOrEmpty(tenCongTy))
            {
                if (!string.IsNullOrEmpty(phongBan)) validTargetBoPhans.Add($"{tenCongTy} - {phongBan}");
                foreach (var bp in listTenBoPhan)
                {
                    var target = $"{tenCongTy} - {bp}";
                    if (!validTargetBoPhans.Contains(target)) validTargetBoPhans.Add(target);
                }
            }

            // --- 2. TRUY VẤN DỮ LIỆU ---
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
                bool hasPhu = listTenBoPhan.Any();
                query = query.Where(f =>
                    // Duyệt đơn nội bộ phòng ban của quản lý
                    (hasPhu && f.BoPhan != null && listTenBoPhan.Contains(f.BoPhan)) ||
                    (!hasPhu && f.BoPhan == phongBan) ||
                    // Đơn do chính quản lý tạo
                    (f.IdNguoiTao == userId) ||
                    // MỚI: Hiển thị Đơn 8 nếu quản lý này thuộc Bộ phận liên đới cần xác nhận
                    (f.IdForm == "IT_CapQuyenOChung_8" && f.ItCapQuyenOchung8s.Any(c => c.ItXacNhanCapQuyen8s.Any(x => x.BoPhan != null && validTargetBoPhans.Contains(x.BoPhan))))
                );
            }
            else
            {
                query = query.Where(f => f.IdNguoiTao == userId);
            }

            // --- 4. PROJECT DATA ---
            var danhSachDon = await query
                .OrderByDescending(f => f.Id)
                .Select(item => new
                {
                    Id = item.Id,
                    TenNguoiNv = item.TenNguoiNv ?? "",
                    SoNhanVien = item.SoNhanVien ?? "",
                    BoPhan = item.BoPhan ?? "",
                    Danhmuc = string.IsNullOrEmpty(item.Danhmuc) ? "Phổ thông" : item.Danhmuc,
                    TenForm = item.TenForm ?? "",
                    TimeNguoiTao = item.TimeNguoiTao.HasValue ? item.TimeNguoiTao.Value.ToString("dd/MM/yyyy") : "",
                    IdNguoiDuyet = item.IdNguoiDuyet,
                    IdAdmin = item.IdAdmin,
                    DaDanhGia = item.DanhGiaFormIts.Any(),
                    TenNguoiHoTro = item.ItCtNguoiHoTros
                                        .OrderByDescending(x => x.Stt)
                                        .Select(x => x.IdItNguoiHoTroNavigation != null ? x.IdItNguoiHoTroNavigation.Ten : "Chưa gán IT")
                                        .FirstOrDefault() ?? "Chưa gán IT"
                })
                .ToListAsync();

            return Json(danhSachDon);
        }

        // 3. HÀM XỬ LÝ POST
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
            var userName = User.Identity?.Name ?? "N/A";
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
                        if (!userRoles.Any(r => r == "All" || r == "AdminIT" || r == "QuanLyDuyetDonIT"))
                        {
                            return Json(new { success = false, message = "Bạn không có quyền phê duyệt." });
                        }

                        form.IdNguoiDuyet = userId;
                        form.TenNguoiDuyet = userName;
                        form.TimeNguoiDuyet = now;
                        form.TrangThai = "DaDuyet";

                        tieuDeLichSu = "Phê duyệt đơn IT";
                        moTaChiTiet = $"Người duyệt (Quản lý trực tiếp): {userName}({userEmail}). Bộ phận: {phongBan}.";
                    }
                    else if (request.Action == "Huy")
                    {
                        bool isCreator = form.IdNguoiTao == userId;
                        bool hasApprovalRole = userRoles.Any(r => r == "All" || r == "AdminIT" || r == "QuanLyDuyetDonIT");

                        if (!hasApprovalRole && !isCreator)
                        {
                            return Json(new { success = false, message = "Bạn không có quyền hủy đơn này." });
                        }

                        form.TrangThai = "DaHuy";
                        if (form.TenForm != null && !form.TenForm.StartsWith("[ĐÃ HỦY]"))
                            form.TenForm = "[ĐÃ HỦY] " + form.TenForm;

                        tieuDeLichSu = "Hủy đơn IT";
                        moTaChiTiet = $"Người hủy: {userName}({userEmail}). Lý do: {request.Reason}.";
                    }
                    else if (request.Action == "HoanTat")
                    {
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

        // ===================================================================
        // 4. API MỚI: XỬ LÝ XÁC NHẬN CHO ĐƠN CẤP QUYỀN Ổ CHUNG (FORM 8)
        // ===================================================================

        [HttpPost("/FormIT/XacNhanDon8")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> XacNhanDon8(int idFormIt, string status, string ghiChu)
        {
            // 1. Kiểm tra đăng nhập
            var userIdStr = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userIdStr)) return Json(new { success = false, message = "Hết phiên đăng nhập!" });

            // Lấy mã nhân viên từ Claim "MaNv" đã được thiết lập lúc đăng nhập
            var maNv = User.FindFirst("MaNv")?.Value ?? "N/A";
            var userName = User.Identity?.Name ?? "Quản lý bộ phận";

            // 2. Lấy dữ liệu đơn và các bảng liên kết con
            var don = await _context.FormIts
                .Include(f => f.ItCapQuyenOchung8s)
                    .ThenInclude(c => c.ItXacNhanCapQuyen8s)
                .FirstOrDefaultAsync(f => f.Id == idFormIt);

            if (don == null || !don.ItCapQuyenOchung8s.Any())
                return Json(new { success = false, message = "Đơn không tồn tại!" });

            // RÀNG BUỘC ĐÃ SỬA: Kiểm tra thông tin phê duyệt của Quản lý trực tiếp trong FormIT
            if (don.IdNguoiDuyet == null || string.IsNullOrEmpty(don.TenNguoiDuyet) || don.TimeNguoiDuyet == null)
            {
                return Json(new { success = false, message = "⚠️ Bạn chưa thể xác nhận đơn này do Quản lý trực tiếp của người làm đơn chưa phê duyệt!" });
            }

            var xacNhan = don.ItCapQuyenOchung8s.First().ItXacNhanCapQuyen8s.FirstOrDefault();
            if (xacNhan == null)
                return Json(new { success = false, message = "Không tìm thấy dữ liệu xác nhận bộ phận!" });

            // 3. Cập nhật thông tin người thực hiện xác nhận vào bảng con 8
            xacNhan.TrangThai = status == "Duyet" ? "Đã xác nhận" : "Từ chối";
            xacNhan.IdNguoiXacNhan = userIdStr;
            xacNhan.TenNguoiXacNhan = userName;
            xacNhan.TimeXacNhan = DateTime.Now;
            xacNhan.GhiChu = ghiChu;

            // 4. Tạo nhật ký lịch sử đơn phiếu (Bao gồm đầy đủ: Tên, Mã nhân viên, Bộ phận, Ghi chú)
            _context.LichSuFormIts.Add(new LichSuFormIt
            {
                IdFormIt = idFormIt,
                TieuDe = status == "Duyet" ? "BỘ PHẬN LIÊN ĐỚI XÁC NHẬN" : "BỘ PHẬN LIÊN ĐỚI TỪ CHỐI",
                Mota = $"Người xác nhận: {userName} (Mã NV: {maNv})\n" +
                       $"Bộ phận liên đới: {xacNhan.BoPhan}\n" +
                       $"Nội dung ghi chú: {(string.IsNullOrEmpty(ghiChu) ? "Không có" : ghiChu)}",
                Time = DateTime.Now
            });

            // 5. Lưu thay đổi vào Database
            await _context.SaveChangesAsync();
            return Json(new { success = true, message = "Xử lý phê duyệt bộ phận thành công!" });
        }

        #endregion

        #region QUẢN LÝ XÉT DUYỆT IT (Admin IT, All, IT, Quản lý bộ phận) - PHÂN QUYỀN 2026

        // 1. CHỈ TRẢ VỀ VIEW (Giao diện cũ - Không bao gồm đơn chỉ định)
        [HttpGet("/FormIT/HoanTatDon")]
        public IActionResult HoanTatDon()
        {
            var userIdStr = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userIdStr))
                return Redirect("/DonXetDuyet/DangNhap");

            return View();
        }

        // 2. API TRẢ VỀ DỮ LIỆU JSON CHO JAVASCRIPT (Đã loại trừ đơn Chỉ định người hỗ trợ)
        [HttpGet("/FormIT/GetHoanTatDonData")]
        public async Task<IActionResult> GetHoanTatDonData()
        {
            var userIdStr = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userIdStr)) return Unauthorized();

            int userId = int.Parse(userIdStr);
            var userRoles = User.FindAll(System.Security.Claims.ClaimTypes.Role).Select(c => c.Value).ToList();
            var phongBanSession = User.FindFirst("PhongBan")?.Value?.Trim() ?? "";
            var tenCongTy = User.FindFirst("TenCongTy")?.Value?.Trim() ?? "";

            var listTenBoPhanStr = User.FindFirst("TenBoPhan")?.Value ?? "";
            var listTenBoPhan = listTenBoPhanStr.Split(',')
                                                .Select(s => s.Trim())
                                                .Where(s => !string.IsNullOrEmpty(s))
                                                .ToList();

            // TẠO DANH SÁCH MỤC TIÊU CHO ĐƠN 8
            var validTargetBoPhans = new List<string>();
            if (!string.IsNullOrEmpty(tenCongTy))
            {
                if (!string.IsNullOrEmpty(phongBanSession)) validTargetBoPhans.Add($"{tenCongTy} - {phongBanSession}");
                foreach (var bp in listTenBoPhan)
                {
                    var target = $"{tenCongTy} - {bp}";
                    if (!validTargetBoPhans.Contains(target)) validTargetBoPhans.Add(target);
                }
            }

            // LỌC: Loại trừ các đơn có Danh mục là "Chỉ định người hỗ trợ"
            var query = _context.FormIts.AsNoTracking();

            // --- 3. PHÂN QUYỀN LỌC DỮ LIỆU ---
            if (userRoles.Any(r => r == "All" || r == "AdminIT"))
            {
                query = query.Where(f => f.IdNguoiDuyet != null);
            }
            else if (userRoles.Contains("QuanLyDuyetDonIT"))
            {
                bool hasPhu = listTenBoPhan.Any();
                query = query.Where(f =>
                    (hasPhu && f.BoPhan != null && listTenBoPhan.Contains(f.BoPhan)) ||
                    (!hasPhu && f.BoPhan == phongBanSession) ||
                    (f.IdNguoiTao == userId) ||
                    // MỚI: Hiển thị Đơn 8 nếu quản lý này thuộc Bộ phận liên đới cần xác nhận
                    (f.IdForm == "IT_CapQuyenOChung_8" && f.ItCapQuyenOchung8s.Any(c => c.ItXacNhanCapQuyen8s.Any(x => x.BoPhan != null && validTargetBoPhans.Contains(x.BoPhan))))
                );
            }
            else
            {
                query = query.Where(f => f.IdNguoiTao == userId);
            }

            // --- 4. THỰC THI TRUY VẤN VỚI PROJECTION ---
            var danhSachDon = await query
                .OrderByDescending(f => f.Id)
                .Select(item => new
                {
                    Id = item.Id,
                    TenNguoiNv = item.TenNguoiNv ?? "",
                    SoNhanVien = item.SoNhanVien ?? "",
                    BoPhan = item.BoPhan ?? "",
                    Danhmuc = string.IsNullOrEmpty(item.Danhmuc) ? "Yêu cầu IT" : item.Danhmuc,
                    TenForm = item.TenForm ?? "",
                    TimeNguoiTao = item.TimeNguoiTao.HasValue ? item.TimeNguoiTao.Value.ToString("dd/MM/yyyy") : "",
                    IdNguoiDuyet = item.IdNguoiDuyet,
                    IdAdmin = item.IdAdmin,
                    DaDanhGia = item.DanhGiaFormIts.Any(),
                    NguoiHoTros = item.ItCtNguoiHoTros
                                      .Where(ct => ct.IdItNguoiHoTroNavigation != null)
                                      .Select(ct => ct.IdItNguoiHoTroNavigation!.Ten ?? "N/A")
                                      .ToList()
                })
                .ToListAsync();

            return Json(danhSachDon);
        }

        #endregion

        #region DÙNG CHUNG - LOGIC XÁC NHẬN HOÀN THÀNH & CLASS REQUEST

        // Nút bấm dành cho Đội IT - Xác nhận đã sửa xong/cấp xong thiết bị (Cả 2 view đều gọi chung đến API POST này)
        [HttpPost("/FormIT/XacNhanHoanThanh")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> XacNhanHoanThanh([FromBody] ITCompleteRequest request)
        {
            if (request == null || request.Id <= 0)
                return Json(new { success = false, message = "Dữ liệu không hợp lệ." });

            var userIdStr = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userIdStr))
                return Json(new { success = false, message = "Phiên đăng nhập đã hết hạn." });

            var userRoles = User.FindAll(System.Security.Claims.ClaimTypes.Role).Select(c => c.Value).ToList();
            var userName = User.Identity?.Name ?? "N/A";
            var userEmail = User.FindFirst(System.Security.Claims.ClaimTypes.Email)?.Value ?? "N/A";
            var phongBan = User.FindFirst("PhongBan")?.Value ?? "N/A";

            if (!userRoles.Any(r => r == "All" || r == "AdminIT"))
            {
                return Json(new { success = false, message = "Bạn không có quyền xác nhận hoàn tất đơn này." });
            }

            var form = await _context.FormIts
                .Include(f => f.ItCapQuyenOchung8s)
                    .ThenInclude(c => c.ItXacNhanCapQuyen8s)
                .Include(f => f.ItCtNguoiHoTros)
                    .ThenInclude(ct => ct.IdItNguoiHoTroNavigation)
                .FirstOrDefaultAsync(f => f.Id == request.Id);

            if (form == null)
                return Json(new { success = false, message = "Không tìm thấy đơn IT." });

            bool isCancelled = (form.TenForm != null && form.TenForm.Contains("[ĐÃ HỦY]"));
            if (form.TrangThai == "HoanTat" || isCancelled)
            {
                return Json(new { success = false, message = "Đơn này đã kết thúc hoặc đã hủy." });
            }

            // KIỂM TRA RÀNG BUỘC ĐƠN 8
            if (form.IdForm == "IT_CapQuyenOChung_8" && form.ItCapQuyenOchung8s.Any())
            {
                var xacNhan = form.ItCapQuyenOchung8s.First().ItXacNhanCapQuyen8s.FirstOrDefault();
                if (xacNhan == null || xacNhan.TrangThai != "Đã xác nhận")
                {
                    return Json(new { success = false, message = "Đơn cấp quyền ổ chung yêu cầu phải được Quản lý của bộ phận liên đới XÁC NHẬN CHO PHÉP trước khi IT có thể hoàn tất!" });
                }
            }

            using (var transaction = await _context.Database.BeginTransactionAsync())
            {
                try
                {
                    DateTime now = DateTime.Now;

                    // 1. LUỒNG ĐỔI NGƯỜI HỖ TRỢ (TỰ ĐỘNG CHẠY KHI ẤN XÁC NHẬN)
                    var nvItHienTai = await _context.ItNguoiHoTros.FirstOrDefaultAsync(x => x.GhiChu == userEmail || x.Ten == userName);

                    if (nvItHienTai != null)
                    {
                        var nguoiHoTroMoiNhat = form.ItCtNguoiHoTros
                            .OrderByDescending(x => x.Stt ?? 0)
                            .FirstOrDefault();

                        if (nguoiHoTroMoiNhat == null || nguoiHoTroMoiNhat.IdItNguoiHoTro != nvItHienTai.Id)
                        {
                            var cácBảnGhiCũ = form.ItCtNguoiHoTros.Where(x => x.IdItNguoiHoTro == nvItHienTai.Id).ToList();
                            if (cácBảnGhiCũ.Any())
                            {
                                _context.ItCtNguoiHoTros.RemoveRange(cácBảnGhiCũ);
                            }

                            int maxStt = form.ItCtNguoiHoTros.Any() ? form.ItCtNguoiHoTros.Max(x => x.Stt ?? 0) : 0;

                            var ctMoi = new ItCtNguoiHoTro
                            {
                                IdFormIt = form.Id,
                                IdItNguoiHoTro = nvItHienTai.Id,
                                Stt = maxStt + 1
                            };
                            _context.ItCtNguoiHoTros.Add(ctMoi);

                            _context.LichSuFormIts.Add(new LichSuFormIt
                            {
                                IdFormIt = form.Id,
                                TieuDe = "CẬP NHẬT NGƯỜI HỖ TRỢ MỚI NHẤT",
                                Mota = $"Hệ thống chỉ định {nvItHienTai.Ten} làm người hỗ trợ mới nhất do thực hiện đóng đơn (Xóa bỏ lịch sử phân công cũ của người này).",
                                Time = now
                            });
                        }
                    }

                    // 2. LUỒNG XÁC NHẬN HOÀN TẤT ĐƠN
                    form.IdAdmin = int.Parse(userIdStr);
                    form.TenAdmin = userName;
                    form.TimeAdmin = now;
                    form.TrangThai = "HoanTat";

                    var lichSu = new LichSuFormIt
                    {
                        IdFormIt = form.Id,
                        TieuDe = "IT Xác nhận Hoàn tất",
                        Mota = $"Kỹ thuật thực hiện: {userName} ({userEmail}). Bộ phận: {phongBan}. Nội dung: Đã xử lý hoàn tất yêu cầu.",
                        Time = now
                    };

                    _context.LichSuFormIts.Add(lichSu);
                    _context.FormIts.Update(form);

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

        // --- CLASS REQUEST ---
        public class ITApprovalRequest
        {
            public int Id { get; set; }
            public string Action { get; set; } = string.Empty;
            public string Reason { get; set; } = string.Empty;
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

            var userName = User.Identity?.Name ?? "N/A";
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
            public string? Reason { get; set; }
        }
        #endregion

        #region Đánh giá đơn IT (Tích hợp Sửa lỗi không tồn tại thuộc tính NhanXet)

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

            // 3. Tìm đơn kèm các bảng liên quan
            var form = await _context.FormIts
                .Include(f => f.DanhGiaFormIts)
                .FirstOrDefaultAsync(x => x.Id == request.Id);

            if (form == null)
            {
                return Json(new { success = false, message = "Không tìm thấy đơn yêu cầu hệ thống." });
            }

            // Kiểm tra an toàn chuỗi Tên Công Ty không phân biệt hoa thường và khoảng trắng thừa
            if (!string.IsNullOrEmpty(form.TenCongTy) && !string.Equals(form.TenCongTy.Trim(), tenCongTy, StringComparison.OrdinalIgnoreCase))
            {
                return Json(new { success = false, message = "Bạn không có quyền truy cập hoặc đánh giá đơn thuộc công ty khác." });
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

                    // 8. TẠO BẢN GHI ĐÁNH GIÁ CHI TIẾT (Đã loại bỏ cột NhanXet để đúng 100% với Model thực thể của bạn)
                    var danhGiaMoi = new DanhGiaFormIt
                    {
                        IdFormIt = form.Id,
                        IdNguoiDanhGia = userId,
                        TenNguoiDanhGia = userName,
                        TimeNguoiDanhGia = now,
                        MucDo = request.MucDo
                    };

                    _context.DanhGiaFormIts.Add(danhGiaMoi);

                    // 9. LƯU VÀO LỊCH SỬ FORM (Nơi lưu giữ text nhận xét thực tế để hiển thị ra Log trao đổi)
                    string stars = new string('⭐', request.MucDo);
                    var lichSu = new LichSuFormIt
                    {
                        IdFormIt = form.Id,
                        TieuDe = "NGƯỜI DÙNG ĐÁNH GIÁ DỊCH VỤ",
                        Mota = $"Mức độ: {stars} ({request.MucDo}/5 sao)\n" +
                               $"Người đánh giá: {userName}\n" +
                               $"Nội dung nhận xét: {(string.IsNullOrWhiteSpace(request.NhanXet) ? "(Không có nhận xét)" : request.NhanXet.Trim())}",
                        Time = now,
                        IsRead = false // Để hệ thống hoặc luồng thông báo nhận diện log mới
                    };

                    _context.LichSuFormIts.Add(lichSu);

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
                    return Json(new { success = false, message = "Đã xảy ra lỗi khi lưu dữ liệu đánh giá: " + ex.Message });
                }
            }
        }

        // Request model cho đánh giá
        public class DanhGiaFormItsRequest
        {
            public int Id { get; set; }
            public int MucDo { get; set; } // Giá trị từ 1-5
            public string? NhanXet { get; set; } // Nội dung phản hồi thêm
        }

        #endregion

        #region Thống kê
        #region BÁO CÁO THỐNG KÊ FORM IT

        // 1. Action này chỉ trả về giao diện (View)
        [HttpGet("/FormIT/BaoCaoThongKe")]
        public IActionResult BaoCaoThongKe()
        {
            if (User == null || User.Identity == null || !User.Identity.IsAuthenticated)
            {
                return Redirect("/DonXetDuyet/DangNhap");
            }
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
                    .Select(x => new
                    {
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
                // ĐÃ SỬA: Thêm điều kiện IdFormItNavigation != null để triệt tiêu cảnh báo Null Reference
                if (fromDate.HasValue)
                {
                    query = query.Where(x => x.IdFormItNavigation != null && x.IdFormItNavigation.TimeNguoiDuyet >= fromDate.Value);
                }
                if (toDate.HasValue)
                {
                    var endOfToDate = toDate.Value.Date.AddDays(1).AddTicks(-1);
                    query = query.Where(x => x.IdFormItNavigation != null && x.IdFormItNavigation.TimeNguoiDuyet <= endOfToDate);
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
                    .Select(group =>
                    {
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

        // Định nghĩa Class DTO tường minh để tránh lỗi "Expression tree may not contain a dynamic operation"
        public class MacWifiThongKeDto
        {
            public int FormId { get; set; }
            public string IdForm { get; set; } = "";
            public string TenNguoiNv { get; set; } = "";
            public string BoPhan { get; set; } = "";
            public string LoaiThietBi { get; set; } = "";
            public string MaThietBi { get; set; } = "";
            public string MacTb { get; set; } = "";
            public DateTime? ThoiGianBatDau { get; set; }
            public DateTime? ThoiGianKetThuc { get; set; }
            public string TenAdmin { get; set; } = "";
            public DateTime? TimeAdmin { get; set; }
        }

        // 1. Action này trả về giao diện View mới
        [HttpGet("/FormIT/ThongKeMacWifi")]
        public IActionResult ThongKeMacWifi()
        {
            if (User == null || User.Identity == null || !User.Identity.IsAuthenticated)
            {
                return Redirect("/DonXetDuyet/DangNhap");
            }
            return View();
        }

        // 2. Action này lấy dữ liệu thống kê cho biểu đồ
        [HttpGet("/FormIT/GetDataThongKeMac")]
        public async Task<IActionResult> GetDataThongKeMac()
        {
            try
            {
                var rawData = await _context.ItDangKiSuDungWifi3s
                    .AsNoTracking()
                    .Where(x => !string.IsNullOrEmpty(x.MacTb))
                    .Select(x => new
                    {
                        x.LoaiThietBi,
                        x.MacTb
                    })
                    .ToListAsync();

                // Tách chuỗi '|' an toàn trên Bộ nhớ (In-Memory)
                var flatData = new List<MacWifiThongKeDto>();
                foreach (var item in rawData)
                {
                    var arrMac = item.MacTb!.Split('|', StringSplitOptions.RemoveEmptyEntries);
                    foreach (var mac in arrMac)
                    {
                        flatData.Add(new MacWifiThongKeDto
                        {
                            LoaiThietBi = item.LoaiThietBi ?? "Khác",
                            MacTb = mac.Trim()
                        });
                    }
                }

                var data = flatData
                    .GroupBy(x => x.LoaiThietBi)
                    .Select(g => new
                    {
                        LoaiThietBi = g.Key,
                        SoLuongMacTong = g.Count(),
                        SoLuongMacDuyNhat = g.Select(x => x.MacTb).Distinct().Count()
                    })
                    .ToList();

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
            string sortColumn = "TimeAdmin", string sortDir = "desc")
        {
            try
            {
                // Bước 1: Lấy dữ liệu thô từ Database về Client Memory trước để tránh lỗi Linq Expression không dịch được lệnh Split
                var queryRaw = await _context.ItDangKiSuDungWifi3s
                    .Include(x => x.IdFormItNavigation)
                    .AsNoTracking()
                    .Where(x => !string.IsNullOrEmpty(x.MacTb) &&
                                x.IdFormItNavigation != null &&
                                x.IdFormItNavigation.IdAdmin != null &&
                                x.IdFormItNavigation.TenAdmin != null &&
                                x.IdFormItNavigation.TimeAdmin != null)
                    .ToListAsync();

                // Bước 2: Duyệt bóc tách chuỗi gộp ngăn cách bởi dấu '|' thành danh sách DTO tường minh
                var flatList = new List<MacWifiThongKeDto>();
                foreach (var x in queryRaw)
                {
                    var arrMa = (x.MaThietBi ?? "").Split('|', StringSplitOptions.RemoveEmptyEntries);
                    var arrMac = (x.MacTb ?? "").Split('|', StringSplitOptions.RemoveEmptyEntries);
                    int maxCount = Math.Max(arrMa.Length, arrMac.Length);

                    for (int i = 0; i < maxCount; i++)
                    {
                        string subMa = i < arrMa.Length ? arrMa[i].Trim() : "";
                        string subMac = i < arrMac.Length ? arrMac[i].Trim() : "";

                        flatList.Add(new MacWifiThongKeDto
                        {
                            FormId = x.IdFormItNavigation!.Id,
                            IdForm = x.IdFormItNavigation.IdForm ?? "",
                            TenNguoiNv = x.IdFormItNavigation.TenNguoiNv ?? "",
                            BoPhan = x.IdFormItNavigation.BoPhan ?? "",
                            LoaiThietBi = x.LoaiThietBi ?? "",
                            MaThietBi = subMa,
                            MacTb = subMac,
                            ThoiGianBatDau = x.ThoiGianBatDau,
                            ThoiGianKetThuc = x.ThoiGianKetThuc,
                            TenAdmin = x.IdFormItNavigation.TenAdmin ?? "",
                            TimeAdmin = x.IdFormItNavigation.TimeAdmin
                        });
                    }
                }

                // Bước 3: Áp dụng các bộ lọc tìm kiếm (Chạy mượt mà trên IQueryable của Class DTO)
                var filteredQuery = flatList.AsQueryable();

                if (!string.IsNullOrEmpty(idForm)) filteredQuery = filteredQuery.Where(x => x.IdForm.Contains(idForm, StringComparison.OrdinalIgnoreCase));
                if (!string.IsNullOrEmpty(tenNguoiNv)) filteredQuery = filteredQuery.Where(x => x.TenNguoiNv.Contains(tenNguoiNv, StringComparison.OrdinalIgnoreCase));
                if (!string.IsNullOrEmpty(boPhan)) filteredQuery = filteredQuery.Where(x => x.BoPhan.Contains(boPhan, StringComparison.OrdinalIgnoreCase));
                if (!string.IsNullOrEmpty(loaiThietBi)) filteredQuery = filteredQuery.Where(x => x.LoaiThietBi.Contains(loaiThietBi, StringComparison.OrdinalIgnoreCase));
                if (!string.IsNullOrEmpty(macTb)) filteredQuery = filteredQuery.Where(x => x.MacTb.Contains(macTb, StringComparison.OrdinalIgnoreCase));
                if (!string.IsNullOrEmpty(maThietBi)) filteredQuery = filteredQuery.Where(x => x.MaThietBi.Contains(maThietBi, StringComparison.OrdinalIgnoreCase));

                if (fromDate.HasValue) filteredQuery = filteredQuery.Where(x => x.TimeAdmin >= fromDate.Value);
                if (toDate.HasValue)
                {
                    var toDateEnd = toDate.Value.AddDays(1).AddTicks(-1);
                    filteredQuery = filteredQuery.Where(x => x.TimeAdmin <= toDateEnd);
                }

                // Bước 4: Áp dụng Sắp Xếp Động tương thích hoàn toàn với cấu trúc bảng
                bool isAsc = sortDir == "asc";
                switch (sortColumn)
                {
                    case "IdForm": filteredQuery = isAsc ? filteredQuery.OrderBy(x => x.IdForm) : filteredQuery.OrderByDescending(x => x.IdForm); break;
                    case "TenNguoiNv": filteredQuery = isAsc ? filteredQuery.OrderBy(x => x.TenNguoiNv) : filteredQuery.OrderByDescending(x => x.TenNguoiNv); break;
                    case "BoPhan": filteredQuery = isAsc ? filteredQuery.OrderBy(x => x.BoPhan) : filteredQuery.OrderByDescending(x => x.BoPhan); break;
                    case "MaThietBi": filteredQuery = isAsc ? filteredQuery.OrderBy(x => x.MaThietBi) : filteredQuery.OrderByDescending(x => x.MaThietBi); break;
                    case "LoaiThietBi": filteredQuery = isAsc ? filteredQuery.OrderBy(x => x.LoaiThietBi) : filteredQuery.OrderByDescending(x => x.LoaiThietBi); break;
                    case "MacTb": filteredQuery = isAsc ? filteredQuery.OrderBy(x => x.MacTb) : filteredQuery.OrderByDescending(x => x.MacTb); break;
                    case "ThoiGianBatDau": filteredQuery = isAsc ? filteredQuery.OrderBy(x => x.ThoiGianBatDau) : filteredQuery.OrderByDescending(x => x.ThoiGianBatDau); break;
                    case "ThoiGianKetThuc": filteredQuery = isAsc ? filteredQuery.OrderBy(x => x.ThoiGianKetThuc) : filteredQuery.OrderByDescending(x => x.ThoiGianKetThuc); break;
                    case "TenAdmin": filteredQuery = isAsc ? filteredQuery.OrderBy(x => x.TenAdmin) : filteredQuery.OrderByDescending(x => x.TenAdmin); break;
                    case "TimeAdmin": filteredQuery = isAsc ? filteredQuery.OrderBy(x => x.TimeAdmin) : filteredQuery.OrderByDescending(x => x.TimeAdmin); break;
                    default: filteredQuery = filteredQuery.OrderByDescending(x => x.TimeAdmin); break;
                }

                var totalRecords = filteredQuery.Count();
                var totalPages = (int)Math.Ceiling(totalRecords / (double)pageSize);

                var data = filteredQuery
                    .Skip((page - 1) * pageSize)
                    .Take(pageSize)
                    .ToList();

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
            string sortColumn = "TimeAdmin", string sortDir = "desc")
        {
            try
            {
                var queryRaw = await _context.ItDangKiSuDungWifi3s
                    .Include(x => x.IdFormItNavigation)
                    .AsNoTracking()
                    .Where(x => !string.IsNullOrEmpty(x.MacTb) &&
                                x.IdFormItNavigation != null &&
                                x.IdFormItNavigation.IdAdmin != null)
                    .ToListAsync();

                // Giải nén mảng chuỗi thành cấu trúc danh sách phẳng phục vụ xuất file báo cáo Excel
                var flatList = new List<MacWifiThongKeDto>();
                foreach (var x in queryRaw)
                {
                    var arrMa = (x.MaThietBi ?? "").Split('|', StringSplitOptions.RemoveEmptyEntries);
                    var arrMac = (x.MacTb ?? "").Split('|', StringSplitOptions.RemoveEmptyEntries);
                    int maxCount = Math.Max(arrMa.Length, arrMac.Length);

                    for (int i = 0; i < maxCount; i++)
                    {
                        string subMa = i < arrMa.Length ? arrMa[i].Trim() : "";
                        string subMac = i < arrMac.Length ? arrMac[i].Trim() : "";

                        flatList.Add(new MacWifiThongKeDto
                        {
                            IdForm = x.IdFormItNavigation!.IdForm ?? "",
                            TenNguoiNv = x.IdFormItNavigation.TenNguoiNv ?? "",
                            BoPhan = x.IdFormItNavigation.BoPhan ?? "",
                            MaThietBi = subMa,
                            LoaiThietBi = x.LoaiThietBi ?? "",
                            MacTb = subMac,
                            ThoiGianBatDau = x.ThoiGianBatDau,
                            ThoiGianKetThuc = x.ThoiGianKetThuc,
                            TenAdmin = x.IdFormItNavigation.TenAdmin ?? "",
                            TimeAdmin = x.IdFormItNavigation.TimeAdmin
                        });
                    }
                }

                var filteredQuery = flatList.AsQueryable();

                if (!string.IsNullOrEmpty(idForm)) filteredQuery = filteredQuery.Where(x => x.IdForm.Contains(idForm, StringComparison.OrdinalIgnoreCase));
                if (!string.IsNullOrEmpty(tenNguoiNv)) filteredQuery = filteredQuery.Where(x => x.TenNguoiNv.Contains(tenNguoiNv, StringComparison.OrdinalIgnoreCase));
                if (!string.IsNullOrEmpty(boPhan)) filteredQuery = filteredQuery.Where(x => x.BoPhan.Contains(boPhan, StringComparison.OrdinalIgnoreCase));
                if (!string.IsNullOrEmpty(loaiThietBi)) filteredQuery = filteredQuery.Where(x => x.LoaiThietBi.Contains(loaiThietBi, StringComparison.OrdinalIgnoreCase));
                if (!string.IsNullOrEmpty(macTb)) filteredQuery = filteredQuery.Where(x => x.MacTb.Contains(macTb, StringComparison.OrdinalIgnoreCase));
                if (!string.IsNullOrEmpty(maThietBi)) filteredQuery = filteredQuery.Where(x => x.MaThietBi.Contains(maThietBi, StringComparison.OrdinalIgnoreCase));

                if (fromDate.HasValue) filteredQuery = filteredQuery.Where(x => x.TimeAdmin >= fromDate.Value);
                if (toDate.HasValue)
                {
                    var toDateEnd = toDate.Value.AddDays(1).AddTicks(-1);
                    filteredQuery = filteredQuery.Where(x => x.TimeAdmin <= toDateEnd);
                }

                bool isAsc = sortDir == "asc";
                switch (sortColumn)
                {
                    case "IdForm": filteredQuery = isAsc ? filteredQuery.OrderBy(x => x.IdForm) : filteredQuery.OrderByDescending(x => x.IdForm); break;
                    case "TenNguoiNv": filteredQuery = isAsc ? filteredQuery.OrderBy(x => x.TenNguoiNv) : filteredQuery.OrderByDescending(x => x.TenNguoiNv); break;
                    case "BoPhan": filteredQuery = isAsc ? filteredQuery.OrderBy(x => x.BoPhan) : filteredQuery.OrderByDescending(x => x.BoPhan); break;
                    case "MaThietBi": filteredQuery = isAsc ? filteredQuery.OrderBy(x => x.MaThietBi) : filteredQuery.OrderByDescending(x => x.MaThietBi); break;
                    case "LoaiThietBi": filteredQuery = isAsc ? filteredQuery.OrderBy(x => x.LoaiThietBi) : filteredQuery.OrderByDescending(x => x.LoaiThietBi); break;
                    case "MacTb": filteredQuery = isAsc ? filteredQuery.OrderBy(x => x.MacTb) : filteredQuery.OrderByDescending(x => x.MacTb); break;
                    case "ThoiGianBatDau": filteredQuery = isAsc ? filteredQuery.OrderBy(x => x.ThoiGianBatDau) : filteredQuery.OrderByDescending(x => x.ThoiGianBatDau); break;
                    case "ThoiGianKetThuc": filteredQuery = isAsc ? filteredQuery.OrderBy(x => x.ThoiGianKetThuc) : filteredQuery.OrderByDescending(x => x.ThoiGianKetThuc); break;
                    case "TenAdmin": filteredQuery = isAsc ? filteredQuery.OrderBy(x => x.TenAdmin) : filteredQuery.OrderByDescending(x => x.TenAdmin); break;
                    case "TimeAdmin": filteredQuery = isAsc ? filteredQuery.OrderBy(x => x.TimeAdmin) : filteredQuery.OrderByDescending(x => x.TimeAdmin); break;
                    default: filteredQuery = filteredQuery.OrderByDescending(x => x.TimeAdmin); break;
                }

                var data = filteredQuery.ToList();

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

        #region THỐNG KÊ TIẾN TRÌNH ĐƠN (TIMELINE V6)

        // Class DTO giữ nguyên toàn bộ thuộc tính hiện có của bạn để không ảnh hưởng logic ánh xạ dữ liệu
        public class FormItTimelineDto
        {
            public int Id { get; set; }
            public string IdForm { get; set; } = "";
            public string TenForm { get; set; } = "";
            public string TenNguoiNv { get; set; } = "";
            public string BoPhan { get; set; } = "";
            public string TrangThai { get; set; } = "";
            // Giai đoạn 1: Tạo đơn
            public string TenNguoiTao { get; set; } = "";
            public DateTime? TimeNguoiTao { get; set; }
            // Giai đoạn 2: Quản lý duyệt
            public string TenNguoiDuyet { get; set; } = "";
            public DateTime? TimeNguoiDuyet { get; set; }
            // Giai đoạn 3: IT Hoàn thành
            public string TenAdmin { get; set; } = "";
            public DateTime? TimeAdmin { get; set; }
            // Thuộc tính Danh mục giữ nguyên gốc của model
            public string Danhmuc { get; set; } = "";
        }

        [HttpGet("/FormIT/BaoCaoGanttTimeline")]
        public IActionResult BaoCaoGanttTimeline()
        {
            if (User == null || User.Identity == null || !User.Identity.IsAuthenticated)
            {
                return Redirect("/DonXetDuyet/DangNhap");
            }
            return View();
        }

        // API lấy dữ liệu tiến trình - Chỉ lấy đơn đã duyệt, loại bỏ hoàn toàn đơn HỦY
        // Đặc biệt: Đơn chưa hoàn thành (TimeAdmin == null) luôn luôn hiển thị lơ lửng bất kể bộ lọc ngày tháng
        [HttpGet("/FormIT/GetTimelineData")]
        public async Task<IActionResult> GetTimelineData(DateTime? fromDate, DateTime? toDate)
        {
            try
            {
                // Loại bỏ các đơn có TrangThai chứa từ "HỦY" hoặc hành động hủy "DaHuy" để khớp logic hệ thống của bạn
                // Contains(string, StringComparison) không dịch được sang SQL nên EF Core ném lỗi runtime; dùng Contains(string) thường (dịch sang LIKE, không phân biệt hoa/thường theo collation mặc định)
                var query = _context.FormIts.AsNoTracking()
                    .Where(x => x.IdNguoiDuyet != null && x.TenNguoiDuyet != null && x.TimeNguoiDuyet != null)
                    .Where(x => x.TrangThai == null ||
                               (!x.TrangThai.Contains("HỦY") &&
                                !x.TrangThai.Contains("DAHUY")));

                // Mặc định lùi 1 tháng nếu giao diện chưa kịp truyền tham số lên ban đầu
                var startFilter = fromDate ?? DateTime.Today.AddMonths(-1);
                var endFilter = toDate ?? DateTime.Today.AddDays(1).AddTicks(-1);

                if (fromDate.HasValue)
                {
                    startFilter = fromDate.Value.Date;
                }
                if (toDate.HasValue)
                {
                    endFilter = toDate.Value.Date.AddDays(1).AddTicks(-1);
                }

                // CẬP NHẬT LOGIC ĐẶC BIỆT: Nằm trong khoảng lọc ngày HOẶC đơn đó chưa hoàn thành (TimeAdmin == null)
                // Giúp đơn chưa hoàn thành không liên quan đến bộ lọc và luôn luôn hiển thị lơ lửng trên biểu đồ
                query = query.Where(x => (x.TimeNguoiTao >= startFilter && x.TimeNguoiTao <= endFilter) || x.TimeAdmin == null);

                // Sắp xếp đơn theo TimeNguoiTao hoặc Id
                query = query.OrderByDescending(x => x.TimeNguoiTao ?? DateTime.MinValue).ThenByDescending(x => x.Id);

                var totalRecords = await query.CountAsync();

                var rawData = await query
                    .Select(x => new FormItTimelineDto
                    {
                        Id = x.Id,
                        IdForm = x.IdForm ?? "N/A",
                        TenForm = x.TenForm ?? "Đơn không tên",
                        TenNguoiNv = x.TenNguoiNv ?? "Ẩn danh",
                        BoPhan = x.BoPhan ?? "N/A",
                        TrangThai = x.TrangThai ?? "CHỜ DUYỆT",
                        TenNguoiTao = x.TenNguoiTao ?? "Chưa rõ",
                        TimeNguoiTao = x.TimeNguoiTao,
                        TenNguoiDuyet = x.TenNguoiDuyet ?? "Chưa duyệt",
                        TimeNguoiDuyet = x.TimeNguoiDuyet,
                        TenAdmin = x.TenAdmin ?? "Chưa xử lý",
                        TimeAdmin = x.TimeAdmin,
                        Danhmuc = x.Danhmuc ?? "Chưa phân loại"
                    })
                    .ToListAsync();

                return Json(new { data = rawData, totalRecords });
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
            var phongBanSession = User.FindFirst("PhongBan")?.Value?.Trim() ?? "";

            var listTenBoPhanStr = User.FindFirst("TenBoPhan")?.Value ?? "";
            var listTenBoPhan = listTenBoPhanStr.Split(',')
                                                .Select(s => s.Trim())
                                                .Where(s => !string.IsNullOrEmpty(s))
                                                .ToList();

            // TẠO DANH SÁCH MỤC TIÊU (CÔNG TY - BỘ PHẬN) ĐỂ CHECK CHO ĐƠN 8
            var validTargetBoPhans = new List<string>();
            if (!string.IsNullOrEmpty(tenCongTy))
            {
                if (!string.IsNullOrEmpty(phongBanSession)) validTargetBoPhans.Add($"{tenCongTy} - {phongBanSession}");
                foreach (var bp in listTenBoPhan)
                {
                    var target = $"{tenCongTy} - {bp}";
                    if (!validTargetBoPhans.Contains(target)) validTargetBoPhans.Add(target);
                }
            }

            // Khởi tạo query không lọc sẵn công ty
            var query = _context.LichSuFormIts.AsNoTracking();

            // Phân quyền
            if (User.IsInRole("All"))
            {
                /* Xem toàn bộ - Không lọc theo TenCongTy */
            }
            else if (User.IsInRole("AdminIT"))
            {
                /* AdminIT - Không lọc theo TenCongTy */
                query = query.Where(l =>
                    l.IdFormItNavigation != null &&
                    l.IdFormItNavigation.IdNguoiDuyet != null &&
                    (l.IdFormItNavigation.IdAdmin == userId ||
                     l.IdFormItNavigation.ItCtNguoiHoTros.Any(ct => ct.IdItNguoiHoTroNavigation != null && ct.IdItNguoiHoTroNavigation.MaNv == userEmail))
                );
            }
            else
            {
                // Các trường hợp còn lại áp dụng lọc theo công ty
                query = query.Where(l => l.IdFormItNavigation != null && l.IdFormItNavigation.TenCongTy == tenCongTy);

                if (User.IsInRole("QuanLyDuyetDonIT"))
                {
                    query = query.Where(l =>
                        l.IdFormItNavigation != null &&
                        (
                            l.IdFormItNavigation.IdNguoiTao == userId ||
                            l.IdFormItNavigation.IdNguoiDuyet == userId ||
                            // MỚI: Nhìn thấy lịch sử Đơn 8 NẾU Quản lý trực tiếp ĐÃ DUYỆT (IdNguoiDuyet != null) và Quản lý này thuộc Bộ phận liên đới
                            (l.IdFormItNavigation.IdNguoiDuyet != null && l.IdFormItNavigation.IdForm == "IT_CapQuyenOChung_8" &&
                             l.IdFormItNavigation.ItCapQuyenOchung8s.Any(c => c.ItXacNhanCapQuyen8s.Any(x => x.BoPhan != null && validTargetBoPhans.Contains(x.BoPhan))))
                        )
                    );
                }
                else
                {
                    query = query.Where(l => l.IdFormItNavigation != null && (l.IdFormItNavigation.IdNguoiTao == userId || l.IdFormItNavigation.ItCtNguoiHoTros.Any(ct => ct.IdItNguoiHoTroNavigation != null && ct.IdItNguoiHoTroNavigation.MaNv == userEmail)));
                }
            }

            // Bước 2: Select trực tiếp (Projections)
            var rawData = await query
                .OrderByDescending(l => l.Time)
                .Select(l => new
                {
                    l.IdFormIt,
                    l.Time,
                    l.TieuDe,
                    l.Mota,
                    f = l.IdFormItNavigation,
                    CurrentSupporterTen = l.IdFormItNavigation != null
                        ? l.IdFormItNavigation.ItCtNguoiHoTros
                            .OrderByDescending(x => x.Stt)
                            .Select(x => x.IdItNguoiHoTroNavigation != null ? x.IdItNguoiHoTroNavigation.Ten : null)
                            .FirstOrDefault()
                        : null,
                    DanhGia = l.IdFormItNavigation != null
                        ? l.IdFormItNavigation.DanhGiaFormIts.Select(d => new { d.TimeNguoiDanhGia, d.MucDo }).FirstOrDefault()
                        : null
                })
                .ToListAsync();

            // Bước 3: Map logic màu sắc và text
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

        private static string FormatTimeSpan(TimeSpan span)
        {
            if (span.TotalDays >= 1) return $"{(int)span.TotalDays}n {span.Hours}g";
            if (span.TotalHours >= 1) return $"{(int)span.TotalHours}g {span.Minutes}p";
            return $"{(int)span.TotalMinutes}p";
        }

        // 3. GET NOTIFICATIONS
        [HttpGet("/FormIT/GetNotifications")]
        public async Task<IActionResult> GetNotifications(int skip = 0, int take = 20)
        {
            var userIdStr = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userIdStr)) return Unauthorized();

            int userId = int.Parse(userIdStr);
            var userEmail = User.FindFirst(System.Security.Claims.ClaimTypes.Email)?.Value ?? "";
            var tenCongTy = User.FindFirst("TenCongTy")?.Value?.Trim() ?? "";
            var phongBanSession = User.FindFirst("PhongBan")?.Value?.Trim() ?? "";

            var listTenBoPhanStr = User.FindFirst("TenBoPhan")?.Value ?? "";
            var listTenBoPhan = listTenBoPhanStr.Split(',')
                                                .Select(s => s.Trim())
                                                .Where(s => !string.IsNullOrEmpty(s))
                                                .ToList();

            // TẠO DANH SÁCH MỤC TIÊU CHO ĐƠN 8
            var validTargetBoPhans = new List<string>();
            if (!string.IsNullOrEmpty(tenCongTy))
            {
                if (!string.IsNullOrEmpty(phongBanSession)) validTargetBoPhans.Add($"{tenCongTy} - {phongBanSession}");
                foreach (var bp in listTenBoPhan)
                {
                    var target = $"{tenCongTy} - {bp}";
                    if (!validTargetBoPhans.Contains(target)) validTargetBoPhans.Add(target);
                }
            }

            var query = _context.LichSuFormIts.AsNoTracking();

            if (User.IsInRole("All")) { /* Xem toàn bộ */ }
            else if (User.IsInRole("AdminIT"))
            {
                query = query.Where(l =>
                    l.IdFormItNavigation != null &&
                    l.IdFormItNavigation.IdNguoiDuyet != null &&
                    (l.IdFormItNavigation.IdAdmin == userId ||
                     l.IdFormItNavigation.ItCtNguoiHoTros.Any(ct => ct.IdItNguoiHoTroNavigation != null && ct.IdItNguoiHoTroNavigation.MaNv == userEmail))
                );
            }
            else
            {
                query = query.Where(l => l.IdFormItNavigation != null && l.IdFormItNavigation.TenCongTy == tenCongTy);

                if (User.IsInRole("QuanLyDuyetDonIT"))
                {
                    query = query.Where(l =>
                        l.IdFormItNavigation != null &&
                        (
                            l.IdFormItNavigation.IdNguoiTao == userId ||
                            l.IdFormItNavigation.IdNguoiDuyet == userId ||
                            // MỚI: Nhận thông báo Đơn 8 NẾU Quản lý trực tiếp ĐÃ DUYỆT (IdNguoiDuyet != null) và Quản lý này thuộc Bộ phận liên đới
                            (l.IdFormItNavigation.IdNguoiDuyet != null && l.IdFormItNavigation.IdForm == "IT_CapQuyenOChung_8" &&
                             l.IdFormItNavigation.ItCapQuyenOchung8s.Any(c => c.ItXacNhanCapQuyen8s.Any(x => x.BoPhan != null && validTargetBoPhans.Contains(x.BoPhan))))
                        )
                    );
                }
                else
                {
                    query = query.Where(l => l.IdFormItNavigation != null && l.IdFormItNavigation.IdNguoiTao == userId);
                }
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
            if (User == null || User.Identity == null || !User.Identity.IsAuthenticated)
            {
                return Redirect("/DonXetDuyet/DangNhap");
            }
            return View("ThongKeKiemKe");
        }

        // 1. HÀM NÀY DÙNG ĐỂ VẼ BIỂU ĐỒ (Giữ nguyên logic cực chuẩn của bạn)
        [HttpGet("/QLKiemKe/GetDuLieuThongKe")]
        public IActionResult GetDuLieuThongKe()
        {
            try
            {
                // Lấy toàn bộ thiết bị đang hoạt động (Bỏ qua các thiết bị nằm trong thùng rác/có NgayXoa)
                // Đã chuyển đổi phép so sánh chuỗi sang Equals kèm StringComparison để dứt điểm cảnh báo
                var thietBis = _context.KkThietBis
                    .Include(x => x.IdTrangThaiNavigation)
                    .Include(x => x.IdcongTyNavigation)
                    .Include(x => x.IdboPhanNavigation)
                    .Where(x => x.NgayXoa == null && (x.IdTrangThaiNavigation == null || !x.IdTrangThaiNavigation.TenTrangThai.Equals("xóa", StringComparison.OrdinalIgnoreCase)))
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
                return Json(new { success = false, message = ex.Message });
            }
        }

        // 2. HÀM NÀY DÙNG ĐỂ ĐỔ DỮ LIỆU VÀO BẢNG & XUẤT EXCEL (Đã thêm WinLicense và OfficeLicense)
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
                        tenViTri = x.TenViTri, // ĐÃ ĐỒNG BỘ: Đổi sang cấu trúc thuộc tính mới từ dữ liệu
                        tenMayTinh = x.TenMayTinh,
                        loaiThietBi = x.LoaiThietBi,
                        quyCach = x.QuyCach,
                        seribacode = x.Seribacode,
                        hanBaoHanh = x.HanBaoHanh,
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
                        ngayXoa = x.NgayXoa,

                        // Thêm trường bản quyền để đồng bộ hiển thị và lọc nâng cao
                        winLicense = x.WinLicense,
                        officeLicense = x.OfficeLicense
                    })
                    .ToList();

                return Json(new { success = true, data = data });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = ex.Message });
            }
        }

        // 3. HÀM NÀY DÙNG ĐỂ LẤY LỊCH SỬ THAO TÁC CỦA THIẾT BỊ THEO ID
        [HttpGet("/QLKiemKe/GetLichSuThietBi")]
        public IActionResult GetLichSuThietBi(int idThietBi)
        {
            try
            {
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
                return Json(new { success = false, message = ex.Message });
            }
        }

        #endregion

        #region Quản lý kiểm kê: Danh mục & Cấu hình (Công Ty, Bộ Phận, Loại Thiết Bị, Trạng Thái, Lịch Sử)
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
            if (User == null || User.Identity == null || !User.Identity.IsAuthenticated)
            {
                return Redirect("/DonXetDuyet/DangNhap");
            }
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
            catch (Exception ex) { return Json(new { success = false, message = ex.Message }); }
        }

        // ==================== TRẠNG THÁI ====================
        // Lấy danh sách các giá trị Bản quyền Windows/Office ĐÃ TỪNG được nhập trong hệ thống, dùng làm gợi ý (datalist)
        // khi thêm/sửa thiết bị thủ công - thay vì chỉ có sẵn vài mẫu cứng, gợi ý sẽ phản ánh đúng dữ liệu thực tế đang dùng.
        [HttpGet("/QLKiemKe/GetGoiYBanQuyen")]
        public IActionResult GetGoiYBanQuyen()
        {
            try
            {
                var dsWin = _context.KkThietBis
                    .Where(x => x.WinLicense != null && x.WinLicense != "")
                    .Select(x => x.WinLicense!.Trim())
                    .Distinct()
                    .OrderBy(x => x)
                    .ToList();

                var dsOffice = _context.KkThietBis
                    .Where(x => x.OfficeLicense != null && x.OfficeLicense != "")
                    .Select(x => x.OfficeLicense!.Trim())
                    .Distinct()
                    .OrderBy(x => x)
                    .ToList();

                return Json(new { success = true, dsWin, dsOffice });
            }
            catch (Exception ex) { return Json(new { success = false, message = ex.Message }); }
        }

        [HttpGet("/QLKiemKe/GetKkTrangThais")]
        public IActionResult GetKkTrangThais()
        {
            try
            {
                var data = _context.KkTrangThais.OrderByDescending(x => x.IdTrangThai).ToList();
                return Json(new { success = true, data = data });
            }
            catch (Exception ex) { return Json(new { success = false, message = ex.Message }); }
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

                return Json(new { success = true, message = "Lưu trạng thái thành công!" });
            }
            catch (Exception ex) { return Json(new { success = false, message = ex.Message }); }
        }

        [HttpPost("/QLKiemKe/DeleteKkTrangThai")]
        public IActionResult DeleteKkTrangThai(int id)
        {
            try
            {
                var item = _context.KkTrangThais.Find(id);
                if (item != null)
                {
                    // Sử dụng Equals kết hợp StringComparison.OrdinalIgnoreCase để dứt điểm cảnh báo
                    if (item.TenTrangThai != null && item.TenTrangThai.Equals("xóa", StringComparison.OrdinalIgnoreCase))
                        return Json(new { success = false, message = "Không thể xóa trạng thái mặc định của hệ thống!" });

                    string tenTT = item.TenTrangThai ?? "";
                    _context.KkTrangThais.Remove(item);
                    _context.SaveChanges();

                    GhiLichSu("Xóa", "Trạng Thái", id, $"Đã xóa trạng thái: {tenTT}");
                }
                return Json(new { success = true, message = "Đã xóa trạng thái!" });
            }
            catch (Exception) { return Json(new { success = false, message = "Không thể xóa vì trạng thái này đang được gán cho thiết bị." }); }
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
            catch (Exception ex) { return Json(new { success = false, message = ex.Message }); }
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

                return Json(new { success = true, message = "Lưu công ty thành công!" });
            }
            catch (Exception ex) { return Json(new { success = false, message = ex.Message }); }
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
                return Json(new { success = true, message = "Đã xóa công ty!" });
            }
            catch (Exception) { return Json(new { success = false, message = "Không thể xóa vì công ty này đang được sử dụng." }); }
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
            catch (Exception ex) { return Json(new { success = false, message = ex.Message }); }
        }

        [HttpPost("/QLKiemKe/SaveKkBoPhan")]
        public IActionResult SaveKkBoPhan(KkBoPhan model)
        {
            try
            {
                // Chặn trùng Tên bộ phận trong cùng 1 Công ty (không phân biệt hoa/thường, khoảng trắng thừa)
                if (!string.IsNullOrWhiteSpace(model.TenBoPhan))
                {
                    string trimmedTen = model.TenBoPhan.Trim().ToLower();
                    bool isDuplicate = _context.KkBoPhans.Any(x =>
                        x.IdboPhan != model.IdboPhan &&
                        x.IdcongTy == model.IdcongTy &&
                        x.TenBoPhan != null && x.TenBoPhan.Trim().ToLower() == trimmedTen);

                    if (isDuplicate)
                        return Json(new { success = false, message = $"Bộ phận '{model.TenBoPhan}' đã tồn tại trong công ty này. Vui lòng kiểm tra lại!" });
                }

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

                return Json(new { success = true, message = "Lưu bộ phận thành công!" });
            }
            catch (Exception ex) { return Json(new { success = false, message = ex.Message }); }
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
                return Json(new { success = true, message = "Đã xóa bộ phận!" });
            }
            catch (Exception) { return Json(new { success = false, message = "Không thể xóa vì bộ phận này đang được sử dụng." }); }
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
            catch (Exception ex) { return Json(new { success = false, message = ex.Message }); }
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

                return Json(new { success = true, message = "Lưu loại thiết bị thành công!" });
            }
            catch (Exception ex) { return Json(new { success = false, message = ex.Message }); }
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
                return Json(new { success = true, message = "Đã xóa loại thiết bị!" });
            }
            catch (Exception) { return Json(new { success = false, message = "Không thể xóa vì loại thiết bị này đang được gán cho thiết bị." }); }
        }

        #endregion


        #region 2. QL KIỂM KÊ: THIẾT BỊ (Xử lý Thiết Bị & Hình Ảnh)

        // View chuyên biệt quản lý Thiết Bị
        [HttpGet("/QLKiemKe/ThietBi")]
        public IActionResult IndexThietBi()
        {
            if (User == null || User.Identity == null || !User.Identity.IsAuthenticated)
            {
                return Redirect("/DonXetDuyet/DangNhap");
            }
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

                // Sử dụng EF.Functions.Like để so sánh không phân biệt hoa thường ở tầng Database (Tối ưu Index)
                var itemsToDelete = _context.KkThietBis
                    .Where(x => x.IdTrangThaiNavigation != null
                             && x.IdTrangThaiNavigation.TenTrangThai != null
                             && EF.Functions.Like(x.IdTrangThaiNavigation.TenTrangThai, "xóa")
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
                        x.TenViTri, // ĐÃ ĐỒNG BỘ: Thay thế trường TenThietBi thành TenViTri
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
                        x.ThoiGianCheck, // Trả về thời gian check
                        x.QuyCach, // BỔ SUNG: Trả về trường QuyCach ra View
                        x.Seribacode, // ĐẢM BẢO ĐẦY ĐỦ: Lấy dữ liệu các trường mới ra View
                        x.HanBaoHanh,
                        x.WinLicense,
                        x.OfficeLicense,
                        x.IdMay
                    })
                    .ToList(); // Tải dữ liệu về bộ nhớ trước để xử lý chuẩn hóa chuỗi tiếng Việt chuẩn xác nhất

                // THỰC HIỆN SẮP XẾP TRÊN MEMORY (Chuẩn hóa loại bỏ khoảng trắng thừa và không phân biệt hoa thường)
                var sortedData = data
                    .OrderBy(x => (x.TenMayTinh ?? "").Trim().ToLower(), StringComparer.Ordinal)
                    .ThenByDescending(x => x.IdThietBi)
                    .ToList();

                return Json(new { success = true, data = sortedData });
            }
            catch (Exception ex) { return Json(new { success = false, message = ex.Message }); }
        }

        // HÀM LẤY LỊCH SỬ CÁC LẦN CHECK VÀ BẰNG CHỨNG ẢNH CỦA THIẾT BỊ
        [HttpGet("/QLKiemKe/GetKkBangChungChecks")]
        public IActionResult GetKkBangChungChecks(int idThietBi)
        {
            try
            {
                var data = _context.KkBangChungChecks
                    .Where(x => x.IdThietBi == idThietBi)
                    .Select(x => new
                    {
                        x.IdBangChung,
                        x.IdThietBi,
                        x.ThoiGianCheck,
                        x.DuongDanAnh,
                        x.GhiChu
                    })
                    .OrderByDescending(x => x.ThoiGianCheck) // Mới nhất xếp lên đầu
                    .ToList();

                return Json(new { success = true, data = data });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = ex.Message });
            }
        }

        // HÀM LƯU THIẾT BỊ (CẬP NHẬT: CHECK TRÙNG THEO LOẠI & THAM SỐ LƯU NHÂN BẢN SAVE AS NEW)
        [HttpPost("/QLKiemKe/SaveKkThietBi")]
        public async Task<IActionResult> SaveKkThietBi([FromForm] KkThietBi model, IFormFile? AnhThietBi, bool saveAsNew = false)
        {
            try
            {
                // Nếu người dùng chọn lưu thành bản sao mới, reset Id về 0 để sinh khóa tự động
                if (saveAsNew)
                {
                    model.IdThietBi = 0;
                }

                bool isDuplicate = false;
                if (!string.IsNullOrWhiteSpace(model.TenMayTinh))
                {
                    string trimmedTarget = model.TenMayTinh.Trim().ToLower(); // Chuyển target về chữ thường để so sánh
                    string currentLoai = (model.LoaiThietBi ?? "").Trim().ToLower();

                    // THAY ĐỔI: So sánh đồng thời cả tên máy tính VÀ loại thiết bị giống nhau thì mới tính là trùng
                    // Bỏ qua các thiết bị đã nằm trong Thùng rác (NgayXoa != null), vì chúng không còn "đang tồn tại" nên không được tính là trùng
                    if (model.IdThietBi == 0)
                    {
                        isDuplicate = _context.KkThietBis.Any(x => x.TenMayTinh != null
                            && x.TenMayTinh.Trim().ToLower() == trimmedTarget
                            && x.LoaiThietBi != null
                            && x.LoaiThietBi.Trim().ToLower() == currentLoai
                            && x.NgayXoa == null);
                    }
                    else
                    {
                        isDuplicate = _context.KkThietBis.Any(x => x.TenMayTinh != null
                            && x.TenMayTinh.Trim().ToLower() == trimmedTarget
                            && x.LoaiThietBi != null
                            && x.LoaiThietBi.Trim().ToLower() == currentLoai
                            && x.IdThietBi != model.IdThietBi
                            && x.NgayXoa == null);
                    }
                }

                if (isDuplicate)
                    return Json(new { success = false, message = $"Tên máy tính (Hostname) '{model.TenMayTinh}' kèm Loại thiết bị '{model.LoaiThietBi}' đã tồn tại trong hệ thống. Vui lòng kiểm tra lại!" });

                if (string.IsNullOrWhiteSpace(model.TenViTri)) model.TenViTri = "";

                string action = model.IdThietBi == 0 ? (saveAsNew ? "Nhân bản mới" : "Thêm mới") : "Cập nhật";
                int objId = model.IdThietBi;

                var statusCheck = _context.KkTrangThais.Find(model.IdTrangThai);
                string statusName = statusCheck != null ? (statusCheck.TenTrangThai ?? "Chưa rõ") : "Chưa rõ";

                // --- XỬ LÝ UPLOAD VÀ LƯU ẢNH LÊN THƯ MỤC MẠNG ---
                string? newImageFileName = null;
                if (AnhThietBi != null && AnhThietBi.Length > 0)
                {
                    string networkPath = @"\\10.0.60.30\BPVN-Fileserver\Public\IT-Information Technology Dept\5.E-Form\AnhKiemKe";

                    // Tạo folder nếu chưa tồn tại
                    if (!Directory.Exists(networkPath))
                    {
                        Directory.CreateDirectory(networkPath);
                    }

                    string imgExtension = Path.GetExtension(AnhThietBi.FileName) ?? "";
                    if (string.IsNullOrEmpty(imgExtension)) imgExtension = ".jpg";

                    string timeStamp = DateTime.Now.ToString("yyyyMMddHHmmss");
                    // Xóa ký tự đặc biệt khỏi Tên vị trí để làm tên file (Sử dụng StringSplitOptions để tối ưu hóa)
                    string safeName = string.Join("_", (model.TenViTri ?? "").Split(Path.GetInvalidFileNameChars(), StringSplitOptions.RemoveEmptyEntries));
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
                    model.QuyCach = model.QuyCach?.Trim(); // Gán Quy cách khi thêm mới

                    // Gán các trường mới khi thêm mới dữ liệu
                    model.Seribacode = model.Seribacode?.Trim();
                    model.HanBaoHanh = model.HanBaoHanh;
                    model.WinLicense = model.WinLicense;
                    model.OfficeLicense = model.OfficeLicense;

                    _context.KkThietBis.Add(model);
                }
                else
                {
                    var existing = _context.KkThietBis.Find(model.IdThietBi);
                    if (existing != null)
                    {
                        existing.TenViTri = model.TenViTri ?? "";
                        existing.TenMayTinh = model.TenMayTinh ?? "";
                        existing.TenDangNhap = model.TenDangNhap ?? "";
                        existing.LoaiThietBi = model.LoaiThietBi ?? "";
                        existing.GhiChu = model.GhiChu ?? "";
                        existing.IdNguoiDung = model.IdNguoiDung;
                        existing.IdcongTy = model.IdcongTy;
                        existing.IdboPhan = model.IdboPhan;
                        existing.IdTrangThai = model.IdTrangThai;
                        existing.NgayCapNhat = DateTime.Now;
                        existing.ThoiGianCheck = DateTime.Now; // Làm mới thời gian check khi cập nhật
                        existing.QuyCach = model.QuyCach?.Trim(); // BỔ SUNG: Cập nhật trường QuyCach vào cơ sở dữ liệu

                        // CẬP NHẬT ĐẦY ĐỦ CÁC TRƯỜNG DỮ LIỆU MỚI VÀO CƠ SỞ DỮ LIỆU
                        existing.Seribacode = model.Seribacode?.Trim();
                        existing.HanBaoHanh = model.HanBaoHanh;
                        existing.WinLicense = model.WinLicense;
                        existing.OfficeLicense = model.OfficeLicense;

                        // Chỉ cập nhật DuongDanAnh nếu có ảnh mới upload lên
                        if (newImageFileName != null)
                        {
                            existing.DuongDanAnh = newImageFileName;
                        }

                        // Nếu cập nhật thoát khỏi trạng thái Xóa thì clear ngày xóa & lý do xóa
                        if (!statusName.Equals("xóa", StringComparison.OrdinalIgnoreCase))
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
                    if (user != null) tenNguoiDung = user.HoTen ?? "Chưa cấp phát";
                }

                string tenBoPhan = "Chưa gắn";
                if (model.IdboPhan.HasValue)
                {
                    var bp = _context.KkBoPhans.Find(model.IdboPhan.Value);
                    if (bp != null) tenBoPhan = bp.TenBoPhan ?? "Chưa gắn";
                }

                string chiTietLog = $"Vị trí: {model.TenViTri} | Máy tính: {model.TenMayTinh} | Trạng thái: {statusName} | Account: {model.TenDangNhap} | N.Dùng: {tenNguoiDung} | B.Phận: {tenBoPhan} | Loại: {model.LoaiThietBi} | Quy cách: {model.QuyCach}";
                GhiLichSu(action, "Thiết Bị", objId, chiTietLog);

                return Json(new { success = true, message = "Lưu thiết bị thành công!" });
            }
            catch (Exception ex) { return Json(new { success = false, message = ex.Message }); }
        }

        public class ThemNhanhTaiSanRow
        {
            public string? Serial { get; set; }
            public string? LoaiThietBi { get; set; }
            public string? QuyCach { get; set; }
            public string? TenViTri { get; set; }
            public IFormFile? Anh { get; set; }
        }

        // HÀM THÊM NHANH TÀI SẢN KHÁC (Súng bắn mã vạch, Điện thoại bàn, Máy chiếu, PDA, ...) TỪ TRANG XÁC NHẬN TÀI SẢN
        // Dùng chung Tài khoản/Công ty/Bộ phận đã nhập ở form Xác nhận tài sản. Ghép trùng theo Serial + Loại thiết bị: có rồi thì Cập nhật, chưa có thì Thêm mới.
        [HttpPost("/QLKiemKe/ThemNhanhTaiSanKhac")]
        public async Task<IActionResult> ThemNhanhTaiSanKhac(string taikhoan, string matkhau, int? idCongTy, int? idBoPhan, [FromForm] List<ThemNhanhTaiSanRow> rows)
        {
            if (string.IsNullOrWhiteSpace(taikhoan) || string.IsNullOrWhiteSpace(matkhau))
            {
                return Json(new { success = false, message = "Vui lòng nhập Tài khoản và Mật khẩu ở phần Xác nhận tài sản trước." });
            }
            if (!idCongTy.HasValue)
            {
                return Json(new { success = false, message = "Vui lòng chọn Công ty." });
            }
            if (!idBoPhan.HasValue)
            {
                return Json(new { success = false, message = "Vui lòng chọn Bộ phận." });
            }
            if (rows == null || rows.Count == 0)
            {
                return Json(new { success = false, message = "Vui lòng thêm ít nhất một thiết bị." });
            }

            try
            {
                var user = await XacThucTaiKhoanHybrid(taikhoan, matkhau);
                if (user == null)
                {
                    return Json(new { success = false, message = "Tài khoản hoặc mật khẩu xác nhận không chính xác." });
                }

                for (int i = 0; i < rows.Count; i++)
                {
                    if (string.IsNullOrWhiteSpace(rows[i].Serial))
                        return Json(new { success = false, message = $"Dòng {i + 1}: Vui lòng nhập Serial." });
                    if (string.IsNullOrWhiteSpace(rows[i].LoaiThietBi))
                        return Json(new { success = false, message = $"Dòng {i + 1}: Vui lòng chọn Loại thiết bị." });
                }

                string networkPath = @"\\10.0.60.30\BPVN-Fileserver\Public\IT-Information Technology Dept\5.E-Form\AnhKiemKe";
                int soThem = 0, soCapNhat = 0;

                // Trạng thái mặc định gán cho thiết bị THÊM MỚI (không đổi trạng thái của thiết bị đã tồn tại khi cập nhật)
                int? idTrangThaiMacDinh = _context.KkTrangThais
                    .Where(x => x.TenTrangThai == "Đang hoạt động")
                    .Select(x => (int?)x.IdTrangThai)
                    .FirstOrDefault();

                foreach (var row in rows)
                {
                    string serialTrim = row.Serial!.Trim();
                    string loaiTrim = row.LoaiThietBi!.Trim();

                    var existing = _context.KkThietBis.FirstOrDefault(x =>
                        x.Seribacode != null && x.Seribacode.Trim().ToLower() == serialTrim.ToLower() &&
                        x.LoaiThietBi != null && x.LoaiThietBi.Trim().ToLower() == loaiTrim.ToLower());

                    string? tenFileAnhMoi = null;
                    if (row.Anh != null && row.Anh.Length > 0)
                    {
                        if (!Directory.Exists(networkPath)) Directory.CreateDirectory(networkPath);

                        string imgExtension = Path.GetExtension(row.Anh.FileName) ?? "";
                        if (string.IsNullOrEmpty(imgExtension)) imgExtension = ".jpg";
                        string timeStamp = DateTime.Now.ToString("yyyyMMddHHmmssfff");
                        string safeName = string.Join("_", (row.TenViTri ?? loaiTrim).Split(Path.GetInvalidFileNameChars(), StringSplitOptions.RemoveEmptyEntries));
                        if (string.IsNullOrEmpty(safeName)) safeName = "TB";

                        tenFileAnhMoi = $"AnhTB_{safeName}_{timeStamp}{imgExtension}";
                        string imgFullPath = Path.Combine(networkPath, tenFileAnhMoi);
                        using (var imgStream = new FileStream(imgFullPath, FileMode.Create))
                        {
                            await row.Anh.CopyToAsync(imgStream);
                        }
                    }

                    KkThietBi thietBi;
                    bool laCapNhat = existing != null;
                    if (existing != null)
                    {
                        if (!string.IsNullOrWhiteSpace(row.TenViTri)) existing.TenViTri = row.TenViTri.Trim();
                        if (!string.IsNullOrWhiteSpace(row.QuyCach)) existing.QuyCach = row.QuyCach.Trim();
                        existing.IdcongTy = idCongTy;
                        existing.IdboPhan = idBoPhan;
                        existing.TenDangNhap = taikhoan;
                        existing.IdNguoiDung = user.IdNguoiDung;
                        existing.NgayCapNhat = DateTime.Now;
                        existing.ThoiGianCheck = DateTime.Now;
                        if (tenFileAnhMoi != null) existing.DuongDanAnh = tenFileAnhMoi;
                        thietBi = existing;
                        soCapNhat++;
                    }
                    else
                    {
                        thietBi = new KkThietBi
                        {
                            TenViTri = row.TenViTri?.Trim() ?? "",
                            LoaiThietBi = loaiTrim,
                            QuyCach = row.QuyCach?.Trim(),
                            Seribacode = serialTrim,
                            TenDangNhap = taikhoan,
                            IdNguoiDung = user.IdNguoiDung,
                            IdcongTy = idCongTy,
                            IdboPhan = idBoPhan,
                            IdTrangThai = idTrangThaiMacDinh,
                            DuongDanAnh = tenFileAnhMoi,
                            NgayTao = DateTime.Now,
                            NgayCapNhat = DateTime.Now,
                            ThoiGianCheck = DateTime.Now
                        };
                        _context.KkThietBis.Add(thietBi);
                        soThem++;
                    }

                    _context.SaveChanges();

                    _context.KkBangChungChecks.Add(new KkBangChungCheck
                    {
                        IdThietBi = thietBi.IdThietBi,
                        ThoiGianCheck = DateTime.Now,
                        DuongDanAnh = tenFileAnhMoi,
                        GhiChu = string.IsNullOrWhiteSpace(row.TenViTri) ? $"Xác nhận kiểm kê ({loaiTrim})" : row.TenViTri!.Trim()
                    });
                    _context.SaveChanges();

                    GhiLichSu(laCapNhat ? "Cập nhật" : "Thêm mới", "Thiết Bị", thietBi.IdThietBi,
                        $"[Tài sản khác] Serial: {serialTrim} | Loại: {loaiTrim} | Vị trí: {row.TenViTri} | Account: {taikhoan}");
                }

                return Json(new { success = true, message = $"Đã xử lý {rows.Count} thiết bị (Thêm mới: {soThem}, Cập nhật: {soCapNhat})." });
            }
            catch (Exception ex)
            {
                var chiTietLoi = ex.InnerException != null ? ex.InnerException.Message : ex.Message;
                return Json(new { success = false, message = $"Lỗi hệ thống khi thêm tài sản: {chiTietLoi}" });
            }
        }

        public class CapNhatLoaiThietBiRequest
        {
            public int IdMay { get; set; }
            public string? LoaiThietBi { get; set; }
        }

        // HÀM SỬA TRỰC TIẾP LOẠI THIẾT BỊ (Laptop/PC/MayAo/Server) CỦA 1 MÁY TÍNH TỪ TRANG DANH SÁCH
        [HttpPost("/QLKiemKe/CapNhatLoaiThietBiMay")]
        public async Task<IActionResult> CapNhatLoaiThietBiMay([FromBody] CapNhatLoaiThietBiRequest req)
        {
            try
            {
                if (req == null) return Json(new { success = false, message = "Dữ liệu không hợp lệ." });

                var may = await _context.TscnThongTinMays.FindAsync(req.IdMay);
                if (may == null) return Json(new { success = false, message = "Không tìm thấy máy tính." });

                may.LoaiThietBi = string.IsNullOrWhiteSpace(req.LoaiThietBi) ? null : req.LoaiThietBi.Trim();
                await _context.SaveChangesAsync();

                return Json(new { success = true, message = "Cập nhật loại thiết bị thành công!" });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = ex.Message });
            }
        }

        // HÀM TỰ ĐỘNG PHÂN LOẠI HÀNG LOẠT CÁC MÁY ĐANG TRỐNG "LOẠI THIẾT BỊ" (dùng ở nút "Tự động phân loại" trang Danh sách tất cả máy tính)
        public class TuDongPhanLoaiRequest
        {
            public List<int>? DanhSachIdMay { get; set; }
        }

        [HttpPost("/QLKiemKe/TuDongPhanLoaiThietBiChuaPhanLoai")]
        public async Task<IActionResult> TuDongPhanLoaiThietBiChuaPhanLoai([FromBody] TuDongPhanLoaiRequest? req)
        {
            try
            {
                // Nếu Client gửi kèm danh sách IdMay đã tích chọn -> chỉ xử lý đúng các máy đó và GHI ĐÈ luôn Loại thiết bị cũ (nếu đoán được)
                bool coChonMay = req?.DanhSachIdMay != null && req.DanhSachIdMay.Count > 0;

                var query = _context.TscnThongTinMays.AsQueryable();
                query = coChonMay
                    ? query.Where(m => req!.DanhSachIdMay!.Contains(m.IdMay))
                    : query.Where(m => m.LoaiThietBi == null || m.LoaiThietBi == "");

                var dsMayXuLy = query.ToList();

                int soDaPhanLoai = 0;
                foreach (var may in dsMayXuLy)
                {
                    var loaiDoan = TuPhanLoaiThietBiTheoDongMay(may.DongMay);
                    if (!string.IsNullOrWhiteSpace(loaiDoan))
                    {
                        may.LoaiThietBi = loaiDoan;
                        soDaPhanLoai++;
                    }
                }

                await _context.SaveChangesAsync();

                int soKhongXacDinh = dsMayXuLy.Count - soDaPhanLoai;
                return Json(new { success = true, soDaPhanLoai = soDaPhanLoai, soKhongXacDinh = soKhongXacDinh });
            }
            catch (Exception ex)
            {
                var chiTietLoi = ex.InnerException != null ? ex.InnerException.Message : ex.Message;
                return Json(new { success = false, message = $"Lỗi hệ thống khi tự động phân loại: {chiTietLoi}" });
            }
        }

        // Tự phân loại nhanh Laptop/PC/MayAo/Server dựa theo chuỗi Dòng máy (Model), dùng khi máy vừa quét lần đầu chưa có Loại thiết bị
        private string? TuPhanLoaiThietBiTheoDongMay(string? dongMay)
        {
            if (string.IsNullOrWhiteSpace(dongMay)) return null;
            string d = dongMay.Trim().ToLower();

            if (d.Contains("virtual machine") || d.Contains("vmware") || d.Contains("virtualbox") || d.Contains("hyper-v") || d.Contains("qemu") || d.Contains("kvm"))
                return "MayAo";
            if (d.Contains("proliant"))
                return "Server";
            if (d.Contains("thinkpad") || d.Contains("notebook") || d.Contains("elitebook") || d.Contains("probook") ||
                d.Contains("zbook") || d.Contains("legion") || d.Contains("ideapad") || d.Contains("latitude") ||
                d.Contains("macbook") || d.Contains("zhaoyang") ||
                // Bổ sung thêm các dòng Laptop phổ biến khác để giảm số máy không đoán được (Acer/HP/Lenovo/Dell/LG/Microsoft/Asus/MSI)
                d.Contains("travelmate") || d.Contains("swift") || d.Contains("spin") || d.Contains("aspire a") ||
                d.Contains("yoga") || d.Contains("thinkbook") || d.Contains("spectre") || d.Contains("pavilion x") ||
                d.Contains("surface laptop") || d.Contains("surface book") || d.Contains("surface go") ||
                d.Contains("gram") || d.Contains("chromebook") || d.Contains("vivobook") || d.Contains("zenbook") ||
                d.Contains("rog strix") || d.Contains("rog zephyrus") || d.Contains("modern 14") || d.Contains("modern 15"))
                return "Laptop";
            // Thêm "slim" (case Dell Slim - máy bàn dạng mỏng) và hậu tố " t" (VD: "Vostro 3020 T" - ký hiệu kiểu dáng Tower của Dell)
            if (d.Contains("desktop") || d.Contains("sff") || d.Contains("small form factor") || d.Contains("microtower") ||
                d.Contains("tower") || d.Contains("workstation") || d.EndsWith(" mt") || d.EndsWith(" dm") || d.EndsWith(" t") ||
                d.Contains("base model") || d.Contains("inspiron") || d.Contains("slim") ||
                // Bổ sung thêm các dòng PC bàn phổ biến khác (Dell/HP/Lenovo/Acer)
                d.Contains("optiplex") || d.Contains("thinkcentre") || d.Contains("thinkstation") ||
                d.Contains("elitedesk") || d.Contains("prodesk") || d.Contains("veriton") || d.Contains("aio") ||
                d.Contains("all-in-one") || d.Contains("all in one"))
                return "PC";

            // Dell Vostro dùng chung tên dòng máy cho cả Laptop và PC bàn, chỉ phân biệt được qua hậu tố kiểu dáng đã xét ở trên
            // (T/SFF/MT/Tower/Slim). Nếu không có hậu tố kiểu dáng nào thì đây là dòng số thuần (VD: Vostro 3400/3500) -
            // trên thực tế các model này đều thuộc dòng Laptop 14"/15" của Dell.
            if (d.Contains("vostro"))
                return "Laptop";

            return null; // Không đủ dấu hiệu để tự phân loại theo tên - hàm gọi sẽ tự áp mặc định, admin có thể sửa tay sau ở trang danh sách máy tính
        }

        // Chuyển Loại thiết bị của TSCN_ThongTinMay (PC/Laptop/MayAo/Server) sang đúng tên danh mục dùng trong KK_LoaiThietBi
        private string ChuanHoaLoaiThietBiSangDanhMuc(string loaiTuMay)
        {
            switch ((loaiTuMay ?? "").Trim())
            {
                case "PC": return "Máy tính";
                case "MayAo": return "Máy ảo";
                default: return (loaiTuMay ?? "").Trim(); // Laptop, Server đã đúng tên sẵn
            }
        }

        // Tìm thiết bị KK_ThietBi đã tồn tại tương ứng với 1 máy tính (TSCN_ThongTinMay) để CẬP NHẬT thay vì thêm trùng, ưu tiên khớp theo:
        // 1) Đã liên kết cùng IdMay từ lần đồng bộ trước, 2) Số Serial trùng khớp (định danh phần cứng duy nhất - đáng tin cậy nhất),
        // 3) Tên máy trùng khớp nhưng bản ghi cũ CHƯA có Serial (VD: được tạo tay/từ Tài sản khác trước đó) -> nhận là cùng 1 máy và bổ sung Serial vào,
        //    tránh vừa đổi Loại thiết bị (PC/Laptop) giữa các lần vừa tạo Serial đều làm phát sinh bản ghi trùng như logic khớp theo Tên máy + Loại thiết bị cũ.
        private KkThietBi? TimThietBiTrungTheoMayTinh(int idMay, string tenMay, string? seriMay)
        {
            string trimmedTen = tenMay.Trim().ToLower();
            string trimmedSerial = (seriMay ?? "").Trim().ToLower();

            return _context.KkThietBis.FirstOrDefault(x =>
                x.IdMay == idMay ||
                (trimmedSerial != "" && x.Seribacode != null && x.Seribacode.Trim().ToLower() == trimmedSerial) ||
                (x.TenMayTinh != null && x.TenMayTinh.Trim().ToLower() == trimmedTen &&
                 (x.Seribacode == null || x.Seribacode.Trim() == "")));
        }

        // Cắt bớt chuỗi cho vừa giới hạn độ dài cột đích trong DB, tránh lỗi "String or binary data would be truncated"
        private static string? CatChuoiToiDa(string? chuoi, int doDaiToiDa)
        {
            if (string.IsNullOrWhiteSpace(chuoi)) return chuoi?.Trim();
            var trimmed = chuoi.Trim();
            return trimmed.Length > doDaiToiDa ? trimmed.Substring(0, doDaiToiDa) : trimmed;
        }

        // Trích ra "bản" (edition) của Windows kèm trạng thái có key kích hoạt hay không, VD: "Professional - Có bản quyền", "Enterprise - Chưa có bản quyền"
        // Tool local trả về nhiều định dạng chuỗi khác nhau tùy phiên bản (VD: "Name: Windows(R), Professional edition | ... | License Status: Đã kích hoạt (Licensed)..."
        // hoặc "Đã kích hoạt bản quyền (Licensed) | Đang dùng key: Windows(R), Professional edition - XXXXX (loại: OEM)") nên chỉ dựa vào từ khóa trạng thái + tên edition, bỏ qua Partial Product Key/Description dài dòng.
        private static string? TrichPhienBanWindows(string? banQuyenWinRaw)
        {
            if (string.IsNullOrWhiteSpace(banQuyenWinRaw)) return null;
            string d = banQuyenWinRaw.ToLower();

            string trangThai;
            if (d.Contains("đã kích hoạt")) trangThai = "Có bản quyền";
            else if (d.Contains("không phát hiện") || d.Contains("chưa kích hoạt") || d.Contains("hết hạn") || d.Contains("notification"))
                trangThai = "Chưa có bản quyền";
            else
                trangThai = "Không xác định";

            var match = System.Text.RegularExpressions.Regex.Match(banQuyenWinRaw, @"Windows\(R\),\s*([^|]+?)\s*edition", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
            string? edition = match.Success ? match.Groups[1].Value.Trim() : null;

            string ketQua = string.IsNullOrWhiteSpace(edition) ? trangThai : $"{edition} - {trangThai}";
            return CatChuoiToiDa(ketQua, 150);
        }

        public class DongBoMayTinhRequest
        {
            public List<int> DanhSachIdMay { get; set; } = new();
            public int? IdTrangThai { get; set; }
        }

        // HÀM ĐỒNG BỘ MÁY TÍNH (TSCN_ThongTinMay) SANG THIẾT BỊ (KK_ThietBi)
        // Trùng theo cặp TenMayTinh + LoaiThietBi thì cập nhật, không trùng thì thêm mới. TenViTri/IDCongTy/IDBoPhan/Ảnh bỏ qua, chỉ IdTrangThai được gán nếu người dùng chọn.
        // Bản quyền Windows được đồng bộ theo đúng dữ liệu quét được từ máy (BanQuyenWin). Bản quyền Office KHÔNG đụng vào ở luồng này -
        // trường này được quản lý riêng qua chức năng nhập nhanh Excel (ImportOfficeLicenseExcel), tránh bị ghi đè ngoài ý muốn khi đồng bộ.
        [HttpPost("/QLKiemKe/DongBoMayTinhSangThietBi")]
        public async Task<IActionResult> DongBoMayTinhSangThietBi([FromBody] DongBoMayTinhRequest req)
        {
            try
            {
                if (req == null || req.DanhSachIdMay == null || !req.DanhSachIdMay.Any())
                    return Json(new { success = false, message = "Chưa chọn máy tính nào để đồng bộ." });

                int soThemMoi = 0, soCapNhat = 0;
                var dsBoQua = new List<string>();

                // Trạng thái mặc định gán cho thiết bị THÊM MỚI khi người dùng không chọn trạng thái nào trong hộp thoại
                int? idTrangThaiMacDinh = _context.KkTrangThais
                    .Where(x => x.TenTrangThai == "Đang hoạt động")
                    .Select(x => (int?)x.IdTrangThai)
                    .FirstOrDefault();

                var danhSachMay = _context.TscnThongTinMays
                    .Where(m => req.DanhSachIdMay.Contains(m.IdMay))
                    .ToList();

                foreach (var may in danhSachMay)
                {
                    if (string.IsNullOrWhiteSpace(may.TenMay) || string.IsNullOrWhiteSpace(may.LoaiThietBi))
                    {
                        dsBoQua.Add($"{may.TenMay ?? "(Không tên)"} (thiếu Tên máy hoặc Loại thiết bị)");
                        continue;
                    }

                    // Chuẩn hóa Loại thiết bị của TSCN_ThongTinMay (PC/Laptop/MayAo/Server) sang đúng tên danh mục dùng ở KK_LoaiThietBi
                    string loaiThietBiChuan = ChuanHoaLoaiThietBiSangDanhMuc(may.LoaiThietBi);

                    // Khớp theo IdMay đã liên kết / Serial trùng / Tên máy trùng nhưng chưa có Serial - để CẬP NHẬT thay vì thêm trùng
                    var existing = TimThietBiTrungTheoMayTinh(may.IdMay, may.TenMay, may.SeriMay);

                    if (existing != null)
                    {
                        if (!string.IsNullOrWhiteSpace(may.SeriMay)) existing.Seribacode = may.SeriMay.Trim();
                        if (!string.IsNullOrWhiteSpace(may.DongMay)) existing.QuyCach = may.DongMay.Trim();
                        if (may.IdNguoiDung.HasValue) existing.IdNguoiDung = may.IdNguoiDung;
                        if (req.IdTrangThai.HasValue) existing.IdTrangThai = req.IdTrangThai;
                        if (!string.IsNullOrWhiteSpace(may.BanQuyenWin)) existing.WinLicense = TrichPhienBanWindows(may.BanQuyenWin);
                        // Không đụng vào existing.OfficeLicense - trường này chỉ được cập nhật qua chức năng nhập nhanh Excel riêng
                        existing.LoaiThietBi = loaiThietBiChuan;
                        existing.IdMay = may.IdMay;
                        existing.NgayCapNhat = DateTime.Now;
                        existing.ThoiGianCheck = DateTime.Now;
                        soCapNhat++;
                    }
                    else
                    {
                        var moi = new KkThietBi
                        {
                            TenViTri = "",
                            TenMayTinh = may.TenMay.Trim(),
                            LoaiThietBi = loaiThietBiChuan,
                            Seribacode = may.SeriMay?.Trim(),
                            QuyCach = may.DongMay?.Trim(),
                            IdNguoiDung = may.IdNguoiDung,
                            IdTrangThai = req.IdTrangThai ?? idTrangThaiMacDinh,
                            WinLicense = TrichPhienBanWindows(may.BanQuyenWin),
                            IdMay = may.IdMay,
                            NgayTao = DateTime.Now,
                            NgayCapNhat = DateTime.Now,
                            ThoiGianCheck = DateTime.Now
                        };
                        _context.KkThietBis.Add(moi);
                        soThemMoi++;
                    }
                }

                await _context.SaveChangesAsync();

                GhiLichSu("Đồng bộ", "Thiết Bị", 0, $"Đồng bộ từ danh sách máy tính: {soThemMoi} thêm mới, {soCapNhat} cập nhật, {dsBoQua.Count} bỏ qua.");

                return Json(new { success = true, themMoi = soThemMoi, capNhat = soCapNhat, boQua = dsBoQua });
            }
            catch (Exception ex)
            {
                var chiTietLoi = ex.InnerException != null ? ex.InnerException.Message : ex.Message;
                return Json(new { success = false, message = $"Lỗi hệ thống khi đồng bộ: {chiTietLoi}" });
            }
        }

        // HÀM XUẤT ẢNH TỪ FILE SERVER RA TRÌNH DUYỆT (THƯ MỤC THIẾT BỊ CHÍNH)
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

        // BỔ SUNG SỬA LỖI: HÀM XUẤT ẢNH BẰNG CHỨNG TỪ THƯ MỤC RIÊNG "BangChungKiemKe" RA VIEW
        [HttpGet("/QLKiemKe/GetEvidenceImage")]
        public IActionResult GetEvidenceImage(string fileName)
        {
            if (string.IsNullOrEmpty(fileName)) return NotFound();

            string networkPath = @"\\10.0.60.30\BPVN-Fileserver\Public\IT-Information Technology Dept\5.E-Form\BangChungKiemKe";
            string filePath = Path.Combine(networkPath, fileName);

            // Ảnh xác nhận tài sản (tenFileAnhMoi từ XacNhanTaiSanMaMay) được lưu vật lý trong thư mục "AnhKiemKe"
            // nhưng vẫn được ghi vào KkBangChungCheck.DuongDanAnh -> cần dò thêm thư mục này nếu không thấy ở BangChungKiemKe
            if (!System.IO.File.Exists(filePath))
            {
                string fallbackPath = @"\\10.0.60.30\BPVN-Fileserver\Public\IT-Information Technology Dept\5.E-Form\AnhKiemKe";
                filePath = Path.Combine(fallbackPath, fileName);
            }

            if (!System.IO.File.Exists(filePath)) return NotFound();

            var ext = Path.GetExtension(filePath).ToLowerInvariant();
            string mimeType = "image/jpeg";

            if (ext == ".png") mimeType = "image/png";
            else if (ext == ".gif") mimeType = "image/gif";
            else if (ext == ".webp") mimeType = "image/webp";

            return PhysicalFile(filePath, mimeType);
        }

        // HÀM XÁC NHẬN CHECK THIẾT BỊ (MỚI: LƯU LỊCH SỬ BẰNG CHỨNG KIỂM KÊ VÀO BẢNG RIÊNG & THƯ MỤC MẠNG RIÊNG)
        [HttpPost("/QLKiemKe/XacNhanCheck")]
        public async Task<IActionResult> XacNhanCheck([FromForm] int idThietBi, [FromForm] IFormFile? AnhBangChung, [FromForm] string? ghiChuCheck)
        {
            try
            {
                var thietBi = await _context.KkThietBis.FindAsync(idThietBi);
                if (thietBi == null) return Json(new { success = false, message = "Không tìm thấy thiết bị trong hệ thống." });

                DateTime currentCheckTime = DateTime.Now;

                // 1. Cập nhật thời gian kiểm tra cuối cùng ở bảng thiết bị chính
                thietBi.ThoiGianCheck = currentCheckTime;
                _context.Update(thietBi);

                // 2. Xử lý lưu tệp ảnh bằng chứng vào thư mục mạng BangChungKiemKe
                string? evidenceFileName = null;
                if (AnhBangChung != null && AnhBangChung.Length > 0)
                {
                    string networkPath = @"\\10.0.60.30\BPVN-Fileserver\Public\IT-Information Technology Dept\5.E-Form\BangChungKiemKe";

                    if (!Directory.Exists(networkPath))
                    {
                        Directory.CreateDirectory(networkPath);
                    }

                    string imgExtension = Path.GetExtension(AnhBangChung.FileName) ?? "";
                    if (string.IsNullOrEmpty(imgExtension)) imgExtension = ".jpg";

                    string timeStamp = currentCheckTime.ToString("yyyyMMddHHmmss");
                    string safeName = string.Join("_", (thietBi.TenViTri ?? "").Split(Path.GetInvalidFileNameChars(), StringSplitOptions.RemoveEmptyEntries));
                    if (string.IsNullOrEmpty(safeName)) safeName = "TB";

                    evidenceFileName = $"BC_{safeName}_{idThietBi}_{timeStamp}{imgExtension}";
                    string imgFullPath = Path.Combine(networkPath, evidenceFileName);

                    using (var imgStream = new FileStream(imgFullPath, FileMode.Create))
                    {
                        await AnhBangChung.CopyToAsync(imgStream);
                    }
                }

                // 3. Ghi dữ liệu vào bảng lịch sử bằng chứng kiểm kê mới
                var bangChung = new KkBangChungCheck
                {
                    IdThietBi = idThietBi,
                    ThoiGianCheck = currentCheckTime,
                    DuongDanAnh = evidenceFileName,
                    GhiChu = string.IsNullOrWhiteSpace(ghiChuCheck) ? "Xác nhận kiểm kê" : ghiChuCheck.Trim()
                };
                _context.Add(bangChung);

                await _context.SaveChangesAsync();

                GhiLichSu("Xác nhận Check", "Thiết Bị", idThietBi, $"Cập nhật thời gian check và lưu bằng chứng kiểm kê. Có tệp đính kèm: {(evidenceFileName != null ? "Có" : "Không")}");

                return Json(new { success = true, message = "Cập nhật thời gian kiểm tra và lưu bằng chứng thành công!" });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = ex.Message });
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
                    // string.Equals(..., StringComparison.OrdinalIgnoreCase) không dịch được sang SQL nên EF Core ném lỗi runtime; dùng ToLower() để so sánh không phân biệt hoa/thường.
                    var statusXoa = _context.KkTrangThais
                        .FirstOrDefault(x => x.TenTrangThai != null && x.TenTrangThai.ToLower() == "xóa");

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
                return Json(new { success = true, message = "Đã chuyển thiết bị vào Thùng Rác!" });
            }
            catch (Exception ex) { return Json(new { success = false, message = ex.Message }); }
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
                    // string.Equals(..., StringComparison.OrdinalIgnoreCase) không dịch được sang SQL nên EF Core ném lỗi runtime; dùng ToLower() để so sánh không phân biệt hoa/thường.
                    var statusHoatDong = _context.KkTrangThais.FirstOrDefault(x => x.TenTrangThai != null && x.TenTrangThai.ToLower() == "đang hoạt động");

                    item.IdTrangThai = statusHoatDong?.IdTrangThai;
                    item.NgayXoa = null;
                    item.LyDoXoa = null;
                    item.NgayCapNhat = DateTime.Now;

                    _context.SaveChanges();

                    GhiLichSu("Khôi phục", "Thiết Bị", id, $"Đã khôi phục thiết bị: {item.TenMayTinh}");
                }
                return Json(new { success = true, message = "Khôi phục thiết bị thành công!" });
            }
            catch (Exception ex) { return Json(new { success = false, message = ex.Message }); }
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
                    string tenMay = item.TenMayTinh ?? item.TenViTri ?? "Không rõ";
                    _context.KkThietBis.Remove(item);
                    _context.SaveChanges();

                    GhiLichSu("Xóa vĩnh viễn", "Thiết Bị", id, $"Đã xóa vĩnh viễn thiết bị (Hostname/Mã: {tenMay}) khỏi thùng rác.");
                }
                return Json(new { success = true, message = "Đã xóa vĩnh viễn thiết bị khỏi hệ thống!" });
            }
            catch (Exception ex) { return Json(new { success = false, message = "Lỗi: Không thể xóa vì ràng buộc dữ liệu hoặc lỗi hệ thống. Chi tiết: " + ex.Message }); }
        }

        // HÀM 1: Lấy cục bộ dữ liệu JSON chi tiết đầy đủ phục vụ hiển thị nhanh qua Modal/API AJAX
        [HttpGet("/QLKiemKe/GetChiTietThietBi")]
        public IActionResult GetChiTietThietBi(int idThietBi)
        {
            try
            {
                var item = _context.KkThietBis
                    .Include(x => x.IdNguoiDungNavigation)
                    .Include(x => x.IdcongTyNavigation)
                    .Include(x => x.IdboPhanNavigation)
                    .Include(x => x.IdTrangThaiNavigation)
                    .FirstOrDefault(x => x.IdThietBi == idThietBi);

                if (item == null)
                {
                    return Json(new { success = false, message = "Không tìm thấy thiết bị hoặc thông tin vị trí này trong hệ thống." });
                }

                var detailData = new
                {
                    item.IdThietBi,
                    item.TenViTri,
                    item.TenMayTinh,
                    item.TenDangNhap,
                    item.LoaiThietBi,
                    item.GhiChu,
                    item.QuyCach,
                    item.Seribacode,
                    HanBaoHanh = item.HanBaoHanh.HasValue ? item.HanBaoHanh.Value.ToString("yyyy-MM-dd") : "",
                    item.WinLicense,
                    item.OfficeLicense,
                    item.DuongDanAnh,
                    NgayTao = item.NgayTao.HasValue ? item.NgayTao.Value.ToString("dd/MM/yyyy HH:mm:ss") : "",
                    NgayCapNhat = item.NgayCapNhat.HasValue ? item.NgayCapNhat.Value.ToString("dd/MM/yyyy HH:mm:ss") : "",
                    ThoiGianCheck = item.ThoiGianCheck.HasValue ? item.ThoiGianCheck.Value.ToString("dd/MM/yyyy HH:mm:ss") : "Chưa Check",
                    NgayXoa = item.NgayXoa.HasValue ? item.NgayXoa.Value.ToString("dd/MM/yyyy HH:mm:ss") : "",
                    item.LyDoXoa,
                    item.IdNguoiDung,
                    item.IdcongTy,
                    item.IdboPhan,
                    item.IdTrangThai,
                    TenNguoiDung = item.IdNguoiDungNavigation != null ? item.IdNguoiDungNavigation.HoTen : "Chưa cấp phát",
                    TkNguoiDung = item.IdNguoiDungNavigation != null ? item.IdNguoiDungNavigation.Tk : "N/A",
                    TenCongTy = item.IdcongTyNavigation != null ? item.IdcongTyNavigation.TenCongTy : "Chưa gắn",
                    TenBoPhan = item.IdboPhanNavigation != null ? item.IdboPhanNavigation.TenBoPhan : "Chưa gắn",
                    TenTrangThai = item.IdTrangThaiNavigation != null ? item.IdTrangThaiNavigation.TenTrangThai : "Chưa rõ"
                };

                return Json(new { success = true, data = detailData });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = "Lỗi hệ thống khi tải chi tiết: " + ex.Message });
            }
        }

        // HÀM ĐIỀU HƯỚNG: Trang độc lập sang View (ChiTietThietBi.cshtml) hiển thị đầy đủ thông tin thực thể dữ liệu
        [HttpGet("/QLKiemKe/ChiTietThietBi/{id}")]
        public IActionResult ChiTietThietBi(int id)
        {
            var item = _context.KkThietBis
                .Include(x => x.IdNguoiDungNavigation)
                .Include(x => x.IdTrangThaiNavigation)
                .Include(x => x.IdboPhanNavigation)
                .Include(x => x.IdcongTyNavigation)
                .FirstOrDefault(x => x.IdThietBi == id);

            if (item == null) return NotFound();

            // 1. LẤY NHẬT KÝ LỊCH SỬ THAO TÁC CỦA THIẾT BỊ NÀY
            // Map theo quy tắc: DoiTuong = "Thiết Bị" và IdDoiTuong = id của thiết bị, xếp mới nhất lên đầu
            ViewBag.LichSuThaoTac = _context.KkLichSuThaoTacs
                .Where(x => x.DoiTuong == "Thiết Bị" && x.IdDoiTuong == id)
                .OrderByDescending(x => x.ThoiGian)
                .ToList();

            // 2. LẤY DANH SÁCH BẰNG CHỨNG KIỂM KÊ KÈM ẢNH CHECK
            // Lấy từ bảng KK_BangChungCheck liên kết trực tiếp, xếp mới nhất lên đầu
            ViewBag.BangChungCheck = _context.KkBangChungChecks
                .Where(x => x.IdThietBi == id)
                .OrderByDescending(x => x.ThoiGianCheck)
                .ToList();

            return View("ChiTietThietBi", item); // Trả dữ liệu sang giao diện Razor View
        }

        // NHẬP NHANH TÌNH TRẠNG KEY OFFICE TỪ FILE EXCEL: Cột A = Tên máy tính, Cột B = Tình trạng (Activated / Not Activated)
        // Bỏ qua các dòng có Cột B trống hoặc không khớp Tên máy tính nào trong hệ thống. Dòng 1 được coi là tiêu đề nên luôn bỏ qua.
        // Chỉ áp dụng cho thiết bị loại "Máy tính" (PC/máy cây) và "Laptop" - các loại thiết bị khác (Màn hình, Máy in...) không có Office nên bỏ qua dù trùng tên máy.
        // Chuẩn hóa Activated/Not Activated về đúng 2 giá trị đã dùng sẵn trong hệ thống ("Có bản quyền" / "Không có bản quyền") để đồng bộ với datalist và bộ lọc hiện có.
        [HttpPost("/QLKiemKe/ImportOfficeLicenseExcel")]
        public IActionResult ImportOfficeLicenseExcel(IFormFile file)
        {
            if (file == null || file.Length == 0)
            {
                return Json(new { success = false, message = "Vui lòng chọn file Excel." });
            }

            try
            {
                int soDaCapNhat = 0;
                int soBoQuaTrong = 0;
                var dsKhongTimThay = new List<string>();
                var dsBoQuaKhacLoai = new List<string>();
                string[] loaiMayTinhHopLe = { "máy tính", "laptop" };

                using (var stream = new MemoryStream())
                {
                    file.CopyTo(stream);
                    using (var workbook = new ClosedXML.Excel.XLWorkbook(stream))
                    {
                        var worksheet = workbook.Worksheet(1);
                        var lastRow = worksheet.LastRowUsed()?.RowNumber() ?? 1;

                        for (int row = 2; row <= lastRow; row++)
                        {
                            string tenMay = worksheet.Cell(row, 1).GetString().Trim();
                            string tinhTrangGoc = worksheet.Cell(row, 2).GetString().Trim();

                            if (string.IsNullOrEmpty(tenMay)) continue;
                            if (string.IsNullOrEmpty(tinhTrangGoc)) { soBoQuaTrong++; continue; }

                            // Quy đổi về đúng chuẩn nhãn đang dùng trong hệ thống: Activated -> Có bản quyền, Not Activated -> Chưa có bản quyền,
                            // giá trị khác lạ không khớp 2 mẫu trên (nhưng không trống) thì coi là Không xác định.
                            string tinhTrangGocLower = tinhTrangGoc.Trim().ToLower();
                            string tinhTrang = tinhTrangGocLower == "not activated" ? "Chưa có bản quyền"
                                             : tinhTrangGocLower == "activated" ? "Có bản quyền"
                                             : "Không xác định";

                            var dsThietBiCungTen = _context.KkThietBis
                                .Where(x => x.TenMayTinh != null && x.TenMayTinh.Trim().ToLower() == tenMay.ToLower())
                                .ToList();

                            if (dsThietBiCungTen.Count == 0) { dsKhongTimThay.Add(tenMay); continue; }

                            // Chỉ cập nhật đúng thiết bị loại Máy tính/Laptop, các thiết bị khác trùng tên (VD: Màn hình cùng vị trí) thì bỏ qua
                            var dsThietBiKhop = dsThietBiCungTen
                                .Where(x => x.LoaiThietBi != null && loaiMayTinhHopLe.Contains(x.LoaiThietBi.Trim().ToLower()))
                                .ToList();

                            if (dsThietBiKhop.Count == 0) { dsBoQuaKhacLoai.Add(tenMay); continue; }

                            foreach (var thietBi in dsThietBiKhop)
                            {
                                thietBi.OfficeLicense = tinhTrang;
                                thietBi.NgayCapNhat = DateTime.Now;
                                soDaCapNhat++;
                            }
                        }

                        _context.SaveChanges();
                    }
                }

                GhiLichSu("Nhập Excel", "Thiết Bị", 0, $"Nhập nhanh tình trạng Key Office từ Excel: đã cập nhật {soDaCapNhat} thiết bị, bỏ qua {soBoQuaTrong} dòng trống, không tìm thấy {dsKhongTimThay.Count} tên máy, bỏ qua {dsBoQuaKhacLoai.Count} thiết bị khác loại Máy tính/Laptop.");

                return Json(new
                {
                    success = true,
                    soDaCapNhat,
                    soBoQuaTrong,
                    dsKhongTimThay,
                    dsBoQuaKhacLoai
                });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = "Lỗi khi đọc file Excel: " + ex.Message });
            }
        }

        #endregion


        #endregion

        // Giả định bạn đã inject DbContext vào Controller qua Constructor, ví dụ: _context
        // private readonly YourDbContext _context;
        #region check máy hiện tại 

        // 1. Action này chỉ trả về giao diện (View)
        [HttpGet("/QLKiemKe/ViewCheckMayHienTai")]
        public IActionResult ViewCheckMayHienTai()
        {
            if (User == null || User.Identity == null || !User.Identity.IsAuthenticated)
            {
                return Redirect("/DonXetDuyet/DangNhap");
            }
            return View();
        }

        // Xác định Công ty + Bộ phận của 1 người dùng, dựa vào thiết bị KK_ThietBi đã được xác nhận đứng tên họ (mới cập nhật gần nhất)
        private (int? IdCongTy, int? IdBoPhan, string? TenCongTy, string? TenBoPhan) LayCongTyBoPhanCuaUser(int idNguoiDung)
        {
            var thietBi = _context.KkThietBis
                .Include(x => x.IdcongTyNavigation)
                .Include(x => x.IdboPhanNavigation)
                .Where(x => x.IdNguoiDung == idNguoiDung && x.IdcongTy != null && x.IdboPhan != null && x.NgayXoa == null)
                .OrderByDescending(x => x.NgayCapNhat)
                .FirstOrDefault();

            if (thietBi == null) return (null, null, null, null);
            return (thietBi.IdcongTy, thietBi.IdboPhan, thietBi.IdcongTyNavigation?.TenCongTy, thietBi.IdboPhanNavigation?.TenBoPhan);
        }

        // Trang riêng dành cho người có quyền "XemTSCN" xem toàn bộ thiết bị của đúng Công ty + Bộ phận mà bản thân họ đang thuộc về
        // (xác định qua thiết bị đã Xác nhận tài sản đứng tên họ), giúp bộ phận tự quản lý tài sản của mình mà không cần quyền AdminIT.
        [HttpGet("/QLKiemKe/TaiSanBoPhan")]
        public IActionResult TaiSanBoPhan()
        {
            if (User == null || User.Identity == null || !User.Identity.IsAuthenticated)
            {
                return Redirect("/DonXetDuyet/DangNhap");
            }
            if (!User.IsInRole("XemTSCN") && !User.IsInRole("All"))
            {
                return Forbid();
            }
            return View("TaiSanBoPhan");
        }

        // API dữ liệu cho trang TaiSanBoPhan ở trên - luôn kiểm tra lại quyền ở tầng Server, không chỉ dựa vào việc ẩn nút ở giao diện.
        // Luôn trả về đúng Công ty/Bộ phận của bản thân người dùng. Quyền "All" dùng thêm GetTatCaThietBiChoAll bên dưới
        // để xem/lọc theo nhiều bộ phận khác qua ô Dropdown multi-checkbox trên giao diện.
        [HttpGet("/QLKiemKe/GetThietBiBoPhanCuaToi")]
        public IActionResult GetThietBiBoPhanCuaToi()
        {
            try
            {
                if (User == null || User.Identity == null || !User.Identity.IsAuthenticated)
                    return Json(new { success = false, message = "Bạn chưa đăng nhập." });
                if (!User.IsInRole("XemTSCN") && !User.IsInRole("All"))
                    return Json(new { success = false, message = "Bạn không có quyền xem dữ liệu này." });

                bool isAll = User.IsInRole("All");

                var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                if (string.IsNullOrEmpty(userIdClaim) || !int.TryParse(userIdClaim, out int idNguoiDung))
                    return Json(new { success = false, message = "Không xác định được người dùng hiện tại.", isAll });

                var (idCongTy, idBoPhan, tenCongTy, tenBoPhan) = LayCongTyBoPhanCuaUser(idNguoiDung);
                if (idCongTy == null || idBoPhan == null)
                    return Json(new { success = false, message = "Bạn chưa có thiết bị nào được Xác nhận tài sản gắn Công ty/Bộ phận, nên chưa thể xác định phạm vi quản lý.", isAll });

                var data = _context.KkThietBis
                    .Where(x => x.IdcongTy == idCongTy && x.IdboPhan == idBoPhan && x.NgayXoa == null)
                    .Select(x => new
                    {
                        x.IdThietBi,
                        IdCongTy = x.IdcongTy,
                        IdBoPhan = x.IdboPhan,
                        TenCongTy = tenCongTy,
                        TenBoPhan = tenBoPhan,
                        x.TenViTri,
                        x.TenMayTinh,
                        x.LoaiThietBi,
                        x.QuyCach,
                        x.Seribacode,
                        TenNguoiDung = x.IdNguoiDungNavigation != null ? x.IdNguoiDungNavigation.HoTen : "",
                        Tk = x.IdNguoiDungNavigation != null ? x.IdNguoiDungNavigation.Tk : "",
                        TenTrangThai = x.IdTrangThaiNavigation != null ? x.IdTrangThaiNavigation.TenTrangThai : "",
                        x.DuongDanAnh,
                        x.ThoiGianCheck,
                        x.GhiChu
                    })
                    .OrderBy(x => x.TenViTri)
                    .ToList();

                return Json(new { success = true, tenCongTy, tenBoPhan, isAll, idCongTy, idBoPhan, data });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = ex.Message });
            }
        }

        // Toàn bộ thiết bị trong hệ thống (không giới hạn theo Công ty/Bộ phận), CHỈ dành cho quyền "All".
        // Dùng để đổ dữ liệu cho ô Dropdown multi-checkbox chọn xem nhiều bộ phận cùng lúc trên trang Tài sản Bộ phận,
        // bao gồm cả những thiết bị chưa xác định được Công ty/Bộ phận (IdboPhan null) để không bị bỏ sót khi kiểm kê.
        [HttpGet("/QLKiemKe/GetTatCaThietBiChoAll")]
        public IActionResult GetTatCaThietBiChoAll()
        {
            try
            {
                if (User == null || User.Identity == null || !User.Identity.IsAuthenticated)
                    return Json(new { success = false, message = "Bạn chưa đăng nhập." });
                if (!User.IsInRole("All"))
                    return Json(new { success = false, message = "Bạn không có quyền xem dữ liệu này." });

                var data = _context.KkThietBis
                    .Where(x => x.NgayXoa == null)
                    .Select(x => new
                    {
                        x.IdThietBi,
                        IdCongTy = x.IdcongTy,
                        IdBoPhan = x.IdboPhan,
                        TenCongTy = x.IdcongTyNavigation != null ? x.IdcongTyNavigation.TenCongTy : null,
                        TenBoPhan = x.IdboPhanNavigation != null ? x.IdboPhanNavigation.TenBoPhan : null,
                        x.TenViTri,
                        x.TenMayTinh,
                        x.LoaiThietBi,
                        x.QuyCach,
                        x.Seribacode,
                        TenNguoiDung = x.IdNguoiDungNavigation != null ? x.IdNguoiDungNavigation.HoTen : "",
                        Tk = x.IdNguoiDungNavigation != null ? x.IdNguoiDungNavigation.Tk : "",
                        TenTrangThai = x.IdTrangThaiNavigation != null ? x.IdTrangThaiNavigation.TenTrangThai : "",
                        x.DuongDanAnh,
                        x.ThoiGianCheck,
                        x.GhiChu
                    })
                    .OrderBy(x => x.TenCongTy).ThenBy(x => x.TenBoPhan).ThenBy(x => x.TenViTri)
                    .ToList();

                return Json(new { success = true, data });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = ex.Message });
            }
        }

        private class BienBanThietBiRow
        {
            public string TenCongTy { get; set; } = "Chưa xác định";
            public string TenBoPhan { get; set; } = "Chưa xác định";
            public string? TenViTri { get; set; }
            public string? TenMayTinh { get; set; }
            public string? LoaiThietBi { get; set; }
            public string? QuyCach { get; set; }
            public string? Seribacode { get; set; }
            public string? TenNguoiDung { get; set; }
            public string? Tk { get; set; }
            public string? TenTrangThai { get; set; }
            public string? GhiChu { get; set; }
        }

        // Xuất Biên bản kiểm kê - xác nhận tài sản bộ phận dạng in giấy để ký tay (khác chữ ký điện tử của luồng FormIT).
        // ids: danh sách IdThietBi cách nhau bởi dấu phẩy, ứng đúng những dòng đang hiển thị (đã lọc, có thể gồm nhiều bộ phận
        // khác nhau nếu người dùng có quyền "All") trên giao diện.
        [HttpGet("/QLKiemKe/ExportBienBanTaiSanBoPhan")]
        public IActionResult ExportBienBanTaiSanBoPhan(string? ids)
        {
            if (User == null || User.Identity == null || !User.Identity.IsAuthenticated)
                return Redirect("/DonXetDuyet/DangNhap");
            if (!User.IsInRole("XemTSCN") && !User.IsInRole("All"))
                return Forbid();

            bool isAll = User.IsInRole("All");

            List<int>? dsId = null;
            if (!string.IsNullOrWhiteSpace(ids))
            {
                dsId = ids.Split(',', StringSplitOptions.RemoveEmptyEntries)
                    .Select(s => int.TryParse(s.Trim(), out int id) ? id : (int?)null)
                    .Where(id => id.HasValue)
                    .Select(id => id!.Value)
                    .ToList();
            }

            IQueryable<KkThietBi> query;

            if (isAll)
            {
                // Quyền All đã tự chọn đúng danh sách thiết bị (có thể trải nhiều Công ty/Bộ phận, kể cả thiết bị chưa
                // xác định bộ phận) ở giao diện, nên chỉ cần lọc theo đúng danh sách Id đó, không giới hạn phạm vi.
                if (dsId == null || dsId.Count == 0)
                    return Content("Vui lòng chọn danh sách thiết bị cần xuất biên bản.");
                query = _context.KkThietBis.Where(x => x.NgayXoa == null && dsId.Contains(x.IdThietBi));
            }
            else
            {
                var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                if (string.IsNullOrEmpty(userIdClaim) || !int.TryParse(userIdClaim, out int idNguoiDung))
                    return Content("Không xác định được người dùng hiện tại.");

                var (idCongTy, idBoPhan, _, _) = LayCongTyBoPhanCuaUser(idNguoiDung);
                if (idCongTy == null || idBoPhan == null)
                    return Content("Bạn chưa có thiết bị nào được Xác nhận tài sản gắn Công ty/Bộ phận, nên chưa thể xuất biên bản.");

                query = _context.KkThietBis.Where(x => x.IdcongTy == idCongTy && x.IdboPhan == idBoPhan && x.NgayXoa == null);
                if (dsId != null && dsId.Count > 0) query = query.Where(x => dsId.Contains(x.IdThietBi));
            }

            var danhSach = query
                .OrderBy(x => x.IdcongTyNavigation!.TenCongTy).ThenBy(x => x.IdboPhanNavigation!.TenBoPhan).ThenBy(x => x.TenViTri)
                .Select(x => new BienBanThietBiRow
                {
                    TenCongTy = x.IdcongTyNavigation != null ? x.IdcongTyNavigation.TenCongTy : "Chưa xác định",
                    TenBoPhan = x.IdboPhanNavigation != null ? x.IdboPhanNavigation.TenBoPhan : "Chưa xác định",
                    TenViTri = x.TenViTri,
                    TenMayTinh = x.TenMayTinh,
                    LoaiThietBi = x.LoaiThietBi,
                    QuyCach = x.QuyCach,
                    Seribacode = x.Seribacode,
                    TenNguoiDung = x.IdNguoiDungNavigation != null ? x.IdNguoiDungNavigation.HoTen : "",
                    Tk = x.IdNguoiDungNavigation != null ? x.IdNguoiDungNavigation.Tk : "",
                    TenTrangThai = x.IdTrangThaiNavigation != null ? x.IdTrangThaiNavigation.TenTrangThai : "",
                    GhiChu = x.GhiChu
                })
                .ToList();

            string tenNguoiLap = User.FindFirst(ClaimTypes.Name)?.Value ?? "";
            string htmlContent = BuildHtmlBienBanTaiSanBoPhan(tenNguoiLap, danhSach);

            return Content(htmlContent, "text/html", System.Text.Encoding.UTF8);
        }

        // ============================================================
        // HÀM HỖ TRỢ BUILD HTML BIÊN BẢN XÁC NHẬN TÀI SẢN BỘ PHẬN (DÙNG CHUNG PHONG CÁCH VĂN BẢN VỚI BuildHtmlContentIT)
        // ============================================================
        private string BuildHtmlBienBanTaiSanBoPhan(string tenNguoiLap, List<BienBanThietBiRow> danhSach)
        {
            var sb = new System.Text.StringBuilder();

            sb.Append("<html><head><meta charset='utf-8'/><title>Bien ban xac nhan tai san bo phan</title>");
            sb.Append("<style>");
            sb.Append(@"
                body { font-family: 'Times New Roman', Times, serif; line-height: 1.25; margin: 0; padding: 0; color: #000; background: #fff; }
                .document-container { max-width: 1050px; margin: 0 auto; padding: 20px; background: #fff; }
                .header-table { width: 100%; border: none; margin-bottom: 10px; text-align: center; }
                .header-table td { border: none; padding: 0; }
                .company-name { font-size: 13pt; font-weight: bold; text-transform: uppercase; }
                .company-sub { font-size: 11pt; font-weight: bold; text-decoration: underline; margin-bottom: 4px; }
                .national-title { font-size: 13pt; font-weight: bold; text-transform: uppercase; }
                .national-sub { font-size: 12pt; font-weight: bold; text-decoration: underline; }
                .form-title { font-size: 16pt; font-weight: bold; text-align: center; text-transform: uppercase; margin: 12px 0 4px 0; }
                .form-id { font-size: 11pt; text-align: center; font-style: italic; margin-bottom: 10px; }
                .info-line { font-size: 11pt; margin-bottom: 2px; }
                .data-table { width: 100%; border-collapse: collapse; margin-bottom: 8px; }
                .data-table th, .data-table td { border: 1px solid #000; padding: 2px 5px; font-size: 10pt; line-height: 1.15; vertical-align: middle; }
                .data-table thead th { background-color: #f2f2f2; font-weight: bold; text-align: center; padding: 4px 5px; }
                .data-table thead { display: table-header-group; }
                .data-table tr { page-break-inside: avoid; }
                .tong-so { font-size: 11pt; font-weight: bold; margin: 6px 0 10px 0; }
                .ngay-ky { text-align: right; font-style: italic; font-size: 11pt; margin: 8px 0; }
                .cam-ket { font-size: 11pt; margin-bottom: 6px; text-align: justify; }
                .signature-table { width: 100%; text-align: center; margin-top: 6px; border: none; table-layout: fixed; page-break-inside: avoid; }
                .signature-table td { vertical-align: top; border: none; font-size: 11pt; padding: 4px; word-wrap: break-word; }
                .sig-note { font-size: 9pt; font-style: italic; color: #444; }
            ");
            sb.Append("@page { size: A4 landscape; margin: 15mm; } ");
            sb.Append("@media print { .document-container { padding: 0; } } ");
            sb.Append("</style><script>window.onload = function() { window.print(); }</script></head><body>");

            sb.Append("<div class='document-container'>");

            sb.Append("<table class='header-table'><tr>");
            sb.Append("<td style='width:45%;'><div class='company-name'>BEST PACIFIC</div><div class='company-sub'>PHÒNG CÔNG NGHỆ THÔNG TIN (IT)</div></td>");
            sb.Append("<td style='width:55%;'><div class='national-title'>CỘNG HÒA XÃ HỘI CHỦ NGHĨA VIỆT NAM</div><div class='national-sub'>Độc lập - Tự do - Hạnh phúc</div></td>");
            sb.Append("</tr></table>");

            sb.Append("<div class='form-title'>Biên bản kiểm kê - xác nhận tài sản bộ phận</div>");
            sb.Append($"<div class='form-id'>Ngày lập: {DateTime.Now:dd/MM/yyyy HH:mm}</div>");

            var dsPhamVi = danhSach.Select(x => (x.TenCongTy, x.TenBoPhan)).Distinct().ToList();
            if (dsPhamVi.Count == 1)
            {
                sb.Append($"<div class='info-line'><b>Công ty:</b> {System.Net.WebUtility.HtmlEncode(dsPhamVi[0].TenCongTy)}</div>");
                sb.Append($"<div class='info-line'><b>Bộ phận:</b> {System.Net.WebUtility.HtmlEncode(dsPhamVi[0].TenBoPhan)}</div>");
            }
            else
            {
                string dsTen = string.Join("; ", dsPhamVi.Select(x => System.Net.WebUtility.HtmlEncode(x.TenCongTy) + " - " + System.Net.WebUtility.HtmlEncode(x.TenBoPhan)));
                sb.Append($"<div class='info-line'><b>Phạm vi:</b> {dsPhamVi.Count} bộ phận đã chọn ({dsTen})</div>");
            }
            sb.Append($"<div class='info-line' style='margin-bottom:16px;'><b>Người lập biên bản:</b> {System.Net.WebUtility.HtmlEncode(tenNguoiLap)}</div>");

            sb.Append("<table class='data-table'><thead><tr>");
            sb.Append("<th style='width:3%;'>STT</th>");
            sb.Append("<th style='width:16%;'>Công ty / Bộ phận</th>");
            sb.Append("<th style='width:12%;'>Tên vị trí</th>");
            sb.Append("<th style='width:12%;'>Tên máy (Hostname)</th>");
            sb.Append("<th style='width:9%;'>Loại TB</th>");
            sb.Append("<th style='width:13%;'>Quy cách</th>");
            sb.Append("<th style='width:11%;'>Serial / Barcode</th>");
            sb.Append("<th style='width:13%;'>Người sử dụng</th>");
            sb.Append("<th>Trạng thái</th>");
            sb.Append("</tr></thead><tbody>");

            int stt = 1;
            foreach (var item in danhSach)
            {
                string userInfo = !string.IsNullOrEmpty(item.TenNguoiDung)
                    ? item.TenNguoiDung + (!string.IsNullOrEmpty(item.Tk) ? $" ({item.Tk})" : "")
                    : "Chưa cấp phát";

                sb.Append("<tr>");
                sb.Append($"<td style='text-align:center;'>{stt++}</td>");
                sb.Append($"<td>{System.Net.WebUtility.HtmlEncode(item.TenCongTy)} - {System.Net.WebUtility.HtmlEncode(item.TenBoPhan)}</td>");
                sb.Append($"<td>{System.Net.WebUtility.HtmlEncode(item.TenViTri ?? "Chưa rõ")}</td>");
                sb.Append($"<td>{System.Net.WebUtility.HtmlEncode(item.TenMayTinh ?? "N/A")}</td>");
                sb.Append($"<td>{System.Net.WebUtility.HtmlEncode(item.LoaiThietBi ?? "Khác")}</td>");
                sb.Append($"<td>{System.Net.WebUtility.HtmlEncode(item.QuyCach ?? "-")}</td>");
                sb.Append($"<td>{System.Net.WebUtility.HtmlEncode(item.Seribacode ?? "-")}</td>");
                sb.Append($"<td>{System.Net.WebUtility.HtmlEncode(userInfo)}</td>");
                sb.Append($"<td>{System.Net.WebUtility.HtmlEncode(item.TenTrangThai ?? "Chưa rõ")}</td>");
                sb.Append("</tr>");
            }

            if (danhSach.Count == 0)
            {
                sb.Append("<tr><td colspan='9' style='text-align:center;'>Không có thiết bị nào.</td></tr>");
            }

            sb.Append("</tbody></table>");
            sb.Append($"<div class='tong-so'>Tổng cộng: {danhSach.Count} thiết bị</div>");

            sb.Append("<div class='cam-ket'>Chúng tôi, đại diện Bộ phận nêu trên, đã kiểm tra, đối chiếu số lượng và tình trạng tài sản được liệt kê trong biên bản này là đúng với thực tế đang sử dụng/quản lý tại bộ phận. Biên bản được lập thành các bên liên quan cùng lưu để đối chiếu khi cần.</div>");

            sb.Append($"<div class='ngay-ky'>......................., ngày {DateTime.Now:dd} tháng {DateTime.Now:MM} năm {DateTime.Now:yyyy}</div>");

            sb.Append("<table class='signature-table'><tr>");
            sb.Append("<td style='width:33.33%;'><strong>NGƯỜI LẬP BIÊN BẢN</strong><br/><span class='sig-note'>(Ký, ghi rõ họ tên)</span><br/><br/><br/><strong>" + System.Net.WebUtility.HtmlEncode(tenNguoiLap) + "</strong></td>");
            sb.Append("<td style='width:33.33%;'><strong>TRƯỞNG / PHỤ TRÁCH BỘ PHẬN XÁC NHẬN</strong><br/><span class='sig-note'>(Ký, ghi rõ họ tên)</span><br/><br/><br/><br/></td>");
            sb.Append("<td style='width:33.33%;'><strong>ĐẠI DIỆN PHÒNG IT</strong><br/><span class='sig-note'>(Ký, ghi rõ họ tên)</span><br/><br/><br/><br/></td>");
            sb.Append("</tr></table>");

            sb.Append("</div></body></html>");
            return sb.ToString();
        }

        // Tra cứu Máy tính + Màn hình rời theo Serial trong KK_ThietBi để biết đang thuộc về ai, kèm toàn bộ Tài sản khác của đúng người đó
        [HttpGet("/QLKiemKe/GetThongTinSoHuuTheoSeri")]
        public IActionResult GetThongTinSoHuuTheoSeri(string? seriMay, string? seriManHinh)
        {
            try
            {
                object? mayInfo = null;
                object? manHinhInfo = null;
                int? idNguoiDungChuMay = null;

                if (!string.IsNullOrWhiteSpace(seriMay))
                {
                    string seriMayChuan = seriMay.Trim().ToLower();
                    var tbMay = _context.KkThietBis
                        .Where(x => x.Seribacode != null && x.Seribacode.Trim().ToLower() == seriMayChuan)
                        .Select(x => new
                        {
                            x.IdThietBi,
                            x.LoaiThietBi,
                            x.IdNguoiDung,
                            TenNguoiDung = x.IdNguoiDungNavigation != null ? x.IdNguoiDungNavigation.HoTen : null,
                            Tk = x.IdNguoiDungNavigation != null ? x.IdNguoiDungNavigation.Tk : null,
                            x.DuongDanAnh,
                            x.TenMayTinh,
                            x.Seribacode,
                            x.QuyCach
                        })
                        .FirstOrDefault();

                    if (tbMay != null)
                    {
                        mayInfo = tbMay;
                        idNguoiDungChuMay = tbMay.IdNguoiDung;
                    }
                }

                if (!string.IsNullOrWhiteSpace(seriManHinh))
                {
                    string seriManHinhChuan = seriManHinh.Trim().ToLower();
                    manHinhInfo = _context.KkThietBis
                        .Where(x => x.Seribacode != null && x.Seribacode.Trim().ToLower() == seriManHinhChuan)
                        .Select(x => new
                        {
                            x.IdThietBi,
                            x.LoaiThietBi,
                            x.IdNguoiDung,
                            TenNguoiDung = x.IdNguoiDungNavigation != null ? x.IdNguoiDungNavigation.HoTen : null,
                            Tk = x.IdNguoiDungNavigation != null ? x.IdNguoiDungNavigation.Tk : null,
                            x.DuongDanAnh,
                            x.TenMayTinh,
                            x.Seribacode,
                            x.QuyCach
                        })
                        .FirstOrDefault();
                }

                List<object> dsTaiSanKhac = new();
                if (idNguoiDungChuMay.HasValue)
                {
                    string seriMayChuan = (seriMay ?? "").Trim().ToLower();
                    dsTaiSanKhac = _context.KkThietBis
                        .Where(x => x.IdNguoiDung == idNguoiDungChuMay &&
                                    (x.Seribacode == null || x.Seribacode.Trim().ToLower() != seriMayChuan))
                        .Select(x => new
                        {
                            x.IdThietBi,
                            x.LoaiThietBi,
                            x.Seribacode,
                            x.QuyCach,
                            x.TenViTri,
                            x.DuongDanAnh
                        })
                        .ToList<object>();
                }

                return Json(new { success = true, may = mayInfo, manHinh = manHinhInfo, taiSanKhac = dsTaiSanKhac });
            }
            catch (Exception ex)
            {
                var chiTietLoi = ex.InnerException != null ? ex.InnerException.Message : ex.Message;
                return Json(new { success = false, message = $"Lỗi hệ thống khi tra cứu chủ sở hữu: {chiTietLoi}" });
            }
        }

        // Cập nhật ẢNH THIẾT BỊ (KK_ThietBi.DuongDanAnh) của máy/màn hình đang xem ở trang ViewCheckMayHienTai - dùng chung
        // thư mục/quy ước tên file với SaveKkThietBi. Chỉ người có quyền XemTSCN/All mới được sửa ảnh thiết bị không phải của mình.
        [HttpPost("/QLKiemKe/CapNhatAnhThietBiSoHuu")]
        public async Task<IActionResult> CapNhatAnhThietBiSoHuu(int idThietBi, IFormFile? fileAnh)
        {
            if (User == null || User.Identity == null || !User.Identity.IsAuthenticated)
                return Json(new { success = false, message = "Bạn chưa đăng nhập." });
            if (!User.IsInRole("XemTSCN") && !User.IsInRole("All"))
                return Json(new { success = false, message = "Bạn không có quyền cập nhật ảnh thiết bị này." });
            if (fileAnh == null || fileAnh.Length == 0)
                return Json(new { success = false, message = "File không hợp lệ." });

            try
            {
                var thietBi = await _context.KkThietBis.FindAsync(idThietBi);
                if (thietBi == null) return Json(new { success = false, message = "Không tìm thấy thiết bị." });

                string networkPath = @"\\10.0.60.30\BPVN-Fileserver\Public\IT-Information Technology Dept\5.E-Form\AnhKiemKe";
                if (!Directory.Exists(networkPath)) Directory.CreateDirectory(networkPath);

                string safeName = string.Join("_", (thietBi.TenViTri ?? "TB").Split(Path.GetInvalidFileNameChars(), StringSplitOptions.RemoveEmptyEntries));
                if (string.IsNullOrEmpty(safeName)) safeName = "TB";

                string fileName = $"AnhTB_{safeName}_{DateTime.Now:yyyyMMddHHmmss}{Path.GetExtension(fileAnh.FileName)}";
                string fullPath = Path.Combine(networkPath, fileName);

                using (var stream = new FileStream(fullPath, FileMode.Create))
                {
                    await fileAnh.CopyToAsync(stream);
                }

                thietBi.DuongDanAnh = fileName;
                thietBi.NgayCapNhat = DateTime.Now;
                await _context.SaveChangesAsync();

                return Json(new { success = true, fileName });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = "Lỗi Server: " + ex.Message });
            }
        }

        // ACTION ĐÃ ĐƯỢC SỬA: Bổ sung IgnoreAntiforgeryToken để chặn lỗi kết nối đường truyền khi POST AJAX
        [IgnoreAntiforgeryToken]
        [HttpPost("/QLKiemKe/XacThucAdmin")]
        public async Task<IActionResult> XacThucAdmin(string taikhoan, string matkhau)
        {
            if (string.IsNullOrEmpty(taikhoan) || string.IsNullOrEmpty(matkhau))
            {
                return Json(new { success = false, message = "Vui lòng nhập đầy đủ tài khoản và mật khẩu." });
            }

            try
            {
                // 1. Xác thực Hybrid (Domain hoặc DB tùy theo cấu hình LoginMode của tài khoản) - dùng chung logic với trang đăng nhập chính
                var user = await XacThucTaiKhoanHybrid(taikhoan, matkhau);

                if (user == null)
                {
                    return Json(new { success = false, message = "Tài khoản hoặc mật khẩu không chính xác." });
                }

                // 2. Check xem tài khoản đó có TenQuyen là AdminIT không dựa vào mối quan hệ nhiều-nhiều qua bảng trung gian UserQuyen và Quyen
                bool isAdminIT = _context.UserQuyens
                    .Any(uq => uq.IdNguoiDung == user.IdNguoiDung && uq.IdQuyenNavigation.TenQuyen == "AdminIT");

                if (!isAdminIT)
                {
                    return Json(new { success = false, message = "Tài khoản hợp lệ nhưng bạn không có quyền AdminIT." });
                }

                return Json(new { success = true, message = "Xác thực thành công!" });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = $"Lỗi hệ thống xác thực: {ex.Message}" });
            }
        }

        // ACTION MỚI: Xử lý khi ấn xác nhận tài sản, nhận toàn bộ dữ liệu phần cứng từ Client
        [IgnoreAntiforgeryToken]
        [HttpPost("/QLKiemKe/XacNhanTaiSanMaMay")]
        public async Task<IActionResult> XacNhanTaiSanMaMay(
            string taikhoan,
            string matkhau,
            string seriMay,
            string tenMay,
            string dongMay,
            string heDieuHanh,
            string kienTruc,
            string phienBanNet,
            int? soLoiCPU,
            string tenNguoiDungHeThong,
            string thuMucHeThong,
            string thoiGianHoatDong,
            string ramKhaDung,
            string thongTinManHinhNgoai,
            string thongTinOffice,
            string banQuyenWin,
            string banQuyenOffice,
            string dsRamRaw = "",       // Tham số mở rộng nhận chuỗi RAM phân tách bởi dấu phẩy từ tool
            string dsOcungRaw = "",     // Tham số mở rộng nhận chuỗi Ổ cứng phân tách bởi dấu phẩy từ tool
            string dsManHinhRaw = "",   // Tham số mở rộng nhận chuỗi Màn hình phân tách bởi dấu đứng | từ tool
            string dsMacWifiRaw = "",   // Tham số mở rộng nhận chuỗi MAC Wifi phân tách bởi dấu phẩy từ tool
            string? tenViTri = null,    // Vị trí đặt máy - người xác nhận điền tại chỗ để đồng bộ sang KK_ThietBi
            int? idCongTy = null,
            int? idBoPhan = null,
            IFormFile? anhThietBi = null) // Ảnh chụp thiết bị lúc xác nhận (tùy chọn)
        {
            if (string.IsNullOrEmpty(taikhoan) || string.IsNullOrEmpty(matkhau))
            {
                return Json(new { success = false, message = "Vui lòng nhập đầy đủ tài khoản và mật khẩu." });
            }

            // ĐÃ BỔ SUNG CHẶN RỖNG: Kiểm tra Số Serial Máy quét lên không được rỗng, null hoặc mặc định
            if (string.IsNullOrEmpty(seriMay) || seriMay.Trim() == "N/A" || seriMay.Trim() == "Đang tải...")
            {
                return Json(new { success = false, message = "Không tìm thấy Số Serial Máy (Serial Number). Vui lòng liên hệ với IT để check lỗi." });
            }

            // Vị trí, Công ty, Bộ phận là bắt buộc khi xác nhận tài sản
            if (string.IsNullOrWhiteSpace(tenViTri))
            {
                return Json(new { success = false, message = "Vui lòng nhập Vị trí đặt máy." });
            }
            if (!idCongTy.HasValue)
            {
                return Json(new { success = false, message = "Vui lòng chọn Công ty." });
            }
            if (!idBoPhan.HasValue)
            {
                return Json(new { success = false, message = "Vui lòng chọn Bộ phận." });
            }

            try
            {
                // 1. Xác thực Hybrid (Domain hoặc DB tùy theo cấu hình LoginMode của tài khoản) - dùng chung logic với trang đăng nhập chính
                var user = await XacThucTaiKhoanHybrid(taikhoan, matkhau);

                if (user == null)
                {
                    return Json(new { success = false, message = "Tài khoản hoặc mật khẩu xác nhận không chính xác." });
                }

                // 2. Kiểm tra điều kiện: Số Serial Máy quét được phải trùng khớp với máy đã tồn tại trong DB
                var mayTonTai = _context.TscnThongTinMays
                    .FirstOrDefault(m => m.SeriMay == seriMay.Trim());

                // ĐÃ SỬA ĐỔI THEO YÊU CẦU: Nếu máy CHƯA TỒN TẠI -> THÊM MỚI với toàn bộ thông số lấy từ Tool local
                if (mayTonTai == null)
                {
                    mayTonTai = new TscnThongTinMay
                    {
                        SeriMay = seriMay.Trim(),
                        TenMay = string.IsNullOrEmpty(tenMay) ? "Máy mới chưa xác định" : tenMay.Trim(),
                        DongMay = dongMay,
                        HeDieuHanh = heDieuHanh,
                        KienTruc = kienTruc,
                        PhienBanNet = phienBanNet,
                        SoLoiCpu = soLoiCPU,
                        TenNguoiDungHeThong = tenNguoiDungHeThong,
                        ThuMucHeThong = thuMucHeThong,
                        ThoiGianHoatDong = thoiGianHoatDong,
                        RamKhaDung = ramKhaDung,
                        ThongTinManHinhNgoai = thongTinManHinhNgoai,
                        ThongTinOffice = thongTinOffice,
                        BanQuyenWin = banQuyenWin,
                        BanQuyenOffice = banQuyenOffice,
                        IdNguoiDung = user.IdNguoiDung, // Lưu người gần đây nhấn xác nhận sở hữu vào bảng chính
                        NgayCapNhat = DateTime.Now
                    };

                    _context.TscnThongTinMays.Add(mayTonTai);
                    _context.SaveChanges(); // Lưu tạo mới ID thiết bị để lấy IdMay cấp phát cho các bảng con
                }
                else
                {
                    // CHẶN BỔ SUNG THEO YÊU CẦU: Tên máy trong cơ sở dữ liệu hoặc quét được không được phép rỗng hoặc null
                    if (string.IsNullOrEmpty(mayTonTai.TenMay) || mayTonTai.TenMay.Trim() == "N/A")
                    {
                        return Json(new { success = false, message = "Tên máy (Machine Name) của thiết bị này trống hoặc không hợp lệ. Vui lòng liên hệ với IT để check lỗi." });
                    }

                    // ĐỒNG BỘ: Nếu máy ĐÃ CÓ, cập nhật các thông số phần cứng mới quét và gán IdNguoiDung là người gần nhất xác nhận
                    mayTonTai.DongMay = dongMay;
                    mayTonTai.HeDieuHanh = heDieuHanh;
                    mayTonTai.KienTruc = kienTruc;
                    mayTonTai.PhienBanNet = phienBanNet;
                    mayTonTai.SoLoiCpu = soLoiCPU;
                    mayTonTai.TenNguoiDungHeThong = tenNguoiDungHeThong;
                    mayTonTai.ThuMucHeThong = thuMucHeThong;
                    mayTonTai.ThoiGianHoatDong = thoiGianHoatDong;
                    mayTonTai.RamKhaDung = ramKhaDung;
                    mayTonTai.ThongTinManHinhNgoai = thongTinManHinhNgoai;
                    mayTonTai.ThongTinOffice = thongTinOffice;
                    mayTonTai.BanQuyenWin = banQuyenWin;
                    mayTonTai.BanQuyenOffice = banQuyenOffice;

                    mayTonTai.IdNguoiDung = user.IdNguoiDung; // Lưu người gần đây nhấn xác nhận sở hữu vào bảng chính
                    mayTonTai.NgayCapNhat = DateTime.Now;

                    // Làm sạch dữ liệu cấu hình cũ ở các bảng chi tiết nhánh để ghi đè dữ liệu mới quét tinh chỉnh
                    _context.TscnChiTietRams.RemoveRange(_context.TscnChiTietRams.Where(r => r.IdMay == mayTonTai.IdMay));
                    _context.TscnChiTietOcungs.RemoveRange(_context.TscnChiTietOcungs.Where(o => o.IdMay == mayTonTai.IdMay));
                    _context.TscnChiTietManHinhs.RemoveRange(_context.TscnChiTietManHinhs.Where(m => m.IdMay == mayTonTai.IdMay));
                    _context.TscnChiTietMacWifis.RemoveRange(_context.TscnChiTietMacWifis.Where(w => w.IdMay == mayTonTai.IdMay));
                    _context.SaveChanges();
                }

                // 3. Tiến hành bóc tách chuỗi và lưu dữ liệu vào các bảng cấu hình chi tiết (Sub-tables)
                // --- Lưu chi tiết RAM ---
                if (!string.IsNullOrEmpty(dsRamRaw))
                {
                    var rams = dsRamRaw.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries);
                    foreach (var r in rams)
                    {
                        _context.TscnChiTietRams.Add(new TscnChiTietRam { IdMay = mayTonTai.IdMay, ThanhRam = r.Trim() });
                    }
                }

                // --- Lưu chi tiết Ổ Cứng ---
                if (!string.IsNullOrEmpty(dsOcungRaw))
                {
                    var hdds = dsOcungRaw.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries);
                    foreach (var h in hdds)
                    {
                        _context.TscnChiTietOcungs.Add(new TscnChiTietOcung { IdMay = mayTonTai.IdMay, ThongTinOcung = h.Trim() });
                    }
                }

                // --- Lưu chi tiết Màn hình ---
                if (!string.IsNullOrEmpty(dsManHinhRaw))
                {
                    var screens = dsManHinhRaw.Split(new[] { '|' }, StringSplitOptions.RemoveEmptyEntries);
                    foreach (var s in screens)
                    {
                        _context.TscnChiTietManHinhs.Add(new TscnChiTietManHinh { IdMay = mayTonTai.IdMay, ThongTinManHinh = s.Trim() });
                    }
                }

                // --- Lưu chi tiết địa chỉ MAC Wireless ---
                if (!string.IsNullOrEmpty(dsMacWifiRaw))
                {
                    var macs = dsMacWifiRaw.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries);
                    foreach (var m in macs)
                    {
                        _context.TscnChiTietMacWifis.Add(new TscnChiTietMacWifi
                        {
                            IdMay = mayTonTai.IdMay,
                            TenCard = "Wi-Fi Adapter",
                            DiaChiMac = m.Trim()
                        });
                    }
                }

                // 4. Logic xử lý gắn kết máy với tài khoản qua bảng lịch sử xác thực trung gian thực tế của bạn
                var lichSu = new TscnLichSuXacThucNguoiDung
                {
                    IdMay = mayTonTai.IdMay,
                    IdNguoiDung = user.IdNguoiDung, // Luôn lưu người vừa xác nhận vào lịch sử log
                    NgayXacThuc = DateTime.Now,
                    GhiChu = $"Tài khoản {taikhoan} xác nhận sở hữu thiết bị này."
                };

                _context.TscnLichSuXacThucNguoiDungs.Add(lichSu);

                // Đồng thời ghi nhận vào log lịch sử xác thực quản trị (Admin Audit)
                _context.TscnLichSuXacThucAdmins.Add(new TscnLichSuXacThucAdmin
                {
                    IdMay = mayTonTai.IdMay,
                    TaiKhoanXacThuc = taikhoan,
                    ThoiGianXacThuc = DateTime.Now,
                    TrangThai = "Xác nhận thành công"
                });

                _context.SaveChanges();

                // 5. ĐỒNG BỘ SANG KK_ThietBi kèm Vị trí/Công ty/Bộ phận vừa xác nhận + ảnh chụp (nếu có)
                if (!string.IsNullOrWhiteSpace(mayTonTai.TenMay))
                {
                    // TSCN_ThongTinMay mới quét lần đầu chưa có Loại thiết bị -> tự phân loại nhanh theo Dòng máy để có thể đồng bộ
                    // Nếu tên Dòng máy không đủ dấu hiệu nhận diện (model lạ/không có trong danh sách từ khóa) -> mặc định "PC" (Máy tính bàn)
                    // để thiết bị VẪN được thêm vào trang Thiết bị ngay lúc xác nhận thay vì bị bỏ sót; admin có thể sửa lại tay sau nếu đoán sai.
                    if (string.IsNullOrWhiteSpace(mayTonTai.LoaiThietBi))
                    {
                        mayTonTai.LoaiThietBi = TuPhanLoaiThietBiTheoDongMay(mayTonTai.DongMay) ?? "PC";
                    }

                    if (!string.IsNullOrWhiteSpace(mayTonTai.LoaiThietBi))
                    {
                        string loaiThietBiChuan = ChuanHoaLoaiThietBiSangDanhMuc(mayTonTai.LoaiThietBi);

                        // Khớp theo IdMay đã liên kết / Serial trùng / Tên máy trùng nhưng chưa có Serial - để CẬP NHẬT thay vì thêm trùng
                        var thietBiLienKet = TimThietBiTrungTheoMayTinh(mayTonTai.IdMay, mayTonTai.TenMay, mayTonTai.SeriMay);

                        // Xử lý lưu ảnh chụp thiết bị (nếu người xác nhận có tải ảnh lên)
                        string? tenFileAnhMoi = null;
                        if (anhThietBi != null && anhThietBi.Length > 0)
                        {
                            string networkPath = @"\\10.0.60.30\BPVN-Fileserver\Public\IT-Information Technology Dept\5.E-Form\AnhKiemKe";
                            if (!Directory.Exists(networkPath)) Directory.CreateDirectory(networkPath);

                            string imgExtension = Path.GetExtension(anhThietBi.FileName) ?? "";
                            if (string.IsNullOrEmpty(imgExtension)) imgExtension = ".jpg";
                            string timeStamp = DateTime.Now.ToString("yyyyMMddHHmmss");
                            string safeName = string.Join("_", (tenViTri ?? mayTonTai.TenMay).Split(Path.GetInvalidFileNameChars(), StringSplitOptions.RemoveEmptyEntries));
                            if (string.IsNullOrEmpty(safeName)) safeName = "TB";

                            tenFileAnhMoi = $"AnhTB_{safeName}_{timeStamp}{imgExtension}";
                            string imgFullPath = Path.Combine(networkPath, tenFileAnhMoi);
                            using (var imgStream = new FileStream(imgFullPath, FileMode.Create))
                            {
                                await anhThietBi.CopyToAsync(imgStream);
                            }
                        }

                        if (thietBiLienKet != null)
                        {
                            if (!string.IsNullOrWhiteSpace(tenViTri)) thietBiLienKet.TenViTri = tenViTri.Trim();
                            if (idCongTy.HasValue) thietBiLienKet.IdcongTy = idCongTy;
                            if (idBoPhan.HasValue) thietBiLienKet.IdboPhan = idBoPhan;
                            if (!string.IsNullOrWhiteSpace(mayTonTai.SeriMay)) thietBiLienKet.Seribacode = mayTonTai.SeriMay.Trim();
                            if (!string.IsNullOrWhiteSpace(mayTonTai.DongMay)) thietBiLienKet.QuyCach = mayTonTai.DongMay.Trim();
                            thietBiLienKet.IdNguoiDung = mayTonTai.IdNguoiDung;
                            thietBiLienKet.LoaiThietBi = loaiThietBiChuan;
                            thietBiLienKet.IdMay = mayTonTai.IdMay;
                            thietBiLienKet.NgayCapNhat = DateTime.Now;
                            thietBiLienKet.ThoiGianCheck = DateTime.Now;
                            // Chỉ gán làm ảnh chính nếu thiết bị chưa từng có ảnh nào trước đó
                            if (tenFileAnhMoi != null && string.IsNullOrWhiteSpace(thietBiLienKet.DuongDanAnh))
                            {
                                thietBiLienKet.DuongDanAnh = tenFileAnhMoi;
                            }
                        }
                        else
                        {
                            thietBiLienKet = new KkThietBi
                            {
                                TenViTri = string.IsNullOrWhiteSpace(tenViTri) ? "" : tenViTri.Trim(),
                                TenMayTinh = mayTonTai.TenMay.Trim(),
                                LoaiThietBi = loaiThietBiChuan,
                                Seribacode = mayTonTai.SeriMay?.Trim(),
                                QuyCach = mayTonTai.DongMay?.Trim(),
                                IdNguoiDung = mayTonTai.IdNguoiDung,
                                IdcongTy = idCongTy,
                                IdboPhan = idBoPhan,
                                IdMay = mayTonTai.IdMay,
                                DuongDanAnh = tenFileAnhMoi, // Ảnh đầu tiên (nếu có) được gán luôn làm ảnh chính
                                NgayTao = DateTime.Now,
                                NgayCapNhat = DateTime.Now,
                                ThoiGianCheck = DateTime.Now
                            };
                            _context.KkThietBis.Add(thietBiLienKet);
                        }

                        _context.SaveChanges(); // Cần lưu trước để có IdThietBi khi thiết bị vừa được thêm mới

                        // Luôn ghi 1 dòng Bằng chứng kiểm kê mỗi lần xác nhận, có ảnh hay không cũng ghi - Ghi chú lấy đúng Tên vị trí người xác nhận vừa điền
                        _context.KkBangChungChecks.Add(new KkBangChungCheck
                        {
                            IdThietBi = thietBiLienKet.IdThietBi,
                            ThoiGianCheck = DateTime.Now,
                            DuongDanAnh = tenFileAnhMoi, // null nếu không tải ảnh lên
                            GhiChu = string.IsNullOrWhiteSpace(tenViTri) ? "Xác nhận kiểm kê" : tenViTri.Trim()
                        });
                        _context.SaveChanges();
                    }
                }

                return Json(new { success = true, message = $"Xác nhận tài sản thành công! Máy '{mayTonTai.TenMay}' (Serial: {seriMay}) đã được liên kết với tài khoản {taikhoan} vào lúc {lichSu.NgayXacThuc:HH:mm:ss dd/MM/yyyy}." });
            }
            catch (Exception ex)
            {
                var chiTietLoi = ex.InnerException != null ? ex.InnerException.Message : ex.Message;
                return Json(new { success = false, message = $"Lỗi hệ thống khi gắn kết tài sản: {chiTietLoi}" });
            }
        }

        // Giữ nguyên Action gốc phòng trường hợp gõ trực tiếp URL hoặc điều hướng hệ thống cũ
        [HttpGet("/QLKiemKe/CheckMayHienTai")]
        public IActionResult CheckMayHienTai()
        {
            if (Request.Headers["X-Requested-With"] != "XMLHttpRequest")
            {
                return View("CheckMayHienTai");
            }

            // Từ chối nếu không phải AdminIT
            if (User == null || !User.IsInRole("AdminIT"))
            {
                return Json(new { success = false, message = "Truy cập bị từ chối: Chỉ tài khoản AdminIT mới có quyền quét thông tin phần cứng." });
            }

            return Json(new { success = false, message = "Vui lòng kết nối qua ứng dụng Helper local để lấy thông tin máy khách hiện tại." });
        }

        #endregion


        #region hiển thị danh sách tất cả thiết bị máy tính

        // 1. Action trả về giao diện trang xem danh sách toàn bộ thiết bị máy tính
        [HttpGet("/QLKiemKe/ViewDanhSachTatCaMayTinh")]
        public IActionResult ViewDanhSachTatCaMayTinh()
        {
            if (User == null || User.Identity == null || !User.Identity.IsAuthenticated)
            {
                return Redirect("/DonXetDuyet/DangNhap");
            }
            return View();
        }

        // 2. API lấy toàn bộ danh sách máy tính từ hệ thống để hiển thị lên View dữ liệu
        // API lấy toàn bộ danh sách máy tính từ hệ thống để hiển thị lên View dữ liệu
        [HttpGet("/QLKiemKe/DanhSachTatCaMayTinh")]
        public IActionResult DanhSachTatCaMayTinh()
        {
            try
            {
                var danhSachMay = _context.TscnThongTinMays
                    .Include(m => m.IdNguoiDungNavigation)
                    .Include(m => m.TscnChiTietManHinhs)
                    .OrderByDescending(m => m.NgayCapNhat)
                    .Select(m => new
                    {
                        idMay = m.IdMay,
                        tenMay = m.TenMay,
                        seriMay = m.SeriMay,
                        dongMay = m.DongMay,
                        loaiThietBi = m.LoaiThietBi,
                        thongTinOffice = m.ThongTinOffice, // Lấy phiên bản Office truyền xuống Client
                        thongTinManHinhNgoai = m.ThongTinManHinhNgoai,
                        thongTinManHinh = m.TscnChiTietManHinhs.Any()
                            ? string.Join("<br/>", m.TscnChiTietManHinhs.Select(x => x.ThongTinManHinh))
                            : "N/A",
                        banQuyenWin = m.BanQuyenWin,
                        banQuyenOffice = m.BanQuyenOffice,
                        ngayCapNhat = m.NgayCapNhat.HasValue ? m.NgayCapNhat.Value.ToString("yyyy-MM-dd HH:mm:ss") : "N/A",
                        idNguoiDung = m.IdNguoiDung,

                        // Lấy thông tin liên kết từ thực thể User
                        taiKhoanSoHuu = m.IdNguoiDungNavigation != null ? m.IdNguoiDungNavigation.Tk : "Chưa bàn giao",
                        hoTenNguoiSoHuu = m.IdNguoiDungNavigation != null ? m.IdNguoiDungNavigation.HoTen : "Chưa bàn giao",
                        maNhanVienSoHuu = m.IdNguoiDungNavigation != null ? m.IdNguoiDungNavigation.MaNhanVien : "" // Trả thêm Mã nhân viên về Client
                    })
                    .ToList();

                return Json(new { success = true, data = danhSachMay });
            }
            catch (Exception ex)
            {
                var chiTietLoi = ex.InnerException != null ? ex.InnerException.Message : ex.Message;
                return Json(new { success = false, message = $"Lỗi hệ thống khi tải danh sách thiết bị máy tính: {chiTietLoi}" });
            }
        }

        // 2b. ĐỒNG BỘ THIẾT BỊ TỪ CHI NHÁNH KHÁC (SQL Server 10.0.55.3, database ITForm - chỉ có bảng TSCN_ThongTinMay)
        // Đồng bộ 1 chiều remote -> local: máy remote nào có Serial THẬT mà local chưa có thì thêm mới (kèm bảng con RAM/Ổ cứng/Màn hình/MacWifi).
        // Máy đã tồn tại (trùng Serial thật) thì bỏ qua để tránh ghi đè dữ liệu đã kiểm kê/gán chủ sở hữu ở local.
        // Serial dạng rác (BIOS không set, ví dụ "To be filled by O.E.M.") không dùng để đối chiếu trùng vì không phải định danh duy nhất thật sự - luôn coi là máy mới.
        private static readonly string[] SeriRacKhongDoiChieu = { "to be filled by o.e.m.", "default string", "system serial number", "none", "0" };

        [HttpPost("/QLKiemKe/DongBoTuChiNhanh")]
        public async Task<IActionResult> DongBoTuChiNhanh()
        {
            try
            {
                var remoteConnStr = _config.GetConnectionString("ChiNhanhConnection");

                using var remoteConn = new SqlConnection(remoteConnStr);
                await remoteConn.OpenAsync();

                var dsMayRemote = new List<(int IdMayRemote, string TenMay, string? SeriMay, string? DongMay, string? HeDieuHanh, string? KienTruc, string? PhienBanNet, int? SoLoiCpu, string? TenNguoiDungHeThong, string? ThuMucHeThong, string? ThoiGianHoatDong, string? RamKhaDung, string? ThongTinManHinhNgoai, string? ThongTinOffice, string? BanQuyenWin, string? BanQuyenOffice, DateTime? NgayCapNhat, string? LoaiThietBi)>();

                using (var cmd = new SqlCommand("SELECT IdMay, TenMay, SeriMay, DongMay, HeDieuHanh, KienTruc, PhienBanNet, SoLoiCPU, TenNguoiDungHeThong, ThuMucHeThong, ThoiGianHoatDong, RamKhaDung, ThongTinManHinhNgoai, ThongTinOffice, BanQuyenWin, BanQuyenOffice, NgayCapNhat, LoaiThietBi FROM TSCN_ThongTinMay", remoteConn))
                using (var reader = await cmd.ExecuteReaderAsync())
                {
                    while (await reader.ReadAsync())
                    {
                        dsMayRemote.Add((
                            reader.GetInt32(0),
                            reader.GetString(1),
                            reader.IsDBNull(2) ? null : reader.GetString(2),
                            reader.IsDBNull(3) ? null : reader.GetString(3),
                            reader.IsDBNull(4) ? null : reader.GetString(4),
                            reader.IsDBNull(5) ? null : reader.GetString(5),
                            reader.IsDBNull(6) ? null : reader.GetString(6),
                            reader.IsDBNull(7) ? null : reader.GetInt32(7),
                            reader.IsDBNull(8) ? null : reader.GetString(8),
                            reader.IsDBNull(9) ? null : reader.GetString(9),
                            reader.IsDBNull(10) ? null : reader.GetString(10),
                            reader.IsDBNull(11) ? null : reader.GetString(11),
                            reader.IsDBNull(12) ? null : reader.GetString(12),
                            reader.IsDBNull(13) ? null : reader.GetString(13),
                            reader.IsDBNull(14) ? null : reader.GetString(14),
                            reader.IsDBNull(15) ? null : reader.GetString(15),
                            reader.IsDBNull(16) ? null : reader.GetDateTime(16),
                            reader.IsDBNull(17) ? null : reader.GetString(17)
                        ));
                    }
                }

                // Tập hợp Serial local hiện có (chuẩn hóa trim + lower) để đối chiếu trùng nhanh
                var seriLocalDaCo = new HashSet<string>(
                    _context.TscnThongTinMays
                        .Where(m => m.SeriMay != null)
                        .Select(m => m.SeriMay!.Trim().ToLower())
                        .ToList()
                );

                int soDaThem = 0, soBoQuaTrungSeri = 0;
                var chiTietDaThem = new List<object>();

                foreach (var may in dsMayRemote)
                {
                    string seriChuanHoa = (may.SeriMay ?? "").Trim().ToLower();
                    bool laSeriRac = string.IsNullOrEmpty(seriChuanHoa) || SeriRacKhongDoiChieu.Contains(seriChuanHoa);

                    if (!laSeriRac && seriLocalDaCo.Contains(seriChuanHoa))
                    {
                        soBoQuaTrungSeri++;
                        continue;
                    }

                    var mayMoi = new TscnThongTinMay
                    {
                        TenMay = may.TenMay,
                        SeriMay = may.SeriMay,
                        DongMay = may.DongMay,
                        HeDieuHanh = may.HeDieuHanh,
                        KienTruc = may.KienTruc,
                        PhienBanNet = may.PhienBanNet,
                        SoLoiCpu = may.SoLoiCpu,
                        TenNguoiDungHeThong = may.TenNguoiDungHeThong,
                        ThuMucHeThong = may.ThuMucHeThong,
                        ThoiGianHoatDong = may.ThoiGianHoatDong,
                        RamKhaDung = may.RamKhaDung,
                        ThongTinManHinhNgoai = may.ThongTinManHinhNgoai,
                        ThongTinOffice = may.ThongTinOffice,
                        BanQuyenWin = may.BanQuyenWin,
                        BanQuyenOffice = may.BanQuyenOffice,
                        NgayCapNhat = may.NgayCapNhat,
                        LoaiThietBi = may.LoaiThietBi,
                        IdNguoiDung = null // Người sở hữu bên chi nhánh không có bảng User tương ứng ở local nên để trống, gán tay sau
                    };
                    _context.TscnThongTinMays.Add(mayMoi);
                    _context.SaveChanges(); // Lưu ngay để lấy IdMay mới sinh, phục vụ gán các bảng con bên dưới

                    if (!laSeriRac) seriLocalDaCo.Add(seriChuanHoa); // Tránh trùng nếu nhiều máy remote lỡ trùng serial nhau trong cùng 1 lượt đồng bộ

                    await DongBoBangConTuChiNhanh(remoteConn, may.IdMayRemote, mayMoi.IdMay);

                    soDaThem++;
                    chiTietDaThem.Add(new { tenMay = may.TenMay, seriMay = may.SeriMay });
                }

                return Json(new { success = true, soDaThem, soBoQuaTrungSeri, chiTiet = chiTietDaThem });
            }
            catch (Exception ex)
            {
                var chiTietLoi = ex.InnerException != null ? ex.InnerException.Message : ex.Message;
                return Json(new { success = false, message = $"Lỗi khi đồng bộ từ chi nhánh 10.0.55.3: {chiTietLoi}" });
            }
        }

        // Sao chép các bảng con (RAM/Ổ cứng/Màn hình/MAC Wifi) của 1 máy từ chi nhánh sang máy vừa tạo ở local
        private async Task DongBoBangConTuChiNhanh(SqlConnection remoteConn, int idMayRemote, int idMayLocal)
        {
            using (var cmd = new SqlCommand("SELECT ThongTinManHinh FROM TSCN_ChiTietManHinh WHERE IdMay = @idMay", remoteConn))
            {
                cmd.Parameters.AddWithValue("@idMay", idMayRemote);
                using var reader = await cmd.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                {
                    if (!reader.IsDBNull(0)) _context.TscnChiTietManHinhs.Add(new TscnChiTietManHinh { IdMay = idMayLocal, ThongTinManHinh = reader.GetString(0) });
                }
            }

            using (var cmd = new SqlCommand("SELECT ThanhRam FROM TSCN_ChiTietRam WHERE IdMay = @idMay", remoteConn))
            {
                cmd.Parameters.AddWithValue("@idMay", idMayRemote);
                using var reader = await cmd.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                {
                    if (!reader.IsDBNull(0)) _context.TscnChiTietRams.Add(new TscnChiTietRam { IdMay = idMayLocal, ThanhRam = reader.GetString(0) });
                }
            }

            using (var cmd = new SqlCommand("SELECT ThongTinOcung FROM TSCN_ChiTietOCung WHERE IdMay = @idMay", remoteConn))
            {
                cmd.Parameters.AddWithValue("@idMay", idMayRemote);
                using var reader = await cmd.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                {
                    if (!reader.IsDBNull(0)) _context.TscnChiTietOcungs.Add(new TscnChiTietOcung { IdMay = idMayLocal, ThongTinOcung = reader.GetString(0) });
                }
            }

            using (var cmd = new SqlCommand("SELECT TenCard, DiaChiMac FROM TSCN_ChiTietMacWifi WHERE IdMay = @idMay", remoteConn))
            {
                cmd.Parameters.AddWithValue("@idMay", idMayRemote);
                using var reader = await cmd.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                {
                    if (!reader.IsDBNull(1))
                    {
                        _context.TscnChiTietMacWifis.Add(new TscnChiTietMacWifi
                        {
                            IdMay = idMayLocal,
                            TenCard = reader.IsDBNull(0) ? null : reader.GetString(0),
                            DiaChiMac = reader.GetString(1)
                        });
                    }
                }
            }

            await _context.SaveChangesAsync();
        }

        // 3. Giao diện trang xem cấu hình chi tiết mở rộng
        [HttpGet("/QLKiemKe/ViewChiTietMayTinh")]
        public IActionResult ViewChiTietMayTinh(int idMay)
        {
            if (User == null || User.Identity == null || !User.Identity.IsAuthenticated)
            {
                return Redirect("/DonXetDuyet/DangNhap");
            }
            return View();
        }

        // 4. API bốc tách chuyên sâu cấu hình theo ID máy, liên kết toàn bộ Sub-Tables
        [HttpGet("/QLKiemKe/ChiTietMayTinhTheoId")]
        public IActionResult ChiTietMayTinhTheoId(int idMay)
        {
            try
            {
                var may = _context.TscnThongTinMays
                    .Include(m => m.IdNguoiDungNavigation)
                    .Include(m => m.TscnChiTietRams)
                    .Include(m => m.TscnChiTietOcungs)
                    .Include(m => m.TscnChiTietManHinhs)
                    .Include(m => m.TscnChiTietMacWifis)
                    .FirstOrDefault(m => m.IdMay == idMay);

                if (may == null)
                {
                    return Json(new { success = false, message = "Thiết bị không tồn tại trên hệ thống dữ liệu." });
                }

                var data = new
                {
                    idMay = may.IdMay,
                    tenMay = may.TenMay,
                    seriMay = may.SeriMay,
                    dongMay = may.DongMay,
                    heDieuHanh = may.HeDieuHanh,
                    kienTruc = may.KienTruc,
                    phienBanNet = may.PhienBanNet,
                    soLoiCpu = may.SoLoiCpu,
                    tenNguoiDungHeThong = may.TenNguoiDungHeThong,
                    thuMucHeThong = may.ThuMucHeThong,
                    thoiGianHoatDong = may.ThoiGianHoatDong,
                    ramKhaDung = may.RamKhaDung,
                    thongTinManHinhNgoai = may.ThongTinManHinhNgoai,
                    thongTinOffice = may.ThongTinOffice,
                    banQuyenWin = may.BanQuyenWin,
                    banQuyenOffice = may.BanQuyenOffice,
                    ngayCapNhat = may.NgayCapNhat.HasValue ? may.NgayCapNhat.Value.ToString("yyyy-MM-dd HH:mm:ss") : "N/A",
                    taiKhoanSoHuu = may.IdNguoiDungNavigation != null ? may.IdNguoiDungNavigation.Tk : "Chưa xác nhận",

                    // Lấy mảng dữ liệu từ bảng liên kết con phụ trợ
                    listRams = may.TscnChiTietRams.Select(r => r.ThanhRam).ToList(),
                    listOcungs = may.TscnChiTietOcungs.Select(o => o.ThongTinOcung).ToList(),
                    listManHinh = may.TscnChiTietManHinhs.Select(m => m.ThongTinManHinh).ToList(),
                    listMacWifis = may.TscnChiTietMacWifis.Select(w => new { tenCard = w.TenCard, diaChiMac = w.DiaChiMac }).ToList()
                };

                return Json(new { success = true, data = data });
            }
            catch (Exception ex)
            {
                var chiTietLoi = ex.InnerException != null ? ex.InnerException.Message : ex.Message;
                return Json(new { success = false, message = $"Lỗi hệ thống khi tải cấu hình phụ: {chiTietLoi}" });
            }
        }

        // 5. API LẤY DANH SÁCH NHẬT KÝ LỊCH SỬ THAY ĐỔI CẤU HÌNH THEO ID MÁY (BỔ SUNG MỚI CHO COLLAPSE)
        [HttpGet("/QLKiemKe/LichSuThayDoiMayTinh")]
        public IActionResult LichSuThayDoiMayTinh(int idMay)
        {
            try
            {
                var truongAn = new[] { "ThoiGianHoatDong", "RamKhaDung" };
                var logs = _context.TscnLichSuThayDois
                    .Where(l => l.IdMay == idMay && !truongAn.Contains(l.TenTruongThayDoi))
                    .OrderByDescending(l => l.ThoiGianThayDoi)
                    .Select(l => new {
                        thoiGianThayDoi = l.ThoiGianThayDoi.HasValue ? l.ThoiGianThayDoi.Value.ToString("yyyy-MM-dd HH:mm:ss") : "N/A",
                        tenTruongThayDoi = l.TenTruongThayDoi,
                        giaTriCu = l.GiaTriCu,
                        giaTriMoi = l.GiaTriMoi,
                        ghiChu = l.GhiChu
                    })
                    .ToList();

                return Json(new { success = true, data = logs });
            }
            catch (Exception ex)
            {
                var chiTietLoi = ex.InnerException != null ? ex.InnerException.Message : ex.Message;
                return Json(new { success = false, message = $"Lỗi hệ thống khi tải lịch sử thay đổi cấu hình: {chiTietLoi}" });
            }
        }

        #endregion




    }
}
 