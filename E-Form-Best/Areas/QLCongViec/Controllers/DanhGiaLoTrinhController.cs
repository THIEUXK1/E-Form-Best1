using E_Form_Best.Areas.QLCongViec.Services;
using Microsoft.AspNetCore.Mvc;

namespace E_Form_Best.Areas.QLCongViec.Controllers
{
    /// <summary>
    /// Lộ trình thăng tiến: /QLCongViec/DanhGia/LoTrinhCuaToi (nhân viên chỉ xem), /QLCongViec/DanhGia/LoTrinh/{maNv} (quản lý).
    /// Dùng chung view + JS. Nhân viên định danh bằng claim MaNv, không nhận mã từ client; mọi thao tác ghi chỉ dành cho quản lý.
    /// </summary>
    [Area("QLCongViec")]
    public class DanhGiaLoTrinhController : Controller
    {
        private readonly DanhGiaLoTrinhService _loTrinh;
        private readonly DanhGiaQuanLyService _quanLy;

        public DanhGiaLoTrinhController(DanhGiaLoTrinhService loTrinh, DanhGiaQuanLyService quanLy)
        {
            _loTrinh = loTrinh;
            _quanLy = quanLy;
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

        // Chống gọi thẳng API với mã tuỳ ý
        private async Task<string?> LoiMa(string? ma)
        {
            if (ma == null) return "Thiếu mã nhân viên.";
            return await _quanLy.TrongHeThongAsync(ma) ? null : $"{ma} không có trong danh sách đánh giá.";
        }

        #region View

        [HttpGet("/QLCongViec/DanhGia/LoTrinhCuaToi")]
        public IActionResult LoTrinhCuaToi()
        {
            if (User?.Identity?.IsAuthenticated != true) return Redirect("/DonXetDuyet/DangNhap");
            if (!LaNhanVien()) return Forbid();
            ViewBag.MaNv = "";
            return View("LoTrinh");
        }

        [HttpGet("/QLCongViec/DanhGia/LoTrinh/{maNv}")]
        public IActionResult LoTrinh(string maNv)
        {
            if (User?.Identity?.IsAuthenticated != true) return Redirect("/DonXetDuyet/DangNhap");
            if (!LaQuanLy()) return Forbid();
            var ma = ChuanMa(maNv);
            if (ma == null) return NotFound();
            ViewBag.MaNv = ma;
            return View("LoTrinh");
        }

        #endregion

        #region API

        [HttpGet("/QLCongViec/DanhGia/LoTrinhApi/CuaToi")]
        public async Task<IActionResult> CuaToi()
        {
            if (ChanAjax(LaNhanVien()) is { } chan) return chan;
            var ma = MaCuaToi();
            if (string.IsNullOrEmpty(ma)) return Json(new { thanhCong = false, thongBao = "Tài khoản chưa gắn mã nhân viên." });
            try
            {
                return Json(new { thanhCong = true, duLieu = await _loTrinh.LayAsync(ma, false) });
            }
            catch (Exception ex)
            {
                return Json(new { thanhCong = false, thongBao = "Không tải được lộ trình: " + ex.Message });
            }
        }

        [HttpGet("/QLCongViec/DanhGia/LoTrinhApi/NhanVien")]
        public async Task<IActionResult> NhanVien(string? maNv)
        {
            if (ChanAjax(LaQuanLy()) is { } chan) return chan;
            var ma = ChuanMa(maNv);
            if (await LoiMa(ma) is { } loi) return Json(new { thanhCong = false, thongBao = loi });
            try
            {
                return Json(new { thanhCong = true, duLieu = await _loTrinh.LayAsync(ma!, true) });
            }
            catch (Exception ex)
            {
                return Json(new { thanhCong = false, thongBao = "Không tải được lộ trình: " + ex.Message });
            }
        }

        // Mọi thao tác ghi trả lại toàn bộ lộ trình để trang vẽ lại tại chỗ
        private async Task<IActionResult> GhiRoiTraVe(string? maNv, Func<string, Task<string?>> ghi, string thongBao)
        {
            if (ChanAjax(LaQuanLy()) is { } chan) return chan;
            var ma = ChuanMa(maNv);
            if (await LoiMa(ma) is { } loiMa) return Json(new { thanhCong = false, thongBao = loiMa });
            try
            {
                var loi = await ghi(NguoiThucHien());
                if (loi != null) return Json(new { thanhCong = false, thongBao = loi });
                return Json(new { thanhCong = true, thongBao, duLieu = await _loTrinh.LayAsync(ma!, true) });
            }
            catch (Exception ex)
            {
                return Json(new { thanhCong = false, thongBao = "Lưu thất bại: " + ex.Message });
            }
        }

        [HttpPost("/QLCongViec/DanhGia/LoTrinhApi/ThemThi")]
        [ValidateAntiForgeryToken]
        public Task<IActionResult> ThemThi([FromBody] DanhGiaLoTrinhService.ThemThiVao? vao)
            => GhiRoiTraVe(vao?.MaNv, nguoi => _loTrinh.ThemThiAsync(vao!, nguoi), "Đã ghi kết quả thi.");

        [HttpPost("/QLCongViec/DanhGia/LoTrinhApi/XoaThi")]
        [ValidateAntiForgeryToken]
        public Task<IActionResult> XoaThi([FromBody] DanhGiaLoTrinhService.ThiVao? vao)
            => GhiRoiTraVe(vao?.MaNv, nguoi => _loTrinh.XoaThiAsync(vao!, nguoi), "Đã xoá kết quả thi.");

        [HttpPost("/QLCongViec/DanhGia/LoTrinhApi/XacNhanLenBac")]
        [ValidateAntiForgeryToken]
        public Task<IActionResult> XacNhanLenBac([FromBody] DanhGiaLoTrinhService.ThiVao? vao)
            => GhiRoiTraVe(vao?.MaNv, nguoi => _loTrinh.XacNhanLenBacAsync(vao!, nguoi), "Đã xác nhận lên bậc.");

        [HttpPost("/QLCongViec/DanhGia/LoTrinhApi/LuuNgayGiuBac")]
        [ValidateAntiForgeryToken]
        public Task<IActionResult> LuuNgayGiuBac([FromBody] DanhGiaLoTrinhService.NgayGiuBacVao? vao)
            => GhiRoiTraVe(vao?.MaNv, nguoi => _loTrinh.LuuNgayGiuBacAsync(vao!, nguoi), "Đã lưu ngày giữ bậc.");

        #endregion
    }
}
