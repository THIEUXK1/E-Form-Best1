using E_Form_Best.Areas.QLCongViec.Services;
using Microsoft.AspNetCore.Mvc;

namespace E_Form_Best.Areas.QLCongViec.Controllers
{
    /// <summary>
    /// Kế hoạch phát triển: /QLCongViec/DanhGia/KeHoachCuaToi (nhân viên), /QLCongViec/DanhGia/KeHoach/{maNv} (quản lý).
    /// Dùng chung view + JS, khác nguồn dữ liệu. Nhân viên định danh bằng claim MaNv, không nhận mã từ client.
    /// </summary>
    [Area("QLCongViec")]
    public class DanhGiaKeHoachController : Controller
    {
        private readonly DanhGiaMucTieuService _mucTieu;
        private readonly DanhGiaNangLucService _nangLuc;
        private readonly DanhGiaQuanLyService _quanLy;
        private readonly DanhGiaService _danhGia;

        public DanhGiaKeHoachController(DanhGiaMucTieuService mucTieu, DanhGiaNangLucService nangLuc, DanhGiaQuanLyService quanLy, DanhGiaService danhGia)
        {
            _mucTieu = mucTieu;
            _nangLuc = nangLuc;
            _quanLy = quanLy;
            _danhGia = danhGia;
        }

        private bool LaQuanLy() => User.IsInRole("DanhGiaQuanLy") || User.IsInRole("All");

        // Cùng điều kiện với DanhGiaController
        private bool LaNhanVien() => User.IsInRole("FORM_CV_VIEW") || User.IsInRole("All");

        private string? MaCuaToi() => User.FindFirst("MaNv")?.Value?.Trim();

        private string NguoiThucHien() => MaCuaToi() ?? User.Identity?.Name ?? "";

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

        // Quản lý đọc/ghi hộ: mã phải thuộc danh sách của kỳ, chống gọi thẳng API với mã tuỳ ý
        private async Task<string?> LoiMaQuanLy(string? ma, DanhGiaService.KyDto ky)
        {
            if (ma == null) return "Thiếu mã nhân viên.";
            return (await _quanLy.DsMaNvTheoKyAsync(ky)).Contains(ma, StringComparer.OrdinalIgnoreCase)
                ? null : $"{ma} không có trong danh sách đánh giá của kỳ {ky.Ten}.";
        }

        #region View

        [HttpGet("/QLCongViec/DanhGia/KeHoachCuaToi")]
        public IActionResult KeHoachCuaToi()
        {
            if (User?.Identity?.IsAuthenticated != true) return Redirect("/DonXetDuyet/DangNhap");
            if (!LaNhanVien()) return Forbid();
            ViewBag.MaNv = "";
            return View("KeHoach");
        }

        [HttpGet("/QLCongViec/DanhGia/KeHoach/{maNv}")]
        public IActionResult KeHoach(string maNv)
        {
            if (User?.Identity?.IsAuthenticated != true) return Redirect("/DonXetDuyet/DangNhap");
            if (!LaQuanLy()) return Forbid();
            var ma = ChuanMa(maNv);
            if (ma == null) return NotFound();
            ViewBag.MaNv = ma;
            return View();
        }

        #endregion

        #region API

        [HttpGet("/QLCongViec/DanhGia/KeHoachApi/CuaToi")]
        public async Task<IActionResult> DuLieuCuaToi(Guid? idKy)
        {
            if (ChanAjax(LaNhanVien()) is { } chan) return chan;
            var ma = MaCuaToi();
            if (string.IsNullOrEmpty(ma)) return Json(new { thanhCong = false, thongBao = "Tài khoản chưa có mã nhân viên." });
            try
            {
                var ky = await _danhGia.LayKyAsync(idKy);
                if (ky == null) return Json(new { thanhCong = false, thongBao = "Chưa có kỳ đánh giá nào." });
                return Json(new { thanhCong = true, duLieu = await _mucTieu.LayAsync(ma, ky, await _nangLuc.LayCuaToiAsync(ma, ky)) });
            }
            catch (Exception ex)
            {
                return Json(new { thanhCong = false, thongBao = "Không tải được kế hoạch: " + ex.Message });
            }
        }

        [HttpGet("/QLCongViec/DanhGia/KeHoachApi/NhanVien")]
        public async Task<IActionResult> DuLieuNhanVien(string? maNv, Guid? idKy)
        {
            if (ChanAjax(LaQuanLy()) is { } chan) return chan;
            var ma = ChuanMa(maNv);
            try
            {
                var ky = await _danhGia.LayKyAsync(idKy);
                if (ky == null) return Json(new { thanhCong = false, thongBao = "Chưa có kỳ đánh giá nào." });
                if (await LoiMaQuanLy(ma, ky) is { } loi) return Json(new { thanhCong = false, thongBao = loi });
                return Json(new { thanhCong = true, duLieu = await _mucTieu.LayAsync(ma!, ky, await _nangLuc.LayCuaNguoiAsync(ma!, ky)) });
            }
            catch (Exception ex)
            {
                return Json(new { thanhCong = false, thongBao = "Không tải được kế hoạch: " + ex.Message });
            }
        }

        private async Task<IActionResult> Luu(string ma, DanhGiaMucTieuService.LuuVao vao)
        {
            try
            {
                var loi = await _mucTieu.LuuAsync(ma, vao, NguoiThucHien());
                return Json(new { thanhCong = loi == null, thongBao = loi ?? "Đã lưu mục tiêu." });
            }
            catch (Exception ex)
            {
                return Json(new { thanhCong = false, thongBao = "Lưu mục tiêu thất bại: " + ex.Message });
            }
        }

        private async Task<IActionResult> Xoa(string ma, Guid idMucTieu)
        {
            try
            {
                var loi = await _mucTieu.XoaAsync(ma, idMucTieu, NguoiThucHien());
                return Json(new { thanhCong = loi == null, thongBao = loi ?? "Đã xoá mục tiêu." });
            }
            catch (Exception ex)
            {
                return Json(new { thanhCong = false, thongBao = "Xoá mục tiêu thất bại: " + ex.Message });
            }
        }

        [HttpPost("/QLCongViec/DanhGia/KeHoachApi/LuuCuaToi")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> LuuCuaToi([FromBody] DanhGiaMucTieuService.LuuVao? vao)
        {
            if (ChanAjax(LaNhanVien()) is { } chan) return chan;
            var ma = MaCuaToi();
            if (string.IsNullOrEmpty(ma) || vao == null) return Json(new { thanhCong = false, thongBao = "Dữ liệu gửi lên không hợp lệ." });
            return await Luu(ma, vao);
        }

        [HttpPost("/QLCongViec/DanhGia/KeHoachApi/LuuQuanLy")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> LuuQuanLy([FromBody] DanhGiaMucTieuService.LuuVao? vao)
        {
            if (ChanAjax(LaQuanLy()) is { } chan) return chan;
            var ma = ChuanMa(vao?.MaNv);
            var ky = vao == null ? null : await _danhGia.LayKyAsync(vao.IdKy);
            if (ky == null) return Json(new { thanhCong = false, thongBao = "Dữ liệu gửi lên không hợp lệ." });
            if (await LoiMaQuanLy(ma, ky) is { } loi) return Json(new { thanhCong = false, thongBao = loi });
            return await Luu(ma!, vao!);
        }

        [HttpPost("/QLCongViec/DanhGia/KeHoachApi/XoaCuaToi")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> XoaCuaToi([FromBody] DanhGiaMucTieuService.XoaVao? vao)
        {
            if (ChanAjax(LaNhanVien()) is { } chan) return chan;
            var ma = MaCuaToi();
            if (string.IsNullOrEmpty(ma) || vao == null) return Json(new { thanhCong = false, thongBao = "Dữ liệu gửi lên không hợp lệ." });
            return await Xoa(ma, vao.IdMucTieu);
        }

        [HttpPost("/QLCongViec/DanhGia/KeHoachApi/XoaQuanLy")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> XoaQuanLy([FromBody] DanhGiaMucTieuService.XoaVao? vao)
        {
            if (ChanAjax(LaQuanLy()) is { } chan) return chan;
            var ma = ChuanMa(vao?.MaNv);
            if (ma == null || !await _quanLy.TrongHeThongAsync(ma)) return Json(new { thanhCong = false, thongBao = "Nhân viên không có trong danh sách đánh giá." });
            return await Xoa(ma, vao!.IdMucTieu);
        }

        #endregion
    }
}
