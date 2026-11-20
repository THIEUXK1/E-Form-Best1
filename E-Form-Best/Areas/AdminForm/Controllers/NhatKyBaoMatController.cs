using E_Form_Best.Areas.ITForm.Services;
using E_Form_Best.Context;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace E_Form_Best.Areas.AdminForm.Controllers
{
    /// <summary>
    /// Trang Nhật ký bảo mật — chỉ quyền All. Gom 3 nguồn:
    ///  - baomat     : LichSuBaoMat (bị chặn chưa đăng nhập / thiếu quyền, lấy dữ liệu nhạy cảm)
    ///  - dangnhaploi: LichSuDangNhapLoi (sai mật khẩu, tài khoản không tồn tại, bị khoá...)
    ///  - truycap    : LichSuTruyCap (đăng nhập / đăng xuất thành công)
    /// Chỉ đọc; các bảng LichSu* là append-only.
    /// </summary>
    [Area("AdminForm")]
    // NhatKyBaoMat: quyền cá nhân (bảng Quyen) để mở trang cho người không có siêu quyền All.
    [Authorize(Roles = "All,NhatKyBaoMat")]
    public class NhatKyBaoMatController : Controller
    {
        private const int SoDongMoiTrang = 50;
        private const int SoNgayToiDa = 366;

        private readonly ITFormContext _context;

        public NhatKyBaoMatController(ITFormContext context)
        {
            _context = context;
        }

        [HttpGet("/NhatKyBaoMat")]
        public IActionResult Index() => View();

        // Số liệu 24 giờ qua cho các ô tổng quan + IP bị chặn nhiều nhất
        [HttpGet("/NhatKyBaoMat/TongQuan")]
        public async Task<IActionResult> TongQuan()
        {
            try
            {
                var tu = DateTime.Now.AddHours(-24);
                var demBaoMat = await _context.LichSuBaoMats.AsNoTracking()
                    .Where(x => x.ThoiGian >= tu)
                    .GroupBy(x => x.Loai)
                    .Select(g => new { Loai = g.Key, SoLan = g.Count() })
                    .ToListAsync();
                var dangNhapLoi = await _context.LichSuDangNhapLois.AsNoTracking().CountAsync(x => x.ThoiGian >= tu);
                var dangNhap = await _context.LichSuTruyCaps.AsNoTracking().CountAsync(x => x.ThoiGianDangNhap >= tu);

                var ipBiChan = await _context.LichSuBaoMats.AsNoTracking()
                    .Where(x => x.ThoiGian >= tu && x.Loai != NhatKyBaoMat.DuLieuNhayCam && x.DiaChiIp != null)
                    .GroupBy(x => x.DiaChiIp)
                    .Select(g => new { ip = g.Key, soLan = g.Count(), lanCuoi = g.Max(x => x.ThoiGian) })
                    .OrderByDescending(x => x.soLan).Take(5)
                    .ToListAsync();
                var ipDangNhapLoi = await _context.LichSuDangNhapLois.AsNoTracking()
                    .Where(x => x.ThoiGian >= tu && x.DiaChiIp != null)
                    .GroupBy(x => x.DiaChiIp)
                    .Select(g => new { ip = g.Key, soLan = g.Count(), lanCuoi = g.Max(x => x.ThoiGian) })
                    .OrderByDescending(x => x.soLan).Take(5)
                    .ToListAsync();

                int Dem(string loai) => demBaoMat.FirstOrDefault(x => x.Loai == loai)?.SoLan ?? 0;
                return Json(new
                {
                    thanhCong = true,
                    duLieu = new
                    {
                        chuaDangNhap = Dem(NhatKyBaoMat.ChuaDangNhap),
                        khongDuQuyen = Dem(NhatKyBaoMat.KhongDuQuyen),
                        duLieuNhayCam = Dem(NhatKyBaoMat.DuLieuNhayCam),
                        dangNhapLoi,
                        dangNhap,
                        ipBiChan,
                        ipDangNhapLoi
                    }
                });
            }
            catch (Exception ex)
            {
                return Json(new { thanhCong = false, thongBao = "Không tải được tổng quan: " + ex.Message });
            }
        }

        [HttpGet("/NhatKyBaoMat/DuLieu")]
        public async Task<IActionResult> DuLieu(string? nguon, DateTime? tuNgay, DateTime? denNgay, string? loai, string? q, int trang = 1)
        {
            var den = (denNgay ?? DateTime.Today).Date.AddDays(1);
            var tu = (tuNgay ?? DateTime.Today.AddDays(-6)).Date;
            if (tu >= den) return Json(new { thanhCong = false, thongBao = "Từ ngày phải trước hoặc bằng đến ngày." });
            if ((den - tu).TotalDays > SoNgayToiDa) return Json(new { thanhCong = false, thongBao = $"Chỉ xem tối đa {SoNgayToiDa} ngày một lần." });
            if (trang < 1) trang = 1;
            var tuKhoa = string.IsNullOrWhiteSpace(q) ? null : q.Trim();

            try
            {
                switch (nguon)
                {
                    case "dangnhaploi":
                    {
                        var query = _context.LichSuDangNhapLois.AsNoTracking().Where(x => x.ThoiGian >= tu && x.ThoiGian < den);
                        if (!string.IsNullOrEmpty(loai)) query = query.Where(x => x.LyDo == loai);
                        if (tuKhoa != null)
                            query = query.Where(x => (x.TaiKhoanNhap != null && x.TaiKhoanNhap.Contains(tuKhoa))
                                                  || (x.DiaChiIp != null && x.DiaChiIp.Contains(tuKhoa)));
                        var tong = await query.CountAsync();
                        var dong = await query.OrderByDescending(x => x.ThoiGian).ThenByDescending(x => x.Id)
                            .Skip((trang - 1) * SoDongMoiTrang).Take(SoDongMoiTrang)
                            .Select(x => new
                            {
                                thoiGian = x.ThoiGian,
                                taiKhoan = x.TaiKhoanNhap,
                                x.IdNguoiDung,
                                lyDo = x.LyDo,
                                noiGoi = x.NoiGoi,
                                ip = x.DiaChiIp,
                                trinhDuyet = x.TrinhDuyet
                            }).ToListAsync();
                        return Trang(tong, trang, dong);
                    }
                    case "truycap":
                    {
                        var query = _context.LichSuTruyCaps.AsNoTracking().Where(x => x.ThoiGianDangNhap >= tu && x.ThoiGianDangNhap < den);
                        if (tuKhoa != null)
                            query = query.Where(x => (x.DiaChiIp != null && x.DiaChiIp.Contains(tuKhoa))
                                                  || (x.TenMayTinh != null && x.TenMayTinh.Contains(tuKhoa))
                                                  || (x.IdNguoiDungNavigation.HoTen != null && x.IdNguoiDungNavigation.HoTen.Contains(tuKhoa))
                                                  || (x.IdNguoiDungNavigation.Tk != null && x.IdNguoiDungNavigation.Tk.Contains(tuKhoa)));
                        var tong = await query.CountAsync();
                        var dong = await query.OrderByDescending(x => x.ThoiGianDangNhap).ThenByDescending(x => x.IdLichSu)
                            .Skip((trang - 1) * SoDongMoiTrang).Take(SoDongMoiTrang)
                            .Select(x => new
                            {
                                thoiGian = x.ThoiGianDangNhap,
                                dangXuat = x.ThoiGianDangXuat,
                                taiKhoan = x.IdNguoiDungNavigation.Tk,
                                hoTen = x.IdNguoiDungNavigation.HoTen,
                                mayTinh = x.TenMayTinh,
                                ip = x.DiaChiIp,
                                trangThai = x.TrangThai,
                                trinhDuyet = x.TrinhDuyet
                            }).ToListAsync();
                        return Trang(tong, trang, dong);
                    }
                    default:
                    {
                        var query = _context.LichSuBaoMats.AsNoTracking().Where(x => x.ThoiGian >= tu && x.ThoiGian < den);
                        if (!string.IsNullOrEmpty(loai)) query = query.Where(x => x.Loai == loai);
                        if (tuKhoa != null)
                            query = query.Where(x => x.DuongDan.Contains(tuKhoa)
                                                  || (x.TaiKhoan != null && x.TaiKhoan.Contains(tuKhoa))
                                                  || (x.HoTen != null && x.HoTen.Contains(tuKhoa))
                                                  || (x.DiaChiIp != null && x.DiaChiIp.Contains(tuKhoa))
                                                  || (x.MoTa != null && x.MoTa.Contains(tuKhoa)));
                        var tong = await query.CountAsync();
                        var dong = await query.OrderByDescending(x => x.ThoiGian).ThenByDescending(x => x.Id)
                            .Skip((trang - 1) * SoDongMoiTrang).Take(SoDongMoiTrang)
                            .Select(x => new
                            {
                                thoiGian = x.ThoiGian,
                                loai = x.Loai,
                                taiKhoan = x.TaiKhoan,
                                hoTen = x.HoTen,
                                phuongThuc = x.PhuongThuc,
                                duongDan = x.DuongDan,
                                moTa = x.MoTa,
                                ip = x.DiaChiIp,
                                trinhDuyet = x.TrinhDuyet
                            }).ToListAsync();
                        return Trang(tong, trang, dong);
                    }
                }
            }
            catch (Exception ex)
            {
                return Json(new { thanhCong = false, thongBao = "Không tải được nhật ký: " + ex.Message });
            }
        }

        private JsonResult Trang(int tong, int trang, object dong) => Json(new
        {
            thanhCong = true,
            duLieu = new
            {
                tong,
                trang,
                soTrang = Math.Max(1, (int)Math.Ceiling(tong / (double)SoDongMoiTrang)),
                dong
            }
        });
    }
}
