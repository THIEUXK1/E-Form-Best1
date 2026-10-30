using E_Form_Best.Context;
using E_Form_Best.Models.ITForm;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.StaticFiles;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace E_Form_Best.Areas.ITForm.Controllers
{
    /// <summary>
    /// Trang "Theo dõi hệ thống": ô vuông các hệ thống nội bộ (MES / WMS / SCM / EHR...), bấm vào mở
    /// hộp theo dõi sự cố của hệ thống đó (KK_HeThongSuCo + nhật ký KK_HeThongSuCoLichSu).
    /// Danh sách hệ thống lấy từ cấu hình "TheoDoiHeThong:HeThong" để thêm/đổi không phải sửa code.
    /// Chỉ AdminIT và All được xem/ghi.
    /// </summary>
    [Area("ITform")]
    public class TheoDoiHeThongController : Controller
    {
        private readonly IConfiguration _config;
        private readonly ITFormContext _context;

        public TheoDoiHeThongController(IConfiguration config, ITFormContext context)
        {
            _config = config;
            _context = context;
        }

        public record HeThongLink(string Ten, string? MoTa, string? Url, string? Icon);

        private static readonly Dictionary<string, string> TenTrangThai = new()
        {
            [KkHeThongSuCo.DangXuLy] = "Đang xử lý",
            [KkHeThongSuCo.TheoDoi] = "Đang theo dõi",
            [KkHeThongSuCo.DaKhacPhuc] = "Đã khắc phục",
        };

        private bool CoQuyen() => User.IsInRole("AdminIT") || User.IsInRole("All");

        private List<HeThongLink> DsHeThong() => _config.GetSection("TheoDoiHeThong:HeThong").GetChildren()
            .Select(x => new HeThongLink(x["Ten"] ?? "", x["MoTa"], x["Url"], x["Icon"]))
            .Where(x => x.Ten != "")
            .ToList();

        /// <summary>Chỉ nhận mã hệ thống có trong cấu hình, không cho ghi sự cố cho hệ thống tuỳ ý.</summary>
        private string? MaHopLe(string? ma) => DsHeThong().Select(x => x.Ten)
            .FirstOrDefault(x => x.Equals(ma?.Trim(), StringComparison.OrdinalIgnoreCase));

        #region File bằng chứng (thư mục chia sẻ, không lưu DB: mỗi sự cố 1 thư mục SuCo_{id})

        private const int SoFileToiDa = 20;
        private const long CoFileToiDa = 30L * 1024 * 1024;
        private const long CoGuiToiDa = 100L * 1024 * 1024;

        // Danh sách trắng: chặn .exe/.bat/.html/.svg... vì file được mở trực tiếp từ trình duyệt
        private static readonly HashSet<string> DuoiChoPhep = new(StringComparer.OrdinalIgnoreCase)
        {
            ".jpg", ".jpeg", ".png", ".gif", ".bmp", ".webp",
            ".pdf", ".doc", ".docx", ".xls", ".xlsx", ".ppt", ".pptx", ".txt", ".log", ".csv",
            ".zip", ".rar", ".7z", ".mp4", ".mov", ".eml", ".msg"
        };
        private static readonly HashSet<string> DuoiAnh = new(StringComparer.OrdinalIgnoreCase)
            { ".jpg", ".jpeg", ".png", ".gif", ".bmp", ".webp" };
        private static readonly HashSet<string> DuoiXemTrucTiep = new(StringComparer.OrdinalIgnoreCase)
            { ".jpg", ".jpeg", ".png", ".gif", ".bmp", ".webp", ".pdf", ".txt", ".log", ".mp4", ".mov" };

        private string ThuMucSuCo(int idSuCo)
        {
            var goc = _config["TheoDoiHeThong:ThuMucBangChung"];
            if (string.IsNullOrWhiteSpace(goc)) throw new InvalidOperationException("Chưa cấu hình TheoDoiHeThong:ThuMucBangChung.");
            return Path.Combine(goc, $"SuCo_{idSuCo}");
        }

        /// <summary>Tên lưu = yyyyMMdd_HHmmss_{8 ký tự}_{tên gốc}; bỏ 3 đoạn đầu ra tên gốc để hiển thị.</summary>
        private static string TenHienThi(string tenLuu)
        {
            var phan = tenLuu.Split('_', 4);
            return phan.Length == 4 ? phan[3] : tenLuu;
        }

        private static string? KiemTraFile(List<IFormFile> files)
        {
            if (files.Count > SoFileToiDa) return $"Tối đa {SoFileToiDa} file mỗi lần.";
            if (files.Sum(x => x.Length) > CoGuiToiDa) return $"Tổng dung lượng tối đa {CoGuiToiDa / 1024 / 1024} MB mỗi lần.";
            foreach (var f in files)
            {
                if (f.Length > CoFileToiDa) return $"File \"{f.FileName}\" vượt quá {CoFileToiDa / 1024 / 1024} MB.";
                if (!DuoiChoPhep.Contains(Path.GetExtension(f.FileName))) return $"Không nhận loại file \"{Path.GetExtension(f.FileName)}\" ({f.FileName}).";
            }
            return null;
        }

        /// <summary>Ghi file vào thư mục sự cố, trả danh sách đường dẫn đã ghi để xoá lại nếu bước DB sau đó lỗi.</summary>
        private async Task<List<string>> GhiFileAsync(int idSuCo, List<IFormFile> files)
        {
            var daGhi = new List<string>();
            if (files.Count == 0) return daGhi;
            var thuMuc = ThuMucSuCo(idSuCo);
            Directory.CreateDirectory(thuMuc);
            try
            {
                foreach (var f in files)
                {
                    var tenGoc = string.Concat(Path.GetFileName(f.FileName).Split(Path.GetInvalidFileNameChars()));
                    var duoi = Path.GetExtension(tenGoc);
                    var than = Path.GetFileNameWithoutExtension(tenGoc);
                    if (than.Length > 80) than = than[..80];
                    var duongDan = Path.Combine(thuMuc, $"{DateTime.Now:yyyyMMdd_HHmmss}_{Guid.NewGuid().ToString("N")[..8]}_{than}{duoi}");
                    await using (var s = new FileStream(duongDan, FileMode.CreateNew))
                        await f.CopyToAsync(s);
                    daGhi.Add(duongDan);
                }
                return daGhi;
            }
            catch
            {
                XoaFile(daGhi);
                throw;
            }
        }

        private static void XoaFile(IEnumerable<string> dsDuongDan)
        {
            foreach (var p in dsDuongDan)
            {
                try { System.IO.File.Delete(p); } catch { /* dọn rác, lỗi xoá không che lỗi chính */ }
            }
        }

        private List<object> DanhSachFile(int idSuCo)
        {
            var thuMuc = ThuMucSuCo(idSuCo);
            if (!Directory.Exists(thuMuc)) return new();
            return new DirectoryInfo(thuMuc).GetFiles()
                .OrderBy(x => x.Name)
                .Select(x => (object)new
                {
                    tenLuu = x.Name,
                    ten = TenHienThi(x.Name),
                    kichThuoc = x.Length,
                    laAnh = DuoiAnh.Contains(x.Extension)
                })
                .ToList();
        }

        [HttpGet("/TheoDoiHeThong/SuCo/{id:int}/File/{tenLuu}")]
        public async Task<IActionResult> XemFile(int id, string tenLuu)
        {
            if (!CoQuyen()) return Forbid();
            if (!await _context.KkHeThongSuCos.AnyAsync(x => x.IdSuCo == id && x.NgayXoa == null)) return NotFound();

            // Chặn ../ và đường dẫn tuyệt đối: tên phải là tên file thuần và nằm đúng trong thư mục sự cố
            if (string.IsNullOrWhiteSpace(tenLuu) || Path.GetFileName(tenLuu) != tenLuu) return NotFound();
            var thuMuc = Path.GetFullPath(ThuMucSuCo(id));
            var duongDan = Path.GetFullPath(Path.Combine(thuMuc, tenLuu));
            if (!duongDan.StartsWith(thuMuc + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase)
                || !System.IO.File.Exists(duongDan)) return NotFound();

            if (!new FileExtensionContentTypeProvider().TryGetContentType(duongDan, out var loai)) loai = "application/octet-stream";
            var stream = new FileStream(duongDan, FileMode.Open, FileAccess.Read, FileShare.Read);
            // Ảnh/PDF/video mở ngay trên trình duyệt, loại khác tải về với tên gốc
            return DuoiXemTrucTiep.Contains(Path.GetExtension(duongDan))
                ? File(stream, loai, enableRangeProcessing: true)
                : File(stream, loai, TenHienThi(tenLuu), enableRangeProcessing: true);
        }

        #endregion

        private static string? Gon(string? s, int toiDa)
        {
            if (string.IsNullOrWhiteSpace(s)) return null;
            s = s.Trim();
            return s.Length > toiDa ? s[..toiDa] : s;
        }

        [HttpGet("/TheoDoiHeThong")]
        public async Task<IActionResult> Index()
        {
            if (!CoQuyen()) return Forbid();

            var ds = DsHeThong();
            var dsMa = ds.Select(x => x.Ten).ToList();
            // Trạng thái hiện tại của hệ thống = số sự cố chưa khắc phục
            ViewBag.SoSuCoMo = await _context.KkHeThongSuCos
                .Where(x => x.NgayXoa == null && x.TrangThai != KkHeThongSuCo.DaKhacPhuc && dsMa.Contains(x.MaHeThong))
                .GroupBy(x => x.MaHeThong)
                .Select(g => new { g.Key, SoLuong = g.Count() })
                .ToDictionaryAsync(x => x.Key, x => x.SoLuong);
            ViewBag.DsHeThong = ds;
            return View();
        }

        [HttpGet("/TheoDoiHeThong/SuCo")]
        public async Task<IActionResult> DanhSachSuCo(string? maHeThong)
        {
            if (!CoQuyen()) return Json(new { thanhCong = false, thongBao = "Bạn không có quyền xem." });
            var ma = MaHopLe(maHeThong);
            if (ma == null) return Json(new { thanhCong = false, thongBao = "Hệ thống không hợp lệ." });

            try
            {
                var ds = await _context.KkHeThongSuCos
                    .Where(x => x.MaHeThong == ma && x.NgayXoa == null)
                    .OrderByDescending(x => x.BatDauLuc).ThenByDescending(x => x.IdSuCo)
                    .Take(200)
                    .Select(x => new
                    {
                        x.IdSuCo, x.TieuDe, x.TrangThai, x.NguyenNhan, x.NguoiChiuTrachNhiem,
                        x.BatDauLuc, x.KetThucLuc
                    })
                    .ToListAsync();
                return Json(new { thanhCong = true, duLieu = ds });
            }
            catch (Exception ex) { return Json(new { thanhCong = false, thongBao = ex.Message }); }
        }

        [HttpGet("/TheoDoiHeThong/SuCo/{id:int}")]
        public async Task<IActionResult> ChiTietSuCo(int id)
        {
            if (!CoQuyen()) return Json(new { thanhCong = false, thongBao = "Bạn không có quyền xem." });

            try
            {
                var suCo = await _context.KkHeThongSuCos.AsNoTracking()
                    .FirstOrDefaultAsync(x => x.IdSuCo == id && x.NgayXoa == null);
                if (suCo == null) return Json(new { thanhCong = false, thongBao = "Không tìm thấy sự cố." });

                var lichSu = await _context.KkHeThongSuCoLichSus
                    .Where(x => x.IdSuCo == id)
                    .OrderByDescending(x => x.ThoiGian).ThenByDescending(x => x.Id)
                    .Select(x => new { x.ThoiGian, x.NguoiThaoTac, x.HanhDong, x.ThayDoi, x.GhiChu })
                    .ToListAsync();

                // Fileserver mất kết nối thì vẫn xem/sửa được sự cố, chỉ báo lỗi ở phần bằng chứng
                List<object> files;
                string? loiFile = null;
                try { files = DanhSachFile(id); }
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidOperationException)
                {
                    files = new();
                    loiFile = "Không đọc được thư mục bằng chứng: " + ex.Message;
                }

                return Json(new
                {
                    thanhCong = true,
                    duLieu = new
                    {
                        suCo.IdSuCo, suCo.MaHeThong, suCo.TieuDe, suCo.MoTa, suCo.TrangThai, suCo.NguyenNhan,
                        suCo.NguoiChiuTrachNhiem, suCo.BatDauLuc, suCo.KetThucLuc,
                        suCo.NguoiTao, suCo.NgayTao, suCo.NguoiCapNhat, suCo.NgayCapNhat,
                        lichSu,
                        files,
                        loiFile
                    }
                });
            }
            catch (Exception ex) { return Json(new { thanhCong = false, thongBao = ex.Message }); }
        }

        /// <summary>
        /// Tạo mới (idSuCo null) hoặc cập nhật sự cố. maGui do client sinh mỗi lần mở form:
        /// bấm lưu 2 lần thì lần sau trúng unique index và được coi là đã lưu.
        /// </summary>
        [HttpPost("/TheoDoiHeThong/SuCo/Luu")]
        [ValidateAntiForgeryToken]
        [RequestSizeLimit(CoGuiToiDa + 1024 * 1024)]
        [RequestFormLimits(MultipartBodyLengthLimit = CoGuiToiDa + 1024 * 1024)]
        public async Task<IActionResult> LuuSuCo(int? idSuCo, string? maHeThong, string? tieuDe, string? moTa,
            string? trangThai, string? nguyenNhan, string? nguoiChiuTrachNhiem,
            DateTime? batDauLuc, DateTime? ketThucLuc, string? ghiChu, Guid? maGui, List<IFormFile>? files)
        {
            if (!CoQuyen()) return Json(new { thanhCong = false, thongBao = "Bạn không có quyền thao tác." });
            if (maGui == null || maGui == Guid.Empty) return Json(new { thanhCong = false, thongBao = "Thiếu mã gửi, tải lại trang rồi thử lại." });
            files = (files ?? new()).Where(x => x.Length > 0).ToList();
            var loiFile = KiemTraFile(files);
            if (loiFile != null) return Json(new { thanhCong = false, thongBao = loiFile });
            // Tên file ghi vào nhật ký để biết ai đính kèm gì, lúc nào
            var ghiChuFile = files.Count > 0 ? "Đính kèm: " + string.Join(", ", files.Select(x => Path.GetFileName(x.FileName))) : null;
            var daGhi = new List<string>();

            tieuDe = Gon(tieuDe, 200);
            moTa = Gon(moTa, 2000);
            nguyenNhan = Gon(nguyenNhan, 1000);
            nguoiChiuTrachNhiem = Gon(nguoiChiuTrachNhiem, 100);
            ghiChu = Gon(ghiChu, 1000);

            if (tieuDe == null) return Json(new { thanhCong = false, thongBao = "Nhập tiêu đề sự cố." });
            if (trangThai == null || !TenTrangThai.ContainsKey(trangThai)) return Json(new { thanhCong = false, thongBao = "Trạng thái không hợp lệ." });
            if (batDauLuc == null) return Json(new { thanhCong = false, thongBao = "Nhập thời điểm bắt đầu." });
            // Chuyển sang đã khắc phục mà chưa nhập giờ kết thúc thì lấy giờ hiện tại
            if (trangThai == KkHeThongSuCo.DaKhacPhuc && ketThucLuc == null) ketThucLuc = DateTime.Now;
            if (trangThai != KkHeThongSuCo.DaKhacPhuc) ketThucLuc = null;
            if (ketThucLuc != null && ketThucLuc < batDauLuc) return Json(new { thanhCong = false, thongBao = "Thời điểm kết thúc phải sau thời điểm bắt đầu." });

            var nguoi = User.Identity?.Name;
            var bayGio = DateTime.Now;

            try
            {
                if (idSuCo == null)
                {
                    var ma = MaHopLe(maHeThong);
                    if (ma == null) return Json(new { thanhCong = false, thongBao = "Hệ thống không hợp lệ." });

                    var moi = new KkHeThongSuCo
                    {
                        MaHeThong = ma, TieuDe = tieuDe, MoTa = moTa, TrangThai = trangThai, NguyenNhan = nguyenNhan,
                        NguoiChiuTrachNhiem = nguoiChiuTrachNhiem, BatDauLuc = batDauLuc.Value, KetThucLuc = ketThucLuc,
                        MaGui = maGui, NguoiTao = nguoi, NgayTao = bayGio
                    };
                    // Cần id sự cố để đặt thư mục file → lưu sự cố trước, ghi file, rồi nhật ký; lỗi ở đâu cũng huỷ cả
                    await using var gd = await _context.Database.BeginTransactionAsync();
                    _context.KkHeThongSuCos.Add(moi);
                    await _context.SaveChangesAsync();
                    daGhi = await GhiFileAsync(moi.IdSuCo, files);
                    _context.KkHeThongSuCoLichSus.Add(new KkHeThongSuCoLichSu
                    {
                        IdSuCo = moi.IdSuCo, ThoiGian = bayGio, NguoiThaoTac = nguoi, HanhDong = KkHeThongSuCoLichSu.Tao,
                        ThayDoi = Gon($"Tạo sự cố \"{tieuDe}\" — {TenTrangThai[trangThai]}"
                                      + (nguyenNhan != null ? $"; Nguyên nhân: {nguyenNhan}" : "")
                                      + (nguoiChiuTrachNhiem != null ? $"; Người chịu trách nhiệm: {nguoiChiuTrachNhiem}" : "")
                                      + (ghiChuFile != null ? $"; {ghiChuFile}" : ""), 2000),
                        GhiChu = ghiChu
                    });
                    await _context.SaveChangesAsync();
                    await gd.CommitAsync();
                    return Json(new { thanhCong = true, thongBao = "Đã thêm sự cố.", duLieu = new { moi.IdSuCo, soSuCoMo = await SoSuCoMoAsync(ma) } });
                }

                var suCo = await _context.KkHeThongSuCos.FirstOrDefaultAsync(x => x.IdSuCo == idSuCo && x.NgayXoa == null);
                if (suCo == null) return Json(new { thanhCong = false, thongBao = "Không tìm thấy sự cố." });

                // Ghi lại trước → sau từng trường đổi để nhật ký đọc được không cần so bản ghi
                var thayDoi = new List<string>();
                void Doi(string ten, string? truoc, string? sau)
                {
                    if ((truoc ?? "") != (sau ?? "")) thayDoi.Add($"{ten}: {(string.IsNullOrEmpty(truoc) ? "(trống)" : truoc)} → {(string.IsNullOrEmpty(sau) ? "(trống)" : sau)}");
                }
                Doi("Tiêu đề", suCo.TieuDe, tieuDe);
                Doi("Mô tả", suCo.MoTa, moTa);
                Doi("Trạng thái", TenTrangThai.GetValueOrDefault(suCo.TrangThai, suCo.TrangThai), TenTrangThai[trangThai]);
                Doi("Nguyên nhân", suCo.NguyenNhan, nguyenNhan);
                Doi("Người chịu trách nhiệm", suCo.NguoiChiuTrachNhiem, nguoiChiuTrachNhiem);
                Doi("Bắt đầu", suCo.BatDauLuc.ToString("dd/MM/yyyy HH:mm"), batDauLuc.Value.ToString("dd/MM/yyyy HH:mm"));
                Doi("Kết thúc", suCo.KetThucLuc?.ToString("dd/MM/yyyy HH:mm"), ketThucLuc?.ToString("dd/MM/yyyy HH:mm"));

                if (ghiChuFile != null) thayDoi.Add(ghiChuFile);

                if (thayDoi.Count == 0 && ghiChu == null)
                    return Json(new { thanhCong = true, thongBao = "Không có thay đổi.", duLieu = new { suCo.IdSuCo, soSuCoMo = await SoSuCoMoAsync(suCo.MaHeThong) } });

                if (thayDoi.Count > 0)
                {
                    suCo.TieuDe = tieuDe; suCo.MoTa = moTa; suCo.TrangThai = trangThai; suCo.NguyenNhan = nguyenNhan;
                    suCo.NguoiChiuTrachNhiem = nguoiChiuTrachNhiem; suCo.BatDauLuc = batDauLuc.Value; suCo.KetThucLuc = ketThucLuc;
                    suCo.NguoiCapNhat = nguoi; suCo.NgayCapNhat = bayGio;
                }
                daGhi = await GhiFileAsync(suCo.IdSuCo, files);
                // Một SaveChanges = một giao dịch: sửa sự cố và dòng nhật ký cùng thành công hoặc cùng huỷ
                _context.KkHeThongSuCoLichSus.Add(new KkHeThongSuCoLichSu
                {
                    IdSuCo = suCo.IdSuCo, ThoiGian = bayGio, NguoiThaoTac = nguoi,
                    HanhDong = thayDoi.Count > 0 ? KkHeThongSuCoLichSu.CapNhat : KkHeThongSuCoLichSu.GhiChuTienDo,
                    ThayDoi = Gon(string.Join("; ", thayDoi), 2000), GhiChu = ghiChu, MaGui = maGui
                });
                await _context.SaveChangesAsync();
                return Json(new { thanhCong = true, thongBao = "Đã cập nhật sự cố.", duLieu = new { suCo.IdSuCo, soSuCoMo = await SoSuCoMoAsync(suCo.MaHeThong) } });
            }
            catch (DbUpdateException ex) when (ex.InnerException is SqlException sql && (sql.Number == 2601 || sql.Number == 2627))
            {
                // Trùng ma_gui = lần bấm trước đã lưu rồi, không tạo thêm; file của lần trùng này thì dọn đi
                XoaFile(daGhi);
                return Json(new { thanhCong = true, thongBao = "Đã lưu (bỏ qua lần bấm trùng)." });
            }
            catch (Exception ex)
            {
                XoaFile(daGhi);
                return Json(new { thanhCong = false, thongBao = ex.Message });
            }
        }

        private Task<int> SoSuCoMoAsync(string ma) => _context.KkHeThongSuCos
            .CountAsync(x => x.MaHeThong == ma && x.NgayXoa == null && x.TrangThai != KkHeThongSuCo.DaKhacPhuc);
    }
}
