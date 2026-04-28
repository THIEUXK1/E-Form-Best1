using ClosedXML.Excel; // Cần cài đặt package ClosedXML
using DocumentFormat.OpenXml.InkML;
using E_Form_Best.Context;
using E_Form_Best.Models.ITForm;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.IO;
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

            // --- MỚI: Lấy danh sách nhân sự hỗ trợ hiển thị ra View ---
            ViewBag.ListNguoiHoTro = _context.HrNguoiHoTros
                .Include(x => x.CongViecHrs.Where(cv => cv.Ten == "Đăng ký đơn xin ra ngoài"))
                .Where(x => x.CongViecHrs.Any(cv => cv.Ten == "Đăng ký đơn xin ra ngoài"))
                .ToList();

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
        public async Task<IActionResult> DonXinRaNgoai(FormHr form, [FromForm] HrXinRaNgoai1 xinRaNgoai, int[] SelectedCongViecIds)
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

                    // --- BƯỚC 2: LƯU NHÂN SỰ HỖ TRỢ ĐÃ CHỌN ---
                    if (SelectedCongViecIds != null && SelectedCongViecIds.Length > 0)
                    {
                        foreach (var cvId in SelectedCongViecIds)
                        {
                            var congViec = await _context.CongViecHrs.FindAsync(cvId);
                            if (congViec != null)
                            {
                                var ctHoTro = new HrCtNguoiHoTro
                                {
                                    IdFormHr = form.Id,
                                    IdHrNguoiHoTro = congViec.IdHrNguoiHoTro,
                                    Stt = 1
                                };
                                _context.HrCtNguoiHoTros.Add(ctHoTro);
                            }
                        }
                        await _context.SaveChangesAsync();
                    }

                    // --- BƯỚC 3: TỰ ĐỘNG THÊM NGƯỜI XÁC NHẬN (CẤU HÌNH THEO BỘ PHẬN & CÔNG TY) ---
                    // Truy vấn lấy người duyệt từ cấu hình dựa trên IdForm, Tên Bộ Phận và Tên Công Ty
                    var listDuyetTheoBoPhan = await _context.DmNguoiDuyetLoaiDonBoPhans
                        .Include(x => x.IdnguoiXacNhanNavigation)
                        .Where(x => x.IdloaiDonNavigation.MaLoaiDon == form.IdForm
                                    && x.IdboPhanNavigation.TenBoPhan == form.BoPhan
                                    && x.IdcongTyNavigation.TenCongTy == form.TenCongTy
                                    && x.TrangThai == true)
                        .OrderBy(x => x.Stt)
                        .ToListAsync();

                    if (listDuyetTheoBoPhan.Any())
                    {
                        foreach (var item in listDuyetTheoBoPhan)
                        {
                            var quanLyDuyet = new HrQuanLyDuyetB2
                            {
                                IdFormHr = form.Id,
                                IdnguoiXacNhan = item.IdnguoiXacNhan,
                                ThuTuXacNhan = item.Stt,
                                MaNguoiXacNhan = item.IdnguoiXacNhanNavigation?.MaNv,
                                TenNguoiXacNhan = item.IdnguoiXacNhanNavigation?.HoTen,
                                TrangThaiXacNhan = 0, // 0: Chờ duyệt
                                ThoiGianXacNhan = null,
                                Loai = item.Loai // Map thêm cột Loai (AND / OR) từ cấu hình để file View nhận diện
                            };
                            _context.HrQuanLyDuyetB2s.Add(quanLyDuyet);
                        }
                        await _context.SaveChangesAsync();
                    }

                    // Fallback: Nếu không có cấu hình theo bộ phận, lấy theo cấu hình mặc định (DmNguoiXacNhanLoaiDon)
                    var loaiDon = await _context.DmLoaiDons.FirstOrDefaultAsync(x => x.MaLoaiDon == form.IdForm && x.TrangThai == true);
                    if (loaiDon != null)
                    {
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
                                ThuTuXacNhan = item.CapDoXacNhan,
                                MaNguoiXacNhan = item.IdnguoiXacNhanNavigation?.MaNv,
                                TenNguoiXacNhan = item.IdnguoiXacNhanNavigation?.HoTen,
                                TrangThaiXacNhan = 0,
                                ThoiGianXacNhan = null
                            };
                            _context.HrNguoiXacNhans.Add(hrXacNhan);
                        }
                        await _context.SaveChangesAsync();
                    }


                    // --- BƯỚC 4: LƯU LỊCH SỬ THAO TÁC ---
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

                    // --- BƯỚC 5: XỬ LÝ FILE/ẢNH TRÊN FILESERVER ---
                    if (!Directory.Exists(networkPath)) Directory.CreateDirectory(networkPath);

                    string safeName = RemoveSign4VietnameseString(userName).Replace(" ", "");
                    string timeStamp = DateTime.Now.ToString("ddMMyy_HHmm");

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
                        xinRaNgoai.Anh = null;
                        _context.HrXinRaNgoai1s.Add(xinRaNgoai);
                        await _context.SaveChangesAsync();
                    }

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

            // --- MỚI: Lấy danh sách nhân sự hỗ trợ Đăng ký mang hàng hóa ra cổng ---
            ViewBag.ListNguoiHoTro = _context.HrNguoiHoTros
                .Include(x => x.CongViecHrs.Where(cv => cv.Ten == "Đăng ký mang hàng hóa ra cổng"))
                .Where(x => x.CongViecHrs.Any(cv => cv.Ten == "Đăng ký mang hàng hóa ra cổng"))
                .ToList();

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
        public async Task<IActionResult> MangHangHoaRaCong(FormHr form, [FromForm] HrMangHangHoaRaCong2 chiTiet, int[] SelectedCongViecIds)
        {
            if (!User.Identity.IsAuthenticated) return Redirect("/DonXetDuyet/DangNhap");

            // --- GIA CỐ: Nếu Binder bị lỗi và trả về null ---
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
                    form.IdForm = "HR_MangHangHoaRaCong_2";
                    form.TenForm = "Đơn mang hàng hóa ra cổng";

                    _context.FormHrs.Add(form);
                    await _context.SaveChangesAsync();

                    // --- BƯỚC 2: LƯU NHÂN SỰ HỖ TRỢ ĐÃ CHỌN (MỚI) ---
                    if (SelectedCongViecIds != null && SelectedCongViecIds.Length > 0)
                    {
                        foreach (var cvId in SelectedCongViecIds)
                        {
                            var congViec = await _context.CongViecHrs.FindAsync(cvId);
                            if (congViec != null)
                            {
                                var ctHoTro = new HrCtNguoiHoTro
                                {
                                    IdFormHr = form.Id,
                                    IdHrNguoiHoTro = congViec.IdHrNguoiHoTro,
                                    Stt = 1
                                };
                                _context.HrCtNguoiHoTros.Add(ctHoTro);
                            }
                        }
                        await _context.SaveChangesAsync();
                    }

                    // --- BƯỚC 3: TỰ ĐỘNG THÊM NGƯỜI XÁC NHẬN (CẤU HÌNH THEO BỘ PHẬN & CÔNG TY) ---
                    // Truy vấn lấy người duyệt từ cấu hình dựa trên IdForm, Tên Bộ Phận và Tên Công Ty
                    var listDuyetTheoBoPhan = await _context.DmNguoiDuyetLoaiDonBoPhans
                        .Include(x => x.IdnguoiXacNhanNavigation)
                        .Where(x => x.IdloaiDonNavigation.MaLoaiDon == form.IdForm
                                    && x.IdboPhanNavigation.TenBoPhan == form.BoPhan
                                    && x.IdcongTyNavigation.TenCongTy == form.TenCongTy
                                    && x.TrangThai == true)
                        .OrderBy(x => x.Stt)
                        .ToListAsync();

                    if (listDuyetTheoBoPhan.Any())
                    {
                        foreach (var item in listDuyetTheoBoPhan)
                        {
                            var quanLyDuyet = new HrQuanLyDuyetB2
                            {
                                IdFormHr = form.Id,
                                IdnguoiXacNhan = item.IdnguoiXacNhan,
                                ThuTuXacNhan = item.Stt,
                                MaNguoiXacNhan = item.IdnguoiXacNhanNavigation?.MaNv,
                                TenNguoiXacNhan = item.IdnguoiXacNhanNavigation?.HoTen,
                                TrangThaiXacNhan = 0, // 0: Chờ duyệt
                                ThoiGianXacNhan = null,
                                Loai = item.Loai // Map thêm cột Loai (AND / OR) từ cấu hình để file View nhận diện
                            };
                            _context.HrQuanLyDuyetB2s.Add(quanLyDuyet);
                        }
                        await _context.SaveChangesAsync();
                    }

                    // Fallback: Nếu không có cấu hình theo bộ phận, lấy theo cấu hình mặc định (DmNguoiXacNhanLoaiDon)
                    var loaiDon = await _context.DmLoaiDons.FirstOrDefaultAsync(x => x.MaLoaiDon == form.IdForm && x.TrangThai == true);
                    if (loaiDon != null)
                    {
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
                                ThuTuXacNhan = item.CapDoXacNhan,
                                MaNguoiXacNhan = item.IdnguoiXacNhanNavigation?.MaNv,
                                TenNguoiXacNhan = item.IdnguoiXacNhanNavigation?.HoTen,
                                TrangThaiXacNhan = 0,
                                ThoiGianXacNhan = null
                            };
                            _context.HrNguoiXacNhans.Add(hrXacNhan);
                        }
                        await _context.SaveChangesAsync();
                    }


                    // --- BƯỚC 4: LƯU LỊCH SỬ ---
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

                    // --- BƯỚC 5: XỬ LÝ FILE TRÊN FILESERVER ---
                    if (!Directory.Exists(networkPath)) Directory.CreateDirectory(networkPath);
                    string safeName = RemoveSign4VietnameseString(userName).Replace(" ", "");
                    string timeStamp = DateTime.Now.ToString("ddMMyy_HHmm");

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
                        await _context.SaveChangesAsync();
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

            // --- MỚI: Lấy danh sách nhân sự hỗ trợ hiển thị ra View ---
            ViewBag.ListNguoiHoTro = _context.HrNguoiHoTros
                .Include(x => x.CongViecHrs.Where(cv => cv.Ten == "Đăng ký sử dụng xe công tác"))
                .Where(x => x.CongViecHrs.Any(cv => cv.Ten == "Đăng ký sử dụng xe công tác"))
                .ToList();

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
        public async Task<IActionResult> DangKySuDungXeCongTac(FormHr form, [FromForm] HrDangKySuDungXeCongTac3 chiTiet, int[] SelectedCongViecIds)
        {
            if (!User.Identity.IsAuthenticated) return Redirect("/DonXetDuyet/DangNhap");

            // Gia cố Binding nếu chiTiet bị null
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
                    form.IdForm = "HR_DangKySuDungXeCongTac_3";
                    form.TenForm = "Đơn đăng ký sử dụng xe công tác";
                    form.Danhmuc = "ĐƠN ĐĂNG KÝ SỬ DỤNG XE CÔNG TÁC";

                    _context.FormHrs.Add(form);
                    await _context.SaveChangesAsync();

                    // --- BƯỚC 2: LƯU NHÂN SỰ HỖ TRỢ ĐÃ CHỌN (MỚI) ---
                    if (SelectedCongViecIds != null && SelectedCongViecIds.Length > 0)
                    {
                        foreach (var cvId in SelectedCongViecIds)
                        {
                            var congViec = await _context.CongViecHrs.FindAsync(cvId);
                            if (congViec != null)
                            {
                                var ctHoTro = new HrCtNguoiHoTro
                                {
                                    IdFormHr = form.Id,
                                    IdHrNguoiHoTro = congViec.IdHrNguoiHoTro,
                                    Stt = 1
                                };
                                _context.HrCtNguoiHoTros.Add(ctHoTro);
                            }
                        }
                        await _context.SaveChangesAsync();
                    }

                    // --- BƯỚC 3: TỰ ĐỘNG THÊM NGƯỜI XÁC NHẬN (CẤU HÌNH THEO BỘ PHẬN & CÔNG TY) ---
                    // Truy vấn lấy người duyệt từ cấu hình dựa trên IdForm, Tên Bộ Phận và Tên Công Ty
                    var listDuyetTheoBoPhan = await _context.DmNguoiDuyetLoaiDonBoPhans
                        .Include(x => x.IdnguoiXacNhanNavigation)
                        .Where(x => x.IdloaiDonNavigation.MaLoaiDon == form.IdForm
                                    && x.IdboPhanNavigation.TenBoPhan == form.BoPhan
                                    && x.IdcongTyNavigation.TenCongTy == form.TenCongTy
                                    && x.TrangThai == true)
                        .OrderBy(x => x.Stt)
                        .ToListAsync();

                    if (listDuyetTheoBoPhan.Any())
                    {
                        foreach (var item in listDuyetTheoBoPhan)
                        {
                            var quanLyDuyet = new HrQuanLyDuyetB2
                            {
                                IdFormHr = form.Id,
                                IdnguoiXacNhan = item.IdnguoiXacNhan,
                                ThuTuXacNhan = item.Stt,
                                MaNguoiXacNhan = item.IdnguoiXacNhanNavigation?.MaNv,
                                TenNguoiXacNhan = item.IdnguoiXacNhanNavigation?.HoTen,
                                TrangThaiXacNhan = 0, // 0: Chờ duyệt
                                ThoiGianXacNhan = null,
                                Loai = item.Loai // Map thêm cột Loai (AND / OR) từ cấu hình để file View nhận diện
                            };
                            _context.HrQuanLyDuyetB2s.Add(quanLyDuyet);
                        }
                        await _context.SaveChangesAsync();
                    }

                    // Fallback: Nếu không có cấu hình theo bộ phận, lấy theo cấu hình mặc định (DmNguoiXacNhanLoaiDon)
                    var loaiDon = await _context.DmLoaiDons.FirstOrDefaultAsync(x => x.MaLoaiDon == form.IdForm && x.TrangThai == true);
                    if (loaiDon != null)
                    {
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
                                ThuTuXacNhan = item.CapDoXacNhan,
                                MaNguoiXacNhan = item.IdnguoiXacNhanNavigation?.MaNv,
                                TenNguoiXacNhan = item.IdnguoiXacNhanNavigation?.HoTen,
                                TrangThaiXacNhan = 0,
                                ThoiGianXacNhan = null
                            };
                            _context.HrNguoiXacNhans.Add(hrXacNhan);
                        }
                        await _context.SaveChangesAsync();
                    }


                    // --- BƯỚC 4: LƯU LỊCH SỬ THAO TÁC ---
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

                    // --- BƯỚC 5: XỬ LÝ FILE TRÊN FILESERVER ---
                    if (!Directory.Exists(networkPath)) Directory.CreateDirectory(networkPath);
                    string safeName = RemoveSign4VietnameseString(userName).Replace(" ", "");
                    string timeStamp = DateTime.Now.ToString("ddMMyy_HHmm");

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

                        chiTiet.Anh = null;
                        _context.HrDangKySuDungXeCongTac3s.Add(chiTiet);
                        await _context.SaveChangesAsync();
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

            // --- MỚI: Lấy danh sách nhân sự hỗ trợ hiển thị ra View ---
            ViewBag.ListNguoiHoTro = _context.HrNguoiHoTros
                // 1. Chỉ Include những công việc thỏa mãn điều kiện tên
                .Include(x => x.CongViecHrs.Where(cv => cv.Ten == "Đăng ký sử dụng xe Daily"))
                // 2. Chỉ lấy những người có ít nhất 1 công việc thỏa mãn
                .Where(x => x.CongViecHrs.Any(cv => cv.Ten == "Đăng ký sử dụng xe Daily"))
                .ToList();

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
        public async Task<IActionResult> DangKySuDungXeDaily(FormHr form, [FromForm] HrDangKySuDungXeDaily4 chiTiet, int[] SelectedCongViecIds)
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

                    // --- BƯỚC 2: LƯU NHÂN SỰ HỖ TRỢ ĐÃ CHỌN (MỚI) ---
                    if (SelectedCongViecIds != null && SelectedCongViecIds.Length > 0)
                    {
                        foreach (var cvId in SelectedCongViecIds)
                        {
                            var congViec = await _context.CongViecHrs.FindAsync(cvId);
                            if (congViec != null)
                            {
                                var ctHoTro = new HrCtNguoiHoTro
                                {
                                    IdFormHr = form.Id,
                                    IdHrNguoiHoTro = congViec.IdHrNguoiHoTro,
                                    Stt = 1
                                };
                                _context.HrCtNguoiHoTros.Add(ctHoTro);
                            }
                        }
                        await _context.SaveChangesAsync();
                    }
                    // --- BƯỚC 3: TỰ ĐỘNG THÊM NGƯỜI XÁC NHẬN (CẤU HÌNH THEO BỘ PHẬN & CÔNG TY) ---
                    // Truy vấn lấy người duyệt từ cấu hình dựa trên IdForm, Tên Bộ Phận và Tên Công Ty
                    var listDuyetTheoBoPhan = await _context.DmNguoiDuyetLoaiDonBoPhans
                        .Include(x => x.IdnguoiXacNhanNavigation)
                        .Where(x => x.IdloaiDonNavigation.MaLoaiDon == form.IdForm
                                    && x.IdboPhanNavigation.TenBoPhan == form.BoPhan
                                    && x.IdcongTyNavigation.TenCongTy == form.TenCongTy
                                    && x.TrangThai == true)
                        .OrderBy(x => x.Stt)
                        .ToListAsync();

                    if (listDuyetTheoBoPhan.Any())
                    {
                        foreach (var item in listDuyetTheoBoPhan)
                        {
                            var quanLyDuyet = new HrQuanLyDuyetB2
                            {
                                IdFormHr = form.Id,
                                IdnguoiXacNhan = item.IdnguoiXacNhan,
                                ThuTuXacNhan = item.Stt,
                                MaNguoiXacNhan = item.IdnguoiXacNhanNavigation?.MaNv,
                                TenNguoiXacNhan = item.IdnguoiXacNhanNavigation?.HoTen,
                                TrangThaiXacNhan = 0, // 0: Chờ duyệt
                                ThoiGianXacNhan = null,
                                Loai = item.Loai // Map thêm cột Loai (AND / OR) từ cấu hình để file View nhận diện
                            };
                            _context.HrQuanLyDuyetB2s.Add(quanLyDuyet);
                        }
                        await _context.SaveChangesAsync();
                    }

                    // Fallback: Nếu không có cấu hình theo bộ phận, lấy theo cấu hình mặc định (DmNguoiXacNhanLoaiDon)
                    var loaiDon = await _context.DmLoaiDons.FirstOrDefaultAsync(x => x.MaLoaiDon == form.IdForm && x.TrangThai == true);
                    if (loaiDon != null)
                    {
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
                                ThuTuXacNhan = item.CapDoXacNhan,
                                MaNguoiXacNhan = item.IdnguoiXacNhanNavigation?.MaNv,
                                TenNguoiXacNhan = item.IdnguoiXacNhanNavigation?.HoTen,
                                TrangThaiXacNhan = 0,
                                ThoiGianXacNhan = null
                            };
                            _context.HrNguoiXacNhans.Add(hrXacNhan);
                        }
                        await _context.SaveChangesAsync();
                    }


                    // --- BƯỚC 4: LƯU LỊCH SỬ THAO TÁC ---
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

                    // --- BƯỚC 5: XỬ LÝ FILE/ẢNH TRÊN FILESERVER ---
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

                        chiTiet.Anh = null;
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

            var userIdString = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userIdString)) return Redirect("/DonXetDuyet/DangNhap");

            // 2. Lấy danh sách phòng họp để hiển thị dropdown ở View
            ViewBag.DanhSachPhongHop = _context.PhongHopHrs.AsNoTracking().ToList();

            // --- MỚI: Lấy danh sách nhân sự hỗ trợ hiển thị ra View ---
            ViewBag.ListNguoiHoTro = _context.HrNguoiHoTros
                .Include(x => x.CongViecHrs.Where(cv => cv.Ten == "Đăng ký tiếp khách"))
                .Where(x => x.CongViecHrs.Any(cv => cv.Ten == "Đăng ký tiếp khách"))
                .ToList();

            // 3. Khởi tạo Model chính hiển thị lên View
            var model = new FormHr
            {
                IdForm = "HR_DonTiepKhac_5",
                TenForm = "Đơn đăng ký tiếp khách",
                Danhmuc = "ĐƠN TIẾP KHÁCH",
                TenCongTy = User.FindFirst("TenCongTy")?.Value ?? "",
                TenNguoiNv = User.FindFirst(ClaimTypes.Name)?.Value ?? "",
                BoPhan = User.FindFirst("PhongBan")?.Value ?? "",
                ViTri = User.FindFirst(ClaimTypes.Role)?.Value ?? "",
                SoNhanVien = User.FindFirst(ClaimTypes.Email)?.Value ?? "",
                Ngay = DateOnly.FromDateTime(DateTime.Now),
                IdNguoiTao = int.Parse(userIdString),
                TenNguoiTao = User.FindFirst(ClaimTypes.Name)?.Value ?? "",
                TimeNguoiTao = DateTime.Now,
                TrangThai = "ChoDuyet"
            };

            return View(model);
        }

        [HttpPost("/FormHR/DonTiepKhac")]
        public async Task<IActionResult> DonTiepKhac(FormHr form, [FromForm] HrDonTiepKhac5 chiTiet, int[] SelectedCongViecIds)
        {
            // 1. Kiểm tra đăng nhập lại khi Post
            if (!User.Identity.IsAuthenticated) return Redirect("/DonXetDuyet/DangNhap");

            var userIdString = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            var userName = User.FindFirst(ClaimTypes.Name)?.Value ?? "";
            var phongBan = User.FindFirst("PhongBan")?.Value ?? "";
            var viTri = User.FindFirst(ClaimTypes.Role)?.Value ?? "";
            var soNhanVien = User.FindFirst(ClaimTypes.Email)?.Value ?? "";
            var tenCongTy = User.FindFirst("TenCongTy")?.Value ?? "";

            if (string.IsNullOrEmpty(userIdString)) return Redirect("/DonXetDuyet/DangNhap");
            int userId = int.Parse(userIdString);

            // Đường dẫn Fileserver
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
                    form.IdForm = "HR_DonTiepKhac_5";
                    form.TenForm = "Đơn đăng ký tiếp khách";

                    _context.FormHrs.Add(form);
                    await _context.SaveChangesAsync();

                    // --- BƯỚC 2: LƯU NHÂN SỰ HỖ TRỢ ĐÃ CHỌN (MỚI) ---
                    if (SelectedCongViecIds != null && SelectedCongViecIds.Length > 0)
                    {
                        foreach (var cvId in SelectedCongViecIds)
                        {
                            var congViec = await _context.CongViecHrs.FindAsync(cvId);
                            if (congViec != null)
                            {
                                var ctHoTro = new HrCtNguoiHoTro
                                {
                                    IdFormHr = form.Id,
                                    IdHrNguoiHoTro = congViec.IdHrNguoiHoTro,
                                    Stt = 1
                                };
                                _context.HrCtNguoiHoTros.Add(ctHoTro);
                            }
                        }
                        await _context.SaveChangesAsync();
                    }
                    // --- BƯỚC 3: TỰ ĐỘNG THÊM NGƯỜI XÁC NHẬN (CẤU HÌNH THEO BỘ PHẬN & CÔNG TY) ---
                    // Truy vấn lấy người duyệt từ cấu hình dựa trên IdForm, Tên Bộ Phận và Tên Công Ty
                    var listDuyetTheoBoPhan = await _context.DmNguoiDuyetLoaiDonBoPhans
                        .Include(x => x.IdnguoiXacNhanNavigation)
                        .Where(x => x.IdloaiDonNavigation.MaLoaiDon == form.IdForm
                                    && x.IdboPhanNavigation.TenBoPhan == form.BoPhan
                                    && x.IdcongTyNavigation.TenCongTy == form.TenCongTy
                                    && x.TrangThai == true)
                        .OrderBy(x => x.Stt)
                        .ToListAsync();

                    if (listDuyetTheoBoPhan.Any())
                    {
                        foreach (var item in listDuyetTheoBoPhan)
                        {
                            var quanLyDuyet = new HrQuanLyDuyetB2
                            {
                                IdFormHr = form.Id,
                                IdnguoiXacNhan = item.IdnguoiXacNhan,
                                ThuTuXacNhan = item.Stt,
                                MaNguoiXacNhan = item.IdnguoiXacNhanNavigation?.MaNv,
                                TenNguoiXacNhan = item.IdnguoiXacNhanNavigation?.HoTen,
                                TrangThaiXacNhan = 0, // 0: Chờ duyệt
                                ThoiGianXacNhan = null,
                                Loai = item.Loai // Map thêm cột Loai (AND / OR) từ cấu hình để file View nhận diện
                            };
                            _context.HrQuanLyDuyetB2s.Add(quanLyDuyet);
                        }
                        await _context.SaveChangesAsync();
                    }

                    // Fallback: Nếu không có cấu hình theo bộ phận, lấy theo cấu hình mặc định (DmNguoiXacNhanLoaiDon)
                    var loaiDon = await _context.DmLoaiDons.FirstOrDefaultAsync(x => x.MaLoaiDon == form.IdForm && x.TrangThai == true);
                    if (loaiDon != null)
                    {
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
                                ThuTuXacNhan = item.CapDoXacNhan,
                                MaNguoiXacNhan = item.IdnguoiXacNhanNavigation?.MaNv,
                                TenNguoiXacNhan = item.IdnguoiXacNhanNavigation?.HoTen,
                                TrangThaiXacNhan = 0,
                                ThoiGianXacNhan = null
                            };
                            _context.HrNguoiXacNhans.Add(hrXacNhan);
                        }
                        await _context.SaveChangesAsync();
                    }


                    // --- BƯỚC 4: XỬ LÝ CHI TIẾT ĐƠN (HrDonTiepKhac5) ---
                    if (chiTiet != null)
                    {
                        chiTiet.IdFormHr = form.Id;
                        chiTiet.TrangThaiPhong = "off";
                        chiTiet.NgayYeuCau = DateTime.Now;

                        if (!Directory.Exists(networkPath)) Directory.CreateDirectory(networkPath);
                        string safeName = RemoveSign4VietnameseString(userName).Replace(" ", "");
                        string timeStamp = DateTime.Now.ToString("ddMMyy_HHmm");

                        // 4.1 Lưu File tài liệu đính kèm
                        var uploadFile = Request.Form.Files["UploadFile"];
                        if (uploadFile != null && uploadFile.Length > 0)
                        {
                            string fileName = $"File_TK_{form.Id}_{safeName}_{timeStamp}{Path.GetExtension(uploadFile.FileName)}";
                            string fullPath = Path.Combine(networkPath, fileName);
                            using (var stream = new FileStream(fullPath, FileMode.Create))
                            {
                                await uploadFile.CopyToAsync(stream);
                            }
                            form.FileDinhKem = fileName;
                        }

                        // 4.2 Lưu Ảnh minh chứng
                        var anhFile = Request.Form.Files["AnhMinhChung"];
                        if (anhFile != null && anhFile.Length > 0)
                        {
                            string imgName = $"Anh_TK_{form.Id}_{safeName}_{timeStamp}{Path.GetExtension(anhFile.FileName)}";
                            string imgPath = Path.Combine(networkPath, imgName);
                            using (var stream = new FileStream(imgPath, FileMode.Create))
                            {
                                await anhFile.CopyToAsync(stream);
                            }
                            chiTiet.DuongDanAnh = imgName;
                        }

                        _context.HrDonTiepKhac5s.Add(chiTiet);
                    }

                    // --- BƯỚC 5: LƯU LỊCH SỬ THAO TÁC ---
                    string moTaLichSu = $"Nhân viên {userName} ({soNhanVien}) đã tạo đơn tiếp khách.\n" +
                                        $"- Đối tác: {chiTiet?.TenCongTyKhach}\n" +
                                        $"- Phòng: {chiTiet?.TenPhongHop ?? "Không đăng ký"}\n" +
                                        $"- Suất ăn: {chiTiet?.LoaiSuatAn ?? "Không đăng ký"}";

                    _context.LichSuFormHrs.Add(new LichSuFormHr
                    {
                        IdFormHr = form.Id,
                        TieuDe = "Khởi tạo đơn đăng ký tiếp khách",
                        Mota = moTaLichSu,
                        Time = DateTime.Now,
                        IsRead = false
                    });

                    _context.Entry(form).State = EntityState.Modified;

                    await _context.SaveChangesAsync();
                    await transaction.CommitAsync();

                    TempData["Success"] = "Gửi đơn đăng ký tiếp khách thành công!";
                    return RedirectToAction("DonCho");
                }
                catch (Exception ex)
                {
                    await transaction.RollbackAsync();
                    ModelState.AddModelError("", "Có lỗi xảy ra: " + ex.Message);
                    ViewBag.DanhSachPhongHop = _context.PhongHopHrs.AsNoTracking().ToList();
                    ViewBag.ListNguoiHoTro = _context.HrNguoiHoTros
                        .Include(x => x.CongViecHrs.Where(cv => cv.Ten == "Đăng ký tiếp khách"))
                        .Where(x => x.CongViecHrs.Any(cv => cv.Ten == "Đăng ký tiếp khách"))
                        .ToList();
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

            // --- MỚI: Lấy danh sách nhân sự hỗ trợ hiển thị ra View ---
            ViewBag.ListNguoiHoTro = _context.HrNguoiHoTros
                .Include(x => x.CongViecHrs.Where(cv => cv.Ten == "Đăng ký Nhà thầu qua cổng"))
                .Where(x => x.CongViecHrs.Any(cv => cv.Ten == "Đăng ký Nhà thầu qua cổng"))
                .ToList();

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
        public async Task<IActionResult> NhaThauQuaCong(FormHr form, HrNhaThauQuaCong6 chiTiet, int[] SelectedCongViecIds)
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

                    // --- BƯỚC 2: LƯU NHÂN SỰ HỖ TRỢ ĐÃ CHỌN ---
                    if (SelectedCongViecIds != null && SelectedCongViecIds.Length > 0)
                    {
                        foreach (var cvId in SelectedCongViecIds)
                        {
                            var congViec = await _context.CongViecHrs.FindAsync(cvId);
                            if (congViec != null)
                            {
                                var ctHoTro = new HrCtNguoiHoTro
                                {
                                    IdFormHr = form.Id,
                                    IdHrNguoiHoTro = congViec.IdHrNguoiHoTro,
                                    Stt = 1
                                };
                                _context.HrCtNguoiHoTros.Add(ctHoTro);
                            }
                        }
                        await _context.SaveChangesAsync();
                    }

                    // --- BƯỚC 3: TỰ ĐỘNG THÊM NGƯỜI XÁC NHẬN (CẤU HÌNH THEO BỘ PHẬN & CÔNG TY) ---
                    // Truy vấn lấy người duyệt từ cấu hình dựa trên IdForm, Tên Bộ Phận và Tên Công Ty
                    var listDuyetTheoBoPhan = await _context.DmNguoiDuyetLoaiDonBoPhans
                        .Include(x => x.IdnguoiXacNhanNavigation)
                        .Where(x => x.IdloaiDonNavigation.MaLoaiDon == form.IdForm
                                    && x.IdboPhanNavigation.TenBoPhan == form.BoPhan
                                    && x.IdcongTyNavigation.TenCongTy == form.TenCongTy
                                    && x.TrangThai == true)
                        .OrderBy(x => x.Stt)
                        .ToListAsync();

                    if (listDuyetTheoBoPhan.Any())
                    {
                        foreach (var item in listDuyetTheoBoPhan)
                        {
                            var quanLyDuyet = new HrQuanLyDuyetB2
                            {
                                IdFormHr = form.Id,
                                IdnguoiXacNhan = item.IdnguoiXacNhan,
                                ThuTuXacNhan = item.Stt,
                                MaNguoiXacNhan = item.IdnguoiXacNhanNavigation?.MaNv,
                                TenNguoiXacNhan = item.IdnguoiXacNhanNavigation?.HoTen,
                                TrangThaiXacNhan = 0, // 0: Chờ duyệt
                                ThoiGianXacNhan = null,
                                Loai = item.Loai // Map thêm cột Loai (AND / OR) từ cấu hình để file View nhận diện
                            };
                            _context.HrQuanLyDuyetB2s.Add(quanLyDuyet);
                        }
                        await _context.SaveChangesAsync();
                    }

                    // Fallback: Nếu không có cấu hình theo bộ phận, lấy theo cấu hình mặc định (DmNguoiXacNhanLoaiDon)
                    var loaiDon = await _context.DmLoaiDons.FirstOrDefaultAsync(x => x.MaLoaiDon == form.IdForm && x.TrangThai == true);
                    if (loaiDon != null)
                    {
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
                                ThuTuXacNhan = item.CapDoXacNhan,
                                MaNguoiXacNhan = item.IdnguoiXacNhanNavigation?.MaNv,
                                TenNguoiXacNhan = item.IdnguoiXacNhanNavigation?.HoTen,
                                TrangThaiXacNhan = 0,
                                ThoiGianXacNhan = null
                            };
                            _context.HrNguoiXacNhans.Add(hrXacNhan);
                        }
                        await _context.SaveChangesAsync();
                    }


                    // --- BƯỚC 4: XỬ LÝ FILE ĐÍNH KÈM & ẢNH TRÊN FILESERVER ---
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

                    // --- BƯỚC 5: LƯU CHI TIẾT NHÀ THẦU ---
                    if (chiTiet != null)
                    {
                        chiTiet.IdFormHr = form.Id;
                        chiTiet.Anh = null;

                        _context.HrNhaThauQuaCong6s.Add(chiTiet);
                        await _context.SaveChangesAsync();
                    }

                    // --- BƯỚC 6: LƯU LỊCH SỬ THAO TÁC (LichSuFormHr) ---
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

                    // Cập nhật lại FormHr để lưu FileDinhKem
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

            // --- MỚI: Lấy danh sách nhân sự hỗ trợ hiển thị ra View ---
            ViewBag.ListNguoiHoTro = _context.HrNguoiHoTros
                .Include(x => x.CongViecHrs.Where(cv => cv.Ten == "Đăng ký hỗ trợ tiền điện thoại"))
                .Where(x => x.CongViecHrs.Any(cv => cv.Ten == "Đăng ký hỗ trợ tiền điện thoại"))
                .ToList();

            // 3. Khởi tạo model với đầy đủ thông tin từ CK
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
        public async Task<IActionResult> HoTroTienDienThoai(FormHr form, [FromForm] HrHoTroTienDienThoai7 chiTiet, int[] SelectedCongViecIds)
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
                    form.IdForm = "HR_HoTroTienDienThoai_7";
                    form.TenForm = "Đơn đăng ký hỗ trợ tiền điện thoại";

                    _context.FormHrs.Add(form);
                    await _context.SaveChangesAsync();

                    // --- BƯỚC 2: LƯU NHÂN SỰ HỖ TRỢ ĐÃ CHỌN (MỚI) ---
                    if (SelectedCongViecIds != null && SelectedCongViecIds.Length > 0)
                    {
                        foreach (var cvId in SelectedCongViecIds)
                        {
                            var congViec = await _context.CongViecHrs.FindAsync(cvId);
                            if (congViec != null)
                            {
                                var ctHoTro = new HrCtNguoiHoTro
                                {
                                    IdFormHr = form.Id,
                                    IdHrNguoiHoTro = congViec.IdHrNguoiHoTro,
                                    Stt = 1
                                };
                                _context.HrCtNguoiHoTros.Add(ctHoTro);
                            }
                        }
                        await _context.SaveChangesAsync();
                    }

                    // --- BƯỚC 3: TỰ ĐỘNG THÊM NGƯỜI XÁC NHẬN (CẤU HÌNH THEO BỘ PHẬN & CÔNG TY) ---
                    // Truy vấn lấy người duyệt từ cấu hình dựa trên IdForm, Tên Bộ Phận và Tên Công Ty
                    var listDuyetTheoBoPhan = await _context.DmNguoiDuyetLoaiDonBoPhans
                        .Include(x => x.IdnguoiXacNhanNavigation)
                        .Where(x => x.IdloaiDonNavigation.MaLoaiDon == form.IdForm
                                    && x.IdboPhanNavigation.TenBoPhan == form.BoPhan
                                    && x.IdcongTyNavigation.TenCongTy == form.TenCongTy
                                    && x.TrangThai == true)
                        .OrderBy(x => x.Stt)
                        .ToListAsync();

                    if (listDuyetTheoBoPhan.Any())
                    {
                        foreach (var item in listDuyetTheoBoPhan)
                        {
                            var quanLyDuyet = new HrQuanLyDuyetB2
                            {
                                IdFormHr = form.Id,
                                IdnguoiXacNhan = item.IdnguoiXacNhan,
                                ThuTuXacNhan = item.Stt,
                                MaNguoiXacNhan = item.IdnguoiXacNhanNavigation?.MaNv,
                                TenNguoiXacNhan = item.IdnguoiXacNhanNavigation?.HoTen,
                                TrangThaiXacNhan = 0, // 0: Chờ duyệt
                                ThoiGianXacNhan = null,
                                Loai = item.Loai // Map thêm cột Loai (AND / OR) từ cấu hình để file View nhận diện
                            };
                            _context.HrQuanLyDuyetB2s.Add(quanLyDuyet);
                        }
                        await _context.SaveChangesAsync();
                    }

                    // Fallback: Nếu không có cấu hình theo bộ phận, lấy theo cấu hình mặc định (DmNguoiXacNhanLoaiDon)
                    var loaiDon = await _context.DmLoaiDons.FirstOrDefaultAsync(x => x.MaLoaiDon == form.IdForm && x.TrangThai == true);
                    if (loaiDon != null)
                    {
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
                                ThuTuXacNhan = item.CapDoXacNhan,
                                MaNguoiXacNhan = item.IdnguoiXacNhanNavigation?.MaNv,
                                TenNguoiXacNhan = item.IdnguoiXacNhanNavigation?.HoTen,
                                TrangThaiXacNhan = 0,
                                ThoiGianXacNhan = null
                            };
                            _context.HrNguoiXacNhans.Add(hrXacNhan);
                        }
                        await _context.SaveChangesAsync();
                    }

                    // --- BƯỚC 4: XỬ LÝ FILE/ẢNH TRÊN FILESERVER ---
                    if (!Directory.Exists(networkPath)) Directory.CreateDirectory(networkPath);

                    string safeName = RemoveSign4VietnameseString(userName).Replace(" ", "");
                    string timeStamp = DateTime.Now.ToString("ddMMyy_HHmm");

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

                    if (chiTiet != null)
                    {
                        chiTiet.IdFormHr = form.Id;

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

                        chiTiet.Anh = null;
                        _context.HrHoTroTienDienThoai7s.Add(chiTiet);
                        await _context.SaveChangesAsync();
                    }

                    // --- BƯỚC 5: LƯU LỊCH SỬ THAO TÁC (LichSuFormHr) ---
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

            // --- MỚI: Lấy danh sách nhân sự hỗ trợ hiển thị ra View ---
            ViewBag.ListNguoiHoTro = _context.HrNguoiHoTros
                // 1. Chỉ Include những công việc thỏa mãn điều kiện tên
                .Include(x => x.CongViecHrs.Where(cv => cv.Ten == "Đăng ký đổi ca làm việc"))
                // 2. Chỉ lấy những người có ít nhất 1 công việc thỏa mãn
                .Where(x => x.CongViecHrs.Any(cv => cv.Ten == "Đăng ký đổi ca làm việc"))
                .ToList();

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
        public async Task<IActionResult> DoiCaLam(FormHr form, [FromForm] HrDoiCaLam8 chiTiet, int[] SelectedCongViecIds)
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

                    // --- BƯỚC 2: LƯU NHÂN SỰ HỖ TRỢ ĐÃ CHỌN (MỚI) ---
                    if (SelectedCongViecIds != null && SelectedCongViecIds.Length > 0)
                    {
                        foreach (var cvId in SelectedCongViecIds)
                        {
                            var congViec = await _context.CongViecHrs.FindAsync(cvId);
                            if (congViec != null)
                            {
                                var ctHoTro = new HrCtNguoiHoTro
                                {
                                    IdFormHr = form.Id,
                                    IdHrNguoiHoTro = congViec.IdHrNguoiHoTro,
                                    Stt = 1
                                };
                                _context.HrCtNguoiHoTros.Add(ctHoTro);
                            }
                        }
                        await _context.SaveChangesAsync();
                    }

                    // --- BƯỚC 3: TỰ ĐỘNG THÊM NGƯỜI XÁC NHẬN (CẤU HÌNH THEO BỘ PHẬN & CÔNG TY) ---
                    // Truy vấn lấy người duyệt từ cấu hình dựa trên IdForm, Tên Bộ Phận và Tên Công Ty
                    var listDuyetTheoBoPhan = await _context.DmNguoiDuyetLoaiDonBoPhans
                        .Include(x => x.IdnguoiXacNhanNavigation)
                        .Where(x => x.IdloaiDonNavigation.MaLoaiDon == form.IdForm
                                    && x.IdboPhanNavigation.TenBoPhan == form.BoPhan
                                    && x.IdcongTyNavigation.TenCongTy == form.TenCongTy
                                    && x.TrangThai == true)
                        .OrderBy(x => x.Stt)
                        .ToListAsync();

                    if (listDuyetTheoBoPhan.Any())
                    {
                        foreach (var item in listDuyetTheoBoPhan)
                        {
                            var quanLyDuyet = new HrQuanLyDuyetB2
                            {
                                IdFormHr = form.Id,
                                IdnguoiXacNhan = item.IdnguoiXacNhan,
                                ThuTuXacNhan = item.Stt,
                                MaNguoiXacNhan = item.IdnguoiXacNhanNavigation?.MaNv,
                                TenNguoiXacNhan = item.IdnguoiXacNhanNavigation?.HoTen,
                                TrangThaiXacNhan = 0, // 0: Chờ duyệt
                                ThoiGianXacNhan = null,
                                Loai = item.Loai // Map thêm cột Loai (AND / OR) từ cấu hình để file View nhận diện
                            };
                            _context.HrQuanLyDuyetB2s.Add(quanLyDuyet);
                        }
                        await _context.SaveChangesAsync();
                    }

                    // Fallback: Nếu không có cấu hình theo bộ phận, lấy theo cấu hình mặc định (DmNguoiXacNhanLoaiDon)
                    var loaiDon = await _context.DmLoaiDons.FirstOrDefaultAsync(x => x.MaLoaiDon == form.IdForm && x.TrangThai == true);
                    if (loaiDon != null)
                    {
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
                                ThuTuXacNhan = item.CapDoXacNhan,
                                MaNguoiXacNhan = item.IdnguoiXacNhanNavigation?.MaNv,
                                TenNguoiXacNhan = item.IdnguoiXacNhanNavigation?.HoTen,
                                TrangThaiXacNhan = 0,
                                ThoiGianXacNhan = null
                            };
                            _context.HrNguoiXacNhans.Add(hrXacNhan);
                        }
                        await _context.SaveChangesAsync();
                    }
                    // --- BƯỚC 4: LƯU LỊCH SỬ THAO TÁC ---
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

                    // --- BƯỚC 5: XỬ LÝ FILE/ẢNH TRÊN FILESERVER ---
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

            // --- MỚI: Lấy danh sách nhân sự hỗ trợ hiển thị ra View ---
            ViewBag.ListNguoiHoTro = _context.HrNguoiHoTros
                .Include(x => x.CongViecHrs.Where(cv => cv.Ten == "Đăng ký hỗ trợ công tác"))
                .Where(x => x.CongViecHrs.Any(cv => cv.Ten == "Đăng ký hỗ trợ công tác"))
                .ToList();

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
        public async Task<IActionResult> DonHoTroCongTac(FormHr form, [FromForm] HrDonHoTroCongTac9 hoTro, int[] SelectedCongViecIds)
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

            // Đường dẫn FileServer
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
                    form.IdForm = "HR_DonHoTroCongTac_9";
                    form.TenForm = "Đơn hỗ trợ công tác";
                    form.Danhmuc = "ĐƠN HỖ TRỢ CÔNG TÁC";

                    _context.FormHrs.Add(form);
                    await _context.SaveChangesAsync();

                    // --- BƯỚC 2: LƯU NHÂN SỰ HỖ TRỢ ĐÃ CHỌN (MỚI) ---
                    if (SelectedCongViecIds != null && SelectedCongViecIds.Length > 0)
                    {
                        foreach (var cvId in SelectedCongViecIds)
                        {
                            var congViec = await _context.CongViecHrs.FindAsync(cvId);
                            if (congViec != null)
                            {
                                var ctHoTro = new HrCtNguoiHoTro
                                {
                                    IdFormHr = form.Id,
                                    IdHrNguoiHoTro = congViec.IdHrNguoiHoTro,
                                    Stt = 1
                                };
                                _context.HrCtNguoiHoTros.Add(ctHoTro);
                            }
                        }
                        await _context.SaveChangesAsync();
                    }

                    // --- BƯỚC 3: TỰ ĐỘNG THÊM NGƯỜI XÁC NHẬN (CẤU HÌNH THEO BỘ PHẬN & CÔNG TY) ---
                    // Truy vấn lấy người duyệt từ cấu hình dựa trên IdForm, Tên Bộ Phận và Tên Công Ty
                    var listDuyetTheoBoPhan = await _context.DmNguoiDuyetLoaiDonBoPhans
                        .Include(x => x.IdnguoiXacNhanNavigation)
                        .Where(x => x.IdloaiDonNavigation.MaLoaiDon == form.IdForm
                                    && x.IdboPhanNavigation.TenBoPhan == form.BoPhan
                                    && x.IdcongTyNavigation.TenCongTy == form.TenCongTy
                                    && x.TrangThai == true)
                        .OrderBy(x => x.Stt)
                        .ToListAsync();

                    if (listDuyetTheoBoPhan.Any())
                    {
                        foreach (var item in listDuyetTheoBoPhan)
                        {
                            var quanLyDuyet = new HrQuanLyDuyetB2
                            {
                                IdFormHr = form.Id,
                                IdnguoiXacNhan = item.IdnguoiXacNhan,
                                ThuTuXacNhan = item.Stt,
                                MaNguoiXacNhan = item.IdnguoiXacNhanNavigation?.MaNv,
                                TenNguoiXacNhan = item.IdnguoiXacNhanNavigation?.HoTen,
                                TrangThaiXacNhan = 0, // 0: Chờ duyệt
                                ThoiGianXacNhan = null,
                                Loai = item.Loai // Map thêm cột Loai (AND / OR) từ cấu hình để file View nhận diện
                            };
                            _context.HrQuanLyDuyetB2s.Add(quanLyDuyet);
                        }
                        await _context.SaveChangesAsync();
                    }

                    // Fallback: Nếu không có cấu hình theo bộ phận, lấy theo cấu hình mặc định (DmNguoiXacNhanLoaiDon)
                    var loaiDon = await _context.DmLoaiDons.FirstOrDefaultAsync(x => x.MaLoaiDon == form.IdForm && x.TrangThai == true);
                    if (loaiDon != null)
                    {
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
                                ThuTuXacNhan = item.CapDoXacNhan,
                                MaNguoiXacNhan = item.IdnguoiXacNhanNavigation?.MaNv,
                                TenNguoiXacNhan = item.IdnguoiXacNhanNavigation?.HoTen,
                                TrangThaiXacNhan = 0,
                                ThoiGianXacNhan = null
                            };
                            _context.HrNguoiXacNhans.Add(hrXacNhan);
                        }
                        await _context.SaveChangesAsync();
                    }

                    // --- BƯỚC 4: XỬ LÝ FILE VÀ BẢNG CHI TIẾT (HrDonHoTroCongTac9) ---
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

                        // B. Ảnh minh chứng (Anh)
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
                            hoTro.DuongDanAnh = imgName;
                        }

                        hoTro.Anh = null;
                        _context.HrDonHoTroCongTac9s.Add(hoTro);
                        await _context.SaveChangesAsync();
                    }

                    // --- BƯỚC 5: LƯU LỊCH SỬ THAO TÁC (LichSuFormHr) ---
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

        // Action xem chi tiết: Giữ nguyên 9 loại đơn và bổ sung load danh sách Bước 2
        [HttpGet("/FormHR/ChiTiet/{id}")]
        public async Task<IActionResult> ChiTiet(int id)
        {
            var userIdStr = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userIdStr))
                return RedirectToAction("DangNhap", "DonXetDuyet");

            int userId = int.Parse(userIdStr);
            // Fix lỗi System.Security.Claims.Email bằng cách dùng ClaimTypes.Email
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
                .Include(f => f.HrQuanLyDuyetB2s)
                .FirstOrDefaultAsync(m => m.Id == id);

            if (don == null)
            {
                TempData["Error"] = "⚠️ Không tìm thấy đơn yêu cầu HR!";
                return RedirectToAction("LichSuHR");
            }

            // Sắp xếp nhật ký
            if (don.LichSuFormHrs != null)
                don.LichSuFormHrs = don.LichSuFormHrs.OrderByDescending(x => x.Time).ToList();

            // Load danh sách HR hỗ trợ cho Admin
            if (User.IsInRole("All") || User.IsInRole("AdminHR"))
            {
                ViewBag.ListNguoiHoTro = await _context.HrNguoiHoTros
                    .Where(x => x.BoPhan == "HR")
                    .AsNoTracking()
                    .ToListAsync();
            }

            ViewBag.CurrentUserId = userId;
            ViewBag.UserEmail = userEmail;
            ViewBag.BaoVeRecord = await _context.BaoVeHrs.FirstOrDefaultAsync(x => x.IdFormHr == id);

            ViewBag.DanhSachXacNhan = don.HrNguoiXacNhans?
                .OrderBy(x => x.ThuTuXacNhan).ToList() ?? new List<HrNguoiXacNhan>();

            // ── XỬ LÝ DANH SÁCH BƯỚC 2 ──
            ViewBag.DanhSachB2 = don.HrQuanLyDuyetB2s?
                .OrderBy(x => x.ThuTuXacNhan).ToList() ?? new List<HrQuanLyDuyetB2>();

            return View(don);
        }

        // Action xử lý duyệt Bước 2: Tích hợp logic Duyệt tất cả (AND) vs Duyệt 1 người (OR)
        [HttpPost("/FormHR/PheDuyetB2")]
        public async Task<IActionResult> PheDuyetB2([FromBody] System.Text.Json.JsonElement data)
        {
            try
            {
                int idB2 = data.GetProperty("idB2").GetInt32();
                string action = data.GetProperty("action").GetString(); // "Approve" hoặc "Reject"
                string note = data.GetProperty("note").GetString() ?? "";

                var record = await _context.HrQuanLyDuyetB2s.FindAsync(idB2);
                if (record == null) return Json(new { success = false, message = "Không tìm thấy dữ liệu!" });

                var don = await _context.FormHrs.FindAsync(record.IdFormHr);

                record.TrangThaiXacNhan = (action == "Approve" ? 1 : 2);
                record.ThoiGianXacNhan = DateTime.Now;
                record.GhiChu = note;

                // Logic biểu đạt: Kiểm tra hình thức duyệt (Loai)
                // Giả sử record.Loai == "OR" là chỉ cần 1 người, "AND" là tất cả
                if (action == "Approve" && (record.Loai == "OR" || record.Loai == "ANY"))
                {
                    // Nếu là loại OR, khi 1 người duyệt, ta có thể đánh dấu các người khác trong cùng bước là "Skip" hoặc đơn giản là đơn chuyển trạng thái
                    _context.LichSuFormHrs.Add(new LichSuFormHr
                    {
                        IdFormHr = don.Id,
                        TieuDe = "PHÊ DUYỆT BƯỚC 2 (OR)",
                        Mota = $"Người duyệt {record.TenNguoiXacNhan} đã phê duyệt. Hệ thống xác nhận hoàn tất bước này.",
                        Time = DateTime.Now
                    });
                }
                else
                {
                    _context.LichSuFormHrs.Add(new LichSuFormHr
                    {
                        IdFormHr = don.Id,
                        TieuDe = action == "Approve" ? "BƯỚC 2: CHẤP THUẬN" : "BƯỚC 2: TỪ CHỐI",
                        Mota = $"Nhân viên {record.TenNguoiXacNhan} đã xử lý. Nội dung: {note}",
                        Time = DateTime.Now
                    });
                }

                await _context.SaveChangesAsync();
                return Json(new { success = true, message = "Cập nhật trạng thái thành công!" });
            }
            catch (Exception ex) { return Json(new { success = false, message = "Lỗi: " + ex.Message }); }
        }

        [HttpGet("/FormHR/DownloadFile/{fileName}")]
        public async Task<IActionResult> DownloadFile(string fileName)
        {
            if (string.IsNullOrEmpty(fileName)) return NotFound();
            string networkPath = @"\\10.0.60.30\BPVN-Fileserver\Public\IT-Information Technology Dept\5.E-Form\DonHR";
            string fullPath = Path.Combine(networkPath, fileName);
            if (!System.IO.File.Exists(fullPath)) return NotFound("Tệp tin không tồn tại.");

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
                ".pdf" => "application/pdf",
                ".xlsx" => "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                _ => "application/octet-stream"
            };
            return contentType.StartsWith("image/") ? File(memory, contentType) : File(memory, contentType, fileName);
        }

        [HttpPost("/FormHR/ThemNguoiHoTro")]
        public async Task<IActionResult> ThemNguoiHoTro([FromBody] System.Text.Json.JsonElement data)
        {
            var roles = User.FindAll(ClaimTypes.Role).Select(r => r.Value).ToList();
            if (!roles.Any(r => r == "AdminHR" || r == "All"))
                return Json(new { success = false, message = "Không có quyền!" });

            try
            {
                int idForm = data.GetProperty("idFormHr").GetInt32();
                string maNvMoi = data.GetProperty("maNv").GetString();
                var nvHr = await _context.HrNguoiHoTros.FirstOrDefaultAsync(x => x.MaNv == maNvMoi);

                var hienTai = await _context.HrCtNguoiHoTros.Where(x => x.IdFormHr == idForm).OrderByDescending(x => x.Stt).FirstOrDefaultAsync();
                if (hienTai?.IdHrNguoiHoTroNavigation?.MaNv == maNvMoi)
                    return Json(new { success = false, message = "Nhân viên này đang xử lý rồi!" });

                _context.HrCtNguoiHoTros.Add(new HrCtNguoiHoTro { IdFormHr = idForm, IdHrNguoiHoTro = nvHr.Id, Stt = (hienTai?.Stt ?? 0) + 1 });
                _context.LichSuFormHrs.Add(new LichSuFormHr
                {
                    IdFormHr = idForm,
                    TieuDe = "CHỈ ĐỊNH NGƯỜI HỖ TRỢ",
                    Mota = $"Thay đổi sang: {nvHr.Ten} ({nvHr.MaNv})",
                    Time = DateTime.Now
                });
                await _context.SaveChangesAsync();
                return Json(new { success = true });
            }
            catch (Exception ex) { return Json(new { success = false, message = ex.Message }); }
        }

        #endregion

        #region Xuất file Excel, Word, PDF

        // ============================================================
        // ACTION XUẤT BIỂU MẪU (EXCEL, WORD, PDF)
        // ============================================================

        [HttpGet("/FormHR/ExportExcel/{id}")]
        public async Task<IActionResult> ExportExcel(int id)
        {
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
                .Include(f => f.HrQuanLyDuyetB2s)
                .Include(f => f.HrNguoiXacNhans).ThenInclude(x => x.IdnguoiXacNhanNavigation)
                .FirstOrDefaultAsync(m => m.Id == id);

            if (don == null || don.IdAdmin == null)
                return NotFound("Đơn chưa hoàn tất hoặc không tồn tại.");

            using (var workbook = new ClosedXML.Excel.XLWorkbook())
            {
                var worksheet = workbook.Worksheets.Add("ChiTietDon");

                // Header chung
                worksheet.Cell(1, 1).Value = "HƯỚNG DẪN SỬ DỤNG HỆ THỐNG E-FORM HR - BEST PACIFIC";
                worksheet.Range("A1:E1").Merge().Style.Font.SetBold().Font.FontSize = 14;
                worksheet.Range("A1:E1").Style.Alignment.SetHorizontal(ClosedXML.Excel.XLAlignmentHorizontalValues.Center);

                worksheet.Cell(3, 1).Value = "Mã Đơn:"; worksheet.Cell(3, 2).Value = don.Id;
                worksheet.Cell(4, 1).Value = "Tên Form:"; worksheet.Cell(4, 2).Value = don.TenForm;
                worksheet.Cell(5, 1).Value = "Mã NV:"; worksheet.Cell(5, 2).Value = don.SoNhanVien;
                worksheet.Cell(6, 1).Value = "Họ Tên:"; worksheet.Cell(6, 2).Value = don.TenNguoiNv;
                worksheet.Cell(7, 1).Value = "Bộ Phận:"; worksheet.Cell(7, 2).Value = don.BoPhan;
                worksheet.Cell(8, 1).Value = "Ngày Tạo:"; worksheet.Cell(8, 2).Value = don.TimeNguoiTao?.ToString("dd/MM/yyyy HH:mm");
                worksheet.Cell(9, 1).Value = "Trạng Thái:"; worksheet.Cell(9, 2).Value = "HOÀN TẤT";

                var rangeChung = worksheet.Range("A3:B9");
                rangeChung.Style.Border.SetOutsideBorder(ClosedXML.Excel.XLBorderStyleValues.Thin);
                rangeChung.Style.Border.SetInsideBorder(ClosedXML.Excel.XLBorderStyleValues.Thin);
                worksheet.Range("A3:A9").Style.Font.SetBold().Fill.SetBackgroundColor(ClosedXML.Excel.XLColor.LightGray);

                int currentRow = 11;

                // Xử lý 9 loại đơn chi tiết
                if (don.HrXinRaNgoai1s.Any())
                {
                    var ct = don.HrXinRaNgoai1s.First();
                    worksheet.Cell(currentRow, 1).Value = "I. CHI TIẾT ĐƠN: XIN RA NGOÀI";
                    worksheet.Range(currentRow, 1, currentRow, 2).Merge().Style.Font.SetBold().Fill.SetBackgroundColor(ClosedXML.Excel.XLColor.LightBlue);
                    currentRow++;
                    worksheet.Cell(currentRow, 1).Value = "Lý do:"; worksheet.Cell(currentRow, 2).Value = ct.LiDo; currentRow++;
                    worksheet.Cell(currentRow, 1).Value = "Địa điểm:"; worksheet.Cell(currentRow, 2).Value = ct.DiaDiem; currentRow++;
                    worksheet.Cell(currentRow, 1).Value = "Thời gian ra:"; worksheet.Cell(currentRow, 2).Value = ct.ThoiGianRa?.ToString("dd/MM/yyyy HH:mm"); currentRow++;
                    worksheet.Cell(currentRow, 1).Value = "Dự kiến về:"; worksheet.Cell(currentRow, 2).Value = ct.ThoiGianVeDuTinh?.ToString("dd/MM/yyyy HH:mm"); currentRow++;
                }
                else if (don.HrMangHangHoaRaCong2s.Any())
                {
                    var ct = don.HrMangHangHoaRaCong2s.First();
                    worksheet.Cell(currentRow, 1).Value = "I. CHI TIẾT ĐƠN: MANG HÀNG HÓA RA CỔNG";
                    worksheet.Range(currentRow, 1, currentRow, 2).Merge().Style.Font.SetBold().Fill.SetBackgroundColor(ClosedXML.Excel.XLColor.LightGreen);
                    currentRow++;
                    worksheet.Cell(currentRow, 1).Value = "Mô tả:"; worksheet.Cell(currentRow, 2).Value = ct.MoTa; currentRow++;
                    worksheet.Cell(currentRow, 1).Value = "Thời gian dự tính:"; worksheet.Cell(currentRow, 2).Value = ct.TimeDuTinh?.ToString("dd/MM/yyyy HH:mm"); currentRow++;
                }
                else if (don.HrDangKySuDungXeCongTac3s.Any())
                {
                    var ct = don.HrDangKySuDungXeCongTac3s.First();
                    worksheet.Cell(currentRow, 1).Value = "I. CHI TIẾT ĐƠN: ĐĂNG KÝ XE CÔNG TÁC";
                    worksheet.Range(currentRow, 1, currentRow, 2).Merge().Style.Font.SetBold().Fill.SetBackgroundColor(ClosedXML.Excel.XLColor.LightYellow);
                    currentRow++;
                    worksheet.Cell(currentRow, 1).Value = "Số điện thoại:"; worksheet.Cell(currentRow, 2).Value = ct.SoDienThoai; currentRow++;
                    worksheet.Cell(currentRow, 1).Value = "Số lượng người:"; worksheet.Cell(currentRow, 2).Value = ct.SoLuong; currentRow++;
                    worksheet.Cell(currentRow, 1).Value = "Lý do / Lộ trình:"; worksheet.Cell(currentRow, 2).Value = ct.LiDo; currentRow++;
                    worksheet.Cell(currentRow, 1).Value = "TG Đi:"; worksheet.Cell(currentRow, 2).Value = ct.TimeDuTinh?.ToString("dd/MM/yyyy HH:mm"); currentRow++;
                    worksheet.Cell(currentRow, 1).Value = "TG Về:"; worksheet.Cell(currentRow, 2).Value = ct.ThoiGianVe?.ToString("dd/MM/yyyy HH:mm"); currentRow++;
                    worksheet.Cell(currentRow, 1).Value = "Ghi chú:"; worksheet.Cell(currentRow, 2).Value = ct.GhiChu; currentRow++;
                }
                else if (don.HrDangKySuDungXeDaily4s.Any())
                {
                    var ct = don.HrDangKySuDungXeDaily4s.First();
                    worksheet.Cell(currentRow, 1).Value = "I. CHI TIẾT ĐƠN: ĐĂNG KÝ XE DAILY";
                    worksheet.Range(currentRow, 1, currentRow, 2).Merge().Style.Font.SetBold().Fill.SetBackgroundColor(ClosedXML.Excel.XLColor.Orange);
                    currentRow++;
                    worksheet.Cell(currentRow, 1).Value = "Điểm đón:"; worksheet.Cell(currentRow, 2).Value = ct.DiemDon; currentRow++;
                    worksheet.Cell(currentRow, 1).Value = "Lý do:"; worksheet.Cell(currentRow, 2).Value = ct.LiDo; currentRow++;
                    worksheet.Cell(currentRow, 1).Value = "Thời gian:"; worksheet.Cell(currentRow, 2).Value = ct.TimeDuTinh?.ToString("dd/MM/yyyy HH:mm"); currentRow++;
                }
                else if (don.HrDonTiepKhac5s.Any())
                {
                    var ct = don.HrDonTiepKhac5s.First();
                    worksheet.Cell(currentRow, 1).Value = "I. CHI TIẾT ĐƠN: ĐĂNG KÝ TIẾP KHÁCH";
                    worksheet.Range(currentRow, 1, currentRow, 2).Merge().Style.Font.SetBold().Fill.SetBackgroundColor(ClosedXML.Excel.XLColor.Pink);
                    currentRow++;
                    worksheet.Cell(currentRow, 1).Value = "Công ty khách:"; worksheet.Cell(currentRow, 2).Value = ct.TenCongTyKhach; currentRow++;
                    worksheet.Cell(currentRow, 1).Value = "Số lượng khách:"; worksheet.Cell(currentRow, 2).Value = ct.SoLuongKhach; currentRow++;
                    worksheet.Cell(currentRow, 1).Value = "Người đặt:"; worksheet.Cell(currentRow, 2).Value = ct.NguoiBook; currentRow++;
                    worksheet.Cell(currentRow, 1).Value = "Yêu cầu:"; worksheet.Cell(currentRow, 2).Value = ct.YeuCauTiepKhach; currentRow++;
                    worksheet.Cell(currentRow, 1).Value = "Phòng họp:"; worksheet.Cell(currentRow, 2).Value = ct.TenPhongHop; currentRow++;
                    worksheet.Cell(currentRow, 1).Value = "Loại suất ăn:"; worksheet.Cell(currentRow, 2).Value = ct.LoaiSuatAn; currentRow++;
                    worksheet.Cell(currentRow, 1).Value = "Ghi chú suất ăn:"; worksheet.Cell(currentRow, 2).Value = ct.GhiChuSuatAn; currentRow++;
                }
                else if (don.HrNhaThauQuaCong6s.Any())
                {
                    var ct = don.HrNhaThauQuaCong6s.First();
                    worksheet.Cell(currentRow, 1).Value = "I. CHI TIẾT ĐƠN: NHÀ THẦU QUA CỔNG";
                    worksheet.Range(currentRow, 1, currentRow, 2).Merge().Style.Font.SetBold().Fill.SetBackgroundColor(ClosedXML.Excel.XLColor.LightGray);
                    currentRow++;
                    worksheet.Cell(currentRow, 1).Value = "Tên nhà thầu:"; worksheet.Cell(currentRow, 2).Value = ct.TenNhaThau; currentRow++;
                    worksheet.Cell(currentRow, 1).Value = "Số người:"; worksheet.Cell(currentRow, 2).Value = ct.SoNguoi; currentRow++;
                    worksheet.Cell(currentRow, 1).Value = "Người đăng ký:"; worksheet.Cell(currentRow, 2).Value = ct.NguoiDangKy; currentRow++;
                    worksheet.Cell(currentRow, 1).Value = "Mục đích:"; worksheet.Cell(currentRow, 2).Value = ct.MucDichCongViec; currentRow++;
                }
                else if (don.HrHoTroTienDienThoai7s.Any())
                {
                    var ct = don.HrHoTroTienDienThoai7s.First();
                    worksheet.Cell(currentRow, 1).Value = "I. CHI TIẾT ĐƠN: HỖ TRỢ TIỀN ĐIỆN THOẠI";
                    worksheet.Range(currentRow, 1, currentRow, 2).Merge().Style.Font.SetBold().Fill.SetBackgroundColor(ClosedXML.Excel.XLColor.Gold);
                    currentRow++;
                    worksheet.Cell(currentRow, 1).Value = "Số điện thoại:"; worksheet.Cell(currentRow, 2).Value = ct.SoDienThoai; currentRow++;
                    worksheet.Cell(currentRow, 1).Value = "Mức hỗ trợ:"; worksheet.Cell(currentRow, 2).Value = ct.MucHoTro; currentRow++;
                    worksheet.Cell(currentRow, 1).Value = "Mục đích:"; worksheet.Cell(currentRow, 2).Value = ct.MucDich; currentRow++;
                }
                else if (don.HrDoiCaLam8s.Any())
                {
                    var ct = don.HrDoiCaLam8s.First();
                    worksheet.Cell(currentRow, 1).Value = "I. CHI TIẾT ĐƠN: ĐỔI CA LÀM VIỆC";
                    worksheet.Range(currentRow, 1, currentRow, 2).Merge().Style.Font.SetBold().Fill.SetBackgroundColor(ClosedXML.Excel.XLColor.Cyan);
                    currentRow++;
                    worksheet.Cell(currentRow, 1).Value = "Ngày cần đổi:"; worksheet.Cell(currentRow, 2).Value = ct.NgayCanDoi?.ToString("dd/MM/yyyy"); currentRow++;
                    worksheet.Cell(currentRow, 1).Value = "Ca gốc:"; worksheet.Cell(currentRow, 2).Value = ct.CaGoc; currentRow++;
                    worksheet.Cell(currentRow, 1).Value = "Ca muốn đổi:"; worksheet.Cell(currentRow, 2).Value = ct.CaMuonDoi; currentRow++;
                    worksheet.Cell(currentRow, 1).Value = "Lý do:"; worksheet.Cell(currentRow, 2).Value = ct.LyDoDoiCa; currentRow++;
                }
                else if (don.HrDonHoTroCongTac9s.Any())
                {
                    var ct = don.HrDonHoTroCongTac9s.First();
                    worksheet.Cell(currentRow, 1).Value = "I. CHI TIẾT ĐƠN: HỖ TRỢ CÔNG TÁC";
                    worksheet.Range(currentRow, 1, currentRow, 2).Merge().Style.Font.SetBold().Fill.SetBackgroundColor(ClosedXML.Excel.XLColor.Purple);
                    currentRow++;
                    worksheet.Cell(currentRow, 1).Value = "Mã NV Công tác:"; worksheet.Cell(currentRow, 2).Value = ct.MaNhanVien; currentRow++;
                    worksheet.Cell(currentRow, 1).Value = "Khách hàng:"; worksheet.Cell(currentRow, 2).Value = ct.TenKhachHang; currentRow++;
                    worksheet.Cell(currentRow, 1).Value = "Đặt vé máy bay:"; worksheet.Cell(currentRow, 2).Value = ct.DatVeMayBay == true ? "Có" : "Không"; currentRow++;
                    worksheet.Cell(currentRow, 1).Value = "Đặt chỗ ở:"; worksheet.Cell(currentRow, 2).Value = ct.DatChoO == true ? "Có" : "Không"; currentRow++;
                    worksheet.Cell(currentRow, 1).Value = "Đặt bữa ăn:"; worksheet.Cell(currentRow, 2).Value = ct.DatBuaAn == true ? "Có" : "Không"; currentRow++;
                    worksheet.Cell(currentRow, 1).Value = "Xe đưa đón:"; worksheet.Cell(currentRow, 2).Value = ct.BookXeCtyDuaDon == true ? "Có" : "Không"; currentRow++;
                    worksheet.Cell(currentRow, 1).Value = "Chi tiết:"; worksheet.Cell(currentRow, 2).Value = ct.NoiDungYeuCauChiTiet; currentRow++;
                }

                if (currentRow > 11)
                {
                    var rangeChiTiet = worksheet.Range(11, 1, currentRow - 1, 2);
                    rangeChiTiet.Style.Border.SetOutsideBorder(ClosedXML.Excel.XLBorderStyleValues.Thin);
                    rangeChiTiet.Style.Border.SetInsideBorder(ClosedXML.Excel.XLBorderStyleValues.Thin);
                    worksheet.Range(12, 1, currentRow - 1, 1).Style.Font.SetBold().Fill.SetBackgroundColor(ClosedXML.Excel.XLColor.WhiteSmoke);
                }

                currentRow++;

                // BƯỚC 2: QUẢN LÝ DUYỆT B2
                if (don.HrQuanLyDuyetB2s != null && don.HrQuanLyDuyetB2s.Any())
                {
                    worksheet.Cell(currentRow, 1).Value = "II. DANH SÁCH DUYỆT BƯỚC 2";
                    worksheet.Range(currentRow, 1, currentRow, 4).Merge().Style.Font.SetBold().Fill.SetBackgroundColor(ClosedXML.Excel.XLColor.Lavender);
                    currentRow++;

                    worksheet.Cell(currentRow, 1).Value = "Tên Người Duyệt";
                    worksheet.Cell(currentRow, 2).Value = "Thời Gian";
                    worksheet.Cell(currentRow, 3).Value = "Trạng Thái";
                    worksheet.Cell(currentRow, 4).Value = "Ghi Chú";
                    worksheet.Range(currentRow, 1, currentRow, 4).Style.Font.SetBold().Fill.SetBackgroundColor(ClosedXML.Excel.XLColor.LightGray);

                    int startRow = currentRow;
                    foreach (var b2 in don.HrQuanLyDuyetB2s.OrderBy(x => x.ThuTuXacNhan))
                    {
                        currentRow++;
                        string ttText = b2.TrangThaiXacNhan == 1 ? "Đã Duyệt" : b2.TrangThaiXacNhan == 2 ? "Từ Chối" : "Chờ Duyệt";
                        worksheet.Cell(currentRow, 1).Value = b2.TenNguoiXacNhan;
                        worksheet.Cell(currentRow, 2).Value = b2.ThoiGianXacNhan?.ToString("dd/MM/yyyy HH:mm");
                        worksheet.Cell(currentRow, 3).Value = ttText;
                        worksheet.Cell(currentRow, 4).Value = b2.GhiChu;
                    }
                    var rangeB2 = worksheet.Range(startRow, 1, currentRow, 4);
                    rangeB2.Style.Border.SetOutsideBorder(ClosedXML.Excel.XLBorderStyleValues.Thin);
                    rangeB2.Style.Border.SetInsideBorder(ClosedXML.Excel.XLBorderStyleValues.Thin);
                    currentRow++;
                }

                // BƯỚC 3: GIÁM ĐỐC XÁC NHẬN
                if (don.HrNguoiXacNhans != null && don.HrNguoiXacNhans.Any())
                {
                    worksheet.Cell(currentRow, 1).Value = "III. XÁC NHẬN GIÁM ĐỐC";
                    worksheet.Range(currentRow, 1, currentRow, 4).Merge().Style.Font.SetBold().Fill.SetBackgroundColor(ClosedXML.Excel.XLColor.MistyRose);
                    currentRow++;

                    worksheet.Cell(currentRow, 1).Value = "Tên Giám Đốc";
                    worksheet.Cell(currentRow, 2).Value = "Thời Gian";
                    worksheet.Cell(currentRow, 3).Value = "Trạng Thái";
                    worksheet.Cell(currentRow, 4).Value = "Ghi Chú";
                    worksheet.Range(currentRow, 1, currentRow, 4).Style.Font.SetBold().Fill.SetBackgroundColor(ClosedXML.Excel.XLColor.LightGray);

                    int startRow = currentRow;
                    foreach (var xn in don.HrNguoiXacNhans)
                    {
                        currentRow++;
                        string ttText = xn.TrangThaiXacNhan == 1 ? "Đã Duyệt" : xn.TrangThaiXacNhan == 2 ? "Từ Chối" : "Chờ Duyệt";
                        worksheet.Cell(currentRow, 1).Value = xn.IdnguoiXacNhanNavigation?.HoTen ?? xn.TenNguoiXacNhan;
                        worksheet.Cell(currentRow, 2).Value = xn.ThoiGianXacNhan?.ToString("dd/MM/yyyy HH:mm");
                        worksheet.Cell(currentRow, 3).Value = ttText;
                        worksheet.Cell(currentRow, 4).Value = xn.GhiChu;
                    }
                    var rangeXN = worksheet.Range(startRow, 1, currentRow, 4);
                    rangeXN.Style.Border.SetOutsideBorder(ClosedXML.Excel.XLBorderStyleValues.Thin);
                    rangeXN.Style.Border.SetInsideBorder(ClosedXML.Excel.XLBorderStyleValues.Thin);
                }

                worksheet.Columns().AdjustToContents();

                using (var stream = new System.IO.MemoryStream())
                {
                    workbook.SaveAs(stream);
                    return File(stream.ToArray(), "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", $"Don_EFormHR_{id}.xlsx");
                }
            }
        }

        [HttpGet("/FormHR/ExportWord/{id}")]
        public async Task<IActionResult> ExportWord(int id)
        {
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
                .Include(f => f.HrQuanLyDuyetB2s)
                .Include(f => f.HrNguoiXacNhans).ThenInclude(x => x.IdnguoiXacNhanNavigation)
                .FirstOrDefaultAsync(m => m.Id == id);

            if (don == null || don.IdAdmin == null)
                return NotFound("Đơn chưa hoàn tất hoặc không tồn tại.");

            string htmlContent = BuildHtmlContent(don, isForWord: true);

            byte[] byteArray = System.Text.Encoding.UTF8.GetBytes(htmlContent);
            return File(byteArray, "application/msword", $"Don_EFormHR_{id}.doc");
        }

        [HttpGet("/FormHR/ExportPDF/{id}")]
        public async Task<IActionResult> ExportPDF(int id)
        {
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
                .Include(f => f.HrQuanLyDuyetB2s)
                .Include(f => f.HrNguoiXacNhans).ThenInclude(x => x.IdnguoiXacNhanNavigation)
                .FirstOrDefaultAsync(m => m.Id == id);

            if (don == null || don.IdAdmin == null)
                return NotFound("Đơn chưa hoàn tất hoặc không tồn tại.");

            string htmlContent = BuildHtmlContent(don, isForWord: false);
            return Content(htmlContent, "text/html", System.Text.Encoding.UTF8);
        }

        // ============================================================
        // HÀM HỖ TRỢ BUILD HTML CHUYÊN NGHIỆP CHO WORD & PDF
        // ============================================================
        private string BuildHtmlContent(FormHr don, bool isForWord = false)
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
                .status-badge { font-weight: bold; color: #000; }
                .signature-table { width: 100%; text-align: center; margin-top: 40px; border: none; page-break-inside: avoid; }
                .signature-table td { width: 25%; vertical-align: top; border: none; font-size: 12pt; }
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

            // Phần Quốc hiệu, Tiêu ngữ và Tên công ty
            sb.Append("<table class='header-table'><tr>");
            sb.Append("<td style='width:40%;'><div class='company-name'>BEST PACIFIC</div><div class='company-sub'>PHÒNG NHÂN SỰ (HR)</div></td>");
            sb.Append("<td style='width:60%;'><div class='national-title'>CỘNG HÒA XÃ HỘI CHỦ NGHĨA VIỆT NAM</div><div class='national-sub'>Độc lập - Tự do - Hạnh phúc</div></td>");
            sb.Append("</tr></table>");

            // Tên đơn
            sb.Append($"<div class='form-title'>{don.TenForm}</div>");
            sb.Append($"<div class='form-id'>Mã phiếu: #{don.Id} | Trạng thái: HOÀN TẤT</div>");

            // I. THÔNG TIN NGƯỜI TẠO
            sb.Append("<div class='section-title'>I. THÔNG TIN NGƯỜI TẠO ĐƠN</div>");
            sb.Append("<table class='data-table'>");
            sb.Append($"<tr><th>Họ và tên nhân viên</th><td>{don.TenNguoiNv}</td></tr>");
            sb.Append($"<tr><th>Mã nhân viên</th><td>{don.SoNhanVien}</td></tr>");
            sb.Append($"<tr><th>Bộ phận / Phòng ban</th><td>{don.BoPhan}</td></tr>");
            sb.Append($"<tr><th>Thời gian lập đơn</th><td>{don.TimeNguoiTao?.ToString("dd/MM/yyyy HH:mm")}</td></tr>");
            sb.Append("</table>");

            // II. CHI TIẾT YÊU CẦU
            sb.Append("<div class='section-title'>II. NỘI DUNG CHI TIẾT</div>");
            sb.Append("<table class='data-table'>");
            if (don.HrXinRaNgoai1s.Any())
            {
                var ct = don.HrXinRaNgoai1s.First();
                sb.Append($"<tr><th>Loại yêu cầu</th><td style='font-weight:bold;'>Xin ra ngoài</td></tr>");
                sb.Append($"<tr><th>Địa điểm</th><td>{ct.DiaDiem}</td></tr>");
                sb.Append($"<tr><th>Lý do</th><td>{ct.LiDo}</td></tr>");
                sb.Append($"<tr><th>Giờ ra dự kiến</th><td>{ct.ThoiGianRa?.ToString("HH:mm dd/MM/yyyy")}</td></tr>");
                sb.Append($"<tr><th>Giờ về dự kiến</th><td>{ct.ThoiGianVeDuTinh?.ToString("HH:mm dd/MM/yyyy")}</td></tr>");
            }
            else if (don.HrMangHangHoaRaCong2s.Any())
            {
                var ct = don.HrMangHangHoaRaCong2s.First();
                sb.Append($"<tr><th>Loại yêu cầu</th><td style='font-weight:bold;'>Mang hàng hóa ra cổng</td></tr>");
                sb.Append($"<tr><th>Mô tả chi tiết hàng hóa</th><td>{ct.MoTa}</td></tr>");
                sb.Append($"<tr><th>Thời gian ra dự tính</th><td>{ct.TimeDuTinh?.ToString("HH:mm dd/MM/yyyy")}</td></tr>");
            }
            else if (don.HrDangKySuDungXeCongTac3s.Any())
            {
                var ct = don.HrDangKySuDungXeCongTac3s.First();
                sb.Append($"<tr><th>Loại yêu cầu</th><td style='font-weight:bold;'>Đăng ký xe công tác</td></tr>");
                sb.Append($"<tr><th>Số điện thoại liên hệ</th><td>{ct.SoDienThoai}</td></tr>");
                sb.Append($"<tr><th>Số lượng hành khách</th><td>{ct.SoLuong} người</td></tr>");
                sb.Append($"<tr><th>Mục đích / Lộ trình</th><td>{ct.LiDo}</td></tr>");
                sb.Append($"<tr><th>Thời gian khởi hành</th><td>{ct.TimeDuTinh?.ToString("HH:mm dd/MM/yyyy")}</td></tr>");
                sb.Append($"<tr><th>Thời gian về dự kiến</th><td>{ct.ThoiGianVe?.ToString("HH:mm dd/MM/yyyy")}</td></tr>");
                sb.Append($"<tr><th>Ghi chú thêm</th><td>{ct.GhiChu}</td></tr>");
            }
            else if (don.HrDangKySuDungXeDaily4s.Any())
            {
                var ct = don.HrDangKySuDungXeDaily4s.First();
                sb.Append($"<tr><th>Loại yêu cầu</th><td style='font-weight:bold;'>Đăng ký xe Daily</td></tr>");
                sb.Append($"<tr><th>Điểm đón</th><td>{ct.DiemDon}</td></tr>");
                sb.Append($"<tr><th>Lý do sử dụng</th><td>{ct.LiDo}</td></tr>");
                sb.Append($"<tr><th>Thời gian sử dụng</th><td>{ct.TimeDuTinh?.ToString("HH:mm dd/MM/yyyy")}</td></tr>");
            }
            else if (don.HrDonTiepKhac5s.Any())
            {
                var ct = don.HrDonTiepKhac5s.First();
                sb.Append($"<tr><th>Loại yêu cầu</th><td style='font-weight:bold;'>Đăng ký tiếp khách</td></tr>");
                sb.Append($"<tr><th>Tên công ty khách</th><td>{ct.TenCongTyKhach}</td></tr>");
                sb.Append($"<tr><th>Số lượng khách</th><td>{ct.SoLuongKhach} người</td></tr>");
                sb.Append($"<tr><th>Thông tin người đặt</th><td>{ct.NguoiBook} (Ext: {ct.SoMayBan})</td></tr>");
                sb.Append($"<tr><th>Yêu cầu đặc biệt</th><td>{ct.YeuCauTiepKhach}</td></tr>");
                sb.Append($"<tr><th>Phòng họp / Tiện ích</th><td>{ct.TenPhongHop} - {ct.NhuCauPhongHop}</td></tr>");
                sb.Append($"<tr><th>Thời gian họp</th><td>{ct.ThoiGianBatDau?.ToString("HH:mm")} - {ct.ThoiGianKetThuc?.ToString("HH:mm dd/MM/yyyy")}</td></tr>");
                sb.Append($"<tr><th>Thông tin suất ăn</th><td>Loại: {ct.LoaiSuatAn} ({ct.SoLuongSuat} suất) | Chay: {ct.AnChay} ({ct.SoLuongSuatAnChay} suất)</td></tr>");
                sb.Append($"<tr><th>Ghi chú suất ăn</th><td>{ct.GhiChuSuatAn}</td></tr>");
            }
            else if (don.HrNhaThauQuaCong6s.Any())
            {
                var ct = don.HrNhaThauQuaCong6s.First();
                sb.Append($"<tr><th>Loại yêu cầu</th><td style='font-weight:bold;'>Nhà thầu qua cổng</td></tr>");
                sb.Append($"<tr><th>Tên nhà thầu</th><td>{ct.TenNhaThau}</td></tr>");
                sb.Append($"<tr><th>Số lượng người</th><td>{ct.SoNguoi} người</td></tr>");
                sb.Append($"<tr><th>Đại diện đăng ký</th><td>{ct.NguoiDangKy}</td></tr>");
                sb.Append($"<tr><th>Mục đích công việc</th><td>{ct.MucDichCongViec}</td></tr>");
            }
            else if (don.HrHoTroTienDienThoai7s.Any())
            {
                var ct = don.HrHoTroTienDienThoai7s.First();
                sb.Append($"<tr><th>Loại yêu cầu</th><td style='font-weight:bold;'>Hỗ trợ tiền điện thoại</td></tr>");
                sb.Append($"<tr><th>Số điện thoại</th><td>{ct.SoDienThoai}</td></tr>");
                sb.Append($"<tr><th>Mức đề nghị hỗ trợ</th><td>{(ct.MucHoTro.HasValue ? ct.MucHoTro.Value.ToString("N0") : "0")} VNĐ</td></tr>");
                sb.Append($"<tr><th>Mục đích hỗ trợ</th><td>{ct.MucDich}</td></tr>");
            }
            else if (don.HrDoiCaLam8s.Any())
            {
                var ct = don.HrDoiCaLam8s.First();
                sb.Append($"<tr><th>Loại yêu cầu</th><td style='font-weight:bold;'>Đổi ca làm việc</td></tr>");
                sb.Append($"<tr><th>Ngày thực hiện đổi</th><td>{ct.NgayCanDoi?.ToString("dd/MM/yyyy")}</td></tr>");
                sb.Append($"<tr><th>Nội dung đổi</th><td>Từ ca: {ct.CaGoc} sang ca: {ct.CaMuonDoi}</td></tr>");
                sb.Append($"<tr><th>Lý do đổi ca</th><td>{ct.LyDoDoiCa}</td></tr>");
            }
            else if (don.HrDonHoTroCongTac9s.Any())
            {
                var ct = don.HrDonHoTroCongTac9s.First();
                sb.Append($"<tr><th>Loại yêu cầu</th><td style='font-weight:bold;'>Hỗ trợ công tác</td></tr>");
                sb.Append($"<tr><th>Mã NV người công tác</th><td>{ct.MaNhanVien}</td></tr>");
                sb.Append($"<tr><th>Khách hàng làm việc</th><td>[{ct.GioiTinh}] {ct.TenKhachHang}</td></tr>");
                List<string> srv = new List<string>();
                if (ct.DatVeMayBay == true) srv.Add("Vé máy bay");
                if (ct.DatChoO == true) srv.Add("Chỗ ở");
                if (ct.BookXeCtyDuaDon == true) srv.Add("Xe đưa đón");
                if (ct.DatBuaAn == true) srv.Add("Đặt bữa ăn");
                sb.Append($"<tr><th>Các dịch vụ đề nghị</th><td>{(srv.Count > 0 ? string.Join(", ", srv) : "Không có")}</td></tr>");
                sb.Append($"<tr><th>Nội dung chi tiết</th><td>{ct.NoiDungYeuCauChiTiet}</td></tr>");
            }
            sb.Append("</table>");

            // III. LỊCH SỬ PHÊ DUYỆT (Bước 2 & Giám Đốc)
            bool hasB2 = don.HrQuanLyDuyetB2s != null && don.HrQuanLyDuyetB2s.Any();
            bool hasGD = don.HrNguoiXacNhans != null && don.HrNguoiXacNhans.Any();

            if (hasB2 || hasGD)
            {
                sb.Append("<div class='section-title'>III. LỊCH SỬ PHÊ DUYỆT</div>");
                sb.Append("<table class='data-table'>");
                sb.Append("<tr><th style='width:30%'>Cấp duyệt</th><th style='width:30%'>Họ và tên</th><th style='width:20%'>Trạng thái</th><th style='width:20%'>Thời gian</th></tr>");

                if (hasB2)
                {
                    foreach (var b2 in don.HrQuanLyDuyetB2s.OrderBy(x => x.ThuTuXacNhan))
                    {
                        string tt = b2.TrangThaiXacNhan == 1 ? "Đã duyệt" : b2.TrangThaiXacNhan == 2 ? "Từ chối" : "Chờ duyệt";
                        sb.Append($"<tr><td>Quản lý (Bước 2)</td><td>{b2.TenNguoiXacNhan}</td><td>{tt}</td><td>{b2.ThoiGianXacNhan?.ToString("HH:mm dd/MM/yyyy")}</td></tr>");
                        if (!string.IsNullOrEmpty(b2.GhiChu))
                        {
                            sb.Append($"<tr><td colspan='4' style='font-style:italic; font-size:11pt;'>- Ghi chú: {b2.GhiChu}</td></tr>");
                        }
                    }
                }

                if (hasGD)
                {
                    foreach (var xn in don.HrNguoiXacNhans)
                    {
                        string tt = xn.TrangThaiXacNhan == 1 ? "Đã duyệt" : xn.TrangThaiXacNhan == 2 ? "Từ chối" : "Chờ duyệt";
                        string tenGD = xn.IdnguoiXacNhanNavigation?.HoTen ?? xn.TenNguoiXacNhan;
                        sb.Append($"<tr><td>Giám đốc</td><td>{tenGD}</td><td>{tt}</td><td>{xn.ThoiGianXacNhan?.ToString("HH:mm dd/MM/yyyy")}</td></tr>");
                        if (!string.IsNullOrEmpty(xn.GhiChu))
                        {
                            sb.Append($"<tr><td colspan='4' style='font-style:italic; font-size:11pt;'>- Ghi chú: {xn.GhiChu}</td></tr>");
                        }
                    }
                }
                sb.Append("</table>");
            }

            // IV. CHỮ KÝ XÁC NHẬN
            sb.Append("<table class='signature-table'><tr>");
            sb.Append("<td><strong>NGƯỜI LẬP ĐƠN</strong><br/><span style='font-size:10pt;'>(Ký, ghi rõ họ tên)</span><br/><br/><br/><br/><strong>" + don.TenNguoiTao + "</strong></td>");
            sb.Append("<td><strong>QUẢN LÝ DUYỆT</strong><br/><span style='font-size:10pt;'>(Ký, ghi rõ họ tên)</span><br/><br/><br/><br/><strong>" + (don.TenNguoiDuyet ?? "") + "</strong></td>");

            // Nếu có Giám đốc duyệt, để trống 1 cột cho GĐ ký
            if (hasGD)
            {
                sb.Append("<td><strong>BAN GIÁM ĐỐC</strong><br/><span style='font-size:10pt;'>(Ký, ghi rõ họ tên)</span><br/><br/><br/><br/></td>");
            }
            else
            {
                sb.Append("<td></td>");
            }

            sb.Append("<td><strong>XÁC NHẬN (HR)</strong><br/><span style='font-size:10pt;'>(Ký, ghi rõ họ tên)</span><br/><br/><br/><br/><strong>" + (don.TenAdmin ?? "") + "</strong></td>");
            sb.Append("</tr></table>");

            sb.Append("</div></body></html>");
            return sb.ToString();
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

        [HttpGet("/FormHR/DonCho")]
        public async Task<IActionResult> DonCho()
        {
            // 1. Kiểm tra đăng nhập
            if (User == null || User.Identity == null || !User.Identity.IsAuthenticated)
            {
                return Redirect("/DonXetDuyet/DangNhap");
            }

            // 2. Lấy ID người dùng từ Claim
            var userIdClaim = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value
                              ?? User.FindFirst("UserId")?.Value;

            if (string.IsNullOrEmpty(userIdClaim))
            {
                return Redirect("/DonXetDuyet/DangNhap");
            }

            try
            {
                int currentUserId = int.Parse(userIdClaim);

                // 3. Truy vấn danh sách đơn với đầy đủ các bảng liên quan để xử lý logic tiến độ
                var danhSachDon = await _context.FormHrs
                    // Lấy thông tin Giám đốc/Người xác nhận (HrNguoiXacNhan)
                    .Include(f => f.HrNguoiXacNhans)
                        .ThenInclude(xn => xn.IdnguoiXacNhanNavigation)

                    // Lấy thông tin Bộ phận xác nhận B2 (HrQuanLyDuyetB2)
                    .Include(f => f.HrQuanLyDuyetB2s)

                    // Lấy thông tin Bảo vệ (nếu đơn có liên quan bảo vệ)
                    .Include(f => f.BaoVeHrs)

                    // Lấy thông tin Người hỗ trợ HR
                    .Include(f => f.HrCtNguoiHoTros)
                        .ThenInclude(ct => ct.IdHrNguoiHoTroNavigation)

                    // Lọc theo người tạo và sắp xếp mới nhất lên đầu
                    .Where(f => f.IdNguoiTao == currentUserId)
                    .OrderByDescending(f => f.Id)
                    .AsNoTracking() // Tối ưu hiệu năng vì chỉ dùng để hiển thị (Read-only)
                    .ToListAsync();

                return View(danhSachDon);
            }
            catch (Exception ex)
            {
                // Ghi log lỗi nếu cần thiết
                TempData["Error"] = "Có lỗi xảy ra khi tải danh sách đơn: " + ex.Message;
                return View(new List<E_Form_Best.Models.ITForm.FormHr>());
            }
        }
        #endregion

        #region XỬ LÝ ĐƠN HR (Duyệt / Hủy / Hoàn tất) - PHÂN QUYỀN MỚI 2026

        [HttpGet("/FormHR/QuanLyXetDuyet")]
        public async Task<IActionResult> QuanLyXetDuyet()
        {
            // --- 1. LẤY THÔNG TIN TỪ CLAIMS ---
            var userIdStr = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userIdStr)) return Redirect("/DonXetDuyet/DangNhap");
            int userId = int.Parse(userIdStr);

            var phongBan = User.FindFirst("PhongBan")?.Value?.Trim() ?? "";
            var tenCongTy = User.FindFirst("TenCongTy")?.Value?.Trim() ?? "";

            var listTenBoPhanStr = User.FindFirst("TenBoPhan")?.Value ?? "";
            var listTenBoPhan = listTenBoPhanStr.Split(',')
                                                .Select(s => s.Trim().ToLower())
                                                .Where(s => !string.IsNullOrEmpty(s))
                                                .ToList();

            // --- 2. TRUY VẤN DỮ LIỆU ---
            IQueryable<E_Form_Best.Models.ITForm.FormHr> query = _context.FormHrs
                .Include(f => f.HrNguoiXacNhans)
                    .ThenInclude(xn => xn.IdnguoiXacNhanNavigation)
                .Include(f => f.HrQuanLyDuyetB2s) // BỔ SUNG ĐỂ KIỂM TRA TRẠNG THÁI B2
                .Include(f => f.BaoVeHrs)
                .Include(f => f.HrCtNguoiHoTros)
                    .ThenInclude(ct => ct.IdHrNguoiHoTroNavigation);

            // --- 3. LOGIC PHÂN QUYỀN ---
            if (User.IsInRole("All"))
            {
                // Nhìn thấy hết toàn hệ thống
            }
            else
            {
                if (!string.IsNullOrEmpty(tenCongTy))
                {
                    query = query.Where(f => f.TenCongTy == tenCongTy);
                }

                if (User.IsInRole("QuanLyDuyetDonHR"))
                {
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
                    query = query.Where(f => f.IdNguoiTao == userId);
                }
            }

            var danhSachDon = await query
                .OrderByDescending(f => f.Id)
                .AsNoTracking()
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

        #region XỬ LÝ ĐƠN HR B2 - PHÂN QUYỀN MỚI 2026

        // GET: /FormHR/QuanLyXetDuyetB2
        [HttpGet("/FormHR/QuanLyXetDuyetB2")]
        public async Task<IActionResult> QuanLyXetDuyetB2()
        {
            // --- 1. LẤY THÔNG TIN TỪ CLAIMS ---
            var userIdStr = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userIdStr)) return Redirect("/DonXetDuyet/DangNhap");

            int userId = int.Parse(userIdStr);
            // Lấy Email/Tài khoản để so khớp với MaNguoiXacNhan trong bảng B2
            var userEmail = (User.FindFirst(System.Security.Claims.ClaimTypes.Email)?.Value ?? "").Trim().ToLower();
            var tenCongTy = User.FindFirst("TenCongTy")?.Value?.Trim() ?? "";

            // Chỉ kiểm tra 2 quyền này theo yêu cầu của bạn
            bool isAll = User.IsInRole("All");
            bool isQuanLyB2 = User.IsInRole("QuanLyDuyetDonHR_B2");

            // --- 2. TRUY VẤN DỮ LIỆU ---
            IQueryable<E_Form_Best.Models.ITForm.FormHr> query = _context.FormHrs
                .Include(f => f.HrNguoiXacNhans)
                    .ThenInclude(xn => xn.IdnguoiXacNhanNavigation)
                .Include(f => f.HrQuanLyDuyetB2s)
                .Include(f => f.BaoVeHrs)
                .Include(f => f.HrCtNguoiHoTros)
                    .ThenInclude(ct => ct.IdHrNguoiHoTroNavigation);

            // --- 3. LOGIC LỌC DỮ LIỆU ---
            // Điều kiện tiên quyết: IdNguoiDuyet khác null (Quản lý trực tiếp đã duyệt)
            query = query.Where(f => f.IdNguoiDuyet != null);

            // Lọc theo quyền (Sử dụng OR để cộng dồn điều kiện)
            query = query.Where(f =>
                // Nếu là All: Thấy hết các đơn đã duyệt cấp 1
                isAll ||

                // Nếu là QuanLyB2: Thấy đơn nếu có tên trong danh sách xác nhận B2
                (isQuanLyB2 && f.HrQuanLyDuyetB2s.Any(b2 =>
                    (b2.MaNguoiXacNhan != null && b2.MaNguoiXacNhan.ToLower() == userEmail) ||
                    b2.IdnguoiXacNhan == userId))
            );

            // Lọc theo công ty nếu không phải quyền All (Global)
            if (!isAll && !string.IsNullOrEmpty(tenCongTy))
            {
                query = query.Where(f => f.TenCongTy == tenCongTy);
            }

            // --- 4. THỰC THI TRUY VẤN ---
            var danhSachDon = await query
                .OrderByDescending(f => f.Id)
                .AsNoTracking()
                .ToListAsync();

            return View(danhSachDon);
        }


        #region DUYỆT BƯỚC 2 (HR_QuanLyDuyetB2) - SO SÁNH QUA MÃ NHÂN VIÊN

        public class DuyetB2Request
        {
            public int idB2 { get; set; }
            public int idForm { get; set; }
            public string loaiHanhDong { get; set; } = ""; // Approve / Reject
            public string ghiChu { get; set; } = "";
        }

        [HttpPost("/FormHR/DuyetB2")]
        public async Task<IActionResult> DuyetB2([FromBody] DuyetB2Request req)
        {
            // 0. Kiểm tra đăng nhập
            // Lấy định danh người dùng (thường là MaNV hoặc Email tùy cấu hình lúc Login)
            var userEmail = User.FindFirst(System.Security.Claims.ClaimTypes.Email)?.Value?.Trim() ?? "";
            var userName = User.Identity?.Name ?? "";

            if (string.IsNullOrEmpty(userEmail))
                return Json(new { success = false, message = "⚠️ Phiên đăng nhập hết hạn!" });

            // 1. Kiểm tra tính hợp lệ của request
            if (req == null || req.idB2 <= 0 || req.idForm <= 0 || string.IsNullOrEmpty(req.loaiHanhDong))
                return Json(new { success = false, message = "⚠️ Dữ liệu gửi lên không hợp lệ." });

            try
            {
                // 2. Load record B2 và đơn liên quan
                var record = await _context.HrQuanLyDuyetB2s
                    .Include(x => x.IdFormHrNavigation)
                    .FirstOrDefaultAsync(x => x.Id == req.idB2);

                if (record == null)
                    return Json(new { success = false, message = "⚠️ Không tìm thấy bản ghi duyệt (B2)." });

                if (record.IdFormHr != req.idForm)
                    return Json(new { success = false, message = "⚠️ Bản ghi không thuộc đơn này." });

                var don = record.IdFormHrNavigation;
                if (don == null)
                    return Json(new { success = false, message = "⚠️ Không tìm thấy đơn liên quan." });

                // 3. Kiểm tra trạng thái: Chỉ xử lý nếu đang chờ (0 hoặc null)
                if ((record.TrangThaiXacNhan ?? 0) != 0)
                    return Json(new { success = false, message = "⚠️ Bước này đã được xử lý trước đó rồi." });

                // 4. KIỂM TRA QUYỀN (CHỈ SO SÁNH QUA MaNguoiXacNhan)
                var assignedMa = (record.MaNguoiXacNhan ?? "").Trim();

                // So sánh trực tiếp mã được gán trong đơn và mã của người đang đăng nhập
                bool isRightUser = !string.IsNullOrEmpty(assignedMa) &&
                                   string.Equals(assignedMa, userEmail, StringComparison.OrdinalIgnoreCase);

                if (!isRightUser)
                {
                    return Json(new
                    {
                        success = false,
                        message = $"🚫 Bạn không có quyền duyệt bước này. (Người được phân công: {record.TenNguoiXacNhan ?? "N/A"})"
                    });
                }

                // 5. Thiết lập hành động
                bool isApprove = string.Equals(req.loaiHanhDong, "Approve", StringComparison.OrdinalIgnoreCase);
                int newStatus = isApprove ? 1 : 2;

                record.TrangThaiXacNhan = newStatus;
                record.ThoiGianXacNhan = DateTime.Now;
                if (!string.IsNullOrWhiteSpace(req.ghiChu))
                {
                    record.GhiChu = req.ghiChu.Trim();
                }

                string tieuDeLS;
                string moTaLS;

                if (!isApprove) // Hành động: TỪ CHỐI (Reject)
                {
                    don.TrangThai = "Huy";
                    if (don.TenForm != null && !don.TenForm.Contains("[ĐÃ HỦY]"))
                    {
                        don.TenForm += " [ĐÃ HỦY]";
                    }

                    tieuDeLS = "BƯỚC 2 — TỪ CHỐI";
                    moTaLS = $"{userName} ({userEmail}) đã TỪ CHỐI duyệt bước 2. Lý do: {(string.IsNullOrWhiteSpace(req.ghiChu) ? "Không có lý do cụ thể" : req.ghiChu)}";
                }
                else // Hành động: DUYỆT (Approve)
                {
                    tieuDeLS = "BƯỚC 2 — ĐÃ DUYỆT";
                    moTaLS = $"{userName} ({userEmail}) đã DUYỆT bước 2 (thứ tự: {record.ThuTuXacNhan}).";
                    if (!string.IsNullOrWhiteSpace(req.ghiChu)) moTaLS += $"\nGhi chú: {req.ghiChu}";

                    // Kiểm tra xem đã hoàn tất Bước 2 chưa
                    bool conAiChuaDuyet = await _context.HrQuanLyDuyetB2s
                        .AnyAsync(x => x.IdFormHr == req.idForm
                                    && x.Id != record.Id
                                    && (x.TrangThaiXacNhan == null || x.TrangThaiXacNhan == 0));

                    if (!conAiChuaDuyet)
                    {
                        don.TrangThai = "DaDuyet"; // Chuyển đơn sang trạng thái đã duyệt (Chờ HR)
                        tieuDeLS = "BƯỚC 2 — HOÀN TẤT";
                        moTaLS = "Toàn bộ Bước 2 đã duyệt xong. Đơn chuyển sang trạng thái Chờ HR xử lý.";
                    }
                }

                // 6. Lưu dữ liệu với Transaction
                using var tx = await _context.Database.BeginTransactionAsync();
                try
                {
                    _context.Update(record);
                    _context.Update(don);

                    _context.LichSuFormHrs.Add(new LichSuFormHr
                    {
                        IdFormHr = don.Id,
                        TieuDe = tieuDeLS,
                        Mota = moTaLS,
                        Time = DateTime.Now,
                        IsRead = false
                    });

                    await _context.SaveChangesAsync();
                    await tx.CommitAsync();

                    return Json(new
                    {
                        success = true,
                        message = isApprove ? "✅ Đã duyệt Bước 2 thành công!" : "❌ Đã từ chối đơn thành công!",
                        trangThai = newStatus
                    });
                }
                catch (Exception ex)
                {
                    await tx.RollbackAsync();
                    return Json(new { success = false, message = "❌ Lỗi khi lưu dữ liệu: " + ex.Message });
                }
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = "❌ Lỗi hệ thống: " + ex.Message });
            }
        }

        #endregion
        #endregion

        #region PHÊ DUYỆT CẤP CAO (GIÁM ĐỐC)

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

            // 2. Kiểm tra Role (Đánh dấu quyền độc lập)
            bool isAll = User.IsInRole("All");
            bool isGiamDoc = User.IsInRole("GiamDocHR");

            if (!isAll && !isGiamDoc)
            {
                return Content("Tài khoản của bạn không có quyền truy cập trang phê duyệt cấp cao.");
            }

            // 3. Lấy thông tin định danh (Lấy Email/Mã NV để so khớp với MaNguoiXacNhan)
            var userEmail = (User.FindFirst(System.Security.Claims.ClaimTypes.Email)?.Value
                            ?? User.FindFirst("Email")?.Value ?? "").Trim().ToLower();

            var userIdStr = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
            int userId = string.IsNullOrEmpty(userIdStr) ? 0 : int.Parse(userIdStr);

            if (string.IsNullOrEmpty(userEmail) && !isAll)
            {
                return Content("Không xác định được thông tin định danh người duyệt.");
            }

            // 4. Khởi tạo Query và nạp dữ liệu liên quan
            IQueryable<E_Form_Best.Models.ITForm.FormHr> query = _context.FormHrs
                .Include(f => f.HrNguoiXacNhans)
                    .ThenInclude(x => x.IdnguoiXacNhanNavigation)
                .Include(f => f.HrQuanLyDuyetB2s)
                .Include(f => f.BaoVeHrs);

            // 5. ĐIỀU KIỆN LỌC CHUNG
            // - Đã được Quản lý trực tiếp ký duyệt (IdNguoiDuyet != null)
            // - Đã qua bước duyệt B2 (Áp dụng logic cấu hình duyệt AND / OR)
            query = query.Where(f => f.IdNguoiDuyet != null &&
                (
                    !f.HrQuanLyDuyetB2s.Any() || // Trường hợp đơn không cấu hình duyệt B2 thì cho qua
                    (f.HrQuanLyDuyetB2s.Any(b2 => b2.Loai == "OR" || b2.Loai == "ANY") && f.HrQuanLyDuyetB2s.Any(q => q.TrangThaiXacNhan == 1)) || // Hình thức OR: Có ít nhất 1 người duyệt
                    (!f.HrQuanLyDuyetB2s.Any(b2 => b2.Loai == "OR" || b2.Loai == "ANY") && f.HrQuanLyDuyetB2s.All(q => q.TrangThaiXacNhan != 0)) // Hình thức AND: Tất cả đã xử lý xong
                )
            );

            // 6. PHÂN QUYỀN HIỂN THỊ (Logic tương đương B2)
            query = query.Where(f =>
                // Quyền All: Xem toàn bộ đơn đã qua bước duyệt trung gian
                isAll ||

                // Quyền GiamDocHR: Chỉ xem những đơn mà mình có tên trong bảng HrNguoiXacNhan
                (isGiamDoc && f.HrNguoiXacNhans.Any(xn =>
                    (xn.MaNguoiXacNhan != null && xn.MaNguoiXacNhan.ToLower() == userEmail) ||
                    xn.IdnguoiXacNhan == userId))
            );

            // 7. Thực thi truy vấn và sắp xếp
            try
            {
                var danhSachDon = await query
                    .OrderByDescending(f => f.Id)
                    .AsNoTracking()
                    .ToListAsync();

                ViewBag.UserEmail = userEmail;
                return View(danhSachDon);
            }
            catch (Exception ex)
            {
                TempData["Error"] = "Lỗi truy vấn dữ liệu phê duyệt cấp cao: " + ex.Message;
                return View(new List<E_Form_Best.Models.ITForm.FormHr>());
            }
        }
        /// <summary>
        /// POST: /FormHR/GiamDocPheDuyet
        /// Giám đốc hoặc Admin phê duyệt/từ chối đơn HR
        [HttpPost("/FormHR/GiamDocPheDuyet")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> GiamDocPheDuyet([FromBody] System.Text.Json.JsonElement data)
        {
            using (var transaction = await _context.Database.BeginTransactionAsync())
            {
                try
                {
                    // 1. Lấy dữ liệu từ Request
                    if (!data.TryGetProperty("idForm", out var idFormProp) || !data.TryGetProperty("idNguoiXacNhan", out var idXnProp))
                    {
                        return Json(new { success = false, message = "⚠️ Dữ liệu yêu cầu không đầy đủ." });
                    }

                    int idForm = idFormProp.GetInt32();
                    int idNguoiXacNhan = idXnProp.GetInt32();
                    var loaiHanhDong = data.TryGetProperty("loaiHanhDong", out var lh) ? lh.GetString() ?? "" : "";
                    var lyDo = data.TryGetProperty("lyDo", out var p) ? (p.GetString() ?? "") : "";

                    // 2. Lấy thông tin người thao tác từ Claims
                    var userEmail = User.FindFirst(System.Security.Claims.ClaimTypes.Email)?.Value?.Trim() ?? "";
                    var userName = User.FindFirst(System.Security.Claims.ClaimTypes.Name)?.Value ?? "N/A";
                    var phongBan = User.FindFirst("PhongBan")?.Value ?? "N/A";

                    if (string.IsNullOrEmpty(userEmail))
                        return Json(new { success = false, message = "⚠️ Phiên đăng nhập hết hạn!" });

                    var roles = User.FindAll(System.Security.Claims.ClaimTypes.Role)
                                    .Select(r => r.Value.Trim().ToUpper())
                                    .ToList();
                    var userRoleCustom = User.FindFirst("UserRole")?.Value?.Trim().ToUpper();
                    if (!string.IsNullOrEmpty(userRoleCustom) && !roles.Contains(userRoleCustom)) roles.Add(userRoleCustom);

                    // 3. Tìm bản ghi người xác nhận và đơn HR liên quan
                    var xn = await _context.HrNguoiXacNhans
                        .Include(x => x.IdnguoiXacNhanNavigation)
                        .Include(x => x.IdFormHrNavigation)
                        .FirstOrDefaultAsync(x => x.Id == idNguoiXacNhan && x.IdFormHr == idForm);

                    if (xn == null)
                        return Json(new { success = false, message = "⚠️ Không tìm thấy bản ghi xác nhận trong hệ thống." });

                    var don = xn.IdFormHrNavigation;
                    if (don == null)
                        return Json(new { success = false, message = "⚠️ Không tìm thấy thông tin đơn gốc." });

                    // 4. Kiểm tra quyền thực hiện
                    var assignedMa = (xn.MaNguoiXacNhan ?? "").Trim();
                    bool isAssignee = !string.IsNullOrEmpty(assignedMa) && string.Equals(userEmail, assignedMa, StringComparison.OrdinalIgnoreCase);

                    bool canAct = roles.Contains("ALL") || roles.Contains("ADMIN") || roles.Contains("GIAMDOCHR")
                                  || isAssignee
                                  || string.Equals(userEmail, xn.IdnguoiXacNhanNavigation?.MaNv ?? "", StringComparison.OrdinalIgnoreCase);

                    if (!canAct)
                        return Json(new { success = false, message = $"🚫 Bạn không có quyền phê duyệt mục này. (Người được gán: {xn.TenNguoiXacNhan ?? "N/A"})" });

                    // 5. Kiểm tra nếu trạng thái đã được xử lý trước đó
                    if (xn.TrangThaiXacNhan != null && xn.TrangThaiXacNhan != 0)
                        return Json(new { success = false, message = "⚠️ Mục này đã được xử lý trước đó." });

                    // 6. Cập nhật trạng thái xác nhận
                    int newTrangThai = loaiHanhDong.Equals("Approve", StringComparison.OrdinalIgnoreCase) ? 1 : 2;
                    string hanhDongStr = (newTrangThai == 1) ? "Phê duyệt" : "Từ chối";
                    DateTime now = DateTime.Now;

                    xn.TrangThaiXacNhan = newTrangThai;
                    xn.ThoiGianXacNhan = now;
                    xn.GhiChu = string.IsNullOrWhiteSpace(lyDo) ? null : lyDo.Trim();

                    _context.HrNguoiXacNhans.Update(xn);

                    // 7. Xử lý logic hủy đơn nếu Giám đốc từ chối
                    if (newTrangThai == 2)
                    {
                        don.TrangThai = "Huy";
                        if (don.TenForm != null && !don.TenForm.Contains("[ĐÃ HỦY]"))
                        {
                            don.TenForm += " [ĐÃ HỦY]";
                        }
                        _context.FormHrs.Update(don);
                    }

                    // 8. GHI LỊCH SỬ THAO TÁC
                    var lichSu = new LichSuFormHr
                    {
                        IdFormHr = idForm,
                        TieuDe = $"CẤP CAO {hanhDongStr.ToUpper()}",
                        Mota = $"Nhân sự thực hiện: {userName} ({userEmail}). " +
                               $"Bộ phận: {phongBan}. " +
                               $"Nội dung: Đã {hanhDongStr.ToLower()} yêu cầu xác nhận cấp cao. " +
                               $"Ghi chú: {(string.IsNullOrWhiteSpace(lyDo) ? "Không có" : lyDo)}",
                        Time = now,
                        IsRead = false
                    };
                    _context.LichSuFormHrs.Add(lichSu);

                    // 9. Lưu thay đổi và Hoàn tất Transaction
                    await _context.SaveChangesAsync();
                    await transaction.CommitAsync();

                    return Json(new
                    {
                        success = true,
                        message = $"✅ Đã {hanhDongStr.ToLower()} thành công",
                        trangThai = newTrangThai,
                        thoiGian = now.ToString("HH:mm dd/MM/yyyy")
                    });
                }
                catch (Exception ex)
                {
                    await transaction.RollbackAsync();
                    return Json(new { success = false, message = "❌ Lỗi hệ thống: " + ex.Message });
                }
            }
        }
        #endregion

        #region QUẢN LÝ PHÊ DUYỆT HR (Admin, All, Quản lý)

        [HttpGet("/FormHR/HoanTatDon")]
        public async Task<IActionResult> HoanTatDon()
        {
            // --- 1. LẤY THÔNG TIN TỪ CLAIMS ---
            var userIdStr = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value
                            ?? User.FindFirst("UserId")?.Value;

            if (string.IsNullOrEmpty(userIdStr))
                return Redirect("/DonXetDuyet/DangNhap");

            int userId = int.Parse(userIdStr);
            var userEmail = (User.FindFirst(System.Security.Claims.ClaimTypes.Email)?.Value ?? "").Trim().ToLower();
            var userRoles = User.FindAll(System.Security.Claims.ClaimTypes.Role).Select(c => c.Value).ToList();
            var phongBanSession = User.FindFirst("PhongBan")?.Value?.Trim() ?? "";
            var tenCongTy = User.FindFirst("TenCongTy")?.Value?.Trim() ?? "";

            var listTenBoPhanStr = User.FindFirst("TenBoPhan")?.Value ?? "";
            var listTenBoPhan = listTenBoPhanStr.Split(',')
                                                .Select(s => s.Trim().ToLower())
                                                .Where(s => !string.IsNullOrEmpty(s))
                                                .ToList();

            // Đánh dấu các quyền để check độc lập
            bool isAll = userRoles.Contains("All");
            bool isAdminHR = userRoles.Contains("AdminHR");
            bool isQuanLyB2 = userRoles.Contains("QuanLyDuyetDonHR_B2");
            bool isQuanLyDuyet = userRoles.Contains("QuanLyDuyetDonHR");
            bool isGiamDocHR = userRoles.Contains("GiamDocHR");

            // --- 2. KHỞI TẠO QUERY ---
            IQueryable<E_Form_Best.Models.ITForm.FormHr> query = _context.FormHrs
                .Include(f => f.HrNguoiXacNhans)
                    .ThenInclude(xn => xn.IdnguoiXacNhanNavigation)
                .Include(f => f.HrQuanLyDuyetB2s)
                .Include(f => f.BaoVeHrs)
                .Include(f => f.HrCtNguoiHoTros)
                    .ThenInclude(ct => ct.IdHrNguoiHoTroNavigation);

            // --- 3. ĐIỀU KIỆN CƠ BẢN: QUẢN LÝ ĐÃ DUYỆT ---
            // Như bạn yêu cầu: IdNguoiDuyet khác null thì mới hiển thị ở đây
            query = query.Where(f => f.IdNguoiDuyet != null && f.TenNguoiDuyet != null && f.TimeNguoiDuyet != null);

            // --- 4. LOGIC PHÂN QUYỀN ĐỘC LẬP (CỘNG DỒN OR) ---
            query = query.Where(f =>
                isAll || isAdminHR || // Admin thấy hết các đơn đã qua bước duyệt 1

                // Quyền B2: Thấy đơn mình có tên trong danh sách xác nhận B2
                (isQuanLyB2 && f.HrQuanLyDuyetB2s.Any(b2 => b2.IdnguoiXacNhan == userId || (b2.MaNguoiXacNhan != null && b2.MaNguoiXacNhan.ToLower() == userEmail))) ||

                // Quyền Giám đốc (Cấp 2): Thấy đơn mình có tên trong danh sách xác nhận
                (isGiamDocHR && f.HrNguoiXacNhans.Any(xn => xn.IdnguoiXacNhan == userId || (xn.MaNguoiXacNhan != null && xn.MaNguoiXacNhan.ToLower() == userEmail))) ||

                // Quyền Quản lý duyệt: Theo bộ phận hoặc đơn mình tạo
                (isQuanLyDuyet && (
                    f.IdNguoiTao == userId ||
                    (listTenBoPhan.Any() && f.BoPhan != null && listTenBoPhan.Contains(f.BoPhan.Trim().ToLower())) ||
                    (!listTenBoPhan.Any() && !string.IsNullOrEmpty(phongBanSession) && f.BoPhan != null && f.BoPhan.Trim().ToLower() == phongBanSession.ToLower())
                )) ||

                // User thường: Thấy đơn mình tạo hoặc đơn mình được phân công hỗ trợ
                (f.IdNguoiTao == userId || f.HrCtNguoiHoTros.Any(ct => ct.IdHrNguoiHoTroNavigation.MaNv.ToLower() == userEmail))
            );

            // Lọc theo công ty cho các quyền không phải Global
            if (!isAll && !isAdminHR && !string.IsNullOrEmpty(tenCongTy))
            {
                query = query.Where(f => f.TenCongTy == tenCongTy);
            }

            // --- 5. THỰC THI VÀ GÁN TRẠNG THÁI CHI TIẾT ---
            try
            {
                var danhSachDon = await query
                    .OrderByDescending(f => f.Id)
                    .AsNoTracking()
                    .ToListAsync();

                foreach (var item in danhSachDon)
                {
                    bool isCancelled = (item.TenForm ?? "").Contains("[ĐÃ HỦY]") || item.TrangThai == "DaHuy";
                    bool isFinished = item.IdAdmin != null || item.TrangThai == "HoanTat";

                    bool hasB2 = item.HrQuanLyDuyetB2s != null && item.HrQuanLyDuyetB2s.Any();

                    // --- ÁP DỤNG LOGIC AND/OR B2 ---
                    string b2Type = hasB2 ? (item.HrQuanLyDuyetB2s.FirstOrDefault()?.Loai?.ToUpper() ?? "AND") : "AND";
                    bool isB2Approved = false;

                    if (hasB2)
                    {
                        if (b2Type == "OR" || b2Type == "ANY")
                        {
                            isB2Approved = item.HrQuanLyDuyetB2s.Any(x => x.TrangThaiXacNhan == 1);
                        }
                        else
                        {
                            isB2Approved = item.HrQuanLyDuyetB2s.All(x => x.TrangThaiXacNhan == 1);
                        }
                    }

                    bool isB2Rejected = hasB2 && item.HrQuanLyDuyetB2s.Any(x => x.TrangThaiXacNhan == 2);

                    bool hasGD = item.HrNguoiXacNhans != null && item.HrNguoiXacNhans.Any();
                    bool isGDApproved = hasGD && item.HrNguoiXacNhans.All(x => x.TrangThaiXacNhan == 1);
                    bool isGDRejected = hasGD && item.HrNguoiXacNhans.Any(x => x.TrangThaiXacNhan == 2);

                    if (isCancelled || isB2Rejected || isGDRejected)
                    {
                        item.TrangThai = "Đã hủy/Từ chối";
                    }
                    else if (isFinished)
                    {
                        item.TrangThai = "Hoàn tất";
                    }
                    else if (hasB2 && !isB2Approved)
                    {
                        item.TrangThai = "Chờ BP Xác nhận";
                    }
                    else if (hasGD && !isGDApproved)
                    {
                        item.TrangThai = "Chờ Giám đốc";
                    }
                    else
                    {
                        item.TrangThai = "HR Đang xử lý";
                    }
                }

                return View(danhSachDon);
            }
            catch (Exception ex)
            {
                TempData["Error"] = "Lỗi hệ thống: " + ex.Message;
                return View(new List<E_Form_Best.Models.ITForm.FormHr>());
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

            if (!User.IsInRole("All") && !User.IsInRole("AdminHR"))
            {
                return Json(new { success = false, message = "Bạn không có quyền phê duyệt đơn này." });
            }

            // 3. Tìm đơn HR và Include thông tin cần thiết
            var form = await _context.FormHrs
                .Include(f => f.HrCtNguoiHoTros)
                    .ThenInclude(ct => ct.IdHrNguoiHoTroNavigation)
                .Include(f => f.HrDonTiepKhac5s)
                .Include(f => f.HrNguoiXacNhans)      // Load danh sách người xác nhận (loại 1)
                .Include(f => f.HrQuanLyDuyetB2s)    // Load danh sách quản lý duyệt (loại 2)
                .FirstOrDefaultAsync(f => f.Id == request.Id);

            if (form == null)
                return Json(new { success = false, message = "Không tìm thấy đơn HR." });

            // --- KIỂM TRA TRẠNG THÁI XÁC NHẬN CỦA CÁC CẤP ---
            // Kiểm tra trong bảng HrNguoiXacNhans
            bool chuaXacNhanNguoi = form.HrNguoiXacNhans != null && form.HrNguoiXacNhans.Any(x => x.TrangThaiXacNhan != 1);

            // Kiểm tra trong bảng HrQuanLyDuyetB2s (Áp dụng logic AND / OR)
            bool chuaXacNhanQuanLy = false;
            if (form.HrQuanLyDuyetB2s != null && form.HrQuanLyDuyetB2s.Any())
            {
                string hinhThucDuyet = form.HrQuanLyDuyetB2s.FirstOrDefault()?.Loai?.ToUpper() ?? "AND";

                if (hinhThucDuyet == "OR" || hinhThucDuyet == "ANY")
                {
                    // Hình thức OR: Phải có ÍT NHẤT 1 người duyệt (TrangThaiXacNhan == 1). Nếu không có ai duyệt thì là chưa xác nhận.
                    chuaXacNhanQuanLy = !form.HrQuanLyDuyetB2s.Any(x => x.TrangThaiXacNhan == 1);
                }
                else
                {
                    // Hình thức AND: Tất cả phải duyệt. Nếu có BẤT KỲ AI chưa duyệt (TrangThaiXacNhan != 1) thì là chưa xác nhận.
                    chuaXacNhanQuanLy = form.HrQuanLyDuyetB2s.Any(x => x.TrangThaiXacNhan != 1);
                }
            }

            if (chuaXacNhanNguoi || chuaXacNhanQuanLy)
            {
                return Json(new
                {
                    success = false,
                    message = "Đơn này chưa được các cấp quản lý xác nhận đủ theo cấu hình duyệt. Không thể hoàn tất!"
                });
            }

            // --- BIẾN TẠM ĐỂ XỬ LÝ CHI TIẾT ĐƠN TIẾP KHÁCH ---
            HrDonTiepKhac5 chiTietTiepKhac = null;

            // 4. Logic kiểm tra trùng lịch (Riêng cho đơn Tiếp Khách)
            if (form.IdForm == "HR_DonTiepKhac_5")
            {
                chiTietTiepKhac = form.HrDonTiepKhac5s.FirstOrDefault();
                if (chiTietTiepKhac != null &&
                    chiTietTiepKhac.ThoiGianBatDau.HasValue &&
                    chiTietTiepKhac.ThoiGianKetThuc.HasValue &&
                    !string.IsNullOrEmpty(chiTietTiepKhac.TenPhongHop))
                {
                    var danhSachDangOn = await _context.HrDonTiepKhac5s
                        .Where(x => x.Id != chiTietTiepKhac.Id
                                    && x.TenPhongHop == chiTietTiepKhac.TenPhongHop
                                    && x.TrangThaiPhong == "on")
                        .ToListAsync();

                    bool biTrungTren5Phut = false;

                    foreach (var item in danhSachDangOn)
                    {
                        var startMax = chiTietTiepKhac.ThoiGianBatDau > item.ThoiGianBatDau ? chiTietTiepKhac.ThoiGianBatDau.Value : item.ThoiGianBatDau.Value;
                        var endMin = chiTietTiepKhac.ThoiGianKetThuc < item.ThoiGianKetThuc ? chiTietTiepKhac.ThoiGianKetThuc.Value : item.ThoiGianKetThuc.Value;

                        if (startMax < endMin)
                        {
                            double overlapMinutes = (endMin - startMax).TotalMinutes;
                            if (overlapMinutes > 5)
                            {
                                biTrungTren5Phut = true;
                                break;
                            }
                        }
                    }

                    if (biTrungTren5Phut)
                    {
                        return Json(new { success = false, message = $"Phòng [{chiTietTiepKhac.TenPhongHop}] đã có lịch 'on' trùng quá 5 phút. Vui lòng kiểm tra lại!" });
                    }

                    chiTietTiepKhac.TrangThaiPhong = "on";
                }
            }

            // 5. Kiểm tra bảo mật (Cùng công ty hoặc quyền All)
            if (form.TenCongTy?.Trim() != tenCongTy && !User.IsInRole("All"))
            {
                return Json(new { success = false, message = "Bạn không có quyền thao tác trên đơn của công ty khác." });
            }

            // 6. Kiểm tra quyền thực hiện
            bool isAdmin = User.IsInRole("All") || User.IsInRole("AdminHR") || User.IsInRole("ADMIN");
            bool isSupporter = form.HrCtNguoiHoTros.Any(ct => ct.IdHrNguoiHoTroNavigation?.MaNv == userEmail);

            if (!isAdmin && !isSupporter)
            {
                return Json(new { success = false, message = "Chỉ nhân sự hỗ trợ hoặc Admin mới có thể hoàn tất đơn này." });
            }

            // 7. Kiểm tra trạng thái đơn
            bool isCancelled = (form.TenForm != null && form.TenForm.Contains("[ĐÃ HỦY]"));
            if (form.TrangThai == "HoanTat" || isCancelled)
            {
                return Json(new { success = false, message = "Đơn đã hoàn tất hoặc đã bị hủy trước đó." });
            }

            // 8. Thực hiện cập nhật dữ liệu
            using (var transaction = await _context.Database.BeginTransactionAsync())
            {
                try
                {
                    DateTime now = DateTime.Now;

                    form.IdAdmin = userId;
                    form.TenAdmin = userName;
                    form.TimeAdmin = now;
                    form.TrangThai = "HoanTat";

                    if (chiTietTiepKhac != null)
                    {
                        _context.HrDonTiepKhac5s.Update(chiTietTiepKhac);
                    }

                    var lichSu = new LichSuFormHr
                    {
                        IdFormHr = form.Id,
                        TieuDe = "HR Xác nhận Hoàn tất",
                        Mota = $"Nhân sự thực hiện: {userName} ({userEmail}). " +
                               $"Bộ phận: {phongBan}. " +
                               $"Nội dung: Đã xử lý hoàn tất. " +
                               (chiTietTiepKhac != null ? $"Trạng thái phòng: {chiTietTiepKhac.TrangThaiPhong}" : ""),
                        Time = now
                    };

                    _context.LichSuFormHrs.Add(lichSu);
                    _context.FormHrs.Update(form);

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

        #region QUẢN TRỊ & XUẤT BÁO CÁO HR (CHỈ ĐƠN ĐÃ HOÀN TẤT - FULL 9 LOẠI)
        [HttpGet("/FormHR/XuatBaoCao")]
        public async Task<IActionResult> XuatBaoCao()
        {
            // --- 1. LẤY THÔNG TIN TỪ CLAIMS ---
            var userIdStr = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value
                            ?? User.FindFirst("UserId")?.Value;

            if (string.IsNullOrEmpty(userIdStr))
                return Redirect("/DonXetDuyet/DangNhap");

            int userId = int.Parse(userIdStr);
            var userRoles = User.FindAll(System.Security.Claims.ClaimTypes.Role).Select(c => c.Value).ToList();
            var tenCongTy = User.FindFirst("TenCongTy")?.Value?.Trim() ?? "";

            // --- 2. KHỞI TẠO QUERY ---
            IQueryable<FormHr> query = _context.FormHrs
                .Include(f => f.HrNguoiXacNhans)
                .Include(f => f.HrQuanLyDuyetB2s)
                .Include(f => f.BaoVeHrs)
                .Include(f => f.HrCtNguoiHoTros);

            // --- 3. ĐIỀU KIỆN HIỂN THỊ: CHỈ LẤY ĐƠN ĐÃ ĐƯỢC ADMIN XỬ LÝ XONG ---
            query = query.Where(f => f.IdAdmin != null && f.TenAdmin != null && f.TimeAdmin != null);

            // --- 4. PHÂN QUYỀN LỌC DỮ LIỆU ---
            if (userRoles.Contains("All"))
            {
                /* Quyền All nhìn thấy toàn bộ đơn đã hoàn tất trong hệ thống */
            }
            else
            {
                // Lọc theo công ty cho các quyền còn lại
                if (!string.IsNullOrEmpty(tenCongTy))
                    query = query.Where(f => f.TenCongTy == tenCongTy);

                if (userRoles.Contains("AdminHR"))
                {
                    // Nếu là AdminHR: Nhìn thấy toàn bộ đơn hoàn tất của công ty mình (Không lọc theo bộ phận)
                }
                else
                {
                    // User thường: Chỉ thấy đơn mình tạo
                    query = query.Where(f => f.IdNguoiTao == userId);
                }
            }

            // --- 5. THỰC THI TRUY VẤN ---
            try
            {
                var danhSachDon = await query.OrderByDescending(f => f.TimeAdmin).AsNoTracking().ToListAsync();

                foreach (var item in danhSachDon)
                {
                    // Gán trạng thái cố định là Hoàn tất cho danh sách báo cáo
                    item.TrangThai = "Hoàn tất";
                }

                return View(danhSachDon);
            }
            catch (Exception ex)
            {
                TempData["Error"] = "Lỗi hệ thống: " + ex.Message;
                return View(new List<FormHr>());
            }
        }

        /// <summary>
        /// Xuất dữ liệu ra Excel CHI TIẾT các đơn ĐÃ HOÀN TẤT
        /// </summary>
        [HttpGet("/FormHR/ExportExcelHR")]
        public async Task<IActionResult> ExportExcelHR(DateTime? tuNgay, DateTime? denNgay, string loaiDon)
        {
            // --- 1. LẤY THÔNG TIN CLAIMS ---
            var userIdStr = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value
                            ?? User.FindFirst("UserId")?.Value;
            if (string.IsNullOrEmpty(userIdStr)) return Unauthorized();

            int userId = int.Parse(userIdStr);
            var userRoles = User.FindAll(System.Security.Claims.ClaimTypes.Role).Select(c => c.Value).ToList();
            var tenCongTy = User.FindFirst("TenCongTy")?.Value?.Trim() ?? "";

            // --- 2. TRUY VẤN DỮ LIỆU & INCLUDE ĐẦY ĐỦ CHI TIẾT ---
            IQueryable<FormHr> query = _context.FormHrs
                .Include(f => f.HrNguoiXacNhans).Include(f => f.HrQuanLyDuyetB2s)
                .Include(f => f.HrCtNguoiHoTros).ThenInclude(ct => ct.IdHrNguoiHoTroNavigation)
                .Include(f => f.HrXinRaNgoai1s).Include(f => f.HrMangHangHoaRaCong2s)
                .Include(f => f.HrDangKySuDungXeCongTac3s).Include(f => f.HrDangKySuDungXeDaily4s)
                .Include(f => f.HrDonTiepKhac5s).Include(f => f.HrNhaThauQuaCong6s)
                .Include(f => f.HrHoTroTienDienThoai7s).Include(f => f.HrDoiCaLam8s)
                .Include(f => f.HrDonHoTroCongTac9s);

            // --- 3. ĐIỀU KIỆN HIỂN THỊ: CHỈ XUẤT ĐƠN ĐÃ ĐƯỢC ADMIN HOÀN TẤT ---
            // Khớp với yêu cầu: IdAdmin, TenAdmin, TimeAdmin đều phải khác null
            query = query.Where(f => f.IdAdmin != null && f.TenAdmin != null && f.TimeAdmin != null);

            // --- 4. BỘ LỌC NGÀY HOÀN TẤT & LOẠI ĐƠN ---
            if (tuNgay.HasValue) query = query.Where(f => f.TimeAdmin >= tuNgay.Value);
            if (denNgay.HasValue) query = query.Where(f => f.TimeAdmin <= denNgay.Value.AddDays(1).AddSeconds(-1));
            if (!string.IsNullOrEmpty(loaiDon)) query = query.Where(f => f.IdForm == loaiDon);

            // --- 5. PHÂN QUYỀN LỌC DỮ LIỆU (Đồng bộ tối giản với XuatBaoCao) ---
            if (userRoles.Contains("All"))
            {
                /* Admin hệ thống xuất toàn bộ */
            }
            else
            {
                // Lọc theo công ty cho các quyền còn lại
                if (!string.IsNullOrEmpty(tenCongTy))
                    query = query.Where(f => f.TenCongTy == tenCongTy);

                if (userRoles.Contains("AdminHR"))
                {
                    // Nếu là AdminHR: Xuất toàn bộ đơn hoàn tất của công ty mình (Không lọc bộ phận)
                }
                else
                {
                    // User thường: Chỉ xuất đơn mình tạo
                    query = query.Where(f => f.IdNguoiTao == userId);
                }
            }

            var data = await query.OrderByDescending(f => f.TimeAdmin).AsNoTracking().ToListAsync();

            // --- 6. TẠO FILE EXCEL ---
            using (var workbook = new XLWorkbook())
            {
                var worksheet = workbook.Worksheets.Add("BaoCao_HoanTat_HR");
                string[] headers = { "STT", "ID", "Loại Đơn", "Mã NV", "Họ Tên", "Bộ Phận", "Ngày Hoàn Tất", "Người Duyệt HR", "Người Hỗ Trợ", "CHI TIẾT NỘI DUNG" };

                // Định dạng Header
                for (int i = 0; i < headers.Length; i++)
                {
                    var cell = worksheet.Cell(1, i + 1);
                    cell.Value = headers[i];
                    cell.Style.Font.Bold = true;
                    cell.Style.Fill.BackgroundColor = XLColor.FromHtml("#1e40af"); // Màu xanh đậm
                    cell.Style.Font.FontColor = XLColor.White;
                    cell.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
                }

                int currentRow = 2;
                foreach (var item in data)
                {
                    worksheet.Cell(currentRow, 1).Value = currentRow - 1;
                    worksheet.Cell(currentRow, 2).Value = item.Id;
                    worksheet.Cell(currentRow, 3).Value = GetShortName(item.IdForm);
                    worksheet.Cell(currentRow, 4).Value = item.SoNhanVien;
                    worksheet.Cell(currentRow, 5).Value = item.TenNguoiNv;
                    worksheet.Cell(currentRow, 6).Value = item.BoPhan;
                    worksheet.Cell(currentRow, 7).Value = item.TimeAdmin?.ToString("dd/MM/yyyy HH:mm");
                    worksheet.Cell(currentRow, 8).Value = item.TenAdmin; // Người hoàn tất đơn

                    var support = item.HrCtNguoiHoTros.OrderByDescending(s => s.Stt).FirstOrDefault();
                    worksheet.Cell(currentRow, 9).Value = support?.IdHrNguoiHoTroNavigation?.Ten ?? "";

                    // Nội dung chi tiết đơn (Gộp từ 9 bảng chi tiết)
                    worksheet.Cell(currentRow, 10).Value = GetChiTietDon(item);
                    worksheet.Cell(currentRow, 10).Style.Alignment.WrapText = true;

                    currentRow++;
                }

                // Tinh chỉnh giao diện bảng Excel
                worksheet.Columns().AdjustToContents();
                worksheet.Column(10).Width = 80; // Độ rộng cho cột chi tiết nội dung
                worksheet.RangeUsed().Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
                worksheet.RangeUsed().Style.Border.InsideBorder = XLBorderStyleValues.Thin;

                using (var stream = new MemoryStream())
                {
                    workbook.SaveAs(stream);
                    string fileName = $"BaoCao_HR_HoanTat_{DateTime.Now:yyyyMMdd_HHmm}.xlsx";
                    return File(stream.ToArray(), "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", fileName);
                }
            }
        }

        // --- CÁC HÀM HELPER (DUY NHẤT MỘT BẢN TẠI ĐÂY) ---

        private static string CalculateStatus(string? tenForm, int? idQL, int? idAdmin, IEnumerable<int?> b2, IEnumerable<int?> gd)
        {
            if ((tenForm ?? "").Contains("[ĐÃ HỦY]") || b2.Any(v => v == 2) || gd.Any(v => v == 2)) return "ĐÃ HỦY/TỪ CHỐI";
            if (idAdmin != null) return "HOÀN TẤT";
            if (idQL == null) return "CHỜ QL DUYỆT";
            if (b2.Any() && !b2.All(v => v == 1)) return "CHỜ BP XÁC NHẬN";
            if (gd.Any() && !gd.All(v => v == 1)) return "CHỜ GIÁM ĐỐC";
            return "HR ĐANG XỬ LÝ";
        }

        private static string GetShortName(string? id) => id switch
        {
            "HR_XinRaNgoai_1" => "Ra ngoài",
            "HR_MangHangHoaRaCong_2" => "Hàng hóa",
            "HR_XeCongTac_3" => "Xe công tác",
            "HR_XeDaily_4" => "Xe Daily",
            "HR_DonTiepKhac_5" => "Đón khách",
            "HR_NhaThauQuaCong_6" => "Nhà thầu",
            "HR_HoTroTienDienThoai_7" => "Tiền ĐT",
            "HR_DoiCaLam_8" => "Đổi ca",
            "HR_DonHoTroCongTac_9" => "HT Công tác",
            _ => string.IsNullOrEmpty(id) ? "N/A" : id.Replace("HR_", "")
        };

        private string GetChiTietDon(FormHr item)
        {
            try
            {
                switch (item.IdForm)
                {
                    case "HR_XinRaNgoai_1":
                        var f1 = item.HrXinRaNgoai1s.FirstOrDefault();
                        return f1 != null ? $"- Lý do: {f1.LiDo}\n- Địa điểm: {f1.DiaDiem}\n- Giờ ra: {f1.ThoiGianRa:HH:mm}\n- Giờ vào dự kiến: {f1.ThoiGianVeDuTinh:HH:mm}" : "";
                    case "HR_MangHangHoaRaCong_2":
                        var f2 = item.HrMangHangHoaRaCong2s.FirstOrDefault();
                        return f2 != null ? $"- Mô tả: {f2.MoTa}\n- Cổng: {f2.TenCong}\n- Thời gian: {f2.ThoiGianRa:HH:mm} - {f2.ThoiGianVao:HH:mm}" : "";
                    case "HR_XeCongTac_3":
                        var f3 = item.HrDangKySuDungXeCongTac3s.FirstOrDefault();
                        return f3 != null ? $"- Lý do: {f3.LiDo}\n- SĐT: {f3.SoDienThoai}\n- Số người: {f3.SoLuong}\n- Về lúc: {f3.ThoiGianVe:dd/MM HH:mm}" : "";
                    case "HR_XeDaily_4":
                        var f4 = item.HrDangKySuDungXeDaily4s.FirstOrDefault();
                        return f4 != null ? $"- Điểm đón: {f4.DiemDon}\n- Lý do: {f4.LiDo}\n- Giờ đi: {f4.TimeDuTinh:HH:mm}" : "";
                    case "HR_DonTiepKhac_5":
                        var f5 = item.HrDonTiepKhac5s.FirstOrDefault();
                        return f5 != null ? $"- Khách: {f5.TenCongTyKhach} ({f5.SoLuongKhach} người)\n- Phòng: {f5.TenPhongHop}\n- Suất ăn: {f5.LoaiSuatAn} ({f5.SoLuongSuat} suất)\n- Yêu cầu: {f5.YeuCauTiepKhach}" : "";
                    case "HR_NhaThauQuaCong_6":
                        var f6 = item.HrNhaThauQuaCong6s.FirstOrDefault();
                        return f6 != null ? $"- Nhà thầu: {f6.TenNhaThau}\n- Số người: {f6.SoNguoi}\n- Công việc: {f6.MucDichCongViec}" : "";
                    case "HR_HoTroTienDienThoai_7":
                        var f7 = item.HrHoTroTienDienThoai7s.FirstOrDefault();
                        return f7 != null ? $"- Số ĐT: {f7.SoDienThoai}\n- Mức hỗ trợ: {f7.MucHoTro:N0}\n- Mục đích: {f7.MucDich}" : "";
                    case "HR_DoiCaLam_8":
                        var f8 = item.HrDoiCaLam8s.FirstOrDefault();
                        return f8 != null ? $"- Ngày đổi: {f8.NgayCanDoi:dd/MM}\n- Ca: {f8.CaGoc} -> {f8.CaMuonDoi}\n- Lý do: {f8.LyDoDoiCa}" : "";
                    case "HR_DonHoTroCongTac_9":
                        var f9 = item.HrDonHoTroCongTac9s.FirstOrDefault();
                        if (f9 == null) return "";
                        string dv = (f9.DatVeMayBay == true ? "[Vé máy bay] " : "") + (f9.DatChoO == true ? "[Phòng nghỉ] " : "") + (f9.DatBuaAn == true ? "[Bữa ăn] " : "");
                        return $"- Khách: {f9.TenKhachHang}\n- Dịch vụ: {dv}\n- Nội dung: {f9.NoiDungYeuCauChiTiet}";
                    default: return "N/A";
                }
            }
            catch { return "Lỗi xử lý dữ liệu chi tiết"; }
        }

        #endregion

        #region BÁO CÁO THỐNG KÊ FORM HR (Đồng bộ 7 trạng thái)

        [HttpGet("/FormHR/BaoCaoThongKe")]
        public async Task<IActionResult> BaoCaoThongKe()
        {
            if (!User.Identity.IsAuthenticated) return Redirect("/DonXetDuyet/DangNhap");
            bool isAuthorized = User.IsInRole("AdminHR") || User.IsInRole("All");
            if (!isAuthorized) return Redirect("/");

            var allForms = await _context.FormHrs
                .AsNoTracking()
                .OrderByDescending(x => x.TimeNguoiTao)
                .ToListAsync();

            return View(allForms);
        }

        [HttpGet("/FormHR/GetDataThongKe")]
        public async Task<IActionResult> GetDataThongKe()
        {
            var dataRaw = await _context.FormHrs
                .Include(f => f.HrQuanLyDuyetB2s)
                .Include(f => f.HrNguoiXacNhans)
                .AsNoTracking()
                .Select(x => new
                {
                    x.Id,
                    x.IdForm,
                    TenLoaiDon = GetShortName(x.IdForm),
                    x.BoPhan,
                    x.IdNguoiDuyet,
                    x.IdAdmin,
                    x.TenForm,
                    x.Danhmuc,
                    // Lấy danh sách trạng thái để tính toán logic tại API
                    B2States = x.HrQuanLyDuyetB2s.Select(b => b.TrangThaiXacNhan),
                    GDStates = x.HrNguoiXacNhans.Select(g => g.TrangThaiXacNhan)
                })
                .ToListAsync();

            var processedData = dataRaw.Select(x => new
            {
                x.Id,
                x.TenLoaiDon,
                x.BoPhan,
                x.Danhmuc,
                TrangThaiDon = CalculateStatus(x.TenForm, x.IdNguoiDuyet, x.IdAdmin, x.B2States, x.GDStates)
            });

            return Json(processedData);
        }

        [HttpGet("/FormHR/GetDataNguoiHoTro")]
        public async Task<IActionResult> GetDataNguoiHoTro()
        {
            var allDetails = await _context.HrCtNguoiHoTros
                .AsNoTracking()
                .Include(x => x.IdHrNguoiHoTroNavigation)
                .Include(x => x.IdFormHrNavigation)
                    .ThenInclude(f => f.HrQuanLyDuyetB2s)
                .Include(x => x.IdFormHrNavigation)
                    .ThenInclude(f => f.HrNguoiXacNhans)
                .ToListAsync();

            var filteredData = allDetails
                .GroupBy(x => x.IdFormHr)
                .Select(group =>
                {
                    var topSupport = group.OrderByDescending(x => x.Stt).FirstOrDefault();
                    if (topSupport?.IdFormHrNavigation == null) return null;

                    var f = topSupport.IdFormHrNavigation;
                    double? minutes = (f.TimeAdmin.HasValue && f.TimeNguoiDuyet.HasValue)
                                      ? (f.TimeAdmin.Value - f.TimeNguoiDuyet.Value).TotalMinutes : null;

                    return new
                    {
                        TenNguoiHoTro = topSupport.IdHrNguoiHoTroNavigation?.Ten ?? "Chưa xác định",
                        DanhMuc = f.Danhmuc ?? "N/A",
                        TrangThai = CalculateStatus(f.TenForm, f.IdNguoiDuyet, f.IdAdmin,
                                    f.HrQuanLyDuyetB2s.Select(b => b.TrangThaiXacNhan),
                                    f.HrNguoiXacNhans.Select(g => g.TrangThaiXacNhan)),
                        PhutXuLy = minutes
                    };
                })
                .Where(x => x != null)
                .ToList();

            return Json(filteredData);
        }

       

        #endregion

        #region QUY TRÌNH KIỂM SOÁT CỔNG BẢO VỆ

        /// <summary>
        /// Trang dành riêng cho bộ phận Bảo vệ để xác nhận ra/vào cổng dựa trên đơn đã được duyệt
        /// </summary>
        [HttpGet("/FormHR/BaoVePheDuyet")]
        public async Task<IActionResult> BaoVePheDuyet()
        {
            // --- 1. LẤY THÔNG TIN TỪ CLAIMS (Đồng bộ với QuanLyXetDuyet) ---
            var userIdStr = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userIdStr)) return Redirect("/DonXetDuyet/DangNhap");

            var tenCongTy = User.FindFirst("TenCongTy")?.Value?.Trim() ?? "";
            var userRole = (User.FindFirst(System.Security.Claims.ClaimTypes.Role)?.Value
                           ?? User.FindFirst("UserRole")?.Value
                           ?? "GUEST").Trim().ToUpper();

            // --- 2. TRUY VẤN DỮ LIỆU (Include đầy đủ để đồng bộ logic View) ---
            IQueryable<FormHr> query = _context.FormHrs
                .Include(f => f.HrNguoiXacNhans)
                .Include(f => f.HrQuanLyDuyetB2s)
                .Include(f => f.BaoVeHrs)
                .Include(f => f.HrCtNguoiHoTros)
                    .ThenInclude(ct => ct.IdHrNguoiHoTroNavigation);

            // --- 3. LOGIC PHÂN QUYỀN & LỌC ---
            // Lọc theo công ty (nếu không phải Role All)
            if (!User.IsInRole("All") && !string.IsNullOrEmpty(tenCongTy))
            {
                query = query.Where(f => f.TenCongTy == tenCongTy);
            }

            // LỌC RIÊNG CHO BẢO VỆ: Chỉ lấy những đơn có yêu cầu bảo vệ (BaoVeHrs không trống)
            query = query.Where(f => f.BaoVeHrs != null && f.BaoVeHrs.Any());

            try
            {
                var danhSachDon = await query
                    .OrderByDescending(f => f.Id)
                    .AsNoTracking()
                    .ToListAsync();

                ViewBag.UserRole = userRole;
                return View(danhSachDon);
            }
            catch (Exception ex)
            {
                TempData["Error"] = "Lỗi hệ thống bảo vệ: " + ex.Message;
                return View(new List<FormHr>());
            }
        }

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

                var userEmail = User.FindFirst(System.Security.Claims.ClaimTypes.Email)?.Value ?? "";
                var userName = User.FindFirst(System.Security.Claims.ClaimTypes.Name)?.Value ?? "N/A";
                var phongBan = User.FindFirst("PhongBan")?.Value ?? "N/A";

                var record = new BaoVeHr
                {
                    IdFormHr = dto.IdFormHr,
                    TrangThai = 0   // 0 = chờ xử lý, 1 = hoàn thành
                };

                _context.BaoVeHrs.Add(record);

                // Ghi nhật ký (Theo cấu trúc chi tiết)
                _context.LichSuFormHrs.Add(new LichSuFormHr
                {
                    IdFormHr = dto.IdFormHr,
                    TieuDe = "ĐẨY SANG BẢO VỆ",
                    Mota = $"Nhân sự thực hiện: {userName} ({userEmail}). " +
                           $"Bộ phận: {phongBan}. " +
                           $"Nội dung: Đã chuyển phiếu sang đội Bảo Vệ xử lý.",
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
                var userName = User.FindFirst(System.Security.Claims.ClaimTypes.Name)?.Value ?? "N/A";
                var phongBan = User.FindFirst("PhongBan")?.Value ?? "N/A";

                record.GhiChu = dto.GhiChu;
                record.IdBaoVe = userEmail;
                record.TenBaoVe = userName;
                record.TimeBaoVe = DateTime.Now;
                record.TrangThai = 1;   // Hoàn thành

                // Ghi nhật ký (Theo cấu trúc chi tiết)
                _context.LichSuFormHrs.Add(new LichSuFormHr
                {
                    IdFormHr = record.IdFormHr,
                    TieuDe = "BẢO VỆ XÁC NHẬN HOÀN TẤT",
                    Mota = $"Nhân sự thực hiện: {userName} ({userEmail}). " +
                           $"Bộ phận: {phongBan}. " +
                           $"Nội dung: Bảo Vệ đã xác nhận hoàn tất. Ghi chú: {dto.GhiChu}",
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

        #endregion

        #region LỊCH SỬ VÀ THÔNG BÁO FORM HR (PHÂN TÁCH ALL & GIAMDOCHR)

        [HttpGet("/FormHR/LichSuHR")]
        public async Task<IActionResult> LogLichSuHR()
        {
            // 1. Lấy thông tin User từ Claims
            var userIdStr = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userIdStr)) return RedirectToAction("DangNhap", "DonXetDuyet");

            int userId = int.Parse(userIdStr);
            var userEmail = (User.FindFirst(System.Security.Claims.ClaimTypes.Email)?.Value ?? "").Trim().ToLower();
            var tenCongTy = User.FindFirst("TenCongTy")?.Value?.Trim() ?? "";

            // Đánh dấu các quyền của User
            bool isAll = User.IsInRole("All");
            bool isAdminHR = User.IsInRole("AdminHR");
            bool isGiamDocHR = User.IsInRole("GiamDocHR");
            bool isBaoVeHR = User.IsInRole("BaoVeHR");
            bool isQuanLyB2 = User.IsInRole("QuanLyDuyetDonHR_B2");
            bool isQuanLyDuyet = User.IsInRole("QuanLyDuyetDonHR");

            // 2. Khởi tạo Query cơ bản
            var query = _context.LichSuFormHrs
                .Include(l => l.IdFormHrNavigation)
                    .ThenInclude(f => f.HrNguoiXacNhans)
                .Include(l => l.IdFormHrNavigation)
                    .ThenInclude(f => f.HrCtNguoiHoTros)
                        .ThenInclude(ct => ct.IdHrNguoiHoTroNavigation)
                .Include(l => l.IdFormHrNavigation)
                    .ThenInclude(f => f.HrQuanLyDuyetB2s)
                .Include(l => l.IdFormHrNavigation)
                    .ThenInclude(f => f.BaoVeHrs)
                .AsQueryable();

            // 3. Logic lọc độc lập (OR): Cộng dồn các quyền
            query = query.Where(l =>
                // 1. QUYỀN ALL: Thấy toàn bộ hệ thống
                isAll ||

                // 2. CHÍNH CHỦ (Người tạo): Thấy đơn của mình từ đầu đến cuối
                l.IdFormHrNavigation.IdNguoiTao == userId ||

                // 3. NGƯỜI HỖ TRỢ (AdminHR): 
                // Thỏa mãn: Có Role AdminHR + Được gắn vào đơn + Quản lý cấp 1 đã duyệt
                (isAdminHR &&
                 l.IdFormHrNavigation.IdNguoiDuyet != null &&
                 l.IdFormHrNavigation.HrCtNguoiHoTros.Any(ct =>
                    ct.IdHrNguoiHoTroNavigation != null &&
                    ct.IdHrNguoiHoTroNavigation.MaNv.ToLower() == userEmail)) ||

                // 4. Các quyền xác nhận khác (Cũng cần qua bước duyệt 1)
                (l.IdFormHrNavigation.IdNguoiDuyet != null && (
                    (isGiamDocHR && l.IdFormHrNavigation.HrNguoiXacNhans.Any(xn => xn.IdnguoiXacNhan == userId || (xn.MaNguoiXacNhan != null && xn.MaNguoiXacNhan.ToLower() == userEmail))) ||
                    (isQuanLyB2 && l.IdFormHrNavigation.HrQuanLyDuyetB2s.Any(b2 => b2.IdnguoiXacNhan == userId || (b2.MaNguoiXacNhan != null && b2.MaNguoiXacNhan.ToLower() == userEmail))) ||
                    (isBaoVeHR && l.IdFormHrNavigation.BaoVeHrs.Any())
                )) ||

                // 5. QUẢN LÝ DUYỆT (Cấp 1): Thấy đơn mình là người duyệt
                (isQuanLyDuyet && l.IdFormHrNavigation.IdNguoiDuyet == userId)
            );

            var logs = await query.OrderByDescending(l => l.Time).AsNoTracking().ToListAsync();
            return View(logs);
        }

        [HttpGet("/FormHR/GetNotificationsHR")]
        public async Task<IActionResult> GetNotificationsHR(int skip = 0, int take = 20)
        {
            var userIdStr = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userIdStr)) return Unauthorized();

            int userId = int.Parse(userIdStr);
            var userEmail = (User.FindFirst(System.Security.Claims.ClaimTypes.Email)?.Value ?? "").Trim().ToLower();

            bool isAll = User.IsInRole("All");
            bool isAdminHR = User.IsInRole("AdminHR");
            bool isGiamDocHR = User.IsInRole("GiamDocHR");
            bool isBaoVeHR = User.IsInRole("BaoVeHR");
            bool isQuanLyB2 = User.IsInRole("QuanLyDuyetDonHR_B2");
            bool isQuanLyDuyet = User.IsInRole("QuanLyDuyetDonHR");

            var query = _context.LichSuFormHrs
               .Include(l => l.IdFormHrNavigation)
                    .ThenInclude(f => f.HrCtNguoiHoTros)
                        .ThenInclude(ct => ct.IdHrNguoiHoTroNavigation)
               .AsQueryable();

            // --- ĐỒNG BỘ LOGIC THÔNG BÁO ---
            query = query.Where(l =>
                isAll ||
                l.IdFormHrNavigation.IdNguoiTao == userId ||
                // Người hỗ trợ: Phải có Role + Được gắn + QL đã duyệt
                (isAdminHR &&
                 l.IdFormHrNavigation.IdNguoiDuyet != null &&
                 l.IdFormHrNavigation.HrCtNguoiHoTros.Any(ct =>
                    ct.IdHrNguoiHoTroNavigation != null &&
                    ct.IdHrNguoiHoTroNavigation.MaNv.ToLower() == userEmail)) ||
                (l.IdFormHrNavigation.IdNguoiDuyet != null && (
                    (isGiamDocHR && l.IdFormHrNavigation.HrNguoiXacNhans.Any(xn => xn.IdnguoiXacNhan == userId || (xn.MaNguoiXacNhan != null && xn.MaNguoiXacNhan.ToLower() == userEmail))) ||
                    (isQuanLyB2 && l.IdFormHrNavigation.HrQuanLyDuyetB2s.Any(b2 => b2.IdnguoiXacNhan == userId || (b2.MaNguoiXacNhan != null && b2.MaNguoiXacNhan.ToLower() == userEmail))) ||
                    (isBaoVeHR && l.IdFormHrNavigation.BaoVeHrs.Any())
                )) ||
                (isQuanLyDuyet && l.IdFormHrNavigation.IdNguoiDuyet == userId)
            );

            var unreadCount = await query.CountAsync(l => l.IsRead != true);
            var logs = await query.OrderByDescending(l => l.Time)
                                  .Skip(skip)
                                  .Take(take)
                                  .Select(l => new
                                  {
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

        #region QUẢN LÝ PHÒNG HỌP (HR & Quản lý)

        // =================================================================================
        // 1. PHẦN XỬ LÝ LỊCH ĐẶT PHÒNG (Dữ liệu giao dịch từ đơn HrDonTiepKhac5)
        // =================================================================================

        /// <summary>
        /// Giao diện chính: Tab 1 (Lịch sử/Trạng thái đặt) & Tab 2 (Danh mục gốc)
        /// </summary>
        [HttpGet("/FormHR/QuanLyPhongHop")]
        public async Task<IActionResult> QuanLyPhongHop()
        {
            // 1. Kiểm tra đăng nhập
            var userIdStr = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userIdStr)) return Redirect("/DonXetDuyet/DangNhap");

            var tenCongTy = User.FindFirst("TenCongTy")?.Value?.Trim() ?? "";
            bool isAll = User.IsInRole("All") || User.IsInRole("AdminHR");

            // --- Lấy danh mục phòng gốc (Master Data) gửi qua ViewBag cho Tab 2 ---
            ViewBag.DanhMucPhong = await _context.PhongHopHrs.AsNoTracking().ToListAsync();

            // --- Lấy danh sách các đơn đã đăng ký phòng (Transaction Data) cho Tab 1 ---
            IQueryable<HrDonTiepKhac5> query = _context.HrDonTiepKhac5s
                .Include(x => x.IdFormHrNavigation)
                .Where(x => !string.IsNullOrEmpty(x.TenPhongHop));

            // Phân quyền: Admin xem tất cả, User thường/BP xem theo công ty
            if (!isAll && !string.IsNullOrEmpty(tenCongTy))
            {
                query = query.Where(x => x.IdFormHrNavigation.TenCongTy == tenCongTy);
            }

            var lichDatPhong = await query.OrderByDescending(x => x.Id).ToListAsync();
            return View(lichDatPhong);
        }

        /// <summary>
        /// Cập nhật trạng thái sử dụng phòng (Dùng AJAX - Toggle ON/OFF)
        /// </summary>
        [HttpPost("/FormHR/UpdateTrangThaiPhong")]
        [ValidateAntiForgeryToken] // Chống tấn công giả mạo đơn hàng
        public async Task<IActionResult> UpdateTrangThaiPhong(int id, string status)
        {
            var userIdStr = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userIdStr)) return Json(new { success = false, message = "Hết phiên đăng nhập" });

            var chiTiet = await _context.HrDonTiepKhac5s.Include(x => x.IdFormHrNavigation).FirstOrDefaultAsync(x => x.Id == id);
            if (chiTiet == null) return Json(new { success = false, message = "Không tìm thấy dữ liệu đơn." });

            bool isAdmin = User.IsInRole("All") || User.IsInRole("AdminHR");
            bool isOwner = chiTiet.IdFormHrNavigation?.IdNguoiTao.ToString() == userIdStr;

            if (!isAdmin && !isOwner)
                return Json(new { success = false, message = "Bạn không có quyền thao tác trên đơn này." });

            try
            {
                if (status == "on")
                {
                    // Kiểm tra xem phòng này có đang bị đơn khác chiếm dụng 'on' không
                    var trungLich = await _context.HrDonTiepKhac5s
                        .AnyAsync(x => x.Id != id
                                    && x.TenPhongHop == chiTiet.TenPhongHop
                                    && x.TrangThaiPhong == "on"
                                    && x.ThoiGianBatDau < chiTiet.ThoiGianKetThuc
                                    && x.ThoiGianKetThuc > chiTiet.ThoiGianBatDau);

                    if (trungLich) return Json(new { success = false, message = "Phòng hiện đang có lịch 'ON' khác trùng thời gian." });
                }

                chiTiet.TrangThaiPhong = status;

                // Lưu vết lịch sử
                _context.LichSuFormHrs.Add(new LichSuFormHr
                {
                    IdFormHr = chiTiet.IdFormHr ?? 0,
                    TieuDe = "Cập nhật trạng thái phòng",
                    Mota = $"Thao tác: {status.ToUpper()}. Người thực hiện: {User.FindFirst(ClaimTypes.Name)?.Value}.",
                    Time = DateTime.Now
                });

                await _context.SaveChangesAsync();
                return Json(new { success = true, message = "Cập nhật thành công!" });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = "Lỗi: " + ex.Message });
            }
        }

        /// <summary>
        /// API kiểm tra nhanh phòng trống khi người dùng đang làm đơn
        /// </summary>
        [HttpGet("/FormHR/CheckPhongTrong")]
        public async Task<IActionResult> CheckPhongTrong(string tenPhong, DateTime batDau, DateTime ketThuc)
        {
            var lichTrung = await _context.HrDonTiepKhac5s
                .Where(x => x.TenPhongHop == tenPhong && x.TrangThaiPhong == "on")
                .Where(x => (batDau < x.ThoiGianKetThuc && ketThuc > x.ThoiGianBatDau))
                .Select(x => new { x.ThoiGianBatDau, x.ThoiGianKetThuc, x.NguoiBook })
                .ToListAsync();

            if (lichTrung.Any())
                return Json(new { trong = false, message = "Phòng đã bận.", data = lichTrung });

            return Json(new { trong = true });
        }

        // =================================================================================
        // 2. PHẦN QUẢN LÝ DANH MỤC PHÒNG HỌP (Master Data - Bảng PhongHopHR)
        // =================================================================================

        /// <summary>
        /// Thêm mới hoặc Cập nhật danh mục phòng họp
        /// </summary>
        [HttpPost("/FormHR/LuuPhongHop")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> LuuPhongHop(PhongHopHr model)
        {
            if (!User.IsInRole("AdminHR") && !User.IsInRole("All")) return Forbid();

            try
            {
                if (model.Id == 0)
                {
                    // THÊM MỚI
                    _context.PhongHopHrs.Add(model);
                    TempData["Success"] = "Đã thêm phòng họp mới vào danh sách.";
                }
                else
                {
                    // SỬA: Tìm đối tác cũ để cập nhật (Tránh lỗi tạo mới khi sửa)
                    var existing = await _context.PhongHopHrs.FindAsync(model.Id);
                    if (existing == null)
                    {
                        TempData["Error"] = "Không tìm thấy phòng họp để cập nhật.";
                        return RedirectToAction("QuanLyPhongHop");
                    }

                    existing.TenPhongHop = model.TenPhongHop;
                    existing.GhiChu = model.GhiChu;

                    _context.PhongHopHrs.Update(existing);
                    TempData["Success"] = "Đã cập nhật thông tin phòng họp.";
                }

                await _context.SaveChangesAsync();
            }
            catch (Exception ex)
            {
                TempData["Error"] = "Lỗi hệ thống: " + ex.Message;
            }

            return RedirectToAction("QuanLyPhongHop");
        }

        /// <summary>
        /// Xóa phòng họp (Dùng AJAX)
        /// </summary>
        [HttpPost("/FormHR/XoaPhongHop")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> XoaPhongHop(int id)
        {
            if (!User.IsInRole("AdminHR") && !User.IsInRole("All"))
                return Json(new { success = false, message = "Bạn không có quyền xóa dữ liệu này." });

            try
            {
                var phong = await _context.PhongHopHrs.FindAsync(id);
                if (phong == null)
                    return Json(new { success = false, message = "Dữ liệu không tồn tại hoặc đã bị xóa trước đó." });

                _context.PhongHopHrs.Remove(phong);
                await _context.SaveChangesAsync();

                return Json(new { success = true, message = "Đã xóa phòng họp khỏi danh mục." });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = "Không thể xóa: " + ex.Message });
            }
        }

        #endregion

    }
}
