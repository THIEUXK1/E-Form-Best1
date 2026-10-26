using System.Globalization;
using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;

namespace E_Form_Best.Areas.ITForm.Services
{
    /// <summary>
    /// Thêm MAC thiết bị Wi-Fi vào các WLC của công ty qua app automation Node (POST {BaseUrl}/api/exec).
    /// App Node SSH vào WLC bằng chính tài khoản AD của IT đang thao tác nên trên WLC lưu đúng người làm;
    /// mật khẩu AD chỉ đi qua trong request này, không lưu ở đâu.
    ///
    /// Cấu hình (.env): WlcExec__BaseUrl (nhiều máy dự phòng "url1;url2"), WlcExec__ApiKey.
    /// WLC số 0 (Cisco 9800) theo khoá cũ: WlcExec__{CÔNG TY}__DeviceIp, __DeviceType (mặc định cisco_xe),
    /// __Profiles = "MAC-Staff:BPVN_STAFF;MAC-Guest:BPVN_GUEST" (aaa list:wlan profile) — danh sách profile trong modal.
    /// WLC thêm: WlcExec__{CÔNG TY}__Wlc__{N}__Ip / __Ten / __Loai (Cisco9800 | AireOS) / __DeviceType,
    /// AireOS thêm __Wlan = "BPVN_STAFF=3:management;..." (profile ở modal = WLAN ID:interface trên con AireOS).
    /// </summary>
    public class WlcMacService
    {
        public const string TenHttpClient = "WlcExec";

        public record HoSoWlan(string AaaList, string WlanProfile)
        {
            public string Ma => $"{AaaList}:{WlanProfile}";
        }

        /// <summary>Một WLC của công ty. AireOsWlan: tên WLAN profile ở modal -> (WLAN ID, interface) trên con AireOS.</summary>
        public record Wlc(string Ten, string Ip, bool LaAireOs, string DeviceType, Dictionary<string, (string WlanId, string Interface)> AireOsWlan);

        /// <summary>Kết quả trên 1 WLC. Lenh = lệnh đã thêm (ghi lịch sử đơn), null nếu không thêm.</summary>
        public record KetQuaWlc(string Wlc, string Ip, bool ThanhCong, bool DaTonTai, string ThongBao, CauHinhMac? HienCo = null, string? Lenh = null)
        {
            public bool XongViec => ThanhCong || DaTonTai;
        }

        /// <summary>Cấu hình MAC đang có trên WLC. Với AireOS: AaaList = interface, WlanProfile = "WLAN {id}".</summary>
        public record CauHinhMac(string Mac, string? AaaList, string? WlanProfile, string? MoTa, string Dong)
        {
            /// <summary>Tách từ dòng 9800 "username {mac} mac aaa attribute list ... description ...".</summary>
            public static CauHinhMac TuDong(string dong)
            {
                var m = Regex.Match(dong, @"^username\s+(\S+)(?:.*? attribute list\s+(\S+))?(?:.*? wlan-profile-name\s+(\S+))?(?:.*? description\s+(.+))?$", RegexOptions.IgnoreCase);
                return m.Success
                    ? new CauHinhMac(m.Groups[1].Value, NullNeuRong(m.Groups[2].Value), NullNeuRong(m.Groups[3].Value), NullNeuRong(m.Groups[4].Value.Trim().Trim('"')), dong)
                    : new CauHinhMac("", null, null, null, dong);
            }

            /// <summary>Tách từ "show macfilter detail" của AireOS (dạng "WLAN Identifier........ 3"); không có MAC trả null.</summary>
            public static CauHinhMac? TuAireOs(string output)
            {
                string? Truong(string ten)
                {
                    var m = Regex.Match(output, $@"^\s*{ten}\.*\s+(.+?)\s*$", RegexOptions.IgnoreCase | RegexOptions.Multiline);
                    return m.Success ? NullNeuRong(m.Groups[1].Value) : null;
                }
                var mac = Truong("MAC Address");
                if (mac == null || !Regex.IsMatch(mac, "^[0-9a-f]{2}(:[0-9a-f]{2}){5}$", RegexOptions.IgnoreCase)) return null;
                var wlan = Truong("WLAN Identifier");
                var iface = Truong("Interface Name");
                var moTa = Truong("Description");
                return new CauHinhMac(mac, iface, wlan == null ? null : "WLAN " + wlan, moTa,
                    $"macfilter {mac} wlan {wlan} interface {iface} description {moTa}");
            }

            private static string? NullNeuRong(string s) => string.IsNullOrWhiteSpace(s) ? null : s;
        }

        private readonly IHttpClientFactory _httpFactory;
        private readonly IConfiguration _configuration;
        private readonly ILogger<WlcMacService> _logger;

        public WlcMacService(IHttpClientFactory httpFactory, IConfiguration configuration, ILogger<WlcMacService> logger)
        {
            _httpFactory = httpFactory;
            _configuration = configuration;
            _logger = logger;
        }

        private string? CauHinh(string congTy, string khoa) => _configuration[$"WlcExec:{congTy}:{khoa}"];

        public bool CoCauHinh(string? congTy)
            => !string.IsNullOrWhiteSpace(congTy)
               && !string.IsNullOrWhiteSpace(_configuration["WlcExec:BaseUrl"])
               && DsWlc(congTy).Count > 0
               && DsHoSo(congTy).Count > 0;

        public List<HoSoWlan> DsHoSo(string? congTy)
        {
            var raw = string.IsNullOrWhiteSpace(congTy) ? null : CauHinh(congTy.Trim(), "Profiles");
            if (string.IsNullOrWhiteSpace(raw)) return new();
            return raw.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Select(x => x.Split(':', 2, StringSplitOptions.TrimEntries))
                .Where(x => x.Length == 2 && LaTuAnToan(x[0]) && LaTuAnToan(x[1]))
                .Select(x => new HoSoWlan(x[0], x[1]))
                .ToList();
        }

        /// <summary>Các WLC của công ty, theo thứ tự thêm: WLC khoá cũ (DeviceIp) trước, rồi Wlc__0.., Wlc__N.</summary>
        public List<Wlc> DsWlc(string? congTy)
        {
            var ds = new List<Wlc>();
            if (string.IsNullOrWhiteSpace(congTy)) return ds;
            congTy = congTy.Trim();

            var ipCu = CauHinh(congTy, "DeviceIp");
            if (LaIp(ipCu)) ds.Add(new Wlc($"WLC 9800 {ipCu}", ipCu!, false, CauHinh(congTy, "DeviceType") ?? "cisco_xe", new()));

            foreach (var muc in _configuration.GetSection($"WlcExec:{congTy}:Wlc").GetChildren().OrderBy(x => int.TryParse(x.Key, out var n) ? n : 999))
            {
                var ip = muc["Ip"];
                if (!LaIp(ip) || ds.Any(w => w.Ip == ip)) continue;
                var laAireOs = string.Equals(muc["Loai"], "AireOS", StringComparison.OrdinalIgnoreCase);
                var wlan = new Dictionary<string, (string, string)>(StringComparer.OrdinalIgnoreCase);
                foreach (var cap in (muc["Wlan"] ?? "").Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
                {
                    // "BPVN_STAFF=5:dyn interface wifi staff" — tên interface AireOS có thể chứa khoảng trắng
                    var m = Regex.Match(cap, @"^([A-Za-z0-9._-]{1,64})=(\d{1,4}):([A-Za-z0-9._ -]{1,64})$");
                    if (m.Success) wlan[m.Groups[1].Value] = (m.Groups[2].Value, m.Groups[3].Value.Trim());
                }
                ds.Add(new Wlc(muc["Ten"] ?? $"{(laAireOs ? "WLC AireOS" : "WLC 9800")} {ip}", ip!, laAireOs,
                    muc["DeviceType"] ?? (laAireOs ? "cisco_aireos" : "cisco_xe"), wlan));
            }
            return ds;
        }

        private static bool LaIp(string? s) => !string.IsNullOrWhiteSpace(s) && IPAddress.TryParse(s.Trim(), out _);

        /// <summary>MAC về 12 ký tự hex thường liền (dạng username trên WLC); sai định dạng trả null.</summary>
        public static string? ChuanHoaMac(string? mac)
        {
            if (string.IsNullOrWhiteSpace(mac)) return null;
            var hex = new string(mac.Where(Uri.IsHexDigit).ToArray()).ToLowerInvariant();
            return hex.Length == 12 ? hex : null;
        }

        /// <summary>AireOS dùng MAC dạng aa:bb:cc:dd:ee:ff.</summary>
        private static string MacHaiCham(string mac12) => string.Join(":", Enumerable.Range(0, 6).Select(i => mac12.Substring(i * 2, 2)));

        /// <summary>
        /// Mô tả đưa thẳng vào dòng lệnh CLI nên chỉ giữ chữ không dấu, số, . _ - (bỏ dấu, khoảng trắng thành _),
        /// tránh chèn ký tự xuống dòng/lệnh khác vào phiên configure terminal.
        /// </summary>
        public static string ChuanHoaMoTa(string? moTa)
        {
            if (string.IsNullOrWhiteSpace(moTa)) return "";
            var boDau = moTa.Trim().Replace('đ', 'd').Replace('Đ', 'D').Normalize(NormalizationForm.FormD);
            var sb = new StringBuilder();
            foreach (var c in boDau)
            {
                if (CharUnicodeInfo.GetUnicodeCategory(c) == UnicodeCategory.NonSpacingMark) continue;
                if (char.IsAsciiLetterOrDigit(c) || c is '.' or '_' or '-') sb.Append(c);
                else if (char.IsWhiteSpace(c) || c is '/' or '|') sb.Append('_');
            }
            var kq = Regex.Replace(sb.ToString(), "_{2,}", "_").Trim('_');
            return kq.Length > 60 ? kq[..60] : kq;
        }

        private static bool LaTuAnToan(string s) => Regex.IsMatch(s, "^[A-Za-z0-9._-]{1,64}$");

        private record ExecVao(
            [property: JsonPropertyName("deviceIp")] string DeviceIp,
            [property: JsonPropertyName("username")] string Username,
            [property: JsonPropertyName("password")] string Password,
            [property: JsonPropertyName("deviceType")] string DeviceType,
            [property: JsonPropertyName("commands")] string[] Commands,
            [property: JsonPropertyName("save")] bool Save);

        private record ExecLenh(
            [property: JsonPropertyName("command")] string? Command,
            [property: JsonPropertyName("ok")] bool Ok,
            [property: JsonPropertyName("output")] string? Output,
            [property: JsonPropertyName("error")] string? Error);

        private record ExecRa(
            [property: JsonPropertyName("ok")] bool Ok,
            [property: JsonPropertyName("message")] string? Message,
            [property: JsonPropertyName("error")] string? Error,
            [property: JsonPropertyName("results")] List<ExecLenh>? Results);

        /// <summary>Lệnh thêm MAC trên 1 WLC; AireOS chưa khai WLAN cho profile đã chọn thì null.</summary>
        private static string? LenhThem(Wlc wlc, string mac, HoSoWlan hoSo, string moTa)
        {
            if (!wlc.LaAireOs)
                return $"username {mac} mac aaa attribute list {hoSo.AaaList} wlan-profile-name {hoSo.WlanProfile}"
                       + (string.IsNullOrEmpty(moTa) ? "" : $" description {moTa}");
            if (!wlc.AireOsWlan.TryGetValue(hoSo.WlanProfile, out var w)) return null;
            // AireOS giới hạn mô tả macfilter 32 ký tự
            var moTa32 = moTa.Length > 32 ? moTa[..32] : moTa;
            var iface = w.Interface.Contains(' ') ? $"\"{w.Interface}\"" : w.Interface;
            return $"config macfilter add {MacHaiCham(mac)} {w.WlanId} {iface}" + (string.IsNullOrEmpty(moTa32) ? "" : $" {moTa32}");
        }

        /// <summary>
        /// Tra MAC trên từng WLC, lần lượt; sai mật khẩu ở con đầu thì dừng (không thử tiếp để khỏi khoá tài khoản AD).
        /// </summary>
        public async Task<List<KetQuaWlc>> KiemTraMacAsync(string congTy, string mac, string taiKhoanAd, string matKhauAd, CancellationToken ct = default)
        {
            var ds = new List<KetQuaWlc>();
            foreach (var wlc in DsWlc(congTy))
            {
                if (ds.Any(k => LaSaiMatKhau(k.ThongBao))) { ds.Add(BoQuaViSaiMatKhau(wlc)); continue; }
                var (ok, hienCo, thongBao) = await KiemTraMotWlcAsync(wlc, mac, taiKhoanAd, matKhauAd, ct);
                ds.Add(new KetQuaWlc(wlc.Ten, wlc.Ip, ok && hienCo == null, hienCo != null, thongBao, hienCo));
            }
            return ds;
        }

        /// <summary>Tra rồi thêm trên từng WLC: con nào đã có MAC thì giữ nguyên (không ghi đè), con nào chưa có thì thêm + lưu.</summary>
        public async Task<List<KetQuaWlc>> ThemMacAsync(string congTy, string mac, HoSoWlan hoSo, string moTa,
            string taiKhoanAd, string matKhauAd, CancellationToken ct = default)
        {
            var ds = new List<KetQuaWlc>();
            foreach (var wlc in DsWlc(congTy))
            {
                if (ds.Any(k => LaSaiMatKhau(k.ThongBao))) { ds.Add(BoQuaViSaiMatKhau(wlc)); continue; }
                ds.Add(await ThemMotWlcAsync(wlc, mac, hoSo, moTa, taiKhoanAd, matKhauAd, ct));
            }
            return ds;
        }

        private static KetQuaWlc BoQuaViSaiMatKhau(Wlc wlc)
            => new(wlc.Ten, wlc.Ip, false, false, "Bỏ qua: WLC trước đã từ chối mật khẩu domain.");

        /// <summary>
        /// Phải tra trước khi thêm: trên 9800 gõ lại "username {mac} ..." cho MAC đã có thì ghi đè profile/mô tả cũ
        /// mà không báo lỗi. HienCo là cấu hình đang có (null = chưa có).
        /// </summary>
        private async Task<(bool ThanhCong, CauHinhMac? HienCo, string ThongBao)> KiemTraMotWlcAsync(Wlc wlc, string mac,
            string taiKhoanAd, string matKhauAd, CancellationToken ct)
        {
            if (wlc.LaAireOs)
            {
                var (raA, loiA) = await GuiAsync(wlc, $"show macfilter detail {MacHaiCham(mac)}", false, taiKhoanAd, matKhauAd, ct);
                if (raA == null) return (false, null, loiA!);
                if (!raA.Ok) return (false, null, DichLoi(LoiLenh(raA) ?? raA.Message ?? raA.Error ?? "WLC không trả kết quả tra cứu."));
                var hienCoA = CauHinhMac.TuAireOs(raA.Results?.FirstOrDefault()?.Output ?? "");
                return (true, hienCoA, hienCoA == null ? "MAC chưa có trên WLC." : "MAC đã có trên WLC.");
            }

            // App automation vào sẵn configure terminal nên cần "do"; nếu nó chạy ở exec mode thì "do" báo
            // Invalid input -> thử lại lệnh show trần.
            var (ra, loi) = await GuiAsync(wlc, $"do show running-config | include username {mac} ", false, taiKhoanAd, matKhauAd, ct);
            if (ra != null && LoiLenh(ra)?.Contains("Invalid input", StringComparison.OrdinalIgnoreCase) == true)
                (ra, loi) = await GuiAsync(wlc, $"show running-config | include username {mac} ", false, taiKhoanAd, matKhauAd, ct);
            if (ra == null) return (false, null, loi!);
            if (!ra.Ok) return (false, null, DichLoi(LoiLenh(ra) ?? ra.Message ?? ra.Error ?? "WLC không trả kết quả tra cứu."));

            var dong = (ra.Results?.FirstOrDefault()?.Output ?? "")
                .Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .FirstOrDefault(d => d.StartsWith($"username {mac} ", StringComparison.OrdinalIgnoreCase));
            return (true, dong == null ? null : CauHinhMac.TuDong(dong), dong == null ? "MAC chưa có trên WLC." : "MAC đã có trên WLC.");
        }

        private async Task<KetQuaWlc> ThemMotWlcAsync(Wlc wlc, string mac, HoSoWlan hoSo, string moTa,
            string taiKhoanAd, string matKhauAd, CancellationToken ct)
        {
            var lenh = LenhThem(wlc, mac, hoSo, moTa);
            if (lenh == null)
                return new KetQuaWlc(wlc.Ten, wlc.Ip, false, false, $"Chưa khai WLAN cho profile {hoSo.WlanProfile} trên WLC này (WlcExec__...__Wlan).");

            var kiemTra = await KiemTraMotWlcAsync(wlc, mac, taiKhoanAd, matKhauAd, ct);
            if (!kiemTra.ThanhCong) return new KetQuaWlc(wlc.Ten, wlc.Ip, false, false, kiemTra.ThongBao);
            if (kiemTra.HienCo != null)
                return new KetQuaWlc(wlc.Ten, wlc.Ip, false, true, "MAC đã có, không ghi đè.", kiemTra.HienCo);

            var (ra, loi) = await GuiAsync(wlc, lenh, true, taiKhoanAd, matKhauAd, ct);
            if (ra == null) return new KetQuaWlc(wlc.Ten, wlc.Ip, false, false, loi!);

            // AireOS báo lỗi bằng chữ thường ("Incorrect usage", "Error ...") nên kiểm thêm output
            var output = ra.Results?.FirstOrDefault()?.Output ?? "";
            var loiAireOs = wlc.LaAireOs && Regex.IsMatch(output, @"Incorrect (usage|input)|^\s*Error|Invalid|already", RegexOptions.IgnoreCase | RegexOptions.Multiline);
            if (ra.Ok && !loiAireOs) return new KetQuaWlc(wlc.Ten, wlc.Ip, true, false, "Đã thêm và lưu cấu hình.", null, lenh);

            var loiLenh = LoiLenh(ra) ?? (loiAireOs ? output.Trim() : null);
            if (loiLenh != null && loiLenh.Contains("already", StringComparison.OrdinalIgnoreCase))
                return new KetQuaWlc(wlc.Ten, wlc.Ip, false, true, "MAC này đã có trên WLC.");
            return new KetQuaWlc(wlc.Ten, wlc.Ip, false, false, DichLoi(loiLenh ?? ra.Message ?? ra.Error ?? "WLC không nhận lệnh."));
        }

        /// <summary>
        /// Gỡ MAC khỏi đúng 1 WLC (chọn theo IP trong cấu hình công ty, không nhận IP lạ từ client).
        /// Tra trước: không có thì báo, không gửi lệnh xoá; gỡ xong tra lại để chắc WLC đã bỏ thật.
        /// </summary>
        public async Task<KetQuaWlc> GoMacAsync(string congTy, string ip, string mac, string taiKhoanAd, string matKhauAd, CancellationToken ct = default)
        {
            var wlc = DsWlc(congTy).FirstOrDefault(w => w.Ip == ip?.Trim());
            if (wlc == null) return new KetQuaWlc(ip ?? "", ip ?? "", false, false, "WLC không thuộc cấu hình của công ty.");

            var kiemTra = await KiemTraMotWlcAsync(wlc, mac, taiKhoanAd, matKhauAd, ct);
            if (!kiemTra.ThanhCong) return new KetQuaWlc(wlc.Ten, wlc.Ip, false, false, kiemTra.ThongBao);
            if (kiemTra.HienCo == null) return new KetQuaWlc(wlc.Ten, wlc.Ip, false, false, "MAC không có trên WLC này, không cần gỡ.");

            var lenh = wlc.LaAireOs ? $"config macfilter delete {MacHaiCham(mac)}" : $"no username {mac}";
            var (ra, loi) = await GuiAsync(wlc, lenh, true, taiKhoanAd, matKhauAd, ct);
            if (ra == null) return new KetQuaWlc(wlc.Ten, wlc.Ip, false, true, loi!, kiemTra.HienCo);

            var output = ra.Results?.FirstOrDefault()?.Output ?? "";
            var loiAireOs = wlc.LaAireOs && Regex.IsMatch(output, @"Incorrect (usage|input)|^\s*Error|Invalid|does not exist", RegexOptions.IgnoreCase | RegexOptions.Multiline);
            if (!ra.Ok || loiAireOs)
                return new KetQuaWlc(wlc.Ten, wlc.Ip, false, true,
                    DichLoi(LoiLenh(ra) ?? (loiAireOs ? output.Trim() : null) ?? ra.Message ?? ra.Error ?? "WLC không nhận lệnh."), kiemTra.HienCo);

            var sau = await KiemTraMotWlcAsync(wlc, mac, taiKhoanAd, matKhauAd, ct);
            if (sau.ThanhCong && sau.HienCo != null)
                return new KetQuaWlc(wlc.Ten, wlc.Ip, false, true, "Đã gửi lệnh gỡ nhưng WLC vẫn còn MAC — kiểm tra trực tiếp trên WLC.", sau.HienCo, lenh);
            return new KetQuaWlc(wlc.Ten, wlc.Ip, true, false, "Đã gỡ và lưu cấu hình.", kiemTra.HienCo, lenh);
        }

        private static string? LoiLenh(ExecRa ra) => ra.Results?.FirstOrDefault(r => !r.Ok)?.Error;

        private const string ThongBaoSaiMatKhau = "WLC từ chối đăng nhập SSH: sai mật khẩu domain";

        private static bool LaSaiMatKhau(string thongBao) => thongBao.StartsWith(ThongBaoSaiMatKhau, StringComparison.Ordinal);

        /// <summary>Lỗi SSH của app automation (thư viện ssh2) đổi sang câu IT đọc hiểu; lỗi khác giữ nguyên.</summary>
        private static string DichLoi(string loi)
        {
            if (loi.Contains("authentication methods failed", StringComparison.OrdinalIgnoreCase))
                return ThongBaoSaiMatKhau + " (kiểm tra Caps Lock / trình duyệt tự điền). Sai nhiều lần có thể khoá tài khoản domain.";
            if (loi.Contains("ETIMEDOUT", StringComparison.OrdinalIgnoreCase) || loi.Contains("ECONNREFUSED", StringComparison.OrdinalIgnoreCase))
                return "App automation không SSH tới được WLC: " + loi;
            if (loi.Contains("deviceType", StringComparison.OrdinalIgnoreCase) || loi.Contains("device_type", StringComparison.OrdinalIgnoreCase))
                return "App automation chưa hỗ trợ loại WLC này: " + loi;
            return loi;
        }

        /// <summary>Các công ty có khai WLC trong .env (khoá con của WlcExec trừ khoá chung).</summary>
        public List<string> DsCongTyCoCauHinh()
            => _configuration.GetSection("WlcExec").GetChildren().Select(x => x.Key)
                .Where(k => k is not ("BaseUrl" or "ApiKey" or "Quet") && CoCauHinh(k))
                .ToList();

        /// <summary>
        /// Toàn bộ MAC đang khai trên 1 WLC (1 phiên SSH): 9800 đọc các dòng "username {mac} mac ..." trong running-config,
        /// AireOS đọc bảng "show macfilter summary". Lỗi trả (null, thông báo) — không được coi là danh sách rỗng.
        /// </summary>
        public async Task<(List<CauHinhMac>? Ds, string? Loi)> LayDsMacAsync(Wlc wlc, string taiKhoanAd, string matKhauAd, CancellationToken ct = default)
        {
            if (wlc.LaAireOs)
            {
                // Tắt phân trang trong phiên này, không thì bảng dài dừng ở "--More--" và MAC phía sau bị coi là đã mất
                var (raA, loiA) = await GuiAsync(wlc, new[] { "config paging disable", "show macfilter summary" }, false, taiKhoanAd, matKhauAd, ct);
                if (raA == null) return (null, loiA);
                var kqA = raA.Results?.LastOrDefault();
                if (!raA.Ok || kqA == null || !kqA.Ok)
                    return (null, DichLoi(LoiLenh(raA) ?? raA.Message ?? raA.Error ?? "WLC không trả danh sách MAC."));
                var outA = kqA.Output ?? "";
                if (outA.Contains("--More--")) return (null, "Danh sách MAC trên WLC bị phân trang, chưa đọc hết.");
                if (!outA.Contains("MAC Address", StringComparison.OrdinalIgnoreCase))
                    return (null, "Không đọc được bảng MAC filter: " + (outA.Length > 200 ? outA[..200] : outA).Trim());
                var dsA = Regex.Matches(outA, @"^\s*([0-9a-f]{2}(?::[0-9a-f]{2}){5})\s+(\d+)\s+\S+[ \t]*(.*?)\s*$", RegexOptions.IgnoreCase | RegexOptions.Multiline)
                    .Select(m => new CauHinhMac(m.Groups[1].Value, null, "WLAN " + m.Groups[2].Value,
                        string.IsNullOrWhiteSpace(m.Groups[3].Value) ? null : m.Groups[3].Value, m.Value.Trim()))
                    .ToList();
                return (dsA, null);
            }

            // Cùng lý do với tra 1 MAC: app có thể đang ở configure terminal (cần "do") hoặc exec mode (không "do")
            var (ra, loi) = await GuiAsync(wlc, new[] { "do terminal length 0", "do show running-config | include ^username " }, false, taiKhoanAd, matKhauAd, ct);
            if (ra != null && ra.Results?.Any(r => (r.Error ?? r.Output ?? "").Contains("Invalid input", StringComparison.OrdinalIgnoreCase)) == true)
                (ra, loi) = await GuiAsync(wlc, new[] { "terminal length 0", "show running-config | include ^username " }, false, taiKhoanAd, matKhauAd, ct);
            if (ra == null) return (null, loi);
            var kq = ra.Results?.LastOrDefault();
            if (!ra.Ok || kq == null || !kq.Ok)
                return (null, DichLoi(LoiLenh(ra) ?? ra.Message ?? ra.Error ?? "WLC không trả danh sách MAC."));
            var output = kq.Output ?? "";
            if (output.Contains("--More--")) return (null, "Danh sách MAC trên WLC bị phân trang, chưa đọc hết.");
            var ds = output.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Where(d => Regex.IsMatch(d, @"^username\s+[0-9a-f]{12}\s+mac\b", RegexOptions.IgnoreCase))
                .Select(CauHinhMac.TuDong)
                .ToList();
            return (ds, null);
        }

        public static bool LaLoiSaiMatKhau(string? loi) => loi != null && LaSaiMatKhau(loi);

        /// <summary>Gửi 1 lệnh qua app automation; lỗi kết nối/HTTP trả (null, thông báo).</summary>
        private Task<(ExecRa? Ra, string? Loi)> GuiAsync(Wlc wlc, string lenh, bool luu,
            string taiKhoanAd, string matKhauAd, CancellationToken ct)
            => GuiAsync(wlc, new[] { lenh }, luu, taiKhoanAd, matKhauAd, ct);

        /// <summary>Gửi nhiều lệnh trong cùng 1 phiên SSH; lỗi kết nối/HTTP trả (null, thông báo).</summary>
        private async Task<(ExecRa? Ra, string? Loi)> GuiAsync(Wlc wlc, string[] dsLenh, bool luu,
            string taiKhoanAd, string matKhauAd, CancellationToken ct)
        {
            var vao = new ExecVao(wlc.Ip, taiKhoanAd, matKhauAd, wlc.DeviceType, dsLenh, luu);

            // BaseUrl có thể nhiều máy "url1;url2": máy đầu không kết nối được mới thử máy sau.
            // Timeout thì dừng luôn — lệnh có thể đã tới WLC, gửi tiếp máy khác dễ chạy 2 lần.
            var dsUrl = (_configuration["WlcExec:BaseUrl"] ?? "")
                .Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Select(u => u.TrimEnd('/')).ToList();
            HttpResponseMessage? res = null;
            foreach (var baseUrl in dsUrl)
            {
                using var req = new HttpRequestMessage(HttpMethod.Post, $"{baseUrl}/api/exec") { Content = JsonContent.Create(vao) };
                req.Headers.Add("X-API-Key", _configuration["WlcExec:ApiKey"] ?? "");
                try
                {
                    res = await _httpFactory.CreateClient(TenHttpClient).SendAsync(req, ct);
                    break;
                }
                catch (HttpRequestException ex)
                {
                    // Không log request: trong body có mật khẩu AD.
                    _logger.LogWarning("WLC exec: không gọi được {Url}: {Loi}", baseUrl, ex.Message);
                }
                catch (TaskCanceledException)
                {
                    _logger.LogWarning("WLC exec: {Url} quá thời gian chờ ({Wlc})", baseUrl, wlc.Ip);
                    return (null, "App automation WLC không phản hồi kịp — kiểm tra trên WLC trước khi thử lại.");
                }
            }
            if (res == null) return (null, "Không kết nối được app automation WLC.");

            using (res)
            {
                ExecRa? ra = null;
                try { ra = await res.Content.ReadFromJsonAsync<ExecRa>(cancellationToken: ct); } catch { }

                if (res.StatusCode == HttpStatusCode.Unauthorized)
                {
                    _logger.LogWarning("WLC exec: API key bị từ chối (401)");
                    return (null, "App automation từ chối API key — báo quản trị kiểm tra WlcExec__ApiKey.");
                }
                if (!res.IsSuccessStatusCode || ra == null)
                    return (null, DichLoi(ra?.Error ?? ra?.Message ?? $"App automation trả lỗi HTTP {(int)res.StatusCode}."));
                return (ra, null);
            }
        }
    }
}
