using E_Form_Best.Areas.QLCongViec.Services;
using Microsoft.AspNetCore.Mvc;

namespace E_Form_Best.Areas.QLCongViec.Controllers
{
    /// <summary>
    /// Ký xác nhận 3 cấp. /QLCongViec/DanhGia/DuyetPhieu là hàng đợi của cấp trên; việc duyệt làm ở trang Chi tiết để
    /// cấp trên đọc phiếu trước khi quyết. Nhân viên ký ở trang Tự đánh giá, định danh bằng claim MaNv.
    /// </summary>
    [Area("QLCongViec")]
    public class DanhGiaKyXacNhanController : Controller
    {
        private readonly DanhGiaKyXacNhanService _kyXacNhan;
        private readonly DanhGiaQuanLyService _quanLy;
        private readonly DanhGiaService _danhGia;

        public DanhGiaKyXacNhanController(DanhGiaKyXacNhanService kyXacNhan, DanhGiaQuanLyService quanLy, DanhGiaService danhGia)
        {
            _kyXacNhan = kyXacNhan;
            _quanLy = quanLy;
            _danhGia = danhGia;
        }

        private bool LaQuanLy() => User.IsInRole("DanhGiaQuanLy") || User.IsInRole("All");

        private bool LaCapTren() => User.IsInRole("DanhGiaCapTren") || User.IsInRole("All");

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

        // Chống gọi thẳng API với mã tuỳ ý: người đã ngừng đánh giá vẫn xem lại được phiếu của kỳ cũ
        private async Task<string?> LoiMa(string? ma, DanhGiaService.KyDto ky)
        {
            if (ma == null) return "Thiếu mã nhân viên.";
            return (await _quanLy.DsMaNvTheoKyAsync(ky)).Contains(ma, StringComparer.OrdinalIgnoreCase)
                ? null : $"{ma} không có trong danh sách đánh giá của kỳ {ky.Ten}.";
        }

        [HttpGet("/QLCongViec/DanhGia/DuyetPhieu")]
        public IActionResult DuyetPhieu()
        {
            if (User?.Identity?.IsAuthenticated != true) return Redirect("/DonXetDuyet/DangNhap");
            if (!LaCapTren()) return Forbid();
            return View();
        }

        [HttpGet("/QLCongViec/DanhGia/KyXacNhanApi/DanhSach")]
        public async Task<IActionResult> DanhSach(Guid? idKy)
        {
            if (ChanAjax(LaCapTren()) is { } chan) return chan;
            try
            {
                var ky = await _danhGia.LayKyAsync(idKy);
                if (ky == null) return Json(new { thanhCong = false, thongBao = "Chưa có kỳ đánh giá nào." });
                return Json(new { thanhCong = true, duLieu = await _kyXacNhan.DanhSachAsync(ky) });
            }
            catch (Exception ex)
            {
                return Json(new { thanhCong = false, thongBao = "Không tải được danh sách: " + ex.Message });
            }
        }

        [HttpGet("/QLCongViec/DanhGia/KyXacNhanApi/CuaToi")]
        public async Task<IActionResult> CuaToi(Guid? idKy)
        {
            if (ChanAjax(LaNhanVien()) is { } chan) return chan;
            var ma = MaCuaToi();
            if (string.IsNullOrEmpty(ma)) return Json(new { thanhCong = false, thongBao = "Tài khoản chưa gắn mã nhân viên." });
            try
            {
                var ky = await _danhGia.LayKyAsync(idKy);
                if (ky == null) return Json(new { thanhCong = false, thongBao = "Chưa có kỳ đánh giá nào." });
                return Json(new { thanhCong = true, duLieu = await _kyXacNhan.LayAsync(ma, ky, true, false, ma) });
            }
            catch (Exception ex)
            {
                return Json(new { thanhCong = false, thongBao = "Không tải được trạng thái ký: " + ex.Message });
            }
        }

        [HttpGet("/QLCongViec/DanhGia/KyXacNhanApi/NhanVien")]
        public async Task<IActionResult> NhanVien(string? maNv, Guid? idKy)
        {
            if (ChanAjax(LaQuanLy() || LaCapTren()) is { } chan) return chan;
            var ma = ChuanMa(maNv);
            try
            {
                var ky = await _danhGia.LayKyAsync(idKy);
                if (ky == null) return Json(new { thanhCong = false, thongBao = "Chưa có kỳ đánh giá nào." });
                if (await LoiMa(ma, ky) is { } loi) return Json(new { thanhCong = false, thongBao = loi });
                return Json(new { thanhCong = true, duLieu = await _kyXacNhan.LayAsync(ma!, ky, false, LaCapTren(), NguoiThucHien()) });
            }
            catch (Exception ex)
            {
                return Json(new { thanhCong = false, thongBao = "Không tải được trạng thái ký: " + ex.Message });
            }
        }

        [HttpPost("/QLCongViec/DanhGia/KyXacNhanApi/Duyet")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Duyet([FromBody] DanhGiaKyXacNhanService.PhieuVao? vao)
        {
            if (ChanAjax(LaCapTren()) is { } chan) return chan;
            var ma = ChuanMa(vao?.MaNv);
            if (ma == null) return Json(new { thanhCong = false, thongBao = "Thiếu mã nhân viên." });
            try
            {
                var loi = await _kyXacNhan.DuyetAsync(ma, vao!.IdKy, NguoiThucHien());
                return Json(new { thanhCong = loi == null, thongBao = loi ?? "Đã duyệt, phiếu chuyển nhân viên ký." });
            }
            catch (Exception ex)
            {
                return Json(new { thanhCong = false, thongBao = "Duyệt thất bại: " + ex.Message });
            }
        }

        [HttpPost("/QLCongViec/DanhGia/KyXacNhanApi/TuChoi")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> TuChoi([FromBody] DanhGiaKyXacNhanService.TuChoiVao? vao)
        {
            if (ChanAjax(LaCapTren()) is { } chan) return chan;
            var ma = ChuanMa(vao?.MaNv);
            if (ma == null) return Json(new { thanhCong = false, thongBao = "Thiếu mã nhân viên." });
            try
            {
                var loi = await _kyXacNhan.TuChoiAsync(ma, vao!.IdKy, vao.LyDo, NguoiThucHien());
                return Json(new { thanhCong = loi == null, thongBao = loi ?? "Đã từ chối, phiếu trả về người chấm." });
            }
            catch (Exception ex)
            {
                return Json(new { thanhCong = false, thongBao = "Từ chối thất bại: " + ex.Message });
            }
        }

        [HttpPost("/QLCongViec/DanhGia/KyXacNhanApi/KyCuaToi")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> KyCuaToi([FromBody] DanhGiaKyXacNhanService.KyVao? vao)
        {
            if (ChanAjax(LaNhanVien()) is { } chan) return chan;
            var ma = MaCuaToi();
            if (string.IsNullOrEmpty(ma)) return Json(new { thanhCong = false, thongBao = "Tài khoản chưa gắn mã nhân viên." });
            if (vao == null) return Json(new { thanhCong = false, thongBao = "Thiếu dữ liệu ký." });
            try
            {
                var loi = await _kyXacNhan.NhanVienKyAsync(ma, vao);
                return Json(new { thanhCong = loi == null, thongBao = loi ?? "Đã ký xác nhận." });
            }
            catch (Exception ex)
            {
                return Json(new { thanhCong = false, thongBao = "Ký thất bại: " + ex.Message });
            }
        }
    }
}
