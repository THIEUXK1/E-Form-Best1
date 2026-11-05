using E_Form_Best.Areas.QLCongViec.Services;
using Microsoft.AspNetCore.Mvc;

namespace E_Form_Best.Areas.QLCongViec.Controllers
{
    /// <summary>
    /// Bản đồ năng lực + GAP: /QLCongViec/DanhGia/NangLuc (quản lý), /QLCongViec/DanhGia/NangLuc/{maNv} (quản lý chấm),
    /// /QLCongViec/DanhGia/NangLucCuaToi (nhân viên tự chấm). Hai trang chấm dùng chung view + JS, khác nguồn dữ liệu.
    /// Nhân viên định danh bằng claim MaNv, không nhận mã từ client; quản lý không tự chấm cho chính mình.
    /// </summary>
    [Area("QLCongViec")]
    public class DanhGiaNangLucController : Controller
    {
        private readonly DanhGiaNangLucService _nangLuc;
        private readonly DanhGiaQuanLyService _quanLy;
        private readonly DanhGiaService _danhGia;

        public DanhGiaNangLucController(DanhGiaNangLucService nangLuc, DanhGiaQuanLyService quanLy, DanhGiaService danhGia)
        {
            _nangLuc = nangLuc;
            _quanLy = quanLy;
            _danhGia = danhGia;
        }

        private bool LaQuanLy() => User.IsInRole("DanhGiaQuanLy") || User.IsInRole("All");

        // Cùng điều kiện với DanhGiaController
        private bool LaNhanVien() => User.IsInRole("FORM_CV_VIEW") || User.IsInRole("All");

        private string? MaCuaToi() => User.FindFirst("MaNv")?.Value?.Trim();

        private IActionResult? ChanAjax(bool coQuyen)
        {
            if (User?.Identity?.IsAuthenticated != true)
            {
                Response.StatusCode = StatusCodes.Status401Unauthorized;
                return Json(new { thanhCong = false, thongBao = "Phiên đăng nhập đã hết hạn. Vui lòng đăng nhập lại." });
            }
            if (!coQuyen)
            {
                Response.StatusCode = StatusCodes.Status403Forbidden;
                return Json(new { thanhCong = false, thongBao = "Bạn không có quyền dùng chức năng này." });
            }
            return null;
        }

        private static string? ChuanMa(string? maNv)
        {
            var ma = maNv?.Trim();
            return string.IsNullOrEmpty(ma) || ma.Length > 20 ? null : ma;
        }

        // Dữ liệu một người: gói chung để 2 trang chấm nhận cùng cấu trúc
        private async Task<object> GoiDuLieu(string ma, DanhGiaService.KyDto ky, DanhGiaNangLucService.KetQuaNguoi kq, bool laChinhMinh) => new
        {
            ky,
            maNv = ma,
            hoTen = (await _danhGia.LayThongTinAsync(ma)).HoTen,
            thangToiDa = DanhGiaNangLucService.ThangDiemToiDa,
            laChinhMinh,
            daChot = await _danhGia.PhieuDaKhoaAsync(ma, ky.IdKy),
            ketQua = kq
        };

        #region View

        [HttpGet("/QLCongViec/DanhGia/NangLuc")]
        public IActionResult BanDo()
        {
            if (User?.Identity?.IsAuthenticated != true) return Redirect("/DonXetDuyet/DangNhap");
            if (!LaQuanLy()) return Forbid();
            return View();
        }

        [HttpGet("/QLCongViec/DanhGia/NangLuc/{maNv}")]
        public IActionResult ChamNangLuc(string maNv)
        {
            if (User?.Identity?.IsAuthenticated != true) return Redirect("/DonXetDuyet/DangNhap");
            if (!LaQuanLy()) return Forbid();
            var ma = ChuanMa(maNv);
            if (ma == null) return NotFound();
            ViewBag.MaNv = ma;
            return View();
        }

        [HttpGet("/QLCongViec/DanhGia/NangLucCuaToi")]
        public IActionResult NangLucCuaToi()
        {
            if (User?.Identity?.IsAuthenticated != true) return Redirect("/DonXetDuyet/DangNhap");
            if (!LaNhanVien()) return Forbid();
            ViewBag.MaNv = "";
            return View("ChamNangLuc");
        }

        #endregion

        #region API quản lý

        [HttpGet("/QLCongViec/DanhGia/NangLucApi/BanDo")]
        public async Task<IActionResult> DuLieuBanDo(Guid? idKy)
        {
            if (ChanAjax(LaQuanLy()) is { } chan) return chan;
            try
            {
                var ky = await _danhGia.LayKyAsync(idKy);
                if (ky == null) return Json(new { thanhCong = false, thongBao = "Chưa có kỳ đánh giá nào." });
                return Json(new { thanhCong = true, duLieu = new { ky, dsNhanVien = await _nangLuc.BanDoAsync(ky) } });
            }
            catch (Exception ex)
            {
                return Json(new { thanhCong = false, thongBao = "Không tải được bản đồ năng lực: " + ex.Message });
            }
        }

        [HttpGet("/QLCongViec/DanhGia/NangLucApi/NhanVien")]
        public async Task<IActionResult> DuLieuNhanVien(string? maNv, Guid? idKy)
        {
            if (ChanAjax(LaQuanLy()) is { } chan) return chan;
            var ma = ChuanMa(maNv);
            if (ma == null) return Json(new { thanhCong = false, thongBao = "Thiếu mã nhân viên." });
            try
            {
                var ky = await _danhGia.LayKyAsync(idKy);
                if (ky == null) return Json(new { thanhCong = false, thongBao = "Chưa có kỳ đánh giá nào." });
                if (!(await _quanLy.DsMaNvTheoKyAsync(ky)).Contains(ma, StringComparer.OrdinalIgnoreCase))
                    return Json(new { thanhCong = false, thongBao = $"{ma} không có trong danh sách đánh giá của kỳ {ky.Ten}." });
                var laChinhMinh = string.Equals(ma, MaCuaToi(), StringComparison.OrdinalIgnoreCase);
                return Json(new { thanhCong = true, duLieu = await GoiDuLieu(ma, ky, await _nangLuc.LayCuaNguoiAsync(ma, ky), laChinhMinh) });
            }
            catch (Exception ex)
            {
                return Json(new { thanhCong = false, thongBao = "Không tải được năng lực: " + ex.Message });
            }
        }

        [HttpPost("/QLCongViec/DanhGia/NangLucApi/LuuQuanLy")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> LuuQuanLy([FromBody] DanhGiaNangLucService.LuuVao? vao)
        {
            if (ChanAjax(LaQuanLy()) is { } chan) return chan;
            var ma = ChuanMa(vao?.MaNv);
            if (ma == null) return Json(new { thanhCong = false, thongBao = "Thiếu mã nhân viên." });
            if (string.Equals(ma, MaCuaToi(), StringComparison.OrdinalIgnoreCase))
                return Json(new { thanhCong = false, thongBao = "Không tự chấm phiếu quản lý cho chính mình." });
            if (!await _quanLy.TrongHeThongAsync(ma))
                return Json(new { thanhCong = false, thongBao = $"{ma} không có trong danh sách đánh giá." });
            try
            {
                var loi = await _nangLuc.LuuAsync(ma, vao!.IdKy, DanhGiaService.LoaiQuanLy, vao.DsDiem);
                if (loi != null) return Json(new { thanhCong = false, thongBao = loi });
                var ky = (await _danhGia.LayKyAsync(vao.IdKy))!;
                return Json(new { thanhCong = true, thongBao = "Đã lưu điểm năng lực.", duLieu = await GoiDuLieu(ma, ky, await _nangLuc.LayCuaNguoiAsync(ma, ky), false) });
            }
            catch (Exception ex)
            {
                return Json(new { thanhCong = false, thongBao = "Lưu điểm năng lực thất bại: " + ex.Message });
            }
        }

        #endregion

        #region API nhân viên

        [HttpGet("/QLCongViec/DanhGia/NangLucApi/CuaToi")]
        public async Task<IActionResult> DuLieuCuaToi(Guid? idKy)
        {
            if (ChanAjax(LaNhanVien()) is { } chan) return chan;
            var ma = MaCuaToi();
            if (string.IsNullOrEmpty(ma)) return Json(new { thanhCong = false, thongBao = "Tài khoản chưa có mã nhân viên." });
            try
            {
                var ky = await _danhGia.LayKyAsync(idKy);
                if (ky == null) return Json(new { thanhCong = false, thongBao = "Chưa có kỳ đánh giá nào." });
                return Json(new { thanhCong = true, duLieu = await GoiDuLieu(ma, ky, await _nangLuc.LayCuaToiAsync(ma, ky), true) });
            }
            catch (Exception ex)
            {
                return Json(new { thanhCong = false, thongBao = "Không tải được năng lực: " + ex.Message });
            }
        }

        [HttpPost("/QLCongViec/DanhGia/NangLucApi/LuuTuCham")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> LuuTuCham([FromBody] DanhGiaNangLucService.LuuVao? vao)
        {
            if (ChanAjax(LaNhanVien()) is { } chan) return chan;
            var ma = MaCuaToi();
            if (string.IsNullOrEmpty(ma)) return Json(new { thanhCong = false, thongBao = "Tài khoản chưa có mã nhân viên." });
            if (vao == null) return Json(new { thanhCong = false, thongBao = "Dữ liệu gửi lên không hợp lệ." });
            try
            {
                var loi = await _nangLuc.LuuAsync(ma, vao.IdKy, DanhGiaService.LoaiTuDanhGia, vao.DsDiem);
                if (loi != null) return Json(new { thanhCong = false, thongBao = loi });
                var ky = (await _danhGia.LayKyAsync(vao.IdKy))!;
                return Json(new { thanhCong = true, thongBao = "Đã lưu điểm tự chấm.", duLieu = await GoiDuLieu(ma, ky, await _nangLuc.LayCuaToiAsync(ma, ky), true) });
            }
            catch (Exception ex)
            {
                return Json(new { thanhCong = false, thongBao = "Lưu điểm thất bại: " + ex.Message });
            }
        }

        #endregion
    }
}
