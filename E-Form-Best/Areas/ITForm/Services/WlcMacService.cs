using System.Globalization;
using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;

namespace E_Form_Best.Areas.ITForm.Services
{
    /// <summary>
    /// Thêm MAC thiết bị Wi-Fi vào WLC (Cisco 9800) qua app automation Node (POST {BaseUrl}/api/exec).
    /// App Node SSH vào WLC bằng chính tài khoản AD của IT đang thao tác nên trên WLC lưu đúng người làm;
    /// mật khẩu AD chỉ đi qua trong request này, không lưu ở đâu.
    /// Cấu hình (.env): WlcExec__BaseUrl (nhiều máy dự phòng "url1;url2"), WlcExec__ApiKey; theo công ty WlcExec__{CÔNG TY}__DeviceIp,
    /// __DeviceType (mặc định cisco_xe), __Profiles = "MAC-Staff:BPVN_STAFF;MAC-Guest:BPVN_GUEST" (aaa list:wlan profile).
    /// </summary>
    public class WlcMacService
    {
        public const string TenHttpClient = "WlcExec";

        public record HoSoWlan(string AaaList, string WlanProfile)
        {
            public string Ma => $"{AaaList}:{WlanProfile}";
        }

        public record KetQua(bool ThanhCong, bool DaTonTai, string ThongBao, CauHinhMac? HienCo = null);

        /// <summary>Cấu hình MAC đang có trên WLC, tách từ dòng "username {mac} mac aaa attribute list ... description ...".</summary>
        public record CauHinhMac(string Mac, string? AaaList, string? WlanProfile, string? MoTa, string Dong)
        {
            public static CauHinhMac TuDong(string dong)
            {
                var m = Regex.Match(dong, @"^username\s+(\S+)(?:.*? attribute list\s+(\S+))?(?:.*? wlan-profile-name\s+(\S+))?(?:.*? description\s+(.+))?$", RegexOptions.IgnoreCase);
                return m.Success
                    ? new CauHinhMac(m.Groups[1].Value, NullNeuRong(m.Groups[2].Value), NullNeuRong(m.Groups[3].Value), NullNeuRong(m.Groups[4].Value.Trim().Trim('"')), dong)
                    : new CauHinhMac("", null, null, null, dong);
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
               && !string.IsNullOrWhiteSpace(CauHinh(congTy.Trim(), "DeviceIp"))
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

        /// <summary>MAC về 12 ký tự hex thường liền (dạng username trên WLC); sai định dạng trả null.</summary>
        public static string? ChuanHoaMac(string? mac)
        {
            if (string.IsNullOrWhiteSpace(mac)) return null;
            var hex = new string(mac.Where(Uri.IsHexDigit).ToArray()).ToLowerInvariant();
            return hex.Length == 12 ? hex : null;
        }

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

        /// <summary>Lệnh sẽ chạy trên WLC, dùng chung cho gửi đi và ghi lịch sử đơn.</summary>
        public static string LenhThem(string mac, HoSoWlan hoSo, string moTa)
            => $"username {mac} mac aaa attribute list {hoSo.AaaList} wlan-profile-name {hoSo.WlanProfile}"
               + (string.IsNullOrEmpty(moTa) ? "" : $" description {moTa}");

        /// <summary>
        /// Tra MAC trên WLC. Phải tra trước khi thêm: gõ lại "username {mac} ..." cho MAC đã có thì IOS-XE
        /// ghi đè profile/mô tả cũ mà không báo lỗi. HienCo là cấu hình đang có (null = chưa có).
        /// </summary>
        public async Task<(bool ThanhCong, CauHinhMac? HienCo, string ThongBao)> KiemTraMacAsync(string congTy, string mac,
            string taiKhoanAd, string matKhauAd, CancellationToken ct = default)
        {
            // App automation vào sẵn configure terminal nên cần "do"; nếu nó chạy ở exec mode thì "do" báo
            // Invalid input -> thử lại lệnh show trần.
            var (ra, loi) = await GuiAsync(congTy, $"do show running-config | include username {mac} ", false, taiKhoanAd, matKhauAd, ct);
            if (ra != null && LoiLenh(ra)?.Contains("Invalid input", StringComparison.OrdinalIgnoreCase) == true)
                (ra, loi) = await GuiAsync(congTy, $"show running-config | include username {mac} ", false, taiKhoanAd, matKhauAd, ct);
            if (ra == null) return (false, null, loi!);
            if (!ra.Ok) return (false, null, DichLoi(LoiLenh(ra) ?? ra.Message ?? ra.Error ?? "WLC không trả kết quả tra cứu."));

            var dong = (ra.Results?.FirstOrDefault()?.Output ?? "")
                .Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .FirstOrDefault(d => d.StartsWith($"username {mac} ", StringComparison.OrdinalIgnoreCase));
            return (true, dong == null ? null : CauHinhMac.TuDong(dong), dong == null ? "MAC chưa có trên WLC." : "MAC đã có trên WLC.");
        }

        public async Task<KetQua> ThemMacAsync(string congTy, string mac, HoSoWlan hoSo, string moTa,
            string taiKhoanAd, string matKhauAd, CancellationToken ct = default)
        {
            var kiemTra = await KiemTraMacAsync(congTy, mac, taiKhoanAd, matKhauAd, ct);
            if (!kiemTra.ThanhCong) return new KetQua(false, false, kiemTra.ThongBao);
            if (kiemTra.HienCo != null) return new KetQua(false, true, $"MAC đã có trên WLC, không ghi đè: {kiemTra.HienCo.Dong}", kiemTra.HienCo);

            var (ra, loi) = await GuiAsync(congTy, LenhThem(mac, hoSo, moTa), true, taiKhoanAd, matKhauAd, ct);
            if (ra == null) return new KetQua(false, false, loi!);
            if (ra.Ok) return new KetQua(true, false, "Đã thêm MAC lên WLC và lưu cấu hình.");

            var loiLenh = LoiLenh(ra);
            if (loiLenh != null && loiLenh.Contains("already exists", StringComparison.OrdinalIgnoreCase))
                return new KetQua(false, true, "MAC này đã có trên WLC.");
            return new KetQua(false, false, DichLoi(loiLenh ?? ra.Message ?? ra.Error ?? "WLC không nhận lệnh."));
        }

        private static string? LoiLenh(ExecRa ra) => ra.Results?.FirstOrDefault(r => !r.Ok)?.Error;

        /// <summary>Lỗi SSH của app automation (thư viện ssh2) đổi sang câu IT đọc hiểu; lỗi khác giữ nguyên.</summary>
        private static string DichLoi(string loi)
        {
            if (loi.Contains("authentication methods failed", StringComparison.OrdinalIgnoreCase))
                return "WLC từ chối đăng nhập SSH: sai mật khẩu domain (kiểm tra Caps Lock / trình duyệt tự điền). "
                       + "Sai nhiều lần có thể khoá tài khoản domain.";
            if (loi.Contains("ETIMEDOUT", StringComparison.OrdinalIgnoreCase) || loi.Contains("ECONNREFUSED", StringComparison.OrdinalIgnoreCase))
                return "App automation không SSH tới được WLC: " + loi;
            return loi;
        }

        /// <summary>Gửi 1 lệnh qua app automation; lỗi kết nối/HTTP trả (null, thông báo).</summary>
        private async Task<(ExecRa? Ra, string? Loi)> GuiAsync(string congTy, string lenh, bool luu,
            string taiKhoanAd, string matKhauAd, CancellationToken ct)
        {
            congTy = congTy.Trim();
            var vao = new ExecVao(CauHinh(congTy, "DeviceIp")!, taiKhoanAd, matKhauAd,
                CauHinh(congTy, "DeviceType") ?? "cisco_xe", new[] { lenh }, luu);

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
                    _logger.LogWarning("WLC exec: {Url} quá thời gian chờ", baseUrl);
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
                    return (null, ra?.Error ?? ra?.Message ?? $"App automation trả lỗi HTTP {(int)res.StatusCode}.");
                return (ra, null);
            }
        }
    }
}
