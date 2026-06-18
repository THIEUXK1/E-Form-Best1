using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using E_Form_Best.Context;
using E_Form_Best.Models.ITForm;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

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

                    // --- CẤU HÌNH ĐƯỜNG DẪN MẠNG LƯU TRỮ TRÊN FILESERVER ---
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

            // LỌC: CHỈ LẤY các đơn có Danh mục là "Công việc chỉ định" đúng chuẩn nghiệp vụ mới
            var query = _context.FormCongViecs.AsNoTracking()
                                .Where(f => f.Danhmuc == "Công việc chỉ định");

            // --- PHÂN QUYỀN LỌC DỮ LIỆU ĐỒNG BỘ HỆ THỐNG ---
            if (userRoles.Any(r => r == "All" || r == "AdminCV" || r == "AdminIT"))
            {
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
                query = query.Where(f => f.IdNguoiTao == userId);
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

                    // --- TRUY VẤN CƠ CẤU TRƯỜNG THỜI HẠN HOÀN THÀNH & MỨC ĐỘ ƯU TIÊN PHÂN HỆ CÔNG VIỆC ---
                    ThoiHanHoanThanh = item.CvCongViecOrder1s.Select(o => o.ThoiHanHoanThanh).FirstOrDefault(),
                    MucDoUuTien = item.CvCongViecOrder1s.Select(o => o.MucDoUuTien).FirstOrDefault() ?? ""
                })
                .ToListAsync();

            return Json(danhSachDon);
        }

        #endregion


    }
}