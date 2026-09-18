using E_Form_Best.Areas.ITForm.Services;
using E_Form_Best.Context;
using E_Form_Best.Models.ITForm;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace E_Form_Best.Areas.ITForm.Controllers
{
    /// <summary>
    /// Quản lý máy in: danh mục máy, chỉ số trang in theo ngày, mực và trống.
    /// Số hoá file Excel "Print out information2026.xlsx" của bộ phận IT.
    /// Module mới nên tách file riêng, không nhồi vào ITFormController.
    /// </summary>
    [Area("ITform")]
    public class QLMayInController : Controller
    {
        private readonly ITFormContext _context;
        private readonly MayInApiService _mayInApiService;
        private readonly MayInQuetService _mayInQuetService;
        private readonly IConfiguration _configuration;
        private readonly IServiceScopeFactory _scopeFactory;

        public QLMayInController(ITFormContext context, MayInApiService mayInApiService,
            MayInQuetService mayInQuetService, IConfiguration configuration, IServiceScopeFactory scopeFactory)
        {
            _context = context;
            _mayInApiService = mayInApiService;
            _mayInQuetService = mayInQuetService;
            _configuration = configuration;
            _scopeFactory = scopeFactory;
        }

        // Quyền "All" và "AdminIT" được vào (nhóm menu "Máy in & CCDC" dùng chung hai quyền này).
        // Ẩn mục trên menu không phải là phân quyền nên mọi action đều phải gọi lại hàm này.
        private bool CoQuyen() => User.IsInRole("AdminIT") || User.IsInRole("All");

        private IActionResult? KiemQuyen()
        {
            if (User?.Identity?.IsAuthenticated != true)
                return Redirect("/DonXetDuyet/DangNhap");

            if (!CoQuyen())
                return Forbid();

            return null;
        }

        /// <summary>
        /// Mở cột vat_tu_json thành danh sách {ten, phanTram} cho giao diện. Dòng chốt cũ và dòng
        /// nhập tay không có cột này nên trả null, giao diện tự lui về hiện hai thanh Mực/Trống.
        /// JSON hỏng thì bỏ qua chứ không làm hỏng cả trang danh sách.
        /// </summary>
        private static List<VatTuHienThi>? PhanTichVatTu(string? json)
        {
            if (string.IsNullOrWhiteSpace(json)) return null;

            try
            {
                // JSON lưu bằng tên camelCase (ten, phanTram) nên phải bỏ qua hoa thường khi đọc lại
                var ds = System.Text.Json.JsonSerializer.Deserialize<List<VatTuHienThi>>(
                    json, new System.Text.Json.JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                return ds is { Count: > 0 } ? ds : null;
            }
            catch (System.Text.Json.JsonException)
            {
                return null;
            }
        }

        /// <summary>Một vật tư kèm phần trăm còn lại, ví dụ TONER_C 37%.</summary>
        public class VatTuHienThi
        {
            public string Ten { get; set; } = "";
            public int PhanTram { get; set; }
        }

        #region View

        [HttpGet("/QLMayIn")]
        public async Task<IActionResult> Index()
        {
            var chan = KiemQuyen();
            if (chan != null) return chan;

            // Dropdown lọc: chiếu vào kiểu tường minh, KHÔNG dùng anonymous type vì ở Development
            // bật AddRazorRuntimeCompilation, view nằm ở assembly khác nên không thấy type internal.
            ViewBag.DsBoPhan = await _context.MayIns
                .Where(m => m.BoPhan != null)
                .Select(m => m.BoPhan!)
                .Distinct()
                .OrderBy(x => x)
                .ToListAsync();

            ViewBag.DsModel = await _context.MayIns
                .Select(m => m.Model)
                .Distinct()
                .OrderBy(x => x)
                .ToListAsync();

            return View();
        }

        /// <summary>
        /// Trang chi tiết một máy in. Đây là điều hướng thật (URL bookmark được, gửi cho nhau được)
        /// chứ không phải hộp thoại — số liệu trong trang vẫn lấy qua AJAX từ các action JSON bên dưới.
        /// </summary>
        [HttpGet("/QLMayIn/May/{id:int}")]
        public async Task<IActionResult> TrangChiTiet(int id)
        {
            var chan = KiemQuyen();
            if (chan != null) return chan;

            var coMay = await _context.MayIns.AnyAsync(m => m.IdMayIn == id);
            if (!coMay) return Redirect("/QLMayIn");

            ViewBag.IdMayIn = id;
            return View("ChiTietMay");
        }
        #endregion


        #region API danh sách

        [HttpGet("/QLMayIn/GetDanhSach")]
        public async Task<IActionResult> GetDanhSach(string? tuKhoa, string? boPhan, string? model, string? trangThai)
        {
            var chan = KiemQuyen();
            if (chan != null) return chan;

            var truyVan = _context.MayIns.AsNoTracking().AsQueryable();

            if (!string.IsNullOrWhiteSpace(tuKhoa))
            {
                var tk = tuKhoa.Trim();
                truyVan = truyVan.Where(m => m.Serial.Contains(tk)
                                          || m.Model.Contains(tk)
                                          || (m.ViTri != null && m.ViTri.Contains(tk))
                                          || (m.TenHangDoi != null && m.TenHangDoi.Contains(tk))
                                          || (m.DiaChiIp != null && m.DiaChiIp.Contains(tk)));
            }

            if (!string.IsNullOrWhiteSpace(boPhan)) truyVan = truyVan.Where(m => m.BoPhan == boPhan);
            if (!string.IsNullOrWhiteSpace(model)) truyVan = truyVan.Where(m => m.Model == model);
            if (!string.IsNullOrWhiteSpace(trangThai)) truyVan = truyVan.Where(m => m.TrangThai == trangThai);

            var dsMay = await truyVan
                // Máy cắm USB / chưa có IP thì không theo dõi được qua mạng — đẩy xuống cuối danh sách
                .OrderBy(m => m.DiaChiIp == null ? 1 : 0)
                .ThenBy(m => m.BoPhan).ThenBy(m => m.ViTri)
                .Select(m => new
                {
                    m.IdMayIn,
                    m.BoPhan,
                    m.Model,
                    m.Serial,
                    m.DiaChiIp,
                    m.TenHangDoi,
                    m.ViTri,
                    m.TrangThai,
                    m.TheoDoiTuDong,
                    m.LanDocCuoi,
                    m.KetQuaDocCuoi,
                    m.GhiChu
                })
                .ToListAsync();

            // Lấy gọn chỉ số 31 ngày gần nhất của toàn bộ máy trong một truy vấn thay vì
            // mỗi máy một lần gọi DB (112 máy = 112 vòng lặp N+1).
            var tuNgay = DateOnly.FromDateTime(DateTime.Now.AddDays(-31));
            var dsChiSo = await _context.MayInChiSos.AsNoTracking()
                .Where(c => c.NgayChot >= tuNgay && c.CounterTong != null)
                .Select(c => new { c.IdMayIn, c.NgayChot, c.CounterTong, c.TonerPhanTram, c.DrumPhanTram, c.VatTuJson, c.TrangThaiThietBi })
                .ToListAsync();

            var theoMay = dsChiSo.GroupBy(c => c.IdMayIn)
                                 .ToDictionary(g => g.Key, g => g.OrderByDescending(x => x.NgayChot).ToList());

            // Ping toàn bộ IP một lượt (có cache 60 giây) để xếp máy đang bật lên đầu
            var dsIp = dsMay.Where(m => m.DiaChiIp != null).Select(m => m.DiaChiIp!).Distinct().ToList();
            var trangThaiPing = await _mayInQuetService.PingNhieuAsync(dsIp, HttpContext.RequestAborted);

            var homNay = DateOnly.FromDateTime(DateTime.Now);

            var duLieu = dsMay.Select(m =>
            {
                theoMay.TryGetValue(m.IdMayIn, out var lichSu);
                var moiNhat = lichSu?.FirstOrDefault();

                // Trang in trong N ngày = chênh lệch counter giữa bản đọc mới nhất và bản đọc
                // cũ nhất còn nằm trong khoảng đó. Thiếu mốc cũ thì không suy ra được → null.
                int? TrangIn(int soNgay)
                {
                    if (lichSu == null || moiNhat?.CounterTong == null) return null;
                    var moc = lichSu.Where(x => x.NgayChot <= homNay.AddDays(-soNgay))
                                    .OrderByDescending(x => x.NgayChot)
                                    .FirstOrDefault()
                              ?? lichSu.LastOrDefault();
                    if (moc?.CounterTong == null || moc.NgayChot == moiNhat.NgayChot) return null;
                    var chenh = moiNhat.CounterTong.Value - moc.CounterTong.Value;
                    return chenh < 0 ? null : chenh; // counter tụt = thay main board / reset, bỏ qua
                }

                // Số tờ in trong hôm nay = chỉ số chốt hôm nay trừ chỉ số của lần chốt gần nhất trước đó
                int? TrangInHomNay()
                {
                    var cuaHomNay = lichSu?.FirstOrDefault(x => x.NgayChot == homNay);
                    if (cuaHomNay?.CounterTong == null) return null;

                    var truocDo = lichSu!.FirstOrDefault(x => x.NgayChot < homNay && x.CounterTong != null);
                    if (truocDo?.CounterTong == null) return null;

                    var chenh = cuaHomNay.CounterTong.Value - truocDo.CounterTong.Value;
                    return chenh < 0 ? null : chenh;
                }

                return new
                {
                    m.IdMayIn,
                    m.BoPhan,
                    m.Model,
                    m.Serial,
                    m.DiaChiIp,
                    m.TenHangDoi,
                    m.ViTri,
                    m.TrangThai,
                    m.TheoDoiTuDong,
                    m.GhiChu,
                    LanDocCuoi = m.LanDocCuoi?.ToString("dd/MM/yyyy HH:mm"),
                    m.KetQuaDocCuoi,
                    CounterTong = moiNhat?.CounterTong,
                    NgayChiSo = moiNhat?.NgayChot.ToString("dd/MM/yyyy"),
                    Toner = moiNhat?.TonerPhanTram,
                    Drum = moiNhat?.DrumPhanTram,
                    // Máy in màu: danh sách đầy đủ từng mực/trống để hover lên thanh là thấy màu nào sắp hết
                    VatTu = PhanTichVatTu(moiNhat?.VatTuJson),
                    TrangThaiThietBi = moiNhat?.TrangThaiThietBi,
                    TrangInHomNay = TrangInHomNay(),
                    TrangIn7Ngay = TrangIn(7),
                    TrangIn30Ngay = TrangIn(30),
                    Online = m.DiaChiIp != null && trangThaiPing.TryGetValue(m.DiaChiIp, out var song) && song
                };
            })
            // Thứ tự cuối cùng: máy đọc được mực/trống → máy đang bật → máy có IP nhưng không
            // phản hồi → máy cắm USB/không IP. Máy có số mực là nhóm cần theo dõi sát nhất vì
            // biết trước lúc phải thay vật tư.
            .OrderByDescending(x => x.Toner != null || x.Drum != null)
            .ThenByDescending(x => x.Online)
            .ThenBy(x => x.DiaChiIp == null)
            .ThenBy(x => x.BoPhan)
            .ThenBy(x => x.ViTri)
            .ToList();

            return Json(new
            {
                thanhCong = true,
                duLieu,
                tomTat = new
                {
                    tongMay = duLieu.Count,
                    dangChay = duLieu.Count(x => x.TrangThai == "HoatDong"),
                    theoDoiTuDong = duLieu.Count(x => x.TheoDoiTuDong),
                    dangBat = duLieu.Count(x => x.Online),
                    tonerThap = duLieu.Count(x => x.Toner != null && x.Toner <= 20),
                    trangIn30Ngay = duLieu.Sum(x => x.TrangIn30Ngay ?? 0),
                    trangInHomNay = duLieu.Sum(x => x.TrangInHomNay ?? 0)
                }
            });
        }

        #endregion

        #region API chi tiết

        [HttpGet("/QLMayIn/ChiTiet/{id:int}")]
        public async Task<IActionResult> ChiTiet(int id, int soNgay = 30)
        {
            var chan = KiemQuyen();
            if (chan != null) return chan;

            var may = await _context.MayIns.AsNoTracking()
                .Where(m => m.IdMayIn == id)
                .Select(m => new
                {
                    m.IdMayIn, m.BoPhan, m.Model, m.Serial, m.DiaChiIp, m.TenHangDoi, m.ViTri,
                    m.TrangThai, m.TheoDoiTuDong, m.GhiChu, m.KetQuaDocCuoi,
                    m.NgayTao, m.NgayCapNhat,
                    LanDocCuoi = m.LanDocCuoi
                })
                .FirstOrDefaultAsync();

            if (may is null) return Json(new { thanhCong = false, thongBao = "Không tìm thấy máy in" });

            if (soNgay < 7) soNgay = 7;
            if (soNgay > 365) soNgay = 365;

            var tuNgay = DateOnly.FromDateTime(DateTime.Now.AddDays(-soNgay));
            var lichSu = await _context.MayInChiSos.AsNoTracking()
                .Where(c => c.IdMayIn == id && c.NgayChot >= tuNgay)
                .OrderBy(c => c.NgayChot)
                .Select(c => new
                {
                    c.NgayChot, c.CounterIn, c.CounterCopy, c.CounterScan, c.CounterTong,
                    c.TonerPhanTram, c.DrumPhanTram, c.VatTuJson, c.Nguon, c.ThoiDiemDoc
                })
                .ToListAsync();

            // Biểu đồ vẽ ĐỒNG HỒ TỔNG của máy theo thời gian (số trang đã in từ lúc xuất xưởng),
            // kèm theo chênh lệch với lần đọc trước để xem nhịp in. Ngày không đọc được thì không
            // có điểm, không tự bịa số 0 và cũng không nối ngang cho đẹp.
            var theoNgay = new List<object>();
            var tongTrangKhoang = 0;
            var tongNgayKhoang = 0;
            var cacMoc = lichSu.Where(c => c.CounterTong != null).ToList();

            for (var i = 0; i < cacMoc.Count; i++)
            {
                var hienTai = cacMoc[i];
                var truoc = i > 0 ? cacMoc[i - 1] : null;

                int? chenh = null;
                int? soNgayCach = null;

                if (truoc is not null)
                {
                    var d = hienTai.CounterTong!.Value - truoc.CounterTong!.Value;
                    if (d >= 0)
                    {
                        chenh = d;
                        soNgayCach = hienTai.NgayChot.DayNumber - truoc.NgayChot.DayNumber;
                        tongTrangKhoang += d;
                        tongNgayKhoang += soNgayCach.Value;
                    }
                }

                theoNgay.Add(new
                {
                    ngay = hienTai.NgayChot.ToString("dd/MM"),
                    counterTong = hienTai.CounterTong,
                    soTrang = chenh,
                    soNgayCach
                });
            }

            // Chốt theo kỳ: mỗi tháng lấy chỉ số đọc được tại ngày chốt (mặc định 20) để đối chiếu
            // với bảng theo dõi trang in của bộ phận IT. Ngày đó máy tắt / không đọc được thì lấy
            // lần đọc gần ngày chốt nhất trong cùng tháng và ghi rõ ngày thật, không nội suy.
            var ngayChotThang = _configuration.GetValue<int?>("MayIn:NgayChotThang") ?? 20;
            if (ngayChotThang < 1 || ngayChotThang > 28) ngayChotThang = 20;

            var moiKy = lichSu
                .Where(c => c.CounterTong != null)
                .GroupBy(c => new { c.NgayChot.Year, c.NgayChot.Month })
                .Select(g => g.OrderBy(c => Math.Abs(c.NgayChot.Day - ngayChotThang))
                              .ThenByDescending(c => c.NgayChot)
                              .First())
                .OrderBy(c => c.NgayChot)
                .ToList();

            var chotThang = new List<object>();
            for (var i = 0; i < moiKy.Count; i++)
            {
                var ky = moiKy[i];
                var truocKy = i > 0 ? moiKy[i - 1] : null;
                var trongKy = truocKy?.CounterTong is null
                    ? (int?)null
                    : ky.CounterTong!.Value - truocKy.CounterTong.Value;

                chotThang.Add(new
                {
                    ky = ky.NgayChot.ToString("MM/yyyy"),
                    ngayDoc = ky.NgayChot.ToString("dd/MM/yyyy"),
                    dungNgayChot = ky.NgayChot.Day == ngayChotThang,
                    soNgayLech = ky.NgayChot.Day - ngayChotThang,
                    counterTong = ky.CounterTong,
                    counterIn = ky.CounterIn,
                    counterCopy = ky.CounterCopy,
                    trangInTrongKy = trongKy < 0 ? null : trongKy,
                    nguon = ky.Nguon
                });
            }

            chotThang.Reverse();

            // Thống kê gọn cho phần đầu trang chi tiết: lấy từ chính lịch sử vừa đọc, không gọi DB thêm
            var moiNhat = lichSu.LastOrDefault(c => c.CounterTong != null);
            var homNay = DateOnly.FromDateTime(DateTime.Now);

            int? ChenhTuMoc(int soNgayLui)
            {
                if (moiNhat?.CounterTong is null) return null;

                var moc = lichSu.Where(c => c.CounterTong != null && c.NgayChot <= homNay.AddDays(-soNgayLui))
                                .OrderByDescending(c => c.NgayChot)
                                .FirstOrDefault()
                          ?? lichSu.FirstOrDefault(c => c.CounterTong != null);

                if (moc?.CounterTong is null || moc.NgayChot == moiNhat.NgayChot) return null;

                var chenh = moiNhat.CounterTong.Value - moc.CounterTong.Value;
                return chenh < 0 ? null : chenh;
            }

            int? TrangInHomNay()
            {
                var cuaHomNay = lichSu.LastOrDefault(c => c.NgayChot == homNay && c.CounterTong != null);
                if (cuaHomNay?.CounterTong is null) return null;

                var truocDo = lichSu.Where(c => c.CounterTong != null && c.NgayChot < homNay)
                                    .OrderByDescending(c => c.NgayChot)
                                    .FirstOrDefault();
                if (truocDo?.CounterTong is null) return null;

                var chenh = cuaHomNay.CounterTong.Value - truocDo.CounterTong.Value;
                return chenh < 0 ? null : chenh;
            }

            return Json(new
            {
                thanhCong = true,
                may = new
                {
                    may.IdMayIn, may.BoPhan, may.Model, may.Serial, may.DiaChiIp, may.TenHangDoi, may.ViTri,
                    may.TrangThai, may.TheoDoiTuDong, may.GhiChu, may.KetQuaDocCuoi,
                    NgayTao = may.NgayTao.ToString("dd/MM/yyyy"),
                    NgayCapNhat = may.NgayCapNhat?.ToString("dd/MM/yyyy HH:mm"),
                    LanDocCuoi = may.LanDocCuoi?.ToString("dd/MM/yyyy HH:mm")
                },
                thongKe = new
                {
                    counterTong = moiNhat?.CounterTong,
                    counterIn = moiNhat?.CounterIn,
                    counterCopy = moiNhat?.CounterCopy,
                    counterScan = moiNhat?.CounterScan,
                    ngayChiSo = moiNhat?.NgayChot.ToString("dd/MM/yyyy"),
                    nguonMoiNhat = moiNhat?.Nguon,
                    toner = moiNhat?.TonerPhanTram,
                    drum = moiNhat?.DrumPhanTram,
                    vatTu = PhanTichVatTu(moiNhat?.VatTuJson),
                    trangInHomNay = TrangInHomNay(),
                    trangIn7Ngay = ChenhTuMoc(7),
                    trangIn30Ngay = ChenhTuMoc(30),
                    trungBinhMoiNgay = tongNgayKhoang > 0 ? Math.Round((double)tongTrangKhoang / tongNgayKhoang, 1) : (double?)null,
                    soLanDoc = lichSu.Count,
                    soNgayTheoDoi = soNgay
                },

                lichSu = lichSu.Select(c => new
                {
                    ngay = c.NgayChot.ToString("dd/MM/yyyy"),
                    c.CounterIn, c.CounterCopy, c.CounterScan, c.CounterTong,
                    c.TonerPhanTram, c.DrumPhanTram, c.Nguon,
                    vatTu = PhanTichVatTu(c.VatTuJson),
                    thoiDiem = c.ThoiDiemDoc.ToString("dd/MM/yyyy HH:mm")
                }),
                theoNgay,
                chotThang,
                ngayChotThang
            });
        }

        #endregion

        #region Quét thiết bị theo IP

        /// <summary>
        /// Ping + dò cổng + đọc toàn bộ nhóm API web của máy in để xem "lý lịch" thiết bị tại chỗ.
        /// Chỉ đọc, không ghi gì vào CSDL lẫn vào máy in.
        /// </summary>
        [HttpGet("/QLMayIn/QuetThietBi/{id:int}")]
        public async Task<IActionResult> QuetThietBi(int id)
        {
            var chan = KiemQuyen();
            if (chan != null) return chan;

            var may = await _context.MayIns.AsNoTracking()
                .Where(m => m.IdMayIn == id)
                .Select(m => new { m.IdMayIn, m.Model, m.Serial, m.DiaChiIp, m.ViTri, m.BoPhan })
                .FirstOrDefaultAsync();

            if (may is null) return Json(new { thanhCong = false, thongBao = "Không tìm thấy máy in" });

            if (string.IsNullOrWhiteSpace(may.DiaChiIp))
                return Json(new { thanhCong = false, thongBao = "Máy này không có IP (cắm USB) nên không quét được qua mạng" });

            var ketQua = await _mayInQuetService.QuetAsync(may.DiaChiIp, may.Model, HttpContext.RequestAborted);

            return Json(new
            {
                thanhCong = true,
                thongBao = "Quét xong",
                may,
                duLieu = ketQua,
                thoiDiemQuet = DateTime.Now.ToString("dd/MM/yyyy HH:mm:ss")
            });
        }

        #endregion

        #region Đọc chỉ số từ máy in

        [HttpPost("/QLMayIn/DocChiSo/{id:int}")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DocChiSo(int id)
        {
            var chan = KiemQuyen();
            if (chan != null) return chan;

            var may = await _context.MayIns.FirstOrDefaultAsync(m => m.IdMayIn == id);
            if (may is null) return Json(new { thanhCong = false, thongBao = "Không tìm thấy máy in" });

            if (string.IsNullOrWhiteSpace(may.DiaChiIp))
                return Json(new { thanhCong = false, thongBao = "Máy này không có IP (cắm USB) — phải nhập chỉ số tay" });

            var ketQua = await _mayInApiService.DocVaLuuAsync(may, HttpContext.RequestAborted);

            return Json(new
            {
                thanhCong = ketQua.ThanhCong,
                thongBao = ketQua.ThongBao,
                duLieu = new
                {
                    ketQua.CounterIn,
                    ketQua.CounterCopy,
                    ketQua.CounterScan,
                    ketQua.CounterTong,
                    ketQua.TonerPhanTram,
                    ketQua.DrumPhanTram,
                    ketQua.TrangThaiThietBi
                }
            });
        }

        /// <summary>
        /// Nạp chỉ số quá khứ từ nhật ký lỗi của máy — cách duy nhất lấy lại được số liệu trước
        /// ngày hệ thống bắt đầu theo dõi. Chỉ thêm ngày còn trống, không đè số đã có.
        /// </summary>
        [HttpPost("/QLMayIn/NapLichSu/{id:int}")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> NapLichSu(int id)
        {
            var chan = KiemQuyen();
            if (chan != null) return chan;

            var may = await _context.MayIns.FirstOrDefaultAsync(m => m.IdMayIn == id);
            if (may is null) return Json(new { thanhCong = false, thongBao = "Không tìm thấy máy in" });

            var ketQua = await _mayInApiService.NapLichSuTuMayAsync(may, HttpContext.RequestAborted);

            return Json(new
            {
                thanhCong = ketQua.ThanhCong,
                thongBao = ketQua.ThongBao,
                duLieu = new { ketQua.SoMocDocDuoc, ketQua.SoNgayThemMoi }
            });
        }

        /// <summary>Nạp nhật ký lỗi của toàn bộ máy đang bật đọc tự động, chạy tuần tự.</summary>
        [HttpPost("/QLMayIn/NapLichSuTatCa")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> NapLichSuTatCa()
        {
            var chan = KiemQuyen();
            if (chan != null) return chan;

            var dsMay = await _context.MayIns
                .Where(m => m.TheoDoiTuDong && m.DiaChiIp != null && m.TrangThai == "HoatDong")
                .ToListAsync();

            var soMayCoLichSu = 0;
            var tongNgayThem = 0;
            var khoa = new object();

            await ChayHangLoatAsync(dsMay, async (dichVu, may, ct) =>
            {
                var ketQua = await dichVu.NapLichSuTuMayAsync(may, ct);
                if (!ketQua.ThanhCong || ketQua.SoNgayThemMoi == 0) return;

                lock (khoa)
                {
                    soMayCoLichSu++;
                    tongNgayThem += ketQua.SoNgayThemMoi;
                }
            });

            return Json(new
            {
                thanhCong = true,
                thongBao = $"Đã nạp thêm {tongNgayThem} ngày chỉ số từ nhật ký lỗi của {soMayCoLichSu}/{dsMay.Count} máy"
            });
        }

        [HttpPost("/QLMayIn/DocTatCa")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DocTatCa()
        {
            var chan = KiemQuyen();
            if (chan != null) return chan;

            var dsMay = await _context.MayIns
                .Where(m => m.TheoDoiTuDong && m.DiaChiIp != null && m.TrangThai == "HoatDong")
                .ToListAsync();

            var thanhCong = 0;
            var loi = new List<string>();

            await ChayHangLoatAsync(dsMay, async (dichVu, may, ct) =>
            {
                var ketQua = await dichVu.DocVaLuuAsync(may, ct);
                lock (loi)
                {
                    if (ketQua.ThanhCong) thanhCong++;
                    else if (loi.Count < 10) loi.Add($"{may.Serial} ({may.DiaChiIp}): {ketQua.ThongBao}");
                }
            });

            return Json(new
            {
                thanhCong = true,
                thongBao = $"Đọc được {thanhCong}/{dsMay.Count} máy",
                duLieu = new { tong = dsMay.Count, doc = thanhCong, loi }
            });
        }

        /// <summary>
        /// Chạy một việc trên nhiều máy in cùng lúc. Mỗi máy được một scope DI riêng vì
        /// <see cref="ITFormContext"/> không dùng song song được trên cùng một thực thể — dùng chung
        /// sẽ ném "A second operation was started on this context" ngay lần thứ hai.
        ///
        /// Số luồng lấy theo MayIn:SoMayDocSongSong (mặc định 8) như job nền, để một cú bấm không
        /// bắn vài chục request cùng lúc vào mạng nhà máy.
        /// </summary>
        private async Task ChayHangLoatAsync(IReadOnlyCollection<MayIn> dsMay,
            Func<MayInApiService, MayIn, CancellationToken, Task> viec)
        {
            var soLuong = _configuration.GetValue<int?>("MayIn:SoMayDocSongSong") ?? 8;
            if (soLuong < 1) soLuong = 1;

            var tuyChon = new ParallelOptions
            {
                MaxDegreeOfParallelism = soLuong,
                CancellationToken = HttpContext.RequestAborted
            };

            await Parallel.ForEachAsync(dsMay, tuyChon, async (may, ct) =>
            {
                using var pham = _scopeFactory.CreateScope();
                var dichVu = pham.ServiceProvider.GetRequiredService<MayInApiService>();

                // Thực thể lấy từ context của controller không thuộc context mới nên nạp lại theo id
                var context = pham.ServiceProvider.GetRequiredService<ITFormContext>();
                var mayTrongScope = await context.MayIns.FirstOrDefaultAsync(m => m.IdMayIn == may.IdMayIn, ct);
                if (mayTrongScope is null) return;

                await viec(dichVu, mayTrongScope, ct);
            });
        }

        #endregion

        #region Thêm / sửa máy in

        public class MayInModel
        {
            public int IdMayIn { get; set; }
            public string? BoPhan { get; set; }
            public string Model { get; set; } = "";
            public string Serial { get; set; } = "";
            public string? DiaChiIp { get; set; }
            public string? TenHangDoi { get; set; }
            public string? ViTri { get; set; }
            public string TrangThai { get; set; } = "HoatDong";
            public bool TheoDoiTuDong { get; set; }
            public string? GhiChu { get; set; }
        }

        [HttpPost("/QLMayIn/Luu")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Luu([FromBody] MayInModel model)
        {
            var chan = KiemQuyen();
            if (chan != null) return chan;

            if (string.IsNullOrWhiteSpace(model.Model) || string.IsNullOrWhiteSpace(model.Serial))
                return Json(new { thanhCong = false, thongBao = "Phải nhập Model và Serial" });

            var trangThaiHopLe = new[] { "HoatDong", "TamDung", "BaoPhe" };
            if (!trangThaiHopLe.Contains(model.TrangThai)) model.TrangThai = "HoatDong";

            var ip = string.IsNullOrWhiteSpace(model.DiaChiIp) ? null : model.DiaChiIp.Trim();
            if (ip != null && !System.Net.IPAddress.TryParse(ip, out _))
                return Json(new { thanhCong = false, thongBao = "Địa chỉ IP không hợp lệ (máy cắm USB thì để trống)" });

            var serial = model.Serial.Trim();
            var tenModel = model.Model.Trim();

            // Chặn trùng ngay ở tầng ứng dụng để báo lỗi dễ hiểu; DB vẫn có UNIQUE(model, serial) làm chốt cuối.
            var daTonTai = await _context.MayIns.AnyAsync(m => m.Model == tenModel
                                                            && m.Serial == serial
                                                            && m.IdMayIn != model.IdMayIn);
            if (daTonTai)
                return Json(new { thanhCong = false, thongBao = $"Đã có máy {tenModel} serial {serial} trong danh sách" });

            MayIn may;
            if (model.IdMayIn > 0)
            {
                may = await _context.MayIns.FirstOrDefaultAsync(m => m.IdMayIn == model.IdMayIn)
                      ?? throw new InvalidOperationException("Không tìm thấy máy in cần sửa");
            }
            else
            {
                may = new MayIn { NgayTao = DateTime.Now };
                _context.MayIns.Add(may);
            }

            may.BoPhan = model.BoPhan?.Trim();
            may.Model = tenModel;
            may.Serial = serial;
            may.DiaChiIp = ip;
            may.TenHangDoi = string.IsNullOrWhiteSpace(model.TenHangDoi) ? null : model.TenHangDoi.Trim();
            may.ViTri = model.ViTri?.Trim();
            may.TrangThai = model.TrangThai;
            // Không có IP thì không thể đọc tự động, dù người dùng có tích ô
            may.TheoDoiTuDong = model.TheoDoiTuDong && ip != null;
            may.GhiChu = model.GhiChu?.Trim();
            may.NgayCapNhat = DateTime.Now;

            await _context.SaveChangesAsync();

            return Json(new { thanhCong = true, thongBao = model.IdMayIn > 0 ? "Đã cập nhật máy in" : "Đã thêm máy in", duLieu = new { may.IdMayIn } });
        }

        public class ChiSoTayModel
        {
            public int IdMayIn { get; set; }
            public DateOnly? NgayChot { get; set; }
            public int? CounterTong { get; set; }
            public int? TonerPhanTram { get; set; }
            public string? GhiChu { get; set; }
        }

        /// <summary>
        /// Nhập chỉ số tay cho máy đời cũ không có API (FX3105, FX3205, FX3360s...).
        /// Ghi đè đúng dòng của ngày đó nên nhập lại nhiều lần không tạo dữ liệu trùng.
        /// </summary>
        [HttpPost("/QLMayIn/NhapChiSoTay")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> NhapChiSoTay([FromBody] ChiSoTayModel model)
        {
            var chan = KiemQuyen();
            if (chan != null) return chan;

            var may = await _context.MayIns.FirstOrDefaultAsync(m => m.IdMayIn == model.IdMayIn);
            if (may is null) return Json(new { thanhCong = false, thongBao = "Không tìm thấy máy in" });

            if (model.CounterTong is null or < 0)
                return Json(new { thanhCong = false, thongBao = "Chỉ số phải là số không âm" });

            var ngay = model.NgayChot ?? DateOnly.FromDateTime(DateTime.Now);
            if (ngay > DateOnly.FromDateTime(DateTime.Now))
                return Json(new { thanhCong = false, thongBao = "Không nhập chỉ số cho ngày trong tương lai" });

            var chiSo = await _context.MayInChiSos
                .FirstOrDefaultAsync(c => c.IdMayIn == model.IdMayIn && c.NgayChot == ngay);

            if (chiSo is null)
            {
                chiSo = new MayInChiSo { IdMayIn = model.IdMayIn, NgayChot = ngay };
                _context.MayInChiSos.Add(chiSo);
            }

            chiSo.ThoiDiemDoc = DateTime.Now;
            chiSo.CounterTong = model.CounterTong;
            chiSo.TonerPhanTram = model.TonerPhanTram;
            chiSo.Nguon = "NhapTay";
            chiSo.GhiChu = string.IsNullOrWhiteSpace(model.GhiChu)
                ? $"Nhập tay bởi {User.Identity?.Name}"
                : model.GhiChu.Trim();

            may.NgayCapNhat = DateTime.Now;
            await _context.SaveChangesAsync();

            return Json(new { thanhCong = true, thongBao = $"Đã lưu chỉ số ngày {ngay:dd/MM/yyyy}" });
        }

        #endregion
    }
}
