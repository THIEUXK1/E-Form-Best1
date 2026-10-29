using System.Net;
using System.Net.Http.Json;
using System.Text.RegularExpressions;

namespace E_Form_Best.Areas.ITForm.Services
{
    /// <summary>
    /// Thêm MAC thiết bị không đi qua WLC (PDA, TV...) vào DaloRADIUS/FreeRADIUS qua app automation
    /// (POST {BaseUrl}/api/radius/mac, header X-API-Key). App tự thêm cả 2 dạng MAC (có/không dấu :) vào radcheck + radusergroup,
    /// description ghi vào userinfo.firstname; MAC đã có trả code ALREADY_EXISTS nên bấm lại không tạo trùng.
    ///
    /// Cấu hình (.env): RadiusMac__BaseUrl, RadiusMac__ApiKey,
    /// RadiusMac__{CÔNG TY}__Nhom = "PDA:VLAN16_QR;TV:VLAN16_QR" (loại thiết bị:group RADIUS) — công ty chưa có thì ẩn phần RADIUS.
    /// </summary>
    public class RadiusMacService
    {
        public const string TenHttpClient = "RadiusMac";

        public record NhomRadius(string LoaiThietBi, string Group)
        {
            public string Ma => $"{LoaiThietBi}:{Group}";
        }

        /// <summary>Code: CREATED | ALREADY_EXISTS | INVALID_MAC | lỗi khác; Usernames = các dạng MAC app đã ghi.</summary>
        public record KetQuaRadius(bool ThanhCong, bool DaTonTai, string Code, string ThongBao, string[]? Usernames = null)
        {
            public bool XongViec => ThanhCong || DaTonTai;
        }

        private record ThemVao(string Mac, string Description, string Group, string Notes, string Department, string CreatedBy);
        private record ThemRa(bool Ok, string? Code, string? Message, string? Mac, string? MacColon, string[]? Usernames, string? Group);

        private readonly IHttpClientFactory _httpFactory;
        private readonly IConfiguration _configuration;
        private readonly ILogger<RadiusMacService> _logger;

        public RadiusMacService(IHttpClientFactory httpFactory, IConfiguration configuration, ILogger<RadiusMacService> logger)
        {
            _httpFactory = httpFactory;
            _configuration = configuration;
            _logger = logger;
        }

        public bool CoCauHinh(string? congTy)
            => !string.IsNullOrWhiteSpace(_configuration["RadiusMac:BaseUrl"])
               && !string.IsNullOrWhiteSpace(_configuration["RadiusMac:ApiKey"])
               && DsNhom(congTy).Count > 0;

        public List<NhomRadius> DsNhom(string? congTy)
        {
            var raw = string.IsNullOrWhiteSpace(congTy) ? null : _configuration[$"RadiusMac:{congTy.Trim()}:Nhom"];
            if (string.IsNullOrWhiteSpace(raw)) return new();
            return raw.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Select(x => x.Split(':', 2, StringSplitOptions.TrimEntries))
                .Where(x => x.Length == 2 && Regex.IsMatch(x[0], @"^[\p{L}0-9 ._-]{1,30}$") && Regex.IsMatch(x[1], @"^[A-Za-z0-9._-]{1,64}$"))
                .Select(x => new NhomRadius(x[0], x[1]))
                .ToList();
        }

        /// <summary>mac12 là MAC đã chuẩn hoá (WlcMacService.ChuanHoaMac); app nhận cả dạng liền.</summary>
        public async Task<KetQuaRadius> ThemMacAsync(string mac12, NhomRadius nhom, string moTa, string ghiChu, CancellationToken ct)
        {
            var baseUrl = (_configuration["RadiusMac:BaseUrl"] ?? "").Trim().TrimEnd('/');
            var vao = new ThemVao(mac12, moTa, nhom.Group, ghiChu, "", "AppIT");
            using var req = new HttpRequestMessage(HttpMethod.Post, $"{baseUrl}/api/radius/mac") { Content = JsonContent.Create(vao) };
            req.Headers.Add("X-API-Key", _configuration["RadiusMac:ApiKey"] ?? "");

            HttpResponseMessage res;
            try
            {
                res = await _httpFactory.CreateClient(TenHttpClient).SendAsync(req, ct);
            }
            catch (HttpRequestException ex)
            {
                _logger.LogWarning("RADIUS: không gọi được {Url}: {Loi}", baseUrl, ex.Message);
                return new(false, false, "KET_NOI", "Không kết nối được app automation RADIUS.");
            }
            catch (TaskCanceledException)
            {
                // Có thể đã ghi xong trên RADIUS — bấm lại an toàn vì app trả ALREADY_EXISTS
                return new(false, false, "TIMEOUT", "App RADIUS không phản hồi kịp — bấm lại để kiểm tra (MAC đã có sẽ báo tồn tại).");
            }

            using (res)
            {
                ThemRa? ra = null;
                try { ra = await res.Content.ReadFromJsonAsync<ThemRa>(cancellationToken: ct); } catch { }

                if (res.StatusCode == HttpStatusCode.Unauthorized)
                {
                    _logger.LogWarning("RADIUS: API key bị từ chối (401)");
                    return new(false, false, "HTTP_401", "App RADIUS từ chối API key — báo quản trị kiểm tra RadiusMac__ApiKey.");
                }
                var code = ra?.Code ?? $"HTTP_{(int)res.StatusCode}";
                return code switch
                {
                    "CREATED" => new(true, false, code, $"Đã thêm vào RADIUS, group {ra?.Group ?? nhom.Group}.", ra?.Usernames),
                    "ALREADY_EXISTS" => new(false, true, code, "MAC đã có trên RADIUS, không ghi đè.", ra?.Usernames),
                    "INVALID_MAC" => new(false, false, code, "RADIUS báo MAC không hợp lệ (cần 12 ký tự hex)."),
                    _ => new(false, false, code, "RADIUS lỗi: " + (ra?.Message ?? $"HTTP {(int)res.StatusCode}"))
                };
            }
        }
    }
}
