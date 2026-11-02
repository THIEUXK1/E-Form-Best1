using E_Form_Best.Areas.QLCongViec.Services;
using Microsoft.AspNetCore.Mvc;

namespace E_Form_Best.Areas.QLCongViec.Controllers
{
    /// <summary>
    /// Mục "Đánh giá" phía quản lý: Quản lý nhân viên (/QLCongViec/DanhGia/NhanVien) và
    /// Đánh giá nhân viên (/QLCongViec/DanhGia/DanhGiaNhanVien). Quyền: 'DanhGiaQuanLy' hoặc 'All'.
    /// Quản lý không tự chấm phiếu quản lý cho chính mình.
    /// </summary>
    [Area("QLCongViec")]
    public class DanhGiaQuanLyController : Controller
    {
        private readonly DanhGiaQuanLyService _quanLy;

        public DanhGiaQuanLyController(DanhGiaQuanLyService quanLy)
        {
            _quanLy = quanLy;
        }

        private bool CoQuyen() => User.IsInRole("DanhGiaQuanLy") || User.IsInRole("All");

        private string MaQuanLy() => User.FindFirst("MaNv")?.Value?.Trim() ?? User.Identity?.Name ?? "";

        private IActionResult? ChanNeuKhongHopLe()
        {
            if (User?.Identity?.IsAuthenticated != true)
            {
                Response.StatusCode = StatusCodes.Status401Unauthorized;
                return Json(new { thanhCong = false, thongBao = "Phiên đăng nhập đã hết hạn. Vui lòng đăng nhập lại." });
            }
            if (!CoQuyen())
            {
                Response.StatusCode = StatusCodes.Status403Forbidden;
                return Json(new { thanhCong = false, thongBao = "Bạn không có quyền quản lý đánh giá." });
            }
            return null;
        }

        private static string? ChuanMa(string? maNv)
        {
            var ma = maNv?.Trim();
            return string.IsNullOrEmpty(ma) || ma.Length > 20 ? null : ma;
        }

        // Phiếu/hồ sơ chỉ thao tác trên người đang trong hệ thống đánh giá — chống gọi thẳng API với mã tuỳ ý
        private async Task<IActionResult?> ChanNeuKhongThuocDanhSach(string? ma)
        {
            if (ma == null) return Json(new { thanhCong = false, thongBao = "Thiếu mã nhân viên." });
            if (!await _quanLy.TrongHeThongAsync(ma))
                return Json(new { thanhCong = false, thongBao = $"{ma} không có trong danh sách đánh giá." });
            return null;
        }

        #region View

        [HttpGet("/QLCongViec/DanhGia/NhanVien")]
        public IActionResult NhanVien()
        {
            if (User?.Identity?.IsAuthenticated != true) return Redirect("/DonXetDuyet/DangNhap");
            if (!CoQuyen()) return Forbid();
            return View();
        }

        [HttpGet("/QLCongViec/DanhGia/DanhGiaNhanVien")]
        public IActionResult DanhGiaNhanVien()
        {
            if (User?.Identity?.IsAuthenticated != true) return Redirect("/DonXetDuyet/DangNhap");
            if (!CoQuyen()) return Forbid();
            return View();
        }

        /// <summary>Thống kê đánh giá: dữ liệu lấy AJAX từ QuanLy/DanhSach, gom nhóm/vẽ biểu đồ ở client.</summary>
        [HttpGet("/QLCongViec/DanhGia/ThongKe")]
        public IActionResult ThongKe()
        {
            if (User?.Identity?.IsAuthenticated != true) return Redirect("/DonXetDuyet/DangNhap");
            if (!CoQuyen()) return Forbid();
            return View();
        }

        /// <summary>Nhân viên chưa hoàn thành đánh giá, chia theo bước còn thiếu. Dữ liệu AJAX từ QuanLy/DanhSach.</summary>
        [HttpGet("/QLCongViec/DanhGia/ChuaHoanThanh")]
        public IActionResult ChuaHoanThanh()
        {
            if (User?.Identity?.IsAuthenticated != true) return Redirect("/DonXetDuyet/DangNhap");
            if (!CoQuyen()) return Forbid();
            return View();
        }

        /// <summary>Chi tiết đánh giá 1 nhân viên (bookmark được). Dữ liệu tải AJAX từ DuLieuDanhGia.</summary>
        [HttpGet("/QLCongViec/DanhGia/ChiTiet/{maNv}")]
        public IActionResult ChiTiet(string maNv)
        {
            if (User?.Identity?.IsAuthenticated != true) return Redirect("/DonXetDuyet/DangNhap");
            if (!CoQuyen()) return Forbid();
            var ma = ChuanMa(maNv);
            if (ma == null) return NotFound();
            ViewBag.MaNv = ma;
            return View();
        }

        #endregion

        #region Quản lý nhân viên

        [HttpGet("/QLCongViec/DanhGia/QuanLy/DanhSach")]
        public async Task<IActionResult> DanhSach()
        {
            if (ChanNeuKhongHopLe() is { } chan) return chan;
            try
            {
                return Json(new { thanhCong = true, duLieu = await _quanLy.DanhSachAsync() });
            }
            catch (Exception ex)
            {
                return Json(new { thanhCong = false, thongBao = "Không tải được danh sách nhân viên: " + ex.Message });
            }
        }

        public class MaNvVao { public string? MaNv { get; set; } }

        [HttpPost("/QLCongViec/DanhGia/QuanLy/Them")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Them([FromBody] MaNvVao? vao)
        {
            if (ChanNeuKhongHopLe() is { } chan) return chan;
            var ma = ChuanMa(vao?.MaNv)?.ToUpperInvariant();
            if (ma == null) return Json(new { thanhCong = false, thongBao = "Nhập mã nhân viên (tối đa 20 ký tự)." });
            try
            {
                var loi = await _quanLy.ThemNhanVienAsync(ma, MaQuanLy());
                return loi != null
                    ? Json(new { thanhCong = false, thongBao = loi })
                    : Json(new { thanhCong = true, thongBao = $"Đã thêm {ma} vào danh sách đánh giá.", duLieu = new { maNv = ma } });
            }
            catch (Exception ex)
            {
                return Json(new { thanhCong = false, thongBao = "Thêm nhân viên thất bại: " + ex.Message });
            }
        }

        [HttpPost("/QLCongViec/DanhGia/QuanLy/Ngung")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Ngung([FromBody] MaNvVao? vao)
        {
            if (ChanNeuKhongHopLe() is { } chan) return chan;
            var ma = ChuanMa(vao?.MaNv);
            if (await ChanNeuKhongThuocDanhSach(ma) is { } chan2) return chan2;
            try
            {
                await _quanLy.NgungDanhGiaAsync(ma!, MaQuanLy());
                return Json(new { thanhCong = true, thongBao = $"Đã ngừng đánh giá {ma}. Phiếu cũ vẫn được giữ lại." });
            }
            catch (Exception ex)
            {
                return Json(new { thanhCong = false, thongBao = "Thao tác thất bại: " + ex.Message });
            }
        }

        [HttpGet("/QLCongViec/DanhGia/QuanLy/HoSo")]
        public async Task<IActionResult> HoSo(string? maNv)
        {
            if (ChanNeuKhongHopLe() is { } chan) return chan;
            var ma = ChuanMa(maNv);
            if (await ChanNeuKhongThuocDanhSach(ma) is { } chan2) return chan2;
            try
            {
                return Json(new { thanhCong = true, duLieu = await _quanLy.LayHoSoSuaAsync(ma!) });
            }
            catch (Exception ex)
            {
                return Json(new { thanhCong = false, thongBao = "Không tải được hồ sơ: " + ex.Message });
            }
        }

        [HttpPost("/QLCongViec/DanhGia/QuanLy/LuuHoSo")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> LuuHoSo([FromBody] DanhGiaQuanLyService.LuuHoSoVao? vao)
        {
            if (ChanNeuKhongHopLe() is { } chan) return chan;
            var ma = ChuanMa(vao?.MaNv);
            if (await ChanNeuKhongThuocDanhSach(ma) is { } chan2) return chan2;
            try
            {
                var loi = await _quanLy.LuuHoSoAsync(vao!, MaQuanLy());
                return loi != null
                    ? Json(new { thanhCong = false, thongBao = loi })
                    : Json(new { thanhCong = true, thongBao = "Đã lưu hồ sơ, KPI giao và tiêu chí năng lực." });
            }
            catch (Exception ex)
            {
                return Json(new { thanhCong = false, thongBao = "Lưu hồ sơ thất bại: " + ex.Message });
            }
        }

        public class ThemCapBacVao { public string? CapBac { get; set; } public string? SaoChepTu { get; set; } }

        [HttpPost("/QLCongViec/DanhGia/QuanLy/ThemCapBac")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ThemCapBac([FromBody] ThemCapBacVao? vao)
        {
            if (ChanNeuKhongHopLe() is { } chan) return chan;
            try
            {
                var (loi, daCo) = await _quanLy.ThemCapBacAsync(vao?.CapBac, vao?.SaoChepTu);
                if (loi != null) return Json(new { thanhCong = false, thongBao = loi });
                var ma = vao!.CapBac!.Trim().ToUpperInvariant();
                return Json(new
                {
                    thanhCong = true,
                    thongBao = daCo ? $"Cấp bậc {ma} đã có sẵn." : $"Đã thêm cấp bậc {ma} (điểm yêu cầu sao chép từ {vao.SaoChepTu?.Trim()}).",
                    duLieu = new { capBac = ma }
                });
            }
            catch (Exception ex)
            {
                return Json(new { thanhCong = false, thongBao = "Thêm cấp bậc thất bại: " + ex.Message });
            }
        }

        /// <summary>Tải file Excel phiếu đánh giá (điều hướng thật được phép: tải file).</summary>
        [HttpGet("/QLCongViec/DanhGia/QuanLy/Excel")]
        public async Task<IActionResult> Excel(string? maNv)
        {
            if (User?.Identity?.IsAuthenticated != true) return Redirect("/DonXetDuyet/DangNhap");
            if (!CoQuyen()) return Forbid();
            var ma = ChuanMa(maNv);
            if (ma == null || !await _quanLy.TrongHeThongAsync(ma)) return NotFound();
            var file = await _quanLy.XuatExcelAsync(ma);
            return File(file, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                $"DanhGia_{ma}_{DateTime.Now:yyyyMMdd}.xlsx");
        }

        /// <summary>Cả danh sách đánh giá trong 1 file, mỗi nhân viên 1 sheet như file mẫu Excel 2025.</summary>
        [HttpGet("/QLCongViec/DanhGia/QuanLy/ExcelTatCa")]
        public async Task<IActionResult> ExcelTatCa()
        {
            if (User?.Identity?.IsAuthenticated != true) return Redirect("/DonXetDuyet/DangNhap");
            if (!CoQuyen()) return Forbid();
            var file = await _quanLy.XuatExcelTatCaAsync();
            return File(file, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                $"DanhGia_TatCa_{DateTime.Now:yyyyMMdd}.xlsx");
        }

        #endregion

        #region Đánh giá nhân viên

        [HttpGet("/QLCongViec/DanhGia/QuanLy/DuLieuDanhGia")]
        public async Task<IActionResult> DuLieuDanhGia(string? maNv)
        {
            if (ChanNeuKhongHopLe() is { } chan) return chan;
            var ma = ChuanMa(maNv);
            if (await ChanNeuKhongThuocDanhSach(ma) is { } chan2) return chan2;
            try
            {
                return Json(new
                {
                    thanhCong = true,
                    duLieu = await _quanLy.DuLieuDanhGiaAsync(ma!),
                    laChinhMinh = string.Equals(ma, MaQuanLy(), StringComparison.OrdinalIgnoreCase)
                });
            }
            catch (Exception ex)
            {
                return Json(new { thanhCong = false, thongBao = "Không tải được dữ liệu đánh giá: " + ex.Message });
            }
        }

        [HttpPost("/QLCongViec/DanhGia/QuanLy/LuuDanhGia")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> LuuDanhGia([FromBody] DanhGiaQuanLyService.LuuDanhGiaVao? vao)
        {
            if (ChanNeuKhongHopLe() is { } chan) return chan;
            var ma = ChuanMa(vao?.MaNv);
            if (await ChanNeuKhongThuocDanhSach(ma) is { } chan2) return chan2;
            if (string.Equals(ma, MaQuanLy(), StringComparison.OrdinalIgnoreCase))
                return Json(new { thanhCong = false, thongBao = "Không tự chấm phiếu quản lý cho chính mình." });
            try
            {
                var loi = await _quanLy.LuuDanhGiaAsync(vao!, MaQuanLy());
                if (loi != null) return Json(new { thanhCong = false, thongBao = loi });
                return Json(new
                {
                    thanhCong = true,
                    thongBao = vao!.HoanThanh ? "Đã gửi đánh giá của quản lý." : "Đã lưu.",
                    duLieu = await _quanLy.DuLieuDanhGiaAsync(ma!)
                });
            }
            catch (Exception ex)
            {
                return Json(new { thanhCong = false, thongBao = "Lưu đánh giá thất bại: " + ex.Message });
            }
        }

        [HttpPost("/QLCongViec/DanhGia/QuanLy/MoLai")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> MoLai([FromBody] MaNvVao? vao)
        {
            if (ChanNeuKhongHopLe() is { } chan) return chan;
            var ma = ChuanMa(vao?.MaNv);
            if (await ChanNeuKhongThuocDanhSach(ma) is { } chan2) return chan2;
            try
            {
                await _quanLy.MoLaiAsync(ma!);
                return Json(new { thanhCong = true, thongBao = "Đã mở lại phiếu, nhân viên sửa được tự đánh giá.", duLieu = await _quanLy.DuLieuDanhGiaAsync(ma!) });
            }
            catch (Exception ex)
            {
                return Json(new { thanhCong = false, thongBao = "Mở lại thất bại: " + ex.Message });
            }
        }

        #endregion
    }
}
