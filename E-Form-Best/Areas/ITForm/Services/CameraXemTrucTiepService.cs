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
            // Máy chủ không thông ISAPI đầu ghi (10.0.60.39) -> lấy ảnh qua go2rtc ở máy thông mạng (10.0.60.238)
            if (_configuration.GetValue<bool?>("CameraNvr:AnhQuaGo2rtc") ?? false)
                return await LayAnhQuaGo2rtcAsync(nvrIp, kenh, ct);

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
        public async Task<(byte[] anh, DateTime chupLuc)?> LayAnhLuuAsync(string nvrIp, int kenh, CancellationToken ct, string? congTy = null)
        {
            var thuMuc = congTy == null ? _configuration["CameraNvr:ThuMucAnhLuu"] : ThuMucAnhLuuCongTy(congTy);
            if (string.IsNullOrWhiteSpace(thuMuc)) return null;

            // Tên file chỉ ghép từ IPv4 + số kênh, không cho ký tự đường dẫn lọt vào
            if (!System.Net.IPAddress.TryParse(nvrIp, out var ip)
                || ip.AddressFamily != System.Net.Sockets.AddressFamily.InterNetwork) return null;

            var duongDan = Path.Combine(thuMuc, $"{ip}_{kenh}.jpg");
            if (!File.Exists(duongDan)) return null;

            return (await File.ReadAllBytesAsync(duongDan, ct), File.GetLastWriteTime(duongDan));
        }

        /// <summary>Công ty có camera lấy qua ảnh lưu sẵn (không có hệ thống giám sát ISAPI như BPVN).</summary>
        public static readonly string[] DsCongTyAnhLuu = { "PFVN", "MEGA" };

        /// <summary>Một kênh camera của PFVN/MEGA, đọc từ "_kenh.json" do script chụp ảnh trên máy bên đó ghi.</summary>
        public class KenhCongTy
        {
            public string Nvr { get; set; } = "";
            public string? TenDauGhi { get; set; }
            public int Kenh { get; set; }
            public string? Ten { get; set; }
            public string? IpCamera { get; set; }
            public bool Online { get; set; }
            /// <summary>OK (chụp trực tiếp) · PLAYBACK / BOQUA (ảnh từ bản ghi) · KHONG / LOI (không lấy được).</summary>
            public string? KetQua { get; set; }
            /// <summary>Giờ của ảnh lưu (giờ file); null = chưa có ảnh nào.</summary>
            public DateTime? AnhLuc { get; set; }
        }

        /// <summary>
        /// go2rtc đặt ở máy thông mạng đầu ghi của công ty (PFVN: ZPVN-WEBSRV 10.0.193.240:1984), khai trong .env:
        /// Camera{CÔNG TY}__Go2rtcUrl / __Go2rtcTaiKhoan / __Go2rtcMatKhau. Luồng đã khai sẵn trong go2rtc.yaml bên đó
        /// (tools/cai-go2rtc-pfvn.ps1) tên "{công ty}_{ip}_{kênh}" — E-Form không cần mật khẩu đầu ghi.
        /// Chưa khai thì trả null: trang công ty đó chỉ có ảnh lưu sẵn.
        /// </summary>
        private (string url, string taiKhoan, string matKhau)? Go2rtcCongTy(string congTy)
        {
            var ten = DsCongTyAnhLuu.FirstOrDefault(x => x.Equals(congTy, StringComparison.OrdinalIgnoreCase));
            if (ten == null) return null;
            var url = _configuration[$"Camera{ten}:Go2rtcUrl"];
            var tk = _configuration[$"Camera{ten}:Go2rtcTaiKhoan"];
            var mk = _configuration[$"Camera{ten}:Go2rtcMatKhau"];
            if (string.IsNullOrWhiteSpace(url) || string.IsNullOrWhiteSpace(tk) || string.IsNullOrWhiteSpace(mk)) return null;
            return (url.TrimEnd('/'), tk, mk);
        }

        /// <summary>Có xem trực tiếp (ít nhất ảnh 1 giây/lần): qua go2rtc bên công ty hoặc gọi thẳng đầu ghi.</summary>
        public bool CoXemTrucTiepCongTy(string congTy) => Go2rtcCongTy(congTy) != null || CoDauGhiTrucTiepCongTy(_configuration, congTy);

        /// <summary>Có video: chỉ khi có go2rtc (gọi thẳng đầu ghi qua ISAPI chỉ lấy được ảnh).</summary>
        public bool CoVideoCongTy(string congTy) => Go2rtcCongTy(congTy) != null;

        /// <summary>
        /// Đầu ghi công ty mà máy chủ E-Form gọi THẲNG được (MEGA: IP public, ISAPI qua https:443), khai trong .env
        /// Camera{CÔNG TY}__DauGhi__N=gốc|tài khoản|mật khẩu. Có thì E-Form tự đọc trạng thái, chụp ảnh lưu và ảnh trực
        /// tiếp, không cần máy trung gian như PFVN. Khoá = IP trong gốc (cũng là "nvr" trong file trạng thái/ảnh).
        /// </summary>
        private static Dictionary<string, (string goc, string taiKhoan, string matKhau)> DauGhiTrucTiepCongTy(IConfiguration cfg, string congTy)
        {
            var kq = new Dictionary<string, (string, string, string)>(StringComparer.OrdinalIgnoreCase);
            var ten = DsCongTyAnhLuu.FirstOrDefault(x => x.Equals(congTy, StringComparison.OrdinalIgnoreCase));
            if (ten == null) return kq;

            foreach (var muc in cfg.GetSection($"Camera{ten}:DauGhi").GetChildren())
            {
                var p = (muc.Value ?? "").Split('|');
                // Chỉ nhận IPv4: IP là khoá tên file ảnh "{ip}_{kênh}.jpg"
                if (p.Length < 3 || !Uri.TryCreate(p[0].Trim().TrimEnd('/'), UriKind.Absolute, out var goc)
                    || goc.Scheme is not ("http" or "https") || goc.HostNameType != UriHostNameType.IPv4) continue;
                kq[goc.Host] = (goc.GetLeftPart(UriPartial.Authority), p[1].Trim(), string.Join('|', p[2..]));
            }
            return kq;
        }

        public static bool CoDauGhiTrucTiepCongTy(IConfiguration cfg, string congTy) => DauGhiTrucTiepCongTy(cfg, congTy).Count > 0;

        // Mỗi đầu ghi một HttpClient riêng mang Digest của nó (tài khoản khác nhau theo đầu ghi, không dùng chung
        // client "CameraNvr" của BPVN). Chứng thư tự ký -> bỏ kiểm, chỉ trong client này.
        private static readonly ConcurrentDictionary<string, HttpClient> _clientDauGhi = new();

        private static HttpClient ClientDauGhi((string goc, string taiKhoan, string matKhau) dg)
            => _clientDauGhi.GetOrAdd(dg.goc + "|" + dg.taiKhoan + "|" + dg.matKhau, _ => new HttpClient(new HttpClientHandler
            {
                ServerCertificateCustomValidationCallback = HttpClientHandler.DangerousAcceptAnyServerCertificateValidator,
                Credentials = new System.Net.NetworkCredential(dg.taiKhoan, dg.matKhau)
            }) { Timeout = TimeSpan.FromSeconds(10) });

        /// <summary>1 ảnh JPEG luồng phụ qua ISAPI; null nếu đầu ghi không trả ảnh (camera mất kết nối...).</summary>
        private static async Task<byte[]?> ChupIsapiAsync((string goc, string taiKhoan, string matKhau) dg, int kenh, CancellationToken ct)
        {
            var client = ClientDauGhi(dg);
            foreach (var duongDan in new[]
            {
                $"/ISAPI/Streaming/channels/{kenh}02/picture",
                $"/ISAPI/ContentMgmt/StreamingProxy/channels/{kenh}02/picture"
            })
            {
                using var traLoi = await client.GetAsync(dg.goc + duongDan, ct);
                if (!traLoi.IsSuccessStatusCode) continue;
                var anh = await traLoi.Content.ReadAsByteArrayAsync(ct);
                // Có firmware trả 200 kèm XML báo lỗi: chỉ nhận đúng JPEG (FF D8)
                if (anh.Length > 2 && anh[0] == 0xFF && anh[1] == 0xD8) return anh;
            }
            return null;
        }

        private static string? Con(System.Xml.Linq.XElement e, string ten)
            => e.Elements().FirstOrDefault(x => x.Name.LocalName == ten)?.Value;

        /// <summary>Kênh + online của một đầu ghi gọi thẳng (InputProxy/channels + /status), tuỳ chọn kèm tên đầu ghi.</summary>
        private static async Task<List<KenhCongTy>> DocKenhIsapiAsync(string ip, (string goc, string taiKhoan, string matKhau) dg,
            bool layTenDauGhi, CancellationToken ct)
        {
            var client = ClientDauGhi(dg);
            async Task<System.Xml.Linq.XDocument> Doc(string duongDan)
            {
                using var traLoi = await client.GetAsync(dg.goc + duongDan, ct);
                traLoi.EnsureSuccessStatusCode();
                return System.Xml.Linq.XDocument.Parse(await traLoi.Content.ReadAsStringAsync(ct));
            }

            var online = new Dictionary<string, bool>();
            foreach (var e in (await Doc("/ISAPI/ContentMgmt/InputProxy/channels/status")).Descendants()
                         .Where(x => x.Name.LocalName == "InputProxyChannelStatus"))
                online[Con(e, "id") ?? ""] = Con(e, "online") == "true";

            var dsKenh = await Doc("/ISAPI/ContentMgmt/InputProxy/channels");

            string? tenDauGhi = null;
            if (layTenDauGhi)
            {
                try { tenDauGhi = Con((await Doc("/ISAPI/System/deviceInfo")).Root!, "deviceName"); }
                catch (HttpRequestException) { /* thiếu tên đầu ghi thì hiện theo công ty + IP */ }
            }

            return dsKenh.Descendants().Where(x => x.Name.LocalName == "InputProxyChannel")
                .Select(e => new KenhCongTy
                {
                    Nvr = ip,
                    TenDauGhi = tenDauGhi,
                    Kenh = int.TryParse(Con(e, "id"), out var so) ? so : 0,
                    Ten = Con(e, "name"),
                    IpCamera = e.Descendants().FirstOrDefault(x => x.Name.LocalName == "ipAddress")?.Value,
                    Online = online.TryGetValue(Con(e, "id") ?? "", out var on) && on
                })
                .Where(k => k.Kenh > 0)
                .ToList();
        }

        private static readonly JsonSerializerOptions _jsonCamel = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

        // Ghi ra file tạm rồi đổi tên đè: trang đang đọc không bao giờ gặp file ghi dở
        private static async Task GhiFileAsync(string dich, byte[] noiDung, CancellationToken ct)
        {
            var tam = dich + ".tmp";
            await File.WriteAllBytesAsync(tam, noiDung, ct);
            File.Move(tam, dich, true);
        }

        /// <summary>
        /// Đầu ghi gọi thẳng được: đọc trạng thái kênh rồi ghi "_trang-thai.json" đúng định dạng script PFVN
        /// (tools/trang-thai-camera-pfvn.ps1) để phần đọc/lịch sử dùng chung. Không cấu hình thì trả false.
        /// </summary>
        public async Task<bool> CapNhatTrangThaiTrucTiepAsync(string congTy, CancellationToken ct)
        {
            var dsDauGhi = DauGhiTrucTiepCongTy(_configuration, congTy);
            var thuMuc = ThuMucAnhLuuCongTy(congTy);
            if (dsDauGhi.Count == 0 || thuMuc == null) return false;

            var kenh = new List<object>();
            var dauGhi = new List<object>();
            foreach (var (ip, dg) in dsDauGhi)
            {
                try
                {
                    kenh.AddRange((await DocKenhIsapiAsync(ip, dg, false, ct))
                        .Select(k => new { nvr = ip, kenh = k.Kenh, ten = k.Ten, ipCamera = k.IpCamera, online = k.Online }));
                    dauGhi.Add(new { nvr = ip, goc = dg.goc, loi = (string?)null });
                }
                catch (Exception ex) when (ex is HttpRequestException or System.Xml.XmlException
                                           || (ex is TaskCanceledException && !ct.IsCancellationRequested))
                {
                    // Đầu ghi không trả lời: ghi lỗi, phần đọc tự coi các kênh của nó là mất kết nối
                    dauGhi.Add(new { nvr = ip, goc = dg.goc, loi = ex.Message });
                }
            }

            Directory.CreateDirectory(thuMuc);
            await GhiFileAsync(Path.Combine(thuMuc, "_trang-thai.json"), JsonSerializer.SerializeToUtf8Bytes(
                new { capNhat = DateTime.Now.ToString("yyyy-MM-ddTHH:mm:ss"), dauGhi, kenh }, _jsonCamel), ct);
            return true;
        }

        /// <summary>
        /// Một lượt chụp ảnh lưu cho đầu ghi gọi thẳng được: "{ip}_{kênh}.jpg" + "_kenh.json" (như script PFVN).
        /// Kênh không chụp được thì giữ ảnh cũ — đầu ghi MEGA không mở RTSP ra ngoài nên không lấy được playback.
        /// </summary>
        public async Task<(int tong, int duoc)> ChupAnhLuuTrucTiepAsync(string congTy, CancellationToken ct)
        {
            var dsDauGhi = DauGhiTrucTiepCongTy(_configuration, congTy);
            var thuMuc = ThuMucAnhLuuCongTy(congTy);
            if (dsDauGhi.Count == 0 || thuMuc == null) return (0, 0);
            Directory.CreateDirectory(thuMuc);

            var tatCa = new List<KenhCongTy>();
            var duoc = 0;
            foreach (var (ip, dg) in dsDauGhi)
            {
                List<KenhCongTy> ds;
                try { ds = await DocKenhIsapiAsync(ip, dg, true, ct); }
                catch (Exception ex) when (ex is HttpRequestException or System.Xml.XmlException
                                           || (ex is TaskCanceledException && !ct.IsCancellationRequested))
                {
                    Console.WriteLine($"[CameraAnhLuu {congTy}] Đầu ghi {ip} không trả lời: {ex.Message}");
                    continue;
                }

                // 3 ảnh song song: đường internet công cộng, không dồn ép đầu ghi
                await Parallel.ForEachAsync(ds, new ParallelOptions { MaxDegreeOfParallelism = 3, CancellationToken = ct },
                    async (k, token) =>
                    {
                        byte[]? anh = null;
                        try { anh = await ChupIsapiAsync(dg, k.Kenh, token); }
                        catch (Exception ex) when (ex is HttpRequestException || (ex is TaskCanceledException && !ct.IsCancellationRequested)) { }

                        if (anh == null) { k.KetQua = "KHONG"; return; }
                        await GhiFileAsync(Path.Combine(thuMuc, $"{ip}_{k.Kenh}.jpg"), anh, token);
                        k.KetQua = "OK";
                        Interlocked.Increment(ref duoc);
                    });
                tatCa.AddRange(ds);
            }

            if (tatCa.Count > 0)
            {
                await GhiFileAsync(Path.Combine(thuMuc, "_kenh.json"), JsonSerializer.SerializeToUtf8Bytes(new
                {
                    capNhat = DateTime.Now.ToString("yyyy-MM-ddTHH:mm:ss"),
                    kenh = tatCa.Select(k => new { nvr = k.Nvr, tenDauGhi = k.TenDauGhi, kenh = k.Kenh, ten = k.Ten, ipCamera = k.IpCamera, online = k.Online, ketQua = k.KetQua })
                }, _jsonCamel), ct);
            }
            return (tatCa.Count, duoc);
        }

        private HttpRequestMessage YeuCauGo2rtcCongTy(string congTy, string duongDan)
        {
            var cfg = Go2rtcCongTy(congTy) ?? throw new InvalidOperationException($"Chưa cấu hình go2rtc cho {congTy}.");
            var yeuCau = new HttpRequestMessage(HttpMethod.Get, cfg.url + duongDan);
            yeuCau.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Basic",
                Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(cfg.taiKhoan + ":" + cfg.matKhau)));
            return yeuCau;
        }

        private static string TenLuongCongTy(string congTy, string nvrIp, int kenh)
            => $"{congTy.ToLowerInvariant()}_{nvrIp.Replace('.', '_')}_{kenh}";

        /// <summary>1 ảnh JPEG trực tiếp của kênh PFVN/MEGA qua go2rtc bên công ty đó (go2rtc giải mã 1 khung hình).</summary>
        public async Task<byte[]?> LayAnhChupCongTyAsync(string congTy, string nvrIp, int kenh, CancellationToken ct)
        {
            // Đầu ghi gọi thẳng được (MEGA) -> ISAPI, không qua go2rtc
            if (DauGhiTrucTiepCongTy(_configuration, congTy).TryGetValue(nvrIp, out var dg))
                return await ChupIsapiAsync(dg, kenh, ct);

            // Client go2rtc không giới hạn thời gian (dùng chung cho video) -> tự đặt 20 giây cho ảnh
            using var hetGio = CancellationTokenSource.CreateLinkedTokenSource(ct);
            hetGio.CancelAfter(TimeSpan.FromSeconds(20));
            var client = _httpClientFactory.CreateClient(TenClientGo2rtc);
            using var yeuCau = YeuCauGo2rtcCongTy(congTy, $"/api/frame.jpeg?src={Uri.EscapeDataString(TenLuongCongTy(congTy, nvrIp, kenh))}");
            using var traLoi = await client.SendAsync(yeuCau, hetGio.Token);
            return traLoi.IsSuccessStatusCode ? await traLoi.Content.ReadAsByteArrayAsync(hetGio.Token) : null;
        }

        /// <summary>Luồng fMP4 (H.264) của kênh PFVN/MEGA qua go2rtc bên công ty đó. Người gọi Dispose response.</summary>
        public async Task<HttpResponseMessage?> MoLuongVideoCongTyAsync(string congTy, string nvrIp, int kenh, CancellationToken ct)
        {
            var client = _httpClientFactory.CreateClient(TenClientGo2rtc);
            using var yeuCau = YeuCauGo2rtcCongTy(congTy,
                $"/api/stream.mp4?src={Uri.EscapeDataString(TenLuongCongTy(congTy, nvrIp, kenh))}&video=h264");
            var traLoi = await client.SendAsync(yeuCau, HttpCompletionOption.ResponseHeadersRead, ct);
            if (traLoi.IsSuccessStatusCode) return traLoi;
            traLoi.Dispose();
            return null;
        }

        /// <summary>Thư mục ảnh lưu của PFVN/MEGA: thư mục con "{công ty}" trong CameraNvr:ThuMucAnhLuu.</summary>
        private string? ThuMucAnhLuuCongTy(string congTy)
        {
            var thuMuc = _configuration["CameraNvr:ThuMucAnhLuu"];
            var ten = DsCongTyAnhLuu.FirstOrDefault(x => x.Equals(congTy, StringComparison.OrdinalIgnoreCase));
            return string.IsNullOrWhiteSpace(thuMuc) || ten == null ? null : Path.Combine(thuMuc, ten);
        }

        /// <summary>
        /// Danh sách kênh camera của PFVN/MEGA kèm giờ ảnh lưu. Script chup-anh-camera-pfvn.ps1 (chạy ở máy
        /// thông mạng đầu ghi bên đó) ghi "_kenh.json"; máy dev kéo về rồi đẩy lên máy chủ. Trả (null, rỗng)
        /// khi công ty chưa có dữ liệu — trang hiện "chưa kết nối".
        /// </summary>
        public async Task<(DateTime? capNhat, List<KenhCongTy> kenh)> DanhSachKenhCongTyAsync(string congTy, CancellationToken ct)
        {
            var thuMuc = ThuMucAnhLuuCongTy(congTy);
            if (thuMuc == null) return (null, new List<KenhCongTy>());
            var (capNhat, ds) = await DanhSachKenhLuotChupAsync(thuMuc, ct);

            // Trạng thái 5 phút/lần (_trang-thai.json) mới hơn lượt chụp ảnh thì lấy trạng thái đó; kênh mới thêm
            // trên đầu ghi (chưa có trong lượt chụp) vẫn hiện, chỉ là chưa có ảnh
            var (gioTrangThai, dsTrangThai) = await TrangThaiCongTyAsync(congTy, ct);
            if (gioTrangThai != null && (capNhat == null || gioTrangThai > capNhat))
            {
                var theoKhoa = ds.ToDictionary(x => (x.Nvr, x.Kenh));
                foreach (var t in dsTrangThai)
                {
                    if (theoKhoa.TryGetValue((t.Nvr, t.Kenh), out var k))
                    {
                        k.Online = t.Online;
                        if (!string.IsNullOrWhiteSpace(t.Ten)) k.Ten = t.Ten;
                    }
                    else ds.Add(t);
                }
                capNhat = gioTrangThai;
            }

            foreach (var k in ds)
            {
                var anh = Path.Combine(thuMuc!, $"{k.Nvr}_{k.Kenh}.jpg");
                k.AnhLuc = System.Net.IPAddress.TryParse(k.Nvr, out _) && File.Exists(anh) ? File.GetLastWriteTime(anh) : null;
            }
            return (capNhat, ds.OrderBy(x => x.Nvr).ThenBy(x => x.Kenh).ToList());
        }

        /// <summary>Danh sách kênh theo lượt chụp ảnh gần nhất ("_kenh.json" do script chụp ảnh ghi).</summary>
        private static async Task<(DateTime? capNhat, List<KenhCongTy> kenh)> DanhSachKenhLuotChupAsync(string thuMuc, CancellationToken ct)
        {
            var file = Path.Combine(thuMuc, "_kenh.json");
            if (!File.Exists(file)) return (null, new List<KenhCongTy>());

            await using var luong = File.OpenRead(file);
            using var doc = await JsonDocument.ParseAsync(luong, cancellationToken: ct);
            var goc = doc.RootElement;

            DateTime? capNhat = goc.TryGetProperty("capNhat", out var cn) && DateTime.TryParse(cn.GetString(), out var gio) ? gio : null;
            var ds = goc.TryGetProperty("kenh", out var dsKenh) && dsKenh.ValueKind == JsonValueKind.Array
                ? dsKenh.Deserialize<List<KenhCongTy>>(new JsonSerializerOptions { PropertyNameCaseInsensitive = true }) ?? new()
                : new List<KenhCongTy>();
            return (capNhat, ds);
        }

        /// <summary>
        /// Trạng thái online/offline mới nhất của PFVN/MEGA ("_trang-thai.json", máy dev đẩy lên 5 phút/lần).
        /// Kênh của đầu ghi không trả lời lượt đó coi là mất kết nối. Chưa có file thì trả (null, rỗng).
        /// </summary>
        public async Task<(DateTime? capNhat, List<KenhCongTy> kenh)> TrangThaiCongTyAsync(string congTy, CancellationToken ct)
        {
            var thuMuc = ThuMucAnhLuuCongTy(congTy);
            var file = thuMuc == null ? null : Path.Combine(thuMuc, "_trang-thai.json");
            if (file == null || !File.Exists(file)) return (null, new List<KenhCongTy>());

            await using var luong = File.OpenRead(file);
            using var doc = await JsonDocument.ParseAsync(luong, cancellationToken: ct);
            var goc = doc.RootElement;
            var tuyChon = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };

            DateTime? capNhat = goc.TryGetProperty("capNhat", out var cn) && DateTime.TryParse(cn.GetString(), out var gio) ? gio : null;
            var ds = goc.TryGetProperty("kenh", out var dsKenh) && dsKenh.ValueKind == JsonValueKind.Array
                ? dsKenh.Deserialize<List<KenhCongTy>>(tuyChon) ?? new()
                : new List<KenhCongTy>();

            // Đầu ghi lỗi lượt này: script không liệt kê được kênh của nó -> lấy kênh từ lượt chụp ảnh, đánh mất kết nối
            if (goc.TryGetProperty("dauGhi", out var dsDauGhi) && dsDauGhi.ValueKind == JsonValueKind.Array)
            {
                var loi = dsDauGhi.EnumerateArray()
                    .Where(d => d.TryGetProperty("loi", out var l) && l.ValueKind == JsonValueKind.String)
                    .Select(d => d.GetProperty("nvr").GetString())
                    .ToHashSet();
                if (loi.Count > 0)
                {
                    var (_, theoAnh) = await DanhSachKenhLuotChupAsync(thuMuc!, ct);
                    ds.AddRange(theoAnh.Where(x => loi.Contains(x.Nvr)).Select(x => { x.Online = false; return x; }));
                }
            }
            return (capNhat, ds);
        }

        /// <summary>
        /// IP đầu ghi -> địa chỉ gốc web ("https://10.0.200.251", "http://10.0.200.252:8005") của PFVN/MEGA, lấy từ
        /// "_trang-thai.json" (script bên đó đọc từ .env, không có mật khẩu). Chỉ nhận đúng dạng http(s)://IPv4[:cổng].
        /// </summary>
        public async Task<Dictionary<string, string>> DiaChiDauGhiCongTyAsync(string congTy, CancellationToken ct)
        {
            var kq = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            var thuMuc = ThuMucAnhLuuCongTy(congTy);
            var file = thuMuc == null ? null : Path.Combine(thuMuc, "_trang-thai.json");
            if (file == null || !File.Exists(file)) return kq;

            await using var luong = File.OpenRead(file);
            using var doc = await JsonDocument.ParseAsync(luong, cancellationToken: ct);
            if (!doc.RootElement.TryGetProperty("dauGhi", out var ds) || ds.ValueKind != JsonValueKind.Array) return kq;

            foreach (var d in ds.EnumerateArray())
            {
                var ip = d.TryGetProperty("nvr", out var n) ? n.GetString() : null;
                var goc = d.TryGetProperty("goc", out var g) ? g.GetString()?.TrimEnd('/') : null;
                if (ip != null && goc != null
                    && System.Text.RegularExpressions.Regex.IsMatch(goc, @"^https?://\d{1,3}(\.\d{1,3}){3}(:\d{1,5})?$"))
                    kq[ip] = goc;
            }
            return kq;
        }

        /// <summary>Tên đầu ghi hiển thị: tên mặc định ("Network Video Recorder", "DeepinMind") thì đặt theo công ty + đuôi IP.</summary>
        public static string TenDauGhiCongTy(string congTy, string ip, string? ten)
            => string.IsNullOrWhiteSpace(ten) || ten is "Network Video Recorder" or "DeepinMind"
                ? $"{congTy} .{ip.Split('.').Last()}"
                : ten;

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
                    return cong;
                }
                catch (Exception ex) when (ex is HttpRequestException || ex is System.Xml.XmlException
                    || (ex is TaskCanceledException && !ct.IsCancellationRequested))
                {
                    // cổng này không trả lời -> thử cổng kế tiếp
                }
            }
            // Không hỏi được (máy chủ không thông ISAPI đầu ghi): nhớ cổng mặc định 10 phút, khỏi lần nào cũng chờ hết giờ
            _cache.Set(khoa, cong, TimeSpan.FromMinutes(10));
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
                using var yeuCau = YeuCauGo2rtc(HttpMethod.Get, $"{go2rtc}/api/stream.mp4?src={Uri.EscapeDataString(ten)}&video=h264");
                var traLoi = await client.SendAsync(yeuCau, HttpCompletionOption.ResponseHeadersRead, ct);
                if (traLoi.IsSuccessStatusCode) return traLoi;

                traLoi.Dispose();
                _daKhai.TryRemove(ten, out _);
            }

            return null;
        }

        /// <summary>
        /// Ảnh trực tiếp qua go2rtc (go2rtc giải mã 1 khung hình) — dùng khi máy chủ E-Form không gọi thẳng được
        /// ISAPI đầu ghi nhưng go2rtc (10.0.60.238) thì được: bật bằng CameraNvr__AnhQuaGo2rtc=true.
        /// </summary>
        private async Task<byte[]?> LayAnhQuaGo2rtcAsync(string nvrIp, int kenh, CancellationToken ct)
        {
            var go2rtc = DiaChiGo2rtc();
            var client = _httpClientFactory.CreateClient(TenClientGo2rtc);
            var ten = $"nvr_{nvrIp.Replace('.', '_')}_{kenh}";
            // Client go2rtc không giới hạn thời gian (dùng chung cho video) -> tự đặt 20 giây cho ảnh
            using var hetGio = CancellationTokenSource.CreateLinkedTokenSource(ct);
            hetGio.CancelAfter(TimeSpan.FromSeconds(20));

            for (var lan = 0; lan < 2; lan++)
            {
                if (lan > 0 || !_daKhai.ContainsKey(ten))
                {
                    await KhaiLuongAsync(client, go2rtc, ten, nvrIp, kenh, hetGio.Token);
                }

                using var yeuCau = YeuCauGo2rtc(HttpMethod.Get, $"{go2rtc}/api/frame.jpeg?src={Uri.EscapeDataString(ten)}");
                using var traLoi = await client.SendAsync(yeuCau, hetGio.Token);
                if (traLoi.IsSuccessStatusCode) return await traLoi.Content.ReadAsByteArrayAsync(hetGio.Token);
                _daKhai.TryRemove(ten, out _);
            }
            return null;
        }

        private async Task KhaiLuongAsync(HttpClient client, string go2rtc, string ten, string nvrIp, int kenh, CancellationToken ct)
        {
            var (taiKhoan, matKhau) = TaiKhoanNvr();
            // Cổng RTSP theo từng đầu ghi (10.0.28.254 dùng 8002), không phải lúc nào cũng 554
            var cong = await CongRtspAsync(nvrIp, ct);
            var rtsp = $"rtsp://{Uri.EscapeDataString(taiKhoan)}:{Uri.EscapeDataString(matKhau)}@{nvrIp}:{cong}/Streaming/Channels/{kenh}02";

            // Hai nguồn: RTSP gốc + bản ffmpeg chuyển H.264. Luồng phụ nhiều camera BPVN là H.265, Chrome/Edge
            // không có giải mã phần cứng thì không phát được. go2rtc chỉ bật ffmpeg khi nguồn gốc không phải H.264.
            var ffmpeg = $"ffmpeg:{ten}#video=h264";
            using var yeuCau = YeuCauGo2rtc(HttpMethod.Put,
                $"{go2rtc}/api/streams?name={Uri.EscapeDataString(ten)}&src={Uri.EscapeDataString(rtsp)}&src={Uri.EscapeDataString(ffmpeg)}");
            using var traLoi = await client.SendAsync(yeuCau, ct);
            if (!traLoi.IsSuccessStatusCode)
                throw new InvalidOperationException($"go2rtc từ chối khai luồng (HTTP {(int)traLoi.StatusCode}).");

            _daKhai[ten] = true;
        }

        /// <summary>
        /// Request tới go2rtc của BPVN; go2rtc đặt ở máy khác (10.0.60.238) thì có tài khoản API
        /// (CameraNvr__Go2rtcTaiKhoan / __Go2rtcMatKhau). go2rtc cùng máy (127.0.0.1) thì để trống.
        /// </summary>
        private HttpRequestMessage YeuCauGo2rtc(HttpMethod phuongThuc, string url)
        {
            var yeuCau = new HttpRequestMessage(phuongThuc, url);
            var tk = _configuration["CameraNvr:Go2rtcTaiKhoan"];
            var mk = _configuration["CameraNvr:Go2rtcMatKhau"];
            if (!string.IsNullOrWhiteSpace(tk) && !string.IsNullOrWhiteSpace(mk))
            {
                yeuCau.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Basic",
                    Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(tk + ":" + mk)));
            }
            return yeuCau;
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
