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
    /// Quản lý Switch 3 công ty (cùng khuôn "Quản lý AP"): mỗi công ty một trang /QLSwitch/{công ty}
    /// gồm danh sách tài sản + lịch sử online/offline, và trang /QLSwitch/TongQuan báo cáo theo kỳ.
    /// Trạng thái kết nối do SwitchPingWorker ghi, controller chỉ đọc.
    /// </summary>
    [Area("ITform")]
    public class QLSwitchController : Controller
    {
        private readonly ITFormContext _context;
        private readonly IConfiguration _configuration;

        // Danh mục cố định, đổi ở đây là view + lọc ăn theo
        public static readonly string[] DsTinhTrang = { "Đang hoạt động", "Hư hỏng", "Ngừng sử dụng", "Trong kho" };
        public static readonly string[] DsVaiTro = { "Core", "Phân phối", "Truy cập" };

        public QLSwitchController(ITFormContext context, IConfiguration configuration)
        {
            _context = context;
            _configuration = configuration;
        }

        // "All" toàn quyền switch cả 3 công ty; "SwBPVN" / "SwPFVN" / "SwMEGA" chỉ được xem switch công ty đó.
        // Ẩn nút ở UI không phải phân quyền nên mọi action đều phải gọi.
        private bool CoQuyenXem(string congTy) => User.IsInRole("All") || User.IsInRole("Sw" + congTy.ToUpperInvariant());
        private bool CoQuyenSua() => User.IsInRole("All");

        private static string? CongTyHopLe(string? congTy)
            => SwitchPingWorker.DsCongTy.FirstOrDefault(x => x.Equals(congTy?.Trim(), StringComparison.OrdinalIgnoreCase));

        private Task<int?> IdCongTyAsync(string congTy)
            => _context.KkCongTies.Where(x => x.TenCongTy == congTy).Select(x => (int?)x.IdcongTy).FirstOrDefaultAsync();

        #region View

        [HttpGet("/QLSwitch/{congTy:regex(^(BPVN|PFVN|MEGA)$)}")]
        public async Task<IActionResult> Index(string congTy)
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
            ViewBag.DsVaiTro = DsVaiTro;
            ViewBag.CoQuyenSua = CoQuyenSua();
            // Công ty có cấu hình dải quét máy chủ thì hiện thêm tab "Máy chủ"
            ViewBag.DaiQuet = QuetIpWorker.DsDaiQuet(_configuration).GetValueOrDefault(cty);
            return View();
        }

        #endregion

        #region Danh sách

        [HttpGet("/QLSwitch/{congTy:regex(^(BPVN|PFVN|MEGA)$)}/DanhSach")]
        public async Task<IActionResult> DanhSach(string congTy, string? tuKhoa, int? idBoPhan, string? tinhTrang, string? ketNoi,
            [FromServices] CameraGiamSatService giamSat)
        {
            var cty = CongTyHopLe(congTy)!;
            if (!CoQuyenXem(cty)) return Json(new { thanhCong = false, thongBao = "Bạn không có quyền xem dữ liệu này." });

            // SNMP switch chỉ BPVN có (hệ thống giám sát 10.0.60.238 poll). Lỗi đọc không làm hỏng danh sách:
            // trả rỗng + snmpLoi để trang báo nhẹ
            var snmp = new Dictionary<string, CameraGiamSatService.SnmpSwitch>();
            string? snmpLoi = null;
            if (cty == "BPVN")
            {
                try { snmp = await giamSat.SnmpSwitchTheoIpAsync(HttpContext.RequestAborted); }
                catch (Exception ex) when (ex is not OperationCanceledException || !HttpContext.RequestAborted.IsCancellationRequested)
                { snmpLoi = "Không đọc được số liệu SNMP từ hệ thống giám sát: " + ex.Message; }
            }

            try
            {
                var ds = await TruyVanDanhSach(await IdCongTyAsync(cty), tuKhoa, idBoPhan, tinhTrang, ketNoi)
                    .Select(x => new
                    {
                        x.IdSwitch, x.MaSwitch, x.TenSwitch, x.DiaChiIp, x.DiaChiMac, x.HangSanXuat, x.Model, x.Serial,
                        x.SoCong, x.VaiTro, x.PhienBan, x.IdboPhan,
                        TenBoPhan = x.IdboPhanNavigation != null ? x.IdboPhanNavigation.TenBoPhan : "",
                        x.ViTri, x.TinhTrang, x.NgayLapDat, x.HanBaoHanh, x.GhiChu,
                        x.TrangThaiKetNoi, x.DoiTrangThaiLuc, x.KiemTraLuc
                    })
                    .ToListAsync();

                // switch không ping (chưa khai IP / trong kho / ngừng dùng) hiện "Không theo dõi" thay vì trạng thái cũ
                var duLieu = ds.Select(x => new
                {
                    x.IdSwitch, x.MaSwitch, x.TenSwitch, x.DiaChiIp, x.DiaChiMac, x.HangSanXuat, x.Model, x.Serial,
                    x.SoCong, x.VaiTro, x.PhienBan, x.IdboPhan, x.TenBoPhan, x.ViTri, x.TinhTrang, x.NgayLapDat, x.HanBaoHanh, x.GhiChu,
                    TheoDoi = TheoDoi(x.DiaChiIp, x.TinhTrang),
                    KetNoi = TheoDoi(x.DiaChiIp, x.TinhTrang) ? x.TrangThaiKetNoi : null,
                    x.DoiTrangThaiLuc, x.KiemTraLuc,
                    Snmp = x.DiaChiIp != null ? snmp.GetValueOrDefault(x.DiaChiIp.Trim()) : null
                }).ToList();

                return Json(new
                {
                    thanhCong = true,
                    duLieu,
                    coSnmp = cty == "BPVN",
                    snmpLoi,
                    tongDong = duLieu.Count,
                    tongOnline = duLieu.Count(x => x.KetNoi == "UP"),
                    tongOffline = duLieu.Count(x => x.KetNoi == "DOWN"),
                    // Lần ping gần nhất: quá lâu thì trang báo job nền không chạy ở máy chủ này
                    kiemTraGanNhat = duLieu.Max(x => x.KiemTraLuc),
                    chuKyGiay = Math.Clamp(_configuration.GetValue<int?>("Switch:ChuKyGiay") ?? 120, 60, 3600)
                });
            }
            catch (Exception ex) { return Json(new { thanhCong = false, thongBao = ex.Message }); }
        }

        /// <summary>Danh sách switch 1 công ty ra Excel, theo đúng bộ lọc đang chọn trên trang.</summary>
        [HttpGet("/QLSwitch/{congTy:regex(^(BPVN|PFVN|MEGA)$)}/XuatExcel")]
        public async Task<IActionResult> XuatExcel(string congTy, string? tuKhoa, int? idBoPhan, string? tinhTrang, string? ketNoi)
        {
            var cty = CongTyHopLe(congTy)!;
            if (!CoQuyenXem(cty)) return StatusCode(403);

            var ds = await TruyVanDanhSach(await IdCongTyAsync(cty), tuKhoa, idBoPhan, tinhTrang, ketNoi)
                .Select(x => new
                {
                    x.MaSwitch, x.TenSwitch, x.DiaChiIp, x.DiaChiMac, x.HangSanXuat, x.Model, x.Serial, x.SoCong, x.VaiTro,
                    TenBoPhan = x.IdboPhanNavigation != null ? x.IdboPhanNavigation.TenBoPhan : null,
                    x.ViTri, x.TinhTrang, x.TrangThaiKetNoi, x.KiemTraLuc, x.NgayLapDat, x.HanBaoHanh, x.GhiChu
                })
                .ToListAsync();

            var dong = ds.Select(x => new SwitchBaoCaoExcel.DongDanhSach(
                x.MaSwitch, x.TenSwitch, x.DiaChiIp, x.DiaChiMac,
                string.Join(" / ", new[] { x.HangSanXuat, x.Model }.Where(s => !string.IsNullOrWhiteSpace(s))),
                x.Serial, x.SoCong?.ToString(), x.VaiTro, x.TenBoPhan, x.ViTri, x.TinhTrang,
                !TheoDoi(x.DiaChiIp, x.TinhTrang) ? "Không theo dõi"
                    : x.TrangThaiKetNoi == "UP" ? "Online" : x.TrangThaiKetNoi == "DOWN" ? "Mất KN" : "Chưa kiểm",
                x.KiemTraLuc, x.NgayLapDat, x.HanBaoHanh, x.GhiChu)).ToList();

            var file = SwitchBaoCaoExcel.TaoDanhSach(dong, cty, User.Identity?.Name);
            return File(file, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                $"DanhSachSwitch_{cty}_{DateTime.Now:yyyyMMdd}.xlsx");
        }

        private IQueryable<KkSwitch> TruyVanDanhSach(int? idCongTy, string? tuKhoa, int? idBoPhan, string? tinhTrang, string? ketNoi)
        {
            var query = _context.KkSwitches.Where(x => x.NgayXoa == null && x.IdcongTy == idCongTy);

            if (!string.IsNullOrWhiteSpace(tuKhoa))
            {
                var kw = tuKhoa.Trim();
                query = query.Where(x =>
                    x.TenSwitch.Contains(kw) ||
                    (x.MaSwitch != null && x.MaSwitch.Contains(kw)) ||
                    (x.DiaChiIp != null && x.DiaChiIp.Contains(kw)) ||
                    (x.DiaChiMac != null && x.DiaChiMac.Contains(kw)) ||
                    (x.HangSanXuat != null && x.HangSanXuat.Contains(kw)) ||
                    (x.Model != null && x.Model.Contains(kw)) ||
                    (x.Serial != null && x.Serial.Contains(kw)) ||
                    (x.VaiTro != null && x.VaiTro.Contains(kw)) ||
                    (x.ViTri != null && x.ViTri.Contains(kw)));
            }
            if (idBoPhan > 0) query = query.Where(x => x.IdboPhan == idBoPhan);
            if (!string.IsNullOrWhiteSpace(tinhTrang)) query = query.Where(x => x.TinhTrang == tinhTrang);

            // Lọc kết nối chỉ xét switch đang theo dõi (có IP, không trong kho / ngừng dùng)
            var khongTheoDoi = SwitchPingWorker.DsTinhTrangKhongTheoDoi;
            if (ketNoi == "online" || ketNoi == "offline")
            {
                var tt = ketNoi == "online" ? "UP" : "DOWN";
                query = query.Where(x => x.DiaChiIp != null && x.TrangThaiKetNoi == tt
                                         && (x.TinhTrang == null || !khongTheoDoi.Contains(x.TinhTrang)));
            }

            return query.OrderBy(x => x.IdboPhanNavigation != null ? x.IdboPhanNavigation.TenBoPhan : "")
                .ThenBy(x => x.ViTri).ThenBy(x => x.TenSwitch);
        }

        private static bool TheoDoi(string? ip, string? tinhTrang)
            => !string.IsNullOrWhiteSpace(ip) && (tinhTrang == null || !SwitchPingWorker.DsTinhTrangKhongTheoDoi.Contains(tinhTrang));

        #endregion

        #region Thêm / Sửa / Xoá

        // Tham số KHÔNG được đặt tên "model": KkSwitch có thuộc tính Model, binder sẽ coi ô "Model" của form
        // là tiền tố "model." và bỏ trống mọi trường còn lại.
        [HttpPost("/QLSwitch/{congTy:regex(^(BPVN|PFVN|MEGA)$)}/Luu")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Luu(string congTy, KkSwitch sw)
        {
            var cty = CongTyHopLe(congTy)!;
            if (!CoQuyenSua()) return Json(new { thanhCong = false, thongBao = "Bạn không có quyền thao tác." });

            sw.TenSwitch = sw.TenSwitch?.Trim() ?? "";
            if (sw.TenSwitch.Length == 0)
                return Json(new { thanhCong = false, thongBao = "Vui lòng nhập tên switch." });

            sw.DiaChiIp = string.IsNullOrWhiteSpace(sw.DiaChiIp) ? null : sw.DiaChiIp.Trim();
            if (sw.DiaChiIp != null && !IPAddress.TryParse(sw.DiaChiIp, out _))
                return Json(new { thanhCong = false, thongBao = $"Địa chỉ IP \"{sw.DiaChiIp}\" không hợp lệ." });

            if (!string.IsNullOrWhiteSpace(sw.DiaChiMac))
            {
                var mac = ChuanHoaMac(sw.DiaChiMac);
                if (mac == null)
                    return Json(new { thanhCong = false, thongBao = $"Địa chỉ MAC \"{sw.DiaChiMac.Trim()}\" không hợp lệ." });
                sw.DiaChiMac = mac;
            }
            else sw.DiaChiMac = null;

            if (sw.TinhTrang != null && !DsTinhTrang.Contains(sw.TinhTrang))
                return Json(new { thanhCong = false, thongBao = "Tình trạng không hợp lệ." });
            if (!string.IsNullOrEmpty(sw.VaiTro) && !DsVaiTro.Contains(sw.VaiTro))
                return Json(new { thanhCong = false, thongBao = "Vai trò không hợp lệ." });
            if (string.IsNullOrEmpty(sw.VaiTro)) sw.VaiTro = null;
            if (sw.SoCong is < 1 or > 1000)
                return Json(new { thanhCong = false, thongBao = "Số cổng phải từ 1 đến 1000." });

            try
            {
                // Công ty lấy theo trang đang mở, không nhận giá trị client gửi lên
                var idCongTy = await IdCongTyAsync(cty);
                if (idCongTy == null)
                    return Json(new { thanhCong = false, thongBao = $"Chưa có công ty {cty} trong danh mục công ty." });

                if (sw.IdboPhan.HasValue
                    && !await _context.KkBoPhans.AnyAsync(x => x.IdboPhan == sw.IdboPhan && x.IdcongTy == idCongTy))
                    return Json(new { thanhCong = false, thongBao = "Bộ phận không thuộc công ty " + cty + "." });

                // Báo trùng rõ ràng trước; unique index (công ty, IP) ở DB vẫn là chốt cuối khi hai người lưu cùng lúc
                if (sw.DiaChiIp != null)
                {
                    var trung = await _context.KkSwitches
                        .Where(x => x.NgayXoa == null && x.IdcongTy == idCongTy && x.DiaChiIp == sw.DiaChiIp && x.IdSwitch != sw.IdSwitch)
                        .Select(x => x.TenSwitch).FirstOrDefaultAsync();
                    if (trung != null)
                        return Json(new { thanhCong = false, thongBao = $"IP {sw.DiaChiIp} đã được gán cho switch \"{trung}\"." });
                }
                if (sw.DiaChiMac != null)
                {
                    var trung = await _context.KkSwitches
                        .Where(x => x.NgayXoa == null && x.DiaChiMac == sw.DiaChiMac && x.IdSwitch != sw.IdSwitch)
                        .Select(x => x.TenSwitch).FirstOrDefaultAsync();
                    if (trung != null)
                        return Json(new { thanhCong = false, thongBao = $"MAC {sw.DiaChiMac} đã có ở switch \"{trung}\"." });
                }

                string hanhDong = sw.IdSwitch == 0 ? "Thêm mới" : "Cập nhật";

                if (sw.IdSwitch == 0)
                {
                    // switch chưa có IP/MAC thì không có gì để báo trùng: chặn bấm Lưu 2 lần bằng tên + người tạo trong 30 giây
                    if (sw.DiaChiIp == null && sw.DiaChiMac == null)
                    {
                        var moiDay = DateTime.Now.AddSeconds(-30);
                        var nguoiTao = User.Identity?.Name;
                        if (await _context.KkSwitches.AnyAsync(x => x.NgayXoa == null && x.IdcongTy == idCongTy
                                && x.TenSwitch == sw.TenSwitch && x.NguoiTao == nguoiTao && x.NgayTao >= moiDay))
                            return Json(new { thanhCong = false, thongBao = $"Switch \"{sw.TenSwitch}\" vừa được thêm." });
                    }

                    sw.IdcongTy = idCongTy.Value;
                    sw.NgayTao = DateTime.Now;
                    sw.NguoiTao = User.Identity?.Name;
                    sw.NgayXoa = null;
                    sw.LyDoXoa = null;
                    // Trạng thái kết nối chỉ job ping được ghi
                    sw.TrangThaiKetNoi = null;
                    sw.DoiTrangThaiLuc = null;
                    sw.KiemTraLuc = null;
                    _context.KkSwitches.Add(sw);
                }
                else
                {
                    // Chỉ sửa được switch còn hiệu lực của công ty trang đang mở (chặn IDOR qua id gửi lên)
                    var db = await _context.KkSwitches
                        .FirstOrDefaultAsync(x => x.IdSwitch == sw.IdSwitch && x.IdcongTy == idCongTy && x.NgayXoa == null);
                    if (db == null)
                        return Json(new { thanhCong = false, thongBao = "Không tìm thấy switch cần cập nhật." });

                    // Đổi IP thì trạng thái cũ không còn đúng: để job ping kiểm lại từ đầu, không sinh sự kiện giả
                    if (db.DiaChiIp != sw.DiaChiIp)
                    {
                        db.TrangThaiKetNoi = null;
                        db.DoiTrangThaiLuc = null;
                        db.KiemTraLuc = null;
                    }

                    db.MaSwitch = sw.MaSwitch;
                    db.TenSwitch = sw.TenSwitch;
                    db.DiaChiIp = sw.DiaChiIp;
                    db.DiaChiMac = sw.DiaChiMac;
                    db.HangSanXuat = sw.HangSanXuat;
                    db.Model = sw.Model;
                    db.Serial = sw.Serial;
                    db.SoCong = sw.SoCong;
                    db.VaiTro = sw.VaiTro;
                    db.PhienBan = sw.PhienBan;
                    db.IdboPhan = sw.IdboPhan;
                    db.ViTri = sw.ViTri;
                    db.TinhTrang = sw.TinhTrang;
                    db.NgayLapDat = sw.NgayLapDat;
                    db.HanBaoHanh = sw.HanBaoHanh;
                    db.GhiChu = sw.GhiChu;
                    db.NgayCapNhat = DateTime.Now;
                }

                await _context.SaveChangesAsync();
                await GhiLichSuAsync(hanhDong, sw.IdSwitch, $"Switch {cty}: {sw.TenSwitch} (IP: {sw.DiaChiIp ?? "chưa khai"})");

                return Json(new { thanhCong = true, thongBao = $"{hanhDong} switch thành công!" });
            }
            catch (DbUpdateException ex) when (ex.InnerException is SqlException sql && (sql.Number == 2601 || sql.Number == 2627))
            {
                return Json(new { thanhCong = false, thongBao = $"IP {sw.DiaChiIp} vừa được gán cho switch khác." });
            }
            catch (Exception ex) { return Json(new { thanhCong = false, thongBao = ex.Message }); }
        }

        [HttpPost("/QLSwitch/{congTy:regex(^(BPVN|PFVN|MEGA)$)}/Xoa")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Xoa(string congTy, int id, string? lyDo)
        {
            var cty = CongTyHopLe(congTy)!;
            if (!CoQuyenSua()) return Json(new { thanhCong = false, thongBao = "Bạn không có quyền thao tác." });

            try
            {
                var idCongTy = await IdCongTyAsync(cty);
                var item = await _context.KkSwitches
                    .FirstOrDefaultAsync(x => x.IdSwitch == id && x.IdcongTy == idCongTy && x.NgayXoa == null);
                if (item == null)
                    return Json(new { thanhCong = false, thongBao = "Không tìm thấy switch cần xoá." });

                // Xoá mềm để còn truy vết + giữ lịch sử online/offline
                item.NgayXoa = DateTime.Now;
                item.LyDoXoa = string.IsNullOrWhiteSpace(lyDo) ? "Không ghi lý do" : lyDo.Trim();
                await _context.SaveChangesAsync();

                await GhiLichSuAsync("Xóa", id, $"Đã xoá switch {cty}: {item.TenSwitch} (IP: {item.DiaChiIp ?? "chưa khai"}). Lý do: {item.LyDoXoa}");
                return Json(new { thanhCong = true, thongBao = "Đã xoá switch!" });
            }
            catch (Exception ex) { return Json(new { thanhCong = false, thongBao = ex.Message }); }
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

        [HttpGet("/QLSwitch/{congTy:regex(^(BPVN|PFVN|MEGA)$)}/LichSu")]
        public async Task<IActionResult> LichSu(string congTy, DateTime? tuNgay, DateTime? denNgay, string? loai, string? tuKhoa)
        {
            var cty = CongTyHopLe(congTy)!;
            if (!CoQuyenXem(cty)) return Json(new { thanhCong = false, thongBao = "Bạn không có quyền xem dữ liệu này." });

            try
            {
                var (tu, den) = KyBaoCao(tuNgay, denNgay);
                var denHet = den.AddDays(1);
                var idCongTy = await IdCongTyAsync(cty);

                var query = _context.KkSwitchLichSus
                    .Where(x => x.IdcongTy == idCongTy && x.ThoiGian >= tu && x.ThoiGian < denHet);
                if (loai == "UP" || loai == "DOWN") query = query.Where(x => x.SangTrangThai == loai);
                if (!string.IsNullOrWhiteSpace(tuKhoa))
                {
                    var kw = tuKhoa.Trim();
                    query = query.Where(x => (x.TenSwitch != null && x.TenSwitch.Contains(kw)) || (x.DiaChiIp != null && x.DiaChiIp.Contains(kw)));
                }

                const int toiDa = 2000;
                var duLieu = await query
                    .OrderByDescending(x => x.ThoiGian).ThenByDescending(x => x.IdLichSu)
                    .Take(toiDa)
                    .Select(x => new { x.ThoiGian, x.IdSwitch, x.TenSwitch, x.DiaChiIp, x.TuTrangThai, x.SangTrangThai, x.ThoiLuongGiay })
                    .ToListAsync();

                return Json(new
                {
                    thanhCong = true,
                    duLieu,
                    tong = duLieu.Count,
                    soDown = duLieu.Count(x => x.SangTrangThai == "DOWN"),
                    soUp = duLieu.Count(x => x.SangTrangThai == "UP"),
                    soSwitch = duLieu.Select(x => x.IdSwitch).Distinct().Count(),
                    biCat = duLieu.Count == toiDa
                });
            }
            catch (Exception ex) { return Json(new { thanhCong = false, thongBao = ex.Message }); }
        }

        #endregion

        #region Máy chủ (quét dải IP)

        /// <summary>
        /// IP trong dải quét của công ty (KK_QuetIp, do QuetIpWorker ghi) kèm đối chiếu khai báo:
        /// IP đó đã có trong Switch / AP / thiết bị kiểm kê chưa — IP sống mà "Chưa khai báo" là máy lạ cần rà.
        /// </summary>
        [HttpGet("/QLSwitch/{congTy:regex(^(BPVN|PFVN|MEGA)$)}/MayChu/DanhSach")]
        public async Task<IActionResult> MayChuDanhSach(string congTy, string? tuKhoa, string? ketNoi, string? khaiBao)
        {
            var cty = CongTyHopLe(congTy)!;
            if (!CoQuyenXem(cty)) return Json(new { thanhCong = false, thongBao = "Bạn không có quyền xem dữ liệu này." });

            var dai = QuetIpWorker.DsDaiQuet(_configuration).GetValueOrDefault(cty);
            if (dai == null) return Json(new { thanhCong = false, thongBao = $"Chưa cấu hình dải quét máy chủ cho {cty} (QuetIp:DaiMang)." });

            try
            {
                var idCongTy = await IdCongTyAsync(cty);
                var tienTo = dai + ".";

                var ds = await _context.KkQuetIps
                    .Where(x => x.IdcongTy == idCongTy && x.DaiMang == dai)
                    .Select(x => new { x.IdQuetIp, x.DiaChiIp, x.TenMay, x.TrangThaiKetNoi, x.DoiTrangThaiLuc, x.KiemTraLuc, x.LanDauThay, x.LanCuoiSong })
                    .ToListAsync();

                // Đối chiếu khai báo: dải chỉ 254 IP nên kéo hết về rồi ghép trong bộ nhớ
                var khaiBaoTheoIp = new Dictionary<string, List<string>>();
                void Them(string? ip, string nhan)
                {
                    if (string.IsNullOrWhiteSpace(ip)) return;
                    // Cột ip thiết bị có thể chứa nhiều IP ("a, b") -> tách ra so khớp đúng từng IP
                    foreach (var mot in ip.Split(new[] { ',', ';', ' ', '|' }, StringSplitOptions.RemoveEmptyEntries))
                    {
                        if (!mot.StartsWith(tienTo)) continue;
                        if (!khaiBaoTheoIp.TryGetValue(mot, out var l)) khaiBaoTheoIp[mot] = l = new List<string>();
                        if (!l.Contains(nhan)) l.Add(nhan);
                    }
                }

                foreach (var x in await _context.KkSwitches
                             .Where(x => x.NgayXoa == null && x.IdcongTy == idCongTy && x.DiaChiIp != null && x.DiaChiIp.StartsWith(tienTo))
                             .Select(x => new { x.DiaChiIp, x.TenSwitch }).ToListAsync())
                    Them(x.DiaChiIp, "Switch: " + x.TenSwitch);
                foreach (var x in await _context.KkAccessPoints
                             .Where(x => x.NgayXoa == null && x.IdcongTy == idCongTy && x.DiaChiIp != null && x.DiaChiIp.StartsWith(tienTo))
                             .Select(x => new { x.DiaChiIp, x.TenAp }).ToListAsync())
                    Them(x.DiaChiIp, "AP: " + x.TenAp);
                foreach (var x in await _context.KkThietBis
                             .Where(x => x.NgayXoa == null && x.Ip != null && x.Ip.Contains(tienTo))
                             .Select(x => new { x.Ip, x.TenMayTinh, x.LoaiThietBi }).ToListAsync())
                    Them(x.Ip, (string.IsNullOrWhiteSpace(x.LoaiThietBi) ? "Thiết bị" : x.LoaiThietBi) + ": " + (x.TenMayTinh ?? ""));

                var duLieu = ds
                    .Select(x => new
                    {
                        x.IdQuetIp, x.DiaChiIp, x.TenMay, KetNoi = x.TrangThaiKetNoi, x.DoiTrangThaiLuc, x.KiemTraLuc, x.LanDauThay, x.LanCuoiSong,
                        KhaiBao = khaiBaoTheoIp.TryGetValue(x.DiaChiIp, out var l) ? string.Join(" · ", l) : null,
                        SoCuoi = int.TryParse(x.DiaChiIp[(x.DiaChiIp.LastIndexOf('.') + 1)..], out var n) ? n : 0
                    })
                    .OrderBy(x => x.SoCuoi)
                    .ToList();

                // Tổng đếm trên cả dải, bộ lọc chỉ áp vào bảng
                var tongDong = duLieu.Count;
                var tongOnline = duLieu.Count(x => x.KetNoi == "UP");
                var tongOffline = duLieu.Count(x => x.KetNoi == "DOWN");
                var tongChuaKhaiBao = duLieu.Count(x => x.KhaiBao == null);

                if (ketNoi == "UP" || ketNoi == "DOWN") duLieu = duLieu.Where(x => x.KetNoi == ketNoi).ToList();
                if (khaiBao == "co") duLieu = duLieu.Where(x => x.KhaiBao != null).ToList();
                else if (khaiBao == "chua") duLieu = duLieu.Where(x => x.KhaiBao == null).ToList();
                if (!string.IsNullOrWhiteSpace(tuKhoa))
                {
                    var kw = tuKhoa.Trim();
                    duLieu = duLieu.Where(x => x.DiaChiIp.Contains(kw, StringComparison.OrdinalIgnoreCase)
                                               || (x.TenMay?.Contains(kw, StringComparison.OrdinalIgnoreCase) ?? false)
                                               || (x.KhaiBao?.Contains(kw, StringComparison.OrdinalIgnoreCase) ?? false)).ToList();
                }

                return Json(new
                {
                    thanhCong = true,
                    duLieu,
                    daiMang = dai,
                    tongDong, tongOnline, tongOffline, tongChuaKhaiBao,
                    kiemTraGanNhat = ds.Max(x => x.KiemTraLuc),
                    chuKyGiay = QuetIpWorker.ChuKyGiay(_configuration)
                });
            }
            catch (Exception ex) { return Json(new { thanhCong = false, thongBao = ex.Message }); }
        }

        [HttpGet("/QLSwitch/{congTy:regex(^(BPVN|PFVN|MEGA)$)}/MayChu/LichSu")]
        public async Task<IActionResult> MayChuLichSu(string congTy, DateTime? tuNgay, DateTime? denNgay, string? loai, string? tuKhoa)
        {
            var cty = CongTyHopLe(congTy)!;
            if (!CoQuyenXem(cty)) return Json(new { thanhCong = false, thongBao = "Bạn không có quyền xem dữ liệu này." });

            try
            {
                var (tu, den) = KyBaoCao(tuNgay, denNgay);
                var denHet = den.AddDays(1);
                var idCongTy = await IdCongTyAsync(cty);

                var query = _context.KkQuetIpLichSus
                    .Where(x => x.IdcongTy == idCongTy && x.ThoiGian >= tu && x.ThoiGian < denHet);
                if (loai == "UP" || loai == "DOWN") query = query.Where(x => x.SangTrangThai == loai);
                if (!string.IsNullOrWhiteSpace(tuKhoa))
                {
                    var kw = tuKhoa.Trim();
                    query = query.Where(x => (x.TenMay != null && x.TenMay.Contains(kw)) || (x.DiaChiIp != null && x.DiaChiIp.Contains(kw)));
                }

                const int toiDa = 2000;
                var duLieu = await query
                    .OrderByDescending(x => x.ThoiGian).ThenByDescending(x => x.IdLichSu)
                    .Take(toiDa)
                    .Select(x => new { x.ThoiGian, x.IdQuetIp, x.TenMay, x.DiaChiIp, x.TuTrangThai, x.SangTrangThai, x.ThoiLuongGiay })
                    .ToListAsync();

                return Json(new
                {
                    thanhCong = true,
                    duLieu,
                    tong = duLieu.Count,
                    soDown = duLieu.Count(x => x.SangTrangThai == "DOWN"),
                    soUp = duLieu.Count(x => x.SangTrangThai == "UP"),
                    soMay = duLieu.Select(x => x.IdQuetIp).Distinct().Count(),
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
            // Quyền "BaoCaoDinhKy" xem tổng quan cả 3 công ty như AdminIT (tab Switch của /BaoCaoDinhKy)
            var laAdminIt = User.IsInRole("AdminIT") || User.IsInRole("BaoCaoDinhKy");
            if (string.IsNullOrWhiteSpace(congTy))
                return SwitchPingWorker.DsCongTy.Where(x => laAdminIt || CoQuyenXem(x)).ToList();

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

        [HttpGet("/QLSwitch/TongQuan")]
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
            // Trang "Báo cáo định kì" nạp phần thân làm 1 tab (fetch kèm X-Requested-With), không kèm layout
            if (Request.Headers.XRequestedWith == "XMLHttpRequest") return PartialView("_TongQuanNoiDung");
            return View();
        }

        [HttpGet("/QLSwitch/TongQuan/DuLieu")]
        public async Task<IActionResult> TongQuanDuLieu(DateTime? tuNgay, DateTime? denNgay, string? congTy, [FromServices] SwitchBaoCaoService baoCao)
        {
            var dsCongTy = DsCongTyBaoCao(congTy);
            if (dsCongTy == null || dsCongTy.Count == 0) return Json(new { thanhCong = false, thongBao = "Bạn không có quyền xem dữ liệu này." });

            try
            {
                var (tu, den) = KyBaoCao(tuNgay, denNgay);
                var duLieu = await baoCao.TaoAsync(dsCongTy, tu, den, HttpContext.RequestAborted);
                // Công ty mở được trang chi tiết — thẻ công ty khác chỉ hiện số, không thành link
                var moDuoc = SwitchPingWorker.DsCongTy.Where(CoQuyenXem).ToList();
                return Json(new { thanhCong = true, duLieu, moDuoc });
            }
            catch (Exception ex) when (ex is SqlException or InvalidOperationException)
            {
                return Json(new { thanhCong = false, thongBao = "Không tạo được báo cáo: " + ex.Message });
            }
        }

        [HttpGet("/QLSwitch/TongQuan/XuatExcel")]
        public async Task<IActionResult> TongQuanXuatExcel(DateTime? tuNgay, DateTime? denNgay, string? congTy, [FromServices] SwitchBaoCaoService baoCao)
        {
            var dsCongTy = DsCongTyBaoCao(congTy);
            if (dsCongTy == null) return NotFound();
            if (dsCongTy.Count == 0) return StatusCode(403);

            var (tu, den) = KyBaoCao(tuNgay, denNgay);
            try
            {
                var bc = await baoCao.TaoAsync(dsCongTy, tu, den, HttpContext.RequestAborted);
                var motCongTy = string.IsNullOrWhiteSpace(congTy) ? null : dsCongTy[0];
                var file = SwitchBaoCaoExcel.TaoBaoCao(bc, motCongTy, User.Identity?.Name);
                return File(file, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                    $"BaoCaoAP_{motCongTy ?? "TongQuan"}_{tu:yyyyMMdd}-{den:yyyyMMdd}.xlsx");
            }
            catch (SqlException ex)
            {
                return Content("Không tạo được báo cáo: " + ex.Message);
            }
        }

        #endregion

        /// <summary>Ghi vào KK_LichSuThaoTac để mọi thao tác thêm/sửa/xoá switch đều có vết.</summary>
        private async Task GhiLichSuAsync(string hanhDong, int idDoiTuong, string chiTiet)
        {
            try
            {
                _context.KkLichSuThaoTacs.Add(new KkLichSuThaoTac
                {
                    HanhDong = hanhDong,
                    DoiTuong = "Switch",
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
