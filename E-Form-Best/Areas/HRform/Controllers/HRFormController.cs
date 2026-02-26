//using E_Form_Best.Context;
//using E_Form_Best.Models.ITForm;
//using Microsoft.AspNetCore.Mvc;
//using Microsoft.EntityFrameworkCore;
//using System.Security.Claims;

//namespace E_Form_Best.Areas.HRform.Controllers
//{
//    [Area("HRform")]
//    public class HRFormController : Controller
//    {
//        public ITFormContext _context;
//        public HRFormController()
//        {
//            _context = new ITFormContext();
//        }
//        #region Trang logo
//        [HttpGet("/HRForm/logo")]
//        public IActionResult logo()
//        {
//            return View();
//        }
//        #endregion

//        #region xử lý ảnh
//        private string RemoveSign4VietnameseString(string str)
//        {
//            if (string.IsNullOrEmpty(str)) return "";
//            string[] VietnameseSigns = new string[]
//            {
//        "aAeEoOuUiIdDyY",
//        "áàạảãâấầậẩẫăắằặẳẵ",
//        "ÁÀẠẢÃÂẤẦẬẨẪĂẮẰẶẲẴ",
//        "éèẹẻẽêếềệểễ",
//        "ÉÈẸẺẼÊẾỀỆỂỄ",
//        "óòọỏõôốồộổỗơớờợởỡ",
//        "ÓÒỌỎÕÔỐỒỘỔỖƠỚỜỢỞỠ",
//        "úùụủũưứừựửữ",
//        "ÚÙỤỦŨƯỨỪỰỬỮ",
//        "íìịỉĩ",
//        "ÍÌỊỈĨ",
//        "đ",
//        "Đ",
//        "ýỳỵỷỹ",
//        "ÝỲỴỶỸ"
//            };
//            for (int i = 1; i < VietnameseSigns.Length; i++)
//            {
//                for (int j = 0; j < VietnameseSigns[i].Length; j++)
//                    str = str.Replace(VietnameseSigns[i][j], VietnameseSigns[0][i - 1]);
//            }
//            return str;
//        }

//        // HÀM HELPER - Xử lý chuyển đổi file sang byte[] (Dùng cho cột kiểu varbinary/image trong DB)
//        private async Task<byte[]> GetFileBytesAsync2(string inputName)
//        {
//            try
//            {
//                // Kiểm tra xem trong request có file đính kèm với name tương ứng không
//                if (Request.Form.Files.Count > 0)
//                {
//                    var file = Request.Form.Files[inputName];
//                    if (file != null && file.Length > 0)
//                    {
//                        using var ms = new MemoryStream();
//                        await file.CopyToAsync(ms);
//                        return ms.ToArray();
//                    }
//                }
//            }
//            catch
//            {
//                return null;
//            }
//            return null;
//        }
//        #endregion

//        #region Don Xin Ra Ngoai

//        [HttpGet("/FormHR/DonXinRaNgoai")]
//        public IActionResult DonXinRaNgoai()
//        {
//            // 1. Kiểm tra đăng nhập qua Cookie (User.Identity)
//            if (!User.Identity.IsAuthenticated) return Redirect("/DonXetDuyet/DangNhap");

//            // 2. Lấy thông tin từ Claims (CK)
//            var userIdString = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
//            if (string.IsNullOrEmpty(userIdString)) return Redirect("/DonXetDuyet/DangNhap");

//            int userId = int.Parse(userIdString);
//            var userName = User.FindFirst(ClaimTypes.Name)?.Value ?? "";
//            var phongBan = User.FindFirst("PhongBan")?.Value ?? "";
//            var viTri = User.FindFirst(ClaimTypes.Role)?.Value ?? ""; // Hoặc User.FindFirst("UserRole") tùy cách bạn lưu
//            var soNhanVien = User.FindFirst(ClaimTypes.Email)?.Value ?? "";

//            // 3. Khởi tạo Model hiển thị lên View
//            var model = new FormHr
//            {
//                TenNguoiNv = userName,
//                PhanBo = phongBan,
//                ViTri = viTri,
//                SoNhanVien = soNhanVien,
//                Ngay = DateOnly.FromDateTime(DateTime.Now),
//                IdNguoiTao = userId,
//                TenNguoiTao = userName,
//                TimeNguoiTao = DateTime.Now,
//                TrangThai = "ChoDuyet"
//            };

//            return View(model);
//        }

//        [HttpPost("/FormHR/DonXinRaNgoai")]
//        public async Task<IActionResult> DonXinRaNgoai(FormHr form, [FromForm] CtXinRaNgoai1 xinRaNgoai)
//        {
//            // 1. Kiểm tra đăng nhập & Lấy thông tin từ CK
//            if (!User.Identity.IsAuthenticated) return Redirect("/DonXetDuyet/DangNhap");

//            var userIdString = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
//            var userName = User.FindFirst(ClaimTypes.Name)?.Value ?? "";
//            var phongBan = User.FindFirst("PhongBan")?.Value ?? "";
//            var viTri = User.FindFirst(ClaimTypes.Role)?.Value ?? "";
//            var soNhanVien = User.FindFirst(ClaimTypes.Email)?.Value ?? "";

//            if (string.IsNullOrEmpty(userIdString)) return Redirect("/DonXetDuyet/DangNhap");
//            int userId = int.Parse(userIdString);

//            using (var transaction = await _context.Database.BeginTransactionAsync())
//            {
//                try
//                {
//                    // --- BƯỚC 1: THIẾT LẬP THÔNG TIN BẢNG CHÍNH (FormHr) ---
//                    form.Ngay = DateOnly.FromDateTime(DateTime.Now);
//                    form.IdNguoiTao = userId;
//                    form.TenNguoiTao = userName;
//                    form.TimeNguoiTao = DateTime.Now;
//                    form.TenNguoiNv = userName;
//                    form.PhanBo = phongBan;
//                    form.ViTri = viTri;
//                    form.SoNhanVien = soNhanVien;
//                    form.TrangThai = "ChoDuyet";
//                    form.IdForm = "CT_XinRaNgoai_1";
//                    form.TenForm = "Đơn xin ra ngoài";

//                    _context.FormHrs.Add(form);
//                    await _context.SaveChangesAsync();

//                    // --- BƯỚC 2: XỬ LÝ FILE ĐÍNH KÈM (Giữ nguyên logic của bạn) ---
//                    var uploadFile = Request.Form.Files["UploadFile"];
//                    if (uploadFile != null && uploadFile.Length > 0)
//                    {
//                        string wwwRootPath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "FileHR");
//                        if (!Directory.Exists(wwwRootPath)) Directory.CreateDirectory(wwwRootPath);

//                        string extension = Path.GetExtension(uploadFile.FileName);
//                        // Sử dụng hàm xóa dấu của bạn
//                        string safeName = RemoveSign4VietnameseString(userName).Replace(" ", "");
//                        string timeStamp = DateTime.Now.ToString("ddMMyy_HHmm");

//                        string fileName = $"XinRaNgoai_Don{form.Id}_User{userId}_{safeName}_{timeStamp}_{Guid.NewGuid().ToString().Substring(0, 4)}{extension}";
//                        string fullPath = Path.Combine(wwwRootPath, fileName);

//                        using (var fileStream = new FileStream(fullPath, FileMode.Create))
//                        {
//                            await uploadFile.CopyToAsync(fileStream);
//                        }

//                        form.FileDinhKem = fileName;
//                        _context.Entry(form).State = Microsoft.EntityFrameworkCore.EntityState.Modified;
//                        await _context.SaveChangesAsync();
//                    }

//                    // --- BƯỚC 3: XỬ LÝ BẢNG CHI TIẾT (CtXinRaNgoai1) ---
//                    if (xinRaNgoai != null)
//                    {
//                        xinRaNgoai.IdFormHr = form.Id;
//                        // Lưu ảnh vào byte[] trực tiếp như yêu cầu
//                        xinRaNgoai.Anh = await GetFileBytesAsync2("AnhXinRaNgoai");

//                        _context.CtXinRaNgoai1s.Add(xinRaNgoai);
//                        await _context.SaveChangesAsync();
//                    }

//                    // --- BƯỚC 4: HOÀN TẤT ---
//                    await transaction.CommitAsync();

//                    TempData["Success"] = "Gửi đơn xin ra ngoài thành công!";
//                    return RedirectToAction("DonCho");
//                }
//                catch (Exception ex)
//                {
//                    await transaction.RollbackAsync();
//                    ModelState.AddModelError("", "Lỗi hệ thống: " + ex.Message);
//                    return View(form);
//                }
//            }
//        }
//        #endregion

//        #region Đơn Mang Hàng Hóa Ra Cổng

//        [HttpGet("/FormHR/MangHangHoaRaCong")]
//        public IActionResult MangHangHoaRaCong()
//        {
//            // 1. Kiểm tra xác thực qua Cookie
//            if (!User.Identity.IsAuthenticated) return Redirect("/DonXetDuyet/DangNhap");

//            // 2. Lấy thông tin từ Claims (CK) thay cho Session
//            var userIdString = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
//            if (string.IsNullOrEmpty(userIdString)) return Redirect("/DonXetDuyet/DangNhap");

//            int userId = int.Parse(userIdString);
//            var userName = User.FindFirst(System.Security.Claims.ClaimTypes.Name)?.Value ?? "";
//            var phongBan = User.FindFirst("PhongBan")?.Value ?? "";
//            var viTri = User.FindFirst(System.Security.Claims.ClaimTypes.Role)?.Value ?? ""; // Hoặc "UserRole" tùy Claim bạn nạp
//            var soNhanVien = User.FindFirst(System.Security.Claims.ClaimTypes.Email)?.Value ?? "";

//            // 3. Khởi tạo model với thông tin đã lấy
//            var model = new FormHr
//            {
//                TenNguoiNv = userName,
//                PhanBo = phongBan,
//                ViTri = viTri,
//                SoNhanVien = soNhanVien,
//                Ngay = DateOnly.FromDateTime(DateTime.Now),
//                IdNguoiTao = userId,
//                TenNguoiTao = userName,
//                TimeNguoiTao = DateTime.Now,
//                TrangThai = "ChoDuyet"
//            };

//            return View(model);
//        }

//        [HttpPost("/FormHR/MangHangHoaRaCong")]
//        public async Task<IActionResult> MangHangHoaRaCong(FormHr form, [FromForm] CtMangHangHoaRaCong2 chiTiet)
//        {
//            // 1. Kiểm tra xác thực & Lấy thông tin từ CK
//            if (!User.Identity.IsAuthenticated) return Redirect("/DonXetDuyet/DangNhap");

//            var userIdString = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
//            var userName = User.FindFirst(System.Security.Claims.ClaimTypes.Name)?.Value ?? "";
//            var phongBan = User.FindFirst("PhongBan")?.Value ?? "";
//            var viTri = User.FindFirst(System.Security.Claims.ClaimTypes.Role)?.Value ?? "";
//            var soNhanVien = User.FindFirst(System.Security.Claims.ClaimTypes.Email)?.Value ?? "";

//            if (string.IsNullOrEmpty(userIdString)) return Redirect("/DonXetDuyet/DangNhap");
//            int userId = int.Parse(userIdString);

//            using (var transaction = await _context.Database.BeginTransactionAsync())
//            {
//                try
//                {
//                    // --- BƯỚC 1: THIẾT LẬP THÔNG TIN BẢNG CHÍNH (FormHr) ---
//                    form.Ngay = DateOnly.FromDateTime(DateTime.Now);
//                    form.IdNguoiTao = userId;
//                    form.TenNguoiTao = userName;
//                    form.TimeNguoiTao = DateTime.Now;
//                    form.TenNguoiNv = userName;
//                    form.PhanBo = phongBan;
//                    form.ViTri = viTri;
//                    form.SoNhanVien = soNhanVien;
//                    form.TrangThai = "ChoDuyet";
//                    form.IdForm = "CT_MangHangHoaRaCong_2";
//                    form.TenForm = "Đơn mang hàng hóa ra cổng";

//                    // Lưu bảng chính trước để lấy ID tự tăng
//                    _context.FormHrs.Add(form);
//                    await _context.SaveChangesAsync();

//                    // --- BƯỚC 2: XỬ LÝ FILE ĐÍNH KÈM (PDF/EXCEL) ---
//                    var uploadFile = Request.Form.Files["UploadFile"];
//                    if (uploadFile != null && uploadFile.Length > 0)
//                    {
//                        string wwwRootPath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "FileHR");
//                        if (!Directory.Exists(wwwRootPath)) Directory.CreateDirectory(wwwRootPath);

//                        string extension = Path.GetExtension(uploadFile.FileName);
//                        // Loại bỏ dấu tiếng Việt (Sử dụng hàm của bạn)
//                        string safeName = RemoveSign4VietnameseString(userName).Replace(" ", "");
//                        string timeStamp = DateTime.Now.ToString("ddMMyy_HHmm");

//                        // Tạo tên file độc nhất theo cấu trúc bạn yêu cầu
//                        string fileName = $"HangHoa_Don{form.Id}_User{userId}_{safeName}_{timeStamp}_{Guid.NewGuid().ToString().Substring(0, 4)}{extension}";
//                        string fullPath = Path.Combine(wwwRootPath, fileName);

//                        using (var fileStream = new FileStream(fullPath, FileMode.Create))
//                        {
//                            await uploadFile.CopyToAsync(fileStream);
//                        }

//                        form.FileDinhKem = fileName;
//                        // Cập nhật lại tên file vào record hiện tại
//                        _context.Entry(form).State = Microsoft.EntityFrameworkCore.EntityState.Modified;
//                        await _context.SaveChangesAsync();
//                    }

//                    // --- BƯỚC 3: XỬ LÝ BẢNG CHI TIẾT (CtMangHangHoaRaCong2) ---
//                    if (chiTiet != null)
//                    {
//                        chiTiet.IdFormHr = form.Id;

//                        // Lưu ảnh trực tiếp vào database dưới dạng byte[]
//                        // "AnhHangHoa" là name của input file trong View
//                        chiTiet.Anh = await GetFileBytesAsync2("AnhHangHoa");

//                        _context.CtMangHangHoaRaCong2s.Add(chiTiet);
//                        await _context.SaveChangesAsync();
//                    }

//                    // Commit giao dịch thành công
//                    await transaction.CommitAsync();

//                    TempData["Success"] = "Gửi đơn mang hàng hóa thành công!";
//                    return RedirectToAction("DonCho");
//                }
//                catch (Exception ex)
//                {
//                    // Rollback nếu có bất kỳ lỗi nào để đảm bảo tính toàn vẹn dữ liệu
//                    await transaction.RollbackAsync();
//                    ModelState.AddModelError("", "Lỗi hệ thống: " + ex.Message);
//                    return View(form);
//                }
//            }
//        }

//        #endregion

//        #region ĐƠN XE ĐI CÔNG TÁC (CT_DangKySuDungXeCongTac_3)

//        /// <summary>
//        /// Hiển thị form đăng ký mới bằng Cookie Authentication
//        /// </summary>
//        [HttpGet("/FormHR/DangKySuDungXeCongTac")]
//        public IActionResult DangKySuDungXeCongTac()
//        {
//            // 1. Kiểm tra đăng nhập qua CK
//            if (!User.Identity.IsAuthenticated) return Redirect("/DonXetDuyet/DangNhap");

//            // 2. Lấy thông tin từ Claims (CK)
//            var userIdString = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
//            if (string.IsNullOrEmpty(userIdString)) return Redirect("/DonXetDuyet/DangNhap");

//            int userId = int.Parse(userIdString);
//            var userName = User.FindFirst(System.Security.Claims.ClaimTypes.Name)?.Value ?? "";
//            var phongBan = User.FindFirst("PhongBan")?.Value ?? "";
//            var viTri = User.FindFirst(System.Security.Claims.ClaimTypes.Role)?.Value ?? "";
//            var soNhanVien = User.FindFirst(System.Security.Claims.ClaimTypes.Email)?.Value ?? "";

//            // 3. Khởi tạo model với thông tin nhân sự
//            var model = new FormHr
//            {
//                TenNguoiNv = userName,
//                PhanBo = phongBan,
//                ViTri = viTri,
//                SoNhanVien = soNhanVien,
//                Ngay = DateOnly.FromDateTime(DateTime.Now),
//                IdNguoiTao = userId,
//                TenNguoiTao = userName,
//                TimeNguoiTao = DateTime.Now,
//                TrangThai = "ChoDuyet"
//            };

//            return View(model);
//        }

//        /// <summary>
//        /// Xử lý gửi đơn đăng ký xe bằng Cookie Authentication
//        /// </summary>
//        [HttpPost("/FormHR/DangKySuDungXeCongTac")]
//        [ValidateAntiForgeryToken]
//        public async Task<IActionResult> DangKySuDungXeCongTac(FormHr form, [FromForm] CtDangKySuDungXeCongTac3 chiTiet)
//        {
//            // 1. Kiểm tra xác thực CK
//            if (!User.Identity.IsAuthenticated) return Redirect("/DonXetDuyet/DangNhap");

//            var userIdString = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
//            var userName = User.FindFirst(System.Security.Claims.ClaimTypes.Name)?.Value ?? "Unknown";
//            var phongBan = User.FindFirst("PhongBan")?.Value ?? "";
//            var viTri = User.FindFirst(System.Security.Claims.ClaimTypes.Role)?.Value ?? "";
//            var soNhanVien = User.FindFirst(System.Security.Claims.ClaimTypes.Email)?.Value ?? "";

//            if (string.IsNullOrEmpty(userIdString)) return Redirect("/DonXetDuyet/DangNhap");
//            int userId = int.Parse(userIdString);

//            // Chuẩn bị tên không dấu để lưu file
//            string safeName = RemoveSign4VietnameseString(userName).Replace(" ", "");

//            using (var transaction = await _context.Database.BeginTransactionAsync())
//            {
//                try
//                {
//                    // --- BƯỚC 1: THIẾT LẬP THÔNG TIN BẢNG CHÍNH (FormHr) ---
//                    form.Ngay = DateOnly.FromDateTime(DateTime.Now);
//                    form.IdNguoiTao = userId;
//                    form.TenNguoiTao = userName;
//                    form.TimeNguoiTao = DateTime.Now;
//                    form.TenNguoiNv = userName;
//                    form.PhanBo = phongBan;
//                    form.ViTri = viTri;
//                    form.SoNhanVien = soNhanVien;
//                    form.TrangThai = "ChoDuyet";

//                    // Định danh loại Form
//                    form.IdForm = "CT_DangKySuDungXeCongTac_3";
//                    form.TenForm = "Đơn đăng ký sử dụng xe công tác";

//                    _context.FormHrs.Add(form);
//                    await _context.SaveChangesAsync(); // Lưu để lấy form.Id tự tăng

//                    // --- BƯỚC 2: XỬ LÝ FILE CHỨNG TỪ ĐÍNH KÈM (PDF/EXCEL/WORD) ---
//                    var uploadFile = Request.Form.Files["UploadFile"];
//                    if (uploadFile != null && uploadFile.Length > 0)
//                    {
//                        string wwwRootPath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "FileHR");
//                        if (!Directory.Exists(wwwRootPath)) Directory.CreateDirectory(wwwRootPath);

//                        string extension = Path.GetExtension(uploadFile.FileName);
//                        string timeStamp = DateTime.Now.ToString("ddMMyy_HHmm");

//                        // Tên file theo cấu trúc cũ của bạn
//                        string fileName = $"Xe_ID{form.Id}_{safeName}_{timeStamp}{extension}";
//                        string fullPath = Path.Combine(wwwRootPath, fileName);

//                        using (var fileStream = new FileStream(fullPath, FileMode.Create))
//                        {
//                            await uploadFile.CopyToAsync(fileStream);
//                        }

//                        form.FileDinhKem = fileName;
//                        _context.Entry(form).State = EntityState.Modified;
//                        await _context.SaveChangesAsync();
//                    }

//                    // --- BƯỚC 3: XỬ LÝ BẢNG CHI TIẾT (CtDangKySuDungXeCongTac3) ---
//                    if (chiTiet != null)
//                    {
//                        chiTiet.IdFormHr = form.Id;

//                        // Xử lý ảnh minh chứng (Byte Array) - Input trong View phải là name="AnhXe"
//                        chiTiet.Anh = await GetFileBytesAsync2("AnhXe");

//                        _context.CtDangKySuDungXeCongTac3s.Add(chiTiet);
//                        await _context.SaveChangesAsync();
//                    }

//                    // --- BƯỚC 4: HOÀN TẤT ---
//                    await transaction.CommitAsync();

//                    TempData["Success"] = "Gửi đơn đăng ký xe công tác thành công!";
//                    return RedirectToAction("DonCho");
//                }
//                catch (Exception ex)
//                {
//                    // Rollback nếu có lỗi để tránh dữ liệu rác
//                    await transaction.RollbackAsync();
//                    ModelState.AddModelError("", "Lỗi hệ thống: " + ex.Message);
//                    return View(form);
//                }
//            }
//        }

//        #endregion

//        #region ĐƠN XE ĐI DangKySuDungXeDaily (CT_DangKySuDungXeDaily_4)

//        /// <summary>
//        /// Hiển thị form đăng ký xe Daily sử dụng Cookie Authentication
//        /// </summary>
//        [HttpGet("/FormHR/DangKySuDungXeDaily")]
//        public IActionResult DangKySuDungXeDaily()
//        {
//            // 1. Kiểm tra xác thực qua Cookie
//            if (!User.Identity.IsAuthenticated) return Redirect("/DonXetDuyet/DangNhap");

//            // 2. Lấy thông tin từ Claims (CK)
//            var userIdString = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
//            if (string.IsNullOrEmpty(userIdString)) return Redirect("/DonXetDuyet/DangNhap");

//            int userId = int.Parse(userIdString);
//            var userName = User.FindFirst(System.Security.Claims.ClaimTypes.Name)?.Value ?? "";
//            var phongBan = User.FindFirst("PhongBan")?.Value ?? "";
//            var viTri = User.FindFirst(System.Security.Claims.ClaimTypes.Role)?.Value ?? "";
//            var soNhanVien = User.FindFirst(System.Security.Claims.ClaimTypes.Email)?.Value ?? "";

//            // 3. Khởi tạo model đồng bộ dữ liệu người dùng
//            var model = new FormHr
//            {
//                TenNguoiNv = userName,
//                PhanBo = phongBan,
//                ViTri = viTri,
//                SoNhanVien = soNhanVien,
//                Ngay = DateOnly.FromDateTime(DateTime.Now),
//                IdNguoiTao = userId,
//                TenNguoiTao = userName,
//                TimeNguoiTao = DateTime.Now,
//                TrangThai = "ChoDuyet"
//            };

//            return View(model);
//        }

//        /// <summary>
//        /// Xử lý gửi đơn xe Daily sử dụng Cookie Authentication
//        /// </summary>
//        [HttpPost("/FormHR/DangKySuDungXeDaily")]
//        [ValidateAntiForgeryToken]
//        public async Task<IActionResult> DangKySuDungXeDaily(FormHr form, [FromForm] CtDangKySuDungXeDaily4 chiTiet)
//        {
//            // 1. Kiểm tra xác thực & Lấy thông tin từ CK
//            if (!User.Identity.IsAuthenticated) return Redirect("/DonXetDuyet/DangNhap");

//            var userIdString = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
//            var userName = User.FindFirst(System.Security.Claims.ClaimTypes.Name)?.Value ?? "Unknown";
//            var phongBan = User.FindFirst("PhongBan")?.Value ?? "";
//            var viTri = User.FindFirst(System.Security.Claims.ClaimTypes.Role)?.Value ?? "";
//            var soNhanVien = User.FindFirst(System.Security.Claims.ClaimTypes.Email)?.Value ?? "";

//            if (string.IsNullOrEmpty(userIdString)) return Redirect("/DonXetDuyet/DangNhap");
//            int userId = int.Parse(userIdString);

//            // Chuẩn bị tên file không dấu
//            string safeName = RemoveSign4VietnameseString(userName).Replace(" ", "");

//            using (var transaction = await _context.Database.BeginTransactionAsync())
//            {
//                try
//                {
//                    // --- BƯỚC 1: CẬP NHẬT THÔNG TIN BẢNG CHÍNH (FormHr) ---
//                    form.Ngay = DateOnly.FromDateTime(DateTime.Now);
//                    form.IdNguoiTao = userId;
//                    form.TenNguoiTao = userName;
//                    form.TimeNguoiTao = DateTime.Now;
//                    form.TenNguoiNv = userName;
//                    form.PhanBo = phongBan;
//                    form.ViTri = viTri;
//                    form.SoNhanVien = soNhanVien;
//                    form.TrangThai = "ChoDuyet";
//                    form.IdForm = "CT_DangKySuDungXeDaily_4";
//                    form.TenForm = "Đăng ký sử dụng xe Daily";

//                    _context.FormHrs.Add(form);
//                    await _context.SaveChangesAsync(); // Lưu để lấy ID tự tăng

//                    // --- BƯỚC 2: XỬ LÝ FILE ĐÍNH KÈM (PDF/EXCEL/WORD) ---
//                    var uploadFile = Request.Form.Files["UploadFile"];
//                    if (uploadFile != null && uploadFile.Length > 0)
//                    {
//                        string wwwRootPath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "FileHR");
//                        if (!Directory.Exists(wwwRootPath)) Directory.CreateDirectory(wwwRootPath);

//                        // Tạo tên file theo định dạng bạn yêu cầu
//                        string extension = Path.GetExtension(uploadFile.FileName);
//                        string timeStamp = DateTime.Now.ToString("ddMMyy_HHmm");
//                        string fileName = $"XeDaily_ID{form.Id}_{safeName}_{timeStamp}{extension}";
//                        string fullPath = Path.Combine(wwwRootPath, fileName);

//                        using (var fileStream = new FileStream(fullPath, FileMode.Create))
//                        {
//                            await uploadFile.CopyToAsync(fileStream);
//                        }

//                        form.FileDinhKem = fileName;
//                        _context.Entry(form).State = Microsoft.EntityFrameworkCore.EntityState.Modified;
//                        await _context.SaveChangesAsync();
//                    }

//                    // --- BƯỚC 3: CẬP NHẬT BẢNG CHI TIẾT (CtDangKySuDungXeDaily4) ---
//                    if (chiTiet != null)
//                    {
//                        chiTiet.IdFormHr = form.Id;

//                        // Lưu ảnh từ vùng Paste/Upload (Byte Array) - Tên input: "AnhXe"
//                        chiTiet.Anh = await GetFileBytesAsync2("AnhXe");

//                        _context.CtDangKySuDungXeDaily4s.Add(chiTiet);
//                        await _context.SaveChangesAsync();
//                    }

//                    // Hoàn tất mọi thay đổi
//                    await transaction.CommitAsync();

//                    TempData["Success"] = "Gửi đơn đăng ký xe Daily thành công!";
//                    return RedirectToAction("DonCho");
//                }
//                catch (Exception ex)
//                {
//                    // Nếu có lỗi, thu hồi toàn bộ (Rollback) tránh dữ liệu rác
//                    await transaction.RollbackAsync();
//                    ModelState.AddModelError("", "Lỗi hệ thống: " + ex.Message);
//                    return View(form);
//                }
//            }
//        }
//        #endregion

//        #region ĐƠN Tiếp Khách (CT_DonTiepKhac_5)

//        /// <summary>
//        /// Hiển thị form đăng ký tiếp khách sử dụng Cookie Authentication
//        /// </summary>
//        [HttpGet("/FormHR/DonTiepKhac")]
//        public IActionResult DonTiepKhac()
//        {
//            // 1. Kiểm tra xác thực qua Cookie
//            if (!User.Identity.IsAuthenticated) return Redirect("/DonXetDuyet/DangNhap");

//            // 2. Lấy thông tin từ Claims (CK) thay cho Session
//            var userIdString = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
//            if (string.IsNullOrEmpty(userIdString)) return Redirect("/DonXetDuyet/DangNhap");

//            int userId = int.Parse(userIdString);
//            var userName = User.FindFirst(System.Security.Claims.ClaimTypes.Name)?.Value ?? "";
//            var phongBan = User.FindFirst("PhongBan")?.Value ?? "";
//            var viTri = User.FindFirst(System.Security.Claims.ClaimTypes.Role)?.Value ?? "";
//            var soNhanVien = User.FindFirst(System.Security.Claims.ClaimTypes.Email)?.Value ?? "";

//            // 3. Khởi tạo model đồng bộ dữ liệu người dùng
//            var model = new FormHr
//            {
//                TenNguoiNv = userName,
//                PhanBo = phongBan,
//                ViTri = viTri,
//                SoNhanVien = soNhanVien,
//                Ngay = DateOnly.FromDateTime(DateTime.Now),
//                IdNguoiTao = userId,
//                TenNguoiTao = userName,
//                TimeNguoiTao = DateTime.Now,
//                TrangThai = "ChoDuyet"
//            };
//            return View(model);
//        }

//        /// <summary>
//        /// Xử lý gửi đơn tiếp khách sử dụng Cookie Authentication
//        /// </summary>
//        [HttpPost("/FormHR/DonTiepKhac")]
//        [ValidateAntiForgeryToken]
//        public async Task<IActionResult> DonTiepKhac(FormHr form, [FromForm] CtDonTiepKhac5 chiTiet)
//        {
//            // 1. Kiểm tra xác thực & Lấy thông tin từ CK
//            if (!User.Identity.IsAuthenticated) return Redirect("/DonXetDuyet/DangNhap");

//            var userIdString = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
//            var userName = User.FindFirst(System.Security.Claims.ClaimTypes.Name)?.Value ?? "Unknown";
//            var phongBan = User.FindFirst("PhongBan")?.Value ?? "";
//            var viTri = User.FindFirst(System.Security.Claims.ClaimTypes.Role)?.Value ?? "";
//            var soNhanVien = User.FindFirst(System.Security.Claims.ClaimTypes.Email)?.Value ?? "";

//            if (string.IsNullOrEmpty(userIdString)) return Redirect("/DonXetDuyet/DangNhap");
//            int userId = int.Parse(userIdString);

//            // Chuẩn bị tên file không dấu
//            string safeName = RemoveSign4VietnameseString(userName).Replace(" ", "");

//            using (var transaction = await _context.Database.BeginTransactionAsync())
//            {
//                try
//                {
//                    // --- BƯỚC 1: CẬP NHẬT THÔNG TIN BẢNG CHÍNH (FormHr) ---
//                    form.Ngay = DateOnly.FromDateTime(DateTime.Now);
//                    form.IdNguoiTao = userId;
//                    form.TenNguoiTao = userName;
//                    form.TimeNguoiTao = DateTime.Now;
//                    form.TenNguoiNv = userName;
//                    form.PhanBo = phongBan;
//                    form.ViTri = viTri;
//                    form.SoNhanVien = soNhanVien;
//                    form.TrangThai = "ChoDuyet";
//                    form.IdForm = "CT_DonTiepKhac_5";
//                    form.TenForm = "Đơn đăng ký tiếp khách";

//                    _context.FormHrs.Add(form);
//                    await _context.SaveChangesAsync(); // Lưu để lấy ID tự tăng

//                    // --- BƯỚC 2: XỬ LÝ FILE ĐÍNH KÈM (PDF/EXCEL/WORD) ---
//                    var uploadFile = Request.Form.Files["UploadFile"];
//                    if (uploadFile != null && uploadFile.Length > 0)
//                    {
//                        string wwwRootPath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "FileHR");
//                        if (!Directory.Exists(wwwRootPath)) Directory.CreateDirectory(wwwRootPath);

//                        string fileName = $"TiepKhac_ID{form.Id}_{safeName}_{DateTime.Now:ddMMyy_HHmm}{Path.GetExtension(uploadFile.FileName)}";
//                        string fullPath = Path.Combine(wwwRootPath, fileName);

//                        using (var fileStream = new FileStream(fullPath, FileMode.Create))
//                        {
//                            await uploadFile.CopyToAsync(fileStream);
//                        }
//                        form.FileDinhKem = fileName;
//                        _context.Entry(form).State = EntityState.Modified;
//                        await _context.SaveChangesAsync();
//                    }

//                    // --- BƯỚC 3: CẬP NHẬT BẢNG CHI TIẾT (CtDonTiepKhac5) ---
//                    if (chiTiet != null)
//                    {
//                        chiTiet.IdFormHr = form.Id;

//                        // Xử lý cả 2 ảnh minh chứng (Phòng và Đặt cơm) thành Byte Array
//                        // Lưu ý: name trong View phải khớp là "AnhPhong" và "AnhDatCom"
//                        chiTiet.AnhPhong = await GetFileBytesAsync2("AnhPhong");
//                        chiTiet.AnhDatCom = await GetFileBytesAsync2("AnhDatCom");

//                        _context.CtDonTiepKhac5s.Add(chiTiet);
//                        await _context.SaveChangesAsync();
//                    }

//                    // Hoàn tất mọi thay đổi
//                    await transaction.CommitAsync();

//                    TempData["Success"] = "Gửi đơn tiếp khách thành công!";
//                    return RedirectToAction("DonCho");
//                }
//                catch (Exception ex)
//                {
//                    // Nếu có lỗi, thu hồi toàn bộ (Rollback)
//                    await transaction.RollbackAsync();
//                    ModelState.AddModelError("", "Lỗi: " + ex.Message);
//                    return View(form);
//                }
//            }
//        }
//        #endregion

//        #region ĐƠN NhaThauQuaCong (CT_NhaThauQuaCong_6)

//        /// <summary>
//        /// Hiển thị form đăng ký nhà thầu qua cổng sử dụng Cookie Authentication
//        /// </summary>
//        [HttpGet("/FormHR/NhaThauQuaCong")]
//        public IActionResult NhaThauQuaCong()
//        {
//            // 1. Kiểm tra xác thực qua Cookie
//            if (!User.Identity.IsAuthenticated) return Redirect("/DonXetDuyet/DangNhap");

//            // 2. Lấy thông tin từ Claims (CK)
//            var userIdString = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
//            if (string.IsNullOrEmpty(userIdString)) return Redirect("/DonXetDuyet/DangNhap");

//            int userId = int.Parse(userIdString);
//            var userName = User.FindFirst(System.Security.Claims.ClaimTypes.Name)?.Value ?? "";
//            var phongBan = User.FindFirst("PhongBan")?.Value ?? "";
//            var viTri = User.FindFirst(System.Security.Claims.ClaimTypes.Role)?.Value ?? "";
//            var soNhanVien = User.FindFirst(System.Security.Claims.ClaimTypes.Email)?.Value ?? "";

//            // 3. Khởi tạo model đồng bộ dữ liệu người dùng
//            var model = new FormHr
//            {
//                TenNguoiNv = userName,
//                PhanBo = phongBan,
//                ViTri = viTri,
//                SoNhanVien = soNhanVien,
//                Ngay = DateOnly.FromDateTime(DateTime.Now),
//                IdNguoiTao = userId,
//                TenNguoiTao = userName,
//                TimeNguoiTao = DateTime.Now,
//                TrangThai = "ChoDuyet"
//            };
//            return View(model);
//        }

//        /// <summary>
//        /// Xử lý gửi đơn nhà thầu sử dụng Cookie Authentication
//        /// </summary>
//        [HttpPost("/FormHR/NhaThauQuaCong")]
//        [ValidateAntiForgeryToken]
//        public async Task<IActionResult> NhaThauQuaCong(FormHr form, [FromForm] CtNhaThauQuaCong6 chiTiet)
//        {
//            // 1. Kiểm tra xác thực & Lấy thông tin từ CK
//            if (!User.Identity.IsAuthenticated) return Redirect("/DonXetDuyet/DangNhap");

//            var userIdString = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
//            var userName = User.FindFirst(System.Security.Claims.ClaimTypes.Name)?.Value ?? "Unknown";
//            var phongBan = User.FindFirst("PhongBan")?.Value ?? "";
//            var viTri = User.FindFirst(System.Security.Claims.ClaimTypes.Role)?.Value ?? "";
//            var soNhanVien = User.FindFirst(System.Security.Claims.ClaimTypes.Email)?.Value ?? "";

//            if (string.IsNullOrEmpty(userIdString)) return Redirect("/DonXetDuyet/DangNhap");
//            int userId = int.Parse(userIdString);

//            // Chuẩn bị tên không dấu cho việc lưu file
//            string safeName = RemoveSign4VietnameseString(userName).Replace(" ", "");

//            using (var transaction = await _context.Database.BeginTransactionAsync())
//            {
//                try
//                {
//                    // --- BƯỚC 1: THIẾT LẬP THÔNG TIN BẢNG CHÍNH (FormHr) ---
//                    form.Ngay = DateOnly.FromDateTime(DateTime.Now);
//                    form.IdNguoiTao = userId;
//                    form.TenNguoiTao = userName;
//                    form.TimeNguoiTao = DateTime.Now;
//                    form.TenNguoiNv = userName;
//                    form.PhanBo = phongBan;
//                    form.ViTri = viTri;
//                    form.SoNhanVien = soNhanVien;
//                    form.TrangThai = "ChoDuyet";
//                    form.IdForm = "CT_NhaThauQuaCong_6";
//                    form.TenForm = "Đơn đăng ký nhà thầu qua cổng";

//                    _context.FormHrs.Add(form);
//                    await _context.SaveChangesAsync(); // Lưu để lấy ID tự tăng

//                    // --- BƯỚC 2: XỬ LÝ FILE ĐÍNH KÈM (PDF/EXCEL/WORD) ---
//                    var uploadFile = Request.Form.Files["UploadFile"];
//                    if (uploadFile != null && uploadFile.Length > 0)
//                    {
//                        string wwwRootPath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "FileHR");
//                        if (!Directory.Exists(wwwRootPath)) Directory.CreateDirectory(wwwRootPath);

//                        string extension = Path.GetExtension(uploadFile.FileName);
//                        string timeStamp = DateTime.Now.ToString("ddMMyy_HHmm");
//                        string fileName = $"NhaThau_ID{form.Id}_{safeName}_{timeStamp}{extension}";
//                        string fullPath = Path.Combine(wwwRootPath, fileName);

//                        using (var fileStream = new FileStream(fullPath, FileMode.Create))
//                        {
//                            await uploadFile.CopyToAsync(fileStream);
//                        }
//                        form.FileDinhKem = fileName;
//                        _context.Entry(form).State = Microsoft.EntityFrameworkCore.EntityState.Modified;
//                        await _context.SaveChangesAsync();
//                    }

//                    // --- BƯỚC 3: XỬ LÝ BẢNG CHI TIẾT (CtNhaThauQuaCong6) ---
//                    if (chiTiet != null)
//                    {
//                        chiTiet.IdFormHr = form.Id;

//                        // Lưu ảnh minh chứng (Mảng byte) - Tên input trong View: "Anh"
//                        chiTiet.Anh = await GetFileBytesAsync2("Anh");

//                        _context.CtNhaThauQuaCong6s.Add(chiTiet);
//                        await _context.SaveChangesAsync();
//                    }

//                    // --- BƯỚC 4: HOÀN TẤT ---
//                    await transaction.CommitAsync();

//                    TempData["Success"] = "Gửi đơn đăng ký nhà thầu thành công!";
//                    return RedirectToAction("DonCho");
//                }
//                catch (Exception ex)
//                {
//                    // Rollback nếu có lỗi xảy ra
//                    await transaction.RollbackAsync();
//                    ModelState.AddModelError("", "Lỗi hệ thống: " + ex.Message);
//                    return View(form);
//                }
//            }
//        }
//        #endregion

//        #region ĐƠN HoTroTienDienThoai (CT_HoTroTienDienThoai_7)

//        /// <summary>
//        /// Hiển thị form đăng ký hỗ trợ tiền điện thoại sử dụng Cookie Authentication
//        /// </summary>
//        [HttpGet("/FormHR/HoTroTienDienThoai")]
//        public IActionResult HoTroTienDienThoai()
//        {
//            // 1. Kiểm tra xác thực qua Cookie
//            if (!User.Identity.IsAuthenticated) return Redirect("/DonXetDuyet/DangNhap");

//            // 2. Lấy thông tin từ Claims (CK)
//            var userIdString = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
//            if (string.IsNullOrEmpty(userIdString)) return Redirect("/DonXetDuyet/DangNhap");

//            int userId = int.Parse(userIdString);
//            var userName = User.FindFirst(System.Security.Claims.ClaimTypes.Name)?.Value ?? "";
//            var phongBan = User.FindFirst("PhongBan")?.Value ?? "";
//            var viTri = User.FindFirst(System.Security.Claims.ClaimTypes.Role)?.Value ?? "";
//            var soNhanVien = User.FindFirst(System.Security.Claims.ClaimTypes.Email)?.Value ?? "";

//            // 3. Khởi tạo model với đầy đủ thông tin từ CK
//            var model = new FormHr
//            {
//                TenNguoiNv = userName,
//                PhanBo = phongBan,
//                ViTri = viTri,
//                SoNhanVien = soNhanVien,
//                Ngay = DateOnly.FromDateTime(DateTime.Now),
//                IdNguoiTao = userId,
//                TenNguoiTao = userName,
//                TimeNguoiTao = DateTime.Now,
//                TrangThai = "ChoDuyet"
//            };
//            return View(model);
//        }

//        /// <summary>
//        /// Xử lý gửi đơn hỗ trợ tiền điện thoại sử dụng Cookie Authentication
//        /// </summary>
//        [HttpPost("/FormHR/HoTroTienDienThoai")]
//        [ValidateAntiForgeryToken]
//        public async Task<IActionResult> HoTroTienDienThoai(FormHr form, [FromForm] CtHoTroTienDienThoai7 chiTiet)
//        {
//            // 1. Kiểm tra xác thực & Lấy thông tin từ CK
//            if (!User.Identity.IsAuthenticated) return Redirect("/DonXetDuyet/DangNhap");

//            var userIdString = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
//            var userName = User.FindFirst(System.Security.Claims.ClaimTypes.Name)?.Value ?? "Unknown";
//            var phongBan = User.FindFirst("PhongBan")?.Value ?? "";
//            var viTri = User.FindFirst(System.Security.Claims.ClaimTypes.Role)?.Value ?? "";
//            var soNhanVien = User.FindFirst(System.Security.Claims.ClaimTypes.Email)?.Value ?? "";

//            if (string.IsNullOrEmpty(userIdString)) return Redirect("/DonXetDuyet/DangNhap");
//            int userId = int.Parse(userIdString);

//            // Chuẩn bị tên không dấu để đặt tên file
//            string safeName = RemoveSign4VietnameseString(userName).Replace(" ", "");

//            using (var transaction = await _context.Database.BeginTransactionAsync())
//            {
//                try
//                {
//                    // --- BƯỚC 1: THIẾT LẬP THÔNG TIN BẢNG CHÍNH (FormHr) ---
//                    form.Ngay = DateOnly.FromDateTime(DateTime.Now);
//                    form.IdNguoiTao = userId;
//                    form.TenNguoiTao = userName;
//                    form.TimeNguoiTao = DateTime.Now;
//                    form.TenNguoiNv = userName;
//                    form.BoPhan = phongBan;
//                    form.ViTri = viTri;
//                    form.SoNhanVien = soNhanVien;
//                    form.TrangThai = "ChoDuyet";
//                    form.IdForm = "CT_HoTroTienDienThoai_7";
//                    form.TenForm = "Đơn đăng ký hỗ trợ tiền điện thoại";

//                    _context.FormHrs.Add(form);
//                    await _context.SaveChangesAsync(); // Lưu để lấy form.Id

//                    // --- BƯỚC 2: XỬ LÝ FILE ĐÍNH KÈM (PDF/EXCEL/IMAGE...) ---
//                    var uploadFile = Request.Form.Files["UploadFile"];
//                    if (uploadFile != null && uploadFile.Length > 0)
//                    {
//                        string wwwRootPath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "FileHR");
//                        if (!Directory.Exists(wwwRootPath)) Directory.CreateDirectory(wwwRootPath);

//                        string extension = Path.GetExtension(uploadFile.FileName);
//                        string timeStamp = DateTime.Now.ToString("ddMMyy_HHmm");

//                        // Tên file: TienDienThoai_ID[Id]_[Ten]_[ThoiGian].[Ext]
//                        string fileName = $"TienDienThoai_ID{form.Id}_{safeName}_{timeStamp}{extension}";
//                        string fullPath = Path.Combine(wwwRootPath, fileName);

//                        using (var fileStream = new FileStream(fullPath, FileMode.Create))
//                        {
//                            await uploadFile.CopyToAsync(fileStream);
//                        }
//                        form.FileDinhKem = fileName;
//                        _context.Entry(form).State = EntityState.Modified;
//                        await _context.SaveChangesAsync();
//                    }

//                    // --- BƯỚC 3: XỬ LÝ BẢNG CHI TIẾT (CtHoTroTienDienThoai7) ---
//                    if (chiTiet != null)
//                    {
//                        chiTiet.IdFormHr = form.Id;

//                        // Chuyển đổi ảnh minh chứng (Paste/Upload) thành mảng Byte
//                        // Lưu ý: Input trong View phải có name="Anh"
//                        chiTiet.Anh = await GetFileBytesAsync2("Anh");

//                        _context.HrHoTroTienDienThoai7s.Add(chiTiet);
//                        await _context.SaveChangesAsync();
//                    }

//                    // Hoàn tất mọi thay đổi vào Database
//                    await transaction.CommitAsync();

//                    TempData["Success"] = "Gửi đơn hỗ trợ tiền điện thoại thành công!";
//                    return RedirectToAction("DonCho");
//                }
//                catch (Exception ex)
//                {
//                    // Nếu lỗi, Rollback toàn bộ để đảm bảo tính toàn vẹn
//                    await transaction.RollbackAsync();
//                    ModelState.AddModelError("", "Lỗi hệ thống: " + ex.Message);
//                    return View(form);
//                }
//            }
//        }

//        #endregion

//        #region ĐƠN DoiCaLam (CT_DoiCaLam_8)

//        /// <summary>
//        /// Hiển thị form đăng ký đổi ca làm việc sử dụng Cookie Authentication
//        /// </summary>
//        [HttpGet("/FormHR/DoiCaLam")]
//        public IActionResult DoiCaLam()
//        {
//            // 1. Kiểm tra xác thực qua CK
//            if (!User.Identity.IsAuthenticated) return Redirect("/DonXetDuyet/DangNhap");

//            // 2. Lấy thông tin từ Claims (Thay thế hoàn toàn Session)
//            var userIdString = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
//            if (string.IsNullOrEmpty(userIdString)) return Redirect("/DonXetDuyet/DangNhap");

//            int userId = int.Parse(userIdString);
//            var userName = User.FindFirst(System.Security.Claims.ClaimTypes.Name)?.Value ?? "";
//            var phongBan = User.FindFirst("PhongBan")?.Value ?? "";
//            var viTri = User.FindFirst(System.Security.Claims.ClaimTypes.Role)?.Value ?? "";
//            var soNhanVien = User.FindFirst(System.Security.Claims.ClaimTypes.Email)?.Value ?? "";

//            // 3. Khởi tạo model với thông tin từ CK
//            var model = new FormHr
//            {
//                TenNguoiNv = userName,
//                BoPhan = phongBan,
//                ViTri = viTri,
//                SoNhanVien = soNhanVien,
//                Ngay = DateOnly.FromDateTime(DateTime.Now),
//                IdNguoiTao = userId,
//                TenNguoiTao = userName,
//                TimeNguoiTao = DateTime.Now,
//                TrangThai = "ChoDuyet"
//            };
//            return View(model);
//        }

//        /// <summary>
//        /// Xử lý gửi đơn đổi ca làm việc sử dụng Cookie Authentication
//        /// </summary>
//        [HttpPost("/FormHR/DoiCaLam")]
//        [ValidateAntiForgeryToken]
//        public async Task<IActionResult> DoiCaLam(FormHr form, [FromForm] CtDoiCaLam8 chiTiet)
//        {
//            // 1. Kiểm tra xác thực CK
//            if (!User.Identity.IsAuthenticated) return Redirect("/DonXetDuyet/DangNhap");

//            var userIdString = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
//            var userName = User.FindFirst(System.Security.Claims.ClaimTypes.Name)?.Value ?? "Unknown";
//            var phongBan = User.FindFirst("PhongBan")?.Value ?? "";
//            var viTri = User.FindFirst(System.Security.Claims.ClaimTypes.Role)?.Value ?? "";
//            var soNhanVien = User.FindFirst(System.Security.Claims.ClaimTypes.Email)?.Value ?? "";

//            if (string.IsNullOrEmpty(userIdString)) return Redirect("/DonXetDuyet/DangNhap");
//            int userId = int.Parse(userIdString);

//            // Chuẩn bị tên không dấu để lưu file
//            string safeName = RemoveSign4VietnameseString(userName).Replace(" ", "");

//            using (var transaction = await _context.Database.BeginTransactionAsync())
//            {
//                try
//                {
//                    // --- BƯỚC 1: CẬP NHẬT THÔNG TIN BẢNG CHÍNH (FormHr) ---
//                    form.Ngay = DateOnly.FromDateTime(DateTime.Now);
//                    form.IdNguoiTao = userId;
//                    form.TenNguoiTao = userName;
//                    form.TimeNguoiTao = DateTime.Now;
//                    form.TenNguoiNv = userName;
//                    form.BoPhan = phongBan;
//                    form.ViTri = viTri;
//                    form.SoNhanVien = soNhanVien;
//                    form.TrangThai = "ChoDuyet";
//                    form.IdForm = "CT_DoiCaLam_8"; // Định danh đơn số 8
//                    form.TenForm = "Đơn đăng ký đổi ca làm việc";

//                    _context.FormHrs.Add(form);
//                    await _context.SaveChangesAsync(); // Lưu để lấy form.Id

//                    // --- BƯỚC 2: XỬ LÝ FILE ĐÍNH KÈM (PDF/WORD/EXCEL) ---
//                    var uploadFile = Request.Form.Files["UploadFile"];
//                    if (uploadFile != null && uploadFile.Length > 0)
//                    {
//                        string wwwRootPath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "FileHR");
//                        if (!Directory.Exists(wwwRootPath)) Directory.CreateDirectory(wwwRootPath);

//                        string extension = Path.GetExtension(uploadFile.FileName);
//                        string timeStamp = DateTime.Now.ToString("ddMMyy_HHmm");

//                        // Cấu trúc tên file: DoiCaLam_ID[Id]_[Ten]_[ThoiGian]
//                        string fileName = $"DoiCaLam_ID{form.Id}_{safeName}_{timeStamp}{extension}";
//                        string fullPath = Path.Combine(wwwRootPath, fileName);

//                        using (var fileStream = new FileStream(fullPath, FileMode.Create))
//                        {
//                            await uploadFile.CopyToAsync(fileStream);
//                        }
//                        form.FileDinhKem = fileName;
//                        _context.Entry(form).State = EntityState.Modified;
//                        await _context.SaveChangesAsync();
//                    }

//                    // --- BƯỚC 3: XỬ LÝ BẢNG CHI TIẾT (CtDoiCaLam8) ---
//                    if (chiTiet != null)
//                    {
//                        chiTiet.IdFormHr = form.Id;

//                        // Chuyển đổi ảnh minh chứng (Paste từ Clipboard hoặc Upload) thành mảng Byte
//                        // Đảm bảo trong View input file/hidden có name="Anh"
//                        chiTiet.Anh = await GetFileBytesAsync2("Anh");

//                        _context.HrDoiCaLam8s.Add(chiTiet);
//                        await _context.SaveChangesAsync();
//                    }

//                    // Hoàn tất giao dịch thành công
//                    await transaction.CommitAsync();

//                    TempData["Success"] = "Gửi đơn đổi ca làm thành công!";
//                    return RedirectToAction("DonCho");
//                }
//                catch (Exception ex)
//                {
//                    // Hủy bỏ nếu có bất kỳ lỗi nào phát sinh
//                    await transaction.RollbackAsync();
//                    ModelState.AddModelError("", "Lỗi hệ thống: " + ex.Message);
//                    return View(form);
//                }
//            }
//        }

//        #endregion

//        #region CHI TIẾT ĐƠN FORM HR (TẤT CẢ 8 LOẠI ĐƠN)

//        /// <summary>
//        /// Xem chi tiết nội dung đơn HR dựa trên ID. 
//        /// Tự động nạp dữ liệu từ tất cả 8 bảng chi tiết để dùng Cookie Authentication (CK).
//        /// </summary>
//        [HttpGet("/FormHR/ChiTiet/{id}")]
//        public async Task<IActionResult> ChiTiet(int id)
//        {
//            // 1. Kiểm tra xác thực Cookie
//            if (User.Identity == null || !User.Identity.IsAuthenticated)
//            {
//                return RedirectToAction("DangNhap", "DonXetDuyet");
//            }

//            // 2. Lấy thông tin User từ Claims (CK)
//            var userIdClaim = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
//            // Lấy Role linh hoạt (hỗ trợ cả schema URL dài của CK)
//            var userRole = User.FindFirst(System.Security.Claims.ClaimTypes.Role)?.Value
//                          ?? User.FindFirst("http://schemas.microsoft.com/ws/2008/06/identity/claims/role")?.Value
//                          ?? "";

//            if (string.IsNullOrEmpty(userIdClaim))
//            {
//                return RedirectToAction("DangNhap", "DonXetDuyet");
//            }

//            // 3. Truy vấn đơn và Eager Loading đầy đủ 8 bảng (KHÔNG BỎ BỚT)
//            var don = await _context.FormHrs
//                .Include(f => f.CtXinRaNgoai1s)                 // Loại 1
//                .Include(f => f.CtMangHangHoaRaCong2s)          // Loại 2
//                .Include(f => f.CtDangKySuDungXeCongTac3s)      // Loại 3
//                .Include(f => f.CtDangKySuDungXeDaily4s)        // Loại 4
//                .Include(f => f.CtDonTiepKhac5s)                // Loại 5
//                .Include(f => f.CtNhaThauQuaCong6s)             // Loại 6
//                .Include(f => f.CtHoTroTienDienThoai7s)         // Loại 7
//                .Include(f => f.CtDoiCaLam8s)                   // Loại 8
//                .FirstOrDefaultAsync(m => m.Id == id);

//            // 4. Kiểm tra tồn tại
//            if (don == null)
//            {
//                TempData["Error"] = "Không tìm thấy dữ liệu đơn yêu cầu!";
//                return RedirectToAction("DonCho");
//            }

//            // 5. Logic bảo mật (Tùy chọn): Chỉ người liên quan mới được xem
//            /*
//            int currentUserId = int.Parse(userIdClaim);
//            bool isAdminOrManager = userRole.Contains("Admin") || userRole.Contains("QuanLy") || userRole.Contains("All");
//            if (don.IdNguoiTao != currentUserId && !isAdminOrManager)
//            {
//                return Forbid(); // Trả về lỗi 403 nếu xem trộm đơn
//            }
//            */

//            return View(don);
//        }

//        #endregion

//        #region ĐƠN CHỜ XÉT DUYỆT (Dành cho nhân viên xem danh sách đơn đã tạo)

//        /// <summary>
//        /// GET: /FormHR/DonCho
//        /// Hiển thị danh sách các đơn mà nhân viên hiện tại đã gửi và đang chờ xét duyệt
//        /// </summary>
//        [HttpGet("/FormHR/DonCho")]
//        public async Task<IActionResult> DonCho()
//        {
//            // 1. Kiểm tra xác thực qua Cookie Authentication (CK)
//            if (!User.Identity.IsAuthenticated)
//            {
//                return Redirect("/DonXetDuyet/DangNhap");
//            }

//            // 2. Lấy UserId từ Claims (CK) thay thế cho Session
//            var userIdClaim = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
//            if (string.IsNullOrEmpty(userIdClaim))
//            {
//                return Redirect("/DonXetDuyet/DangNhap");
//            }

//            int currentUserId = int.Parse(userIdClaim);

//            // 3. Truy vấn danh sách đơn của chính người dùng này
//            // Sử dụng OrderByDescending để đơn mới nhất luôn nằm ở vị trí đầu tiên
//            var danhSachDon = await _context.FormHrs
//                .Where(f => f.IdNguoiTao == currentUserId)
//                .OrderByDescending(f => f.Id)
//                .ToListAsync();

//            // 4. Trả về View cùng danh sách đơn
//            return View(danhSachDon);
//        }

//        #endregion

//        #region XỬ LÝ ĐƠN (Duyệt / Hủy / Quản lý)

//        /// <summary>
//        /// GET: /FormHR/QuanLyXetDuyet
//        /// Lọc đơn theo bộ phận cho Quản lý/Admin hoặc toàn bộ cho quyền ALL
//        /// </summary>
//        [HttpGet("/FormHR/QuanLyXetDuyet")]
//        public async Task<IActionResult> QuanLyXetDuyet()
//        {
//            // 1. Kiểm tra đăng nhập qua Cookie Authentication
//            if (!User.Identity.IsAuthenticated) return Redirect("/DonXetDuyet/DangNhap");

//            // 2. Lấy thông tin từ Claims (CK)
//            var userIdString = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
//            if (string.IsNullOrEmpty(userIdString)) return Redirect("/DonXetDuyet/DangNhap");

//            int userId = int.Parse(userIdString);
//            // Chuẩn hóa Role về viết hoa để so sánh chính xác
//            var userRole = (User.FindFirst(System.Security.Claims.ClaimTypes.Role)?.Value
//                           ?? User.FindFirst("UserRole")?.Value
//                           ?? "").Trim().ToUpper();

//            // Lấy PhongBan từ Claim (Bộ phận của người đang đăng nhập)
//            var phongBan = (User.FindFirst("PhongBan")?.Value
//                           ?? User.FindFirst("PhanBo")?.Value
//                           ?? "").Trim();

//            // Bảo vệ: Quyền Bảo vệ không được vào trang này
//            if (userRole == "BAOVE") return Forbid();

//            IQueryable<FormHr> query = _context.FormHrs;

//            // 3. Xử lý phân quyền xem dữ liệu (Không bỏ bớt trường hợp nào)
//            if (userRole == "ALL")
//            {
//                // QUYỀN ALL: Xem được tất cả mọi đơn không cần lọc
//            }
//            else if (userRole == "ADMIN" || userRole == "QUANLY")
//            {
//                // XEM THEO BỘ PHẬN: Chỉ thấy đơn của nhân viên cùng bộ phận
//                if (!string.IsNullOrEmpty(phongBan))
//                {
//                    // So sánh không phân biệt hoa thường và xóa khoảng trắng
//                    // Lọc các đơn mà trường 'PhanBo' trong DB khớp với 'phongBan' trong Cookie
//                    query = query.Where(f => f.BoPhan != null && f.BoPhan.Trim().ToLower() == phongBan.ToLower());
//                }
//                else
//                {
//                    // Nếu Quản lý không có thông tin phòng ban trong Cookie -> Trả về trống để an toàn
//                    query = query.Where(f => false);
//                }
//            }
//            else
//            {
//                // NHÂN VIÊN bình thường: Chỉ thấy đơn của chính mình
//                query = query.Where(f => f.IdNguoiTao == userId);
//            }

//            // 4. Lấy dữ liệu và sắp xếp đơn mới nhất lên đầu
//            var danhSachDon = await query
//                .OrderByDescending(f => f.Id)
//                .AsNoTracking()
//                .ToListAsync();

//            return View(danhSachDon);
//        }

//        /// <summary>
//        /// POST: /FormHR/XuLyDon
//        /// Xử lý Duyệt hoặc Hủy đơn từ Quản lý/Admin qua AJAX
//        /// </summary>
//        [HttpPost("/FormHR/XuLyDon")]
//        public async Task<IActionResult> XuLyDon([FromBody] ApprovalRequest request)
//        {
//            // 1. Tìm đơn
//            var form = await _context.FormHrs.FindAsync(request.Id);
//            if (form == null) return Json(new { success = false, message = "Không tìm thấy đơn" });

//            // 2. Lấy thông tin người duyệt hiện tại từ Cookie
//            var userIdString = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
//            var userName = User.FindFirst(System.Security.Claims.ClaimTypes.Name)?.Value;

//            if (string.IsNullOrEmpty(userIdString))
//                return Json(new { success = false, message = "Phiên đăng nhập đã hết hạn" });

//            int currentUserId = int.Parse(userIdString);

//            // 3. Logic xử lý hành động
//            if (request.Action == "Duyet")
//            {
//                // Cập nhật thông tin Quản lý duyệt
//                form.IdNguoiDuyet = currentUserId;
//                form.TenNguoiDuyet = userName;
//                form.TimeNguoiDuyet = DateTime.Now;
//                // Nếu bạn dùng thêm cột TrangThai thì cập nhật ở đây
//            }
//            else if (request.Action == "Huy")
//            {
//                // Đánh dấu đơn đã hủy (Logic giữ nguyên: Thêm tiền tố [ĐÃ HỦY])
//                if (!string.IsNullOrEmpty(form.TenForm) && !form.TenForm.StartsWith("[ĐÃ HỦY]"))
//                {
//                    form.TenForm = "[ĐÃ HỦY] " + form.TenForm;
//                }
//                // Có thể lưu thêm ai là người hủy vào cột ghi chú nếu cần
//            }

//            // 4. Lưu và phản hồi
//            try
//            {
//                await _context.SaveChangesAsync();
//                return Json(new { success = true });
//            }
//            catch (Exception ex)
//            {
//                return Json(new { success = false, message = "Lỗi khi lưu dữ liệu: " + ex.Message });
//            }
//        }

//        #endregion

//        #region QUẢN LÝ PHÊ DUYỆT (Admin, All, Quản lý)

//        /// <summary>
//        /// GET: /FormHR/HoanTatDon
//        /// Hiển thị danh sách các đơn đã được duyệt hoặc cần Admin xác nhận hoàn tất.
//        /// </summary>
//        [HttpGet("/FormHR/HoanTatDon")]
//        public async Task<IActionResult> HoanTatDon()
//        {
//            // 1. Kiểm tra xác thực qua Cookie Authentication (CK)
//            if (!User.Identity.IsAuthenticated) return Redirect("/DonXetDuyet/DangNhap");

//            // 2. Lấy thông tin từ Claims (Id, Role, Phòng ban)
//            var userIdString = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
//            if (string.IsNullOrEmpty(userIdString)) return Redirect("/DonXetDuyet/DangNhap");

//            int userId = int.Parse(userIdString);

//            // Chuẩn hóa Role (Viết hoa toàn bộ để so sánh chuẩn)
//            var userRole = (User.FindFirst(System.Security.Claims.ClaimTypes.Role)?.Value
//                           ?? User.FindFirst("UserRole")?.Value
//                           ?? "").Trim().ToUpper();

//            // Lấy Phòng Ban (Kiểm tra cả 2 key phổ biến)
//            var phongBan = (User.FindFirst("PhongBan")?.Value
//                           ?? User.FindFirst("PhanBo")?.Value
//                           ?? "").Trim();

//            // Khởi tạo query ban đầu
//            IQueryable<FormHr> query = _context.FormHrs;

//            // 3. Xử lý logic phân quyền và điều kiện hiển thị đơn (KHÔNG BỎ BỚT NHÓM QUYỀN)
//            if (userRole == "ALL" || userRole == "ADMIN")
//            {
//                // QUYỀN ALL & ADMIN: Xem tất cả các đơn đã có Quản lý duyệt (không phân biệt bộ phận)
//                // Điều kiện: Đơn đã qua bước phê duyệt đầu tiên (IdNguoiDuyet có giá trị)
//                query = query.Where(f => f.IdNguoiDuyet != null
//                                      && f.TenNguoiDuyet != null
//                                      && f.TimeNguoiDuyet != null);
//            }
//            else if (userRole == "QUANLY")
//            {
//                // QUẢN LÝ: Chỉ xem đơn của bộ phận mình VÀ đã được duyệt
//                if (!string.IsNullOrEmpty(phongBan))
//                {
//                    query = query.Where(f => f.BoPhan != null
//                                          && f.BoPhan.Trim().ToLower() == phongBan.ToLower()
//                                          && f.IdNguoiDuyet != null);
//                }
//                else
//                {
//                    // Bảo mật: Không có thông tin phòng ban trong Cookie thì không thấy dữ liệu
//                    query = query.Where(f => false);
//                }
//            }
//            else if (userRole == "NHANVIEN")
//            {
//                // NHÂN VIÊN: Xem lại đơn của chính mình đã được duyệt thành công
//                query = query.Where(f => f.IdNguoiTao == userId
//                                      && f.IdNguoiDuyet != null);
//            }
//            else
//            {
//                // Các quyền khác như Bảo vệ hoặc khách không có quyền vào danh sách này
//                return Forbid();
//            }

//            // 4. Thực thi lấy dữ liệu, sắp xếp theo thời gian duyệt mới nhất để theo dõi
//            var danhSachDon = await query
//                .OrderByDescending(f => f.TimeNguoiDuyet)
//                .AsNoTracking()
//                .ToListAsync();

//            return View(danhSachDon);
//        }

//        /// <summary>
//        /// POST: /FormHR/XacNhanHoanThanh
//        /// Dùng cho nút xác nhận cuối cùng của Admin (Ghi vào cột IdAdmin, TenAdmin)
//        /// </summary>
//        [HttpPost("/FormHR/XacNhanHoanThanh")]
//        public async Task<IActionResult> XacNhanHoanThanh([FromBody] ApprovalRequest request)
//        {
//            // 1. Kiểm tra quyền qua CK: Chỉ cho phép Admin và All mới có quyền bấm nút cuối cùng
//            var userRole = (User.FindFirst(System.Security.Claims.ClaimTypes.Role)?.Value
//                           ?? User.FindFirst("UserRole")?.Value
//                           ?? "").Trim().ToUpper();

//            if (userRole != "ADMIN" && userRole != "ALL")
//            {
//                return Json(new
//                {
//                    success = false,
//                    message = "Bạn không có quyền thực hiện xác nhận hoàn thành (Chỉ dành cho ADMIN)."
//                });
//            }

//            // 2. Tìm đơn và kiểm tra tồn tại trong Database
//            var form = await _context.FormHrs.FindAsync(request.Id);
//            if (form == null) return Json(new { success = false, message = "Không tìm thấy đơn" });

//            // 3. Lấy thông tin Admin đang thực hiện từ Cookie Claims
//            var userIdString = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
//            var userName = User.FindFirst(System.Security.Claims.ClaimTypes.Name)?.Value;

//            if (string.IsNullOrEmpty(userIdString))
//                return Json(new { success = false, message = "Phiên đăng nhập hết hạn. Vui lòng đăng nhập lại." });

//            // 4. Ghi nhận thông tin phê duyệt cuối cùng vào các cột dành cho ADMIN
//            try
//            {
//                form.IdAdmin = int.Parse(userIdString);
//                form.TenAdmin = userName;
//                form.TimeAdmin = DateTime.Now;

//                // Cập nhật trạng thái chuỗi nếu hệ thống của bạn có cột này
//                form.TrangThai = "HoanThanh";

//                await _context.SaveChangesAsync();
//                return Json(new { success = true });
//            }
//            catch (Exception ex)
//            {
//                return Json(new { success = false, message = "Lỗi hệ thống: " + ex.Message });
//            }
//        }

//        /// <summary>
//        /// Class nhận dữ liệu từ AJAX (Giữ nguyên để không lỗi binding)
//        /// </summary>
//        public class ApprovalRequest
//        {
//            public int Id { get; set; }
//            public string Action { get; set; } // "Duyet" hoặc "Huy"
//        }

//        #endregion
//        #region DanhSachBaoVe (Dành cho bộ phận cổng kiểm soát)

//        /// <summary>
//        /// GET: /FormHR/DanhSachBaoVe
//        /// Hiển thị danh sách các đơn đã hoàn tất phê duyệt để bảo vệ kiểm soát người và hàng hóa ra vào.
//        /// </summary>
//        [HttpGet("/FormHR/DanhSachBaoVe")]
//        public async Task<IActionResult> DanhSachBaoVe()
//        {
//            // 1. Kiểm tra xác thực qua Cookie Authentication (CK)
//            if (!User.Identity.IsAuthenticated)
//            {
//                return Redirect("/DonXetDuyet/DangNhap");
//            }

//            // (Tùy chọn) Kiểm tra nếu bạn muốn chỉ duy nhất quyền "BaoVe" hoặc "Admin/All" mới được vào trang này
//            /*
//            var userRole = User.FindFirst(System.Security.Claims.ClaimTypes.Role)?.Value ?? "";
//            if (userRole != "BaoVe" && userRole != "Admin" && userRole != "All")
//            {
//                return Forbid();
//            }
//            */

//            // 2. Danh sách các loại đơn mà bảo vệ được phép xem (Giữ nguyên logic của bạn)
//            // Đơn 1: Xin ra ngoài | Đơn 2: Mang hàng hóa ra cổng
//            string[] allowedForms = { "CT_XinRaNgoai_1", "CT_MangHangHoaRaCong_2" };

//            // 3. Truy vấn dữ liệu với các điều kiện chặt chẽ
//            var model = await _context.FormHrs
//                .Include(f => f.HrXinRaNgoai1s)           // Nạp chi tiết đơn xin ra ngoài
//                .Include(f => f.HrMangHangHoaRaCong2s)    // Nạp chi tiết đơn mang hàng hóa
//                .Where(f => f.IdAdmin != null             // ĐIỀU KIỆN 1: Đã được Admin/HR phê duyệt cuối cùng
//                         && allowedForms.Contains(f.IdForm) // ĐIỀU KIỆN 2: Chỉ lấy đúng 2 loại đơn quy định
//                         && !(f.TenForm.Contains("[ĐÃ HỦY]"))) // ĐIỀU KIỆN 3: Không lấy các đơn đã bị quản lý hủy
//                .OrderByDescending(f => f.TimeAdmin)      // Ưu tiên đơn vừa duyệt xong lên đầu danh sách
//                .AsNoTracking()                           // Tối ưu hiệu năng cho trang danh sách
//                .ToListAsync();

//            // 4. Trả về View cho bảo vệ
//            return View(model);
//        }

//        #endregion

//        #region BÁO CÁO THỐNG KÊ FORM HR (Dành cho Admin/All)

//        /// <summary>
//        /// GET: /FormHR/BaoCaoThongKe
//        /// Thống kê số lượng đơn theo loại và hiển thị danh sách tổng hợp.
//        /// </summary>
//        [HttpGet("/FormHR/BaoCaoThongKe")]
//        public async Task<IActionResult> BaoCaoThongKe()
//        {
//            // 1. Kiểm tra xác thực và quyền hạn qua CK
//            if (!User.Identity.IsAuthenticated) return Redirect("/DonXetDuyet/DangNhap");

//            var userRole = User.FindFirst(System.Security.Claims.ClaimTypes.Role)?.Value ?? "";

//            // Chỉ Admin hoặc quyền All mới có thể xem báo cáo tổng thể
//            if (userRole != "Admin" && userRole != "All")
//            {
//                return Redirect("/");
//            }

//            // 2. Lấy toàn bộ danh sách đơn
//            var allForms = await _context.FormHrs.ToListAsync();

//            // 3. Chuẩn bị dữ liệu cho biểu đồ (Grouping)
//            var stats = allForms
//                .Where(f => !string.IsNullOrEmpty(f.IdForm)) // Loại bỏ đơn không có ID form
//                .GroupBy(f => f.IdForm)
//                .Select(g => new {
//                    Name = GetShortName(g.Key),
//                    Count = g.Count()
//                })
//                .OrderByDescending(x => x.Count)
//                .ToList();

//            // 4. Truyền dữ liệu sang View thông qua ViewBag để vẽ biểu đồ (Chart.js)
//            ViewBag.TypeLabels = stats.Select(s => s.Name).ToList();
//            ViewBag.TypeCounts = stats.Select(s => s.Count).ToList();

//            // Trả về view kèm danh sách đầy đủ để hiển thị bảng dữ liệu (DataTables)
//            return View(allForms);
//        }

//        /// <summary>
//        /// Hàm Helper: Chuyển đổi ID Form sang tên ngắn gọn để hiển thị trên biểu đồ
//        /// Bảo toàn và cập nhật đầy đủ 8 loại đơn.
//        /// </summary>
//        private string GetShortName(string id) => id switch
//        {
//            "CT_XinRaNgoai_1" => "Ra ngoài",
//            "CT_MangHangHoaRaCong_2" => "Hàng hóa",
//            "CT_XeCongTac_3" => "Xe công tác", // Sửa lại key cho khớp với phần đăng ký bạn đã viết
//            "CT_XeDaily_4" => "Xe Daily",
//            "CT_DonTiepKhac_5" => "Đón khách",
//            "CT_NhaThauQuaCong_6" => "Nhà thầu",
//            "CT_HoTroTienDienThoai_7" => "Tiền ĐT", // Bổ sung đơn 7
//            "CT_DoiCaLam_8" => "Đổi ca",           // Bổ sung đơn 8
//            _ => "Khác (" + id + ")"
//        };

//        #endregion

//    }
//}
