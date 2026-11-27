using System.Globalization;
using E_Form_Best.Context;
using E_Form_Best.Models.ITForm;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace E_Form_Best.Areas.NhaAn.Controllers
{
    /// <summary>
    /// Phân hệ Nhà ăn (/NhaAn): trang thông báo thực đơn — hôm nay và cả tuần có món gì.
    /// Không có đăng ký suất ăn. Mọi người đã đăng nhập chỉ xem; quyền 'NhaAnQuanLy' (hoặc 'All') sửa thực đơn.
    /// Danh sách bữa (mã, tên, giờ ăn) lấy từ appsettings NhaAn:Ca để đổi không phải sửa code.
    /// </summary>
    [Area("NhaAn")]
    public class NhaAnController : Controller
    {
        private readonly ITFormContext _context;
        private readonly IConfiguration _config;

        public NhaAnController(ITFormContext context, IConfiguration config)
        {
            _context = context;
            _config = config;
        }

        public record CaAn(string Ma, string Ten, string GioAn);
        public record ThucDonVao(string? Ngay, string? Ca, string? MonAn, string? GhiChu);
        public record SaoChepTuanVao(string? TuNgay);

        private bool CoQuyenQuanLy() => User.IsInRole("NhaAnQuanLy") || User.IsInRole("All");
        private string? NguoiThaoTac => User.Identity?.Name;

        private List<CaAn> DsCa() => _config.GetSection("NhaAn:Ca").GetChildren()
            .Select(x => new CaAn(x["Ma"] ?? "", x["Ten"] ?? x["Ma"] ?? "", x["GioAn"] ?? ""))
            .Where(x => x.Ma != "")
            .ToList();

        /// <summary>Chỉ nhận mã bữa có trong cấu hình, không cho ghi bữa tuỳ ý.</summary>
        private CaAn? TimCa(string? ma) => DsCa().FirstOrDefault(x => x.Ma.Equals(ma?.Trim(), StringComparison.OrdinalIgnoreCase));

        private static DateTime? DocNgay(string? s) =>
            DateTime.TryParseExact(s?.Trim(), "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var d) ? d.Date : null;

        private static DateTime DauTuan(DateTime d) => d.Date.AddDays(-(((int)d.DayOfWeek + 6) % 7));

        private JsonResult Loi(string thongBao) => Json(new { thanhCong = false, thongBao });

        private void GhiLichSu(int id, string hanhDong, string? noiDung)
        {
            _context.NaLichSus.Add(new NaLichSu
            {
                ThoiGian = DateTime.Now,
                NguoiThaoTac = NguoiThaoTac,
                DoiTuong = NaLichSu.DoiTuongThucDon,
                IdDoiTuong = id,
                HanhDong = hanhDong,
                NoiDung = noiDung?.Length > 2000 ? noiDung[..2000] : noiDung,
            });
        }

        [HttpGet("/NhaAn")]
        public async Task<IActionResult> Index()
        {
            // Ảnh đại diện đọc từ DB như trang chủ: claim lúc đăng nhập có thể đã cũ sau khi đổi ảnh
            if (int.TryParse(User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value, out var idNd))
                ViewBag.AnhDaiDien = await _context.Users.AsNoTracking()
                    .Where(u => u.IdNguoiDung == idNd).Select(u => u.AnhDaiDien).FirstOrDefaultAsync();
            ViewBag.DsCa = DsCa();
            ViewBag.CoQuyenQuanLy = CoQuyenQuanLy();
            ViewBag.UrlCanteen = _config["NhaAn:UrlCanteen"];
            return View("~/Areas/NhaAn/Views/NhaAn/Index.cshtml");
        }

        /// <summary>Thực đơn 1 tuần (thứ 2 → CN).</summary>
        [HttpGet("/NhaAn/Tuan")]
        public async Task<IActionResult> Tuan(string? tuNgay)
        {
            try
            {
                var dau = DauTuan(DocNgay(tuNgay) ?? DateTime.Today);
                var cuoi = dau.AddDays(6);
                var thucDon = await _context.NaThucDons.AsNoTracking()
                    .Where(x => x.NgayXoa == null && x.Ngay >= dau && x.Ngay <= cuoi)
                    .Select(x => new { x.Ngay, x.Ca, x.MonAn, x.GhiChu })
                    .ToListAsync();
                // Thẻ "Hôm nay" luôn cố định trên trang, kể cả khi đang xem tuần khác → trả riêng thực đơn hôm nay
                var homNay = DateTime.Today;
                var thucDonHomNay = homNay >= dau && homNay <= cuoi
                    ? thucDon.Where(x => x.Ngay == homNay).ToList()
                    : await _context.NaThucDons.AsNoTracking()
                        .Where(x => x.NgayXoa == null && x.Ngay == homNay)
                        .Select(x => new { x.Ngay, x.Ca, x.MonAn, x.GhiChu })
                        .ToListAsync();

                return Json(new
                {
                    thanhCong = true,
                    duLieu = new
                    {
                        tuNgay = dau.ToString("yyyy-MM-dd"),
                        denNgay = cuoi.ToString("yyyy-MM-dd"),
                        homNay = DateTime.Today.ToString("yyyy-MM-dd"),
                        dsCa = DsCa(),
                        thucDon = thucDon.Select(x => new { ngay = x.Ngay.ToString("yyyy-MM-dd"), ca = x.Ca, monAn = x.MonAn, ghiChu = x.GhiChu }),
                        thucDonHomNay = thucDonHomNay.Select(x => new { ca = x.Ca, monAn = x.MonAn, ghiChu = x.GhiChu }),
                    }
                });
            }
            catch (Exception ex)
            {
                return Loi("Không tải được thực đơn: " + ex.Message);
            }
        }

        /// <summary>Quản lý: lưu thực đơn 1 ô. Món ăn để trống = xoá mềm thực đơn ô đó.</summary>
        [HttpPost("/NhaAn/ThucDon")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ThucDon([FromBody] ThucDonVao vao)
        {
            if (!CoQuyenQuanLy()) return Loi("Bạn không có quyền sửa thực đơn.");
            var ngay = DocNgay(vao?.Ngay);
            var ca = TimCa(vao?.Ca);
            if (ngay == null || ca == null) return Loi("Ngày hoặc bữa không hợp lệ.");
            var monAn = string.Join("\n", (vao!.MonAn ?? "").Split('\n').Select(x => x.Trim()).Where(x => x != ""));
            var ghiChu = string.IsNullOrWhiteSpace(vao.GhiChu) ? null : vao.GhiChu.Trim();
            if (monAn.Length > 1000) return Loi("Danh sách món tối đa 1000 ký tự.");
            if (ghiChu?.Length > 500) return Loi("Ghi chú tối đa 500 ký tự.");
            try
            {
                var td = await _context.NaThucDons.FirstOrDefaultAsync(x => x.NgayXoa == null && x.Ngay == ngay && x.Ca == ca.Ma);
                var nhan = $"{ngay:dd/MM/yyyy} · {ca.Ten}";
                if (monAn == "")
                {
                    if (td == null) return Json(new { thanhCong = true, thongBao = "Ô này chưa có thực đơn." });
                    td.NgayXoa = DateTime.Now;
                    td.NguoiXoa = NguoiThaoTac;
                    GhiLichSu(td.IdThucDon, NaLichSu.Xoa, $"{nhan}: {td.MonAn.Replace("\n", ", ")}");
                    await _context.SaveChangesAsync();
                    return Json(new { thanhCong = true, thongBao = "Đã xoá thực đơn." });
                }
                if (td == null)
                {
                    td = new NaThucDon { Ngay = ngay.Value, Ca = ca.Ma, MonAn = monAn, GhiChu = ghiChu, NguoiTao = NguoiThaoTac, NgayTao = DateTime.Now };
                    _context.NaThucDons.Add(td);
                    await _context.SaveChangesAsync();
                    GhiLichSu(td.IdThucDon, NaLichSu.Tao, $"{nhan}: {monAn.Replace("\n", ", ")}");
                }
                else
                {
                    if (td.MonAn == monAn && td.GhiChu == ghiChu) return Json(new { thanhCong = true, thongBao = "Không có gì thay đổi." });
                    var truoc = td.MonAn.Replace("\n", ", ");
                    td.MonAn = monAn;
                    td.GhiChu = ghiChu;
                    td.NguoiCapNhat = NguoiThaoTac;
                    td.NgayCapNhat = DateTime.Now;
                    GhiLichSu(td.IdThucDon, NaLichSu.CapNhat, $"{nhan}: {truoc} → {monAn.Replace("\n", ", ")}");
                }
                await _context.SaveChangesAsync();
                return Json(new { thanhCong = true, thongBao = "Đã lưu thực đơn." });
            }
            catch (DbUpdateException)
            {
                // UNIQUE (ngày, bữa) ở DB: người khác vừa lưu cùng ô
                return Loi("Ô này vừa được người khác lưu, tải lại tuần rồi sửa tiếp.");
            }
            catch (Exception ex)
            {
                return Loi("Không lưu được: " + ex.Message);
            }
        }

        /// <summary>Quản lý: chép thực đơn tuần trước sang tuần này — chỉ điền ô còn trống, không ghi đè.</summary>
        [HttpPost("/NhaAn/SaoChepTuan")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SaoChepTuan([FromBody] SaoChepTuanVao vao)
        {
            if (!CoQuyenQuanLy()) return Loi("Bạn không có quyền sửa thực đơn.");
            var dauVao = DocNgay(vao?.TuNgay);
            if (dauVao == null) return Loi("Tuần không hợp lệ.");
            try
            {
                var dau = DauTuan(dauVao.Value);
                var dauTruoc = dau.AddDays(-7);
                var maCa = DsCa().Select(x => x.Ma).ToList();
                var tuanTruoc = await _context.NaThucDons.AsNoTracking()
                    .Where(x => x.NgayXoa == null && x.Ngay >= dauTruoc && x.Ngay < dau && maCa.Contains(x.Ca))
                    .ToListAsync();
                var daCo = await _context.NaThucDons.AsNoTracking()
                    .Where(x => x.NgayXoa == null && x.Ngay >= dau && x.Ngay <= dau.AddDays(6))
                    .Select(x => new { x.Ngay, x.Ca })
                    .ToListAsync();
                var moi = tuanTruoc
                    .Where(x => !daCo.Any(c => c.Ngay == x.Ngay.AddDays(7) && c.Ca == x.Ca))
                    .Select(x => new NaThucDon { Ngay = x.Ngay.AddDays(7), Ca = x.Ca, MonAn = x.MonAn, GhiChu = x.GhiChu, NguoiTao = NguoiThaoTac, NgayTao = DateTime.Now })
                    .ToList();
                if (moi.Count == 0) return Json(new { thanhCong = true, thongBao = "Không có ô trống nào để chép (tuần trước trống hoặc tuần này đã đủ)." });
                _context.NaThucDons.AddRange(moi);
                await _context.SaveChangesAsync();
                foreach (var td in moi)
                    GhiLichSu(td.IdThucDon, NaLichSu.Tao, $"{td.Ngay:dd/MM/yyyy} · {td.Ca}: chép từ tuần trước");
                await _context.SaveChangesAsync();
                return Json(new { thanhCong = true, thongBao = $"Đã chép {moi.Count} ô thực đơn từ tuần trước." });
            }
            catch (DbUpdateException)
            {
                return Loi("Có ô vừa được người khác lưu, tải lại tuần rồi thử lại.");
            }
            catch (Exception ex)
            {
                return Loi("Không chép được: " + ex.Message);
            }
        }
    }
}
