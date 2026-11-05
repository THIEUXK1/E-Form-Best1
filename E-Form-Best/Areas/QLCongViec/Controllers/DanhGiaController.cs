using E_Form_Best.Areas.QLCongViec.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.StaticFiles;

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
        private const string ThuMucMinhChungMacDinh = @"\\10.0.60.30\BPVN-Fileserver\Public\IT-Information Technology Dept\5.E-Form\KPI";
        // Thư mục cũ (trước 16/10/2026 chỉ nhận ảnh) - giữ để vẫn mở được minh chứng đã tải trước đó
        private const string ThuMucAnhCu = @"\\10.0.60.30\BPVN-Fileserver\Public\IT-Information Technology Dept\5.E-Form\DanhGia";
        private const long KichThuocFileToiDa = 15 * 1024 * 1024;
        private static readonly string[] DuoiFileHopLe =
        {
            ".jpg", ".jpeg", ".png", ".webp", ".gif", ".bmp",
            ".pdf", ".doc", ".docx", ".xls", ".xlsx", ".ppt", ".pptx", ".txt", ".csv", ".zip", ".rar", ".7z"
        };

        private readonly DanhGiaService _danhGia;
        private readonly DanhGiaSoLieuService _soLieu;
        private readonly IConfiguration _configuration;

        public DanhGiaController(DanhGiaService danhGia, DanhGiaSoLieuService soLieu, IConfiguration configuration)
        {
            _danhGia = danhGia;
            _soLieu = soLieu;
            _configuration = configuration;
        }

        // Cùng điều kiện hiện menu Công việc trong _Layout QLCongViec. Ẩn menu không phải phân quyền nên
        // mọi action đều kiểm lại ở đây.
        private bool CoQuyen() => User.IsInRole("FORM_CV_VIEW") || User.IsInRole("All");

        private string? MaNv() => User.FindFirst("MaNv")?.Value?.Trim();

        private string ThuMucMinhChung() => _configuration["DanhGia:ThuMucMinhChung"] ?? ThuMucMinhChungMacDinh;

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

        /// <summary>Danh sách kỳ cho ô chọn kỳ dùng chung (ky-danh-gia.js), kể cả người chỉ có quyền quản lý.</summary>
        [HttpGet("/QLCongViec/DanhGia/DsKy")]
        public async Task<IActionResult> DsKy()
        {
            if (User?.Identity?.IsAuthenticated != true)
            {
                Response.StatusCode = StatusCodes.Status401Unauthorized;
                return Json(new { thanhCong = false, thongBao = "Phiên đăng nhập đã hết hạn. Vui lòng đăng nhập lại." });
            }
            if (!CoQuyen() && !User.IsInRole("DanhGiaQuanLy") && !User.IsInRole("DanhGiaCapTren"))
            {
                Response.StatusCode = StatusCodes.Status403Forbidden;
                return Json(new { thanhCong = false, thongBao = "Bạn không có quyền dùng chức năng đánh giá." });
            }
            try
            {
                return Json(new { thanhCong = true, duLieu = await _danhGia.DsKyAsync() });
            }
            catch (Exception ex)
            {
                return Json(new { thanhCong = false, thongBao = "Không tải được danh sách kỳ: " + ex.Message });
            }
        }

        /// <summary>Toàn bộ dữ liệu của 1 kỳ: đầu phiếu, phiếu đã lưu (gồm Part 3), KPI Part 1, tiêu chí Part 2.</summary>
        [HttpGet("/QLCongViec/DanhGia/DuLieu")]
        public async Task<IActionResult> DuLieu(Guid? idKy)
        {
            if (ChanNeuKhongHopLe(out var maNv) is { } chan) return chan;
            try
            {
                var ky = await _danhGia.LayKyAsync(idKy);
                if (ky == null) return Json(new { thanhCong = false, thongBao = "Chưa có kỳ đánh giá nào." });
                var phieu = await _danhGia.LayPhieuTuDanhGiaAsync(maNv, ky.IdKy);
                var dsPhieuKyDong = ky.DangMo ? null : (phieu == null ? Array.Empty<Guid>() : new[] { phieu.IdPhieu });
                return Json(new
                {
                    thanhCong = true,
                    duLieu = new
                    {
                        ky,
                        thongTin = await _danhGia.LayThongTinAsync(maNv),
                        hoSo = await _danhGia.LayHoSoAsync(maNv),
                        phieu,
                        dsKpi = await _danhGia.LayKpiAsync(maNv, phieu?.IdPhieu, ky.DangMo),
                        dsTieuChi = await _danhGia.LayTieuChiAsync(maNv, phieu?.IdPhieu, dsPhieuKyDong)
                    }
                });
            }
            catch (Exception ex)
            {
                return Json(new { thanhCong = false, thongBao = "Không tải được dữ liệu đánh giá: " + ex.Message });
            }
        }

        /// <summary>Số liệu tham khảo của chính mình; khoảng ngày mặc định theo năm của kỳ.</summary>
        [HttpGet("/QLCongViec/DanhGia/SoLieuCuaToi")]
        public async Task<IActionResult> SoLieuCuaToi(Guid? idKy, DateOnly? tuNgay, DateOnly? denNgay)
        {
            if (ChanNeuKhongHopLe(out var maNv) is { } chan) return chan;
            try
            {
                var ky = await _danhGia.LayKyAsync(idKy);
                if (ky == null) return Json(new { thanhCong = false, thongBao = "Chưa có kỳ đánh giá nào." });
                var (tu, den) = DanhGiaSoLieuService.KhoangMacDinh(ky);
                tu = tuNgay ?? tu;
                den = denNgay ?? den;
                if (den < tu) return Json(new { thanhCong = false, thongBao = "Đến ngày phải sau từ ngày." });
                return Json(new { thanhCong = true, duLieu = await _soLieu.LayAsync(maNv, tu, den) });
            }
            catch (Exception ex)
            {
                return Json(new { thanhCong = false, thongBao = "Không tải được số liệu: " + ex.Message });
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
                    duLieu = await _danhGia.LayPhieuTuDanhGiaAsync(maNv, vao.IdKy)
                });
            }
            catch (Exception ex)
            {
                return Json(new { thanhCong = false, thongBao = "Lưu đánh giá thất bại: " + ex.Message });
            }
        }

        [HttpPost("/QLCongViec/DanhGia/AnhKpi/{idKpiMau:guid}")]
        [ValidateAntiForgeryToken]
        [RequestSizeLimit(16 * 1024 * 1024)]
        [RequestFormLimits(MultipartBodyLengthLimit = 16 * 1024 * 1024)]
        public async Task<IActionResult> TaiAnhKpi(Guid idKpiMau, IFormFile? file)
        {
            if (ChanNeuKhongHopLe(out var maNv) is { } chan) return chan;
            if (file == null || file.Length == 0) return Json(new { thanhCong = false, thongBao = "Chưa chọn file." });
            if (file.Length > KichThuocFileToiDa) return Json(new { thanhCong = false, thongBao = "File vượt quá 15MB." });
            var duoi = Path.GetExtension(file.FileName).ToLowerInvariant();
            if (!DuoiFileHopLe.Contains(duoi))
                return Json(new { thanhCong = false, thongBao = "Chỉ nhận ảnh hoặc tài liệu (PDF, Word, Excel, PowerPoint, TXT, CSV, ZIP/RAR/7Z)." });

            try
            {
                var idKy = await _danhGia.IdKyDangMoAsync();
                if (idKy == null) return Json(new { thanhCong = false, thongBao = DanhGiaService.LoiKyDong });
                if (await _danhGia.PhieuDaKhoaAsync(maNv, idKy.Value))
                    return Json(new { thanhCong = false, thongBao = "Phiếu đã được quản lý đánh giá xong, không sửa được nữa." });

                var dong = await _danhGia.LayHoacTaoDongKpiAsync(maNv, idKy.Value, idKpiMau);
                if (dong == null) return Json(new { thanhCong = false, thongBao = "KPI này không được giao cho bạn." });

                var thuMuc = ThuMucMinhChung();
                Directory.CreateDirectory(thuMuc);
                // Tên file do hệ thống sinh, không dùng tên người dùng gửi lên (chống path traversal)
                var tenFile = $"KpiDanhGia_{maNv}_{DateTime.Now:yyyyMMdd_HHmmss}_{Guid.NewGuid().ToString("N")[..8]}{duoi}";
                await using (var stream = new FileStream(Path.Combine(thuMuc, tenFile), FileMode.CreateNew))
                    await file.CopyToAsync(stream);

                // File cũ giữ lại trên fileserver để truy vết, chỉ đổi tham chiếu
                await _danhGia.GanAnhKpiAsync(dong, tenFile);
                return Json(new { thanhCong = true, thongBao = "Đã tải minh chứng.", duLieu = new { laAnh = DanhGiaService.LaFileAnh(tenFile) } });
            }
            catch (Exception ex)
            {
                return Json(new { thanhCong = false, thongBao = "Tải minh chứng thất bại: " + ex.Message });
            }
        }

        [HttpPost("/QLCongViec/DanhGia/XoaAnhKpi/{idKpiMau:guid}")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> XoaAnhKpi(Guid idKpiMau)
        {
            if (ChanNeuKhongHopLe(out var maNv) is { } chan) return chan;
            try
            {
                var idKy = await _danhGia.IdKyDangMoAsync();
                if (idKy == null) return Json(new { thanhCong = false, thongBao = DanhGiaService.LoiKyDong });
                if (await _danhGia.PhieuDaKhoaAsync(maNv, idKy.Value))
                    return Json(new { thanhCong = false, thongBao = "Phiếu đã được quản lý đánh giá xong, không sửa được nữa." });

                var dong = await _danhGia.LayDongKpiAsync(maNv, idKy.Value, idKpiMau);
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
        public async Task<IActionResult> XemAnhKpi(Guid idKpiMau, string? maNv, Guid? idKy)
        {
            if (User?.Identity?.IsAuthenticated != true) return Unauthorized();
            bool laQuanLy = User.IsInRole("DanhGiaQuanLy") || User.IsInRole("DanhGiaCapTren") || User.IsInRole("All");
            if (!CoQuyen() && !laQuanLy) return Forbid();
            if (string.IsNullOrWhiteSpace(maNv)) maNv = MaNv();
            else if (!laQuanLy) return Forbid();
            else maNv = maNv.Trim();
            if (string.IsNullOrEmpty(maNv)) return NotFound();

            // Cùng một KPI có ở nhiều kỳ nên phải biết đang xem kỳ nào; ảnh kỳ này không được hiện sang kỳ khác
            var ky = await _danhGia.LayKyAsync(idKy);
            if (ky == null) return NotFound();
            var dong = await _danhGia.LayDongKpiAsync(maNv, ky.IdKy, idKpiMau);
            var tenFile = dong?.AnhMinhChung;
            // Tên file trong DB luôn là tên trần do hệ thống sinh; vẫn chặn ký tự phân tách cho chắc
            if (string.IsNullOrEmpty(tenFile) || Path.GetFileName(tenFile) != tenFile) return NotFound();

            var duongDan = Path.Combine(ThuMucMinhChung(), tenFile);
            if (!System.IO.File.Exists(duongDan)) duongDan = Path.Combine(ThuMucAnhCu, tenFile);
            if (!System.IO.File.Exists(duongDan)) return NotFound();

            if (!new FileExtensionContentTypeProvider().TryGetContentType(tenFile, out var loai))
                loai = "application/octet-stream";
            // Ảnh, PDF mở thẳng trên trình duyệt; Word/Excel/nén... trả kèm tên file để tải về
            bool moThang = DanhGiaService.LaFileAnh(tenFile) || tenFile.EndsWith(".pdf", StringComparison.OrdinalIgnoreCase);
            return moThang ? PhysicalFile(duongDan, loai) : PhysicalFile(duongDan, loai, tenFile);
        }

        #endregion
    }
}
