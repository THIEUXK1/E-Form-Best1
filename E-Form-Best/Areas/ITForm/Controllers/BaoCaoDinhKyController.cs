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

            // Cùng quy tắc kỳ với các trang Tổng quan: mặc định 7 ngày, tối đa 366 ngày
            var den = (denNgay ?? DateTime.Today).Date;
            var tu = (tuNgay ?? den.AddDays(-6)).Date;
            if (tu > den) (tu, den) = (den, tu);
            if ((den - tu).TotalDays > 366) tu = den.AddDays(-366);

            var ct = HttpContext.RequestAborted;
            var nguoiLap = User.Identity?.Name;
            try
            {
                // Chạy tuần tự: các service dùng chung một ITFormContext (scoped), không gọi song song được
                var bcCamera = await camera.TaoAsync(dsCongTy, tu, den, ct);
                var bcAp = await ap.TaoAsync(dsCongTy, tu, den, ct);
                var bcSwitch = await sw.TaoAsync(dsCongTy, tu, den, ct);
                var thietBi = await baoCao.ThietBiAsync(ct);
                var donIt = await baoCao.DonItAsync(tu, den, ct);
                var donCv = await baoCao.DonCongViecAsync(tu, den, ct);

                var file = BaoCaoDinhKyExcel.Tao(new BaoCaoDinhKyExcel.DauVao(
                    tu, den, motCongTy, nguoiLap,
                    bcCamera, CameraBaoCaoExcel.Tao(bcCamera, motCongTy, nguoiLap),
                    bcAp, AccessPointBaoCaoExcel.TaoBaoCao(bcAp, motCongTy, nguoiLap),
                    bcSwitch, SwitchBaoCaoExcel.TaoBaoCao(bcSwitch, motCongTy, nguoiLap),
                    thietBi, donIt, donCv));

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
