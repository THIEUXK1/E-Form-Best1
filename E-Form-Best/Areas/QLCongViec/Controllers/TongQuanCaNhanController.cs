using E_Form_Best.Areas.QLCongViec.Services;
using Microsoft.AspNetCore.Mvc;

namespace E_Form_Best.Areas.QLCongViec.Controllers
{
    /// <summary>Dữ liệu dashboard /QLCongViec/Logo. Định danh bằng claim MaNv, không nhận mã từ client nên chỉ đọc được của chính mình.</summary>
    [Area("QLCongViec")]
    public class TongQuanCaNhanController : Controller
    {
        private readonly TongQuanCaNhanService _tongQuan;

        public TongQuanCaNhanController(TongQuanCaNhanService tongQuan)
        {
            _tongQuan = tongQuan;
        }

        [HttpGet("/QLCongViec/TongQuanApi")]
        public async Task<IActionResult> LayDuLieu()
        {
            if (User?.Identity?.IsAuthenticated != true)
            {
                Response.StatusCode = StatusCodes.Status401Unauthorized;
                return Json(new { thanhCong = false, thongBao = "Phiên đăng nhập đã hết hạn. Vui lòng đăng nhập lại." });
            }
            var ma = User.FindFirst("MaNv")?.Value?.Trim();
            // Tài khoản không gắn mã nhân viên vẫn dùng được phần đơn công việc ở client
            if (string.IsNullOrEmpty(ma)) return Json(new { thanhCong = true, duLieu = new TongQuanCaNhanService.TongQuanDto() });
            try
            {
                var laQuanLy = User.IsInRole("DanhGiaQuanLy") || User.IsInRole("All");
                var laCapTren = User.IsInRole("DanhGiaCapTren") || User.IsInRole("All");
                return Json(new { thanhCong = true, duLieu = await _tongQuan.LayAsync(ma, laQuanLy, laCapTren) });
            }
            catch (Exception ex)
            {
                return Json(new { thanhCong = false, thongBao = "Không tải được tổng quan: " + ex.Message });
            }
        }
    }
}
