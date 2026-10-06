using System.Collections.Concurrent;
using System.Text.Json;
using Microsoft.Extensions.Caching.Memory;

namespace E_Form_Best.Areas.ITForm.Services
{
    /// <summary>
    /// Xem trực tiếp camera BPVN:
    ///  - Ảnh chụp: gọi thẳng ISAPI của đầu ghi Hikvision (/ISAPI/Streaming/channels/{kênh}02/picture, Digest).
    ///  - Video: go2rtc chạy CÙNG MÁY (chỉ nghe 127.0.0.1) kéo RTSP luồng phụ, chuyển H.265 -> H.264 bằng
    ///    ffmpeg khi cần, rồi trả fMP4; controller
    ///    chuyển tiếp nguyên luồng cho trình duyệt. Trình duyệt không bao giờ thấy mật khẩu đầu ghi
    ///    hay địa chỉ go2rtc, và mọi lượt xem đều qua đăng nhập + phân quyền của E-Form.
    ///
    /// Chỉ cho phép IP đầu ghi có trong danh sách của hệ thống giám sát ISAPI — chặn việc dùng
    /// E-Form làm cầu gọi tới máy bất kỳ trong mạng (SSRF).
    /// </summary>
    public class CameraXemTrucTiepService
    {
        public const string TenClientNvr = "CameraNvr";
        public const string TenClientGo2rtc = "CameraGo2rtc";
        private const string KhoaDsNvr = "CameraXem:DsNvr";

        // Tên luồng đã khai với go2rtc trong vòng đời tiến trình; go2rtc khởi động lại thì nhánh thử lại tự khai lại
        private static readonly ConcurrentDictionary<string, bool> _daKhai = new();

        private readonly IHttpClientFactory _httpClientFactory;
        private readonly IConfiguration _configuration;
        private readonly IMemoryCache _cache;
        private readonly CameraGiamSatService _giamSat;

        public CameraXemTrucTiepService(IHttpClientFactory httpClientFactory, IConfiguration configuration,
            IMemoryCache cache, CameraGiamSatService giamSat)
        {
            _httpClientFactory = httpClientFactory;
            _configuration = configuration;
            _cache = cache;
            _giamSat = giamSat;
        }

        /// <summary>IP đầu ghi có nằm trong danh sách hệ thống giám sát không (cache 5 phút).</summary>
        public async Task<bool> LaDauGhiHopLeAsync(string? nvrIp, CancellationToken ct)
        {
            if (string.IsNullOrWhiteSpace(nvrIp)) return false;

            if (!_cache.TryGetValue(KhoaDsNvr, out HashSet<string>? ds) || ds is null)
            {
                var duLieu = await _giamSat.DanhSachDauGhiAsync(ct);
                ds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                if (duLieu.TryGetProperty("nvrs", out var nvrs) && nvrs.ValueKind == JsonValueKind.Array)
                {
                    foreach (var n in nvrs.EnumerateArray())
                        if (n.TryGetProperty("nvr_ip", out var ip) && ip.GetString() is { } s) ds.Add(s);
                }
                _cache.Set(KhoaDsNvr, ds, TimeSpan.FromMinutes(5));
            }

            return ds.Contains(nvrIp.Trim());
        }

        /// <summary>Lấy 1 ảnh JPEG luồng phụ. Trả null nếu đầu ghi không trả ảnh.</summary>
        public async Task<byte[]?> LayAnhChupAsync(string nvrIp, int kenh, CancellationToken ct)
        {
            var client = _httpClientFactory.CreateClient(TenClientNvr);
            using var traLoi = await client.GetAsync($"http://{nvrIp}/ISAPI/Streaming/channels/{kenh}02/picture", ct);
            if (!traLoi.IsSuccessStatusCode) return null;
            return await traLoi.Content.ReadAsByteArrayAsync(ct);
        }

        /// <summary>
        /// Mở luồng fMP4 từ go2rtc (đã khai nguồn RTSP). Người gọi chịu trách nhiệm Dispose response.
        /// Trả null khi go2rtc không mở được luồng sau một lần khai lại.
        /// </summary>
        public async Task<HttpResponseMessage?> MoLuongVideoAsync(string nvrIp, int kenh, CancellationToken ct)
        {
            var go2rtc = DiaChiGo2rtc();
            var client = _httpClientFactory.CreateClient(TenClientGo2rtc);
            var ten = $"nvr_{nvrIp.Replace('.', '_')}_{kenh}";

            for (var lan = 0; lan < 2; lan++)
            {
                if (lan > 0 || !_daKhai.ContainsKey(ten))
                {
                    await KhaiLuongAsync(client, go2rtc, ten, nvrIp, kenh, ct);
                }

                // video=h264: trình duyệt nào cũng giải mã được. Camera vốn H.264 thì go2rtc lấy thẳng nguồn RTSP,
                // camera H.265 thì go2rtc tự rơi xuống nguồn ffmpeg chuyển mã (xem KhaiLuongAsync)
                var yeuCau = new HttpRequestMessage(HttpMethod.Get, $"{go2rtc}/api/stream.mp4?src={Uri.EscapeDataString(ten)}&video=h264");
                var traLoi = await client.SendAsync(yeuCau, HttpCompletionOption.ResponseHeadersRead, ct);
                if (traLoi.IsSuccessStatusCode) return traLoi;

                traLoi.Dispose();
                _daKhai.TryRemove(ten, out _);
            }

            return null;
        }

        private async Task KhaiLuongAsync(HttpClient client, string go2rtc, string ten, string nvrIp, int kenh, CancellationToken ct)
        {
            var (taiKhoan, matKhau) = TaiKhoanNvr();
            var cong = _configuration.GetValue<int?>("CameraNvr:RtspPort") ?? 554;
            var rtsp = $"rtsp://{Uri.EscapeDataString(taiKhoan)}:{Uri.EscapeDataString(matKhau)}@{nvrIp}:{cong}/Streaming/Channels/{kenh}02";

            // Hai nguồn: RTSP gốc + bản ffmpeg chuyển H.264. Luồng phụ nhiều camera BPVN là H.265, Chrome/Edge
            // không có giải mã phần cứng thì không phát được. go2rtc chỉ bật ffmpeg khi nguồn gốc không phải H.264.
            var ffmpeg = $"ffmpeg:{ten}#video=h264";
            using var traLoi = await client.PutAsync(
                $"{go2rtc}/api/streams?name={Uri.EscapeDataString(ten)}&src={Uri.EscapeDataString(rtsp)}&src={Uri.EscapeDataString(ffmpeg)}", null, ct);
            if (!traLoi.IsSuccessStatusCode)
                throw new InvalidOperationException($"go2rtc từ chối khai luồng (HTTP {(int)traLoi.StatusCode}).");

            _daKhai[ten] = true;
        }

        private string DiaChiGo2rtc()
        {
            var url = _configuration["CameraNvr:Go2rtcUrl"];
            if (string.IsNullOrWhiteSpace(url))
                throw new InvalidOperationException("Chưa cấu hình CameraNvr__Go2rtcUrl trong .env.");
            return url.TrimEnd('/');
        }

        private (string taiKhoan, string matKhau) TaiKhoanNvr()
        {
            var taiKhoan = _configuration["CameraNvr:TaiKhoan"];
            var matKhau = _configuration["CameraNvr:MatKhau"];
            if (string.IsNullOrWhiteSpace(taiKhoan) || string.IsNullOrWhiteSpace(matKhau))
                throw new InvalidOperationException("Chưa cấu hình CameraNvr__TaiKhoan / CameraNvr__MatKhau trong .env.");
            return (taiKhoan, matKhau);
        }
    }
}
