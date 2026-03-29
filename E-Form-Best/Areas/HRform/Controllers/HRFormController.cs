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
                    form.IdForm = "HR_XinRaNgoai_1";
                    form.TenForm = "Đơn xin ra ngoài";

                    _context.FormHrs.Add(form);
                    await _context.SaveChangesAsync();

                    // --- BƯỚC 2: TỰ ĐỘNG THÊM NGƯỜI XÁC NHẬN ---
                    // Tìm loại đơn tương ứng với IdForm đã gán bên trên
                    var loaiDon = await _context.DmLoaiDons
                        .FirstOrDefaultAsync(x => x.MaLoaiDon == form.IdForm && x.TrangThai == true);

                    if (loaiDon != null)
                    {
                        // Lấy danh sách cấu hình người xác nhận cho loại đơn này
                        var listCauHinhXacNhan = await _context.DmNguoiXacNhanLoaiDons
                            .Include(x => x.IdnguoiXacNhanNavigation)
                            .Where(x => x.IdloaiDon == loaiDon.IdloaiDon)
                            .ToListAsync();

                        foreach (var item in listCauHinhXacNhan)
                        {
                            var hrXacNhan = new HrNguoiXacNhan
                            {
                                IdFormHr = form.Id,
                                IdnguoiXacNhan = item.IdnguoiXacNhan,
                                ThuTuXacNhan = item.CapDoXacNhan, // Cùng CapDoXacNhan thì ThuTuXacNhan bằng nhau
                                MaNguoiXacNhan = item.IdnguoiXacNhanNavigation?.MaNv,
                                TenNguoiXacNhan = item.IdnguoiXacNhanNavigation?.HoTen,
                                TrangThaiXacNhan = 0, // Mặc định chờ duyệt
                                ThoiGianXacNhan = null,
                                GhiChu = null
                            };
                            _context.HrNguoiXacNhans.Add(hrXacNhan);
                        }
                        await _context.SaveChangesAsync();
                    }

                    // --- BƯỚC 3: LƯU LỊCH SỬ THAO TÁC (LichSuFormHr) ---
                    string chiTietDon = "";
                    if (xinRaNgoai != null)
                    {
                        chiTietDon = $"- Lý do: {xinRaNgoai.LiDo}\n" +
                                     $"- Địa điểm: {xinRaNgoai.DiaDiem}\n" +
                                     $"- Thời gian ra: {xinRaNgoai.ThoiGianRa?.ToString("dd/MM/yyyy HH:mm")}\n" +
                                     $"- Dự kiến về: {xinRaNgoai.ThoiGianVeDuTinh?.ToString("dd/MM/yyyy HH:mm")}";
                    }

                    var lichSu = new LichSuFormHr
                    {
                        IdFormHr = form.Id,
                        TieuDe = "Khởi tạo đơn xin ra ngoài",
                        Mota = $"Nhân viên {userName} ({soNhanVien}) đã tạo đơn.\n{chiTietDon}",
                        Time = DateTime.Now,
                        IsRead = false
                    };
                    _context.LichSuFormHrs.Add(lichSu);
                    await _context.SaveChangesAsync();

                    // --- BƯỚC 4: XỬ LÝ FILE/ẢNH TRÊN FILESERVER ---
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

                    // B. Xử lý bảng chi tiết và Ảnh minh chứng
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
                            xinRaNgoai.DuongDanAnh = imgName;
                        }

                        xinRaNgoai.Anh = null; // Triệt để không lưu byte[] vào DB
                        _context.HrXinRaNgoai1s.Add(xinRaNgoai);
                        await _context.SaveChangesAsync();
                    }

                    // Cập nhật lại FormHr nếu có FileDinhKem từ bước xử lý file
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

        #region Đơn Mang Hàng Hóa Ra Cổng (HR_MangHangHoaRaCong_2)

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

            // --- GIA CỐ: Nếu Binder bị lỗi và trả về null, tự bốc dữ liệu từ Form ---
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
                    form.IdForm = "HR_MangHangHoaRaCong_2"; // Mã loại đơn để check danh mục
                    form.TenForm = "Đơn mang hàng hóa ra cổng";

                    _context.FormHrs.Add(form);
                    await _context.SaveChangesAsync();

                    // --- BƯỚC 2: TỰ ĐỘNG THÊM NGƯỜI XÁC NHẬN ---
                    var loaiDon = await _context.DmLoaiDons
                        .FirstOrDefaultAsync(x => x.MaLoaiDon == form.IdForm && x.TrangThai == true);

                    if (loaiDon != null)
                    {
                        var listCauHinh = await _context.DmNguoiXacNhanLoaiDons
                            .Include(x => x.IdnguoiXacNhanNavigation)
                            .Where(x => x.IdloaiDon == loaiDon.IdloaiDon)
                            .ToListAsync();

                        foreach (var item in listCauHinh)
                        {
                            var hrXacNhan = new HrNguoiXacNhan
                            {
                                IdFormHr = form.Id,
                                IdnguoiXacNhan = item.IdnguoiXacNhan,
                                ThuTuXacNhan = item.CapDoXacNhan, // Cùng cấp độ thì thứ tự bằng nhau
                                MaNguoiXacNhan = item.IdnguoiXacNhanNavigation?.MaNv,
                                TenNguoiXacNhan = item.IdnguoiXacNhanNavigation?.HoTen,
                                TrangThaiXacNhan = 0,
                                ThoiGianXacNhan = null,
                                GhiChu = null
                            };
                            _context.HrNguoiXacNhans.Add(hrXacNhan);
                        }
                        await _context.SaveChangesAsync();
                    }

                    // --- BƯỚC 3: LƯU LỊCH SỬ ---
                    string chiTietDon = "";
                    if (chiTiet != null)
                    {
                        chiTietDon = $"- Nội dung: {chiTiet.MoTa}\n" +
                                     $"- Cổng ra: {chiTiet.TenCong}\n" +
                                     $"- Thời gian dự tính: {chiTiet.TimeDuTinh?.ToString("dd/MM/yyyy HH:mm")}";
                    }

                    var lichSu = new LichSuFormHr
                    {
                        IdFormHr = form.Id,
                        TieuDe = "Khởi tạo đơn mang hàng",
                        Mota = $"Nhân viên {userName} ({soNhanVien}) đã tạo đơn mang hàng hóa ra cổng.\n{chiTietDon}",
                        Time = DateTime.Now,
                        IsRead = false
                    };
                    _context.LichSuFormHrs.Add(lichSu);
                    await _context.SaveChangesAsync();

                    // --- BƯỚC 4: XỬ LÝ FILE TRÊN FILESERVER ---
                    if (!Directory.Exists(networkPath)) Directory.CreateDirectory(networkPath);
                    string safeName = RemoveSign4VietnameseString(userName).Replace(" ", "");
                    string timeStamp = DateTime.Now.ToString("ddMMyy_HHmm");

                    // A. File tài liệu đính kèm (UploadFile)
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

                    // B. Xử lý Chi tiết hàng hóa và Ảnh hàng hóa
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

                        chiTiet.Anh = null; // Triệt để không lưu byte[]
                        _context.HrMangHangHoaRaCong2s.Add(chiTiet);
                        await _context.SaveChangesAsync();
                    }

                    // Cập nhật lại FormHr nếu có FileDinhKem
                    _context.Entry(form).State = EntityState.Modified;
                    await _context.SaveChangesAsync();

                    await transaction.CommitAsync();

                    TempData["Success"] = "Gửi đơn mang hàng hóa thành công!";
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

        #region ĐƠN XE ĐI CÔNG TÁC (HR_DangKySuDungXeCongTac_3)

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
        public async Task<IActionResult> DangKySuDungXeCongTac(FormHr form, [FromForm] HrDangKySuDungXeCongTac3 chiTiet)
        {
            if (!User.Identity.IsAuthenticated) return Redirect("/DonXetDuyet/DangNhap");

            // Gia cố Binding nếu chiTiet bị null (đề phòng lỗi truyền từ View)
            if (chiTiet == null || (string.IsNullOrEmpty(chiTiet.LiDo) && Request.Form.ContainsKey("chiTiet.LiDo")))
            {
                chiTiet = new HrDangKySuDungXeCongTac3
                {
                    LiDo = Request.Form["chiTiet.LiDo"]
                };
                if (DateTime.TryParse(Request.Form["chiTiet.TimeDuTinh"], out var time))
                    chiTiet.TimeDuTinh = time;
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
                    form.IdForm = "HR_DangKySuDungXeCongTac_3"; // Mã định danh loại đơn
                    form.TenForm = "Đơn đăng ký sử dụng xe công tác";
                    form.Danhmuc = "ĐƠN ĐĂNG KÝ SỬ DỤNG XE CÔNG TÁC";

                    _context.FormHrs.Add(form);
                    await _context.SaveChangesAsync();

                    // --- BƯỚC 2: TỰ ĐỘNG THÊM NGƯỜI XÁC NHẬN TỪ DANH MỤC ---
                    var loaiDon = await _context.DmLoaiDons
                        .FirstOrDefaultAsync(x => x.MaLoaiDon == form.IdForm && x.TrangThai == true);

                    if (loaiDon != null)
                    {
                        var listCauHinh = await _context.DmNguoiXacNhanLoaiDons
                            .Include(x => x.IdnguoiXacNhanNavigation)
                            .Where(x => x.IdloaiDon == loaiDon.IdloaiDon)
                            .ToListAsync();

                        foreach (var item in listCauHinh)
                        {
                            var hrXacNhan = new HrNguoiXacNhan
                            {
                                IdFormHr = form.Id,
                                IdnguoiXacNhan = item.IdnguoiXacNhan,
                                ThuTuXacNhan = item.CapDoXacNhan, // Cùng cấp độ thì thứ tự bằng nhau
                                MaNguoiXacNhan = item.IdnguoiXacNhanNavigation?.MaNv,
                                TenNguoiXacNhan = item.IdnguoiXacNhanNavigation?.HoTen,
                                TrangThaiXacNhan = 0,
                                ThoiGianXacNhan = null,
                                GhiChu = null
                            };
                            _context.HrNguoiXacNhans.Add(hrXacNhan);
                        }
                        await _context.SaveChangesAsync();
                    }

                    // --- BƯỚC 3: LƯU LỊCH SỬ THAO TÁC ---
                    string chiTietXe = "";
                    if (chiTiet != null)
                    {
                        chiTietXe = $"- Lý do sử dụng: {chiTiet.LiDo}\n" +
                                     $"- Thời gian dự tính: {chiTiet.TimeDuTinh?.ToString("dd/MM/yyyy HH:mm")}";
                    }

                    var lichSu = new LichSuFormHr
                    {
                        IdFormHr = form.Id,
                        TieuDe = "Khởi tạo đơn xe công tác",
                        Mota = $"Nhân viên {userName} ({soNhanVien}) đã tạo đơn đăng ký xe công tác.\n{chiTietXe}",
                        Time = DateTime.Now,
                        IsRead = false
                    };
                    _context.LichSuFormHrs.Add(lichSu);
                    await _context.SaveChangesAsync();

                    // --- BƯỚC 4: XỬ LÝ FILE TRÊN FILESERVER ---
                    if (!Directory.Exists(networkPath)) Directory.CreateDirectory(networkPath);
                    string safeName = RemoveSign4VietnameseString(userName).Replace(" ", "");
                    string timeStamp = DateTime.Now.ToString("ddMMyy_HHmm");

                    // A. File đính kèm tài liệu chung (UploadFile)
                    var uploadFile = Request.Form.Files["UploadFile"];
                    if (uploadFile != null && uploadFile.Length > 0)
                    {
                        string ext = Path.GetExtension(uploadFile.FileName);
                        string fName = $"Doc_Xe_Don{form.Id}_{safeName}_{timeStamp}{ext}";
                        using (var fs = new FileStream(Path.Combine(networkPath, fName), FileMode.Create))
                        {
                            await uploadFile.CopyToAsync(fs);
                        }
                        form.FileDinhKem = fName;
                    }

                    // B. Xử lý Chi tiết đơn & Ảnh đính kèm (AnhXe)
                    if (chiTiet != null)
                    {
                        chiTiet.IdFormHr = form.Id;
                        var anhXe = Request.Form.Files["AnhXe"];
                        if (anhXe != null && anhXe.Length > 0)
                        {
                            string imgExt = Path.GetExtension(anhXe.FileName) ?? ".jpg";
                            string imgName = $"Anh_Xe_Don{form.Id}_{safeName}_{timeStamp}{imgExt}";
                            using (var fs = new FileStream(Path.Combine(networkPath, imgName), FileMode.Create))
                            {
                                await anhXe.CopyToAsync(fs);
                            }
                            chiTiet.DuongDanAnh = imgName;
                        }

                        chiTiet.Anh = null; // Triệt để không lưu byte[] vào DB
                        _context.HrDangKySuDungXeCongTac3s.Add(chiTiet);
                        await _context.SaveChangesAsync();
                    }

                    // Cập nhật lại FormHr nếu có đường dẫn FileDinhKem
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

        #region ĐƠN XE ĐI DangKySuDungXeDaily (HR_DangKySuDungXeDaily_4)

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
                    form.IdForm = "HR_DangKySuDungXeDaily_4"; // Mã định danh loại đơn
                    form.TenForm = "Đăng ký sử dụng xe Daily";

                    _context.FormHrs.Add(form);
                    await _context.SaveChangesAsync();

                    // --- BƯỚC 2: TỰ ĐỘNG THÊM NGƯỜI XÁC NHẬN TỪ DANH MỤC ---
                    var loaiDon = await _context.DmLoaiDons
                        .FirstOrDefaultAsync(x => x.MaLoaiDon == form.IdForm && x.TrangThai == true);

                    if (loaiDon != null)
                    {
                        var listCauHinh = await _context.DmNguoiXacNhanLoaiDons
                            .Include(x => x.IdnguoiXacNhanNavigation)
                            .Where(x => x.IdloaiDon == loaiDon.IdloaiDon)
                            .ToListAsync();

                        foreach (var item in listCauHinh)
                        {
                            var hrXacNhan = new HrNguoiXacNhan
                            {
                                IdFormHr = form.Id,
                                IdnguoiXacNhan = item.IdnguoiXacNhan,
                                ThuTuXacNhan = item.CapDoXacNhan, // Cùng cấp độ thì thứ tự bằng nhau
                                MaNguoiXacNhan = item.IdnguoiXacNhanNavigation?.MaNv,
                                TenNguoiXacNhan = item.IdnguoiXacNhanNavigation?.HoTen,
                                TrangThaiXacNhan = 0,
                                ThoiGianXacNhan = null,
                                GhiChu = null
                            };
                            _context.HrNguoiXacNhans.Add(hrXacNhan);
                        }
                        await _context.SaveChangesAsync();
                    }

                    // --- BƯỚC 3: LƯU LỊCH SỬ THAO TÁC ---
                    string chiTietXeDaily = "";
                    if (chiTiet != null)
                    {
                        chiTietXeDaily = $"- Điểm đón: {chiTiet.DiemDon}\n" +
                                         $"- Lý do: {chiTiet.LiDo}\n" +
                                         $"- Thời gian dự tính: {chiTiet.TimeDuTinh?.ToString("dd/MM/yyyy HH:mm")}";
                    }

                    var lichSu = new LichSuFormHr
                    {
                        IdFormHr = form.Id,
                        TieuDe = "Khởi tạo đơn xe Daily",
                        Mota = $"Nhân viên {userName} ({soNhanVien}) đã tạo đơn đăng ký xe Daily.\n{chiTietXeDaily}",
                        Time = DateTime.Now,
                        IsRead = false
                    };
                    _context.LichSuFormHrs.Add(lichSu);
                    await _context.SaveChangesAsync();

                    // --- BƯỚC 4: XỬ LÝ FILE/ẢNH TRÊN FILESERVER ---
                    if (!Directory.Exists(networkPath)) Directory.CreateDirectory(networkPath);

                    string safeName = RemoveSign4VietnameseString(userName).Replace(" ", "");
                    string timeStamp = DateTime.Now.ToString("ddMMyy_HHmm");

                    // A. File đính kèm tài liệu chung (UploadFile)
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

                    // B. Chi tiết và Ảnh minh chứng (AnhXe)
                    if (chiTiet != null)
                    {
                        chiTiet.IdFormHr = form.Id;

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
                            chiTiet.DuongDanAnh = imgName;
                        }

                        chiTiet.Anh = null; // Triệt để không lưu byte[] vào DB
                        _context.HrDangKySuDungXeDaily4s.Add(chiTiet);
                        await _context.SaveChangesAsync();
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

        #region ĐƠN Tiếp Khách (HR_DonTiepKhac_5)

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
                IdForm = "HR_DonTiepKhac_5",
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
                    form.IdForm = "HR_DonTiepKhac_5"; // Mã định danh để check cấu hình duyệt
                    form.TenForm = "Đơn đăng ký tiếp khách";

                    _context.FormHrs.Add(form);
                    await _context.SaveChangesAsync();

                    // --- BƯỚC 2: TỰ ĐỘNG THÊM NGƯỜI XÁC NHẬN TỪ DANH MỤC ---
                    var loaiDon = await _context.DmLoaiDons
                        .FirstOrDefaultAsync(x => x.MaLoaiDon == form.IdForm && x.TrangThai == true);

                    if (loaiDon != null)
                    {
                        var listCauHinh = await _context.DmNguoiXacNhanLoaiDons
                            .Include(x => x.IdnguoiXacNhanNavigation)
                            .Where(x => x.IdloaiDon == loaiDon.IdloaiDon)
                            .ToListAsync();

                        foreach (var item in listCauHinh)
                        {
                            var hrXacNhan = new HrNguoiXacNhan
                            {
                                IdFormHr = form.Id,
                                IdnguoiXacNhan = item.IdnguoiXacNhan,
                                ThuTuXacNhan = item.CapDoXacNhan, // Cùng cấp độ thì thứ tự bằng nhau
                                MaNguoiXacNhan = item.IdnguoiXacNhanNavigation?.MaNv,
                                TenNguoiXacNhan = item.IdnguoiXacNhanNavigation?.HoTen,
                                TrangThaiXacNhan = 0, // Mặc định: Chờ duyệt
                                ThoiGianXacNhan = null,
                                GhiChu = null
                            };
                            _context.HrNguoiXacNhans.Add(hrXacNhan);
                        }
                        await _context.SaveChangesAsync();
                    }

                    // --- BƯỚC 3: LƯU LỊCH SỬ THAO TÁC (LichSuFormHr) ---
                    string chiTietTiepKhach = "";
                    if (chiTiet != null)
                    {
                        chiTietTiepKhach = $"- Khách từ: {chiTiet.TenCongTyKhach}\n" +
                                            $"- Số lượng: {chiTiet.SoLuongKhach} người\n" +
                                            $"- Yêu cầu: {chiTiet.YeuCauTiepKhach}";
                    }

                    var lichSu = new LichSuFormHr
                    {
                        IdFormHr = form.Id,
                        TieuDe = "Khởi tạo đơn tiếp khách",
                        Mota = $"Nhân viên {userName} ({soNhanVien}) đã tạo đơn đăng ký tiếp khách.\n{chiTietTiepKhach}",
                        Time = DateTime.Now,
                        IsRead = false
                    };
                    _context.LichSuFormHrs.Add(lichSu);
                    await _context.SaveChangesAsync();

                    // Tạo thư mục nếu chưa có
                    if (!Directory.Exists(networkPath)) Directory.CreateDirectory(networkPath);

                    string safeName = RemoveSign4VietnameseString(userName).Replace(" ", "");
                    string timeStamp = DateTime.Now.ToString("ddMMyy_HHmm");

                    // --- BƯỚC 4: XỬ LÝ FILE ĐÍNH KÈM (UploadFile) ---
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
                        form.FileDinhKem = fileName;
                    }

                    // --- BƯỚC 5: XỬ LÝ CHI TIẾT (HrDonTiepKhac5) & ẢNH MINH CHỨNG ---
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
                            chiTiet.DuongDanAnh = imgName;
                        }

                        _context.HrDonTiepKhac5s.Add(chiTiet);
                        await _context.SaveChangesAsync();
                    }

                    // Cập nhật lại trạng thái FormHr nếu có thay đổi (FileDinhKem)
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

        #region ĐƠN NhaThauQuaCong (HR_NhaThauQuaCong_6)

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
                    form.IdForm = "HR_NhaThauQuaCong_6"; // Mã định danh để check cấu hình duyệt
                    form.TenForm = "Đơn đăng ký nhà thầu qua cổng";
                    form.Danhmuc = "ĐƠN ĐĂNG KÝ NHÀ THẦU QUA CỔNG";

                    _context.FormHrs.Add(form);
                    await _context.SaveChangesAsync(); // Lưu để lấy form.Id

                    // --- BƯỚC 2: TỰ ĐỘNG THÊM NGƯỜI XÁC NHẬN TỪ DANH MỤC ---
                    var loaiDon = await _context.DmLoaiDons
                        .FirstOrDefaultAsync(x => x.MaLoaiDon == form.IdForm && x.TrangThai == true);

                    if (loaiDon != null)
                    {
                        var listCauHinh = await _context.DmNguoiXacNhanLoaiDons
                            .Include(x => x.IdnguoiXacNhanNavigation)
                            .Where(x => x.IdloaiDon == loaiDon.IdloaiDon)
                            .ToListAsync();

                        foreach (var item in listCauHinh)
                        {
                            var hrXacNhan = new HrNguoiXacNhan
                            {
                                IdFormHr = form.Id,
                                IdnguoiXacNhan = item.IdnguoiXacNhan,
                                ThuTuXacNhan = item.CapDoXacNhan, // Cùng cấp độ thì thứ tự bằng nhau
                                MaNguoiXacNhan = item.IdnguoiXacNhanNavigation?.MaNv,
                                TenNguoiXacNhan = item.IdnguoiXacNhanNavigation?.HoTen,
                                TrangThaiXacNhan = 0, // Mặc định: Chờ duyệt
                                ThoiGianXacNhan = null,
                                GhiChu = null
                            };
                            _context.HrNguoiXacNhans.Add(hrXacNhan);
                        }
                        await _context.SaveChangesAsync();
                    }

                    // --- BƯỚC 3: XỬ LÝ FILE ĐÍNH KÈM & ẢNH TRÊN FILESERVER ---
                    if (!Directory.Exists(networkPath)) Directory.CreateDirectory(networkPath);

                    string safeName = RemoveSign4VietnameseString(userName).Replace(" ", "");
                    string timeStamp = DateTime.Now.ToString("ddMMyy_HHmm");

                    // A. File tài liệu đính kèm (PDF/Excel) - UploadFile
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

                    // B. Ảnh minh chứng (Paste image/Upload) - Anh
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

                    // --- BƯỚC 4: LƯU CHI TIẾT NHÀ THẦU ---
                    if (chiTiet != null)
                    {
                        chiTiet.IdFormHr = form.Id;
                        chiTiet.Anh = null; // Triệt để không lưu byte[] vào DB

                        _context.HrNhaThauQuaCong6s.Add(chiTiet);
                        await _context.SaveChangesAsync();
                    }

                    // --- BƯỚC 5: LƯU LỊCH SỬ THAO TÁC (LichSuFormHr) ---
                    string chiTietNhaThau = "";
                    if (chiTiet != null)
                    {
                        chiTietNhaThau = $"- Nhà thầu: {chiTiet.TenNhaThau}\n" +
                                         $"- Số người: {chiTiet.SoNguoi}\n" +
                                         $"- Người đăng ký: {chiTiet.NguoiDangKy}\n" +
                                         $"- Mục đích: {chiTiet.MucDichCongViec}";
                    }

                    var lichSu = new LichSuFormHr
                    {
                        IdFormHr = form.Id,
                        TieuDe = "Khởi tạo đơn nhà thầu",
                        Mota = $"Nhân viên {userName} ({soNhanVien}) đã tạo đơn đăng ký nhà thầu qua cổng.\n{chiTietNhaThau}",
                        Time = DateTime.Now,
                        IsRead = false
                    };
                    _context.LichSuFormHrs.Add(lichSu);
                    await _context.SaveChangesAsync();

                    // Cập nhật lại FormHr để lưu FileDinhKem từ Bước 3
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

        #region ĐƠN HoTroTienDienThoai (HR_HoTroTienDienThoai_7)

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
        public async Task<IActionResult> HoTroTienDienThoai(FormHr form, [FromForm] HrHoTroTienDienThoai7 chiTiet)
        {
            // 1. Kiểm tra xác thực
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
                    form.IdForm = "HR_HoTroTienDienThoai_7"; // Mã định danh loại đơn
                    form.TenForm = "Đơn đăng ký hỗ trợ tiền điện thoại";

                    _context.FormHrs.Add(form);
                    await _context.SaveChangesAsync(); // Lưu để lấy form.Id

                    // --- BƯỚC 2: TỰ ĐỘNG THÊM NGƯỜI XÁC NHẬN TỪ DANH MỤC ---
                    var loaiDon = await _context.DmLoaiDons
                        .FirstOrDefaultAsync(x => x.MaLoaiDon == form.IdForm && x.TrangThai == true);

                    if (loaiDon != null)
                    {
                        var listCauHinh = await _context.DmNguoiXacNhanLoaiDons
                            .Include(x => x.IdnguoiXacNhanNavigation)
                            .Where(x => x.IdloaiDon == loaiDon.IdloaiDon)
                            .ToListAsync();

                        foreach (var item in listCauHinh)
                        {
                            var hrXacNhan = new HrNguoiXacNhan
                            {
                                IdFormHr = form.Id,
                                IdnguoiXacNhan = item.IdnguoiXacNhan,
                                ThuTuXacNhan = item.CapDoXacNhan,
                                MaNguoiXacNhan = item.IdnguoiXacNhanNavigation?.MaNv,
                                TenNguoiXacNhan = item.IdnguoiXacNhanNavigation?.HoTen,
                                TrangThaiXacNhan = 0, // Chờ duyệt
                                ThoiGianXacNhan = null,
                                GhiChu = null
                            };
                            _context.HrNguoiXacNhans.Add(hrXacNhan);
                        }
                        await _context.SaveChangesAsync();
                    }

                    // --- BƯỚC 3: XỬ LÝ FILE/ẢNH TRÊN FILESERVER ---
                    if (!Directory.Exists(networkPath)) Directory.CreateDirectory(networkPath);

                    string safeName = RemoveSign4VietnameseString(userName).Replace(" ", "");
                    string timeStamp = DateTime.Now.ToString("ddMMyy_HHmm");

                    // A. Xử lý File đính kèm tài liệu (UploadFile)
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

                    // B. Xử lý Chi tiết đơn (Khớp với Model của bạn)
                    if (chiTiet != null)
                    {
                        chiTiet.IdFormHr = form.Id;

                        // Xử lý Ảnh minh chứng (Anh)
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
                            chiTiet.DuongDanAnh = imgName;
                        }

                        chiTiet.Anh = null; // Triệt để không lưu dữ liệu nhị phân byte[] vào Database
                        _context.HrHoTroTienDienThoai7s.Add(chiTiet);
                        await _context.SaveChangesAsync();
                    }

                    // --- BƯỚC 4: LƯU LỊCH SỬ THAO TÁC (LichSuFormHr) ---
                    string moTaLichSu = $"Nhân viên {userName} ({soNhanVien}) đã tạo đơn hỗ trợ tiền điện thoại.";
                    if (chiTiet != null)
                    {
                        moTaLichSu += $"\n- Mức hỗ trợ: {chiTiet.MucHoTro?.ToString("N0")} VNĐ\n- Mục đích: {chiTiet.MucDich}";
                    }

                    var lichSu = new LichSuFormHr
                    {
                        IdFormHr = form.Id,
                        TieuDe = "Khởi tạo đơn",
                        Mota = moTaLichSu,
                        Time = DateTime.Now,
                        IsRead = false
                    };
                    _context.LichSuFormHrs.Add(lichSu);
                    await _context.SaveChangesAsync();

                    // Cập nhật lại FormHr để lưu FileDinhKem
                    _context.Entry(form).State = Microsoft.EntityFrameworkCore.EntityState.Modified;
                    await _context.SaveChangesAsync();

                    await transaction.CommitAsync();

                    TempData["Success"] = "Gửi đơn hỗ trợ tiền điện thoại thành công!";
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

        #region ĐƠN DoiCaLam (HR_DoiCaLam_8)

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
                    form.Danhmuc = "ĐƠN ĐỔI CA LÀM VIỆC";
                    form.TrangThai = "ChoDuyet";
                    form.IdForm = "HR_DoiCaLam_8"; // Mã định danh loại đơn
                    form.TenForm = "Đơn đăng ký đổi ca làm việc";

                    _context.FormHrs.Add(form);
                    await _context.SaveChangesAsync();

                    // --- BƯỚC 2: TỰ ĐỘNG THÊM NGƯỜI XÁC NHẬN TỪ DANH MỤC ---
                    var loaiDon = await _context.DmLoaiDons
                        .FirstOrDefaultAsync(x => x.MaLoaiDon == form.IdForm && x.TrangThai == true);

                    if (loaiDon != null)
                    {
                        var listCauHinh = await _context.DmNguoiXacNhanLoaiDons
                            .Include(x => x.IdnguoiXacNhanNavigation)
                            .Where(x => x.IdloaiDon == loaiDon.IdloaiDon)
                            .ToListAsync();

                        foreach (var item in listCauHinh)
                        {
                            var hrXacNhan = new HrNguoiXacNhan
                            {
                                IdFormHr = form.Id,
                                IdnguoiXacNhan = item.IdnguoiXacNhan,
                                ThuTuXacNhan = item.CapDoXacNhan,
                                MaNguoiXacNhan = item.IdnguoiXacNhanNavigation?.MaNv,
                                TenNguoiXacNhan = item.IdnguoiXacNhanNavigation?.HoTen,
                                TrangThaiXacNhan = 0, // Chờ duyệt
                                ThoiGianXacNhan = null,
                                GhiChu = null
                            };
                            _context.HrNguoiXacNhans.Add(hrXacNhan);
                        }
                        await _context.SaveChangesAsync();
                    }

                    // --- BƯỚC 3: LƯU LỊCH SỬ THAO TÁC ---
                    string chiTietDoiCa = "";
                    if (chiTiet != null)
                    {
                        chiTietDoiCa = $"- Ngày đổi: {chiTiet.NgayCanDoi?.ToString("dd/MM/yyyy")}\n" +
                                       $"- Đổi từ {chiTiet.CaGoc} sang {chiTiet.CaMuonDoi}\n" +
                                       $"- Lý do: {chiTiet.LyDoDoiCa}";
                    }

                    var lichSu = new LichSuFormHr
                    {
                        IdFormHr = form.Id,
                        TieuDe = "Khởi tạo đơn đổi ca",
                        Mota = $"Nhân viên {userName} ({soNhanVien}) đã tạo đơn đăng ký đổi ca làm việc.\n{chiTietDoiCa}",
                        Time = DateTime.Now,
                        IsRead = false
                    };
                    _context.LichSuFormHrs.Add(lichSu);
                    await _context.SaveChangesAsync();

                    // --- BƯỚC 4: XỬ LÝ FILE/ẢNH TRÊN FILESERVER ---
                    if (!Directory.Exists(networkPath)) Directory.CreateDirectory(networkPath);

                    string safeName = RemoveSign4VietnameseString(userName).Replace(" ", "");
                    string timeStamp = DateTime.Now.ToString("ddMMyy_HHmm");

                    // A. Xử lý File đính kèm tài liệu (UploadFile)
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

                    // B. Xử lý bảng chi tiết và Ảnh minh chứng (Anh)
                    if (chiTiet != null)
                    {
                        chiTiet.IdFormHr = form.Id;

                        var anhFile = Request.Form.Files["Anh"];
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
                            chiTiet.DuongDanAnh = imgName;
                        }

                        chiTiet.Anh = null; // Triệt để không lưu byte[] vào DB
                        _context.HrDoiCaLam8s.Add(chiTiet);
                        await _context.SaveChangesAsync();
                    }

                    // Cập nhật lại FormHr để lưu tên FileDinhKem từ Bước 4
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

        #region Don Ho Tro Cong Tac (HrDonHoTroCongTac9)

        [HttpGet("/FormHR/DonHoTroCongTac")]
        public IActionResult DonHoTroCongTac()
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
                Danhmuc = "ĐƠN HỖ TRỢ CÔNG TÁC",
                Ngay = DateOnly.FromDateTime(DateTime.Now),
                IdNguoiTao = userId,
                TenNguoiTao = userName,
                TimeNguoiTao = DateTime.Now,
                TrangThai = "ChoDuyet"
            };

            return View(model);
        }

        [HttpPost("/FormHR/DonHoTroCongTac")]
        public async Task<IActionResult> DonHoTroCongTac(FormHr form, [FromForm] HrDonHoTroCongTac9 hoTro)
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

            // Đường dẫn FileServer (Giữ nguyên cấu trúc của bạn)
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
                    form.IdForm = "HR_DonHoTroCongTac_9"; // Mã định danh loại đơn
                    form.TenForm = "Đơn hỗ trợ công tác";
                    form.Danhmuc = "ĐƠN HỖ TRỢ CÔNG TÁC";

                    _context.FormHrs.Add(form);
                    await _context.SaveChangesAsync(); // Lưu để lấy form.Id

                    // --- BƯỚC 2: TỰ ĐỘNG THÊM NGƯỜI XÁC NHẬN TỪ DANH MỤC ---
                    var loaiDon = await _context.DmLoaiDons
                        .FirstOrDefaultAsync(x => x.MaLoaiDon == form.IdForm && x.TrangThai == true);

                    if (loaiDon != null)
                    {
                        var listCauHinh = await _context.DmNguoiXacNhanLoaiDons
                            .Include(x => x.IdnguoiXacNhanNavigation)
                            .Where(x => x.IdloaiDon == loaiDon.IdloaiDon)
                            .ToListAsync();

                        foreach (var item in listCauHinh)
                        {
                            var hrXacNhan = new HrNguoiXacNhan
                            {
                                IdFormHr = form.Id,
                                IdnguoiXacNhan = item.IdnguoiXacNhan,
                                ThuTuXacNhan = item.CapDoXacNhan, // Cấp độ duyệt
                                MaNguoiXacNhan = item.IdnguoiXacNhanNavigation?.MaNv,
                                TenNguoiXacNhan = item.IdnguoiXacNhanNavigation?.HoTen,
                                TrangThaiXacNhan = 0, // Mặc định: Chờ duyệt
                                ThoiGianXacNhan = null,
                                GhiChu = null
                            };
                            _context.HrNguoiXacNhans.Add(hrXacNhan);
                        }
                        await _context.SaveChangesAsync();
                    }

                    // --- BƯỚC 3: XỬ LÝ FILE VÀ BẢNG CHI TIẾT (HrDonHoTroCongTac9) ---
                    if (hoTro != null)
                    {
                        hoTro.IdFormHr = form.Id;
                        hoTro.MaNhanVien = soNhanVien;
                        hoTro.NgayTao = DateTime.Now;

                        if (!Directory.Exists(networkPath)) Directory.CreateDirectory(networkPath);
                        string safeName = RemoveSign4VietnameseString(userName).Replace(" ", "");
                        string timeStamp = DateTime.Now.ToString("ddMMyy_HHmm");

                        // A. File đính kèm chính (UploadFile)
                        var uploadFile = Request.Form.Files["UploadFile"];
                        if (uploadFile != null && uploadFile.Length > 0)
                        {
                            string ext = Path.GetExtension(uploadFile.FileName);
                            string fileName = $"File_HTCT_ID{form.Id}_{safeName}_{timeStamp}{ext}";
                            string fullPath = Path.Combine(networkPath, fileName);
                            using (var fs = new FileStream(fullPath, FileMode.Create))
                            {
                                await uploadFile.CopyToAsync(fs);
                            }
                            form.FileDinhKem = fileName;
                        }

                        // B. Ảnh minh chứng (Anh) - Xử lý dán/tải lên
                        var anhFile = Request.Form.Files["Anh"];
                        if (anhFile != null && anhFile.Length > 0)
                        {
                            string imgExt = Path.GetExtension(anhFile.FileName) ?? ".jpg";
                            string imgName = $"Anh_HTCT_ID{form.Id}_{safeName}_{timeStamp}{imgExt}";
                            string imgPath = Path.Combine(networkPath, imgName);
                            using (var fs = new FileStream(imgPath, FileMode.Create))
                            {
                                await anhFile.CopyToAsync(fs);
                            }
                            // Lưu tên file vào DB, không lưu dữ liệu nhị phân để nhẹ database
                            hoTro.DuongDanAnh = imgName;
                        }

                        hoTro.Anh = null; // Triệt để không lưu byte[] vào DB
                        _context.HrDonHoTroCongTac9s.Add(hoTro);
                        await _context.SaveChangesAsync();
                    }

                    // --- BƯỚC 4: LƯU LỊCH SỬ THAO TÁC (LichSuFormHr) ---
                    string chiTietHoTro = "";
                    if (hoTro != null)
                    {
                        var hangMuc = new List<string>();
                        if (hoTro.DatVeMayBay == true) hangMuc.Add("Vé máy bay");
                        if (hoTro.DatChoO == true) hangMuc.Add("Chỗ ở");
                        if (hoTro.BookXeCtyDuaDon == true) hangMuc.Add("Xe đưa đón");
                        if (hoTro.DatBuaAn == true) hangMuc.Add("Đặt bữa ăn");

                        chiTietHoTro = $"\n- Khách hàng: {hoTro.TenKhachHang}\n" +
                                       $"- Hạng mục yêu cầu: {(hangMuc.Count > 0 ? string.Join(", ", hangMuc) : "Không có")}\n" +
                                       $"- Nội dung chi tiết: {hoTro.NoiDungYeuCauChiTiet}";
                    }

                    var lichSu = new LichSuFormHr
                    {
                        IdFormHr = form.Id,
                        TieuDe = "Khởi tạo đơn hỗ trợ công tác",
                        Mota = $"Nhân viên {userName} ({soNhanVien}) đã gửi đơn hỗ trợ công tác.{chiTietHoTro}",
                        Time = DateTime.Now,
                        IsRead = false
                    };
                    _context.LichSuFormHrs.Add(lichSu);
                    await _context.SaveChangesAsync();

                    // Cập nhật lại FormHr để lưu FileDinhKem từ Bước 3
                    _context.Entry(form).State = Microsoft.EntityFrameworkCore.EntityState.Modified;
                    await _context.SaveChangesAsync();

                    await transaction.CommitAsync();

                    TempData["Success"] = "Gửi yêu cầu hỗ trợ thành công!";
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

        #region CHI TIẾT ĐƠN FORM HR (TẤT CẢ 9 LOẠI ĐƠN)
        // Replace or insert these two methods in HRFormController.cs

        [HttpGet("/FormHR/ChiTiet/{id}")]
        public async Task<IActionResult> ChiTiet(int id)
        {
            var userIdStr = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userIdStr))
                return RedirectToAction("DangNhap", "DonXetDuyet");

            int userId = int.Parse(userIdStr);
            var userEmail = User.FindFirst(System.Security.Claims.ClaimTypes.Email)?.Value ?? "";

            var don = await _context.FormHrs
                .Include(f => f.HrXinRaNgoai1s)
                .Include(f => f.HrMangHangHoaRaCong2s)
                .Include(f => f.HrDangKySuDungXeCongTac3s)
                .Include(f => f.HrDangKySuDungXeDaily4s)
                .Include(f => f.HrDonTiepKhac5s)
                .Include(f => f.HrNhaThauQuaCong6s)
                .Include(f => f.HrHoTroTienDienThoai7s)
                .Include(f => f.HrDoiCaLam8s)
                .Include(f => f.HrDonHoTroCongTac9s)
                .Include(f => f.HrCtNguoiHoTros)
                    .ThenInclude(ct => ct.IdHrNguoiHoTroNavigation)
                .Include(f => f.HrNguoiXacNhans)
                    .ThenInclude(x => x.IdnguoiXacNhanNavigation)
                .Include(f => f.LichSuFormHrs)
                .Include(f => f.BinhLuanFormHrs)
                .FirstOrDefaultAsync(m => m.Id == id);

            if (don == null)
            {
                TempData["Error"] = "⚠️ Không tìm thấy đơn yêu cầu HR!";
                return RedirectToAction("LichSuHR");
            }       

            if (don.LichSuFormHrs != null)
                don.LichSuFormHrs = don.LichSuFormHrs.OrderByDescending(x => x.Time).ToList();

            ViewBag.ListNguoiHoTro = await _context.HrNguoiHoTros
                .Where(x => x.BoPhan == "HR").ToListAsync();

            ViewBag.CurrentUserId = userId;
            ViewBag.UserEmail = userEmail;
            // Thêm dòng này vào cuối action ChiTiet, trước return View(don);
            ViewBag.BaoVeRecord = await _context.BaoVeHrs
                .FirstOrDefaultAsync(x => x.IdFormHr == id);

            ViewBag.DanhSachXacNhan = don.HrNguoiXacNhans?
                .OrderBy(x => x.ThuTuXacNhan)
                .ToList() ?? new List<HrNguoiXacNhan>();

            return View(don);
        }

  
        // ============================================================
        // ACTION XỬ LÝ FILE TẢI VỀ VÀ ẢNH HIỂN THỊ
        // ============================================================
        [HttpGet("/FormHR/DownloadFile/{fileName}")]
        public async Task<IActionResult> DownloadFile(string fileName)
        {
            if (string.IsNullOrEmpty(fileName)) return NotFound();

            string networkPath = @"\\10.0.60.30\BPVN-Fileserver\Public\IT-Information Technology Dept\5.E-Form\DonHR";
            string fullPath = Path.Combine(networkPath, fileName);

            if (!System.IO.File.Exists(fullPath))
                return NotFound("Tệp tin không tồn tại trên hệ thống lưu trữ.");

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
                ".bmp" => "image/bmp",
                ".pdf" => "application/pdf",
                ".xlsx" => "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                ".xls" => "application/vnd.ms-excel",
                ".docx" => "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
                ".doc" => "application/msword",
                _ => "application/octet-stream"
            };

            bool isImage = contentType.StartsWith("image/");

            return isImage
                ? File(memory, contentType)
                : File(memory, contentType, fileName);
        }

        [HttpPost("/FormHR/ThemNguoiHoTro")]
        public async Task<IActionResult> ThemNguoiHoTro([FromBody] System.Text.Json.JsonElement data)
        {
            try
            {
                int idForm = data.GetProperty("idFormHr").GetInt32();
                string maNvMoi = data.GetProperty("maNv").GetString();

                var nvHr = await _context.HrNguoiHoTros.FirstOrDefaultAsync(x => x.MaNv == maNvMoi);
                if (nvHr == null)
                {
                    return Json(new { success = false, message = "Mã nhân viên HR không tồn tại!" });
                }

                var hienTai = await _context.HrCtNguoiHoTros
                    .Include(x => x.IdHrNguoiHoTroNavigation)
                    .Where(x => x.IdFormHr == idForm)
                    .OrderByDescending(x => x.Stt)
                    .FirstOrDefaultAsync();

                if (hienTai != null && hienTai.IdHrNguoiHoTroNavigation?.MaNv == maNvMoi)
                {
                    return Json(new
                    {
                        success = false,
                        message = $"Nhân viên {nvHr.Ten} hiện đang là người xử lý cuối cùng. Vui lòng chọn người khác!"
                    });
                }

                int sttMoi = (hienTai?.Stt ?? 0) + 1;

                var ctMoi = new HrCtNguoiHoTro
                {
                    IdFormHr = idForm,
                    IdHrNguoiHoTro = nvHr.Id,
                    Stt = sttMoi
                };
                _context.HrCtNguoiHoTros.Add(ctMoi);

                var lichSu = new LichSuFormHr
                {
                    IdFormHr = idForm,
                    TieuDe = "CHỈ ĐỊNH NGƯỜI HỖ TRỢ MỚI",
                    Mota = $"Nhân viên {User.Identity.Name} đã thay đổi người hỗ trợ sang: {nvHr.Ten} ({nvHr.MaNv}).",
                    Time = DateTime.Now
                };
                _context.LichSuFormHrs.Add(lichSu);

                await _context.SaveChangesAsync();
                return Json(new { success = true, message = "Đã cập nhật người hỗ trợ mới!" });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = "Lỗi: " + ex.Message });
            }
        }

        #endregion

        #region BÌNH LUẬN ĐƠN HR

        [HttpGet("/FormHR/LayBinhLuan/{idForm}")]
        public async Task<IActionResult> LayBinhLuan(int idForm, int skip = 0, int take = 20)
        {
            try
            {
                var binhLuans = await _context.BinhLuanFormHrs
                    .Where(bl => bl.IdFormHr == idForm && bl.TrangThai == 1)
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

                binhLuans.Reverse();

                return Json(new { success = true, data = binhLuans });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = ex.Message });
            }
        }

        [HttpPost("/FormHR/ThemBinhLuan")]
        public async Task<IActionResult> ThemBinhLuan()
        {
            try
            {
                var idForm = int.Parse(Request.Form["idForm"]);
                var noiDung = Request.Form["noiDung"].ToString();
                var file = Request.Form.Files.GetFile("file");

                var userIdStr = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                var userName = User.Identity?.Name ?? "Unknown";
                var userMa = User.FindFirst(ClaimTypes.Email)?.Value ?? "";
                var userPhongBan = User.FindFirst("PhongBan")?.Value ?? "";
                var userTenCongTy = User.FindFirst("TenCongTy")?.Value ?? "";

                if (string.IsNullOrEmpty(userIdStr))
                    return Json(new { success = false, message = "Chưa đăng nhập" });

                var formHr = await _context.FormHrs.FindAsync(idForm);
                if (formHr == null)
                    return Json(new { success = false, message = "Không tìm thấy đơn" });

                string fileName = null;

                if (file != null && file.Length > 0)
                {
                    if (file.Length > 50 * 1024 * 1024)
                        return Json(new { success = false, message = "File không được vượt quá 50MB" });

                    string networkPath = @"\\10.0.60.30\BPVN-Fileserver\Public\IT-Information Technology Dept\5.E-Form\BinhLuanDonHR";

                    try
                    {
                        if (!Directory.Exists(networkPath))
                            Directory.CreateDirectory(networkPath);
                    }
                    catch (Exception ex)
                    {
                        return Json(new { success = false, message = "Không thể truy cập thư mục lưu trữ: " + ex.Message });
                    }

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

                if (string.IsNullOrWhiteSpace(noiDung) && file == null)
                    return Json(new { success = false, message = "Vui lòng nhập nội dung hoặc đính kèm file" });

                // Parse IdNguoiBinhLuan to int
                int idNguoiBinhLuan = 0;
                if (!string.IsNullOrEmpty(userIdStr))
                {
                    int.TryParse(userIdStr, out idNguoiBinhLuan);
                }

                var binhLuan = new BinhLuanFormHr
                {
                    IdFormHr = idForm,
                    NoiDung = noiDung?.Trim(),
                    IdNguoiBinhLuan = idNguoiBinhLuan,
                    TenNguoiBinhLuan = userName,
                    Ma = userMa,
                    PhongBan = userPhongBan,
                    TenCongTy = userTenCongTy,
                    ThoiGian = DateTime.Now,
                    TrangThai = 1,
                    FileDinhKem = fileName
                };

                _context.BinhLuanFormHrs.Add(binhLuan);
                await _context.SaveChangesAsync();

                var lichSu = new LichSuFormHr
                {
                    IdFormHr = idForm,
                    TieuDe = "BÌNH LUẬN MỚI",
                    Mota = $"👤 {userName} ({userMa})\n" +
                           $"🏢 {userPhongBan} - {userTenCongTy}\n" +
                           $"💬 {(string.IsNullOrWhiteSpace(noiDung) ? "[File đính kèm]" : (noiDung.Length > 50 ? noiDung.Substring(0, 50) + "..." : noiDung))}\n" +
                           $"{(fileName != null ? "📎 Có đính kèm file" : "")}",
                    Time = DateTime.Now
                };
                _context.LichSuFormHrs.Add(lichSu);
                await _context.SaveChangesAsync();

                return Json(new
                {
                    success = true,
                    data = new
                    {
                        id = binhLuan.Id,
                        noiDung = binhLuan.NoiDung,
                        tenNguoiBinhLuan = binhLuan.TenNguoiBinhLuan,
                        idNguoiBinhLuan = binhLuan.IdNguoiBinhLuan?.ToString(),
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

        [HttpPost("/FormHR/XoaBinhLuan")]
        public async Task<IActionResult> XoaBinhLuan([FromBody] dynamic data)
        {
            try
            {
                int id = (int)data.id;
                var userIdStr = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                var userRole = User.FindFirst("UserRole")?.Value ?? "";

                var binhLuan = await _context.BinhLuanFormHrs.FindAsync(id);
                if (binhLuan == null)
                    return Json(new { success = false, message = "Không tìm thấy bình luận" });

                // So sánh với int
                int currentUserId = 0;
                if (!string.IsNullOrEmpty(userIdStr))
                {
                    int.TryParse(userIdStr, out currentUserId);
                }

                if (binhLuan.IdNguoiBinhLuan != currentUserId && userRole != "AdminHR" && userRole != "All")
                    return Json(new { success = false, message = "Bạn không có quyền xóa bình luận này" });

                if (!string.IsNullOrEmpty(binhLuan.FileDinhKem))
                {
                    string networkPath = @"\\10.0.60.30\BPVN-Fileserver\Public\IT-Information Technology Dept\5.E-Form\BinhLuanDonHR";
                    string fullPath = Path.Combine(networkPath, binhLuan.FileDinhKem);

                    try
                    {
                        if (System.IO.File.Exists(fullPath))
                            System.IO.File.Delete(fullPath);
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Không thể xóa file: {ex.Message}");
                    }
                }

                _context.BinhLuanFormHrs.Remove(binhLuan);
                await _context.SaveChangesAsync();

                var lichSu = new LichSuFormHr
                {
                    IdFormHr = binhLuan.IdFormHr,
                    TieuDe = "XÓA BÌNH LUẬN",
                    Mota = $"{User.Identity.Name} đã xóa bình luận của {binhLuan.TenNguoiBinhLuan}",
                    Time = DateTime.Now
                };
                _context.LichSuFormHrs.Add(lichSu);
                await _context.SaveChangesAsync();

                return Json(new { success = true, message = "Đã xóa bình luận" });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = "Lỗi hệ thống: " + ex.Message });
            }
        }

        [HttpGet("/FormHR/DownloadBinhLuanFile/{fileName}")]
        public async Task<IActionResult> DownloadBinhLuanFile(string fileName)
        {
            if (string.IsNullOrEmpty(fileName))
                return NotFound();

            string networkPath = @"\\10.0.60.30\BPVN-Fileserver\Public\IT-Information Technology Dept\5.E-Form\BinhLuanDonHR";
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

                string originalFileName = string.Join("_", fileName.Split('_').Skip(2));

                return File(memory, contentType, originalFileName);
            }
            catch (Exception ex)
            {
                return StatusCode(500, "Lỗi khi tải file: " + ex.Message);
            }
        }

        #endregion

        #region ĐƠN CHỜ XÉT DUYỆT (Dành cho nhân viên xem danh sách đơn đã tạo)

        // GET: /FormHR/DonCho
        [HttpGet("/FormHR/DonCho")]
        public async Task<IActionResult> DonCho()
        {
            // 1. Kiểm tra xác thực qua Cookie Authentication
            // Đảm bảo User đã đăng nhập, nếu chưa trả về trang đăng nhập
            if (User == null || User.Identity == null || !User.Identity.IsAuthenticated)
            {
                return Redirect("/DonXetDuyet/DangNhap");
            }

            // 2. Lấy UserId từ Claims (NameIdentifier)
            // Lưu ý: Đảm bảo khi Đăng nhập bạn đã lưu ClaimTypes.NameIdentifier là ID của nhân viên
            var userIdClaim = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value
                              ?? User.FindFirst("UserId")?.Value;

            if (string.IsNullOrEmpty(userIdClaim))
            {
                // Nếu không tìm thấy ID người dùng trong Identity, yêu cầu đăng nhập lại
                return Redirect("/DonXetDuyet/DangNhap");
            }

            try
            {
                int currentUserId = int.Parse(userIdClaim);

                // 3. Truy vấn danh sách đơn của chính người tạo
                // Sử dụng Eager Loading (.Include) để lấy dữ liệu các bảng liên quan trong 1 lần truy vấn
                var danhSachDon = await _context.FormHrs
                    .Include(f => f.HrNguoiXacNhans) // Để hiển thị danh sách người xác nhận & trạng thái (1/3, 2/3...)
                        .ThenInclude(xn => xn.IdnguoiXacNhanNavigation) // Lấy thông tin tên người xác nhận nếu cần
                    .Include(f => f.BaoVeHrs)       // Để hiển thị trạng thái Bảo vệ đã duyệt hay chưa
                    .Where(f => f.IdNguoiTao == currentUserId)
                    .OrderByDescending(f => f.Id)   // Đơn mới nhất lên đầu
                    .AsNoTracking()                 // Tối ưu hiệu năng vì đây là trang chỉ xem (Read-only)
                    .ToListAsync();

                // 4. Trả về View cùng danh sách dữ liệu
                // View này sẽ sử dụng @model IEnumerable<E_Form_Best.Models.ITForm.FormHr>
                return View(danhSachDon);
            }
            catch (Exception ex)
            {
                // Ghi log lỗi nếu cần thiết
                TempData["Error"] = "Có lỗi xảy ra khi tải danh sách đơn: " + ex.Message;
                return View(new List<FormHr>());
            }
        }

        #endregion

        #region XỬ LÝ ĐƠN HR (Duyệt / Hủy / Hoàn tất) - PHÂN QUYỀN MỚI 2026

        // GET: /FormHR/QuanLyXetDuyet
        [HttpGet("/FormHR/QuanLyXetDuyet")]
        public async Task<IActionResult> QuanLyXetDuyet()
        {
            // --- 1. LẤY THÔNG TIN TỪ CLAIMS ---
            var userIdStr = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userIdStr)) return Redirect("/DonXetDuyet/DangNhap");
            int userId = int.Parse(userIdStr);

            // Lấy tất cả các Roles từ Claims để kiểm tra đa quyền
            var userRoles = User.FindAll(System.Security.Claims.ClaimTypes.Role)
                                .Select(c => c.Value.Trim().ToUpper())
                                .ToList();

            var phongBan = User.FindFirst("PhongBan")?.Value?.Trim() ?? "";
            var tenCongTy = User.FindFirst("TenCongTy")?.Value?.Trim() ?? "";

            // Lấy danh sách bộ phận được quản lý
            var listTenBoPhanStr = User.FindFirst("TenBoPhan")?.Value ?? "";
            var listTenBoPhan = listTenBoPhanStr.Split(',')
                                                .Select(s => s.Trim().ToLower())
                                                .Where(s => !string.IsNullOrEmpty(s))
                                                .ToList();

            // Chặn Bảo vệ - Nếu role là BAOVE thì không cho vào trang quản lý chung này
            if (userRoles.Contains("BAOVE")) return Forbid();

            // --- 2. TRUY VẤN DỮ LIỆU ---
            // Tích hợp Include để lấy thông tin xác nhận, bảo vệ và người hỗ trợ
            IQueryable<FormHr> query = _context.FormHrs
                .Include(f => f.HrNguoiXacNhans)
                .Include(f => f.BaoVeHrs)
                .Include(f => f.HrCtNguoiHoTros)
                    .ThenInclude(ct => ct.IdHrNguoiHoTroNavigation);

            // --- 3. LOGIC PHÂN QUYỀN MỚI: ƯU TIÊN QUYỀN "ALL" NHÌN THẤY HẾT ---

            bool isAll = userRoles.Contains("ALL");
            bool isAdminHR = userRoles.Contains("ADMINHR");
            bool isQuanLy = userRoles.Contains("QUANLYDUYETDONHR") || userRoles.Contains("QUANLY");

            if (isAll)
            {
                // [QUYỀN CAO NHẤT]: Không lọc gì cả, nhìn thấy toàn bộ đơn của tất cả các công ty
            }
            else
            {
                // CÁC QUYỀN CÒN LẠI: Bắt buộc lọc theo Công ty của người dùng đang đăng nhập
                if (!string.IsNullOrEmpty(tenCongTy))
                {
                    query = query.Where(f => f.TenCongTy == tenCongTy);
                }

                if (isAdminHR)
                {
                    // AdminHR: Xem toàn bộ đơn trong công ty của mình (Đã lọc công ty ở trên)
                }
                else if (isQuanLy)
                {
                    // Quản lý: Lọc theo danh sách bộ phận được phân công quản lý
                    if (listTenBoPhan.Any())
                    {
                        query = query.Where(f => f.BoPhan != null && listTenBoPhan.Contains(f.BoPhan.Trim().ToLower()));
                    }
                    // Nếu không có danh sách cụ thể, lọc theo phòng ban của bản thân
                    else if (!string.IsNullOrEmpty(phongBan))
                    {
                        query = query.Where(f => f.BoPhan != null && f.BoPhan.Trim().ToLower() == phongBan.ToLower());
                    }
                    else
                    {
                        // Trường hợp không xác định được phòng ban thì chỉ xem đơn mình tạo
                        query = query.Where(f => f.IdNguoiTao == userId);
                    }
                }
                else
                {
                    // Nhân viên thường (User): Chỉ xem đơn của chính mình tạo ra
                    query = query.Where(f => f.IdNguoiTao == userId);
                }
            }

            // Thực thi truy vấn và trả về danh sách
            var danhSachDon = await query
                .OrderByDescending(f => f.Id)
                .AsNoTracking() // Tăng hiệu suất cho truy vấn chỉ đọc
                .ToListAsync();

            return View(danhSachDon);
        }


        [HttpPost("/FormHR/XuLyDon")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> XuLyDon([FromBody] HRApprovalRequest request)
        {
            if (request == null || request.Id <= 0)
                return Json(new { success = false, message = "Dữ liệu không hợp lệ." });

            var userIdStr = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userIdStr))
                return Json(new { success = false, message = "Hết phiên đăng nhập." });

            int userId = int.Parse(userIdStr);
            var userName = User.FindFirst(System.Security.Claims.ClaimTypes.Name)?.Value ?? "N/A";
            var userEmail = User.FindFirst(System.Security.Claims.ClaimTypes.Email)?.Value ?? "";
            var tenCongTyUser = User.FindFirst("TenCongTy")?.Value?.Trim() ?? "";
            var phongBanUser = User.FindFirst("PhongBan")?.Value ?? "N/A";

            var form = await _context.FormHrs
                .Include(f => f.HrCtNguoiHoTros)
                    .ThenInclude(ct => ct.IdHrNguoiHoTroNavigation)
                .FirstOrDefaultAsync(f => f.Id == request.Id);

            if (form == null) return Json(new { success = false, message = "Không tìm thấy đơn." });

            // KIỂM TRA BẢO MẬT: Quyền "ALL" có thể xử lý mọi đơn, các quyền khác phải cùng công ty
            if (!User.IsInRole("All") && form.TenCongTy?.Trim() != tenCongTyUser)
            {
                return Json(new { success = false, message = "Bạn không có quyền thao tác trên đơn của công ty khác." });
            }

            using (var transaction = await _context.Database.BeginTransactionAsync())
            {
                try
                {
                    DateTime now = DateTime.Now;
                    string tieuDeLichSu = "";
                    string moTaChiTiet = "";

                    if (request.Action == "Duyet")
                    {
                        if (!User.IsInRole("All") && !User.IsInRole("AdminHR") && !User.IsInRole("QuanLyDuyetDonHR"))
                        {
                            return Json(new { success = false, message = "Bạn không có quyền phê duyệt đơn này." });
                        }

                        form.IdNguoiDuyet = userId;
                        form.TenNguoiDuyet = userName;
                        form.TimeNguoiDuyet = now;
                        form.TrangThai = "DaDuyet";

                        tieuDeLichSu = "Phê duyệt đơn HR";
                        moTaChiTiet = $"Người duyệt: {userName}. Bộ phận xử lý: {phongBanUser}.";
                    }
                    else if (request.Action == "Huy")
                    {
                        bool isOwner = form.IdNguoiTao == userId;
                        bool canApprove = User.IsInRole("All") || User.IsInRole("AdminHR") || User.IsInRole("QuanLyDuyetDonHR");

                        if (!isOwner && !canApprove)
                        {
                            return Json(new { success = false, message = "Bạn không có quyền hủy đơn này." });
                        }

                        form.TrangThai = "DaHuy";
                        if (form.TenForm != null && !form.TenForm.StartsWith("[ĐÃ HỦY]"))
                            form.TenForm = "[ĐÃ HỦY] " + form.TenForm;

                        tieuDeLichSu = "Hủy đơn HR";
                        moTaChiTiet = $"Người hủy: {userName}. Lý do: {request.Reason}.";
                    }
                    else if (request.Action == "HoanTat")
                    {
                        bool isAdmin = User.IsInRole("All") || User.IsInRole("AdminHR");
                        bool isSupporter = form.HrCtNguoiHoTros.Any(ct => ct.IdHrNguoiHoTroNavigation?.MaNv == userEmail);

                        if (!isAdmin && !isSupporter)
                        {
                            return Json(new { success = false, message = "Chỉ nhân sự được gán hỗ trợ hoặc Admin mới có thể hoàn tất." });
                        }

                        form.IdAdmin = userId;
                        form.TenAdmin = userName;
                        form.TimeAdmin = now;
                        form.TrangThai = "HoanTat";

                        tieuDeLichSu = "Hoàn tất đơn HR";
                        moTaChiTiet = $"Đơn đã được xử lý xong bởi: {userName}.";
                    }

                    // Lưu lịch sử thao tác
                    _context.LichSuFormHrs.Add(new LichSuFormHr
                    {
                        IdFormHr = form.Id,
                        TieuDe = tieuDeLichSu,
                        Mota = moTaChiTiet,
                        Time = now
                    });

                    _context.FormHrs.Update(form);
                    await _context.SaveChangesAsync();
                    await transaction.CommitAsync();

                    return Json(new { success = true, message = tieuDeLichSu });
                }
                catch (Exception ex)
                {
                    await transaction.RollbackAsync();
                    return Json(new { success = false, message = "Lỗi hệ thống: " + ex.Message });
                }
            }
        }
        #endregion

        #region QUẢN LÝ PHÊ DUYỆT HR (Admin, All, Quản lý)

        /// <summary>
        /// GET: /FormHR/HoanTatDon
        /// Trang hiển thị danh sách đơn dành cho cấp quản lý và admin xử lý
        /// </summary>
        [HttpGet("/FormHR/HoanTatDon")]
        public async Task<IActionResult> HoanTatDon()
        {
            // 1. Kiểm tra xác thực qua Cookie Authentication
            if (User == null || User.Identity == null || !User.Identity.IsAuthenticated)
            {
                return Redirect("/DonXetDuyet/DangNhap");
            }

            // 2. Lấy thông tin từ Claims (Id, Role, PhongBan)
            var userIdString = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value
                               ?? User.FindFirst("UserId")?.Value;

            if (string.IsNullOrEmpty(userIdString)) return Redirect("/DonXetDuyet/DangNhap");

            int userId = int.Parse(userIdString);

            // Lấy Role và chuẩn hóa để so sánh
            var userRole = (User.FindFirst(System.Security.Claims.ClaimTypes.Role)?.Value
                           ?? User.FindFirst("UserRole")?.Value
                           ?? "GUEST").Trim().ToUpper();

            // Lấy thông tin phòng ban của người đang đăng nhập
            var userPhongBan = (User.FindFirst("PhongBan")?.Value
                               ?? User.FindFirst("Department")?.Value
                               ?? "").Trim();

            // 3. Khởi tạo Query với Eager Loading (Include các bảng liên quan)
            // Cần Include để hiển thị đúng badge trạng thái ở View
            IQueryable<FormHr> query = _context.FormHrs
                .Include(f => f.HrNguoiXacNhans)
                .Include(f => f.BaoVeHrs);

            // 4. Phân quyền lọc dữ liệu (GIỮ NGUYÊN TOÀN BỘ NHÓM QUYỀN CỦA BẠN)
            if (userRole == "ALL" || userRole == "ADMIN" || userRole == "ADMINHR" || userRole == "HR")
            {
                // Nhóm quản trị/HR: Xem toàn bộ các đơn đã được Quản lý bộ phận duyệt (hoặc tất cả tùy logic)
                // Thông thường Admin HR xem các đơn đã qua bước Quản lý duyệt (IdNguoiDuyet != null)
                query = query.Where(f => f.IdNguoiDuyet != null || (f.TenForm ?? "").Contains("[ĐÃ HỦY]"));
            }
            else if (userRole == "QUANLY" || userRole == "QUANLYDUYETDONHR" || userRole == "MANAGER")
            {
                // Quản lý: Xem đơn của bộ phận mình phụ trách
                if (!string.IsNullOrEmpty(userPhongBan))
                {
                    // So khớp phòng ban (không phân biệt hoa thường)
                    query = query.Where(f => f.BoPhan != null && f.BoPhan.Trim().ToLower() == userPhongBan.ToLower());
                }
                else
                {
                    // Nếu quản lý nhưng không có thông tin phòng ban trong claim, chỉ cho xem đơn của chính mình
                    query = query.Where(f => f.IdNguoiTao == userId);
                }
            }
            else
            {
                // Nhân viên bình thường: Chỉ xem đơn do chính mình tạo
                query = query.Where(f => f.IdNguoiTao == userId);
            }

            // 5. Thực thi truy vấn, sắp xếp và tối ưu hiệu năng
            try
            {
                var danhSachDon = await query
                    .OrderByDescending(f => f.Id) // Đơn mới nhất lên đầu
                    .AsNoTracking()               // Tăng tốc độ truy vấn vì chỉ để hiển thị
                    .ToListAsync();

                return View(danhSachDon);
            }
            catch (Exception ex)
            {
                // Xử lý lỗi nếu truy vấn database thất bại
                TempData["Error"] = "Lỗi tải dữ liệu: " + ex.Message;
                return View(new List<FormHr>());
            }
        }

        /// <summary>
        /// POST: /FormHR/XacNhanHoanThanh
        /// <summary>
        /// Nút bấm dành cho Đội HR - Xác nhận đã xử lý xong các thủ tục/hồ sơ
        /// </summary>
        [HttpPost("/FormHR/XacNhanHoanThanh")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> XacNhanHoanThanh([FromBody] HRCompleteRequest request)
        {
            // 1. Kiểm tra đầu vào
            if (request == null || request.Id <= 0)
                return Json(new { success = false, message = "Dữ liệu không hợp lệ." });

            // 2. Thông tin người thao tác từ Claims
            var userIdStr = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userIdStr))
                return Json(new { success = false, message = "Phiên đăng nhập đã hết hạn." });

            int userId = int.Parse(userIdStr);
            var userName = User.FindFirst(System.Security.Claims.ClaimTypes.Name)?.Value ?? "N/A";
            var userEmail = User.FindFirst(System.Security.Claims.ClaimTypes.Email)?.Value ?? "";
            var tenCongTy = User.FindFirst("TenCongTy")?.Value?.Trim() ?? "";
            var phongBan = User.FindFirst("PhongBan")?.Value ?? "N/A";

            // 3. Tìm đơn HR và Include thông tin hỗ trợ để kiểm tra quyền chính xác
            var form = await _context.FormHrs
                .Include(f => f.HrCtNguoiHoTros)
                    .ThenInclude(ct => ct.IdHrNguoiHoTroNavigation)
                .FirstOrDefaultAsync(f => f.Id == request.Id);

            if (form == null)
                return Json(new { success = false, message = "Không tìm thấy đơn HR." });

            // 4. Kiểm tra bảo mật: Phải cùng công ty mới được thao tác (Trừ quyền All)
            if (form.TenCongTy?.Trim() != tenCongTy && !User.IsInRole("All"))
            {
                return Json(new { success = false, message = "Bạn không có quyền thao tác trên đơn của công ty khác." });
            }

            // 5. Kiểm tra quyền thực hiện: AdminHR, Role "All" hoặc là Người hỗ trợ được gán trực tiếp
            bool isAdmin = User.IsInRole("All") || User.IsInRole("AdminHR") || User.IsInRole("ADMIN");
            bool isSupporter = form.HrCtNguoiHoTros.Any(ct => ct.IdHrNguoiHoTroNavigation?.MaNv == userEmail);

            if (!isAdmin && !isSupporter)
            {
                return Json(new { success = false, message = "Chỉ nhân sự được gán hỗ trợ hoặc Admin mới có thể hoàn tất đơn này." });
            }

            // 6. Kiểm tra trạng thái đơn (Tránh hoàn tất đơn đã hủy hoặc đã xong)
            bool isCancelled = (form.TenForm != null && form.TenForm.Contains("[ĐÃ HỦY]"));
            if (form.TrangThai == "HoanTat" || isCancelled)
            {
                return Json(new { success = false, message = "Đơn này đã ở trạng thái hoàn tất hoặc đã bị hủy." });
            }

            // 7. Bắt đầu Transaction để đảm bảo tính toàn vẹn dữ liệu
            using (var transaction = await _context.Database.BeginTransactionAsync())
            {
                try
                {
                    DateTime now = DateTime.Now;

                    // 8. Cập nhật thông tin HR xử lý
                    form.IdAdmin = userId;
                    form.TenAdmin = userName;
                    form.TimeAdmin = now;
                    form.TrangThai = "HoanTat";

                    // 9. Lưu Lịch sử thao tác (LichSuFormHr)
                    var lichSu = new LichSuFormHr
                    {
                        IdFormHr = form.Id,
                        TieuDe = "HR Xác nhận Hoàn tất",
                        Mota = $"Nhân sự thực hiện: {userName} ({userEmail}). " +
                               $"Bộ phận: {phongBan}. " +
                               $"Nội dung: Đã xử lý hoàn tất các thủ tục/yêu cầu.",
                        Time = now
                    };

                    _context.LichSuFormHrs.Add(lichSu);
                    _context.FormHrs.Update(form);

                    // 10. Lưu DB
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

        // --- DÀNH RIÊNG CHO HR ---
        public class HRApprovalRequest
        {
            public int Id { get; set; }
            public string Action { get; set; }
            public string Reason { get; set; }
        }

        public class HRCompleteRequest
        {
            public int Id { get; set; }
            public string HRNote { get; set; }
        }

        #endregion

        #region PHÊ DUYỆT CẤP CAO (GIÁM ĐỐC)

        /// <summary>
        /// GET: /FormHR/PheDuyetCapCao
        /// </summary>
        /// <summary>
        /// GET: /FormHR/PheDuyetCapCao
        /// Trang dành cho Ban Giám Đốc hoặc Quản trị cấp cao phê duyệt các loại đơn đặc thù
        /// </summary>
        [HttpGet("/FormHR/PheDuyetCapCao")]
        public async Task<IActionResult> PheDuyetCapCao()
        {
            // 1. Kiểm tra xác thực qua Cookie Authentication
            if (User == null || User.Identity == null || !User.Identity.IsAuthenticated)
            {
                return Redirect("/DonXetDuyet/DangNhap");
            }

            // 2. LẤY TOÀN BỘ DANH SÁCH QUYỀN (ROLES)
            // Thu thập từ cả hệ thống Claim chuẩn và Custom Claim của bạn
            var roles = User.FindAll(System.Security.Claims.ClaimTypes.Role)
                            .Select(r => r.Value.Trim().ToUpper())
                            .ToList();

            var userRoleCustom = (User.FindFirst("UserRole")?.Value ?? "").Trim().ToUpper();
            if (!string.IsNullOrEmpty(userRoleCustom) && !roles.Contains(userRoleCustom))
            {
                roles.Add(userRoleCustom);
            }

            // 3. KIỂM TRA QUYỀN TRUY CẬP (Yêu cầu ít nhất 1 trong các quyền cấp cao)
            bool isAll = roles.Contains("ALL") || roles.Contains("ADMIN");
            bool isGiamDoc = roles.Contains("GIAMDOCHR") || roles.Contains("DIRECTOR");

            if (!isAll && !isGiamDoc)
            {
                return Content("Tài khoản của bạn không có quyền truy cập trang phê duyệt cấp cao.");
            }

            // 4. Lấy thông tin định danh (Dùng Email hoặc MaNV làm định danh người duyệt)
            var userEmail = User.FindFirst(System.Security.Claims.ClaimTypes.Email)?.Value
                            ?? User.FindFirst("Email")?.Value ?? "";

            if (string.IsNullOrEmpty(userEmail))
            {
                return Content("Không xác định được thông tin định danh (Email) người duyệt.");
            }

            // 5. Lấy danh sách Mã Loại Đơn mà User này được phép xác nhận (Phân quyền theo bảng cấu hình)
            var dsMaLoaiDon = await _context.DmNguoiXacNhanLoaiDons
                .Where(rel => rel.IdnguoiXacNhanNavigation.MaNv == userEmail)
                .Select(rel => rel.IdloaiDonNavigation.MaLoaiDon)
                .Distinct()
                .ToListAsync();

            // 6. Khởi tạo Query và nạp dữ liệu liên quan (Eager Loading)
            IQueryable<FormHr> query = _context.FormHrs
                .Include(f => f.HrNguoiXacNhans) // Quan trọng: Hiển thị tiến độ xác nhận của hội đồng
                    .ThenInclude(x => x.IdnguoiXacNhanNavigation)
                .Include(f => f.BaoVeHrs);      // Quan trọng: Hiển thị trạng thái bảo vệ (nếu đơn ra cổng)

            // 7. PHÂN QUYỀN LỌC DỮ LIỆU
            if (isAll)
            {
                // Nhóm Quản trị (ALL/ADMIN): Xem tất cả các đơn đã được Quản lý bộ phận duyệt sơ bộ
                // (Không lọc theo loại đơn, xem được toàn bộ hệ thống)
                query = query.Where(f => f.IdNguoiDuyet != null);
            }
            else
            {
                // Nhóm Giám đốc: Chỉ xem những loại đơn mình được phân quyền xác nhận
                // Và đơn đó phải được Quản lý trực tiếp của nhân viên duyệt trước (IdNguoiDuyet != null)
                query = query.Where(f => dsMaLoaiDon.Contains(f.IdForm) && f.IdNguoiDuyet != null);
            }

            // Chỉ lấy những đơn có thiết lập quy trình xác nhận (định tuyến)
            query = query.Where(f => f.HrNguoiXacNhans != null && f.HrNguoiXacNhans.Any());

            // 8. Thực thi truy vấn và sắp xếp
            try
            {
                var danhSachDon = await query
                    .OrderByDescending(f => f.Id) // Đơn mới nhất lên đầu
                    .AsNoTracking()               // Tối ưu hiệu năng đọc dữ liệu
                    .ToListAsync();

                // Gửi thông tin bổ trợ ra View để xử lý logic ẩn/hiện nút bấm
                ViewBag.UserEmail = userEmail;
                ViewBag.Roles = roles;

                return View(danhSachDon);
            }
            catch (Exception ex)
            {
                // Ghi log lỗi nếu cần thiết
                TempData["Error"] = "Lỗi truy vấn dữ liệu phê duyệt cấp cao: " + ex.Message;
                return View(new List<FormHr>());
            }
        }
        /// <summary>
        /// POST: /FormHR/GiamDocPheDuyet
        /// Giám đốc hoặc Admin phê duyệt/từ chối đơn HR
        /// </summary>
        [HttpPost("/FormHR/GiamDocPheDuyet")]
        public async Task<IActionResult> GiamDocPheDuyet([FromBody] System.Text.Json.JsonElement data)
        {
            using (var transaction = await _context.Database.BeginTransactionAsync())
            {
                try
                {
                    // 1. Lấy dữ liệu từ Request
                    int idForm = data.GetProperty("idForm").GetInt32();
                    int idNguoiXacNhan = data.GetProperty("idNguoiXacNhan").GetInt32();
                    var loaiHanhDong = data.GetProperty("loaiHanhDong").GetString() ?? "";
                    var lyDo = data.TryGetProperty("lyDo", out var p) ? (p.GetString() ?? "") : "";

                    // 2. Lấy thông tin người thao tác từ Claims
                    var userEmail = User.FindFirst(System.Security.Claims.ClaimTypes.Email)?.Value ?? "";
                    var userName = User.FindFirst(System.Security.Claims.ClaimTypes.Name)?.Value ?? "N/A";

                    // Lấy danh sách quyền để kiểm tra
                    var roles = User.FindAll(System.Security.Claims.ClaimTypes.Role)
                                    .Select(r => r.Value.Trim().ToUpper())
                                    .ToList();
                    var userRoleCustom = User.FindFirst("UserRole")?.Value?.Trim().ToUpper();
                    if (!string.IsNullOrEmpty(userRoleCustom) && !roles.Contains(userRoleCustom)) roles.Add(userRoleCustom);

                    // 3. Tìm bản ghi người xác nhận và đơn HR liên quan
                    var xn = await _context.HrNguoiXacNhans
                        .Include(x => x.IdnguoiXacNhanNavigation)
                        .Include(x => x.IdFormHrNavigation) // Load đơn HR để ghi lịch sử
                        .FirstOrDefaultAsync(x => x.Id == idNguoiXacNhan && x.IdFormHr == idForm);

                    if (xn == null)
                        return Json(new { success = false, message = "Không tìm thấy bản ghi xác nhận." });

                    // 4. Kiểm tra quyền thực hiện (1 trong 3: ALL, GIAMDOCHR, hoặc đúng Email người được gán)
                    bool canAct = roles.Contains("ALL") || roles.Contains("GIAMDOCHR")
                                  || string.Equals(userEmail, xn.MaNguoiXacNhan ?? "", StringComparison.OrdinalIgnoreCase)
                                  || string.Equals(userEmail, xn.IdnguoiXacNhanNavigation?.MaNv ?? "", StringComparison.OrdinalIgnoreCase);

                    if (!canAct)
                        return Json(new { success = false, message = "Bạn không có quyền thực hiện thao tác này." });

                    // 5. Kiểm tra nếu đã duyệt rồi
                    if (xn.TrangThaiXacNhan != null && xn.TrangThaiXacNhan != 0)
                        return Json(new { success = false, message = "Mục này đã được xác nhận hoặc từ chối trước đó." });

                    // 6. Cập nhật trạng thái xác nhận
                    int newTrangThai = loaiHanhDong.Equals("Approve", StringComparison.OrdinalIgnoreCase) ? 1 : 2;
                    string hanhDongStr = (newTrangThai == 1) ? "Phê duyệt" : "Từ chối";

                    DateTime now = DateTime.Now;
                    xn.TrangThaiXacNhan = newTrangThai;
                    xn.ThoiGianXacNhan = now;
                    xn.GhiChu = string.IsNullOrWhiteSpace(lyDo) ? null : lyDo;

                    _context.HrNguoiXacNhans.Update(xn);

                    // 7. GHI LỊCH SỬ THAO TÁC (LichSuFormHr)
                    var lichSu = new LichSuFormHr
                    {
                        IdFormHr = idForm,
                        TieuDe = $"Cấp cao {hanhDongStr}",
                        Mota = $"Người thực hiện: {userName} ({userEmail}). " +
                               $"Kết quả: {hanhDongStr}. " +
                               $"Nội dung ghi chú: {(string.IsNullOrWhiteSpace(lyDo) ? "Không có" : lyDo)}",
                        Time = now
                    };
                    _context.LichSuFormHrs.Add(lichSu);

                    // 8. Lưu thay đổi và Commit Transaction
                    await _context.SaveChangesAsync();
                    await transaction.CommitAsync();

                    return Json(new
                    {
                        success = true,
                        message = $"Đã {hanhDongStr.ToLower()} thành công",
                        trangThai = newTrangThai,
                        thoiGian = now.ToString("HH:mm dd/MM/yyyy")
                    });
                }
                catch (Exception ex)
                {
                    await transaction.RollbackAsync();
                    return Json(new { success = false, message = "Lỗi hệ thống: " + ex.Message });
                }
            }
        }
        #endregion

        #region ĐẨY SANG BẢO VỆ

        public class DayBaoVeDto
        {
            public int IdFormHr { get; set; }
        }

        public class XacNhanBaoVeDto
        {
            public int IdBaoVe { get; set; }   // Id của record BaoVeHr
            public string? GhiChu { get; set; }
        }

        [HttpPost("/FormHR/DayDenBaoVe")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DayDenBaoVe([FromBody] DayBaoVeDto dto)
        {
            try
            {
                // Kiểm tra quyền
                if (!User.IsInRole("All") && !User.IsInRole("AdminHR")
                    && !User.IsInRole("BaoVeHR") && !User.IsInRole("QuanLyDuyetDonHR"))
                    return Json(new { success = false, message = "Bạn không có quyền thực hiện thao tác này!" });

                // Chống tạo trùng
                bool daCoRoi = await _context.BaoVeHrs.AnyAsync(x => x.IdFormHr == dto.IdFormHr);
                if (daCoRoi)
                    return Json(new { success = false, message = "Phiếu này đã được đẩy sang Bảo Vệ rồi!" });

                var record = new BaoVeHr
                {
                    IdFormHr = dto.IdFormHr,
                    TrangThai = 0   // 0 = chờ xử lý, 1 = hoàn thành
                                    // GhiChu, IdBaoVe, TenBaoVe, TimeBaoVe — để null, BaoVe tự điền sau
                };

                _context.BaoVeHrs.Add(record);

                // Ghi nhật ký
                _context.LichSuFormHrs.Add(new LichSuFormHr
                {
                    IdFormHr = dto.IdFormHr,
                    TieuDe = "ĐẨY SANG BẢO VỆ",
                    Mota = $"{User.Identity?.Name} đã chuyển phiếu sang đội Bảo Vệ xử lý.",
                    Time = DateTime.Now
                });

                await _context.SaveChangesAsync();
                return Json(new { success = true, message = "Đã đẩy sang Bảo Vệ thành công!" });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = "Lỗi: " + ex.Message });
            }
        }

        [HttpPost("/FormHR/BaoVeXacNhan")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> BaoVeXacNhan([FromBody] XacNhanBaoVeDto dto)
        {
            try
            {
                // Chỉ BaoVeHR hoặc All mới được xác nhận
                if (!User.IsInRole("All") && !User.IsInRole("BaoVeHR"))
                    return Json(new { success = false, message = "Bạn không có quyền xác nhận!" });

                var record = await _context.BaoVeHrs.FirstOrDefaultAsync(x => x.Id == dto.IdBaoVe);
                if (record == null)
                    return Json(new { success = false, message = "Không tìm thấy bản ghi Bảo Vệ!" });

                var userEmail = User.FindFirst(System.Security.Claims.ClaimTypes.Email)?.Value ?? "";
                var userNv = await _context.HrNguoiHoTros.FirstOrDefaultAsync(x => x.MaNv == userEmail);

                record.GhiChu = dto.GhiChu;
                record.IdBaoVe = userEmail;
                record.TenBaoVe = User.Identity?.Name ?? userEmail;
                record.TimeBaoVe = DateTime.Now;
                record.TrangThai = 1;   // Hoàn thành

                // Ghi nhật ký
                _context.LichSuFormHrs.Add(new LichSuFormHr
                {
                    IdFormHr = record.IdFormHr,
                    TieuDe = "BẢO VỆ XÁC NHẬN HOÀN TẤT",
                    Mota = $"Bảo Vệ [{User.Identity?.Name}] đã xác nhận hoàn tất.\nGhi chú: {dto.GhiChu}",
                    Time = DateTime.Now
                });

                await _context.SaveChangesAsync();
                return Json(new
                {
                    success = true,
                    message = "Xác nhận hoàn tất thành công!",
                    tenBaoVe = record.TenBaoVe,
                    thoiGian = record.TimeBaoVe?.ToString("HH:mm dd/MM/yyyy"),
                    ghiChu = record.GhiChu
                });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = "Lỗi: " + ex.Message });
            }
        }

        #endregion

        #region QUY TRÌNH KIỂM SOÁT CỔNG BẢO VỆ

        /// <summary>
        /// GET: /FormHR/BaoVePheDuyet
        /// Trang dành riêng cho bộ phận Bảo vệ để xác nhận ra/vào cổng dựa trên đơn đã được duyệt
        /// </summary>
        [HttpGet("/FormHR/BaoVePheDuyet")]
        public async Task<IActionResult> BaoVePheDuyet()
        {
            // 1. Kiểm tra xác thực qua Cookie Authentication
            if (User == null || User.Identity == null || !User.Identity.IsAuthenticated)
            {
                return Redirect("/DonXetDuyet/DangNhap");
            }

            // 2. Lấy Role để hiển thị (Mặc định GUEST nếu không có role)
            var userRole = (User.FindFirst(System.Security.Claims.ClaimTypes.Role)?.Value
                           ?? User.FindFirst("UserRole")?.Value
                           ?? "GUEST").Trim().ToUpper();

            // 3. Khởi tạo truy vấn và lọc dữ liệu
            // LỌC: Chỉ lấy những đơn mà danh sách BaoVeHrs có ít nhất một bản ghi (không null và có data)
            IQueryable<FormHr> query = _context.FormHrs
                .Include(f => f.BaoVeHrs) // Load thông tin trạng thái bảo vệ dựa trên model BaoVeHr bạn cung cấp
                .Where(f => f.BaoVeHrs != null && f.BaoVeHrs.Any());

            // 4. Thực thi và sắp xếp theo ID giảm dần (đơn mới nhất lên đầu)
            try
            {
                var danhSachDon = await query
                    .OrderByDescending(f => f.Id)
                    .AsNoTracking() // Tăng tốc độ đọc dữ liệu
                    .ToListAsync();

                ViewBag.UserRole = userRole;

                return View(danhSachDon);
            }
            catch (Exception ex)
            {
                // Xử lý ngoại lệ nếu truy vấn lỗi
                TempData["Error"] = "Lỗi hệ thống bảo vệ: " + ex.Message;
                return View(new List<FormHr>());
            }
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
            "HR_XinRaNgoai_1" => "Ra ngoài",
            "HR_MangHangHoaRaCong_2" => "Hàng hóa",
            "HR_XeCongTac_3" => "Xe công tác", // Sửa lại key cho khớp với phần đăng ký bạn đã viết
            "HR_XeDaily_4" => "Xe Daily",
            "HR_DonTiepKhac_5" => "Đón khách",
            "HR_NhaThauQuaCong_6" => "Nhà thầu",
            "HR_HoTroTienDienThoai_7" => "Tiền ĐT", // Bổ sung đơn 7
            "HR_DoiCaLam_8" => "Đổi ca",           // Bổ sung đơn 8
            _ => "Khác (" + id + ")"
        };

        #endregion

        #region LỊCH SỬ VÀ THÔNG BÁO FORM HR (PHÂN TÁCH ALL & GIAMDOCHR)

        [HttpGet("/FormHR/LichSuHR")]
        public async Task<IActionResult> LogLichSuHR()
        {
            var userIdStr = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userIdStr)) return RedirectToAction("DangNhap", "DonXetDuyet");

            int userId = int.Parse(userIdStr);
            var userEmail = User.FindFirst(System.Security.Claims.ClaimTypes.Email)?.Value ?? "";
            var tenCongTy = User.FindFirst("TenCongTy")?.Value?.Trim() ?? "";

            var userRoles = User.FindAll(System.Security.Claims.ClaimTypes.Role)
                                .Select(c => c.Value.Trim().ToUpper())
                                .ToList();

            var query = _context.LichSuFormHrs
                .Include(l => l.IdFormHrNavigation)
                    .ThenInclude(f => f.HrNguoiXacNhans)
                .Include(l => l.IdFormHrNavigation)
                    .ThenInclude(f => f.HrCtNguoiHoTros)
                        .ThenInclude(ct => ct.IdHrNguoiHoTroNavigation)
                .AsQueryable();

            // --- LOGIC PHÂN QUYỀN ---
            if (userRoles.Contains("ALL"))
            {
                // [ALL]: XEM TẤT CẢ - KHÔNG LỌC GÌ
            }
            else if (userRoles.Contains("GIAMDOCHR"))
            {
                // [GIAMDOCHR]: XEM XUYÊN CÔNG TY + ĐIỀU KIỆN RÀNG BUỘC
                query = query.Where(l =>
                    l.IdFormHrNavigation.HrNguoiXacNhans.Any() &&
                    l.IdFormHrNavigation.IdNguoiDuyet != null
                );
            }
            else
            {
                // [CÁC QUYỀN CÒN LẠI]: PHẢI THEO CÔNG TY
                if (!string.IsNullOrEmpty(tenCongTy))
                {
                    query = query.Where(l => l.IdFormHrNavigation.TenCongTy.Trim() == tenCongTy);
                }

                if (userRoles.Contains("ADMINHR") || userRoles.Contains("ADMIN"))
                {
                    query = query.Where(l =>
                        l.IdFormHrNavigation.IdNguoiDuyet != null &&
                        (l.IdFormHrNavigation.IdAdmin == userId ||
                         l.IdFormHrNavigation.HrCtNguoiHoTros.Any(ct => ct.IdHrNguoiHoTroNavigation.MaNv == userEmail))
                    );
                }
                else if (userRoles.Contains("QUANLYDUYETDONHR") || userRoles.Contains("QUANLY"))
                {
                    query = query.Where(l =>
                        l.IdFormHrNavigation.IdNguoiTao == userId ||
                        l.IdFormHrNavigation.IdNguoiDuyet == userId
                    );
                }
                else
                {
                    query = query.Where(l =>
                        l.IdFormHrNavigation.IdNguoiTao == userId ||
                        l.IdFormHrNavigation.HrCtNguoiHoTros.Any(ct => ct.IdHrNguoiHoTroNavigation.MaNv == userEmail)
                    );
                }
            }

            var logs = await query.OrderByDescending(l => l.Time).AsNoTracking().ToListAsync();
            return View(logs);
        }

        [HttpGet("/FormHR/GetNotificationsHR")]
        public async Task<IActionResult> GetNotificationsHR(int skip = 0, int take = 20)
        {
            var userIdStr = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userIdStr)) return Unauthorized();

            int userId = int.Parse(userIdStr);
            var userEmail = User.FindFirst(System.Security.Claims.ClaimTypes.Email)?.Value ?? "";
            var tenCongTy = User.FindFirst("TenCongTy")?.Value?.Trim() ?? "";
            var userRoles = User.FindAll(System.Security.Claims.ClaimTypes.Role)
                                .Select(c => c.Value.Trim().ToUpper())
                                .ToList();

            var query = _context.LichSuFormHrs
                .Include(l => l.IdFormHrNavigation)
                    .ThenInclude(f => f.HrNguoiXacNhans)
                .AsQueryable();

            // --- ĐỒNG BỘ LOGIC VỚI LOGLICSU ---
            if (userRoles.Contains("ALL"))
            {
                // [ALL]: XEM TẤT CẢ
            }
            else if (userRoles.Contains("GIAMDOCHR"))
            {
                // [GIAMDOCHR]: XEM XUYÊN CÔNG TY + RÀNG BUỘC
                query = query.Where(l =>
                    l.IdFormHrNavigation.HrNguoiXacNhans.Any() &&
                    l.IdFormHrNavigation.IdNguoiDuyet != null
                );
            }
            else
            {
                if (!string.IsNullOrEmpty(tenCongTy))
                {
                    query = query.Where(l => l.IdFormHrNavigation.TenCongTy.Trim() == tenCongTy);
                }

                if (userRoles.Contains("ADMINHR") || userRoles.Contains("ADMIN"))
                {
                    query = query.Where(l =>
                        l.IdFormHrNavigation.IdNguoiDuyet != null &&
                        (l.IdFormHrNavigation.IdAdmin == userId ||
                         l.IdFormHrNavigation.HrCtNguoiHoTros.Any(ct => ct.IdHrNguoiHoTroNavigation.MaNv == userEmail))
                    );
                }
                else if (userRoles.Contains("QUANLYDUYETDONHR") || userRoles.Contains("QUANLY"))
                {
                    query = query.Where(l => l.IdFormHrNavigation.IdNguoiTao == userId || l.IdFormHrNavigation.IdNguoiDuyet == userId);
                }
                else
                {
                    query = query.Where(l =>
                        l.IdFormHrNavigation.IdNguoiTao == userId ||
                        l.IdFormHrNavigation.HrCtNguoiHoTros.Any(ct => ct.IdHrNguoiHoTroNavigation.MaNv == userEmail)
                    );
                }
            }

            var unreadCount = await query.CountAsync(l => l.IsRead != true);
            var logs = await query.OrderByDescending(l => l.Time)
                                  .Skip(skip)
                                  .Take(take)
                                  .Select(l => new {
                                      l.Id,
                                      l.IdFormHr,
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
