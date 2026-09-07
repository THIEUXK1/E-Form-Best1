using E_Form_Best.Context;
using E_Form_Best.Models.ITForm;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace E_Form_Best.Areas.ITForm.Controllers
{
    /// <summary>
    /// Lưu cài đặt giao diện theo tài khoản. Hiện mới có hiệu ứng nền (mùa / ngày lễ):
    /// trước đây chỉ nằm ở localStorage nên đổi máy là mất và không biết ai đang bật.
    /// Route để tuyệt đối vì 5 Area đều dùng chung một file JS.
    /// </summary>
    [Area("ITform")]
    [Authorize]
    public class CaiDatGiaoDienController : Controller
    {
        private readonly ITFormContext _context;

        public CaiDatGiaoDienController(ITFormContext context)
        {
            _context = context;
        }

        private int? IdNguoiDungHienTai()
        {
            var s = User.FindFirstValue(ClaimTypes.NameIdentifier);
            return int.TryParse(s, out var id) ? id : null;
        }

        [HttpGet("/CaiDat/HieuUngNen")]
        public async Task<IActionResult> LayHieuUngNen()
        {
            var id = IdNguoiDungHienTai();
            if (id == null) return Json(new { thanhCong = false, bat = (bool?)null });

            // Bảng có thể chưa được tạo trên máy chủ đang chạy -> không để nổ cả trang,
            // JS sẽ tự lùi về localStorage như cũ.
            try
            {
                var bat = await _context.UserHieuUngNens
                    .AsNoTracking()
                    .Where(x => x.IdNguoiDung == id.Value)
                    .Select(x => (bool?)x.Bat)
                    .FirstOrDefaultAsync();

                return Json(new { thanhCong = true, bat });
            }
            catch
            {
                return Json(new { thanhCong = false, bat = (bool?)null });
            }
        }

        [HttpPost("/CaiDat/HieuUngNen")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> LuuHieuUngNen([FromBody] YeuCauHieuUngNen yeuCau)
        {
            var id = IdNguoiDungHienTai();
            if (id == null) return Json(new { thanhCong = false, thongBao = "Chưa đăng nhập" });

            try
            {
                var dong = await _context.UserHieuUngNens.FirstOrDefaultAsync(x => x.IdNguoiDung == id.Value);
                if (dong == null)
                {
                    dong = new UserHieuUngNen { IdNguoiDung = id.Value };
                    _context.UserHieuUngNens.Add(dong);
                }

                // Ghi đè trạng thái mới nhất; bấm nhiều lần chỉ cập nhật một dòng nên không sợ trùng.
                dong.Bat = yeuCau?.Bat ?? false;
                dong.NgayCapNhat = DateTime.Now;
                dong.TenMay = LayTenMay();
                // Bấm bật cũng tính là đang dùng ngay, khỏi chờ tới nhịp ping đầu tiên
                dong.LanCuoiPing = dong.Bat ? DateTime.Now : null;

                await _context.SaveChangesAsync();
                return Json(new { thanhCong = true, bat = dong.Bat });
            }
            catch
            {
                return Json(new { thanhCong = false, thongBao = "Không lưu được cài đặt" });
            }
        }

        /// <summary>
        /// Nhịp tim 30 phút/lần từ trang đang mở, chỉ gửi khi hiệu ứng đang bật.
        /// Chỉ chạm đúng một cột thời gian của đúng một dòng nên gọi lại nhiều lần vô hại;
        /// không tạo dòng mới — ai chưa từng bật thì không nằm trong bảng.
        /// </summary>
        [HttpPost("/CaiDat/HieuUngNen/Ping")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> PingHieuUngNen()
        {
            var id = IdNguoiDungHienTai();
            if (id == null) return Json(new { thanhCong = false });

            try
            {
                var dong = await _context.UserHieuUngNens.FirstOrDefaultAsync(x => x.IdNguoiDung == id.Value);
                if (dong == null || !dong.Bat) return Json(new { thanhCong = false });

                dong.LanCuoiPing = DateTime.Now;
                await _context.SaveChangesAsync();
                return Json(new { thanhCong = true });
            }
            catch
            {
                return Json(new { thanhCong = false });
            }
        }

        private string? LayTenMay()
        {
            var ua = Request.Headers.UserAgent.ToString();
            if (string.IsNullOrWhiteSpace(ua)) return null;
            return ua.Length > 255 ? ua.Substring(0, 255) : ua;
        }

        public class YeuCauHieuUngNen
        {
            public bool Bat { get; set; }
        }
    }
}
