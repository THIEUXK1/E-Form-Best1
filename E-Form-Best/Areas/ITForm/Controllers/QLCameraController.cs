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
    /// Quản lý camera giám sát của bộ phận IT (nhóm menu "Máy in & CCDC").
    /// Tách controller riêng như QLCCDC, không nhồi thêm vào ITFormController.
    /// </summary>
    [Area("ITform")]
    public class QLCameraController : Controller
    {
        private readonly ITFormContext _context;
        private readonly MayInQuetService _mayInQuetService;
        private readonly CameraGiamSatService _giamSat;

        // Danh mục cố định, đổi ở đây là view + lọc ăn theo
        public static readonly string[] DsTinhTrang = { "Đang hoạt động", "Hư hỏng", "Ngừng sử dụng", "Trong kho" };
        public static readonly string[] DsTrangThaiDauGhi = { "Đang dùng", "Đã tháo", "Ngừng dùng" };

        public QLCameraController(ITFormContext context, MayInQuetService mayInQuetService, CameraGiamSatService giamSat)
        {
            _context = context;
            _mayInQuetService = mayInQuetService;
            _giamSat = giamSat;
        }

        // "All" toàn quyền camera cả 3 công ty; "CamBPVN" / "CamPFVN" / "CamMEGA" chỉ được xem camera công ty đó.
        // AdminIT không còn tự có quyền camera. Ẩn nút ở UI không phải phân quyền nên mọi action đều phải gọi.
        private bool CoQuyenXem(string congTy) => User.IsInRole("All") || User.IsInRole("Cam" + congTy.ToUpperInvariant());
        private bool CoQuyenSua() => User.IsInRole("All");

        /// <summary>Công ty gửi lên từ trang camera; giá trị lạ / bỏ trống coi là BPVN.</summary>
        private static string CongTyHopLe(string? congTy)
            => CameraXemTrucTiepService.DsCongTyAnhLuu.FirstOrDefault(x => x.Equals(congTy, StringComparison.OrdinalIgnoreCase)) ?? "BPVN";

        #region View

        [HttpGet("/QLCamera")]
        public async Task<IActionResult> Index()
        {
            if (User?.Identity?.IsAuthenticated != true)
                return Redirect("/DonXetDuyet/DangNhap");

            if (!CoQuyenXem("BPVN"))
                return Forbid();

            // Chiếu vào entity public, không dùng anonymous type (xem ghi chú ở QLCCDCController.Index)
            ViewBag.DsCongTy = await _context.KkCongTies
                .OrderBy(x => x.TenCongTy)
                .Select(x => new KkCongTy { IdcongTy = x.IdcongTy, TenCongTy = x.TenCongTy })
                .ToListAsync();

            ViewBag.DsBoPhan = await _context.KkBoPhans
                .OrderBy(x => x.TenBoPhan)
                .Select(x => new KkBoPhan { IdboPhan = x.IdboPhan, TenBoPhan = x.TenBoPhan, IdcongTy = x.IdcongTy })
                .ToListAsync();

            ViewBag.DsTinhTrang = DsTinhTrang;
            ViewBag.DsTrangThaiDauGhi = DsTrangThaiDauGhi;
            ViewBag.CoQuyenSua = CoQuyenSua();

            return View();
        }

        /// <summary>
        /// Camera PFVN / MEGA: dùng chung giao diện trang BPVN (tab Giám sát) nhưng dữ liệu là danh sách kênh
        /// + ảnh lưu do script chụp ảnh bên đó đẩy về (không có hệ thống giám sát ISAPI, không xem trực tiếp
        /// được vì máy chủ không thông mạng đầu ghi). Công ty chưa có dữ liệu thì hiện khung chờ.
        /// </summary>
        [HttpGet("/QLCamera/{congTy:regex(^(PFVN|MEGA)$)}")]
        public async Task<IActionResult> CongTyKhac(string congTy, [FromServices] CameraXemTrucTiepService xem)
        {
            if (User?.Identity?.IsAuthenticated != true)
                return Redirect("/DonXetDuyet/DangNhap");

            if (!CoQuyenXem(congTy))
                return Forbid();

            var cty = CongTyHopLe(congTy);
            ViewBag.CongTy = cty;

            var (capNhat, kenh) = await xem.DanhSachKenhCongTyAsync(cty, HttpContext.RequestAborted);
            if (capNhat == null && kenh.Count == 0) return View("CongTyKhac");

            ViewBag.CoQuyenSua = CoQuyenSua();
            ViewBag.CoXemTrucTiep = xem.CoXemTrucTiepCongTy(cty);
            ViewBag.CoVideo = xem.CoVideoCongTy(cty);
            return View("Index");
        }

        /// <summary>
        /// Dữ liệu tab Giám sát cho PFVN/MEGA, dựng từ "_kenh.json" theo đúng khuôn JSON của hệ thống giám sát
        /// BPVN (snake_case) để camera-giam-sat.js dùng lại nguyên cách vẽ. Trạng thái là lúc chụp ảnh gần nhất.
        /// </summary>
        [HttpGet("/QLCamera/{congTy:regex(^(PFVN|MEGA)$)}/GiamSat/{loai:regex(^(TongQuan|Camera|DauGhi|DsAnhLuu|DiaChiDauGhi)$)}")]
        public async Task<IActionResult> CongTyKhacGiamSat(string congTy, string loai, [FromServices] CameraXemTrucTiepService xem)
        {
            var cty = CongTyHopLe(congTy);
            if (!CoQuyenXem(cty)) return Json(new { thanhCong = false, thongBao = "Bạn không có quyền xem dữ liệu này." });

            try
            {
                var (capNhat, ds) = await xem.DanhSachKenhCongTyAsync(cty, HttpContext.RequestAborted);
                // Mốc đổi trạng thái do job CameraLichSuWorker ghi (KK_CameraTrangThai), giống BPVN
                var dsIp = ds.Select(x => x.Nvr).Distinct().ToList();
                var doiLuc = loai == "Camera"
                    ? (await _context.KkCameraTrangThais.Where(x => dsIp.Contains(x.NvrIp))
                        .Select(x => new { x.NvrIp, x.Kenh, x.DoiLuc }).ToListAsync())
                        .ToDictionary(x => (x.NvrIp, x.Kenh), x => x.DoiLuc)
                    : new Dictionary<(string, int), DateTime?>();
                var dsDauGhi = ds.GroupBy(x => x.Nvr).Select(g => new
                {
                    nvr_ip = g.Key,
                    // Tên đầu ghi để mặc định "Network Video Recorder" thì đặt theo công ty + đuôi IP cho dễ phân biệt
                    nvr_name = CameraXemTrucTiepService.TenDauGhiCongTy(cty, g.Key, g.First().TenDauGhi),
                    zone = (string?)null,
                    channel_count = g.Count(),
                    readiness = "READY",
                    last_error = (string?)null,
                    last_poll_at = capNhat
                }).ToList();

                // Địa chỉ web đầu ghi theo đúng cổng/https (script bên đó gửi kèm trong file trạng thái)
                var diaChi = loai == "DiaChiDauGhi" ? await xem.DiaChiDauGhiCongTyAsync(cty, HttpContext.RequestAborted) : null;

                object duLieu = loai switch
                {
                    "TongQuan" => new
                    {
                        total = ds.Count,
                        up = ds.Count(x => x.Online),
                        down = ds.Count(x => !x.Online),
                        nvrs_ready = dsDauGhi.Count,
                        nvr_total = dsDauGhi.Count,
                        nvr_errors = 0,
                        excluded_count = 0,
                        watchlist_count = 0,
                        last_poll_at = capNhat,
                        last_poll_status = "ok"
                    },
                    "DauGhi" => new { nvrs = dsDauGhi },
                    "Camera" => new
                    {
                        cameras = ds.Select(x => new
                        {
                            nvr_ip = x.Nvr,
                            nvr_name = CameraXemTrucTiepService.TenDauGhiCongTy(cty, x.Nvr, x.TenDauGhi),
                            cam_id = x.Kenh,
                            name = string.IsNullOrWhiteSpace(x.Ten) ? $"Kênh {x.Kenh}" : x.Ten,
                            ip = x.IpCamera,
                            status = x.Online ? "UP" : "DOWN",
                            zone = (string?)null,
                            // Mốc rớt theo lần kiểm tra 5 phút; chưa có (job chưa thấy đổi) mà ảnh lấy từ bản ghi
                            // thì dùng giờ khung hình ghi cuối
                            down_since_at = x.Online ? null
                                : doiLuc.GetValueOrDefault((x.Nvr, x.Kenh)) ?? (x.KetQua is "PLAYBACK" or "BOQUA" ? x.AnhLuc : null),
                            status_changed_at = doiLuc.GetValueOrDefault((x.Nvr, x.Kenh)),
                            excluded = false,
                            is_watchlist = false
                        })
                    },
                    "DsAnhLuu" => ds.Where(x => x.AnhLuc != null).Select(x => x.Nvr + "|" + x.Kenh).ToList(),
                    _ => diaChi!
                };
                return Json(new { thanhCong = true, duLieu });
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or System.Text.Json.JsonException)
            {
                return Json(new { thanhCong = false, thongBao = "Không đọc được dữ liệu camera " + cty + ": " + ex.Message });
            }
        }


        /// <summary>Danh sách kênh camera PFVN/MEGA kèm giờ ảnh lưu (đọc "_kenh.json" trong thư mục ảnh lưu của công ty).</summary>
        [HttpGet("/QLCamera/{congTy:regex(^(PFVN|MEGA)$)}/DanhSach")]
        public async Task<IActionResult> CongTyKhacDanhSach(string congTy, [FromServices] CameraXemTrucTiepService xem)
        {
            if (!CoQuyenXem(congTy)) return Json(new { thanhCong = false, thongBao = "Bạn không có quyền xem dữ liệu này." });
            try
            {
                var (capNhat, kenh) = await xem.DanhSachKenhCongTyAsync(congTy, HttpContext.RequestAborted);
                return Json(new { thanhCong = true, duLieu = new { capNhat, kenh } });
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or System.Text.Json.JsonException)
            {
                return Json(new { thanhCong = false, thongBao = "Không đọc được dữ liệu camera " + congTy.ToUpperInvariant() + ": " + ex.Message });
            }
        }

        /// <summary>Ảnh lưu sẵn của 1 kênh PFVN/MEGA; giờ chụp trả qua header X-Chup-Luc.</summary>
        [HttpGet("/QLCamera/{congTy:regex(^(PFVN|MEGA)$)}/AnhLuu")]
        public async Task<IActionResult> CongTyKhacAnhLuu(string congTy, string? nvrIp, int kenh, [FromServices] CameraXemTrucTiepService xem)
        {
            if (!CoQuyenXem(congTy)) return StatusCode(403);
            if (kenh < 1 || kenh > 512 || string.IsNullOrWhiteSpace(nvrIp)) return BadRequest();

            try
            {
                var ct = HttpContext.RequestAborted;
                // Chỉ phục vụ kênh có trong danh sách của công ty — không cho đoán tên file tuỳ ý
                var (_, ds) = await xem.DanhSachKenhCongTyAsync(congTy, ct);
                if (!ds.Any(x => x.Nvr == nvrIp.Trim() && x.Kenh == kenh)) return NotFound();

                var luu = await xem.LayAnhLuuAsync(nvrIp.Trim(), kenh, ct, congTy);
                if (luu == null) return NotFound();

                Response.Headers.CacheControl = "no-store";
                Response.Headers["X-Chup-Luc"] = luu.Value.chupLuc.ToString("yyyy-MM-ddTHH:mm:ss");
                return File(luu.Value.anh, "image/jpeg");
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or System.Text.Json.JsonException)
            {
                return StatusCode(502);
            }
        }

        #endregion

        #region Giám sát (đọc từ hệ thống ISAPI 10.0.60.238:3005)

        [HttpGet("/QLCamera/GiamSat/TongQuan")]
        public Task<IActionResult> GiamSatTongQuan()
            => DocGiamSatAsync(ct => _giamSat.TongQuanAsync(ct));

        [HttpGet("/QLCamera/GiamSat/DauGhi")]
        public Task<IActionResult> GiamSatDauGhi()
            => DocGiamSatAsync(ct => _giamSat.DanhSachDauGhiAsync(ct));

        [HttpGet("/QLCamera/GiamSat/Camera")]
        public Task<IActionResult> GiamSatCamera(string? trangThai, string? nvrIp, string? khuVuc, bool gomDaLoaiTru = false, bool chiCanChuY = false)
            => DocGiamSatAsync(ct => _giamSat.DanhSachCameraAsync(trangThai, nvrIp, khuVuc, gomDaLoaiTru, chiCanChuY, ct));

        /// <summary>Địa chỉ trang web của từng đầu ghi (đúng cổng/https theo inventory) để mở ở tab mới.</summary>
        [HttpGet("/QLCamera/GiamSat/DiaChiDauGhi")]
        public async Task<IActionResult> GiamSatDiaChiDauGhi([FromServices] CameraXemTrucTiepService xem)
        {
            if (!CoQuyenXem("BPVN")) return Json(new { thanhCong = false, thongBao = "Bạn không có quyền xem dữ liệu này." });
            var duLieu = await xem.BangDiaChiNvrAsync(HttpContext.RequestAborted);
            return Json(new { thanhCong = true, duLieu });
        }

        /// <summary>Kênh nào đã có ảnh lưu sẵn (khoá "nvrIp|kênh"); null khi máy chủ chưa có thư mục ảnh lưu.</summary>
        [HttpGet("/QLCamera/GiamSat/DsAnhLuu")]
        public IActionResult GiamSatDsAnhLuu([FromServices] CameraXemTrucTiepService xem)
        {
            if (!CoQuyenXem("BPVN")) return Json(new { thanhCong = false, thongBao = "Bạn không có quyền xem dữ liệu này." });
            try
            {
                return Json(new { thanhCong = true, duLieu = xem.DanhSachKenhCoAnhLuu() });
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                return Json(new { thanhCong = false, thongBao = "Không đọc được thư mục ảnh lưu: " + ex.Message });
            }
        }

        [HttpGet("/QLCamera/GiamSat/LichSu")]
        public Task<IActionResult> GiamSatLichSu(int soLan = 72)
            => DocGiamSatAsync(ct => _giamSat.LichSuKiemTraAsync(soLan, ct));

        /// <summary>
        /// Bọc chung: kiểm quyền + đổi lỗi mạng/HTTP của hệ thống bên kia thành thông báo dễ hiểu,
        /// để trang vẫn hiện tab tài sản / đầu ghi chi nhánh khi hệ thống giám sát đang sập.
        /// </summary>
        private async Task<IActionResult> DocGiamSatAsync(Func<CancellationToken, Task<System.Text.Json.JsonElement>> doc)
        {
            if (!CoQuyenXem("BPVN")) return Json(new { thanhCong = false, thongBao = "Bạn không có quyền xem dữ liệu này." });

            try
            {
                var duLieu = await doc(HttpContext.RequestAborted);
                return Json(new { thanhCong = true, duLieu });
            }
            catch (HttpRequestException)
            {
                return Json(new { thanhCong = false, thongBao = "Không kết nối được hệ thống giám sát camera (10.0.60.238:3005)." });
            }
            catch (TaskCanceledException) when (!HttpContext.RequestAborted.IsCancellationRequested)
            {
                return Json(new { thanhCong = false, thongBao = "Hệ thống giám sát camera không phản hồi (quá thời gian chờ)." });
            }
            catch (InvalidOperationException ex) { return Json(new { thanhCong = false, thongBao = ex.Message }); }
        }

        #endregion

        #region Ghi chú camera (KK_CameraGhiChu, khoá đầu ghi + kênh)

        /// <summary>Toàn bộ ghi chú đang có, JS ghép vào danh sách camera theo "nvrIp|kenh".</summary>
        [HttpGet("/QLCamera/GhiChu/DanhSach")]
        public async Task<IActionResult> GhiChuDanhSach(string? congTy)
        {
            // Ghi chú dùng chung bảng cho mọi công ty (khoá nvrIp|kênh, IP đầu ghi các công ty không trùng nhau)
            if (!CoQuyenXem(CongTyHopLe(congTy))) return Json(new { thanhCong = false, thongBao = "Bạn không có quyền xem dữ liệu này." });

            try
            {
                var duLieu = await _context.KkCameraGhiChus
                    .Where(x => x.GhiChu != null)
                    .Select(x => new { x.NvrIp, x.Kenh, x.GhiChu, x.NguoiCapNhat, x.NgayCapNhat })
                    .ToListAsync();
                return Json(new { thanhCong = true, duLieu });
            }
            catch (Exception ex) { return Json(new { thanhCong = false, thongBao = ex.Message }); }
        }

        [HttpPost("/QLCamera/GhiChu/Luu")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> GhiChuLuu(string? nvrIp, int kenh, string? tenCamera, string? ghiChu,
            [FromServices] CameraXemTrucTiepService xem, string? congTy = null)
        {
            var cty = CongTyHopLe(congTy);
            if (!CoQuyenSua()) return Json(new { thanhCong = false, thongBao = "Bạn không có quyền thao tác." });
            if (kenh < 1 || kenh > 512) return Json(new { thanhCong = false, thongBao = "Kênh không hợp lệ." });

            ghiChu = string.IsNullOrWhiteSpace(ghiChu) ? null : ghiChu.Trim();
            if (ghiChu?.Length > 1000) return Json(new { thanhCong = false, thongBao = "Ghi chú tối đa 1000 ký tự." });

            try
            {
                // Chỉ nhận đầu ghi có thật (BPVN: hệ thống giám sát; PFVN/MEGA: danh sách kênh của công ty),
                // không cho ghi rác theo IP tuỳ ý
                var hopLe = cty == "BPVN"
                    ? await xem.LaDauGhiHopLeAsync(nvrIp, HttpContext.RequestAborted)
                    : !string.IsNullOrWhiteSpace(nvrIp) && (await xem.DanhSachKenhCongTyAsync(cty, HttpContext.RequestAborted)).kenh
                        .Any(x => x.Nvr == nvrIp.Trim() && x.Kenh == kenh);
                if (!hopLe)
                    return Json(new { thanhCong = false, thongBao = "Đầu ghi không có trong hệ thống giám sát." });
                nvrIp = nvrIp!.Trim();

                var db = await _context.KkCameraGhiChus.FirstOrDefaultAsync(x => x.NvrIp == nvrIp && x.Kenh == kenh);
                var truoc = db?.GhiChu;
                if (truoc == ghiChu)
                    return Json(new { thanhCong = true, thongBao = "Không có thay đổi.", duLieu = new { ghiChu } });

                if (db == null)
                {
                    db = new KkCameraGhiChu { NvrIp = nvrIp, Kenh = kenh };
                    _context.KkCameraGhiChus.Add(db);
                }
                db.TenCamera = string.IsNullOrWhiteSpace(tenCamera) ? db.TenCamera : tenCamera.Trim()[..Math.Min(tenCamera.Trim().Length, 255)];
                db.GhiChu = ghiChu;
                db.NguoiCapNhat = User.Identity?.Name;
                db.NgayCapNhat = DateTime.Now;

                await _context.SaveChangesAsync();
                await GhiLichSuAsync("Cập nhật ghi chú", db.IdGhiChu,
                    $"Camera {db.TenCamera} ({nvrIp} kênh {kenh}): \"{truoc ?? ""}\" → \"{ghiChu ?? ""}\"", "Ghi chú camera");

                return Json(new { thanhCong = true, thongBao = "Đã lưu ghi chú.", duLieu = new { ghiChu, db.NguoiCapNhat, db.NgayCapNhat } });
            }
            catch (DbUpdateException ex) when (ex.InnerException is SqlException sql && (sql.Number == 2601 || sql.Number == 2627))
            {
                // Hai người cùng ghi chú lần đầu cho một kênh: người sau trúng unique index
                return Json(new { thanhCong = false, thongBao = "Có người vừa ghi chú camera này, tải lại rồi thử lại." });
            }
            catch (Exception ex) { return Json(new { thanhCong = false, thongBao = ex.Message }); }
        }

        #endregion

        #region Lịch sử đổi trạng thái (KK_CameraLichSu, do CameraLichSuWorker ghi)

        [HttpGet("/QLCamera/LichSu/DanhSach")]
        public async Task<IActionResult> LichSuDanhSach(DateTime? tuNgay, DateTime? denNgay, string? loai, string? nvrIp, string? tuKhoa,
            string? congTy, [FromServices] CameraXemTrucTiepService xem)
        {
            var cty = CongTyHopLe(congTy);
            if (!CoQuyenXem(cty)) return Json(new { thanhCong = false, thongBao = "Bạn không có quyền xem dữ liệu này." });

            const int toiDa = 2000;
            try
            {
                var tu = (tuNgay ?? DateTime.Today.AddDays(-6)).Date;
                var den = (denNgay ?? DateTime.Today).Date.AddDays(1);   // gồm trọn ngày cuối

                // KK_CameraLichSu dùng chung mọi công ty, tách theo IP đầu ghi: PFVN/MEGA lấy đúng đầu ghi của mình,
                // BPVN thì bỏ đầu ghi của các công ty kia
                var ipCongTy = new Dictionary<string, List<string>>();
                foreach (var c in CameraXemTrucTiepService.DsCongTyAnhLuu)
                    ipCongTy[c] = (await xem.DanhSachKenhCongTyAsync(c, HttpContext.RequestAborted)).kenh.Select(x => x.Nvr).Distinct().ToList();
                var ipKhac = ipCongTy.Where(x => x.Key != cty).SelectMany(x => x.Value).ToList();
                var ipCuaCongTy = cty == "BPVN" ? new List<string>() : ipCongTy[cty];

                var goc = cty == "BPVN"
                    ? _context.KkCameraLichSus.Where(x => !ipKhac.Contains(x.NvrIp))
                    : _context.KkCameraLichSus.Where(x => ipCuaCongTy.Contains(x.NvrIp));
                var query = goc.Where(x => x.ThoiGian >= tu && x.ThoiGian < den);
                if (!string.IsNullOrWhiteSpace(loai)) query = query.Where(x => x.SangTrangThai == loai);
                if (!string.IsNullOrWhiteSpace(nvrIp)) query = query.Where(x => x.NvrIp == nvrIp);
                if (!string.IsNullOrWhiteSpace(tuKhoa))
                {
                    var kw = tuKhoa.Trim();
                    query = query.Where(x =>
                        (x.TenCamera != null && x.TenCamera.Contains(kw)) ||
                        (x.IpCamera != null && x.IpCamera.Contains(kw)) ||
                        (x.TenDauGhi != null && x.TenDauGhi.Contains(kw)) ||
                        x.NvrIp.Contains(kw) ||
                        (x.KhuVuc != null && x.KhuVuc.Contains(kw)));
                }

                // Truy vấn đếm riêng từng cái: GroupBy + Distinct lồng nhau dễ không dịch được sang SQL
                var tongHop = new
                {
                    tong = await query.CountAsync(),
                    matKetNoi = await query.CountAsync(x => x.SangTrangThai == "DOWN"),
                    hoatDongLai = await query.CountAsync(x => x.SangTrangThai == "UP"),
                    soCamera = await query.Select(x => new { x.NvrIp, x.Kenh }).Distinct().CountAsync()
                };

                var duLieu = await query
                    .OrderByDescending(x => x.ThoiGian).ThenByDescending(x => x.IdLichSu)
                    .Take(toiDa)
                    .Select(x => new
                    {
                        x.ThoiGian, x.NvrIp, x.TenDauGhi, x.KhuVuc, x.Kenh, x.TenCamera, x.IpCamera,
                        x.TuTrangThai, x.SangTrangThai, x.ThoiLuongGiay
                    })
                    .ToListAsync();

                // Mốc sự kiện sớm nhất: lịch sử chỉ có từ lúc job nền bắt đầu chạy, không có quá khứ
                var dauTien = await goc.MinAsync(x => (DateTime?)x.ThoiGian);

                return Json(new
                {
                    thanhCong = true,
                    duLieu,
                    tongHop,
                    biCat = tongHop.tong > toiDa,
                    toiDa,
                    batDauGhi = dauTien
                });
            }
            catch (Exception ex) { return Json(new { thanhCong = false, thongBao = ex.Message }); }
        }

        #endregion

        #region Ghi chú có sẵn (KK_CameraGhiChuMau, dùng chung)

        [HttpGet("/QLCamera/GhiChuMau/DanhSach")]
        public async Task<IActionResult> GhiChuMauDanhSach(string? congTy)
        {
            if (!CoQuyenXem(CongTyHopLe(congTy))) return Json(new { thanhCong = false, thongBao = "Bạn không có quyền xem dữ liệu này." });

            try
            {
                var duLieu = await _context.KkCameraGhiChuMaus
                    .Where(x => x.NgayXoa == null)
                    .OrderBy(x => x.NoiDung)
                    .Select(x => new { x.IdMau, x.NoiDung })
                    .ToListAsync();
                return Json(new { thanhCong = true, duLieu });
            }
            catch (Exception ex) { return Json(new { thanhCong = false, thongBao = ex.Message }); }
        }

        [HttpPost("/QLCamera/GhiChuMau/Them")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> GhiChuMauThem(string? noiDung)
        {
            if (!CoQuyenSua()) return Json(new { thanhCong = false, thongBao = "Bạn không có quyền thao tác." });

            noiDung = noiDung?.Trim();
            if (string.IsNullOrEmpty(noiDung)) return Json(new { thanhCong = false, thongBao = "Gõ nội dung ghi chú trước rồi bấm +." });
            if (noiDung.Length > 200) return Json(new { thanhCong = false, thongBao = "Ghi chú có sẵn tối đa 200 ký tự." });

            try
            {
                // Đã có thì coi như thành công (bấm + hai lần không sinh lỗi, không nhân đôi)
                var daCo = await _context.KkCameraGhiChuMaus.FirstOrDefaultAsync(x => x.NgayXoa == null && x.NoiDung == noiDung);
                if (daCo != null)
                    return Json(new { thanhCong = true, thongBao = "Ghi chú này đã có trong danh sách.", duLieu = new { daCo.IdMau, daCo.NoiDung } });

                var mau = new KkCameraGhiChuMau { NoiDung = noiDung, NguoiTao = User.Identity?.Name, NgayTao = DateTime.Now };
                _context.KkCameraGhiChuMaus.Add(mau);
                await _context.SaveChangesAsync();
                await GhiLichSuAsync("Thêm mới", mau.IdMau, $"Ghi chú có sẵn: \"{noiDung}\"", "Ghi chú camera có sẵn");

                return Json(new { thanhCong = true, thongBao = "Đã thêm vào ghi chú có sẵn.", duLieu = new { mau.IdMau, mau.NoiDung } });
            }
            catch (DbUpdateException ex) when (ex.InnerException is SqlException sql && (sql.Number == 2601 || sql.Number == 2627))
            {
                return Json(new { thanhCong = true, thongBao = "Ghi chú này vừa được người khác thêm." });
            }
            catch (Exception ex) { return Json(new { thanhCong = false, thongBao = ex.Message }); }
        }

        [HttpPost("/QLCamera/GhiChuMau/Xoa")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> GhiChuMauXoa(int id)
        {
            if (!CoQuyenSua()) return Json(new { thanhCong = false, thongBao = "Bạn không có quyền thao tác." });

            try
            {
                var mau = await _context.KkCameraGhiChuMaus.FirstOrDefaultAsync(x => x.IdMau == id && x.NgayXoa == null);
                if (mau == null) return Json(new { thanhCong = false, thongBao = "Không tìm thấy ghi chú có sẵn." });

                // Xoá mềm: chỉ bỏ khỏi danh sách chọn, ghi chú đã gán cho camera vẫn giữ nguyên
                mau.NgayXoa = DateTime.Now;
                mau.NguoiXoa = User.Identity?.Name;
                await _context.SaveChangesAsync();
                await GhiLichSuAsync("Xóa", id, $"Ghi chú có sẵn: \"{mau.NoiDung}\"", "Ghi chú camera có sẵn");

                return Json(new { thanhCong = true, thongBao = "Đã bỏ khỏi danh sách có sẵn." });
            }
            catch (Exception ex) { return Json(new { thanhCong = false, thongBao = ex.Message }); }
        }

        #endregion

        #region Xem trực tiếp (ảnh chụp từ đầu ghi, video qua go2rtc cùng máy)

        [HttpGet("/QLCamera/Xem/AnhChup")]
        public async Task<IActionResult> XemAnhChup(string? nvrIp, int kenh, [FromServices] CameraXemTrucTiepService xem)
        {
            if (!CoQuyenXem("BPVN")) return StatusCode(403);
            if (kenh < 1 || kenh > 512) return BadRequest();

            try
            {
                var ct = HttpContext.RequestAborted;
                if (!await xem.LaDauGhiHopLeAsync(nvrIp, ct)) return NotFound();

                var anh = await xem.LayAnhChupAsync(nvrIp!.Trim(), kenh, ct);
                if (anh == null) return StatusCode(502);

                // Ảnh trực tiếp: không cho trình duyệt/nginx giữ cache, mỗi lần tải là một ảnh mới
                Response.Headers.CacheControl = "no-store";
                return File(anh, "image/jpeg");
            }
            catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or InvalidOperationException)
            {
                return StatusCode(502);
            }
        }

        /// <summary>
        /// Ảnh lưu sẵn — dùng khi máy chủ không gọi thẳng được đầu ghi. Thời điểm chụp trả qua header
        /// X-Chup-Luc để giao diện ghi rõ đây là ảnh cũ, không phải hình trực tiếp.
        /// </summary>
        [HttpGet("/QLCamera/Xem/AnhLuu")]
        public async Task<IActionResult> XemAnhLuu(string? nvrIp, int kenh, [FromServices] CameraXemTrucTiepService xem)
        {
            if (!CoQuyenXem("BPVN")) return StatusCode(403);
            if (kenh < 1 || kenh > 512) return BadRequest();

            try
            {
                var ct = HttpContext.RequestAborted;
                if (!await xem.LaDauGhiHopLeAsync(nvrIp, ct)) return NotFound();

                var luu = await xem.LayAnhLuuAsync(nvrIp!.Trim(), kenh, ct);
                if (luu == null) return NotFound();

                Response.Headers.CacheControl = "no-store";
                Response.Headers["X-Chup-Luc"] = luu.Value.chupLuc.ToString("yyyy-MM-ddTHH:mm:ss");
                return File(luu.Value.anh, "image/jpeg");
            }
            catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or InvalidOperationException or IOException)
            {
                return StatusCode(502);
            }
        }

        /// <summary>
        /// Chuyển tiếp luồng fMP4 của go2rtc cho thẻ &lt;video&gt;. Luồng chạy tới khi người dùng đóng
        /// cửa sổ xem (RequestAborted) — go2rtc tự ngắt RTSP khi không còn ai xem.
        /// </summary>
        [HttpGet("/QLCamera/Xem/Video")]
        public async Task XemVideo(string? nvrIp, int kenh, [FromServices] CameraXemTrucTiepService xem)
        {
            var ct = HttpContext.RequestAborted;

            if (!CoQuyenXem("BPVN")) { Response.StatusCode = 403; return; }
            if (kenh < 1 || kenh > 512) { Response.StatusCode = 400; return; }

            HttpResponseMessage? luong = null;
            try
            {
                if (!await xem.LaDauGhiHopLeAsync(nvrIp, ct)) { Response.StatusCode = 404; return; }

                luong = await xem.MoLuongVideoAsync(nvrIp!.Trim(), kenh, ct);
                if (luong == null) { Response.StatusCode = 502; return; }

                await ChuyenTiepVideoAsync(luong, ct);
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                // Người dùng đóng cửa sổ xem — kết thúc bình thường
            }
            catch (Exception ex) when (ex is HttpRequestException or InvalidOperationException)
            {
                if (!Response.HasStarted) Response.StatusCode = 502;
            }
            finally
            {
                luong?.Dispose();
            }
        }

        /// <summary>Đẩy nguyên luồng fMP4 của go2rtc xuống trình duyệt tới khi người dùng đóng cửa sổ xem.</summary>
        private async Task ChuyenTiepVideoAsync(HttpResponseMessage luong, CancellationToken ct)
        {
            Response.ContentType = luong.Content.Headers.ContentType?.ToString() ?? "video/mp4";
            Response.Headers.CacheControl = "no-store";
            // nginx mặc định gom đệm response -> video đứng hình; header này bảo nginx đẩy thẳng
            Response.Headers["X-Accel-Buffering"] = "no";
            HttpContext.Features.Get<Microsoft.AspNetCore.Http.Features.IHttpResponseBodyFeature>()?.DisableBuffering();

            await using var nguon = await luong.Content.ReadAsStreamAsync(ct);
            await nguon.CopyToAsync(Response.Body, ct);
        }

        /// <summary>Kênh có trong danh sách camera của công ty không — chặn gọi go2rtc bên đó với kênh tuỳ ý.</summary>
        private static async Task<bool> LaKenhCongTyAsync(CameraXemTrucTiepService xem, string congTy, string? nvrIp, int kenh, CancellationToken ct)
            => !string.IsNullOrWhiteSpace(nvrIp) && kenh >= 1 && kenh <= 512
               && (await xem.DanhSachKenhCongTyAsync(congTy, ct)).kenh.Any(x => x.Nvr == nvrIp.Trim() && x.Kenh == kenh);

        /// <summary>Ảnh trực tiếp camera PFVN/MEGA qua go2rtc đặt ở máy bên công ty đó.</summary>
        [HttpGet("/QLCamera/{congTy:regex(^(PFVN|MEGA)$)}/Xem/AnhChup")]
        public async Task<IActionResult> CongTyKhacAnhChup(string congTy, string? nvrIp, int kenh, [FromServices] CameraXemTrucTiepService xem)
        {
            var cty = CongTyHopLe(congTy);
            if (!CoQuyenXem(cty)) return StatusCode(403);
            var ct = HttpContext.RequestAborted;

            try
            {
                if (!await LaKenhCongTyAsync(xem, cty, nvrIp, kenh, ct)) return NotFound();
                var anh = await xem.LayAnhChupCongTyAsync(cty, nvrIp!.Trim(), kenh, ct);
                if (anh == null) return StatusCode(502);

                Response.Headers.CacheControl = "no-store";
                return File(anh, "image/jpeg");
            }
            catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or InvalidOperationException or IOException)
            {
                return StatusCode(502);
            }
        }

        /// <summary>Video trực tiếp camera PFVN/MEGA (fMP4) qua go2rtc đặt ở máy bên công ty đó.</summary>
        [HttpGet("/QLCamera/{congTy:regex(^(PFVN|MEGA)$)}/Xem/Video")]
        public async Task CongTyKhacVideo(string congTy, string? nvrIp, int kenh, [FromServices] CameraXemTrucTiepService xem)
        {
            var cty = CongTyHopLe(congTy);
            var ct = HttpContext.RequestAborted;
            if (!CoQuyenXem(cty)) { Response.StatusCode = 403; return; }

            HttpResponseMessage? luong = null;
            try
            {
                if (!await LaKenhCongTyAsync(xem, cty, nvrIp, kenh, ct)) { Response.StatusCode = 404; return; }

                luong = await xem.MoLuongVideoCongTyAsync(cty, nvrIp!.Trim(), kenh, ct);
                if (luong == null) { Response.StatusCode = 502; return; }

                await ChuyenTiepVideoAsync(luong, ct);
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                // Người dùng đóng cửa sổ xem — kết thúc bình thường
            }
            catch (Exception ex) when (ex is HttpRequestException or InvalidOperationException or IOException)
            {
                if (!Response.HasStarted) Response.StatusCode = 502;
            }
            finally
            {
                luong?.Dispose();
            }
        }

        #endregion

        #region Đầu ghi chi nhánh (KK_DauGhi, nhập tay — không lưu mật khẩu)

        [HttpGet("/QLCamera/DauGhi/GetDanhSach")]
        public async Task<IActionResult> DauGhiGetDanhSach(string? tuKhoa, string? trangThai, string? congTy)
        {
            var cty = CongTyHopLe(congTy);
            if (!CoQuyenXem(cty)) return Json(new { thanhCong = false, thongBao = "Bạn không có quyền xem dữ liệu này." });

            try
            {
                var query = await LocDauGhiCongTyAsync(_context.KkDauGhis.Where(x => x.NgayXoa == null), cty);

                if (!string.IsNullOrWhiteSpace(tuKhoa))
                {
                    var kw = tuKhoa.Trim();
                    query = query.Where(x =>
                        x.DiaDiem.Contains(kw) ||
                        (x.IpPublic != null && x.IpPublic.Contains(kw)) ||
                        (x.IpLocal != null && x.IpLocal.Contains(kw)) ||
                        (x.GhiChu != null && x.GhiChu.Contains(kw)));
                }
                if (!string.IsNullOrWhiteSpace(trangThai)) query = query.Where(x => x.TrangThai == trangThai);

                var duLieu = await query
                    .OrderBy(x => x.DiaDiem).ThenBy(x => x.IpLocal)
                    .Select(x => new
                    {
                        x.IdDauGhi, x.DiaDiem, x.IpPublic, x.IpLocal, x.PortSv, x.PortWeb,
                        x.TongCamera, x.Raid, x.TrangThai, x.GhiChu, x.NgayCapNhat
                    })
                    .ToListAsync();

                return Json(new { thanhCong = true, duLieu, tongDong = duLieu.Count, tongCamera = duLieu.Sum(x => x.TongCamera ?? 0) });
            }
            catch (Exception ex) { return Json(new { thanhCong = false, thongBao = ex.Message }); }
        }

        [HttpPost("/QLCamera/DauGhi/Save")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DauGhiSave(KkDauGhi model, string? congTy)
        {
            var cty = CongTyHopLe(congTy);
            if (!CoQuyenSua()) return Json(new { thanhCong = false, thongBao = "Bạn không có quyền thao tác." });

            model.DiaDiem = model.DiaDiem?.Trim() ?? "";
            if (model.DiaDiem.Length == 0)
                return Json(new { thanhCong = false, thongBao = "Vui lòng nhập địa điểm." });

            model.IpLocal = string.IsNullOrWhiteSpace(model.IpLocal) ? null : model.IpLocal.Trim();
            if (model.IpLocal != null && !IPAddress.TryParse(model.IpLocal, out _))
                return Json(new { thanhCong = false, thongBao = $"IP local \"{model.IpLocal}\" không hợp lệ." });

            // IP public có thể là tên miền DDNS nên chỉ cắt khoảng trắng, không ép định dạng IP
            model.IpPublic = string.IsNullOrWhiteSpace(model.IpPublic) ? null : model.IpPublic.Trim();

            if (model.PortSv is < 1 or > 65535 || model.PortWeb is < 1 or > 65535)
                return Json(new { thanhCong = false, thongBao = "Cổng phải nằm trong khoảng 1–65535." });
            if (model.TongCamera < 0)
                return Json(new { thanhCong = false, thongBao = "Tổng camera không được âm." });
            if (model.TrangThai != null && !DsTrangThaiDauGhi.Contains(model.TrangThai))
                return Json(new { thanhCong = false, thongBao = "Trạng thái không hợp lệ." });

            try
            {
                string hanhDong = model.IdDauGhi == 0 ? "Thêm mới" : "Cập nhật";

                if (model.IdDauGhi == 0)
                {
                    // Chặn bấm Lưu 2 lần tạo 2 dòng giống hệt nhau
                    var trung = await _context.KkDauGhis.AnyAsync(x => x.NgayXoa == null
                        && x.DiaDiem == model.DiaDiem && x.IpLocal == model.IpLocal && x.IpPublic == model.IpPublic);
                    if (trung)
                        return Json(new { thanhCong = false, thongBao = "Đầu ghi này (cùng địa điểm + IP) đã có trong danh sách." });

                    model.NgayTao = DateTime.Now;
                    model.NguoiTao = User.Identity?.Name;
                    model.NgayXoa = null;
                    model.LyDoXoa = null;
                    // Công ty lấy theo trang đang mở, không nhận giá trị client gửi lên
                    model.IdcongTy = await _context.KkCongTies.Where(x => x.TenCongTy == cty)
                        .Select(x => (int?)x.IdcongTy).FirstOrDefaultAsync();
                    _context.KkDauGhis.Add(model);
                }
                else
                {
                    // Chỉ sửa được đầu ghi thuộc công ty của trang đang mở (id lấy từ client)
                    var db = await (await LocDauGhiCongTyAsync(_context.KkDauGhis, cty))
                        .FirstOrDefaultAsync(x => x.IdDauGhi == model.IdDauGhi && x.NgayXoa == null);
                    if (db == null)
                        return Json(new { thanhCong = false, thongBao = "Không tìm thấy đầu ghi cần cập nhật." });

                    db.DiaDiem = model.DiaDiem;
                    db.IpPublic = model.IpPublic;
                    db.IpLocal = model.IpLocal;
                    db.PortSv = model.PortSv;
                    db.PortWeb = model.PortWeb;
                    db.TongCamera = model.TongCamera;
                    db.Raid = model.Raid;
                    db.TrangThai = model.TrangThai;
                    db.GhiChu = model.GhiChu;
                    db.NgayCapNhat = DateTime.Now;
                }

                await _context.SaveChangesAsync();
                await GhiLichSuAsync(hanhDong, model.IdDauGhi,
                    $"Đầu ghi: {model.DiaDiem} (local: {model.IpLocal ?? "-"}, public: {model.IpPublic ?? "-"})", "Đầu ghi camera");

                return Json(new { thanhCong = true, thongBao = $"{hanhDong} đầu ghi thành công!" });
            }
            catch (Exception ex) { return Json(new { thanhCong = false, thongBao = ex.Message }); }
        }

        [HttpPost("/QLCamera/DauGhi/Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DauGhiDelete(int id, string? lyDo, string? congTy)
        {
            if (!CoQuyenSua()) return Json(new { thanhCong = false, thongBao = "Bạn không có quyền thao tác." });

            try
            {
                // Chỉ xoá được đầu ghi thuộc công ty của trang đang mở (id lấy từ client)
                var item = await (await LocDauGhiCongTyAsync(_context.KkDauGhis, CongTyHopLe(congTy)))
                    .FirstOrDefaultAsync(x => x.IdDauGhi == id && x.NgayXoa == null);
                if (item == null)
                    return Json(new { thanhCong = false, thongBao = "Không tìm thấy đầu ghi cần xoá." });

                item.NgayXoa = DateTime.Now;
                item.LyDoXoa = string.IsNullOrWhiteSpace(lyDo) ? "Không ghi lý do" : lyDo.Trim();
                await _context.SaveChangesAsync();

                await GhiLichSuAsync("Xóa", id, $"Đã xoá đầu ghi: {item.DiaDiem} ({item.IpLocal ?? item.IpPublic}). Lý do: {item.LyDoXoa}", "Đầu ghi camera");
                return Json(new { thanhCong = true, thongBao = "Đã xoá đầu ghi!" });
            }
            catch (Exception ex) { return Json(new { thanhCong = false, thongBao = ex.Message }); }
        }

        #endregion

        /// <summary>
        /// Đầu ghi chi nhánh của một công ty (cột IDCongTy). BPVN gồm cả dòng chưa gán (NULL) — dữ liệu
        /// trước khi tách công ty và dòng thêm tay ngoài web.
        /// </summary>
        private async Task<IQueryable<KkDauGhi>> LocDauGhiCongTyAsync(IQueryable<KkDauGhi> query, string congTy)
        {
            var idCongTy = await _context.KkCongTies.Where(x => x.TenCongTy == congTy)
                .Select(x => (int?)x.IdcongTy).FirstOrDefaultAsync();
            return congTy == "BPVN"
                ? query.Where(x => x.IdcongTy == null || x.IdcongTy == idCongTy)
                : query.Where(x => x.IdcongTy != null && x.IdcongTy == idCongTy);
        }

        #region API danh sách

        [HttpGet("/QLCamera/GetDanhSach")]
        public async Task<IActionResult> GetDanhSach(string? tuKhoa, int? idCongTy, int? idBoPhan, string? tinhTrang, string? trucTuyen)
        {
            if (!CoQuyenXem("BPVN")) return Json(new { thanhCong = false, thongBao = "Bạn không có quyền xem dữ liệu này." });

            try
            {
                var query = _context.KkCameras.Where(x => x.NgayXoa == null);

                if (!string.IsNullOrWhiteSpace(tuKhoa))
                {
                    var kw = tuKhoa.Trim();
                    query = query.Where(x =>
                        x.TenCamera.Contains(kw) ||
                        (x.MaCamera != null && x.MaCamera.Contains(kw)) ||
                        (x.DiaChiIp != null && x.DiaChiIp.Contains(kw)) ||
                        (x.HangSanXuat != null && x.HangSanXuat.Contains(kw)) ||
                        (x.Model != null && x.Model.Contains(kw)) ||
                        (x.Serial != null && x.Serial.Contains(kw)) ||
                        (x.DauGhi != null && x.DauGhi.Contains(kw)) ||
                        (x.ViTri != null && x.ViTri.Contains(kw)));
                }

                if (idCongTy.HasValue && idCongTy > 0) query = query.Where(x => x.IdcongTy == idCongTy);
                // Người chỉ có CamBPVN không được thấy tài sản camera của công ty khác
                if (!CoQuyenSua()) query = query.Where(x => x.IdcongTyNavigation != null && x.IdcongTyNavigation.TenCongTy == "BPVN");
                if (idBoPhan.HasValue && idBoPhan > 0) query = query.Where(x => x.IdboPhan == idBoPhan);
                if (!string.IsNullOrWhiteSpace(tinhTrang)) query = query.Where(x => x.TinhTrang == tinhTrang);

                var data = await query
                    .OrderBy(x => x.DauGhi).ThenBy(x => x.Kenh).ThenBy(x => x.TenCamera)
                    .Select(x => new
                    {
                        x.IdCamera,
                        x.MaCamera,
                        x.TenCamera,
                        x.DiaChiIp,
                        x.HangSanXuat,
                        x.Model,
                        x.Serial,
                        x.DauGhi,
                        x.Kenh,
                        x.IdcongTy,
                        TenCongTy = x.IdcongTyNavigation != null ? x.IdcongTyNavigation.TenCongTy : "",
                        x.IdboPhan,
                        TenBoPhan = x.IdboPhanNavigation != null ? x.IdboPhanNavigation.TenBoPhan : "",
                        x.ViTri,
                        x.TinhTrang,
                        x.NgayLapDat,
                        x.HanBaoHanh,
                        x.GhiChu
                    })
                    .ToListAsync();

                // Ping một lượt (cache 60 giây, khoá riêng với máy in) để biết camera nào đang mất kết nối
                var dsIp = data.Where(x => !string.IsNullOrWhiteSpace(x.DiaChiIp)).Select(x => x.DiaChiIp!).Distinct().ToList();
                var trangThaiPing = dsIp.Count == 0
                    ? new Dictionary<string, bool>()
                    : await _mayInQuetService.PingNhieuAsync(dsIp, HttpContext.RequestAborted, "Camera:TrangThaiPing");

                var ketQua = data.Select(x => new
                {
                    x.IdCamera, x.MaCamera, x.TenCamera, x.DiaChiIp, x.HangSanXuat, x.Model, x.Serial,
                    x.DauGhi, x.Kenh, x.IdcongTy, x.TenCongTy, x.IdboPhan, x.TenBoPhan,
                    x.ViTri, x.TinhTrang, x.NgayLapDat, x.HanBaoHanh, x.GhiChu,
                    // null = chưa khai IP nên không kiểm được
                    Online = string.IsNullOrWhiteSpace(x.DiaChiIp)
                        ? (bool?)null
                        : trangThaiPing.TryGetValue(x.DiaChiIp, out var song) && song
                }).ToList();

                if (trucTuyen == "online") ketQua = ketQua.Where(x => x.Online == true).ToList();
                else if (trucTuyen == "offline") ketQua = ketQua.Where(x => x.Online == false).ToList();

                return Json(new
                {
                    thanhCong = true,
                    duLieu = ketQua,
                    tongDong = ketQua.Count,
                    tongOnline = ketQua.Count(x => x.Online == true),
                    tongOffline = ketQua.Count(x => x.Online == false)
                });
            }
            catch (Exception ex) { return Json(new { thanhCong = false, thongBao = ex.Message }); }
        }

        #endregion

        #region Thêm / Sửa / Xoá

        // Tham số KHÔNG được đặt tên "model": KkCamera có thuộc tính Model, binder sẽ coi ô "Model" của form
        // là tiền tố "model." và bỏ trống mọi trường còn lại.
        [HttpPost("/QLCamera/Save")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Save(KkCamera camera)
        {
            if (!CoQuyenSua()) return Json(new { thanhCong = false, thongBao = "Bạn không có quyền thao tác." });

            camera.TenCamera = camera.TenCamera?.Trim() ?? "";
            if (camera.TenCamera.Length == 0)
                return Json(new { thanhCong = false, thongBao = "Vui lòng nhập tên camera." });

            camera.DiaChiIp = string.IsNullOrWhiteSpace(camera.DiaChiIp) ? null : camera.DiaChiIp.Trim();
            if (camera.DiaChiIp != null && !IPAddress.TryParse(camera.DiaChiIp, out _))
                return Json(new { thanhCong = false, thongBao = $"Địa chỉ IP \"{camera.DiaChiIp}\" không hợp lệ." });

            if (camera.Kenh.HasValue && camera.Kenh < 0)
                return Json(new { thanhCong = false, thongBao = "Kênh đầu ghi không được âm." });

            if (camera.TinhTrang != null && !DsTinhTrang.Contains(camera.TinhTrang))
                return Json(new { thanhCong = false, thongBao = "Tình trạng không hợp lệ." });

            try
            {
                // Báo trùng IP rõ ràng trước; unique index ở DB vẫn là chốt cuối khi hai người lưu cùng lúc
                if (camera.DiaChiIp != null)
                {
                    var trung = await _context.KkCameras
                        .Where(x => x.NgayXoa == null && x.DiaChiIp == camera.DiaChiIp && x.IdCamera != camera.IdCamera)
                        .Select(x => x.TenCamera)
                        .FirstOrDefaultAsync();
                    if (trung != null)
                        return Json(new { thanhCong = false, thongBao = $"IP {camera.DiaChiIp} đã được gán cho camera \"{trung}\"." });
                }

                string hanhDong = camera.IdCamera == 0 ? "Thêm mới" : "Cập nhật";

                if (camera.IdCamera == 0)
                {
                    camera.NgayTao = DateTime.Now;
                    camera.NguoiTao = User.Identity?.Name;
                    camera.NgayXoa = null;
                    camera.LyDoXoa = null;
                    _context.KkCameras.Add(camera);
                }
                else
                {
                    // Chỉ cho sửa bản ghi chưa bị xoá mềm (chặn IDOR qua id gửi lên)
                    var db = await _context.KkCameras
                        .FirstOrDefaultAsync(x => x.IdCamera == camera.IdCamera && x.NgayXoa == null);
                    if (db == null)
                        return Json(new { thanhCong = false, thongBao = "Không tìm thấy camera cần cập nhật." });

                    db.MaCamera = camera.MaCamera;
                    db.TenCamera = camera.TenCamera;
                    db.DiaChiIp = camera.DiaChiIp;
                    db.HangSanXuat = camera.HangSanXuat;
                    db.Model = camera.Model;
                    db.Serial = camera.Serial;
                    db.DauGhi = camera.DauGhi;
                    db.Kenh = camera.Kenh;
                    db.IdcongTy = camera.IdcongTy;
                    db.IdboPhan = camera.IdboPhan;
                    db.ViTri = camera.ViTri;
                    db.TinhTrang = camera.TinhTrang;
                    db.NgayLapDat = camera.NgayLapDat;
                    db.HanBaoHanh = camera.HanBaoHanh;
                    db.GhiChu = camera.GhiChu;
                    db.NgayCapNhat = DateTime.Now;
                }

                await _context.SaveChangesAsync();
                await GhiLichSuAsync(hanhDong, camera.IdCamera, $"Camera: {camera.TenCamera} (IP: {camera.DiaChiIp ?? "chưa khai"})");

                return Json(new { thanhCong = true, thongBao = $"{hanhDong} camera thành công!" });
            }
            catch (DbUpdateException ex) when (ex.InnerException is SqlException sql && (sql.Number == 2601 || sql.Number == 2627))
            {
                return Json(new { thanhCong = false, thongBao = $"IP {camera.DiaChiIp} vừa được gán cho camera khác." });
            }
            catch (Exception ex) { return Json(new { thanhCong = false, thongBao = ex.Message }); }
        }

        [HttpPost("/QLCamera/Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete(int id, string? lyDo)
        {
            if (!CoQuyenSua()) return Json(new { thanhCong = false, thongBao = "Bạn không có quyền thao tác." });

            try
            {
                var item = await _context.KkCameras.FirstOrDefaultAsync(x => x.IdCamera == id && x.NgayXoa == null);
                if (item == null)
                    return Json(new { thanhCong = false, thongBao = "Không tìm thấy camera cần xoá." });

                // Xoá mềm để còn truy vết
                item.NgayXoa = DateTime.Now;
                item.LyDoXoa = string.IsNullOrWhiteSpace(lyDo) ? "Không ghi lý do" : lyDo.Trim();
                await _context.SaveChangesAsync();

                await GhiLichSuAsync("Xóa", id, $"Đã xoá camera: {item.TenCamera} (IP: {item.DiaChiIp ?? "chưa khai"}). Lý do: {item.LyDoXoa}");
                return Json(new { thanhCong = true, thongBao = "Đã xoá camera!" });
            }
            catch (Exception ex) { return Json(new { thanhCong = false, thongBao = ex.Message }); }
        }

        #endregion

        /// <summary>Ghi vào KK_LichSuThaoTac để mọi thao tác thêm/sửa/xoá camera, đầu ghi đều có vết.</summary>
        private async Task GhiLichSuAsync(string hanhDong, int idDoiTuong, string chiTiet, string doiTuong = "Camera")
        {
            try
            {
                _context.KkLichSuThaoTacs.Add(new KkLichSuThaoTac
                {
                    HanhDong = hanhDong,
                    DoiTuong = doiTuong,
                    IdDoiTuong = idDoiTuong,
                    ChiTiet = chiTiet,
                    ThoiGian = DateTime.Now,
                    NguoiThaoTac = User.Identity?.Name ?? "Hệ thống"
                });
                await _context.SaveChangesAsync();
            }
            catch { /* Bỏ qua lỗi ghi log để không làm gián đoạn luồng chính (cùng cách QLCCDC) */ }
        }
    }
}
