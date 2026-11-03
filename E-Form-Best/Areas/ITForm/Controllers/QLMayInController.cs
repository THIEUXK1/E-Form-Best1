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
        private readonly MayInLichSuInService _mayInLichSuInService;

        public QLMayInController(ITFormContext context, MayInApiService mayInApiService,
            MayInQuetService mayInQuetService, IConfiguration configuration, IServiceScopeFactory scopeFactory,
            MayInLichSuInService mayInLichSuInService)
        {
            _context = context;
            _mayInApiService = mayInApiService;
            _mayInQuetService = mayInQuetService;
            _mayInLichSuInService = mayInLichSuInService;
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
        /// Một lần chốt chỉ số dùng cho bảng danh sách và file xuất. Đặt thành lớp có tên (không
        /// dùng kiểu ẩn danh) để hàm tính chênh lệch nhận được bộ chọn cột In / Copy / Scan / Tổng.
        /// </summary>
        private sealed class ChiSoMoc
        {
            public int IdMayIn { get; init; }
            public DateOnly NgayChot { get; init; }
            public int? CounterTong { get; init; }
            public int? CounterIn { get; init; }
            public int? CounterCopy { get; init; }
            public int? CounterScan { get; init; }
            public int? CounterInMau { get; init; }
            public int? CounterInDenTrang { get; init; }
            public int? TonerPhanTram { get; init; }
            public int? DrumPhanTram { get; init; }
            public string? VatTuJson { get; init; }
            public string? TrangThaiThietBi { get; init; }
            public string Nguon { get; init; } = "";
        }

        /// <summary>
        /// Số trang trong kỳ của MỘT cột đồng hồ (In / Copy / Scan / màu / đen trắng): chỉ xét
        /// những mốc thật sự có cột đó, vì dòng nạp lịch sử / nhập tay chỉ có Tổng.
        /// <paramref name="ls"/> phải sắp giảm dần theo ngày.
        /// </summary>
        private static int? ChenhTheoCot(List<ChiSoMoc>? ls, DateOnly homNay, int soNgayLui, Func<ChiSoMoc, int?> lay)
        {
            var dsCo = ls?.Where(x => lay(x) != null).ToList();
            if (dsCo is null || dsCo.Count < 2) return null;

            var mocNay = dsCo[0];
            var mocTruoc = dsCo.FirstOrDefault(x => x.NgayChot <= homNay.AddDays(-soNgayLui)) ?? dsCo[^1];
            if (mocTruoc.NgayChot == mocNay.NgayChot) return null;

            var d = lay(mocNay)!.Value - lay(mocTruoc)!.Value;
            return d < 0 ? null : d; // counter tụt = thay main board / reset
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

        /// <summary>
        /// Danh sách máy in màu IT khai tay trong appsettings (nguồn: file Print out information
        /// của bộ phận IT). Khoá "BoPhan|Serial", bỏ dấu cách và không phân biệt hoa thường;
        /// dòng chỉ ghi serial thì khớp mọi bộ phận.
        /// </summary>
        private HashSet<string> LayKhoaMayMau()
        {
            var ds = _configuration.GetSection("MayIn:SerialMayMau").Get<string[]>() ?? Array.Empty<string>();
            return ds.Where(x => !string.IsNullOrWhiteSpace(x))
                     .Select(x => x.Replace(" ", "").ToUpperInvariant())
                     .ToHashSet();
        }

        /// <summary>Máy có nằm trong danh sách máy màu khai tay không.</summary>
        private static bool TrongDanhSachMayMau(HashSet<string> khoa, string? boPhan, string? serial)
        {
            if (string.IsNullOrWhiteSpace(serial)) return false;
            var s = serial.Replace(" ", "").ToUpperInvariant();
            return khoa.Contains(s)
                || khoa.Contains(((boPhan ?? "").Replace(" ", "") + "|" + s).ToUpperInvariant());
        }

        /// <summary>
        /// Máy in màu hay đen trắng. Căn cứ chắc chắn nhất là vật tư đọc được từ chính máy: có
        /// TONER/DRUM màu (_C, _M, _Y) là máy màu, chỉ có _K là máy đen trắng. Máy chưa đọc được
        /// vật tư thì dò từ khoá trong model / ghi chú; không đủ căn cứ thì trả "Chưa rõ" chứ
        /// không đoán bừa — biên bản mà ghi sai loại máy còn phiền hơn để trống.
        /// </summary>
        private static string PhanLoaiMau(List<VatTuHienThi>? vatTu, string? model, string? ghiChu)
        {
            if (vatTu is { Count: > 0 })
            {
                var ten = vatTu.Select(v => (v.Ten ?? "").ToUpperInvariant()).ToList();
                if (ten.Any(t => t.EndsWith("_C") || t.EndsWith("_M") || t.EndsWith("_Y"))) return "Màu";
                if (ten.Any(t => t.EndsWith("_K"))) return "Đen trắng";
            }

            var chu = ((model ?? "") + " " + (ghiChu ?? "")).ToLowerInvariant();
            if (chu.Contains("đen trắng") || chu.Contains("den trang")
                || chu.Contains("mono") || chu.Contains("b/w")) return "Đen trắng";
            if (chu.Contains("color") || chu.Contains("colour") || chu.Contains("màu")) return "Màu";

            return "Chưa rõ";
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
                .Select(c => new ChiSoMoc
                {
                    IdMayIn = c.IdMayIn, NgayChot = c.NgayChot, CounterTong = c.CounterTong,
                    CounterIn = c.CounterIn, CounterCopy = c.CounterCopy, CounterScan = c.CounterScan,
                    TonerPhanTram = c.TonerPhanTram, DrumPhanTram = c.DrumPhanTram,
                    VatTuJson = c.VatTuJson, TrangThaiThietBi = c.TrangThaiThietBi
                })
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
                // Chỉ lấy mốc có đúng cột đang tính: dòng nạp lịch sử / nhập tay chỉ có Tổng,
                // không có In/Copy/Scan, nên không được dùng làm mốc cho các cột tách.
                int? TrangIn(int soNgay, Func<ChiSoMoc, int?> lay)
                {
                    var nay = moiNhat == null ? null : lay(moiNhat);
                    if (lichSu == null || nay == null) return null;
                    var coCot = lichSu.Where(x => lay(x) != null).ToList();
                    var moc = coCot.FirstOrDefault(x => x.NgayChot <= homNay.AddDays(-soNgay))
                              ?? coCot.LastOrDefault();
                    if (moc == null || moc.NgayChot == moiNhat!.NgayChot) return null;
                    var chenh = nay.Value - lay(moc)!.Value;
                    return chenh < 0 ? null : chenh; // counter tụt = thay main board / reset, bỏ qua
                }

                // Số tờ in trong hôm nay = chỉ số chốt hôm nay trừ chỉ số của lần chốt gần nhất trước đó
                int? TrangInHomNay(Func<ChiSoMoc, int?> lay)
                {
                    var cuaHomNay = lichSu?.FirstOrDefault(x => x.NgayChot == homNay);
                    var nay = cuaHomNay == null ? null : lay(cuaHomNay);
                    if (nay == null) return null;

                    var truocDo = lichSu!.FirstOrDefault(x => x.NgayChot < homNay && lay(x) != null);
                    if (truocDo == null) return null;

                    var chenh = nay.Value - lay(truocDo)!.Value;
                    return chenh < 0 ? null : chenh;
                }

                // Tách riêng số trang In / Copy / Scan của một kỳ. Tổng (In + Copy) giữ nguyên ở
                // các trường TrangIn* vì thẻ tóm tắt và bộ lọc đang dùng; Scan không tính vào tổng
                // vì không tốn giấy.
                object TachKy(Func<Func<ChiSoMoc, int?>, int?> tinh) => new
                {
                    In = tinh(c => c.CounterIn),
                    Copy = tinh(c => c.CounterCopy),
                    Scan = tinh(c => c.CounterScan)
                };

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
                    TrangInHomNay = TrangInHomNay(c => c.CounterTong),
                    TrangIn7Ngay = TrangIn(7, c => c.CounterTong),
                    TrangIn30Ngay = TrangIn(30, c => c.CounterTong),
                    TachChiSo = new { In = moiNhat?.CounterIn, Copy = moiNhat?.CounterCopy, Scan = moiNhat?.CounterScan },
                    TachHomNay = TachKy(lay => TrangInHomNay(lay)),
                    Tach7Ngay = TachKy(lay => TrangIn(7, lay)),
                    Tach30Ngay = TachKy(lay => TrangIn(30, lay)),
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

        #region Xuất Excel báo cáo trang in

        /// <summary>
        /// Xuất báo cáo chỉ số trang in hiện tại của các máy đang xem.
        /// Máy nào không có chỉ số chuẩn lấy qua IP — cắm USB, chưa bật đọc tự động, số liệu cũ,
        /// nhập tay — thì đánh dấu "Cần lấy trực tiếp" kèm lý do, và gom sang sheet thứ hai làm
        /// phiếu đi đọc tay tại máy.
        ///
        /// KHÁC với bảng trên màn hình: file xuất luôn có đủ cả máy Tạm dừng và Báo phế, bất kể
        /// màn hình đang lọc trạng thái nào, vì báo cáo dùng để đối chiếu tài sản — thiếu máy báo
        /// phế là thiếu số liệu. Tham số trangThai vẫn nhận cho tương thích URL cũ nhưng không lọc.
        ///
        /// Đây là hành động tải file xuống nên được phép điều hướng thật, không trả JSON.
        /// </summary>
        [HttpGet("/QLMayIn/XuatExcel")]
        public async Task<IActionResult> XuatExcel(string? tuKhoa, string? boPhan, string? model, string? trangThai)
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
            // Cố ý KHÔNG lọc theo trangThai — xem phần mô tả ở đầu hàm

            var dsMay = await truyVan
                .OrderBy(m => m.BoPhan).ThenBy(m => m.ViTri).ThenBy(m => m.Model).ThenBy(m => m.Serial)
                .Select(m => new
                {
                    m.IdMayIn, m.BoPhan, m.Model, m.Serial, m.DiaChiIp, m.TenHangDoi, m.ViTri,
                    m.TrangThai, m.TheoDoiTuDong, m.LanDocCuoi, m.KetQuaDocCuoi, m.GhiChu
                })
                .ToListAsync();

            var dsId = dsMay.Select(m => m.IdMayIn).ToList();
            var homNay = DateOnly.FromDateTime(DateTime.Now);
            var tuNgay = homNay.AddDays(-60);

            // Lấy một lượt chỉ số 60 ngày gần nhất của cả danh sách để tính chênh lệch 1/7/30 ngày
            var lichSu = await _context.MayInChiSos.AsNoTracking()
                .Where(c => dsId.Contains(c.IdMayIn) && c.NgayChot >= tuNgay && c.CounterTong != null)
                .Select(c => new ChiSoMoc
                {
                    IdMayIn = c.IdMayIn, NgayChot = c.NgayChot, CounterTong = c.CounterTong,
                    CounterIn = c.CounterIn, CounterCopy = c.CounterCopy, CounterScan = c.CounterScan,
                    CounterInMau = c.CounterInMau, CounterInDenTrang = c.CounterInDenTrang,
                    TonerPhanTram = c.TonerPhanTram, DrumPhanTram = c.DrumPhanTram,
                    Nguon = c.Nguon, VatTuJson = c.VatTuJson
                })
                .ToListAsync();

            var theoMay = lichSu.GroupBy(c => c.IdMayIn)
                                .ToDictionary(g => g.Key, g => g.OrderByDescending(x => x.NgayChot).ToList());

            // Máy đã lâu không đọc được (bản ghi cuối cũ hơn 60 ngày) vẫn phải hiện số cũ để đối chiếu,
            // nên lấy bù bản ghi cuối cùng của riêng nhóm này.
            var idThieu = dsId.Where(id => !theoMay.ContainsKey(id)).ToList();
            if (idThieu.Count > 0)
            {
                var ngayMax = await _context.MayInChiSos.AsNoTracking()
                    .Where(c => idThieu.Contains(c.IdMayIn) && c.CounterTong != null)
                    .GroupBy(c => c.IdMayIn)
                    .Select(g => g.Max(x => x.NgayChot))
                    .ToListAsync();

                var dongCu = await _context.MayInChiSos.AsNoTracking()
                    .Where(c => idThieu.Contains(c.IdMayIn) && ngayMax.Contains(c.NgayChot) && c.CounterTong != null)
                    .Select(c => new ChiSoMoc
                    {
                        IdMayIn = c.IdMayIn, NgayChot = c.NgayChot, CounterTong = c.CounterTong,
                        CounterIn = c.CounterIn, CounterCopy = c.CounterCopy, CounterScan = c.CounterScan,
                        CounterInMau = c.CounterInMau, CounterInDenTrang = c.CounterInDenTrang,
                        TonerPhanTram = c.TonerPhanTram, DrumPhanTram = c.DrumPhanTram,
                        Nguon = c.Nguon, VatTuJson = c.VatTuJson
                    })
                    .ToListAsync();

                foreach (var nhom in dongCu.GroupBy(c => c.IdMayIn))
                    theoMay[nhom.Key] = nhom.OrderByDescending(x => x.NgayChot).ToList();
            }

            // Chỉ số đọc trong vòng ngần này ngày vẫn coi là "hiện tại"; job nền chốt mỗi ngày nên
            // mặc định cho lệch 1 ngày (buổi sáng có thể mới chỉ có số của hôm qua).
            var soNgayConTinCay = _configuration.GetValue<int?>("MayIn:SoNgayChiSoConTinCay") ?? 1;
            if (soNgayConTinCay < 0) soNgayConTinCay = 0;

            var khoaMayMau = LayKhoaMayMau();

            var dong = dsMay.Select(m =>
            {
                theoMay.TryGetValue(m.IdMayIn, out var ls);
                var moiNhat = ls?.FirstOrDefault();

                int? Chenh(int soNgayLui)
                {
                    if (ls == null || moiNhat?.CounterTong == null) return null;
                    var moc = ls.FirstOrDefault(x => x.NgayChot <= homNay.AddDays(-soNgayLui)) ?? ls.LastOrDefault();
                    if (moc?.CounterTong == null || moc.NgayChot == moiNhat.NgayChot) return null;
                    var d = moiNhat.CounterTong.Value - moc.CounterTong.Value;
                    return d < 0 ? null : d; // counter tụt = thay main board / reset, không quy ra số trang
                }

                // Số tờ màu / đen trắng trong kỳ — cùng cách tính với tổng, nhưng chỉ chạy trên
                // những mốc THẬT SỰ có số tách màu. Máy đọc qua CentreWare cũ / PJL không có hai
                // chỉ số này nên trả null, không suy ngược từ tổng.
                int? ChenhMau(int soNgayLui, bool laMau) =>
                    ChenhTheoCot(ls, homNay, soNgayLui, x => laMau ? x.CounterInMau : x.CounterInDenTrang);

                // Đồng hồ In / Copy / Scan mới nhất; dòng nhập tay / nạp lịch sử không có ba cột này
                // nên lùi về lần chốt gần nhất có số.
                var mocTach = ls?.FirstOrDefault(x => x.CounterIn != null || x.CounterCopy != null || x.CounterScan != null);

                // Gom mọi lý do khiến con số không đáng tin, để người đi đọc tay biết vì sao phải đi
                var lyDo = new List<string>();
                if (string.IsNullOrWhiteSpace(m.DiaChiIp)) lyDo.Add("Không có IP (cắm USB)");
                else if (!m.TheoDoiTuDong) lyDo.Add("Chưa bật đọc tự động");

                if (moiNhat is null)
                {
                    lyDo.Add("Chưa có chỉ số nào");
                }
                else
                {
                    var soNgayCu = homNay.DayNumber - moiNhat.NgayChot.DayNumber;
                    if (soNgayCu > soNgayConTinCay)
                        lyDo.Add($"Chỉ số cũ {soNgayCu} ngày (máy tắt hoặc không phản hồi)");
                    if (moiNhat.Nguon != "API")
                        lyDo.Add($"Nguồn {moiNhat.Nguon}, không đọc qua IP");
                }

                if (lyDo.Count > 0 && !string.IsNullOrWhiteSpace(m.KetQuaDocCuoi)
                    && !m.KetQuaDocCuoi.StartsWith("OK", StringComparison.OrdinalIgnoreCase))
                    lyDo.Add($"Lần đọc cuối: {m.KetQuaDocCuoi}");

                // Lấy bản đọc gần nhất CÓ vật tư để đoán màu; bản mới nhất đôi khi là dòng nhập tay
                // không kèm vật tư, lùi lại vài ngày vẫn ra đúng loại máy.
                var vatTu = ls?.Select(x => PhanTichVatTu(x.VatTuJson)).FirstOrDefault(v => v != null);

                // Đồng hồ tách màu gần nhất đọc được. Đây là số có NGAY từ lần chốt đầu tiên,
                // khác với cột chênh lệch 30 ngày phải đợi đủ hai mốc mới ra số.
                var mocTachMau = ls?.FirstOrDefault(x => x.CounterInMau != null || x.CounterInDenTrang != null);

                // Máy nào báo được đồng hồ in màu thì chắc chắn là máy màu, kể cả khi số đang là 0
                // (máy có khả năng in màu nhưng chưa ai in) — căn cứ này chắc hơn cả vật tư.
                // Máy chưa đọc được tách màu thì tra danh sách IT khai tay trước khi đoán theo vật tư.
                var color = mocTachMau?.CounterInMau != null
                    ? "Màu"
                    : TrongDanhSachMayMau(khoaMayMau, m.BoPhan, m.Serial)
                        ? "Màu"
                        : PhanLoaiMau(vatTu, m.Model, m.GhiChu);

                return new
                {
                    m.BoPhan, m.Model, m.Serial, m.DiaChiIp, m.TenHangDoi, m.ViTri,
                    Color = color,
                    CounterInMau = mocTachMau?.CounterInMau,
                    CounterInDenTrang = mocTachMau?.CounterInDenTrang,
                    TrangThaiGoc = m.TrangThai,
                    TrangThai = m.TrangThai switch
                    {
                        "TamDung" => "Tạm dừng",
                        "BaoPhe" => "Báo phế",
                        _ => "Hoạt động"
                    },
                    CounterTong = moiNhat?.CounterTong,
                    CounterIn = mocTach?.CounterIn,
                    CounterCopy = mocTach?.CounterCopy,
                    CounterScan = mocTach?.CounterScan,
                    NgayChiSo = moiNhat?.NgayChot,
                    Nguon = moiNhat?.Nguon,
                    TrangInHomNay = Chenh(1),
                    TrangIn7Ngay = Chenh(7),
                    TrangIn30Ngay = Chenh(30),
                    In30Ngay = ChenhTheoCot(ls, homNay, 30, x => x.CounterIn),
                    Copy30Ngay = ChenhTheoCot(ls, homNay, 30, x => x.CounterCopy),
                    Scan30Ngay = ChenhTheoCot(ls, homNay, 30, x => x.CounterScan),
                    TrangMau30Ngay = ChenhMau(30, true),
                    TrangDenTrang30Ngay = ChenhMau(30, false),
                    Toner = moiNhat?.TonerPhanTram,
                    Drum = moiNhat?.DrumPhanTram,
                    CanLayTrucTiep = lyDo.Count > 0,
                    LyDo = string.Join("; ", lyDo)
                };
            })
            // Máy màu lên đầu (số ít, hay phải tra), rồi tới máy lấy được chỉ số qua IP;
            // máy phải đọc tay dồn xuống cuối từng nhóm
            .OrderByDescending(x => x.Color == "Màu")
            .ThenBy(x => x.CanLayTrucTiep)
            .ThenBy(x => x.BoPhan).ThenBy(x => x.ViTri).ThenBy(x => x.Model).ThenBy(x => x.Serial)
            .ToList();

            var soCanLayTay = dong.Count(x => x.CanLayTrucTiep);
            var soMau = dong.Count(x => x.Color == "Màu");
            var soDenTrang = dong.Count(x => x.Color == "Đen trắng");
            var soTachDuoc = dong.Count(x => x.CounterInMau != null || x.CounterInDenTrang != null);

            // Máy ngừng dùng gom riêng xuống dưới để không lẫn vào số liệu máy đang chạy,
            // nhưng vẫn phải có mặt đủ trong file.
            var thuTuTrangThai = new[] { "HoatDong", "TamDung", "BaoPhe" };
            var cacNhom = thuTuTrangThai
                .Select(tt => (
                    Ten: tt switch { "TamDung" => "MÁY TẠM DỪNG", "BaoPhe" => "MÁY BÁO PHẾ", _ => "MÁY ĐANG HOẠT ĐỘNG" },
                    Ds: dong.Where(x => x.TrangThaiGoc == tt).ToList()))
                .Where(n => n.Ds.Count > 0)
                .ToList();

            // Trạng thái lạ (dữ liệu cũ ghi khác ba giá trị chuẩn) vẫn phải xuất, gom vào nhóm cuối
            var dsLac = dong.Where(x => !thuTuTrangThai.Contains(x.TrangThaiGoc)).ToList();
            if (dsLac.Count > 0) cacNhom.Add(("TRẠNG THÁI KHÁC", dsLac));

            using var wb = new ClosedXML.Excel.XLWorkbook();
            var ws = wb.Worksheets.Add("Trang in");

            var tieuDe = new[]
            {
                "STT", "Bộ phận", "Model", "Color", "Serial", "Tên máy in", "IP", "Vị trí", "Trạng thái",
                "Chỉ số hiện tại", "Chỉ số In", "Chỉ số Copy", "Chỉ số Scan",
                "Chỉ số in màu", "Chỉ số in đen trắng",
                "Ngày đọc", "Nguồn", "Hôm nay", "7 ngày", "30 ngày",
                "In 30 ngày", "Copy 30 ngày", "Scan 30 ngày",
                "Màu 30 ngày", "Đen trắng 30 ngày",
                "Mực (%)", "Trống (%)", "Cần lấy trực tiếp", "Lý do / ghi chú"
            };

            const int cotColor = 4;
            const int cotChiSo = 10;
            const int cotIn = 11;
            const int cotCopy = 12;
            const int cotScan = 13;
            const int cotMau = 14;        // đồng hồ tích luỹ, có ngay từ lần chốt đầu tiên
            const int cotDenTrang = 15;
            const int cotNgayDoc = 16;
            const int cotNguon = 17;
            const int cotHomNay = 18;
            const int cot7Ngay = 19;
            const int cot30Ngay = 20;
            const int cotIn30 = 21;
            const int cotCopy30 = 22;
            const int cotScan30 = 23;
            const int cotMau30 = 24;
            const int cotDenTrang30 = 25;
            const int cotMuc = 26;
            const int cotTrong = 27;
            const int cotCanLay = 28;
            const int cotLyDo = 29;

            ws.Cell(1, 1).Value = "BÁO CÁO CHỈ SỐ TRANG IN HIỆN TẠI";
            ws.Range(1, 1, 1, tieuDe.Length).Merge().Style.Font.SetBold().Font.SetFontSize(14)
              .Alignment.SetHorizontal(ClosedXML.Excel.XLAlignmentHorizontalValues.Center);

            ws.Cell(2, 1).Value = $"Xuất lúc {DateTime.Now:dd/MM/yyyy HH:mm} — {dong.Count} máy "
                               + $"(máy in màu: {soMau}, đen trắng: {soDenTrang}), "
                               + $"{soCanLayTay} máy cần lấy chỉ số trực tiếp tại máy. "
                               + "Cột \"Chỉ số hiện tại\": xanh = đọc được qua IP, "
                               + "cam = số cũ / nhập tay (chỉ tham khảo), xám = chưa có chỉ số. "
                               + $"Chỉ {soTachDuoc}/{dong.Count} máy báo được đồng hồ tách màu; "
                               + "Chỉ số hiện tại = In + Copy (Scan không tốn giấy nên không cộng). "
                               + "Máy đen trắng: cột in đen trắng lấy số bản In (in nghiêng = số suy ra). "
                               + "Các cột \"… 30 ngày\" chỉ có số khi máy đã có từ 2 lần chốt trở lên. "
                               + "Máy màu chưa đọc được đồng hồ tách màu (IT khai trong file Print out "
                               + "information) ghi \"?\" ở cột chỉ số in màu; máy màu xếp lên đầu mỗi nhóm.";
            ws.Range(2, 1, 2, tieuDe.Length).Merge().Style.Font.SetItalic()
              .Alignment.SetHorizontal(ClosedXML.Excel.XLAlignmentHorizontalValues.Center);

            for (var i = 0; i < tieuDe.Length; i++) ws.Cell(4, i + 1).Value = tieuDe[i];
            ws.Range(4, 1, 4, tieuDe.Length).Style.Font.SetBold()
              .Fill.SetBackgroundColor(ClosedXML.Excel.XLColor.LightGray)
              .Alignment.SetHorizontal(ClosedXML.Excel.XLAlignmentHorizontalValues.Center);

            // "Chỉ số hiện tại" là con số người ta mở file ra để xem, nên đánh dấu riêng cả tiêu đề
            // lẫn từng ô: xanh = số đọc được qua IP, cam = số cũ / nhập tay chỉ để tham khảo.
            ws.Cell(4, cotChiSo).Style.Fill.SetBackgroundColor(ClosedXML.Excel.XLColor.FromArgb(31, 78, 121))
              .Font.SetFontColor(ClosedXML.Excel.XLColor.White);

            // Hai cột đồng hồ tách màu: tô đúng bộ màu đang dùng cho cột Color
            ws.Cell(4, cotMau).Style.Fill.SetBackgroundColor(ClosedXML.Excel.XLColor.FromArgb(175, 30, 120))
              .Font.SetFontColor(ClosedXML.Excel.XLColor.White);
            ws.Cell(4, cotDenTrang).Style.Fill.SetBackgroundColor(ClosedXML.Excel.XLColor.FromArgb(64, 64, 64))
              .Font.SetFontColor(ClosedXML.Excel.XLColor.White);

            var r = 5;
            foreach (var (tenNhom, dsNhom) in cacNhom)
            {
                // Chỉ có đúng một nhóm thì khỏi chèn dòng tiêu đề cho đỡ rối
                if (cacNhom.Count > 1)
                {
                    ws.Cell(r, 1).Value = $"{tenNhom} ({dsNhom.Count})";
                    ws.Range(r, 1, r, tieuDe.Length).Merge().Style
                      .Font.SetBold()
                      .Fill.SetBackgroundColor(ClosedXML.Excel.XLColor.FromArgb(221, 235, 247))
                      .Alignment.SetHorizontal(ClosedXML.Excel.XLAlignmentHorizontalValues.Left);
                    r++;
                }

                var stt = 1;
                foreach (var d in dsNhom)
                {
                    ws.Cell(r, 1).Value = stt++;
                    ws.Cell(r, 2).Value = d.BoPhan;
                    ws.Cell(r, 3).Value = d.Model;
                    ws.Cell(r, cotColor).Value = d.Color;
                    ws.Cell(r, 5).Value = d.Serial;
                    ws.Cell(r, 6).Value = d.TenHangDoi;
                    ws.Cell(r, 7).Value = d.DiaChiIp;
                    ws.Cell(r, 8).Value = d.ViTri;
                    ws.Cell(r, 9).Value = d.TrangThai;

                    if (d.CounterTong.HasValue) ws.Cell(r, cotChiSo).Value = d.CounterTong.Value;
                    else ws.Cell(r, cotChiSo).Value = "—";
                    // Máy không tách được màu để trống hẳn, khác hẳn với máy tách được mà in 0 tờ màu
                    // Riêng máy ĐÃ BIẾT là máy màu (IT khai trong file) mà chưa đọc được đồng hồ tách
                    // màu thì ghi "?" để khỏi đọc nhầm thành máy đen trắng — số của nó nằm gộp trong
                    // cột "Chỉ số hiện tại".
                    if (d.CounterInMau.HasValue) ws.Cell(r, cotMau).Value = d.CounterInMau.Value;
                    else if (d.Color == "Màu")
                    {
                        ws.Cell(r, cotMau).Value = "?";
                        ws.Cell(r, cotMau).Style.Font.SetBold()
                          .Font.SetFontColor(ClosedXML.Excel.XLColor.FromArgb(150, 20, 100))
                          .Alignment.SetHorizontal(ClosedXML.Excel.XLAlignmentHorizontalValues.Center);
                    }
                    if (d.CounterIn.HasValue) ws.Cell(r, cotIn).Value = d.CounterIn.Value;
                    if (d.CounterCopy.HasValue) ws.Cell(r, cotCopy).Value = d.CounterCopy.Value;
                    if (d.CounterScan.HasValue) ws.Cell(r, cotScan).Value = d.CounterScan.Value;

                    // Máy đen trắng không báo đồng hồ tách màu, nhưng mọi bản in của nó đều là đen
                    // trắng → lấy số bản In (máy chỉ có tổng thì lấy tổng), in nghiêng để biết là số suy ra.
                    var denTrangSuyRa = !d.CounterInDenTrang.HasValue && d.Color == "Đen trắng";
                    var chiSoDenTrang = d.CounterInDenTrang ?? (denTrangSuyRa ? d.CounterIn ?? d.CounterTong : null);
                    var denTrang30 = d.TrangDenTrang30Ngay ?? (denTrangSuyRa ? d.In30Ngay ?? d.TrangIn30Ngay : null);
                    if (chiSoDenTrang.HasValue)
                    {
                        ws.Cell(r, cotDenTrang).Value = chiSoDenTrang.Value;
                        if (denTrangSuyRa) ws.Cell(r, cotDenTrang).Style.Font.SetItalic();
                    }

                    if (d.NgayChiSo.HasValue) ws.Cell(r, cotNgayDoc).Value = d.NgayChiSo.Value.ToString("dd/MM/yyyy");
                    ws.Cell(r, cotNguon).Value = d.Nguon;
                    if (d.TrangInHomNay.HasValue) ws.Cell(r, cotHomNay).Value = d.TrangInHomNay.Value;
                    if (d.TrangIn7Ngay.HasValue) ws.Cell(r, cot7Ngay).Value = d.TrangIn7Ngay.Value;
                    if (d.TrangIn30Ngay.HasValue) ws.Cell(r, cot30Ngay).Value = d.TrangIn30Ngay.Value;
                    if (d.In30Ngay.HasValue) ws.Cell(r, cotIn30).Value = d.In30Ngay.Value;
                    if (d.Copy30Ngay.HasValue) ws.Cell(r, cotCopy30).Value = d.Copy30Ngay.Value;
                    if (d.Scan30Ngay.HasValue) ws.Cell(r, cotScan30).Value = d.Scan30Ngay.Value;
                    if (d.TrangMau30Ngay.HasValue) ws.Cell(r, cotMau30).Value = d.TrangMau30Ngay.Value;
                    if (denTrang30.HasValue)
                    {
                        ws.Cell(r, cotDenTrang30).Value = denTrang30.Value;
                        if (denTrangSuyRa) ws.Cell(r, cotDenTrang30).Style.Font.SetItalic();
                    }
                    if (d.Toner.HasValue) ws.Cell(r, cotMuc).Value = d.Toner.Value;
                    if (d.Drum.HasValue) ws.Cell(r, cotTrong).Value = d.Drum.Value;

                    ws.Cell(r, cotCanLay).Value = d.CanLayTrucTiep ? "X" : "";
                    ws.Cell(r, cotLyDo).Value = d.LyDo;

                    // Tô vàng cả dòng để lúc in ra giấy vẫn thấy ngay máy nào phải đi đọc tay
                    if (d.CanLayTrucTiep)
                        ws.Range(r, 1, r, tieuDe.Length).Style.Fill
                          .SetBackgroundColor(ClosedXML.Excel.XLColor.FromArgb(255, 242, 204));

                    // Hai ô đánh dấu tô SAU nền cả dòng, nếu không màu vàng của dòng sẽ đè mất
                    var oColor = ws.Cell(r, cotColor).Style;
                    oColor.Font.SetBold()
                          .Alignment.SetHorizontal(ClosedXML.Excel.XLAlignmentHorizontalValues.Center);
                    if (d.Color == "Màu")
                    {
                        oColor.Fill.SetBackgroundColor(ClosedXML.Excel.XLColor.FromArgb(255, 230, 246));
                        oColor.Font.SetFontColor(ClosedXML.Excel.XLColor.FromArgb(175, 30, 120));
                    }
                    else if (d.Color == "Đen trắng")
                    {
                        oColor.Fill.SetBackgroundColor(ClosedXML.Excel.XLColor.FromArgb(235, 235, 235));
                        oColor.Font.SetFontColor(ClosedXML.Excel.XLColor.FromArgb(64, 64, 64));
                    }
                    else
                    {
                        oColor.Font.SetFontColor(ClosedXML.Excel.XLColor.FromArgb(150, 150, 150));
                    }

                    var oChiSo = ws.Cell(r, cotChiSo).Style;
                    oChiSo.Font.SetBold().Alignment
                          .SetHorizontal(ClosedXML.Excel.XLAlignmentHorizontalValues.Right);

                    if (!d.CounterTong.HasValue)
                    {
                        oChiSo.Fill.SetBackgroundColor(ClosedXML.Excel.XLColor.FromArgb(242, 242, 242));
                        oChiSo.Font.SetFontColor(ClosedXML.Excel.XLColor.FromArgb(128, 128, 128));
                        oChiSo.Alignment.SetHorizontal(ClosedXML.Excel.XLAlignmentHorizontalValues.Center);
                    }
                    else if (d.CanLayTrucTiep)
                    {
                        oChiSo.Fill.SetBackgroundColor(ClosedXML.Excel.XLColor.FromArgb(252, 228, 214));
                        oChiSo.Font.SetFontColor(ClosedXML.Excel.XLColor.FromArgb(192, 80, 0));
                    }
                    else
                    {
                        oChiSo.Fill.SetBackgroundColor(ClosedXML.Excel.XLColor.FromArgb(226, 240, 217));
                        oChiSo.Font.SetFontColor(ClosedXML.Excel.XLColor.FromArgb(0, 97, 0));
                    }

                    r++;
                }
            }

            if (r > 5)
            {
                var vung = ws.Range(4, 1, r - 1, tieuDe.Length);
                vung.Style.Border.SetOutsideBorder(ClosedXML.Excel.XLBorderStyleValues.Thin);
                vung.Style.Border.SetInsideBorder(ClosedXML.Excel.XLBorderStyleValues.Thin);
                ws.Range(5, cotChiSo, r - 1, cotTrong).Style.NumberFormat.SetFormat("#,##0");
                // Có dòng tiêu đề nhóm (ô merge) thì không bật AutoFilter, lọc sẽ giấu mất dòng nhóm
                if (cacNhom.Count <= 1) vung.SetAutoFilter();
                ws.SheetView.FreezeRows(4);
            }

            ws.Columns(1, tieuDe.Length).AdjustToContents();
            ws.Column(cotLyDo).Width = 45;
            ws.Column(cotLyDo).Style.Alignment.SetWrapText(true);

            // Sheet 2: phiếu đi đọc tay — chỉ máy không lấy được số chuẩn qua IP, chừa cột trống để ghi.
            // Máy Tạm dừng / Báo phế không đưa vào đây vì không ai phải đi đọc chỉ số máy đã ngừng dùng;
            // số liệu của chúng vẫn nằm đủ ở sheet 1.
            var dsDiDoc = dong.Where(x => x.CanLayTrucTiep && x.TrangThaiGoc == "HoatDong").ToList();

            var ws2 = wb.Worksheets.Add("Cần lấy trực tiếp");

            var tieuDe2 = new[]
            {
                "STT", "Bộ phận", "Model", "Serial", "Vị trí", "IP",
                "Chỉ số hệ thống đang có", "Ngày của chỉ số đó", "Lý do phải đọc tay",
                "Chỉ số đọc tay (điền)"
            };

            ws2.Cell(1, 1).Value = "DANH SÁCH MÁY CẦN ĐỌC CHỈ SỐ TRỰC TIẾP TẠI MÁY";
            ws2.Range(1, 1, 1, tieuDe2.Length).Merge().Style.Font.SetBold().Font.SetFontSize(14)
               .Alignment.SetHorizontal(ClosedXML.Excel.XLAlignmentHorizontalValues.Center);
            ws2.Cell(2, 1).Value = $"Xuất lúc {DateTime.Now:dd/MM/yyyy HH:mm} — {dsDiDoc.Count} máy đang hoạt động";
            ws2.Range(2, 1, 2, tieuDe2.Length).Merge().Style.Font.SetItalic()
               .Alignment.SetHorizontal(ClosedXML.Excel.XLAlignmentHorizontalValues.Center);

            for (var i = 0; i < tieuDe2.Length; i++) ws2.Cell(4, i + 1).Value = tieuDe2[i];
            ws2.Range(4, 1, 4, tieuDe2.Length).Style.Font.SetBold()
               .Fill.SetBackgroundColor(ClosedXML.Excel.XLColor.LightGray)
               .Alignment.SetHorizontal(ClosedXML.Excel.XLAlignmentHorizontalValues.Center);

            // Cùng bộ màu với sheet 1: cột chỉ số hệ thống đang có (cam = chỉ để tham khảo),
            // cột chỉ số đọc tay để trống cho người đi đọc điền vào
            ws2.Cell(4, 7).Style.Fill.SetBackgroundColor(ClosedXML.Excel.XLColor.FromArgb(192, 80, 0))
               .Font.SetFontColor(ClosedXML.Excel.XLColor.White);
            ws2.Cell(4, 10).Style.Fill.SetBackgroundColor(ClosedXML.Excel.XLColor.FromArgb(31, 78, 121))
               .Font.SetFontColor(ClosedXML.Excel.XLColor.White);

            var r2 = 5;
            foreach (var d in dsDiDoc)
            {
                ws2.Cell(r2, 1).Value = r2 - 4;
                ws2.Cell(r2, 2).Value = d.BoPhan;
                ws2.Cell(r2, 3).Value = d.Model;
                ws2.Cell(r2, 4).Value = d.Serial;
                ws2.Cell(r2, 5).Value = d.ViTri;
                ws2.Cell(r2, 6).Value = string.IsNullOrWhiteSpace(d.DiaChiIp) ? "Cắm USB" : d.DiaChiIp;
                if (d.CounterTong.HasValue) ws2.Cell(r2, 7).Value = d.CounterTong.Value;
                if (d.NgayChiSo.HasValue) ws2.Cell(r2, 8).Value = d.NgayChiSo.Value.ToString("dd/MM/yyyy");
                ws2.Cell(r2, 9).Value = d.LyDo;
                r2++;
            }

            if (r2 > 5)
            {
                var vung2 = ws2.Range(4, 1, r2 - 1, tieuDe2.Length);
                vung2.Style.Border.SetOutsideBorder(ClosedXML.Excel.XLBorderStyleValues.Thin);
                vung2.Style.Border.SetInsideBorder(ClosedXML.Excel.XLBorderStyleValues.Thin);
                ws2.Range(5, 7, r2 - 1, 7).Style.NumberFormat.SetFormat("#,##0");
                ws2.Range(5, 7, r2 - 1, 7).Style.Font.SetBold()
                   .Font.SetFontColor(ClosedXML.Excel.XLColor.FromArgb(192, 80, 0))
                   .Fill.SetBackgroundColor(ClosedXML.Excel.XLColor.FromArgb(252, 228, 214));
                ws2.Range(5, 10, r2 - 1, 10).Style.Fill
                   .SetBackgroundColor(ClosedXML.Excel.XLColor.FromArgb(221, 235, 247));
                ws2.SheetView.FreezeRows(4);
            }
            else
            {
                ws2.Cell(5, 1).Value = "Tất cả máy đang hoạt động trong bộ lọc đều lấy được chỉ số qua IP.";
            }

            ws2.Columns(1, tieuDe2.Length).AdjustToContents();
            ws2.Column(9).Width = 45;
            ws2.Column(9).Style.Alignment.SetWrapText(true);
            ws2.Column(10).Width = 22;

            using var luong = new MemoryStream();
            wb.SaveAs(luong);

            return File(luong.ToArray(),
                "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                $"BaoCaoTrangIn_{DateTime.Now:yyyyMMdd_HHmm}.xlsx");
        }

        /// <summary>
        /// Xuất bảng theo dõi trang in theo tháng, dựng đúng kiểu file Excel IT đang dùng tay:
        /// mỗi tháng chiếm HAI cột — cột 1 đen trắng, cột 2 màu — mỗi dòng là một máy.
        ///
        /// Số của một tháng là chỉ số chốt CUỐI CÙNG đọc được trong tháng đó (đồng hồ tích luỹ,
        /// không phải số tờ in trong tháng). Máy không có đồng hồ tách màu thì cột đen trắng lấy
        /// counter tổng (máy đen trắng thì tổng chính là đen trắng) và được in nghiêng để biết là
        /// số suy ra, cột màu để trống.
        ///
        /// Cũng như XuatExcel: không lọc theo trạng thái, máy Tạm dừng / Báo phế vẫn phải có mặt
        /// đủ trong file để đối chiếu tài sản. Đây là hành động tải file nên trả file, không JSON.
        /// </summary>
        [HttpGet("/QLMayIn/XuatExcelTheoThang")]
        public async Task<IActionResult> XuatExcelTheoThang(int? nam, string? tuKhoa, string? boPhan, string? model)
        {
            var chan = KiemQuyen();
            if (chan != null) return chan;

            var namXuat = nam ?? DateTime.Now.Year;
            if (namXuat < 2000 || namXuat > DateTime.Now.Year + 1) namXuat = DateTime.Now.Year;

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

            var dsMay = await truyVan
                .OrderBy(m => m.BoPhan).ThenBy(m => m.ViTri).ThenBy(m => m.Model).ThenBy(m => m.Serial)
                .Select(m => new
                {
                    m.IdMayIn, m.BoPhan, m.Model, m.Serial, m.DiaChiIp, m.TenHangDoi, m.ViTri,
                    m.TrangThai, m.GhiChu
                })
                .ToListAsync();

            var dsId = dsMay.Select(m => m.IdMayIn).ToList();
            var dauNam = new DateOnly(namXuat, 1, 1);
            var cuoiNam = new DateOnly(namXuat, 12, 31);

            var chiSoNam = await _context.MayInChiSos.AsNoTracking()
                .Where(c => dsId.Contains(c.IdMayIn) && c.NgayChot >= dauNam && c.NgayChot <= cuoiNam
                         && (c.CounterTong != null || c.CounterInMau != null || c.CounterInDenTrang != null))
                .Select(c => new { c.IdMayIn, c.NgayChot, c.CounterTong, c.CounterInMau, c.CounterInDenTrang })
                .ToListAsync();

            // (id máy, tháng) -> (đen trắng, màu, đen trắng có phải suy từ tổng không)
            var oTheoThang = new Dictionary<(int IdMayIn, int Thang), (int? DenTrang, int? Mau, bool SuyTuTong)>();

            foreach (var nhom in chiSoNam.GroupBy(c => (c.IdMayIn, c.NgayChot.Month)))
            {
                // Bản chốt cuối tháng đôi khi là dòng nhập tay không kèm số tách màu, nên mỗi cột
                // tự lùi tìm mốc mới nhất CÓ số của chính nó thay vì dùng chung một bản ghi.
                var theoNgay = nhom.OrderByDescending(c => c.NgayChot).ToList();
                var mocDen = theoNgay.FirstOrDefault(c => c.CounterInDenTrang != null);
                var mocMau = theoNgay.FirstOrDefault(c => c.CounterInMau != null);
                var mocTong = theoNgay.FirstOrDefault(c => c.CounterTong != null);

                int? denTrang = mocDen?.CounterInDenTrang;
                var suyTuTong = false;

                if (denTrang == null && mocMau == null && mocTong != null)
                {
                    denTrang = mocTong.CounterTong;
                    suyTuTong = true;
                }

                oTheoThang[(nhom.Key.IdMayIn, nhom.Key.Month)] = (denTrang, mocMau?.CounterInMau, suyTuTong);
            }

            // Năm đang chạy thì chỉ dựng tới tháng hiện tại, khỏi kéo ngang qua mấy cột rỗng
            var soThang = namXuat == DateTime.Now.Year ? DateTime.Now.Month : 12;

            using var wb = new ClosedXML.Excel.XLWorkbook();
            var ws = wb.Worksheets.Add($"Trang in {namXuat}");

            // Máy nào là máy màu: chắc nhất là chính máy báo được đồng hồ in màu trong năm; máy chưa
            // đọc được thì tra danh sách IT khai tay trong appsettings (nguồn: file Print out
            // information), cuối cùng mới đoán theo model / ghi chú.
            var khoaMayMau = LayKhoaMayMau();
            var idCoDongHoMau = chiSoNam.Where(c => c.CounterInMau != null)
                                        .Select(c => c.IdMayIn).ToHashSet();

            string LoaiMay(int idMayIn, string? boPhan, string? serial, string? model, string? ghiChu)
                => idCoDongHoMau.Contains(idMayIn) || TrongDanhSachMayMau(khoaMayMau, boPhan, serial)
                    ? "Màu"
                    : PhanLoaiMau(null, model, ghiChu);

            var tieuDeCoDinh = new[] { "Bộ phận", "Model", "Serial No", "IP add", "Tên máy / Vị trí", "Loại" };
            const int cotLoai = 6;
            const int cotDauThang = 7;
            var cotCuoi = cotDauThang + soThang * 2 - 1;

            ws.Cell(1, 1).Value = $"BẢNG THEO DÕI TRANG IN {namXuat}";
            ws.Range(1, 1, 1, cotCuoi).Merge().Style.Font.SetBold().Font.SetFontSize(14)
              .Alignment.SetHorizontal(ClosedXML.Excel.XLAlignmentHorizontalValues.Center);

            ws.Cell(2, 1).Value = $"Xuất lúc {DateTime.Now:dd/MM/yyyy HH:mm} — {dsMay.Count} máy. "
                               + "Mỗi tháng gồm 2 cột: cột 1 đen trắng, cột 2 màu. "
                               + "Số hiển thị là chỉ số đồng hồ chốt lần cuối trong tháng đó (tích luỹ), "
                               + "không phải số tờ in riêng trong tháng. "
                               + "Số in nghiêng là máy không có đồng hồ tách màu, lấy tạm counter tổng. "
                               + "Cột \"Loại\": máy tự báo đồng hồ in màu, hoặc nằm trong danh sách máy màu "
                               + "IT khai trong file Print out information — cả hai đều tính là máy màu. "
                               + "Máy màu chưa đọc được đồng hồ tách màu thì cột màu ghi \"?\", "
                               + "số ở cột đen trắng khi đó là counter tổng (gồm cả tờ màu).";
            ws.Range(2, 1, 2, cotCuoi).Merge().Style.Font.SetItalic()
              .Alignment.SetHorizontal(ClosedXML.Excel.XLAlignmentHorizontalValues.Center);

            // Hai dòng tiêu đề: dòng 4 tên tháng (merge 2 cột), dòng 5 "Đen trắng" / "Màu"
            for (var i = 0; i < tieuDeCoDinh.Length; i++)
            {
                ws.Range(4, i + 1, 5, i + 1).Merge();
                ws.Cell(4, i + 1).Value = tieuDeCoDinh[i];
            }

            for (var t = 1; t <= soThang; t++)
            {
                var cotDen = cotDauThang + (t - 1) * 2;
                var cotMauT = cotDen + 1;

                ws.Range(4, cotDen, 4, cotMauT).Merge();
                ws.Cell(4, cotDen).Value = $"Tháng {t}/{namXuat}";
                ws.Cell(5, cotDen).Value = "Đen trắng";
                ws.Cell(5, cotMauT).Value = "Màu";

                ws.Cell(5, cotDen).Style.Fill.SetBackgroundColor(ClosedXML.Excel.XLColor.FromArgb(64, 64, 64))
                  .Font.SetFontColor(ClosedXML.Excel.XLColor.White);
                ws.Cell(5, cotMauT).Style.Fill.SetBackgroundColor(ClosedXML.Excel.XLColor.FromArgb(175, 30, 120))
                  .Font.SetFontColor(ClosedXML.Excel.XLColor.White);

                // Tháng chẵn / lẻ tô nền khác nhau để mắt không lạc cột khi kéo ngang
                if (t % 2 == 0)
                    ws.Cell(4, cotDen).Style.Fill.SetBackgroundColor(ClosedXML.Excel.XLColor.FromArgb(221, 235, 247));
            }

            ws.Range(4, 1, 5, cotCuoi).Style.Font.SetBold()
              .Alignment.SetHorizontal(ClosedXML.Excel.XLAlignmentHorizontalValues.Center)
              .Alignment.SetVertical(ClosedXML.Excel.XLAlignmentVerticalValues.Center);
            ws.Range(4, 1, 4, cotDauThang - 1).Style.Fill.SetBackgroundColor(ClosedXML.Excel.XLColor.LightGray);

            // Máy đang chạy lên trên, Tạm dừng rồi Báo phế dồn xuống cuối, mỗi nhóm một dòng nhãn —
            // số liệu máy đã ngừng dùng không lẫn vào phần đang theo dõi nhưng vẫn có mặt đủ trong file.
            // Loại máy tính sẵn một lần: vừa để đổ ra cột "Loại", vừa để xếp máy màu lên đầu mỗi nhóm.
            var dsMayLoai = dsMay
                .Select(m => new { May = m, Loai = LoaiMay(m.IdMayIn, m.BoPhan, m.Serial, m.Model, m.GhiChu) })
                .ToList();

            // Trong từng nhóm trạng thái: máy màu lên trên cùng (số ít, hay phải tra), rồi tới đen
            // trắng / chưa rõ; OrderBy của LINQ ổn định nên thứ tự Bộ phận → Vị trí → Model → Serial
            // lấy từ CSDL vẫn được giữ nguyên bên trong mỗi loại.
            var thuTuTrangThai = new[] { "HoatDong", "TamDung", "BaoPhe" };
            var cacNhom = thuTuTrangThai
                .Select(tt => (
                    Ten: tt switch { "TamDung" => "MÁY TẠM DỪNG", "BaoPhe" => "MÁY BÁO PHẾ", _ => "MÁY ĐANG HOẠT ĐỘNG" },
                    Ds: dsMayLoai.Where(x => x.May.TrangThai == tt)
                                 .OrderByDescending(x => x.Loai == "Màu").ToList()))
                .Where(n => n.Ds.Count > 0)
                .ToList();

            // Trạng thái lạ (dữ liệu cũ ghi khác ba giá trị chuẩn) vẫn phải xuất, gom vào nhóm cuối
            var dsLac = dsMayLoai.Where(x => !thuTuTrangThai.Contains(x.May.TrangThai))
                                 .OrderByDescending(x => x.Loai == "Màu").ToList();
            if (dsLac.Count > 0) cacNhom.Add(("TRẠNG THÁI KHÁC", dsLac));

            var r = 6;
            foreach (var (tenNhom, dsNhom) in cacNhom)
            {
                // Chỉ có đúng một nhóm thì khỏi chèn dòng nhãn cho đỡ rối
                if (cacNhom.Count > 1)
                {
                    ws.Cell(r, 1).Value = $"{tenNhom} ({dsNhom.Count})";
                    ws.Range(r, 1, r, cotCuoi).Merge().Style
                      .Font.SetBold()
                      .Fill.SetBackgroundColor(ClosedXML.Excel.XLColor.FromArgb(221, 235, 247))
                      .Alignment.SetHorizontal(ClosedXML.Excel.XLAlignmentHorizontalValues.Left);
                    r++;
                }

                foreach (var (m, loai) in dsNhom.Select(x => (x.May, x.Loai)))
                {
                    ws.Cell(r, 1).Value = m.BoPhan;
                    ws.Cell(r, 2).Value = m.Model;
                    ws.Cell(r, 3).Value = m.Serial;
                    ws.Cell(r, 4).Value = string.IsNullOrWhiteSpace(m.DiaChiIp) ? "Cắm USB" : m.DiaChiIp;
                    ws.Cell(r, 5).Value = string.IsNullOrWhiteSpace(m.TenHangDoi) ? m.ViTri : m.TenHangDoi;

                    ws.Cell(r, cotLoai).Value = loai;
                    var oLoai = ws.Cell(r, cotLoai).Style;
                    oLoai.Font.SetBold()
                         .Alignment.SetHorizontal(ClosedXML.Excel.XLAlignmentHorizontalValues.Center);
                    if (loai == "Màu")
                    {
                        oLoai.Fill.SetBackgroundColor(ClosedXML.Excel.XLColor.FromArgb(255, 230, 246));
                        oLoai.Font.SetFontColor(ClosedXML.Excel.XLColor.FromArgb(150, 20, 100));
                    }
                    else if (loai == "Đen trắng")
                    {
                        oLoai.Fill.SetBackgroundColor(ClosedXML.Excel.XLColor.FromArgb(235, 235, 235));
                        oLoai.Font.SetFontColor(ClosedXML.Excel.XLColor.FromArgb(50, 50, 50));
                    }
                    else
                    {
                        oLoai.Font.SetFontColor(ClosedXML.Excel.XLColor.FromArgb(120, 120, 120));
                    }

                    // Máy đã ngừng dùng: tháng nào không có số thì ghi thẳng trạng thái như file làm tay
                    var chuTrangThai = m.TrangThai switch
                    {
                        "TamDung" => "Tạm dừng",
                        "BaoPhe" => "Báo phế",
                        _ => null
                    };

                    for (var t = 1; t <= soThang; t++)
                    {
                        var cotDen = cotDauThang + (t - 1) * 2;
                        var cotMauT = cotDen + 1;

                        oTheoThang.TryGetValue((m.IdMayIn, t), out var o);

                        if (o.DenTrang.HasValue)
                        {
                            ws.Cell(r, cotDen).Value = o.DenTrang.Value;
                            // Con số là thứ người ta mở file ra để đọc nên luôn đậm và đậm màu;
                            // số suy từ counter tổng chỉ phân biệt bằng in nghiêng, không làm mờ đi.
                            ws.Cell(r, cotDen).Style.Font.SetBold()
                              .Font.SetFontColor(ClosedXML.Excel.XLColor.FromArgb(33, 33, 33));
                            if (o.SuyTuTong) ws.Cell(r, cotDen).Style.Font.SetItalic();
                        }
                        else if (chuTrangThai != null)
                        {
                            ws.Cell(r, cotDen).Value = chuTrangThai;
                            ws.Cell(r, cotDen).Style.Font.SetItalic()
                              .Font.SetFontColor(ClosedXML.Excel.XLColor.FromArgb(110, 110, 110))
                              .Alignment.SetHorizontal(ClosedXML.Excel.XLAlignmentHorizontalValues.Center);
                        }

                        // Cột màu để TRỐNG hẳn khi máy không tách được màu, khác với máy tách được mà in 0 tờ
                        if (o.Mau.HasValue)
                        {
                            ws.Cell(r, cotMauT).Value = o.Mau.Value;
                            ws.Cell(r, cotMauT).Style.Font.SetBold()
                              .Font.SetFontColor(ClosedXML.Excel.XLColor.FromArgb(150, 20, 100));
                        }
                        else if (loai == "Màu" && o.DenTrang.HasValue)
                        {
                            // Máy IT khai là máy màu nhưng chưa đọc được đồng hồ tách màu: đánh dấu
                            // "?" để không bị đọc nhầm thành máy đen trắng, số tổng nằm ở cột bên trái.
                            ws.Cell(r, cotMauT).Value = "?";
                            ws.Cell(r, cotMauT).Style.Font.SetBold()
                              .Font.SetFontColor(ClosedXML.Excel.XLColor.FromArgb(150, 20, 100))
                              .Alignment.SetHorizontal(ClosedXML.Excel.XLAlignmentHorizontalValues.Center);
                        }
                    }

                    r++;
                }
            }

            if (r > 6)
            {
                var vung = ws.Range(4, 1, r - 1, cotCuoi);
                vung.Style.Border.SetOutsideBorder(ClosedXML.Excel.XLBorderStyleValues.Thin);
                vung.Style.Border.SetInsideBorder(ClosedXML.Excel.XLBorderStyleValues.Thin);
                ws.Range(6, cotDauThang, r - 1, cotCuoi).Style.NumberFormat.SetFormat("#,##0");
                ws.Range(6, cotDauThang, r - 1, cotCuoi).Style.Alignment
                  .SetHorizontal(ClosedXML.Excel.XLAlignmentHorizontalValues.Right);
                // Giữ nguyên 5 cột nhận diện máy khi kéo ngang qua 12 tháng
                ws.SheetView.Freeze(5, 5);
            }
            else
            {
                ws.Cell(6, 1).Value = "Không có máy nào khớp bộ lọc.";
            }

            ws.Columns(1, cotCuoi).AdjustToContents();
            ws.Column(5).Width = 28;

            using var luong = new MemoryStream();
            wb.SaveAs(luong);

            return File(luong.ToArray(),
                "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                $"BangTheoDoiTrangIn_{namXuat}.xlsx");
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
                    c.CounterInMau, c.CounterInDenTrang,
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

                // Số tờ màu / đen trắng trong kỳ tính y hệt tổng: hiệu hai mốc chốt. Kỳ nào thiếu
                // một trong hai mốc (máy cũ chưa lưu tách màu) thì để null chứ không suy ra từ tổng.
                int? ChenhKy(int? nay, int? truoc)
                {
                    if (nay is null || truoc is null) return null;
                    var d = nay.Value - truoc.Value;
                    return d < 0 ? null : d;
                }

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
                    trangMauTrongKy = ChenhKy(ky.CounterInMau, truocKy?.CounterInMau),
                    trangDenTrangTrongKy = ChenhKy(ky.CounterInDenTrang, truocKy?.CounterInDenTrang),
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

            // Số tờ màu / đen trắng trong N ngày. Chỉ tính khi CẢ hai mốc đều có số tách màu —
            // máy đọc qua CentreWare cũ / PJL không trả về hai chỉ số này nên phải để null,
            // không được suy ngược từ tổng.
            var moiNhatMau = lichSu.LastOrDefault(c => c.CounterInMau != null || c.CounterInDenTrang != null);

            int? ChenhMau(int soNgayLui, bool laMau)
            {
                // lichSu đã sắp xếp tăng dần theo ngày nên phần tử cuối là mốc mới nhất
                var dsCo = lichSu.Where(c => (laMau ? c.CounterInMau : c.CounterInDenTrang) != null).ToList();
                if (dsCo.Count < 2) return null;

                var mocNay = dsCo[^1];
                var mocTruoc = dsCo.Where(c => c.NgayChot <= homNay.AddDays(-soNgayLui))
                                   .OrderByDescending(c => c.NgayChot)
                                   .FirstOrDefault()
                              ?? dsCo[0];

                if (mocTruoc.NgayChot == mocNay.NgayChot) return null;

                var nay = laMau ? mocNay.CounterInMau : mocNay.CounterInDenTrang;
                var truoc = laMau ? mocTruoc.CounterInMau : mocTruoc.CounterInDenTrang;
                if (nay is null || truoc is null) return null;

                var d = nay.Value - truoc.Value;
                return d < 0 ? null : d;
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
                    // Đồng hồ tách màu mới nhất + số tờ màu/đen trắng trong 30 ngày.
                    // Máy không tách được màu thì cả bốn giá trị này là null, giao diện tự ẩn.
                    counterInMau = moiNhatMau?.CounterInMau,
                    counterInDenTrang = moiNhatMau?.CounterInDenTrang,
                    trangMau30Ngay = ChenhMau(30, true),
                    trangDenTrang30Ngay = ChenhMau(30, false),
                    trungBinhMoiNgay = tongNgayKhoang > 0 ? Math.Round((double)tongTrangKhoang / tongNgayKhoang, 1) : (double?)null,
                    soLanDoc = lichSu.Count,
                    soNgayTheoDoi = soNgay
                },

                lichSu = lichSu.Select(c => new
                {
                    ngay = c.NgayChot.ToString("dd/MM/yyyy"),
                    c.CounterIn, c.CounterCopy, c.CounterScan, c.CounterTong,
                    c.CounterInMau, c.CounterInDenTrang,
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

        /// <summary>
        /// Kéo Job History của máy về bảng lịch sử in ngay (job nền cũng kéo mỗi chu kỳ đọc chỉ số).
        /// Chạy lại vô hại: dòng đã có bị bỏ qua nhờ khoá chống trùng.
        /// </summary>
        [HttpPost("/QLMayIn/DongBoLichSuIn/{id:int}")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DongBoLichSuIn(int id)
        {
            var chan = KiemQuyen();
            if (chan != null) return chan;

            var may = await _context.MayIns.AsNoTracking().FirstOrDefaultAsync(m => m.IdMayIn == id);
            if (may is null) return Json(new { thanhCong = false, thongBao = "Không tìm thấy máy in" });

            try
            {
                var ketQua = await _mayInLichSuInService.DongBoAsync(may, HttpContext.RequestAborted);
                return Json(new
                {
                    thanhCong = ketQua.ThanhCong,
                    thongBao = ketQua.ThongBao,
                    duLieu = new { ketQua.CoJobHistory, ketQua.SoDocDuoc, ketQua.SoThemMoi }
                });
            }
            catch (Exception ex) when (ex is DbUpdateException or Microsoft.Data.SqlClient.SqlException)
            {
                return Json(new { thanhCong = false, thongBao = "Lỗi ghi lịch sử in: " + ex.GetBaseException().Message });
            }
        }

        /// <summary>
        /// Lịch sử lệnh in đã đồng bộ của một máy trong khoảng ngày (mặc định 7 ngày gần nhất),
        /// kèm tổng hợp theo người in. Owner trên máy in là tài khoản Windows nên ghép được họ tên từ User.TK.
        /// </summary>
        [HttpGet("/QLMayIn/LichSuIn/{id:int}")]
        public async Task<IActionResult> LichSuIn(int id, DateTime? tuNgay, DateTime? denNgay, string? nguoiIn)
        {
            var chan = KiemQuyen();
            if (chan != null) return chan;

            var den = (denNgay ?? DateTime.Today).Date.AddDays(1);
            var tu = (tuNgay ?? DateTime.Today.AddDays(-6)).Date;
            if (tu >= den) return Json(new { thanhCong = false, thongBao = "Từ ngày phải trước đến ngày" });
            if ((den - tu).TotalDays > 366) return Json(new { thanhCong = false, thongBao = "Chỉ xem tối đa một năm mỗi lần" });

            try
            {
                var truyVan = _context.MayInLichSuIns.AsNoTracking()
                    .Where(x => x.IdMayIn == id && x.ThoiDiemIn >= tu && x.ThoiDiemIn < den);
                if (!string.IsNullOrWhiteSpace(nguoiIn)) truyVan = truyVan.Where(x => x.NguoiIn == nguoiIn.Trim());

                var tongHop = await truyVan
                    .GroupBy(x => x.NguoiIn)
                    .Select(g => new
                    {
                        nguoiIn = g.Key,
                        soLenh = g.Count(),
                        soTrang = g.Sum(x => x.SoTrang ?? 0),
                        lanCuoi = g.Max(x => x.ThoiDiemIn)
                    })
                    .OrderByDescending(x => x.soTrang)
                    .ToListAsync();

                // Bảng chi tiết cắt ở 2000 dòng cho trang khỏi nặng; tổng hợp phía trên vẫn đủ
                const int toiDaDong = 2000;
                var dsLenh = await truyVan
                    .OrderByDescending(x => x.ThoiDiemIn).ThenByDescending(x => x.IdLichSuIn)
                    .Take(toiDaDong)
                    .Select(x => new
                    {
                        thoiDiem = x.ThoiDiemIn,
                        x.NguoiIn,
                        x.TenTaiLieu,
                        x.Loai,
                        x.TrangThai,
                        x.SoTrang,
                        x.CongNhan
                    })
                    .ToListAsync();

                var dsTk = tongHop.Where(x => x.nguoiIn != null).Select(x => x.nguoiIn!).ToList();
                var hoTenTheoTk = (await _context.Users.AsNoTracking()
                        .Where(u => u.Tk != null && dsTk.Contains(u.Tk))
                        .Select(u => new { u.Tk, u.HoTen })
                        .ToListAsync())
                    .GroupBy(u => u.Tk!, StringComparer.OrdinalIgnoreCase)
                    .ToDictionary(g => g.Key, g => g.First().HoTen, StringComparer.OrdinalIgnoreCase);

                string? HoTen(string? tk) => tk != null && hoTenTheoTk.TryGetValue(tk, out var ht) ? ht : null;

                var lanDongBoCuoi = await _context.MayInLichSuIns.AsNoTracking()
                    .Where(x => x.IdMayIn == id)
                    .MaxAsync(x => (DateTime?)x.NgayDongBo);

                return Json(new
                {
                    thanhCong = true,
                    thongBao = "",
                    duLieu = new
                    {
                        tuNgay = tu.ToString("yyyy-MM-dd"),
                        denNgay = den.AddDays(-1).ToString("yyyy-MM-dd"),
                        lanDongBoCuoi = lanDongBoCuoi?.ToString("dd/MM/yyyy HH:mm"),
                        biCat = dsLenh.Count >= toiDaDong,
                        tongHop = tongHop.Select(x => new { x.nguoiIn, hoTen = HoTen(x.nguoiIn), x.soLenh, x.soTrang, lanCuoi = x.lanCuoi.ToString("dd/MM/yyyy HH:mm") }),
                        dsLenh = dsLenh.Select(x => new
                        {
                            thoiDiem = x.thoiDiem.ToString("dd/MM/yyyy HH:mm"),
                            x.NguoiIn,
                            hoTen = HoTen(x.NguoiIn),
                            x.TenTaiLieu,
                            x.Loai,
                            x.TrangThai,
                            x.SoTrang,
                            x.CongNhan
                        })
                    }
                });
            }
            catch (Microsoft.Data.SqlClient.SqlException ex)
            {
                // Hay gặp nhất: chưa chạy script tạo bảng MayIn_LichSuIn trên DB
                return Json(new { thanhCong = false, thongBao = "Lỗi đọc lịch sử in: " + ex.Message });
            }
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
