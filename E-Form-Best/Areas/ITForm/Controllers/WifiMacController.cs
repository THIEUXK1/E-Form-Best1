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

        public WifiMacController(ITFormContext context, WlcMacService wlc)
        {
            _context = context;
            _wlc = wlc;
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

            var kq = await _wlc.KiemTraMacAsync(congTy, mac, taiKhoan, vao!.MatKhau!, HttpContext.RequestAborted);
            return Json(new { thanhCong = kq.ThanhCong, thongBao = kq.ThongBao, duLieu = new { mac, daTonTai = kq.HienCo != null, hienCo = kq.HienCo } });
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

            var kq = await _wlc.ThemMacAsync(congTy, mac, hoSo, moTa, taiKhoan, vao.MatKhau!, HttpContext.RequestAborted);

            // Ghi lịch sử cả khi đã tồn tại để biết ai đã kiểm; lỗi đăng nhập/kết nối thì không ghi cho đỡ rác.
            if (kq.ThanhCong || kq.DaTonTai)
            {
                _context.LichSuFormIts.Add(new LichSuFormIt
                {
                    IdFormIt = vao.IdFormIt,
                    TieuDe = kq.ThanhCong ? "Thêm MAC lên WLC" : "MAC đã có trên WLC",
                    Mota = $"{User.FindFirst(ClaimTypes.Name)?.Value} ({User.FindFirst("MaNv")?.Value}), SSH bằng {taiKhoan}: "
                           + (kq.ThanhCong ? WlcMacService.LenhThem(mac, hoSo, moTa) : kq.ThongBao),
                    Time = DateTime.Now
                });
                await _context.SaveChangesAsync();
            }

            return Json(new { thanhCong = kq.ThanhCong, thongBao = kq.ThongBao, duLieu = new { mac, daTonTai = kq.DaTonTai, hienCo = kq.HienCo } });
        }
    }
}
