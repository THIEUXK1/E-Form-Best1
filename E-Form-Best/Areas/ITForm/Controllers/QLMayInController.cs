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
                .Select(c => new { c.IdMayIn, c.NgayChot, c.CounterTong, c.CounterInMau, c.CounterInDenTrang,
                                   c.TonerPhanTram, c.DrumPhanTram, c.Nguon, c.VatTuJson })
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
                    .Select(c => new { c.IdMayIn, c.NgayChot, c.CounterTong, c.CounterInMau, c.CounterInDenTrang,
                                       c.TonerPhanTram, c.DrumPhanTram, c.Nguon, c.VatTuJson })
                    .ToListAsync();

                foreach (var nhom in dongCu.GroupBy(c => c.IdMayIn))
                    theoMay[nhom.Key] = nhom.OrderByDescending(x => x.NgayChot).ToList();
            }

            // Chỉ số đọc trong vòng ngần này ngày vẫn coi là "hiện tại"; job nền chốt mỗi ngày nên
            // mặc định cho lệch 1 ngày (buổi sáng có thể mới chỉ có số của hôm qua).
            var soNgayConTinCay = _configuration.GetValue<int?>("MayIn:SoNgayChiSoConTinCay") ?? 1;
            if (soNgayConTinCay < 0) soNgayConTinCay = 0;

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
                // ls sắp xếp giảm dần theo ngày nên phần tử đầu là mốc mới nhất.
                int? ChenhMau(int soNgayLui, bool laMau)
                {
                    var dsCo = ls?.Where(x => (laMau ? x.CounterInMau : x.CounterInDenTrang) != null).ToList();
                    if (dsCo is null || dsCo.Count < 2) return null;

                    var mocNay = dsCo[0];
                    var mocTruoc = dsCo.FirstOrDefault(x => x.NgayChot <= homNay.AddDays(-soNgayLui)) ?? dsCo[^1];
                    if (mocTruoc.NgayChot == mocNay.NgayChot) return null;

                    var nay = laMau ? mocNay.CounterInMau : mocNay.CounterInDenTrang;
                    var truoc = laMau ? mocTruoc.CounterInMau : mocTruoc.CounterInDenTrang;
                    if (nay is null || truoc is null) return null;

                    var d = nay.Value - truoc.Value;
                    return d < 0 ? null : d;
                }

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
                var color = mocTachMau?.CounterInMau != null
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
                    NgayChiSo = moiNhat?.NgayChot,
                    Nguon = moiNhat?.Nguon,
                    TrangInHomNay = Chenh(1),
                    TrangIn7Ngay = Chenh(7),
                    TrangIn30Ngay = Chenh(30),
                    TrangMau30Ngay = ChenhMau(30, true),
                    TrangDenTrang30Ngay = ChenhMau(30, false),
                    Toner = moiNhat?.TonerPhanTram,
                    Drum = moiNhat?.DrumPhanTram,
                    CanLayTrucTiep = lyDo.Count > 0,
                    LyDo = string.Join("; ", lyDo)
                };
            })
            // Máy lấy được chỉ số qua IP xếp trước; máy phải đọc tay dồn xuống cuối từng nhóm
            .OrderBy(x => x.CanLayTrucTiep)
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
                "Chỉ số hiện tại", "Chỉ số in màu", "Chỉ số in đen trắng",
                "Ngày đọc", "Nguồn", "Hôm nay", "7 ngày", "30 ngày",
                "Màu 30 ngày", "Đen trắng 30 ngày",
                "Mực (%)", "Trống (%)", "Cần lấy trực tiếp", "Lý do / ghi chú"
            };

            const int cotColor = 4;
            const int cotChiSo = 10;
            const int cotMau = 11;        // đồng hồ tích luỹ, có ngay từ lần chốt đầu tiên
            const int cotDenTrang = 12;
            const int cotLyDo = 23;

            ws.Cell(1, 1).Value = "BÁO CÁO CHỈ SỐ TRANG IN HIỆN TẠI";
            ws.Range(1, 1, 1, tieuDe.Length).Merge().Style.Font.SetBold().Font.SetFontSize(14)
              .Alignment.SetHorizontal(ClosedXML.Excel.XLAlignmentHorizontalValues.Center);

            ws.Cell(2, 1).Value = $"Xuất lúc {DateTime.Now:dd/MM/yyyy HH:mm} — {dong.Count} máy "
                               + $"(máy in màu: {soMau}, đen trắng: {soDenTrang}), "
                               + $"{soCanLayTay} máy cần lấy chỉ số trực tiếp tại máy. "
                               + "Cột \"Chỉ số hiện tại\": xanh = đọc được qua IP, "
                               + "cam = số cũ / nhập tay (chỉ tham khảo), xám = chưa có chỉ số. "
                               + $"Chỉ {soTachDuoc}/{dong.Count} máy báo được đồng hồ tách màu; "
                               + "hai cột \"… 30 ngày\" chỉ có số khi máy đã có từ 2 lần chốt trở lên.";
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
                    if (d.CounterInMau.HasValue) ws.Cell(r, cotMau).Value = d.CounterInMau.Value;
                    if (d.CounterInDenTrang.HasValue) ws.Cell(r, cotDenTrang).Value = d.CounterInDenTrang.Value;
                    if (d.NgayChiSo.HasValue) ws.Cell(r, 13).Value = d.NgayChiSo.Value.ToString("dd/MM/yyyy");
                    ws.Cell(r, 14).Value = d.Nguon;
                    if (d.TrangInHomNay.HasValue) ws.Cell(r, 15).Value = d.TrangInHomNay.Value;
                    if (d.TrangIn7Ngay.HasValue) ws.Cell(r, 16).Value = d.TrangIn7Ngay.Value;
                    if (d.TrangIn30Ngay.HasValue) ws.Cell(r, 17).Value = d.TrangIn30Ngay.Value;
                    if (d.TrangMau30Ngay.HasValue) ws.Cell(r, 18).Value = d.TrangMau30Ngay.Value;
                    if (d.TrangDenTrang30Ngay.HasValue) ws.Cell(r, 19).Value = d.TrangDenTrang30Ngay.Value;
                    if (d.Toner.HasValue) ws.Cell(r, 20).Value = d.Toner.Value;
                    if (d.Drum.HasValue) ws.Cell(r, 21).Value = d.Drum.Value;

                    ws.Cell(r, 22).Value = d.CanLayTrucTiep ? "X" : "";
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
                ws.Range(5, cotChiSo, r - 1, 21).Style.NumberFormat.SetFormat("#,##0");
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
