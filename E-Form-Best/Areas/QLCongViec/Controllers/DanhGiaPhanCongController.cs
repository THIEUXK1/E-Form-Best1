using E_Form_Best.Areas.QLCongViec.Services;
using Microsoft.AspNetCore.Mvc;

namespace E_Form_Best.Areas.QLCongViec.Controllers
{
    /// <summary>Ma trận phân công /QLCongViec/DanhGia/PhanCong: quản lý sửa, cấp trên chỉ xem.</summary>
    [Area("QLCongViec")]
    public class DanhGiaPhanCongController : Controller
    {
        private readonly DanhGiaPhanCongService _phanCong;

        public DanhGiaPhanCongController(DanhGiaPhanCongService phanCong)
        {
            _phanCong = phanCong;
        }

        private bool LaQuanLy() => User.IsInRole("DanhGiaQuanLy") || User.IsInRole("All");

        private bool CoQuyenXem() => LaQuanLy() || User.IsInRole("DanhGiaCapTren");

        private string NguoiThucHien() => User.FindFirst("MaNv")?.Value?.Trim() ?? User.Identity?.Name ?? "";

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

        [HttpGet("/QLCongViec/DanhGia/PhanCong")]
        public IActionResult PhanCong()
        {
            if (User?.Identity?.IsAuthenticated != true) return Redirect("/DonXetDuyet/DangNhap");
            if (!CoQuyenXem()) return Forbid();
            ViewBag.DuocSua = LaQuanLy();
            return View();
        }

        [HttpGet("/QLCongViec/DanhGia/PhanCongApi/MaTran")]
        public async Task<IActionResult> MaTran()
        {
            if (ChanAjax(CoQuyenXem()) is { } chan) return chan;
            try
            {
                return Json(new { thanhCong = true, duLieu = await _phanCong.MaTranAsync() });
            }
            catch (Exception ex)
            {
                return Json(new { thanhCong = false, thongBao = "Không tải được ma trận: " + ex.Message });
            }
        }

        /// <summary>Tải riêng sau ma trận vì phải đếm đơn IT từng người.</summary>
        [HttpGet("/QLCongViec/DanhGia/PhanCongApi/DonTheoNguoi")]
        public async Task<IActionResult> DonTheoNguoi()
        {
            if (ChanAjax(CoQuyenXem()) is { } chan) return chan;
            try
            {
                var maTran = await _phanCong.MaTranAsync();
                return Json(new { thanhCong = true, duLieu = await _phanCong.DonTheoNguoiAsync(maTran.DsNguoi.Select(x => x.MaNv)) });
            }
            catch (Exception ex)
            {
                return Json(new { thanhCong = false, thongBao = "Không tải được số đơn IT: " + ex.Message });
            }
        }

        private async Task<IActionResult> GhiRoiTraVe(Func<string, Task<string?>> ghi, string thongBao)
        {
            if (ChanAjax(LaQuanLy()) is { } chan) return chan;
            try
            {
                var loi = await ghi(NguoiThucHien());
                if (loi != null) return Json(new { thanhCong = false, thongBao = loi });
                return Json(new { thanhCong = true, thongBao, duLieu = await _phanCong.MaTranAsync() });
            }
            catch (Exception ex)
            {
                return Json(new { thanhCong = false, thongBao = "Lưu thất bại: " + ex.Message });
            }
        }

        [HttpPost("/QLCongViec/DanhGia/PhanCongApi/LuuO")]
        [ValidateAntiForgeryToken]
        public Task<IActionResult> LuuO([FromBody] DanhGiaPhanCongService.LuuOVao? vao)
            => GhiRoiTraVe(nguoi => vao == null ? Task.FromResult<string?>("Thiếu dữ liệu.") : _phanCong.LuuOAsync(vao, nguoi), "Đã lưu.");

        [HttpPost("/QLCongViec/DanhGia/PhanCongApi/LuuDauViec")]
        [ValidateAntiForgeryToken]
        public Task<IActionResult> LuuDauViec([FromBody] DanhGiaPhanCongService.LuuDauViecVao? vao)
            => GhiRoiTraVe(nguoi => vao == null ? Task.FromResult<string?>("Thiếu dữ liệu.") : _phanCong.LuuDauViecAsync(vao, nguoi), "Đã lưu đầu việc.");

        [HttpPost("/QLCongViec/DanhGia/PhanCongApi/XoaDauViec")]
        [ValidateAntiForgeryToken]
        public Task<IActionResult> XoaDauViec([FromBody] DanhGiaPhanCongService.XoaDauViecVao? vao)
            => GhiRoiTraVe(nguoi => vao == null ? Task.FromResult<string?>("Thiếu dữ liệu.") : _phanCong.XoaDauViecAsync(vao.IdDauViec, nguoi), "Đã xoá đầu việc.");
    }
}
