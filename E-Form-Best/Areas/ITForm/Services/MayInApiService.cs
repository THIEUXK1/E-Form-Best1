using System.Net.Sockets;
using System.Text;
using System.Text.RegularExpressions;
using System.Text.Json;
using E_Form_Best.Context;
using E_Form_Best.Models.ITForm;
using Microsoft.EntityFrameworkCore;

namespace E_Form_Best.Areas.ITForm.Services
{
    /// <summary>
    /// Đọc chỉ số máy in qua API web của FUJIFILM Apeos (không cần đăng nhập, chỉ GET):
    ///   /home/api/about           → tên máy, serial, trạng thái thiết bị
    ///   /home/api/billing-counter → đồng hồ đếm in / copy / scan
    ///   /home/api/supplies-info   → % mực (TONER_K) và trống (DRUM_K)
    ///
    /// Dòng Fuji Xerox đời cũ (DocuPrint 3105/3205, ApeosPort-V/VI/VII) không có các endpoint này
    /// và SNMP cũng bị tắt, nhưng trang CentreWare vẫn nhúng đồng hồ đếm trong biến JavaScript nên
    /// vẫn đọc được — xem DocQuaWebCuAsync. Nhóm này không có % mực.
    /// </summary>
    public class MayInApiService
    {
        public const string TenHttpClient = "MayIn";

        private readonly IHttpClientFactory _httpClientFactory;
        private readonly ITFormContext _context;

        public MayInApiService(IHttpClientFactory httpClientFactory, ITFormContext context)
        {
            _httpClientFactory = httpClientFactory;
            _context = context;
        }

        /// <summary>Một vật tư đo được phần trăm còn lại, ví dụ TONER_C 37%.</summary>
        public record VatTuDoc(string Ten, int PhanTram);

        /// <summary>Kết quả một lần đọc; ThanhCong = false thì chỉ có ThongBao.</summary>
        public class KetQuaDoc
        {
            public bool ThanhCong { get; set; }
            public string ThongBao { get; set; } = "";
            public int? CounterIn { get; set; }
            public int? CounterCopy { get; set; }
            public int? CounterScan { get; set; }
            public int? CounterTong { get; set; }
            /// <summary>Mực thấp nhất trong các TONER_* — máy đen trắng thì chính là TONER_K.</summary>
            public int? TonerPhanTram { get; set; }

            /// <summary>Trống thấp nhất trong các DRUM_*.</summary>
            public int? DrumPhanTram { get; set; }

            /// <summary>Toàn bộ vật tư đo được %, giữ nguyên thứ tự máy trả về.</summary>
            public List<VatTuDoc> VatTu { get; } = new();
            public string? TrangThaiThietBi { get; set; }
            public string? TenMay { get; set; }
            public string? SerialDocDuoc { get; set; }

            /// <summary>API (máy đời mới) hoặc WebCu (CentreWare) — ghi vào cột nguon của bảng chỉ số.</summary>
            public string NguonDoc { get; set; } = "API";
        }

        public async Task<KetQuaDoc> DocChiSoAsync(string diaChiIp, CancellationToken ct = default)
        {
            var ketQua = new KetQuaDoc();

            if (string.IsNullOrWhiteSpace(diaChiIp))
            {
                ketQua.ThongBao = "Máy in chưa có địa chỉ IP";
                return ketQua;
            }

            var client = _httpClientFactory.CreateClient(TenHttpClient);

            try
            {
                // Máy dùng chứng thư tự ký nên phải đi HTTPS với handler bỏ kiểm chứng thư
                // (cấu hình ở Program.cs). Cổng 80 cũng mở nhưng chuyển hướng sang HTTPS.
                // Phần lớn máy Apeos đời mới ép HTTPS, nhưng có máy (Apeos C3570 ở 10.0.59.128) chỉ
                // mở API trên cổng 80 — thử HTTPS trước rồi mới hạ xuống HTTP.
                var goc = $"https://{diaChiIp}";
                var counter = await LayJsonAsync(client, $"{goc}/home/api/billing-counter", ct);

                if (counter is null)
                {
                    goc = $"http://{diaChiIp}";
                    counter = await LayJsonAsync(client, $"{goc}/home/api/billing-counter", ct);
                }

                if (counter is null)
                {
                    // Máy đời cũ chạy CentreWare: không có /home/api nhưng trang trạng thái vẫn có đồng hồ.
                    // Đa số mở cổng 80, một số máy chỉ bật HTTPS nên phải thử cả hai.
                    var theoWebCu = await DocQuaWebCuAsync(client, $"http://{diaChiIp}", ct)
                                    ?? await DocQuaWebCuAsync(client, $"https://{diaChiIp}", ct);
                    if (theoWebCu is not null) return theoWebCu;

                    // Cuối cùng thử PJL cổng 9100 — đường duy nhất còn lại của vài dòng máy nhỏ
                    var theoPjl = await DocQuaPjlAsync(diaChiIp, ct);
                    if (theoPjl is not null) return theoPjl;

                    ketQua.ThongBao = "Không đọc được đồng hồ đếm (không có /home/api, trang CentreWare lẫn PJL)";
                    return ketQua;
                }

                if (counter.Value.TryGetProperty("UsageCounters", out var dsCounter)
                    && dsCounter.ValueKind == JsonValueKind.Array)
                {
                    foreach (var muc in dsCounter.EnumerateArray())
                    {
                        var ten = muc.TryGetProperty("DomesticName", out var t) ? t.GetString() ?? "" : "";
                        if (!muc.TryGetProperty("Count", out var c) || c.ValueKind != JsonValueKind.Number) continue;
                        var soLuong = c.GetInt32();

                        // Chỉ lấy dòng TOTAL_IMPRESSION; các dòng COLOR/BW là thành phần con của nó,
                        // cộng thêm là đếm trùng.
                        switch (ten)
                        {
                            case "PRINT_TOTAL_IMPRESSION": ketQua.CounterIn = soLuong; break;
                            case "COPY_TOTAL_IMPRESSION": ketQua.CounterCopy = soLuong; break;
                            case "SCAN_TOTAL_IMPRESSION": ketQua.CounterScan = soLuong; break;
                        }
                    }
                }

                if (ketQua.CounterIn is null && ketQua.CounterCopy is null)
                {
                    ketQua.ThongBao = "Máy trả về dữ liệu nhưng không có đồng hồ đếm in/copy";
                    return ketQua;
                }

                // Tổng = in + copy, đúng cách tính cột "trang in" của bảng theo dõi Excel cũ
                ketQua.CounterTong = (ketQua.CounterIn ?? 0) + (ketQua.CounterCopy ?? 0);

                var vatTu = await LayJsonAsync(client, $"{goc}/home/api/supplies-info", ct);
                if (vatTu is not null
                    && vatTu.Value.TryGetProperty("Supplies", out var dsVatTu)
                    && dsVatTu.ValueKind == JsonValueKind.Array)
                {
                    foreach (var muc in dsVatTu.EnumerateArray())
                    {
                        var ten = muc.TryGetProperty("Name", out var n) ? n.GetString() ?? "" : "";
                        // "Remaining" là chuỗi phần trăm; vật tư không đo được (FUSER, BTR) không có khoá này
                        if (!muc.TryGetProperty("Remaining", out var r)) continue;
                        if (!int.TryParse(r.GetString(), out var phanTram)) continue;
                        if (ten.Length == 0) continue;

                        ketQua.VatTu.Add(new VatTuDoc(ten, phanTram));

                        // Máy màu có TONER_C/M/Y/K: lấy màu thấp nhất làm con số cảnh báo, vì hết
                        // bất kỳ màu nào là máy đã ngừng in màu, không đợi tới lúc mực đen cạn.
                        if (ten.StartsWith("TONER", StringComparison.OrdinalIgnoreCase))
                            ketQua.TonerPhanTram = Math.Min(ketQua.TonerPhanTram ?? int.MaxValue, phanTram);
                        else if (ten.StartsWith("DRUM", StringComparison.OrdinalIgnoreCase))
                            ketQua.DrumPhanTram = Math.Min(ketQua.DrumPhanTram ?? int.MaxValue, phanTram);
                    }
                }

                var about = await LayJsonAsync(client, $"{goc}/home/api/about", ct);
                if (about is not null)
                {
                    ketQua.TrangThaiThietBi = about.Value.TryGetProperty("DeviceStatus", out var ds) ? ds.GetString() : null;
                    ketQua.TenMay = about.Value.TryGetProperty("DevFrndlName", out var dn) ? dn.GetString() : null;
                    ketQua.SerialDocDuoc = about.Value.TryGetProperty("SerialNumber", out var sn) ? sn.GetString() : null;
                }

                ketQua.ThanhCong = true;
                ketQua.ThongBao = "Đọc thành công";
                return ketQua;
            }
            catch (TaskCanceledException)
            {
                ketQua.ThongBao = "Hết thời gian chờ — máy không phản hồi";
                return ketQua;
            }
            catch (HttpRequestException ex)
            {
                ketQua.ThongBao = "Không kết nối được: " + ex.Message;
                return ketQua;
            }
            catch (JsonException)
            {
                ketQua.ThongBao = "Máy trả về dữ liệu không phải JSON (máy đời cũ)";
                return ketQua;
            }
        }

        /// <summary>
        /// Đọc một máy rồi ghi chỉ số. Mỗi máy mỗi ngày giữ đúng một dòng — gọi lại nhiều lần
        /// trong ngày chỉ cập nhật dòng đó, không nhân bản dữ liệu.
        /// </summary>
        public async Task<KetQuaDoc> DocVaLuuAsync(MayIn mayIn, CancellationToken ct = default)
        {
            var ketQua = await DocChiSoAsync(mayIn.DiaChiIp ?? "", ct);

            mayIn.LanDocCuoi = DateTime.Now;
            mayIn.KetQuaDocCuoi = ketQua.ThongBao.Length > 255 ? ketQua.ThongBao[..255] : ketQua.ThongBao;
            mayIn.NgayCapNhat = DateTime.Now;

            if (ketQua.ThanhCong)
            {
                var homNay = DateOnly.FromDateTime(DateTime.Now);
                var chiSo = await _context.MayInChiSos
                    .FirstOrDefaultAsync(x => x.IdMayIn == mayIn.IdMayIn && x.NgayChot == homNay, ct);

                if (chiSo is null)
                {
                    chiSo = new MayInChiSo { IdMayIn = mayIn.IdMayIn, NgayChot = homNay, Nguon = ketQua.NguonDoc };
                    _context.MayInChiSos.Add(chiSo);
                }

                chiSo.ThoiDiemDoc = DateTime.Now;
                chiSo.CounterIn = ketQua.CounterIn;
                chiSo.CounterCopy = ketQua.CounterCopy;
                chiSo.CounterScan = ketQua.CounterScan;
                chiSo.CounterTong = ketQua.CounterTong;
                chiSo.TonerPhanTram = ketQua.TonerPhanTram;
                chiSo.DrumPhanTram = ketQua.DrumPhanTram;
                chiSo.VatTuJson = GoiVatTuJson(ketQua.VatTu);
                chiSo.TrangThaiThietBi = ketQua.TrangThaiThietBi;
                chiSo.Nguon = ketQua.NguonDoc;
            }

            await _context.SaveChangesAsync(ct);
            return ketQua;
        }

        /// <summary>Kết quả nạp lịch sử quá khứ từ nhật ký lỗi của máy.</summary>
        public class KetQuaNapLichSu
        {
            public bool ThanhCong { get; set; }
            public string ThongBao { get; set; } = "";
            public int SoMocDocDuoc { get; set; }
            public int SoNgayThemMoi { get; set; }
            public DateOnly? NgayCuNhat { get; set; }
            public DateOnly? NgayMoiNhat { get; set; }
        }

        /// <summary>
        /// Nạp chỉ số QUÁ KHỨ từ nhật ký lỗi của máy (/home/api/faulthistory). Mỗi lần máy báo lỗi
        /// (kẹt giấy, hết giấy, mở nắp...) nó ghi lại thời điểm kèm số trang đã in lúc đó, giữ được
        /// 40 mốc gần nhất — tức là một chuỗi chỉ số theo thời gian có sẵn trong máy.
        ///
        /// Độ sâu tuỳ máy: máy ít lỗi giữ được cả năm, máy hay kẹt giấy thì 40 mốc chỉ trong một ngày.
        ///
        /// Chỉ THÊM ngày còn trống, không ghi đè dòng đã có (đọc trực tiếp / Excel / nhập tay luôn
        /// đáng tin hơn số suy ra từ nhật ký lỗi). Ngày có nhiều mốc thì lấy mốc muộn nhất trong ngày.
        /// </summary>
        public async Task<KetQuaNapLichSu> NapLichSuTuMayAsync(MayIn mayIn, CancellationToken ct = default)
        {
            var ketQua = new KetQuaNapLichSu();

            if (string.IsNullOrWhiteSpace(mayIn.DiaChiIp))
            {
                ketQua.ThongBao = "Máy in chưa có địa chỉ IP";
                return ketQua;
            }

            var client = _httpClientFactory.CreateClient(TenHttpClient);
            JsonElement? nhatKy;

            try
            {
                // Thử HTTPS trước, máy nào chỉ mở API trên cổng 80 thì hạ xuống HTTP
                nhatKy = await LayJsonAsync(client, $"https://{mayIn.DiaChiIp}/home/api/faulthistory", ct)
                         ?? await LayJsonAsync(client, $"http://{mayIn.DiaChiIp}/home/api/faulthistory", ct);
            }
            catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or JsonException)
            {
                ketQua.ThongBao = "Không đọc được nhật ký lỗi: " + ex.Message;
                return ketQua;
            }

            if (nhatKy is null
                || !nhatKy.Value.TryGetProperty("FaultHistory", out var ds)
                || ds.ValueKind != JsonValueKind.Array)
            {
                ketQua.ThongBao = "Máy không có nhật ký lỗi (dòng đời cũ chạy CentreWare không hỗ trợ)";
                return ketQua;
            }

            // Gom theo ngày, giữ mốc muộn nhất trong ngày vì đó là số trang cuối ngày hôm đó
            var theoNgay = new Dictionary<DateOnly, (DateTime ThoiDiem, int Volume)>();

            foreach (var muc in ds.EnumerateArray())
            {
                if (!muc.TryGetProperty("Volume", out var v) || v.ValueKind != JsonValueKind.Number) continue;
                var volume = v.GetInt32();
                if (volume <= 0) continue;

                var nam = LaySo(muc, "Year");
                var thang = LaySo(muc, "Month");
                var ngay = LaySo(muc, "Day");
                if (nam is null or < 2000 || thang is null or < 1 or > 12 || ngay is null or < 1 or > 31) continue;

                DateTime thoiDiem;
                try
                {
                    thoiDiem = new DateTime(nam.Value, thang.Value, ngay.Value,
                        Math.Clamp(LaySo(muc, "Hour") ?? 0, 0, 23),
                        Math.Clamp(LaySo(muc, "Minute") ?? 0, 0, 59), 0);
                }
                catch (ArgumentOutOfRangeException) { continue; }

                if (thoiDiem > DateTime.Now) continue;

                var khoa = DateOnly.FromDateTime(thoiDiem);
                if (!theoNgay.TryGetValue(khoa, out var dangCo) || thoiDiem > dangCo.ThoiDiem)
                    theoNgay[khoa] = (thoiDiem, volume);
            }

            ketQua.SoMocDocDuoc = theoNgay.Count;

            if (theoNgay.Count == 0)
            {
                ketQua.ThanhCong = true;
                ketQua.ThongBao = "Máy chưa ghi mốc lỗi nào nên không có lịch sử để nạp";
                return ketQua;
            }

            var dsNgay = theoNgay.Keys.ToList();
            var daCo = await _context.MayInChiSos
                .Where(c => c.IdMayIn == mayIn.IdMayIn && dsNgay.Contains(c.NgayChot))
                .Select(c => c.NgayChot)
                .ToListAsync(ct);

            var boDaCo = daCo.ToHashSet();

            foreach (var (ngayChot, moc) in theoNgay)
            {
                if (boDaCo.Contains(ngayChot)) continue;

                _context.MayInChiSos.Add(new MayInChiSo
                {
                    IdMayIn = mayIn.IdMayIn,
                    NgayChot = ngayChot,
                    ThoiDiemDoc = moc.ThoiDiem,
                    CounterTong = moc.Volume,
                    Nguon = "LichSuLoi",
                    GhiChu = "Suy ra từ nhật ký lỗi của máy, không phải số đọc trực tiếp"
                });

                ketQua.SoNgayThemMoi++;
            }

            if (ketQua.SoNgayThemMoi > 0) await _context.SaveChangesAsync(ct);

            ketQua.ThanhCong = true;
            ketQua.NgayCuNhat = theoNgay.Keys.Min();
            ketQua.NgayMoiNhat = theoNgay.Keys.Max();
            ketQua.ThongBao = ketQua.SoNgayThemMoi == 0
                ? $"Máy có {theoNgay.Count} ngày trong nhật ký lỗi, tất cả đã có sẵn trong lịch sử"
                : $"Đã thêm {ketQua.SoNgayThemMoi} ngày từ nhật ký lỗi "
                  + $"({ketQua.NgayCuNhat:dd/MM/yyyy} → {ketQua.NgayMoiNhat:dd/MM/yyyy})";

            return ketQua;
        }

        private static int? LaySo(JsonElement goc, string khoa)
            => goc.TryGetProperty(khoa, out var v) && v.ValueKind == JsonValueKind.Number ? v.GetInt32() : null;

        /// <summary>
        /// Gói danh sách vật tư thành JSON để lưu một cột. Cột chỉ có 1000 ký tự nên máy nào báo
        /// quá nhiều vật tư thì cắt bớt, ưu tiên giữ mực và trống (thứ duy nhất cần theo dõi).
        /// </summary>
        private static string? GoiVatTuJson(IReadOnlyCollection<VatTuDoc> vatTu)
        {
            if (vatTu.Count == 0) return null;

            var json = JsonSerializer.Serialize(vatTu.Select(v => new { ten = v.Ten, phanTram = v.PhanTram }));
            if (json.Length <= 1000) return json;

            var rutGon = vatTu
                .Where(v => v.Ten.StartsWith("TONER", StringComparison.OrdinalIgnoreCase)
                            || v.Ten.StartsWith("DRUM", StringComparison.OrdinalIgnoreCase))
                .Take(12)
                .Select(v => new { ten = v.Ten, phanTram = v.PhanTram });

            var jsonRutGon = JsonSerializer.Serialize(rutGon);
            return jsonRutGon.Length <= 1000 ? jsonRutGon : null;
        }


        /// <summary>
        /// Đường đọc thứ hai cho máy Fuji Xerox đời cũ (DocuPrint, ApeosPort-V/VI/VII) chạy
        /// CentreWare Internet Services: không có /home/api và SNMP đã tắt, nhưng hai trang
        /// trạng thái vẫn mở và nhúng số liệu ngay trong biến JavaScript:
        ///
        ///   http://ip/prcnt.htm → var info=['Total Printed Impressions',96537,'Total Copied Impressions',...]
        ///   http://ip/stgen.htm → var spcs=['DocuPrint 3205 d',[...],'','Machine in Power Saver Mode','i']
        ///
        /// Trang vật tư (stsply.htm) để mảng dữ liệu rỗng nên nhóm máy này không có % mực.
        /// </summary>
        private static async Task<KetQuaDoc?> DocQuaWebCuAsync(HttpClient client, string goc, CancellationToken ct)
        {
            var trangCounter = await LayVanBanAsync(client, $"{goc}/prcnt.htm", ct);
            if (trangCounter is null) return null;

            var khoiInfo = Regex.Match(trangCounter, @"var\s+info\s*=\s*\[(?<noiDung>.*?)\]", RegexOptions.Singleline);
            if (!khoiInfo.Success) return null;

            var ketQua = new KetQuaDoc();

            // Các cặp 'Nhãn',<số> nằm liên tiếp trong cùng một mảng
            foreach (Match cap in Regex.Matches(khoiInfo.Groups["noiDung"].Value, @"'(?<nhan>[^']+)'\s*,\s*(?<so>\d+)"))
            {
                if (!int.TryParse(cap.Groups["so"].Value, out var so)) continue;

                switch (cap.Groups["nhan"].Value)
                {
                    case "Total Printed Impressions": ketQua.CounterIn = so; break;
                    case "Total Copied Impressions": ketQua.CounterCopy = so; break;
                    case "Total Scanned Images": ketQua.CounterScan = so; break;
                }
            }

            if (ketQua.CounterIn is null && ketQua.CounterCopy is null) return null;

            ketQua.CounterTong = (ketQua.CounterIn ?? 0) + (ketQua.CounterCopy ?? 0);

            var trangTongQuan = await LayVanBanAsync(client, $"{goc}/stgen.htm", ct);
            if (trangTongQuan is not null)
            {
                var spcs = Regex.Match(trangTongQuan, @"var\s+spcs\s*=\s*\['(?<model>[^']*)'(?<conLai>.*?)\];", RegexOptions.Singleline);
                if (spcs.Success)
                {
                    ketQua.TenMay = spcs.Groups["model"].Value;

                    // Chuỗi mô tả trạng thái là đoạn văn bản dài nhất còn lại trong mảng
                    // ("Machine in Power Saver Mode", "Ready"...), vị trí thay đổi theo đời máy.
                    var moTa = Regex.Matches(spcs.Groups["conLai"].Value, @"'(?<gt>[^']{4,})'")
                                    .Select(x => x.Groups["gt"].Value)
                                    .LastOrDefault();
                    ketQua.TrangThaiThietBi = moTa;
                }
            }

            ketQua.ThanhCong = true;
            ketQua.NguonDoc = "WebCu";
            ketQua.ThongBao = "Đọc thành công qua web đời cũ (CentreWare)";
            return ketQua;
        }


        /// <summary>
        /// Đường đọc thứ ba: PJL qua cổng in thô 9100. Dùng cho máy không có API lẫn trang
        /// CentreWare (ví dụ ApeosPrint C325/328 dw). Máy trả về:
        ///   @PJL INFO PAGECOUNT → PAGECOUNT=5137
        ///   @PJL INFO ID        → "FUJIFILM ApeosPrint C325/328 dw"
        ///   @PJL INFO STATUS    → DISPLAY="Ready"
        ///
        /// PJL chỉ có số trang in tổng, không tách copy/scan và không có % mực.
        /// Không phải máy nào cũng bật PJL: có máy nhận lệnh rồi im (chỉ vọng lại dòng lệnh).
        /// </summary>
        private static async Task<KetQuaDoc?> DocQuaPjlAsync(string diaChiIp, CancellationToken ct)
        {
            var traLoi = await GuiLenhPjlAsync(diaChiIp, "INFO PAGECOUNT", ct);
            if (traLoi is null) return null;

            var soTrang = Regex.Match(traLoi, @"PAGECOUNT\s*=\s*(?<so>\d+)");
            if (!soTrang.Success || !int.TryParse(soTrang.Groups["so"].Value, out var trang)) return null;

            var ketQua = new KetQuaDoc
            {
                CounterIn = trang,
                CounterTong = trang,
                ThanhCong = true,
                NguonDoc = "PJL",
                ThongBao = "Đọc thành công qua PJL cổng 9100"
            };

            var dinhDanh = await GuiLenhPjlAsync(diaChiIp, "INFO ID", ct);
            if (dinhDanh is not null)
            {
                var ten = Regex.Match(dinhDanh, "\"(?<ten>[^\"]+)\"");
                if (ten.Success) ketQua.TenMay = ten.Groups["ten"].Value;
            }

            var trangThai = await GuiLenhPjlAsync(diaChiIp, "INFO STATUS", ct);
            if (trangThai is not null)
            {
                var hienThi = Regex.Match(trangThai, "DISPLAY\\s*=\\s*\"(?<gt>[^\"]+)\"");
                if (hienThi.Success) ketQua.TrangThaiThietBi = hienThi.Groups["gt"].Value;
            }

            return ketQua;
        }

        private static async Task<string?> GuiLenhPjlAsync(string diaChiIp, string lenh, CancellationToken ct)
        {
            const char kyTuThoat = '\u001b';

            try
            {
                using var tcp = new TcpClient();
                using var hetGio = CancellationTokenSource.CreateLinkedTokenSource(ct);
                hetGio.CancelAfter(TimeSpan.FromSeconds(4));

                await tcp.ConnectAsync(diaChiIp, 9100, hetGio.Token);

                await using var luong = tcp.GetStream();
                var goi = Encoding.ASCII.GetBytes($"{kyTuThoat}%-12345X@PJL {lenh}\r\n{kyTuThoat}%-12345X");
                await luong.WriteAsync(goi, hetGio.Token);
                await luong.FlushAsync(hetGio.Token);

                var bo = new byte[2048];
                var soByte = await luong.ReadAsync(bo, hetGio.Token);
                return soByte <= 0 ? null : Encoding.ASCII.GetString(bo, 0, soByte);
            }
            catch (Exception)
            {
                // Máy không bật PJL, cổng bị chặn, hoặc đang bận in — đều coi như không đọc được
                return null;
            }
        }
        private static async Task<string?> LayVanBanAsync(HttpClient client, string url, CancellationToken ct)
        {
            try
            {
                using var phanHoi = await client.GetAsync(url, ct);
                if (!phanHoi.IsSuccessStatusCode) return null;

                var noiDung = await phanHoi.Content.ReadAsStringAsync(ct);
                return string.IsNullOrWhiteSpace(noiDung) ? null : noiDung;
            }
            catch (Exception)
            {
                return null;
            }
        }
        /// <summary>
        /// Gọi một endpoint JSON, KHÔNG ném lỗi kết nối ra ngoài mà trả null.
        ///
        /// Đây là chỗ từng làm hỏng cả nhóm máy đời cũ: máy DocuPrint/ApeosPort không mở cổng 443
        /// nên lệnh gọi HTTPS đầu tiên ném HttpRequestException (từ chối / reset) hoặc treo tới hết
        /// thời gian chờ. Lỗi đó thoát thẳng ra khối try ngoài cùng và hàm trả về "Không kết nối
        /// được" ngay, nên hai đường dự phòng CentreWare và PJL không bao giờ được chạy tới.
        ///
        /// Riêng việc người dùng đóng trang (ct bị huỷ) vẫn phải ném ra để dừng hẳn, không nuốt.
        /// </summary>
        private static async Task<JsonElement?> LayJsonAsync(HttpClient client, string url, CancellationToken ct)
        {
            try
            {
                using var phanHoi = await client.GetAsync(url, ct);
                if (!phanHoi.IsSuccessStatusCode) return null;

                var noiDung = await phanHoi.Content.ReadAsStringAsync(ct);
                // Máy đời cũ trả về trang HTML "FAILED" với mã 200 — không phải JSON thì bỏ qua
                if (string.IsNullOrWhiteSpace(noiDung) || noiDung.TrimStart().StartsWith('<')) return null;

                using var tep = JsonDocument.Parse(noiDung);
                return tep.RootElement.Clone();
            }
            catch (HttpRequestException)
            {
                return null;
            }
            catch (TaskCanceledException) when (!ct.IsCancellationRequested)
            {
                // Hết thời gian chờ của riêng request này, không phải người dùng huỷ
                return null;
            }
            catch (JsonException)
            {
                return null;
            }
        }
    }
}
