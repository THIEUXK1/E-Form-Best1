using ClosedXML.Excel; // Cần cài đặt package ClosedXML
using DocumentFormat.OpenXml.InkML;
using E_Form_Best.Context;
using E_Form_Best.Models.ITForm;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.IO;
using System.Security.Claims;
using static E_Form_Best.Areas.ITForm.Controllers.ITFormController;

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
        // Thay đổi kiểu trả về thành byte[]? để thông báo tường minh rằng hàm có thể trả về null
        private async Task<byte[]?> GetFileBytesAsync2(string inputName)
        {
            try
            {
                // Kiểm tra xem trong request có file đính kèm với name tương ứng không
                if (Request.Form.Files.Count > 0)
                {
                    var file = Request.Form.Files[inputName];
                    // Sử dụng toán tử null-conditional để đảm bảo an toàn nếu file là null
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
                // Giữ nguyên cơ chế bắt lỗi của bạn
                return null;
            }
            // Trả về null nếu không tìm thấy file hoặc file rỗng
            return null;
        }
        #endregion

        #region Don Xin Ra Ngoai (HrXinRaNgoai1)

        [HttpGet("/FormHR/DonXinRaNgoai")]
        public IActionResult DonXinRaNgoai()
        {
            // Kiểm tra User và User.Identity trước khi truy cập IsAuthenticated
            if (User?.Identity == null || !User.Identity.IsAuthenticated)
                return Redirect("/DonXetDuyet/DangNhap");

            var userIdString = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userIdString))
                return Redirect("/DonXetDuyet/DangNhap");

            int userId = int.Parse(userIdString);

            // Sử dụng toán tử null-coalescing (??) để đảm bảo không gán null vào model
            var userName = User.FindFirst(ClaimTypes.Name)?.Value ?? "";
            var phongBan = User.FindFirst("PhongBan")?.Value ?? "";
            var viTri = User.FindFirst(ClaimTypes.Role)?.Value ?? "";
            var soNhanVien = User.FindFirst(ClaimTypes.Email)?.Value ?? "";
            var tenCongTy = User.FindFirst("TenCongTy")?.Value ?? "";

            // --- Lấy danh sách nhân sự hỗ trợ ---
            ViewBag.ListNguoiHoTro = _context.HrNguoiHoTros
                .Include(x => x.CongViecHrs.Where(cv => cv != null && cv.Ten == "Đăng ký đơn xin ra ngoài"))
                .Where(x => x.CongViecHrs != null && x.CongViecHrs.Any(cv => cv != null && cv.Ten == "Đăng ký đơn xin ra ngoài"))
                .ToList();

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
            // Kiểm tra User.Identity trước khi sử dụng
            if (User.Identity == null || !User.Identity.IsAuthenticated) return Redirect("/DonXetDuyet/DangNhap");

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
                    // --- BƯỚC 1: LƯU BẢNG CHÍNH ---
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

                    // --- BƯỚC 2: LƯU NHÂN SỰ HỖ TRỢ ---
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

                    // --- BƯỚC 3: TỰ ĐỘNG THÊM NGƯỜI XÁC NHẬN ---
                    var listDuyetTheoBoPhan = await _context.DmNguoiDuyetLoaiDonBoPhans
                        .Include(x => x.IdnguoiXacNhanNavigation)
                        .Include(x => x.DmCtChiTietUyQuyens.Where(uq => uq.TrangThai == true))
                            .ThenInclude(uq => uq.IduyQuyenNavigation)
                        .Where(x => x.IdloaiDonNavigation != null && x.IdloaiDonNavigation.MaLoaiDon == form.IdForm
                                    && x.IdboPhanNavigation != null && x.IdboPhanNavigation.TenBoPhan == form.BoPhan
                                    && x.IdcongTyNavigation != null && x.IdcongTyNavigation.TenCongTy == form.TenCongTy
                                    && x.TrangThai == true)
                        .OrderBy(x => x.Stt)
                        .ToListAsync();

                    foreach (var item in listDuyetTheoBoPhan)
                    {
                        var quanLyDuyet = new HrQuanLyDuyetB2
                        {
                            IdFormHr = form.Id,
                            IdnguoiXacNhan = item.IdnguoiXacNhan,
                            ThuTuXacNhan = item.Stt,
                            MaNguoiXacNhan = item.IdnguoiXacNhanNavigation?.MaNv,
                            TenNguoiXacNhan = item.IdnguoiXacNhanNavigation?.HoTen,
                            TrangThaiXacNhan = 0,
                            ThoiGianXacNhan = null,
                            Loai = item.Loai
                        };
                        _context.HrQuanLyDuyetB2s.Add(quanLyDuyet);
                        await _context.SaveChangesAsync();

                        var currentTime = DateTime.Now;
                        var listUyQuyenHopLe = item.DmCtChiTietUyQuyens
                            .Where(uq => (uq.ThoiGianBatDau == null || uq.ThoiGianBatDau <= currentTime) && (uq.ThoiGianKetThuc == null || uq.ThoiGianKetThuc >= currentTime))
                            .ToList();

                        foreach (var uq in listUyQuyenHopLe.Where(x => x.IduyQuyenNavigation != null))
                        {
                            _context.HrQuanLyDuyetB2UyQuyens.Add(new HrQuanLyDuyetB2UyQuyen
                            {
                                IdHrQuanLyDuyetB2 = quanLyDuyet.Id,
                                MaNvuyQuyen = uq.IduyQuyenNavigation!.MaNvuyQuyen, // Sử dụng ! vì đã kiểm tra null ở trên
                                HoTenUyQuyen = uq.IduyQuyenNavigation.HoTenUyQuyen
                            });
                        }
                    }
                    await _context.SaveChangesAsync();

                    // --- BƯỚC 4: LƯU LỊCH SỬ THAO TÁC ---
                    string chiTietDon = "";
                    if (xinRaNgoai != null)
                    {
                        chiTietDon = $"- Người xin: {xinRaNgoai.NguoiXin}\n" +
                                     $"- Lý do: {xinRaNgoai.LiDo}\n" +
                                     $"- Địa điểm: {xinRaNgoai.DiaDiem}\n" +
                                     $"- Thời gian ra: {xinRaNgoai.ThoiGianRa?.ToString("dd/MM/yyyy HH:mm")}\n" +
                                     $"- Dự kiến về: {xinRaNgoai.ThoiGianVeDuTinh?.ToString("dd/MM/yyyy HH:mm")}";
                    }

                    _context.LichSuFormHrs.Add(new LichSuFormHr
                    {
                        IdFormHr = form.Id,
                        TieuDe = "Khởi tạo đơn xin ra ngoài",
                        Mota = $"Nhân viên {userName} ({soNhanVien}) đã tạo đơn.\n{chiTietDon}",
                        Time = DateTime.Now,
                        IsRead = false,
                        TrangThaiAnHien = true
                    });
                    await _context.SaveChangesAsync();

                    // --- BƯỚC 5: XỬ LÝ FILE/ẢNH ---
                    if (!Directory.Exists(networkPath)) Directory.CreateDirectory(networkPath);
                    string safeName = RemoveSign4VietnameseString(userName).Replace(" ", "");
                    string timeStamp = DateTime.Now.ToString("ddMMyy_HHmm");

                    if (Request.Form.Files["UploadFile"] is { Length: > 0 } uploadFile)
                    {
                        string ext = Path.GetExtension(uploadFile.FileName);
                        string fileName = $"File_Don{form.Id}_User{userId}_{safeName}_{timeStamp}{ext}";
                        using (var fs = new FileStream(Path.Combine(networkPath, fileName), FileMode.Create)) await uploadFile.CopyToAsync(fs);
                        form.FileDinhKem = fileName;
                    }

                    if (xinRaNgoai != null)
                    {
                        xinRaNgoai.IdFormHr = form.Id;
                        if (Request.Form.Files["AnhXinRaNgoai"] is { Length: > 0 } anhFile)
                        {
                            string imgExt = Path.GetExtension(anhFile.FileName) ?? ".jpg";
                            string imgName = $"Anh_Don{form.Id}_User{userId}_{safeName}_{timeStamp}{imgExt}";
                            using (var fs = new FileStream(Path.Combine(networkPath, imgName), FileMode.Create)) await anhFile.CopyToAsync(fs);
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
            // Kiểm tra an toàn cho User và Identity
            if (User?.Identity == null || !User.Identity.IsAuthenticated)
                return Redirect("/DonXetDuyet/DangNhap");

            var userIdString = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userIdString)) return Redirect("/DonXetDuyet/DangNhap");

            int userId = int.Parse(userIdString);
            var userName = User.FindFirst(ClaimTypes.Name)?.Value ?? "";
            var phongBan = User.FindFirst("PhongBan")?.Value ?? "";
            var viTri = User.FindFirst(ClaimTypes.Role)?.Value ?? "";
            var soNhanVien = User.FindFirst(ClaimTypes.Email)?.Value ?? "";
            var tenCongTy = User.FindFirst("TenCongTy")?.Value ?? "";

            // --- Lấy danh sách nhân sự hỗ trợ ---
            // Thêm kiểm tra null cho các navigation property trong truy vấn để đảm bảo an toàn cho trình biên dịch
            ViewBag.ListNguoiHoTro = _context.HrNguoiHoTros
                .Include(x => x.CongViecHrs.Where(cv => cv != null && cv.Ten == "Đăng ký mang hàng hóa ra cổng"))
                .Where(x => x.CongViecHrs != null && x.CongViecHrs.Any(cv => cv != null && cv.Ten == "Đăng ký mang hàng hóa ra cổng"))
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
        public async Task<IActionResult> MangHangHoaRaCong(FormHr form, [FromForm] HrMangHangHoaRaCong2 chiTiet, int[] SelectedCongViecIds, List<string> arrTenLoaiHang, List<string> arrSoLuong)
        {
            if (User?.Identity == null || !User.Identity.IsAuthenticated) return Redirect("/DonXetDuyet/DangNhap");

            // Gia cố Binding nếu chiTiet bị null
            if (chiTiet == null || (string.IsNullOrEmpty(chiTiet.MoTa) && Request.Form.ContainsKey("chiTiet.MoTa")))
            {
                chiTiet = new HrMangHangHoaRaCong2
                {
                    MoTa = Request.Form["chiTiet.MoTa"],
                    TenCong = Request.Form["chiTiet.TenCong"]
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
                    // --- BƯỚC 1: LƯU BẢNG CHÍNH ---
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

                    // --- BƯỚC 2: LƯU NHÂN SỰ HỖ TRỢ ---
                    if (SelectedCongViecIds != null && SelectedCongViecIds.Length > 0)
                    {
                        foreach (var cvId in SelectedCongViecIds)
                        {
                            var congViec = await _context.CongViecHrs.FindAsync(cvId);
                            if (congViec != null)
                            {
                                _context.HrCtNguoiHoTros.Add(new HrCtNguoiHoTro
                                {
                                    IdFormHr = form.Id,
                                    IdHrNguoiHoTro = congViec.IdHrNguoiHoTro,
                                    Stt = 1
                                });
                            }
                        }
                        await _context.SaveChangesAsync();
                    }

                    // --- BƯỚC 3: TỰ ĐỘNG THÊM NGƯỜI XÁC NHẬN ---
                    var listDuyet = await _context.DmNguoiDuyetLoaiDonBoPhans
                        .Include(x => x.IdnguoiXacNhanNavigation)
                        .Include(x => x.DmCtChiTietUyQuyens.Where(uq => uq.TrangThai == true))
                            .ThenInclude(uq => uq.IduyQuyenNavigation)
                        .Where(x => x.IdloaiDonNavigation != null && x.IdloaiDonNavigation.MaLoaiDon == form.IdForm
                                    && x.IdboPhanNavigation != null && x.IdboPhanNavigation.TenBoPhan == form.BoPhan
                                    && x.IdcongTyNavigation != null && x.IdcongTyNavigation.TenCongTy == form.TenCongTy
                                    && x.TrangThai == true)
                        .OrderBy(x => x.Stt)
                        .ToListAsync();

                    foreach (var item in listDuyet)
                    {
                        var quanLyDuyet = new HrQuanLyDuyetB2
                        {
                            IdFormHr = form.Id,
                            IdnguoiXacNhan = item.IdnguoiXacNhan,
                            ThuTuXacNhan = item.Stt,
                            MaNguoiXacNhan = item.IdnguoiXacNhanNavigation?.MaNv,
                            TenNguoiXacNhan = item.IdnguoiXacNhanNavigation?.HoTen,
                            TrangThaiXacNhan = 0,
                            Loai = item.Loai
                        };
                        _context.HrQuanLyDuyetB2s.Add(quanLyDuyet);
                        await _context.SaveChangesAsync();

                        var currentTime = DateTime.Now;
                        var listUyQuyen = item.DmCtChiTietUyQuyens
                            .Where(uq => (uq.ThoiGianBatDau == null || uq.ThoiGianBatDau <= currentTime) && (uq.ThoiGianKetThuc == null || uq.ThoiGianKetThuc >= currentTime));

                        foreach (var uq in listUyQuyen.Where(x => x.IduyQuyenNavigation != null))
                        {
                            _context.HrQuanLyDuyetB2UyQuyens.Add(new HrQuanLyDuyetB2UyQuyen
                            {
                                IdHrQuanLyDuyetB2 = quanLyDuyet.Id,
                                MaNvuyQuyen = uq.IduyQuyenNavigation!.MaNvuyQuyen,
                                HoTenUyQuyen = uq.IduyQuyenNavigation.HoTenUyQuyen
                            });
                        }
                    }

                    // --- BƯỚC 4: XỬ LÝ GHÉP DANH SÁCH MẶT HÀNG ---
                    int tongSl = 0;
                    if (arrTenLoaiHang != null && arrTenLoaiHang.Count > 0 && chiTiet != null)
                    {
                        var dsHang = new List<string>();
                        var dsSoLuong = new List<string>();
                        for (int i = 0; i < arrTenLoaiHang.Count; i++)
                        {
                            if (!string.IsNullOrWhiteSpace(arrTenLoaiHang[i]))
                            {
                                dsHang.Add(arrTenLoaiHang[i].Trim());
                                int sl = (arrSoLuong != null && i < arrSoLuong.Count) ? int.Parse("0" + arrSoLuong[i]) : 0;
                                dsSoLuong.Add(sl.ToString());
                                tongSl += sl;
                            }
                        }
                        chiTiet.TenLoaiHang = string.Join(" | ", dsHang);
                        chiTiet.SoLuong = string.Join(" | ", dsSoLuong);
                    }

                    // --- BƯỚC 5: LỊCH SỬ THAO TÁC ---
                    _context.LichSuFormHrs.Add(new LichSuFormHr
                    {
                        IdFormHr = form.Id,
                        TieuDe = "Khởi tạo đơn mang hàng",
                        Mota = $"Nhân viên {userName} ({soNhanVien}) tạo đơn. Tổng SL: {tongSl}",
                        Time = DateTime.Now,
                        IsRead = false,
                        TrangThaiAnHien = true
                    });
                    await _context.SaveChangesAsync();

                    // --- BƯỚC 6: XỬ LÝ FILE/ẢNH ---
                    if (!Directory.Exists(networkPath)) Directory.CreateDirectory(networkPath);
                    string safeName = RemoveSign4VietnameseString(userName).Replace(" ", "");
                    string timeStamp = DateTime.Now.ToString("ddMMyy_HHmm");

                    if (Request.Form.Files["UploadFile"] is { Length: > 0 } uploadFile)
                    {
                        string fileName = $"File_NT_Don{form.Id}_{safeName}_{timeStamp}{Path.GetExtension(uploadFile.FileName)}";
                        using (var fs = new FileStream(Path.Combine(networkPath, fileName), FileMode.Create)) await uploadFile.CopyToAsync(fs);
                        form.FileDinhKem = fileName;
                    }

                    if (chiTiet != null)
                    {
                        chiTiet.IdFormHr = form.Id;
                        if (Request.Form.Files["AnhHangHoa"] is { Length: > 0 } anhFile)
                        {
                            string imgName = $"Anh_Hang_Don{form.Id}_{safeName}_{timeStamp}{Path.GetExtension(anhFile.FileName) ?? ".jpg"}";
                            using (var fs = new FileStream(Path.Combine(networkPath, imgName), FileMode.Create)) await anhFile.CopyToAsync(fs);
                            chiTiet.DuongDanAnh = imgName;
                        }
                        _context.HrMangHangHoaRaCong2s.Add(chiTiet);
                        await _context.SaveChangesAsync();
                    }

                    _context.Entry(form).State = EntityState.Modified;
                    await _context.SaveChangesAsync();
                    await transaction.CommitAsync();

                    TempData["Success"] = "Gửi đơn thành công!";
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

        #region ĐƠN XE ĐI CÔNG TÁC (HR_DangKySuDungXeCongTac_3)

        [HttpGet("/FormHR/DangKySuDungXeCongTac")]
        public IActionResult DangKySuDungXeCongTac()
        {
            // Kiểm tra an toàn cho User và Identity
            if (User?.Identity == null || !User.Identity.IsAuthenticated)
                return Redirect("/DonXetDuyet/DangNhap");

            var userIdString = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userIdString)) return Redirect("/DonXetDuyet/DangNhap");

            int userId = int.Parse(userIdString);
            var userName = User.FindFirst(ClaimTypes.Name)?.Value ?? "";
            var phongBan = User.FindFirst("PhongBan")?.Value ?? "";
            var viTri = User.FindFirst(ClaimTypes.Role)?.Value ?? "";
            var soNhanVien = User.FindFirst(ClaimTypes.Email)?.Value ?? "";
            var tenCongTy = User.FindFirst("TenCongTy")?.Value ?? "";

            // Lấy danh sách nhân sự hỗ trợ
            // Bổ sung kiểm tra null cho các navigation property để tránh cảnh báo
            ViewBag.ListNguoiHoTro = _context.HrNguoiHoTros
                .Include(x => x.CongViecHrs.Where(cv => cv != null && cv.Ten == "Đăng ký sử dụng xe công tác"))
                .Where(x => x.CongViecHrs != null && x.CongViecHrs.Any(cv => cv != null && cv.Ten == "Đăng ký sử dụng xe công tác"))
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
            // Kiểm tra an toàn cho User và Identity
            if (User?.Identity == null || !User.Identity.IsAuthenticated) return Redirect("/DonXetDuyet/DangNhap");

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
                    // --- BƯỚC 1: LƯU BẢNG CHÍNH ---
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

                    // --- BƯỚC 2: LƯU NHÂN SỰ HỖ TRỢ ---
                    if (SelectedCongViecIds != null && SelectedCongViecIds.Length > 0)
                    {
                        foreach (var cvId in SelectedCongViecIds)
                        {
                            var congViec = await _context.CongViecHrs.FindAsync(cvId);
                            if (congViec != null)
                            {
                                _context.HrCtNguoiHoTros.Add(new HrCtNguoiHoTro
                                {
                                    IdFormHr = form.Id,
                                    IdHrNguoiHoTro = congViec.IdHrNguoiHoTro,
                                    Stt = 1
                                });
                            }
                        }
                        await _context.SaveChangesAsync();
                    }

                    // --- BƯỚC 3: TỰ ĐỘNG THÊM NGƯỜI XÁC NHẬN ---
                    var listDuyetTheoBoPhan = await _context.DmNguoiDuyetLoaiDonBoPhans
                        .Include(x => x.IdnguoiXacNhanNavigation)
                        .Include(x => x.DmCtChiTietUyQuyens.Where(uq => uq.TrangThai == true))
                            .ThenInclude(uq => uq.IduyQuyenNavigation)
                        .Where(x => x.IdloaiDonNavigation != null && x.IdloaiDonNavigation.MaLoaiDon == form.IdForm
                                    && x.IdboPhanNavigation != null && x.IdboPhanNavigation.TenBoPhan == form.BoPhan
                                    && x.IdcongTyNavigation != null && x.IdcongTyNavigation.TenCongTy == form.TenCongTy
                                    && x.TrangThai == true)
                        .OrderBy(x => x.Stt)
                        .ToListAsync();

                    foreach (var item in listDuyetTheoBoPhan)
                    {
                        var quanLyDuyet = new HrQuanLyDuyetB2
                        {
                            IdFormHr = form.Id,
                            IdnguoiXacNhan = item.IdnguoiXacNhan,
                            ThuTuXacNhan = item.Stt,
                            MaNguoiXacNhan = item.IdnguoiXacNhanNavigation?.MaNv,
                            TenNguoiXacNhan = item.IdnguoiXacNhanNavigation?.HoTen,
                            TrangThaiXacNhan = 0,
                            Loai = item.Loai
                        };
                        _context.HrQuanLyDuyetB2s.Add(quanLyDuyet);
                        await _context.SaveChangesAsync();

                        var currentTime = DateTime.Now;
                        var listUyQuyenHopLe = item.DmCtChiTietUyQuyens
                            .Where(uq => (uq.ThoiGianBatDau == null || uq.ThoiGianBatDau <= currentTime) &&
                                         (uq.ThoiGianKetThuc == null || uq.ThoiGianKetThuc >= currentTime))
                            .ToList();

                        foreach (var uq in listUyQuyenHopLe.Where(x => x.IduyQuyenNavigation != null))
                        {
                            _context.HrQuanLyDuyetB2UyQuyens.Add(new HrQuanLyDuyetB2UyQuyen
                            {
                                IdHrQuanLyDuyetB2 = quanLyDuyet.Id,
                                MaNvuyQuyen = uq.IduyQuyenNavigation!.MaNvuyQuyen,
                                HoTenUyQuyen = uq.IduyQuyenNavigation.HoTenUyQuyen
                            });
                        }
                    }
                    await _context.SaveChangesAsync();

                    // --- BƯỚC 4: LƯU LỊCH SỬ ---
                    string chiTietXe = (chiTiet != null) ? $"- Lý do: {chiTiet.LiDo}\n- Thời gian dự tính: {chiTiet.TimeDuTinh?.ToString("dd/MM/yyyy HH:mm")}" : "";

                    _context.LichSuFormHrs.Add(new LichSuFormHr
                    {
                        IdFormHr = form.Id,
                        TieuDe = "Khởi tạo đơn xe công tác",
                        Mota = $"Nhân viên {userName} ({soNhanVien}) đã tạo đơn.\n{chiTietXe}",
                        Time = DateTime.Now,
                        IsRead = false,
                        TrangThaiAnHien = true
                    });
                    await _context.SaveChangesAsync();

                    // --- BƯỚC 5: FILE SERVER ---
                    if (!Directory.Exists(networkPath)) Directory.CreateDirectory(networkPath);
                    string safeName = RemoveSign4VietnameseString(userName).Replace(" ", "");
                    string timeStamp = DateTime.Now.ToString("ddMMyy_HHmm");

                    if (Request.Form.Files["UploadFile"] is { Length: > 0 } uploadFile)
                    {
                        string fName = $"Doc_Xe_Don{form.Id}_{safeName}_{timeStamp}{Path.GetExtension(uploadFile.FileName)}";
                        using (var fs = new FileStream(Path.Combine(networkPath, fName), FileMode.Create)) await uploadFile.CopyToAsync(fs);
                        form.FileDinhKem = fName;
                    }

                    if (chiTiet != null)
                    {
                        chiTiet.IdFormHr = form.Id;
                        if (Request.Form.Files["AnhXe"] is { Length: > 0 } anhXe)
                        {
                            string imgName = $"Anh_Xe_Don{form.Id}_{safeName}_{timeStamp}{Path.GetExtension(anhXe.FileName) ?? ".jpg"}";
                            using (var fs = new FileStream(Path.Combine(networkPath, imgName), FileMode.Create)) await anhXe.CopyToAsync(fs);
                            chiTiet.DuongDanAnh = imgName;
                        }
                        chiTiet.Anh = null;
                        _context.HrDangKySuDungXeCongTac3s.Add(chiTiet);
                        await _context.SaveChangesAsync();
                    }

                    _context.Entry(form).State = EntityState.Modified;
                    await _context.SaveChangesAsync();
                    await transaction.CommitAsync();

                    TempData["Success"] = "Gửi đơn thành công!";
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
            // Kiểm tra an toàn cho User và Identity
            if (User?.Identity == null || !User.Identity.IsAuthenticated)
                return Redirect("/DonXetDuyet/DangNhap");

            var userIdString = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userIdString)) return Redirect("/DonXetDuyet/DangNhap");

            int userId = int.Parse(userIdString);
            var userName = User.FindFirst(ClaimTypes.Name)?.Value ?? "";
            var phongBan = User.FindFirst("PhongBan")?.Value ?? "";
            var viTri = User.FindFirst(ClaimTypes.Role)?.Value ?? "";
            var soNhanVien = User.FindFirst(ClaimTypes.Email)?.Value ?? "";
            var tenCongTy = User.FindFirst("TenCongTy")?.Value ?? "";

            // --- Lấy danh sách nhân sự hỗ trợ ---
            // Bổ sung kiểm tra null cho các navigation property để tránh cảnh báo
            ViewBag.ListNguoiHoTro = _context.HrNguoiHoTros
                .Include(x => x.CongViecHrs.Where(cv => cv != null && cv.Ten == "Đăng ký sử dụng xe Daily"))
                .Where(x => x.CongViecHrs != null && x.CongViecHrs.Any(cv => cv != null && cv.Ten == "Đăng ký sử dụng xe Daily"))
                .ToList();

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
        public async Task<IActionResult> DangKySuDungXeDaily(FormHr form, [FromForm] HrDangKySuDungXeDaily4 chiTiet, int[] SelectedCongViecIds, List<string> arrHoTen, List<string> arrDiemDon, List<string> arrTime)
        {
            // Kiểm tra an toàn User.Identity
            if (User?.Identity == null || !User.Identity.IsAuthenticated) return Redirect("/DonXetDuyet/DangNhap");

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
                    // --- BƯỚC 1: LƯU BẢNG CHÍNH ---
                    form.Ngay = DateOnly.FromDateTime(DateTime.Now);
                    form.IdNguoiTao = userId;
                    form.TenNguoiTao = userName;
                    form.TimeNguoiTao = DateTime.Now;
                    form.TenNguoiNv = userName;
                    form.BoPhan = phongBan;
                    form.ViTri = viTri;
                    form.SoNhanVien = soNhanVien;
                    form.TenCongTy = tenCongTy;
                    form.Danhmuc = "ĐƠN ĐĂNG KÝ SỬ DỤNG XE DAILY";
                    form.TrangThai = "ChoDuyet";
                    form.IdForm = "HR_DangKySuDungXeDaily_4";
                    form.TenForm = "Đăng ký sử dụng xe Daily";

                    _context.FormHrs.Add(form);
                    await _context.SaveChangesAsync();

                    // --- BƯỚC 2: LƯU NHÂN SỰ HỖ TRỢ ---
                    if (SelectedCongViecIds != null && SelectedCongViecIds.Length > 0)
                    {
                        foreach (var cvId in SelectedCongViecIds)
                        {
                            var congViec = await _context.CongViecHrs.FindAsync(cvId);
                            if (congViec != null)
                            {
                                _context.HrCtNguoiHoTros.Add(new HrCtNguoiHoTro
                                {
                                    IdFormHr = form.Id,
                                    IdHrNguoiHoTro = congViec.IdHrNguoiHoTro,
                                    Stt = 1
                                });
                            }
                        }
                        await _context.SaveChangesAsync();
                    }

                    // --- BƯỚC 3: TỰ ĐỘNG THÊM NGƯỜI XÁC NHẬN ---
                    var listDuyet = await _context.DmNguoiDuyetLoaiDonBoPhans
                        .Include(x => x.IdnguoiXacNhanNavigation)
                        .Include(x => x.DmCtChiTietUyQuyens.Where(uq => uq.TrangThai == true))
                            .ThenInclude(uq => uq.IduyQuyenNavigation)
                        .Where(x => x.IdloaiDonNavigation != null && x.IdloaiDonNavigation.MaLoaiDon == form.IdForm
                                    && x.IdboPhanNavigation != null && x.IdboPhanNavigation.TenBoPhan == form.BoPhan
                                    && x.IdcongTyNavigation != null && x.IdcongTyNavigation.TenCongTy == form.TenCongTy
                                    && x.TrangThai == true)
                        .OrderBy(x => x.Stt).ToListAsync();

                    foreach (var item in listDuyet)
                    {
                        var qlDuyet = new HrQuanLyDuyetB2
                        {
                            IdFormHr = form.Id,
                            IdnguoiXacNhan = item.IdnguoiXacNhan,
                            ThuTuXacNhan = item.Stt,
                            MaNguoiXacNhan = item.IdnguoiXacNhanNavigation?.MaNv,
                            TenNguoiXacNhan = item.IdnguoiXacNhanNavigation?.HoTen,
                            TrangThaiXacNhan = 0,
                            Loai = item.Loai
                        };
                        _context.HrQuanLyDuyetB2s.Add(qlDuyet);
                        await _context.SaveChangesAsync();

                        var currentTime = DateTime.Now;
                        var uqs = item.DmCtChiTietUyQuyens.Where(u => (u.ThoiGianBatDau == null || u.ThoiGianBatDau <= currentTime)
                                                                  && (u.ThoiGianKetThuc == null || u.ThoiGianKetThuc >= currentTime));
                        foreach (var uq in uqs.Where(x => x.IduyQuyenNavigation != null))
                        {
                            _context.HrQuanLyDuyetB2UyQuyens.Add(new HrQuanLyDuyetB2UyQuyen
                            {
                                IdHrQuanLyDuyetB2 = qlDuyet.Id,
                                MaNvuyQuyen = uq.IduyQuyenNavigation!.MaNvuyQuyen,
                                HoTenUyQuyen = uq.IduyQuyenNavigation.HoTenUyQuyen
                            });
                        }
                    }
                    await _context.SaveChangesAsync();

                    // --- BƯỚC 4: XỬ LÝ DỮ LIỆU ĐIỂM ĐẾN ---
                    if (arrHoTen != null && arrHoTen.Count > 0 && chiTiet != null)
                    {
                        var dsHoTen = new List<string>();
                        var dsDiemDon = new List<string>();
                        var dsTimeStr = new List<string>();

                        for (int i = 0; i < arrHoTen.Count; i++)
                        {
                            if (!string.IsNullOrWhiteSpace(arrHoTen[i]))
                            {
                                dsHoTen.Add(arrHoTen[i].Trim());
                                dsDiemDon.Add((arrDiemDon != null && i < arrDiemDon.Count) ? arrDiemDon[i].Trim() : "");
                                string tg = (arrTime != null && i < arrTime.Count) ? arrTime[i].Trim() : "";
                                if (DateTime.TryParse(tg, out DateTime dt)) dsTimeStr.Add(dt.ToString("dd/MM/yyyy HH:mm"));
                                else dsTimeStr.Add(tg);
                            }
                        }
                        chiTiet.HoTenNguoiDangKy = string.Join(" | ", dsHoTen);
                        chiTiet.DiemDon = string.Join(" | ", dsDiemDon);
                        if (arrTime != null && arrTime.Count > 0 && DateTime.TryParse(arrTime[0], out var firstTime)) chiTiet.TimeDuTinh = firstTime;
                        if (dsTimeStr.Count > 1) chiTiet.LiDo += $"\n[Chi tiết: {string.Join(" | ", dsTimeStr)}]";
                    }

                    // --- BƯỚC 5: LỊCH SỬ THAO TÁC ---
                    _context.LichSuFormHrs.Add(new LichSuFormHr
                    {
                        IdFormHr = form.Id,
                        TieuDe = "Khởi tạo đơn xe Daily",
                        Mota = $"Nhân viên {userName} đã tạo đơn đăng ký xe Daily.",
                        Time = DateTime.Now,
                        TrangThaiAnHien = true
                    });
                    await _context.SaveChangesAsync();

                    // --- BƯỚC 6: FILE & ẢNH ---
                    if (!Directory.Exists(networkPath)) Directory.CreateDirectory(networkPath);
                    string safeName = RemoveSign4VietnameseString(userName).Replace(" ", "");
                    string timeStamp = DateTime.Now.ToString("ddMMyy_HHmm");

                    if (Request.Form.Files["UploadFile"] is { Length: > 0 } uploadFile)
                    {
                        string fName = $"File_XeDaily_ID{form.Id}_{safeName}_{timeStamp}{Path.GetExtension(uploadFile.FileName)}";
                        using (var fs = new FileStream(Path.Combine(networkPath, fName), FileMode.Create)) await uploadFile.CopyToAsync(fs);
                        form.FileDinhKem = fName;
                    }

                    if (chiTiet != null)
                    {
                        chiTiet.IdFormHr = form.Id;
                        if (Request.Form.Files["AnhXe"] is { Length: > 0 } anhFile)
                        {
                            string imgName = $"Anh_XeDaily_ID{form.Id}_{safeName}_{timeStamp}{Path.GetExtension(anhFile.FileName) ?? ".jpg"}";
                            using (var fs = new FileStream(Path.Combine(networkPath, imgName), FileMode.Create)) await anhFile.CopyToAsync(fs);
                            chiTiet.DuongDanAnh = imgName;
                        }
                        chiTiet.Anh = null;
                        _context.HrDangKySuDungXeDaily4s.Add(chiTiet);
                        await _context.SaveChangesAsync();
                    }

                    _context.Entry(form).State = EntityState.Modified;
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

        #region ĐƠN TIẾP KHÁCH (HR_DonTiepKhac_5)

        [HttpGet("/FormHR/DonTiepKhac")]
        public IActionResult DonTiepKhac()
        {
            // Kiểm tra an toàn User và Identity
            if (User?.Identity == null || !User.Identity.IsAuthenticated)
                return Redirect("/DonXetDuyet/DangNhap");

            var userIdString = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userIdString)) return Redirect("/DonXetDuyet/DangNhap");

            // Lấy dữ liệu với kiểm tra null an toàn
            ViewBag.DanhSachPhongHop = _context.PhongHopHrs.AsNoTracking().ToList();

            // Bổ sung kiểm tra null cho các navigation property trong LINQ để tránh cảnh báo
            ViewBag.ListNguoiHoTro = _context.HrNguoiHoTros
                .Include(x => x.CongViecHrs.Where(cv => cv != null && cv.Ten == "Đăng ký tiếp khách"))
                .Where(x => x.CongViecHrs != null && x.CongViecHrs.Any(cv => cv != null && cv.Ten == "Đăng ký tiếp khách"))
                .ToList();

            // Khởi tạo model với các giá trị được gán an toàn
            var model = new FormHr
            {
                IdForm = "HR_DonTiepKhac_5",
                TenForm = "Đơn đăng ký tiếp khách",
                Danhmuc = "ĐƠN TIẾP KHÁCH",
                TenCongTy = User.FindFirst("TenCongTy")?.Value ?? "",
                TenNguoiNv = User.FindFirst(System.Security.Claims.ClaimTypes.Name)?.Value ?? "",
                BoPhan = User.FindFirst("PhongBan")?.Value ?? "",
                ViTri = User.FindFirst(System.Security.Claims.ClaimTypes.Role)?.Value ?? "",
                SoNhanVien = User.FindFirst(System.Security.Claims.ClaimTypes.Email)?.Value ?? "",
                Ngay = DateOnly.FromDateTime(DateTime.Now),
                IdNguoiTao = int.Parse(userIdString),
                TenNguoiTao = User.FindFirst(System.Security.Claims.ClaimTypes.Name)?.Value ?? "",
                TimeNguoiTao = DateTime.Now,
                TrangThai = "ChoDuyet"
            };

            return View(model);
        }

        [HttpPost("/FormHR/DonTiepKhac")]
        public async Task<IActionResult> DonTiepKhac(FormHr form, [FromForm] HrDonTiepKhac5 chiTiet, int[] SelectedCongViecIds)
        {
            // 1. Kiểm tra an toàn User.Identity
            if (User?.Identity == null || !User.Identity.IsAuthenticated)
                return Redirect("/DonXetDuyet/DangNhap");

            var userIdString = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userIdString)) return Redirect("/DonXetDuyet/DangNhap");

            int userId = int.Parse(userIdString);
            var userName = User.FindFirst(System.Security.Claims.ClaimTypes.Name)?.Value ?? "Unknown";
            var phongBan = User.FindFirst("PhongBan")?.Value ?? "";
            var viTri = User.FindFirst(System.Security.Claims.ClaimTypes.Role)?.Value ?? "";
            var soNhanVien = User.FindFirst(System.Security.Claims.ClaimTypes.Email)?.Value ?? "";
            var tenCongTy = User.FindFirst("TenCongTy")?.Value ?? "";

            string networkPath = @"\\10.0.60.30\BPVN-Fileserver\Public\IT-Information Technology Dept\5.E-Form\DonHR";

            using (var transaction = await _context.Database.BeginTransactionAsync())
            {
                try
                {
                    // --- BƯỚC 1: LƯU BẢNG CHÍNH ---
                    form.Ngay = DateOnly.FromDateTime(DateTime.Now);
                    form.IdNguoiTao = userId;
                    form.TenNguoiTao = userName;
                    form.TimeNguoiTao = DateTime.Now;
                    form.TenNguoiNv = userName;
                    form.BoPhan = phongBan;
                    form.ViTri = viTri;
                    form.SoNhanVien = soNhanVien;
                    form.TenCongTy = tenCongTy;
                    form.IdForm = "HR_DonTiepKhac_5";
                    form.TenForm = "Đơn đăng ký tiếp khách";
                    form.Danhmuc = "ĐƠN TIẾP KHÁCH";
                    form.TrangThai = "ChoDuyet";

                    _context.FormHrs.Add(form);
                    await _context.SaveChangesAsync();

                    // --- BƯỚC 2: HỖ TRỢ ---
                    if (SelectedCongViecIds != null && SelectedCongViecIds.Length > 0)
                    {
                        foreach (var cvId in SelectedCongViecIds)
                        {
                            var congViec = await _context.CongViecHrs.FindAsync(cvId);
                            if (congViec != null)
                                _context.HrCtNguoiHoTros.Add(new HrCtNguoiHoTro { IdFormHr = form.Id, IdHrNguoiHoTro = congViec.IdHrNguoiHoTro, Stt = 1 });
                        }
                        await _context.SaveChangesAsync();
                    }

                    // --- BƯỚC 3: DUYỆT B2 + ỦY QUYỀN ---
                    var listDuyet = await _context.DmNguoiDuyetLoaiDonBoPhans
                        .Include(x => x.IdnguoiXacNhanNavigation)
                        .Include(x => x.DmCtChiTietUyQuyens.Where(uq => uq.TrangThai == true))
                            .ThenInclude(uq => uq.IduyQuyenNavigation)
                        .Where(x => x.IdloaiDonNavigation != null && x.IdloaiDonNavigation.MaLoaiDon == form.IdForm
                                    && x.IdboPhanNavigation != null && x.IdboPhanNavigation.TenBoPhan == form.BoPhan
                                    && x.IdcongTyNavigation != null && x.IdcongTyNavigation.TenCongTy == form.TenCongTy
                                    && x.TrangThai == true)
                        .OrderBy(x => x.Stt).ToListAsync();

                    foreach (var item in listDuyet)
                    {
                        var qlDuyet = new HrQuanLyDuyetB2 { IdFormHr = form.Id, IdnguoiXacNhan = item.IdnguoiXacNhan, ThuTuXacNhan = item.Stt, MaNguoiXacNhan = item.IdnguoiXacNhanNavigation?.MaNv, TenNguoiXacNhan = item.IdnguoiXacNhanNavigation?.HoTen, TrangThaiXacNhan = 0, Loai = item.Loai };
                        _context.HrQuanLyDuyetB2s.Add(qlDuyet);
                        await _context.SaveChangesAsync();

                        var currentTime = DateTime.Now;
                        var uqs = item.DmCtChiTietUyQuyens.Where(u => (u.ThoiGianBatDau == null || u.ThoiGianBatDau <= currentTime) && (u.ThoiGianKetThuc == null || u.ThoiGianKetThuc >= currentTime));
                        foreach (var uq in uqs.Where(x => x.IduyQuyenNavigation != null))
                        {
                            _context.HrQuanLyDuyetB2UyQuyens.Add(new HrQuanLyDuyetB2UyQuyen { IdHrQuanLyDuyetB2 = qlDuyet.Id, MaNvuyQuyen = uq.IduyQuyenNavigation!.MaNvuyQuyen, HoTenUyQuyen = uq.IduyQuyenNavigation.HoTenUyQuyen });
                        }
                    }

                    // --- BƯỚC 4: CHI TIẾT + FILES ---
                    if (chiTiet != null)
                    {
                        chiTiet.IdFormHr = form.Id;
                        chiTiet.TrangThaiPhong = "off";
                        chiTiet.NgayYeuCau = DateTime.Now;

                        if (!Directory.Exists(networkPath)) Directory.CreateDirectory(networkPath);
                        string safeName = RemoveSign4VietnameseString(userName).Replace(" ", "");
                        string timeStamp = DateTime.Now.ToString("ddMMyy_HHmm");

                        if (Request.Form.Files["UploadFile"] is { Length: > 0 } upFile)
                        {
                            string fName = $"File_TK_{form.Id}_{safeName}_{timeStamp}{Path.GetExtension(upFile.FileName)}";
                            using (var fs = new FileStream(Path.Combine(networkPath, fName), FileMode.Create)) await upFile.CopyToAsync(fs);
                            form.FileDinhKem = fName;
                        }

                        if (Request.Form.Files["AnhMinhChung"] is { Length: > 0 } anhFile)
                        {
                            string imgName = $"Anh_TK_{form.Id}_{safeName}_{timeStamp}{Path.GetExtension(anhFile.FileName) ?? ".jpg"}";
                            using (var fs = new FileStream(Path.Combine(networkPath, imgName), FileMode.Create)) await anhFile.CopyToAsync(fs);
                            chiTiet.DuongDanAnh = imgName;
                        }
                        _context.HrDonTiepKhac5s.Add(chiTiet);
                    }

                    // --- BƯỚC 5: LỊCH SỬ ---
                    _context.LichSuFormHrs.Add(new LichSuFormHr
                    {
                        IdFormHr = form.Id,
                        TieuDe = "Khởi tạo đơn tiếp khách",
                        Mota = $"Nhân viên {userName} ({soNhanVien}) đã tạo đơn.",
                        Time = DateTime.Now,
                        TrangThaiAnHien = true
                    });

                    await _context.SaveChangesAsync();
                    await transaction.CommitAsync();
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
            // 1. Kiểm tra an toàn User và Identity
            if (User?.Identity == null || !User.Identity.IsAuthenticated)
                return Redirect("/DonXetDuyet/DangNhap");

            var userIdString = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userIdString)) return Redirect("/DonXetDuyet/DangNhap");

            int userId = int.Parse(userIdString);
            var userName = User.FindFirst(System.Security.Claims.ClaimTypes.Name)?.Value ?? "";
            var phongBan = User.FindFirst("PhongBan")?.Value ?? "";
            var viTri = User.FindFirst(System.Security.Claims.ClaimTypes.Role)?.Value ?? "";
            var soNhanVien = User.FindFirst(System.Security.Claims.ClaimTypes.Email)?.Value ?? "";
            var tenCongTy = User.FindFirst("TenCongTy")?.Value ?? "";

            // --- Lấy danh sách nhân sự hỗ trợ ---
            // Thêm kiểm tra null cho các thuộc tính navigation để làm hài lòng trình biên dịch
            ViewBag.ListNguoiHoTro = _context.HrNguoiHoTros
                .Include(x => x.CongViecHrs.Where(cv => cv != null && cv.Ten == "Đăng ký Nhà thầu qua cổng"))
                .Where(x => x.CongViecHrs != null && x.CongViecHrs.Any(cv => cv != null && cv.Ten == "Đăng ký Nhà thầu qua cổng"))
                .ToList();

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
        public async Task<IActionResult> NhaThauQuaCong(FormHr form, [FromForm] HrNhaThauQuaCong6 chiTiet, int[] SelectedCongViecIds)
        {
            // 1. Kiểm tra an toàn User và Identity
            if (User?.Identity == null || !User.Identity.IsAuthenticated) return Redirect("/DonXetDuyet/DangNhap");

            var userIdString = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userIdString)) return Redirect("/DonXetDuyet/DangNhap");

            int userId = int.Parse(userIdString);
            var userName = User.FindFirst(System.Security.Claims.ClaimTypes.Name)?.Value ?? "Unknown";
            var phongBan = User.FindFirst("PhongBan")?.Value ?? "";
            var viTri = User.FindFirst(System.Security.Claims.ClaimTypes.Role)?.Value ?? "";
            var soNhanVien = User.FindFirst(System.Security.Claims.ClaimTypes.Email)?.Value ?? "";
            var tenCongTy = User.FindFirst("TenCongTy")?.Value ?? "";

            string networkPath = @"\\10.0.60.30\BPVN-Fileserver\Public\IT-Information Technology Dept\5.E-Form\DonHR";

            using (var transaction = await _context.Database.BeginTransactionAsync())
            {
                try
                {
                    // --- BƯỚC 1: LƯU BẢNG CHÍNH ---
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
                    form.IdForm = "HR_NhaThauQuaCong_6";
                    form.TenForm = "Đơn đăng ký nhà thầu qua cổng";
                    form.Danhmuc = "ĐƠN ĐĂNG KÝ NHÀ THẦU QUA CỔNG";

                    _context.FormHrs.Add(form);
                    await _context.SaveChangesAsync();

                    // --- BƯỚC 2: LƯU NHÂN SỰ HỖ TRỢ ---
                    if (SelectedCongViecIds != null && SelectedCongViecIds.Length > 0)
                    {
                        foreach (var cvId in SelectedCongViecIds)
                        {
                            var congViec = await _context.CongViecHrs.FindAsync(cvId);
                            if (congViec != null)
                            {
                                _context.HrCtNguoiHoTros.Add(new HrCtNguoiHoTro
                                {
                                    IdFormHr = form.Id,
                                    IdHrNguoiHoTro = congViec.IdHrNguoiHoTro,
                                    Stt = 1
                                });
                            }
                        }
                        await _context.SaveChangesAsync();
                    }

                    // --- BƯỚC 3: TỰ ĐỘNG THÊM NGƯỜI XÁC NHẬN ---
                    var listDuyet = await _context.DmNguoiDuyetLoaiDonBoPhans
                        .Include(x => x.IdnguoiXacNhanNavigation)
                        .Include(x => x.DmCtChiTietUyQuyens.Where(uq => uq.TrangThai == true))
                            .ThenInclude(uq => uq.IduyQuyenNavigation)
                        .Where(x => x.IdloaiDonNavigation != null && x.IdloaiDonNavigation.MaLoaiDon == form.IdForm
                                    && x.IdboPhanNavigation != null && x.IdboPhanNavigation.TenBoPhan == form.BoPhan
                                    && x.IdcongTyNavigation != null && x.IdcongTyNavigation.TenCongTy == form.TenCongTy
                                    && x.TrangThai == true)
                        .OrderBy(x => x.Stt).ToListAsync();

                    foreach (var item in listDuyet)
                    {
                        var qlDuyet = new HrQuanLyDuyetB2
                        {
                            IdFormHr = form.Id,
                            IdnguoiXacNhan = item.IdnguoiXacNhan,
                            ThuTuXacNhan = item.Stt,
                            MaNguoiXacNhan = item.IdnguoiXacNhanNavigation?.MaNv,
                            TenNguoiXacNhan = item.IdnguoiXacNhanNavigation?.HoTen,
                            TrangThaiXacNhan = 0,
                            Loai = item.Loai
                        };
                        _context.HrQuanLyDuyetB2s.Add(qlDuyet);
                        await _context.SaveChangesAsync();

                        var currentTime = DateTime.Now;
                        var listUyQuyen = item.DmCtChiTietUyQuyens
                            .Where(uq => (uq.ThoiGianBatDau == null || uq.ThoiGianBatDau <= currentTime)
                                      && (uq.ThoiGianKetThuc == null || uq.ThoiGianKetThuc >= currentTime));

                        foreach (var uq in listUyQuyen.Where(x => x.IduyQuyenNavigation != null))
                        {
                            _context.HrQuanLyDuyetB2UyQuyens.Add(new HrQuanLyDuyetB2UyQuyen
                            {
                                IdHrQuanLyDuyetB2 = qlDuyet.Id,
                                MaNvuyQuyen = uq.IduyQuyenNavigation!.MaNvuyQuyen,
                                HoTenUyQuyen = uq.IduyQuyenNavigation.HoTenUyQuyen
                            });
                        }
                    }
                    await _context.SaveChangesAsync();

                    // --- BƯỚC 4: LƯU CHI TIẾT ---
                    if (chiTiet != null)
                    {
                        chiTiet.IdFormHr = form.Id;
                        if (!Directory.Exists(networkPath)) Directory.CreateDirectory(networkPath);
                        string safeName = RemoveSign4VietnameseString(userName).Replace(" ", "");
                        string timeStamp = DateTime.Now.ToString("ddMMyy_HHmm");

                        if (Request.Form.Files["UploadFile"] is { Length: > 0 } upFile)
                        {
                            string fileName = $"File_NT_{form.Id}_{safeName}_{timeStamp}{Path.GetExtension(upFile.FileName)}";
                            using (var fs = new FileStream(Path.Combine(networkPath, fileName), FileMode.Create)) await upFile.CopyToAsync(fs);
                            form.FileDinhKem = fileName;
                        }

                        if (Request.Form.Files["Anh"] is { Length: > 0 } anhFile)
                        {
                            string imgName = $"Anh_NT_{form.Id}_{safeName}_{timeStamp}{Path.GetExtension(anhFile.FileName) ?? ".jpg"}";
                            using (var fs = new FileStream(Path.Combine(networkPath, imgName), FileMode.Create)) await anhFile.CopyToAsync(fs);
                            chiTiet.DuongDanAnh = imgName;
                        }

                        chiTiet.Anh = null;
                        _context.HrNhaThauQuaCong6s.Add(chiTiet);
                        await _context.SaveChangesAsync();
                    }

                    // --- BƯỚC 5: LỊCH SỬ THAO TÁC ---
                    _context.LichSuFormHrs.Add(new LichSuFormHr
                    {
                        IdFormHr = form.Id,
                        TieuDe = "Khởi tạo đơn nhà thầu",
                        Mota = $"Nhân viên {userName} đã tạo đơn đăng ký nhà thầu qua cổng.",
                        Time = DateTime.Now,
                        IsRead = false,
                        TrangThaiAnHien = true
                    });
                    await _context.SaveChangesAsync();

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
        /// Hiển thị form đăng ký hỗ trợ tiền điện thoại
        /// </summary>
        [HttpGet("/FormHR/HoTroTienDienThoai")]
        public IActionResult HoTroTienDienThoai()
        {
            // Kiểm tra an toàn cho User và Identity
            if (User?.Identity == null || !User.Identity.IsAuthenticated)
                return Redirect("/DonXetDuyet/DangNhap");

            var userIdString = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userIdString)) return Redirect("/DonXetDuyet/DangNhap");

            int userId = int.Parse(userIdString);
            var userName = User.FindFirst(System.Security.Claims.ClaimTypes.Name)?.Value ?? "";
            var phongBan = User.FindFirst("PhongBan")?.Value ?? "";
            var viTri = User.FindFirst(System.Security.Claims.ClaimTypes.Role)?.Value ?? "";
            var soNhanVien = User.FindFirst(System.Security.Claims.ClaimTypes.Email)?.Value ?? "";
            var tenCongTy = User.FindFirst("TenCongTy")?.Value ?? "";

            // Lấy danh sách nhân sự hỗ trợ với kiểm tra an toàn LINQ
            ViewBag.ListNguoiHoTro = _context.HrNguoiHoTros
                .Include(x => x.CongViecHrs.Where(cv => cv != null && cv.Ten == "Đăng ký hỗ trợ tiền điện thoại"))
                .Where(x => x.CongViecHrs != null && x.CongViecHrs.Any(cv => cv != null && cv.Ten == "Đăng ký hỗ trợ tiền điện thoại"))
                .ToList();

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
        /// Xử lý gửi đơn hỗ trợ tiền điện thoại
        /// </summary>
        [HttpPost("/FormHR/HoTroTienDienThoai")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> HoTroTienDienThoai(FormHr form, [FromForm] HrHoTroTienDienThoai7 chiTiet, int[] SelectedCongViecIds)
        {
            // Kiểm tra an toàn User và Identity
            if (User?.Identity == null || !User.Identity.IsAuthenticated) return Redirect("/DonXetDuyet/DangNhap");

            var userIdString = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userIdString)) return Redirect("/DonXetDuyet/DangNhap");

            int userId = int.Parse(userIdString);
            var userName = User.FindFirst(System.Security.Claims.ClaimTypes.Name)?.Value ?? "Unknown";
            var phongBan = User.FindFirst("PhongBan")?.Value ?? "";
            var viTri = User.FindFirst(System.Security.Claims.ClaimTypes.Role)?.Value ?? "";
            var soNhanVien = User.FindFirst(System.Security.Claims.ClaimTypes.Email)?.Value ?? "";
            var tenCongTy = User.FindFirst("TenCongTy")?.Value ?? "";

            string networkPath = @"\\10.0.60.30\BPVN-Fileserver\Public\IT-Information Technology Dept\5.E-Form\DonHR";

            using (var transaction = await _context.Database.BeginTransactionAsync())
            {
                try
                {
                    // --- BƯỚC 1: LƯU BẢNG CHÍNH ---
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

                    // --- BƯỚC 2: HỖ TRỢ ---
                    if (SelectedCongViecIds != null && SelectedCongViecIds.Length > 0)
                    {
                        foreach (var cvId in SelectedCongViecIds)
                        {
                            var congViec = await _context.CongViecHrs.FindAsync(cvId);
                            if (congViec != null)
                                _context.HrCtNguoiHoTros.Add(new HrCtNguoiHoTro { IdFormHr = form.Id, IdHrNguoiHoTro = congViec.IdHrNguoiHoTro, Stt = 1 });
                        }
                        await _context.SaveChangesAsync();
                    }

                    // --- BƯỚC 3: DUYỆT B2 + ỦY QUYỀN ---
                    var listDuyet = await _context.DmNguoiDuyetLoaiDonBoPhans
                        .Include(x => x.IdnguoiXacNhanNavigation)
                        .Include(x => x.DmCtChiTietUyQuyens.Where(uq => uq.TrangThai == true))
                            .ThenInclude(uq => uq.IduyQuyenNavigation)
                        .Where(x => x.IdloaiDonNavigation != null && x.IdloaiDonNavigation.MaLoaiDon == form.IdForm
                                    && x.IdboPhanNavigation != null && x.IdboPhanNavigation.TenBoPhan == form.BoPhan
                                    && x.IdcongTyNavigation != null && x.IdcongTyNavigation.TenCongTy == form.TenCongTy
                                    && x.TrangThai == true)
                        .OrderBy(x => x.Stt).ToListAsync();

                    foreach (var item in listDuyet)
                    {
                        var qlDuyet = new HrQuanLyDuyetB2
                        {
                            IdFormHr = form.Id,
                            IdnguoiXacNhan = item.IdnguoiXacNhan,
                            ThuTuXacNhan = item.Stt,
                            MaNguoiXacNhan = item.IdnguoiXacNhanNavigation?.MaNv,
                            TenNguoiXacNhan = item.IdnguoiXacNhanNavigation?.HoTen,
                            TrangThaiXacNhan = 0,
                            Loai = item.Loai
                        };
                        _context.HrQuanLyDuyetB2s.Add(qlDuyet);
                        await _context.SaveChangesAsync();

                        var currentTime = DateTime.Now;
                        var uqs = item.DmCtChiTietUyQuyens.Where(u => (u.ThoiGianBatDau == null || u.ThoiGianBatDau <= currentTime) && (u.ThoiGianKetThuc == null || u.ThoiGianKetThuc >= currentTime));
                        foreach (var uq in uqs.Where(x => x.IduyQuyenNavigation != null))
                        {
                            _context.HrQuanLyDuyetB2UyQuyens.Add(new HrQuanLyDuyetB2UyQuyen
                            {
                                IdHrQuanLyDuyetB2 = qlDuyet.Id,
                                MaNvuyQuyen = uq.IduyQuyenNavigation!.MaNvuyQuyen,
                                HoTenUyQuyen = uq.IduyQuyenNavigation.HoTenUyQuyen
                            });
                        }
                    }
                    await _context.SaveChangesAsync();

                    // --- BƯỚC 4: LƯU CHI TIẾT + FILES ---
                    if (chiTiet != null)
                    {
                        chiTiet.IdFormHr = form.Id;
                        if (!Directory.Exists(networkPath)) Directory.CreateDirectory(networkPath);
                        string safeName = RemoveSign4VietnameseString(userName).Replace(" ", "");
                        string ts = DateTime.Now.ToString("ddMMyy_HHmm");

                        if (Request.Form.Files["UploadFile"] is { Length: > 0 } upFile)
                        {
                            string fName = $"File_TDT_{form.Id}_{safeName}_{ts}{Path.GetExtension(upFile.FileName)}";
                            using (var fs = new FileStream(Path.Combine(networkPath, fName), FileMode.Create)) await upFile.CopyToAsync(fs);
                            form.FileDinhKem = fName;
                        }

                        if (Request.Form.Files["AnhMinhChung"] is { Length: > 0 } anhFile)
                        {
                            string imgName = $"Anh_TDT_{form.Id}_{safeName}_{ts}{Path.GetExtension(anhFile.FileName) ?? ".jpg"}";
                            using (var fs = new FileStream(Path.Combine(networkPath, imgName), FileMode.Create)) await anhFile.CopyToAsync(fs);
                            chiTiet.DuongDanAnh = imgName;
                        }
                        chiTiet.Anh = null;
                        _context.HrHoTroTienDienThoai7s.Add(chiTiet);
                        await _context.SaveChangesAsync();
                    }

                    // --- BƯỚC 5: LỊCH SỬ ---
                    _context.LichSuFormHrs.Add(new LichSuFormHr
                    {
                        IdFormHr = form.Id,
                        TieuDe = "Khởi tạo đơn hỗ trợ tiền điện thoại",
                        Mota = $"Nhân viên {userName} ({soNhanVien}) đã tạo đơn.",
                        Time = DateTime.Now,
                        TrangThaiAnHien = true
                    });

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
        /// Hiển thị form đăng ký đổi ca làm việc
        /// </summary>
        [HttpGet("/FormHR/DoiCaLam")]
        public IActionResult DoiCaLam()
        {
            // Kiểm tra an toàn cho User và Identity
            if (User?.Identity == null || !User.Identity.IsAuthenticated)
                return Redirect("/DonXetDuyet/DangNhap");

            var userIdString = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userIdString)) return Redirect("/DonXetDuyet/DangNhap");

            int userId = int.Parse(userIdString);
            var userName = User.FindFirst(ClaimTypes.Name)?.Value ?? "";
            var phongBan = User.FindFirst("PhongBan")?.Value ?? "";
            var viTri = User.FindFirst(ClaimTypes.Role)?.Value ?? "";
            var soNhanVien = User.FindFirst(ClaimTypes.Email)?.Value ?? "";
            var tenCongTy = User.FindFirst("TenCongTy")?.Value ?? "";

            // --- Lấy danh sách nhân sự hỗ trợ ---
            // Bổ sung kiểm tra null cho các navigation property để làm hài lòng trình biên dịch
            ViewBag.ListNguoiHoTro = _context.HrNguoiHoTros
                .Include(x => x.CongViecHrs.Where(cv => cv != null && cv.Ten == "Đăng ký đổi ca làm việc"))
                .Where(x => x.CongViecHrs != null && x.CongViecHrs.Any(cv => cv != null && cv.Ten == "Đăng ký đổi ca làm việc"))
                .ToList();

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
        /// Xử lý gửi đơn đổi ca làm việc
        /// </summary>
        [HttpPost("/FormHR/DoiCaLam")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DoiCaLam(FormHr form, [FromForm] HrDoiCaLam8 chiTiet, int[] SelectedCongViecIds)
        {
            // Kiểm tra an toàn User và Identity
            if (User?.Identity == null || !User.Identity.IsAuthenticated) return Redirect("/DonXetDuyet/DangNhap");

            var userIdString = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            var userName = User.FindFirst(ClaimTypes.Name)?.Value ?? "Unknown";
            var phongBan = User.FindFirst("PhongBan")?.Value ?? "";
            var soNhanVien = User.FindFirst(ClaimTypes.Email)?.Value ?? "";
            var tenCongTy = User.FindFirst("TenCongTy")?.Value ?? "";

            if (string.IsNullOrEmpty(userIdString)) return Redirect("/DonXetDuyet/DangNhap");
            int userId = int.Parse(userIdString);

            string networkPath = @"\\10.0.60.30\BPVN-Fileserver\Public\IT-Information Technology Dept\5.E-Form\DonHR";

            using (var transaction = await _context.Database.BeginTransactionAsync())
            {
                try
                {
                    // --- BƯỚC 1: LƯU BẢNG CHÍNH ---
                    form.Ngay = DateOnly.FromDateTime(DateTime.Now);
                    form.IdNguoiTao = userId;
                    form.TenNguoiTao = userName;
                    form.TimeNguoiTao = DateTime.Now;
                    form.TenNguoiNv = userName;
                    form.BoPhan = phongBan;
                    form.SoNhanVien = soNhanVien;
                    form.TenCongTy = tenCongTy;
                    form.Danhmuc = "ĐƠN ĐỔI CA LÀM VIỆC";
                    form.TrangThai = "ChoDuyet";
                    form.IdForm = "HR_DoiCaLam_8";
                    form.TenForm = "Đơn đăng ký đổi ca làm việc";

                    _context.FormHrs.Add(form);
                    await _context.SaveChangesAsync();

                    // --- BƯỚC 2: LƯU NHÂN SỰ HỖ TRỢ ---
                    if (SelectedCongViecIds != null && SelectedCongViecIds.Length > 0)
                    {
                        foreach (var cvId in SelectedCongViecIds)
                        {
                            var congViec = await _context.CongViecHrs.FindAsync(cvId);
                            if (congViec != null)
                                _context.HrCtNguoiHoTros.Add(new HrCtNguoiHoTro { IdFormHr = form.Id, IdHrNguoiHoTro = congViec.IdHrNguoiHoTro, Stt = 1 });
                        }
                        await _context.SaveChangesAsync();
                    }

                    // --- BƯỚC 3: TỰ ĐỘNG THÊM NGƯỜI XÁC NHẬN ---
                    var listDuyet = await _context.DmNguoiDuyetLoaiDonBoPhans
                        .Include(x => x.IdnguoiXacNhanNavigation)
                        .Include(x => x.DmCtChiTietUyQuyens.Where(uq => uq.TrangThai == true))
                            .ThenInclude(uq => uq.IduyQuyenNavigation)
                        .Where(x => x.IdloaiDonNavigation != null && x.IdloaiDonNavigation.MaLoaiDon == form.IdForm
                                    && x.IdboPhanNavigation != null && x.IdboPhanNavigation.TenBoPhan == form.BoPhan
                                    && x.IdcongTyNavigation != null && x.IdcongTyNavigation.TenCongTy == form.TenCongTy
                                    && x.TrangThai == true)
                        .OrderBy(x => x.Stt).ToListAsync();

                    foreach (var item in listDuyet)
                    {
                        var qlDuyet = new HrQuanLyDuyetB2
                        {
                            IdFormHr = form.Id,
                            IdnguoiXacNhan = item.IdnguoiXacNhan,
                            ThuTuXacNhan = item.Stt,
                            MaNguoiXacNhan = item.IdnguoiXacNhanNavigation?.MaNv,
                            TenNguoiXacNhan = item.IdnguoiXacNhanNavigation?.HoTen,
                            TrangThaiXacNhan = 0,
                            Loai = item.Loai
                        };
                        _context.HrQuanLyDuyetB2s.Add(qlDuyet);
                        await _context.SaveChangesAsync();

                        var currentTime = DateTime.Now;
                        var uqs = item.DmCtChiTietUyQuyens.Where(u => (u.ThoiGianBatDau == null || u.ThoiGianBatDau <= currentTime)
                                                                  && (u.ThoiGianKetThuc == null || u.ThoiGianKetThuc >= currentTime));
                        foreach (var uq in uqs.Where(x => x.IduyQuyenNavigation != null))
                        {
                            _context.HrQuanLyDuyetB2UyQuyens.Add(new HrQuanLyDuyetB2UyQuyen
                            {
                                IdHrQuanLyDuyetB2 = qlDuyet.Id,
                                MaNvuyQuyen = uq.IduyQuyenNavigation!.MaNvuyQuyen,
                                HoTenUyQuyen = uq.IduyQuyenNavigation.HoTenUyQuyen
                            });
                        }
                    }
                    await _context.SaveChangesAsync();

                    // --- BƯỚC 4: CHI TIẾT + FILES ---
                    if (chiTiet != null)
                    {
                        chiTiet.IdFormHr = form.Id;
                        if (!Directory.Exists(networkPath)) Directory.CreateDirectory(networkPath);
                        string safeName = RemoveSign4VietnameseString(userName).Replace(" ", "");
                        string ts = DateTime.Now.ToString("ddMMyy_HHmm");

                        if (Request.Form.Files["UploadFile"] is { Length: > 0 } upFile)
                        {
                            string fName = $"File_DoiCa_{form.Id}_{safeName}_{ts}{Path.GetExtension(upFile.FileName)}";
                            using (var fs = new FileStream(Path.Combine(networkPath, fName), FileMode.Create)) await upFile.CopyToAsync(fs);
                            form.FileDinhKem = fName;
                        }

                        if (Request.Form.Files["Anh"] is { Length: > 0 } anhFile)
                        {
                            string aName = $"Anh_DoiCa_{form.Id}_{safeName}_{ts}{Path.GetExtension(anhFile.FileName) ?? ".jpg"}";
                            using (var fs = new FileStream(Path.Combine(networkPath, aName), FileMode.Create)) await anhFile.CopyToAsync(fs);
                            chiTiet.DuongDanAnh = aName;
                        }
                        chiTiet.Anh = null;
                        _context.HrDoiCaLam8s.Add(chiTiet);
                        await _context.SaveChangesAsync();
                    }

                    // --- BƯỚC 5: LỊCH SỬ ---
                    _context.LichSuFormHrs.Add(new LichSuFormHr
                    {
                        IdFormHr = form.Id,
                        TieuDe = "Khởi tạo đơn đổi ca",
                        Mota = $"Nhân viên {userName} đã tạo đơn đăng ký đổi ca.",
                        Time = DateTime.Now,
                        IsRead = false,
                        TrangThaiAnHien = true
                    });
                    await _context.SaveChangesAsync();

                    _context.Entry(form).State = EntityState.Modified;
                    await _context.SaveChangesAsync();
                    await transaction.CommitAsync();

                    TempData["Success"] = "Gửi đơn đổi ca thành công!";
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

        #region ĐƠN HoTroTienDienThoai (HR_DonHoTroCongTac_9)

        [HttpGet("/FormHR/DonHoTroCongTac")]
        public IActionResult DonHoTroCongTac()
        {
            // Kiểm tra an toàn cho User và Identity
            if (User?.Identity == null || !User.Identity.IsAuthenticated)
                return Redirect("/DonXetDuyet/DangNhap");

            var userIdString = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userIdString)) return Redirect("/DonXetDuyet/DangNhap");

            int userId = int.Parse(userIdString);
            var userName = User.FindFirst(ClaimTypes.Name)?.Value ?? "";
            var phongBan = User.FindFirst("PhongBan")?.Value ?? "";
            var viTri = User.FindFirst(ClaimTypes.Role)?.Value ?? "";
            var soNhanVien = User.FindFirst(ClaimTypes.Email)?.Value ?? "";
            var tenCongTy = User.FindFirst("TenCongTy")?.Value ?? "";

            // --- MỚI: Lấy danh sách nhân sự hỗ trợ hiển thị ra View ---
            // Thêm kiểm tra null cho các thuộc tính navigation để tránh cảnh báo trình biên dịch
            ViewBag.ListNguoiHoTro = _context.HrNguoiHoTros
                .Include(x => x.CongViecHrs.Where(cv => cv != null && cv.Ten == "Đăng ký hỗ trợ công tác"))
                .Where(x => x.CongViecHrs != null && x.CongViecHrs.Any(cv => cv != null && cv.Ten == "Đăng ký hỗ trợ công tác"))
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
        public async Task<IActionResult> DonHoTroCongTac(FormHr form, [FromForm] HrDonHoTroCongTac9 chiTiet, int[] SelectedCongViecIds)
        {
            // Kiểm tra an toàn User.Identity
            if (User?.Identity == null || !User.Identity.IsAuthenticated) return Redirect("/DonXetDuyet/DangNhap");

            var userIdString = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userIdString)) return Redirect("/DonXetDuyet/DangNhap");

            int userId = int.Parse(userIdString);
            var userName = User.FindFirst(System.Security.Claims.ClaimTypes.Name)?.Value ?? "Unknown";
            var phongBan = User.FindFirst("PhongBan")?.Value ?? "";
            var tenCongTy = User.FindFirst("TenCongTy")?.Value ?? "";

            string networkPath = @"\\10.0.60.30\BPVN-Fileserver\Public\IT-Information Technology Dept\5.E-Form\DonHR";

            using (var transaction = await _context.Database.BeginTransactionAsync())
            {
                try
                {
                    // --- BƯỚC 1: LƯU BẢNG CHÍNH ---
                    form.Ngay = DateOnly.FromDateTime(DateTime.Now);
                    form.IdNguoiTao = userId;
                    form.TenNguoiTao = userName;
                    form.TimeNguoiTao = DateTime.Now;
                    form.TenNguoiNv = userName;
                    form.BoPhan = phongBan;
                    form.TenCongTy = tenCongTy;
                    form.IdForm = "HR_DonHoTroCongTac_9";
                    form.TenForm = "Đơn hỗ trợ công tác";
                    form.TrangThai = "ChoDuyet";

                    _context.FormHrs.Add(form);
                    await _context.SaveChangesAsync();

                    // --- BƯỚC 2: NHÂN SỰ HỖ TRỢ ---
                    if (SelectedCongViecIds != null && SelectedCongViecIds.Length > 0)
                    {
                        foreach (var cvId in SelectedCongViecIds)
                        {
                            var cv = await _context.CongViecHrs.FindAsync(cvId);
                            if (cv != null)
                                _context.HrCtNguoiHoTros.Add(new HrCtNguoiHoTro { IdFormHr = form.Id, IdHrNguoiHoTro = cv.IdHrNguoiHoTro, Stt = 1 });
                        }
                        await _context.SaveChangesAsync();
                    }

                    // --- BƯỚC 3: DUYỆT B2 + ỦY QUYỀN ---
                    var listDuyet = await _context.DmNguoiDuyetLoaiDonBoPhans
                        .Include(x => x.IdnguoiXacNhanNavigation)
                        .Include(x => x.DmCtChiTietUyQuyens.Where(uq => uq.TrangThai == true))
                            .ThenInclude(uq => uq.IduyQuyenNavigation)
                        .Where(x => x.IdloaiDonNavigation != null && x.IdloaiDonNavigation.MaLoaiDon == form.IdForm
                                    && x.IdboPhanNavigation != null && x.IdboPhanNavigation.TenBoPhan == form.BoPhan
                                    && x.TrangThai == true)
                        .OrderBy(x => x.Stt).ToListAsync();

                    foreach (var item in listDuyet)
                    {
                        var qlDuyet = new HrQuanLyDuyetB2
                        {
                            IdFormHr = form.Id,
                            IdnguoiXacNhan = item.IdnguoiXacNhan,
                            ThuTuXacNhan = item.Stt,
                            MaNguoiXacNhan = item.IdnguoiXacNhanNavigation?.MaNv,
                            TenNguoiXacNhan = item.IdnguoiXacNhanNavigation?.HoTen,
                            TrangThaiXacNhan = 0,
                            Loai = item.Loai
                        };
                        _context.HrQuanLyDuyetB2s.Add(qlDuyet);
                        await _context.SaveChangesAsync();

                        var currentTime = DateTime.Now;
                        var uqs = item.DmCtChiTietUyQuyens.Where(u => (u.ThoiGianBatDau == null || u.ThoiGianBatDau <= currentTime)
                                                                  && (u.ThoiGianKetThuc == null || u.ThoiGianKetThuc >= currentTime));
                        foreach (var uq in uqs.Where(x => x.IduyQuyenNavigation != null))
                        {
                            _context.HrQuanLyDuyetB2UyQuyens.Add(new HrQuanLyDuyetB2UyQuyen
                            {
                                IdHrQuanLyDuyetB2 = qlDuyet.Id,
                                MaNvuyQuyen = uq.IduyQuyenNavigation!.MaNvuyQuyen,
                                HoTenUyQuyen = uq.IduyQuyenNavigation.HoTenUyQuyen
                            });
                        }
                    }

                    // --- BƯỚC 4: CHI TIẾT + FILES ---
                    if (chiTiet != null)
                    {
                        chiTiet.IdFormHr = form.Id;
                        if (!Directory.Exists(networkPath)) Directory.CreateDirectory(networkPath);
                        string ts = DateTime.Now.ToString("ddMMyy_HHmm");
                        string safeName = RemoveSign4VietnameseString(userName).Replace(" ", "");

                        if (Request.Form.Files["UploadFiles"] is { Length: > 0 } upFile)
                        {
                            string fName = $"File_HTCT_{form.Id}_{safeName}_{ts}{Path.GetExtension(upFile.FileName)}";
                            using (var fs = new FileStream(Path.Combine(networkPath, fName), FileMode.Create)) await upFile.CopyToAsync(fs);
                            form.FileDinhKem = fName;
                        }

                        if (Request.Form.Files["AnhMinhChung"] is { Length: > 0 } anh)
                        {
                            string aName = $"Anh_HTCT_{form.Id}_{safeName}_{ts}{Path.GetExtension(anh.FileName) ?? ".jpg"}";
                            using (var fs = new FileStream(Path.Combine(networkPath, aName), FileMode.Create)) await anh.CopyToAsync(fs);
                            chiTiet.DuongDanAnh = aName;
                        }
                        _context.HrDonHoTroCongTac9s.Add(chiTiet);
                    }

                    // --- BƯỚC 5: LỊCH SỬ ---
                    _context.LichSuFormHrs.Add(new LichSuFormHr
                    {
                        IdFormHr = form.Id,
                        TieuDe = "Khởi tạo đơn hỗ trợ công tác",
                        Mota = $"Nhân viên {userName} đã tạo đơn.",
                        Time = DateTime.Now,
                        TrangThaiAnHien = true
                    });

                    await _context.SaveChangesAsync();
                    await transaction.CommitAsync();
                    TempData["Success"] = "Gửi đơn thành công!";
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

        #region Đơn Đăng Ký Ký Túc Xá (HR_DonKiTucXa_10)

        [HttpGet("/FormHR/DonKiTucXa")]
        public IActionResult DonKiTucXa()
        {
            // Kiểm tra an toàn cho User và Identity
            if (User?.Identity == null || !User.Identity.IsAuthenticated)
                return Redirect("/DonXetDuyet/DangNhap");

            var userIdString = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userIdString)) return Redirect("/DonXetDuyet/DangNhap");

            int userId = int.Parse(userIdString);
            var userName = User.FindFirst(ClaimTypes.Name)?.Value ?? "";
            var phongBan = User.FindFirst("PhongBan")?.Value ?? "";
            var viTri = User.FindFirst(ClaimTypes.Role)?.Value ?? "";
            var soNhanVien = User.FindFirst(ClaimTypes.Email)?.Value ?? "";
            var tenCongTy = User.FindFirst("TenCongTy")?.Value ?? "";

            // Lấy danh sách nhân sự hỗ trợ
            // Bổ sung kiểm tra null cho các thuộc tính navigation để tránh cảnh báo trình biên dịch
            ViewBag.ListNguoiHoTro = _context.HrNguoiHoTros
                .Include(x => x.CongViecHrs.Where(cv => cv != null && cv.Ten == "Đăng ký ký túc xá"))
                .Where(x => x.CongViecHrs != null && x.CongViecHrs.Any(cv => cv != null && cv.Ten == "Đăng ký ký túc xá"))
                .ToList();

            var model = new FormHr
            {
                TenNguoiNv = userName,
                BoPhan = phongBan,
                ViTri = viTri,
                SoNhanVien = soNhanVien,
                TenCongTy = tenCongTy,
                Danhmuc = "ĐƠN KÝ TÚC XÁ",
                Ngay = DateOnly.FromDateTime(DateTime.Now),
                IdNguoiTao = userId,
                TenNguoiTao = userName,
                TimeNguoiTao = DateTime.Now,
                TrangThai = "ChoDuyet"
            };

            return View(model);
        }

        [HttpPost("/FormHR/DonKiTucXa")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DonKiTucXa(FormHr form, [FromForm] HrDonKiTucXa10 chiTiet, int[] SelectedCongViecIds)
        {
            // 1. Kiểm tra an toàn User và Identity
            if (User?.Identity == null || !User.Identity.IsAuthenticated) return Redirect("/DonXetDuyet/DangNhap");

            var userIdString = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userIdString)) return Redirect("/DonXetDuyet/DangNhap");

            int userId = int.Parse(userIdString);
            var userName = User.FindFirst(ClaimTypes.Name)?.Value ?? "Unknown";
            var phongBan = User.FindFirst("PhongBan")?.Value ?? "";
            var viTri = User.FindFirst(ClaimTypes.Role)?.Value ?? "";
            var soNhanVien = User.FindFirst(ClaimTypes.Email)?.Value ?? "";
            var tenCongTy = User.FindFirst("TenCongTy")?.Value ?? "";

            string networkPath = @"\\10.0.60.30\BPVN-Fileserver\Public\IT-Information Technology Dept\5.E-Form\DonHR";

            using (var transaction = await _context.Database.BeginTransactionAsync())
            {
                try
                {
                    // --- BƯỚC 1: LƯU BẢNG CHÍNH ---
                    form.Ngay = DateOnly.FromDateTime(DateTime.Now);
                    form.IdNguoiTao = userId;
                    form.TenNguoiTao = userName;
                    form.TimeNguoiTao = DateTime.Now;
                    form.TenNguoiNv = userName;
                    form.BoPhan = phongBan;
                    form.ViTri = viTri;
                    form.SoNhanVien = soNhanVien;
                    form.TenCongTy = tenCongTy;
                    form.Danhmuc = "ĐƠN KÝ TÚC XÁ";
                    form.TrangThai = "ChoDuyet";
                    form.IdForm = "HR_DonKiTucXa_10";
                    form.TenForm = "Đơn đăng ký ký túc xá";

                    _context.FormHrs.Add(form);
                    await _context.SaveChangesAsync();

                    // --- BƯỚC 2: LƯU NHÂN SỰ HỖ TRỢ ---
                    if (SelectedCongViecIds != null && SelectedCongViecIds.Length > 0)
                    {
                        foreach (var cvId in SelectedCongViecIds)
                        {
                            var congViec = await _context.CongViecHrs.FindAsync(cvId);
                            if (congViec != null)
                            {
                                _context.HrCtNguoiHoTros.Add(new HrCtNguoiHoTro
                                {
                                    IdFormHr = form.Id,
                                    IdHrNguoiHoTro = congViec.IdHrNguoiHoTro,
                                    Stt = 1
                                });
                            }
                        }
                        await _context.SaveChangesAsync();
                    }

                    // --- BƯỚC 3: TỰ ĐỘNG THÊM NGƯỜI XÁC NHẬN ---
                    var listDuyet = await _context.DmNguoiDuyetLoaiDonBoPhans
                        .Include(x => x.IdnguoiXacNhanNavigation)
                        .Include(x => x.DmCtChiTietUyQuyens.Where(uq => uq.TrangThai == true))
                            .ThenInclude(uq => uq.IduyQuyenNavigation)
                        .Where(x => x.IdloaiDonNavigation != null && x.IdloaiDonNavigation.MaLoaiDon == form.IdForm
                                    && x.IdboPhanNavigation != null && x.IdboPhanNavigation.TenBoPhan == form.BoPhan
                                    && x.IdcongTyNavigation != null && x.IdcongTyNavigation.TenCongTy == form.TenCongTy
                                    && x.TrangThai == true)
                        .OrderBy(x => x.Stt).ToListAsync();

                    foreach (var item in listDuyet)
                    {
                        var quanLyDuyet = new HrQuanLyDuyetB2
                        {
                            IdFormHr = form.Id,
                            IdnguoiXacNhan = item.IdnguoiXacNhan,
                            ThuTuXacNhan = item.Stt,
                            MaNguoiXacNhan = item.IdnguoiXacNhanNavigation?.MaNv,
                            TenNguoiXacNhan = item.IdnguoiXacNhanNavigation?.HoTen,
                            TrangThaiXacNhan = 0,
                            Loai = item.Loai
                        };
                        _context.HrQuanLyDuyetB2s.Add(quanLyDuyet);
                        await _context.SaveChangesAsync();

                        var currentTime = DateTime.Now;
                        var uqs = item.DmCtChiTietUyQuyens.Where(u => (u.ThoiGianBatDau == null || u.ThoiGianBatDau <= currentTime)
                                                                  && (u.ThoiGianKetThuc == null || u.ThoiGianKetThuc >= currentTime));
                        foreach (var uq in uqs.Where(x => x.IduyQuyenNavigation != null))
                        {
                            _context.HrQuanLyDuyetB2UyQuyens.Add(new HrQuanLyDuyetB2UyQuyen
                            {
                                IdHrQuanLyDuyetB2 = quanLyDuyet.Id,
                                MaNvuyQuyen = uq.IduyQuyenNavigation!.MaNvuyQuyen,
                                HoTenUyQuyen = uq.IduyQuyenNavigation.HoTenUyQuyen
                            });
                        }
                    }
                    await _context.SaveChangesAsync();

                    // --- BƯỚC 4: CHI TIẾT + FILES ---
                    if (chiTiet != null)
                    {
                        chiTiet.IdFormHr = form.Id;
                        if (!Directory.Exists(networkPath)) Directory.CreateDirectory(networkPath);
                        string safeName = RemoveSign4VietnameseString(userName).Replace(" ", "");
                        string ts = DateTime.Now.ToString("ddMMyy_HHmm");

                        if (Request.Form.Files["UploadFile"] is { Length: > 0 } upFile)
                        {
                            string fName = $"File_KTX_{form.Id}_{safeName}_{ts}{Path.GetExtension(upFile.FileName)}";
                            using (var fs = new FileStream(Path.Combine(networkPath, fName), FileMode.Create)) await upFile.CopyToAsync(fs);
                            form.FileDinhKem = fName;
                        }

                        if (Request.Form.Files["AnhMinhChung"] is { Length: > 0 } anhFile)
                        {
                            string imgName = $"Anh_KTX_{form.Id}_{safeName}_{ts}{Path.GetExtension(anhFile.FileName) ?? ".jpg"}";
                            using (var fs = new FileStream(Path.Combine(networkPath, imgName), FileMode.Create)) await anhFile.CopyToAsync(fs);
                            chiTiet.DuongDanAnh = imgName;
                        }
                        _context.HrDonKiTucXa10s.Add(chiTiet);
                        await _context.SaveChangesAsync();
                    }

                    // --- BƯỚC 5: LỊCH SỬ ---
                    _context.LichSuFormHrs.Add(new LichSuFormHr
                    {
                        IdFormHr = form.Id,
                        TieuDe = "Khởi tạo đơn đăng ký ký túc xá",
                        Mota = $"Nhân viên {userName} ({soNhanVien}) đã tạo đơn.",
                        Time = DateTime.Now,
                        IsRead = false,
                        TrangThaiAnHien = true
                    });
                    await _context.SaveChangesAsync();

                    _context.Entry(form).State = EntityState.Modified;
                    await _context.SaveChangesAsync();
                    await transaction.CommitAsync();

                    TempData["Success"] = "Gửi đơn đăng ký ký túc xá thành công!";
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

        #region Đơn Làm Lại Thẻ (HR_DonLamLaiThe_11)

        [HttpGet("/FormHR/DonLamLaiThe")]
        public IActionResult DonLamLaiThe()
        {
            // 1. Kiểm tra an toàn cho User và Identity
            if (User?.Identity == null || !User.Identity.IsAuthenticated)
                return Redirect("/DonXetDuyet/DangNhap");

            var userIdString = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userIdString)) return Redirect("/DonXetDuyet/DangNhap");

            int userId = int.Parse(userIdString);
            var userName = User.FindFirst(ClaimTypes.Name)?.Value ?? "";
            var phongBan = User.FindFirst("PhongBan")?.Value ?? "";
            var viTri = User.FindFirst(ClaimTypes.Role)?.Value ?? "";
            var soNhanVien = User.FindFirst(ClaimTypes.Email)?.Value ?? "";
            var tenCongTy = User.FindFirst("TenCongTy")?.Value ?? "";

            // --- Lấy danh sách nhân sự hỗ trợ ---
            // Bổ sung kiểm tra null cho các thuộc tính navigation để tránh cảnh báo trình biên dịch
            ViewBag.ListNguoiHoTro = _context.HrNguoiHoTros
                .Include(x => x.CongViecHrs.Where(cv => cv != null && cv.Ten == "Đăng ký làm lại thẻ"))
                .Where(x => x.CongViecHrs != null && x.CongViecHrs.Any(cv => cv != null && cv.Ten == "Đăng ký làm lại thẻ"))
                .ToList();

            var model = new FormHr
            {
                TenNguoiNv = userName,
                BoPhan = phongBan,
                ViTri = viTri,
                SoNhanVien = soNhanVien,
                TenCongTy = tenCongTy,
                Danhmuc = "ĐƠN LÀM LẠI THẺ",
                Ngay = DateOnly.FromDateTime(DateTime.Now),
                IdNguoiTao = userId,
                TenNguoiTao = userName,
                TimeNguoiTao = DateTime.Now,
                TrangThai = "ChoDuyet"
            };

            return View(model);
        }

        [HttpPost("/FormHR/DonLamLaiThe")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DonLamLaiThe(FormHr form, [FromForm] HrDonLamLaiThe11 chiTiet, int[] SelectedCongViecIds)
        {
            // Kiểm tra an toàn User và Identity
            if (User?.Identity == null || !User.Identity.IsAuthenticated) return Redirect("/DonXetDuyet/DangNhap");

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
                    // --- BƯỚC 1: LƯU BẢNG CHÍNH ---
                    form.Ngay = DateOnly.FromDateTime(DateTime.Now);
                    form.IdNguoiTao = userId;
                    form.TenNguoiTao = userName;
                    form.TimeNguoiTao = DateTime.Now;
                    form.TenNguoiNv = userName;
                    form.BoPhan = phongBan;
                    form.ViTri = viTri;
                    form.SoNhanVien = soNhanVien;
                    form.TenCongTy = tenCongTy;
                    form.Danhmuc = "ĐƠN LÀM LẠI THẺ";
                    form.TrangThai = "ChoDuyet";
                    form.IdForm = "HR_DonLamLaiThe_11";
                    form.TenForm = "Đơn xin làm lại thẻ";

                    _context.FormHrs.Add(form);
                    await _context.SaveChangesAsync();

                    // --- BƯỚC 2: LƯU NHÂN SỰ HỖ TRỢ ---
                    if (SelectedCongViecIds != null && SelectedCongViecIds.Length > 0)
                    {
                        foreach (var cvId in SelectedCongViecIds)
                        {
                            var congViec = await _context.CongViecHrs.FindAsync(cvId);
                            if (congViec != null)
                            {
                                _context.HrCtNguoiHoTros.Add(new HrCtNguoiHoTro
                                {
                                    IdFormHr = form.Id,
                                    IdHrNguoiHoTro = congViec.IdHrNguoiHoTro,
                                    Stt = 1
                                });
                            }
                        }
                        await _context.SaveChangesAsync();
                    }

                    // --- BƯỚC 3: TỰ ĐỘNG THÊM NGƯỜI XÁC NHẬN ---
                    var listDuyet = await _context.DmNguoiDuyetLoaiDonBoPhans
                        .Include(x => x.IdnguoiXacNhanNavigation)
                        .Include(x => x.DmCtChiTietUyQuyens.Where(uq => uq.TrangThai == true))
                            .ThenInclude(uq => uq.IduyQuyenNavigation)
                        .Where(x => x.IdloaiDonNavigation != null && x.IdloaiDonNavigation.MaLoaiDon == form.IdForm
                                    && x.IdboPhanNavigation != null && x.IdboPhanNavigation.TenBoPhan == form.BoPhan
                                    && x.IdcongTyNavigation != null && x.IdcongTyNavigation.TenCongTy == form.TenCongTy
                                    && x.TrangThai == true)
                        .OrderBy(x => x.Stt).ToListAsync();

                    foreach (var item in listDuyet)
                    {
                        var qlDuyet = new HrQuanLyDuyetB2
                        {
                            IdFormHr = form.Id,
                            IdnguoiXacNhan = item.IdnguoiXacNhan,
                            ThuTuXacNhan = item.Stt,
                            MaNguoiXacNhan = item.IdnguoiXacNhanNavigation?.MaNv,
                            TenNguoiXacNhan = item.IdnguoiXacNhanNavigation?.HoTen,
                            TrangThaiXacNhan = 0,
                            Loai = item.Loai
                        };
                        _context.HrQuanLyDuyetB2s.Add(qlDuyet);
                        await _context.SaveChangesAsync();

                        var currentTime = DateTime.Now;
                        var uqs = item.DmCtChiTietUyQuyens.Where(u => (u.ThoiGianBatDau == null || u.ThoiGianBatDau <= currentTime)
                                                                  && (u.ThoiGianKetThuc == null || u.ThoiGianKetThuc >= currentTime));
                        foreach (var uq in uqs.Where(x => x.IduyQuyenNavigation != null))
                        {
                            _context.HrQuanLyDuyetB2UyQuyens.Add(new HrQuanLyDuyetB2UyQuyen
                            {
                                IdHrQuanLyDuyetB2 = qlDuyet.Id,
                                MaNvuyQuyen = uq.IduyQuyenNavigation!.MaNvuyQuyen,
                                HoTenUyQuyen = uq.IduyQuyenNavigation.HoTenUyQuyen
                            });
                        }
                    }
                    await _context.SaveChangesAsync();

                    // --- BƯỚC 4: CHI TIẾT + FILES ---
                    if (chiTiet != null)
                    {
                        chiTiet.IdFormHr = form.Id;
                        if (!Directory.Exists(networkPath)) Directory.CreateDirectory(networkPath);
                        string safeName = RemoveSign4VietnameseString(userName).Replace(" ", "");
                        string ts = DateTime.Now.ToString("ddMMyy_HHmm");

                        if (Request.Form.Files["UploadFile"] is { Length: > 0 } upFile)
                        {
                            string fName = $"File_The_{form.Id}_{safeName}_{ts}{Path.GetExtension(upFile.FileName)}";
                            using (var fs = new FileStream(Path.Combine(networkPath, fName), FileMode.Create)) await upFile.CopyToAsync(fs);
                            form.FileDinhKem = fName;
                        }

                        if (Request.Form.Files["AnhMinhChung"] is { Length: > 0 } anhFile)
                        {
                            string imgName = $"Anh_The_{form.Id}_{safeName}_{ts}{Path.GetExtension(anhFile.FileName) ?? ".jpg"}";
                            using (var fs = new FileStream(Path.Combine(networkPath, imgName), FileMode.Create)) await anhFile.CopyToAsync(fs);
                            chiTiet.DuongDanAnh = imgName;
                        }

                        _context.HrDonLamLaiThe11s.Add(chiTiet);
                        await _context.SaveChangesAsync();
                    }

                    // --- BƯỚC 5: LỊCH SỬ ---
                    _context.LichSuFormHrs.Add(new LichSuFormHr
                    {
                        IdFormHr = form.Id,
                        TieuDe = "Khởi tạo đơn xin làm lại thẻ",
                        Mota = $"Nhân viên {userName} ({soNhanVien}) đã tạo đơn.",
                        Time = DateTime.Now,
                        IsRead = false,
                        TrangThaiAnHien = true
                    });
                    await _context.SaveChangesAsync();

                    _context.Entry(form).State = EntityState.Modified;
                    await _context.SaveChangesAsync();
                    await transaction.CommitAsync();

                    TempData["Success"] = "Gửi đơn xin làm lại thẻ thành công!";
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

        #region ĐƠN Xin Sử Dụng Điện Thoại (HR_DonSuDungDienThoai_12)

        [HttpGet("/FormHR/DonSuDungDienThoai")]
        public IActionResult DonSuDungDienThoai()
        {
            // Kiểm tra an toàn cho User và Identity
            if (User?.Identity == null || !User.Identity.IsAuthenticated)
                return Redirect("/DonXetDuyet/DangNhap");

            var userIdString = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userIdString)) return Redirect("/DonXetDuyet/DangNhap");

            int userId = int.Parse(userIdString);
            var userName = User.FindFirst(ClaimTypes.Name)?.Value ?? "";
            var phongBan = User.FindFirst("PhongBan")?.Value ?? "";
            var viTri = User.FindFirst(ClaimTypes.Role)?.Value ?? "";
            var soNhanVien = User.FindFirst(ClaimTypes.Email)?.Value ?? "";
            var tenCongTy = User.FindFirst("TenCongTy")?.Value ?? "";

            // --- Lấy danh sách nhân sự hỗ trợ ---
            // Bổ sung kiểm tra null cho các navigation property để tránh cảnh báo trình biên dịch
            ViewBag.ListNguoiHoTro = _context.HrNguoiHoTros
                .Include(x => x.CongViecHrs.Where(cv => cv != null && cv.Ten == "Đăng ký sử dụng điện thoại"))
                .Where(x => x.CongViecHrs != null && x.CongViecHrs.Any(cv => cv != null && cv.Ten == "Đăng ký sử dụng điện thoại"))
                .ToList();

            var model = new FormHr
            {
                TenNguoiNv = userName,
                BoPhan = phongBan,
                ViTri = viTri,
                SoNhanVien = soNhanVien,
                TenCongTy = tenCongTy,
                Danhmuc = "ĐƠN SỬ DỤNG ĐIỆN THOẠI",
                Ngay = DateOnly.FromDateTime(DateTime.Now),
                IdNguoiTao = userId,
                TenNguoiTao = userName,
                TimeNguoiTao = DateTime.Now,
                TrangThai = "ChoDuyet"
            };

            return View(model);
        }

        [HttpPost("/FormHR/DonSuDungDienThoai")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DonSuDungDienThoai(FormHr form, [FromForm] HrDonSuDungDienThoai12 chiTiet, int[] SelectedCongViecIds)
        {
            // Kiểm tra an toàn User và Identity
            if (User?.Identity == null || !User.Identity.IsAuthenticated) return Redirect("/DonXetDuyet/DangNhap");

            var userIdString = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            var userName = User.FindFirst(ClaimTypes.Name)?.Value ?? "Unknown";
            var phongBan = User.FindFirst("PhongBan")?.Value ?? "";
            var soNhanVien = User.FindFirst(ClaimTypes.Email)?.Value ?? "";
            var tenCongTy = User.FindFirst("TenCongTy")?.Value ?? "";

            if (string.IsNullOrEmpty(userIdString)) return Redirect("/DonXetDuyet/DangNhap");
            int userId = int.Parse(userIdString);

            string networkPath = @"\\10.0.60.30\BPVN-Fileserver\Public\IT-Information Technology Dept\5.E-Form\DonHR";

            using (var transaction = await _context.Database.BeginTransactionAsync())
            {
                try
                {
                    // --- BƯỚC 1: LƯU BẢNG CHÍNH ---
                    form.Ngay = DateOnly.FromDateTime(DateTime.Now);
                    form.IdNguoiTao = userId;
                    form.TenNguoiTao = userName;
                    form.TimeNguoiTao = DateTime.Now;
                    form.TenNguoiNv = userName;
                    form.BoPhan = phongBan;
                    form.SoNhanVien = soNhanVien;
                    form.TenCongTy = tenCongTy;
                    form.Danhmuc = "ĐƠN SỬ DỤNG ĐIỆN THOẠI";
                    form.TrangThai = "ChoDuyet";
                    form.IdForm = "HR_DonSuDungDienThoai_12";
                    form.TenForm = "Đơn xin sử dụng điện thoại";

                    _context.FormHrs.Add(form);
                    await _context.SaveChangesAsync();

                    // --- BƯỚC 2: LƯU NHÂN SỰ HỖ TRỢ ---
                    if (SelectedCongViecIds != null && SelectedCongViecIds.Length > 0)
                    {
                        foreach (var cvId in SelectedCongViecIds)
                        {
                            var cv = await _context.CongViecHrs.FindAsync(cvId);
                            if (cv != null)
                                _context.HrCtNguoiHoTros.Add(new HrCtNguoiHoTro { IdFormHr = form.Id, IdHrNguoiHoTro = cv.IdHrNguoiHoTro, Stt = 1 });
                        }
                        await _context.SaveChangesAsync();
                    }

                    // --- BƯỚC 3: TỰ ĐỘNG THÊM NGƯỜI XÁC NHẬN ---
                    var listDuyet = await _context.DmNguoiDuyetLoaiDonBoPhans
                        .Include(x => x.IdnguoiXacNhanNavigation)
                        .Include(x => x.DmCtChiTietUyQuyens.Where(uq => uq.TrangThai == true))
                            .ThenInclude(uq => uq.IduyQuyenNavigation)
                        .Where(x => x.IdloaiDonNavigation != null && x.IdloaiDonNavigation.MaLoaiDon == form.IdForm
                                    && x.IdboPhanNavigation != null && x.IdboPhanNavigation.TenBoPhan == form.BoPhan
                                    && x.IdcongTyNavigation != null && x.IdcongTyNavigation.TenCongTy == form.TenCongTy
                                    && x.TrangThai == true)
                        .OrderBy(x => x.Stt).ToListAsync();

                    foreach (var item in listDuyet)
                    {
                        var qlDuyet = new HrQuanLyDuyetB2
                        {
                            IdFormHr = form.Id,
                            IdnguoiXacNhan = item.IdnguoiXacNhan,
                            ThuTuXacNhan = item.Stt,
                            MaNguoiXacNhan = item.IdnguoiXacNhanNavigation?.MaNv,
                            TenNguoiXacNhan = item.IdnguoiXacNhanNavigation?.HoTen,
                            TrangThaiXacNhan = 0,
                            Loai = item.Loai
                        };
                        _context.HrQuanLyDuyetB2s.Add(qlDuyet);
                        await _context.SaveChangesAsync();

                        var currentTime = DateTime.Now;
                        var uqs = item.DmCtChiTietUyQuyens.Where(u => (u.ThoiGianBatDau == null || u.ThoiGianBatDau <= currentTime)
                                                                  && (u.ThoiGianKetThuc == null || u.ThoiGianKetThuc >= currentTime));
                        foreach (var uq in uqs.Where(x => x.IduyQuyenNavigation != null))
                        {
                            _context.HrQuanLyDuyetB2UyQuyens.Add(new HrQuanLyDuyetB2UyQuyen
                            {
                                IdHrQuanLyDuyetB2 = qlDuyet.Id,
                                MaNvuyQuyen = uq.IduyQuyenNavigation!.MaNvuyQuyen,
                                HoTenUyQuyen = uq.IduyQuyenNavigation.HoTenUyQuyen
                            });
                        }
                    }
                    await _context.SaveChangesAsync();

                    // --- BƯỚC 4: CHI TIẾT + FILES ---
                    if (chiTiet != null)
                    {
                        chiTiet.IdFormHr = form.Id;
                        if (!Directory.Exists(networkPath)) Directory.CreateDirectory(networkPath);
                        string safeName = RemoveSign4VietnameseString(userName).Replace(" ", "");
                        string ts = DateTime.Now.ToString("ddMMyy_HHmm");

                        if (Request.Form.Files["UploadFile"] is { Length: > 0 } upFile)
                        {
                            string fName = $"File_DienThoai_{form.Id}_{safeName}_{ts}{Path.GetExtension(upFile.FileName)}";
                            using (var fs = new FileStream(Path.Combine(networkPath, fName), FileMode.Create)) await upFile.CopyToAsync(fs);
                            form.FileDinhKem = fName;
                        }

                        if (Request.Form.Files["AnhMinhChung"] is { Length: > 0 } anhFile)
                        {
                            string imgName = $"Anh_DienThoai_{form.Id}_{safeName}_{ts}{Path.GetExtension(anhFile.FileName) ?? ".jpg"}";
                            using (var fs = new FileStream(Path.Combine(networkPath, imgName), FileMode.Create)) await anhFile.CopyToAsync(fs);
                            chiTiet.DuongDanAnh = imgName;
                        }
                        _context.HrDonSuDungDienThoai12s.Add(chiTiet);
                        await _context.SaveChangesAsync();
                    }

                    // --- BƯỚC 5: LỊCH SỬ ---
                    _context.LichSuFormHrs.Add(new LichSuFormHr
                    {
                        IdFormHr = form.Id,
                        TieuDe = "Khởi tạo đơn sử dụng điện thoại",
                        Mota = $"Nhân viên {userName} ({soNhanVien}) đã tạo đơn.",
                        Time = DateTime.Now,
                        IsRead = false,
                        TrangThaiAnHien = true
                    });
                    await _context.SaveChangesAsync();

                    _context.Entry(form).State = EntityState.Modified;
                    await _context.SaveChangesAsync();
                    await transaction.CommitAsync();

                    TempData["Success"] = "Gửi đơn xin sử dụng điện thoại thành công!";
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

        #region CHI TIẾT ĐƠN FORM HR (TẤT CẢ 12 LOẠI ĐƠN)

        [HttpGet("/FormHR/ChiTiet/{id}")]
        public async Task<IActionResult> ChiTiet(int id)
        {
            // 1. Kiểm tra đăng nhập
            var userIdStr = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userIdStr))
                return RedirectToAction("DangNhap", "DonXetDuyet");

            int userId = int.Parse(userIdStr);
            var userEmail = User.FindFirst(System.Security.Claims.ClaimTypes.Email)?.Value ?? "";
            var tenCongTyUser = User.FindFirst("TenCongTy")?.Value?.Trim() ?? "";
            var boPhanUser = User.FindFirst("TenBoPhan")?.Value ?? "";

            // 2. Lấy dữ liệu đơn (Include 9 Form cũ + 3 Form mới + CtNguoiHoTro + XacNhan + B2 + UyQuyen)
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
                // --- THÊM 3 FORM MỚI ---
                .Include(f => f.HrDonKiTucXa10s)
                .Include(f => f.HrDonLamLaiThe11s)
                .Include(f => f.HrDonSuDungDienThoai12s)
                // --- CÁC BẢNG HỆ THỐNG ---
                .Include(f => f.HrCtNguoiHoTros)
                    .ThenInclude(ct => ct.IdHrNguoiHoTroNavigation)
                .Include(f => f.HrNguoiXacNhans)
                    .ThenInclude(x => x.IdnguoiXacNhanNavigation)
                .Include(f => f.LichSuFormHrs)
                .Include(f => f.BinhLuanFormHrs)
                .Include(f => f.HrQuanLyDuyetB2s)
                    .ThenInclude(b2 => b2.HrQuanLyDuyetB2UyQuyens) // ĐÃ BỔ SUNG ĐỂ LẤY ỦY QUYỀN
                .FirstOrDefaultAsync(m => m.Id == id);

            if (don == null)
            {
                TempData["Error"] = "⚠️ Không tìm thấy đơn yêu cầu HR!";
                return RedirectToAction("LichSuHR");
            }

            // 3. KIỂM TRA QUYỀN XEM
            bool isAllowed = false;
            if (don.IdNguoiTao == userId)
            {
                isAllowed = true;
            }
            else if (User.IsInRole("All") || User.IsInRole("AdminHR"))
            {
                isAllowed = true;
            }
            else if (User.IsInRole("QuanLyDuyetDonHR"))
            {
                bool isSameCompany = string.Equals(don.TenCongTy?.Trim(), tenCongTyUser, StringComparison.OrdinalIgnoreCase);
                string listBoPhan = User.FindFirst("TenBoPhan")?.Value ?? "";
                string phongBanDon = User.FindFirst("PhongBan")?.Value ?? "";

                bool isSameDepartment = false;
                if (!string.IsNullOrEmpty(don.BoPhan))
                {
                    if (!string.IsNullOrEmpty(listBoPhan))
                        isSameDepartment = listBoPhan.Contains(don.BoPhan);
                    else
                        isSameDepartment = string.Equals(don.BoPhan.Trim(), phongBanDon.Trim(), StringComparison.OrdinalIgnoreCase);
                }
                if (isSameCompany && isSameDepartment) isAllowed = true;
            }
            else if (User.IsInRole("QuanLyDuyetDonHR_B2") || User.IsInRole("GiamDocHR"))
            {
                isAllowed = true;
            }

            if (!isAllowed) return Forbid();

            // 4. Xử lý hiển thị
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

            // Sử dụng AsNoTracking để tránh xung đột tracking entity
            ViewBag.BaoVeRecord = await _context.BaoVeHrs
                .AsNoTracking()
                .FirstOrDefaultAsync(x => x.IdFormHr == id);

            ViewBag.DanhSachXacNhan = don.HrNguoiXacNhans?
                .OrderBy(x => x.ThuTuXacNhan).ToList() ?? new List<HrNguoiXacNhan>();

            // Truyền danh sách B2 đã bao gồm dữ liệu ủy quyền vào View
            ViewBag.DanhSachB2 = don.HrQuanLyDuyetB2s?
                .OrderBy(x => x.ThuTuXacNhan).ToList() ?? new List<HrQuanLyDuyetB2>();

            return View(don);
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
            var roles = User.FindAll(System.Security.Claims.ClaimTypes.Role).Select(r => r.Value).ToList();
            if (!roles.Any(r => r == "AdminHR" || r == "All"))
                return Json(new { success = false, message = "Không có quyền!" });

            try
            {
                if (!data.TryGetProperty("idFormHr", out var idFormProp) || !data.TryGetProperty("maNv", out var maNvProp))
                    return Json(new { success = false, message = "Dữ liệu không hợp lệ!" });

                int idForm = idFormProp.GetInt32();
                string maNvMoi = maNvProp.GetString() ?? "";

                var nvHr = await _context.HrNguoiHoTros.FirstOrDefaultAsync(x => x.MaNv == maNvMoi);
                if (nvHr == null)
                    return Json(new { success = false, message = "Không tìm thấy nhân viên hỗ trợ!" });

                var hienTai = await _context.HrCtNguoiHoTros
                    .Include(x => x.IdHrNguoiHoTroNavigation)
                    .Where(x => x.IdFormHr == idForm)
                    .OrderByDescending(x => x.Stt)
                    .FirstOrDefaultAsync();

                if (hienTai?.IdHrNguoiHoTroNavigation?.MaNv == maNvMoi)
                    return Json(new { success = false, message = "Nhân viên này đang xử lý rồi!" });

                _context.HrCtNguoiHoTros.Add(new HrCtNguoiHoTro
                {
                    IdFormHr = idForm,
                    IdHrNguoiHoTro = nvHr.Id,
                    Stt = (hienTai?.Stt ?? 0) + 1
                });

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
            catch (Exception ex)
            {
                return Json(new { success = false, message = ex.Message });
            }
        }

        [HttpPost("/FormHR/ToggleLichSuAnHien")]
        public async Task<IActionResult> ToggleLichSuAnHien([FromBody] System.Text.Json.JsonElement data)
        {
            if (!User.IsInRole("All") && !User.IsInRole("AdminHR"))
                return Json(new { success = false, message = "Không có quyền thao tác!" });

            try
            {
                int idLichSu = data.GetProperty("idLichSu").GetInt32();

                var ls = await _context.LichSuFormHrs.FindAsync(idLichSu);
                if (ls == null) return Json(new { success = false, message = "Không tìm thấy bản ghi lịch sử này." });

                ls.TrangThaiAnHien = !ls.TrangThaiAnHien;
                await _context.SaveChangesAsync();

                return Json(new { success = true, newState = ls.TrangThaiAnHien });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = ex.Message });
            }
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
                .Include(f => f.HrDonKiTucXa10s)
                .Include(f => f.HrDonLamLaiThe11s)
                .Include(f => f.HrDonSuDungDienThoai12s)
                .Include(f => f.HrQuanLyDuyetB2s)
                .Include(f => f.HrNguoiXacNhans).ThenInclude(x => x.IdnguoiXacNhanNavigation)
                .FirstOrDefaultAsync(m => m.Id == id);

            if (don == null || don.IdAdmin == null)
                return NotFound("Đơn chưa hoàn tất hoặc không tồn tại.");

            using (var workbook = new ClosedXML.Excel.XLWorkbook())
            {
                var worksheet = workbook.Worksheets.Add("ChiTietDon");

                // KIỂM TRA NẾU LÀ ĐƠN SỬ DỤNG ĐIỆN THOẠI (LOẠI 12)
                if (don.HrDonSuDungDienThoai12s != null && don.HrDonSuDungDienThoai12s.Any())
                {
                    var ct = don.HrDonSuDungDienThoai12s.First();
                    var ngayTao = don.TimeNguoiTao ?? DateTime.Now;

                    // Tiêu đề công ty
                    worksheet.Cell(1, 1).Value = "CÔNG TY TNHH BEST PACIFIC VIỆT NAM";
                    worksheet.Cell(2, 1).Value = "超盈纺织（越南）有限公司";
                    worksheet.Range("A1:H1").Merge().Style.Font.SetBold().Font.FontSize = 14;
                    worksheet.Range("A2:H2").Merge().Style.Font.SetBold().Font.FontSize = 12;
                    worksheet.Range("A1:H2").Style.Alignment.SetHorizontal(ClosedXML.Excel.XLAlignmentHorizontalValues.Center);

                    // Tiêu đề đơn
                    worksheet.Cell(4, 1).Value = "ĐƠN XIN SỬ DỤNG ĐIỆN THOẠI TRONG CÔNG VIỆC";
                    worksheet.Cell(5, 1).Value = "工作时间使用手机申请单";
                    worksheet.Range("A4:H4").Merge().Style.Font.SetBold().Font.FontSize = 16;
                    worksheet.Range("A5:H5").Merge().Style.Font.SetBold().Font.FontSize = 13;
                    worksheet.Range("A4:H5").Style.Alignment.SetHorizontal(ClosedXML.Excel.XLAlignmentHorizontalValues.Center);

                    // Thời gian xin đơn
                    worksheet.Cell(7, 1).Value = $"Thời gian(申请日期): Ngày( 日 ) {ngayTao.Day} Tháng( 月 ) {ngayTao.Month} Năm( 年 ) {ngayTao.Year}";
                    worksheet.Range("A7:H7").Merge().Style.Font.SetItalic().Alignment.SetHorizontal(ClosedXML.Excel.XLAlignmentHorizontalValues.Right);

                    // --- BẢNG DỮ LIỆU CHÍNH ---
                    // Hàng 1: Họ tên | Mã số thẻ | Cấp bậc | Chức vụ
                    worksheet.Cell(9, 1).Value = "Họ tên\n姓名";
                    worksheet.Cell(9, 2).Value = ct.HoTen;
                    worksheet.Cell(9, 3).Value = "Mã số thẻ\n工号";
                    worksheet.Cell(9, 4).Value = ct.MaSoThe;
                    worksheet.Cell(9, 5).Value = "Cấp bậc\n级别";
                    worksheet.Cell(9, 6).Value = ct.CapBac;
                    worksheet.Cell(9, 7).Value = "Chức vụ\n职务/职称";
                    worksheet.Cell(9, 8).Value = ct.ChucVu;

                    // Hàng 2: Bộ phận | Thời gian bắt đầu sử dụng
                    worksheet.Cell(10, 1).Value = "Bộ phận\n部门";
                    worksheet.Cell(10, 2).Value = ct.BoPhan;
                    worksheet.Cell(10, 3).Value = "Thời gian bắt đầu\nsử dụng\n使用开始日期";
                    worksheet.Cell(10, 4).Value = ct.ThoiGianBatDauSuDung?.ToString("dd/MM/yyyy HH:mm");
                    worksheet.Range("D10:H10").Merge();

                    // Hàng 3: Lý do sử dụng
                    worksheet.Cell(11, 1).Value = "Lý do sử dụng\n使用原因";
                    worksheet.Cell(11, 2).Value = ct.LyDoSuDung + (string.IsNullOrEmpty(ct.GhiChu) ? "" : $" (Ghi chú: {ct.GhiChu})");
                    worksheet.Range("B11:H11").Merge();

                    // Định dạng borders & căn lề cho bảng thông tin
                    var mainGrid = worksheet.Range("A9:H11");
                    mainGrid.Style.Border.SetOutsideBorder(ClosedXML.Excel.XLBorderStyleValues.Thin);
                    mainGrid.Style.Border.SetInsideBorder(ClosedXML.Excel.XLBorderStyleValues.Thin);
                    mainGrid.Style.Alignment.SetVertical(ClosedXML.Excel.XLAlignmentVerticalValues.Center);
                    mainGrid.Style.Alignment.SetHorizontal(ClosedXML.Excel.XLAlignmentHorizontalValues.Center);
                    mainGrid.Style.Alignment.WrapText = true;

                    // Định dạng màu nền tiêu đề cột trong bảng
                    worksheet.Cell(9, 1).Style.Font.SetBold().Fill.SetBackgroundColor(ClosedXML.Excel.XLColor.LightGray);
                    worksheet.Cell(9, 3).Style.Font.SetBold().Fill.SetBackgroundColor(ClosedXML.Excel.XLColor.LightGray);
                    worksheet.Cell(9, 5).Style.Font.SetBold().Fill.SetBackgroundColor(ClosedXML.Excel.XLColor.LightGray);
                    worksheet.Cell(9, 7).Style.Font.SetBold().Fill.SetBackgroundColor(ClosedXML.Excel.XLColor.LightGray);
                    worksheet.Cell(10, 1).Style.Font.SetBold().Fill.SetBackgroundColor(ClosedXML.Excel.XLColor.LightGray);
                    worksheet.Cell(10, 3).Style.Font.SetBold().Fill.SetBackgroundColor(ClosedXML.Excel.XLColor.LightGray);
                    worksheet.Cell(11, 1).Style.Font.SetBold().Fill.SetBackgroundColor(ClosedXML.Excel.XLColor.LightGray);

                    worksheet.Row(9).Height = 35;
                    worksheet.Row(10).Height = 40;
                    worksheet.Row(11).Height = 55;
                    worksheet.Cell(11, 2).Style.Alignment.SetHorizontal(ClosedXML.Excel.XLAlignmentHorizontalValues.Left);

                    // --- KHỐI KÝ TÊN BIỂU MẪU ---
                    int sigRow = 14;
                    worksheet.Cell(sigRow, 1).Value = "Người xin\n申请人";
                    worksheet.Cell(sigRow, 4).Value = "Quản lý bộ phận\n部门经理";
                    worksheet.Cell(sigRow, 7).Value = "HCNS xác nhận\n人力资源部";

                    worksheet.Range(sigRow, 1, sigRow, 2).Merge();
                    worksheet.Range(sigRow, 4, sigRow, 5).Merge();
                    worksheet.Range(sigRow, 7, sigRow, 8).Merge();

                    var sigRange = worksheet.Range(sigRow, 1, sigRow, 8);
                    sigRange.Style.Font.SetBold().Alignment.SetHorizontal(ClosedXML.Excel.XLAlignmentHorizontalValues.Center);
                    sigRange.Style.Alignment.WrapText = true;

                    worksheet.Cell(sigRow + 2, 1).Value = don.TenNguoiTao;
                    worksheet.Cell(sigRow + 2, 4).Value = don.TenNguoiDuyet;
                    worksheet.Cell(sigRow + 2, 7).Value = don.TenAdmin;
                    worksheet.Range(sigRow + 2, 1, sigRow + 2, 2).Merge();
                    worksheet.Range(sigRow + 2, 4, sigRow + 2, 5).Merge();
                    worksheet.Range(sigRow + 2, 7, sigRow + 2, 8).Merge();
                    worksheet.Range(sigRow + 2, 1, sigRow + 2, 8).Style.Font.SetItalic().Alignment.SetHorizontal(ClosedXML.Excel.XLAlignmentHorizontalValues.Center);

                    // Footer Mã số biểu mẫu ở góc dưới phải
                    worksheet.Cell(20, 7).Value = "BPVN-HR-PR-006 A/1";
                    worksheet.Range("G20:H20").Merge().Style.Font.SetBold().Alignment.SetHorizontal(ClosedXML.Excel.XLAlignmentHorizontalValues.Right);
                }
                else
                {
                    // HƯỚNG DẪN MẪU KHÁC CỦA HỆ THỐNG (GIỮ NGUYÊN HOÀN TOÀN KHÔNG SỬA ĐỔI)
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
                    else if (don.HrDonKiTucXa10s.Any())
                    {
                        var ct = don.HrDonKiTucXa10s.First();
                        worksheet.Cell(currentRow, 1).Value = "I. CHI TIẾT ĐƠN: ĐĂNG KÝ KÍ TÚC XÁ";
                        worksheet.Range(currentRow, 1, currentRow, 2).Merge().Style.Font.SetBold().Fill.SetBackgroundColor(ClosedXML.Excel.XLColor.CornflowerBlue);
                        currentRow++;
                        worksheet.Cell(currentRow, 1).Value = "Mã nhân viên:"; worksheet.Cell(currentRow, 2).Value = ct.MaNhanVien; currentRow++;
                        worksheet.Cell(currentRow, 1).Value = "Họ và tên:"; worksheet.Cell(currentRow, 2).Value = ct.HoTen; currentRow++;
                        worksheet.Cell(currentRow, 1).Value = "Phòng ban / Chức vụ:"; worksheet.Cell(currentRow, 2).Value = $"{ct.PhongBan} / {ct.ChucVu}"; currentRow++;
                        worksheet.Cell(currentRow, 1).Value = "Thời gian nhận phòng:"; worksheet.Cell(currentRow, 2).Value = ct.ThoiGianNhanPhong?.ToString("dd/MM/yyyy HH:mm"); currentRow++;
                        worksheet.Cell(currentRow, 1).Value = "Thời gian trả phòng:"; worksheet.Cell(currentRow, 2).Value = ct.ThoiGianTraPhong?.ToString("dd/MM/yyyy HH:mm"); currentRow++;
                        worksheet.Cell(currentRow, 1).Value = "Loại phòng đăng ký:"; worksheet.Cell(currentRow, 2).Value = ct.LoaiPhong; currentRow++;
                        worksheet.Cell(currentRow, 1).Value = "Ghi chú:"; worksheet.Cell(currentRow, 2).Value = ct.GhiChu; currentRow++;
                    }
                    else if (don.HrDonLamLaiThe11s.Any())
                    {
                        var ct = don.HrDonLamLaiThe11s.First();
                        worksheet.Cell(currentRow, 1).Value = "I. CHI TIẾT ĐƠN: ĐĂNG KÝ LÀM LẠI THẺ";
                        worksheet.Range(currentRow, 1, currentRow, 2).Merge().Style.Font.SetBold().Fill.SetBackgroundColor(ClosedXML.Excel.XLColor.Rose);
                        currentRow++;
                        worksheet.Cell(currentRow, 1).Value = "Mã số thẻ:"; worksheet.Cell(currentRow, 2).Value = ct.MaSoThe; currentRow++;
                        worksheet.Cell(currentRow, 1).Value = "Họ và tên:"; worksheet.Cell(currentRow, 2).Value = ct.HoTen; currentRow++;
                        worksheet.Cell(currentRow, 1).Value = "Bộ phận / Cấp bậc:"; worksheet.Cell(currentRow, 2).Value = $"{ct.BoPhan} / {ct.CapBac}"; currentRow++;
                        worksheet.Cell(currentRow, 1).Value = "Chức vụ:"; worksheet.Cell(currentRow, 2).Value = ct.ChucVu; currentRow++;
                        worksheet.Cell(currentRow, 1).Value = "Lý do làm lại:"; worksheet.Cell(currentRow, 2).Value = ct.LyDoLamLaiThe; currentRow++;
                        worksheet.Cell(currentRow, 1).Value = "Ghi chú:"; worksheet.Cell(currentRow, 2).Value = ct.GhiChu; currentRow++;
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
                .Include(f => f.HrDonKiTucXa10s)
                .Include(f => f.HrDonLamLaiThe11s)
                .Include(f => f.HrDonSuDungDienThoai12s)
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
                .Include(f => f.HrDonKiTucXa10s)
                .Include(f => f.HrDonLamLaiThe11s)
                .Include(f => f.HrDonSuDungDienThoai12s)
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
            var b2List = don.HrQuanLyDuyetB2s ?? Enumerable.Empty<HrQuanLyDuyetB2>();
            var gdList = don.HrNguoiXacNhans ?? Enumerable.Empty<HrNguoiXacNhan>();

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
        .form-title { font-size: 18pt; font-weight: bold; text-align: center; text-transform: uppercase; margin: 25px 0 5px 0; }
        .form-id { font-size: 12pt; text-align: center; font-style: italic; margin-bottom: 30px; }
        .section-title { font-size: 14pt; font-weight: bold; margin-top: 25px; margin-bottom: 10px; text-transform: uppercase; border-bottom: 2px solid #000; padding-bottom: 5px; }
        .data-table { width: 100%; border-collapse: collapse; margin-bottom: 20px; }
        .data-table th, .data-table td { border: 1px solid #000; padding: 8px 12px; font-size: 12pt; vertical-align: top; }
        .data-table th { background-color: #f2f2f2; font-weight: bold; text-align: left; width: 35%; }
        
        .form-card-table { width: 100%; border-collapse: collapse; margin-top: 20px; margin-bottom: 20px; table-layout: fixed; }
        .form-card-table td { border: 1px solid #000; padding: 10px; font-size: 11pt; vertical-align: middle; word-wrap: break-word; }
        .lang-zh { font-family: 'SimSun', 'STSong', sans-serif; display: block; font-weight: normal; color: #333; margin-top: 2px; }
        .text-center { text-align: center; }
        .text-bold { font-weight: bold; }

        .signature-table { width: 100%; text-align: center; margin-top: 40px; border: none; table-layout: fixed; page-break-inside: avoid; }
        .signature-table td { vertical-align: top; border: none; font-size: 11pt; padding: 5px; word-wrap: break-word; }
        .digital-signature-box { border: 1px solid #2e7d32; padding: 6px; text-align: left; background-color: #f1f8e9; margin: 8px auto 0 auto; display: block; width: 98%; max-width: 180px; position: relative; box-sizing: border-box; }
        .sig-status { color: #2e7d32; font-size: 10pt; font-weight: bold; margin-bottom: 3px; }
        .sig-info { font-size: 8.5pt; color: #202124; line-height: 1.35; }
        .sig-check-mark { position: absolute; right: 6px; bottom: 4px; font-size: 16pt; font-weight: bold; color: rgba(46, 125, 50, 0.25); }
        .doc-footer-id { text-align: right; font-size: 10pt; font-weight: bold; margin-top: 20px; }
    ");

            if (!isForWord) sb.Append("@page { size: A4; margin: 20mm; } @media print { .document-container { padding: 0; } } </style><script>window.onload = function() { window.print(); }</script></head><body>");
            else sb.Append("</style></head><body>");

            sb.Append("<div class='document-container'>");

            // KIỂM TRA NẾU LÀ ĐƠN SỬ DỤNG ĐIỆN THOẠI (LOẠI 12) THÌ APPLY MẪU SONG NGỮ THEO ẢNH
            if (don.HrDonSuDungDienThoai12s != null && don.HrDonSuDungDienThoai12s.Any())
            {
                var ct = don.HrDonSuDungDienThoai12s.First();

                // Header chuẩn BEST PACIFIC VIỆT NAM (Dòng 1: Logo, Dòng 2: Tên tiếng Việt, Dòng 3: Tên tiếng Trung căn giữa)
                sb.Append("<table class='header-table' style='margin-bottom: 10px; width: 100%; border-collapse: collapse;'>");

                // Dòng 1: Logo BEST PACIFIC ở góc phải
                sb.Append("<tr>");
                sb.Append("<td style='text-align: right; font-size: 14pt; font-weight: bold; color: #555; font-family: sans-serif; padding-bottom: 5px; border: none;'>");
                sb.Append("BEST PACIFIC");
                sb.Append("</td>");
                sb.Append("</tr>");

                // Dòng 2: Tên công ty tiếng Việt căn giữa tuyệt đối
                sb.Append("<tr>");
                sb.Append("<td style='text-align: center; width: 100%; border: none;'>");
                sb.Append("<div class='company-name' style='font-size: 14pt; font-weight: bold; white-space: nowrap; display: inline-block;'>CÔNG TY TNHH BEST PACIFIC VIỆT NAM</div>");
                sb.Append("</td>");
                sb.Append("</tr>");

                // Dòng 3: Tên tiếng Trung hạ xuống dòng 3 và căn giữa tuyệt đối
                sb.Append("<tr>");
                sb.Append("<td style='text-align: center; width: 100%; border: none;'>");
                sb.Append("<div class='lang-zh' style='font-size: 13pt; font-weight: bold; white-space: nowrap; display: inline-block;'>超盈纺织 (越南) 有限公司</div>");
                sb.Append("</td>");
                sb.Append("</tr>");
                sb.Append("</table>");

                // Tiêu đề đơn song ngữ chính xác theo mẫu (Ép chữ nhỏ lại một chút và không xuống dòng)
                sb.Append("<div class='form-title' style='margin: 20px 0 3px 0; font-size: 16pt; font-family: \"Times New Roman\", Times, serif; white-space: nowrap; text-align: center;'>ĐƠN XIN SỬ DỤNG ĐIỆN THOẠI TRONG CÔNG VIỆC</div>");
                sb.Append("<div class='text-center text-bold' style='font-size: 14pt; margin-bottom: 20px; font-family: \"Times New Roman\", Times, serif; white-space: nowrap;'>工作时间使用手机申请单</div>");

                // Dòng thời gian xin đơn (Căn phải)
                var ngayTao = don.TimeNguoiTao ?? DateTime.Now;
                sb.Append("<div style='text-align: right; font-size: 11pt; font-style: italic; margin-bottom: 8px; padding-right: 5px;'>");
                sb.Append($"Thời gian(申请日期): Ngày( 日 ) {ngayTao.Day} &nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp; Tháng( 月 ) {ngayTao.Month} &nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp; Năm( 年 ) {ngayTao.Year}");
                sb.Append("</div>");

                // Bảng nội dung thông tin chi tiết cấu trúc chuẩn 100% theo ảnh
                sb.Append("<table class='form-card-table'>");
                sb.Append("<tr>");
                sb.Append("<td style='width: 12%; text-align: center;' class='text-bold'>Họ tên<br/><span class='lang-zh'>姓名</span></td>");
                sb.Append($"<td style='width: 22%; text-align: center;'>{ct.HoTen}</td>");
                sb.Append("<td style='width: 13%; text-align: center;' class='text-bold'>Mã số thẻ<br/><span class='lang-zh'>工号</span></td>");
                sb.Append($"<td style='width: 18%; text-align: center;'>{ct.MaSoThe}</td>");
                sb.Append("<td style='width: 10%; text-align: center;' class='text-bold'>Cấp bậc<br/><span class='lang-zh'>级别</span></td>");
                sb.Append($"<td style='width: 9%; text-align: center;'>{ct.CapBac}</td>");
                sb.Append("<td style='width: 11%; text-align: center;' class='text-bold'>Chức vụ<br/><span class='lang-zh'>职务/职称</span></td>");
                sb.Append($"<td style='width: 15%; text-align: center;'>{ct.ChucVu}</td>");
                sb.Append("</tr>");

                sb.Append("<tr>");
                sb.Append("<td style='text-align: center;' class='text-bold'>Bộ phận<br/><span class='lang-zh'>部门</span></td>");
                sb.Append($"<td style='text-align: center;'>{ct.BoPhan}</td>");
                sb.Append("<td style='text-align: center;' class='text-bold'>Thời gian bắt đầu<br/>sử dụng<br/><span class='lang-zh'>使用开始日期</span></td>");
                sb.Append($"<td colspan='5' style='padding-left: 15px;'>{ct.ThoiGianBatDauSuDung?.ToString("dd/MM/yyyy HH:mm")}</td>");
                sb.Append("</tr>");

                sb.Append("<tr>");
                sb.Append("<td style='text-align: center; height: 90px;' class='text-bold'>Lý do sử dụng<br/><span class='lang-zh'>使用原因</span></td>");
                sb.Append($"<td colspan='7' style='vertical-align: top; padding: 12px; line-height: 1.6;'>{ct.LyDoSuDung}{(string.IsNullOrEmpty(ct.GhiChu) ? "" : $"<br/><i>(Ghi chú: {ct.GhiChu})</i>")}</td>");
                sb.Append("</tr>");
                sb.Append("</table>");
            }
            else if (don.HrDonLamLaiThe11s != null && don.HrDonLamLaiThe11s.Any())
            {
                // ĐƠN LÀM LẠI THẺ NHÂN VIÊN (LOẠI 11) - PHẦN TRÊN ĐẦU ĐÃ ĐƯỢC ĐỒNG BỘ GIỐNG ĐƠN ĐIỆN THOẠI
                var ct = don.HrDonLamLaiThe11s.First();

                // Header chuẩn BEST PACIFIC VIỆT NAM (Dòng 1: Logo, Dòng 2: Tên tiếng Việt, Dòng 3: Tên tiếng Trung căn giữa)
                sb.Append("<table class='header-table' style='margin-bottom: 10px; width: 100%; border-collapse: collapse;'>");
                sb.Append("<tr>");
                sb.Append("<td style='text-align: right; font-size: 14pt; font-weight: bold; color: #555; font-family: sans-serif; padding-bottom: 5px; border: none;'>");
                sb.Append("BEST PACIFIC");
                sb.Append("</td>");
                sb.Append("</tr>");
                sb.Append("<tr>");
                sb.Append("<td style='text-align: center; width: 100%; border: none;'>");
                sb.Append("<div class='company-name' style='font-size: 14pt; font-weight: bold; white-space: nowrap; display: inline-block;'>CÔNG TY TNHH BEST PACIFIC VIỆT NAM</div>");
                sb.Append("</td>");
                sb.Append("</tr>");
                sb.Append("<tr>");
                sb.Append("<td style='text-align: center; width: 100%; border: none;'>");
                sb.Append("<div class='lang-zh' style='font-size: 13pt; font-weight: bold; white-space: nowrap; display: inline-block;'>超盈纺织 (越南) 有限公司</div>");
                sb.Append("</td>");
                sb.Append("</tr>");
                sb.Append("</table>");

                // Tiêu đề đơn song ngữ (Ép chữ nhỏ lại một chút và không xuống dòng giống đơn điện thoại)
                sb.Append("<div class='form-title' style='margin: 20px 0 3px 0; font-size: 16pt; font-family: \"Times New Roman\", Times, serif; white-space: nowrap; text-align: center;'>ĐƠN XIN LÀM LẠI THẺ NHÂN VIÊN</div>");
                sb.Append("<div class='text-center text-bold' style='font-size: 14pt; margin-bottom: 20px; font-family: \"Times New Roman\", Times, serif; white-space: nowrap;'>重新办理厂牌申请单</div>");

                var ngayTao = don.TimeNguoiTao ?? DateTime.Now;
                sb.Append("<div style='text-align: right; font-size: 11pt; font-style: italic; margin-bottom: 8px; padding-right: 5px;'>");
                sb.Append($"Thời gian(申请日期): Ngày( 日 ) {ngayTao.Day} &nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp; Tháng( 月 ) {ngayTao.Month} &nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp; Năm( 年 ) {ngayTao.Year}");
                sb.Append("</div>");

                sb.Append("<table class='form-card-table'><tr><td style='width: 18%; text-align: center;' class='text-bold'>Họ tên<br/><span class='lang-zh'>姓名</span></td><td style='width: 27%; text-align: center;'>" + ct.HoTen + "</td><td style='width: 15%; text-align: center;' class='text-bold'>Mã số thẻ<br/><span class='lang-zh'>工号</span></td><td style='width: 20%; text-align: center;'>" + ct.MaSoThe + "</td><td style='width: 10%; text-align: center;' class='text-bold'>Cấp bậc<br/><span class='lang-zh'>级别</span></td><td style='width: 10%; text-align: center;'>" + ct.CapBac + "</td></tr><tr><td style='text-align: center;' class='text-bold'>Bộ phận<br/><span class='lang-zh'>部门</span></td><td style='text-align: center;'>" + ct.BoPhan + "</td><td style='text-align: center;' class='text-bold'>Chức vụ<br/><span class='lang-zh'>职务/职称</span></td><td colspan='3' style='text-align: center;'>" + ct.ChucVu + "</td></tr><tr><td style='text-align: center; height: 60px;' class='text-bold'>Lý do làm lại thẻ<br/><span class='lang-zh'>重新办理原因</span></td><td colspan='5' style='vertical-align: top; padding: 10px; line-height: 1.6;'>" + ct.LyDoLamLaiThe + (string.IsNullOrEmpty(ct.GhiChu) ? "" : " (Ghi chú: " + ct.GhiChu + ")") + "</td></tr></table>");
            }
            else
            {
                // CÁC LOẠI ĐƠN KHÁC (LOẠI 1 ĐẾN LOẠI 10) - GIỮ NGUYÊN HOÀN TOÀN LAYOUT CŨ
                sb.Append("<table class='header-table'><tr><td><div class='company-name'>BEST PACIFIC</div><div class='company-sub'>PHÒNG NHÂN SỰ (HR)</div></td><td><div class='national-title'>CỘNG HÒA XÃ HỘI CHỦ NGHĨA VIỆT NAM</div><div class='national-sub'>Độc lập - Tự do - Hạnh phúc</div></td></tr></table>");
                sb.Append($"<div class='form-title'>{don.TenForm}</div><div class='form-id'>Mã phiếu: #{don.Id} | Trạng thái: HOÀN TẤT</div>");

                sb.Append("<table class='data-table'>");
                sb.Append($"<tr><th>Mã Đơn:</th><td>{don.Id}</td></tr>");
                sb.Append($"<tr><th>Tên Form:</th><td>{don.TenForm}</td></tr>");
                sb.Append($"<tr><th>Mã NV:</th><td>{don.SoNhanVien}</td></tr>");
                sb.Append($"<tr><th>Họ Tên:</th><td>{don.TenNguoiNv}</td></tr>");
                sb.Append($"<tr><th>Bộ Phận:</th><td>{don.BoPhan}</td></tr>");
                sb.Append($"<tr><th>Ngày Tạo:</th><td>{don.TimeNguoiTao?.ToString("dd/MM/yyyy HH:mm")}</td></tr>");
                sb.Append($"<tr><th>Trạng Thái:</th><td><strong style='color: green;'>HOÀN TẤT</strong></td></tr>");
                sb.Append("</table>");

                sb.Append("<div class='section-title'>I. Chi tiết nội dung đơn</div>");
                sb.Append("<table class='data-table'>");

                if (don.HrXinRaNgoai1s.Any())
                {
                    var ct = don.HrXinRaNgoai1s.First();
                    sb.Append($"<tr><th>Lý do:</th><td>{ct.LiDo}</td></tr>");
                    sb.Append($"<tr><th>Địa điểm:</th><td>{ct.DiaDiem}</td></tr>");
                    sb.Append($"<tr><th>Thời gian ra:</th><td>{ct.ThoiGianRa?.ToString("dd/MM/yyyy HH:mm")}</td></tr>");
                    sb.Append($"<tr><th>Dự kiến về:</th><td>{ct.ThoiGianVeDuTinh?.ToString("dd/MM/yyyy HH:mm")}</td></tr>");
                }
                else if (don.HrMangHangHoaRaCong2s.Any())
                {
                    var ct = don.HrMangHangHoaRaCong2s.First();
                    sb.Append($"<tr><th>Mô tả hàng hóa:</th><td>{ct.MoTa}</td></tr>");
                    sb.Append($"<tr><th>Thời gian dự tính:</th><td>{ct.TimeDuTinh?.ToString("dd/MM/yyyy HH:mm")}</td></tr>");
                }
                else if (don.HrDangKySuDungXeCongTac3s.Any())
                {
                    var ct = don.HrDangKySuDungXeCongTac3s.First();
                    sb.Append($"<tr><th>Số điện thoại:</th><td>{ct.SoDienThoai}</td></tr>");
                    sb.Append($"<tr><th>Số lượng người:</th><td>{ct.SoLuong}</td></tr>");
                    sb.Append($"<tr><th>Lý do / Lộ trình:</th><td>{ct.LiDo}</td></tr>");
                    sb.Append($"<tr><th>Thời gian đi:</th><td>{ct.TimeDuTinh?.ToString("dd/MM/yyyy HH:mm")}</td></tr>");
                    sb.Append($"<tr><th>Thời gian về:</th><td>{ct.ThoiGianVe?.ToString("dd/MM/yyyy HH:mm")}</td></tr>");
                    sb.Append($"<tr><th>Ghi chú:</th><td>{ct.GhiChu}</td></tr>");
                }
                else if (don.HrDangKySuDungXeDaily4s.Any())
                {
                    var ct = don.HrDangKySuDungXeDaily4s.First();
                    sb.Append($"<tr><th>Điểm đón:</th><td>{ct.DiemDon}</td></tr>");
                    sb.Append($"<tr><th>Lý do:</th><td>{ct.LiDo}</td></tr>");
                    sb.Append($"<tr><th>Thời gian:</th><td>{ct.TimeDuTinh?.ToString("dd/MM/yyyy HH:mm")}</td></tr>");
                }
                else if (don.HrDonTiepKhac5s.Any())
                {
                    var ct = don.HrDonTiepKhac5s.First();
                    sb.Append($"<tr><th>Tên công ty khách:</th><td>{ct.TenCongTyKhach}</td></tr>");
                    sb.Append($"<tr><th>Số lượng khách:</th><td>{ct.SoLuongKhach}</td></tr>");
                    sb.Append($"<tr><th>Người đặt:</th><td>{ct.NguoiBook}</td></tr>");
                    sb.Append($"<tr><th>Yêu cầu tiếp khách:</th><td>{ct.YeuCauTiepKhach}</td></tr>");
                    sb.Append($"<tr><th>Phòng họp:</th><td>{ct.TenPhongHop}</td></tr>");
                    sb.Append($"<tr><th>Loại suất ăn:</th><td>{ct.LoaiSuatAn}</td></tr>");
                    sb.Append($"<tr><th>Ghi chú suất ăn:</th><td>{ct.GhiChuSuatAn}</td></tr>");
                }
                else if (don.HrNhaThauQuaCong6s.Any())
                {
                    var ct = don.HrNhaThauQuaCong6s.First();
                    sb.Append($"<tr><th>Tên nhà thầu:</th><td>{ct.TenNhaThau}</td></tr>");
                    sb.Append($"<tr><th>Số người:</th><td>{ct.SoNguoi}</td></tr>");
                    sb.Append($"<tr><th>Người đăng ký:</th><td>{ct.NguoiDangKy}</td></tr>");
                    sb.Append($"<tr><th>Mục đích công việc:</th><td>{ct.MucDichCongViec}</td></tr>");
                }
                else if (don.HrHoTroTienDienThoai7s.Any())
                {
                    var ct = don.HrHoTroTienDienThoai7s.First();
                    sb.Append($"<tr><th>Số điện thoại:</th><td>{ct.SoDienThoai}</td></tr>");
                    sb.Append($"<tr><th>Mức hỗ trợ:</th><td>{ct.MucHoTro}</td></tr>");
                    sb.Append($"<tr><th>Mục đích:</th><td>{ct.MucDich}</td></tr>");
                }
                else if (don.HrDoiCaLam8s.Any())
                {
                    var ct = don.HrDoiCaLam8s.First();
                    sb.Append($"<tr><th>Ngày cần đổi:</th><td>{ct.NgayCanDoi?.ToString("dd/MM/yyyy")}</td></tr>");
                    sb.Append($"<tr><th>Ca gốc:</th><td>{ct.CaGoc}</td></tr>");
                    sb.Append($"<tr><th>Ca muốn đổi:</th><td>{ct.CaMuonDoi}</td></tr>");
                    sb.Append($"<tr><th>Lý do đổi ca:</th><td>{ct.LyDoDoiCa}</td></tr>");
                }
                else if (don.HrDonHoTroCongTac9s.Any())
                {
                    var ct = don.HrDonHoTroCongTac9s.First();
                    sb.Append($"<tr><th>Mã NV Công tác:</th><td>{ct.MaNhanVien}</td></tr>");
                    sb.Append($"<tr><th>Tên khách hàng:</th><td>{ct.TenKhachHang}</td></tr>");
                    sb.Append($"<tr><th>Đặt vé máy bay:</th><td>{(ct.DatVeMayBay == true ? "Có" : "Không")}</td></tr>");
                    sb.Append($"<tr><th>Đặt chỗ ở:</th><td>{(ct.DatChoO == true ? "Có" : "Không")}</td></tr>");
                    sb.Append($"<tr><th>Đặt bữa ăn:</th><td>{(ct.DatBuaAn == true ? "Có" : "Không")}</td></tr>");
                    sb.Append($"<tr><th>Xe đưa đón:</th><td>{(ct.BookXeCtyDuaDon == true ? "Có" : "Không")}</td></tr>");
                    sb.Append($"<tr><th>Chi tiết yêu cầu:</th><td>{ct.NoiDungYeuCauChiTiet}</td></tr>");
                }
                else if (don.HrDonKiTucXa10s.Any())
                {
                    var ct = don.HrDonKiTucXa10s.First();
                    sb.Append($"<tr><th>Mã nhân viên:</th><td>{ct.MaNhanVien}</td></tr>");
                    sb.Append($"<tr><th>Họ và tên:</th><td>{ct.HoTen}</td></tr>");
                    sb.Append($"<tr><th>Phòng ban / Chức vụ:</th><td>{ct.PhongBan} / {ct.ChucVu}</td></tr>");
                    sb.Append($"<tr><th>Thời gian nhận phòng:</th><td>{ct.ThoiGianNhanPhong?.ToString("dd/MM/yyyy HH:mm")}</td></tr>");
                    sb.Append($"<tr><th>Thời gian trả phòng:</th><td>{ct.ThoiGianTraPhong?.ToString("dd/MM/yyyy HH:mm")}</td></tr>");
                    sb.Append($"<tr><th>Loại phòng:</th><td>{ct.LoaiPhong}</td></tr>");
                    sb.Append($"<tr><th>Ghi chú:</th><td>{ct.GhiChu}</td></tr>");
                }
                sb.Append("</table>");
            }

            // ============================================================
            // PHẦN CHỮ KÝ ĐIỆN TỬ - TỰ ĐỘNG ĐỔI TIÊU ĐỀ SONG NGỮ THEO LOẠI ĐƠN 11 & 12
            // ============================================================
            bool isMẫuThẻHoặcĐiệnThoại = (don.HrDonLamLaiThe11s != null && don.HrDonLamLaiThe11s.Any()) ||
                                         (don.HrDonSuDungDienThoai12s != null && don.HrDonSuDungDienThoai12s.Any());

            bool isDon12 = don.HrDonSuDungDienThoai12s != null && don.HrDonSuDungDienThoai12s.Any();

            // Tính số lượng cột cho chữ ký để chia tỉ lệ phần trăm đều nhau
            int totalCols = 3; // Mặc định có: Người xin, Quản lý bộ phận, HCNS xác nhận
            if (!isDon12) // Đơn 12 cố định 3 vị trí ký như hình ảnh, mẫu khác load động B2/GD nếu có
            {
                totalCols += b2List.Count() + gdList.Count();
            }
            double colPercent = 100.0 / (totalCols > 0 ? totalCols : 1);

            void AppendSig(string title, string titleZh, string name, DateTime? time)
            {
                sb.Append($"<td style='width:{colPercent}%; text-align: center; vertical-align: top;'>");
                sb.Append($"<strong style='font-size: 11.5pt;'>{title}</strong>");
                if (isMẫuThẻHoặcĐiệnThoại && !string.IsNullOrEmpty(titleZh))
                {
                    sb.Append($"<br/><span class='lang-zh' style='font-size:10.5pt; font-weight: bold; margin-top: 0px;'>{titleZh}</span>");
                }
                sb.Append("<br/><span style='font-size:8.5pt; font-style:italic; color: #666;'>(Chữ ký điện tử)</span><br/>");

                if (time.HasValue)
                    sb.Append($"<div class='digital-signature-box'><div class='sig-status'>Signature Valid</div><div class='sig-info'>Ký bởi: {name}<br/>Ký ngày: {time?.ToString("dd/MM/yyyy")}</div><div class='sig-check-mark'>✓</div></div>");
                else
                    sb.Append($"<br/><br/><br/><br/><strong style='font-size: 11pt;'>{name ?? ""}</strong>");
                sb.Append("</td>");
            }

            sb.Append("<table class='signature-table'><tr>");

            // Vị trí 1: Người xin / 申请人
            AppendSig("Người xin", "申请人", don.TenNguoiTao ?? "", don.TimeNguoiTao);

            // Vị trí 2: Quản lý bộ phận / 部门经理
            AppendSig("Quản lý bộ phận", "部门经理", don.TenNguoiDuyet ?? "", don.TimeNguoiDuyet);

            if (!isDon12)
            {
                // Quản lý bước 2 (Chỉ hiển thị đối với các đơn khác đơn 12)
                foreach (var b2 in b2List.OrderBy(x => x.ThuTuXacNhan))
                {
                    AppendSig("Quản lý B2", "部门经理 B2", b2.TenNguoiXacNhan ?? "", b2.ThoiGianXacNhan);
                }

                // Ban Giám đốc duyệt (Chỉ hiển thị đối với các đơn khác đơn 12)
                foreach (var xn in gdList.OrderBy(x => x.ThuTuXacNhan))
                {
                    AppendSig("Ban Giám Đốc", "总经理", xn.IdnguoiXacNhanNavigation?.HoTen ?? xn.TenNguoiXacNhan ?? "", xn.ThoiGianXacNhan);
                }
            }

            // Vị trí cuối: HCNS xác nhận / 人力资源部
            AppendSig("HCNS xác nhận", "人力资源部", don.TenAdmin ?? "", don.TimeAdmin);

            sb.Append("</tr></table>");

            // Gán mã số biểu mẫu cố định ở góc dưới cùng bên phải
            if (isDon12)
            {
                sb.Append("<div class='doc-footer-id' style='margin-top: 50px; font-family: sans-serif; font-size: 10.5pt;'>BPVN-HR-PR-006 A/1</div>");
            }
            else if (don.HrDonLamLaiThe11s != null && don.HrDonLamLaiThe11s.Any())
            {
                sb.Append("<div class='doc-footer-id' style='margin-top: 40px; font-family: sans-serif; font-size: 10.5pt;'>BPVN-HR-PR-017 A/1</div>");
            }

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

                // Đảo ngược để hiển thị từ cũ tới mới cho giao diện chat
                binhLuans.Reverse();

                return Json(new { success = true, data = binhLuans });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = "Lỗi tải bình luận: " + ex.Message });
            }
        }

        [HttpPost("/FormHR/ThemBinhLuan")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ThemBinhLuan()
        {
            try
            {
                var formCollection = await Request.ReadFormAsync();

                // 1. Kiểm tra ID form an toàn
                if (!int.TryParse(formCollection["idForm"], out int idForm))
                    return Json(new { success = false, message = "ID đơn không hợp lệ" });

                string noiDung = formCollection["noiDung"].ToString();
                var file = formCollection.Files.GetFile("file");

                // 2. Lấy thông tin User an toàn
                var userIdStr = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                if (string.IsNullOrEmpty(userIdStr) || !int.TryParse(userIdStr, out int currentUserId))
                    return Json(new { success = false, message = "Chưa đăng nhập hoặc phiên làm việc hết hạn" });

                var formHr = await _context.FormHrs.FindAsync(idForm);
                if (formHr == null)
                    return Json(new { success = false, message = "Không tìm thấy đơn" });

                // 3. Xử lý File an toàn
                string? fileName = null;
                if (file != null && file.Length > 0)
                {
                    if (file.Length > 50 * 1024 * 1024)
                        return Json(new { success = false, message = "File không được vượt quá 50MB" });

                    string networkPath = @"\\10.0.60.30\BPVN-Fileserver\Public\IT-Information Technology Dept\5.E-Form\BinhLuanDonHR";
                    if (!Directory.Exists(networkPath)) Directory.CreateDirectory(networkPath);

                    fileName = $"{DateTime.Now:yyyyMMdd_HHmmss}_{Guid.NewGuid().ToString().Substring(0, 8)}_{Path.GetFileName(file.FileName)}";
                    using (var stream = new FileStream(Path.Combine(networkPath, fileName), FileMode.Create))
                    {
                        await file.CopyToAsync(stream);
                    }
                }

                if (string.IsNullOrWhiteSpace(noiDung) && fileName == null)
                    return Json(new { success = false, message = "Vui lòng nhập nội dung hoặc đính kèm file" });

                // 4. Tạo đối tượng với các giá trị an toàn
                var binhLuan = new BinhLuanFormHr
                {
                    IdFormHr = idForm,
                    NoiDung = noiDung?.Trim(),
                    IdNguoiBinhLuan = currentUserId,
                    TenNguoiBinhLuan = User.Identity?.Name ?? "Unknown",
                    Ma = User.FindFirst(ClaimTypes.Email)?.Value ?? "",
                    PhongBan = User.FindFirst("PhongBan")?.Value ?? "",
                    TenCongTy = User.FindFirst("TenCongTy")?.Value ?? "",
                    ThoiGian = DateTime.Now,
                    TrangThai = 1,
                    FileDinhKem = fileName
                };

                _context.BinhLuanFormHrs.Add(binhLuan);

                // 5. Ghi log lịch sử
                _context.LichSuFormHrs.Add(new LichSuFormHr
                {
                    IdFormHr = idForm,
                    TieuDe = "BÌNH LUẬN MỚI",
                    Mota = $"Người dùng {binhLuan.TenNguoiBinhLuan} đã bình luận: {(binhLuan.NoiDung?.Length > 50 ? binhLuan.NoiDung.Substring(0, 50) + "..." : binhLuan.NoiDung ?? "Đính kèm file")}",
                    Time = DateTime.Now,
                    TrangThaiAnHien = true
                });

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
                        binhLuan.ThoiGian,
                        binhLuan.FileDinhKem
                    }
                });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = "Lỗi hệ thống: " + ex.Message });
            }
        }

        [HttpPost("/FormHR/XoaBinhLuan")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> XoaBinhLuan([FromBody] System.Text.Json.JsonElement data)
        {
            try
            {
                // 1. Kiểm tra dữ liệu đầu vào
                if (!data.TryGetProperty("id", out var idProp))
                    return Json(new { success = false, message = "Dữ liệu không hợp lệ" });

                int id = idProp.GetInt32();

                // 2. Lấy thông tin user an toàn
                var userIdStr = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                var userRole = User.FindFirst("UserRole")?.Value ?? "";

                var binhLuan = await _context.BinhLuanFormHrs.FindAsync(id);
                if (binhLuan == null)
                    return Json(new { success = false, message = "Không tìm thấy bình luận" });

                // 3. Kiểm tra quyền sở hữu
                int currentUserId = int.Parse(userIdStr ?? "0");
                if (binhLuan.IdNguoiBinhLuan != currentUserId && userRole != "AdminHR" && userRole != "All")
                    return Json(new { success = false, message = "Bạn không có quyền xóa bình luận này" });

                // 4. Xóa file an toàn
                if (!string.IsNullOrEmpty(binhLuan.FileDinhKem))
                {
                    string networkPath = @"\\10.0.60.30\BPVN-Fileserver\Public\IT-Information Technology Dept\5.E-Form\BinhLuanDonHR";
                    string fullPath = Path.Combine(networkPath, binhLuan.FileDinhKem);

                    // Sử dụng FileInfo để xử lý an toàn hơn
                    var fileInfo = new FileInfo(fullPath);
                    if (fileInfo.Exists)
                    {
                        fileInfo.Delete();
                    }
                }

                _context.BinhLuanFormHrs.Remove(binhLuan);

                // 5. Ghi log xóa (Sử dụng null-safe cho User.Identity.Name)
                string userName = User.Identity?.Name ?? "Hệ thống";
                _context.LichSuFormHrs.Add(new LichSuFormHr
                {
                    IdFormHr = binhLuan.IdFormHr,
                    TieuDe = "XÓA BÌNH LUẬN",
                    Mota = $"{userName} đã xóa một bình luận.",
                    Time = DateTime.Now,
                    TrangThaiAnHien = true
                });

                await _context.SaveChangesAsync();
                return Json(new { success = true, message = "Đã xóa bình luận" });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = "Lỗi: " + ex.Message });
            }
        }

        [HttpGet("/FormHR/DownloadBinhLuanFile/{fileName}")]
        public IActionResult DownloadBinhLuanFile(string fileName)
        {
            if (string.IsNullOrEmpty(fileName)) return NotFound();

            string networkPath = @"\\10.0.60.30\BPVN-Fileserver\Public\IT-Information Technology Dept\5.E-Form\BinhLuanDonHR";
            string fullPath = Path.Combine(networkPath, fileName);

            if (!System.IO.File.Exists(fullPath)) return NotFound("File không tồn tại");

            string contentType = "application/octet-stream";
            string originalFileName = string.Join("_", fileName.Split('_').Skip(2));

            return PhysicalFile(fullPath, contentType, originalFileName);
        }

        #endregion 

        #region ĐƠN CHỜ XÉT DUYỆT (Dành cho nhân viên xem danh sách đơn đã tạo)

        // 1. TRẢ VỀ VIEW RỖNG CHO JS TỰ RENDER
        [HttpGet("/FormHR/DonCho")]
        public IActionResult DonCho()
        {
            if (User == null || User.Identity == null || !User.Identity.IsAuthenticated)
                return Redirect("/DonXetDuyet/DangNhap");

            return View();
        }

        // 2. API TRẢ VỀ JSON ĐÃ TÍNH TOÁN SẴN LOGIC B2/GĐ/BV (100% JS)
        [HttpGet("/FormHR/GetDonChoData")]
        public async Task<IActionResult> GetDonChoData()
        {
            var userIdClaim = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value
                              ?? User.FindFirst("UserId")?.Value;

            if (string.IsNullOrEmpty(userIdClaim))
                return Unauthorized();

            try
            {
                int currentUserId = int.Parse(userIdClaim);

                var danhSachDon = await _context.FormHrs
                    .Include(f => f.HrNguoiXacNhans).ThenInclude(xn => xn.IdnguoiXacNhanNavigation)
                    .Include(f => f.HrQuanLyDuyetB2s)
                    .Include(f => f.BaoVeHrs)
                    .Include(f => f.HrCtNguoiHoTros).ThenInclude(ct => ct.IdHrNguoiHoTroNavigation)
                    .Where(f => f.IdNguoiTao == currentUserId)
                    .OrderByDescending(f => f.Id)
                    .AsNoTracking()
                    .ToListAsync();

                var result = danhSachDon.Select(item =>
                {
                    // --- KHỞI TẠO CÁC COLLECTION AN TOÀN ---
                    var b2List = item.HrQuanLyDuyetB2s ?? Enumerable.Empty<HrQuanLyDuyetB2>();
                    var gdList = item.HrNguoiXacNhans ?? Enumerable.Empty<HrNguoiXacNhan>();
                    var bvList = item.BaoVeHrs ?? Enumerable.Empty<BaoVeHr>();
                    var hoTroList = item.HrCtNguoiHoTros ?? Enumerable.Empty<HrCtNguoiHoTro>();

                    // --- LOGIC B2 ---
                    bool hasB2 = b2List.Any();
                    bool checkB2Approved = !hasB2 || (b2List.FirstOrDefault()?.Loai?.ToUpper() == "OR" || b2List.FirstOrDefault()?.Loai?.ToUpper() == "ANY"
                                            ? b2List.Any(x => x.TrangThaiXacNhan == 1)
                                            : b2List.All(x => x.TrangThaiXacNhan == 1));
                    bool checkB2Rejected = hasB2 && b2List.Any(x => x.TrangThaiXacNhan == 2);

                    // --- LOGIC GIÁM ĐỐC ---
                    bool hasGD = gdList.Any();
                    bool isGDApproved = hasGD && gdList.All(x => x.TrangThaiXacNhan == 1);
                    bool isGDRejected = hasGD && gdList.Any(x => x.TrangThaiXacNhan == 2);

                    // --- TRẠNG THÁI CHUNG ---
                    bool isCancelled = (item.TenForm ?? "").Contains("[ĐÃ HỦY]");
                    bool isFinished = item.IdAdmin != null;
                    bool isManagerApproved = item.IdNguoiDuyet != null;

                    string statusText, bgColor, fgColor, progressWidth, progressColor;

                    if (isCancelled || checkB2Rejected || isGDRejected)
                    {
                        statusText = "HỦY/TỪ CHỐI"; bgColor = "#fef2f2"; fgColor = "#b91c1c";
                        progressWidth = "100%"; progressColor = "#ef4444";
                    }
                    else if (isFinished)
                    {
                        statusText = "XONG"; bgColor = "#ecfdf5"; fgColor = "#047857";
                        progressWidth = "100%"; progressColor = "#10b981";
                    }
                    else if (!isManagerApproved)
                    {
                        statusText = "QUẢN LÝ"; bgColor = "#fffbeb"; fgColor = "#b45309";
                        progressWidth = "20%"; progressColor = "#f59e0b";
                    }
                    else if (hasB2 && !checkB2Approved)
                    {
                        statusText = "BP XÁC NHẬN"; bgColor = "#f0f9ff"; fgColor = "#0369a1";
                        progressWidth = "40%"; progressColor = "#0ea5e9";
                    }
                    else if (hasGD && !isGDApproved)
                    {
                        statusText = "GIÁM ĐỐC"; bgColor = "#fdf4ff"; fgColor = "#86198f";
                        progressWidth = "60%"; progressColor = "#d946ef";
                    }
                    else
                    {
                        statusText = "ADMIN"; bgColor = "#eff6ff"; fgColor = "#1e40af";
                        progressWidth = "85%"; progressColor = "#2563eb";
                    }

                    // --- BẢO VỆ VÀ NGƯỜI HỖ TRỢ ---
                    var lastSupportHr = hoTroList.OrderByDescending(x => x.Stt).FirstOrDefault();
                    var bv = bvList.FirstOrDefault();

                    return new
                    {
                        Id = item.Id,
                        TenForm = (item.TenForm ?? "").Replace("[ĐÃ HỦY]", "").Trim(),
                        Danhmuc = item.Danhmuc ?? "Biểu mẫu HR",
                        StatusText = statusText,
                        BgColor = bgColor,
                        FgColor = fgColor,
                        ProgressWidth = progressWidth,
                        ProgressColor = progressColor,
                        SupportName = lastSupportHr?.IdHrNguoiHoTroNavigation?.Ten ?? "Chờ phân công",
                        Ngay = item.Ngay?.ToDateTime(TimeOnly.MinValue).ToString("dd/MM/yyyy") ?? "--",
                        TimeNguoiTao = item.TimeNguoiTao?.ToString("HH:mm") ?? "--",
                        HasB2 = hasB2,
                        B2ApprovedCount = b2List.Count(x => x.TrangThaiXacNhan == 1),
                        B2TotalCount = b2List.Count(),
                        HasGD = hasGD,
                        GDApprovedCount = gdList.Count(x => x.TrangThaiXacNhan == 1),
                        GDTotalCount = gdList.Count(),
                        HasBV = bvList.Any(),
                        BVStatusText = bv?.TrangThai == 1 ? "BV XONG" : "BV CHỜ"
                    };
                });

                return Json(result);
            }
            catch (Exception ex)
            {
                return BadRequest(new { message = "Có lỗi xảy ra: " + ex.Message });
            }
        }
        #endregion

        #region XỬ LÝ ĐƠN HR (Duyệt / Hủy / Hoàn tất) - PHÂN QUYỀN MỚI 2026

        // 1. TRẢ VỀ VIEW RỖNG (Cho JS tự render)
        [HttpGet("/FormHR/QuanLyXetDuyet")]
        public IActionResult QuanLyXetDuyet()
        {
            var userIdStr = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userIdStr)) return Redirect("/DonXetDuyet/DangNhap");
            return View();
        }

        // 2. API TRẢ DỮ LIỆU ĐÃ TÍNH TOÁN (100% JS)
        [HttpGet("/FormHR/GetQuanLyXetDuyetData")]
        public async Task<IActionResult> GetQuanLyXetDuyetData()
        {
            var userIdStr = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userIdStr)) return Unauthorized();
            int userId = int.Parse(userIdStr);

            var phongBan = User.FindFirst("PhongBan")?.Value?.Trim() ?? "";
            var tenCongTy = User.FindFirst("TenCongTy")?.Value?.Trim() ?? "";

            var listTenBoPhanStr = User.FindFirst("TenBoPhan")?.Value ?? "";
            var listTenBoPhan = listTenBoPhanStr.Split(',')
                                                .Select(s => s.Trim())
                                                .Where(s => !string.IsNullOrEmpty(s))
                                                .ToList();

            // --- TRUY VẤN DỮ LIỆU CƠ BẢN ---
            IQueryable<E_Form_Best.Models.ITForm.FormHr> query = _context.FormHrs
                .Include(f => f.HrNguoiXacNhans).ThenInclude(xn => xn.IdnguoiXacNhanNavigation)
                .Include(f => f.HrQuanLyDuyetB2s)
                .Include(f => f.BaoVeHrs)
                .Include(f => f.HrCtNguoiHoTros).ThenInclude(ct => ct.IdHrNguoiHoTroNavigation);

            if (!User.IsInRole("All"))
            {
                if (!string.IsNullOrEmpty(tenCongTy))
                {
                    query = query.Where(f => f.TenCongTy == tenCongTy);
                }

                if (User.IsInRole("QuanLyDuyetDonHR"))
                {
                    // Lưu ý quan trọng: EF Core không dịch trực tiếp StringComparison.OrdinalIgnoreCase ra SQL được.
                    // Thông thường EF Core chạy trên SQL Server đã mặc định cấu hình không phân biệt hoa thường (Case-Insensitive).
                    // Vì vậy ở tầng IQueryable này, ta dùng Equals/Contains thông thường hoặc EF.Functions.Like nếu cần ép luật.
                    if (listTenBoPhan.Any())
                    {
                        query = query.Where(f => f.BoPhan != null && listTenBoPhan.Contains(f.BoPhan.Trim()));
                    }
                    else if (!string.IsNullOrEmpty(phongBan))
                    {
                        query = query.Where(f => f.BoPhan != null && f.BoPhan.Trim() == phongBan);
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

            var danhSachDon = await query.OrderByDescending(f => f.Id).AsNoTracking().ToListAsync();

            var result = danhSachDon.Select(item =>
            {
                // Gán vào biến trung gian an toàn để tránh null reference
                var b2List = item.HrQuanLyDuyetB2s ?? Enumerable.Empty<E_Form_Best.Models.ITForm.HrQuanLyDuyetB2>();
                var gdList = item.HrNguoiXacNhans ?? Enumerable.Empty<E_Form_Best.Models.ITForm.HrNguoiXacNhan>();
                var supportList = item.HrCtNguoiHoTros ?? Enumerable.Empty<E_Form_Best.Models.ITForm.HrCtNguoiHoTro>();

                // Logic B2 (Thực hiện trên Memory sau khi ToListAsync nên dùng tốt StringComparison)
                bool hasB2 = b2List.Any();
                bool checkB2Approved = true;
                if (hasB2)
                {
                    string type = b2List.FirstOrDefault()?.Loai ?? "AND";
                    bool isOrType = string.Equals(type, "OR", StringComparison.OrdinalIgnoreCase) ||
                                    string.Equals(type, "ANY", StringComparison.OrdinalIgnoreCase);

                    checkB2Approved = isOrType ? b2List.Any(x => x.TrangThaiXacNhan == 1) : b2List.All(x => x.TrangThaiXacNhan == 1);
                }
                bool checkB2Rejected = hasB2 && b2List.Any(x => x.TrangThaiXacNhan == 2);

                // Logic Giám đốc
                bool hasGD = gdList.Any();
                bool isGDApproved = hasGD && gdList.All(x => x.TrangThaiXacNhan == 1);
                bool isGDRejected = hasGD && gdList.Any(x => x.TrangThaiXacNhan == 2);

                // Trạng thái chung
                bool isCancelled = (item.TenForm ?? "").Contains("[ĐÃ HỦY]", StringComparison.OrdinalIgnoreCase);
                bool isFinished = item.IdAdmin != null;
                bool isManagerApproved = item.IdNguoiDuyet != null;

                string pWidth, pColor, pText, bg, fg, statusTag;

                if (isCancelled || checkB2Rejected || isGDRejected)
                {
                    pWidth = "100%"; pColor = "#ef4444"; pText = "ĐÃ HỦY/TỪ CHỐI"; bg = "#fee2e2"; fg = "#b91c1c"; statusTag = "ĐÃ HỦY";
                }
                else if (isFinished)
                {
                    pWidth = "100%"; pColor = "#10b981"; pText = "HOÀN TẤT"; bg = "#d1fae5"; fg = "#047857"; statusTag = "HOÀN TẤT";
                }
                else if (!isManagerApproved)
                {
                    pWidth = "20%"; pColor = "#f59e0b"; pText = "CHỜ QL DUYỆT"; bg = "#fef3c7"; fg = "#b45309"; statusTag = "CHỜ QL";
                }
                else if (hasB2 && !checkB2Approved)
                {
                    pWidth = "40%"; pColor = "#0ea5e9"; pText = "BP XÁC NHẬN"; bg = "#e0f2fe"; fg = "#0369a1"; statusTag = "BP XÁC NHẬN";
                }
                else if (hasGD && !isGDApproved)
                {
                    pWidth = "65%"; pColor = "#d946ef"; pText = "GIÁM ĐỐC"; bg = "#fdf4ff"; fg = "#86198f"; statusTag = "GIÁM ĐỐC";
                }
                else
                {
                    pWidth = "85%"; pColor = "#2563eb"; pText = "CHỜ ADMIN"; bg = "#dbeafe"; fg = "#1e40af"; statusTag = "ADMIN";
                }

                var supportLog = supportList.OrderByDescending(x => x.Stt).FirstOrDefault();
                string supportName = supportLog?.IdHrNguoiHoTroNavigation?.Ten ?? "Chưa chỉ định";

                // Loại bỏ cụm "[ĐÃ HỦY]" không phân biệt chữ hoa/thường bằng Replace trong .NET Core
                string responseTenForm = (item.TenForm ?? "");
                responseTenForm = responseTenForm.Replace("[ĐÃ HỦY]", "", StringComparison.OrdinalIgnoreCase).Trim();

                return new
                {
                    item.Id,
                    TenNguoiNv = item.TenNguoiNv ?? "N/A",
                    BoPhan = item.BoPhan ?? "N/A",
                    SoNhanVien = item.SoNhanVien ?? "N/A",
                    TenForm = responseTenForm,
                    TimeNguoiTao = item.TimeNguoiTao?.ToString("dd/MM/yyyy") ?? "--",
                    HasB2 = hasB2,
                    B2ApprovedCount = b2List.Count(x => x.TrangThaiXacNhan == 1),
                    B2TotalCount = b2List.Count(),
                    HasGD = hasGD,
                    GDApprovedCount = gdList.Count(x => x.TrangThaiXacNhan == 1),
                    GDTotalCount = gdList.Count(),
                    SupportName = supportName,
                    PWidth = pWidth,
                    PColor = pColor,
                    PText = pText,
                    Bg = bg,
                    Fg = fg,
                    StatusTag = statusTag,
                    IdNguoiDuyet = item.IdNguoiDuyet
                };
            });

            return Json(result);
        }

        // --- HÀM XỬ LÝ ĐƠN POST CỦA BẠN ĐƯỢC GIỮ NGUYÊN 100% ---
        [HttpPost("/FormHR/XuLyDon")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> XuLyDon([FromBody] HRApprovalRequest request)
        {
            // 1. Kiểm tra request
            if (request == null || request.Id <= 0)
                return Json(new { success = false, message = "Dữ liệu không hợp lệ." });

            var userIdStr = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userIdStr))
                return Json(new { success = false, message = "Hết phiên đăng nhập." });

            int userId = int.Parse(userIdStr);
            var userName = User.FindFirst(System.Security.Claims.ClaimTypes.Name)?.Value ?? "N/A";
            var userEmail = User.FindFirst(System.Security.Claims.ClaimTypes.Email)?.Value?.Trim().ToLower() ?? "";
            var tenCongTyUser = User.FindFirst("TenCongTy")?.Value?.Trim() ?? "";
            var phongBanUser = User.FindFirst("PhongBan")?.Value ?? "N/A";

            // 2. Load đơn kèm theo các bảng cần thiết để kiểm tra logic
            var form = await _context.FormHrs
                .Include(f => f.HrCtNguoiHoTros)
                    .ThenInclude(ct => ct.IdHrNguoiHoTroNavigation)
                .FirstOrDefaultAsync(f => f.Id == request.Id);

            if (form == null) return Json(new { success = false, message = "Không tìm thấy đơn." });

            // 3. Kiểm tra phân quyền công ty (Chỉ cho phép nếu là Admin hệ thống hoặc cùng công ty)
            if (!User.IsInRole("All") && form.TenCongTy?.Trim() != tenCongTyUser)
            {
                return Json(new { success = false, message = "Bạn không có quyền thao tác trên đơn của công ty khác." });
            }

            // 4. Sử dụng Transaction để đảm bảo toàn vẹn dữ liệu
            using (var transaction = await _context.Database.BeginTransactionAsync())
            {
                try
                {
                    DateTime now = DateTime.Now;
                    string tieuDeLichSu = "";
                    string moTaChiTiet = "";

                    // --- XỬ LÝ DUYỆT ---
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
                    // --- XỬ LÝ HỦY ---
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
                    // --- XỬ LÝ HOÀN TẤT ---
                    else if (request.Action == "HoanTat")
                    {
                        bool isAdmin = User.IsInRole("All") || User.IsInRole("AdminHR");
                        bool isSupporter = form.HrCtNguoiHoTros.Any(ct => ct.IdHrNguoiHoTroNavigation?.MaNv != null && ct.IdHrNguoiHoTroNavigation.MaNv.ToLower() == userEmail);

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

                    // 5. Ghi lịch sử với TrangThaiAnHien = true để hiển thị trên giao diện
                    _context.LichSuFormHrs.Add(new LichSuFormHr
                    {
                        IdFormHr = form.Id,
                        TieuDe = tieuDeLichSu,
                        Mota = moTaChiTiet,
                        Time = now,
                        IsRead = false,
                        TrangThaiAnHien = true
                    });

                    // 6. Cập nhật và lưu thay đổi
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

        // Định nghĩa class Request (nếu chưa có)
        public class HRApprovalRequest
        {
            public int Id { get; set; }
            public string Action { get; set; } = string.Empty;
            public string Reason { get; set; } = string.Empty;
        }
        #endregion

        #region XỬ LÝ ĐƠN HR B2 - PHÂN QUYỀN MỚI 2026

        // 1. TRẢ VỀ VIEW RỖNG (Cho JavaScript tự Render)
        [HttpGet("/FormHR/QuanLyXetDuyetB2")]
        public IActionResult QuanLyXetDuyetB2()
        {
            var userIdStr = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userIdStr)) return Redirect("/DonXetDuyet/DangNhap");
            return View();
        }

        // 2. API TRẢ VỀ JSON ĐÃ TÍNH TOÁN LOGIC (100% JS)
        [HttpGet("/FormHR/GetQuanLyXetDuyetB2Data")]
        public async Task<IActionResult> GetQuanLyXetDuyetB2Data()
        {
            var userIdStr = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userIdStr)) return Unauthorized();

            int userId = int.Parse(userIdStr);
            var userEmail = (User.FindFirst(System.Security.Claims.ClaimTypes.Email)?.Value ?? "").Trim().ToLower();
            var tenCongTy = User.FindFirst("TenCongTy")?.Value?.Trim() ?? "";

            bool isAll = User.IsInRole("All");
            bool isQuanLyB2 = User.IsInRole("QuanLyDuyetDonHR_B2");

            IQueryable<E_Form_Best.Models.ITForm.FormHr> query = _context.FormHrs
                .Include(f => f.HrNguoiXacNhans).ThenInclude(xn => xn.IdnguoiXacNhanNavigation)
                .Include(f => f.HrQuanLyDuyetB2s).ThenInclude(b2 => b2.HrQuanLyDuyetB2UyQuyens) // Đã Include thêm bảng Ủy quyền
                .Include(f => f.BaoVeHrs)
                .Include(f => f.HrCtNguoiHoTros).ThenInclude(ct => ct.IdHrNguoiHoTroNavigation);

            // Điều kiện tiên quyết: Đã qua Quản lý trực tiếp
            query = query.Where(f => f.IdNguoiDuyet != null);

            // Lọc theo quyền (B2) - ĐÃ CẬP NHẬT KIỂM TRA ỦY QUYỀN
            query = query.Where(f =>
                isAll ||
                (isQuanLyB2 && f.HrQuanLyDuyetB2s.Any(b2 =>
                    (b2.MaNguoiXacNhan != null && b2.MaNguoiXacNhan.ToLower() == userEmail) ||
                    b2.IdnguoiXacNhan == userId ||
                    // Kiểm tra danh sách ủy quyền nếu userEmail trùng với MaNvuyQuyen
                    b2.HrQuanLyDuyetB2UyQuyens.Any(uq => uq.MaNvuyQuyen != null && uq.MaNvuyQuyen.ToLower() == userEmail)
                ))
            );

            // Lọc công ty
            if (!isAll && !string.IsNullOrEmpty(tenCongTy))
            {
                query = query.Where(f => f.TenCongTy == tenCongTy);
            }

            var danhSachDon = await query.OrderByDescending(f => f.Id).AsNoTracking().ToListAsync();

            // Tính toán trước toàn bộ Giao diện / Logic từ Server
            var result = danhSachDon.Select(item =>
            {
                bool isCancelled = (item.TenForm ?? "").Contains("[ĐÃ HỦY]");
                bool isFinished = item.IdAdmin != null;

                var b2List = item.HrQuanLyDuyetB2s ?? new List<E_Form_Best.Models.ITForm.HrQuanLyDuyetB2>();
                bool hasB2 = b2List.Any();

                // Logic checkB2Approved AND/OR
                bool isB2Approved = true;
                if (hasB2)
                {
                    string type = b2List.FirstOrDefault()?.Loai?.ToUpper() ?? "AND";
                    if (type == "OR" || type == "ANY") isB2Approved = b2List.Any(x => x.TrangThaiXacNhan == 1);
                    else isB2Approved = b2List.All(x => x.TrangThaiXacNhan == 1);
                }

                bool isB2Rejected = hasB2 && b2List.Any(x => x.TrangThaiXacNhan == 2);
                bool isB2Processing = hasB2 && !isB2Approved && !isB2Rejected;

                var gdList = item.HrNguoiXacNhans ?? new List<E_Form_Best.Models.ITForm.HrNguoiXacNhan>();
                bool hasGD = gdList.Any();
                bool isGDApproved = hasGD && gdList.All(x => x.TrangThaiXacNhan == 1);
                bool isGDRejected = hasGD && gdList.Any(x => x.TrangThaiXacNhan == 2);

                string pWidth, pColor, pText, bg, fg, statusTag;

                if (isCancelled || isB2Rejected || isGDRejected)
                {
                    pWidth = "100%"; pColor = "#ef4444"; pText = "ĐÃ HỦY/TỪ CHỐI"; bg = "#fee2e2"; fg = "#b91c1c"; statusTag = "ĐÃ HỦY";
                }
                else if (isFinished)
                {
                    pWidth = "100%"; pColor = "#10b981"; pText = "HOÀN TẤT"; bg = "#d1fae5"; fg = "#047857"; statusTag = "HOÀN TẤT";
                }
                else if (isB2Processing)
                {
                    pWidth = "40%"; pColor = "#0ea5e9"; pText = "CHỜ B2 XÁC NHẬN"; bg = "#e0f2fe"; fg = "#0369a1"; statusTag = "BP XÁC NHẬN";
                }
                else if (hasGD && !isGDApproved)
                {
                    pWidth = "65%"; pColor = "#d946ef"; pText = "CHỜ GIÁM ĐỐC"; bg = "#fdf4ff"; fg = "#86198f"; statusTag = "GIÁM ĐỐC";
                }
                else
                {
                    pWidth = "85%"; pColor = "#2563eb"; pText = "CHỜ ADMIN"; bg = "#eff6ff"; fg = "#1e40af"; statusTag = "ADMIN";
                }

                var supportLog = item.HrCtNguoiHoTros?.OrderByDescending(x => x.Stt).FirstOrDefault();
                string supportName = supportLog?.IdHrNguoiHoTroNavigation?.Ten ?? "Chưa chỉ định";

                return new
                {
                    Id = item.Id,
                    TenNguoiNv = item.TenNguoiNv ?? "N/A",
                    BoPhan = item.BoPhan ?? "N/A",
                    SoNhanVien = item.SoNhanVien ?? "N/A",
                    TenForm = (item.TenForm ?? "").Replace("[ĐÃ HỦY]", "").Trim(),
                    TimeNguoiTao = item.TimeNguoiTao?.ToString("dd/MM/yyyy") ?? "--",
                    HasB2 = hasB2,
                    B2DuyetCount = hasB2 ? b2List.Count(x => x.TrangThaiXacNhan != 0) : 0,
                    B2TotalCount = hasB2 ? b2List.Count : 0,
                    HasGD = hasGD,
                    GDDuyetCount = hasGD ? gdList.Count(x => x.TrangThaiXacNhan == 1) : 0,
                    GDTotalCount = hasGD ? gdList.Count : 0,
                    SupportName = supportName,
                    PWidth = pWidth,
                    PColor = pColor,
                    PText = pText,
                    Bg = bg,
                    Fg = fg,
                    StatusTag = statusTag,
                    IsB2Processing = isB2Processing
                };
            });

            return Json(result);
        }


        #region DUYỆT BƯỚC 2 (HR_QuanLyDuyetB2)

        public class DuyetB2Request
        {
            public int idB2 { get; set; }
            public int idForm { get; set; }
            public string loaiHanhDong { get; set; } = ""; // Approve / Reject
        }

        [HttpPost("/FormHR/DuyetB2")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DuyetB2([FromBody] DuyetB2Request req)
        {
            // 0. Kiểm tra đăng nhập
            var userEmail = User.FindFirst(System.Security.Claims.ClaimTypes.Email)?.Value?.Trim() ?? "";
            var userName = User.Identity?.Name ?? "";

            if (string.IsNullOrEmpty(userEmail))
                return Json(new { success = false, message = "⚠️ Phiên đăng nhập hết hạn!" });

            // 1. Kiểm tra tính hợp lệ của request
            if (req == null || req.idB2 <= 0 || req.idForm <= 0 || string.IsNullOrEmpty(req.loaiHanhDong))
                return Json(new { success = false, message = "⚠️ Dữ liệu gửi lên không hợp lệ." });

            try
            {
                // 2. Load record B2 và đơn liên quan, bao gồm danh sách Ủy quyền
                var record = await _context.HrQuanLyDuyetB2s
                    .Include(x => x.IdFormHrNavigation)
                    .Include(x => x.HrQuanLyDuyetB2UyQuyens)
                    .FirstOrDefaultAsync(x => x.Id == req.idB2);

                if (record == null)
                    return Json(new { success = false, message = "⚠️ Không tìm thấy bản ghi duyệt (B2)." });

                if (record.IdFormHr != req.idForm)
                    return Json(new { success = false, message = "⚠️ Bản ghi không thuộc đơn này." });

                var don = record.IdFormHrNavigation;
                if (don == null)
                    return Json(new { success = false, message = "⚠️ Không tìm thấy đơn liên quan." });

                // --- BỔ SUNG: KIỂM TRA ĐIỀU KIỆN KHÔNG NULL ---
                // Chỉ cho phép thao tác khi IdNguoiDuyet đã được gán giá trị hợp lệ
                if (don.IdNguoiDuyet == null || don.IdNguoiDuyet == 0 || string.IsNullOrEmpty(don.TenNguoiDuyet))
                {
                    return Json(new { success = false, message = "⚠️ Đơn chưa được cấu hình người duyệt, không thể thao tác!" });
                }

                // 3. Kiểm tra trạng thái: Chỉ xử lý nếu đang chờ (0 hoặc null)
                if ((record.TrangThaiXacNhan ?? 0) != 0)
                    return Json(new { success = false, message = "⚠️ Bước này đã được xử lý trước đó rồi." });

                // 4. KIỂM TRA QUYỀN (So sánh qua Mã nhân viên HOẶC người được Ủy quyền)
                var assignedMa = (record.MaNguoiXacNhan ?? "").Trim();

                bool isRightUser = !string.IsNullOrEmpty(assignedMa) &&
                                   string.Equals(assignedMa, userEmail, StringComparison.OrdinalIgnoreCase);

                // Kiểm tra danh sách ủy quyền (Delegate)
                bool isNguoiDuocUyQuyen = record.HrQuanLyDuyetB2UyQuyens.Any(uq =>
                                          !string.IsNullOrEmpty(uq.MaNvuyQuyen) &&
                                          string.Equals(uq.MaNvuyQuyen.Trim(), userEmail, StringComparison.OrdinalIgnoreCase));

                if (!isRightUser && !isNguoiDuocUyQuyen)
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

                // GÁN GHI CHÚ MẶC ĐỊNH
                record.GhiChu = isApprove ? "Đã duyệt" : "Từ chối";

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
                    moTaLS = $"{userName} ({userEmail}) đã TỪ CHỐI duyệt bước 2.";
                }
                else // Hành động: DUYỆT (Approve)
                {
                    tieuDeLS = "BƯỚC 2 — ĐÃ DUYỆT";
                    moTaLS = $"{userName} ({userEmail}) đã DUYỆT bước 2 (thứ tự: {record.ThuTuXacNhan}).";

                    // Kiểm tra quy tắc AND/OR
                    string type = record.Loai?.ToUpper() ?? "AND";
                    bool conAiChuaDuyet = false;

                    if (type != "OR" && type != "ANY")
                    {
                        conAiChuaDuyet = await _context.HrQuanLyDuyetB2s
                            .AnyAsync(x => x.IdFormHr == req.idForm
                                        && x.Id != record.Id
                                        && x.ThuTuXacNhan == record.ThuTuXacNhan
                                        && (x.TrangThaiXacNhan == null || x.TrangThaiXacNhan == 0));
                    }

                    if (!conAiChuaDuyet)
                    {
                        don.TrangThai = "DaDuyet";
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

                    // Lịch sử hiển thị công khai (TrangThaiAnHien = true)
                    _context.LichSuFormHrs.Add(new LichSuFormHr
                    {
                        IdFormHr = don.Id,
                        TieuDe = tieuDeLS,
                        Mota = moTaLS,
                        Time = DateTime.Now,
                        IsRead = false,
                        TrangThaiAnHien = true
                    });

                    // Lịch sử ẩn nếu là người ủy quyền thao tác
                    if (isNguoiDuocUyQuyen)
                    {
                        _context.LichSuFormHrs.Add(new LichSuFormHr
                        {
                            IdFormHr = don.Id,
                            TieuDe = "SYSTEM: LƯU VẾT ỦY QUYỀN",
                            Mota = $"Người được ủy quyền [{userName}] đã thao tác {(isApprove ? "DUYỆT" : "TỪ CHỐI")} thay cho quản lý [{record.TenNguoiXacNhan}].",
                            Time = DateTime.Now,
                            IsRead = true,
                            TrangThaiAnHien = false
                        });
                    }

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

        // 1. TRẢ VỀ VIEW RỖNG (Cho JavaScript tự Render)
        [HttpGet("/FormHR/PheDuyetCapCao")]
        public IActionResult PheDuyetCapCao()
        {
            if (User == null || User.Identity == null || !User.Identity.IsAuthenticated)
            {
                return Redirect("/DonXetDuyet/DangNhap");
            }

            bool isAll = User.IsInRole("All");
            bool isGiamDoc = User.IsInRole("GiamDocHR");

            if (!isAll && !isGiamDoc)
            {
                return Content("Tài khoản của bạn không có quyền truy cập trang phê duyệt cấp cao.");
            }

            var userEmail = (User.FindFirst(System.Security.Claims.ClaimTypes.Email)?.Value
                             ?? User.FindFirst("Email")?.Value ?? "").Trim().ToLower();

            ViewBag.UserEmail = userEmail; // Truyền xuống HTML để xài nếu cần
            return View();
        }

        // 2. API TRẢ VỀ DỮ LIỆU JSON ĐÃ ĐƯỢC TÍNH TOÁN (100% JS)
        [HttpGet("/FormHR/GetPheDuyetCapCaoData")]
        public async Task<IActionResult> GetPheDuyetCapCaoData()
        {
            var userEmail = (User.FindFirst(System.Security.Claims.ClaimTypes.Email)?.Value
                             ?? User.FindFirst("Email")?.Value ?? "").Trim().ToLower();
            var userIdStr = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
            int userId = string.IsNullOrEmpty(userIdStr) ? 0 : int.Parse(userIdStr);

            bool isAll = User.IsInRole("All");
            bool isGiamDoc = User.IsInRole("GiamDocHR");

            if (!isAll && !isGiamDoc) return Unauthorized();

            // Khởi tạo Query và nạp dữ liệu liên quan (Bổ sung HrCtNguoiHoTros để hiển thị Support)
            IQueryable<E_Form_Best.Models.ITForm.FormHr> query = _context.FormHrs
                .Include(f => f.HrNguoiXacNhans).ThenInclude(x => x.IdnguoiXacNhanNavigation)
                .Include(f => f.HrQuanLyDuyetB2s)
                .Include(f => f.BaoVeHrs)
                .Include(f => f.HrCtNguoiHoTros).ThenInclude(ct => ct.IdHrNguoiHoTroNavigation);

            // ĐIỀU KIỆN LỌC CHUNG
            // - Đã được Quản lý trực tiếp ký duyệt
            // - Đã qua bước duyệt B2 (Logic AND / OR nguyên bản)
            query = query.Where(f => f.IdNguoiDuyet != null &&
                (
                    !f.HrQuanLyDuyetB2s.Any() ||
                    (f.HrQuanLyDuyetB2s.Any(b2 => b2.Loai == "OR" || b2.Loai == "ANY") && f.HrQuanLyDuyetB2s.Any(q => q.TrangThaiXacNhan == 1)) ||
                    (!f.HrQuanLyDuyetB2s.Any(b2 => b2.Loai == "OR" || b2.Loai == "ANY") && f.HrQuanLyDuyetB2s.All(q => q.TrangThaiXacNhan != 0))
                )
            );

            // PHÂN QUYỀN HIỂN THỊ
            query = query.Where(f =>
                isAll ||
                (isGiamDoc && f.HrNguoiXacNhans.Any(xn =>
                    (xn.MaNguoiXacNhan != null && xn.MaNguoiXacNhan.ToLower() == userEmail) ||
                    xn.IdnguoiXacNhan == userId))
            );

            var danhSachDon = await query.OrderByDescending(f => f.Id).AsNoTracking().ToListAsync();

            // TÍNH TOÁN LOGIC TẠI SERVER
            var result = danhSachDon.Select(item =>
            {
                bool isCancelled = (item.TenForm ?? "").Contains("[ĐÃ HỦY]");
                bool isFinished = item.IdAdmin != null;

                var currentNguoiXN = item.HrNguoiXacNhans?.FirstOrDefault(x => x.MaNguoiXacNhan != null && x.MaNguoiXacNhan.ToLower() == userEmail);
                bool isNeedAction = currentNguoiXN != null && currentNguoiXN.TrangThaiXacNhan == 0 && !isCancelled;
                bool isProcessed = currentNguoiXN != null && currentNguoiXN.TrangThaiXacNhan != 0;

                string pWidth, pColor, pText, bg, fg, statusTag;
                if (isCancelled)
                {
                    pWidth = "100%"; pColor = "#ef4444"; pText = "ĐÃ HỦY"; bg = "#fee2e2"; fg = "#b91c1c"; statusTag = "ĐÃ HỦY";
                }
                else if (isFinished)
                {
                    pWidth = "100%"; pColor = "#10b981"; pText = "HOÀN TẤT"; bg = "#d1fae5"; fg = "#047857"; statusTag = "HOÀN TẤT";
                }
                else if (isNeedAction)
                {
                    pWidth = "66%"; pColor = "#2563eb"; pText = "CẦN PHÊ DUYỆT"; bg = "#eff6ff"; fg = "#2563eb"; statusTag = "CHỜ DUYỆT";
                }
                else if (isProcessed)
                {
                    pWidth = "85%"; pColor = "#f59e0b"; pText = "ĐÃ XỬ LÝ (CHỜ ADMIN)"; bg = "#fff7ed"; fg = "#c2410c"; statusTag = "ĐÃ XỬ LÝ";
                }
                else
                {
                    pWidth = "33%"; pColor = "#64748b"; pText = "ĐANG LUÂN CHUYỂN"; bg = "#f1f5f9"; fg = "#475569"; statusTag = "LUÂN CHUYỂN";
                }

                var supportLog = item.HrCtNguoiHoTros?.OrderByDescending(x => x.Stt).FirstOrDefault();
                string supportName = supportLog?.IdHrNguoiHoTroNavigation?.Ten ?? "Chưa chỉ định";

                return new
                {
                    Id = item.Id,
                    TenNguoiNv = item.TenNguoiNv ?? "N/A",
                    BoPhan = item.BoPhan ?? "N/A",
                    SoNhanVien = item.SoNhanVien ?? "N/A",
                    TenForm = (item.TenForm ?? "").Replace("[ĐÃ HỦY]", "").Trim(),
                    TimeNguoiTao = item.TimeNguoiTao?.ToString("dd/MM/yyyy") ?? "--",
                    HasGD = item.HrNguoiXacNhans != null && item.HrNguoiXacNhans.Any(),
                    GDDone = item.HrNguoiXacNhans != null ? item.HrNguoiXacNhans.Count(x => x.TrangThaiXacNhan == 1) : 0,
                    GDTotal = item.HrNguoiXacNhans != null ? item.HrNguoiXacNhans.Count() : 0,
                    SupportName = supportName,
                    PWidth = pWidth,
                    PColor = pColor,
                    PText = pText,
                    Bg = bg,
                    Fg = fg,
                    StatusTag = statusTag,
                    IsNeedAction = isNeedAction,
                    IsProcessed = isProcessed,
                    IsFinished = isFinished,
                    IsCancelled = isCancelled
                };
            });

            return Json(result);
        }

        // --- HÀM POST XỬ LÝ ĐƠN GIỮ NGUYÊN 100% KHÔNG THAY ĐỔI DÙ CHỈ 1 KÝ TỰ ---
        /// <summary>
        /// POST: /FormHR/GiamDocPheDuyet
        /// Giám đốc hoặc Admin phê duyệt/từ chối đơn HR
        /// </summary>
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

                    // 8. GHI LỊCH SỬ THAO TÁC (Thêm TrangThaiAnHien = true)
                    var lichSu = new LichSuFormHr
                    {
                        IdFormHr = idForm,
                        TieuDe = $"CẤP CAO {hanhDongStr.ToUpper()}",
                        Mota = $"Nhân sự thực hiện: {userName} ({userEmail}). " +
                               $"Bộ phận: {phongBan}. " +
                               $"Nội dung: Đã {hanhDongStr.ToLower()} yêu cầu xác nhận cấp cao. " +
                               $"Ghi chú: {(string.IsNullOrWhiteSpace(lyDo) ? "Không có" : lyDo)}",
                        Time = now,
                        IsRead = false,
                        TrangThaiAnHien = true // Đảm bảo hiển thị trên log
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

        // 1. TRẢ VỀ VIEW RỖNG (Cho JavaScript tự Render)
        [HttpGet("/FormHR/HoanTatDon")]
        public IActionResult HoanTatDon()
        {
            var userIdStr = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value
                            ?? User.FindFirst("UserId")?.Value;

            if (string.IsNullOrEmpty(userIdStr)) return Redirect("/DonXetDuyet/DangNhap");

            return View();
        }

        // 2. API TRẢ VỀ DỮ LIỆU JSON ĐÃ ĐƯỢC TÍNH TOÁN LOGIC TỪ SERVER
        [HttpGet("/FormHR/GetHoanTatDonData")]
        public async Task<IActionResult> GetHoanTatDonData()
        {
            var userIdStr = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value
                            ?? User.FindFirst("UserId")?.Value;
            if (string.IsNullOrEmpty(userIdStr)) return Unauthorized();

            int userId = int.Parse(userIdStr);
            // Giữ nguyên chuỗi gốc, loại bỏ ToLower() ở đây để tránh tạo chuỗi thừa, việc so sánh sẽ dùng StringComparison sau
            var userEmail = (User.FindFirst(System.Security.Claims.ClaimTypes.Email)?.Value ?? "").Trim();
            var userRoles = User.FindAll(System.Security.Claims.ClaimTypes.Role).Select(c => c.Value).ToList();
            var phongBanSession = User.FindFirst("PhongBan")?.Value?.Trim() ?? "";
            var tenCongTy = User.FindFirst("TenCongTy")?.Value?.Trim() ?? "";

            var listTenBoPhanStr = User.FindFirst("TenBoPhan")?.Value ?? "";
            var listTenBoPhan = listTenBoPhanStr.Split(',')
                                                .Select(s => s.Trim())
                                                .Where(s => !string.IsNullOrEmpty(s))
                                                .ToList();

            bool isAll = userRoles.Contains("All");
            bool isAdminHR = userRoles.Contains("AdminHR");
            bool isQuanLyB2 = userRoles.Contains("QuanLyDuyetDonHR_B2");
            bool isQuanLyDuyet = userRoles.Contains("QuanLyDuyetDonHR");
            bool isGiamDocHR = userRoles.Contains("GiamDocHR");

            // --- TRUY VẤN DỮ LIỆU CƠ BẢN ---
            IQueryable<E_Form_Best.Models.ITForm.FormHr> query = _context.FormHrs
                .Include(f => f.HrNguoiXacNhans).ThenInclude(xn => xn.IdnguoiXacNhanNavigation)
                .Include(f => f.HrQuanLyDuyetB2s)
                .Include(f => f.BaoVeHrs)
                .Include(f => f.HrCtNguoiHoTros).ThenInclude(ct => ct.IdHrNguoiHoTroNavigation);

            query = query.Where(f => f.IdNguoiDuyet != null && f.TenNguoiDuyet != null && f.TimeNguoiDuyet != null);

            // --- LOGIC PHÂN QUYỀN AN TOÀN ---
            // Lưu ý: Các biểu thức trong tầng IQueryable này sẽ được EF Core dịch sang SQL.
            // SQL Server mặc định không phân biệt hoa thường (Case-Insensitive), do đó ta lược bỏ .ToLower() và .Trim() 
            // để giúp EF Core sinh câu lệnh SQL tối ưu nhất và tận dụng được Index của Database.
            query = query.Where(f =>
                isAll || isAdminHR ||
                (isQuanLyB2 && (f.HrQuanLyDuyetB2s ?? Enumerable.Empty<E_Form_Best.Models.ITForm.HrQuanLyDuyetB2>()).Any(b2 => b2.IdnguoiXacNhan == userId || (b2.MaNguoiXacNhan != null && b2.MaNguoiXacNhan == userEmail))) ||
                (isGiamDocHR && (f.HrNguoiXacNhans ?? Enumerable.Empty<E_Form_Best.Models.ITForm.HrNguoiXacNhan>()).Any(xn => xn.IdnguoiXacNhan == userId || (xn.MaNguoiXacNhan != null && xn.MaNguoiXacNhan == userEmail))) ||
                (isQuanLyDuyet && (f.IdNguoiTao == userId || (listTenBoPhan.Any() && f.BoPhan != null && listTenBoPhan.Contains(f.BoPhan.Trim())) || (!listTenBoPhan.Any() && !string.IsNullOrEmpty(phongBanSession) && f.BoPhan != null && f.BoPhan.Trim() == phongBanSession))) ||
                (f.IdNguoiTao == userId || (f.HrCtNguoiHoTros ?? Enumerable.Empty<E_Form_Best.Models.ITForm.HrCtNguoiHoTro>()).Any(ct => ct.IdHrNguoiHoTroNavigation != null && ct.IdHrNguoiHoTroNavigation.MaNv != null && ct.IdHrNguoiHoTroNavigation.MaNv == userEmail))
            );

            if (!isAll && !isAdminHR && !string.IsNullOrEmpty(tenCongTy))
            {
                query = query.Where(f => f.TenCongTy == tenCongTy);
            }

            var danhSachDon = await query.OrderByDescending(f => f.Id).AsNoTracking().ToListAsync();

            // --- XỬ LÝ TRÊN MEMORY (LINQ to Objects) ---
            var result = danhSachDon.Select(item =>
            {
                var b2List = item.HrQuanLyDuyetB2s ?? Enumerable.Empty<E_Form_Best.Models.ITForm.HrQuanLyDuyetB2>();
                var gdList = item.HrNguoiXacNhans ?? Enumerable.Empty<E_Form_Best.Models.ITForm.HrNguoiXacNhan>();
                var hoTroList = item.HrCtNguoiHoTros ?? Enumerable.Empty<E_Form_Best.Models.ITForm.HrCtNguoiHoTro>();

                // Sử dụng StringComparison.OrdinalIgnoreCase cho các thao tác so sánh chuỗi trên bộ nhớ
                bool isCancelled = (item.TenForm ?? "").Contains("[ĐÃ HỦY]", StringComparison.OrdinalIgnoreCase) ||
                                   string.Equals(item.TrangThai, "DaHuy", StringComparison.OrdinalIgnoreCase);

                bool isFinished = item.IdAdmin != null ||
                                  string.Equals(item.TrangThai, "HoanTat", StringComparison.OrdinalIgnoreCase);

                bool isManagerApproved = item.IdNguoiDuyet != null;

                bool hasB2 = b2List.Any();
                string b2Type = hasB2 ? (b2List.FirstOrDefault()?.Loai ?? "AND") : "AND";

                bool isB2OrType = string.Equals(b2Type, "OR", StringComparison.OrdinalIgnoreCase) ||
                                  string.Equals(b2Type, "ANY", StringComparison.OrdinalIgnoreCase);

                bool isB2Approved = hasB2 && (isB2OrType ? b2List.Any(x => x.TrangThaiXacNhan == 1) : b2List.All(x => x.TrangThaiXacNhan == 1));
                bool isB2Rejected = hasB2 && b2List.Any(x => x.TrangThaiXacNhan == 2);

                bool hasGD = gdList.Any();
                bool isGDApproved = hasGD && gdList.All(x => x.TrangThaiXacNhan == 1);
                bool isGDRejected = hasGD && gdList.Any(x => x.TrangThaiXacNhan == 2);

                string pWidth, pColor, pText, bg, fg, statusTag;
                if (isCancelled || isB2Rejected || isGDRejected) { pWidth = "100%"; pColor = "#ef4444"; pText = "ĐÃ HỦY/TỪ CHỐI"; bg = "#fee2e2"; fg = "#b91c1c"; statusTag = "ĐÃ HỦY"; }
                else if (isFinished) { pWidth = "100%"; pColor = "#10b981"; pText = "HOÀN TẤT"; bg = "#ecfdf5"; fg = "#047857"; statusTag = "HOÀN TẤT"; }
                else if (!isManagerApproved) { pWidth = "25%"; pColor = "#f59e0b"; pText = "CHỜ QL DUYỆT"; bg = "#fef3c7"; fg = "#b45309"; statusTag = "CHỜ QL"; }
                else if (hasB2 && !isB2Approved) { pWidth = "45%"; pColor = "#0ea5e9"; pText = "BP XÁC NHẬN"; bg = "#e0f2fe"; fg = "#0369a1"; statusTag = "BP XÁC NHẬN"; }
                else if (hasGD && !isGDApproved) { pWidth = "70%"; pColor = "#d946ef"; pText = "GIÁM ĐỐC"; bg = "#fdf4ff"; fg = "#86198f"; statusTag = "GIÁM ĐỐC"; }
                else { pWidth = "85%"; pColor = "#1e40af"; pText = "CHỜ ADMIN"; bg = "#dbeafe"; fg = "#1e40af"; statusTag = "CHỜ ADMIN"; }

                var latestSupport = hoTroList.OrderByDescending(x => x.Stt).FirstOrDefault();

                // Cắt bỏ chuỗi "[ĐÃ HỦY]" không phân biệt hoa thường an toàn bằng StringComparison
                string responseTenForm = (item.TenForm ?? "").Replace("[ĐÃ HỦY]", "", StringComparison.OrdinalIgnoreCase).Trim();

                return new
                {
                    item.Id,
                    item.IdForm,
                    TenNguoiNv = item.TenNguoiNv ?? "N/A",
                    BoPhan = item.BoPhan ?? "N/A",
                    SoNhanVien = item.SoNhanVien ?? "N/A",
                    TenForm = responseTenForm,
                    TimeNguoiTao = item.TimeNguoiTao?.ToString("dd/MM/yyyy") ?? "--",
                    HasB2 = hasB2,
                    B2DoneCount = b2List.Count(x => x.TrangThaiXacNhan == 1),
                    B2TotalCount = b2List.Count(),
                    HasGD = hasGD,
                    GDDoneCount = gdList.Count(x => x.TrangThaiXacNhan == 1),
                    GDTotalCount = gdList.Count(),
                    SupportName = latestSupport?.IdHrNguoiHoTroNavigation?.Ten ?? "N/A",
                    PWidth = pWidth,
                    pColor,
                    pText,
                    bg,
                    fg,
                    statusTag
                };
            });

            return Json(result);
        }

        // --- HÀM POST XÁC NHẬN HOÀN THÀNH - GIỮ NGUYÊN 100% ---
        /// <summary>
        /// POST: /FormHR/XacNhanHoanThanh
        /// Nút bấm dành cho Đội HR - Xác nhận đã xử lý xong các thủ tục/hồ sơ
        /// </summary>
        [HttpPost("/FormHR/XacNhanHoanThanh")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> XacNhanHoanThanh([FromBody] HRCompleteRequest request)
        {
            // 1. Kiểm tra đầu vào
            if (request == null || request.Id <= 0)
                return Json(new { success = false, message = "⚠️ Dữ liệu không hợp lệ." });

            // 2. Thông tin người thao tác
            var userIdStr = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userIdStr))
                return Json(new { success = false, message = "⚠️ Phiên đăng nhập đã hết hạn." });

            int userId = int.Parse(userIdStr);
            var userName = User.FindFirst(System.Security.Claims.ClaimTypes.Name)?.Value ?? "N/A";
            var userEmail = User.FindFirst(System.Security.Claims.ClaimTypes.Email)?.Value?.Trim().ToLower() ?? "";
            var tenCongTyUser = User.FindFirst("TenCongTy")?.Value?.Trim() ?? "";

            if (!User.IsInRole("All") && !User.IsInRole("AdminHR"))
            {
                return Json(new { success = false, message = "🚫 Bạn không có quyền phê duyệt đơn này." });
            }

            // 3. Tìm đơn HR và Include dữ liệu liên quan
            var form = await _context.FormHrs
                .Include(f => f.HrCtNguoiHoTros).ThenInclude(ct => ct.IdHrNguoiHoTroNavigation)
                .Include(f => f.HrDonTiepKhac5s)
                .Include(f => f.HrNguoiXacNhans)
                .Include(f => f.HrQuanLyDuyetB2s)
                .FirstOrDefaultAsync(f => f.Id == request.Id);

            if (form == null)
                return Json(new { success = false, message = "⚠️ Không tìm thấy đơn HR." });

            // 4. KIỂM TRA TRẠNG THÁI XÁC NHẬN CÁC CẤP (Null-safe)
            bool chuaXacNhanNguoi = form.HrNguoiXacNhans?.Any(x => x.TrangThaiXacNhan != 1) ?? false;

            bool chuaXacNhanQuanLy = false;
            if (form.HrQuanLyDuyetB2s != null && form.HrQuanLyDuyetB2s.Any())
            {
                string hinhThucDuyet = form.HrQuanLyDuyetB2s.FirstOrDefault()?.Loai?.ToUpper() ?? "AND";
                if (hinhThucDuyet == "OR" || hinhThucDuyet == "ANY")
                    chuaXacNhanQuanLy = !form.HrQuanLyDuyetB2s.Any(x => x.TrangThaiXacNhan == 1);
                else
                    chuaXacNhanQuanLy = form.HrQuanLyDuyetB2s.Any(x => x.TrangThaiXacNhan != 1);
            }

            if (chuaXacNhanNguoi || chuaXacNhanQuanLy)
            {
                return Json(new { success = false, message = "⚠️ Đơn này chưa được các cấp quản lý xác nhận đủ!" });
            }

            // 5. Logic kiểm tra trùng lịch (Đơn Tiếp Khách)
            HrDonTiepKhac5? chiTietTiepKhac = null;
            if (form.IdForm == "HR_DonTiepKhac_5")
            {
                chiTietTiepKhac = form.HrDonTiepKhac5s?.FirstOrDefault();
                if (chiTietTiepKhac != null && chiTietTiepKhac.ThoiGianBatDau.HasValue && chiTietTiepKhac.ThoiGianKetThuc.HasValue && !string.IsNullOrEmpty(chiTietTiepKhac.TenPhongHop))
                {
                    var danhSachDangOn = await _context.HrDonTiepKhac5s
                        .Where(x => x.Id != chiTietTiepKhac.Id && x.TenPhongHop == chiTietTiepKhac.TenPhongHop && x.TrangThaiPhong == "on")
                        .ToListAsync();

                    bool biTrungTren5Phut = danhSachDangOn.Any(item => {
                        var startMax = chiTietTiepKhac.ThoiGianBatDau > item.ThoiGianBatDau ? chiTietTiepKhac.ThoiGianBatDau!.Value : item.ThoiGianBatDau!.Value;
                        var endMin = chiTietTiepKhac.ThoiGianKetThuc < item.ThoiGianKetThuc ? chiTietTiepKhac.ThoiGianKetThuc!.Value : item.ThoiGianKetThuc!.Value;
                        return startMax < endMin && (endMin - startMax).TotalMinutes > 5;
                    });

                    if (biTrungTren5Phut)
                        return Json(new { success = false, message = $"⚠️ Phòng [{chiTietTiepKhac.TenPhongHop}] đã có lịch 'on' trùng quá 5 phút." });

                    chiTietTiepKhac.TrangThaiPhong = "on";
                }
            }

            // 6. Kiểm tra quyền sở hữu/công ty (Dùng ?? "" để an toàn)
            if (!User.IsInRole("All") && (form.TenCongTy?.Trim() ?? "") != tenCongTyUser)
                return Json(new { success = false, message = "🚫 Bạn không có quyền thao tác trên đơn của công ty khác." });

            // 7. Kiểm tra quyền thực hiện
            bool isAdmin = User.IsInRole("All") || User.IsInRole("AdminHR");
            bool isSupporter = form.HrCtNguoiHoTros?.Any(ct =>
                ct.IdHrNguoiHoTroNavigation?.MaNv != null &&
                ct.IdHrNguoiHoTroNavigation.MaNv.ToLower() == userEmail) ?? false;

            if (!isAdmin && !isSupporter)
                return Json(new { success = false, message = "🚫 Bạn không có quyền hoàn tất đơn này." });

            if (form.TrangThai == "HoanTat" || (form.TenForm ?? "").Contains("[ĐÃ HỦY]"))
                return Json(new { success = false, message = "⚠️ Đơn đã hoàn tất hoặc đã bị hủy trước đó." });

            // 8. Thực hiện cập nhật
            using (var transaction = await _context.Database.BeginTransactionAsync())
            {
                try
                {
                    DateTime now = DateTime.Now;
                    form.IdAdmin = userId;
                    form.TenAdmin = userName;
                    form.TimeAdmin = now;
                    form.TrangThai = "HoanTat";

                    if (chiTietTiepKhac != null) _context.HrDonTiepKhac5s.Update(chiTietTiepKhac);

                    _context.LichSuFormHrs.Add(new LichSuFormHr
                    {
                        IdFormHr = form.Id,
                        TieuDe = "HR Xác nhận Hoàn tất",
                        Mota = $"Nhân sự thực hiện: {userName}. Đơn đã xử lý xong. Phòng: {(chiTietTiepKhac?.TenPhongHop ?? "N/A")}",
                        Time = now,
                        IsRead = false,
                        TrangThaiAnHien = true
                    });

                    _context.FormHrs.Update(form);
                    await _context.SaveChangesAsync();
                    await transaction.CommitAsync();

                    return Json(new { success = true, message = "✅ Xác nhận hoàn tất thành công!" });
                }
                catch (Exception ex)
                {
                    await transaction.RollbackAsync();
                    return Json(new { success = false, message = "❌ Lỗi hệ thống: " + ex.Message });
                }
            }
        }


        public class HRCompleteRequest
        {
            public int Id { get; set; }
            public string? HRNote { get; set; }
        }

        #endregion

        #region QUẢN TRỊ & XUẤT BÁO CÁO HR (CHỈ ĐƠN ĐÃ HOÀN TẤT - FULL 12 LOẠI)

        // 1. TRẢ VỀ VIEW RỖNG (Cho JavaScript tự Render)
        [HttpGet("/FormHR/XuatBaoCao")]
        public IActionResult XuatBaoCao()
        {
            var userIdStr = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value
                            ?? User.FindFirst("UserId")?.Value;

            if (string.IsNullOrEmpty(userIdStr))
                return Redirect("/DonXetDuyet/DangNhap");

            return View();
        }

        // 2. API TRẢ VỀ DỮ LIỆU JSON (100% JS)
        [HttpGet("/FormHR/GetXuatBaoCaoData")]
        public async Task<IActionResult> GetXuatBaoCaoData()
        {
            var userIdStr = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value
                            ?? User.FindFirst("UserId")?.Value;

            if (string.IsNullOrEmpty(userIdStr)) return Unauthorized();

            int userId = int.Parse(userIdStr);
            var userRoles = User.FindAll(System.Security.Claims.ClaimTypes.Role).Select(c => c.Value).ToList();
            var tenCongTy = User.FindFirst("TenCongTy")?.Value?.Trim() ?? "";

            // KHỞI TẠO QUERY
            IQueryable<E_Form_Best.Models.ITForm.FormHr> query = _context.FormHrs
                .Include(f => f.HrNguoiXacNhans)
                .Include(f => f.HrQuanLyDuyetB2s)
                .Include(f => f.BaoVeHrs)
                .Include(f => f.HrCtNguoiHoTros).ThenInclude(ct => ct.IdHrNguoiHoTroNavigation);

            // ĐIỀU KIỆN HIỂN THỊ: CHỈ LẤY ĐƠN ĐÃ ĐƯỢC ADMIN XỬ LÝ XONG
            query = query.Where(f => f.IdAdmin != null && f.TenAdmin != null && f.TimeAdmin != null);

            // PHÂN QUYỀN LỌC DỮ LIỆU
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

            // THỰC THI TRUY VẤN & MAP SANG JSON
            try
            {
                var danhSachDon = await query.OrderByDescending(f => f.TimeAdmin).AsNoTracking().ToListAsync();

                var result = danhSachDon.Select(item => new
                {
                    Id = item.Id,
                    TenNguoiNv = item.TenNguoiNv ?? "N/A",
                    BoPhan = item.BoPhan ?? "N/A",
                    SoNhanVien = item.SoNhanVien ?? "N/A",
                    TenForm = (item.TenForm ?? "").Replace("[ĐÃ HỦY]", "").Trim(),
                    TimeNguoiTao = item.TimeNguoiTao?.ToString("dd/MM/yyyy") ?? "--",
                    HasB2 = item.HrQuanLyDuyetB2s?.Any() == true,
                    HasGD = item.HrNguoiXacNhans?.Any() == true,
                    SupportNames = item.HrCtNguoiHoTros?.Select(s => s.IdHrNguoiHoTroNavigation?.Ten ?? "N/A").ToList() ?? new List<string>(),
                    TenAdmin = item.TenAdmin ?? "N/A",
                    TimeAdmin = item.TimeAdmin?.ToString("dd/MM/yyyy HH:mm") ?? "--",
                    TrangThai = "Hoàn tất"
                });

                return Json(result);
            }
            catch (Exception ex)
            {
                return BadRequest(new { message = "Lỗi hệ thống: " + ex.Message });
            }
        }

        /// <summary>
        /// Xuất dữ liệu ra Excel CHI TIẾT các đơn ĐÃ HOÀN TẤT
        /// </summary>
        [HttpGet("/FormHR/ExportExcelHR")]
        public async Task<IActionResult> ExportExcelHR(DateTime? tuNgay, DateTime? denNgay, string loaiDon)
        {
            // 1. LẤY THÔNG TIN CLAIMS AN TOÀN
            var userIdStr = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value
                            ?? User.FindFirst("UserId")?.Value;
            if (string.IsNullOrEmpty(userIdStr)) return Unauthorized();

            int userId = int.Parse(userIdStr);
            var userRoles = User.FindAll(System.Security.Claims.ClaimTypes.Role).Select(c => c.Value).ToList();
            var tenCongTy = User.FindFirst("TenCongTy")?.Value?.Trim() ?? "";

            // 2. TRUY VẤN DỮ LIỆU
            IQueryable<E_Form_Best.Models.ITForm.FormHr> query = _context.FormHrs
                .Include(f => f.HrNguoiXacNhans).Include(f => f.HrQuanLyDuyetB2s)
                .Include(f => f.HrCtNguoiHoTros).ThenInclude(ct => ct.IdHrNguoiHoTroNavigation)
                .Include(f => f.HrXinRaNgoai1s).Include(f => f.HrMangHangHoaRaCong2s)
                .Include(f => f.HrDangKySuDungXeCongTac3s).Include(f => f.HrDangKySuDungXeDaily4s)
                .Include(f => f.HrDonTiepKhac5s).Include(f => f.HrNhaThauQuaCong6s)
                .Include(f => f.HrHoTroTienDienThoai7s).Include(f => f.HrDoiCaLam8s)
                .Include(f => f.HrDonHoTroCongTac9s)
                .Include(f => f.HrDonKiTucXa10s)
                .Include(f => f.HrDonLamLaiThe11s)
                .Include(f => f.HrDonSuDungDienThoai12s);

            // Filter dữ liệu hoàn tất
            query = query.Where(f => f.IdAdmin != null && f.TenAdmin != null && f.TimeAdmin != null);

            if (tuNgay.HasValue) query = query.Where(f => f.TimeAdmin >= tuNgay.Value);
            if (denNgay.HasValue) query = query.Where(f => f.TimeAdmin <= denNgay.Value.AddDays(1).AddSeconds(-1));
            if (!string.IsNullOrEmpty(loaiDon)) query = query.Where(f => f.IdForm == loaiDon);

            // Phân quyền
            if (!userRoles.Contains("All"))
            {
                if (!string.IsNullOrEmpty(tenCongTy))
                    query = query.Where(f => f.TenCongTy == tenCongTy);

                if (!userRoles.Contains("AdminHR"))
                    query = query.Where(f => f.IdNguoiTao == userId);
            }

            var data = await query.OrderByDescending(f => f.TimeAdmin).AsNoTracking().ToListAsync();

            // 3. TẠO FILE EXCEL
            using (var workbook = new ClosedXML.Excel.XLWorkbook())
            {
                var worksheet = workbook.Worksheets.Add("BaoCao_HoanTat_HR");
                string[] headers = { "STT", "ID", "Loại Đơn", "Mã NV", "Họ Tên", "Bộ Phận", "Ngày Hoàn Tất", "Người Duyệt HR", "Người Hỗ Trợ", "CHI TIẾT NỘI DUNG" };

                for (int i = 0; i < headers.Length; i++)
                {
                    var cell = worksheet.Cell(1, i + 1);
                    cell.Value = headers[i];
                    cell.Style.Font.Bold = true;
                    cell.Style.Fill.BackgroundColor = ClosedXML.Excel.XLColor.FromHtml("#1e40af");
                    cell.Style.Font.FontColor = ClosedXML.Excel.XLColor.White;
                    cell.Style.Alignment.Horizontal = ClosedXML.Excel.XLAlignmentHorizontalValues.Center;
                }

                int currentRow = 2;
                foreach (var item in data)
                {
                    worksheet.Cell(currentRow, 1).Value = currentRow - 1;
                    worksheet.Cell(currentRow, 2).Value = item.Id;
                    worksheet.Cell(currentRow, 3).Value = GetShortName(item.IdForm ?? "Unknown"); // An toàn với string null
                    worksheet.Cell(currentRow, 4).Value = item.SoNhanVien ?? "N/A";
                    worksheet.Cell(currentRow, 5).Value = item.TenNguoiNv ?? "N/A";
                    worksheet.Cell(currentRow, 6).Value = item.BoPhan ?? "N/A";
                    worksheet.Cell(currentRow, 7).Value = item.TimeAdmin?.ToString("dd/MM/yyyy HH:mm") ?? "--";
                    worksheet.Cell(currentRow, 8).Value = item.TenAdmin ?? "N/A";

                    // Truy xuất người hỗ trợ an toàn
                    var support = item.HrCtNguoiHoTros?.OrderByDescending(s => s.Stt).FirstOrDefault();
                    worksheet.Cell(currentRow, 9).Value = support?.IdHrNguoiHoTroNavigation?.Ten ?? "";

                    // Chi tiết nội dung với null-check
                    worksheet.Cell(currentRow, 10).Value = GetChiTietDon(item);
                    worksheet.Cell(currentRow, 10).Style.Alignment.WrapText = true;

                    currentRow++;
                }

                worksheet.Columns().AdjustToContents();
                worksheet.Column(10).Width = 80;

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
            // --- THÊM 3 TÊN NGẮN MỚI ---
            "HR_DonKiTucXa_10" => "Ký túc xá",
            "HR_DonLamLaiThe_11" => "Làm lại thẻ",
            "HR_DonSuDungDienThoai_12" => "Sử dụng ĐT",
            _ => string.IsNullOrEmpty(id) ? "N/A" : id.Replace("HR_", "")
        };

        private string GetChiTietDon(E_Form_Best.Models.ITForm.FormHr item)
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

                    // --- THÊM XỬ LÝ CHI TIẾT 3 LOẠI ĐƠN MỚI ---
                    case "HR_DonKiTucXa_10":
                        var f10 = item.HrDonKiTucXa10s.FirstOrDefault();
                        if (f10 == null) return "";
                        string outTime = f10.ThoiGianTraPhong.HasValue ? $"\n- Thời gian trả: {f10.ThoiGianTraPhong.Value:dd/MM/yyyy HH:mm}" : "";
                        return $"- Người đăng ký: {f10.HoTen} ({f10.MaNhanVien})\n- Loại phòng: {f10.LoaiPhong}\n- Thời gian nhận: {f10.ThoiGianNhanPhong:dd/MM/yyyy HH:mm}{outTime}\n- Ghi chú: {f10.GhiChu}";
                    case "HR_DonLamLaiThe_11":
                        var f11 = item.HrDonLamLaiThe11s.FirstOrDefault();
                        return f11 != null ? $"- Tên trên thẻ: {f11.HoTen}\n- Mã số thẻ: {f11.MaSoThe}\n- Lý do: {f11.LyDoLamLaiThe}\n- Ghi chú: {f11.GhiChu}" : "";
                    case "HR_DonSuDungDienThoai_12":
                        var f12 = item.HrDonSuDungDienThoai12s.FirstOrDefault();
                        return f12 != null ? $"- Người sử dụng: {f12.HoTen} (Mã NV/Thẻ: {f12.MaSoThe})\n- Lý do: {f12.LyDoSuDung}\n- Thời gian bắt đầu: {f12.ThoiGianBatDauSuDung:dd/MM/yyyy HH:mm}\n- Ghi chú: {f12.GhiChu}" : "";

                    default: return "N/A";
                }
            }
            catch { return "Lỗi xử lý dữ liệu chi tiết"; }
        }

        #endregion

        #region BÁO CÁO THỐNG KÊ FORM HR (Đồng bộ kiến trúc tối ưu từ IT)

        // 1. Action này chỉ trả về giao diện (View)
        [HttpGet("/FormHR/BaoCaoThongKe")]
        public IActionResult BaoCaoThongKe()
        {
            if (User?.Identity == null || !User.Identity.IsAuthenticated)
                return Redirect("/DonXetDuyet/DangNhap");

            bool isAuthorized = User.IsInRole("AdminHR") || User.IsInRole("All");
            if (!isAuthorized)
                return Redirect("/");

            return View();
        }

        [HttpGet("/FormHR/GetDataThongKe")]
        public async Task<IActionResult> GetDataThongKe(DateTime? fromDate, DateTime? toDate)
        {
            try
            {
                var query = _context.FormHrs.AsNoTracking();

                // Lọc theo khoảng thời gian nếu có chọn (Sử dụng TimeNguoiTao làm mốc cho HR)
                if (fromDate.HasValue)
                {
                    query = query.Where(x => x.TimeNguoiTao >= fromDate.Value);
                }
                if (toDate.HasValue)
                {
                    var endOfToDate = toDate.Value.Date.AddDays(1).AddTicks(-1);
                    query = query.Where(x => x.TimeNguoiTao <= endOfToDate);
                }

                // Sử dụng Select trực tiếp để EF Core sinh ra câu lệnh SQL tinh gọn nhất
                var rawData = await query
                    .Select(x => new
                    {
                        x.Id,
                        IdFormValue = x.IdForm ?? "",
                        BoPhan = x.BoPhan,
                        x.IdNguoiDuyet,
                        x.IdAdmin,
                        x.TenForm,
                        Danhmuc = string.IsNullOrEmpty(x.Danhmuc) ? "Biểu mẫu HR" : x.Danhmuc,
                        // Lấy trạng thái B2 và GĐ để xử lý Logic 7 trạng thái
                        B2States = x.HrQuanLyDuyetB2s.Select(b => b.TrangThaiXacNhan).ToList(),
                        GDStates = x.HrNguoiXacNhans.Select(g => g.TrangThaiXacNhan).ToList()
                    })
                    .ToListAsync();

                // Xử lý logic trạng thái trên RAM
                var data = rawData.Select(x => new
                {
                    Id = x.Id,
                    IdForm = GetTenNganGonFormHR(x.IdFormValue), // Chuẩn hóa tên viết tắt cho biểu đồ
                    Danhmuc = x.Danhmuc,
                    BoPhan = string.IsNullOrEmpty(x.BoPhan) ? "N/A" : x.BoPhan,
                    // Đồng bộ trả ra chuỗi trạng thái khớp chính xác với mapStatus() của Frontend
                    TrangThaiDon = CalculateStatusHR(x.TenForm, x.IdNguoiDuyet, x.IdAdmin, x.B2States, x.GDStates)
                }).ToList();

                return Json(data);
            }
            catch (Exception ex)
            {
                return BadRequest(ex.Message);
            }
        }

        [HttpGet("/FormHR/GetDataNguoiHoTro")]
        public async Task<IActionResult> GetDataNguoiHoTro(DateTime? fromDate, DateTime? toDate)
        {
            try
            {
                var query = _context.HrCtNguoiHoTros.AsNoTracking();

                // Lọc theo khoảng thời gian nếu có chọn trước khi Select
                if (fromDate.HasValue)
                {
                    query = query.Where(x => x.IdFormHrNavigation != null && x.IdFormHrNavigation.TimeNguoiTao >= fromDate.Value);
                }
                if (toDate.HasValue)
                {
                    var endOfToDate = toDate.Value.Date.AddDays(1).AddTicks(-1);
                    query = query.Where(x => x.IdFormHrNavigation != null && x.IdFormHrNavigation.TimeNguoiTao <= endOfToDate);
                }

                // Lấy dữ liệu thô từ bảng chi tiết hỗ trợ
                var rawData = await query
                    .Select(x => new
                    {
                        x.IdFormHr,
                        x.Stt,
                        TenNguoiHoTro = x.IdHrNguoiHoTroNavigation != null ? x.IdHrNguoiHoTroNavigation.Ten : "Chưa xác định",
                        Danhmuc = x.IdFormHrNavigation != null && !string.IsNullOrEmpty(x.IdFormHrNavigation.Danhmuc) ? x.IdFormHrNavigation.Danhmuc : "Biểu mẫu HR",
                        TenForm = x.IdFormHrNavigation != null ? x.IdFormHrNavigation.TenForm : "",
                        IdAdmin = x.IdFormHrNavigation != null ? x.IdFormHrNavigation.IdAdmin : null,
                        IdNguoiDuyet = x.IdFormHrNavigation != null ? x.IdFormHrNavigation.IdNguoiDuyet : null,
                        TimeAdmin = x.IdFormHrNavigation != null ? x.IdFormHrNavigation.TimeAdmin : null,
                        TimeNguoiDuyet = x.IdFormHrNavigation != null ? x.IdFormHrNavigation.TimeNguoiDuyet : null,
                        B2States = x.IdFormHrNavigation != null ? x.IdFormHrNavigation.HrQuanLyDuyetB2s.Select(b => b.TrangThaiXacNhan).ToList() : new List<int?>(),
                        GDStates = x.IdFormHrNavigation != null ? x.IdFormHrNavigation.HrNguoiXacNhans.Select(g => g.TrangThaiXacNhan).ToList() : new List<int?>()
                    })
                    .ToListAsync();

                // Sau khi lấy dữ liệu thô về RAM, GroupBy để lấy Stt cao nhất (Người hỗ trợ cuối cùng)
                var filteredData = rawData
                    .GroupBy(x => x.IdFormHr)
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
                            IdFormHr = top.IdFormHr, // Giữ ID gốc phục vụ JS kết nối dữ liệu lọc chéo lọc đồng bộ
                            TenNguoiHoTro = top.TenNguoiHoTro,
                            DanhMuc = top.Danhmuc,
                            // Đồng bộ trả ra chuỗi trạng thái khớp chính xác với mapStatus() của Frontend
                            TrangThai = CalculateStatusHR(top.TenForm, top.IdNguoiDuyet, top.IdAdmin, top.B2States, top.GDStates),
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

        // --- CÁC HÀM HELPER BỔ TRỢ ---

        private static string CalculateStatusHR(string? tenForm, int? idQL, int? idAdmin, IEnumerable<int?> b2, IEnumerable<int?> gd)
        {
            var b2List = b2 ?? Enumerable.Empty<int?>();
            var gdList = gd ?? Enumerable.Empty<int?>();

            if ((tenForm ?? "").Contains("[ĐÃ HỦY]") || b2List.Any(v => v == 2) || gdList.Any(v => v == 2)) return "HỦY";
            if (idAdmin != null) return "HOÀN TẤT";
            if (idQL == null) return "CHỜ QL";
            if (b2List.Any() && !b2List.All(v => v == 1)) return "BP XÁC NHẬN"; // Khớp chuỗi mapStatus() phía JS
            if (gdList.Any() && !gdList.All(v => v == 1)) return "GIÁM ĐỐC";    // Khớp chuỗi mapStatus() phía JS
            return "CHỜ ADMIN"; // Thay cho chuỗi cũ giúp Frontend hiểu ngay không cần qua bước xử lý trung gian
        }

        private static string GetTenNganGonFormHR(string? idForm)
        {
            if (string.IsNullOrEmpty(idForm)) return "Khác";
            if (idForm.Contains("XinRaNgoai_1")) return "Ra ngoài";
            if (idForm.Contains("MangHangHoaRaCong_2")) return "Hàng hóa";
            if (idForm.Contains("XeCongTac_3")) return "Xe công tác";
            if (idForm.Contains("XeDaily_4")) return "Xe Daily";
            if (idForm.Contains("DonTiepKhac_5")) return "Tiếp khách";
            if (idForm.Contains("NhaThauQuaCong_6")) return "Nhà thầu";
            if (idForm.Contains("HoTroTienDienThoai_7")) return "Tiền ĐT";
            if (idForm.Contains("DoiCaLam_8")) return "Đổi ca";
            if (idForm.Contains("DonHoTroCongTac_9")) return "HT Công tác";
            if (idForm.Contains("DonKiTucXa_10")) return "Ký túc xá";
            if (idForm.Contains("DonLamLaiThe_11")) return "Làm lại thẻ";
            if (idForm.Contains("DonSuDungDienThoai_12")) return "SD Điện thoại";

            return idForm;
        }

        #endregion

        #region QUY TRÌNH KIỂM SOÁT CỔNG BẢO VỆ

        /// <summary>
        /// Trang trả về View rỗng để JavaScript Render phân trang
        /// </summary>
        [HttpGet("/FormHR/BaoVePheDuyet")]
        public IActionResult BaoVePheDuyet()
        {
            var userIdStr = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userIdStr)) return Redirect("/DonXetDuyet/DangNhap");

            var userRole = (User.FindFirst(System.Security.Claims.ClaimTypes.Role)?.Value
                           ?? User.FindFirst("UserRole")?.Value
                           ?? "GUEST").Trim().ToUpper();

            ViewBag.UserRole = userRole;
            return View();
        }

        /// <summary>
        /// API trả về dữ liệu JSON danh sách đơn cho Bảo Vệ
        /// </summary>
        [HttpGet("/FormHR/GetBaoVeData")]
        public async Task<IActionResult> GetBaoVeData()
        {
            var userIdStr = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userIdStr)) return Unauthorized();

            var tenCongTy = User.FindFirst("TenCongTy")?.Value?.Trim() ?? "";

            // --- TRUY VẤN DỮ LIỆU ---
            IQueryable<E_Form_Best.Models.ITForm.FormHr> query = _context.FormHrs
                .Include(f => f.HrNguoiXacNhans)
                .Include(f => f.HrQuanLyDuyetB2s)
                .Include(f => f.BaoVeHrs)
                .Include(f => f.HrCtNguoiHoTros).ThenInclude(ct => ct.IdHrNguoiHoTroNavigation);

            if (!User.IsInRole("All") && !string.IsNullOrEmpty(tenCongTy))
            {
                query = query.Where(f => f.TenCongTy == tenCongTy);
            }

            // CHỈ LẤY NHỮNG ĐƠN ĐƯỢC ĐẨY SANG BẢO VỆ
            query = query.Where(f => f.BaoVeHrs != null && f.BaoVeHrs.Any());

            try
            {
                var danhSachDon = await query.OrderByDescending(f => f.Id).AsNoTracking().ToListAsync();

                var result = danhSachDon.Select(item =>
                {
                    // --- KHỞI TẠO BIẾN TRUNG GIAN AN TOÀN ---
                    var b2List = item.HrQuanLyDuyetB2s ?? Enumerable.Empty<E_Form_Best.Models.ITForm.HrQuanLyDuyetB2>();
                    var gdList = item.HrNguoiXacNhans ?? Enumerable.Empty<E_Form_Best.Models.ITForm.HrNguoiXacNhan>();
                    var bvList = item.BaoVeHrs ?? Enumerable.Empty<E_Form_Best.Models.ITForm.BaoVeHr>();

                    // --- LOGIC TÍNH TOÁN ---
                    bool isCancelled = (item.TenForm ?? "").Contains("[ĐÃ HỦY]");
                    bool isFinished = item.IdAdmin != null;
                    bool isManagerApproved = item.IdNguoiDuyet != null;

                    bool hasB2 = b2List.Any();
                    bool isB2Approved = hasB2 && b2List.All(x => x.TrangThaiXacNhan == 1);
                    bool isB2Rejected = hasB2 && b2List.Any(x => x.TrangThaiXacNhan == 2);

                    bool hasGD = gdList.Any();
                    bool isGDApproved = hasGD && gdList.All(x => x.TrangThaiXacNhan == 1);
                    bool isGDRejected = hasGD && gdList.Any(x => x.TrangThaiXacNhan == 2);

                    string pWidth, pColor, pText;
                    if (isCancelled || isB2Rejected || isGDRejected) { pWidth = "100%"; pColor = "#ef4444"; pText = "ĐÃ HỦY/TỪ CHỐI"; }
                    else if (isFinished) { pWidth = "100%"; pColor = "#10b981"; pText = "HOÀN TẤT"; }
                    else if (!isManagerApproved) { pWidth = "20%"; pColor = "#f59e0b"; pText = "CHỜ QL DUYỆT"; }
                    else if (hasB2 && !isB2Approved) { pWidth = "40%"; pColor = "#0ea5e9"; pText = "BP XÁC NHẬN"; }
                    else if (hasGD && !isGDApproved) { pWidth = "65%"; pColor = "#d946ef"; pText = "GIÁM ĐỐC"; }
                    else { pWidth = "85%"; pColor = "#2563eb"; pText = "CHỜ ADMIN"; }

                    // Logic Bảo vệ
                    var bv = bvList.FirstOrDefault();
                    bool isDone = bv?.TrangThai == 1;

                    return new
                    {
                        Id = item.Id,
                        TenNguoiNv = item.TenNguoiNv ?? "N/A",
                        BoPhan = item.BoPhan ?? "N/A",
                        SoNhanVien = item.SoNhanVien ?? "N/A",
                        TenForm = (item.TenForm ?? "").Replace("[ĐÃ HỦY]", "").Trim(),
                        TimeNguoiTao = item.TimeNguoiTao?.ToString("dd/MM/yyyy") ?? "--",
                        HasB2 = hasB2,
                        B2ApprovedCount = b2List.Count(x => x.TrangThaiXacNhan == 1),
                        B2TotalCount = b2List.Count(),
                        PWidth = pWidth,
                        PColor = pColor,
                        PText = pText,
                        IsDone = isDone,
                        TimeBaoVe = bv?.TimeBaoVe?.ToString("HH:mm dd/MM") ?? ""
                    };
                });

                return Json(result);
            }
            catch (Exception ex)
            {
                return BadRequest(new { message = "Lỗi hệ thống bảo vệ: " + ex.Message });
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

        #region LỊCH SỬ VÀ THÔNG BÁO FORM HR

        [HttpGet("/FormHR/LichSuHR")]
        public IActionResult LogLichSuHR()
        {
            var userIdStr = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userIdStr)) return RedirectToAction("DangNhap", "DonXetDuyet");
            return View();
        }

        // --- API: TRẢ VỀ JSON LỊCH SỬ HR ---
        [HttpGet("/FormHR/GetLichSuData")]
        public async Task<IActionResult> GetLichSuData()
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

            // Tối ưu: Include đầy đủ quan hệ bao gồm cả Ủy quyền để lọc dữ liệu
            var query = _context.LichSuFormHrs.AsNoTracking()
                .Include(l => l.IdFormHrNavigation).ThenInclude(f => f!.HrNguoiXacNhans)
                .Include(l => l.IdFormHrNavigation).ThenInclude(f => f!.HrCtNguoiHoTros).ThenInclude(ct => ct.IdHrNguoiHoTroNavigation)
                .Include(l => l.IdFormHrNavigation).ThenInclude(f => f!.HrQuanLyDuyetB2s).ThenInclude(b2 => b2.HrQuanLyDuyetB2UyQuyens)
                .Include(l => l.IdFormHrNavigation).ThenInclude(f => f!.BaoVeHrs)
                .Where(l => isAll || l.TrangThaiAnHien == true)
                .Where(l =>
                    isAll ||
                    (l.IdFormHrNavigation != null && l.IdFormHrNavigation.IdNguoiTao == userId) ||
                    (isAdminHR && l.IdFormHrNavigation != null && l.IdFormHrNavigation.IdNguoiDuyet != null &&
                        l.IdFormHrNavigation.HrCtNguoiHoTros.Any(ct => ct.IdHrNguoiHoTroNavigation != null && ct.IdHrNguoiHoTroNavigation.MaNv != null && ct.IdHrNguoiHoTroNavigation.MaNv.ToLower() == userEmail)) ||
                    (l.IdFormHrNavigation != null && l.IdFormHrNavigation.IdNguoiDuyet != null && (
                        (isGiamDocHR && l.IdFormHrNavigation.HrNguoiXacNhans.Any(xn => xn.IdnguoiXacNhan == userId || (xn.MaNguoiXacNhan != null && xn.MaNguoiXacNhan.ToLower() == userEmail))) ||
                        (isQuanLyB2 && l.IdFormHrNavigation.HrQuanLyDuyetB2s.Any(b2 =>
                            b2.IdnguoiXacNhan == userId ||
                            (b2.MaNguoiXacNhan != null && b2.MaNguoiXacNhan.ToLower() == userEmail) ||
                            b2.HrQuanLyDuyetB2UyQuyens.Any(uq => uq.MaNvuyQuyen != null && uq.MaNvuyQuyen.ToLower() == userEmail)
                        )) ||
                        (isBaoVeHR && l.IdFormHrNavigation.BaoVeHrs.Any())
                    )) ||
                    (isQuanLyDuyet && l.IdFormHrNavigation != null && l.IdFormHrNavigation.IdNguoiDuyet == userId)
                )
                .OrderByDescending(l => l.Time)
                .Take(200);

            var logs = await query.ToListAsync();

            var result = logs.Select(item =>
            {
                var f = item.IdFormHrNavigation;
                if (f == null) return null;

                bool isCancelled = (f.TenForm ?? "").Contains("[ĐÃ HỦY]") || f.TrangThai == "DaHuy";
                bool isFinished = f.IdAdmin != null || f.TrangThai == "HoanTat";

                var b2List = f.HrQuanLyDuyetB2s ?? new List<E_Form_Best.Models.ITForm.HrQuanLyDuyetB2>();
                bool hasB2 = b2List.Any();
                bool isB2Approved = hasB2 && b2List.All(x => x.TrangThaiXacNhan == 1);
                bool isB2Rejected = hasB2 && b2List.Any(x => x.TrangThaiXacNhan == 2);

                var gdList = f.HrNguoiXacNhans ?? new List<E_Form_Best.Models.ITForm.HrNguoiXacNhan>();
                bool hasGD = gdList.Any();
                bool isGDApproved = hasGD && gdList.All(x => x.TrangThaiXacNhan == 1);
                bool isGDRejected = hasGD && gdList.Any(x => x.TrangThaiXacNhan == 2);

                string statusText, statusColor;
                if (isCancelled || isB2Rejected || isGDRejected) { statusText = "ĐÃ HỦY"; statusColor = "#ef4444"; }
                else if (isFinished) { statusText = "HOÀN TẤT"; statusColor = "#10b981"; }
                else if (f.IdNguoiDuyet == null) { statusText = "CHỜ QL"; statusColor = "#f59e0b"; }
                else if (hasB2 && !isB2Approved) { statusText = "BP XÁC NHẬN"; statusColor = "#0ea5e9"; }
                else if (hasGD && !isGDApproved) { statusText = "GIÁM ĐỐC"; statusColor = "#d946ef"; }
                else { statusText = "CHỜ ADMIN"; statusColor = "#2563eb"; }

                string actionColor = "#6366f1";
                string actionTitle = item.TieuDe?.ToUpper() ?? "HÀNH ĐỘNG";
                if (actionTitle.Contains("DUYỆT") || actionTitle.Contains("XÁC NHẬN")) actionColor = "#10b981";
                else if (actionTitle.Contains("HỦY") || actionTitle.Contains("TỪ CHỐI")) actionColor = "#ef4444";
                else if (actionTitle.Contains("HOÀN TẤT")) actionColor = "#1e40af";

                var sup = f.HrCtNguoiHoTros?.OrderByDescending(x => x.Stt).FirstOrDefault()?.IdHrNguoiHoTroNavigation;

                return new
                {
                    item.Id,
                    item.IdFormHr,
                    TimeHHmm = item.Time?.ToString("HH:mm") ?? "--:--",
                    TimeDate = item.Time?.ToString("dd/MM/yyyy") ?? "--/--/----",
                    TieuDe = item.TieuDe ?? "Hành động",
                    Mota = item.Mota ?? "",
                    TenForm = (f.TenForm ?? "").Replace("[ĐÃ HỦY]", ""),
                    TenAdmin = f.TenNguoiTao ?? "Đang xử lý",
                    NguoiHoTro = sup?.Ten,
                    StatusText = statusText,
                    StatusColor = statusColor,
                    ActionColor = actionColor
                };
            }).Where(x => x != null).ToList();

            return Json(result);
        }

        // --- API: THÔNG BÁO HR (TỐI ƯU DÙNG ỦY QUYỀN) ---
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

            // Tối ưu: Include đầy đủ tất cả các quan hệ cần thiết dùng trong biểu thức Where để tránh lỗi runtime
            var query = _context.LichSuFormHrs.AsNoTracking()
                .Include(l => l.IdFormHrNavigation).ThenInclude(f => f!.HrNguoiXacNhans)
                .Include(l => l.IdFormHrNavigation).ThenInclude(f => f!.HrCtNguoiHoTros).ThenInclude(ct => ct.IdHrNguoiHoTroNavigation)
                .Include(l => l.IdFormHrNavigation).ThenInclude(f => f!.HrQuanLyDuyetB2s).ThenInclude(b2 => b2.HrQuanLyDuyetB2UyQuyens)
                .Include(l => l.IdFormHrNavigation).ThenInclude(f => f!.BaoVeHrs)
                .Where(l => isAll || l.TrangThaiAnHien == true)
                .Where(l =>
                    isAll ||
                    (l.IdFormHrNavigation != null && l.IdFormHrNavigation.IdNguoiTao == userId) ||
                    (isAdminHR && l.IdFormHrNavigation != null && l.IdFormHrNavigation.IdNguoiDuyet != null &&
                        l.IdFormHrNavigation.HrCtNguoiHoTros.Any(ct => ct.IdHrNguoiHoTroNavigation != null && ct.IdHrNguoiHoTroNavigation.MaNv != null && ct.IdHrNguoiHoTroNavigation.MaNv.ToLower() == userEmail)) ||
                    (l.IdFormHrNavigation != null && l.IdFormHrNavigation.IdNguoiDuyet != null && (
                        (isGiamDocHR && l.IdFormHrNavigation.HrNguoiXacNhans.Any(xn => xn.IdnguoiXacNhan == userId || (xn.MaNguoiXacNhan != null && xn.MaNguoiXacNhan.ToLower() == userEmail))) ||
                        (isQuanLyB2 && l.IdFormHrNavigation.HrQuanLyDuyetB2s.Any(b2 =>
                            b2.IdnguoiXacNhan == userId ||
                            (b2.MaNguoiXacNhan != null && b2.MaNguoiXacNhan.ToLower() == userEmail) ||
                            b2.HrQuanLyDuyetB2UyQuyens.Any(uq => uq.MaNvuyQuyen != null && uq.MaNvuyQuyen.ToLower() == userEmail)
                        )) ||
                        (isBaoVeHR && l.IdFormHrNavigation.BaoVeHrs.Any())
                    )) ||
                    (isQuanLyDuyet && l.IdFormHrNavigation != null && l.IdFormHrNavigation.IdNguoiDuyet == userId)
                );

            var unreadCount = await query.CountAsync(l => l.IsRead != true);

            var logs = await query.OrderByDescending(l => l.Time)
                                  .Skip(skip)
                                  .Take(take)
                                  .Select(l => new
                                  {
                                      l.Id,
                                      l.IdFormHr,
                                      TieuDe = l.TieuDe ?? "",
                                      Mota = l.Mota ?? "",
                                      Time = l.Time.HasValue ? l.Time.Value.ToString("dd/MM HH:mm") : "",
                                      IsRead = l.IsRead ?? false
                                  })
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
        /// Trả về View rỗng để JavaScript tự động Render và phân trang mượt mà
        /// </summary>
        [HttpGet("/FormHR/QuanLyPhongHop")]
        public IActionResult QuanLyPhongHop()
        {
            var userIdStr = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userIdStr)) return Redirect("/DonXetDuyet/DangNhap");

            ViewBag.UserRole = User.FindFirst(System.Security.Claims.ClaimTypes.Role)?.Value ?? User.FindFirst("UserRole")?.Value ?? "Guest";
            return View();
        }

        /// <summary>
        /// API TRẢ VỀ JSON CHO JAVASCRIPT RENDER
        /// --- ĐÃ NÂNG CẤP: Bổ sung fromDate, toDate để lọc hiệu năng cao tại Database ---
        /// </summary>
        [HttpGet("/FormHR/GetPhongHopData")]
        public async Task<IActionResult> GetPhongHopData(DateTime? fromDate, DateTime? toDate)
        {
            var userIdStr = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userIdStr)) return Unauthorized();

            var tenCongTy = User.FindFirst("TenCongTy")?.Value?.Trim() ?? "";
            bool isAll = User.IsInRole("All") || User.IsInRole("AdminHR");

            // --- Lấy danh mục phòng gốc (Master Data) ---
            var danhMucPhong = await _context.PhongHopHrs.AsNoTracking().Select(x => new
            {
                x.Id,
                TenPhongHop = x.TenPhongHop ?? "",
                GhiChu = x.GhiChu ?? ""
            }).ToListAsync();

            // --- Lấy danh sách các đơn đã đăng ký phòng (Transaction Data) ---
            IQueryable<E_Form_Best.Models.ITForm.HrDonTiepKhac5> query = _context.HrDonTiepKhac5s
                .Include(x => x.IdFormHrNavigation)
                .Where(x => !string.IsNullOrEmpty(x.TenPhongHop));

            // Lọc theo thời gian
            if (fromDate.HasValue)
            {
                query = query.Where(x => x.ThoiGianBatDau >= fromDate.Value);
            }
            if (toDate.HasValue)
            {
                var endOfToDate = toDate.Value.Date.AddDays(1).AddTicks(-1);
                query = query.Where(x => x.ThoiGianBatDau <= endOfToDate);
            }

            // Phân quyền: Admin xem tất cả, User thường/BP xem theo công ty
            if (!isAll && !string.IsNullOrEmpty(tenCongTy))
            {
                query = query.Where(x => x.IdFormHrNavigation != null && x.IdFormHrNavigation.TenCongTy == tenCongTy);
            }

            var lichDatPhongRaw = await query.OrderByDescending(x => x.Id).AsNoTracking().ToListAsync();

            var lichDatPhong = lichDatPhongRaw.Select(item => new
            {
                Id = item.Id,
                IdFormHr = item.IdFormHr,
                TenPhongHop = item.TenPhongHop ?? "",
                TenCongTy = item.IdFormHrNavigation?.TenCongTy ?? "",
                NguoiBook = item.NguoiBook ?? "",
                TenCongTyKhach = item.TenCongTyKhach ?? "",
                ThoiGianBatDau = item.ThoiGianBatDau.HasValue ? item.ThoiGianBatDau.Value.ToString("dd/MM HH:mm") : "",
                ThoiGianKetThuc = item.ThoiGianKetThuc.HasValue ? item.ThoiGianKetThuc.Value.ToString("dd/MM HH:mm") : "",
                TrangThaiPhong = item.TrangThaiPhong ?? "off"
            });

            return Json(new { danhMucPhong, lichDatPhong });
        }

        /// <summary>
        /// Cập nhật trạng thái sử dụng phòng (Dùng AJAX - Toggle ON/OFF)
        /// </summary>
        [HttpPost("/FormHR/UpdateTrangThaiPhong")]
        [ValidateAntiForgeryToken] // Chống tấn công giả mạo đơn hàng
        public async Task<IActionResult> UpdateTrangThaiPhong(int id, string status)
        {
            var userIdStr = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
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
                    Mota = $"Thao tác: {status.ToUpper()}. Người thực hiện: {User.FindFirst(System.Security.Claims.ClaimTypes.Name)?.Value}.",
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
        public async Task<IActionResult> LuuPhongHop(E_Form_Best.Models.ITForm.PhongHopHr model)
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
