using E_Form_Best.Context;
using E_Form_Best.Models.ITForm;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using System.Text.Encodings.Web;
using System.Text.Json;

namespace E_Form_Best.Areas.HRform.Controllers
{
    // Quản lý tủ điện thoại — tách controller riêng thay vì nhồi thêm vào HRFormController (6k dòng).
    [Area("HRform")]
    public class TuDienThoaiController : Controller
    {
        private readonly ITFormContext _context;
        private readonly IConfiguration _configuration;

        public TuDienThoaiController(ITFormContext context, IConfiguration configuration)
        {
            _context = context;
            _configuration = configuration;
        }

        // AdminIT cũng được vào vì IT phối hợp quản lý tủ cùng HR.
        private bool CoQuyenQuanLy() =>
            User?.Identity?.IsAuthenticated == true
            && (User.IsInRole("AdminHR") || User.IsInRole("AdminIT") || User.IsInRole("All"));

        private static string? Gon(string? s) => string.IsNullOrWhiteSpace(s) ? null : s.Trim();

        // Giữ nguyên tiếng Việt trong JSON lịch sử để đọc thẳng trên DB được
        private static readonly JsonSerializerOptions JsonLichSu = new() { Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping };

        // Ảnh chụp tủ lưu vào lịch sử; khoá phải trùng script hr-tu-dien-thoai-lich-su-20261028.sql và tu-dien-thoai.js
        private static string AnhChupTu(HrTuDienThoai t) => JsonSerializer.Serialize(new
        {
            idBoPhan = t.IdBoPhan,
            tenTu = t.TenTu,
            viTri = t.ViTri,
            maNhanVien = t.MaNhanVien,
            nguoiSuDung = t.NguoiSuDung
        }, JsonLichSu);

        private static HrTuDienThoaiLichSu TaoLichSu(int idTu, string hanhDong, string? cu, string? moi, DateTime ngay, string? nguoi, string? nguon = null) =>
            new() { IdTu = idTu, HanhDong = hanhDong, DuLieuCu = cu, DuLieuMoi = moi, Ngay = ngay, Nguoi = nguoi, Nguon = nguon };

        // Người không phải admin chỉ xem tủ của bộ phận mình: lấy theo User_BoPhan; ai chưa gán thì theo PhongBan
        // trong hồ sơ (cùng cách trang IT đang dùng). Trả rỗng = không thuộc bộ phận nào → không thấy tủ nào.
        private async Task<List<int>> BoPhanCuaToiAsync()
        {
            if (!int.TryParse(User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value, out var idNguoiDung))
                return new List<int>();
            var ds = await _context.UserBoPhans
                .Where(x => x.IdNguoiDung == idNguoiDung)
                .Select(x => x.IdBoPhan)
                .Distinct()
                .ToListAsync();
            if (ds.Count > 0) return ds;

            var phongBan = await _context.Users
                .Where(u => u.IdNguoiDung == idNguoiDung)
                .Select(u => u.PhongBan)
                .FirstOrDefaultAsync();
            if (string.IsNullOrWhiteSpace(phongBan)) return ds;
            return await _context.BoPhans.Where(b => b.TenBoPhan == phongBan).Select(b => b.IdBoPhan).ToListAsync();
        }

        public class TuDienThoaiInput
        {
            public int IdTu { get; set; }
            public int IdBoPhan { get; set; }
            public string? TenTu { get; set; }
            public string? ViTri { get; set; }
            public string? MaNhanVien { get; set; }
            public string? NguoiSuDung { get; set; }
            public string? MaBoPhan { get; set; }
        }

        // Ai đăng nhập cũng xem được; admin (AdminHR/AdminIT/All) xem hết và mới được thêm/sửa/xoá/in tem
        [HttpGet("/FormHR/QuanLyTuDienThoai")]
        public async Task<IActionResult> QuanLyTuDienThoai()
        {
            if (User?.Identity?.IsAuthenticated != true)
                return Redirect("/DonXetDuyet/DangNhap");

            bool laQuanLy = CoQuyenQuanLy();
            var q = _context.BoPhans.Where(b => b.TenBoPhan != null);
            if (!laQuanLy)
            {
                var cuaToi = await BoPhanCuaToiAsync();
                q = q.Where(b => cuaToi.Contains(b.IdBoPhan));
            }
            var dsBoPhan = await q
                .OrderBy(b => b.TenBoPhan)
                .Select(b => new { b.IdBoPhan, b.TenBoPhan, b.MaBoPhan })
                .ToListAsync();
            ViewBag.BoPhans = dsBoPhan.Select(b => new SelectListItem(b.TenBoPhan, b.IdBoPhan.ToString())).ToList();
            // Mã BP ghép vào ô chọn bộ phận ở hộp thoại thêm/sửa để gõ "QC" là tìm ra
            ViewBag.MaBoPhan = dsBoPhan.Where(b => b.MaBoPhan != null).ToDictionary(b => b.IdBoPhan.ToString(), b => b.MaBoPhan!);
            ViewBag.LaQuanLy = laQuanLy;
            return View();
        }

        [HttpGet("/FormHR/QuanLyTuDienThoai/DanhSach")]
        public async Task<IActionResult> DanhSach(int? idBoPhan, string? tuKhoa)
        {
            if (User?.Identity?.IsAuthenticated != true) return Json(new { thanhCong = false, thongBao = "Phiên đăng nhập đã hết, vui lòng đăng nhập lại." });

            try
            {
                var q = _context.HrTuDienThoais.Where(t => t.NgayXoa == null);
                // Không phải admin: chỉ tủ thuộc bộ phận của mình (lọc ở máy chủ, không tin tham số gửi lên)
                if (!CoQuyenQuanLy())
                {
                    var cuaToi = await BoPhanCuaToiAsync();
                    q = q.Where(t => cuaToi.Contains(t.IdBoPhan));
                }
                if (idBoPhan.HasValue) q = q.Where(t => t.IdBoPhan == idBoPhan.Value);

                var tk = Gon(tuKhoa);
                if (tk != null)
                    q = q.Where(t => t.TenTu.Contains(tk)
                        || (t.ViTri != null && t.ViTri.Contains(tk))
                        || (t.MaNhanVien != null && t.MaNhanVien.Contains(tk))
                        || (t.NguoiSuDung != null && t.NguoiSuDung.Contains(tk))
                        || _context.BoPhans.Any(b => b.IdBoPhan == t.IdBoPhan && b.MaBoPhan != null && b.MaBoPhan.Contains(tk)));

                var duLieu = await (from t in q
                                    join b in _context.BoPhans on t.IdBoPhan equals b.IdBoPhan
                                    orderby b.TenBoPhan, t.TenTu
                                    select new
                                    {
                                        t.IdTu,
                                        t.IdBoPhan,
                                        b.TenBoPhan,
                                        b.MaBoPhan,
                                        t.TenTu,
                                        t.ViTri,
                                        t.MaNhanVien,
                                        t.NguoiSuDung
                                    }).ToListAsync();

                return Json(new { thanhCong = true, duLieu });
            }
            catch (Exception ex)
            {
                return Json(new { thanhCong = false, thongBao = "Lỗi tải danh sách: " + ex.Message });
            }
        }

        [HttpPost("/FormHR/QuanLyTuDienThoai/Luu")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Luu([FromForm] TuDienThoaiInput input)
        {
            if (!CoQuyenQuanLy()) return Json(new { thanhCong = false, thongBao = "Bạn không có quyền thao tác." });

            var tenTu = Gon(input.TenTu);
            if (tenTu == null) return Json(new { thanhCong = false, thongBao = "Vui lòng nhập tên tủ." });
            if (tenTu.Length > 100) return Json(new { thanhCong = false, thongBao = "Tên tủ tối đa 100 ký tự." });
            var boPhan = await _context.BoPhans.FirstOrDefaultAsync(b => b.IdBoPhan == input.IdBoPhan);
            if (boPhan == null) return Json(new { thanhCong = false, thongBao = "Vui lòng chọn bộ phận." });

            // Mã BP: bộ phận chưa có mã thì lấy mã gõ ở form làm mã luôn; đã có mã khác thì không đè (mã dùng chung nhiều tủ)
            var maBp = Gon(input.MaBoPhan)?.ToUpperInvariant();
            string? maBpMoi = null;
            if (maBp != null)
            {
                if (maBp.Length > 20) return Json(new { thanhCong = false, thongBao = "Mã BP tối đa 20 ký tự." });
                if (boPhan.MaBoPhan == null) { boPhan.MaBoPhan = maBp; maBpMoi = maBp; }
                else if (!string.Equals(boPhan.MaBoPhan, maBp, StringComparison.OrdinalIgnoreCase))
                    return Json(new { thanhCong = false, thongBao = $"Bộ phận này đang dùng mã \"{boPhan.MaBoPhan}\" — đổi mã ở nút Mã bộ phận." });
            }

            var viTri = Gon(input.ViTri);
            var maNv = Gon(input.MaNhanVien);
            var nguoiSuDung = Gon(input.NguoiSuDung);
            if ((viTri?.Length ?? 0) > 200 || (maNv?.Length ?? 0) > 50 || (nguoiSuDung?.Length ?? 0) > 200)
                return Json(new { thanhCong = false, thongBao = "Dữ liệu nhập quá dài." });

            // Kiểm trùng trước cho thông báo dễ hiểu; unique index trên DB vẫn là chốt chặn cuối khi bấm lưu 2 lần
            bool trung = await _context.HrTuDienThoais.AnyAsync(t =>
                t.NgayXoa == null && t.IdBoPhan == input.IdBoPhan && t.TenTu == tenTu && t.IdTu != input.IdTu);
            if (trung) return Json(new { thanhCong = false, thongBao = $"Bộ phận này đã có tủ \"{tenTu}\"." });

            try
            {
                var nguoi = User.Identity?.Name;
                var bayGio = DateTime.Now;
                if (input.IdTu == 0)
                {
                    var tuMoi = new HrTuDienThoai
                    {
                        IdBoPhan = input.IdBoPhan,
                        TenTu = tenTu,
                        ViTri = viTri,
                        MaNhanVien = maNv,
                        NguoiSuDung = nguoiSuDung,
                        NgayTao = bayGio,
                        NguoiTao = nguoi
                    };
                    // Cần id tủ mới để ghi lịch sử → 2 lần SaveChanges trong 1 giao dịch
                    await using var gd = await _context.Database.BeginTransactionAsync();
                    _context.HrTuDienThoais.Add(tuMoi);
                    await _context.SaveChangesAsync();
                    _context.HrTuDienThoaiLichSus.Add(TaoLichSu(tuMoi.IdTu, "Them", null, AnhChupTu(tuMoi), bayGio, nguoi));
                    await _context.SaveChangesAsync();
                    await gd.CommitAsync();
                }
                else
                {
                    var tu = await _context.HrTuDienThoais.FirstOrDefaultAsync(t => t.IdTu == input.IdTu && t.NgayXoa == null);
                    if (tu == null) return Json(new { thanhCong = false, thongBao = "Không tìm thấy tủ cần sửa (có thể đã bị xoá)." });

                    var cu = AnhChupTu(tu);
                    tu.IdBoPhan = input.IdBoPhan;
                    tu.TenTu = tenTu;
                    tu.ViTri = viTri;
                    tu.MaNhanVien = maNv;
                    tu.NguoiSuDung = nguoiSuDung;
                    var moi = AnhChupTu(tu);
                    // Bấm Lưu mà không đổi gì thì không ghi lịch sử rác
                    if (moi != cu)
                    {
                        tu.NgaySua = bayGio;
                        tu.NguoiSua = nguoi;
                        _context.HrTuDienThoaiLichSus.Add(TaoLichSu(tu.IdTu, "Sua", cu, moi, bayGio, nguoi));
                    }
                    await _context.SaveChangesAsync();
                }

                return Json(new
                {
                    thanhCong = true,
                    thongBao = input.IdTu == 0 ? "Đã thêm tủ." : "Đã cập nhật tủ.",
                    duLieu = new { idBoPhan = boPhan.IdBoPhan, maBoPhanMoi = maBpMoi }
                });
            }
            catch (DbUpdateException ex) when (ex.InnerException is SqlException { Number: 2601 or 2627 })
            {
                return Json(new { thanhCong = false, thongBao = $"Bộ phận này đã có tủ \"{tenTu}\"." });
            }
            catch (Exception ex)
            {
                return Json(new { thanhCong = false, thongBao = "Lỗi lưu tủ: " + ex.Message });
            }
        }

        [HttpPost("/FormHR/QuanLyTuDienThoai/Xoa")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Xoa([FromForm] int idTu)
        {
            if (!CoQuyenQuanLy()) return Json(new { thanhCong = false, thongBao = "Bạn không có quyền thao tác." });

            try
            {
                var tu = await _context.HrTuDienThoais.FirstOrDefaultAsync(t => t.IdTu == idTu && t.NgayXoa == null);
                // Đã xoá rồi (bấm 2 lần) thì coi như xong, không báo lỗi
                if (tu == null) return Json(new { thanhCong = true, thongBao = "Tủ đã được xoá." });

                // Xoá mềm để còn truy vết ai đã dùng tủ nào
                tu.NgayXoa = DateTime.Now;
                tu.NguoiXoa = User.Identity?.Name;
                _context.HrTuDienThoaiLichSus.Add(TaoLichSu(tu.IdTu, "Xoa", AnhChupTu(tu), null, tu.NgayXoa.Value, tu.NguoiXoa));
                await _context.SaveChangesAsync();
                return Json(new { thanhCong = true, thongBao = "Đã xoá tủ." });
            }
            catch (Exception ex)
            {
                return Json(new { thanhCong = false, thongBao = "Lỗi xoá tủ: " + ex.Message });
            }
        }

        #region Lịch sử · khôi phục · mã bộ phận

        // Tra nhân viên theo mã để form tủ tự điền họ tên. Ưu tiên dữ liệu HRM (đủ nhân viên, kể cả người chưa có tài khoản E-Form),
        // không có thì tìm trong User.
        [HttpGet("/FormHR/QuanLyTuDienThoai/NhanVien")]
        public async Task<IActionResult> TimNhanVien(string? ma)
        {
            if (!CoQuyenQuanLy()) return Json(new { thanhCong = false, thongBao = "Bạn không có quyền thao tác." });
            var m = Gon(ma);
            if (m == null || m.Length > 50) return Json(new { thanhCong = false, thongBao = "Mã NV không hợp lệ." });

            try
            {
                var hrm = await _context.HrmNhanViens.AsNoTracking()
                    .Where(n => n.MaNhanVien == m)
                    .Select(n => new { n.MaNhanVien, n.HoTen, n.PhongBan, n.DangLamViec })
                    .FirstOrDefaultAsync();
                if (hrm != null)
                    return Json(new { thanhCong = true, duLieu = new { hrm.MaNhanVien, hrm.HoTen, hrm.PhongBan, daNghi = hrm.DangLamViec == false } });

                var u = await _context.Users.AsNoTracking()
                    .Where(x => x.MaNhanVien == m)
                    .Select(x => new { x.MaNhanVien, x.HoTen, x.PhongBan, x.TrangThai })
                    .FirstOrDefaultAsync();
                if (u != null)
                    return Json(new { thanhCong = true, duLieu = new { u.MaNhanVien, u.HoTen, u.PhongBan, daNghi = u.TrangThai == "Đã nghỉ" } });

                return Json(new { thanhCong = false, thongBao = "Không tìm thấy nhân viên mã này." });
            }
            catch (Exception ex)
            {
                return Json(new { thanhCong = false, thongBao = "Lỗi tra nhân viên: " + ex.Message });
            }
        }

        // Vị trí tủ đã dùng (không có bảng riêng: vị trí mới thêm bằng nút + sẽ có trong danh sách sau khi lưu tủ đầu tiên)
        [HttpGet("/FormHR/QuanLyTuDienThoai/ViTri")]
        public async Task<IActionResult> DanhSachViTri()
        {
            if (!CoQuyenQuanLy()) return Json(new { thanhCong = false, thongBao = "Bạn không có quyền thao tác." });

            try
            {
                var duLieu = await _context.HrTuDienThoais
                    .Where(t => t.NgayXoa == null && t.ViTri != null && t.ViTri != "")
                    .GroupBy(t => new { t.ViTri, t.IdBoPhan })
                    .Select(g => new { viTri = g.Key.ViTri, idBoPhan = g.Key.IdBoPhan, soTu = g.Count() })
                    .ToListAsync();
                return Json(new { thanhCong = true, duLieu });
            }
            catch (Exception ex)
            {
                return Json(new { thanhCong = false, thongBao = "Lỗi tải vị trí: " + ex.Message });
            }
        }

        // Lịch sử thao tác (mới nhất trước). idTu: lịch sử 1 tủ; tuKhoa: tìm trong tên tủ, dữ liệu trước/sau, người thao tác.
        [HttpGet("/FormHR/QuanLyTuDienThoai/LichSu")]
        public async Task<IActionResult> LichSu(int? idTu, string? hanhDong, string? tuKhoa)
        {
            if (!CoQuyenQuanLy()) return Json(new { thanhCong = false, thongBao = "Bạn không có quyền xem lịch sử." });

            try
            {
                var q = from l in _context.HrTuDienThoaiLichSus
                        join t in _context.HrTuDienThoais on l.IdTu equals t.IdTu
                        select new { l, t };
                if (idTu.HasValue) q = q.Where(x => x.l.IdTu == idTu.Value);
                var hd = Gon(hanhDong);
                if (hd != null) q = q.Where(x => x.l.HanhDong == hd);
                var tk = Gon(tuKhoa);
                if (tk != null)
                    q = q.Where(x => x.t.TenTu.Contains(tk)
                        || (x.l.Nguoi != null && x.l.Nguoi.Contains(tk))
                        || (x.l.DuLieuCu != null && x.l.DuLieuCu.Contains(tk))
                        || (x.l.DuLieuMoi != null && x.l.DuLieuMoi.Contains(tk)));

                const int toiDa = 300;
                var dong = await q.OrderByDescending(x => x.l.Ngay).ThenByDescending(x => x.l.IdLichSu)
                    .Take(toiDa + 1)
                    .Select(x => new
                    {
                        x.l.IdLichSu,
                        x.l.IdTu,
                        x.l.HanhDong,
                        x.l.Nguon,
                        x.l.DuLieuCu,
                        x.l.DuLieuMoi,
                        x.l.Ngay,
                        x.l.Nguoi,
                        tenTuHienTai = x.t.TenTu,
                        idBoPhanHienTai = x.t.IdBoPhan,
                        daXoa = x.t.NgayXoa != null
                    }).ToListAsync();

                // Dữ liệu trước/sau chỉ lưu id bộ phận → gửi kèm bảng tên để hiển thị
                var boPhan = await _context.BoPhans
                    .Select(b => new { b.IdBoPhan, b.TenBoPhan, b.MaBoPhan })
                    .ToDictionaryAsync(b => b.IdBoPhan, b => new { ten = b.TenBoPhan, ma = b.MaBoPhan });

                return Json(new
                {
                    thanhCong = true,
                    duLieu = new { dong = dong.Take(toiDa), conNua = dong.Count > toiDa, boPhan }
                });
            }
            catch (Exception ex)
            {
                return Json(new { thanhCong = false, thongBao = "Lỗi tải lịch sử: " + ex.Message });
            }
        }

        // Khôi phục tủ đã xoá mềm (xoá nhầm). Bấm 2 lần: lần sau thấy tủ đã hoạt động thì coi như xong.
        [HttpPost("/FormHR/QuanLyTuDienThoai/KhoiPhuc")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> KhoiPhuc([FromForm] int idTu)
        {
            if (!CoQuyenQuanLy()) return Json(new { thanhCong = false, thongBao = "Bạn không có quyền thao tác." });

            try
            {
                var tu = await _context.HrTuDienThoais.FirstOrDefaultAsync(t => t.IdTu == idTu);
                if (tu == null) return Json(new { thanhCong = false, thongBao = "Không tìm thấy tủ." });
                if (tu.NgayXoa == null) return Json(new { thanhCong = true, thongBao = "Tủ đã được khôi phục." });

                // Sau khi xoá có thể đã tạo tủ mới cùng tên trong bộ phận → không khôi phục chồng
                bool trung = await _context.HrTuDienThoais.AnyAsync(t =>
                    t.NgayXoa == null && t.IdBoPhan == tu.IdBoPhan && t.TenTu == tu.TenTu && t.IdTu != tu.IdTu);
                if (trung) return Json(new { thanhCong = false, thongBao = $"Bộ phận này đang có tủ \"{tu.TenTu}\" khác — đổi tên hoặc xoá tủ đó trước khi khôi phục." });

                var bayGio = DateTime.Now;
                var nguoi = User.Identity?.Name;
                tu.NgayXoa = null;
                tu.NguoiXoa = null;
                tu.NgaySua = bayGio;
                tu.NguoiSua = nguoi;
                _context.HrTuDienThoaiLichSus.Add(TaoLichSu(tu.IdTu, "KhoiPhuc", null, AnhChupTu(tu), bayGio, nguoi));
                await _context.SaveChangesAsync();
                return Json(new { thanhCong = true, thongBao = $"Đã khôi phục tủ \"{tu.TenTu}\"." });
            }
            catch (DbUpdateException ex) when (ex.InnerException is SqlException { Number: 2601 or 2627 })
            {
                return Json(new { thanhCong = false, thongBao = "Bộ phận này đang có tủ cùng tên — đổi tên hoặc xoá tủ đó trước khi khôi phục." });
            }
            catch (Exception ex)
            {
                return Json(new { thanhCong = false, thongBao = "Lỗi khôi phục tủ: " + ex.Message });
            }
        }

        [HttpGet("/FormHR/QuanLyTuDienThoai/MaBoPhan")]
        public async Task<IActionResult> DanhSachMaBoPhan()
        {
            if (!CoQuyenQuanLy()) return Json(new { thanhCong = false, thongBao = "Bạn không có quyền thao tác." });

            try
            {
                var soTu = await _context.HrTuDienThoais.Where(t => t.NgayXoa == null)
                    .GroupBy(t => t.IdBoPhan).Select(g => new { g.Key, n = g.Count() })
                    .ToDictionaryAsync(x => x.Key, x => x.n);
                var duLieu = (await _context.BoPhans.Where(b => b.TenBoPhan != null)
                        .OrderBy(b => b.TenBoPhan)
                        .Select(b => new { b.IdBoPhan, b.TenBoPhan, b.MaBoPhan, b.MoTa })
                        .ToListAsync())
                    .Select(b => new { b.IdBoPhan, b.TenBoPhan, b.MaBoPhan, b.MoTa, soTu = soTu.GetValueOrDefault(b.IdBoPhan) });
                return Json(new { thanhCong = true, duLieu });
            }
            catch (Exception ex)
            {
                return Json(new { thanhCong = false, thongBao = "Lỗi tải bộ phận: " + ex.Message });
            }
        }

        // Lưu mã bộ phận: chỉ gửi các dòng đã đổi; mã rỗng = bỏ mã. Gửi lại cùng dữ liệu thì kết quả như nhau.
        [HttpPost("/FormHR/QuanLyTuDienThoai/MaBoPhan/Luu")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> LuuMaBoPhan([FromForm] int[] idBoPhan, [FromForm] string?[] ma)
        {
            if (!CoQuyenQuanLy()) return Json(new { thanhCong = false, thongBao = "Bạn không có quyền thao tác." });
            if (idBoPhan == null || ma == null || idBoPhan.Length != ma.Length) return Json(new { thanhCong = false, thongBao = "Dữ liệu gửi lên không hợp lệ." });
            if (idBoPhan.Length == 0) return Json(new { thanhCong = true, thongBao = "Không có gì thay đổi." });

            var moi = new Dictionary<int, string?>();
            for (int i = 0; i < idBoPhan.Length; i++)
            {
                var m = Gon(ma[i])?.ToUpperInvariant();
                if ((m?.Length ?? 0) > 20) return Json(new { thanhCong = false, thongBao = $"Mã \"{m}\" quá dài (tối đa 20 ký tự)." });
                moi[idBoPhan[i]] = m;
            }

            try
            {
                var ids = moi.Keys.ToList();
                var ds = await _context.BoPhans.Where(b => ids.Contains(b.IdBoPhan)).ToListAsync();
                foreach (var b in ds) b.MaBoPhan = moi[b.IdBoPhan];
                await _context.SaveChangesAsync();
                return Json(new { thanhCong = true, thongBao = $"Đã lưu mã cho {ds.Count} bộ phận." });
            }
            catch (Exception ex)
            {
                return Json(new { thanhCong = false, thongBao = "Lỗi lưu mã bộ phận: " + ex.Message });
            }
        }

        #endregion

        #region Excel: tải mẫu, nhập · xoá nhiều

        [HttpGet("/FormHR/QuanLyTuDienThoai/MauExcel")]
        public async Task<IActionResult> TaiMauExcel()
        {
            if (User?.Identity?.IsAuthenticated != true)
                return Redirect("/DonXetDuyet/DangNhap");
            if (!CoQuyenQuanLy()) return Forbid();

            var dsBoPhan = await _context.BoPhans
                .Where(b => b.TenBoPhan != null && b.TenBoPhan != "")
                .OrderBy(b => b.TenBoPhan)
                .Select(b => new { b.TenBoPhan, b.MaBoPhan })
                .ToListAsync();
            var file = Services.TuDienThoaiExcel.TaoFileMau(dsBoPhan.Select(b => (b.TenBoPhan!, b.MaBoPhan)).ToList());
            return File(file, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", "MauNhapTuDienThoai.xlsx");
        }

        /// <summary>
        /// Nhập tủ từ Excel. luu=false: chỉ kiểm tra, trả kết quả từng dòng để xem trước; luu=true: ghi các dòng hợp lệ.
        /// Khớp tủ theo (Bộ phận + Tên tủ): có rồi thì cập nhật, chưa có thì thêm — nhập lại cùng file không sinh trùng.
        /// </summary>
        [HttpPost("/FormHR/QuanLyTuDienThoai/NhapExcel")]
        [ValidateAntiForgeryToken]
        [RequestSizeLimit(10_000_000)]
        public async Task<IActionResult> NhapExcel(IFormFile? file, [FromForm] bool luu)
        {
            if (!CoQuyenQuanLy()) return Json(new { thanhCong = false, thongBao = "Bạn không có quyền thao tác." });
            if (file == null || file.Length == 0) return Json(new { thanhCong = false, thongBao = "Chưa chọn file Excel." });
            if (!string.Equals(Path.GetExtension(file.FileName), ".xlsx", StringComparison.OrdinalIgnoreCase))
                return Json(new { thanhCong = false, thongBao = "Chỉ nhận file .xlsx (tải mẫu Excel trên trang)." });
            if (file.Length > 5_000_000) return Json(new { thanhCong = false, thongBao = "File quá lớn (tối đa 5 MB)." });

            List<Services.TuDienThoaiExcel.DongNhap> dsDong;
            try
            {
                using var ms = new MemoryStream();
                await file.CopyToAsync(ms);
                ms.Position = 0;
                dsDong = Services.TuDienThoaiExcel.DocFile(ms);
            }
            catch (InvalidDataException ex)
            {
                return Json(new { thanhCong = false, thongBao = ex.Message });
            }
            if (dsDong.Count == 0) return Json(new { thanhCong = false, thongBao = "File không có dòng dữ liệu nào (điền từ dòng 2 của sheet TuDienThoai)." });

            try
            {
                // Bộ phận: so khớp không dấu / không khoảng trắng; tên chuẩn hoá trùng nhau thì đòi ghi đúng tên
                var boPhans = await _context.BoPhans.Where(b => b.TenBoPhan != null)
                    .Select(b => new { b.IdBoPhan, b.TenBoPhan, b.MaBoPhan }).ToListAsync();
                var theoKhoa = boPhans.GroupBy(b => Services.TuDienThoaiExcel.BoDau(b.TenBoPhan)).ToDictionary(g => g.Key, g => g.ToList());
                var theoMa = boPhans.Where(b => !string.IsNullOrWhiteSpace(b.MaBoPhan))
                    .GroupBy(b => Services.TuDienThoaiExcel.BoDau(b.MaBoPhan)).ToDictionary(g => g.Key, g => g.ToList());

                var tuCu = await _context.HrTuDienThoais.Where(t => t.NgayXoa == null).ToListAsync();
                var tuTheoKhoa = tuCu.GroupBy(t => (t.IdBoPhan, Services.TuDienThoaiExcel.BoDau(t.TenTu)))
                    .ToDictionary(g => g.Key, g => g.First());

                var ketQua = new List<object>();
                var trongFile = new HashSet<(int, string)>();
                var canThem = new List<HrTuDienThoai>();
                var canSua = new List<(HrTuDienThoai Tu, Services.TuDienThoaiExcel.DongNhap Dong)>();
                int soThem = 0, soSua = 0, soGiuNguyen = 0, soLoi = 0;

                foreach (var d in dsDong)
                {
                    string? loi = null;
                    int? idBoPhan = null;
                    string? tenBoPhan = d.BoPhan, maBoPhan = d.MaBoPhan;
                    if (d.BoPhan == null && d.MaBoPhan == null) loi = "Thiếu bộ phận (ghi tên Bộ phận hoặc Mã BP)";
                    else if (d.BoPhan == null)
                    {
                        // Chỉ có mã: mã không bắt buộc duy nhất (2 bộ phận QC) → trùng thì đòi ghi tên
                        theoMa.TryGetValue(Services.TuDienThoaiExcel.BoDau(d.MaBoPhan), out var khopMa);
                        if (khopMa == null) loi = "Không có bộ phận mã \"" + d.MaBoPhan + "\" (đặt mã ở nút Mã bộ phận trên trang)";
                        else if (khopMa.Count > 1) loi = "Mã \"" + d.MaBoPhan + "\" dùng cho " + khopMa.Count + " bộ phận, hãy ghi thêm tên Bộ phận";
                        else { idBoPhan = khopMa[0].IdBoPhan; tenBoPhan = khopMa[0].TenBoPhan; }
                    }
                    else if (!theoKhoa.TryGetValue(Services.TuDienThoaiExcel.BoDau(d.BoPhan), out var khop)) loi = "Không có bộ phận \"" + d.BoPhan + "\" trên hệ thống";
                    else
                    {
                        var dung = khop.Count == 1 ? khop[0] : khop.FirstOrDefault(b => string.Equals(b.TenBoPhan!.Trim(), d.BoPhan, StringComparison.OrdinalIgnoreCase));
                        if (dung == null) loi = "Tên bộ phận khớp nhiều bộ phận, hãy chọn đúng tên trong danh sách";
                        // Ghi cả 2 mà lệch nhau thì không đoán bên nào đúng; bộ phận chưa đặt mã thì bỏ qua cột mã
                        else if (d.MaBoPhan != null && dung.MaBoPhan != null
                                 && Services.TuDienThoaiExcel.BoDau(d.MaBoPhan) != Services.TuDienThoaiExcel.BoDau(dung.MaBoPhan))
                            loi = "Mã BP \"" + d.MaBoPhan + "\" không khớp bộ phận này (mã trên hệ thống: " + dung.MaBoPhan + ")";
                        else { idBoPhan = dung.IdBoPhan; maBoPhan = dung.MaBoPhan ?? d.MaBoPhan; }
                    }
                    if (loi == null && d.TenTu == null) loi = "Thiếu tên tủ";
                    if (loi == null && (d.TenTu!.Length > 100 || (d.ViTri?.Length ?? 0) > 200 || (d.MaNhanVien?.Length ?? 0) > 50 || (d.NguoiSuDung?.Length ?? 0) > 200))
                        loi = "Dữ liệu quá dài (tên tủ ≤ 100, vị trí ≤ 200, mã NV ≤ 50, người sử dụng ≤ 200 ký tự)";
                    var khoa = (idBoPhan ?? 0, Services.TuDienThoaiExcel.BoDau(d.TenTu));
                    if (loi == null && !trongFile.Add(khoa)) loi = "Trùng tủ với một dòng phía trên trong file";

                    string trangThai;
                    if (loi != null) { trangThai = "loi"; soLoi++; }
                    else if (tuTheoKhoa.TryGetValue(khoa, out var cu))
                    {
                        bool khac = cu.ViTri != d.ViTri || cu.MaNhanVien != d.MaNhanVien || cu.NguoiSuDung != d.NguoiSuDung;
                        if (khac) { trangThai = "sua"; soSua++; canSua.Add((cu, d)); }
                        else { trangThai = "giu"; soGiuNguyen++; }
                    }
                    else
                    {
                        trangThai = "them"; soThem++;
                        canThem.Add(new HrTuDienThoai
                        {
                            IdBoPhan = idBoPhan!.Value, TenTu = d.TenTu!, ViTri = d.ViTri,
                            MaNhanVien = d.MaNhanVien, NguoiSuDung = d.NguoiSuDung
                        });
                    }
                    ketQua.Add(new { d.Dong, maBoPhan, boPhan = tenBoPhan, d.TenTu, d.ViTri, d.MaNhanVien, d.NguoiSuDung, trangThai, loi });
                }

                bool daLuu = false;
                if (luu && (canThem.Count > 0 || canSua.Count > 0))
                {
                    var nguoi = User.Identity?.Name;
                    var bayGio = DateTime.Now;
                    foreach (var t in canThem) { t.NgayTao = bayGio; t.NguoiTao = nguoi; }
                    foreach (var (tu, d) in canSua)
                    {
                        var cu = AnhChupTu(tu);
                        tu.ViTri = d.ViTri; tu.MaNhanVien = d.MaNhanVien; tu.NguoiSuDung = d.NguoiSuDung;
                        tu.NgaySua = bayGio; tu.NguoiSua = nguoi;
                        _context.HrTuDienThoaiLichSus.Add(TaoLichSu(tu.IdTu, "Sua", cu, AnhChupTu(tu), bayGio, nguoi, "Excel"));
                    }
                    // Một giao dịch: lỗi giữa chừng thì không ghi dở dang; lịch sử tủ mới ghi sau khi có id
                    await using var gd = await _context.Database.BeginTransactionAsync();
                    _context.HrTuDienThoais.AddRange(canThem);
                    await _context.SaveChangesAsync();
                    foreach (var t in canThem)
                        _context.HrTuDienThoaiLichSus.Add(TaoLichSu(t.IdTu, "Them", null, AnhChupTu(t), bayGio, nguoi, "Excel"));
                    await _context.SaveChangesAsync();
                    await gd.CommitAsync();
                    daLuu = true;
                }

                return Json(new
                {
                    thanhCong = true,
                    thongBao = daLuu ? $"Đã nhập: thêm {soThem}, cập nhật {soSua} tủ" + (soLoi > 0 ? $", bỏ qua {soLoi} dòng lỗi." : ".") : null,
                    duLieu = new { daLuu, tong = dsDong.Count, soThem, soSua, soGiuNguyen, soLoi, dong = ketQua }
                });
            }
            catch (DbUpdateException ex) when (ex.InnerException is SqlException { Number: 2601 or 2627 })
            {
                // Người khác vừa thêm đúng tủ đó trong lúc nhập → không ghi gì, kiểm tra lại là thấy thành "cập nhật"
                return Json(new { thanhCong = false, thongBao = "Có tủ vừa được người khác thêm trùng tên trong lúc nhập. Chưa ghi gì — bấm Kiểm tra lại rồi nhập." });
            }
            catch (Exception ex)
            {
                return Json(new { thanhCong = false, thongBao = "Lỗi nhập Excel: " + ex.Message });
            }
        }

        [HttpPost("/FormHR/QuanLyTuDienThoai/XoaNhieu")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> XoaNhieu([FromForm] int[] dsIdTu)
        {
            if (!CoQuyenQuanLy()) return Json(new { thanhCong = false, thongBao = "Bạn không có quyền thao tác." });
            if (dsIdTu == null || dsIdTu.Length == 0) return Json(new { thanhCong = false, thongBao = "Chưa chọn tủ nào." });
            if (dsIdTu.Length > 2000) return Json(new { thanhCong = false, thongBao = "Mỗi lần xoá tối đa 2000 tủ." });

            try
            {
                var ds = await _context.HrTuDienThoais.Where(t => dsIdTu.Contains(t.IdTu) && t.NgayXoa == null).ToListAsync();
                // Xoá mềm để còn truy vết ai đã dùng tủ nào; tủ đã xoá trước đó (bấm 2 lần) thì bỏ qua
                var bayGio = DateTime.Now;
                var nguoi = User.Identity?.Name;
                foreach (var t in ds)
                {
                    t.NgayXoa = bayGio; t.NguoiXoa = nguoi;
                    _context.HrTuDienThoaiLichSus.Add(TaoLichSu(t.IdTu, "Xoa", AnhChupTu(t), null, bayGio, nguoi, "XoaNhieu"));
                }
                await _context.SaveChangesAsync();
                return Json(new { thanhCong = true, thongBao = $"Đã xoá {ds.Count} tủ.", duLieu = new { soXoa = ds.Count } });
            }
            catch (Exception ex)
            {
                return Json(new { thanhCong = false, thongBao = "Lỗi xoá tủ: " + ex.Message });
            }
        }

        #endregion

        #region Mẫu tem in

        // Giới hạn dưới FormOptions.ValueLengthLimit (4 MB); ảnh đã được thu nhỏ ở trình duyệt trước khi nhúng.
        private const int DoDaiMauToiDa = 3_000_000;

        [HttpGet("/FormHR/QuanLyTuDienThoai/ThietKeTem")]
        public IActionResult ThietKeTem()
        {
            if (User?.Identity?.IsAuthenticated != true)
                return Redirect("/DonXetDuyet/DangNhap");
            if (!CoQuyenQuanLy()) return Forbid();

            return View();
        }

        [HttpGet("/FormHR/QuanLyTuDienThoai/MauTem")]
        public async Task<IActionResult> DanhSachMauTem()
        {
            if (!CoQuyenQuanLy()) return Json(new { thanhCong = false, thongBao = "Bạn không có quyền xem." });

            try
            {
                var duLieu = await _context.HrTuDienThoaiMauTems
                    .Where(m => m.NgayXoa == null)
                    .OrderBy(m => m.TenMau)
                    .Select(m => new { m.IdMau, m.TenMau, NgayCapNhat = m.NgaySua ?? m.NgayTao })
                    .ToListAsync();
                return Json(new { thanhCong = true, duLieu });
            }
            catch (Exception ex)
            {
                return Json(new { thanhCong = false, thongBao = "Lỗi tải danh sách mẫu tem: " + ex.Message });
            }
        }

        [HttpGet("/FormHR/QuanLyTuDienThoai/MauTem/{idMau:int}")]
        public async Task<IActionResult> ChiTietMauTem(int idMau)
        {
            if (!CoQuyenQuanLy()) return Json(new { thanhCong = false, thongBao = "Bạn không có quyền xem." });

            try
            {
                var mau = await _context.HrTuDienThoaiMauTems
                    .Where(m => m.IdMau == idMau && m.NgayXoa == null)
                    .Select(m => new { m.IdMau, m.TenMau, m.NoiDung })
                    .FirstOrDefaultAsync();
                if (mau == null) return Json(new { thanhCong = false, thongBao = "Không tìm thấy mẫu tem (có thể đã bị xoá)." });
                return Json(new { thanhCong = true, duLieu = mau });
            }
            catch (Exception ex)
            {
                return Json(new { thanhCong = false, thongBao = "Lỗi tải mẫu tem: " + ex.Message });
            }
        }

        [HttpPost("/FormHR/QuanLyTuDienThoai/MauTem/Luu")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> LuuMauTem([FromForm] int idMau, [FromForm] string? tenMau, [FromForm] string? noiDung)
        {
            if (!CoQuyenQuanLy()) return Json(new { thanhCong = false, thongBao = "Bạn không có quyền thao tác." });

            var ten = Gon(tenMau);
            if (ten == null) return Json(new { thanhCong = false, thongBao = "Vui lòng nhập tên mẫu tem." });
            if (ten.Length > 100) return Json(new { thanhCong = false, thongBao = "Tên mẫu tối đa 100 ký tự." });
            if (string.IsNullOrWhiteSpace(noiDung)) return Json(new { thanhCong = false, thongBao = "Mẫu tem trống." });
            if (noiDung.Length > DoDaiMauToiDa)
                return Json(new { thanhCong = false, thongBao = "Mẫu tem quá lớn (thường do ảnh). Hãy dùng ảnh nhỏ hơn." });

            // Chỉ nhận JSON hợp lệ dạng file .tnl: { Sheets: [trang, ...] } (bản cũ: 1 trang trần), mỗi trang có khổ + danh sách
            // đối tượng — tránh lưu rác làm hỏng trang in
            try
            {
                using var json = System.Text.Json.JsonDocument.Parse(noiDung);
                var goc = json.RootElement;
                static bool LaTrang(System.Text.Json.JsonElement t) =>
                    t.ValueKind == System.Text.Json.JsonValueKind.Object
                    && t.TryGetProperty("WidthMm", out _) && t.TryGetProperty("HeightMm", out _)
                    && t.TryGetProperty("Elements", out var ds) && ds.ValueKind == System.Text.Json.JsonValueKind.Array;
                bool hopLe = goc.ValueKind == System.Text.Json.JsonValueKind.Object
                    && (goc.TryGetProperty("Sheets", out var dsTrang)
                        ? dsTrang.ValueKind == System.Text.Json.JsonValueKind.Array && dsTrang.GetArrayLength() is > 0 and <= 50 && dsTrang.EnumerateArray().All(LaTrang)
                        : LaTrang(goc));
                if (!hopLe) return Json(new { thanhCong = false, thongBao = "Nội dung mẫu tem không đúng định dạng." });
            }
            catch (System.Text.Json.JsonException)
            {
                return Json(new { thanhCong = false, thongBao = "Nội dung mẫu tem không đúng định dạng." });
            }

            bool trung = await _context.HrTuDienThoaiMauTems.AnyAsync(m =>
                m.NgayXoa == null && m.TenMau == ten && m.IdMau != idMau);
            if (trung) return Json(new { thanhCong = false, thongBao = $"Đã có mẫu tem tên \"{ten}\"." });

            try
            {
                var nguoi = User.Identity?.Name;
                HrTuDienThoaiMauTem? mau;
                if (idMau == 0)
                {
                    mau = new HrTuDienThoaiMauTem { TenMau = ten, NoiDung = noiDung, NgayTao = DateTime.Now, NguoiTao = nguoi };
                    _context.HrTuDienThoaiMauTems.Add(mau);
                }
                else
                {
                    mau = await _context.HrTuDienThoaiMauTems.FirstOrDefaultAsync(m => m.IdMau == idMau && m.NgayXoa == null);
                    if (mau == null) return Json(new { thanhCong = false, thongBao = "Không tìm thấy mẫu tem cần lưu (có thể đã bị xoá)." });

                    mau.TenMau = ten;
                    mau.NoiDung = noiDung;
                    mau.NgaySua = DateTime.Now;
                    mau.NguoiSua = nguoi;
                }

                await _context.SaveChangesAsync();
                return Json(new { thanhCong = true, thongBao = "Đã lưu mẫu tem.", duLieu = new { mau.IdMau, mau.TenMau } });
            }
            catch (DbUpdateException ex) when (ex.InnerException is SqlException { Number: 2601 or 2627 })
            {
                return Json(new { thanhCong = false, thongBao = $"Đã có mẫu tem tên \"{ten}\"." });
            }
            catch (Exception ex)
            {
                return Json(new { thanhCong = false, thongBao = "Lỗi lưu mẫu tem: " + ex.Message });
            }
        }

        [HttpPost("/FormHR/QuanLyTuDienThoai/MauTem/Xoa")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> XoaMauTem([FromForm] int idMau)
        {
            if (!CoQuyenQuanLy()) return Json(new { thanhCong = false, thongBao = "Bạn không có quyền thao tác." });

            try
            {
                var mau = await _context.HrTuDienThoaiMauTems.FirstOrDefaultAsync(m => m.IdMau == idMau && m.NgayXoa == null);
                if (mau == null) return Json(new { thanhCong = true, thongBao = "Mẫu tem đã được xoá." });

                // Xoá mềm để khôi phục được mẫu lỡ tay xoá
                mau.NgayXoa = DateTime.Now;
                mau.NguoiXoa = User.Identity?.Name;
                await _context.SaveChangesAsync();
                return Json(new { thanhCong = true, thongBao = "Đã xoá mẫu tem." });
            }
            catch (Exception ex)
            {
                return Json(new { thanhCong = false, thongBao = "Lỗi xoá mẫu tem: " + ex.Message });
            }
        }

        #endregion

        #region Bộ cài Tem in Agent

        // Bộ cài để trên file server (máy chủ web đã đọc được share này — đơn HR/IT cũng lưu ở đây);
        // trình duyệt không mở được đường dẫn \\... từ trang http nên máy chủ đọc rồi trả file về.
        private const string ThuMucAgentMacDinh = @"\\10.0.60.30\BPVN-Fileserver\Public\IT-Information Technology Dept\5.E-Form\TeminHR";
        private const string TenFileAgent = "TemNhanAgent_CaiDat.msi";

        private string ThuMucAgent => _configuration["TemInAgent:ThuMuc"] ?? ThuMucAgentMacDinh;

        [HttpGet("/FormHR/QuanLyTuDienThoai/TemInAgent/KiemTra")]
        public IActionResult KiemTraBoCaiAgent()
        {
            if (!CoQuyenQuanLy()) return Json(new { thanhCong = false, thongBao = "Bạn không có quyền xem." });

            try
            {
                var file = new FileInfo(Path.Combine(ThuMucAgent, TenFileAgent));
                if (!file.Exists)
                    return Json(new { thanhCong = false, thongBao = "Chưa có bộ cài trên file server, liên hệ IT." });
                return Json(new
                {
                    thanhCong = true,
                    duLieu = new { kichThuocMb = Math.Round(file.Length / 1048576.0, 1), ngayCapNhat = file.LastWriteTime }
                });
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                // Không trả đường dẫn share / ex.Message (có chứa \\máy\thư mục) về trình duyệt
                return Json(new { thanhCong = false, thongBao = "Máy chủ không đọc được file server, liên hệ IT." });
            }
        }

        [HttpGet("/FormHR/QuanLyTuDienThoai/TemInAgent/Tai")]
        public IActionResult TaiBoCaiAgent()
        {
            if (User?.Identity?.IsAuthenticated != true)
                return Redirect("/DonXetDuyet/DangNhap");
            if (!CoQuyenQuanLy()) return Forbid();

            var duongDan = Path.Combine(ThuMucAgent, TenFileAgent);
            if (!System.IO.File.Exists(duongDan)) return NotFound("Chưa có bộ cài Tem in Agent trên file server, liên hệ IT.");
            return PhysicalFile(duongDan, "application/x-msi", TenFileAgent, enableRangeProcessing: true);
        }

        #endregion

        #region Driver máy in tem

        // Mỗi thư mục con trong Driver\ là 1 dòng máy in (vd "TSC TE200", "Brother PT-P900W"), bên trong để file cài.
        // IT chép thêm thư mục là có ngay trên trang, không cần sửa code.
        private string ThuMucDriver => _configuration["TemInAgent:ThuMucDriver"] ?? Path.Combine(ThuMucAgent, "Driver");

        [HttpGet("/FormHR/QuanLyTuDienThoai/DriverMayIn/DanhSach")]
        public IActionResult DanhSachDriverMayIn()
        {
            if (!CoQuyenQuanLy()) return Json(new { thanhCong = false, thongBao = "Bạn không có quyền xem." });

            try
            {
                var goc = new DirectoryInfo(ThuMucDriver);
                if (!goc.Exists)
                    return Json(new { thanhCong = false, thongBao = "Chưa có driver nào trên file server, liên hệ IT." });

                var ds = goc.GetDirectories()
                    .OrderBy(d => d.Name)
                    .Select(d => new
                    {
                        may = d.Name,
                        files = d.GetFiles()
                            .Where(f => (f.Attributes & (FileAttributes.Hidden | FileAttributes.System)) == 0)
                            .OrderBy(f => f.Name)
                            .Select(f => new { ten = f.Name, kichThuocMb = Math.Round(f.Length / 1048576.0, 1), ngayCapNhat = f.LastWriteTime })
                            .ToList()
                    })
                    .Where(m => m.files.Count > 0)
                    .ToList();
                return Json(new { thanhCong = true, duLieu = new { ds } });
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                // Như bộ cài Agent: không lộ đường dẫn share ra trình duyệt
                return Json(new { thanhCong = false, thongBao = "Máy chủ không đọc được file server, liên hệ IT." });
            }
        }

        [HttpGet("/FormHR/QuanLyTuDienThoai/DriverMayIn/Tai")]
        public IActionResult TaiDriverMayIn(string may, string file)
        {
            if (User?.Identity?.IsAuthenticated != true)
                return Redirect("/DonXetDuyet/DangNhap");
            if (!CoQuyenQuanLy()) return Forbid();

            // Chỉ nhận đúng tên thư mục / tên file — chặn ..\ hay đường dẫn tuyệt đối đọc file ngoài thư mục driver
            if (string.IsNullOrWhiteSpace(may) || string.IsNullOrWhiteSpace(file)
                || may.Contains("..") || file.Contains("..")
                || Path.GetFileName(may) != may || Path.GetFileName(file) != file)
                return BadRequest("Tên driver không hợp lệ.");

            var duongDan = Path.Combine(ThuMucDriver, may, file);
            if (!System.IO.File.Exists(duongDan)) return NotFound("Không tìm thấy driver này, liên hệ IT.");
            return PhysicalFile(duongDan, "application/octet-stream", file, enableRangeProcessing: true);
        }

        #endregion
    }
}
