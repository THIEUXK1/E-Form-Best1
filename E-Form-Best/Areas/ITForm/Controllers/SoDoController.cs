using E_Form_Best.Areas.ITForm.Services;
using E_Form_Best.Context;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace E_Form_Best.Areas.ITForm.Controllers
{
    /// <summary>
    /// Sơ đồ mạng (topology) từng công ty: /SoDo/{công ty}. Tự sinh từ dữ liệu đang quản lý
    /// (KK_Switch + KK_AccessPoint + KK_Camera), KHÔNG vẽ tay, KHÔNG có đấu nối cổng vật lý trong DB
    /// nên vẽ theo tầng logic: Công ty → Core → Phân phối → Truy cập, kèm cụm AP (theo controller)
    /// và cụm Camera (theo đầu ghi). Trạng thái Online/Offline lấy đúng như các trang quản lý.
    /// Chỉ đọc; màu/vị trí do client (so-do.js) dựng.
    /// </summary>
    [Area("ITform")]
    public class SoDoController : Controller
    {
        private readonly ITFormContext _context;

        public SoDoController(ITFormContext context) => _context = context;

        // Xem được công ty nếu có quyền xem BẤT KỲ module nào (switch/AP/camera) của công ty đó.
        private bool CoQuyenXem(string congTy)
        {
            var c = congTy.ToUpperInvariant();
            return User.IsInRole("All") || User.IsInRole("AdminIT")
                   || User.IsInRole("Sw" + c) || User.IsInRole("Ap" + c) || User.IsInRole("Cam" + c);
        }

        private static string? CongTyHopLe(string? congTy)
            => SwitchPingWorker.DsCongTy.FirstOrDefault(x => x.Equals(congTy?.Trim(), StringComparison.OrdinalIgnoreCase));

        private Task<int?> IdCongTyAsync(string congTy)
            => _context.KkCongTies.Where(x => x.TenCongTy == congTy).Select(x => (int?)x.IdcongTy).FirstOrDefaultAsync();

        /// <summary>Thiết bị có IP và không ở trạng thái "ngưng/kho" thì mới được coi là đang theo dõi.</summary>
        private static bool TheoDoi(string? ip, string? tinhTrang)
            => !string.IsNullOrWhiteSpace(ip) && (tinhTrang == null || !SwitchPingWorker.DsTinhTrangKhongTheoDoi.Contains(tinhTrang));

        /// <summary>"UP"/"DOWN"/null theo dõi; không theo dõi trả "KHONG" để client tô xám.</summary>
        private static string TrangThai(string? ip, string? tinhTrang, string? ketNoi)
            => !TheoDoi(ip, tinhTrang) ? "KHONG" : (ketNoi ?? "CHUA");

        [HttpGet("/SoDo")]
        public IActionResult Index()
        {
            if (User?.Identity?.IsAuthenticated != true)
                return Redirect("/DonXetDuyet/DangNhap");
            var dau = SwitchPingWorker.DsCongTy.FirstOrDefault(CoQuyenXem);
            if (dau == null) return Forbid();
            return Redirect("/SoDo/" + dau);
        }

        [HttpGet("/SoDo/{congTy:regex(^(BPVN|PFVN|MEGA)$)}")]
        public IActionResult CongTy(string congTy)
        {
            if (User?.Identity?.IsAuthenticated != true)
                return Redirect("/DonXetDuyet/DangNhap");
            var cty = CongTyHopLe(congTy)!;
            if (!CoQuyenXem(cty)) return Forbid();
            ViewBag.CongTy = cty;
            return View("Index");
        }

        [HttpGet("/SoDo/{congTy:regex(^(BPVN|PFVN|MEGA)$)}/DuLieu")]
        public async Task<IActionResult> DuLieu(string congTy)
        {
            var cty = CongTyHopLe(congTy)!;
            if (!CoQuyenXem(cty)) return Json(new { thanhCong = false, thongBao = "Bạn không có quyền xem dữ liệu này." });

            try
            {
                var idCongTy = await IdCongTyAsync(cty);
                if (idCongTy == null)
                    return Json(new { thanhCong = false, thongBao = $"Chưa có công ty {cty} trong danh mục." });

                // ----- Switch -----
                var switches = await _context.KkSwitches
                    .Where(x => x.NgayXoa == null && x.IdcongTy == idCongTy)
                    .OrderBy(x => x.TenSwitch)
                    .Select(x => new { x.IdSwitch, x.TenSwitch, x.DiaChiIp, x.VaiTro, x.TinhTrang, x.TrangThaiKetNoi })
                    .ToListAsync();

                object SwNode(dynamic s) => new
                {
                    id = "sw" + s.IdSwitch,
                    ten = s.TenSwitch,
                    ip = s.DiaChiIp,
                    tt = TrangThai(s.DiaChiIp, s.TinhTrang, s.TrangThaiKetNoi)
                };

                var core = switches.Where(x => x.VaiTro == "Core").Select(SwNode).ToList();
                var phanPhoi = switches.Where(x => x.VaiTro == "Phân phối").Select(SwNode).ToList();
                // Truy cập + switch chưa gán vai trò gộp chung tầng truy cập
                var truyCap = switches.Where(x => x.VaiTro != "Core" && x.VaiTro != "Phân phối").Select(SwNode).ToList();

                // ----- AP: gom theo controller (trống thì "Khác") -----
                var aps = await _context.KkAccessPoints
                    .Where(x => x.NgayXoa == null && x.IdcongTy == idCongTy)
                    .OrderBy(x => x.TenAp)
                    .Select(x => new { x.IdAp, x.TenAp, x.DiaChiIp, x.TenController, x.TinhTrang, x.TrangThaiKetNoi })
                    .ToListAsync();

                var apNhom = aps
                    .GroupBy(x => string.IsNullOrWhiteSpace(x.TenController) ? "Chưa rõ controller" : x.TenController!.Trim())
                    .OrderBy(g => g.Key)
                    .Select(g => new
                    {
                        ten = g.Key,
                        items = g.Select(x => new
                        {
                            id = "ap" + x.IdAp,
                            ten = x.TenAp,
                            ip = x.DiaChiIp,
                            tt = TrangThai(x.DiaChiIp, x.TinhTrang, x.TrangThaiKetNoi)
                        }).ToList()
                    }).ToList();

                // ----- Camera: gom theo đầu ghi; trạng thái join KK_CameraTrangThai theo IP camera -----
                var cams = await _context.KkCameras
                    .Where(x => x.NgayXoa == null && x.IdcongTy == idCongTy)
                    .OrderBy(x => x.TenCamera)
                    .Select(x => new { x.IdCamera, x.TenCamera, x.DiaChiIp, x.DauGhi, x.TinhTrang })
                    .ToListAsync();

                // IP camera -> trạng thái gần nhất (bản ghi mới nhất nếu trùng IP)
                var ipCam = cams.Where(x => x.DiaChiIp != null).Select(x => x.DiaChiIp!).Distinct().ToList();
                var ttCam = ipCam.Count == 0
                    ? new Dictionary<string, string>()
                    : (await _context.KkCameraTrangThais
                        .Where(x => x.IpCamera != null && ipCam.Contains(x.IpCamera))
                        .OrderByDescending(x => x.CapNhatLuc)
                        .Select(x => new { x.IpCamera, x.TrangThai })
                        .ToListAsync())
                      .GroupBy(x => x.IpCamera!)
                      .ToDictionary(g => g.Key, g => g.First().TrangThai);

                string TtCamera(string? ip, string? tinhTrang)
                {
                    if (!TheoDoi(ip, tinhTrang)) return "KHONG";
                    return ttCam.TryGetValue(ip!, out var tt) ? tt : "CHUA";
                }

                var camNhom = cams
                    .GroupBy(x => string.IsNullOrWhiteSpace(x.DauGhi) ? "Chưa gán đầu ghi" : x.DauGhi!.Trim())
                    .OrderBy(g => g.Key)
                    .Select(g => new
                    {
                        ten = g.Key,
                        items = g.Select(x => new
                        {
                            id = "cam" + x.IdCamera,
                            ten = x.TenCamera,
                            ip = x.DiaChiIp,
                            tt = TtCamera(x.DiaChiIp, x.TinhTrang)
                        }).ToList()
                    }).ToList();

                int Up(IEnumerable<string> tts) => tts.Count(t => t == "UP");
                int Down(IEnumerable<string> tts) => tts.Count(t => t == "DOWN");

                var ttSwitch = switches.Select(x => TrangThai(x.DiaChiIp, x.TinhTrang, x.TrangThaiKetNoi)).ToList();
                var ttAp = aps.Select(x => TrangThai(x.DiaChiIp, x.TinhTrang, x.TrangThaiKetNoi)).ToList();
                var ttCamList = cams.Select(x => TtCamera(x.DiaChiIp, x.TinhTrang)).ToList();

                return Json(new
                {
                    thanhCong = true,
                    congTy = cty,
                    core,
                    phanPhoi,
                    truyCap,
                    apNhom,
                    camNhom,
                    thongKe = new
                    {
                        sw = new { tong = switches.Count, up = Up(ttSwitch), down = Down(ttSwitch) },
                        ap = new { tong = aps.Count, up = Up(ttAp), down = Down(ttAp) },
                        cam = new { tong = cams.Count, up = Up(ttCamList), down = Down(ttCamList) }
                    }
                });
            }
            catch (Exception ex) when (ex is SqlException or InvalidOperationException)
            {
                return Json(new { thanhCong = false, thongBao = ex.Message });
            }
        }
    }
}
