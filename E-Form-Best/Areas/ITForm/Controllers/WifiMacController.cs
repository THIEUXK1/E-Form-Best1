using System.DirectoryServices.AccountManagement;
using System.Runtime.InteropServices;
using System.Security.Claims;
using System.Text.RegularExpressions;
using E_Form_Best.Areas.ITForm.Services;
using E_Form_Best.Context;
using E_Form_Best.Models.ITForm;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace E_Form_Best.Areas.ITForm.Controllers
{
    /// <summary>
    /// Nút "Thêm nhanh lên WLC" ở trang chi tiết đơn đăng ký Wi-Fi (đơn 3). Quyền: 'AdminIT' hoặc 'All'.
    /// MAC lấy từ đơn trong DB theo vị trí dòng, không nhận MAC/lệnh tự do từ client.
    /// </summary>
    [Area("ITform")]
    public class WifiMacController : Controller
    {
        private readonly ITFormContext _context;
        private readonly WlcMacService _wlc;
        private readonly WlcMacQuetService _quet;
        private readonly RadiusMacService _radius;
        private readonly IConfiguration _configuration;

        public WifiMacController(ITFormContext context, WlcMacService wlc, WlcMacQuetService quet, RadiusMacService radius, IConfiguration configuration)
        {
            _context = context;
            _wlc = wlc;
            _quet = quet;
            _radius = radius;
            _configuration = configuration;
        }

        // Mỗi lần quét đọc toàn bộ danh sách MAC trên WLC: chỉ cho 1 lượt chạy cùng lúc
        private static readonly SemaphoreSlim _khoaQuet = new(1, 1);

        /// <summary>
        /// Trạng thái MAC từng dòng thiết bị của đơn trên từng WLC, theo bản chụp do nút "Kiểm tra trên WLC" / thao tác trước ghi lại,
        /// để trang chi tiết đánh dấu sẵn nút "+" mà không cần mật khẩu domain.
        /// </summary>
        [HttpGet("/FormIT/TrangThaiMacWifi/{idFormIt:int}")]
        public async Task<IActionResult> TrangThaiMac(int idFormIt)
        {
            if (!CoQuyen(User)) return Json(new { thanhCong = false, thongBao = "Bạn không có quyền xem MAC trên WLC." });
            return await TraTrangThaiAsync(idFormIt, false, null);
        }

        /// <summary>
        /// Nút "Kiểm tra trên WLC" ở bảng MAC: đọc ngay danh sách MAC rồi ghi vào bản chụp và trả trạng thái các MAC của đơn.
        /// 9800 đọc qua WLC collector, không cần tài khoản; WLC collector không có (5508) đọc qua SSH bằng tài khoản domain IT nhập
        /// lúc bấm, chưa nhập thì bỏ qua và trả tên WLC đó ở canTaiKhoan để trang hỏi thêm. Chỉ đọc, không ghi gì lên WLC.
        /// </summary>
        [HttpPost("/FormIT/QuetMacWifi/{idFormIt:int}")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> QuetMac(int idFormIt, [FromBody] ThemMacVao? vao)
        {
            if (!CoQuyen(User)) return Json(new { thanhCong = false, thongBao = "Bạn không có quyền kiểm tra MAC trên WLC." });
            var taiKhoan = vao?.TaiKhoan?.Trim() ?? "";
            if (taiKhoan.Length == 0 && string.IsNullOrEmpty(vao?.MatKhau)) return await TraTrangThaiAsync(idFormIt, true, null);
            if (taiKhoan.Length == 0) return Json(new { thanhCong = false, thongBao = "Nhập tài khoản domain." });
            if (!Regex.IsMatch(taiKhoan, @"^[A-Za-z0-9._@\\-]{1,64}$")) return Json(new { thanhCong = false, thongBao = "Tài khoản domain không hợp lệ." });
            if (string.IsNullOrEmpty(vao!.MatKhau)) return Json(new { thanhCong = false, thongBao = "Nhập mật khẩu tài khoản domain." });
            return await TraTrangThaiAsync(idFormIt, true, (taiKhoan, vao.MatKhau));
        }

        private async Task<IActionResult> TraTrangThaiAsync(int idFormIt, bool quetNgay, (string TaiKhoan, string MatKhau)? taiKhoanAd)
        {
            try
            {
                var dsCt = await _context.ItDangKiSuDungWifi3s
                    .Where(x => x.IdFormIt == idFormIt)
                    .Select(x => new { x.Id, x.MacTb, x.IdFormItNavigation!.TenCongTy })
                    .ToListAsync();
                var congTy = dsCt.FirstOrDefault()?.TenCongTy?.Trim() ?? "";
                if (dsCt.Count == 0 || !_wlc.CoCauHinh(congTy)) return Json(new { thanhCong = true, duLieu = Array.Empty<object>() });

                var dsLoi = new List<string>();
                var dsCanTaiKhoan = new List<string>();
                if (quetNgay)
                {
                    if (!await _khoaQuet.WaitAsync(0))
                        return Json(new { thanhCong = false, thongBao = "Đang có lượt kiểm tra WLC khác chạy, thử lại sau ít phút." });
                    try
                    {
                        foreach (var wlc in _wlc.DsWlc(congTy))
                        {
                            var quaCollector = !wlc.LaAireOs && _wlc.CoCollectorMac(congTy);
                            if (!quaCollector && taiKhoanAd == null) { dsCanTaiKhoan.Add(wlc.Ten); continue; }
                            var (ds, loi) = quaCollector
                                ? await _wlc.LayDsMacCollectorAsync(congTy, HttpContext.RequestAborted)
                                : await _wlc.LayDsMacAsync(wlc, taiKhoanAd!.Value.TaiKhoan, taiKhoanAd.Value.MatKhau, HttpContext.RequestAborted);
                            await _quet.GhiKetQuaQuetAsync(congTy, wlc, ds, loi, HttpContext.RequestAborted);
                            if (loi != null) dsLoi.Add(wlc.Ten + ": " + loi);
                            // Sai mật khẩu: dừng luôn, không thử WLC khác để tránh khoá tài khoản AD
                            if (WlcMacService.LaLoiSaiMatKhau(loi)) break;
                        }
                    }
                    finally { _khoaQuet.Release(); }
                }

                var dong = dsCt.SelectMany(ct => (ct.MacTb ?? "").Split('|', StringSplitOptions.RemoveEmptyEntries)
                        .Select((m, i) => new { idChiTiet = ct.Id, viTri = i, mac = WlcMacService.ChuanHoaMac(m) }))
                    .Where(x => x.mac != null).ToList();
                var trangThai = await _quet.TrangThaiAsync(congTy, dong.Select(x => x.mac!));
                // App RADIUS không có API tra cứu: trạng thái theo dòng lịch sử RADIUS mới nhất của MAC ở mọi đơn
                // (thêm / báo đã có = có; đánh dấu gỡ tay = không), giống nút Kiểm tra
                var lichSuRadius = await LichSuRadiusAsync();
                return Json(new
                {
                    thanhCong = true,
                    thongBao = dsLoi.Count > 0 ? "Có WLC chưa đọc được — " + string.Join("; ", dsLoi) : null,
                    canTaiKhoan = dsCanTaiKhoan,
                    duLieu = dong.Select(x => new
                    {
                        x.idChiTiet, x.viTri, wlc = trangThai[x.mac!],
                        radius = CoTrenRadius(lichSuRadius, x.mac!)
                    })
                });
            }
            catch (Exception ex)
            {
                // Bảng KK_WlcMac chưa tạo / mất DB: nút vẫn dùng tay được, chỉ không đánh dấu sẵn
                return Json(new { thanhCong = false, thongBao = "Chưa đọc được bản quét MAC trên WLC: " + ex.Message });
            }
        }

        /// <summary>Ghi ngay kết quả tra/thêm vào bản chụp. Tra cứu: ThanhCong = "chưa có"; thêm: ThanhCong = vừa thêm.</summary>
        private async Task GhiNhanAsync(string mac, List<WlcMacService.KetQuaWlc> ds, bool laTraCuu, WlcMacService.HoSoWlan? hoSo = null, string? moTa = null)
        {
            foreach (var k in ds)
            {
                if (k.DaTonTai) await _quet.GhiNhanAsync(k.Ip, mac, true, k.HienCo?.WlanProfile, k.HienCo?.AaaList, k.HienCo?.MoTa);
                else if (k.ThanhCong && laTraCuu) await _quet.GhiNhanAsync(k.Ip, mac, false);
                else if (k.ThanhCong) await _quet.GhiNhanAsync(k.Ip, mac, true, hoSo?.WlanProfile, hoSo?.AaaList, moTa);
            }
        }

        public static bool CoQuyen(ClaimsPrincipal user) => user.IsInRole("AdminIT") || user.IsInRole("All");

        public class ThemMacVao
        {
            public int IdFormIt { get; set; }
            public int IdChiTiet { get; set; }
            public int ViTri { get; set; }
            public string? HoSo { get; set; }
            public string? MoTa { get; set; }
            public string? TaiKhoan { get; set; }
            public string? MatKhau { get; set; }
            /// <summary>Chỉ dùng khi gỡ: IP WLC cần gỡ, server đối chiếu với cấu hình công ty.</summary>
            public string? IpWlc { get; set; }
        }

        /// <summary>Kiểm quyền + lấy MAC của dòng thiết bị trong đơn. Lỗi trả thông báo, đúng trả công ty/MAC/tài khoản SSH.</summary>
        private async Task<(string? Loi, string CongTy, string Mac, string TaiKhoan)> LayThietBiAsync(ThemMacVao? vao)
        {
            if (!CoQuyen(User)) return ("Bạn không có quyền thêm MAC lên WLC.", "", "", "");
            var taiKhoan = vao?.TaiKhoan?.Trim() ?? "";
            if (taiKhoan.Length == 0) return ("Nhập tài khoản domain.", "", "", "");
            // Tài khoản đi vào phiên SSH: chỉ nhận dạng tên đăng nhập AD (V240298, bestpacific\v240298, a.b@domain)
            if (!Regex.IsMatch(taiKhoan, @"^[A-Za-z0-9._@\\-]{1,64}$")) return ("Tài khoản domain không hợp lệ.", "", "", "");
            if (string.IsNullOrEmpty(vao!.MatKhau)) return ("Nhập mật khẩu tài khoản domain.", "", "", "");

            var ct = await _context.ItDangKiSuDungWifi3s
                .Where(x => x.Id == vao.IdChiTiet && x.IdFormIt == vao.IdFormIt)
                .Select(x => new { x.MacTb, x.IdFormItNavigation!.TenCongTy })
                .FirstOrDefaultAsync();
            if (ct == null) return ("Không tìm thấy thiết bị trong đơn.", "", "", "");

            var congTy = ct.TenCongTy?.Trim() ?? "";
            if (!_wlc.CoCauHinh(congTy)) return ($"Chưa cấu hình WLC cho công ty {congTy}.", "", "", "");

            var dsMac = (ct.MacTb ?? "").Split('|', StringSplitOptions.RemoveEmptyEntries);
            if (vao.ViTri < 0 || vao.ViTri >= dsMac.Length) return ("Dòng thiết bị không hợp lệ.", "", "", "");
            var mac = WlcMacService.ChuanHoaMac(dsMac[vao.ViTri]);
            if (mac == null) return ($"MAC '{dsMac[vao.ViTri].Trim()}' sai định dạng, sửa đơn trước.", "", "", "");

            return (null, congTy, mac, taiKhoan);
        }

        [HttpPost("/FormIT/KiemTraMacWifi")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> KiemTraMac([FromBody] ThemMacVao? vao)
        {
            var (loi, congTy, mac, taiKhoan) = await LayThietBiAsync(vao);
            if (loi != null) return Json(new { thanhCong = false, thongBao = loi });

            var ds = await _wlc.KiemTraMacAsync(congTy, mac, taiKhoan, vao!.MatKhau!, HttpContext.RequestAborted);
            await GhiNhanAsync(mac, ds, true);
            return TraKetQua(mac, ds);
        }

        /// <summary>
        /// thanhCong = mọi WLC đều xong (đã thêm hoặc đã có sẵn); ketQua là chi tiết từng WLC để hiện riêng.
        /// Khi tra cứu, "chưa có" cũng là tra xong nên trả về thông tin, không coi là lỗi.
        /// </summary>
        private JsonResult TraKetQua(string mac, List<WlcMacService.KetQuaWlc> ds)
        {
            var xong = ds.All(k => k.XongViec);
            var thongBao = xong ? "Đã xong trên tất cả WLC." : string.Join(" · ", ds.Where(k => !k.XongViec).Select(k => $"{k.Wlc}: {k.ThongBao}"));
            return Json(new { thanhCong = xong, thongBao, duLieu = new { mac, ketQua = ds } });
        }

        [HttpPost("/FormIT/ThemMacWifi")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ThemMac([FromBody] ThemMacVao? vao)
        {
            var (loi, congTy, mac, taiKhoan) = await LayThietBiAsync(vao);
            if (loi != null) return Json(new { thanhCong = false, thongBao = loi });

            var hoSo = _wlc.DsHoSo(congTy).FirstOrDefault(h => h.Ma == vao!.HoSo);
            if (hoSo == null) return Json(new { thanhCong = false, thongBao = "Chọn WLAN profile." });
            var moTa = WlcMacService.ChuanHoaMoTa(vao!.MoTa);
            if (!string.IsNullOrWhiteSpace(vao.IpWlc) && _wlc.DsWlc(congTy).All(w => w.Ip != vao.IpWlc.Trim()))
                return Json(new { thanhCong = false, thongBao = "WLC không thuộc cấu hình của công ty." });

            var ds = await _wlc.ThemMacAsync(congTy, mac, hoSo, moTa, taiKhoan, vao.MatKhau!, HttpContext.RequestAborted, vao.IpWlc);

            // Mỗi WLC một dòng lịch sử, ghi cả khi đã tồn tại để biết ai đã kiểm; lỗi đăng nhập/kết nối thì không ghi cho đỡ rác.
            var nguoiLam = $"{User.FindFirst(ClaimTypes.Name)?.Value} ({User.FindFirst("MaNv")?.Value}), SSH bằng {taiKhoan}";
            foreach (var k in ds.Where(k => k.XongViec))
            {
                _context.LichSuFormIts.Add(new LichSuFormIt
                {
                    IdFormIt = vao.IdFormIt,
                    TieuDe = k.ThanhCong ? "Thêm MAC lên WLC" : "MAC đã có trên WLC",
                    Mota = $"{nguoiLam} — {k.Wlc}: " + (k.ThanhCong ? k.Lenh : k.HienCo?.Dong ?? k.ThongBao),
                    Time = DateTime.Now
                });
            }
            if (ds.Any(k => k.XongViec)) await _context.SaveChangesAsync();
            await GhiNhanAsync(mac, ds, false, hoSo, moTa);

            return TraKetQua(mac, ds);
        }

        public class SuaMaThietBiVao
        {
            public int IdFormIt { get; set; }
            public int IdChiTiet { get; set; }
            public int ViTri { get; set; }
            public string? MaCu { get; set; }
            public string? MaMoi { get; set; }
        }

        /// <summary>
        /// Quyền 'All' sửa Mã thiết bị / Định danh của một dòng trong đơn đã gửi (người dùng hay gõ sai tên).
        /// Chỉ đổi đúng phần tử theo vị trí, MacTb giữ nguyên để nút WLC/RADIUS vẫn lấy đúng MAC của dòng đó.
        /// </summary>
        [HttpPost("/FormIT/SuaMaThietBiWifi")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SuaMaThietBi([FromBody] SuaMaThietBiVao? vao)
        {
            if (!User.IsInRole("All")) return Json(new { thanhCong = false, thongBao = "Bạn không có quyền sửa mã thiết bị." });
            if (vao == null) return Json(new { thanhCong = false, thongBao = "Thiếu dữ liệu." });

            var maMoi = vao.MaMoi?.Trim() ?? "";
            if (maMoi.Length == 0) return Json(new { thanhCong = false, thongBao = "Nhập mã thiết bị / định danh." });
            // '|' là dấu ngăn cách các dòng trong cột gộp: lọt vào sẽ làm lệch mã với MAC của các dòng sau
            if (maMoi.Contains('|')) return Json(new { thanhCong = false, thongBao = "Mã thiết bị không được chứa ký tự |." });

            var ct = await _context.ItDangKiSuDungWifi3s
                .FirstOrDefaultAsync(x => x.Id == vao.IdChiTiet && x.IdFormIt == vao.IdFormIt);
            if (ct == null) return Json(new { thanhCong = false, thongBao = "Không tìm thấy thiết bị trong đơn." });

            var dsMa = (ct.MaThietBi ?? "").Split('|', StringSplitOptions.RemoveEmptyEntries).Select(x => x.Trim()).ToArray();
            if (vao.ViTri < 0 || vao.ViTri >= dsMa.Length) return Json(new { thanhCong = false, thongBao = "Dòng thiết bị không hợp lệ." });

            var maHienTai = dsMa[vao.ViTri];
            var moTa = WlcMacService.ChuanHoaMoTa(maMoi);

            // Chốt idempotency: bấm lặp hoặc giữ nguyên giá trị thì không ghi thêm dòng lịch sử
            if (maHienTai == maMoi)
                return Json(new { thanhCong = true, thongBao = "Không có thay đổi.", duLieu = new { maThietBi = maMoi, moTa } });
            if (maHienTai != (vao.MaCu?.Trim() ?? ""))
                return Json(new { thanhCong = false, thongBao = $"Mã đã được đổi thành '{maHienTai}', tải lại trang rồi sửa lại." });

            dsMa[vao.ViTri] = maMoi;
            var chuoiMoi = string.Join(" | ", dsMa);
            if (chuoiMoi.Length > 4000) return Json(new { thanhCong = false, thongBao = "Danh sách mã thiết bị của đơn vượt quá 4000 ký tự." });
            ct.MaThietBi = chuoiMoi;

            _context.LichSuFormIts.Add(new LichSuFormIt
            {
                IdFormIt = vao.IdFormIt,
                TieuDe = "Sửa mã thiết bị",
                Mota = $"{User.FindFirst(ClaimTypes.Name)?.Value} ({User.FindFirst("MaNv")?.Value}) đã sửa dòng {vao.ViTri + 1}: '{maHienTai}' → '{maMoi}'.",
                Time = DateTime.Now
            });
            await _context.SaveChangesAsync();

            return Json(new { thanhCong = true, thongBao = "Đã sửa mã thiết bị.", duLieu = new { maThietBi = maMoi, moTa } });
        }

        public class ThemRadiusVao
        {
            public int IdFormIt { get; set; }
            public int IdChiTiet { get; set; }
            public int ViTri { get; set; }
            public string? Nhom { get; set; }
            public string? MoTa { get; set; }
            public string? TaiKhoan { get; set; }
            public string? MatKhau { get; set; }
        }

        /// <summary>
        /// RADIUS gọi bằng API key nên không có bước SSH kiểm mật khẩu như WLC: tự kiểm tài khoản domain với AD
        /// để lịch sử ghi đúng người làm. Cùng cách đăng nhập domain ở ITFormController (bestpacific.com).
        /// </summary>
        private static bool DungTaiKhoanDomain(string taiKhoan, string matKhau)
        {
            if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows)) return false;
            // "bestpacific240298" -> "v240298"; dạng a@domain để nguyên
            var ten = taiKhoan.Contains('\\') ? taiKhoan[(taiKhoan.LastIndexOf('\\') + 1)..] : taiKhoan;
            try
            {
#pragma warning disable CA1416 // đã kiểm Windows ở trên
                using var ctx = new PrincipalContext(ContextType.Domain, "bestpacific.com");
                return ctx.ValidateCredentials(ten, matKhau);
#pragma warning restore CA1416
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// PDA / TV không xác thực qua WLC mà qua RADIUS (MAC auth): form riêng trong cùng modal, không cần tài khoản domain
        /// vì app RADIUS xác thực bằng API key ở máy chủ. MAC vẫn lấy từ đơn theo vị trí dòng như nút WLC.
        /// </summary>
        /// <summary>Kiểm quyền + lấy MAC của dòng thiết bị cho RADIUS (không cần tài khoản domain).</summary>
        private async Task<(string? Loi, string CongTy, string Mac)> LayMacRadiusAsync(ThemRadiusVao? vao)
        {
            if (!CoQuyen(User)) return ("Bạn không có quyền thêm MAC lên RADIUS.", "", "");
            if (vao == null) return ("Thiếu dữ liệu.", "", "");
            var taiKhoan = vao.TaiKhoan?.Trim() ?? "";
            if (taiKhoan.Length == 0) return ("Nhập tài khoản domain.", "", "");
            if (!Regex.IsMatch(taiKhoan, @"^[A-Za-z0-9._@\\-]{1,64}$")) return ("Tài khoản domain không hợp lệ.", "", "");
            if (string.IsNullOrEmpty(vao.MatKhau)) return ("Nhập mật khẩu tài khoản domain.", "", "");
            if (!DungTaiKhoanDomain(taiKhoan, vao.MatKhau)) return ("Sai tài khoản hoặc mật khẩu domain.", "", "");

            var ct = await _context.ItDangKiSuDungWifi3s
                .Where(x => x.Id == vao.IdChiTiet && x.IdFormIt == vao.IdFormIt)
                .Select(x => new { x.MacTb, x.IdFormItNavigation!.TenCongTy })
                .FirstOrDefaultAsync();
            if (ct == null) return ("Không tìm thấy thiết bị trong đơn.", "", "");

            var dsMac = (ct.MacTb ?? "").Split('|', StringSplitOptions.RemoveEmptyEntries);
            if (vao.ViTri < 0 || vao.ViTri >= dsMac.Length) return ("Dòng thiết bị không hợp lệ.", "", "");
            var mac = WlcMacService.ChuanHoaMac(dsMac[vao.ViTri]);
            if (mac == null) return ($"MAC '{dsMac[vao.ViTri].Trim()}' sai định dạng, sửa đơn trước.", "", "");
            return (null, ct.TenCongTy?.Trim() ?? "", mac);
        }

        /// <summary>App RADIUS chưa có API xoá: IT gỡ tay trên DaloRADIUS rồi đánh dấu ở đây, dòng lịch sử này làm MAC thành "không có".</summary>
        private const string TieuDeGoRadius = "Gỡ MAC khỏi RADIUS (gỡ tay trên DaloRADIUS)";

        public record DongLichSuRadius(int? IdFormIt, string? TieuDe, string? Mota, DateTime? Time);

        /// <summary>Mọi dòng lịch sử RADIUS (ít, chỉ có từ khi có form RADIUS), mới nhất trước.</summary>
        private Task<List<DongLichSuRadius>> LichSuRadiusAsync(string? mac = null)
            => _context.LichSuFormIts
                .Where(x => x.TieuDe != null && x.TieuDe.Contains("RADIUS") && x.Mota != null && (mac == null || x.Mota.Contains(mac)))
                .OrderByDescending(x => x.Time).ThenByDescending(x => x.Id)
                .Select(x => new DongLichSuRadius(x.IdFormIt, x.TieuDe, x.Mota, x.Time))
                .ToListAsync();

        /// <summary>Dòng RADIUS mới nhất của MAC quyết định: thêm / đã có = có, đánh dấu gỡ = không.</summary>
        private static bool CoTrenRadius(List<DongLichSuRadius> ds, string mac)
        {
            var moiNhat = ds.FirstOrDefault(x => x.Mota != null && x.Mota.Contains(mac));
            return moiNhat != null && moiNhat.TieuDe != TieuDeGoRadius;
        }

        /// <summary>
        /// App RADIUS chưa có API tra cứu nên "kiểm tra" = tra lịch sử MỌI đơn.
        /// Không thấy không có nghĩa là RADIUS chưa có (có thể IT thêm tay trên DaloRADIUS) — bấm Thêm thì app tự báo ALREADY_EXISTS.
        /// </summary>
        [HttpPost("/FormIT/KiemTraMacRadius")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> KiemTraMacRadius([FromBody] ThemRadiusVao? vao)
        {
            var (loi, _, mac) = await LayMacRadiusAsync(vao);
            if (loi != null) return Json(new { thanhCong = false, thongBao = loi });

            var ds = await LichSuRadiusAsync(mac);
            var co = CoTrenRadius(ds, mac);
            return Json(new
            {
                thanhCong = true,
                thongBao = co ? "MAC đang có trên RADIUS (theo lịch sử E-Form)."
                    : ds.Count > 0 ? "MAC đã được gỡ khỏi RADIUS (theo lịch sử E-Form)."
                    : "Chưa thấy MAC này được thêm lên RADIUS qua E-Form.",
                duLieu = new { mac, coTrenRadius = co, lichSu = ds.Take(5) }
            });
        }

        [HttpPost("/FormIT/DanhDauGoMacRadius")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DanhDauGoMacRadius([FromBody] ThemRadiusVao? vao)
        {
            var (loi, _, mac) = await LayMacRadiusAsync(vao);
            if (loi != null) return Json(new { thanhCong = false, thongBao = loi });

            // Đã đánh dấu gỡ rồi (bấm lại / 2 tab) thì không ghi thêm dòng
            if (CoTrenRadius(await LichSuRadiusAsync(mac), mac))
            {
                _context.LichSuFormIts.Add(new LichSuFormIt
                {
                    IdFormIt = vao!.IdFormIt,
                    TieuDe = TieuDeGoRadius,
                    Mota = $"{User.FindFirst(ClaimTypes.Name)?.Value} ({User.FindFirst("MaNv")?.Value}), xác thực domain {vao.TaiKhoan!.Trim()} — {mac}",
                    Time = DateTime.Now
                });
                await _context.SaveChangesAsync();
            }
            return Json(new { thanhCong = true, thongBao = "Đã ghi nhận MAC đã gỡ khỏi RADIUS.", duLieu = new { mac } });
        }

        [HttpPost("/FormIT/ThemMacRadius")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ThemMacRadius([FromBody] ThemRadiusVao? vao)
        {
            var (loi, congTy, mac) = await LayMacRadiusAsync(vao);
            if (loi != null) return Json(new { thanhCong = false, thongBao = loi });

            var nhom = _radius.DsNhom(congTy).FirstOrDefault(n => n.Ma == vao!.Nhom);
            if (nhom == null) return Json(new { thanhCong = false, thongBao = $"Chọn loại thiết bị (RADIUS chưa cấu hình cho {congTy} thì không thêm được)." });

            var moTa = WlcMacService.ChuanHoaMoTa(vao!.MoTa);
            var nguoiLam = $"{User.FindFirst(ClaimTypes.Name)?.Value} ({User.FindFirst("MaNv")?.Value}), xác thực domain {vao.TaiKhoan!.Trim()}";
            var k = await _radius.ThemMacAsync(mac, nhom, moTa, $"E-Form don #{vao.IdFormIt} {nhom.LoaiThietBi} - {User.FindFirst("MaNv")?.Value}", HttpContext.RequestAborted);

            if (k.XongViec)
            {
                _context.LichSuFormIts.Add(new LichSuFormIt
                {
                    IdFormIt = vao.IdFormIt,
                    TieuDe = k.ThanhCong ? $"Thêm MAC lên RADIUS ({nhom.LoaiThietBi})" : "MAC đã có trên RADIUS",
                    Mota = $"{nguoiLam} — {mac} group {nhom.Group}, description {moTa}" + (k.Usernames?.Length > 0 ? $" ({string.Join(", ", k.Usernames)})" : ""),
                    Time = DateTime.Now
                });
                await _context.SaveChangesAsync();
            }
            return Json(new { thanhCong = k.XongViec, thongBao = k.ThongBao, duLieu = new { mac, k.Code, k.DaTonTai, k.Usernames } });
        }

        [HttpPost("/FormIT/GoMacWifi")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> GoMac([FromBody] ThemMacVao? vao)
        {
            var (loi, congTy, mac, taiKhoan) = await LayThietBiAsync(vao);
            if (loi != null) return Json(new { thanhCong = false, thongBao = loi });

            var k = await _wlc.GoMacAsync(congTy, vao!.IpWlc ?? "", mac, taiKhoan, vao.MatKhau!, HttpContext.RequestAborted);
            if (k.Lenh != null)
            {
                // Có gửi lệnh xoá thì ghi lịch sử, kể cả khi tra lại thấy WLC vẫn còn MAC
                var nguoiLam = $"{User.FindFirst(ClaimTypes.Name)?.Value} ({User.FindFirst("MaNv")?.Value}), SSH bằng {taiKhoan}";
                _context.LichSuFormIts.Add(new LichSuFormIt
                {
                    IdFormIt = vao.IdFormIt,
                    TieuDe = k.ThanhCong ? "Gỡ MAC khỏi WLC" : "Gỡ MAC khỏi WLC (chưa xác nhận)",
                    Mota = $"{nguoiLam} — {k.Wlc}: {k.Lenh} (trước đó: {k.HienCo?.Dong})",
                    Time = DateTime.Now
                });
                await _context.SaveChangesAsync();
            }
            if (k.ThanhCong) await _quet.GhiNhanAsync(k.Ip, mac, false);
            else if (k.DaTonTai) await _quet.GhiNhanAsync(k.Ip, mac, true, k.HienCo?.WlanProfile, k.HienCo?.AaaList, k.HienCo?.MoTa);
            return Json(new { thanhCong = k.ThanhCong, thongBao = $"{k.Wlc}: {k.ThongBao}", duLieu = new { mac, ketQua = k } });
        }
    }
}
