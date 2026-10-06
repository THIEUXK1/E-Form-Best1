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

        // Cùng quyền với nhóm menu "Máy in & CCDC". Ẩn nút ở UI không phải phân quyền nên mọi action đều phải gọi.
        private bool CoQuyen() => User.IsInRole("AdminIT") || User.IsInRole("All");

        #region View

        [HttpGet("/QLCamera")]
        public async Task<IActionResult> Index()
        {
            if (User?.Identity?.IsAuthenticated != true)
                return Redirect("/DonXetDuyet/DangNhap");

            if (!CoQuyen())
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

            return View();
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

        [HttpGet("/QLCamera/GiamSat/LichSu")]
        public Task<IActionResult> GiamSatLichSu(int soLan = 72)
            => DocGiamSatAsync(ct => _giamSat.LichSuKiemTraAsync(soLan, ct));

        /// <summary>
        /// Bọc chung: kiểm quyền + đổi lỗi mạng/HTTP của hệ thống bên kia thành thông báo dễ hiểu,
        /// để trang vẫn hiện tab tài sản / đầu ghi chi nhánh khi hệ thống giám sát đang sập.
        /// </summary>
        private async Task<IActionResult> DocGiamSatAsync(Func<CancellationToken, Task<System.Text.Json.JsonElement>> doc)
        {
            if (!CoQuyen()) return Json(new { thanhCong = false, thongBao = "Bạn không có quyền xem dữ liệu này." });

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
        public async Task<IActionResult> GhiChuDanhSach()
        {
            if (!CoQuyen()) return Json(new { thanhCong = false, thongBao = "Bạn không có quyền xem dữ liệu này." });

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
        public async Task<IActionResult> GhiChuLuu(string? nvrIp, int kenh, string? tenCamera, string? ghiChu,
            [FromServices] CameraXemTrucTiepService xem)
        {
            if (!CoQuyen()) return Json(new { thanhCong = false, thongBao = "Bạn không có quyền thao tác." });
            if (kenh < 1 || kenh > 512) return Json(new { thanhCong = false, thongBao = "Kênh không hợp lệ." });

            ghiChu = string.IsNullOrWhiteSpace(ghiChu) ? null : ghiChu.Trim();
            if (ghiChu?.Length > 1000) return Json(new { thanhCong = false, thongBao = "Ghi chú tối đa 1000 ký tự." });

            try
            {
                // Chỉ nhận đầu ghi có thật trong hệ thống giám sát, không cho ghi rác theo IP tuỳ ý
                if (!await xem.LaDauGhiHopLeAsync(nvrIp, HttpContext.RequestAborted))
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
        public async Task<IActionResult> LichSuDanhSach(DateTime? tuNgay, DateTime? denNgay, string? loai, string? nvrIp, string? tuKhoa)
        {
            if (!CoQuyen()) return Json(new { thanhCong = false, thongBao = "Bạn không có quyền xem dữ liệu này." });

            const int toiDa = 2000;
            try
            {
                var tu = (tuNgay ?? DateTime.Today.AddDays(-6)).Date;
                var den = (denNgay ?? DateTime.Today).Date.AddDays(1);   // gồm trọn ngày cuối

                var query = _context.KkCameraLichSus.Where(x => x.ThoiGian >= tu && x.ThoiGian < den);
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
                var dauTien = await _context.KkCameraLichSus.MinAsync(x => (DateTime?)x.ThoiGian);

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
        public async Task<IActionResult> GhiChuMauDanhSach()
        {
            if (!CoQuyen()) return Json(new { thanhCong = false, thongBao = "Bạn không có quyền xem dữ liệu này." });

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
        public async Task<IActionResult> GhiChuMauThem(string? noiDung)
        {
            if (!CoQuyen()) return Json(new { thanhCong = false, thongBao = "Bạn không có quyền thao tác." });

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
        public async Task<IActionResult> GhiChuMauXoa(int id)
        {
            if (!CoQuyen()) return Json(new { thanhCong = false, thongBao = "Bạn không có quyền thao tác." });

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
            if (!CoQuyen()) return StatusCode(403);
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
        /// Chuyển tiếp luồng fMP4 của go2rtc cho thẻ &lt;video&gt;. Luồng chạy tới khi người dùng đóng
        /// cửa sổ xem (RequestAborted) — go2rtc tự ngắt RTSP khi không còn ai xem.
        /// </summary>
        [HttpGet("/QLCamera/Xem/Video")]
        public async Task XemVideo(string? nvrIp, int kenh, [FromServices] CameraXemTrucTiepService xem)
        {
            var ct = HttpContext.RequestAborted;

            if (!CoQuyen()) { Response.StatusCode = 403; return; }
            if (kenh < 1 || kenh > 512) { Response.StatusCode = 400; return; }

            HttpResponseMessage? luong = null;
            try
            {
                if (!await xem.LaDauGhiHopLeAsync(nvrIp, ct)) { Response.StatusCode = 404; return; }

                luong = await xem.MoLuongVideoAsync(nvrIp!.Trim(), kenh, ct);
                if (luong == null) { Response.StatusCode = 502; return; }

                Response.ContentType = luong.Content.Headers.ContentType?.ToString() ?? "video/mp4";
                Response.Headers.CacheControl = "no-store";
                // nginx mặc định gom đệm response -> video đứng hình; header này bảo nginx đẩy thẳng
                Response.Headers["X-Accel-Buffering"] = "no";
                HttpContext.Features.Get<Microsoft.AspNetCore.Http.Features.IHttpResponseBodyFeature>()?.DisableBuffering();

                await using var nguon = await luong.Content.ReadAsStreamAsync(ct);
                await nguon.CopyToAsync(Response.Body, ct);
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

        #endregion

        #region Đầu ghi chi nhánh (KK_DauGhi, nhập tay — không lưu mật khẩu)

        [HttpGet("/QLCamera/DauGhi/GetDanhSach")]
        public async Task<IActionResult> DauGhiGetDanhSach(string? tuKhoa, string? trangThai)
        {
            if (!CoQuyen()) return Json(new { thanhCong = false, thongBao = "Bạn không có quyền xem dữ liệu này." });

            try
            {
                var query = _context.KkDauGhis.Where(x => x.NgayXoa == null);

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
        public async Task<IActionResult> DauGhiSave(KkDauGhi model)
        {
            if (!CoQuyen()) return Json(new { thanhCong = false, thongBao = "Bạn không có quyền thao tác." });

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
                    _context.KkDauGhis.Add(model);
                }
                else
                {
                    var db = await _context.KkDauGhis.FirstOrDefaultAsync(x => x.IdDauGhi == model.IdDauGhi && x.NgayXoa == null);
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
        public async Task<IActionResult> DauGhiDelete(int id, string? lyDo)
        {
            if (!CoQuyen()) return Json(new { thanhCong = false, thongBao = "Bạn không có quyền thao tác." });

            try
            {
                var item = await _context.KkDauGhis.FirstOrDefaultAsync(x => x.IdDauGhi == id && x.NgayXoa == null);
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

        #region API danh sách

        [HttpGet("/QLCamera/GetDanhSach")]
        public async Task<IActionResult> GetDanhSach(string? tuKhoa, int? idCongTy, int? idBoPhan, string? tinhTrang, string? trucTuyen)
        {
            if (!CoQuyen()) return Json(new { thanhCong = false, thongBao = "Bạn không có quyền xem dữ liệu này." });

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
        public async Task<IActionResult> Save(KkCamera camera)
        {
            if (!CoQuyen()) return Json(new { thanhCong = false, thongBao = "Bạn không có quyền thao tác." });

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
        public async Task<IActionResult> Delete(int id, string? lyDo)
        {
            if (!CoQuyen()) return Json(new { thanhCong = false, thongBao = "Bạn không có quyền thao tác." });

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
