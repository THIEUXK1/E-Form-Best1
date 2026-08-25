using E_Form_Best.Context;
using E_Form_Best.Models.ITForm;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace E_Form_Best.Areas.ITForm.Controllers
{
    /// <summary>
    /// Quản lý Công cụ dụng cụ (CCDC) của bộ phận IT.
    /// Tách riêng khỏi ITFormController vì module mới, không nhồi thêm vào file 8k dòng.
    /// </summary>
    [Area("ITform")]
    public class QLCCDCController : Controller
    {
        private readonly ITFormContext _context;

        public QLCCDCController(ITFormContext context)
        {
            _context = context;
        }

        // Quyền xem/sửa CCDC đi cùng nhóm "Quản trị IT" trên menu.
        // Ẩn nút ở UI không phải là phân quyền nên mọi action đều phải gọi hàm này.
        private bool CoQuyen() => User.IsInRole("AdminIT") || User.IsInRole("All");

        #region View

        [HttpGet("/QLCCDC")]
        public async Task<IActionResult> Index()
        {
            if (User?.Identity?.IsAuthenticated != true)
                return Redirect("/DonXetDuyet/DangNhap");

            if (!CoQuyen())
                return Forbid();

            // View chỉ hiển thị: dữ liệu dropdown được chuẩn bị sẵn ở đây.
            // Phải chiếu vào entity public, KHÔNG dùng anonymous type: ở Development có bật
            // AddRazorRuntimeCompilation nên view được biên dịch sang assembly khác, mà anonymous
            // type là internal => dynamic binder không thấy property, nổ RuntimeBinderException.
            ViewBag.DsCongTy = await _context.KkCongTies
                .OrderBy(x => x.TenCongTy)
                .Select(x => new KkCongTy { IdcongTy = x.IdcongTy, TenCongTy = x.TenCongTy })
                .ToListAsync();

            ViewBag.DsBoPhan = await _context.KkBoPhans
                .OrderBy(x => x.TenBoPhan)
                .Select(x => new KkBoPhan { IdboPhan = x.IdboPhan, TenBoPhan = x.TenBoPhan, IdcongTy = x.IdcongTy })
                .ToListAsync();

            return View();
        }

        #endregion

        #region API danh sách

        [HttpGet("/QLCCDC/GetDanhSach")]
        public async Task<IActionResult> GetDanhSach(string? tuKhoa, int? idCongTy, int? idBoPhan, string? tinhTrang)
        {
            if (!CoQuyen()) return Json(new { success = false, message = "Bạn không có quyền xem dữ liệu này." });

            try
            {
                // Bắt buộc lọc soft-delete, giống quy ước của KK_ThietBi
                var query = _context.KkCongCuDungCus.Where(x => x.NgayXoa == null);

                if (!string.IsNullOrWhiteSpace(tuKhoa))
                {
                    var kw = tuKhoa.Trim();
                    query = query.Where(x =>
                        (x.TenCcdc != null && x.TenCcdc.Contains(kw)) ||
                        (x.MaCcdc != null && x.MaCcdc.Contains(kw)) ||
                        (x.LoaiCcdc != null && x.LoaiCcdc.Contains(kw)) ||
                        (x.NguoiQuanLy != null && x.NguoiQuanLy.Contains(kw)) ||
                        (x.ViTri != null && x.ViTri.Contains(kw)));
                }

                if (idCongTy.HasValue && idCongTy > 0) query = query.Where(x => x.IdcongTy == idCongTy);
                if (idBoPhan.HasValue && idBoPhan > 0) query = query.Where(x => x.IdboPhan == idBoPhan);
                if (!string.IsNullOrWhiteSpace(tinhTrang)) query = query.Where(x => x.TinhTrang == tinhTrang);

                var data = await query
                    .OrderByDescending(x => x.IdCcdc)
                    .Select(x => new
                    {
                        x.IdCcdc,
                        x.MaCcdc,
                        x.TenCcdc,
                        x.LoaiCcdc,
                        x.DonViTinh,
                        x.SoLuong,
                        x.IdcongTy,
                        TenCongTy = x.IdcongTyNavigation != null ? x.IdcongTyNavigation.TenCongTy : "",
                        x.IdboPhan,
                        TenBoPhan = x.IdboPhanNavigation != null ? x.IdboPhanNavigation.TenBoPhan : "",
                        x.NguoiQuanLy,
                        x.ViTri,
                        x.TinhTrang,
                        x.NgayMua,
                        x.GiaTri,
                        x.HanBaoHanh,
                        x.GhiChu,
                        x.NgayTao,
                        x.NgayCapNhat,
                        // Số đang nằm ngoài = tổng chưa trả của các phiếu mượn còn mở
                        DangMuon = _context.KkCcdcMuonTras
                            .Where(m => m.IdCcdc == x.IdCcdc && m.NgayTra == null)
                            .Sum(m => (int?)(m.SoLuongMuon - m.SoLuongDaTra)) ?? 0
                    })
                    .ToListAsync();

                var ketQua = data.Select(x => new
                {
                    x.IdCcdc, x.MaCcdc, x.TenCcdc, x.LoaiCcdc, x.DonViTinh, x.SoLuong,
                    x.IdcongTy, x.TenCongTy, x.IdboPhan, x.TenBoPhan,
                    x.NguoiQuanLy, x.ViTri, x.TinhTrang, x.NgayMua, x.GiaTri, x.HanBaoHanh,
                    x.GhiChu, x.NgayTao, x.NgayCapNhat, x.DangMuon,
                    ConLai = x.SoLuong - x.DangMuon
                }).ToList();

                var tongSoLuong = data.Sum(x => x.SoLuong);
                var tongGiaTri = data.Sum(x => (x.GiaTri ?? 0) * x.SoLuong);
                var tongDangMuon = data.Sum(x => x.DangMuon);

                return Json(new { success = true, data = ketQua, tongDong = data.Count, tongSoLuong, tongGiaTri, tongDangMuon });
            }
            catch (Exception ex) { return Json(new { success = false, message = ex.Message }); }
        }

        #endregion

        #region Thêm / Sửa / Xoá

        [HttpPost("/QLCCDC/Save")]
        public async Task<IActionResult> Save(KkCongCuDungCu model)
        {
            if (!CoQuyen()) return Json(new { success = false, message = "Bạn không có quyền thao tác." });

            if (string.IsNullOrWhiteSpace(model.TenCcdc))
                return Json(new { success = false, message = "Vui lòng nhập tên công cụ dụng cụ." });

            if (model.SoLuong < 0)
                return Json(new { success = false, message = "Số lượng không được âm." });

            try
            {
                string hanhDong = model.IdCcdc == 0 ? "Thêm mới" : "Cập nhật";

                if (model.IdCcdc == 0)
                {
                    model.NgayTao = DateTime.Now;
                    model.NguoiTao = User.Identity?.Name;
                    _context.KkCongCuDungCus.Add(model);
                }
                else
                {
                    // Chỉ cho sửa bản ghi chưa bị xoá mềm (chặn IDOR qua id trên URL)
                    var db = await _context.KkCongCuDungCus
                        .FirstOrDefaultAsync(x => x.IdCcdc == model.IdCcdc && x.NgayXoa == null);
                    if (db == null)
                        return Json(new { success = false, message = "Không tìm thấy CCDC cần cập nhật." });

                    db.MaCcdc = model.MaCcdc;
                    db.TenCcdc = model.TenCcdc;
                    db.LoaiCcdc = model.LoaiCcdc;
                    db.DonViTinh = model.DonViTinh;
                    db.SoLuong = model.SoLuong;
                    db.IdcongTy = model.IdcongTy;
                    db.IdboPhan = model.IdboPhan;
                    db.NguoiQuanLy = model.NguoiQuanLy;
                    db.ViTri = model.ViTri;
                    db.TinhTrang = model.TinhTrang;
                    db.NgayMua = model.NgayMua;
                    db.GiaTri = model.GiaTri;
                    db.HanBaoHanh = model.HanBaoHanh;
                    db.GhiChu = model.GhiChu;
                    db.NgayCapNhat = DateTime.Now;
                }

                await _context.SaveChangesAsync();
                GhiLichSu(hanhDong, model.IdCcdc, $"CCDC: {model.TenCcdc} (SL: {model.SoLuong})");

                return Json(new { success = true, message = $"{hanhDong} công cụ dụng cụ thành công!" });
            }
            catch (Exception ex) { return Json(new { success = false, message = ex.Message }); }
        }

        [HttpPost("/QLCCDC/Delete")]
        public async Task<IActionResult> Delete(int id, string? lyDo)
        {
            if (!CoQuyen()) return Json(new { success = false, message = "Bạn không có quyền thao tác." });

            try
            {
                var item = await _context.KkCongCuDungCus.FirstOrDefaultAsync(x => x.IdCcdc == id && x.NgayXoa == null);
                if (item == null)
                    return Json(new { success = false, message = "Không tìm thấy CCDC cần xoá." });

                // Còn người đang mượn thì không cho xoá, tránh mất dấu món đồ đang nằm ngoài
                var dangMuon = await _context.KkCcdcMuonTras
                    .Where(m => m.IdCcdc == id && m.NgayTra == null)
                    .SumAsync(m => (int?)(m.SoLuongMuon - m.SoLuongDaTra)) ?? 0;
                if (dangMuon > 0)
                    return Json(new { success = false, message = $"Không thể xoá: còn {dangMuon} đơn vị đang cho mượn chưa trả." });

                // Xoá mềm để còn truy vết, không xoá cứng dữ liệu tài sản
                item.NgayXoa = DateTime.Now;
                item.LyDoXoa = string.IsNullOrWhiteSpace(lyDo) ? "Không ghi lý do" : lyDo.Trim();
                await _context.SaveChangesAsync();

                GhiLichSu("Xóa", id, $"Đã xoá CCDC: {item.TenCcdc}. Lý do: {item.LyDoXoa}");
                return Json(new { success = true, message = "Đã xoá công cụ dụng cụ!" });
            }
            catch (Exception ex) { return Json(new { success = false, message = ex.Message }); }
        }

        #endregion

        #region Cho mượn / Nhận trả

        /// <summary>
        /// Tra cứu nhân viên theo mã để tự điền tên + bộ phận khi lập phiếu mượn.
        /// Mã nhân viên trong bảng User là duy nhất nên tra 1-1.
        /// </summary>
        [HttpGet("/QLCCDC/TraCuuNhanVien")]
        public async Task<IActionResult> TraCuuNhanVien(string? maNv)
        {
            if (!CoQuyen()) return Json(new { success = false, message = "Bạn không có quyền tra cứu." });

            if (string.IsNullOrWhiteSpace(maNv))
                return Json(new { success = false, message = "Chưa nhập mã nhân viên." });

            var ma = maNv.Trim();

            var nv = await _context.Users
                .Where(u => u.MaNhanVien == ma)
                .Select(u => new
                {
                    u.IdNguoiDung,
                    u.MaNhanVien,
                    u.HoTen,
                    u.PhongBan,
                    u.TenCongTy,
                    u.TrangThai
                })
                .FirstOrDefaultAsync();

            if (nv == null)
                return Json(new { success = false, message = $"Không tìm thấy nhân viên có mã \"{ma}\"." });

            return Json(new { success = true, data = nv });
        }

        /// <summary>Danh sách phiếu mượn của một CCDC (mặc định chỉ lấy phiếu chưa trả xong).</summary>
        [HttpGet("/QLCCDC/GetPhieuMuon")]
        public async Task<IActionResult> GetPhieuMuon(int idCcdc, bool tatCa = false)
        {
            if (!CoQuyen()) return Json(new { success = false, message = "Bạn không có quyền xem dữ liệu này." });

            try
            {
                var query = _context.KkCcdcMuonTras.Where(m => m.IdCcdc == idCcdc);
                if (!tatCa) query = query.Where(m => m.NgayTra == null);

                var data = await query
                    .OrderByDescending(m => m.IdMuon)
                    .Select(m => new
                    {
                        m.IdMuon,
                        m.MaNhanVien,
                        m.HoTen,
                        m.BoPhan,
                        m.TenCongTy,
                        m.SoLuongMuon,
                        m.SoLuongDaTra,
                        ConNo = m.SoLuongMuon - m.SoLuongDaTra,
                        m.NgayMuon,
                        m.NgayHenTra,
                        m.NgayTra,
                        m.TinhTrangKhiTra,
                        m.NguoiChoMuon,
                        m.NguoiNhanTra,
                        m.GhiChu
                    })
                    .ToListAsync();

                // Tính trễ hạn ở bộ nhớ: DateOnly so với ngày hiện tại không dịch được sang SQL
                var ketQua = data.Select(m => new
                {
                    m.IdMuon, m.MaNhanVien, m.HoTen, m.BoPhan, m.TenCongTy,
                    m.SoLuongMuon, m.SoLuongDaTra, m.ConNo,
                    m.NgayMuon, m.NgayHenTra, m.NgayTra, m.TinhTrangKhiTra,
                    m.NguoiChoMuon, m.NguoiNhanTra, m.GhiChu,
                    SoNgayTre = TinhSoNgayTre(m.NgayHenTra, m.NgayTra)
                });

                return Json(new { success = true, data = ketQua });
            }
            catch (Exception ex) { return Json(new { success = false, message = ex.Message }); }
        }

        /// <summary>
        /// Toàn bộ phiếu còn nợ của MỌI CCDC — để IT nhận trả mà không cần biết trước
        /// nhân viên đã mượn món nào. Trả kèm số ngày trễ hạn.
        /// </summary>
        [HttpGet("/QLCCDC/GetDangMuon")]
        public async Task<IActionResult> GetDangMuon(string? tuKhoa, int? idCongTy, int? idBoPhan, bool chiQuaHan = false)
        {
            if (!CoQuyen()) return Json(new { success = false, message = "Bạn không có quyền xem dữ liệu này." });

            try
            {
                var query = from m in _context.KkCcdcMuonTras
                            join c in _context.KkCongCuDungCus on m.IdCcdc equals c.IdCcdc
                            where m.NgayTra == null && c.NgayXoa == null
                            select new { m, c };

                if (!string.IsNullOrWhiteSpace(tuKhoa))
                {
                    var kw = tuKhoa.Trim();
                    query = query.Where(x =>
                        (x.m.MaNhanVien != null && x.m.MaNhanVien.Contains(kw)) ||
                        (x.m.HoTen != null && x.m.HoTen.Contains(kw)) ||
                        (x.m.BoPhan != null && x.m.BoPhan.Contains(kw)) ||
                        (x.c.TenCcdc != null && x.c.TenCcdc.Contains(kw)) ||
                        (x.c.MaCcdc != null && x.c.MaCcdc.Contains(kw)));
                }

                if (idCongTy.HasValue && idCongTy > 0) query = query.Where(x => x.c.IdcongTy == idCongTy);
                if (idBoPhan.HasValue && idBoPhan > 0) query = query.Where(x => x.c.IdboPhan == idBoPhan);

                var data = await query
                    .OrderBy(x => x.m.NgayHenTra == null)      // phiếu có hẹn trả lên trước
                    .ThenBy(x => x.m.NgayHenTra)                // hẹn sớm nhất (trễ nhiều nhất) lên đầu
                    .Select(x => new
                    {
                        x.m.IdMuon,
                        x.m.IdCcdc,
                        x.c.MaCcdc,
                        x.c.TenCcdc,
                        x.c.DonViTinh,
                        x.m.MaNhanVien,
                        x.m.HoTen,
                        x.m.BoPhan,
                        x.m.TenCongTy,
                        x.m.SoLuongMuon,
                        x.m.SoLuongDaTra,
                        ConNo = x.m.SoLuongMuon - x.m.SoLuongDaTra,
                        x.m.NgayMuon,
                        x.m.NgayHenTra,
                        x.m.GhiChu
                    })
                    .ToListAsync();

                var ketQua = data
                    .Select(m => new
                    {
                        m.IdMuon, m.IdCcdc, m.MaCcdc, m.TenCcdc, m.DonViTinh,
                        m.MaNhanVien, m.HoTen, m.BoPhan, m.TenCongTy,
                        m.SoLuongMuon, m.SoLuongDaTra, m.ConNo,
                        m.NgayMuon, m.NgayHenTra, m.GhiChu,
                        SoNgayTre = TinhSoNgayTre(m.NgayHenTra, null)
                    })
                    .Where(m => !chiQuaHan || m.SoNgayTre > 0)
                    .ToList();

                return Json(new
                {
                    success = true,
                    data = ketQua,
                    tongPhieu = ketQua.Count,
                    tongQuaHan = ketQua.Count(m => m.SoNgayTre > 0),
                    tongConNo = ketQua.Sum(m => m.ConNo)
                });
            }
            catch (Exception ex) { return Json(new { success = false, message = ex.Message }); }
        }

        /// <summary>
        /// Số ngày trễ so với ngày hẹn trả. 0 = đúng hạn / chưa tới hạn / không hẹn / đã trả xong.
        /// Mốc so sánh là ngày hẹn trả: quá nửa đêm hôm đó mới tính là trễ 1 ngày.
        /// </summary>
        private static int TinhSoNgayTre(DateOnly? ngayHenTra, DateTime? ngayTra)
        {
            if (ngayHenTra == null || ngayTra != null) return 0;
            var soNgay = DateOnly.FromDateTime(DateTime.Now).DayNumber - ngayHenTra.Value.DayNumber;
            return soNgay > 0 ? soNgay : 0;
        }

        [HttpPost("/QLCCDC/ChoMuon")]
        public async Task<IActionResult> ChoMuon(int idCcdc, string? maNhanVien, int soLuongMuon,
                                                 DateOnly? ngayHenTra, string? ghiChu)
        {
            if (!CoQuyen()) return Json(new { success = false, message = "Bạn không có quyền thao tác." });

            if (soLuongMuon <= 0)
                return Json(new { success = false, message = "Số lượng mượn phải lớn hơn 0." });

            if (string.IsNullOrWhiteSpace(maNhanVien))
                return Json(new { success = false, message = "Vui lòng nhập mã nhân viên người mượn." });

            try
            {
                var ccdc = await _context.KkCongCuDungCus
                    .FirstOrDefaultAsync(x => x.IdCcdc == idCcdc && x.NgayXoa == null);
                if (ccdc == null)
                    return Json(new { success = false, message = "Không tìm thấy công cụ dụng cụ." });

                var dangMuon = await _context.KkCcdcMuonTras
                    .Where(m => m.IdCcdc == idCcdc && m.NgayTra == null)
                    .SumAsync(m => (int?)(m.SoLuongMuon - m.SoLuongDaTra)) ?? 0;

                int conLai = ccdc.SoLuong - dangMuon;
                if (soLuongMuon > conLai)
                    return Json(new { success = false, message = $"Chỉ còn {conLai} đơn vị khả dụng (tổng {ccdc.SoLuong}, đang cho mượn {dangMuon})." });

                var ma = maNhanVien.Trim();
                var nv = await _context.Users
                    .Where(u => u.MaNhanVien == ma)
                    .Select(u => new { u.IdNguoiDung, u.HoTen, u.PhongBan, u.TenCongTy })
                    .FirstOrDefaultAsync();

                if (nv == null)
                    return Json(new { success = false, message = $"Không tìm thấy nhân viên có mã \"{ma}\"." });

                var phieu = new KkCcdcMuonTra
                {
                    IdCcdc = idCcdc,
                    SoLuongMuon = soLuongMuon,
                    SoLuongDaTra = 0,
                    MaNhanVien = ma,
                    IdNguoiDung = nv.IdNguoiDung,
                    // Chụp lại thông tin người mượn ngay lúc này, xem mô tả ở KkCcdcMuonTra
                    HoTen = nv.HoTen,
                    BoPhan = nv.PhongBan,
                    TenCongTy = nv.TenCongTy,
                    NgayMuon = DateTime.Now,
                    NgayHenTra = ngayHenTra,
                    NguoiChoMuon = User.Identity?.Name,
                    GhiChu = ghiChu,
                    NgayTao = DateTime.Now
                };

                _context.KkCcdcMuonTras.Add(phieu);
                await _context.SaveChangesAsync();

                GhiLichSu("Cho mượn", idCcdc,
                    $"{ccdc.TenCcdc} - SL {soLuongMuon} - người mượn: {ma} {nv.HoTen} ({nv.PhongBan})");

                return Json(new { success = true, message = "Đã ghi nhận cho mượn!" });
            }
            catch (Exception ex) { return Json(new { success = false, message = ex.Message }); }
        }

        [HttpPost("/QLCCDC/NhanTra")]
        public async Task<IActionResult> NhanTra(int idMuon, int soLuongTra, string? tinhTrangKhiTra, string? ghiChu)
        {
            if (!CoQuyen()) return Json(new { success = false, message = "Bạn không có quyền thao tác." });

            try
            {
                var phieu = await _context.KkCcdcMuonTras.FirstOrDefaultAsync(m => m.IdMuon == idMuon);
                if (phieu == null)
                    return Json(new { success = false, message = "Không tìm thấy phiếu mượn." });

                if (phieu.NgayTra != null)
                    return Json(new { success = false, message = "Phiếu này đã trả xong trước đó." });

                int conNo = phieu.SoLuongMuon - phieu.SoLuongDaTra;
                if (soLuongTra <= 0 || soLuongTra > conNo)
                    return Json(new { success = false, message = $"Số lượng trả phải từ 1 đến {conNo}." });

                phieu.SoLuongDaTra += soLuongTra;
                phieu.TinhTrangKhiTra = tinhTrangKhiTra;
                phieu.NguoiNhanTra = User.Identity?.Name;
                phieu.NgayCapNhat = DateTime.Now;

                if (!string.IsNullOrWhiteSpace(ghiChu))
                    phieu.GhiChu = string.IsNullOrWhiteSpace(phieu.GhiChu) ? ghiChu : $"{phieu.GhiChu} | {ghiChu}";

                // Trả đủ mới đóng phiếu; trả thiếu thì phiếu vẫn mở với phần còn nợ
                bool traDu = phieu.SoLuongDaTra >= phieu.SoLuongMuon;
                if (traDu) phieu.NgayTra = DateTime.Now;

                // Trả về hỏng/mất thì kho phải phản ánh ngay, không để tồn kho ảo.
                // "Trầy xước"/"Nguyên vẹn" coi như dùng được, không đụng tới CCDC.
                string? capNhatKho = null;
                var ccdc = await _context.KkCongCuDungCus
                    .FirstOrDefaultAsync(c => c.IdCcdc == phieu.IdCcdc && c.NgayXoa == null);

                if (ccdc != null)
                {
                    if (tinhTrangKhiTra == "Hư hỏng")
                    {
                        ccdc.TinhTrang = "Hư hỏng";
                        ccdc.NgayCapNhat = DateTime.Now;
                        capNhatKho = "đã chuyển tình trạng CCDC sang \"Hư hỏng\"";
                    }
                    else if (tinhTrangKhiTra == "Mất")
                    {
                        // Mất thì món đó không còn trong kho nữa -> trừ khỏi tổng số lượng
                        int soLuongMoi = ccdc.SoLuong - soLuongTra;
                        ccdc.SoLuong = soLuongMoi > 0 ? soLuongMoi : 0;
                        ccdc.NgayCapNhat = DateTime.Now;
                        capNhatKho = $"đã trừ {soLuongTra} khỏi tổng số lượng (còn {ccdc.SoLuong})";
                    }
                }

                await _context.SaveChangesAsync();

                if (capNhatKho != null)
                    GhiLichSu("Cập nhật kho sau khi nhận trả", phieu.IdCcdc,
                        $"Phiếu #{idMuon} - trả về tình trạng \"{tinhTrangKhiTra}\" - {capNhatKho}");

                GhiLichSu(traDu ? "Nhận trả" : "Nhận trả một phần", phieu.IdCcdc,
                    $"Phiếu #{idMuon} - trả {soLuongTra}/{phieu.SoLuongMuon} - người mượn: {phieu.MaNhanVien} {phieu.HoTen} - tình trạng: {tinhTrangKhiTra ?? "không ghi"}");

                var loiNhan = traDu
                    ? "Đã nhận trả đủ, phiếu được đóng."
                    : $"Đã nhận trả {soLuongTra}, còn nợ {phieu.SoLuongMuon - phieu.SoLuongDaTra}.";
                if (capNhatKho != null) loiNhan += $" Đồng thời {capNhatKho}.";

                return Json(new { success = true, message = loiNhan });
            }
            catch (Exception ex) { return Json(new { success = false, message = ex.Message }); }
        }

        #endregion

        /// <summary>Ghi vào KK_LichSuThaoTac để mọi thao tác thêm/sửa/xoá CCDC đều có vết.</summary>
        private void GhiLichSu(string hanhDong, int idDoiTuong, string chiTiet)
        {
            try
            {
                _context.KkLichSuThaoTacs.Add(new KkLichSuThaoTac
                {
                    HanhDong = hanhDong,
                    DoiTuong = "Công Cụ Dụng Cụ",
                    IdDoiTuong = idDoiTuong,
                    ChiTiet = chiTiet,
                    ThoiGian = DateTime.Now,
                    NguoiThaoTac = User.Identity?.Name ?? "Hệ thống"
                });
                _context.SaveChanges();
            }
            catch { /* Bỏ qua lỗi ghi log để không làm gián đoạn luồng chính */ }
        }
    }
}
