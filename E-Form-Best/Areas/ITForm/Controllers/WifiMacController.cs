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

        public WifiMacController(ITFormContext context, WlcMacService wlc, WlcMacQuetService quet)
        {
            _context = context;
            _wlc = wlc;
            _quet = quet;
        }

        /// <summary>
        /// Trạng thái MAC từng dòng thiết bị của đơn trên từng WLC, theo bản chụp do job quét / thao tác trước ghi lại,
        /// để trang chi tiết đánh dấu sẵn nút "+" mà không cần mật khẩu domain.
        /// </summary>
        [HttpGet("/FormIT/TrangThaiMacWifi/{idFormIt:int}")]
        public async Task<IActionResult> TrangThaiMac(int idFormIt)
        {
            if (!CoQuyen(User)) return Json(new { thanhCong = false, thongBao = "Bạn không có quyền xem MAC trên WLC." });
            try
            {
                var dsCt = await _context.ItDangKiSuDungWifi3s
                    .Where(x => x.IdFormIt == idFormIt)
                    .Select(x => new { x.Id, x.MacTb, x.IdFormItNavigation!.TenCongTy })
                    .ToListAsync();
                var congTy = dsCt.FirstOrDefault()?.TenCongTy?.Trim() ?? "";
                if (dsCt.Count == 0 || !_wlc.CoCauHinh(congTy)) return Json(new { thanhCong = true, duLieu = Array.Empty<object>() });

                var dong = dsCt.SelectMany(ct => (ct.MacTb ?? "").Split('|', StringSplitOptions.RemoveEmptyEntries)
                        .Select((m, i) => new { idChiTiet = ct.Id, viTri = i, mac = WlcMacService.ChuanHoaMac(m) }))
                    .Where(x => x.mac != null).ToList();
                var trangThai = await _quet.TrangThaiAsync(congTy, dong.Select(x => x.mac!));
                return Json(new
                {
                    thanhCong = true,
                    duLieu = dong.Select(x => new { x.idChiTiet, x.viTri, wlc = trangThai[x.mac!] })
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

            var ds = await _wlc.ThemMacAsync(congTy, mac, hoSo, moTa, taiKhoan, vao.MatKhau!, HttpContext.RequestAborted);

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
