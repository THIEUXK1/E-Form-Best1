using System.Net;
using E_Form_Best.Areas.ITForm.Services;
using E_Form_Best.Context;
using E_Form_Best.Models.ITForm;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace E_Form_Best.Areas.ITForm.Controllers
{
    /// <summary>
    /// Server và lưu trữ 3 công ty (cùng khuôn "Quản lý Switch"): mỗi công ty một trang /QLServer/{công ty}
    /// gồm danh sách server / máy ảo / NAS / SAN + lịch sử online/offline, và trang /QLServer/TongQuan.
    /// Trạng thái kết nối do ServerPingWorker ghi, controller chỉ đọc.
    /// </summary>
    [Area("ITform")]
    public class QLServerController : Controller
    {
        private readonly ITFormContext _context;
        private readonly IConfiguration _configuration;

        // Danh mục cố định, đổi ở đây là view + lọc ăn theo
        public static readonly string[] DsTinhTrang = { "Đang hoạt động", "Hư hỏng", "Ngừng sử dụng", "Trong kho" };
        public static readonly string[] DsLoai = { "Server", "Máy ảo", "NAS", "SAN" };
        /// <summary>Loại thuộc nhóm "Lưu trữ" — còn lại là nhóm "Server" (bộ lọc nhóm + tổng dung lượng ở Tổng quan).</summary>
        public static readonly string[] DsLoaiLuuTru = { "NAS", "SAN" };

        /// <summary>Ngưỡng % dung lượng đã dùng bị đánh dấu sắp đầy.</summary>
        private const decimal NguongDay = ServerBaoCaoService.NguongDay;
        /// <summary>Bảo hành còn ≤ số ngày này thì đánh dấu sắp hết.</summary>
        private const int NgaySapHetBh = ServerBaoCaoService.NgaySapHetBh;

        public QLServerController(ITFormContext context, IConfiguration configuration)
        {
            _context = context;
            _configuration = configuration;
        }

        // "All" toàn quyền cả 3 công ty; "SvBPVN" / "SvPFVN" / "SvMEGA" chỉ được xem công ty đó.
        // Ẩn nút ở UI không phải phân quyền nên mọi action đều phải gọi.
        private bool CoQuyenXem(string congTy) => User.IsInRole("All") || User.IsInRole("Sv" + congTy.ToUpperInvariant());
        private bool CoQuyenSua() => User.IsInRole("All");

        private static string? CongTyHopLe(string? congTy)
            => ServerPingWorker.DsCongTy.FirstOrDefault(x => x.Equals(congTy?.Trim(), StringComparison.OrdinalIgnoreCase));

        private Task<int?> IdCongTyAsync(string congTy)
            => _context.KkCongTies.Where(x => x.TenCongTy == congTy).Select(x => (int?)x.IdcongTy).FirstOrDefaultAsync();

        private static bool TheoDoi(string? ip, string? tinhTrang)
            => !string.IsNullOrWhiteSpace(ip) && (tinhTrang == null || !ServerPingWorker.DsTinhTrangKhongTheoDoi.Contains(tinhTrang));

        // Máy nạp hàng loạt mang tên mặc định "Server <IP>" (iLO chưa có AMS nên không có hostname):
        // hiện sysName SNMP (1.3.6.1.2.1.1.5.0, job lưu vào KK_ServerSucKhoe.ten_he_thong) thay thế.
        // Chỉ đổi lúc hiển thị — tên đặt tay không bị đè; mở Sửa rồi Lưu thì tên này thành tên thật.
        private static string TenHienThi(string ten, string? ip, string? tenHeThong)
            => !string.IsNullOrWhiteSpace(tenHeThong) && (ten == "Server " + ip || ten == ip) ? tenHeThong.Trim() : ten;

        #region View

        [HttpGet("/QLServer/{congTy:regex(^(BPVN|PFVN|MEGA)$)}")]
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
            ViewBag.DsLoai = DsLoai;
            ViewBag.CoQuyenSua = CoQuyenSua();
            return View();
        }

        #endregion

        #region Danh sách

        [HttpGet("/QLServer/{congTy:regex(^(BPVN|PFVN|MEGA)$)}/DanhSach")]
        public async Task<IActionResult> DanhSach(string congTy, string? tuKhoa, string? nhom, string? loai, int? idBoPhan, string? tinhTrang, string? ketNoi)
        {
            var cty = CongTyHopLe(congTy)!;
            if (!CoQuyenXem(cty)) return Json(new { thanhCong = false, thongBao = "Bạn không có quyền xem dữ liệu này." });

            try
            {
                var ds = await TruyVanDanhSach(await IdCongTyAsync(cty), tuKhoa, nhom, loai, idBoPhan, tinhTrang, ketNoi)
                    .Select(x => new
                    {
                        x.IdServer, x.MaServer, x.TenServer, x.Loai, x.ChucNang, x.DiaChiIp, x.IpQuanTri, x.HangSanXuat, x.Model, x.Serial,
                        x.HeDieuHanh, x.Cpu, x.RamGb, x.DungLuongTb, x.DaDungTb, x.Raid, x.IdboPhan,
                        TenBoPhan = x.IdboPhanNavigation != null ? x.IdboPhanNavigation.TenBoPhan : "",
                        x.ViTri, x.TinhTrang, x.NgayLapDat, x.HanBaoHanh, x.GhiChu,
                        x.TrangThaiKetNoi, x.DoiTrangThaiLuc, x.KiemTraLuc,
                        SucKhoe = _context.KkServerSucKhoes.Where(s => s.IdServer == x.IdServer)
                            .Select(s => new { s.TrangThai, s.TomTat, s.KiemTraLuc, s.Nguon, s.TenHeThong }).FirstOrDefault()
                    })
                    .ToListAsync();

                // Thiết bị không ping (chưa khai IP / trong kho / ngừng dùng) hiện "Không theo dõi" thay vì trạng thái cũ
                var duLieu = ds.Select(x => new
                {
                    x.IdServer, x.MaServer, TenServer = TenHienThi(x.TenServer, x.DiaChiIp, x.SucKhoe?.TenHeThong), x.Loai, x.ChucNang, x.DiaChiIp, x.IpQuanTri, x.HangSanXuat, x.Model, x.Serial,
                    x.HeDieuHanh, x.Cpu, x.RamGb, x.DungLuongTb, x.DaDungTb, x.Raid, x.IdboPhan, x.TenBoPhan,
                    x.ViTri, x.TinhTrang, x.NgayLapDat, x.HanBaoHanh, x.GhiChu,
                    TheoDoi = TheoDoi(x.DiaChiIp, x.TinhTrang),
                    KetNoi = TheoDoi(x.DiaChiIp, x.TinhTrang) ? x.TrangThaiKetNoi : null,
                    x.DoiTrangThaiLuc, x.KiemTraLuc, x.SucKhoe
                }).ToList();

                return Json(new
                {
                    thanhCong = true,
                    duLieu,
                    tongDong = duLieu.Count,
                    tongServer = duLieu.Count(x => !DsLoaiLuuTru.Contains(x.Loai)),
                    tongLuuTru = duLieu.Count(x => DsLoaiLuuTru.Contains(x.Loai)),
                    tongOnline = duLieu.Count(x => x.KetNoi == "UP"),
                    tongOffline = duLieu.Count(x => x.KetNoi == "DOWN"),
                    kiemTraGanNhat = duLieu.Max(x => x.KiemTraLuc),
                    chuKyGiay = ServerPingWorker.ChuKyGiay(_configuration),
                    nguongDay = NguongDay
                });
            }
            catch (Exception ex) { return Json(new { thanhCong = false, thongBao = ex.Message }); }
        }

        /// <summary>Danh sách 1 công ty ra Excel, theo đúng bộ lọc đang chọn trên trang.</summary>
        [HttpGet("/QLServer/{congTy:regex(^(BPVN|PFVN|MEGA)$)}/XuatExcel")]
        public async Task<IActionResult> XuatExcel(string congTy, string? tuKhoa, string? nhom, string? loai, int? idBoPhan, string? tinhTrang, string? ketNoi)
        {
            var cty = CongTyHopLe(congTy)!;
            if (!CoQuyenXem(cty)) return StatusCode(403);

            var ds = await TruyVanDanhSach(await IdCongTyAsync(cty), tuKhoa, nhom, loai, idBoPhan, tinhTrang, ketNoi)
                .Select(x => new
                {
                    x.MaServer, x.TenServer, x.Loai, x.ChucNang, x.DiaChiIp, x.IpQuanTri, x.HangSanXuat, x.Model, x.Serial,
                    x.HeDieuHanh, x.Cpu, x.RamGb, x.DungLuongTb, x.DaDungTb, x.Raid,
                    TenBoPhan = x.IdboPhanNavigation != null ? x.IdboPhanNavigation.TenBoPhan : null,
                    x.ViTri, x.TinhTrang, x.TrangThaiKetNoi, x.KiemTraLuc, x.NgayLapDat, x.HanBaoHanh, x.GhiChu
                })
                .ToListAsync();

            var dong = ds.Select(x => new ServerExcel.Dong(
                x.MaServer, x.TenServer, x.Loai, x.ChucNang, x.DiaChiIp, x.IpQuanTri,
                string.Join(" / ", new[] { x.HangSanXuat, x.Model }.Where(s => !string.IsNullOrWhiteSpace(s))),
                x.Serial, x.HeDieuHanh, x.Cpu, x.RamGb, x.DungLuongTb, x.DaDungTb, x.Raid, x.TenBoPhan, x.ViTri, x.TinhTrang,
                !TheoDoi(x.DiaChiIp, x.TinhTrang) ? "Không theo dõi"
                    : x.TrangThaiKetNoi == "UP" ? "Online" : x.TrangThaiKetNoi == "DOWN" ? "Mất KN" : "Chưa kiểm",
                x.KiemTraLuc, x.NgayLapDat, x.HanBaoHanh, x.GhiChu)).ToList();

            var file = ServerExcel.TaoDanhSach(dong, cty, User.Identity?.Name);
            return File(file, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                $"DanhSachServer_{cty}_{DateTime.Now:yyyyMMdd}.xlsx");
        }

        private IQueryable<KkServer> TruyVanDanhSach(int? idCongTy, string? tuKhoa, string? nhom, string? loai, int? idBoPhan, string? tinhTrang, string? ketNoi)
        {
            var query = _context.KkServers.Where(x => x.NgayXoa == null && x.IdcongTy == idCongTy);

            if (!string.IsNullOrWhiteSpace(tuKhoa))
            {
                var kw = tuKhoa.Trim();
                query = query.Where(x =>
                    x.TenServer.Contains(kw) ||
                    (x.MaServer != null && x.MaServer.Contains(kw)) ||
                    (x.ChucNang != null && x.ChucNang.Contains(kw)) ||
                    (x.DiaChiIp != null && x.DiaChiIp.Contains(kw)) ||
                    (x.IpQuanTri != null && x.IpQuanTri.Contains(kw)) ||
                    (x.HangSanXuat != null && x.HangSanXuat.Contains(kw)) ||
                    (x.Model != null && x.Model.Contains(kw)) ||
                    (x.Serial != null && x.Serial.Contains(kw)) ||
                    (x.HeDieuHanh != null && x.HeDieuHanh.Contains(kw)) ||
                    (x.ViTri != null && x.ViTri.Contains(kw)));
            }
            if (nhom == "luutru") query = query.Where(x => DsLoaiLuuTru.Contains(x.Loai));
            else if (nhom == "server") query = query.Where(x => !DsLoaiLuuTru.Contains(x.Loai));
            if (!string.IsNullOrWhiteSpace(loai)) query = query.Where(x => x.Loai == loai);
            if (idBoPhan > 0) query = query.Where(x => x.IdboPhan == idBoPhan);
            if (!string.IsNullOrWhiteSpace(tinhTrang)) query = query.Where(x => x.TinhTrang == tinhTrang);

            // Lọc kết nối chỉ xét thiết bị đang theo dõi (có IP, không trong kho / ngừng dùng)
            var khongTheoDoi = ServerPingWorker.DsTinhTrangKhongTheoDoi;
            if (ketNoi == "online" || ketNoi == "offline")
            {
                var tt = ketNoi == "online" ? "UP" : "DOWN";
                query = query.Where(x => x.DiaChiIp != null && x.TrangThaiKetNoi == tt
                                         && (x.TinhTrang == null || !khongTheoDoi.Contains(x.TinhTrang)));
            }

            return query.OrderBy(x => x.Loai).ThenBy(x => x.TenServer);
        }

        #endregion

        #region Thêm / Sửa / Xoá

        // Tham số KHÔNG được đặt tên "model": KkServer có thuộc tính Model, binder sẽ coi ô "Model" của form
        // là tiền tố "model." và bỏ trống mọi trường còn lại.
        [HttpPost("/QLServer/{congTy:regex(^(BPVN|PFVN|MEGA)$)}/Luu")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Luu(string congTy, KkServer sv)
        {
            var cty = CongTyHopLe(congTy)!;
            if (!CoQuyenSua()) return Json(new { thanhCong = false, thongBao = "Bạn không có quyền thao tác." });

            sv.TenServer = sv.TenServer?.Trim() ?? "";
            if (sv.TenServer.Length == 0)
                return Json(new { thanhCong = false, thongBao = "Vui lòng nhập tên thiết bị." });
            if (!DsLoai.Contains(sv.Loai))
                return Json(new { thanhCong = false, thongBao = "Vui lòng chọn loại thiết bị." });

            sv.DiaChiIp = string.IsNullOrWhiteSpace(sv.DiaChiIp) ? null : sv.DiaChiIp.Trim();
            if (sv.DiaChiIp != null && !IPAddress.TryParse(sv.DiaChiIp, out _))
                return Json(new { thanhCong = false, thongBao = $"Địa chỉ IP \"{sv.DiaChiIp}\" không hợp lệ." });
            sv.IpQuanTri = string.IsNullOrWhiteSpace(sv.IpQuanTri) ? null : sv.IpQuanTri.Trim();
            if (sv.IpQuanTri != null && !IPAddress.TryParse(sv.IpQuanTri, out _))
                return Json(new { thanhCong = false, thongBao = $"IP quản trị \"{sv.IpQuanTri}\" không hợp lệ." });

            if (sv.TinhTrang != null && !DsTinhTrang.Contains(sv.TinhTrang))
                return Json(new { thanhCong = false, thongBao = "Tình trạng không hợp lệ." });
            if (sv.RamGb is < 1 or > 65536)
                return Json(new { thanhCong = false, thongBao = "RAM phải từ 1 đến 65536 GB." });

            // Ô số thập phân của trình duyệt luôn gửi dấu chấm ("1.5"); binder parse theo culture máy chủ
            // (vi-VN coi "." là phân cách nghìn -> 15) nên đọc lại bằng InvariantCulture
            if (!DocSoTb(Request.Form["DungLuongTb"], out var dungLuong) || !DocSoTb(Request.Form["DaDungTb"], out var daDung))
                return Json(new { thanhCong = false, thongBao = "Dung lượng phải là số TB từ 0 đến 99.999.999 (VD: 7.2)." });
            sv.DungLuongTb = dungLuong;
            sv.DaDungTb = daDung;
            if (sv.DungLuongTb != null && sv.DaDungTb > sv.DungLuongTb)
                return Json(new { thanhCong = false, thongBao = "Dung lượng đã dùng lớn hơn tổng dung lượng." });

            try
            {
                // Công ty lấy theo trang đang mở, không nhận giá trị client gửi lên
                var idCongTy = await IdCongTyAsync(cty);
                if (idCongTy == null)
                    return Json(new { thanhCong = false, thongBao = $"Chưa có công ty {cty} trong danh mục công ty." });

                if (sv.IdboPhan.HasValue
                    && !await _context.KkBoPhans.AnyAsync(x => x.IdboPhan == sv.IdboPhan && x.IdcongTy == idCongTy))
                    return Json(new { thanhCong = false, thongBao = "Bộ phận không thuộc công ty " + cty + "." });

                // Báo trùng rõ ràng trước; unique index (công ty, IP) ở DB vẫn là chốt cuối khi hai người lưu cùng lúc
                if (sv.DiaChiIp != null)
                {
                    var trung = await _context.KkServers
                        .Where(x => x.NgayXoa == null && x.IdcongTy == idCongTy && x.DiaChiIp == sv.DiaChiIp && x.IdServer != sv.IdServer)
                        .Select(x => x.TenServer).FirstOrDefaultAsync();
                    if (trung != null)
                        return Json(new { thanhCong = false, thongBao = $"IP {sv.DiaChiIp} đã được gán cho \"{trung}\"." });
                }

                string hanhDong = sv.IdServer == 0 ? "Thêm mới" : "Cập nhật";

                if (sv.IdServer == 0)
                {
                    // Thiết bị chưa có IP thì không có gì để báo trùng: chặn bấm Lưu 2 lần bằng tên + người tạo trong 30 giây
                    if (sv.DiaChiIp == null)
                    {
                        var moiDay = DateTime.Now.AddSeconds(-30);
                        var nguoiTao = User.Identity?.Name;
                        if (await _context.KkServers.AnyAsync(x => x.NgayXoa == null && x.IdcongTy == idCongTy
                                && x.TenServer == sv.TenServer && x.NguoiTao == nguoiTao && x.NgayTao >= moiDay))
                            return Json(new { thanhCong = false, thongBao = $"\"{sv.TenServer}\" vừa được thêm." });
                    }

                    sv.IdcongTy = idCongTy.Value;
                    sv.NgayTao = DateTime.Now;
                    sv.NguoiTao = User.Identity?.Name;
                    sv.NgayCapNhat = null;
                    sv.NgayXoa = null;
                    sv.LyDoXoa = null;
                    // Trạng thái kết nối chỉ job ping được ghi
                    sv.TrangThaiKetNoi = null;
                    sv.DoiTrangThaiLuc = null;
                    sv.KiemTraLuc = null;
                    _context.KkServers.Add(sv);
                }
                else
                {
                    // Chỉ sửa được thiết bị còn hiệu lực của công ty trang đang mở (chặn IDOR qua id gửi lên)
                    var db = await _context.KkServers
                        .FirstOrDefaultAsync(x => x.IdServer == sv.IdServer && x.IdcongTy == idCongTy && x.NgayXoa == null);
                    if (db == null)
                        return Json(new { thanhCong = false, thongBao = "Không tìm thấy thiết bị cần cập nhật." });

                    // Đổi IP thì trạng thái cũ không còn đúng: để job ping kiểm lại từ đầu, không sinh sự kiện giả
                    if (db.DiaChiIp != sv.DiaChiIp)
                    {
                        db.TrangThaiKetNoi = null;
                        db.DoiTrangThaiLuc = null;
                        db.KiemTraLuc = null;
                    }

                    db.MaServer = sv.MaServer;
                    db.TenServer = sv.TenServer;
                    db.Loai = sv.Loai;
                    db.ChucNang = sv.ChucNang;
                    db.DiaChiIp = sv.DiaChiIp;
                    db.IpQuanTri = sv.IpQuanTri;
                    db.HangSanXuat = sv.HangSanXuat;
                    db.Model = sv.Model;
                    db.Serial = sv.Serial;
                    db.HeDieuHanh = sv.HeDieuHanh;
                    db.Cpu = sv.Cpu;
                    db.RamGb = sv.RamGb;
                    db.DungLuongTb = sv.DungLuongTb;
                    db.DaDungTb = sv.DaDungTb;
                    db.Raid = sv.Raid;
                    db.IdboPhan = sv.IdboPhan;
                    db.ViTri = sv.ViTri;
                    db.TinhTrang = sv.TinhTrang;
                    db.NgayLapDat = sv.NgayLapDat;
                    db.HanBaoHanh = sv.HanBaoHanh;
                    db.GhiChu = sv.GhiChu;
                    db.NgayCapNhat = DateTime.Now;
                }

                await _context.SaveChangesAsync();
                await GhiLichSuAsync(hanhDong, sv.IdServer, $"{sv.Loai} {cty}: {sv.TenServer} (IP: {sv.DiaChiIp ?? "chưa khai"})");

                return Json(new { thanhCong = true, thongBao = $"{hanhDong} thành công!" });
            }
            catch (DbUpdateException ex) when (ex.InnerException is SqlException sql && (sql.Number == 2601 || sql.Number == 2627))
            {
                return Json(new { thanhCong = false, thongBao = $"IP {sv.DiaChiIp} vừa được gán cho thiết bị khác." });
            }
            catch (Exception ex) { return Json(new { thanhCong = false, thongBao = ex.Message }); }
        }

        [HttpPost("/QLServer/{congTy:regex(^(BPVN|PFVN|MEGA)$)}/Xoa")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Xoa(string congTy, int id, string? lyDo)
        {
            var cty = CongTyHopLe(congTy)!;
            if (!CoQuyenSua()) return Json(new { thanhCong = false, thongBao = "Bạn không có quyền thao tác." });

            try
            {
                var idCongTy = await IdCongTyAsync(cty);
                var item = await _context.KkServers
                    .FirstOrDefaultAsync(x => x.IdServer == id && x.IdcongTy == idCongTy && x.NgayXoa == null);
                if (item == null)
                    return Json(new { thanhCong = false, thongBao = "Không tìm thấy thiết bị cần xoá." });

                // Xoá mềm để còn truy vết + giữ lịch sử online/offline
                item.NgayXoa = DateTime.Now;
                item.LyDoXoa = string.IsNullOrWhiteSpace(lyDo) ? "Không ghi lý do" : lyDo.Trim();
                await _context.SaveChangesAsync();

                await GhiLichSuAsync("Xóa", id, $"Đã xoá {item.Loai} {cty}: {item.TenServer} (IP: {item.DiaChiIp ?? "chưa khai"}). Lý do: {item.LyDoXoa}");
                return Json(new { thanhCong = true, thongBao = "Đã xoá!" });
            }
            catch (Exception ex) { return Json(new { thanhCong = false, thongBao = ex.Message }); }
        }

        /// <summary>"" -> null (hợp lệ); "7.2" -> 7.20; sai định dạng / âm / quá lớn -> false.</summary>
        private static bool DocSoTb(string? v, out decimal? ketQua)
        {
            ketQua = null;
            if (string.IsNullOrWhiteSpace(v)) return true;
            if (!decimal.TryParse(v.Trim().Replace(',', '.'), System.Globalization.NumberStyles.AllowDecimalPoint,
                    System.Globalization.CultureInfo.InvariantCulture, out var so) || so > 99999999) return false;
            ketQua = Math.Round(so, 2);
            return true;
        }

        #endregion

        #region Lịch sử online/offline

        [HttpGet("/QLServer/{congTy:regex(^(BPVN|PFVN|MEGA)$)}/LichSu")]
        public async Task<IActionResult> LichSu(string congTy, DateTime? tuNgay, DateTime? denNgay, string? loai, string? tuKhoa)
        {
            var cty = CongTyHopLe(congTy)!;
            if (!CoQuyenXem(cty)) return Json(new { thanhCong = false, thongBao = "Bạn không có quyền xem dữ liệu này." });

            try
            {
                var (tu, den) = KyBaoCao(tuNgay, denNgay);
                var denHet = den.AddDays(1);
                var idCongTy = await IdCongTyAsync(cty);

                var query = _context.KkServerLichSus
                    .Where(x => x.IdcongTy == idCongTy && x.ThoiGian >= tu && x.ThoiGian < denHet);
                if (loai == "UP" || loai == "DOWN") query = query.Where(x => x.SangTrangThai == loai);
                if (!string.IsNullOrWhiteSpace(tuKhoa))
                {
                    var kw = tuKhoa.Trim();
                    query = query.Where(x => (x.TenServer != null && x.TenServer.Contains(kw)) || (x.DiaChiIp != null && x.DiaChiIp.Contains(kw)));
                }

                const int toiDa = 2000;
                var duLieu = await query
                    .OrderByDescending(x => x.ThoiGian).ThenByDescending(x => x.IdLichSu)
                    .Take(toiDa)
                    .Select(x => new { x.ThoiGian, x.IdServer, x.TenServer, x.DiaChiIp, x.TuTrangThai, x.SangTrangThai, x.ThoiLuongGiay })
                    .ToListAsync();

                return Json(new
                {
                    thanhCong = true,
                    duLieu,
                    tong = duLieu.Count,
                    soDown = duLieu.Count(x => x.SangTrangThai == "DOWN"),
                    soUp = duLieu.Count(x => x.SangTrangThai == "UP"),
                    soThietBi = duLieu.Select(x => x.IdServer).Distinct().Count(),
                    biCat = duLieu.Count == toiDa
                });
            }
            catch (Exception ex) { return Json(new { thanhCong = false, thongBao = ex.Message }); }
        }

        /// <summary>
        /// Xu hướng số thiết bị online / mất kết nối trong N giờ gần nhất (cho biểu đồ ở tab Danh sách).
        /// Dựng lại trạng thái từng mốc bằng cách đi ngược sự kiện KK_ServerLichSu (TuTrangThai = trạng thái TRƯỚC khi đổi).
        /// </summary>
        [HttpGet("/QLServer/{congTy:regex(^(BPVN|PFVN|MEGA)$)}/XuHuong")]
        public async Task<IActionResult> XuHuong(string congTy, int? gio)
        {
            var cty = CongTyHopLe(congTy)!;
            if (!CoQuyenXem(cty)) return Json(new { thanhCong = false, thongBao = "Bạn không có quyền xem dữ liệu này." });

            try
            {
                var soGio = gio is 6 or 24 or 72 or 168 ? gio.Value : 24;
                var den = DateTime.Now;
                var tu = den.AddHours(-soGio);
                var idCongTy = await IdCongTyAsync(cty);

                var dsSv = await _context.KkServers
                    .Where(x => x.NgayXoa == null && x.IdcongTy == idCongTy)
                    .Select(x => new { x.IdServer, x.DiaChiIp, x.TinhTrang, x.TrangThaiKetNoi })
                    .ToListAsync();
                // null = chưa có trạng thái tại mốc đang xét (chưa kiểm lần nào) -> không đếm
                var trangThai = dsSv
                    .Where(x => TheoDoi(x.DiaChiIp, x.TinhTrang) && (x.TrangThaiKetNoi == "UP" || x.TrangThaiKetNoi == "DOWN"))
                    .ToDictionary(x => x.IdServer, x => (string?)x.TrangThaiKetNoi);

                var suKien = await _context.KkServerLichSus
                    .Where(x => x.IdcongTy == idCongTy && x.ThoiGian > tu)
                    .OrderByDescending(x => x.ThoiGian).ThenByDescending(x => x.IdLichSu)
                    .Select(x => new { x.IdServer, x.ThoiGian, x.TuTrangThai })
                    .ToListAsync();

                const int soDiem = 72;
                var buoc = TimeSpan.FromTicks((den - tu).Ticks / soDiem);
                var diem = new List<object>(soDiem + 1);
                var i = 0;
                for (var k = 0; k <= soDiem; k++)
                {
                    var t = den - buoc * k;
                    while (i < suKien.Count && suKien[i].ThoiGian > t)
                    {
                        var s = suKien[i++];
                        if (trangThai.ContainsKey(s.IdServer))
                            trangThai[s.IdServer] = s.TuTrangThai == "UP" || s.TuTrangThai == "DOWN" ? s.TuTrangThai : null;
                    }
                    diem.Add(new
                    {
                        thoiGian = t,
                        up = trangThai.Values.Count(v => v == "UP"),
                        down = trangThai.Values.Count(v => v == "DOWN")
                    });
                }
                diem.Reverse();

                return Json(new { thanhCong = true, duLieu = diem });
            }
            catch (Exception ex) { return Json(new { thanhCong = false, thongBao = ex.Message }); }
        }

        #endregion

        #region Sức khoẻ phần cứng (SNMP)

        /// <summary>Ảnh chụp SNMP gần nhất của 1 thiết bị: từng mục ổ cứng / RAM / CPU / quạt / nguồn / nhiệt độ / dung lượng.</summary>
        [HttpGet("/QLServer/{congTy:regex(^(BPVN|PFVN|MEGA)$)}/SucKhoe/{id:int}")]
        public async Task<IActionResult> SucKhoe(string congTy, int id)
        {
            var cty = CongTyHopLe(congTy)!;
            if (!CoQuyenXem(cty)) return Json(new { thanhCong = false, thongBao = "Bạn không có quyền xem dữ liệu này." });

            try
            {
                return Json(await DuLieuSucKhoeAsync(cty, id));
            }
            catch (Exception ex) { return Json(new { thanhCong = false, thongBao = ex.Message }); }
        }

        /// <summary>
        /// Đọc SNMP ngay (không chờ job nền). Chạy trên máy chủ web đang phục vụ request — máy đó phải thông mạng tới thiết bị
        /// (BPVN 10.0.63.x chỉ máy chủ 60.52 tới được).
        /// </summary>
        [HttpPost("/QLServer/{congTy:regex(^(BPVN|PFVN|MEGA)$)}/SucKhoe/{id:int}/KiemTra")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> KiemTraSucKhoe(string congTy, int id)
        {
            var cty = CongTyHopLe(congTy)!;
            if (!CoQuyenSua()) return Json(new { thanhCong = false, thongBao = "Bạn không có quyền thao tác." });

            try
            {
                var idCongTy = await IdCongTyAsync(cty);
                // Chặn IDOR: id phải thuộc công ty trang đang mở
                if (!await _context.KkServers.AnyAsync(x => x.IdServer == id && x.IdcongTy == idCongTy && x.NgayXoa == null))
                    return Json(new { thanhCong = false, thongBao = "Không tìm thấy thiết bị." });

                // Đọc SNMP vài chục giây là cùng; người dùng đóng trang thì huỷ luôn
                using var het = CancellationTokenSource.CreateLinkedTokenSource(HttpContext.RequestAborted);
                het.CancelAfter(TimeSpan.FromSeconds(90));
                await ServerSnmpWorker.DocVaLuuAsync(_context, _configuration, id, het.Token);
                return Json(await DuLieuSucKhoeAsync(cty, id));
            }
            catch (OperationCanceledException) { return Json(new { thanhCong = false, thongBao = "Đọc SNMP quá lâu, thử lại sau." }); }
            catch (Exception ex) { return Json(new { thanhCong = false, thongBao = ex.Message }); }
        }

        /// <summary>
        /// Nút "Kiểm tra kết nối" ở form thêm/sửa: đọc SNMP 1 IP/hostname, trả hãng / model / serial / CPU / RAM / HĐH / dung lượng
        /// để trình duyệt điền form (chưa lưu). Community ô nhập chỉ dùng cho lần đọc này, không lưu, không trả ra trình duyệt;
        /// để trống thì dùng cấu hình .env theo loại. Job theo dõi sức khoẻ sau đó vẫn đọc bằng community .env.
        /// </summary>
        [HttpPost("/QLServer/{congTy:regex(^(BPVN|PFVN|MEGA)$)}/KiemTraSnmp")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> KiemTraSnmp(string congTy, string? ipHoacTen, string? community, string? loai)
        {
            var cty = CongTyHopLe(congTy)!;
            if (!CoQuyenSua()) return Json(new { thanhCong = false, thongBao = "Bạn không có quyền thao tác." });
            if (string.IsNullOrWhiteSpace(ipHoacTen))
                return Json(new { thanhCong = false, thongBao = "Nhập IP hoặc hostname của thiết bị." });
            var com = string.IsNullOrWhiteSpace(community) ? ServerSnmpWorker.Community(_configuration, cty, loai ?? "") : community.Trim();
            if (string.IsNullOrWhiteSpace(com))
                return Json(new { thanhCong = false, thongBao = "Nhập community SNMP (máy chủ chưa cấu hình ServerSnmp__Community cho loại này)." });

            using var het = CancellationTokenSource.CreateLinkedTokenSource(HttpContext.RequestAborted);
            het.CancelAfter(TimeSpan.FromSeconds(90));
            try
            {
                var ip = await SnmpClient.PhanGiaiAsync(ipHoacTen, het.Token);
                if (ip == null)
                    return Json(new { thanhCong = false, thongBao = $"Không phân giải được \"{ipHoacTen.Trim()}\" ra địa chỉ IP." });

                var kq = await ServerSnmpService.DocAsync(ip, com, het.Token);
                if (kq == null)
                    return Json(new { thanhCong = false, thongBao = $"{ip} không trả lời SNMP: sai community, thiết bị chưa bật SNMP v2c, hoặc máy chủ web ({Environment.MachineName}) không thông mạng tới thiết bị." });

                return Json(new
                {
                    thanhCong = true,
                    thongBao = "Kết nối SNMP thành công" + (kq.Nguon != null ? $" ({kq.Nguon})." : "."),
                    duLieu = new
                    {
                        ip, kq.TenHeThong, kq.MoTa, kq.Nguon, kq.Hang, kq.Model, kq.Serial, Cpu = kq.CpuTomTat, kq.RamGb,
                        kq.HeDieuHanh, Raid = kq.RaidTomTat, kq.DungLuongTb, kq.DaDungTb
                    }
                });
            }
            catch (OperationCanceledException) { return Json(new { thanhCong = false, thongBao = "Đọc SNMP quá lâu, thử lại sau." }); }
            catch (Exception ex) { return Json(new { thanhCong = false, thongBao = ex.Message }); }
        }

        private async Task<object> DuLieuSucKhoeAsync(string cty, int id)
        {
            var idCongTy = await IdCongTyAsync(cty);
            var sv = await _context.KkServers
                .Where(x => x.IdServer == id && x.IdcongTy == idCongTy && x.NgayXoa == null)
                .Select(x => new { x.IdServer, x.TenServer, x.Loai, x.DiaChiIp, x.IpQuanTri })
                .FirstOrDefaultAsync();
            if (sv == null) return new { thanhCong = false, thongBao = "Không tìm thấy thiết bị." };

            var sk = await _context.KkServerSucKhoes.AsNoTracking().FirstOrDefaultAsync(x => x.IdServer == id);
            return new
            {
                thanhCong = true,
                thietBi = sv with { TenServer = TenHienThi(sv.TenServer, sv.DiaChiIp, sk?.TenHeThong) },
                // du_lieu là JSON do job ghi: trả nguyên chuỗi, trình duyệt tự JSON.parse
                sucKhoe = sk == null ? null : new { sk.KiemTraLuc, sk.DocDuocLuc, sk.IpSnmp, sk.Nguon, sk.TenHeThong, sk.MoTa, sk.TrangThai, sk.TomTat, sk.Loi, sk.DuLieu },
                chuKyGiay = ServerSnmpWorker.ChuKyGiay(_configuration),
                coCommunity = !string.IsNullOrWhiteSpace(ServerSnmpWorker.Community(_configuration, cty, sv.Loai))
            };
        }

        [HttpGet("/QLServer/{congTy:regex(^(BPVN|PFVN|MEGA)$)}/SucKhoe/LichSu")]
        public async Task<IActionResult> SucKhoeLichSu(string congTy, DateTime? tuNgay, DateTime? denNgay, string? tuKhoa)
        {
            var cty = CongTyHopLe(congTy)!;
            if (!CoQuyenXem(cty)) return Json(new { thanhCong = false, thongBao = "Bạn không có quyền xem dữ liệu này." });

            try
            {
                var (tu, den) = KyBaoCao(tuNgay, denNgay);
                var denHet = den.AddDays(1);
                var idCongTy = await IdCongTyAsync(cty);

                var query = _context.KkServerSucKhoeLichSus
                    .Where(x => x.IdcongTy == idCongTy && x.ThoiGian >= tu && x.ThoiGian < denHet);
                if (!string.IsNullOrWhiteSpace(tuKhoa))
                {
                    var kw = tuKhoa.Trim();
                    query = query.Where(x => (x.TenServer != null && x.TenServer.Contains(kw)) || (x.TomTat != null && x.TomTat.Contains(kw)));
                }

                const int toiDa = 2000;
                var duLieu = await query
                    .OrderByDescending(x => x.ThoiGian).ThenByDescending(x => x.IdLichSu)
                    .Take(toiDa)
                    .Select(x => new { x.ThoiGian, x.IdServer, x.TenServer, x.TuTrangThai, x.SangTrangThai, x.TomTat })
                    .ToListAsync();

                return Json(new { thanhCong = true, duLieu, tong = duLieu.Count, biCat = duLieu.Count == toiDa });
            }
            catch (Exception ex) { return Json(new { thanhCong = false, thongBao = ex.Message }); }
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

        #endregion

        #region Tổng quan

        /// <summary>
        /// Công ty được xem ở Tổng quan (cùng QLSwitchController.DsCongTyBaoCao):
        ///  - không truyền congTy: mọi công ty có quyền Sv*; AdminIT / BaoCaoDinhKy xem cả 3.
        ///  - 1 công ty: cần quyền xem công ty đó hoặc AdminIT. Trả null khi công ty gửi lên không hợp lệ.
        /// </summary>
        private List<string>? DsCongTyBaoCao(string? congTy)
        {
            var laAdminIt = User.IsInRole("AdminIT") || User.IsInRole("BaoCaoDinhKy");
            if (string.IsNullOrWhiteSpace(congTy))
                return ServerPingWorker.DsCongTy.Where(x => laAdminIt || CoQuyenXem(x)).ToList();

            var cty = CongTyHopLe(congTy);
            if (cty == null) return null;
            return laAdminIt || CoQuyenXem(cty) ? new List<string> { cty } : new List<string>();
        }

        [HttpGet("/QLServer/TongQuan")]
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

        /// <summary>Báo cáo theo kỳ: hiện tại (online, phần cứng, dung lượng, bảo hành) + mất kết nối trong kỳ, so kỳ trước.</summary>
        [HttpGet("/QLServer/TongQuan/DuLieu")]
        public async Task<IActionResult> TongQuanDuLieu(DateTime? tuNgay, DateTime? denNgay, string? congTy, [FromServices] ServerBaoCaoService baoCao)
        {
            var dsCongTy = DsCongTyBaoCao(congTy);
            if (dsCongTy == null || dsCongTy.Count == 0) return Json(new { thanhCong = false, thongBao = "Bạn không có quyền xem dữ liệu này." });

            try
            {
                var (tu, den) = KyBaoCao(tuNgay, denNgay);
                var duLieu = await baoCao.TaoAsync(dsCongTy, tu, den, HttpContext.RequestAborted);
                // Công ty mở được trang chi tiết — thẻ công ty khác chỉ hiện số, không thành link
                var moDuoc = ServerPingWorker.DsCongTy.Where(CoQuyenXem).ToList();
                return Json(new { thanhCong = true, duLieu, moDuoc });
            }
            catch (Exception ex) when (ex is SqlException or InvalidOperationException)
            {
                return Json(new { thanhCong = false, thongBao = "Không tạo được báo cáo: " + ex.Message });
            }
        }

        [HttpGet("/QLServer/TongQuan/XuatExcel")]
        public async Task<IActionResult> TongQuanXuatExcel(DateTime? tuNgay, DateTime? denNgay, string? congTy, [FromServices] ServerBaoCaoService baoCao)
        {
            var dsCongTy = DsCongTyBaoCao(congTy);
            if (dsCongTy == null) return NotFound();
            if (dsCongTy.Count == 0) return StatusCode(403);

            var (tu, den) = KyBaoCao(tuNgay, denNgay);
            try
            {
                var bc = await baoCao.TaoAsync(dsCongTy, tu, den, HttpContext.RequestAborted);
                var motCongTy = string.IsNullOrWhiteSpace(congTy) ? null : dsCongTy[0];
                var file = ServerBaoCaoExcel.TaoBaoCao(bc, motCongTy, User.Identity?.Name);
                return File(file, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                    $"BaoCaoServer_{motCongTy ?? "TongQuan"}_{tu:yyyyMMdd}-{den:yyyyMMdd}.xlsx");
            }
            catch (SqlException ex)
            {
                return Content("Không tạo được báo cáo: " + ex.Message);
            }
        }

        #endregion

        /// <summary>Ghi vào KK_LichSuThaoTac để mọi thao tác thêm/sửa/xoá đều có vết.</summary>
        private async Task GhiLichSuAsync(string hanhDong, int idDoiTuong, string chiTiet)
        {
            try
            {
                _context.KkLichSuThaoTacs.Add(new KkLichSuThaoTac
                {
                    HanhDong = hanhDong,
                    DoiTuong = "Server",
                    IdDoiTuong = idDoiTuong,
                    ChiTiet = chiTiet,
                    ThoiGian = DateTime.Now,
                    NguoiThaoTac = User.Identity?.Name ?? "Hệ thống"
                });
                await _context.SaveChangesAsync();
            }
            catch { /* Bỏ qua lỗi ghi log để không làm gián đoạn luồng chính (cùng cách QLSwitch) */ }
        }
    }
}
