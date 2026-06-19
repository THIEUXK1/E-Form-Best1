using E_Form_Best.Context;
using E_Form_Best.Models.ITForm;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Claims;
using System.Text;
using System.Threading.Tasks;
using static E_Form_Best.Areas.ITForm.Controllers.ITFormController;

namespace E_Form_Best.Areas.QLCongViec.Controllers
{
    [Area("QLCongViec")]
    public class CongViecFormController : Controller
    {
        public ITFormContext _context;

        public CongViecFormController()
        {
            _context = new ITFormContext();
        }

        #region logo
        [Route("QLCongViec/Logo")]
        public IActionResult Logo()
        {
            return View();
        }
        #endregion

        #region CvCongViecOrder1

        [HttpGet("/FormCongViec/DonCvCongViecOrder")]
        public async Task<IActionResult> DonCvCongViecOrder()
        {
            // --- LẤY THÔNG TIN TỪ CLAIMS NGƯỜI DÙNG ĐANG ĐĂNG NHẬP ---
            var userIdClaim = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userIdClaim)) return Redirect("/DonXetDuyet/DangNhap");

            int userId = int.Parse(userIdClaim);
            var userName = User.Identity?.Name ?? "";
            var phongBan = User.FindFirst("PhongBan")?.Value ?? "";
            var userRole = User.FindFirst(System.Security.Claims.ClaimTypes.Role) ?? User.FindFirst("UserRole");
            var userEmail = User.FindFirst(System.Security.Claims.ClaimTypes.Email)?.Value ?? "";
            var tenCongTy = User.FindFirst("TenCongTy")?.Value ?? "";

            // --- XỬ LÝ LỌC DANH SÁCH NHÂN SỰ CÙNG BỘ PHẬN THEO LOGIC ĐĂNG NHẬP ---
            var currentUser = await _context.Users
                .Include(u => u.UserBoPhans)
                    .ThenInclude(ub => ub.IdBoPhanNavigation)
                .FirstOrDefaultAsync(u => u.IdNguoiDung == userId);

            List<User> danhSachNguoiCungBoPhan = new List<User>();

            if (currentUser != null)
            {
                // CƠ CHẾ 1: Lọc theo mô hình bộ phận mới (Bảng UserBoPhans) nếu có dữ liệu liên kết
                if (currentUser.UserBoPhans != null && currentUser.UserBoPhans.Any(ub => ub.IdBoPhanNavigation != null))
                {
                    // Lấy tập hợp dải ID bộ phận mà người dùng này trực thuộc
                    var listIdBoPhanCuaToi = currentUser.UserBoPhans
                        .Where(ub => ub.IdBoPhanNavigation != null)
                        .Select(ub => ub.IdBoPhan)
                        .ToList();

                    if (listIdBoPhanCuaToi.Any())
                    {
                        // Tìm kiếm tất cả nhân sự đang làm việc có chung bất kỳ ID bộ phận nào với User này
                        danhSachNguoiCungBoPhan = await _context.Users
                            .Include(u => u.UserBoPhans)
                            .Where(u => (u.TrangThai == "Đang làm" || u.TrangThai == "HoatDong" || u.TrangThai == null) &&
                                        u.UserBoPhans.Any(ub => listIdBoPhanCuaToi.Contains(ub.IdBoPhan)))
                            .OrderBy(u => u.HoTen)
                            .ToListAsync();
                    }
                    else
                    {
                        // Fallback nếu có dòng bộ phận mới nhưng dải ID trống -> Quay về lọc theo chuỗi văn bản phong_ban gốc
                        string phongBanThoCuaToi = currentUser.PhongBan?.Trim() ?? "";
                        danhSachNguoiCungBoPhan = await _context.Users
                            .Where(u => (u.TrangThai == "Đang làm" || u.TrangThai == "HoatDong" || u.TrangThai == null) &&
                                        u.PhongBan != null && u.PhongBan == phongBanThoCuaToi)
                            .OrderBy(u => u.HoTen)
                            .ToListAsync();
                    }
                }
                else
                {
                    // CƠ CHẾ 2 (DỰ PHÒNG CHUỖI THÔ): Nếu bảng trung gian trống -> Lọc theo chuỗi văn bản phong_ban gốc
                    string phongBanThoCuaToi = currentUser.PhongBan?.Trim() ?? "";

                    danhSachNguoiCungBoPhan = await _context.Users
                        .Where(u => (u.TrangThai == "Đang làm" || u.TrangThai == "HoatDong" || u.TrangThai == null) &&
                                    u.PhongBan != null && u.PhongBan == phongBanThoCuaToi)
                        .OrderBy(u => u.HoTen)
                        .ToListAsync();
                }
            }

            // Đẩy danh sách nhân sự cùng bộ phận đã được lọc sạch sang ViewBag phục vụ đa chọn trên đơn
            ViewBag.NguoiNhanViecList = danhSachNguoiCungBoPhan;

            var model = new FormCongViec
            {
                TenNguoiNv = userName,
                BoPhan = phongBan,
                ViTri = userRole?.Value ?? "",
                SoNhanVien = userEmail,
                TenCongTy = tenCongTy,
                Ngay = DateOnly.FromDateTime(DateTime.Now),
                IdNguoiTao = userId,
                TenNguoiTao = userName,
                TimeNguoiTao = DateTime.Now,
                TrangThai = "ChoDuyet",
                IdForm = "CV_CongViec_Order_1",
                TenForm = "Đơn giao việc chỉ định nhân sự"
            };

            return View(model);
        }

        [HttpPost("/FormCongViec/DonCvCongViecOrder")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> HoanTatDonChiDinh(FormCongViec form, [FromForm] CvCongViecOrder1 cvOrder, List<int> selectedNguoiLienQuanIds)
        {
            var userIdClaim = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userIdClaim))
            {
                return Json(new { success = false, message = "Phiên đăng nhập đã hết hạn." });
            }

            int userId = int.Parse(userIdClaim);
            var userName = User.Identity?.Name ?? "";
            var userEmail = User.FindFirst(System.Security.Claims.ClaimTypes.Email)?.Value ?? "";
            var phongBan = User.FindFirst("PhongBan")?.Value ?? "";
            var viTri = User.FindFirst(System.Security.Claims.ClaimTypes.Role)?.Value ?? User.FindFirst("UserRole")?.Value ?? "";
            var tenCongTy = User.FindFirst("TenCongTy")?.Value ?? "";

            if (selectedNguoiLienQuanIds == null || !selectedNguoiLienQuanIds.Any())
            {
                return Json(new { success = false, message = "Vui lòng chọn ít nhất một nhân sự để chỉ định thực hiện công việc này!" });
            }

            using (var transaction = await _context.Database.BeginTransactionAsync())
            {
                try
                {
                    var danhSachUserNhanViec = await _context.Users
                                                                .Where(u => selectedNguoiLienQuanIds.Contains(u.IdNguoiDung))
                                                                .ToListAsync();

                    // --- 1. LƯU THÔNG TIN HÀNH CHÍNH (FormCongViec) ---
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
                    form.IdForm = "CV_CongViec_Order_1";
                    form.TenForm = "Đơn giao việc chỉ định nhân sự";
                    form.Danhmuc = "Công việc chỉ định";

                    form.IdNguoiDuyet = 8;
                    form.TenNguoiDuyet = "Hệ thống E-form";
                    form.TimeNguoiDuyet = DateTime.Now;

                    _context.FormCongViecs.Add(form);
                    await _context.SaveChangesAsync();

                    // --- CẤU HÌNH ĐƯỜNG DẪN MẠNG LƯU TRỮ TRÊN FILESERVER (FIXED: Chuyển sang thư mục DonCongViec) ---
                    string networkPath = @"\\10.0.60.30\BPVN-Fileserver\Public\IT-Information Technology Dept\5.E-Form\DonCongViec";
                    if (!Directory.Exists(networkPath)) Directory.CreateDirectory(networkPath);

                    string safeName = RemoveSign4VietnameseString(userName).Replace(" ", "");
                    string timeStamp = DateTime.Now.ToString("ddMMyy_HHmmss");

                    // --- 2. LƯU CHI TIẾT NỘI DUNG CÔNG VIỆC (CvCongViecOrder1) & XỬ LÝ ẢNH ---
                    if (cvOrder != null)
                    {
                        cvOrder.IdFormCongViec = form.Id;

                        var anhFile = Request.Form.Files["Anh"];
                        if (anhFile != null && anhFile.Length > 0)
                        {
                            string imgExtension = Path.GetExtension(anhFile.FileName);
                            if (string.IsNullOrEmpty(imgExtension)) imgExtension = ".jpg";

                            string imgFileName = $"AnhCV_ID{form.Id}_{safeName}_{timeStamp}{imgExtension}";
                            string imgFullPath = Path.Combine(networkPath, imgFileName);

                            using (var imgStream = new FileStream(imgFullPath, FileMode.Create))
                            {
                                await anhFile.CopyToAsync(imgStream);
                            }

                            cvOrder.DuongDanAnh = imgFileName;
                            cvOrder.Anh = null;
                        }

                        _context.CvCongViecOrder1s.Add(cvOrder);
                    }

                    // --- 3. XỬ LÝ FILE ĐÍNH KÈM TÀI LIỆU ---
                    var uploadFile = Request.Form.Files["UploadFile"];
                    string fileLog = "Không có tệp đính kèm";

                    if (uploadFile != null && uploadFile.Length > 0)
                    {
                        string extension = Path.GetExtension(uploadFile.FileName);
                        string fileName = $"DonCV_ID{form.Id}_{safeName}_{timeStamp}{extension}";
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

                    // --- 4. GÁN NHÂN SỰ XỬ LÝ (FormCongViecNguoiLienQuan) ---
                    foreach (var idNguoiDung in selectedNguoiLienQuanIds)
                    {
                        var nguoiLienQuan = new FormCongViecNguoiLienQuan
                        {
                            IdFormCongViec = form.Id,
                            IdNguoiDung = idNguoiDung,
                            VaiTroLienQuan = "Người thực hiện",
                            NgayGan = DateTime.Now,
                            GhiChu = "Được giao việc trực tiếp hệ thống"
                        };
                        _context.FormCongViecNguoiLienQuans.Add(nguoiLienQuan);
                    }

                    // --- 5. GÌN GIỮ NHẬT KÝ TIẾN TRÌNH ---
                    string chuoiTenNhanVien = string.Join(", ", danhSachUserNhanViec.Select(x => x.HoTen));
                    string staffLog = $"Đã chỉ định xử lý: {(!string.IsNullOrEmpty(chuoiTenNhanVien) ? chuoiTenNhanVien : "N/A")}";
                    string deadlineLog = cvOrder?.ThoiHanHoanThanh.HasValue == true
                        ? $"Thời hạn: {cvOrder.ThoiHanHoanThanh.Value:dd/MM/yyyy HH:mm}"
                        : "Thời hạn: Không chỉ định";
                    string statusAnhLog = string.IsNullOrEmpty(cvOrder?.DuongDanAnh) ? "Không có hình ảnh" : $"Ảnh: {cvOrder.DuongDanAnh}";

                    var lichSu = new LichSuFormCongViec
                    {
                        IdFormCongViec = form.Id,
                        TieuDe = "Khởi tạo đơn công việc chỉ định",
                        Mota = $"[Cty: {tenCongTy}] Người giao: {userName}. {staffLog}. {deadlineLog}. {fileLog}. {statusAnhLog}",
                        Time = DateTime.Now,
                        IsRead = false,
                        TrangThaiAnHien = 1
                    };
                    _context.LichSuFormCongViecs.Add(lichSu);

                    await _context.SaveChangesAsync();
                    await transaction.CommitAsync();

                    return Json(new { success = true, message = "Phát hành đơn và bàn giao chỉ định công việc thành công!" });
                }
                catch (Exception ex)
                {
                    await transaction.RollbackAsync();
                    return Json(new { success = false, message = "Đã xảy ra lỗi xử lý: " + ex.Message });
                }
            }
        }

        private string RemoveSign4VietnameseString(string str)
        {
            if (string.IsNullOrEmpty(str)) return "";
            string[] strFrom = { "á", "à", "ả", "ã", "ạ", "ă", "ắ", "ằ", "ẳ", "ẵ", "ặ", "â", "ấ", "ầ", "ẩ", "ẫ", "ậ", "đ", "é", "è", "ẻ", "ẽ", "ẹ", "ê", "ế", "ề", "ể", "ễ", "ệ", "í", "ì", "ỉ", "ĩ", "ị", "ó", "ò", "ỏ", "õ", "ọ", "ô", "ố", "ồ", "ổ", "ỗ", "ộ", "ơ", "ớ", "ờ", "ở", "ỡ", "ợ", "ú", "ù", "ủ", "ũ", "ụ", "ư", "ứ", "ừ", "ử", "ữ", "ự", "ý", "ỳ", "ỷ", "ỹ", "ỵ", "Á", "À", "Ả", "Ã", "Ạ", "Ă", "Ắ", "Ằ", "Ẳ", "Ẵ", "Ặ", "Â", "Ấ", "Ầ", "Ẩ", "Ẫ", "Ậ", "Đ", "É", "È", "Ẻ", "Ẽ", "Ẹ", "Ê", "Ế", "Ề", "Ể", "Ễ", "Ệ", "Í", "ì", "Ỉ", "Ĩ", "Ị", "Ó", "Ò", "Ỏ", "Õ", "Ọ", "Ô", "Ố", "Ồ", "Ổ", "Ỗ", "Ộ", "Ơ", "Ớ", "Ờ", "Ở", "Ỡ", "Ợ", "Ú", "Ù", "Ủ", "Ũ", "Ụ", "Ư", "Ứ", "Ừ", "Ử", "Ữ", "Ự", "Ý", "Ỳ", "Ỷ", "Ỹ", "Ỵ" };
            string[] strTo = { "a", "a", "a", "a", "a", "a", "a", "a", "a", "a", "a", "a", "a", "a", "a", "a", "a", "d", "e", "e", "e", "e", "e", "e", "e", "e", "e", "e", "e", "i", "i", "i", "i", "i", "o", "o", "o", "o", "o", "o", "o", "o", "o", "o", "o", "o", "o", "o", "o", "o", "o", "u", "u", "u", "u", "u", "u", "u", "u", "u", "u", "u", "y", "y", "y", "y", "y", "A", "A", "A", "A", "A", "A", "A", "A", "A", "A", "A", "A", "A", "a", "A", "A", "A", "Đ", "E", "E", "E", "E", "E", "E", "E", "E", "E", "E", "E", "I", "I", "I", "I", "I", "O", "O", "O", "O", "O", "O", "O", "O", "O", "O", "O", "O", "O", "O", "O", "O", "O", "U", "U", "U", "U", "U", "U", "U", "U", "U", "U", "U", "Y", "Y", "Y", "Y", "Y" };
            for (int i = 0; i < strFrom.Length; i++)
            {
                str = str.Replace(strFrom[i], strTo[i]);
            }
            return str;
        }

        #endregion

        #region CHI TIẾT ĐƠN FORM CÔNG VIỆC (TẤT CẢ LOẠI ĐƠN)

        [HttpGet("/FormCongViec/ChiTiet/{id}")]
        public async Task<IActionResult> ChiTiet(int id)
        {
            // 1. Kiểm tra đăng nhập
            var userIdStr = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userIdStr))
            {
                return Redirect("/DonXetDuyet/DangNhap");
            }

            int userId = int.Parse(userIdStr);

            // Lấy các thông tin từ Claims để kiểm tra quyền hạn điều phối đơn công việc
            var userEmail = User.FindFirst(System.Security.Claims.ClaimTypes.Email)?.Value ?? "";
            var tenCongTyUser = User.FindFirst("TenCongTy")?.Value?.Trim() ?? "";
            var phongBanDon = User.FindFirst("PhongBan")?.Value ?? "";
            var listBoPhan = User.FindFirst("TenBoPhan")?.Value ?? ""; // Chuỗi chứa nhiều bộ phận quản lý phụ

            // 2. Lấy dữ liệu đơn công việc kết hợp nạp các bảng liên kết thực thể quan hệ
            var don = await _context.FormCongViecs
                .Include(f => f.CvCongViecOrder1s)
                .Include(f => f.FormCongViecNguoiLienQuans)
                    .ThenInclude(ct => ct.IdNguoiDungNavigation)
                .Include(f => f.LichSuFormCongViecs)
                .Include(f => f.BinhLuanFormCongViecs)
                .FirstOrDefaultAsync(m => m.Id == id);

            if (don == null)
            {
                TempData["Error"] = "⚠️ Không tìm thấy đơn yêu cầu công việc!";
                return Redirect("/FormCongViec/DonCho");
            }

            // Nạp danh sách đánh giá từ bảng độc lập DanhGiaFormCongViec
            ViewBag.DanhGiaList = await _context.DanhGiaFormCongViecs
                .Where(dg => dg.IdFormCongViec == id)
                .AsNoTracking()
                .ToListAsync();

            // 3. KIỂM TRA QUYỀN XEM (LOGIC CỘNG DỒN ĐỒNG BỘ BAN ĐẦU)
            bool isAllowed = false;

            // Điều kiện 1: Là người tạo đơn công việc này
            if (don.IdNguoiTao == userId)
            {
                isAllowed = true;
            }
            // Điều kiện 2: Có quyền cao nhất hệ thống (AdminCV, AdminIT hoặc All)
            else if (User.IsInRole("AdminCV") || User.IsInRole("AdminIT") || User.IsInRole("All"))
            {
                isAllowed = true;
            }
            // Điều kiện 3: Có quyền Quản lý duyệt đơn phòng ban công việc (QuanLyDuyetDonCV)
            else if (User.IsInRole("QuanLyDuyetDonCV") || User.IsInRole("QuanLyDuyetDonIT"))
            {
                bool isSameCompany = string.Equals(don.TenCongTy?.Trim(), tenCongTyUser, StringComparison.OrdinalIgnoreCase);
                bool isSameDepartment = false;

                if (!string.IsNullOrEmpty(don.BoPhan))
                {
                    if (!string.IsNullOrEmpty(listBoPhan))
                    {
                        var arrBoPhan = listBoPhan.Split(',').Select(x => x.Trim());
                        isSameDepartment = arrBoPhan.Contains(don.BoPhan.Trim(), StringComparer.OrdinalIgnoreCase);
                    }

                    if (!isSameDepartment)
                    {
                        isSameDepartment = string.Equals(don.BoPhan.Trim(), phongBanDon.Trim(), StringComparison.OrdinalIgnoreCase);
                    }
                }

                if (isSameCompany && isSameDepartment)
                {
                    isAllowed = true;
                }
            }

            if (!isAllowed)
            {
                return Forbid();
            }

            // 4. Xử lý sắp xếp dữ liệu dòng thời gian hiển thị lịch sử đơn
            if (don.LichSuFormCongViecs != null)
            {
                don.LichSuFormCongViecs = don.LichSuFormCongViecs.OrderByDescending(x => x.Time).ToList();
            }

            // =========================================================================
            // CẬP NHẬT MỚI: LỌC DANH SÁCH GÁN VIỆC CHỈ HIỂN THỊ NGƯỜI CÙNG BỘ PHẬN VỚI BẠN
            // =========================================================================
            if (User.IsInRole("AdminCV") || User.IsInRole("AdminIT") || User.IsInRole("All"))
            {
                var currentUser = await _context.Users
                    .Include(u => u.UserBoPhans)
                        .ThenInclude(ub => ub.IdBoPhanNavigation)
                    .FirstOrDefaultAsync(u => u.IdNguoiDung == userId);

                List<User> danhSachNguoiCungBoPhan = new List<User>();

                if (currentUser != null)
                {
                    // CƠ CHẾ 1: Kiểm tra mô hình bộ phận mới (Bảng UserBoPhans)
                    if (currentUser.UserBoPhans != null && currentUser.UserBoPhans.Any(ub => ub.IdBoPhanNavigation != null))
                    {
                        var listIdBoPhanCuaToi = currentUser.UserBoPhans
                            .Where(ub => ub.IdBoPhanNavigation != null)
                            .Select(ub => ub.IdBoPhan)
                            .ToList();

                        if (listIdBoPhanCuaToi.Any())
                        {
                            danhSachNguoiCungBoPhan = await _context.Users
                                .Include(u => u.UserBoPhans)
                                .Where(u => (u.TrangThai == "Đang làm" || u.TrangThai == "HoatDong" || u.TrangThai == null) &&
                                            u.UserBoPhans.Any(ub => listIdBoPhanCuaToi.Contains(ub.IdBoPhan)))
                                .OrderBy(u => u.HoTen)
                                .ToListAsync();
                        }
                        else
                        {
                            string phongBanThoCuaToi = currentUser.PhongBan?.Trim() ?? "";
                            danhSachNguoiCungBoPhan = await _context.Users
                                .Where(u => (u.TrangThai == "Đang làm" || u.TrangThai == "HoatDong" || u.TrangThai == null) &&
                                            u.PhongBan != null && u.PhongBan == phongBanThoCuaToi)
                                .OrderBy(u => u.HoTen)
                                .ToListAsync();
                        }
                    }
                    else
                    {
                        // CƠ CHẾ 2 (DỰ PHÒNG CHUỖI THÔ): Nếu bảng trung gian trống -> Lọc theo chuỗi văn bản phong_ban gốc của DB
                        string phongBanThoCuaToi = currentUser.PhongBan?.Trim() ?? "";

                        danhSachNguoiCungBoPhan = await _context.Users
                            .Where(u => (u.TrangThai == "Đang làm" || u.TrangThai == "HoatDong" || u.TrangThai == null) &&
                                        u.PhongBan != null && u.PhongBan == phongBanThoCuaToi)
                            .OrderBy(u => u.HoTen)
                            .ToListAsync();
                    }
                }

                ViewBag.ListNguoiDungHeThong = danhSachNguoiCungBoPhan;
            }

            ViewBag.CurrentUserId = userId;
            ViewBag.UserEmail = userEmail;

            return View(don);
        }

        // --- ACTION DOWNLOAD / XEM FILE ĐÍNH KÈM ĐƠN CÔNG VIỆC ---
        [HttpGet("/FormCongViec/DownloadFile/{fileName}")]
        public async Task<IActionResult> DownloadFile(string fileName)
        {
            if (string.IsNullOrEmpty(fileName)) return NotFound();

            string networkPath = @"\\10.0.60.30\BPVN-Fileserver\Public\IT-Information Technology Dept\5.E-Form\DonCongViec";
            string fullPath = Path.Combine(networkPath, fileName);

            if (!System.IO.File.Exists(fullPath))
                return NotFound("Tệp tin tài liệu công việc không tồn tại.");

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

        // --- ACTION CHỈ ĐỊNH / THAY ĐỔI / GÁN THÊM NHÂN SỰ XỬ LÝ ĐƠN CÔNG VIỆC ---
        [HttpPost("/FormCongViec/ThemNguoiHoTro")]
        public async Task<IActionResult> ThemNguoiHoTro([FromBody] System.Text.Json.JsonElement data)
        {
            var roles = User.FindAll(System.Security.Claims.ClaimTypes.Role).Select(r => r.Value).ToList();
            if (!roles.Any(r => r == "AdminCV" || r == "AdminIT" || r == "All"))
            {
                return Json(new { success = false, message = "Bạn không có đặc quyền để điều phối nhân sự trên phiếu này!" });
            }

            try
            {
                int idForm = data.GetProperty("idFormIt").GetInt32();
                int idNguoiDungMoi = int.Parse(data.GetProperty("maNv").GetString() ?? "0");

                var targetUser = await _context.Users.FirstOrDefaultAsync(x => x.IdNguoiDung == idNguoiDungMoi);
                if (targetUser == null) return Json(new { success = false, message = "Nhân sự được chỉ định không tồn tại hệ thống!" });

                // Kiểm tra xem nhân sự này đã có trong danh sách xử lý đơn này chưa
                var banGhiTrung = await _context.FormCongViecNguoiLienQuans
                    .FirstOrDefaultAsync(x => x.IdFormCongViec == idForm && x.IdNguoiDung == idNguoiDungMoi);

                if (banGhiTrung != null)
                {
                    return Json(new { success = false, message = $"Nhân viên {targetUser.HoTen} hiện tại đã nằm trong danh sách xử lý đơn này!" });
                }

                var ctMoi = new FormCongViecNguoiLienQuan
                {
                    IdFormCongViec = idForm,
                    IdNguoiDung = idNguoiDungMoi,
                    VaiTroLienQuan = "Người thực hiện",
                    NgayGan = DateTime.Now,
                    GhiChu = $"Được điều phối bổ sung bởi {(User.Identity?.Name ?? "Admin")}"
                };
                _context.FormCongViecNguoiLienQuans.Add(ctMoi);

                _context.LichSuFormCongViecs.Add(new LichSuFormCongViec
                {
                    IdFormCongViec = idForm,
                    TieuDe = "ĐIỀU PHỐI BỔ SUNG NHÂN SỰ",
                    Mota = $"{(User.Identity?.Name ?? "Hệ thống")} đã điều phối gán thêm nhân sự xử lý: {targetUser.HoTen}.",
                    Time = DateTime.Now,
                    IsRead = false,
                    TrangThaiAnHien = 1
                });

                await _context.SaveChangesAsync();
                return Json(new { success = true, message = "Bổ sung gán nhân sự điều phối công việc thành công!" });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = "Lỗi gán nhân sự: " + ex.Message });
            }
        }

        [HttpPost("/FormCongViec/XuLyDon")]
        public async Task<IActionResult> XuLyDon([FromBody] System.Text.Json.JsonElement body)
        {
            int id = body.GetProperty("id").GetInt32();
            string action = body.GetProperty("action").GetString() ?? "";
            string reason = body.TryGetProperty("reason", out var r) ? r.GetString() ?? "" : "";

            var don = await _context.FormCongViecs.FirstOrDefaultAsync(x => x.Id == id);
            if (don == null) return Json(new { success = false, message = "Đơn không tồn tại" });

            var userName = User.Identity?.Name ?? "Quản lý";

            if (action == "Duyet")
            {
                don.TrangThai = "DaDuyet";
                don.IdNguoiDuyet = int.Parse(User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)!.Value);
                don.TenNguoiDuyet = userName;
                don.TimeNguoiDuyet = DateTime.Now;
            }
            else if (action == "Huy")
            {
                don.TrangThai = "Huy";
                don.TenForm += " [ĐÃ HỦY]";
            }

            _context.LichSuFormCongViecs.Add(new LichSuFormCongViec
            {
                IdFormCongViec = id,
                TieuDe = action == "Duyet" ? "QUẢN LÝ PHÊ DUYỆT PHIẾU" : "YÊU CẦU HỦY PHIẾU",
                Mota = $"{userName} đã thực hiện hành động này. {(!string.IsNullOrEmpty(reason) ? "Lý do: " + reason : "")}",
                Time = DateTime.Now,
                IsRead = false,
                TrangThaiAnHien = 1
            });

            await _context.SaveChangesAsync();
            return Json(new { success = true, message = "Xử lý phiếu công việc thành công!" });
        }

        [HttpPost("/FormCongViec/XacNhanHoanThanh")]
        public async Task<IActionResult> XacNhanHoanThanh([FromBody] System.Text.Json.JsonElement body)
        {
            int id = body.GetProperty("id").GetInt32();
            var don = await _context.FormCongViecs.FirstOrDefaultAsync(x => x.Id == id);
            if (don == null) return Json(new { success = false });

            don.TrangThai = "HoanTat";
            don.IdAdmin = int.Parse(User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)!.Value);
            don.TenAdmin = User.Identity?.Name ?? "Admin";
            don.TimeAdmin = DateTime.Now;

            _context.LichSuFormCongViecs.Add(new LichSuFormCongViec
            {
                IdFormCongViec = id,
                TieuDe = "XÁC NHẬN HOÀN TẤT CÔNG VIỆC",
                Mota = $"Ban quản trị ghi nhận công việc đã được xử lý hoàn thành triệt để.",
                Time = DateTime.Now,
                IsRead = false,
                TrangThaiAnHien = 1
            });

            await _context.SaveChangesAsync();
            return Json(new { success = true });
        }

        [HttpPost("/FormCongViec/XacNhanChuaHoanThanh")]
        public async Task<IActionResult> XacNhanChuaHoanThanh([FromBody] System.Text.Json.JsonElement body)
        {
            int id = body.GetProperty("id").GetInt32();
            string reason = body.GetProperty("reason").GetString() ?? "";

            var don = await _context.FormCongViecs.FirstOrDefaultAsync(x => x.Id == id);
            if (don == null) return Json(new { success = false });

            don.TrangThai = "DaDuyet"; // Trả về trạng thái chờ xử lý lại
            don.IdAdmin = null;
            don.TenAdmin = null;
            don.TimeAdmin = null;

            _context.LichSuFormCongViecs.Add(new LichSuFormCongViec
            {
                IdFormCongViec = id,
                TieuDe = "YÊU CẦU LÀM LẠI / CHƯA XONG",
                Mota = $"Người tạo đơn không đồng ý kết quả hoàn tất. Lý do phản hồi: {reason}",
                Time = DateTime.Now,
                IsRead = false,
                TrangThaiAnHien = 1
            });

            await _context.SaveChangesAsync();
            return Json(new { success = true });
        }

        [HttpPost("/FormCongViec/DanhGiaFormItsDon")]
        public async Task<IActionResult> DanhGiaFormItsDon([FromBody] System.Text.Json.JsonElement body)
        {
            int id = body.GetProperty("id").GetInt32();
            int mucDo = body.GetProperty("mucDo").GetInt32();
            string nhanXet = body.GetProperty("nhanXet").GetString() ?? "";

            var dg = new DanhGiaFormCongViec
            {
                IdFormCongViec = id,
                IdNguoiDanhGia = int.Parse(User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)!.Value),
                TenNguoiDanhGia = User.Identity?.Name ?? "User",
                TimeNguoiDanhGia = DateTime.Now,
                MucDo = mucDo
            };
            _context.DanhGiaFormCongViecs.Add(dg);

            _context.LichSuFormCongViecs.Add(new LichSuFormCongViec
            {
                IdFormCongViec = id,
                TieuDe = "ĐÁNH GIÁ DỊCH VỤ",
                Mota = $"Người tạo phản hồi mức độ hài lòng: {mucDo} sao. Ghi chú nhận xét: {nhanXet}",
                Time = DateTime.Now,
                IsRead = false,
                TrangThaiAnHien = 1
            });

            await _context.SaveChangesAsync();
            return Json(new { success = true, message = "Cảm ơn bạn đã gửi đánh giá dịch vụ công việc!" });
        }

        // --- CÁC ENDPOINT AJAX PHỤ TRỢ: LẤY / THÊM / XÓA BÌNH LUẬN TRONG PHIẾU CÔNG VIỆC ---
        [HttpGet("/FormCongViec/LayBinhLuan/{idForm}")]
        public async Task<IActionResult> LayBinhLuan(int idForm, int skip = 0, int take = 20)
        {
            try
            {
                var binhLuans = await _context.BinhLuanFormCongViecs
                    .Where(bl => bl.IdForm == idForm && bl.TrangThai == "Active")
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

                var resultData = binhLuans.OrderBy(bl => bl.thoiGian).ToList();
                return Json(new { success = true, data = resultData });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = ex.Message });
            }
        }

        [HttpPost("/FormCongViec/ThemBinhLuan")]
        public async Task<IActionResult> ThemBinhLuan()
        {
            try
            {
                if (!int.TryParse(Request.Form["idForm"], out int idForm))
                    return Json(new { success = false, message = "ID biểu mẫu không hợp lệ." });

                var noiDung = Request.Form["noiDung"].ToString();
                var file = Request.Form.Files.GetFile("file");

                var userIdStr = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                if (string.IsNullOrEmpty(userIdStr))
                    return Json(new { success = false, message = "Phiên đăng nhập đã hết hạn!" });

                int currentUserId = int.Parse(userIdStr);
                var userName = User.Identity?.Name ?? "Unknown";
                var userMa = User.FindFirst(ClaimTypes.Email)?.Value ?? "";
                var userPhongBan = User.FindFirst("PhongBan")?.Value ?? "";
                var userTenCongTy = User.FindFirst("TenCongTy")?.Value ?? "";

                var formCongViec = await _context.FormCongViecs.FindAsync(idForm);
                if (formCongViec == null)
                    return Json(new { success = false, message = "Không tìm thấy dữ liệu phiếu công việc." });

                string? fileName = null;

                if (file != null && file.Length > 0)
                {
                    if (file.Length > 50 * 1024 * 1024)
                        return Json(new { success = false, message = "Tài liệu đính kèm thảo luận tối đa 50MB." });

                    string networkPath = @"\\10.0.60.30\BPVN-Fileserver\Public\IT-Information Technology Dept\5.E-Form\DonCongViec";
                    fileName = $"{DateTime.Now:yyyyMMdd_HHmmss}_{Guid.NewGuid().ToString().Substring(0, 8)}_{Path.GetFileName(file.FileName)}";
                    string fullPath = Path.Combine(networkPath, fileName);

                    using (var stream = new FileStream(fullPath, FileMode.Create))
                    {
                        await file.CopyToAsync(stream);
                    }
                }

                if (string.IsNullOrWhiteSpace(noiDung) && fileName == null)
                    return Json(new { success = false, message = "Vui lòng nhập nội dung trao đổi hoặc đính kèm tài liệu!" });

                var binhLuan = new BinhLuanFormCongViec
                {
                    IdForm = idForm,
                    NoiDung = noiDung?.Trim(),
                    IdNguoiBinhLuan = currentUserId,
                    TenNguoiBinhLuan = userName,
                    Ma = userMa,
                    PhongBan = userPhongBan,
                    TenCongTy = userTenCongTy,
                    ThoiGian = DateTime.Now,
                    TrangThai = "Active",
                    FileDinhKem = fileName
                };

                _context.BinhLuanFormCongViecs.Add(binhLuan);

                string moTaPreview = string.IsNullOrWhiteSpace(noiDung) ? "[Tập tin đính kèm]" :
                                     (noiDung.Length > 50 ? noiDung.Substring(0, 50) + "..." : noiDung);

                var lichSu = new LichSuFormCongViec
                {
                    IdFormCongViec = idForm,
                    TieuDe = "THẢO LUẬN MỚI",
                    Mota = $"👤 {userName} ({userMa})\n🏢 {userPhongBan} - {userTenCongTy}\n💬 {moTaPreview}\n{(fileName != null ? "📎 Có đính kèm file tài liệu" : "")}",
                    Time = DateTime.Now,
                    IsRead = false,
                    TrangThaiAnHien = 1
                };
                _context.LichSuFormCongViecs.Add(lichSu);

                await _context.SaveChangesAsync();

                return Json(new
                {
                    success = true,
                    data = new
                    {
                        binhLuan.Id,
                        binhLuan.NoiDung,
                        binhLuan.TenNguoiBinhLuan,
                        idNguoiBinhLuan = binhLuan.IdNguoiBinhLuan.ToString(),
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
                return Json(new { success = false, message = "Lỗi hệ thống lưu thảo luận: " + ex.Message });
            }
        }

        [HttpPost("/FormCongViec/XoaBinhLuan")]
        public async Task<IActionResult> XoaBinhLuan([FromBody] XoaBinhLuanRequest request)
        {
            try
            {
                if (request == null || request.id <= 0)
                    return Json(new { success = false, message = "Dữ liệu yêu cầu xóa không hợp lệ" });

                var binhLuan = await _context.BinhLuanFormCongViecs.FindAsync(request.id);
                if (binhLuan == null)
                    return Json(new { success = false, message = "Bình luận thảo luận không tồn tại." });

                var userIdStr = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
                var userName = User.Identity?.Name ?? "Unknown";
                var userRole = User.FindFirst("UserRole")?.Value ?? "";

                double soGioDaTroiQua = (DateTime.Now - binhLuan.ThoiGian)?.TotalHours ?? 999;
                bool hetHanXoa = soGioDaTroiQua > 2;

                bool isOwner = userIdStr != null && binhLuan.IdNguoiBinhLuan == int.Parse(userIdStr);
                bool isAdmin = userRole == "Admin" || userRole == "All" || userRole == "AdminCV" || userRole == "AdminIT";

                if (!isAdmin)
                {
                    if (!isOwner)
                        return Json(new { success = false, message = "Bạn không có đặc quyền xóa dòng thảo luận của nhân sự khác!" });

                    if (hetHanXoa)
                        return Json(new { success = false, message = "Đã vượt quá giới hạn thời gian 2 giờ quy định, bạn không thể xóa." });
                }

                if (!string.IsNullOrEmpty(binhLuan.FileDinhKem))
                {
                    string networkPath = @"\\10.0.60.30\BPVN-Fileserver\Public\IT-Information Technology Dept\5.E-Form\DonCongViec";
                    string fullPath = Path.Combine(networkPath, binhLuan.FileDinhKem);

                    try
                    {
                        if (System.IO.File.Exists(fullPath)) System.IO.File.Delete(fullPath);
                    }
                    catch (Exception fileEx)
                    {
                        Console.WriteLine($"Lỗi xóa file đính kèm: {fileEx.Message}");
                    }
                }

                int idFormTam = binhLuan.IdForm ?? 0;
                string textNguoiBiXoa = binhLuan.TenNguoiBinhLuan ?? "Nhân viên";

                _context.BinhLuanFormCongViecs.Remove(binhLuan);
                await _context.SaveChangesAsync();

                if (idFormTam > 0)
                {
                    var lichSu = new LichSuFormCongViec
                    {
                        IdFormCongViec = idFormTam,
                        TieuDe = "XÓA BÌNH LUẬN THẢO LUẬN",
                        Mota = $"{userName} đã xóa dòng thảo luận của nhân sự: {textNguoiBiXoa}",
                        Time = DateTime.Now,
                        IsRead = false,
                        TrangThaiAnHien = 1
                    };
                    _context.LichSuFormCongViecs.Add(lichSu);
                    await _context.SaveChangesAsync();
                }

                return Json(new { success = true, message = "Đã thực hiện gỡ bỏ bình luận thảo luận thành công!" });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = "Lỗi hệ thống: " + ex.Message });
            }
        }

        // Request DTO cho việc xóa bình luận
        public class XoaBinhLuanRequest
        {
            public int id { get; set; }
        }

        #endregion

        #region Xuất file Excel, Word, PDF và Bình luận cho hệ thống Công việc

        // ============================================================
        // ACTION XUẤT BIỂU MẪU CÔNG VIỆC (EXCEL, WORD, PDF)
        // ============================================================

        [HttpGet("/FormCongViec/ExportExcel/{id}")]
        public async Task<IActionResult> ExportExcel(int id)
        {
            var don = await _context.FormCongViecs
                .Include(f => f.CvCongViecOrder1s)
                .FirstOrDefaultAsync(m => m.Id == id);

            if (don == null || don.IdAdmin == null)
                return NotFound("Đơn công việc chưa hoàn tất xử lý hoặc không tồn tại trên hệ thống.");

            using (var workbook = new ClosedXML.Excel.XLWorkbook())
            {
                var worksheet = workbook.Worksheets.Add("ChiTietDonCongViec");

                // Header chung của biểu mẫu theo thương hiệu xanh lá tươi mới
                worksheet.Cell(1, 1).Value = "HỆ THỐNG E-WORKPORTAL - BAN ĐIỀU PHỐI NHIỆM VỤ - BEST PACIFIC";
                worksheet.Range("A1:E1").Merge().Style.Font.SetBold().Font.FontSize = 14;
                worksheet.Range("A1:E1").Style.Alignment.SetHorizontal(ClosedXML.Excel.XLAlignmentHorizontalValues.Center);

                worksheet.Cell(3, 1).Value = "Mã Đơn:"; worksheet.Cell(3, 2).Value = don.Id;
                worksheet.Cell(4, 1).Value = "Tên Form:"; worksheet.Cell(4, 2).Value = don.TenForm;
                worksheet.Cell(5, 1).Value = "Mã NV:"; worksheet.Cell(5, 2).Value = don.SoNhanVien;
                worksheet.Cell(6, 1).Value = "Họ Tên:"; worksheet.Cell(6, 2).Value = don.TenNguoiNv;
                worksheet.Cell(7, 1).Value = "Bộ Phận:"; worksheet.Cell(7, 2).Value = don.BoPhan;
                worksheet.Cell(8, 1).Value = "Ngày Tạo:"; worksheet.Cell(8, 2).Value = don.TimeNguoiTao?.ToString("dd/MM/yyyy HH:mm");
                worksheet.Cell(9, 1).Value = "Trạng Thái:"; worksheet.Cell(9, 2).Value = "CÔNG VIỆC HOÀN TẤT";

                var rangeChung = worksheet.Range("A3:B9");
                rangeChung.Style.Border.SetOutsideBorder(ClosedXML.Excel.XLBorderStyleValues.Thin);
                rangeChung.Style.Border.SetInsideBorder(ClosedXML.Excel.XLBorderStyleValues.Thin);
                worksheet.Range("A3:A9").Style.Font.SetBold().Fill.SetBackgroundColor(ClosedXML.Excel.XLColor.LightGray);

                int currentRow = 11;

                // Nạp dữ liệu chi tiết của đơn Công việc chỉ định duy nhất
                if (don.CvCongViecOrder1s.Any())
                {
                    var ct = don.CvCongViecOrder1s.First();
                    worksheet.Cell(currentRow, 1).Value = "I. CHI TIẾT ĐƠN: CÔNG VIỆC CHỈ ĐỊNH ĐIỀU PHỐI (FORM 1)";
                    worksheet.Range(currentRow, 1, currentRow, 2).Merge().Style.Font.SetBold().Fill.SetBackgroundColor(ClosedXML.Excel.XLColor.AppleGreen);
                    currentRow++;
                    worksheet.Cell(currentRow, 1).Value = "Tiêu đề công việc / Tạng mục:"; worksheet.Cell(currentRow, 2).Value = ct.Ten; currentRow++;
                    worksheet.Cell(currentRow, 1).Value = "Mức độ ưu tiên vận hành:"; worksheet.Cell(currentRow, 2).Value = ct.MucDoUuTien; currentRow++;
                    worksheet.Cell(currentRow, 1).Value = "Thời hạn hoàn thành (Deadline):"; worksheet.Cell(currentRow, 2).Value = ct.ThoiHanHoanThanh?.ToString("dd/MM/yyyy HH:mm") ?? "Không giới hạn"; currentRow++;
                    worksheet.Cell(currentRow, 1).Value = "Nội dung yêu cầu cụ thể:"; worksheet.Cell(currentRow, 2).Value = ct.GhiChu; currentRow++;
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
                    return File(stream.ToArray(), "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", $"Don_EWork_ChiDinh_{id}.xlsx");
                }
            }
        }

        [HttpGet("/FormCongViec/ExportWord/{id}")]
        public async Task<IActionResult> ExportWord(int id)
        {
            var don = await _context.FormCongViecs
                .Include(f => f.CvCongViecOrder1s)
                .FirstOrDefaultAsync(m => m.Id == id);

            if (don == null || don.IdAdmin == null)
                return NotFound("Đơn chưa hoàn tất xử lý hoặc không tồn tại.");

            string htmlContent = BuildHtmlContentCV(don, isForWord: true);

            byte[] byteArray = System.Text.Encoding.UTF8.GetBytes(htmlContent);
            return File(byteArray, "application/msword", $"Don_EWork_ChiDinh_{id}.doc");
        }

        [HttpGet("/FormCongViec/ExportPDF/{id}")]
        public async Task<IActionResult> ExportPDF(int id)
        {
            var don = await _context.FormCongViecs
                .Include(f => f.CvCongViecOrder1s)
                .FirstOrDefaultAsync(m => m.Id == id);

            if (don == null || don.IdAdmin == null)
                return NotFound("Đơn chưa hoàn tất xử lý hoặc không tồn tại.");

            string htmlContent = BuildHtmlContentCV(don, isForWord: false);
            return Content(htmlContent, "text/html", System.Text.Encoding.UTF8);
        }

        // ============================================================
        // HÀM HỖ TRỢ BUILD HTML BIÊN BẢN IN ẤN VÀ CHỮ KÝ ĐIỆN TỬ MÀU XANH LÁ CỎ NON
        // ============================================================
        private string BuildHtmlContentCV(FormCongViec don, bool isForWord = false)
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
                .section-title { font-size: 14pt; font-weight: bold; margin-top: 25px; margin-bottom: 10px; text-transform: uppercase; border-bottom: 2px solid #65a30d; padding-bottom: 5px; }
                .data-table { width: 100%; border-collapse: collapse; margin-bottom: 20px; }
                .data-table th, .data-table td { border: 1px solid #000; padding: 8px 12px; font-size: 12pt; vertical-align: top; }
                .data-table th { background-color: #f7fee7; font-weight: bold; text-align: left; width: 35%; }
                .signature-table { width: 100%; text-align: center; margin-top: 40px; border: none; table-layout: fixed; page-break-inside: avoid; }
                .signature-table td { vertical-align: top; border: none; font-size: 12pt; padding: 5px; word-wrap: break-word; }

                /* CHỮ KÝ ĐIỆN TỬ ĐÃ ĐỒNG BỘ THEO TÔNG MÀU XANH LÁ CHỦ ĐẠO CỦA ĐƠN CÔNG VIỆC */
                .digital-signature-box { 
                    border: 1px solid #65a30d; 
                    padding: 8px; 
                    text-align: left; 
                    background-color: #f7fee7; 
                    margin: 10px auto 0 auto; 
                    display: block; 
                    width: 98%;
                    max-width: 190px;
                    position: relative;
                    box-sizing: border-box;
                }
                .digital-signature-box .sig-status { 
                    color: #65a30d; 
                    font-size: 10.5pt; 
                    font-weight: bold; 
                    margin-bottom: 3px;
                }
                .digital-signature-box .sig-info { 
                    font-size: 9pt; 
                    color: #2d4201; 
                    line-height: 1.35;
                }
                .digital-signature-box .sig-check-mark {
                    position: absolute;
                    right: 6px;
                    bottom: 4px;
                    font-size: 18pt;
                    font-weight: bold;
                    color: rgba(101, 163, 13, 0.25);
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

            // Phần tiêu đề quốc hiệu & cơ cấu ban ngành Công việc
            sb.Append("<table class='header-table'><tr>");
            sb.Append("<td style='width:45%;'><div class='company-name'>BEST PACIFIC</div><div class='company-sub'>BAN ĐIỀU PHỐI NHIỆM VỤ (WORK)</div></td>");
            sb.Append("<td style='width:55%;'><div class='national-title'>CỘNG HÒA XÃ HỘI CHỦ NGHĨA VIỆT NAM</div><div class='national-sub'>Độc lập - Tự do - Hạnh phúc</div></td>");
            sb.Append("</tr></table>");

            sb.Append($"<div class='form-title'>{don.TenForm}</div>");
            sb.Append($"<div class='form-id'>Mã phiếu: #{don.Id} | Trạng thái: CÔNG VIỆC HOÀN TẤT</div>");

            // I. THÔNG TIN NGƯỜI TẠO
            sb.Append("<div class='section-title'>I. THÔNG TIN NHÂN VIÊN GIAO VIỆC</div>");
            sb.Append("<table class='data-table'>");
            sb.Append($"<tr><th>Họ và tên người giao việc</th><td>{don.TenNguoiNv}</td></tr>");
            sb.Append($"<tr><th>Mã số nhân viên</th><td>{don.SoNhanVien}</td></tr>");
            sb.Append($"<tr><th>Bộ phận / Phòng ban lập đơn</th><td>{don.BoPhan}</td></tr>");
            sb.Append($"<tr><th>Thời gian phát hành hệ thống</th><td>{don.TimeNguoiTao?.ToString("dd/MM/yyyy HH:mm")}</td></tr>");
            sb.Append("</table>");

            // II. CHI TIẾT DỮ LIỆU ĐƠN CÔNG VIỆC CHỈ ĐỊNH
            sb.Append("<div class='section-title'>II. NỘI DUNG CHỈ ĐỊNH CÔNG VIỆC</div>");
            sb.Append("<table class='data-table'>");

            if (don.CvCongViecOrder1s.Any())
            {
                var ct = don.CvCongViecOrder1s.First();
                sb.Append($"<tr><th>Phân hệ lưu trữ</th><td style='font-weight:bold; color:#2d4201;'>Biên bản bàn giao công việc chỉ định trực tiếp (Form 1)</td></tr>");
                sb.Append($"<tr><th>Tiêu đề công việc / Hạng mục</th><td>{ct.Ten}</td></tr>");
                sb.Append($"<tr><th>Mức độ ưu tiên phân phối</th><td>{ct.MucDoUuTien}</td></tr>");
                sb.Append($"<tr><th>Thời hạn xử lý (Deadline)</th><td>{ct.ThoiHanHoanThanh?.ToString("dd/MM/yyyy HH:mm") ?? "Không giới hạn"}</td></tr>");
                sb.Append($"<tr><th>Mô tả nội dung tác vụ chi tiết</th><td>{ct.GhiChu}</td></tr>");
            }

            sb.Append("</table>");

            // III. KHỐI CHỮ KÝ XÁC NHẬN ĐIỆN TỬ CỐ ĐỊNH 3 BÊN
            double colPercent = 100.0 / 3.0;
            sb.Append("<table class='signature-table'><tr>");

            // 1. CỘT NGƯỜI LẬP PHIẾU
            sb.Append($"<td style='width:{colPercent}%;'><strong>NGƯỜI GIAO VIỆC</strong><br/><span style='font-size:9pt;'>(Chữ ký điện tử)</span><br/>");
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

            // 2. CỘT QUẢN LÝ PHÊ DUYỆT PHÒNG BAN
            sb.Append($"<td style='width:{colPercent}%;'><strong>QUẢN LÝ PHÊ DUYỆT</strong><br/><span style='font-size:9pt;'>(Chữ ký điện tử)</span><br/>");
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

            // 3. CỘT BAN QUẢN TRỊ XÁC NHẬN ĐÓNG PHIẾU
            sb.Append($"<td style='width:{colPercent}%;'><strong>BAN QUẢN TRỊ ĐÓNG PHIẾU</strong><br/><span style='font-size:9pt;'>(Chữ ký điện tử)</span><br/>");
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

        #region ĐƠN CHỜ XÉT DUYỆT (cho nhân viên tạo form)

        // 1. Chỉ trả về View giao diện Đơn của tôi
        [HttpGet("/FormCongViec/DonCho")]
        public IActionResult DonCho()
        {
            if (User == null || User.Identity == null || !User.Identity.IsAuthenticated)
            {
                return Redirect("/DonXetDuyet/DangNhap");
            }
            return View();
        }

        // 2. API kết xuất dữ liệu JSON tối ưu hóa câu lệnh JOIN tự động tại SQL Server
        [HttpGet("/FormCongViec/GetDonChoData")]
        public async Task<IActionResult> GetDonChoData()
        {
            // 1. LẤY THÔNG TIN TỪ CLAIMS NGƯỜI DÙNG HIỆN TẠI
            var userIdStr = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userIdStr)) return Unauthorized();

            int userId = int.Parse(userIdStr);

            // 2. TRUY VẤN DỮ LIỆU CÔNG VIỆC CHỈ ĐỊNH CỦA TÔI KHÔNG THEO DÕI TRẠNG THÁI (AsNoTracking)
            var danhSachDon = await _context.FormCongViecs
                .AsNoTracking()
                .Where(f => f.IdNguoiTao == userId && f.Danhmuc == "Công việc chỉ định")
                .OrderByDescending(f => f.Id)
                .Select(item => new
                {
                    Id = item.Id,
                    TenForm = item.TenForm ?? "",
                    Danhmuc = item.Danhmuc ?? "N/A",
                    Ngay = item.Ngay.HasValue ? item.Ngay.Value.ToString("dd/MM/yyyy") : "",
                    IdNguoiDuyet = item.IdNguoiDuyet,
                    IdAdmin = item.IdAdmin,

                    // Kiểm tra đánh giá thông qua liên kết thực thể Any()
                    DaDanhGia = _context.DanhGiaFormCongViecs.Any(dg => dg.IdFormCongViec == item.Id),

                    // Truy vấn sâu xuống danh sách người liên quan để lấy họ tên nhân sự chịu trách nhiệm chính cuối cùng
                    TenNguoiHoTro = item.FormCongViecNguoiLienQuans
                                        .OrderByDescending(x => x.Id)
                                        .Select(x => x.IdNguoiDungNavigation != null ? x.IdNguoiDungNavigation.HoTen : "Chưa có")
                                        .FirstOrDefault() ?? "Chưa có"
                })
                .ToListAsync();

            return Json(danhSachDon);
        }

        #endregion

        #region QUẢN LÝ XÉT DUYỆT CÔNG VIỆC - RIÊNG BIỆT ĐƠN CÔNG VIỆC CHỈ ĐỊNH

        // 1. CHỈ TRẢ VỀ VIEW RIÊNG BIỆT CHO ĐƠN CHỈ ĐỊNH CÔNG VIỆC
        [HttpGet("/FormCongViec/HoanTatDonChiDinh")]
        public IActionResult HoanTatDonChiDinh()
        {
            var userIdStr = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userIdStr))
                return Redirect("/DonXetDuyet/DangNhap");

            return View(); // Tạo file View đặt tên là HoanTatDonChiDinh.cshtml trong Area QLCongViec
        }

        // 2. API TRẢ VỀ DỮ LIỆU JSON CHO JAVASCRIPT (Chỉ bao gồm đơn Công việc chỉ định)
        [HttpGet("/FormCongViec/GetHoanTatDonChiDinhData")]
        public async Task<IActionResult> GetHoanTatDonChiDinhData()
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

            // LỌC: CHỈ LẤY các đơn có Danh mục là "Công việc chỉ định"
            var query = _context.FormCongViecs.AsNoTracking()
                                .Where(f => f.Danhmuc == "Công việc chỉ định");

            // --- PHÂN QUYỀN LỌC DỮ LIỆU ĐỒNG BỘ HỆ THỐNG ---
            if (userRoles.Any(r => r == "All" || r == "AdminCV" || r == "AdminIT"))
            {
                // Admin: Xem tất cả
                query = query.Where(f => f.IdNguoiDuyet != null);
            }
            else if (userRoles.Contains("QuanLyDuyetDonCV") || userRoles.Contains("QuanLyDuyetDonIT"))
            {
                bool hasPhu = listTenBoPhan.Any();
                query = query.Where(f =>
                    (hasPhu && f.BoPhan != null && listTenBoPhan.Contains(f.BoPhan)) ||
                    (!hasPhu && f.BoPhan == phongBanSession) ||
                    (f.IdNguoiTao == userId)
                );
            }
            else
            {
                // NHÂN VIÊN THƯỜNG: Xem đơn mình tạo HOẶC đơn của người khác trong CÙNG BỘ PHẬN
                query = query.Where(f =>
                    f.IdNguoiTao == userId ||
                    (f.BoPhan == phongBanSession && f.TenCongTy == tenCongTy)
                );
            }

            // --- THỰC THI TRUY VẤN VỚI PROJECTION TOÀN DIỆN ---
            var danhSachDon = await query
                .OrderByDescending(f => f.Id)
                .Select(item => new
                {
                    Id = item.Id,
                    TenNguoiNv = item.TenNguoiNv ?? "",
                    SoNhanVien = item.SoNhanVien ?? "",
                    BoPhan = item.BoPhan ?? "",
                    Danhmuc = "Công việc chỉ định",
                    TenForm = item.TenForm ?? "",
                    TimeNguoiTao = item.TimeNguoiTao.HasValue ? item.TimeNguoiTao.Value.ToString("dd/MM/yyyy") : "",
                    IdNguoiDuyet = item.IdNguoiDuyet,
                    IdAdmin = item.IdAdmin,
                    DaDanhGia = _context.DanhGiaFormCongViecs.Any(dg => dg.IdFormCongViec == item.Id),
                    NguoiHoTros = item.FormCongViecNguoiLienQuans
                                      .Where(ct => ct.IdNguoiDungNavigation != null)
                                      .Select(ct => ct.IdNguoiDungNavigation!.HoTen ?? "N/A")
                                      .ToList(),

                    // --- TRUY VẤN CƠ CẤU TRƯỜNG THỜI HẠN HOÀN THÀNH, MỨC ĐỘ ƯU TIÊN & TIÊU ĐỀ TÊN CÔNG VIỆC CỤ THỂ ---
                    ThoiHanHoanThanh = item.CvCongViecOrder1s.Select(o => o.ThoiHanHoanThanh).FirstOrDefault(),
                    MucDoUuTien = item.CvCongViecOrder1s.Select(o => o.MucDoUuTien).FirstOrDefault() ?? "",
                    Ten = item.CvCongViecOrder1s.Select(o => o.Ten).FirstOrDefault() ?? "" // ĐÃ THÊM: Đồng bộ dữ liệu tên hạng mục đẩy ra View
                })
                .ToListAsync();

            return Json(danhSachDon);
        }

        #endregion

        #region LỊCH SỬ VÀ THÔNG BÁO FORM CÔNG VIỆC (Tối ưu truy vấn - Đầy đủ logic)

        // 1. TRẢ VỀ VIEW RỖNG NHẬT KÝ TIẾN TRÌNH CÔNG VIỆC
        [HttpGet("/FormCongViec/LichSuCongViec")]
        public IActionResult LogLichSuCongViec()
        {
            var userIdStr = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userIdStr)) return Redirect("/DonXetDuyet/DangNhap");
            return View(); // Tạo file View đặt tên là LichSuCongViec.cshtml
        }

        // 2. API TRẢ VỀ JSON LỊCH SỬ - ĐÃ TỐI ƯU HÓA PROJECTION TRÊN RAM VÀ SQL SERVER
        [HttpGet("/FormCongViec/GetLichSuCongViecData")]
        public async Task<IActionResult> GetLichSuCongViecData()
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

            // Khởi tạo query từ bảng lịch sử tiến trình công việc
            var query = _context.LichSuFormCongViecs.AsNoTracking();

            // --- HỆ THỐNG PHÂN QUYỀN TRUY VẤN LOG CHẶT CHẼ ---
            if (User.IsInRole("All"))
            {
                /* Quyền tối cao: Xem toàn bộ lịch sử hệ thống, không lọc theo công ty */
            }
            else if (User.IsInRole("AdminCV") || User.IsInRole("AdminIT"))
            {
                /* Ban quản trị phân hệ Công việc: Xem các đơn đã được duyệt qua cấp Quản lý */
                query = query.Where(l =>
                    l.IdFormCongViecNavigation != null &&
                    l.IdFormCongViecNavigation.IdNguoiDuyet != null &&
                    (l.IdFormCongViecNavigation.IdAdmin == userId ||
                     l.IdFormCongViecNavigation.FormCongViecNguoiLienQuans.Any(ct => ct.IdNguoiDung == userId))
                );
            }
            else
            {
                // Các tài khoản thông thường bắt buộc phải lọc nghiêm ngặt theo Pháp nhân Công ty
                query = query.Where(l => l.IdFormCongViecNavigation != null && l.IdFormCongViecNavigation.TenCongTy == tenCongTy);

                if (User.IsInRole("QuanLyDuyetDonCV") || User.IsInRole("QuanLyDuyetDonIT"))
                {
                    // Cấp quản lý: Nhìn thấy đơn tự tạo, đơn mình duyệt hoặc đơn thuộc phạm vi bộ phận phụ trách
                    bool hasPhu = listTenBoPhan.Any();
                    query = query.Where(l =>
                        l.IdFormCongViecNavigation != null &&
                        (
                            l.IdFormCongViecNavigation.IdNguoiTao == userId ||
                            l.IdFormCongViecNavigation.IdNguoiDuyet == userId ||
                            (hasPhu && l.IdFormCongViecNavigation.BoPhan != null && listTenBoPhan.Contains(l.IdFormCongViecNavigation.BoPhan)) ||
                            (!hasPhu && l.IdFormCongViecNavigation.BoPhan == phongBanSession)
                        )
                    );
                }
                else
                {
                    // Nhân viên thường: Chỉ nhìn thấy lịch sử đơn do mình tạo hoặc đơn mình được gán tên thực hiện
                    query = query.Where(l =>
                        l.IdFormCongViecNavigation != null &&
                        (l.IdFormCongViecNavigation.IdNguoiTao == userId ||
                         l.IdFormCongViecNavigation.FormCongViecNguoiLienQuans.Any(ct => ct.IdNguoiDung == userId))
                    );
                }
            }

            // --- THỰC THI TRUY VẤN VỚI KỸ THUẬT PROJECTION TỐI ƯU TỐC ĐỘ ---
            var rawData = await query
                .OrderByDescending(l => l.Time)
                .Select(l => new
                {
                    l.IdFormCongViec,
                    l.Time,
                    l.TieuDe,
                    l.Mota,
                    f = l.IdFormCongViecNavigation,
                    // Lấy tên nhân sự xử lý hiện tại (người gán việc có STT cao nhất hoặc người thực hiện)
                    CurrentSupporterTen = l.IdFormCongViecNavigation != null
                        ? l.IdFormCongViecNavigation.FormCongViecNguoiLienQuans
                            .OrderByDescending(x => x.Id)
                            .Select(x => x.IdNguoiDungNavigation != null ? x.IdNguoiDungNavigation.HoTen : null)
                            .FirstOrDefault()
                        : null,
                    // Quét trạng thái bản ghi đánh giá sao từ bảng độc lập DanhGiaFormCongViec
                    DanhGia = l.IdFormCongViecNavigation != null
                        ? _context.DanhGiaFormCongViecs
                            .Where(d => d.IdFormCongViec == l.IdFormCongViec)
                            .Select(d => new { d.TimeNguoiDanhGia, d.MucDo })
                            .FirstOrDefault()
                        : null
                })
                .ToListAsync();

            // --- BƯỚC 3: PHÂN TÍCH LOGIC MÀU SẮC, TRẠNG THÁI VÀ ĐO ĐẠC HIỆU SUẤT TRÊN RAM ---
            var result = rawData.Select(item =>
            {
                var f = item.f;
                bool isCanceled = f?.TrangThai == "Huy" || (f?.TenForm != null && f.TenForm.Contains("[ĐÃ HỦY]"));

                string statusText = "Chờ duyệt";
                string statusColor = "#f59e0b";

                if (isCanceled) { statusText = "Đã hủy đơn"; statusColor = "#ef4444"; }
                else if (f != null)
                {
                    if (f.TimeNguoiDuyet == null) { statusText = "Chờ phê duyệt"; statusColor = "#f97316"; }
                    else if (f.TimeAdmin == null) { statusText = "Đang xử lý"; statusColor = "#3b82f6"; }
                    else if (item.DanhGia == null) { statusText = "Chờ đánh giá"; statusColor = "#db2777"; }
                    else { statusText = "Hoàn tất 100%"; statusColor = "#65a30d"; } // Đồng bộ màu xanh lá tươi thương hiệu mới
                }

                string actionColor = "#65a30d"; // Mặc định xanh lá
                var tieuDeUpper = item.TieuDe?.ToUpper() ?? "";
                if (tieuDeUpper.Contains("DUYỆT") || tieuDeUpper.Contains("HOÀN TẤT")) actionColor = "#16a34a";
                else if (tieuDeUpper.Contains("HỦY") || tieuDeUpper.Contains("LÀM LẠI") || tieuDeUpper.Contains("XÓA")) actionColor = "#ef4444";
                else if (tieuDeUpper.Contains("BỔ SUNG") || tieuDeUpper.Contains("THẢO LUẬN")) actionColor = "#00b4d8";

                string timeProcessing = "";
                string timeTotal = "";

                // Đo đạc chuẩn xác thời gian xử lý từ lúc Quản lý duyệt đến lúc Admin đóng phiếu hoàn tất
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
                    IdFormIt = item.IdFormCongViec, // Giữ nguyên thuộc tính đầu ra phục vụ mapping Jquery ngoài View
                    TimeHHmm = item.Time?.ToString("HH:mm") ?? "",
                    TimeDDMM = item.Time?.ToString("dd/MM/yyyy") ?? "",
                    TieuDe = item.TieuDe ?? "",
                    Mota = item.Mota ?? "",
                    IsCanceled = isCanceled,
                    StatusText = statusText,
                    StatusColor = statusColor,
                    ActionColor = actionColor,
                    TenForm = f?.TenForm ?? "N/A",
                    TenAdmin = f?.TenAdmin ?? "Chưa gán nhân sự",
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

        // 3. GET REAL-TIME NOTIFICATIONS FOR LAYOUT BELL
        [HttpGet("/FormCongViec/GetNotifications")]
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

            var query = _context.LichSuFormCongViecs.AsNoTracking();

            // Phân quyền kéo thông báo thời gian thực đồng bộ logic xem lịch sử
            if (User.IsInRole("All")) { /* Ban quản trị tối cao */ }
            else if (User.IsInRole("AdminCV") || User.IsInRole("AdminIT"))
            {
                query = query.Where(l =>
                    l.IdFormCongViecNavigation != null &&
                    l.IdFormCongViecNavigation.IdNguoiDuyet != null &&
                    (l.IdFormCongViecNavigation.IdAdmin == userId ||
                     l.IdFormCongViecNavigation.FormCongViecNguoiLienQuans.Any(ct => ct.IdNguoiDung == userId))
                );
            }
            else
            {
                query = query.Where(l => l.IdFormCongViecNavigation != null && l.IdFormCongViecNavigation.TenCongTy == tenCongTy);

                if (User.IsInRole("QuanLyDuyetDonCV") || User.IsInRole("QuanLyDuyetDonIT"))
                {
                    bool hasPhu = listTenBoPhan.Any();
                    query = query.Where(l =>
                        l.IdFormCongViecNavigation != null &&
                        (
                            l.IdFormCongViecNavigation.IdNguoiTao == userId ||
                            l.IdFormCongViecNavigation.IdNguoiDuyet == userId ||
                            (hasPhu && l.IdFormCongViecNavigation.BoPhan != null && listTenBoPhan.Contains(l.IdFormCongViecNavigation.BoPhan)) ||
                            (!hasPhu && l.IdFormCongViecNavigation.BoPhan == phongBanSession)
                        )
                    );
                }
                else
                {
                    query = query.Where(l => l.IdFormCongViecNavigation != null && l.IdFormCongViecNavigation.IdNguoiTao == userId);
                }
            }

            // Tính toán tổng số lượng thông báo mới chưa đọc đổ ra Badge quả chuông
            var unreadCount = await query.CountAsync(l => l.IsRead != true);

            var logs = await query.OrderByDescending(l => l.Time)
                                  .Skip(skip)
                                  .Take(take)
                                  .Select(l => new
                                  {
                                      l.Id,
                                      idFormCongViec = l.IdFormCongViec,
                                      l.TieuDe,
                                      l.Mota,
                                      Time = l.Time.HasValue ? l.Time.Value.ToString("dd/MM HH:mm") : "",
                                      IsRead = l.IsRead ?? false
                                  })
                                  .ToListAsync();

            return Ok(new { dataList = logs, unreadCount });
        }

        #endregion

        #region BÁO CÁO THỐNG KÊ FORM CÔNG VIỆC

        // 1. TRẢ VỀ VIEW GIAO DIỆN BÁO CÁO DASHBOARD ĐỒ HỌA
        [HttpGet("/FormCongViec/BaoCaoThongKeCongViec")]
        public IActionResult BaoCaoThongKe()
        {
            var userIdStr = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userIdStr)) return Redirect("/DonXetDuyet/DangNhap");
            return View();
        }

        // 2. API KẾT XUẤT DỮ LIỆU TỔNG QUAN (DÀNH CHO BIỂU ĐỒ TRẠNG THÁI, PHÒNG BAN, DANH MỤC, MÃ FORM)
        [HttpGet("/FormCongViec/GetDataThongKe")]
        public async Task<IActionResult> GetDataThongKe(DateTime? fromDate, DateTime? toDate)
        {
            try
            {
                var query = _context.FormCongViecs.AsNoTracking();

                // Lọc theo khoảng thời gian chọn trên giao diện (Sử dụng mốc thời gian duyệt TimeNguoiDuyet)
                if (fromDate.HasValue)
                {
                    query = query.Where(x => x.TimeNguoiDuyet >= fromDate.Value);
                }
                if (toDate.HasValue)
                {
                    var endOfToDate = toDate.Value.Date.AddDays(1).AddTicks(-1);
                    query = query.Where(x => x.TimeNguoiDuyet <= endOfToDate);
                }

                // Sử dụng Select Projection trực tiếp tối ưu câu lệnh sinh SQL sạch của EF Core
                var data = await query
                    .Select(x => new
                    {
                        x.Id,
                        x.IdForm,   // Mã Form (CV_CongViec_Order_1...)
                        x.Danhmuc,  // Danh mục phân loại
                        x.BoPhan,   // Bộ phận yêu cầu
                        x.IdNguoiDuyet,
                        x.IdAdmin,
                        x.TenForm,
                        IsRated = _context.DanhGiaFormCongViecs.Any(dg => dg.IdFormCongViec == x.Id) // Kiểm tra đánh giá từ bảng độc lập
                    })
                    .ToListAsync();

                return Json(data);
            }
            catch (Exception ex)
            {
                return BadRequest(ex.Message);
            }
        }

        // 3. API KẾT XUẤT DỮ LIỆU HIỆU SUẤT VÀ THỜI GIAN XỬ LÝ THEO NHÂN SỰ ĐƯỢC CHỈ ĐỊNH
        [HttpGet("/FormCongViec/GetDataNguoiHoTro")]
        public async Task<IActionResult> GetDataNguoiHoTro(DateTime? fromDate, DateTime? toDate)
        {
            try
            {
                // Truy vấn từ bảng FormCongViec_NguoiLienQuan đại diện cho mối quan hệ nhân sự được gán việc
                var query = _context.FormCongViecNguoiLienQuans.AsNoTracking();

                // Lọc khoảng thời gian bảo vệ an toàn chống lỗi Null Reference
                if (fromDate.HasValue)
                {
                    query = query.Where(x => x.IdFormCongViecNavigation != null && x.IdFormCongViecNavigation.TimeNguoiDuyet >= fromDate.Value);
                }
                if (toDate.HasValue)
                {
                    var endOfToDate = toDate.Value.Date.AddDays(1).AddTicks(-1);
                    query = query.Where(x => x.IdFormCongViecNavigation != null && x.IdFormCongViecNavigation.TimeNguoiDuyet <= endOfToDate);
                }

                // Chiếu dữ liệu thô (Raw Projection) rút gọn dải cột kéo từ SQL Server về RAM trước khi GroupBy
                var rawData = await query
                    .Select(x => new
                    {
                        x.IdFormCongViec,
                        Stt = x.Id, // Sử dụng khóa chính tăng tự động Id làm mốc Stt nhân sự gán việc cuối cùng
                        TenNguoiHoTro = x.IdNguoiDungNavigation != null ? x.IdNguoiDungNavigation.HoTen : "Chưa xác định",
                        DanhMuc = x.IdFormCongViecNavigation != null ? x.IdFormCongViecNavigation.Danhmuc : "N/A",
                        TenForm = x.IdFormCongViecNavigation != null ? x.IdFormCongViecNavigation.TenForm : "",
                        IdAdmin = x.IdFormCongViecNavigation != null ? x.IdFormCongViecNavigation.IdAdmin : null,
                        IdNguoiDuyet = x.IdFormCongViecNavigation != null ? x.IdFormCongViecNavigation.IdNguoiDuyet : null,
                        TimeAdmin = x.IdFormCongViecNavigation != null ? x.IdFormCongViecNavigation.TimeAdmin : null,
                        TimeNguoiDuyet = x.IdFormCongViecNavigation != null ? x.IdFormCongViecNavigation.TimeNguoiDuyet : null,
                        HasRating = x.IdFormCongViecNavigation != null && _context.DanhGiaFormCongViecs.Any(dg => dg.IdFormCongViec == x.IdFormCongViec)
                    })
                    .ToListAsync();

                // GroupBy trên RAM để cô lập thông tin nhân sự cuối cùng chịu trách nhiệm chính của đơn phiếu đó
                var filteredData = rawData
                    .GroupBy(x => x.IdFormCongViec)
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
                            IdFormIt = top.IdFormCongViec, // Giữ nguyên tên IdFormIt để Jquery phía View đồng bộ tham chiếu không lỗi
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
                return BadRequest(ex.Message);
            }
        }

        #endregion

        #region BÁO CÁO THỐNG KÊ THEO NGÀY (LỊCH CÔNG VIỆC)

        [HttpGet("/FormCongViec/GetLichCongViecData")]
        public async Task<IActionResult> GetLichCongViecData(DateTime start, DateTime end)
        {
            var userIdStr = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userIdStr)) return Unauthorized();

            int userId = int.Parse(userIdStr);

            // Lấy danh sách đơn từ database
            var danhSachDon = await _context.FormCongViecs
                .AsNoTracking()
                .Where(f => f.TimeNguoiTao >= start && f.TimeNguoiTao <= end)
                .Where(f => f.IdNguoiTao == userId || f.FormCongViecNguoiLienQuans.Any(ct => ct.IdNguoiDung == userId))
                .ToListAsync();

            var result = danhSachDon.Select(f => {
                // Logic xác định màu sắc theo trạng thái
                string color = "#65a30d"; // Mặc định xanh lá (Hoàn tất/Bình thường)

                // Kiểm tra trạng thái "Huy" hoặc tiêu đề có chứa "[ĐÃ HỦY]"
                if (f.TrangThai == "Huy" || (f.TenForm != null && f.TenForm.Contains("[ĐÃ HỦY]")))
                    color = "#ef4444"; // Đỏ
                else if (f.TrangThai == "ChoDuyet")
                    color = "#f59e0b"; // Cam
                else if (f.TrangThai == "DaDuyet")
                    color = "#3b82f6"; // Xanh dương

                return new
                {
                    id = f.Id,
                    title = f.TenForm ?? "Công việc không tiêu đề",
                    start = f.TimeNguoiTao?.ToString("yyyy-MM-dd"),
                    backgroundColor = color,
                    borderColor = color,
                    url = "/FormCongViec/ChiTiet/" + f.Id
                };
            });

            return Json(result);
        }

        // Action trả về View chứa lịch
        [HttpGet("/FormCongViec/LichCongViec")]
        public IActionResult LichCongViec()
        {
            return View();
        }

        #endregion
    }
}