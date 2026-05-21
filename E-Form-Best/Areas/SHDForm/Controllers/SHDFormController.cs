using E_Form_Best.Context;
using E_Form_Best.Models.ITForm;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace E_Form_Best.Areas.SHDForm.Controllers
{
    [Area("SHDform")]
    public class SHDFormController : Controller
    {
        public ITFormContext _context;

        public SHDFormController()
        {
            _context= new ITFormContext();
        }

        #region Trang logo
        [HttpGet("/SHDForm/logo")]
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

        #region ĐƠN XE ĐI CÔNG TÁC (SHD_DangKySuDungXeCongTac_1)

        [HttpGet("/FormSHD/DangKySuDungXeCongTac")]
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
            // Lưu ý: Đảm bảo model ShdNguoiHoTro của bạn đã được bổ sung navigation property CongViecShds nếu dùng .Include
            ViewBag.ListNguoiHoTro = _context.ShdNguoiHoTros
                .Where(x => _context.CongViecShds.Any(cv => cv.IdShdNguoiHoTro == x.Id && cv.Ten == "Đăng ký sử dụng xe công tác"))
                .ToList();

            var model = new FormShd
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

        [HttpPost("/FormSHD/DangKySuDungXeCongTac")]
        public async Task<IActionResult> DangKySuDungXeCongTac(FormShd form, [FromForm] ShdDangKySuDungXeCongTac1 chiTiet, int[] SelectedCongViecIds)
        {
            if (!User.Identity.IsAuthenticated) return Redirect("/DonXetDuyet/DangNhap");

            // Gia cố Binding nếu chiTiet bị null
            if (chiTiet == null || (string.IsNullOrEmpty(chiTiet.LiDo) && Request.Form.ContainsKey("chiTiet.LiDo")))
            {
                chiTiet = new ShdDangKySuDungXeCongTac1
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

            string networkPath = @"\\10.0.60.30\BPVN-Fileserver\Public\IT-Information Technology Dept\5.E-Form\DonSHD";

            using (var transaction = await _context.Database.BeginTransactionAsync())
            {
                try
                {
                    // --- BƯỚC 1: LƯU BẢNG CHÍNH (FormShd) ---
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
                    form.IdForm = "SHD_DangKySuDungXeCongTac_1";
                    form.TenForm = "Đơn đăng ký sử dụng xe công tác";
                    form.Danhmuc = "ĐƠN ĐĂNG KÝ SỬ DỤNG XE CÔNG TÁC";

                    _context.FormShds.Add(form);
                    await _context.SaveChangesAsync();

                    // --- BƯỚC 2: LƯU NHÂN SỰ HỖ TRỢ ĐÃ CHỌN ---
                    if (SelectedCongViecIds != null && SelectedCongViecIds.Length > 0)
                    {
                        foreach (var cvId in SelectedCongViecIds)
                        {
                            var congViec = await _context.CongViecShds.FindAsync(cvId);
                            if (congViec != null)
                            {
                                var ctHoTro = new ShdCtNguoiHoTro
                                {
                                    IdFormShd = form.Id,
                                    IdShdNguoiHoTro = congViec.IdShdNguoiHoTro,
                                    Stt = 1
                                };
                                _context.ShdCtNguoiHoTros.Add(ctHoTro);
                            }
                        }
                        await _context.SaveChangesAsync();
                    }

                    // --- BƯỚC 3: TỰ ĐỘNG THÊM NGƯỜI XÁC NHẬN (CẤU HÌNH THEO BỘ PHẬN & CÔNG TY) ---
                    var listDuyetTheoBoPhan = await _context.DmNguoiDuyetLoaiDonBoPhans
                        .Include(x => x.IdnguoiXacNhanNavigation)
                        .Include(x => x.DmCtChiTietUyQuyens.Where(uq => uq.TrangThai == true))
                            .ThenInclude(uq => uq.IduyQuyenNavigation)
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
                            var quanLyDuyet = new ShdQuanLyDuyetB2
                            {
                                IdFormShd = form.Id,
                                IdnguoiXacNhan = item.IdnguoiXacNhan,
                                ThuTuXacNhan = item.Stt,
                                MaNguoiXacNhan = item.IdnguoiXacNhanNavigation?.MaNv,
                                TenNguoiXacNhan = item.IdnguoiXacNhanNavigation?.HoTen,
                                TrangThaiXacNhan = 0,
                                ThoiGianXacNhan = null,
                                Loai = item.Loai
                            };
                            _context.ShdQuanLyDuyetB2s.Add(quanLyDuyet);
                            await _context.SaveChangesAsync();

                            // XỬ LÝ ĐIỀU KIỆN THỜI GIAN ỦY QUYỀN LINH HOẠT (CÓ XỬ LÝ NULL)
                            var currentTime = DateTime.Now;
                            var listUyQuyenHopLe = item.DmCtChiTietUyQuyens
                                .Where(uq =>
                                    (uq.ThoiGianBatDau == null || uq.ThoiGianBatDau <= currentTime) &&
                                    (uq.ThoiGianKetThuc == null || uq.ThoiGianKetThuc >= currentTime)
                                )
                                .ToList();

                            if (listUyQuyenHopLe.Any())
                            {
                                foreach (var uq in listUyQuyenHopLe)
                                {
                                    if (uq.IduyQuyenNavigation != null)
                                    {
                                        var shdUyQuyen = new ShdQuanLyDuyetB2UyQuyen
                                        {
                                            IdShdQuanLyDuyetB2 = quanLyDuyet.Id,
                                            MaNvuyQuyen = uq.IduyQuyenNavigation.MaNvuyQuyen,
                                            HoTenUyQuyen = uq.IduyQuyenNavigation.HoTenUyQuyen
                                        };
                                        _context.ShdQuanLyDuyetB2UyQuyens.Add(shdUyQuyen);
                                    }
                                }
                            }
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
                            var shdXacNhan = new ShdNguoiXacNhan
                            {
                                IdFormShd = form.Id,
                                IdnguoiXacNhan = item.IdnguoiXacNhan,
                                ThuTuXacNhan = item.CapDoXacNhan,
                                MaNguoiXacNhan = item.IdnguoiXacNhanNavigation?.MaNv,
                                TenNguoiXacNhan = item.IdnguoiXacNhanNavigation?.HoTen,
                                TrangThaiXacNhan = 0,
                                ThoiGianXacNhan = null
                            };
                            _context.ShdNguoiXacNhans.Add(shdXacNhan);
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

                    var lichSu = new LichSuFormShd
                    {
                        IdFormShd = form.Id,
                        TieuDe = "Khởi tạo đơn xe công tác",
                        Mota = $"Nhân viên {userName} ({soNhanVien}) đã tạo đơn đăng ký xe công tác.\n{chiTietXe}",
                        Time = DateTime.Now,
                        IsRead = false,
                        TrangThaiAnHien = true // Đảm bảo luôn hiển thị ra View chi tiết đơn
                    };
                    _context.LichSuFormShds.Add(lichSu);
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
                        chiTiet.IdFormShd = form.Id;
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
                        _context.ShdDangKySuDungXeCongTac1s.Add(chiTiet);
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

        #region CHI TIẾT ĐƠN FORM SHD (KHÔNG CÓ BẢO VỆ)

        [HttpGet("/FormSHD/ChiTiet/{id}")]
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

            // 2. Lấy dữ liệu đơn (Include các bảng liên quan của SHD)
            var don = await _context.FormShds
                .Include(f => f.ShdDangKySuDungXeCongTac1s)
                .Include(f => f.ShdDangKySuDungXeDaily2s)
                .Include(f => f.ShdCtNguoiHoTros)
                    .ThenInclude(ct => ct.IdShdNguoiHoTroNavigation)
                .Include(f => f.ShdNguoiXacNhans)
                    .ThenInclude(x => x.IdnguoiXacNhanNavigation)
                .Include(f => f.LichSuFormShds)
                .Include(f => f.BinhLuanFormShds)
                .Include(f => f.ShdQuanLyDuyetB2s)
                    .ThenInclude(b2 => b2.ShdQuanLyDuyetB2UyQuyens)
                .FirstOrDefaultAsync(m => m.Id == id);

            if (don == null)
            {
                TempData["Error"] = "⚠️ Không tìm thấy đơn yêu cầu SHD!";
                return RedirectToAction("LichSuSHD");
            }

            // 3. KIỂM TRA QUYỀN XEM (Logic cộng dồn)
            bool isAllowed = false;

            // Quyền 1: Người tạo đơn
            if (don.IdNguoiTao == userId)
            {
                isAllowed = true;
            }
            // Quyền 2: Admin tổng hoặc AdminSHD
            else if (User.IsInRole("All") || User.IsInRole("AdminSHD"))
            {
                isAllowed = true;
            }
            // Quyền 3: Quản lý duyệt đơn SHD (Cùng công ty và thuộc bộ phận quản lý)
            else if (User.IsInRole("QuanLyDuyetDonSHD"))
            {
                bool isSameCompany = string.Equals(don.TenCongTy?.Trim(), tenCongTyUser, StringComparison.OrdinalIgnoreCase);
                string listBoPhan = User.FindFirst("TenBoPhan")?.Value ?? "";
                string phongBanDon = User.FindFirst("PhongBan")?.Value ?? "";

                bool isSameDepartment = false;
                if (!string.IsNullOrEmpty(don.BoPhan))
                {
                    if (!string.IsNullOrEmpty(listBoPhan))
                    {
                        isSameDepartment = listBoPhan.Contains(don.BoPhan);
                    }
                    else
                    {
                        isSameDepartment = string.Equals(don.BoPhan.Trim(), phongBanDon.Trim(), StringComparison.OrdinalIgnoreCase);
                    }
                }

                if (isSameCompany && isSameDepartment)
                {
                    isAllowed = true;
                }
            }
            // Quyền 4: Thay đổi check theo Role QuanLyDuyetDonSHD_B2 và GiamDocSHD
            else if (User.IsInRole("QuanLyDuyetDonSHD_B2") || User.IsInRole("GiamDocSHD"))
            {
                isAllowed = true;
            }

            if (!isAllowed)
            {
                return Forbid();
            }

            // 4. Xử lý dữ liệu hiển thị
            if (don.LichSuFormShds != null)
                don.LichSuFormShds = don.LichSuFormShds.OrderByDescending(x => x.Time).ToList();

            // Load danh sách người hỗ trợ cho Admin điều phối
            if (User.IsInRole("All") || User.IsInRole("AdminSHD"))
            {
                ViewBag.ListNguoiHoTro = await _context.ShdNguoiHoTros
                    .Where(x => x.BoPhan == "SHD")
                    .AsNoTracking()
                    .ToListAsync();
            }

            ViewBag.CurrentUserId = userId;
            ViewBag.UserEmail = userEmail;

            ViewBag.DanhSachXacNhan = don.ShdNguoiXacNhans?
                .OrderBy(x => x.ThuTuXacNhan).ToList() ?? new List<ShdNguoiXacNhan>();

            ViewBag.DanhSachB2 = don.ShdQuanLyDuyetB2s?
                .OrderBy(x => x.ThuTuXacNhan).ToList() ?? new List<ShdQuanLyDuyetB2>();

            return View(don);
        }

        // 6. Tải tệp tin SHD
        [HttpGet("/FormSHD/DownloadFile/{fileName}")]
        public async Task<IActionResult> DownloadFile(string fileName)
        {
            if (string.IsNullOrEmpty(fileName)) return NotFound();
            string networkPath = @"\\10.0.60.30\BPVN-Fileserver\Public\IT-Information Technology Dept\5.E-Form\DonSHD";
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

        // 7. Chỉ định người hỗ trợ SHD
        [HttpPost("/FormSHD/ThemNguoiHoTro")]
        public async Task<IActionResult> ThemNguoiHoTro([FromBody] System.Text.Json.JsonElement data)
        {
            var roles = User.FindAll(System.Security.Claims.ClaimTypes.Role).Select(r => r.Value).ToList();
            if (!roles.Any(r => r == "AdminSHD" || r == "All"))
                return Json(new { success = false, message = "Không có quyền!" });

            try
            {
                int idForm = data.GetProperty("idFormShd").GetInt32();
                string maNvMoi = data.GetProperty("maNv").GetString();
                var nvShd = await _context.ShdNguoiHoTros.FirstOrDefaultAsync(x => x.MaNv == maNvMoi);

                var hienTai = await _context.ShdCtNguoiHoTros.Where(x => x.IdFormShd == idForm).OrderByDescending(x => x.Stt).FirstOrDefaultAsync();
                if (hienTai?.IdShdNguoiHoTroNavigation?.MaNv == maNvMoi)
                    return Json(new { success = false, message = "Nhân viên này đang xử lý rồi!" });

                _context.ShdCtNguoiHoTros.Add(new ShdCtNguoiHoTro
                {
                    IdFormShd = idForm,
                    IdShdNguoiHoTro = nvShd.Id,
                    Stt = (hienTai?.Stt ?? 0) + 1
                });

                _context.LichSuFormShds.Add(new LichSuFormShd
                {
                    IdFormShd = idForm,
                    TieuDe = "CHỈ ĐỊNH NGƯỜI HỖ TRỢ SHD",
                    Mota = $"Thay đổi sang: {nvShd.Ten} ({nvShd.MaNv})",
                    Time = DateTime.Now,
                    TrangThaiAnHien = true
                });

                await _context.SaveChangesAsync();
                return Json(new { success = true });
            }
            catch (Exception ex) { return Json(new { success = false, message = ex.Message }); }
        }

        // 8. API Toggle Ẩn Hiện Lịch sử (Chỉ dành cho quyền All)
        [HttpPost("/FormSHD/ToggleLichSuAnHien")]
        public async Task<IActionResult> ToggleLichSuAnHien([FromBody] System.Text.Json.JsonElement data)
        {
            if (!User.IsInRole("All"))
                return Json(new { success = false, message = "Không có quyền thao tác!" });

            try
            {
                int idLichSu = data.GetProperty("idLichSu").GetInt32();
                var ls = await _context.LichSuFormShds.FindAsync(idLichSu);
                if (ls == null) return Json(new { success = false, message = "Không tìm thấy bản ghi lịch sử này." });

                ls.TrangThaiAnHien = !ls.TrangThaiAnHien; // Đảo ngược trạng thái
                await _context.SaveChangesAsync();

                return Json(new { success = true, newState = ls.TrangThaiAnHien });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = ex.Message });
            }
        }

        #endregion

        #region Xuất file Excel, Word, PDF cho hệ thống SHD

        // ============================================================
        // ACTION XUẤT BIỂU MẪU SHD (EXCEL, WORD, PDF)
        // ============================================================

        [HttpGet("/FormSHD/ExportExcel/{id}")]
        public async Task<IActionResult> ExportExcel(int id)
        {
            var don = await _context.FormShds
                .Include(f => f.ShdDangKySuDungXeCongTac1s)
                .Include(f => f.ShdQuanLyDuyetB2s)
                .Include(f => f.ShdNguoiXacNhans).ThenInclude(x => x.IdnguoiXacNhanNavigation)
                .FirstOrDefaultAsync(m => m.Id == id);

            if (don == null || don.IdAdmin == null)
                return NotFound("Đơn chưa hoàn tất xử lý hoặc không tồn tại.");

            using (var workbook = new ClosedXML.Excel.XLWorkbook())
            {
                var worksheet = workbook.Worksheets.Add("ChiTietDonSHD");

                // Header chung
                worksheet.Cell(1, 1).Value = "HỆ THỐNG E-FORM SHD - PHÒNG XUẤT NHẬP KHẨU - BEST PACIFIC";
                worksheet.Range("A1:E1").Merge().Style.Font.SetBold().Font.FontSize = 14;
                worksheet.Range("A1:E1").Style.Alignment.SetHorizontal(ClosedXML.Excel.XLAlignmentHorizontalValues.Center);

                worksheet.Cell(3, 1).Value = "Mã Đơn:"; worksheet.Cell(3, 2).Value = don.Id;
                worksheet.Cell(4, 1).Value = "Tên Form:"; worksheet.Cell(4, 2).Value = don.TenForm;
                worksheet.Cell(5, 1).Value = "Mã NV:"; worksheet.Cell(5, 2).Value = don.SoNhanVien;
                worksheet.Cell(6, 1).Value = "Họ Tên:"; worksheet.Cell(6, 2).Value = don.TenNguoiNv;
                worksheet.Cell(7, 1).Value = "Bộ Phận:"; worksheet.Cell(7, 2).Value = don.BoPhan;
                worksheet.Cell(8, 1).Value = "Ngày Tạo:"; worksheet.Cell(8, 2).Value = don.TimeNguoiTao?.ToString("dd/MM/yyyy HH:mm");
                worksheet.Cell(9, 1).Value = "Trạng Thái:"; worksheet.Cell(9, 2).Value = "HOÀN TẤT (SHD)";

                var rangeChung = worksheet.Range("A3:B9");
                rangeChung.Style.Border.SetOutsideBorder(ClosedXML.Excel.XLBorderStyleValues.Thin);
                rangeChung.Style.Border.SetInsideBorder(ClosedXML.Excel.XLBorderStyleValues.Thin);
                worksheet.Range("A3:A9").Style.Font.SetBold().Fill.SetBackgroundColor(ClosedXML.Excel.XLColor.LightGray);

                int currentRow = 11;

                // Xử lý đơn chi tiết: ĐĂNG KÝ XE CÔNG TÁC SHD
                if (don.ShdDangKySuDungXeCongTac1s.Any())
                {
                    var ct = don.ShdDangKySuDungXeCongTac1s.First();
                    worksheet.Cell(currentRow, 1).Value = "I. CHI TIẾT ĐƠN: ĐĂNG KÝ XE CÔNG TÁC SHD";
                    worksheet.Range(currentRow, 1, currentRow, 2).Merge().Style.Font.SetBold().Fill.SetBackgroundColor(ClosedXML.Excel.XLColor.LightYellow);
                    currentRow++;
                    worksheet.Cell(currentRow, 1).Value = "Số điện thoại:"; worksheet.Cell(currentRow, 2).Value = ct.SoDienThoai; currentRow++;
                    worksheet.Cell(currentRow, 1).Value = "Số lượng người:"; worksheet.Cell(currentRow, 2).Value = ct.SoLuong + " người"; currentRow++;
                    worksheet.Cell(currentRow, 1).Value = "Lý do / Lộ trình:"; worksheet.Cell(currentRow, 2).Value = ct.LiDo; currentRow++;
                    worksheet.Cell(currentRow, 1).Value = "TG Đi dự kiến:"; worksheet.Cell(currentRow, 2).Value = ct.TimeDuTinh?.ToString("dd/MM/yyyy HH:mm"); currentRow++;
                    worksheet.Cell(currentRow, 1).Value = "TG Về dự kiến:"; worksheet.Cell(currentRow, 2).Value = ct.ThoiGianVe?.ToString("dd/MM/yyyy HH:mm"); currentRow++;
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

                // BƯỚC 2: QUẢN LÝ DUYỆT B2 (SHD)
                if (don.ShdQuanLyDuyetB2s != null && don.ShdQuanLyDuyetB2s.Any())
                {
                    worksheet.Cell(currentRow, 1).Value = "II. DANH SÁCH DUYỆT BƯỚC 2 (MGR)";
                    worksheet.Range(currentRow, 1, currentRow, 4).Merge().Style.Font.SetBold().Fill.SetBackgroundColor(ClosedXML.Excel.XLColor.Lavender);
                    currentRow++;

                    worksheet.Cell(currentRow, 1).Value = "Tên Người Duyệt";
                    worksheet.Cell(currentRow, 2).Value = "Thời Gian";
                    worksheet.Cell(currentRow, 3).Value = "Trạng Thái";
                    worksheet.Cell(currentRow, 4).Value = "Ghi Chú";
                    worksheet.Range(currentRow, 1, currentRow, 4).Style.Font.SetBold().Fill.SetBackgroundColor(ClosedXML.Excel.XLColor.LightGray);

                    int startRow = currentRow;
                    foreach (var b2 in don.ShdQuanLyDuyetB2s.OrderBy(x => x.ThuTuXacNhan))
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

                // BƯỚC 3: GIÁM ĐỐC XÁC NHẬN (SHD)
                if (don.ShdNguoiXacNhans != null && don.ShdNguoiXacNhans.Any())
                {
                    worksheet.Cell(currentRow, 1).Value = "III. XÁC NHẬN GIÁM ĐỐC / BAN LÃNH ĐẠO";
                    worksheet.Range(currentRow, 1, currentRow, 4).Merge().Style.Font.SetBold().Fill.SetBackgroundColor(ClosedXML.Excel.XLColor.MistyRose);
                    currentRow++;

                    worksheet.Cell(currentRow, 1).Value = "Tên Giám Đốc";
                    worksheet.Cell(currentRow, 2).Value = "Thời Gian";
                    worksheet.Cell(currentRow, 3).Value = "Trạng Thái";
                    worksheet.Cell(currentRow, 4).Value = "Ghi Chú";
                    worksheet.Range(currentRow, 1, currentRow, 4).Style.Font.SetBold().Fill.SetBackgroundColor(ClosedXML.Excel.XLColor.LightGray);

                    int startRow = currentRow;
                    foreach (var xn in don.ShdNguoiXacNhans)
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
                    return File(stream.ToArray(), "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", $"Don_EFormSHD_{id}.xlsx");
                }
            }
        }

        [HttpGet("/FormSHD/ExportWord/{id}")]
        public async Task<IActionResult> ExportWord(int id)
        {
            var don = await _context.FormShds
                .Include(f => f.ShdDangKySuDungXeCongTac1s)
                .Include(f => f.ShdQuanLyDuyetB2s)
                .Include(f => f.ShdNguoiXacNhans).ThenInclude(x => x.IdnguoiXacNhanNavigation)
                .FirstOrDefaultAsync(m => m.Id == id);

            if (don == null || don.IdAdmin == null)
                return NotFound("Đơn chưa hoàn tất hoặc không tồn tại.");

            string htmlContent = BuildHtmlContentSHD(don, isForWord: true);

            byte[] byteArray = System.Text.Encoding.UTF8.GetBytes(htmlContent);
            return File(byteArray, "application/msword", $"Don_EFormSHD_{id}.doc");
        }

        [HttpGet("/FormSHD/ExportPDF/{id}")]
        public async Task<IActionResult> ExportPDF(int id)
        {
            var don = await _context.FormShds
                .Include(f => f.ShdDangKySuDungXeCongTac1s)
                .Include(f => f.ShdQuanLyDuyetB2s)
                .Include(f => f.ShdNguoiXacNhans).ThenInclude(x => x.IdnguoiXacNhanNavigation)
                .FirstOrDefaultAsync(m => m.Id == id);

            if (don == null || don.IdAdmin == null)
                return NotFound("Đơn chưa hoàn tất hoặc không tồn tại.");

            string htmlContent = BuildHtmlContentSHD(don, isForWord: false);
            return Content(htmlContent, "text/html", System.Text.Encoding.UTF8);
        }

        // ============================================================
        // HÀM HỖ TRỢ BUILD HTML CHUYÊN NGHIỆP CHO SHD
        // ============================================================
        private string BuildHtmlContentSHD(FormShd don, bool isForWord = false)
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

            // Phần Quốc hiệu và Tên bộ phận SHD
            sb.Append("<table class='header-table'><tr>");
            sb.Append("<td style='width:45%;'><div class='company-name'>BEST PACIFIC</div><div class='company-sub'>PHÒNG XUẤT NHẬP KHẨU (SHD)</div></td>");
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

            // II. CHI TIẾT YÊU CẦU XE CÔNG TÁC
            sb.Append("<div class='section-title'>II. NỘI DUNG CHI TIẾT YÊU CẦU</div>");
            sb.Append("<table class='data-table'>");
            if (don.ShdDangKySuDungXeCongTac1s.Any())
            {
                var ct = don.ShdDangKySuDungXeCongTac1s.First();
                sb.Append($"<tr><th>Dịch vụ đăng ký</th><td style='font-weight:bold;'>Xe đi công tác SHD</td></tr>");
                sb.Append($"<tr><th>Số điện thoại liên hệ</th><td>{ct.SoDienThoai}</td></tr>");
                sb.Append($"<tr><th>Số lượng người đi</th><td>{ct.SoLuong} người</td></tr>");
                sb.Append($"<tr><th>Mục đích / Lộ trình</th><td>{ct.LiDo}</td></tr>");
                sb.Append($"<tr><th>Thời gian khởi hành</th><td>{ct.TimeDuTinh?.ToString("HH:mm dd/MM/yyyy")}</td></tr>");
                sb.Append($"<tr><th>Thời gian về dự kiến</th><td>{ct.ThoiGianVe?.ToString("HH:mm dd/MM/yyyy")}</td></tr>");
                sb.Append($"<tr><th>Ghi chú đơn</th><td>{ct.GhiChu}</td></tr>");
            }
            sb.Append("</table>");

            // III. LỊCH SỬ PHÊ DUYỆT
            bool hasB2 = don.ShdQuanLyDuyetB2s != null && don.ShdQuanLyDuyetB2s.Any();
            bool hasGD = don.ShdNguoiXacNhans != null && don.ShdNguoiXacNhans.Any();

            if (hasB2 || hasGD)
            {
                sb.Append("<div class='section-title'>III. TIẾN TRÌNH PHÊ DUYỆT HỆ THỐNG</div>");
                sb.Append("<table class='data-table'>");
                sb.Append("<tr><th style='width:30%'>Vai trò</th><th style='width:30%'>Họ và tên</th><th style='width:20%'>Kết quả</th><th style='width:20%'>Thời gian</th></tr>");

                if (hasB2)
                {
                    foreach (var b2 in don.ShdQuanLyDuyetB2s.OrderBy(x => x.ThuTuXacNhan))
                    {
                        string tt = b2.TrangThaiXacNhan == 1 ? "Đã duyệt" : b2.TrangThaiXacNhan == 2 ? "Từ chối" : "Chờ duyệt";
                        sb.Append($"<tr><td>Quản lý (B2)</td><td>{b2.TenNguoiXacNhan}</td><td>{tt}</td><td>{b2.ThoiGianXacNhan?.ToString("HH:mm dd/MM/yyyy")}</td></tr>");
                    }
                }

                if (hasGD)
                {
                    foreach (var xn in don.ShdNguoiXacNhans)
                    {
                        string tt = xn.TrangThaiXacNhan == 1 ? "Đã duyệt" : xn.TrangThaiXacNhan == 2 ? "Từ chối" : "Chờ duyệt";
                        string tenGD = xn.IdnguoiXacNhanNavigation?.HoTen ?? xn.TenNguoiXacNhan;
                        sb.Append($"<tr><td>Giám đốc</td><td>{tenGD}</td><td>{tt}</td><td>{xn.ThoiGianXacNhan?.ToString("HH:mm dd/MM/yyyy")}</td></tr>");
                    }
                }
                sb.Append("</table>");
            }

            // IV. CHỮ KÝ XÁC NHẬN
            sb.Append("<table class='signature-table'><tr>");
            sb.Append("<td><strong>NGƯỜI LẬP PHIẾU</strong><br/><span style='font-size:10pt;'>(Đã xác thực điện tử)</span><br/><br/><br/><br/><strong>" + don.TenNguoiTao + "</strong></td>");
            sb.Append("<td><strong>QUẢN LÝ TRỰC TIẾP</strong><br/><span style='font-size:10pt;'>(Đã duyệt online)</span><br/><br/><br/><br/><strong>" + (don.TenNguoiDuyet ?? "") + "</strong></td>");

            if (hasGD)
                sb.Append("<td><strong>BAN GIÁM ĐỐC</strong><br/><span style='font-size:10pt;'>(Ký và đóng dấu)</span><br/><br/><br/><br/></td>");
            else
                sb.Append("<td></td>");

            sb.Append("<td><strong>PHÒNG SHD XÁC NHẬN</strong><br/><span style='font-size:10pt;'>(Hệ thống xác nhận)</span><br/><br/><br/><br/><strong>" + (don.TenAdmin ?? "") + "</strong></td>");
            sb.Append("</tr></table>");

            sb.Append("</div></body></html>");
            return sb.ToString();
        }

        #endregion

        #region BÌNH LUẬN ĐƠN SHD

        [HttpGet("/FormSHD/LayBinhLuan/{idForm}")]
        public async Task<IActionResult> LayBinhLuan(int idForm, int skip = 0, int take = 20)
        {
            try
            {
                var binhLuans = await _context.BinhLuanFormShds
                    .Where(bl => bl.IdFormShd == idForm && bl.TrangThai == 1)
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
                return Json(new { success = false, message = "Lỗi tải bình luận: " + ex.Message });
            }
        }

        [HttpPost("/FormSHD/ThemBinhLuan")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ThemBinhLuan()
        {
            try
            {
                var formCollection = await Request.ReadFormAsync();
                int idForm = int.Parse(formCollection["idForm"]);
                string noiDung = formCollection["noiDung"].ToString();
                var file = formCollection.Files.GetFile("file");

                var userIdStr = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                if (string.IsNullOrEmpty(userIdStr))
                    return Json(new { success = false, message = "Chưa đăng nhập" });

                var formShd = await _context.FormShds.FindAsync(idForm);
                if (formShd == null)
                    return Json(new { success = false, message = "Không tìm thấy đơn SHD" });

                string fileName = null;

                if (file != null && file.Length > 0)
                {
                    if (file.Length > 50 * 1024 * 1024)
                        return Json(new { success = false, message = "File không được vượt quá 50MB" });

                    string networkPath = @"\\10.0.60.30\BPVN-Fileserver\Public\IT-Information Technology Dept\5.E-Form\BinhLuanDonSHD";
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

                var binhLuan = new BinhLuanFormShd
                {
                    IdFormShd = idForm,
                    NoiDung = noiDung?.Trim(),
                    IdNguoiBinhLuan = int.Parse(userIdStr),
                    TenNguoiBinhLuan = User.Identity?.Name ?? "Unknown",
                    Ma = User.FindFirst(ClaimTypes.Email)?.Value ?? "",
                    PhongBan = User.FindFirst("PhongBan")?.Value ?? "",
                    TenCongTy = User.FindFirst("TenCongTy")?.Value ?? "",
                    ThoiGian = DateTime.Now,
                    TrangThai = 1,
                    FileDinhKem = fileName
                };

                _context.BinhLuanFormShds.Add(binhLuan);

                _context.LichSuFormShds.Add(new LichSuFormShd
                {
                    IdFormShd = idForm,
                    TieuDe = "BÌNH LUẬN MỚI (SHD)",
                    Mota = $"Người dùng {binhLuan.TenNguoiBinhLuan} đã bình luận: {(noiDung?.Length > 50 ? noiDung.Substring(0, 50) + "..." : noiDung)}",
                    Time = DateTime.Now,
                    TrangThaiAnHien = true
                });

                await _context.SaveChangesAsync();

                // TRẢ VỀ DẠNG ANONYMOUS OBJECT ĐỂ TRÁNH LỖI CIRCULAR REFERENCE
                return Json(new
                {
                    success = true,
                    data = new
                    {
                        id = binhLuan.Id,
                        noiDung = binhLuan.NoiDung,
                        tenNguoiBinhLuan = binhLuan.TenNguoiBinhLuan,
                        idNguoiBinhLuan = binhLuan.IdNguoiBinhLuan,
                        thoiGian = binhLuan.ThoiGian,
                        fileDinhKem = binhLuan.FileDinhKem
                    }
                });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = "Lỗi hệ thống SHD: " + ex.Message });
            }
        }

        [HttpPost("/FormSHD/XoaBinhLuan")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> XoaBinhLuan([FromBody] dynamic data)
        {
            try
            {
                // Sử dụng Newtonsoft để parse dynamic an toàn hơn hoặc dùng model class
                int id = int.Parse(data.GetProperty("id").ToString());

                var userIdStr = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                var userRole = User.FindFirst("UserRole")?.Value ?? "";

                var binhLuan = await _context.BinhLuanFormShds.FindAsync(id);
                if (binhLuan == null) return Json(new { success = false, message = "Không tìm thấy bình luận" });

                int currentUserId = int.Parse(userIdStr ?? "0");
                if (binhLuan.IdNguoiBinhLuan != currentUserId && userRole != "AdminSHD" && userRole != "All")
                    return Json(new { success = false, message = "Bạn không có quyền xóa bình luận này" });

                if (!string.IsNullOrEmpty(binhLuan.FileDinhKem))
                {
                    string networkPath = @"\\10.0.60.30\BPVN-Fileserver\Public\IT-Information Technology Dept\5.E-Form\BinhLuanDonSHD";
                    string fullPath = Path.Combine(networkPath, binhLuan.FileDinhKem);
                    if (System.IO.File.Exists(fullPath)) System.IO.File.Delete(fullPath);
                }

                _context.BinhLuanFormShds.Remove(binhLuan);

                _context.LichSuFormShds.Add(new LichSuFormShd
                {
                    IdFormShd = binhLuan.IdFormShd,
                    TieuDe = "XÓA BÌNH LUẬN (SHD)",
                    Mota = $"{User.Identity.Name} đã xóa một bình luận.",
                    Time = DateTime.Now,
                    TrangThaiAnHien = true
                });

                await _context.SaveChangesAsync();
                return Json(new { success = true, message = "Đã xóa bình luận SHD" });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = "Lỗi hệ thống SHD: " + ex.Message });
            }
        }

        [HttpGet("/FormSHD/DownloadBinhLuanFile/{fileName}")]
        public IActionResult DownloadBinhLuanFile(string fileName)
        {
            if (string.IsNullOrEmpty(fileName)) return NotFound();

            string networkPath = @"\\10.0.60.30\BPVN-Fileserver\Public\IT-Information Technology Dept\5.E-Form\BinhLuanDonSHD";
            string fullPath = Path.Combine(networkPath, fileName);

            if (!System.IO.File.Exists(fullPath)) return NotFound("File SHD không tồn tại");

            string originalFileName = string.Join("_", fileName.Split('_').Skip(2));
            return PhysicalFile(fullPath, "application/octet-stream", originalFileName);
        }

        #endregion

        #region ĐƠN CHỜ XÉT DUYỆT SHD (Dành cho nhân viên xem danh sách đơn đã tạo)

        // 1. TRẢ VỀ VIEW RỖNG CHO JS TỰ RENDER
        [HttpGet("/FormSHD/DonCho")]
        public IActionResult DonCho()
        {
            if (User == null || User.Identity == null || !User.Identity.IsAuthenticated)
                return Redirect("/DonXetDuyet/DangNhap");

            return View();
        }

        // 2. API TRẢ VỀ JSON ĐÃ TÍNH TOÁN SẴN LOGIC B2/GĐ (100% JS) - KHÔNG CÓ BẢO VỆ
        [HttpGet("/FormSHD/GetDonChoData")]
        public async Task<IActionResult> GetDonChoData()
        {
            var userIdClaim = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value
                              ?? User.FindFirst("UserId")?.Value;

            if (string.IsNullOrEmpty(userIdClaim))
                return Unauthorized();

            try
            {
                int currentUserId = int.Parse(userIdClaim);

                var danhSachDon = await _context.FormShds
                    .Include(f => f.ShdNguoiXacNhans).ThenInclude(xn => xn.IdnguoiXacNhanNavigation)
                    .Include(f => f.ShdQuanLyDuyetB2s)
                    .Include(f => f.ShdCtNguoiHoTros).ThenInclude(ct => ct.IdShdNguoiHoTroNavigation)
                    .Where(f => f.IdNguoiTao == currentUserId)
                    .OrderByDescending(f => f.Id)
                    .AsNoTracking()
                    .ToListAsync();

                // Tính toán toàn bộ logic của Razor cũ tại Server để JS chỉ việc hiển thị
                var result = danhSachDon.Select(item =>
                {
                    // --- LOGIC B2 (AND/OR) ---
                    bool hasB2 = item.ShdQuanLyDuyetB2s != null && item.ShdQuanLyDuyetB2s.Any();
                    bool checkB2Approved = false;
                    if (!hasB2) checkB2Approved = true;
                    else
                    {
                        string type = item.ShdQuanLyDuyetB2s.FirstOrDefault()?.Loai?.ToUpper() ?? "AND";
                        if (type == "OR" || type == "ANY")
                            checkB2Approved = item.ShdQuanLyDuyetB2s.Any(x => x.TrangThaiXacNhan == 1);
                        else
                            checkB2Approved = item.ShdQuanLyDuyetB2s.All(x => x.TrangThaiXacNhan == 1);
                    }
                    bool checkB2Rejected = hasB2 && item.ShdQuanLyDuyetB2s.Any(x => x.TrangThaiXacNhan == 2);

                    // --- LOGIC GIÁM ĐỐC ---
                    bool hasGD = item.ShdNguoiXacNhans != null && item.ShdNguoiXacNhans.Any();
                    bool isGDApproved = hasGD && item.ShdNguoiXacNhans.All(x => x.TrangThaiXacNhan == 1);
                    bool isGDRejected = hasGD && item.ShdNguoiXacNhans.Any(x => x.TrangThaiXacNhan == 2);

                    // --- TRẠNG THÁI CHUNG ---
                    bool isCancelled = (item.TenForm ?? "").Contains("[ĐÃ HỦY]") || item.TrangThai == "DaHuy" || item.TrangThai == "Huy";
                    bool isFinished = item.IdAdmin != null || item.TrangThai == "HoanTat" || item.TrangThai == "DaXuLy";
                    bool isManagerApproved = item.IdNguoiDuyet != null;

                    string statusText, bgColor, fgColor, progressWidth, progressColor;

                    // Phân loại trạng thái giống hệt if-else của Razor
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
                        statusText = "BP XÁC NHẬN"; bgColor = "#ecfeff"; fgColor = "#0891b2";
                        progressWidth = "40%"; progressColor = "#22d3ee";
                    }
                    else if (hasGD && !isGDApproved)
                    {
                        statusText = "GIÁM ĐỐC"; bgColor = "#fdf4ff"; fgColor = "#86198f";
                        progressWidth = "60%"; progressColor = "#d946ef";
                    }
                    else
                    {
                        statusText = "ADMIN"; bgColor = "#e0e7ff"; fgColor = "#312e81";
                        progressWidth = "85%"; progressColor = "#312e81";
                    }

                    // --- NGƯỜI HỖ TRỢ ---
                    var lastSupportShd = item.ShdCtNguoiHoTros?.OrderByDescending(x => x.Stt).FirstOrDefault();
                    string supportName = lastSupportShd?.IdShdNguoiHoTroNavigation?.Ten ?? "Chờ phân công";

                    return new
                    {
                        Id = item.Id,
                        TenForm = (item.TenForm ?? "").Replace("[ĐÃ HỦY]", "").Trim(),
                        Danhmuc = item.Danhmuc ?? "Biểu mẫu SHD",
                        StatusText = statusText,
                        BgColor = bgColor,
                        FgColor = fgColor,
                        ProgressWidth = progressWidth,
                        ProgressColor = progressColor,
                        SupportName = supportName,
                        Ngay = item.Ngay?.ToDateTime(TimeOnly.MinValue).ToString("dd/MM/yyyy") ?? "--",
                        TimeNguoiTao = item.TimeNguoiTao?.ToString("HH:mm") ?? "--",
                        HasB2 = hasB2,
                        B2ApprovedCount = hasB2 ? item.ShdQuanLyDuyetB2s.Count(x => x.TrangThaiXacNhan == 1) : 0,
                        B2TotalCount = hasB2 ? item.ShdQuanLyDuyetB2s.Count() : 0,
                        HasGD = hasGD,
                        GDApprovedCount = hasGD ? item.ShdNguoiXacNhans.Count(x => x.TrangThaiXacNhan == 1) : 0,
                        GDTotalCount = hasGD ? item.ShdNguoiXacNhans.Count() : 0,
                        HasBV = false, // SHD không dùng bảo vệ
                        BVStatusText = ""
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

        #region XỬ LÝ ĐƠN SHD (Duyệt / Hủy / Hoàn tất) - PHÂN QUYỀN SHD 2026

        // 1. TRẢ VỀ VIEW RỖNG (Cho JS tự render)
        [HttpGet("/FormSHD/QuanLyXetDuyet")]
        public IActionResult QuanLyXetDuyet()
        {
            var userIdStr = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userIdStr)) return Redirect("/DonXetDuyet/DangNhap");
            return View();
        }

        // 2. API TRẢ DỮ LIỆU ĐÃ TÍNH TOÁN (100% JS)
        [HttpGet("/FormSHD/GetQuanLyXetDuyetData")]
        public async Task<IActionResult> GetQuanLyXetDuyetData()
        {
            var userIdStr = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userIdStr)) return Unauthorized();
            int userId = int.Parse(userIdStr);

            var phongBan = User.FindFirst("PhongBan")?.Value?.Trim() ?? "";
            var tenCongTy = User.FindFirst("TenCongTy")?.Value?.Trim() ?? "";

            var listTenBoPhanStr = User.FindFirst("TenBoPhan")?.Value ?? "";
            var listTenBoPhan = listTenBoPhanStr.Split(',')
                                                .Select(s => s.Trim().ToLower())
                                                .Where(s => !string.IsNullOrEmpty(s))
                                                .ToList();

            // --- TRUY VẤN DỮ LIỆU SHD ---
            IQueryable<E_Form_Best.Models.ITForm.FormShd> query = _context.FormShds
                .Include(f => f.ShdNguoiXacNhans).ThenInclude(xn => xn.IdnguoiXacNhanNavigation)
                .Include(f => f.ShdQuanLyDuyetB2s)
                .Include(f => f.ShdCtNguoiHoTros).ThenInclude(ct => ct.IdShdNguoiHoTroNavigation);

            // --- LOGIC PHÂN QUYỀN SHD ---
            if (!User.IsInRole("All"))
            {
                if (!string.IsNullOrEmpty(tenCongTy))
                {
                    query = query.Where(f => f.TenCongTy == tenCongTy);
                }

                if (User.IsInRole("QuanLyDuyetDonSHD"))
                {
                    if (listTenBoPhan.Any()) query = query.Where(f => f.BoPhan != null && listTenBoPhan.Contains(f.BoPhan.Trim().ToLower()));
                    else if (!string.IsNullOrEmpty(phongBan)) query = query.Where(f => f.BoPhan != null && f.BoPhan.Trim().ToLower() == phongBan.ToLower());
                    else query = query.Where(f => f.IdNguoiTao == userId);
                }
                else
                {
                    query = query.Where(f => f.IdNguoiTao == userId);
                }
            }

            var danhSachDon = await query.OrderByDescending(f => f.Id).AsNoTracking().ToListAsync();

            // --- TÍNH TOÁN LOGIC B2, GĐ, TRẠNG THÁI TẠI SERVER (SHD VERSION) ---
            var result = danhSachDon.Select(item =>
            {
                bool hasB2 = item.ShdQuanLyDuyetB2s != null && item.ShdQuanLyDuyetB2s.Any();
                bool checkB2Approved = true;
                if (hasB2)
                {
                    string type = item.ShdQuanLyDuyetB2s.FirstOrDefault()?.Loai?.ToUpper() ?? "AND";
                    if (type == "OR" || type == "ANY") checkB2Approved = item.ShdQuanLyDuyetB2s.Any(x => x.TrangThaiXacNhan == 1);
                    else checkB2Approved = item.ShdQuanLyDuyetB2s.All(x => x.TrangThaiXacNhan == 1);
                }
                bool checkB2Rejected = hasB2 && item.ShdQuanLyDuyetB2s.Any(x => x.TrangThaiXacNhan == 2);

                bool hasGD = item.ShdNguoiXacNhans != null && item.ShdNguoiXacNhans.Any();
                bool isGDApproved = hasGD && item.ShdNguoiXacNhans.All(x => x.TrangThaiXacNhan == 1);
                bool isGDRejected = hasGD && item.ShdNguoiXacNhans.Any(x => x.TrangThaiXacNhan == 2);

                bool isCancelled = (item.TenForm ?? "").Contains("[ĐÃ HỦY]") || item.TrangThai == "DaHuy" || item.TrangThai == "Huy";
                bool isFinished = item.IdAdmin != null || item.TrangThai == "HoanTat" || item.TrangThai == "DaXuLy";
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
                    pWidth = "40%"; pColor = "#22d3ee"; pText = "BP XÁC NHẬN"; bg = "#ecfeff"; fg = "#0891b2"; statusTag = "BP XÁC NHẬN";
                }
                else if (hasGD && !isGDApproved)
                {
                    pWidth = "65%"; pColor = "#d946ef"; pText = "GIÁM ĐỐC"; bg = "#fdf4ff"; fg = "#86198f"; statusTag = "GIÁM ĐỐC";
                }
                else
                {
                    pWidth = "85%"; pColor = "#312e81"; pText = "CHỜ ADMIN"; bg = "#e0e7ff"; fg = "#312e81"; statusTag = "ADMIN";
                }

                var supportLog = item.ShdCtNguoiHoTros?.OrderByDescending(x => x.Stt).FirstOrDefault();
                string supportName = supportLog?.IdShdNguoiHoTroNavigation?.Ten ?? "Chưa chỉ định";

                return new
                {
                    Id = item.Id,
                    TenNguoiNv = item.TenNguoiNv ?? "N/A",
                    BoPhan = item.BoPhan ?? "N/A",
                    SoNhanVien = item.SoNhanVien ?? "N/A",
                    TenForm = (item.TenForm ?? "").Replace("[ĐÃ HỦY]", "").Trim(),
                    TimeNguoiTao = item.TimeNguoiTao?.ToString("dd/MM/yyyy") ?? "--",
                    HasB2 = hasB2,
                    B2ApprovedCount = hasB2 ? item.ShdQuanLyDuyetB2s.Count(x => x.TrangThaiXacNhan == 1) : 0,
                    B2TotalCount = hasB2 ? item.ShdQuanLyDuyetB2s.Count() : 0,
                    HasGD = hasGD,
                    GDApprovedCount = hasGD ? item.ShdNguoiXacNhans.Count(x => x.TrangThaiXacNhan == 1) : 0,
                    GDTotalCount = hasGD ? item.ShdNguoiXacNhans.Count() : 0,
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

        // --- HÀM XỬ LÝ ĐƠN POST CỦA SHD ---
        [HttpPost("/FormSHD/XuLyDon")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> XuLyDon([FromBody] SHDApprovalRequest request)
        {
            if (request == null || request.Id <= 0)
                return Json(new { success = false, message = "Dữ liệu không hợp lệ." });

            var userIdStr = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userIdStr))
                return Json(new { success = false, message = "Hết phiên đăng nhập." });

            int userId = int.Parse(userIdStr);
            var userName = User.Identity.Name ?? "N/A";
            var userEmail = User.FindFirst(System.Security.Claims.ClaimTypes.Email)?.Value ?? "";
            var tenCongTyUser = User.FindFirst("TenCongTy")?.Value?.Trim() ?? "";
            var phongBanUser = User.FindFirst("TenBoPhan")?.Value ?? "N/A";

            var form = await _context.FormShds
                .Include(f => f.ShdCtNguoiHoTros).ThenInclude(ct => ct.IdShdNguoiHoTroNavigation)
                .FirstOrDefaultAsync(f => f.Id == request.Id);

            if (form == null) return Json(new { success = false, message = "Không tìm thấy đơn SHD." });

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
                        if (!User.IsInRole("All") && !User.IsInRole("AdminSHD") && !User.IsInRole("QuanLyDuyetDonSHD"))
                            return Json(new { success = false, message = "Bạn không có quyền phê duyệt đơn này." });

                        form.IdNguoiDuyet = userId;
                        form.TenNguoiDuyet = userName;
                        form.TimeNguoiDuyet = now;
                        form.TrangThai = "DaDuyet";

                        tieuDeLichSu = "Phê duyệt đơn SHD";
                        moTaChiTiet = $"Người duyệt: {userName}({userEmail}). Bộ phận xử lý: {phongBanUser}.";
                    }
                    else if (request.Action == "Huy")
                    {
                        bool isOwner = form.IdNguoiTao == userId;
                        bool canApprove = User.IsInRole("All") || User.IsInRole("AdminSHD") || User.IsInRole("QuanLyDuyetDonSHD");

                        if (!isOwner && !canApprove)
                            return Json(new { success = false, message = "Bạn không có quyền hủy đơn này." });

                        form.TrangThai = "DaHuy";
                        if (form.TenForm != null && !form.TenForm.StartsWith("[ĐÃ HỦY]"))
                            form.TenForm = "[ĐÃ HỦY] " + form.TenForm;

                        tieuDeLichSu = "Hủy đơn SHD";
                        moTaChiTiet = $"Người hủy: {userName}({userEmail}). Lý do: {request.Reason}.";
                    }
                    else if (request.Action == "HoanTat")
                    {
                        bool isAdmin = User.IsInRole("All") || User.IsInRole("AdminSHD");
                        bool isSupporter = form.ShdCtNguoiHoTros.Any(ct => ct.IdShdNguoiHoTroNavigation?.MaNv == userEmail);

                        if (!isAdmin && !isSupporter)
                            return Json(new { success = false, message = "Chỉ nhân sự SHD được gán hỗ trợ hoặc Admin mới có thể hoàn tất." });

                        form.IdAdmin = userId;
                        form.TenAdmin = userName;
                        form.TimeAdmin = now;
                        form.TrangThai = "HoanTat";

                        tieuDeLichSu = "Hoàn tất đơn SHD";
                        moTaChiTiet = $"Đơn đã được xử lý xong bởi: {userName}({userEmail}).";
                    }

                    _context.LichSuFormShds.Add(new LichSuFormShd
                    {
                        IdFormShd = form.Id,
                        TieuDe = tieuDeLichSu,
                        Mota = moTaChiTiet,
                        Time = now,
                        TrangThaiAnHien = true // Luôn hiển thị log thao tác mới
                    });

                    _context.FormShds.Update(form);
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

        public class SHDApprovalRequest
        {
            public int Id { get; set; }
            public string Action { get; set; } // Duyet, Huy, HoanTat
            public string Reason { get; set; }
        }

        #endregion

        #region XỬ LÝ ĐƠN SHD B2 - PHÂN QUYỀN MỚI 2026

        // 1. TRẢ VỀ VIEW RỖNG (Cho JavaScript tự Render)
        [HttpGet("/FormSHD/QuanLyXetDuyetB2")]
        public IActionResult QuanLyXetDuyetB2()
        {
            var userIdStr = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userIdStr)) return Redirect("/DonXetDuyet/DangNhap");
            return View();
        }

        // 2. API TRẢ VỀ JSON ĐÃ TÍNH TOÁN LOGIC (100% JS)
        // 2. API TRẢ VỀ JSON ĐÃ TÍNH TOÁN LOGIC (100% JS)
        [HttpGet("/FormSHD/GetQuanLyXetDuyetB2Data")]
        public async Task<IActionResult> GetQuanLyXetDuyetB2Data()
        {
            var userIdStr = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userIdStr)) return Unauthorized();

            int userId = int.Parse(userIdStr);
            var userEmail = (User.FindFirst(System.Security.Claims.ClaimTypes.Email)?.Value ?? "").Trim().ToLower();
            var tenCongTy = User.FindFirst("TenCongTy")?.Value?.Trim() ?? "";

            bool isAll = User.IsInRole("All");
            bool isQuanLyB2 = User.IsInRole("QuanLyDuyetDonSHD_B2");

            IQueryable<E_Form_Best.Models.ITForm.FormShd> query = _context.FormShds
                .Include(f => f.ShdNguoiXacNhans).ThenInclude(xn => xn.IdnguoiXacNhanNavigation)
                // CẬP NHẬT: Include thêm bảng ủy quyền để phục vụ kiểm tra điều kiện lọc dữ liệu
                .Include(f => f.ShdQuanLyDuyetB2s).ThenInclude(b2 => b2.ShdQuanLyDuyetB2UyQuyens)
                .Include(f => f.ShdCtNguoiHoTros).ThenInclude(ct => ct.IdShdNguoiHoTroNavigation);

            // Điều kiện tiên quyết: Đã qua Quản lý trực tiếp
            query = query.Where(f => f.IdNguoiDuyet != null);

            // Lọc theo quyền (B2) - CẬP NHẬT THÊM ĐIỀU KIỆN ỦY QUYỀN
            query = query.Where(f =>
                isAll ||
                (isQuanLyB2 && f.ShdQuanLyDuyetB2s.Any(b2 =>
                    (b2.MaNguoiXacNhan != null && b2.MaNguoiXacNhan.ToLower() == userEmail) ||
                    b2.IdnguoiXacNhan == userId ||
                    // MỚI: Nếu userEmail trùng với MaNvuyQuyen trong danh sách ủy quyền thì cũng hiển thị đơn
                    b2.ShdQuanLyDuyetB2UyQuyens.Any(uq => uq.MaNvuyQuyen != null && uq.MaNvuyQuyen.ToLower() == userEmail)
                ))
            );

            // Lọc công ty
            if (!isAll && !string.IsNullOrEmpty(tenCongTy))
            {
                query = query.Where(f => f.TenCongTy == tenCongTy);
            }

            var danhSachDon = await query.OrderByDescending(f => f.Id).AsNoTracking().ToListAsync();

            // Tính toán trước toàn bộ Giao diện / Logic từ Server (Màu sắc theo Indigo/Cyan của SHD)
            var result = danhSachDon.Select(item =>
            {
                bool isCancelled = (item.TenForm ?? "").Contains("[ĐÃ HỦY]") || item.TrangThai == "DaHuy" || item.TrangThai == "Huy";
                bool isFinished = item.IdAdmin != null || item.TrangThai == "HoanTat" || item.TrangThai == "DaXuLy";

                var b2List = item.ShdQuanLyDuyetB2s ?? new List<E_Form_Best.Models.ITForm.ShdQuanLyDuyetB2>();
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

                var gdList = item.ShdNguoiXacNhans ?? new List<E_Form_Best.Models.ITForm.ShdNguoiXacNhan>();
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
                    pWidth = "40%"; pColor = "#22d3ee"; pText = "CHỜ B2 XÁC NHẬN"; bg = "#ecfeff"; fg = "#0891b2"; statusTag = "BP XÁC NHẬN";
                }
                else if (hasGD && !isGDApproved)
                {
                    pWidth = "65%"; pColor = "#d946ef"; pText = "CHỜ GIÁM ĐỐC"; bg = "#fdf4ff"; fg = "#86198f"; statusTag = "GIÁM ĐỐC";
                }
                else
                {
                    pWidth = "85%"; pColor = "#312e81"; pText = "CHỜ ADMIN"; bg = "#e0e7ff"; fg = "#312e81"; statusTag = "ADMIN";
                }

                var supportLog = item.ShdCtNguoiHoTros?.OrderByDescending(x => x.Stt).FirstOrDefault();
                string supportName = supportLog?.IdShdNguoiHoTroNavigation?.Ten ?? "Chưa chỉ định";

                return new
                {
                    Id = item.Id,
                    TenNguoiNv = item.TenNguoiNv ?? "N/A",
                    BoPhan = item.BoPhan ?? "N/A",
                    SoNhanVien = item.SoNhanVien ?? "N/A",
                    TenForm = (item.TenForm ?? "").Replace("[ĐÃ HỦY]", "").Trim(),
                    TimeNguoiTao = item.TimeNguoiTao?.ToString("dd/MM/yyyy") ?? "--",
                    HasB2 = hasB2,
                    B2DuyetCount = hasB2 ? b2List.Count(x => x.TrangThaiXacNhan != null && x.TrangThaiXacNhan != 0) : 0,
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


        #region DUYỆT BƯỚC 2 SHD (SHD_QuanLyDuyetB2)
        public class SHDDuyetB2Request
        {
            public int idB2 { get; set; }
            public int idForm { get; set; }
            public string loaiHanhDong { get; set; } = ""; // Approve / Reject
        }

        [HttpPost("/FormSHD/DuyetB2")]
        public async Task<IActionResult> DuyetB2([FromBody] SHDDuyetB2Request req)
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
                // 2. Load record B2 và đơn liên quan, bao gồm cấu hình Ủy quyền
                var record = await _context.ShdQuanLyDuyetB2s
                    .Include(x => x.IdFormShdNavigation)
                    .Include(x => x.ShdQuanLyDuyetB2UyQuyens)
                    .FirstOrDefaultAsync(x => x.Id == req.idB2);

                if (record == null)
                    return Json(new { success = false, message = "⚠️ Không tìm thấy bản ghi duyệt (B2 SHD)." });

                if (record.IdFormShd != req.idForm)
                    return Json(new { success = false, message = "⚠️ Bản ghi không thuộc đơn SHD này." });

                var don = record.IdFormShdNavigation;
                if (don == null)
                    return Json(new { success = false, message = "⚠️ Không tìm thấy đơn SHD liên quan." });

                // 3. Kiểm tra trạng thái: Chỉ xử lý nếu đang chờ (0 hoặc null)
                if (record.TrangThaiXacNhan != null && record.TrangThaiXacNhan != 0)
                    return Json(new { success = false, message = "⚠️ Bước này đã được xử lý trước đó rồi." });

                // 4. KIỂM TRA QUYỀN DUYỆT (Bản thân người duyệt hoặc Người được Ủy quyền)
                var assignedMa = (record.MaNguoiXacNhan ?? "").Trim();
                bool isRightUser = !string.IsNullOrEmpty(assignedMa) && string.Equals(assignedMa, userEmail, StringComparison.OrdinalIgnoreCase);

                bool isNguoiDuocUyQuyen = false;
                if (!isRightUser && record.ShdQuanLyDuyetB2UyQuyens != null && record.ShdQuanLyDuyetB2UyQuyens.Any())
                {
                    isNguoiDuocUyQuyen = record.ShdQuanLyDuyetB2UyQuyens.Any(uq =>
                        !string.IsNullOrEmpty(uq.MaNvuyQuyen) &&
                        string.Equals(uq.MaNvuyQuyen.Trim(), userEmail, StringComparison.OrdinalIgnoreCase));
                }

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

                // Gán mặc định ghi chú nếu là Duyệt hoặc Từ chối
                record.GhiChu = isApprove ? "Đã duyệt" : "Từ chối";

                // Chuẩn bị biến Lịch sử
                string tieuDeLS;
                string moTaLS;
                string nguoiDaiDien = record.TenNguoiXacNhan ?? assignedMa;

                if (!isApprove) // Hành động: TỪ CHỐI (Reject)
                {
                    don.TrangThai = "Huy";
                    if (don.TenForm != null && !don.TenForm.Contains("[ĐÃ HỦY]"))
                    {
                        don.TenForm += " [ĐÃ HỦY]";
                    }

                    tieuDeLS = "BƯỚC 2 SHD — TỪ CHỐI";
                    // Lịch sử hiện ra ngoài vẫn ghi tên người quản lý thật sự
                    moTaLS = $"{nguoiDaiDien} đã TỪ CHỐI duyệt bước 2 SHD.";
                }
                else // Hành động: DUYỆT (Approve)
                {
                    tieuDeLS = "BƯỚC 2 SHD — ĐÃ DUYỆT";
                    moTaLS = $"{nguoiDaiDien} đã DUYỆT bước 2 SHD (thứ tự: {record.ThuTuXacNhan}).";

                    // Kiểm tra quy tắc duyệt AND / OR (Nếu có nhiều người duyệt chung 1 cấp)
                    string type = record.Loai?.ToUpper() ?? "AND";
                    bool conAiChuaDuyet;

                    if (type == "OR" || type == "ANY")
                    {
                        // Quy tắc OR: Đã duyệt 1 người là coi như toàn bộ cấp đó xong, không chờ ai nữa
                        conAiChuaDuyet = false;
                    }
                    else
                    {
                        // Quy tắc AND (Mặc định): Cần check xem những người khác CÙNG CẤP (ThuTuXacNhan) đã duyệt hết chưa
                        conAiChuaDuyet = await _context.ShdQuanLyDuyetB2s
                            .AnyAsync(x => x.IdFormShd == req.idForm
                                        && x.Id != record.Id
                                        && x.ThuTuXacNhan == record.ThuTuXacNhan // Cùng 1 cấp B2
                                        && (x.TrangThaiXacNhan == null || x.TrangThaiXacNhan == 0));
                    }

                    // Nếu không còn ai chưa duyệt (hoặc là dạng OR), thì chuyển bước
                    if (!conAiChuaDuyet)
                    {
                        don.TrangThai = "DaDuyet"; // Chuyển đơn sang trạng thái đã duyệt (Chờ ADMIN SHD)
                        tieuDeLS = "BƯỚC 2 SHD — HOÀN TẤT";
                        moTaLS = "Toàn bộ Bước 2 SHD đã duyệt xong. Đơn chuyển sang trạng thái Chờ ADMIN SHD xử lý.";
                    }
                }

                // 6. Lưu dữ liệu với Transaction
                using var tx = await _context.Database.BeginTransactionAsync();
                try
                {
                    _context.Update(record);
                    _context.Update(don);

                    // 6.1 Ghi lịch sử công khai (Hiển thị thật)
                    _context.LichSuFormShds.Add(new LichSuFormShd
                    {
                        IdFormShd = don.Id,
                        TieuDe = tieuDeLS,
                        Mota = moTaLS,
                        Time = DateTime.Now,
                        TrangThaiAnHien = true // true: Sẽ được hiển thị
                    });

                    // 6.2 Ghi lịch sử ẩn nếu người thao tác là NGƯỜI ĐƯỢC ỦY QUYỀN
                    if (isNguoiDuocUyQuyen)
                    {
                        _context.LichSuFormShds.Add(new LichSuFormShd
                        {
                            IdFormShd = don.Id,
                            TieuDe = "SYSTEM: LƯU VẾT ỦY QUYỀN BƯỚC 2",
                            Mota = $"Tài khoản ủy quyền [{userName} - {userEmail}] đã thao tác {(isApprove ? "DUYỆT" : "TỪ CHỐI")} thay cho quản lý [{nguoiDaiDien}].",
                            Time = DateTime.Now,
                            TrangThaiAnHien = false // false: Lịch sử này ẩn, chỉ Admin thấy nếu cấp quyền All
                        });
                    }

                    await _context.SaveChangesAsync();
                    await tx.CommitAsync();

                    return Json(new
                    {
                        success = true,
                        message = isApprove ? "✅ Đã duyệt Bước 2 SHD thành công!" : "❌ Đã từ chối đơn SHD thành công!",
                        trangThai = newStatus
                    });
                }
                catch (Exception ex)
                {
                    await tx.RollbackAsync();
                    return Json(new { success = false, message = "❌ Lỗi khi lưu dữ liệu SHD: " + ex.Message });
                }
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = "❌ Lỗi hệ thống SHD: " + ex.Message });
            }
        }
        #endregion
        #endregion

        #region QUẢN LÝ PHÊ DUYỆT SHD (Admin, All, Quản lý)

        // 1. TRẢ VỀ VIEW RỖNG (Dành cho trang danh sách xử lý của SHD)
        [HttpGet("/FormSHD/HoanTatDon")]
        public IActionResult HoanTatDon()
        {
            var userIdStr = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value
                            ?? User.FindFirst("UserId")?.Value;

            if (string.IsNullOrEmpty(userIdStr)) return Redirect("/DonXetDuyet/DangNhap");

            return View();
        }

        // 2. API TRẢ VỀ DỮ LIỆU JSON CHO DASHBOARD SHD
        [HttpGet("/FormSHD/GetHoanTatDonData")]
        public async Task<IActionResult> GetHoanTatDonData()
        {
            var userIdStr = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value
                            ?? User.FindFirst("UserId")?.Value;
            if (string.IsNullOrEmpty(userIdStr)) return Unauthorized();

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

            // Đánh dấu các quyền SHD độc lập
            bool isAll = userRoles.Contains("All");
            bool isAdminSHD = userRoles.Contains("AdminSHD");
            bool isQuanLyB2 = userRoles.Contains("QuanLyDuyetDonSHD_B2");
            bool isQuanLyDuyet = userRoles.Contains("QuanLyDuyetDonSHD");
            bool isGiamDocSHD = userRoles.Contains("GiamDocSHD");

            // --- KHỞI TẠO QUERY SHD ---
            IQueryable<E_Form_Best.Models.ITForm.FormShd> query = _context.FormShds
                .Include(f => f.ShdNguoiXacNhans).ThenInclude(xn => xn.IdnguoiXacNhanNavigation)
                .Include(f => f.ShdQuanLyDuyetB2s)
                .Include(f => f.ShdCtNguoiHoTros).ThenInclude(ct => ct.IdShdNguoiHoTroNavigation);

            // --- ĐIỀU KIỆN CƠ BẢN: QUẢN LÝ TRỰC TIẾP ĐÃ DUYỆT BƯỚC 1 ---
            query = query.Where(f => f.IdNguoiDuyet != null && f.TenNguoiDuyet != null && f.TimeNguoiDuyet != null);

            // --- LOGIC PHÂN QUYỀN TRUY CẬP DỮ LIỆU SHD ---
            query = query.Where(f =>
                isAll || isAdminSHD || // Admin SHD thấy toàn bộ đơn đã qua bước 1

                // Quyền B2 SHD: Thấy đơn có tên trong danh sách xác nhận B2
                (isQuanLyB2 && f.ShdQuanLyDuyetB2s.Any(b2 => b2.IdnguoiXacNhan == userId || (b2.MaNguoiXacNhan != null && b2.MaNguoiXacNhan.ToLower() == userEmail))) ||

                // Quyền Giám đốc SHD: Thấy đơn cần mình ký xác nhận
                (isGiamDocSHD && f.ShdNguoiXacNhans.Any(xn => xn.IdnguoiXacNhan == userId || (xn.MaNguoiXacNhan != null && xn.MaNguoiXacNhan.ToLower() == userEmail))) ||

                // Quyền Quản lý duyệt: Theo bộ phận hoặc đơn mình tạo
                (isQuanLyDuyet && (
                    f.IdNguoiTao == userId ||
                    (listTenBoPhan.Any() && f.BoPhan != null && listTenBoPhan.Contains(f.BoPhan.Trim().ToLower())) ||
                    (!listTenBoPhan.Any() && !string.IsNullOrEmpty(phongBanSession) && f.BoPhan != null && f.BoPhan.Trim().ToLower() == phongBanSession.ToLower())
                )) ||

                // Nhân viên SHD hỗ trợ hoặc Người tạo
                (f.IdNguoiTao == userId || f.ShdCtNguoiHoTros.Any(ct => ct.IdShdNguoiHoTroNavigation.MaNv.ToLower() == userEmail))
            );

            // Lọc theo công ty (BPVN/PFVN) nếu không phải quyền Global
            if (!isAll && !isAdminSHD && !string.IsNullOrEmpty(tenCongTy))
            {
                query = query.Where(f => f.TenCongTy == tenCongTy);
            }

            var danhSachDon = await query.OrderByDescending(f => f.Id).AsNoTracking().ToListAsync();

            // --- MAP SANG DỮ LIỆU JSON & TÍNH TOÁN TRẠNG THÁI TIẾN TRÌNH ---
            var result = danhSachDon.Select(item =>
            {
                bool isCancelled = (item.TenForm ?? "").Contains("[ĐÃ HỦY]") || item.TrangThai == "DaHuy" || item.TrangThai == "Huy";
                bool isFinished = item.IdAdmin != null || item.TrangThai == "HoanTat" || item.TrangThai == "DaXuLy";
                bool isManagerApproved = item.IdNguoiDuyet != null;

                // Tính toán Bước 2
                bool hasB2 = item.ShdQuanLyDuyetB2s != null && item.ShdQuanLyDuyetB2s.Any();
                string b2Type = hasB2 ? (item.ShdQuanLyDuyetB2s.FirstOrDefault()?.Loai?.ToUpper() ?? "AND") : "AND";
                bool isB2Approved = false;
                if (hasB2)
                {
                    if (b2Type == "OR" || b2Type == "ANY") isB2Approved = item.ShdQuanLyDuyetB2s.Any(x => x.TrangThaiXacNhan == 1);
                    else isB2Approved = item.ShdQuanLyDuyetB2s.All(x => x.TrangThaiXacNhan == 1);
                }
                bool isB2Rejected = hasB2 && item.ShdQuanLyDuyetB2s.Any(x => x.TrangThaiXacNhan == 2);

                // Tính toán Giám đốc
                bool hasGD = item.ShdNguoiXacNhans != null && item.ShdNguoiXacNhans.Any();
                bool isGDApproved = hasGD && item.ShdNguoiXacNhans.All(x => x.TrangThaiXacNhan == 1);
                bool isGDRejected = hasGD && item.ShdNguoiXacNhans.Any(x => x.TrangThaiXacNhan == 2);

                // Định dạng hiển thị Progress Bar và Badge
                string pWidth, pColor, pText, bg, fg, statusTag;
                if (isCancelled || isB2Rejected || isGDRejected)
                {
                    pWidth = "100%"; pColor = "#ef4444"; pText = "ĐÃ HỦY/TỪ CHỐI"; bg = "#fee2e2"; fg = "#b91c1c"; statusTag = "ĐÃ HỦY";
                }
                else if (isFinished)
                {
                    pWidth = "100%"; pColor = "#10b981"; pText = "HOÀN TẤT"; bg = "#ecfdf5"; fg = "#047857"; statusTag = "HOÀN TẤT";
                }
                else if (!isManagerApproved)
                {
                    pWidth = "25%"; pColor = "#f59e0b"; pText = "CHỜ QL DUYỆT"; bg = "#fef3c7"; fg = "#b45309"; statusTag = "CHỜ QL";
                }
                else if (hasB2 && !isB2Approved)
                {
                    pWidth = "45%"; pColor = "#0ea5e9"; pText = "BỘ PHẬN DUYỆT"; bg = "#e0f2fe"; fg = "#0369a1"; statusTag = "BƯỚC 2";
                }
                else if (hasGD && !isGDApproved)
                {
                    pWidth = "70%"; pColor = "#d946ef"; pText = "GIÁM ĐỐC"; bg = "#fdf4ff"; fg = "#86198f"; statusTag = "GIÁM ĐỐC";
                }
                else
                {
                    pWidth = "85%"; pColor = "#1e40af"; pText = "CHỜ SHD XỬ LÝ"; bg = "#dbeafe"; fg = "#1e40af"; statusTag = "CHỜ SHD";
                }

                var latestSupport = item.ShdCtNguoiHoTros?.OrderByDescending(x => x.Stt).FirstOrDefault();

                return new
                {
                    Id = item.Id,
                    IdForm = item.IdForm,
                    TenNguoiNv = item.TenNguoiNv ?? "N/A",
                    BoPhan = item.BoPhan ?? "N/A",
                    SoNhanVien = item.SoNhanVien ?? "N/A",
                    TenForm = (item.TenForm ?? "").Replace("[ĐÃ HỦY]", "").Trim(),
                    TimeNguoiTao = item.TimeNguoiTao?.ToString("dd/MM/yyyy") ?? "--",
                    HasB2 = hasB2,
                    B2DoneCount = hasB2 ? item.ShdQuanLyDuyetB2s.Count(x => x.TrangThaiXacNhan == 1) : 0,
                    B2TotalCount = hasB2 ? item.ShdQuanLyDuyetB2s.Count : 0,
                    HasGD = hasGD,
                    GDDoneCount = hasGD ? item.ShdNguoiXacNhans.Count(x => x.TrangThaiXacNhan == 1) : 0,
                    GDTotalCount = hasGD ? item.ShdNguoiXacNhans.Count : 0,
                    SupportName = latestSupport?.IdShdNguoiHoTroNavigation?.Ten,
                    PWidth = pWidth,
                    PColor = pColor,
                    PText = pText,
                    Bg = bg,
                    Fg = fg,
                    StatusTag = statusTag
                };
            });

            return Json(result);
        }

        // --- HÀM POST XÁC NHẬN HOÀN THÀNH ---
        /// <summary>
        /// POST: /FormSHD/XacNhanHoanThanh
        /// Nút bấm dành cho Đội SHD - Xác nhận đã xử lý xong các thủ tục/hồ sơ Xuất Nhập Khẩu
        /// </summary>
        [HttpPost("/FormSHD/XacNhanHoanThanh")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> XacNhanHoanThanh([FromBody] SHDCompleteRequest request)
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

            // Kiểm tra quyền (Chỉ All hoặc Admin SHD)
            if (!User.IsInRole("All") && !User.IsInRole("AdminSHD"))
            {
                return Json(new { success = false, message = "Bạn không có quyền hoàn tất đơn SHD này." });
            }

            var form = await _context.FormShds
                .Include(f => f.ShdCtNguoiHoTros).ThenInclude(ct => ct.IdShdNguoiHoTroNavigation)
                .Include(f => f.ShdNguoiXacNhans)      // Bước Giám đốc
                .Include(f => f.ShdQuanLyDuyetB2s)    // Bước 2
                .FirstOrDefaultAsync(f => f.Id == request.Id);

            if (form == null)
                return Json(new { success = false, message = "Không tìm thấy đơn SHD." });

            // --- KIỂM TRA ĐIỀU KIỆN PHÊ DUYỆT CÁC CẤP TRƯỚC KHI HOÀN TẤT ---
            bool chuaXacNhanGD = form.ShdNguoiXacNhans != null && form.ShdNguoiXacNhans.Any(x => x.TrangThaiXacNhan != 1);

            bool chuaXacNhanB2 = false;
            if (form.ShdQuanLyDuyetB2s != null && form.ShdQuanLyDuyetB2s.Any())
            {
                string hinhThucDuyet = form.ShdQuanLyDuyetB2s.FirstOrDefault()?.Loai?.ToUpper() ?? "AND";
                if (hinhThucDuyet == "OR" || hinhThucDuyet == "ANY")
                    chuaXacNhanB2 = !form.ShdQuanLyDuyetB2s.Any(x => x.TrangThaiXacNhan == 1);
                else
                    chuaXacNhanB2 = form.ShdQuanLyDuyetB2s.Any(x => x.TrangThaiXacNhan != 1);
            }

            if (chuaXacNhanGD || chuaXacNhanB2)
            {
                return Json(new { success = false, message = "Đơn SHD này chưa được các cấp quản lý xác nhận đầy đủ. Không thể hoàn tất!" });
            }

            // Kiểm tra bảo mật công ty
            if (form.TenCongTy?.Trim() != tenCongTy && !User.IsInRole("All"))
            {
                return Json(new { success = false, message = "Bạn không thể thao tác trên đơn của công ty khác." });
            }

            // Kiểm tra trạng thái hiện tại
            if (form.TrangThai == "HoanTat" || (form.TenForm != null && form.TenForm.Contains("[ĐÃ HỦY]")))
            {
                return Json(new { success = false, message = "Đơn đã ở trạng thái Hoàn tất hoặc đã bị Hủy." });
            }

            using (var transaction = await _context.Database.BeginTransactionAsync())
            {
                try
                {
                    DateTime now = DateTime.Now;

                    form.IdAdmin = userId;
                    form.TenAdmin = userName;
                    form.TimeAdmin = now;
                    form.TrangThai = "HoanTat";

                    // Lưu lịch sử thao tác SHD
                    var lichSu = new LichSuFormShd
                    {
                        IdFormShd = form.Id,
                        TieuDe = "SHD Xác nhận Hoàn tất",
                        Mota = $"Admin SHD thực hiện: {userName} ({userEmail}). " +
                               $"Bộ phận: {phongBan}. Nội dung: Đã xử lý và hoàn tất hồ sơ SHD.",
                        Time = now,
                        TrangThaiAnHien = true
                    };

                    _context.LichSuFormShds.Add(lichSu);
                    _context.FormShds.Update(form);

                    await _context.SaveChangesAsync();
                    await transaction.CommitAsync();

                    return Json(new { success = true, message = "Xác nhận hoàn tất đơn SHD thành công!" });
                }
                catch (Exception ex)
                {
                    await transaction.RollbackAsync();
                    return Json(new { success = false, message = "Lỗi hệ thống SHD: " + ex.Message });
                }
            }
        }

        public class SHDCompleteRequest
        {
            public int Id { get; set; }
            public string SHDNote { get; set; }
        }

        #endregion

        #region QUẢN TRỊ & XUẤT BÁO CÁO SHD (CHỈ ĐƠN ĐÃ HOÀN TẤT)

        // 1. TRẢ VỀ VIEW RỖNG (Cho JavaScript tự Render)
        [HttpGet("/FormSHD/XuatBaoCao")]
        public IActionResult XuatBaoCao()
        {
            var userIdStr = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value
                            ?? User.FindFirst("UserId")?.Value;

            if (string.IsNullOrEmpty(userIdStr))
                return Redirect("/DonXetDuyet/DangNhap");

            return View();
        }

        // 2. API TRẢ VỀ DỮ LIỆU JSON (100% JS)
        [HttpGet("/FormSHD/GetXuatBaoCaoData")]
        public async Task<IActionResult> GetXuatBaoCaoData()
        {
            var userIdStr = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value
                            ?? User.FindFirst("UserId")?.Value;

            if (string.IsNullOrEmpty(userIdStr)) return Unauthorized();

            int userId = int.Parse(userIdStr);
            var userRoles = User.FindAll(System.Security.Claims.ClaimTypes.Role).Select(c => c.Value).ToList();
            var tenCongTy = User.FindFirst("TenCongTy")?.Value?.Trim() ?? "";

            // KHỞI TẠO QUERY
            IQueryable<E_Form_Best.Models.ITForm.FormShd> query = _context.FormShds
                .Include(f => f.ShdNguoiXacNhans)
                .Include(f => f.ShdQuanLyDuyetB2s)
                .Include(f => f.ShdCtNguoiHoTros).ThenInclude(ct => ct.IdShdNguoiHoTroNavigation);

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

                if (userRoles.Contains("AdminSHD"))
                {
                    // Nếu là AdminSHD: Nhìn thấy toàn bộ đơn hoàn tất của công ty mình (Không lọc theo bộ phận)
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
                    HasB2 = item.ShdQuanLyDuyetB2s?.Any() == true,
                    HasGD = item.ShdNguoiXacNhans?.Any() == true,
                    SupportNames = item.ShdCtNguoiHoTros?.Select(s => s.IdShdNguoiHoTroNavigation?.Ten ?? "N/A").ToList() ?? new List<string>(),
                    TenAdmin = item.TenAdmin ?? "N/A",
                    TimeAdmin = item.TimeAdmin?.ToString("dd/MM/yyyy HH:mm") ?? "--",
                    TrangThai = "Hoàn tất"
                });

                return Json(result);
            }
            catch (Exception ex)
            {
                return BadRequest(new { message = "Lỗi hệ thống SHD: " + ex.Message });
            }
        }

        /// <summary>
        /// Xuất dữ liệu ra Excel CHI TIẾT các đơn ĐÃ HOÀN TẤT
        /// </summary>
        [HttpGet("/FormSHD/ExportExcelSHD")]
        public async Task<IActionResult> ExportExcelSHD(DateTime? tuNgay, DateTime? denNgay, string loaiDon)
        {
            // --- 1. LẤY THÔNG TIN CLAIMS ---
            var userIdStr = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value
                            ?? User.FindFirst("UserId")?.Value;
            if (string.IsNullOrEmpty(userIdStr)) return Unauthorized();

            int userId = int.Parse(userIdStr);
            var userRoles = User.FindAll(System.Security.Claims.ClaimTypes.Role).Select(c => c.Value).ToList();
            var tenCongTy = User.FindFirst("TenCongTy")?.Value?.Trim() ?? "";

            // --- 2. TRUY VẤN DỮ LIỆU & INCLUDE ĐẦY ĐỦ CHI TIẾT ---
            IQueryable<E_Form_Best.Models.ITForm.FormShd> query = _context.FormShds
                .Include(f => f.ShdNguoiXacNhans).Include(f => f.ShdQuanLyDuyetB2s)
                .Include(f => f.ShdCtNguoiHoTros).ThenInclude(ct => ct.IdShdNguoiHoTroNavigation)
                .Include(f => f.ShdDangKySuDungXeCongTac1s)
                .Include(f => f.ShdDangKySuDungXeDaily2s);

            // --- 3. ĐIỀU KIỆN HIỂN THỊ: CHỈ XUẤT ĐƠN ĐÃ ĐƯỢC ADMIN HOÀN TẤT ---
            query = query.Where(f => f.IdAdmin != null && f.TenAdmin != null && f.TimeAdmin != null);

            // --- 4. BỘ LỌC NGÀY HOÀN TẤT & LOẠI ĐƠN ---
            if (tuNgay.HasValue) query = query.Where(f => f.TimeAdmin >= tuNgay.Value);
            if (denNgay.HasValue) query = query.Where(f => f.TimeAdmin <= denNgay.Value.AddDays(1).AddSeconds(-1));
            if (!string.IsNullOrEmpty(loaiDon)) query = query.Where(f => f.IdForm == loaiDon);

            // --- 5. PHÂN QUYỀN LỌC DỮ LIỆU ---
            if (!userRoles.Contains("All"))
            {
                if (!string.IsNullOrEmpty(tenCongTy))
                    query = query.Where(f => f.TenCongTy == tenCongTy);

                if (!userRoles.Contains("AdminSHD"))
                {
                    query = query.Where(f => f.IdNguoiTao == userId);
                }
            }

            var data = await query.OrderByDescending(f => f.TimeAdmin).AsNoTracking().ToListAsync();

            // --- 6. TẠO FILE EXCEL ---
            using (var workbook = new ClosedXML.Excel.XLWorkbook())
            {
                var worksheet = workbook.Worksheets.Add("BaoCao_HoanTat_SHD");
                string[] headers = { "STT", "ID", "Loại Đơn", "Mã NV", "Họ Tên", "Bộ Phận", "Ngày Hoàn Tất", "Người Duyệt SHD", "Người Hỗ Trợ", "CHI TIẾT NỘI DUNG" };

                // Định dạng Header Cyan Luxury 2026
                for (int i = 0; i < headers.Length; i++)
                {
                    var cell = worksheet.Cell(1, i + 1);
                    cell.Value = headers[i];
                    cell.Style.Font.Bold = true;
                    cell.Style.Fill.BackgroundColor = ClosedXML.Excel.XLColor.FromHtml("#0891b2");
                    cell.Style.Font.FontColor = ClosedXML.Excel.XLColor.White;
                    cell.Style.Alignment.Horizontal = ClosedXML.Excel.XLAlignmentHorizontalValues.Center;
                }

                int currentRow = 2;
                foreach (var item in data)
                {
                    worksheet.Cell(currentRow, 1).Value = currentRow - 1;
                    worksheet.Cell(currentRow, 2).Value = item.Id;
                    worksheet.Cell(currentRow, 3).Value = GetShortNameSHD(item.IdForm);
                    worksheet.Cell(currentRow, 4).Value = item.SoNhanVien;
                    worksheet.Cell(currentRow, 5).Value = item.TenNguoiNv;
                    worksheet.Cell(currentRow, 6).Value = item.BoPhan;
                    worksheet.Cell(currentRow, 7).Value = item.TimeAdmin?.ToString("dd/MM/yyyy HH:mm");
                    worksheet.Cell(currentRow, 8).Value = item.TenAdmin;
                    var support = item.ShdCtNguoiHoTros.OrderByDescending(s => s.Stt).FirstOrDefault();
                    worksheet.Cell(currentRow, 9).Value = support?.IdShdNguoiHoTroNavigation?.Ten ?? "";
                    worksheet.Cell(currentRow, 10).Value = GetChiTietDonSHD(item);
                    worksheet.Cell(currentRow, 10).Style.Alignment.WrapText = true;
                    currentRow++;
                }

                worksheet.Columns().AdjustToContents();
                worksheet.Column(10).Width = 80;
                worksheet.RangeUsed().Style.Border.OutsideBorder = ClosedXML.Excel.XLBorderStyleValues.Thin;
                worksheet.RangeUsed().Style.Border.InsideBorder = ClosedXML.Excel.XLBorderStyleValues.Thin;

                using (var stream = new MemoryStream())
                {
                    workbook.SaveAs(stream);
                    string fileName = $"BaoCao_SHD_HoanTat_{DateTime.Now:yyyyMMdd_HHmm}.xlsx";
                    return File(stream.ToArray(), "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", fileName);
                }
            }
        }

        // --- HÀM HELPER HỆ THỐNG SHD ---
        private static string GetShortNameSHD(string? id) => id switch
        {
            "SHD_DangKySuDungXeCongTac_1" => "Xe công tác",
            "SHD_XeDaily_2" => "Xe Daily",
            _ => string.IsNullOrEmpty(id) ? "N/A" : id.Replace("SHD_", "")
        };

        private string GetChiTietDonSHD(E_Form_Best.Models.ITForm.FormShd item)
        {
            try
            {
                switch (item.IdForm)
                {
                    case "SHD_DangKySuDungXeCongTac_1":
                        var f1 = item.ShdDangKySuDungXeCongTac1s.FirstOrDefault();
                        return f1 != null ? $"- Lý do: {f1.LiDo}\n- SĐT: {f1.SoDienThoai}\n- Số người: {f1.SoLuong}\n- Dự kiến đi: {f1.TimeDuTinh:dd/MM HH:mm}\n- Thời gian về: {f1.ThoiGianVe:dd/MM HH:mm}" : "";
                    case "SHD_XeDaily_2":
                        var f2 = item.ShdDangKySuDungXeDaily2s.FirstOrDefault();
                        return f2 != null ? $"- Thông tin lộ trình xe Daily..." : "";
                    default: return "N/A";
                }
            }
            catch { return "Lỗi xử lý dữ liệu chi tiết"; }
        }

        #endregion

        #region BÁO CÁO THỐNG KÊ FORM SHD (ĐỒNG BỘ 7 TRẠNG THÁI - TỐI ƯU TỐC ĐỘ)

        [HttpGet("/FormSHD/BaoCaoThongKe")]
        public async Task<IActionResult> BaoCaoThongKe()
        {
            if (!User.Identity.IsAuthenticated) return Redirect("/DonXetDuyet/DangNhap");

            // Phân quyền cho SHD
            bool isAuthorized = User.IsInRole("AdminSHD") || User.IsInRole("All") || User.IsInRole("SHD");
            if (!isAuthorized) return Redirect("/");

            // TỐI ƯU: Chỉ lấy dữ liệu cần thiết cho View, giới hạn số lượng đơn gần đây để trang load tức thì
            var allForms = await _context.FormShds
                .AsNoTracking()
                .OrderByDescending(x => x.TimeNguoiTao)
                .Take(1000)
                .ToListAsync();

            return View(allForms);
        }

        // --- ĐỒNG BỘ HR: Nhận thêm tham số bộ lọc thời gian từ client gửi lên ---
        [HttpGet("/FormSHD/GetDataThongKe")]
        public async Task<IActionResult> GetDataThongKe(DateTime? fromDate, DateTime? toDate)
        {
            // TỐI ƯU: Select trực tiếp để SQL chỉ trả về các cột cần tính toán, kết hợp lọc Date tại SQL
            var query = _context.FormShds
                .Include(f => f.ShdCtNguoiHoTros).ThenInclude(ct => ct.IdShdNguoiHoTroNavigation)
                .AsNoTracking();

            if (fromDate.HasValue)
            {
                query = query.Where(x => x.TimeNguoiTao >= fromDate.Value);
            }
            if (toDate.HasValue)
            {
                var endOfToDate = toDate.Value.Date.AddDays(1).AddTicks(-1);
                query = query.Where(x => x.TimeNguoiTao <= endOfToDate);
            }

            var dataRaw = await query
                .Select(x => new
                {
                    x.Id,
                    x.IdForm,
                    x.BoPhan,
                    x.IdNguoiDuyet,
                    x.IdAdmin,
                    x.TenForm,
                    x.Danhmuc,
                    // Chỉ lấy mảng trạng thái để tính toán status, không lấy cả object liên kết
                    B2States = x.ShdQuanLyDuyetB2s.Select(b => b.TrangThaiXacNhan).ToList(),
                    GDStates = x.ShdNguoiXacNhans.Select(g => g.TrangThaiXacNhan).ToList(),
                    // Lấy thông tin người hỗ trợ mới nhất phục vụ bộ lọc chéo trên RAM
                    TenNguoiHoTro = x.ShdCtNguoiHoTros.OrderByDescending(ct => ct.Stt).Select(ct => ct.IdShdNguoiHoTroNavigation.Ten).FirstOrDefault()
                })
                .ToListAsync();

            // Xử lý logic tính trạng thái tại RAM
            var processedData = dataRaw.Select(x => new
            {
                x.Id,
                TenLoaiDon = GetShortNameSHD(x.IdForm),
                x.BoPhan,
                x.Danhmuc,
                TenNguoiHoTro = x.TenNguoiHoTro ?? "Chưa xác định",
                TrangThaiDon = CalculateStatusSHD(x.TenForm, x.IdNguoiDuyet, x.IdAdmin, x.B2States, x.GDStates)
            });

            return Json(processedData);
        }

        // --- ĐỒNG BỘ HR: Nhận thêm tham số bộ lọc thời gian tính hiệu suất nhân sự ---
        [HttpGet("/FormSHD/GetDataNguoiHoTro")]
        public async Task<IActionResult> GetDataNguoiHoTro(DateTime? fromDate, DateTime? toDate)
        {
            var query = _context.FormShds
                .AsNoTracking()
                .Where(f => f.ShdCtNguoiHoTros.Any());

            if (fromDate.HasValue)
            {
                query = query.Where(x => x.TimeNguoiTao >= fromDate.Value);
            }
            if (toDate.HasValue)
            {
                var endOfToDate = toDate.Value.Date.AddDays(1).AddTicks(-1);
                query = query.Where(x => x.TimeNguoiTao <= endOfToDate);
            }

            var queryData = await query
                .Select(f => new
                {
                    TenNguoiHoTro = f.ShdCtNguoiHoTros
                                    .OrderByDescending(s => s.Stt)
                                    .Select(s => s.IdShdNguoiHoTroNavigation.Ten)
                                    .FirstOrDefault(),
                    f.BoPhan,
                    f.Danhmuc,
                    f.TenForm,
                    f.IdNguoiDuyet,
                    f.IdAdmin,
                    f.TimeAdmin,
                    f.TimeNguoiDuyet,
                    B2States = f.ShdQuanLyDuyetB2s.Select(b => b.TrangThaiXacNhan).ToList(),
                    GDStates = f.ShdNguoiXacNhans.Select(g => g.TrangThaiXacNhan).ToList()
                })
                .ToListAsync();

            var filteredData = queryData.Select(f =>
            {
                double? minutes = (f.TimeAdmin.HasValue && f.TimeNguoiDuyet.HasValue)
                                  ? (f.TimeAdmin.Value - f.TimeNguoiDuyet.Value).TotalMinutes : null;

                return new
                {
                    TenNguoiHoTro = f.TenNguoiHoTro ?? "Chưa xác định",
                    f.BoPhan,
                    DanhMuc = f.Danhmuc ?? "N/A",
                    TrangThai = CalculateStatusSHD(f.TenForm, f.IdNguoiDuyet, f.IdAdmin, f.B2States, f.GDStates),
                    PhutXuLy = minutes
                };
            }).ToList();

            return Json(filteredData);
        }

        // --- CÁC HÀM HELPER PHÂN HỆ SHD ---
        private static string CalculateStatusSHD(string? tenForm, int? idQL, int? idAdmin, IEnumerable<int?> b2, IEnumerable<int?> gd)
        {
            if ((tenForm ?? "").Contains("[ĐÃ HỦY]") || b2.Any(v => v == 2) || gd.Any(v => v == 2)) return "ĐÃ HỦY/TỪ CHỐI";
            if (idAdmin != null) return "HOÀN TẤT";
            if (idQL == null) return "CHỜ QL DUYỆT";
            if (b2.Any() && !b2.All(v => v == 1)) return "CHỜ BP XÁC NHẬN";
            if (gd.Any() && !gd.All(v => v == 1)) return "CHỜ GIÁM ĐỐC";
            return "SHD ĐANG XỬ LÝ";
        }



        #endregion

        #region LỊCH SỬ VÀ THÔNG BÁO FORM SHD (TỐI ƯU HÓA TỐC ĐỘ)

        [HttpGet("/FormSHD/LichSuSHD")]
        public IActionResult LogLichSuSHD()
        {
            var userIdStr = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userIdStr)) return RedirectToAction("DangNhap", "DonXetDuyet");
            return View();
        }

        // --- API: TRẢ VỀ JSON LỊCH SỬ SHD ---
        [HttpGet("/FormSHD/GetLichSuData")]
        public async Task<IActionResult> GetLichSuData()
        {
            var userIdStr = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userIdStr)) return Unauthorized();

            int userId = int.Parse(userIdStr);
            var userEmail = (User.FindFirst(System.Security.Claims.ClaimTypes.Email)?.Value ?? "").Trim().ToLower();
            bool isAll = User.IsInRole("All");

            // --- CẤU HÌNH CÁC ROLE ---
            bool isAdminSHD = User.IsInRole("AdminSHD") || User.IsInRole("SHD");
            bool isGiamDocSHD = User.IsInRole("GiamDocSHD");
            bool isBaoVeSHD = User.IsInRole("BaoVeSHD");
            bool isQuanLyB2 = User.IsInRole("QuanLyDuyetDonSHD_B2");
            bool isQuanLyDuyet = User.IsInRole("QuanLyDuyetDonSHD");

            // Tối ưu: Chỉ Select những cột cần thiết ngay từ DB
            var query = _context.LichSuFormShds.AsNoTracking()
                .Include(l => l.IdFormShdNavigation)
                    .ThenInclude(f => f.ShdQuanLyDuyetB2s)
                        .ThenInclude(b2 => b2.ShdQuanLyDuyetB2UyQuyens) // Nạp cả dữ liệu Ủy quyền
                .Include(l => l.IdFormShdNavigation)
                    .ThenInclude(f => f.ShdNguoiXacNhans)
                .Include(l => l.IdFormShdNavigation)
                    .ThenInclude(f => f.ShdCtNguoiHoTros)
                        .ThenInclude(ct => ct.IdShdNguoiHoTroNavigation)
                .Where(l => isAll || l.TrangThaiAnHien == true)
                .Where(l =>
                    isAll ||
                    l.IdFormShdNavigation.IdNguoiTao == userId ||
                    (isAdminSHD && l.IdFormShdNavigation.IdNguoiDuyet != null &&
                        l.IdFormShdNavigation.ShdCtNguoiHoTros.Any(ct => ct.IdShdNguoiHoTroNavigation.MaNv != null && ct.IdShdNguoiHoTroNavigation.MaNv.ToLower() == userEmail)) ||
                    (l.IdFormShdNavigation.IdNguoiDuyet != null && (
                        (isGiamDocSHD && l.IdFormShdNavigation.ShdNguoiXacNhans.Any(xn => xn.IdnguoiXacNhan == userId || (xn.MaNguoiXacNhan != null && xn.MaNguoiXacNhan.ToLower() == userEmail))) ||
                        // CẬP NHẬT: Thêm điều kiện kiểm tra MaNvuyQuyen trong danh sách Ủy quyền
                        (isQuanLyB2 && l.IdFormShdNavigation.ShdQuanLyDuyetB2s.Any(b2 =>
                            b2.IdnguoiXacNhan == userId ||
                            (b2.MaNguoiXacNhan != null && b2.MaNguoiXacNhan.ToLower() == userEmail) ||
                            b2.ShdQuanLyDuyetB2UyQuyens.Any(uq => uq.MaNvuyQuyen != null && uq.MaNvuyQuyen.ToLower() == userEmail)
                        )) ||
                        isBaoVeSHD
                    )) ||
                    (isQuanLyDuyet && l.IdFormShdNavigation.IdNguoiDuyet == userId)
                )
                .OrderByDescending(l => l.Time)
                .Take(200)
                .Select(l => new
                {
                    log = l,
                    f = l.IdFormShdNavigation,
                    XacNhans = l.IdFormShdNavigation.ShdNguoiXacNhans.Select(x => x.TrangThaiXacNhan),
                    B2s = l.IdFormShdNavigation.ShdQuanLyDuyetB2s.Select(x => x.TrangThaiXacNhan),
                    NguoiHoTro = l.IdFormShdNavigation.ShdCtNguoiHoTros
                                    .OrderByDescending(x => x.Stt)
                                    .Select(x => x.IdShdNguoiHoTroNavigation.Ten)
                                    .FirstOrDefault()
                });

            var rawData = await query.ToListAsync();

            var result = rawData.Select(item =>
            {
                var log = item.log;
                var f = item.f;

                bool isCancelled = (f.TenForm ?? "").Contains("[ĐÃ HỦY]") || f.TrangThai == "DaHuy" || f.TrangThai == "Huy";
                bool isFinished = f.IdAdmin != null || f.TrangThai == "HoanTat" || f.TrangThai == "DaXuLy";
                bool isManagerApproved = f.IdNguoiDuyet != null;

                bool hasB2 = item.B2s.Any();
                bool isB2Approved = hasB2 && item.B2s.All(x => x == 1);
                bool isB2Rejected = hasB2 && item.B2s.Any(x => x == 2);

                bool hasGD = item.XacNhans.Any();
                bool isGDApproved = hasGD && item.XacNhans.All(x => x == 1);
                bool isGDRejected = hasGD && item.XacNhans.Any(x => x == 2);

                string statusText, statusColor;
                if (isCancelled || isB2Rejected || isGDRejected) { statusText = "ĐÃ HỦY"; statusColor = "#ef4444"; }
                else if (isFinished) { statusText = "HOÀN TẤT"; statusColor = "#10b981"; }
                else if (!isManagerApproved) { statusText = "CHỜ QL"; statusColor = "#f59e0b"; }
                else if (hasB2 && !isB2Approved) { statusText = "BP XÁC NHẬN"; statusColor = "#0ea5e9"; }
                else if (hasGD && !isGDApproved) { statusText = "GIÁM ĐỐC"; statusColor = "#d946ef"; }
                else { statusText = "CHỜ SHD"; statusColor = "#0891b2"; }

                string actionTitle = log.TieuDe?.ToUpper() ?? "HÀNH ĐỘNG";
                string actionColor = actionTitle.Contains("DUYỆT") || actionTitle.Contains("XÁC NHẬN") ? "#10b981" :
                                    actionTitle.Contains("HỦY") || actionTitle.Contains("TỪ CHỐI") ? "#ef4444" :
                                    actionTitle.Contains("HOÀN TẤT") ? "#0891b2" : "#6366f1";

                return new
                {
                    log.Id,
                    log.IdFormShd,
                    TimeHHmm = log.Time?.ToString("HH:mm") ?? "--:--",
                    TimeDate = log.Time?.ToString("dd/MM/yyyy") ?? "--/--/----",
                    TieuDe = log.TieuDe ?? "Hành động",
                    Mota = log.Mota ?? "",
                    TenForm = (f.TenForm ?? "").Replace("[ĐÃ HỦY]", ""),
                    TenAdmin = f.TenAdmin ?? "Đang xử lý",
                    NguoiHoTro = item.NguoiHoTro,
                    StatusText = statusText,
                    StatusColor = statusColor,
                    ActionColor = actionColor,
                    TrangThaiAnHien = log.TrangThaiAnHien
                };
            }).ToList();

            return Json(result);
        }

        // --- API: THÔNG BÁO SHD ---
        [HttpGet("/FormSHD/GetNotificationsSHD")]
        public async Task<IActionResult> GetNotificationsSHD(int skip = 0, int take = 20)
        {
            var userIdStr = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userIdStr)) return Unauthorized();

            int userId = int.Parse(userIdStr);
            var userEmail = (User.FindFirst(System.Security.Claims.ClaimTypes.Email)?.Value ?? "").Trim().ToLower();
            bool isAll = User.IsInRole("All");

            bool isAdminSHD = User.IsInRole("AdminSHD") || User.IsInRole("SHD");
            bool isGiamDocSHD = User.IsInRole("GiamDocSHD");
            bool isBaoVeSHD = User.IsInRole("BaoVeSHD");
            bool isQuanLyB2 = User.IsInRole("QuanLyDuyetDonSHD_B2");
            bool isQuanLyDuyet = User.IsInRole("QuanLyDuyetDonSHD");

            var query = _context.LichSuFormShds.AsNoTracking()
                .Include(l => l.IdFormShdNavigation)
                    .ThenInclude(f => f.ShdQuanLyDuyetB2s)
                        .ThenInclude(b2 => b2.ShdQuanLyDuyetB2UyQuyens)
                .Where(l => isAll || l.TrangThaiAnHien == true)
                .Where(l =>
                    isAll ||
                    l.IdFormShdNavigation.IdNguoiTao == userId ||
                    (isAdminSHD && l.IdFormShdNavigation.IdNguoiDuyet != null &&
                        l.IdFormShdNavigation.ShdCtNguoiHoTros.Any(ct => ct.IdShdNguoiHoTroNavigation.MaNv != null && ct.IdShdNguoiHoTroNavigation.MaNv.ToLower() == userEmail)) ||
                    (l.IdFormShdNavigation.IdNguoiDuyet != null && (
                        (isGiamDocSHD && l.IdFormShdNavigation.ShdNguoiXacNhans.Any(xn => xn.IdnguoiXacNhan == userId || (xn.MaNguoiXacNhan != null && xn.MaNguoiXacNhan.ToLower() == userEmail))) ||
                        (isQuanLyB2 && l.IdFormShdNavigation.ShdQuanLyDuyetB2s.Any(b2 =>
                            b2.IdnguoiXacNhan == userId ||
                            (b2.MaNguoiXacNhan != null && b2.MaNguoiXacNhan.ToLower() == userEmail) ||
                            b2.ShdQuanLyDuyetB2UyQuyens.Any(uq => uq.MaNvuyQuyen != null && uq.MaNvuyQuyen.ToLower() == userEmail)
                        )) ||
                        isBaoVeSHD
                    )) ||
                    (isQuanLyDuyet && l.IdFormShdNavigation.IdNguoiDuyet == userId)
                );

            var unreadCount = await query.CountAsync(l => l.IsRead != true);

            var logs = await query.OrderByDescending(l => l.Time)
                                  .Skip(skip)
                                  .Take(take)
                                  .Select(l => new
                                  {
                                      l.Id,
                                      l.IdFormShd,
                                      l.TieuDe,
                                      l.Mota,
                                      Time = l.Time.HasValue ? l.Time.Value.ToString("dd/MM HH:mm") : "",
                                      IsRead = l.IsRead ?? false
                                  })
                                  .ToListAsync();

            return Ok(new { dataList = logs, unreadCount });
        }

        #endregion
    }
}
