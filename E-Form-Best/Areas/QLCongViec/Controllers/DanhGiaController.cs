using E_Form_Best.Areas.QLCongViec.Services;
using Microsoft.AspNetCore.Mvc;

namespace E_Form_Best.Areas.QLCongViec.Controllers
{
    /// <summary>
    /// Mục "Đánh giá" của E-WorkPortal: trang Tự đánh giá hiệu suất (chuyển từ app evaluation
    /// /employee/evaluation). Nghiệp vụ nằm ở DanhGiaService, controller chỉ kiểm quyền + định dạng JSON.
    /// Người dùng định danh bằng claim MaNv (mã NV), mọi endpoint chỉ đọc/ghi phiếu của chính mình.
    /// </summary>
    [Area("QLCongViec")]
    public class DanhGiaController : Controller
    {
        private const string ThuMucAnhMacDinh = @"\\10.0.60.30\BPVN-Fileserver\Public\IT-Information Technology Dept\5.E-Form\DanhGia";
        private const long KichThuocAnhToiDa = 5 * 1024 * 1024;
        private static readonly string[] DuoiAnhHopLe = { ".jpg", ".jpeg", ".png", ".webp" };

        private readonly DanhGiaService _danhGia;
        private readonly IConfiguration _configuration;

        public DanhGiaController(DanhGiaService danhGia, IConfiguration configuration)
        {
            _danhGia = danhGia;
            _configuration = configuration;
        }

        // Cùng điều kiện hiện menu Công việc trong _Layout QLCongViec. Ẩn menu không phải phân quyền nên
        // mọi action đều kiểm lại ở đây.
        private bool CoQuyen() => User.IsInRole("FORM_CV_VIEW") || User.IsInRole("All");

        private string? MaNv() => User.FindFirst("MaNv")?.Value?.Trim();

        private string ThuMucAnh() => _configuration["DanhGia:ThuMucAnh"] ?? ThuMucAnhMacDinh;

        // Chốt chung cho endpoint AJAX: trả JSON kèm mã lỗi thay vì Redirect vì JS đọc res.json()
        private IActionResult? ChanNeuKhongHopLe(out string maNv)
        {
            maNv = "";
            if (User?.Identity?.IsAuthenticated != true)
            {
                Response.StatusCode = StatusCodes.Status401Unauthorized;
                return Json(new { thanhCong = false, thongBao = "Phiên đăng nhập đã hết hạn. Vui lòng đăng nhập lại." });
            }
            if (!CoQuyen())
            {
                Response.StatusCode = StatusCodes.Status403Forbidden;
                return Json(new { thanhCong = false, thongBao = "Bạn không có quyền dùng chức năng đánh giá." });
            }
            var ma = MaNv();
            if (string.IsNullOrEmpty(ma))
                return Json(new { thanhCong = false, thongBao = "Tài khoản chưa có mã nhân viên, không tự đánh giá được." });
            maNv = ma;
            return null;
        }

        #region View

        [HttpGet("/QLCongViec/DanhGia/TuDanhGia")]
        public IActionResult TuDanhGia()
        {
            if (User?.Identity?.IsAuthenticated != true)
                return Redirect("/DonXetDuyet/DangNhap");
            if (!CoQuyen()) return Forbid();
            return View();
        }

        #endregion

        #region API

        /// <summary>Toàn bộ dữ liệu lần tải đầu: hồ sơ, danh mục lọc, phiếu đã lưu, KPI, năng lực theo bộ lọc của phiếu.</summary>
        [HttpGet("/QLCongViec/DanhGia/DuLieu")]
        public async Task<IActionResult> DuLieu()
        {
            if (ChanNeuKhongHopLe(out var maNv) is { } chan) return chan;
            try
            {
                var hoSo = await _danhGia.LayHoSoAsync(maNv);
                var phieu = await _danhGia.LayPhieuTuDanhGiaAsync(maNv);
                // Đã lưu phiếu thì mở lại đúng bộ lọc lúc chấm, chưa có thì theo hồ sơ
                var capBac = phieu?.CapBac ?? hoSo.CapBac;
                var idNhom = phieu != null ? phieu.IdNhomCongViec : hoSo.IdNhomCongViec;

                return Json(new
                {
                    thanhCong = true,
                    duLieu = new
                    {
                        hoTen = User.Identity!.Name,
                        maNv,
                        phongBan = User.FindFirst("PhongBan")?.Value,
                        hoSo,
                        dsCapBac = await _danhGia.DsCapBacAsync(),
                        dsNhomCongViec = (await _danhGia.DsNhomCongViecAsync()).Select(x => new { id = x.IdNhomCongViec, ten = x.TenNhom }),
                        boLoc = new { capBac, idNhomCongViec = idNhom },
                        phieu,
                        dsKpi = await _danhGia.LayKpiAsync(maNv, phieu?.IdPhieu),
                        dsNhomNangLuc = await _danhGia.LayNangLucAsync(capBac, idNhom)
                    }
                });
            }
            catch (Exception ex)
            {
                return Json(new { thanhCong = false, thongBao = "Không tải được dữ liệu đánh giá: " + ex.Message });
            }
        }

        [HttpGet("/QLCongViec/DanhGia/NangLuc")]
        public async Task<IActionResult> NangLuc(string? capBac, Guid? idNhomCongViec)
        {
            if (ChanNeuKhongHopLe(out _) is { } chan) return chan;
            if (string.IsNullOrWhiteSpace(capBac))
                return Json(new { thanhCong = false, thongBao = "Chưa chọn cấp bậc." });
            try
            {
                return Json(new { thanhCong = true, duLieu = await _danhGia.LayNangLucAsync(capBac.Trim(), idNhomCongViec) });
            }
            catch (Exception ex)
            {
                return Json(new { thanhCong = false, thongBao = "Không tải được danh sách năng lực: " + ex.Message });
            }
        }

        [HttpPost("/QLCongViec/DanhGia/Luu")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Luu([FromBody] DanhGiaService.LuuTuDanhGiaVao? vao)
        {
            if (ChanNeuKhongHopLe(out var maNv) is { } chan) return chan;
            if (vao == null) return Json(new { thanhCong = false, thongBao = "Dữ liệu gửi lên không hợp lệ." });
            try
            {
                var loi = await _danhGia.LuuTuDanhGiaAsync(maNv, vao);
                if (loi != null) return Json(new { thanhCong = false, thongBao = loi });

                return Json(new
                {
                    thanhCong = true,
                    thongBao = vao.HoanThanh ? "Đã gửi bản tự đánh giá." : "Đã lưu.",
                    duLieu = await _danhGia.LayPhieuTuDanhGiaAsync(maNv)
                });
            }
            catch (Exception ex)
            {
                return Json(new { thanhCong = false, thongBao = "Lưu đánh giá thất bại: " + ex.Message });
            }
        }

        [HttpPost("/QLCongViec/DanhGia/AnhKpi/{idKpiMau:guid}")]
        [ValidateAntiForgeryToken]
        [RequestSizeLimit(6 * 1024 * 1024)]
        public async Task<IActionResult> TaiAnhKpi(Guid idKpiMau, IFormFile? file)
        {
            if (ChanNeuKhongHopLe(out var maNv) is { } chan) return chan;
            if (file == null || file.Length == 0) return Json(new { thanhCong = false, thongBao = "Chưa chọn ảnh." });
            if (file.Length > KichThuocAnhToiDa) return Json(new { thanhCong = false, thongBao = "Ảnh vượt quá 5MB." });
            var duoi = Path.GetExtension(file.FileName).ToLowerInvariant();
            if (!DuoiAnhHopLe.Contains(duoi)) return Json(new { thanhCong = false, thongBao = "Chỉ nhận ảnh JPG, PNG, WebP." });

            try
            {
                if (await _danhGia.PhieuDaKhoaAsync(maNv))
                    return Json(new { thanhCong = false, thongBao = "Phiếu đã được quản lý đánh giá xong, không sửa được nữa." });

                var dong = await _danhGia.LayHoacTaoDongKpiAsync(maNv, idKpiMau);
                if (dong == null) return Json(new { thanhCong = false, thongBao = "KPI này không được giao cho bạn." });

                var thuMuc = ThuMucAnh();
                Directory.CreateDirectory(thuMuc);
                // Tên file do hệ thống sinh, không dùng tên người dùng gửi lên (chống path traversal)
                var tenFile = $"KpiDanhGia_{maNv}_{DateTime.Now:yyyyMMdd_HHmmss}_{Guid.NewGuid().ToString("N")[..8]}{duoi}";
                await using (var stream = new FileStream(Path.Combine(thuMuc, tenFile), FileMode.CreateNew))
                    await file.CopyToAsync(stream);

                // File cũ giữ lại trên fileserver để truy vết, chỉ đổi tham chiếu
                await _danhGia.GanAnhKpiAsync(dong, tenFile);
                return Json(new { thanhCong = true, thongBao = "Đã tải ảnh minh chứng." });
            }
            catch (Exception ex)
            {
                return Json(new { thanhCong = false, thongBao = "Tải ảnh thất bại: " + ex.Message });
            }
        }

        [HttpPost("/QLCongViec/DanhGia/XoaAnhKpi/{idKpiMau:guid}")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> XoaAnhKpi(Guid idKpiMau)
        {
            if (ChanNeuKhongHopLe(out var maNv) is { } chan) return chan;
            try
            {
                if (await _danhGia.PhieuDaKhoaAsync(maNv))
                    return Json(new { thanhCong = false, thongBao = "Phiếu đã được quản lý đánh giá xong, không sửa được nữa." });

                var dong = await _danhGia.LayDongKpiAsync(maNv, idKpiMau);
                if (dong == null) return Json(new { thanhCong = false, thongBao = "Không tìm thấy KPI trong phiếu của bạn." });

                await _danhGia.GanAnhKpiAsync(dong, null);
                return Json(new { thanhCong = true, thongBao = "Đã gỡ ảnh minh chứng." });
            }
            catch (Exception ex)
            {
                return Json(new { thanhCong = false, thongBao = "Gỡ ảnh thất bại: " + ex.Message });
            }
        }

        /// <summary>
        /// Xem ảnh minh chứng KPI (điều hướng thật được phép: tải file). Mặc định ảnh của chính mình;
        /// tham số maNv chỉ có hiệu lực với người có quyền quản lý đánh giá (chống IDOR).
        /// </summary>
        [HttpGet("/QLCongViec/DanhGia/AnhKpi/{idKpiMau:guid}")]
        public async Task<IActionResult> XemAnhKpi(Guid idKpiMau, string? maNv)
        {
            if (User?.Identity?.IsAuthenticated != true) return Unauthorized();
            bool laQuanLy = User.IsInRole("DanhGiaQuanLy") || User.IsInRole("All");
            if (!CoQuyen() && !laQuanLy) return Forbid();
            if (string.IsNullOrWhiteSpace(maNv)) maNv = MaNv();
            else if (!laQuanLy) return Forbid();
            else maNv = maNv.Trim();
            if (string.IsNullOrEmpty(maNv)) return NotFound();

            var dong = await _danhGia.LayDongKpiAsync(maNv, idKpiMau);
            var tenFile = dong?.AnhMinhChung;
            // Tên file trong DB luôn là tên trần do hệ thống sinh; vẫn chặn ký tự phân tách cho chắc
            if (string.IsNullOrEmpty(tenFile) || Path.GetFileName(tenFile) != tenFile) return NotFound();

            var duongDan = Path.Combine(ThuMucAnh(), tenFile);
            if (!System.IO.File.Exists(duongDan)) return NotFound();

            var loai = Path.GetExtension(tenFile).ToLowerInvariant() switch
            {
                ".png" => "image/png",
                ".webp" => "image/webp",
                _ => "image/jpeg"
            };
            return PhysicalFile(duongDan, loai);
        }

        #endregion
    }
}
