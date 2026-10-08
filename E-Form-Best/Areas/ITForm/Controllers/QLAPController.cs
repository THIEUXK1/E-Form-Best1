using System.Net;
using System.Text.RegularExpressions;
using E_Form_Best.Areas.ITForm.Services;
using E_Form_Best.Context;
using E_Form_Best.Models.ITForm;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace E_Form_Best.Areas.ITForm.Controllers
{
    /// <summary>
    /// Quản lý AP Wi-Fi 3 công ty (cùng khuôn "Quản lý camera"): mỗi công ty một trang /QLAP/{công ty}
    /// gồm danh sách tài sản + lịch sử online/offline, và trang /QLAP/TongQuan báo cáo theo kỳ.
    /// Trạng thái kết nối do AccessPointPingWorker ghi, controller chỉ đọc.
    /// </summary>
    [Area("ITform")]
    public class QLAPController : Controller
    {
        private readonly ITFormContext _context;
        private readonly IConfiguration _configuration;

        // Danh mục cố định, đổi ở đây là view + lọc ăn theo
        public static readonly string[] DsTinhTrang = { "Đang hoạt động", "Hư hỏng", "Ngừng sử dụng", "Trong kho" };

        public QLAPController(ITFormContext context, IConfiguration configuration)
        {
            _context = context;
            _configuration = configuration;
        }

        // "All" toàn quyền AP cả 3 công ty; "ApBPVN" / "ApPFVN" / "ApMEGA" chỉ được xem AP công ty đó.
        // Ẩn nút ở UI không phải phân quyền nên mọi action đều phải gọi.
        private bool CoQuyenXem(string congTy) => User.IsInRole("All") || User.IsInRole("Ap" + congTy.ToUpperInvariant());
        private bool CoQuyenSua() => User.IsInRole("All");

        private static string? CongTyHopLe(string? congTy)
            => AccessPointPingWorker.DsCongTy.FirstOrDefault(x => x.Equals(congTy?.Trim(), StringComparison.OrdinalIgnoreCase));

        private Task<int?> IdCongTyAsync(string congTy)
            => _context.KkCongTies.Where(x => x.TenCongTy == congTy).Select(x => (int?)x.IdcongTy).FirstOrDefaultAsync();

        #region View

        [HttpGet("/QLAP/{congTy:regex(^(BPVN|PFVN|MEGA)$)}")]
        public async Task<IActionResult> Index(string congTy, [FromServices] AccessPointControllerService apController)
        {
            if (User?.Identity?.IsAuthenticated != true)
                return Redirect("/DonXetDuyet/DangNhap");

            var cty = CongTyHopLe(congTy)!;
            if (!CoQuyenXem(cty)) return Forbid();

            var idCongTy = await IdCongTyAsync(cty);
            // Chiếu vào entity public, không dùng anonymous type (xem ghi chú ở QLCCDCController.Index)
            ViewBag.DsBoPhan = await _context.KkBoPhans
                .Where(x => x.IdcongTy == idCongTy)
                .OrderBy(x => x.TenBoPhan)
                .Select(x => new KkBoPhan { IdboPhan = x.IdboPhan, TenBoPhan = x.TenBoPhan, IdcongTy = x.IdcongTy })
                .ToListAsync();

            ViewBag.CongTy = cty;
            ViewBag.CoCongTy = idCongTy != null;
            ViewBag.DsTinhTrang = DsTinhTrang;
            ViewBag.CoQuyenSua = CoQuyenSua();
            ViewBag.TenController = apController.TenController(cty);
            return View();
        }

        #endregion

        #region Danh sách

        [HttpGet("/QLAP/{congTy:regex(^(BPVN|PFVN|MEGA)$)}/DanhSach")]
        public async Task<IActionResult> DanhSach(string congTy, string? tuKhoa, int? idBoPhan, string? tinhTrang, string? ketNoi)
        {
            var cty = CongTyHopLe(congTy)!;
            if (!CoQuyenXem(cty)) return Json(new { thanhCong = false, thongBao = "Bạn không có quyền xem dữ liệu này." });

            try
            {
                var ds = await TruyVanDanhSach(await IdCongTyAsync(cty), tuKhoa, idBoPhan, tinhTrang, ketNoi)
                    .Select(x => new
                    {
                        x.IdAp, x.MaAp, x.TenAp, x.DiaChiIp, x.DiaChiMac, x.HangSanXuat, x.Model, x.Serial, x.Ssid,
                        x.TenController, x.IdboPhan,
                        TenBoPhan = x.IdboPhanNavigation != null ? x.IdboPhanNavigation.TenBoPhan : "",
                        x.ViTri, x.TinhTrang, x.NgayLapDat, x.HanBaoHanh, x.GhiChu,
                        x.TrangThaiKetNoi, x.DoiTrangThaiLuc, x.KiemTraLuc,
                        x.NguonTrangThai, x.TrangThaiController, x.NhomAp, x.PhienBan, x.SoClient, x.ThoiGianChayGiay
                    })
                    .ToListAsync();

                // AP không ping (chưa khai IP / trong kho / ngừng dùng) hiện "Không theo dõi" thay vì trạng thái cũ
                var duLieu = ds.Select(x => new
                {
                    x.IdAp, x.MaAp, x.TenAp, x.DiaChiIp, x.DiaChiMac, x.HangSanXuat, x.Model, x.Serial, x.Ssid,
                    x.TenController, x.IdboPhan, x.TenBoPhan, x.ViTri, x.TinhTrang, x.NgayLapDat, x.HanBaoHanh, x.GhiChu,
                    TheoDoi = TheoDoi(x.DiaChiIp, x.TinhTrang),
                    KetNoi = TheoDoi(x.DiaChiIp, x.TinhTrang) ? x.TrangThaiKetNoi : null,
                    x.DoiTrangThaiLuc, x.KiemTraLuc,
                    x.NguonTrangThai, x.TrangThaiController, x.NhomAp, x.PhienBan, x.SoClient, x.ThoiGianChayGiay
                }).ToList();

                return Json(new
                {
                    thanhCong = true,
                    duLieu,
                    tongDong = duLieu.Count,
                    tongOnline = duLieu.Count(x => x.KetNoi == "UP"),
                    tongOffline = duLieu.Count(x => x.KetNoi == "DOWN"),
                    tongClient = duLieu.Where(x => x.KetNoi == "UP").Sum(x => x.SoClient ?? 0),
                    // Lần ping gần nhất: quá lâu thì trang báo job nền không chạy ở máy chủ này
                    kiemTraGanNhat = duLieu.Max(x => x.KiemTraLuc),
                    chuKyGiay = Math.Clamp(_configuration.GetValue<int?>("AccessPoint:ChuKyGiay") ?? 120, 60, 3600)
                });
            }
            catch (Exception ex) { return Json(new { thanhCong = false, thongBao = ex.Message }); }
        }

        /// <summary>Danh sách AP 1 công ty ra Excel, theo đúng bộ lọc đang chọn trên trang.</summary>
        [HttpGet("/QLAP/{congTy:regex(^(BPVN|PFVN|MEGA)$)}/XuatExcel")]
        public async Task<IActionResult> XuatExcel(string congTy, string? tuKhoa, int? idBoPhan, string? tinhTrang, string? ketNoi)
        {
            var cty = CongTyHopLe(congTy)!;
            if (!CoQuyenXem(cty)) return StatusCode(403);

            var ds = await TruyVanDanhSach(await IdCongTyAsync(cty), tuKhoa, idBoPhan, tinhTrang, ketNoi)
                .Select(x => new
                {
                    x.MaAp, x.TenAp, x.DiaChiIp, x.DiaChiMac, x.HangSanXuat, x.Model, x.Serial, x.Ssid, x.TenController,
                    TenBoPhan = x.IdboPhanNavigation != null ? x.IdboPhanNavigation.TenBoPhan : null,
                    x.ViTri, x.TinhTrang, x.TrangThaiKetNoi, x.KiemTraLuc, x.NgayLapDat, x.HanBaoHanh, x.GhiChu
                })
                .ToListAsync();

            var dong = ds.Select(x => new AccessPointBaoCaoExcel.DongDanhSach(
                x.MaAp, x.TenAp, x.DiaChiIp, x.DiaChiMac,
                string.Join(" / ", new[] { x.HangSanXuat, x.Model }.Where(s => !string.IsNullOrWhiteSpace(s))),
                x.Serial, x.Ssid, x.TenController, x.TenBoPhan, x.ViTri, x.TinhTrang,
                !TheoDoi(x.DiaChiIp, x.TinhTrang) ? "Không theo dõi"
                    : x.TrangThaiKetNoi == "UP" ? "Online" : x.TrangThaiKetNoi == "DOWN" ? "Mất KN" : "Chưa kiểm",
                x.KiemTraLuc, x.NgayLapDat, x.HanBaoHanh, x.GhiChu)).ToList();

            var file = AccessPointBaoCaoExcel.TaoDanhSach(dong, cty, User.Identity?.Name);
            return File(file, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                $"DanhSachAP_{cty}_{DateTime.Now:yyyyMMdd}.xlsx");
        }

        private IQueryable<KkAccessPoint> TruyVanDanhSach(int? idCongTy, string? tuKhoa, int? idBoPhan, string? tinhTrang, string? ketNoi)
        {
            var query = _context.KkAccessPoints.Where(x => x.NgayXoa == null && x.IdcongTy == idCongTy);

            if (!string.IsNullOrWhiteSpace(tuKhoa))
            {
                var kw = tuKhoa.Trim();
                query = query.Where(x =>
                    x.TenAp.Contains(kw) ||
                    (x.MaAp != null && x.MaAp.Contains(kw)) ||
                    (x.DiaChiIp != null && x.DiaChiIp.Contains(kw)) ||
                    (x.DiaChiMac != null && x.DiaChiMac.Contains(kw)) ||
                    (x.HangSanXuat != null && x.HangSanXuat.Contains(kw)) ||
                    (x.Model != null && x.Model.Contains(kw)) ||
                    (x.Serial != null && x.Serial.Contains(kw)) ||
                    (x.Ssid != null && x.Ssid.Contains(kw)) ||
                    (x.TenController != null && x.TenController.Contains(kw)) ||
                    (x.ViTri != null && x.ViTri.Contains(kw)));
            }
            if (idBoPhan > 0) query = query.Where(x => x.IdboPhan == idBoPhan);
            if (!string.IsNullOrWhiteSpace(tinhTrang)) query = query.Where(x => x.TinhTrang == tinhTrang);

            // Lọc kết nối chỉ xét AP đang theo dõi (có IP, không trong kho / ngừng dùng)
            var khongTheoDoi = AccessPointPingWorker.DsTinhTrangKhongTheoDoi;
            if (ketNoi == "online" || ketNoi == "offline")
            {
                var tt = ketNoi == "online" ? "UP" : "DOWN";
                query = query.Where(x => x.DiaChiIp != null && x.TrangThaiKetNoi == tt
                                         && (x.TinhTrang == null || !khongTheoDoi.Contains(x.TinhTrang)));
            }

            return query.OrderBy(x => x.IdboPhanNavigation != null ? x.IdboPhanNavigation.TenBoPhan : "")
                .ThenBy(x => x.ViTri).ThenBy(x => x.TenAp);
        }

        private static bool TheoDoi(string? ip, string? tinhTrang)
            => !string.IsNullOrWhiteSpace(ip) && (tinhTrang == null || !AccessPointPingWorker.DsTinhTrangKhongTheoDoi.Contains(tinhTrang));

        #endregion

        #region Thêm / Sửa / Xoá

        // Tham số KHÔNG được đặt tên "model": KkAccessPoint có thuộc tính Model, binder sẽ coi ô "Model" của form
        // là tiền tố "model." và bỏ trống mọi trường còn lại.
        [HttpPost("/QLAP/{congTy:regex(^(BPVN|PFVN|MEGA)$)}/Luu")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Luu(string congTy, KkAccessPoint ap)
        {
            var cty = CongTyHopLe(congTy)!;
            if (!CoQuyenSua()) return Json(new { thanhCong = false, thongBao = "Bạn không có quyền thao tác." });

            ap.TenAp = ap.TenAp?.Trim() ?? "";
            if (ap.TenAp.Length == 0)
                return Json(new { thanhCong = false, thongBao = "Vui lòng nhập tên AP." });

            ap.DiaChiIp = string.IsNullOrWhiteSpace(ap.DiaChiIp) ? null : ap.DiaChiIp.Trim();
            if (ap.DiaChiIp != null && !IPAddress.TryParse(ap.DiaChiIp, out _))
                return Json(new { thanhCong = false, thongBao = $"Địa chỉ IP \"{ap.DiaChiIp}\" không hợp lệ." });

            if (!string.IsNullOrWhiteSpace(ap.DiaChiMac))
            {
                var mac = ChuanHoaMac(ap.DiaChiMac);
                if (mac == null)
                    return Json(new { thanhCong = false, thongBao = $"Địa chỉ MAC \"{ap.DiaChiMac.Trim()}\" không hợp lệ." });
                ap.DiaChiMac = mac;
            }
            else ap.DiaChiMac = null;

            if (ap.TinhTrang != null && !DsTinhTrang.Contains(ap.TinhTrang))
                return Json(new { thanhCong = false, thongBao = "Tình trạng không hợp lệ." });

            try
            {
                // Công ty lấy theo trang đang mở, không nhận giá trị client gửi lên
                var idCongTy = await IdCongTyAsync(cty);
                if (idCongTy == null)
                    return Json(new { thanhCong = false, thongBao = $"Chưa có công ty {cty} trong danh mục công ty." });

                if (ap.IdboPhan.HasValue
                    && !await _context.KkBoPhans.AnyAsync(x => x.IdboPhan == ap.IdboPhan && x.IdcongTy == idCongTy))
                    return Json(new { thanhCong = false, thongBao = "Bộ phận không thuộc công ty " + cty + "." });

                // Báo trùng rõ ràng trước; unique index (công ty, IP) ở DB vẫn là chốt cuối khi hai người lưu cùng lúc
                if (ap.DiaChiIp != null)
                {
                    var trung = await _context.KkAccessPoints
                        .Where(x => x.NgayXoa == null && x.IdcongTy == idCongTy && x.DiaChiIp == ap.DiaChiIp && x.IdAp != ap.IdAp)
                        .Select(x => x.TenAp).FirstOrDefaultAsync();
                    if (trung != null)
                        return Json(new { thanhCong = false, thongBao = $"IP {ap.DiaChiIp} đã được gán cho AP \"{trung}\"." });
                }
                if (ap.DiaChiMac != null)
                {
                    var trung = await _context.KkAccessPoints
                        .Where(x => x.NgayXoa == null && x.DiaChiMac == ap.DiaChiMac && x.IdAp != ap.IdAp)
                        .Select(x => x.TenAp).FirstOrDefaultAsync();
                    if (trung != null)
                        return Json(new { thanhCong = false, thongBao = $"MAC {ap.DiaChiMac} đã có ở AP \"{trung}\"." });
                }

                string hanhDong = ap.IdAp == 0 ? "Thêm mới" : "Cập nhật";

                if (ap.IdAp == 0)
                {
                    // AP chưa có IP/MAC thì không có gì để báo trùng: chặn bấm Lưu 2 lần bằng tên + người tạo trong 30 giây
                    if (ap.DiaChiIp == null && ap.DiaChiMac == null)
                    {
                        var moiDay = DateTime.Now.AddSeconds(-30);
                        var nguoiTao = User.Identity?.Name;
                        if (await _context.KkAccessPoints.AnyAsync(x => x.NgayXoa == null && x.IdcongTy == idCongTy
                                && x.TenAp == ap.TenAp && x.NguoiTao == nguoiTao && x.NgayTao >= moiDay))
                            return Json(new { thanhCong = false, thongBao = $"AP \"{ap.TenAp}\" vừa được thêm." });
                    }

                    ap.IdcongTy = idCongTy.Value;
                    ap.NgayTao = DateTime.Now;
                    ap.NguoiTao = User.Identity?.Name;
                    ap.NgayXoa = null;
                    ap.LyDoXoa = null;
                    // Trạng thái kết nối chỉ job ping được ghi
                    ap.TrangThaiKetNoi = null;
                    ap.DoiTrangThaiLuc = null;
                    ap.KiemTraLuc = null;
                    _context.KkAccessPoints.Add(ap);
                }
                else
                {
                    // Chỉ sửa được AP còn hiệu lực của công ty trang đang mở (chặn IDOR qua id gửi lên)
                    var db = await _context.KkAccessPoints
                        .FirstOrDefaultAsync(x => x.IdAp == ap.IdAp && x.IdcongTy == idCongTy && x.NgayXoa == null);
                    if (db == null)
                        return Json(new { thanhCong = false, thongBao = "Không tìm thấy AP cần cập nhật." });

                    // Đổi IP thì trạng thái cũ không còn đúng: để job ping kiểm lại từ đầu, không sinh sự kiện giả
                    if (db.DiaChiIp != ap.DiaChiIp)
                    {
                        db.TrangThaiKetNoi = null;
                        db.DoiTrangThaiLuc = null;
                        db.KiemTraLuc = null;
                    }

                    db.MaAp = ap.MaAp;
                    db.TenAp = ap.TenAp;
                    db.DiaChiIp = ap.DiaChiIp;
                    db.DiaChiMac = ap.DiaChiMac;
                    db.HangSanXuat = ap.HangSanXuat;
                    db.Model = ap.Model;
                    db.Serial = ap.Serial;
                    db.Ssid = ap.Ssid;
                    db.TenController = ap.TenController;
                    db.IdboPhan = ap.IdboPhan;
                    db.ViTri = ap.ViTri;
                    db.TinhTrang = ap.TinhTrang;
                    db.NgayLapDat = ap.NgayLapDat;
                    db.HanBaoHanh = ap.HanBaoHanh;
                    db.GhiChu = ap.GhiChu;
                    db.NgayCapNhat = DateTime.Now;
                }

                await _context.SaveChangesAsync();
                await GhiLichSuAsync(hanhDong, ap.IdAp, $"AP {cty}: {ap.TenAp} (IP: {ap.DiaChiIp ?? "chưa khai"})");

                return Json(new { thanhCong = true, thongBao = $"{hanhDong} AP thành công!" });
            }
            catch (DbUpdateException ex) when (ex.InnerException is SqlException sql && (sql.Number == 2601 || sql.Number == 2627))
            {
                return Json(new { thanhCong = false, thongBao = $"IP {ap.DiaChiIp} vừa được gán cho AP khác." });
            }
            catch (Exception ex) { return Json(new { thanhCong = false, thongBao = ex.Message }); }
        }

        [HttpPost("/QLAP/{congTy:regex(^(BPVN|PFVN|MEGA)$)}/Xoa")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Xoa(string congTy, int id, string? lyDo)
        {
            var cty = CongTyHopLe(congTy)!;
            if (!CoQuyenSua()) return Json(new { thanhCong = false, thongBao = "Bạn không có quyền thao tác." });

            try
            {
                var idCongTy = await IdCongTyAsync(cty);
                var item = await _context.KkAccessPoints
                    .FirstOrDefaultAsync(x => x.IdAp == id && x.IdcongTy == idCongTy && x.NgayXoa == null);
                if (item == null)
                    return Json(new { thanhCong = false, thongBao = "Không tìm thấy AP cần xoá." });

                // Xoá mềm để còn truy vết + giữ lịch sử online/offline
                item.NgayXoa = DateTime.Now;
                item.LyDoXoa = string.IsNullOrWhiteSpace(lyDo) ? "Không ghi lý do" : lyDo.Trim();
                await _context.SaveChangesAsync();

                await GhiLichSuAsync("Xóa", id, $"Đã xoá AP {cty}: {item.TenAp} (IP: {item.DiaChiIp ?? "chưa khai"}). Lý do: {item.LyDoXoa}");
                return Json(new { thanhCong = true, thongBao = "Đã xoá AP!" });
            }
            catch (Exception ex) { return Json(new { thanhCong = false, thongBao = ex.Message }); }
        }

        /// <summary>
        /// Đọc ngay danh sách AP trên controller của công ty và đưa vào bảng (job nền cũng làm mỗi chu kỳ,
        /// nút này để không phải chờ). Idempotent: khoá theo MAC, bấm nhiều lần không sinh dòng trùng.
        /// </summary>
        [HttpPost("/QLAP/{congTy:regex(^(BPVN|PFVN|MEGA)$)}/DongBoController")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DongBoController(string congTy, [FromServices] AccessPointControllerService controller)
        {
            var cty = CongTyHopLe(congTy)!;
            if (!CoQuyenSua()) return Json(new { thanhCong = false, thongBao = "Bạn không có quyền thao tác." });
            if (!controller.CoController(cty))
                return Json(new { thanhCong = false, thongBao = $"Chưa cấu hình AP controller cho {cty}." });

            try
            {
                var dsAc = await controller.DanhSachAsync(cty, HttpContext.RequestAborted);
                var kq = await controller.DongBoAsync(_context, cty, dsAc, HttpContext.RequestAborted);
                if (kq.ThemMoi > 0 || kq.CapNhat > 0)
                    await GhiLichSuAsync("Đồng bộ", 0, $"Đồng bộ AP {cty} từ {controller.TenController(cty)}: {kq.TongTrenController} AP, thêm {kq.ThemMoi}, cập nhật {kq.CapNhat}");
                return Json(new
                {
                    thanhCong = true,
                    thongBao = $"Controller có {kq.TongTrenController} AP — thêm mới {kq.ThemMoi}, cập nhật {kq.CapNhat}. Trạng thái online cập nhật ở lượt kiểm kế tiếp.",
                    duLieu = kq
                });
            }
            catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or InvalidOperationException
                                           or NotSupportedException or System.Xml.XmlException or DbUpdateException or SqlException)
            {
                return Json(new { thanhCong = false, thongBao = "Không đồng bộ được từ controller: " + ex.Message });
            }
        }

        /// <summary>"aa-bb-cc-dd-ee-ff", "aabb.ccdd.eeff", "AABBCCDDEEFF"... -> "AA:BB:CC:DD:EE:FF"; sai định dạng trả null.</summary>
        private static string? ChuanHoaMac(string v)
        {
            var hex = Regex.Replace(v.Trim(), "[^0-9A-Fa-f]", "");
            if (hex.Length != 12 || Regex.IsMatch(v.Trim(), "[^0-9A-Fa-f:.\\- ]")) return null;
            return string.Join(":", Enumerable.Range(0, 6).Select(i => hex.Substring(i * 2, 2))).ToUpperInvariant();
        }

        #endregion

        #region Lịch sử online/offline

        [HttpGet("/QLAP/{congTy:regex(^(BPVN|PFVN|MEGA)$)}/LichSu")]
        public async Task<IActionResult> LichSu(string congTy, DateTime? tuNgay, DateTime? denNgay, string? loai, string? tuKhoa)
        {
            var cty = CongTyHopLe(congTy)!;
            if (!CoQuyenXem(cty)) return Json(new { thanhCong = false, thongBao = "Bạn không có quyền xem dữ liệu này." });

            try
            {
                var (tu, den) = KyBaoCao(tuNgay, denNgay);
                var denHet = den.AddDays(1);
                var idCongTy = await IdCongTyAsync(cty);

                var query = _context.KkAccessPointLichSus
                    .Where(x => x.IdcongTy == idCongTy && x.ThoiGian >= tu && x.ThoiGian < denHet);
                if (loai == "UP" || loai == "DOWN") query = query.Where(x => x.SangTrangThai == loai);
                if (!string.IsNullOrWhiteSpace(tuKhoa))
                {
                    var kw = tuKhoa.Trim();
                    query = query.Where(x => (x.TenAp != null && x.TenAp.Contains(kw)) || (x.DiaChiIp != null && x.DiaChiIp.Contains(kw)));
                }

                const int toiDa = 2000;
                var duLieu = await query
                    .OrderByDescending(x => x.ThoiGian).ThenByDescending(x => x.IdLichSu)
                    .Take(toiDa)
                    .Select(x => new { x.ThoiGian, x.IdAp, x.TenAp, x.DiaChiIp, x.TuTrangThai, x.SangTrangThai, x.ThoiLuongGiay })
                    .ToListAsync();

                return Json(new
                {
                    thanhCong = true,
                    duLieu,
                    tong = duLieu.Count,
                    soDown = duLieu.Count(x => x.SangTrangThai == "DOWN"),
                    soUp = duLieu.Count(x => x.SangTrangThai == "UP"),
                    soAp = duLieu.Select(x => x.IdAp).Distinct().Count(),
                    biCat = duLieu.Count == toiDa
                });
            }
            catch (Exception ex) { return Json(new { thanhCong = false, thongBao = ex.Message }); }
        }

        #endregion

        #region Tổng quan / báo cáo

        /// <summary>
        /// Công ty được đưa vào báo cáo:
        ///  - Tổng quan (không truyền congTy): mọi công ty được xem; AdminIT xem tổng quan cả 3.
        ///  - Báo cáo riêng 1 công ty: cần quyền xem công ty đó hoặc AdminIT.
        /// Trả null khi công ty gửi lên không hợp lệ.
        /// </summary>
        private List<string>? DsCongTyBaoCao(string? congTy)
        {
            var laAdminIt = User.IsInRole("AdminIT");
            if (string.IsNullOrWhiteSpace(congTy))
                return AccessPointPingWorker.DsCongTy.Where(x => laAdminIt || CoQuyenXem(x)).ToList();

            var cty = CongTyHopLe(congTy);
            if (cty == null) return null;
            return laAdminIt || CoQuyenXem(cty) ? new List<string> { cty } : new List<string>();
        }

        /// <summary>Kỳ: mặc định 7 ngày gần nhất; tối đa 366 ngày cho nhẹ truy vấn lịch sử.</summary>
        private static (DateTime tu, DateTime den) KyBaoCao(DateTime? tuNgay, DateTime? denNgay)
        {
            var den = (denNgay ?? DateTime.Today).Date;
            var tu = (tuNgay ?? den.AddDays(-6)).Date;
            if (tu > den) (tu, den) = (den, tu);
            if ((den - tu).TotalDays > 366) tu = den.AddDays(-366);
            return (tu, den);
        }

        [HttpGet("/QLAP/TongQuan")]
        public IActionResult TongQuan(string? congTy)
        {
            if (User?.Identity?.IsAuthenticated != true)
                return Redirect("/DonXetDuyet/DangNhap");

            var ds = DsCongTyBaoCao(congTy);
            if (ds == null) return NotFound();
            if (ds.Count == 0) return Forbid();

            ViewBag.CongTyBaoCao = string.IsNullOrWhiteSpace(congTy) ? null : ds[0];
            // Công ty chọn được trong ô "Phạm vi"; server vẫn kiểm lại ở DuLieu/XuatExcel
            ViewBag.DsCongTyChon = DsCongTyBaoCao(null) ?? new List<string>();
            return View();
        }

        [HttpGet("/QLAP/TongQuan/DuLieu")]
        public async Task<IActionResult> TongQuanDuLieu(DateTime? tuNgay, DateTime? denNgay, string? congTy, [FromServices] AccessPointBaoCaoService baoCao)
        {
            var dsCongTy = DsCongTyBaoCao(congTy);
            if (dsCongTy == null || dsCongTy.Count == 0) return Json(new { thanhCong = false, thongBao = "Bạn không có quyền xem dữ liệu này." });

            try
            {
                var (tu, den) = KyBaoCao(tuNgay, denNgay);
                var duLieu = await baoCao.TaoAsync(dsCongTy, tu, den, HttpContext.RequestAborted);
                // Công ty mở được trang chi tiết — thẻ công ty khác chỉ hiện số, không thành link
                var moDuoc = AccessPointPingWorker.DsCongTy.Where(CoQuyenXem).ToList();
                return Json(new { thanhCong = true, duLieu, moDuoc });
            }
            catch (Exception ex) when (ex is SqlException or InvalidOperationException)
            {
                return Json(new { thanhCong = false, thongBao = "Không tạo được báo cáo: " + ex.Message });
            }
        }

        [HttpGet("/QLAP/TongQuan/XuatExcel")]
        public async Task<IActionResult> TongQuanXuatExcel(DateTime? tuNgay, DateTime? denNgay, string? congTy, [FromServices] AccessPointBaoCaoService baoCao)
        {
            var dsCongTy = DsCongTyBaoCao(congTy);
            if (dsCongTy == null) return NotFound();
            if (dsCongTy.Count == 0) return StatusCode(403);

            var (tu, den) = KyBaoCao(tuNgay, denNgay);
            try
            {
                var bc = await baoCao.TaoAsync(dsCongTy, tu, den, HttpContext.RequestAborted);
                var motCongTy = string.IsNullOrWhiteSpace(congTy) ? null : dsCongTy[0];
                var file = AccessPointBaoCaoExcel.TaoBaoCao(bc, motCongTy, User.Identity?.Name);
                return File(file, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                    $"BaoCaoAP_{motCongTy ?? "TongQuan"}_{tu:yyyyMMdd}-{den:yyyyMMdd}.xlsx");
            }
            catch (SqlException ex)
            {
                return Content("Không tạo được báo cáo: " + ex.Message);
            }
        }

        #endregion

        /// <summary>Ghi vào KK_LichSuThaoTac để mọi thao tác thêm/sửa/xoá AP đều có vết.</summary>
        private async Task GhiLichSuAsync(string hanhDong, int idDoiTuong, string chiTiet)
        {
            try
            {
                _context.KkLichSuThaoTacs.Add(new KkLichSuThaoTac
                {
                    HanhDong = hanhDong,
                    DoiTuong = "Access Point",
                    IdDoiTuong = idDoiTuong,
                    ChiTiet = chiTiet,
                    ThoiGian = DateTime.Now,
                    NguoiThaoTac = User.Identity?.Name ?? "Hệ thống"
                });
                await _context.SaveChangesAsync();
            }
            catch { /* Bỏ qua lỗi ghi log để không làm gián đoạn luồng chính (cùng cách QLCamera) */ }
        }
    }
}
