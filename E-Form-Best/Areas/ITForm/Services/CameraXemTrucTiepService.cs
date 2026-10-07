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

        /// <summary>
        /// Lấy 1 ảnh JPEG luồng phụ. Thử cổng/giao thức của đầu ghi theo inventory ISAPI trước
        /// (10.0.28.254 dùng http:8001, 10.0.29.254 dùng https:8003...), không được thì lùi về http:80.
        /// Trả null nếu không cách nào lấy được ảnh.
        /// </summary>
        public async Task<byte[]?> LayAnhChupAsync(string nvrIp, int kenh, CancellationToken ct)
        {
            var client = _httpClientFactory.CreateClient(TenClientNvr);

            foreach (var goc in await DsDiaChiNvrAsync(nvrIp, ct))
            {
                // Firmware NVR cũ (DS-7732NI-K4 V4.30 ở 10.0.28.5) trả 400 "Invalid XML Content" cho
                // /Streaming/channels/.../picture, chỉ chụp được qua /ContentMgmt/StreamingProxy/...
                foreach (var duongDan in new[]
                {
                    $"/ISAPI/Streaming/channels/{kenh}02/picture",
                    $"/ISAPI/ContentMgmt/StreamingProxy/channels/{kenh}02/picture"
                })
                {
                    try
                    {
                        using var traLoi = await client.GetAsync(goc + duongDan, ct);
                        if (traLoi.IsSuccessStatusCode) return await traLoi.Content.ReadAsByteArrayAsync(ct);
                        // 4xx/5xx: đầu ghi có trả lời -> thử đường dẫn kế tiếp trên cùng cổng
                    }
                    catch (Exception ex) when (ex is HttpRequestException || (ex is TaskCanceledException && !ct.IsCancellationRequested))
                    {
                        break; // cổng này không trả lời -> bỏ các đường dẫn còn lại, thử cổng kế tiếp
                    }
                }
            }
            return null;
        }

        /// <summary>Các địa chỉ gốc để gọi ISAPI của một đầu ghi, cổng theo inventory đứng trước (cache 10 phút).</summary>
        private async Task<List<string>> DsDiaChiNvrAsync(string nvrIp, CancellationToken ct)
        {
            var bang = await BangDiaChiNvrAsync(ct);

            var ds = new List<string>();
            if (bang.TryGetValue(nvrIp, out var goc)) ds.Add(goc);
            var macDinh = $"http://{nvrIp}:80";
            if (!ds.Contains(macDinh, StringComparer.OrdinalIgnoreCase)) ds.Add(macDinh);
            return ds;
        }

        /// <summary>
        /// IP đầu ghi -> địa chỉ gốc web/ISAPI ("https://10.0.29.254:8003") theo inventory ISAPI (cache 10 phút).
        /// Chỉ chứa giao thức + IP + cổng, không có tài khoản — trả ra trình duyệt được (link mở trang đầu ghi).
        /// Hệ thống ISAPI sập thì trả bảng rỗng.
        /// </summary>
        public async Task<Dictionary<string, string>> BangDiaChiNvrAsync(CancellationToken ct)
        {
            const string khoa = "CameraXem:CongNvr";
            if (!_cache.TryGetValue(khoa, out Dictionary<string, string>? bang) || bang is null)
            {
                bang = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                try
                {
                    var kho = await _giamSat.KhoDauGhiAsync(ct);
                    if (kho.TryGetProperty("nvrs", out var nvrs) && nvrs.ValueKind == JsonValueKind.Array)
                    {
                        foreach (var n in nvrs.EnumerateArray())
                        {
                            if (!n.TryGetProperty("ip", out var ipEl) || ipEl.GetString() is not { } ip) continue;
                            var https = n.TryGetProperty("https", out var h) && h.ValueKind == JsonValueKind.True;
                            var cong = n.TryGetProperty("api_port", out var p) && p.TryGetInt32(out var so) ? so : (https ? 443 : 80);
                            bang[ip] = $"{(https ? "https" : "http")}://{ip}:{cong}";
                        }
                    }
                    _cache.Set(khoa, bang, TimeSpan.FromMinutes(10));
                }
                catch (Exception ex) when (ex is HttpRequestException or InvalidOperationException or TaskCanceledException && !ct.IsCancellationRequested)
                {
                    // Hệ thống ISAPI sập: vẫn thử http:80 như trước, không cache để lần sau đọc lại
                }
            }
            return bang;
        }

        /// <summary>
        /// Ảnh lưu sẵn của camera: máy chủ E-Form không thông mạng tới dải đầu ghi, nên một máy trong
        /// mạng camera chụp định kỳ rồi đẩy file "{nvrIp}_{kênh}.jpg" vào thư mục CameraNvr:ThuMucAnhLuu.
        /// Trả null khi chưa có ảnh. nvrIp đã được kiểm nằm trong danh sách đầu ghi trước khi gọi tới đây.
        /// </summary>
        public async Task<(byte[] anh, DateTime chupLuc)?> LayAnhLuuAsync(string nvrIp, int kenh, CancellationToken ct)
        {
            var thuMuc = _configuration["CameraNvr:ThuMucAnhLuu"];
            if (string.IsNullOrWhiteSpace(thuMuc)) return null;

            // Tên file chỉ ghép từ IPv4 + số kênh, không cho ký tự đường dẫn lọt vào
            if (!System.Net.IPAddress.TryParse(nvrIp, out var ip)
                || ip.AddressFamily != System.Net.Sockets.AddressFamily.InterNetwork) return null;

            var duongDan = Path.Combine(thuMuc, $"{ip}_{kenh}.jpg");
            if (!File.Exists(duongDan)) return null;

            return (await File.ReadAllBytesAsync(duongDan, ct), File.GetLastWriteTime(duongDan));
        }

        /// <summary>
        /// Các kênh đã có ảnh lưu sẵn, khoá "nvrIp|kênh" — để danh sách đánh dấu camera chưa lấy được ảnh nào
        /// (chụp trực tiếp lẫn playback đều không được). Chưa cấu hình / không mở được thư mục thì trả null,
        /// giao diện khi đó không đánh dấu gì thay vì báo nhầm mọi camera đều thiếu ảnh.
        /// </summary>
        public HashSet<string>? DanhSachKenhCoAnhLuu()
        {
            var thuMuc = _configuration["CameraNvr:ThuMucAnhLuu"];
            if (string.IsNullOrWhiteSpace(thuMuc) || !Directory.Exists(thuMuc)) return null;

            var ds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var file in Directory.EnumerateFiles(thuMuc, "*.jpg"))
            {
                var m = System.Text.RegularExpressions.Regex.Match(Path.GetFileName(file), @"^(\d{1,3}(?:\.\d{1,3}){3})_(\d+)\.jpg$");
                if (m.Success) ds.Add(m.Groups[1].Value + "|" + m.Groups[2].Value);
            }
            return ds;
        }

        /// <summary>
        /// Đoạn ghi hình cuối cùng của một kênh trên đầu ghi (ISAPI ContentMgmt/search) — dùng cho ảnh lưu
        /// sẵn của camera mất kết nối: lấy khung hình cuối camera còn ghi được. Dò lùi theo từng khoảng
        /// (1 ngày, 7, 30, 90, 365 ngày) để camera mới rớt chỉ tốn 1-2 lượt gọi. Không có bản ghi trong
        /// 1 năm thì trả null. Giờ đầu ghi trả có đuôi "Z" nhưng thực chất là giờ VN (đã đối chiếu với
        /// down_since_at của hệ thống giám sát), nên đọc nguyên giá trị, không đổi múi giờ.
        /// </summary>
        public async Task<(DateTime batDau, DateTime ketThuc)?> TimGhiHinhCuoiAsync(string nvrIp, int kenh, CancellationToken ct)
        {
            var client = _httpClientFactory.CreateClient(TenClientNvr);
            var dsGoc = await DsDiaChiNvrAsync(nvrIp, ct);
            var bayGio = DateTime.Now;
            (DateTime, DateTime)? ketQua = null;

            var moc = new[] { 0, 1, 7, 30, 90, 365 };
            for (var i = 1; i < moc.Length && ketQua == null; i++)
            {
                ketQua = await TimTrongKhoangAsync(client, dsGoc, kenh, bayGio.AddDays(-moc[i]), bayGio.AddDays(-moc[i - 1]), ct);
            }
            return ketQua;
        }

        private async Task<(DateTime, DateTime)?> TimTrongKhoangAsync(HttpClient client, List<string> dsGoc, int kenh,
            DateTime tu, DateTime den, CancellationToken ct)
        {
            (DateTime, DateTime)? cuoi = null;
            var viTri = 0;

            // Đầu ghi trả kết quả tăng dần theo thời gian, mỗi trang tối đa 50 đoạn. Biết tổng số thì
            // nhảy thẳng tới trang cuối; giới hạn 20 trang để firmware lạ không làm vòng lặp chạy mãi
            for (var trang = 0; trang < 20; trang++)
            {
                var xml = await GoiTimKiemAsync(client, dsGoc, kenh, tu, den, viTri, ct);
                if (xml == null) return cuoi;

                var ns = xml.Root!.GetDefaultNamespace();
                foreach (var muc in xml.Descendants(ns + "searchMatchItem"))
                {
                    var khung = muc.Element(ns + "timeSpan");
                    if (!DocGioDauGhi(khung?.Element(ns + "startTime")?.Value, out var bd)
                        || !DocGioDauGhi(khung?.Element(ns + "endTime")?.Value, out var kt)) continue;
                    if (cuoi == null || kt > cuoi.Value.Item2) cuoi = (bd, kt);
                }

                var trangThai = xml.Root.Element(ns + "responseStatusStrg")?.Value;
                if (!string.Equals(trangThai, "MORE", StringComparison.OrdinalIgnoreCase)) return cuoi;

                int.TryParse(xml.Root.Element(ns + "numOfMatches")?.Value, out var soTrongTrang);
                int.TryParse(xml.Root.Element(ns + "totalMatches")?.Value, out var tong);
                if (soTrongTrang <= 0) return cuoi;
                viTri = tong > viTri + soTrongTrang ? Math.Max(viTri + soTrongTrang, tong - 50) : viTri + soTrongTrang;
            }
            return cuoi;
        }

        private static async Task<System.Xml.Linq.XDocument?> GoiTimKiemAsync(HttpClient client, List<string> dsGoc, int kenh,
            DateTime tu, DateTime den, int viTri, CancellationToken ct)
        {
            // Track {kênh}01 = luồng chính được ghi; "searchResultPostion" là đúng chính tả của Hikvision
            var noiDung = "<?xml version=\"1.0\" encoding=\"UTF-8\"?><CMSearchDescription>"
                + $"<searchID>{Guid.NewGuid():D}</searchID>"
                + $"<trackList><trackID>{kenh}01</trackID></trackList>"
                + $"<timeSpanList><timeSpan><startTime>{tu:yyyy-MM-ddTHH:mm:ss}Z</startTime><endTime>{den:yyyy-MM-ddTHH:mm:ss}Z</endTime></timeSpan></timeSpanList>"
                + $"<maxResults>50</maxResults><searchResultPostion>{viTri}</searchResultPostion>"
                + "<metadataList><metadataDescriptor>//recordType.meta.std-cgi.com</metadataDescriptor></metadataList>"
                + "</CMSearchDescription>";

            foreach (var goc in dsGoc)
            {
                try
                {
                    // Đầu ghi 10.0.21.251 hay trả 403 khi đang bị gọi dồn (lượt chụp, ffmpeg khác đang kéo) -> thử lại 3 lần
                    for (var lan = 0; lan < 3; lan++)
                    {
                        if (lan > 0) await Task.Delay(TimeSpan.FromSeconds(3), ct);
                        using var traLoi = await client.PostAsync(goc + "/ISAPI/ContentMgmt/search",
                            new StringContent(noiDung, System.Text.Encoding.UTF8, "application/xml"), ct);
                        if (traLoi.StatusCode == System.Net.HttpStatusCode.Forbidden) continue;
                        if (!traLoi.IsSuccessStatusCode) break;
                        return System.Xml.Linq.XDocument.Parse(await traLoi.Content.ReadAsStringAsync(ct));
                    }
                }
                catch (Exception ex) when (ex is HttpRequestException || ex is System.Xml.XmlException
                    || (ex is TaskCanceledException && !ct.IsCancellationRequested))
                {
                    // cổng này không trả lời / trả rác -> thử cổng kế tiếp
                }
            }
            return null;
        }

        private static bool DocGioDauGhi(string? s, out DateTime gio)
            => DateTime.TryParseExact(s?.TrimEnd('Z', 'z'), "yyyy-MM-ddTHH:mm:ss",
                System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.None, out gio);

        // Mỗi lần lấy khung hình là một tiến trình ffmpeg kéo playback từ đầu ghi: giới hạn 2 cái cùng lúc
        private static readonly SemaphoreSlim _gioiHanFfmpeg = new(2, 2);

        /// <summary>Giờ của khung hình sẽ lấy từ đoạn ghi: lùi 10 giây khỏi điểm kết thúc, sát mép đầu ghi hay trả luồng rỗng.</summary>
        public static DateTime GioKhungHinhPlayback(DateTime batDau, DateTime ketThuc)
            => ketThuc.AddSeconds(-10) < batDau ? batDau : ketThuc.AddSeconds(-10);

        /// <summary>
        /// Khung hình JPEG ở cuối đoạn ghi (ffmpeg đọc RTSP playback của đầu ghi, ~6-10 giây/lần).
        /// Dùng cho ảnh lưu sẵn khi chụp trực tiếp không được (camera mất kết nối).
        /// Trả null khi đầu ghi không trả được hình.
        /// </summary>
        public async Task<(byte[] anh, DateTime gio)?> ChupKhungHinhPlaybackAsync(string nvrIp, int kenh,
            DateTime batDau, DateTime ketThuc, CancellationToken ct)
        {
            if (!System.Net.IPAddress.TryParse(nvrIp, out var ip)
                || ip.AddressFamily != System.Net.Sockets.AddressFamily.InterNetwork) return null;

            var ffmpeg = _configuration["CameraNvr:FfmpegPath"];
            if (string.IsNullOrWhiteSpace(ffmpeg)) ffmpeg = @"C:\go2rtc\ffmpeg.exe";
            if (!File.Exists(ffmpeg))
                throw new InvalidOperationException("Không tìm thấy ffmpeg, khai CameraNvr__FfmpegPath trong .env.");

            var (taiKhoan, matKhau) = TaiKhoanNvr();
            var cong = await CongRtspAsync(nvrIp, ct);

            await _gioiHanFfmpeg.WaitAsync(ct);
            try
            {
                // Luồng H.265 đang ghi dở đôi khi hỏng ở đoạn sát cuối (ffmpeg INVALIDDATA) -> thử lại ở 60 giây trước điểm kết thúc
                foreach (var tu in new[] { GioKhungHinhPlayback(batDau, ketThuc), ketThuc.AddSeconds(-60) < batDau ? batDau : ketThuc.AddSeconds(-60) })
                {
                    var rtsp = $"rtsp://{Uri.EscapeDataString(taiKhoan)}:{Uri.EscapeDataString(matKhau)}@{ip}:{cong}"
                        + $"/Streaming/tracks/{kenh}01?starttime={tu:yyyyMMddTHHmmss}Z&endtime={ketThuc:yyyyMMddTHHmmss}Z";
                    var anh = await ChayFfmpegAsync(ffmpeg, rtsp, ct);
                    if (anh != null && anh.Length >= 1000) return (anh, tu);
                }
                return null;
            }
            finally { _gioiHanFfmpeg.Release(); }
        }

        /// <summary>
        /// Cổng RTSP thật của đầu ghi (10.0.28.254 dùng 8002, không mở 554) hỏi qua ISAPI adminAccesses,
        /// cache 1 giờ. Hỏi không được thì dùng CameraNvr:RtspPort (mặc định 554).
        /// </summary>
        private async Task<int> CongRtspAsync(string nvrIp, CancellationToken ct)
        {
            var khoa = $"CameraXem:CongRtsp:{nvrIp}";
            if (_cache.TryGetValue(khoa, out int daCo)) return daCo;

            var cong = _configuration.GetValue<int?>("CameraNvr:RtspPort") ?? 554;
            var client = _httpClientFactory.CreateClient(TenClientNvr);
            foreach (var goc in await DsDiaChiNvrAsync(nvrIp, ct))
            {
                try
                {
                    using var traLoi = await client.GetAsync(goc + "/ISAPI/Security/adminAccesses", ct);
                    if (!traLoi.IsSuccessStatusCode) continue;
                    var xml = System.Xml.Linq.XDocument.Parse(await traLoi.Content.ReadAsStringAsync(ct));
                    var ns = xml.Root!.GetDefaultNamespace();
                    var rtsp = xml.Descendants(ns + "AdminAccessProtocol")
                        .FirstOrDefault(p => string.Equals(p.Element(ns + "protocol")?.Value, "RTSP", StringComparison.OrdinalIgnoreCase));
                    if (int.TryParse(rtsp?.Element(ns + "portNo")?.Value, out var so) && so > 0) cong = so;
                    _cache.Set(khoa, cong, TimeSpan.FromHours(1));
                    break;
                }
                catch (Exception ex) when (ex is HttpRequestException || ex is System.Xml.XmlException
                    || (ex is TaskCanceledException && !ct.IsCancellationRequested))
                {
                    // cổng này không trả lời -> thử cổng kế tiếp
                }
            }
            return cong;
        }

        private static async Task<byte[]?> ChayFfmpegAsync(string ffmpeg, string rtsp, CancellationToken ct)
        {
            var thongTin = new System.Diagnostics.ProcessStartInfo(ffmpeg)
            {
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };
            // ArgumentList: từng tham số tách riêng, không qua shell. Ảnh luồng chính 2688x1520 nên thu về 1280 ngang
            foreach (var thamSo in new[] { "-hide_banner", "-loglevel", "error", "-rtsp_transport", "tcp", "-timeout", "10000000",
                "-i", rtsp, "-frames:v", "1", "-vf", "scale='min(1280,iw)':-2", "-q:v", "4", "-f", "image2pipe", "-vcodec", "mjpeg", "pipe:1" })
                thongTin.ArgumentList.Add(thamSo);

            using var tienTrinh = System.Diagnostics.Process.Start(thongTin)
                ?? throw new InvalidOperationException("Không chạy được ffmpeg.");
            using var hetGio = CancellationTokenSource.CreateLinkedTokenSource(ct);
            hetGio.CancelAfter(TimeSpan.FromSeconds(30));
            try
            {
                using var bo = new MemoryStream();
                var docLoi = tienTrinh.StandardError.ReadToEndAsync(hetGio.Token);
                await tienTrinh.StandardOutput.BaseStream.CopyToAsync(bo, hetGio.Token);
                await tienTrinh.WaitForExitAsync(hetGio.Token);
                await docLoi;
                return tienTrinh.ExitCode == 0 ? bo.ToArray() : null;
            }
            catch (OperationCanceledException) when (!ct.IsCancellationRequested)
            {
                // Quá 30 giây (đầu ghi treo luồng playback): coi như không lấy được
                return null;
            }
            finally
            {
                if (!tienTrinh.HasExited) tienTrinh.Kill(true);
            }
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
