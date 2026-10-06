using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Caching.Memory;

namespace E_Form_Best.Areas.ITForm.Services
{
    /// <summary>
    /// Đọc dữ liệu từ hệ thống giám sát camera ISAPI (BPVN Camera ISAPI, mặc định http://10.0.60.238:3005).
    /// Hệ thống đó mới là nơi poll đầu ghi Hikvision mỗi vài phút; E-Form chỉ ĐỌC để hiển thị,
    /// không poll trùng và không gọi các API ghi (thêm NVR, loại trừ, watchlist...).
    ///
    /// Tài khoản đặt ở .env (CameraIsapi__TaiKhoan / CameraIsapi__MatKhau), chỉ dùng phía server —
    /// trình duyệt không bao giờ thấy token hay mật khẩu.
    /// </summary>
    public class CameraGiamSatService
    {
        public const string TenHttpClient = "CameraIsapi";
        private const string KhoaToken = "CameraIsapi:Token";

        private readonly IHttpClientFactory _httpClientFactory;
        private readonly IConfiguration _configuration;
        private readonly IMemoryCache _cache;

        public CameraGiamSatService(IHttpClientFactory httpClientFactory, IConfiguration configuration, IMemoryCache cache)
        {
            _httpClientFactory = httpClientFactory;
            _configuration = configuration;
            _cache = cache;
        }

        public Task<JsonElement> TongQuanAsync(CancellationToken ct) => GetAsync("/api/summary", ct);

        // Dùng /api/nvrs thay vì /api/nvr-inventory: bản inventory có kèm user + tên biến mật khẩu đầu ghi
        public Task<JsonElement> DanhSachDauGhiAsync(CancellationToken ct) => GetAsync("/api/nvrs", ct);

        public Task<JsonElement> DanhSachCameraAsync(string? trangThai, string? nvrIp, string? khuVuc,
            bool gomDaLoaiTru, bool chiCanChuY, CancellationToken ct)
        {
            var thamSo = new List<string>();
            if (!string.IsNullOrWhiteSpace(trangThai)) thamSo.Add("status=" + Uri.EscapeDataString(trangThai));
            if (!string.IsNullOrWhiteSpace(nvrIp)) thamSo.Add("nvr_ip=" + Uri.EscapeDataString(nvrIp));
            if (!string.IsNullOrWhiteSpace(khuVuc)) thamSo.Add("zone=" + Uri.EscapeDataString(khuVuc));
            if (gomDaLoaiTru) thamSo.Add("include_excluded=true");
            if (chiCanChuY) thamSo.Add("watchlist_only=true");

            return GetAsync("/api/cameras" + (thamSo.Count > 0 ? "?" + string.Join("&", thamSo) : ""), ct);
        }

        /// <summary>Các lần poll gần nhất (mỗi lần ~5 phút) để vẽ xu hướng online/mất kết nối.</summary>
        public Task<JsonElement> LichSuKiemTraAsync(int soLan, CancellationToken ct)
            => GetAsync($"/api/poll-runs/recent?limit={Math.Clamp(soLan, 1, 2000)}", ct);

        private async Task<JsonElement> GetAsync(string duongDan, CancellationToken ct)
        {
            var client = TaoClient();

            var token = await LayTokenAsync(client, false, ct);
            using var traLoi = await GuiAsync(client, duongDan, token, ct);

            if (traLoi.StatusCode == HttpStatusCode.Unauthorized)
            {
                // Token hết hạn trước thời gian cache -> đăng nhập lại đúng một lần
                token = await LayTokenAsync(client, true, ct);
                using var traLoiLai = await GuiAsync(client, duongDan, token, ct);
                return await DocJsonAsync(traLoiLai, ct);
            }

            return await DocJsonAsync(traLoi, ct);
        }

        private static async Task<HttpResponseMessage> GuiAsync(HttpClient client, string duongDan, string token, CancellationToken ct)
        {
            using var yeuCau = new HttpRequestMessage(HttpMethod.Get, duongDan);
            yeuCau.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
            return await client.SendAsync(yeuCau, ct);
        }

        private static async Task<JsonElement> DocJsonAsync(HttpResponseMessage traLoi, CancellationToken ct)
        {
            if (!traLoi.IsSuccessStatusCode)
                throw new InvalidOperationException($"Hệ thống giám sát camera trả lỗi HTTP {(int)traLoi.StatusCode}.");

            using var tai = await traLoi.Content.ReadAsStreamAsync(ct);
            using var doc = await JsonDocument.ParseAsync(tai, cancellationToken: ct);
            return doc.RootElement.Clone();
        }

        private async Task<string> LayTokenAsync(HttpClient client, bool batBuocMoi, CancellationToken ct)
        {
            if (!batBuocMoi && _cache.TryGetValue(KhoaToken, out string? daCo) && !string.IsNullOrEmpty(daCo))
                return daCo;

            var taiKhoan = _configuration["CameraIsapi:TaiKhoan"];
            var matKhau = _configuration["CameraIsapi:MatKhau"];
            if (string.IsNullOrWhiteSpace(taiKhoan) || string.IsNullOrWhiteSpace(matKhau))
                throw new InvalidOperationException("Chưa cấu hình CameraIsapi__TaiKhoan / CameraIsapi__MatKhau trong .env.");

            using var traLoi = await client.PostAsJsonAsync("/auth/login", new { username = taiKhoan, password = matKhau }, ct);
            if (!traLoi.IsSuccessStatusCode)
                throw new InvalidOperationException($"Không đăng nhập được hệ thống giám sát camera (HTTP {(int)traLoi.StatusCode}).");

            using var doc = await JsonDocument.ParseAsync(await traLoi.Content.ReadAsStreamAsync(ct), cancellationToken: ct);
            var token = doc.RootElement.TryGetProperty("access_token", out var t) ? t.GetString() : null;
            if (string.IsNullOrEmpty(token))
                throw new InvalidOperationException("Hệ thống giám sát camera không trả token đăng nhập.");

            // Không biết chắc hạn của JWT bên kia nên giữ ngắn; hết hạn sớm hơn thì nhánh 401 ở trên tự lo
            _cache.Set(KhoaToken, token, TimeSpan.FromMinutes(20));
            return token;
        }

        private HttpClient TaoClient()
        {
            var baseUrl = _configuration["CameraIsapi:BaseUrl"];
            if (string.IsNullOrWhiteSpace(baseUrl))
                throw new InvalidOperationException("Chưa cấu hình CameraIsapi__BaseUrl trong .env.");

            var client = _httpClientFactory.CreateClient(TenHttpClient);
            client.BaseAddress = new Uri(baseUrl.TrimEnd('/'));
            return client;
        }
    }
}
