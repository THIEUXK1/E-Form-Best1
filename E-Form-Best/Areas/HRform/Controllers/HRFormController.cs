using E_Form_Best.Context;
using E_Form_Best.Models.ITForm;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace E_Form_Best.Areas.HRform.Controllers
{
    [Area("HRform")]
    public class HRFormController : Controller
    {
        public ITFormContext _context;
        public HRFormController()
        {
            _context = new ITFormContext();
        }
        #region Trang logo
        [HttpGet("/HRForm/logo")]
        public IActionResult logo()
        {
            return View();
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

        #region Don Xin Ra Ngoai (HrXinRaNgoai1)

        [HttpGet("/FormHR/DonXinRaNgoai")]
        public IActionResult DonXinRaNgoai()
        {
            // 1. Kiểm tra đăng nhập qua Cookie (User.Identity)
            if (!User.Identity.IsAuthenticated) return Redirect("/DonXetDuyet/DangNhap");

            // 2. Lấy thông tin từ Claims (CK)
            var userIdString = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userIdString)) return Redirect("/DonXetDuyet/DangNhap");

            int userId = int.Parse(userIdString);
            var userName = User.FindFirst(ClaimTypes.Name)?.Value ?? "";
            var phongBan = User.FindFirst("PhongBan")?.Value ?? "";
            var viTri = User.FindFirst(ClaimTypes.Role)?.Value ?? "";
            var soNhanVien = User.FindFirst(ClaimTypes.Email)?.Value ?? "";
            var tenCongTy = User.FindFirst("TenCongTy")?.Value ?? "";

            // 3. Khởi tạo Model hiển thị lên View
            var model = new FormHr
            {
                TenNguoiNv = userName,
                BoPhan = phongBan,
                ViTri = viTri,
                SoNhanVien = soNhanVien,
                TenCongTy = tenCongTy,
                Danhmuc = "ĐƠN XIN RA NGOÀI",
                Ngay = DateOnly.FromDateTime(DateTime.Now),
                IdNguoiTao = userId,
                TenNguoiTao = userName,
                TimeNguoiTao = DateTime.Now,
                TrangThai = "ChoDuyet"
            };

            return View(model);
        }

        [HttpPost("/FormHR/DonXinRaNgoai")]
        public async Task<IActionResult> DonXinRaNgoai(FormHr form, [FromForm] HrXinRaNgoai1 xinRaNgoai)
        {
            // 1. Kiểm tra đăng nhập
            if (!User.Identity.IsAuthenticated) return Redirect("/DonXetDuyet/DangNhap");

            var userIdString = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            var userName = User.FindFirst(ClaimTypes.Name)?.Value ?? "";
            var phongBan = User.FindFirst("PhongBan")?.Value ?? "";
            var viTri = User.FindFirst(ClaimTypes.Role)?.Value ?? "";
            var soNhanVien = User.FindFirst(ClaimTypes.Email)?.Value ?? "";
            var tenCongTy = User.FindFirst("TenCongTy")?.Value ?? "";

            if (string.IsNullOrEmpty(userIdString)) return Redirect("/DonXetDuyet/DangNhap");
            int userId = int.Parse(userIdString);

            // Đường dẫn Fileserver mạng
            string networkPath = @"\\10.0.60.30\BPVN-Fileserver\Public\IT-Information Technology Dept\5.E-Form\DonHR";

            using (var transaction = await _context.Database.BeginTransactionAsync())
            {
                try
                {
                    // --- BƯỚC 1: LƯU BẢNG CHÍNH (FormHr) ---
                    form.Ngay = DateOnly.FromDateTime(DateTime.Now);
                    form.IdNguoiTao = userId;
                    form.TenNguoiTao = userName;
                    form.TimeNguoiTao = DateTime.Now;
                    form.TenNguoiNv = userName;
                    form.BoPhan = phongBan;
                    form.ViTri = viTri;
                    form.SoNhanVien = soNhanVien;
                    form.TenCongTy = tenCongTy;
                    form.Danhmuc = "ĐƠN XIN RA NGOÀI";
                    form.TrangThai = "ChoDuyet";
                    form.IdForm = "CT_XinRaNgoai_1";
                    form.TenForm = "Đơn xin ra ngoài";

                    _context.FormHrs.Add(form);
                    await _context.SaveChangesAsync();

                    // --- BƯỚC 2: LƯU LỊCH SỬ THAO TÁC (LichSuFormHr) ---
                    var lichSu = new LichSuFormHr
                    {
                        IdFormHr = form.Id,
                        TieuDe = "Khởi tạo đơn",
                        Mota = $"Nhân viên {userName} ({soNhanVien}) đã tạo đơn xin ra ngoài.",
                        Time = DateTime.Now,
                        IsRead = false
                    };
                    _context.LichSuFormHrs.Add(lichSu);
                    await _context.SaveChangesAsync();

                    // --- BƯỚC 3: XỬ LÝ FILE/ẢNH TRÊN FILESERVER ---
                    if (!Directory.Exists(networkPath)) Directory.CreateDirectory(networkPath);

                    string safeName = RemoveSign4VietnameseString(userName).Replace(" ", "");
                    string timeStamp = DateTime.Now.ToString("ddMMyy_HHmm");

                    // A. Xử lý File đính kèm
                    var uploadFile = Request.Form.Files["UploadFile"];
                    if (uploadFile != null && uploadFile.Length > 0)
                    {
                        string extension = Path.GetExtension(uploadFile.FileName);
                        string fileName = $"File_Don{form.Id}_User{userId}_{safeName}_{timeStamp}{extension}";
                        string fullPath = Path.Combine(networkPath, fileName);

                        using (var fileStream = new FileStream(fullPath, FileMode.Create))
                        {
                            await uploadFile.CopyToAsync(fileStream);
                        }
                        form.FileDinhKem = fileName;
                    }

                    // B. Xử lý bảng chi tiết và Ảnh minh chứng (Chỉ lưu đường dẫn)
                    if (xinRaNgoai != null)
                    {
                        xinRaNgoai.IdFormHr = form.Id;

                        var anhFile = Request.Form.Files["AnhXinRaNgoai"];
                        if (anhFile != null && anhFile.Length > 0)
                        {
                            string imgExt = Path.GetExtension(anhFile.FileName);
                            if (string.IsNullOrEmpty(imgExt)) imgExt = ".jpg";

                            string imgName = $"Anh_Don{form.Id}_User{userId}_{safeName}_{timeStamp}{imgExt}";
                            string imgPath = Path.Combine(networkPath, imgName);

                            using (var fileStream = new FileStream(imgPath, FileMode.Create))
                            {
                                await anhFile.CopyToAsync(fileStream);
                            }

                            // LƯU ĐƯỜNG DẪN ẢNH VÀO DB
                            xinRaNgoai.DuongDanAnh = imgName;
                        }

                        // TRIỆT ĐỂ: Không gán dữ liệu cho trường Anh (byte[])
                        xinRaNgoai.Anh = null;

                        _context.HrXinRaNgoai1s.Add(xinRaNgoai);
                        await _context.SaveChangesAsync();
                    }

                    // Cập nhật lại FormHr nếu có FileDinhKem
                    _context.Entry(form).State = Microsoft.EntityFrameworkCore.EntityState.Modified;
                    await _context.SaveChangesAsync();

                    await transaction.CommitAsync();

                    TempData["Success"] = "Gửi đơn xin ra ngoài thành công!";
                    return RedirectToAction("DonCho");
                }
                catch (Exception ex)
                {
                    await transaction.RollbackAsync();
                    ModelState.AddModelError("", "Lỗi hệ thống: " + ex.Message);
                    return View(form);
                }
            }
        }
        #endregion

        #region Đơn Mang Hàng Hóa Ra Cổng

        [HttpGet("/FormHR/MangHangHoaRaCong")]
        public IActionResult MangHangHoaRaCong()
        {
            if (!User.Identity.IsAuthenticated) return Redirect("/DonXetDuyet/DangNhap");

            var userIdString = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userIdString)) return Redirect("/DonXetDuyet/DangNhap");

            int userId = int.Parse(userIdString);
            var userName = User.FindFirst(ClaimTypes.Name)?.Value ?? "";
            var phongBan = User.FindFirst("PhongBan")?.Value ?? "";
            var viTri = User.FindFirst(ClaimTypes.Role)?.Value ?? "";
            var soNhanVien = User.FindFirst(ClaimTypes.Email)?.Value ?? "";
            var tenCongTy = User.FindFirst("TenCongTy")?.Value ?? "";

            var model = new FormHr
            {
                TenNguoiNv = userName,
                BoPhan = phongBan,
                ViTri = viTri,
                SoNhanVien = soNhanVien,
                TenCongTy = tenCongTy,
                Ngay = DateOnly.FromDateTime(DateTime.Now),
                IdNguoiTao = userId,
                TenNguoiTao = userName,
                TimeNguoiTao = DateTime.Now,
                TrangThai = "ChoDuyet",
                Danhmuc = "ĐƠN MANG HÀNG HÓA RA CỔNG"
            };

            return View(model);
        }

        [HttpPost("/FormHR/MangHangHoaRaCong")]
        public async Task<IActionResult> MangHangHoaRaCong(FormHr form, [FromForm] HrMangHangHoaRaCong2 chiTiet)
        {
            if (!User.Identity.IsAuthenticated) return Redirect("/DonXetDuyet/DangNhap");

            // --- GIA CỐ: Nếu Binder bị lỗi và trả về null, ta tự bốc dữ liệu từ Form ---
            if (chiTiet == null || (string.IsNullOrEmpty(chiTiet.MoTa) && Request.Form.ContainsKey("chiTiet.MoTa")))
            {
                chiTiet = new HrMangHangHoaRaCong2
                {
                    MoTa = Request.Form["chiTiet.MoTa"],
                    TenCong = Request.Form["chiTiet.TenCong"]
                };

                if (DateTime.TryParse(Request.Form["chiTiet.TimeDuTinh"], out var time))
                {
                    chiTiet.TimeDuTinh = time;
                }
            }

            var userIdString = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            var userName = User.FindFirst(ClaimTypes.Name)?.Value ?? "";
            var phongBan = User.FindFirst("PhongBan")?.Value ?? "";
            var viTri = User.FindFirst(ClaimTypes.Role)?.Value ?? "";
            var soNhanVien = User.FindFirst(ClaimTypes.Email)?.Value ?? "";
            var tenCongTy = User.FindFirst("TenCongTy")?.Value ?? "";

            if (string.IsNullOrEmpty(userIdString)) return Redirect("/DonXetDuyet/DangNhap");
            int userId = int.Parse(userIdString);

            string networkPath = @"\\10.0.60.30\BPVN-Fileserver\Public\IT-Information Technology Dept\5.E-Form\DonHR";

            using (var transaction = await _context.Database.BeginTransactionAsync())
            {
                try
                {
                    // --- BƯỚC 1: LƯU BẢNG CHÍNH (FormHr) ---
                    form.Ngay = DateOnly.FromDateTime(DateTime.Now);
                    form.IdNguoiTao = userId;
                    form.TenNguoiTao = userName;
                    form.TimeNguoiTao = DateTime.Now;
                    form.TenNguoiNv = userName;
                    form.BoPhan = phongBan;
                    form.ViTri = viTri;
                    form.SoNhanVien = soNhanVien;
                    form.TenCongTy = tenCongTy;
                    form.Danhmuc = "ĐƠN MANG HÀNG HÓA RA CỔNG";
                    form.TrangThai = "ChoDuyet";
                    form.IdForm = "CT_MangHangHoaRaCong_2";
                    form.TenForm = "Đơn mang hàng hóa ra cổng";

                    _context.FormHrs.Add(form);
                    await _context.SaveChangesAsync();

                    // --- BƯỚC 2: LƯU LỊCH SỬ ---
                    var lichSu = new LichSuFormHr
                    {
                        IdFormHr = form.Id,
                        TieuDe = "Khởi tạo đơn mang hàng",
                        Mota = $"Nhân viên {userName} ({soNhanVien}) đã tạo đơn mang hàng hóa ra cổng.",
                        Time = DateTime.Now,
                        IsRead = false
                    };
                    _context.LichSuFormHrs.Add(lichSu);
                    await _context.SaveChangesAsync();

                    // --- BƯỚC 3: XỬ LÝ FILE ---
                    if (!Directory.Exists(networkPath)) Directory.CreateDirectory(networkPath);
                    string safeName = RemoveSign4VietnameseString(userName).Replace(" ", "");
                    string timeStamp = DateTime.Now.ToString("ddMMyy_HHmm");

                    // A. File tài liệu (UploadFile)
                    var uploadFile = Request.Form.Files["UploadFile"];
                    if (uploadFile != null && uploadFile.Length > 0)
                    {
                        string extension = Path.GetExtension(uploadFile.FileName);
                        string fileName = $"Doc_Don{form.Id}_{safeName}_{timeStamp}{extension}";
                        using (var fs = new FileStream(Path.Combine(networkPath, fileName), FileMode.Create))
                        {
                            await uploadFile.CopyToAsync(fs);
                        }
                        form.FileDinhKem = fileName;
                    }

                    // B. Xử lý Chi tiết và Ảnh hàng hóa
                    if (chiTiet != null)
                    {
                        chiTiet.IdFormHr = form.Id;
                        var anhHangHoa = Request.Form.Files["AnhHangHoa"];
                        if (anhHangHoa != null && anhHangHoa.Length > 0)
                        {
                            string imgExt = Path.GetExtension(anhHangHoa.FileName) ?? ".jpg";
                            string imgName = $"Anh_Hang_Don{form.Id}_{safeName}_{timeStamp}{imgExt}";
                            using (var fs = new FileStream(Path.Combine(networkPath, imgName), FileMode.Create))
                            {
                                await anhHangHoa.CopyToAsync(fs);
                            }
                            chiTiet.DuongDanAnh = imgName;
                        }
                        chiTiet.Anh = null;
                        _context.HrMangHangHoaRaCong2s.Add(chiTiet);
                    }

                    _context.Entry(form).State = EntityState.Modified;
                    await _context.SaveChangesAsync();
                    await transaction.CommitAsync();

                    TempData["Success"] = "Gửi đơn mang hàng hóa thành công!";
                    return RedirectToAction("DonCho");
                }
                catch (Exception ex)
                {
                    await transaction.RollbackAsync();
                    ModelState.AddModelError("", "Lỗi: " + ex.Message);
                    return View(form);
                }
            }
        }

        #endregion

        #region ĐƠN XE ĐI CÔNG TÁC (CT_DangKySuDungXeCongTac_3)

        [HttpGet("/FormHR/DangKySuDungXeCongTac")]
        public IActionResult DangKySuDungXeCongTac()
        {
            if (!User.Identity.IsAuthenticated) return Redirect("/DonXetDuyet/DangNhap");

            var userIdString = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userIdString)) return Redirect("/DonXetDuyet/DangNhap");

            int userId = int.Parse(userIdString);
            var userName = User.FindFirst(ClaimTypes.Name)?.Value ?? "";
            var phongBan = User.FindFirst("PhongBan")?.Value ?? "";
            var viTri = User.FindFirst(ClaimTypes.Role)?.Value ?? "";
            var soNhanVien = User.FindFirst(ClaimTypes.Email)?.Value ?? "";
            var tenCongTy = User.FindFirst("TenCongTy")?.Value ?? "";

            var model = new FormHr
            {
                TenNguoiNv = userName,
                BoPhan = phongBan,
                ViTri = viTri,
                SoNhanVien = soNhanVien,
                TenCongTy = tenCongTy,
                Ngay = DateOnly.FromDateTime(DateTime.Now),
                IdNguoiTao = userId,
                TenNguoiTao = userName,
                TimeNguoiTao = DateTime.Now,
                TrangThai = "ChoDuyet",
                Danhmuc = "ĐƠN ĐĂNG KÝ SỬ DỤNG XE CÔNG TÁC"
            };

            return View(model);
        }

        [HttpPost("/FormHR/DangKySuDungXeCongTac")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DangKySuDungXeCongTac(FormHr form, [FromForm] HrDangKySuDungXeCongTac3 chiTiet)
        {
            if (!User.Identity.IsAuthenticated) return Redirect("/DonXetDuyet/DangNhap");

            // Gia cố Binding nếu chiTiet bị null
            if (chiTiet == null || (string.IsNullOrEmpty(chiTiet.LiDo) && Request.Form.ContainsKey("chiTiet.LiDo")))
            {
                chiTiet = new HrDangKySuDungXeCongTac3
                {
                    LiDo = Request.Form["chiTiet.LiDo"]
                };
                if (DateTime.TryParse(Request.Form["chiTiet.TimeDuTinh"], out var time)) chiTiet.TimeDuTinh = time;
            }

            var userIdString = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            var userName = User.FindFirst(ClaimTypes.Name)?.Value ?? "Unknown";
            var phongBan = User.FindFirst("PhongBan")?.Value ?? "";
            var viTri = User.FindFirst(ClaimTypes.Role)?.Value ?? "";
            var soNhanVien = User.FindFirst(ClaimTypes.Email)?.Value ?? "";
            var tenCongTy = User.FindFirst("TenCongTy")?.Value ?? "";

            if (string.IsNullOrEmpty(userIdString)) return Redirect("/DonXetDuyet/DangNhap");
            int userId = int.Parse(userIdString);

            string networkPath = @"\\10.0.60.30\BPVN-Fileserver\Public\IT-Information Technology Dept\5.E-Form\DonHR";

            using (var transaction = await _context.Database.BeginTransactionAsync())
            {
                try
                {
                    // 1. Lưu Form chính
                    form.Ngay = DateOnly.FromDateTime(DateTime.Now);
                    form.IdNguoiTao = userId;
                    form.TenNguoiTao = userName;
                    form.TimeNguoiTao = DateTime.Now;
                    form.TenNguoiNv = userName;
                    form.BoPhan = phongBan;
                    form.ViTri = viTri;
                    form.SoNhanVien = soNhanVien;
                    form.TenCongTy = tenCongTy;
                    form.TrangThai = "ChoDuyet";
                    form.IdForm = "CT_DangKySuDungXeCongTac_3";
                    form.TenForm = "Đơn đăng ký sử dụng xe công tác";
                    form.Danhmuc = "ĐƠN ĐĂNG KÝ SỬ DỤNG XE CÔNG TÁC";

                    _context.FormHrs.Add(form);
                    await _context.SaveChangesAsync();

                    // 2. Lưu Lịch sử
                    _context.LichSuFormHrs.Add(new LichSuFormHr
                    {
                        IdFormHr = form.Id,
                        TieuDe = "Khởi tạo đơn xe công tác",
                        Mota = $"Nhân viên {userName} đã tạo đơn đăng ký xe.",
                        Time = DateTime.Now,
                        IsRead = false
                    });
                    await _context.SaveChangesAsync();

                    // 3. Xử lý FileServer
                    if (!Directory.Exists(networkPath)) Directory.CreateDirectory(networkPath);
                    string safeName = RemoveSign4VietnameseString(userName).Replace(" ", "");
                    string timeStamp = DateTime.Now.ToString("ddMMyy_HHmm");

                    // A. File đính kèm chung
                    var uploadFile = Request.Form.Files["UploadFile"];
                    if (uploadFile != null && uploadFile.Length > 0)
                    {
                        string ext = Path.GetExtension(uploadFile.FileName);
                        string fName = $"Doc_Xe_Don{form.Id}_{safeName}_{timeStamp}{ext}";
                        using (var fs = new FileStream(Path.Combine(networkPath, fName), FileMode.Create)) await uploadFile.CopyToAsync(fs);
                        form.FileDinhKem = fName;
                    }

                    // B. Xử lý Chi tiết & Ảnh xe (Ctrl+V)
                    if (chiTiet != null)
                    {
                        chiTiet.IdFormHr = form.Id;
                        var anhXe = Request.Form.Files["AnhXe"]; // Tên name trong view
                        if (anhXe != null && anhXe.Length > 0)
                        {
                            string imgExt = Path.GetExtension(anhXe.FileName) ?? ".jpg";
                            string imgName = $"Anh_Xe_Don{form.Id}_{safeName}_{timeStamp}{imgExt}";
                            using (var fs = new FileStream(Path.Combine(networkPath, imgName), FileMode.Create)) await anhXe.CopyToAsync(fs);
                            chiTiet.DuongDanAnh = imgName;
                        }
                        chiTiet.Anh = null; // Tránh nặng DB
                        _context.HrDangKySuDungXeCongTac3s.Add(chiTiet);
                    }

                    _context.Entry(form).State = EntityState.Modified;
                    await _context.SaveChangesAsync();
                    await transaction.CommitAsync();

                    TempData["Success"] = "Gửi đơn đăng ký xe công tác thành công!";
                    return RedirectToAction("DonCho");
                }
                catch (Exception ex)
                {
                    await transaction.RollbackAsync();
                    ModelState.AddModelError("", "Lỗi hệ thống: " + ex.Message);
                    return View(form);
                }
            }
        }

        #endregion

        #region ĐƠN XE ĐI DangKySuDungXeDaily (CT_DangKySuDungXeDaily_4)

        [HttpGet("/FormHR/DangKySuDungXeDaily")]
        public IActionResult DangKySuDungXeDaily()
        {
            // 1. Kiểm tra xác thực qua Cookie
            if (!User.Identity.IsAuthenticated) return Redirect("/DonXetDuyet/DangNhap");

            // 2. Lấy thông tin từ Claims (CK)
            var userIdString = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userIdString)) return Redirect("/DonXetDuyet/DangNhap");

            int userId = int.Parse(userIdString);
            var userName = User.FindFirst(ClaimTypes.Name)?.Value ?? "";
            var phongBan = User.FindFirst("PhongBan")?.Value ?? "";
            var viTri = User.FindFirst(ClaimTypes.Role)?.Value ?? "";
            var soNhanVien = User.FindFirst(ClaimTypes.Email)?.Value ?? "";
            var tenCongTy = User.FindFirst("TenCongTy")?.Value ?? "";

            // 3. Khởi tạo model
            var model = new FormHr
            {
                TenNguoiNv = userName,
                BoPhan = phongBan,
                ViTri = viTri,
                SoNhanVien = soNhanVien,
                TenCongTy = tenCongTy,
                Danhmuc = "ĐƠN ĐĂNG KÝ SỬ DỤNG XE DAILY",
                Ngay = DateOnly.FromDateTime(DateTime.Now),
                IdNguoiTao = userId,
                TenNguoiTao = userName,
                TimeNguoiTao = DateTime.Now,
                TrangThai = "ChoDuyet"
            };

            return View(model);
        }

        [HttpPost("/FormHR/DangKySuDungXeDaily")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DangKySuDungXeDaily(FormHr form, [FromForm] HrDangKySuDungXeDaily4 chiTiet)
        {
            if (!User.Identity.IsAuthenticated) return Redirect("/DonXetDuyet/DangNhap");

            var userIdString = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            var userName = User.FindFirst(ClaimTypes.Name)?.Value ?? "Unknown";
            var phongBan = User.FindFirst("PhongBan")?.Value ?? "";
            var viTri = User.FindFirst(ClaimTypes.Role)?.Value ?? "";
            var soNhanVien = User.FindFirst(ClaimTypes.Email)?.Value ?? "";
            var tenCongTy = User.FindFirst("TenCongTy")?.Value ?? "";

            if (string.IsNullOrEmpty(userIdString)) return Redirect("/DonXetDuyet/DangNhap");
            int userId = int.Parse(userIdString);

            // Đường dẫn Fileserver mạng
            string networkPath = @"\\10.0.60.30\BPVN-Fileserver\Public\IT-Information Technology Dept\5.E-Form\DonHR";

            using (var transaction = await _context.Database.BeginTransactionAsync())
            {
                try
                {
                    // --- BƯỚC 1: LƯU BẢNG CHÍNH (FormHr) ---
                    form.Ngay = DateOnly.FromDateTime(DateTime.Now);
                    form.IdNguoiTao = userId;
                    form.TenNguoiTao = userName;
                    form.TimeNguoiTao = DateTime.Now;
                    form.TenNguoiNv = userName;
                    form.BoPhan = phongBan;
                    form.ViTri = viTri;
                    form.SoNhanVien = soNhanVien;
                    form.TenCongTy = tenCongTy;
                    form.TrangThai = "ChoDuyet";
                    form.Danhmuc = "ĐƠN ĐĂNG KÝ SỬ DỤNG XE DAILY";
                    form.IdForm = "CT_DangKySuDungXeDaily_4";
                    form.TenForm = "Đăng ký sử dụng xe Daily";

                    _context.FormHrs.Add(form);
                    await _context.SaveChangesAsync();

                    // --- BƯỚC 2: LƯU LỊCH SỬ THAO TÁC ---
                    var lichSu = new LichSuFormHr
                    {
                        IdFormHr = form.Id,
                        TieuDe = "Khởi tạo đơn",
                        Mota = $"Nhân viên {userName} ({soNhanVien}) đã tạo đơn đăng ký xe Daily.",
                        Time = DateTime.Now,
                        IsRead = false
                    };
                    _context.LichSuFormHrs.Add(lichSu);
                    await _context.SaveChangesAsync();

                    // --- BƯỚC 3: XỬ LÝ FILE/ẢNH TRÊN FILESERVER ---
                    if (!Directory.Exists(networkPath)) Directory.CreateDirectory(networkPath);

                    string safeName = RemoveSign4VietnameseString(userName).Replace(" ", "");
                    string timeStamp = DateTime.Now.ToString("ddMMyy_HHmm");

                    // A. File đính kèm (nếu có)
                    var uploadFile = Request.Form.Files["UploadFile"];
                    if (uploadFile != null && uploadFile.Length > 0)
                    {
                        string extension = Path.GetExtension(uploadFile.FileName);
                        string fileName = $"File_XeDaily_ID{form.Id}_{safeName}_{timeStamp}{extension}";
                        string fullPath = Path.Combine(networkPath, fileName);

                        using (var fileStream = new FileStream(fullPath, FileMode.Create))
                        {
                            await uploadFile.CopyToAsync(fileStream);
                        }
                        form.FileDinhKem = fileName;
                    }

                    // B. Chi tiết và Ảnh minh chứng (Dán/Upload)
                    if (chiTiet != null)
                    {
                        chiTiet.IdFormHr = form.Id;

                        // Lấy ảnh từ vùng dán (Input: AnhXe)
                        var anhFile = Request.Form.Files["AnhXe"];
                        if (anhFile != null && anhFile.Length > 0)
                        {
                            string imgExt = Path.GetExtension(anhFile.FileName);
                            if (string.IsNullOrEmpty(imgExt)) imgExt = ".jpg";

                            string imgName = $"Anh_XeDaily_ID{form.Id}_{safeName}_{timeStamp}{imgExt}";
                            string imgPath = Path.Combine(networkPath, imgName);

                            using (var fileStream = new FileStream(imgPath, FileMode.Create))
                            {
                                await anhFile.CopyToAsync(fileStream);
                            }
                            // Lưu tên file vào DuongDanAnh theo đúng Model của bạn
                            chiTiet.DuongDanAnh = imgName;
                        }

                        // Không lưu vào byte[] Anh để nhẹ Database
                        chiTiet.Anh = null;

                        _context.HrDangKySuDungXeDaily4s.Add(chiTiet);
                    }

                    // Cập nhật lại FormHr để lưu FileDinhKem
                    _context.Entry(form).State = Microsoft.EntityFrameworkCore.EntityState.Modified;
                    await _context.SaveChangesAsync();

                    await transaction.CommitAsync();

                    TempData["Success"] = "Gửi đơn đăng ký xe Daily thành công!";
                    return RedirectToAction("DonCho");
                }
                catch (Exception ex)
                {
                    await transaction.RollbackAsync();
                    ModelState.AddModelError("", "Lỗi hệ thống: " + ex.Message);
                    return View(form);
                }
            }
        }
        #endregion

        #region ĐƠN Tiếp Khách (CT_DonTiepKhac_5)

        [HttpGet("/FormHR/DonTiepKhac")]
        public IActionResult DonTiepKhac()
        {
            // 1. Kiểm tra đăng nhập
            if (!User.Identity.IsAuthenticated) return Redirect("/DonXetDuyet/DangNhap");

            // 2. Lấy thông tin từ Claims
            var userIdString = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userIdString)) return Redirect("/DonXetDuyet/DangNhap");

            int userId = int.Parse(userIdString);
            var userName = User.FindFirst(ClaimTypes.Name)?.Value ?? "";
            var phongBan = User.FindFirst("PhongBan")?.Value ?? "";
            var viTri = User.FindFirst(ClaimTypes.Role)?.Value ?? "";
            var soNhanVien = User.FindFirst(ClaimTypes.Email)?.Value ?? "";
            var tenCongTy = User.FindFirst("TenCongTy")?.Value ?? "";

            // 3. Khởi tạo Model hiển thị lên View
            var model = new FormHr
            {
                IdForm = "CT_DonTiepKhac_5",
                TenForm = "Đơn đăng ký tiếp khách",
                Danhmuc = "ĐƠN TIẾP KHÁCH",
                TenCongTy = tenCongTy,
                TenNguoiNv = userName,
                BoPhan = phongBan,
                ViTri = viTri,
                SoNhanVien = soNhanVien,
                Ngay = DateOnly.FromDateTime(DateTime.Now),
                IdNguoiTao = userId,
                TenNguoiTao = userName,
                TimeNguoiTao = DateTime.Now,
                TrangThai = "ChoDuyet"
            };

            return View(model);
        }

        [HttpPost("/FormHR/DonTiepKhac")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DonTiepKhac(FormHr form, [FromForm] HrDonTiepKhac5 chiTiet)
        {
            // 1. Kiểm tra đăng nhập
            if (!User.Identity.IsAuthenticated) return Redirect("/DonXetDuyet/DangNhap");

            var userIdString = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            var userName = User.FindFirst(ClaimTypes.Name)?.Value ?? "";
            var phongBan = User.FindFirst("PhongBan")?.Value ?? "";
            var viTri = User.FindFirst(ClaimTypes.Role)?.Value ?? "";
            var soNhanVien = User.FindFirst(ClaimTypes.Email)?.Value ?? "";
            var tenCongTy = User.FindFirst("TenCongTy")?.Value ?? "";

            if (string.IsNullOrEmpty(userIdString)) return Redirect("/DonXetDuyet/DangNhap");
            int userId = int.Parse(userIdString);

            // Đường dẫn Fileserver mạng
            string networkPath = @"\\10.0.60.30\BPVN-Fileserver\Public\IT-Information Technology Dept\5.E-Form\DonHR";

            using (var transaction = await _context.Database.BeginTransactionAsync())
            {
                try
                {
                    // --- BƯỚC 1: LƯU BẢNG CHÍNH (FormHr) ---
                    form.Ngay = DateOnly.FromDateTime(DateTime.Now);
                    form.IdNguoiTao = userId;
                    form.TenNguoiTao = userName;
                    form.TimeNguoiTao = DateTime.Now;
                    form.TenNguoiNv = userName;
                    form.BoPhan = phongBan;
                    form.ViTri = viTri;
                    form.SoNhanVien = soNhanVien;
                    form.TenCongTy = tenCongTy;
                    form.Danhmuc = "ĐƠN TIẾP KHÁCH";
                    form.TrangThai = "ChoDuyet";
                    form.IdForm = "CT_DonTiepKhac_5";
                    form.TenForm = "Đơn đăng ký tiếp khách";

                    _context.FormHrs.Add(form);
                    await _context.SaveChangesAsync();

                    // --- BƯỚC 2: LƯU LỊCH SỬ THAO TÁC (LichSuFormHr) ---
                    var lichSu = new LichSuFormHr
                    {
                        IdFormHr = form.Id,
                        TieuDe = "Khởi tạo đơn",
                        Mota = $"Nhân viên {userName} ({soNhanVien}) đã tạo đơn tiếp khách.",
                        Time = DateTime.Now,
                        IsRead = false
                    };
                    _context.LichSuFormHrs.Add(lichSu);
                    await _context.SaveChangesAsync();

                    // Tạo thư mục nếu chưa có
                    if (!Directory.Exists(networkPath)) Directory.CreateDirectory(networkPath);

                    string safeName = RemoveSign4VietnameseString(userName).Replace(" ", "");
                    string timeStamp = DateTime.Now.ToString("ddMMyy_HHmm");

                    // --- BƯỚC 3: XỬ LÝ FILE ĐÍNH KÈM (UploadFile) ---
                    var uploadFile = Request.Form.Files["UploadFile"];
                    if (uploadFile != null && uploadFile.Length > 0)
                    {
                        string extension = Path.GetExtension(uploadFile.FileName);
                        string fileName = $"File_TK_{form.Id}_User{userId}_{safeName}_{timeStamp}{extension}";
                        string fullPath = Path.Combine(networkPath, fileName);

                        using (var fileStream = new FileStream(fullPath, FileMode.Create))
                        {
                            await uploadFile.CopyToAsync(fileStream);
                        }
                        form.FileDinhKem = fileName; // Chỉ lưu tên file
                    }

                    // --- BƯỚC 4: XỬ LÝ CHI TIẾT (HrDonTiepKhac5) & ẢNH MINH CHỨNG ---
                    if (chiTiet != null)
                    {
                        chiTiet.IdFormHr = form.Id;

                        // Xử lý Ảnh Minh Chứng (Paste/Upload)
                        var anhFile = Request.Form.Files["AnhMinhChung"];
                        if (anhFile != null && anhFile.Length > 0)
                        {
                            string imgExt = Path.GetExtension(anhFile.FileName);
                            if (string.IsNullOrEmpty(imgExt)) imgExt = ".jpg";

                            string imgName = $"Anh_TK_{form.Id}_User{userId}_{safeName}_{timeStamp}{imgExt}";
                            string imgPath = Path.Combine(networkPath, imgName);

                            using (var fileStream = new FileStream(imgPath, FileMode.Create))
                            {
                                await anhFile.CopyToAsync(fileStream);
                            }
                            chiTiet.DuongDanAnh = imgName; // Lưu tên file/đường dẫn tương đối vào DB
                        }

                        _context.HrDonTiepKhac5s.Add(chiTiet);
                        await _context.SaveChangesAsync();
                    }

                    // Cập nhật lại FormHr nếu có FileDinhKem
                    _context.Entry(form).State = Microsoft.EntityFrameworkCore.EntityState.Modified;
                    await _context.SaveChangesAsync();

                    await transaction.CommitAsync();

                    TempData["Success"] = "Gửi đơn đăng ký tiếp khách thành công!";
                    return RedirectToAction("DonCho");
                }
                catch (Exception ex)
                {
                    await transaction.RollbackAsync();
                    ModelState.AddModelError("", "Lỗi hệ thống: " + ex.Message);
                    return View(form);
                }
            }
        }
        #endregion

        #region ĐƠN NhaThauQuaCong (CT_NhaThauQuaCong_6)

        /// <summary>
        /// Hiển thị form đăng ký nhà thầu qua cổng
        /// </summary>
        [HttpGet("/FormHR/NhaThauQuaCong")]
        public IActionResult NhaThauQuaCong()
        {
            // 1. Kiểm tra xác thực
            if (!User.Identity.IsAuthenticated) return Redirect("/DonXetDuyet/DangNhap");

            // 2. Lấy thông tin từ Claims
            var userIdString = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userIdString)) return Redirect("/DonXetDuyet/DangNhap");

            int userId = int.Parse(userIdString);
            var userName = User.FindFirst(System.Security.Claims.ClaimTypes.Name)?.Value ?? "";
            var phongBan = User.FindFirst("PhongBan")?.Value ?? "";
            var viTri = User.FindFirst(System.Security.Claims.ClaimTypes.Role)?.Value ?? "";
            var soNhanVien = User.FindFirst(System.Security.Claims.ClaimTypes.Email)?.Value ?? "";
            var tenCongTy = User.FindFirst("TenCongTy")?.Value ?? "";

            // 3. Khởi tạo model đồng bộ dữ liệu
            var model = new FormHr
            {
                TenNguoiNv = userName,
                BoPhan = phongBan,
                ViTri = viTri,
                SoNhanVien = soNhanVien,
                TenCongTy = tenCongTy,
                Danhmuc = "ĐƠN ĐĂNG KÝ NHÀ THẦU QUA CỔNG",
                Ngay = DateOnly.FromDateTime(DateTime.Now),
                IdNguoiTao = userId,
                TenNguoiTao = userName,
                TimeNguoiTao = DateTime.Now,
                TrangThai = "ChoDuyet"
            };
            return View(model);
        }

        /// <summary>
        /// Xử lý gửi đơn nhà thầu
        /// </summary>
        [HttpPost("/FormHR/NhaThauQuaCong")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> NhaThauQuaCong(FormHr form, HrNhaThauQuaCong6 chiTiet)
        {
            // 1. Kiểm tra xác thực
            if (!User.Identity.IsAuthenticated) return Redirect("/DonXetDuyet/DangNhap");

            var userIdString = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
            var userName = User.FindFirst(System.Security.Claims.ClaimTypes.Name)?.Value ?? "Unknown";
            var phongBan = User.FindFirst("PhongBan")?.Value ?? "";
            var viTri = User.FindFirst(System.Security.Claims.ClaimTypes.Role)?.Value ?? "";
            var soNhanVien = User.FindFirst(System.Security.Claims.ClaimTypes.Email)?.Value ?? "";
            var tenCongTy = User.FindFirst("TenCongTy")?.Value ?? "";

            if (string.IsNullOrEmpty(userIdString)) return Redirect("/DonXetDuyet/DangNhap");
            int userId = int.Parse(userIdString);

            // Đường dẫn Fileserver
            string networkPath = @"\\10.0.60.30\BPVN-Fileserver\Public\IT-Information Technology Dept\5.E-Form\DonHR";

            using (var transaction = await _context.Database.BeginTransactionAsync())
            {
                try
                {
                    // --- BƯỚC 1: THIẾT LẬP THÔNG TIN FormHr ---
                    form.Ngay = DateOnly.FromDateTime(DateTime.Now);
                    form.IdNguoiTao = userId;
                    form.TenNguoiTao = userName;
                    form.TimeNguoiTao = DateTime.Now;
                    form.TenNguoiNv = userName;
                    form.BoPhan = phongBan;
                    form.ViTri = viTri;
                    form.SoNhanVien = soNhanVien;
                    form.TenCongTy = tenCongTy;
                    form.TrangThai = "ChoDuyet";
                    form.IdForm = "CT_NhaThauQuaCong_6";
                    form.TenForm = "Đơn đăng ký nhà thầu qua cổng";
                    form.Danhmuc = "ĐƠN ĐĂNG KÝ NHÀ THẦU QUA CỔNG";

                    _context.FormHrs.Add(form);
                    await _context.SaveChangesAsync(); // Lưu để lấy form.Id

                    // --- BƯỚC 2: XỬ LÝ FILE ĐÍNH KÈM & ẢNH ---
                    if (!Directory.Exists(networkPath)) Directory.CreateDirectory(networkPath);

                    string safeName = RemoveSign4VietnameseString(userName).Replace(" ", "");
                    string timeStamp = DateTime.Now.ToString("ddMMyy_HHmm");

                    // A. File tài liệu (PDF/Excel)
                    var uploadFile = Request.Form.Files["UploadFile"];
                    if (uploadFile != null && uploadFile.Length > 0)
                    {
                        string extension = Path.GetExtension(uploadFile.FileName);
                        string fileName = $"File_NT_Don{form.Id}_{safeName}_{timeStamp}{extension}";
                        string fullPath = Path.Combine(networkPath, fileName);

                        using (var stream = new FileStream(fullPath, FileMode.Create))
                        {
                            await uploadFile.CopyToAsync(stream);
                        }
                        form.FileDinhKem = fileName;
                    }

                    // B. Ảnh minh chứng (Paste image)
                    var anhFile = Request.Form.Files["Anh"];
                    if (anhFile != null && anhFile.Length > 0)
                    {
                        string imgExt = Path.GetExtension(anhFile.FileName);
                        if (string.IsNullOrEmpty(imgExt)) imgExt = ".jpg";

                        string imgName = $"AnhNT_Don{form.Id}_{safeName}_{timeStamp}{imgExt}";
                        string imgPath = Path.Combine(networkPath, imgName);

                        using (var stream = new FileStream(imgPath, FileMode.Create))
                        {
                            await anhFile.CopyToAsync(stream);
                        }
                        chiTiet.DuongDanAnh = imgName;
                    }

                    // --- BƯỚC 3: LƯU CHI TIẾT NHÀ THẦU ---
                    chiTiet.IdFormHr = form.Id;
                    chiTiet.Anh = null; // Không lưu byte[] vào DB để tối ưu dung lượng

                    _context.HrNhaThauQuaCong6s.Add(chiTiet);
                    await _context.SaveChangesAsync();

                    // --- BƯỚC 4: LƯU LỊCH SỬ THAO TÁC ---
                    var lichSu = new LichSuFormHr
                    {
                        IdFormHr = form.Id,
                        TieuDe = "Khởi tạo đơn",
                        Mota = $"Nhân viên {userName} đã tạo đơn đăng ký nhà thầu: {chiTiet.TenNhaThau} ({chiTiet.SoNguoi} người).",
                        Time = DateTime.Now,
                        IsRead = false
                    };
                    _context.LichSuFormHrs.Add(lichSu);

                    // Cập nhật lại FormHr nếu có FileDinhKem
                    _context.Entry(form).State = Microsoft.EntityFrameworkCore.EntityState.Modified;
                    await _context.SaveChangesAsync();

                    await transaction.CommitAsync();

                    TempData["Success"] = "Gửi đơn đăng ký nhà thầu thành công!";
                    return RedirectToAction("DonCho");
                }
                catch (Exception ex)
                {
                    await transaction.RollbackAsync();
                    ModelState.AddModelError("", "Lỗi hệ thống: " + ex.Message);
                    return View(form);
                }
            }
        }
        #endregion

        #region ĐƠN HoTroTienDienThoai (CT_HoTroTienDienThoai_7)

        /// <summary>
        /// Hiển thị form đăng ký hỗ trợ tiền điện thoại sử dụng Cookie Authentication
        /// </summary>
        [HttpGet("/FormHR/HoTroTienDienThoai")]
        public IActionResult HoTroTienDienThoai()
        {
            // 1. Kiểm tra xác thực qua Cookie
            if (!User.Identity.IsAuthenticated) return Redirect("/DonXetDuyet/DangNhap");

            // 2. Lấy thông tin từ Claims (CK)
            var userIdString = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userIdString)) return Redirect("/DonXetDuyet/DangNhap");

            int userId = int.Parse(userIdString);
            var userName = User.FindFirst(ClaimTypes.Name)?.Value ?? "";
            var phongBan = User.FindFirst("PhongBan")?.Value ?? "";
            var viTri = User.FindFirst(ClaimTypes.Role)?.Value ?? "";
            var soNhanVien = User.FindFirst(ClaimTypes.Email)?.Value ?? "";
            var tenCongTy = User.FindFirst("TenCongTy")?.Value ?? "";

            // 3. Khởi tạo model với đầy đủ thông tin từ CK (Giữ nguyên các thuộc tính bạn đã dùng)
            var model = new FormHr
            {
                TenNguoiNv = userName,
                BoPhan = phongBan,
                ViTri = viTri,
                SoNhanVien = soNhanVien,
                TenCongTy = tenCongTy,
                Danhmuc = "ĐƠN HỖ TRỢ TIỀN ĐIỆN THOẠI",
                Ngay = DateOnly.FromDateTime(DateTime.Now),
                IdNguoiTao = userId,
                TenNguoiTao = userName,
                TimeNguoiTao = DateTime.Now,
                TrangThai = "ChoDuyet"
            };
            return View(model);
        }

        /// <summary>
        /// Xử lý gửi đơn hỗ trợ tiền điện thoại sử dụng Cookie Authentication
        /// </summary>
        [HttpPost("/FormHR/HoTroTienDienThoai")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> HoTroTienDienThoai(FormHr form, [FromForm] HrHoTroTienDienThoai7 chiTiet)
        {
            // 1. Kiểm tra xác thực & Lấy thông tin từ CK
            if (!User.Identity.IsAuthenticated) return Redirect("/DonXetDuyet/DangNhap");

            var userIdString = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            var userName = User.FindFirst(ClaimTypes.Name)?.Value ?? "Unknown";
            var phongBan = User.FindFirst("PhongBan")?.Value ?? "";
            var viTri = User.FindFirst(ClaimTypes.Role)?.Value ?? "";
            var soNhanVien = User.FindFirst(ClaimTypes.Email)?.Value ?? "";
            var tenCongTy = User.FindFirst("TenCongTy")?.Value ?? "";

            if (string.IsNullOrEmpty(userIdString)) return Redirect("/DonXetDuyet/DangNhap");
            int userId = int.Parse(userIdString);

            // Đường dẫn Fileserver mạng (Dùng chung đường dẫn với DonXinRaNgoai)
            string networkPath = @"\\10.0.60.30\BPVN-Fileserver\Public\IT-Information Technology Dept\5.E-Form\DonHR";

            using (var transaction = await _context.Database.BeginTransactionAsync())
            {
                try
                {
                    // --- BƯỚC 1: THIẾT LẬP THÔNG TIN BẢNG CHÍNH (FormHr) ---
                    form.Ngay = DateOnly.FromDateTime(DateTime.Now);
                    form.IdNguoiTao = userId;
                    form.TenNguoiTao = userName;
                    form.TimeNguoiTao = DateTime.Now;
                    form.TenNguoiNv = userName;
                    form.BoPhan = phongBan;
                    form.ViTri = viTri;
                    form.SoNhanVien = soNhanVien;
                    form.TenCongTy = tenCongTy;
                    form.Danhmuc = "ĐƠN HỖ TRỢ TIỀN ĐIỆN THOẠI";
                    form.TrangThai = "ChoDuyet";
                    form.IdForm = "CT_HoTroTienDienThoai_7";
                    form.TenForm = "Đơn đăng ký hỗ trợ tiền điện thoại";

                    _context.FormHrs.Add(form);
                    await _context.SaveChangesAsync(); // Lưu để lấy form.Id

                    // --- BƯỚC 2: LƯU LỊCH SỬ THAO TÁC (LichSuFormHr) ---
                    var lichSu = new LichSuFormHr
                    {
                        IdFormHr = form.Id,
                        TieuDe = "Khởi tạo đơn",
                        Mota = $"Nhân viên {userName} ({soNhanVien}) đã tạo đơn hỗ trợ tiền điện thoại.",
                        Time = DateTime.Now,
                        IsRead = false
                    };
                    _context.LichSuFormHrs.Add(lichSu);
                    await _context.SaveChangesAsync();

                    // --- BƯỚC 3: XỬ LÝ FILE/ẢNH TRÊN FILESERVER ---
                    if (!Directory.Exists(networkPath)) Directory.CreateDirectory(networkPath);

                    string safeName = RemoveSign4VietnameseString(userName).Replace(" ", "");
                    string timeStamp = DateTime.Now.ToString("ddMMyy_HHmm");

                    // A. Xử lý File đính kèm (UploadFile)
                    var uploadFile = Request.Form.Files["UploadFile"];
                    if (uploadFile != null && uploadFile.Length > 0)
                    {
                        string extension = Path.GetExtension(uploadFile.FileName);
                        string fileName = $"File_TDT_Don{form.Id}_User{userId}_{safeName}_{timeStamp}{extension}";
                        string fullPath = Path.Combine(networkPath, fileName);

                        using (var fileStream = new FileStream(fullPath, FileMode.Create))
                        {
                            await uploadFile.CopyToAsync(fileStream);
                        }
                        form.FileDinhKem = fileName;
                    }

                    // B. Xử lý bảng chi tiết (HrHoTroTienDienThoai7)
                    if (chiTiet != null)
                    {
                        chiTiet.IdFormHr = form.Id;

                        // Xử lý Ảnh minh chứng (Input name="Anh")
                        var anhFile = Request.Form.Files["Anh"];
                        if (anhFile != null && anhFile.Length > 0)
                        {
                            string imgExt = Path.GetExtension(anhFile.FileName);
                            if (string.IsNullOrEmpty(imgExt)) imgExt = ".jpg";

                            string imgName = $"Anh_TDT_Don{form.Id}_User{userId}_{safeName}_{timeStamp}{imgExt}";
                            string imgPath = Path.Combine(networkPath, imgName);

                            using (var fileStream = new FileStream(imgPath, FileMode.Create))
                            {
                                await anhFile.CopyToAsync(fileStream);
                            }

                            // Lưu đường dẫn vào database
                            chiTiet.DuongDanAnh = imgName;
                        }

                        // Triệt để: Không lưu byte[] vào trường Anh
                        chiTiet.Anh = null;

                        _context.HrHoTroTienDienThoai7s.Add(chiTiet);
                        await _context.SaveChangesAsync();
                    }

                    // Cập nhật lại FormHr nếu có FileDinhKem
                    _context.Entry(form).State = Microsoft.EntityFrameworkCore.EntityState.Modified;
                    await _context.SaveChangesAsync();

                    // Hoàn tất giao dịch
                    await transaction.CommitAsync();

                    TempData["Success"] = "Gửi đơn hỗ trợ tiền điện thoại thành công!";
                    return RedirectToAction("DonCho");
                }
                catch (Exception ex)
                {
                    // Nếu lỗi, Rollback toàn bộ để đảm bảo tính toàn vẹn dữ liệu
                    await transaction.RollbackAsync();
                    ModelState.AddModelError("", "Lỗi hệ thống: " + ex.Message);
                    return View(form);
                }
            }
        }

        #endregion

        #region ĐƠN DoiCaLam (CT_DoiCaLam_8)

        /// <summary>
        /// Hiển thị form đăng ký đổi ca làm việc sử dụng Cookie Authentication
        /// </summary>
        [HttpGet("/FormHR/DoiCaLam")]
        public IActionResult DoiCaLam()
        {
            // 1. Kiểm tra đăng nhập qua Cookie
            if (!User.Identity.IsAuthenticated) return Redirect("/DonXetDuyet/DangNhap");

            // 2. Lấy thông tin từ Claims
            var userIdString = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userIdString)) return Redirect("/DonXetDuyet/DangNhap");

            int userId = int.Parse(userIdString);
            var userName = User.FindFirst(ClaimTypes.Name)?.Value ?? "";
            var phongBan = User.FindFirst("PhongBan")?.Value ?? "";
            var viTri = User.FindFirst(ClaimTypes.Role)?.Value ?? "";
            var soNhanVien = User.FindFirst(ClaimTypes.Email)?.Value ?? "";
            var tenCongTy = User.FindFirst("TenCongTy")?.Value ?? "";

            // 3. Khởi tạo Model hiển thị lên View
            var model = new FormHr
            {
                TenNguoiNv = userName,
                BoPhan = phongBan,
                ViTri = viTri,
                SoNhanVien = soNhanVien,
                TenCongTy = tenCongTy,
                Danhmuc = "ĐƠN ĐỔI CA LÀM VIỆC",
                Ngay = DateOnly.FromDateTime(DateTime.Now),
                IdNguoiTao = userId,
                TenNguoiTao = userName,
                TimeNguoiTao = DateTime.Now,
                TrangThai = "ChoDuyet"
            };

            return View(model);
        }

        /// <summary>
        /// Xử lý gửi đơn đổi ca làm việc, lưu file lên Fileserver và ghi lịch sử
        /// </summary>
        [HttpPost("/FormHR/DoiCaLam")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DoiCaLam(FormHr form, [FromForm] HrDoiCaLam8 chiTiet)
        {
            // 1. Kiểm tra xác thực
            if (!User.Identity.IsAuthenticated) return Redirect("/DonXetDuyet/DangNhap");

            var userIdString = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            var userName = User.FindFirst(ClaimTypes.Name)?.Value ?? "";
            var phongBan = User.FindFirst("PhongBan")?.Value ?? "";
            var viTri = User.FindFirst(ClaimTypes.Role)?.Value ?? "";
            var soNhanVien = User.FindFirst(ClaimTypes.Email)?.Value ?? "";
            var tenCongTy = User.FindFirst("TenCongTy")?.Value ?? "";

            if (string.IsNullOrEmpty(userIdString)) return Redirect("/DonXetDuyet/DangNhap");
            int userId = int.Parse(userIdString);

            // Đường dẫn Fileserver mạng (Đồng bộ với DonXinRaNgoai)
            string networkPath = @"\\10.0.60.30\BPVN-Fileserver\Public\IT-Information Technology Dept\5.E-Form\DonHR";

            using (var transaction = await _context.Database.BeginTransactionAsync())
            {
                try
                {
                    // --- BƯỚC 1: LƯU BẢNG CHÍNH (FormHr) ---
                    form.Ngay = DateOnly.FromDateTime(DateTime.Now);
                    form.IdNguoiTao = userId;
                    form.TenNguoiTao = userName;
                    form.TimeNguoiTao = DateTime.Now;
                    form.TenNguoiNv = userName;
                    form.BoPhan = phongBan;
                    form.ViTri = viTri;
                    form.SoNhanVien = soNhanVien;
                    form.TenCongTy = tenCongTy;
                    form.Danhmuc = "ĐƠN ĐỔI CA LÀM VIỆC";
                    form.TrangThai = "ChoDuyet";
                    form.IdForm = "CT_DoiCaLam_8";
                    form.TenForm = "Đơn đăng ký đổi ca làm việc";

                    _context.FormHrs.Add(form);
                    await _context.SaveChangesAsync();

                    // --- BƯỚC 2: LƯU LỊCH SỬ THAO TÁC ---
                    var lichSu = new LichSuFormHr
                    {
                        IdFormHr = form.Id,
                        TieuDe = "Khởi tạo đơn",
                        Mota = $"Nhân viên {userName} ({soNhanVien}) đã tạo đơn đổi ca làm việc.",
                        Time = DateTime.Now,
                        IsRead = false
                    };
                    _context.LichSuFormHrs.Add(lichSu);
                    await _context.SaveChangesAsync();

                    // --- BƯỚC 3: XỬ LÝ FILE/ẢNH TRÊN FILESERVER ---
                    if (!Directory.Exists(networkPath)) Directory.CreateDirectory(networkPath);

                    string safeName = RemoveSign4VietnameseString(userName).Replace(" ", "");
                    string timeStamp = DateTime.Now.ToString("ddMMyy_HHmm");

                    // A. Xử lý File đính kèm (UploadFile)
                    var uploadFile = Request.Form.Files["UploadFile"];
                    if (uploadFile != null && uploadFile.Length > 0)
                    {
                        string extension = Path.GetExtension(uploadFile.FileName);
                        string fileName = $"File_DoiCa_ID{form.Id}_User{userId}_{safeName}_{timeStamp}{extension}";
                        string fullPath = Path.Combine(networkPath, fileName);

                        using (var fileStream = new FileStream(fullPath, FileMode.Create))
                        {
                            await uploadFile.CopyToAsync(fileStream);
                        }
                        form.FileDinhKem = fileName;
                    }

                    // B. Xử lý bảng chi tiết và Ảnh minh chứng
                    if (chiTiet != null)
                    {
                        chiTiet.IdFormHr = form.Id;

                        // Xử lý ảnh (Paste hoặc Chọn file)
                        var anhFile = Request.Form.Files["Anh"]; // Đảm bảo name="Anh" trong View
                        if (anhFile != null && anhFile.Length > 0)
                        {
                            string imgExt = Path.GetExtension(anhFile.FileName);
                            if (string.IsNullOrEmpty(imgExt)) imgExt = ".jpg";

                            string imgName = $"Anh_DoiCa_ID{form.Id}_User{userId}_{safeName}_{timeStamp}{imgExt}";
                            string imgPath = Path.Combine(networkPath, imgName);

                            using (var fileStream = new FileStream(imgPath, FileMode.Create))
                            {
                                await anhFile.CopyToAsync(fileStream);
                            }

                            // Lưu đường dẫn ảnh vào DB thay vì mảng byte để nhẹ Database
                            chiTiet.DuongDanAnh = imgName;
                        }

                        // Đảm bảo không lưu byte[] trực tiếp nếu đã dùng đường dẫn
                        chiTiet.Anh = null;

                        _context.HrDoiCaLam8s.Add(chiTiet);
                        await _context.SaveChangesAsync();
                    }

                    // Cập nhật lại FormHr để lưu tên FileDinhKem
                    _context.Entry(form).State = Microsoft.EntityFrameworkCore.EntityState.Modified;
                    await _context.SaveChangesAsync();

                    await transaction.CommitAsync();

                    TempData["Success"] = "Gửi đơn đổi ca làm thành công!";
                    return RedirectToAction("DonCho");
                }
                catch (Exception ex)
                {
                    await transaction.RollbackAsync();
                    ModelState.AddModelError("", "Lỗi hệ thống: " + ex.Message);
                    return View(form);
                }
            }
        }

        #endregion

        #region CHI TIẾT ĐƠN FORM HR (TẤT CẢ 8 LOẠI ĐƠN)

        /// <summary>
        /// Xem chi tiết nội dung đơn HR dựa trên ID. 
        /// Tự động nạp dữ liệu từ tất cả 8 bảng chi tiết để dùng Cookie Authentication (CK).
        /// </summary>
        [HttpGet("/FormHR/ChiTiet/{id}")]
        public async Task<IActionResult> ChiTiet(int id)
        {
            // 1. Kiểm tra xác thực Cookie
            if (User.Identity == null || !User.Identity.IsAuthenticated)
            {
                return RedirectToAction("DangNhap", "DonXetDuyet");
            }

            // 2. Lấy thông tin User từ Claims (CK)
            var userIdClaim = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
            // Lấy Role linh hoạt (hỗ trợ cả schema URL dài của CK)
            var userRole = User.FindFirst(System.Security.Claims.ClaimTypes.Role)?.Value
                          ?? User.FindFirst("http://schemas.microsoft.com/ws/2008/06/identity/claims/role")?.Value
                          ?? "";

            if (string.IsNullOrEmpty(userIdClaim))
            {
                return RedirectToAction("DangNhap", "DonXetDuyet");
            }

            // 3. Truy vấn đơn và Eager Loading đầy đủ 8 bảng (KHÔNG BỎ BỚT)
            var don = await _context.FormHrs
                .Include(f => f.HrXinRaNgoai1s)                 // Loại 1
                .Include(f => f.HrMangHangHoaRaCong2s)          // Loại 2
                .Include(f => f.HrDangKySuDungXeCongTac3s)      // Loại 3
                .Include(f => f.HrDangKySuDungXeDaily4s)        // Loại 4
                .Include(f => f.HrDonTiepKhac5s)                // Loại 5
                .Include(f => f.HrNhaThauQuaCong6s)             // Loại 6
                .Include(f => f.HrHoTroTienDienThoai7s)         // Loại 7
                .Include(f => f.HrDoiCaLam8s)                   // Loại 8
                .FirstOrDefaultAsync(m => m.Id == id);

            // 4. Kiểm tra tồn tại
            if (don == null)
            {
                TempData["Error"] = "Không tìm thấy dữ liệu đơn yêu cầu!";
                return RedirectToAction("DonCho");
            }

            // 5. Logic bảo mật (Tùy chọn): Chỉ người liên quan mới được xem
            /*
            int currentUserId = int.Parse(userIdClaim);
            bool isAdminOrManager = userRole.Contains("Admin") || userRole.Contains("QuanLy") || userRole.Contains("All");
            if (don.IdNguoiTao != currentUserId && !isAdminOrManager)
            {
                return Forbid(); // Trả về lỗi 403 nếu xem trộm đơn
            }
            */

            return View(don);
        }

        #endregion

        #region ĐƠN CHỜ XÉT DUYỆT (Dành cho nhân viên xem danh sách đơn đã tạo)

        /// <summary>
        /// GET: /FormHR/DonCho
        /// Hiển thị danh sách các đơn mà nhân viên hiện tại đã gửi và đang chờ xét duyệt
        /// </summary>
        [HttpGet("/FormHR/DonCho")]
        public async Task<IActionResult> DonCho()
        {
            // 1. Kiểm tra xác thực qua Cookie Authentication (CK)
            if (!User.Identity.IsAuthenticated)
            {
                return Redirect("/DonXetDuyet/DangNhap");
            }

            // 2. Lấy UserId từ Claims (CK) thay thế cho Session
            var userIdClaim = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userIdClaim))
            {
                return Redirect("/DonXetDuyet/DangNhap");
            }

            int currentUserId = int.Parse(userIdClaim);

            // 3. Truy vấn danh sách đơn của chính người dùng này
            // Sử dụng OrderByDescending để đơn mới nhất luôn nằm ở vị trí đầu tiên
            var danhSachDon = await _context.FormHrs
                .Where(f => f.IdNguoiTao == currentUserId)
                .OrderByDescending(f => f.Id)
                .ToListAsync();

            // 4. Trả về View cùng danh sách đơn
            return View(danhSachDon);
        }

        #endregion

        #region XỬ LÝ ĐƠN (Duyệt / Hủy / Quản lý)

        /// <summary>
        /// GET: /FormHR/QuanLyXetDuyet
        /// Lọc đơn theo bộ phận cho Quản lý/Admin hoặc toàn bộ cho quyền ALL
        /// </summary>
        [HttpGet("/FormHR/QuanLyXetDuyet")]
        public async Task<IActionResult> QuanLyXetDuyet()
        {
            // 1. Kiểm tra đăng nhập qua Cookie Authentication
            if (!User.Identity.IsAuthenticated) return Redirect("/DonXetDuyet/DangNhap");

            // 2. Lấy thông tin từ Claims (CK)
            var userIdString = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userIdString)) return Redirect("/DonXetDuyet/DangNhap");

            int userId = int.Parse(userIdString);
            // Chuẩn hóa Role về viết hoa để so sánh chính xác
            var userRole = (User.FindFirst(System.Security.Claims.ClaimTypes.Role)?.Value
                           ?? User.FindFirst("UserRole")?.Value
                           ?? "").Trim().ToUpper();

            // Lấy PhongBan từ Claim (Bộ phận của người đang đăng nhập)
            var phongBan = (User.FindFirst("PhongBan")?.Value
                           ?? User.FindFirst("BoPhan")?.Value
                           ?? "").Trim();

            // Bảo vệ: Quyền Bảo vệ không được vào trang này
            if (userRole == "BAOVE") return Forbid();

            IQueryable<FormHr> query = _context.FormHrs;

            // 3. Xử lý phân quyền xem dữ liệu (Không bỏ bớt trường hợp nào)
            if (userRole == "ALL")
            {
                // QUYỀN ALL: Xem được tất cả mọi đơn không cần lọc
            }
            else if (userRole == "ADMIN" || userRole == "QUANLY")
            {
                // XEM THEO BỘ PHẬN: Chỉ thấy đơn của nhân viên cùng bộ phận
                if (!string.IsNullOrEmpty(phongBan))
                {
                    // So sánh không phân biệt hoa thường và xóa khoảng trắng
                    // Lọc các đơn mà trường 'BoPhan' trong DB khớp với 'phongBan' trong Cookie
                    query = query.Where(f => f.BoPhan != null && f.BoPhan.Trim().ToLower() == phongBan.ToLower());
                }
                else
                {
                    // Nếu Quản lý không có thông tin phòng ban trong Cookie -> Trả về trống để an toàn
                    query = query.Where(f => false);
                }
            }
            else
            {
                // NHÂN VIÊN bình thường: Chỉ thấy đơn của chính mình
                query = query.Where(f => f.IdNguoiTao == userId);
            }

            // 4. Lấy dữ liệu và sắp xếp đơn mới nhất lên đầu
            var danhSachDon = await query
                .OrderByDescending(f => f.Id)
                .AsNoTracking()
                .ToListAsync();

            return View(danhSachDon);
        }

        /// <summary>
        /// POST: /FormHR/XuLyDon
        /// Xử lý Duyệt hoặc Hủy đơn từ Quản lý/Admin qua AJAX
        /// </summary>
        [HttpPost("/FormHR/XuLyDon")]
        public async Task<IActionResult> XuLyDon([FromBody] ApprovalRequest request)
        {
            // 1. Tìm đơn
            var form = await _context.FormHrs.FindAsync(request.Id);
            if (form == null) return Json(new { success = false, message = "Không tìm thấy đơn" });

            // 2. Lấy thông tin người duyệt hiện tại từ Cookie
            var userIdString = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
            var userName = User.FindFirst(System.Security.Claims.ClaimTypes.Name)?.Value;

            if (string.IsNullOrEmpty(userIdString))
                return Json(new { success = false, message = "Phiên đăng nhập đã hết hạn" });

            int currentUserId = int.Parse(userIdString);

            // 3. Logic xử lý hành động
            if (request.Action == "Duyet")
            {
                // Cập nhật thông tin Quản lý duyệt
                form.IdNguoiDuyet = currentUserId;
                form.TenNguoiDuyet = userName;
                form.TimeNguoiDuyet = DateTime.Now;
                // Nếu bạn dùng thêm cột TrangThai thì cập nhật ở đây
            }
            else if (request.Action == "Huy")
            {
                // Đánh dấu đơn đã hủy (Logic giữ nguyên: Thêm tiền tố [ĐÃ HỦY])
                if (!string.IsNullOrEmpty(form.TenForm) && !form.TenForm.StartsWith("[ĐÃ HỦY]"))
                {
                    form.TenForm = "[ĐÃ HỦY] " + form.TenForm;
                }
                // Có thể lưu thêm ai là người hủy vào cột ghi chú nếu cần
            }

            // 4. Lưu và phản hồi
            try
            {
                await _context.SaveChangesAsync();
                return Json(new { success = true });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = "Lỗi khi lưu dữ liệu: " + ex.Message });
            }
        }

        #endregion

        #region QUẢN LÝ PHÊ DUYỆT (Admin, All, Quản lý)

        /// <summary>
        /// GET: /FormHR/HoanTatDon
        /// Hiển thị danh sách các đơn đã được duyệt hoặc cần Admin xác nhận hoàn tất.
        /// </summary>
        [HttpGet("/FormHR/HoanTatDon")]
        public async Task<IActionResult> HoanTatDon()
        {
            // 1. Kiểm tra xác thực qua Cookie Authentication (CK)
            if (!User.Identity.IsAuthenticated) return Redirect("/DonXetDuyet/DangNhap");

            // 2. Lấy thông tin từ Claims (Id, Role, Phòng ban)
            var userIdString = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userIdString)) return Redirect("/DonXetDuyet/DangNhap");

            int userId = int.Parse(userIdString);

            // Chuẩn hóa Role (Viết hoa toàn bộ để so sánh chuẩn)
            var userRole = (User.FindFirst(System.Security.Claims.ClaimTypes.Role)?.Value
                           ?? User.FindFirst("UserRole")?.Value
                           ?? "").Trim().ToUpper();

            // Lấy Phòng Ban (Kiểm tra cả 2 key phổ biến)
            var phongBan = (User.FindFirst("PhongBan")?.Value
                           ?? User.FindFirst("BoPhan")?.Value
                           ?? "").Trim();

            // Khởi tạo query ban đầu
            IQueryable<FormHr> query = _context.FormHrs;

            // 3. Xử lý logic phân quyền và điều kiện hiển thị đơn (KHÔNG BỎ BỚT NHÓM QUYỀN)
            if (userRole == "ALL" || userRole == "ADMIN")
            {
                // QUYỀN ALL & ADMIN: Xem tất cả các đơn đã có Quản lý duyệt (không phân biệt bộ phận)
                // Điều kiện: Đơn đã qua bước phê duyệt đầu tiên (IdNguoiDuyet có giá trị)
                query = query.Where(f => f.IdNguoiDuyet != null
                                      && f.TenNguoiDuyet != null
                                      && f.TimeNguoiDuyet != null);
            }
            else if (userRole == "QUANLY")
            {
                // QUẢN LÝ: Chỉ xem đơn của bộ phận mình VÀ đã được duyệt
                if (!string.IsNullOrEmpty(phongBan))
                {
                    query = query.Where(f => f.BoPhan != null
                                          && f.BoPhan.Trim().ToLower() == phongBan.ToLower()
                                          && f.IdNguoiDuyet != null);
                }
                else
                {
                    // Bảo mật: Không có thông tin phòng ban trong Cookie thì không thấy dữ liệu
                    query = query.Where(f => false);
                }
            }
            else if (userRole == "NHANVIEN")
            {
                // NHÂN VIÊN: Xem lại đơn của chính mình đã được duyệt thành công
                query = query.Where(f => f.IdNguoiTao == userId
                                      && f.IdNguoiDuyet != null);
            }
            else
            {
                // Các quyền khác như Bảo vệ hoặc khách không có quyền vào danh sách này
                return Forbid();
            }

            // 4. Thực thi lấy dữ liệu, sắp xếp theo thời gian duyệt mới nhất để theo dõi
            var danhSachDon = await query
                .OrderByDescending(f => f.TimeNguoiDuyet)
                .AsNoTracking()
                .ToListAsync();

            return View(danhSachDon);
        }

        /// <summary>
        /// POST: /FormHR/XacNhanHoanThanh
        /// Dùng cho nút xác nhận cuối cùng của Admin (Ghi vào cột IdAdmin, TenAdmin)
        /// </summary>
        [HttpPost("/FormHR/XacNhanHoanThanh")]
        public async Task<IActionResult> XacNhanHoanThanh([FromBody] ApprovalRequest request)
        {
            // 1. Kiểm tra quyền qua CK: Chỉ cho phép Admin và All mới có quyền bấm nút cuối cùng
            var userRole = (User.FindFirst(System.Security.Claims.ClaimTypes.Role)?.Value
                           ?? User.FindFirst("UserRole")?.Value
                           ?? "").Trim().ToUpper();

            if (userRole != "ADMIN" && userRole != "ALL")
            {
                return Json(new
                {
                    success = false,
                    message = "Bạn không có quyền thực hiện xác nhận hoàn thành (Chỉ dành cho ADMIN)."
                });
            }

            // 2. Tìm đơn và kiểm tra tồn tại trong Database
            var form = await _context.FormHrs.FindAsync(request.Id);
            if (form == null) return Json(new { success = false, message = "Không tìm thấy đơn" });

            // 3. Lấy thông tin Admin đang thực hiện từ Cookie Claims
            var userIdString = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
            var userName = User.FindFirst(System.Security.Claims.ClaimTypes.Name)?.Value;

            if (string.IsNullOrEmpty(userIdString))
                return Json(new { success = false, message = "Phiên đăng nhập hết hạn. Vui lòng đăng nhập lại." });

            // 4. Ghi nhận thông tin phê duyệt cuối cùng vào các cột dành cho ADMIN
            try
            {
                form.IdAdmin = int.Parse(userIdString);
                form.TenAdmin = userName;
                form.TimeAdmin = DateTime.Now;

                // Cập nhật trạng thái chuỗi nếu hệ thống của bạn có cột này
                form.TrangThai = "HoanThanh";

                await _context.SaveChangesAsync();
                return Json(new { success = true });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = "Lỗi hệ thống: " + ex.Message });
            }
        }

        /// <summary>
        /// Class nhận dữ liệu từ AJAX (Giữ nguyên để không lỗi binding)
        /// </summary>
        public class ApprovalRequest
        {
            public int Id { get; set; }
            public string Action { get; set; } // "Duyet" hoặc "Huy"
        }

        #endregion

        #region DanhSachBaoVe (Dành cho bộ phận cổng kiểm soát)

        /// <summary>
        /// GET: /FormHR/DanhSachBaoVe
        /// Hiển thị danh sách các đơn đã hoàn tất phê duyệt để bảo vệ kiểm soát người và hàng hóa ra vào.
        /// </summary>
        [HttpGet("/FormHR/DanhSachBaoVe")]
        public async Task<IActionResult> DanhSachBaoVe()
        {
            // 1. Kiểm tra xác thực qua Cookie Authentication (CK)
            if (!User.Identity.IsAuthenticated)
            {
                return Redirect("/DonXetDuyet/DangNhap");
            }

            // (Tùy chọn) Kiểm tra nếu bạn muốn chỉ duy nhất quyền "BaoVe" hoặc "Admin/All" mới được vào trang này
            /*
            var userRole = User.FindFirst(System.Security.Claims.ClaimTypes.Role)?.Value ?? "";
            if (userRole != "BaoVe" && userRole != "Admin" && userRole != "All")
            {
                return Forbid();
            }
            */

            // 2. Danh sách các loại đơn mà bảo vệ được phép xem (Giữ nguyên logic của bạn)
            // Đơn 1: Xin ra ngoài | Đơn 2: Mang hàng hóa ra cổng
            string[] allowedForms = { "CT_XinRaNgoai_1", "CT_MangHangHoaRaCong_2" };

            // 3. Truy vấn dữ liệu với các điều kiện chặt chẽ
            var model = await _context.FormHrs
                .Include(f => f.HrXinRaNgoai1s)           // Nạp chi tiết đơn xin ra ngoài
                .Include(f => f.HrMangHangHoaRaCong2s)    // Nạp chi tiết đơn mang hàng hóa
                .Where(f => f.IdAdmin != null             // ĐIỀU KIỆN 1: Đã được Admin/HR phê duyệt cuối cùng
                         && allowedForms.Contains(f.IdForm) // ĐIỀU KIỆN 2: Chỉ lấy đúng 2 loại đơn quy định
                         && !(f.TenForm.Contains("[ĐÃ HỦY]"))) // ĐIỀU KIỆN 3: Không lấy các đơn đã bị quản lý hủy
                .OrderByDescending(f => f.TimeAdmin)      // Ưu tiên đơn vừa duyệt xong lên đầu danh sách
                .AsNoTracking()                           // Tối ưu hiệu năng cho trang danh sách
                .ToListAsync();

            // 4. Trả về View cho bảo vệ
            return View(model);
        }

        #endregion

        #region BÁO CÁO THỐNG KÊ FORM HR (Dành cho Admin/All)

        /// <summary>
        /// GET: /FormHR/BaoCaoThongKe
        /// Thống kê số lượng đơn theo loại và hiển thị danh sách tổng hợp.
        /// </summary>
        [HttpGet("/FormHR/BaoCaoThongKe")]
        public async Task<IActionResult> BaoCaoThongKe()
        {
            // 1. Kiểm tra xác thực và quyền hạn qua CK
            if (!User.Identity.IsAuthenticated) return Redirect("/DonXetDuyet/DangNhap");

            var userRole = User.FindFirst(System.Security.Claims.ClaimTypes.Role)?.Value ?? "";

            // Chỉ Admin hoặc quyền All mới có thể xem báo cáo tổng thể
            if (userRole != "Admin" && userRole != "All")
            {
                return Redirect("/");
            }

            // 2. Lấy toàn bộ danh sách đơn
            var allForms = await _context.FormHrs.ToListAsync();

            // 3. Chuẩn bị dữ liệu cho biểu đồ (Grouping)
            var stats = allForms
                .Where(f => !string.IsNullOrEmpty(f.IdForm)) // Loại bỏ đơn không có ID form
                .GroupBy(f => f.IdForm)
                .Select(g => new
                {
                    Name = GetShortName(g.Key),
                    Count = g.Count()
                })
                .OrderByDescending(x => x.Count)
                .ToList();

            // 4. Truyền dữ liệu sang View thông qua ViewBag để vẽ biểu đồ (Chart.js)
            ViewBag.TypeLabels = stats.Select(s => s.Name).ToList();
            ViewBag.TypeCounts = stats.Select(s => s.Count).ToList();

            // Trả về view kèm danh sách đầy đủ để hiển thị bảng dữ liệu (DataTables)
            return View(allForms);
        }

        /// <summary>
        /// Hàm Helper: Chuyển đổi ID Form sang tên ngắn gọn để hiển thị trên biểu đồ
        /// Bảo toàn và cập nhật đầy đủ 8 loại đơn.
        /// </summary>
        private string GetShortName(string id) => id switch
        {
            "CT_XinRaNgoai_1" => "Ra ngoài",
            "CT_MangHangHoaRaCong_2" => "Hàng hóa",
            "CT_XeCongTac_3" => "Xe công tác", // Sửa lại key cho khớp với phần đăng ký bạn đã viết
            "CT_XeDaily_4" => "Xe Daily",
            "CT_DonTiepKhac_5" => "Đón khách",
            "CT_NhaThauQuaCong_6" => "Nhà thầu",
            "CT_HoTroTienDienThoai_7" => "Tiền ĐT", // Bổ sung đơn 7
            "CT_DoiCaLam_8" => "Đổi ca",           // Bổ sung đơn 8
            _ => "Khác (" + id + ")"
        };

        #endregion

    }
}
