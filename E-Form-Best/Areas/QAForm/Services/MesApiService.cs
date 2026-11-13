using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace E_Form_Best.Areas.QAForm.Services
{
    /// <summary>
    /// Đọc dữ liệu đóng gói từ NH-MES (nhmes.bestpacific.cn) — API POST {BaseUrl}/ePackBatch/report2List,
    /// chính là API trang MES "订单明细" dùng khi bấm 查询.
    ///
    /// MES không có token API: phải đăng nhập SSO OA (Landray EKP, oa.bestpacific.cn) bằng tài khoản kỹ thuật,
    /// giữ cookie phiên trong service singleton; phiên hết hạn (API trả HTML trang login) thì đăng nhập lại 1 lần rồi gọi lại.
    ///
    /// Cấu hình (.env): MesApi__User, MesApi__Pass (tài khoản OA chỉ-đọc MES),
    /// MesApi__BaseUrl (mặc định https://nhmes.bestpacific.cn/nhmes), MesApi__SsoUrl (mặc định https://oa.bestpacific.cn),
    /// MesApi__FactoryCode (mặc định BPVN).
    /// </summary>
    public class MesApiService : IDisposable
    {
        public record DongDongGoi(
            string? EndCustCode, string? OrderUcode, string? CustCode, string? ArtCode, string? ColorCode,
            decimal? PmcNum, decimal? PbNum, decimal? PbNw, string? Remark, decimal? PbBoxnum, string? PbLv,
            string? PbQcdes, string? HandleMethod, string? ManuStep, string? ClNo, string? GetDate,
            string? PbDate, string? PackclNo, string? PbGh, string? DyeMachineNo)
        {
            // SHA-256 của JSON dòng MES — MES không có id dòng nên dùng cái này để nhận lại dòng khi bỏ vào thùng
            public string DauVanTay { get; init; } = "";

            // Nguyên JSON dòng MES (lưu vào QA_ThungDong.du_lieu_mes), không trả ra trình duyệt
            [System.Text.Json.Serialization.JsonIgnore]
            public string Raw { get; init; } = "";

            // Mã thùng đã chứa dòng này (điền ở controller), null = chưa vào thùng
            public string? MaThung { get; set; }

            // id dòng QA_ThungDong tương ứng (để chuyển thùng / bỏ ra), và thùng đó đã đóng chưa
            public int? IdDongThung { get; set; }
            public bool ThungDaDong { get; set; }
        }

        public record KetQuaTraCuu(bool ThanhCong, string ThongBao, int Tong = 0, List<DongDongGoi>? Dong = null);

        public static string TinhDauVanTay(string raw)
            => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(raw))).ToLowerInvariant();

        private static (int tong, List<DongDongGoi> dong) DocTrangMes(string json)
        {
            using var doc = JsonDocument.Parse(json);
            var goc = doc.RootElement;
            int tong = goc.TryGetProperty("total", out var t) && t.ValueKind == JsonValueKind.Number ? t.GetInt32() : 0;
            var dong = new List<DongDongGoi>();
            if (goc.TryGetProperty("rows", out var rows) && rows.ValueKind == JsonValueKind.Array)
            {
                foreach (var el in rows.EnumerateArray())
                {
                    var raw = el.GetRawText();
                    var d = el.Deserialize<DongDongGoi>(JsonOpt)!;
                    dong.Add(d with { Raw = raw, DauVanTay = TinhDauVanTay(raw) });
                }
            }
            return (tong, dong);
        }

        internal static readonly JsonSerializerOptions JsonOpt = new() { PropertyNameCaseInsensitive = true, NumberHandling = System.Text.Json.Serialization.JsonNumberHandling.AllowReadingFromString };

        private readonly IConfiguration _configuration;
        private readonly ILogger<MesApiService> _logger;
        private readonly CookieContainer _cookies = new();
        private readonly HttpClient _http;
        // Chỉ 1 luồng đăng nhập SSO tại một thời điểm, tránh nhiều request cùng lúc đăng nhập chồng nhau làm hỏng phiên.
        private readonly SemaphoreSlim _khoaDangNhap = new(1, 1);

        public MesApiService(IConfiguration configuration, ILogger<MesApiService> logger)
        {
            _configuration = configuration;
            _logger = logger;
            // Tự đi theo redirect: luồng SSO nhảy https (OA) -> http (nhmes/shiro-cas) mà HttpClient không tự theo khi hạ https->http.
            var handler = new SocketsHttpHandler
            {
                CookieContainer = _cookies,
                UseCookies = true,
                AllowAutoRedirect = false,
                PooledConnectionLifetime = TimeSpan.FromMinutes(10)
            };
            _http = new HttpClient(handler) { Timeout = TimeSpan.FromSeconds(40) };
            _http.DefaultRequestHeaders.UserAgent.ParseAdd("Mozilla/5.0 (Windows NT 10.0; Win64; x64) E-Form-QA");
        }

        private string BaseUrl => (_configuration["MesApi:BaseUrl"] ?? "https://nhmes.bestpacific.cn/nhmes").Trim().TrimEnd('/');
        private string SsoUrl => (_configuration["MesApi:SsoUrl"] ?? "https://oa.bestpacific.cn").Trim().TrimEnd('/');
        private string FactoryCode => (_configuration["MesApi:FactoryCode"] ?? "BPVN").Trim();

        public bool CoCauHinh => !string.IsNullOrWhiteSpace(_configuration["MesApi:User"]) && !string.IsNullOrWhiteSpace(_configuration["MesApi:Pass"]);

        // Mỗi lần gọi MES lấy tối đa 1000 dòng; trần tổng để 1 mã quá rộng (vd "P26") không kéo cả kho về.
        private const int DongMoiLanGoi = 1000;
        public const int TranTongDong = 10000;

        /// <summary>
        /// Lấy TẤT CẢ dòng của 配色单号 (gộp nhiều trang MES ở server) để trình duyệt khỏi phải chuyển trang
        /// — mỗi lần chuyển trang trước đây là 1 vòng gọi MES ~1 giây.
        /// </summary>
        public Task<KetQuaTraCuu> TraTatCaTheoPhoiMauAsync(string orderUcode, CancellationToken ct)
            => TraTatCaAsync((trang, soDong) => TaoBody(orderUcode, "", "", trang, soDong), TranTongDong, ct);

        // Trần 1 ngày đóng gói khi đồng bộ về DB — rộng hơn trần tra cứu vì cần đủ cả ngày
        public const int TranDongMotNgay = 50000;

        /// <summary>
        /// Tất cả dòng có ngày đóng gói (pbDate) = ngay — dùng cho job đồng bộ MES về QA_MesDongGoi.
        /// Kq.Tong > Kq.Dong.Count nghĩa là chưa lấy đủ (vượt trần), nơi gọi không được coi là bản đầy đủ của ngày.
        /// </summary>
        public Task<KetQuaTraCuu> TraTatCaTheoNgayAsync(DateOnly ngay, CancellationToken ct)
        {
            var s = ngay.ToString("yyyy-MM-dd");
            return TraTatCaAsync((trang, soDong) => TaoBody("", s, s, trang, soDong), TranDongMotNgay, ct);
        }

        private async Task<KetQuaTraCuu> TraTatCaAsync(Func<int, int, string> taoBody, int tran, CancellationToken ct)
        {
            var dau = await GoiReportAsync(taoBody(1, DongMoiLanGoi), ct);
            if (!dau.ThanhCong || dau.Tong <= DongMoiLanGoi) return dau;

            var tatCa = new List<DongDongGoi>(Math.Min(dau.Tong, tran));
            tatCa.AddRange(dau.Dong!);
            for (int trang = 2; tatCa.Count < Math.Min(dau.Tong, tran); trang++)
            {
                var kq = await GoiReportAsync(taoBody(trang, DongMoiLanGoi), ct);
                if (!kq.ThanhCong) return kq;
                if (kq.Dong == null || kq.Dong.Count == 0) break;
                tatCa.AddRange(kq.Dong);
            }
            if (tatCa.Count > tran) tatCa.RemoveRange(tran, tatCa.Count - tran);
            return new(true, "", dau.Tong, tatCa);
        }

        /// <summary>Tra theo 配色单号 (orderUcode), MES so khớp gần đúng (isLike=like) giống ô tìm trên trang MES.</summary>
        public Task<KetQuaTraCuu> TraTheoPhoiMauAsync(string orderUcode, int trang, int soDong, CancellationToken ct)
            => GoiReportAsync(TaoBody(orderUcode, "", "", trang, soDong), ct);

        private string TaoBody(string orderUcode, string pbDateStart, string pbDateEnd, int trang, int soDong)
            => JsonSerializer.Serialize(new
            {
                pageNumber = trang,
                pageSize = soDong,
                factoryCode = FactoryCode,
                status = "",
                pbLvList = Array.Empty<string>(),
                manuStepList = Array.Empty<string>(),
                pbDateStart,
                pbDateEnd,
                orderUcode,
                isLike = "like",
                orgFilterValue = (string?)null,
                firstSearch = false,
                sortOrder = "asc"
            });

        private async Task<KetQuaTraCuu> GoiReportAsync(string body, CancellationToken ct)
        {
            if (!CoCauHinh) return new(false, "Chưa cấu hình tài khoản MES (MesApi__User / MesApi__Pass trong .env).");

            try
            {
                for (int lan = 0; lan < 2; lan++)
                {
                    var (ok, json) = await GoiApiAsync("/ePackBatch/report2List", body, ct);
                    if (ok)
                    {
                        var (tong, dong) = DocTrangMes(json!);
                        return new(true, "", tong, dong);
                    }
                    // Không phải JSON = phiên MES đã hết -> đăng nhập lại rồi thử thêm 1 lần.
                    if (lan == 0)
                    {
                        var loi = await DangNhapAsync(ct);
                        if (loi != null) return new(false, loi);
                    }
                }
                return new(false, "MES không trả dữ liệu sau khi đăng nhập lại.");
            }
            catch (OperationCanceledException) when (!ct.IsCancellationRequested)
            {
                return new(false, "MES phản hồi quá lâu, thử lại sau.");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Lỗi gọi MES report2List");
                return new(false, "Không kết nối được MES: " + ex.Message);
            }
        }

        private async Task<(bool ok, string? json)> GoiApiAsync(string duongDan, string body, CancellationToken ct)
        {
            using var req = new HttpRequestMessage(HttpMethod.Post, BaseUrl + duongDan)
            {
                Content = new StringContent(body, Encoding.UTF8, "application/json")
            };
            req.Headers.Add("X-Requested-With", "XMLHttpRequest");
            req.Headers.Accept.ParseAdd("application/json");
            using var res = await _http.SendAsync(req, ct);
            if (res.StatusCode != HttpStatusCode.OK) return (false, null);
            var text = await res.Content.ReadAsStringAsync(ct);
            return text.TrimStart().StartsWith('{') ? (true, text) : (false, null);
        }

        /// <summary>Đăng nhập SSO OA giống trình duyệt. Trả null nếu thành công, ngược lại là thông báo lỗi.</summary>
        private async Task<string?> DangNhapAsync(CancellationToken ct)
        {
            await _khoaDangNhap.WaitAsync(ct);
            try
            {
                var service = BaseUrl.Replace("https://", "http://") + "/shiro-cas";
                var vaoSso = $"{SsoUrl}/pro/sso/proSsoRequest.do?method=login&service={service}";
                var redirectTo = $"/pro/sso/proSsoRequest.do?method=login&service={service}";

                // 1) Mở trang login để OA cấp phiên (JSESSIONID); 2) lấy session id làm khoá mã hoá mật khẩu.
                await DiTheoAsync(new HttpRequestMessage(HttpMethod.Get, vaoSso), ct);
                using var sesRes = await _http.GetAsync($"{SsoUrl}/resource/js/session.jsp?_={DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()}", ct);
                var sesJs = await sesRes.Content.ReadAsStringAsync(ct);
                var m = Regex.Match(sesJs, "return\\s*\"([^\"]*)\"");
                if (!m.Success) return "Không lấy được phiên đăng nhập OA.";

                var form = new FormUrlEncodedContent(new Dictionary<string, string>
                {
                    ["j_username"] = _configuration["MesApi:User"]!,
                    ["j_password"] = MaHoaMatKhau(_configuration["MesApi:Pass"]!, m.Groups[1].Value),
                    ["j_redirectto"] = redirectTo
                });
                var cuoi = await DiTheoAsync(new HttpRequestMessage(HttpMethod.Post, $"{SsoUrl}/j_acegi_security_check") { Content = form }, ct);

                if (cuoi == null || cuoi.Host.StartsWith("oa.", StringComparison.OrdinalIgnoreCase) && cuoi.AbsolutePath.Contains("login", StringComparison.OrdinalIgnoreCase))
                {
                    _logger.LogWarning("Đăng nhập SSO OA cho MES thất bại, dừng ở {Url}", cuoi);
                    return "Đăng nhập OA thất bại (kiểm tra MesApi__User / MesApi__Pass).";
                }
                _logger.LogInformation("Đã đăng nhập MES qua SSO OA");
                return null;
            }
            finally
            {
                _khoaDangNhap.Release();
            }
        }

        /// <summary>Gửi request rồi tự đi theo chuỗi redirect (tối đa 10 bước). Trả URL cuối cùng.</summary>
        private async Task<Uri?> DiTheoAsync(HttpRequestMessage req, CancellationToken ct)
        {
            var hienTai = req;
            for (int i = 0; i < 10; i++)
            {
                using var res = await _http.SendAsync(hienTai, ct);
                var url = hienTai.RequestUri;
                hienTai.Dispose();
                if ((int)res.StatusCode is < 300 or >= 400 || res.Headers.Location == null) return url;
                var tiep = res.Headers.Location.IsAbsoluteUri ? res.Headers.Location : new Uri(url!, res.Headers.Location);
                hienTai = new HttpRequestMessage(HttpMethod.Get, tiep);
            }
            return null;
        }

        /// <summary>
        /// Giống desEncrypt() trong /resource/js/security.js của OA (mặc định AES):
        /// khoá = session id (thiếu 32 ký tự thì nối "a..z0..9"), viết hoa; key = 16 ký tự đầu, iv = 16 ký tự sau;
        /// AES-CBC PKCS7 trên UTF-8, base64, thêm tiền tố 䐵匠䴵 để server biết là chuỗi đã mã hoá.
        /// </summary>
        private static string MaHoaMatKhau(string matKhau, string sessionId)
        {
            var s = sessionId.Length < 32 ? sessionId + "abcdefghijklmnopqrstuvwxyz1234567890" : sessionId;
            s = s.ToUpperInvariant();
            using var aes = Aes.Create();
            aes.Key = Encoding.UTF8.GetBytes(s[..16]);
            var iv = Encoding.UTF8.GetBytes(s.Substring(16, 16));
            var maHoa = aes.EncryptCbc(Encoding.UTF8.GetBytes(matKhau), iv, PaddingMode.PKCS7);
            return "䐵匠䴵" + Convert.ToBase64String(maHoa);
        }

        public void Dispose()
        {
            _http.Dispose();
            _khoaDangNhap.Dispose();
        }
    }
}
