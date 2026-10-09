using E_Form_Best.Areas.ITForm.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;

namespace E_Form_Best.Areas.ITForm.Controllers
{
    /// <summary>
    /// "Báo cáo định kì": một trang gom các báo cáo thành tab — Tổng quan Camera / AP / Switch, Tổng quan thiết bị,
    /// Thống kê đơn IT, Thống kê công việc. Trang chỉ là khung; nội dung từng tab lấy từ trang/API sẵn có của module
    /// nên quyền xem số liệu vẫn do controller của từng module kiểm. Nút "Xuất báo cáo" gộp tất cả vào một file Excel.
    /// </summary>
    [Area("ITform")]
    public class BaoCaoDinhKyController : Controller
    {
        // Phải khớp data-loai của các tab trong Views/BaoCaoDinhKy/Index.cshtml
        private static readonly string[] DsLoai = { "camera", "ap", "switch", "thiet-bi", "don-it", "cong-viec" };

        private bool CoQuyen() => User.IsInRole("AdminIT") || User.IsInRole("All");

        [HttpGet("/BaoCaoDinhKy")]
        public IActionResult Index(string? loai, string? congTy)
        {
            if (User?.Identity?.IsAuthenticated != true)
                return Redirect("/DonXetDuyet/DangNhap");
            if (!CoQuyen())
                return Forbid();

            ViewBag.Loai = DsLoai.FirstOrDefault(x => x.Equals(loai?.Trim(), StringComparison.OrdinalIgnoreCase)) ?? "camera";
            ViewBag.CongTy = congTy?.Trim().ToUpperInvariant() ?? "";
            return View();
        }

        // Cùng quy tắc kỳ với các trang Tổng quan: mặc định 7 ngày, tối đa 366 ngày
        private static (DateTime tu, DateTime den) KyBaoCao(DateTime? tuNgay, DateTime? denNgay)
        {
            var den = (denNgay ?? DateTime.Today).Date;
            var tu = (tuNgay ?? den.AddDays(-6)).Date;
            if (tu > den) (tu, den) = (den, tu);
            if ((den - tu).TotalDays > 366) tu = den.AddDays(-366);
            return (tu, den);
        }

        /// <summary>
        /// Khối "So với kỳ trước" của 3 tab Thiết bị / Đơn IT / Công việc (trang thống kê cũ nhúng iframe không có phần này).
        /// loai: thiet-bi | don-it | cong-viec.
        /// </summary>
        [HttpGet("/BaoCaoDinhKy/SoSanh")]
        public async Task<IActionResult> SoSanh(string? loai, DateTime? tuNgay, DateTime? denNgay, [FromServices] BaoCaoDinhKyService baoCao)
        {
            if (User?.Identity?.IsAuthenticated != true || !CoQuyen())
                return Json(new { thanhCong = false, thongBao = "Bạn không có quyền xem báo cáo này." });

            var (tu, den) = KyBaoCao(tuNgay, denNgay);
            var ct = HttpContext.RequestAborted;
            try
            {
                object? duLieu = loai switch
                {
                    "thiet-bi" => await SoSanhThietBiAsync(baoCao, tu, den, ct),
                    "don-it" => await baoCao.SoSanhDonItAsync(tu, den, ct),
                    "cong-viec" => await baoCao.SoSanhDonCongViecAsync(tu, den, ct),
                    _ => null
                };
                if (duLieu == null) return Json(new { thanhCong = false, thongBao = "Loại báo cáo không hợp lệ." });
                return Json(new { thanhCong = true, duLieu });
            }
            catch (Exception ex) when (ex is SqlException or InvalidOperationException)
            {
                return Json(new { thanhCong = false, thongBao = "Không tạo được số liệu so sánh: " + ex.Message });
            }
        }

        // Biến động (thêm/xoá/kiểm kê) + bản quyền/Office theo bản chụp. Phần bản chụp lỗi (vd chưa chạy script tạo
        // KK_ThietBiChotNgay) thì chỉ phần đó báo lỗi, biến động vẫn hiện.
        private static async Task<object> SoSanhThietBiAsync(BaoCaoDinhKyService baoCao, DateTime tu, DateTime den, CancellationToken ct)
        {
            var bd = await baoCao.SoSanhThietBiAsync(tu, den, ct);
            BaoCaoDinhKyService.SoSanhChot? banQuyen = null;
            string? banQuyenLoi = null;
            try { banQuyen = await baoCao.SoSanhChotAsync(tu, den, ct); }
            catch (SqlException ex) { banQuyenLoi = "Không đọc được bản chụp số liệu thiết bị: " + ex.Message; }
            return new { bd.TuNgay, bd.DenNgay, bd.KyTruocTu, bd.KyTruocDen, bd.Nay, bd.Truoc, banQuyen, banQuyenLoi };
        }

        /// <summary>
        /// Một file Excel gồm toàn bộ các mục của trang: kỳ áp cho Camera/AP/Switch và 2 loại đơn,
        /// phạm vi công ty áp cho Camera/AP/Switch (thiết bị và đơn không chia theo 3 mã công ty này).
        /// </summary>
        [HttpGet("/BaoCaoDinhKy/XuatExcel")]
        public async Task<IActionResult> XuatExcel(DateTime? tuNgay, DateTime? denNgay, string? congTy,
            [FromServices] CameraBaoCaoService camera, [FromServices] AccessPointBaoCaoService ap,
            [FromServices] SwitchBaoCaoService sw, [FromServices] BaoCaoDinhKyService baoCao)
        {
            if (User?.Identity?.IsAuthenticated != true) return StatusCode(401);
            if (!CoQuyen()) return StatusCode(403);

            string? motCongTy = null;
            if (!string.IsNullOrWhiteSpace(congTy))
            {
                motCongTy = CameraBaoCaoService.DsCongTy.FirstOrDefault(x => x.Equals(congTy.Trim(), StringComparison.OrdinalIgnoreCase));
                if (motCongTy == null) return NotFound();
            }
            var dsCongTy = motCongTy != null ? new List<string> { motCongTy } : CameraBaoCaoService.DsCongTy.ToList();

            var (tu, den) = KyBaoCao(tuNgay, denNgay);

            var ct = HttpContext.RequestAborted;
            var nguoiLap = User.Identity?.Name;
            try
            {
                // Chạy tuần tự: các service dùng chung một ITFormContext (scoped), không gọi song song được
                var bcCamera = await camera.TaoAsync(dsCongTy, tu, den, ct);
                var bcAp = await ap.TaoAsync(dsCongTy, tu, den, ct);
                var bcSwitch = await sw.TaoAsync(dsCongTy, tu, den, ct);
                var thietBi = await baoCao.ThietBiAsync(ct);
                var bienDongTb = await baoCao.SoSanhThietBiAsync(tu, den, ct);
                BaoCaoDinhKyService.SoSanhChot? banQuyen = null;
                try { banQuyen = await baoCao.SoSanhChotAsync(tu, den, ct); }
                catch (SqlException) { /* chưa có bảng chụp: file vẫn xuất, mục bản quyền ghi "chưa có dữ liệu" */ }
                var donIt = await baoCao.SoSanhDonItAsync(tu, den, ct);
                var donCv = await baoCao.SoSanhDonCongViecAsync(tu, den, ct);

                var file = BaoCaoDinhKyExcel.Tao(new BaoCaoDinhKyExcel.DauVao(
                    tu, den, motCongTy, nguoiLap,
                    bcCamera, CameraBaoCaoExcel.Tao(bcCamera, motCongTy, nguoiLap),
                    bcAp, AccessPointBaoCaoExcel.TaoBaoCao(bcAp, motCongTy, nguoiLap),
                    bcSwitch, SwitchBaoCaoExcel.TaoBaoCao(bcSwitch, motCongTy, nguoiLap),
                    thietBi, bienDongTb, banQuyen, donIt, donCv));

                return File(file, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                    $"BaoCaoDinhKy_{motCongTy ?? "TatCa"}_{tu:yyyyMMdd}-{den:yyyyMMdd}.xlsx");
            }
            catch (Exception ex) when (ex is SqlException or InvalidOperationException)
            {
                return Content("Không tạo được báo cáo: " + ex.Message);
            }
        }
    }
}
