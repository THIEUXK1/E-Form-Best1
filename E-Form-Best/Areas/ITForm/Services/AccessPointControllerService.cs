using System.Collections.Concurrent;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using E_Form_Best.Context;
using E_Form_Best.Models.ITForm;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace E_Form_Best.Areas.ITForm.Services
{
    /// <summary>
    /// Đọc danh sách AP từ AP controller của từng công ty (chỉ ĐỌC, không gửi lệnh cấu hình nào).
    /// Cấu hình ở .env theo công ty: AccessPointController__{CÔNG TY}__Loai / __Url / __TaiKhoan / __MatKhau.
    /// Hiện hỗ trợ Huawei WLAN AC (web V200R0xx — PFVN 10.0.198.199); hãng khác thêm nhánh ở DanhSachAsync.
    ///
    /// Singleton: giữ 1 phiên đăng nhập / công ty và dùng lại giữa các lượt (AC giới hạn số phiên web,
    /// đăng nhập mỗi 2 phút sẽ đầy nhật ký AC). Phiên hết hạn thì tự đăng nhập lại 1 lần.
    /// </summary>
    public class AccessPointControllerService
    {
        public const string TenHttpClient = "ApController";

        /// <summary>Người tạo ghi trên AP do đồng bộ tự thêm, để phân biệt với AP nhập tay.</summary>
        public const string NguoiDongBo = "Đồng bộ AP controller";

        /// <summary>Một AP như controller thấy. Mac dạng "AA:BB:CC:DD:EE:FF".</summary>
        public record ApController(string Mac, string Ten, string? Ip, string? Nhom, string? Model, string? PhienBan,
            string? Serial, string TrangThai, bool Online, int? SoClient, long? ThoiGianChayGiay);

        public record KetQuaDongBo(int TongTrenController, int ThemMoi, int CapNhat);

        private readonly IHttpClientFactory _httpFactory;
        private readonly IConfiguration _configuration;
        private readonly ConcurrentDictionary<string, SemaphoreSlim> _khoa = new();
        private readonly ConcurrentDictionary<string, (string SessionId, string Token)> _phien = new();

        public AccessPointControllerService(IHttpClientFactory httpFactory, IConfiguration configuration)
        {
            _httpFactory = httpFactory;
            _configuration = configuration;
        }

        private string? CauHinh(string congTy, string khoa) => _configuration[$"AccessPointController:{congTy}:{khoa}"];

        public bool CoController(string congTy) => !string.IsNullOrWhiteSpace(CauHinh(congTy, "Url"));

        /// <summary>Tên hiển thị của controller (host trong Url), ghi vào cột "controller" của AP khi đồng bộ.</summary>
        public string? TenController(string congTy)
        {
            var url = CauHinh(congTy, "Url");
            if (string.IsNullOrWhiteSpace(url)) return null;
            var loai = CauHinh(congTy, "Loai") ?? "HuaweiAC";
            return Uri.TryCreate(url, UriKind.Absolute, out var u) ? $"{loai} {u.Host}" : url;
        }

        public async Task<List<ApController>> DanhSachAsync(string congTy, CancellationToken ct)
        {
            var loai = CauHinh(congTy, "Loai") ?? "HuaweiAC";
            if (!loai.Equals("HuaweiAC", StringComparison.OrdinalIgnoreCase))
                throw new NotSupportedException($"Chưa hỗ trợ loại controller \"{loai}\".");

            var khoa = _khoa.GetOrAdd(congTy, _ => new SemaphoreSlim(1, 1));
            await khoa.WaitAsync(ct);
            try
            {
                if (_phien.ContainsKey(congTy))
                {
                    var ds = await HuaweiDanhSachAsync(congTy, ct);
                    if (ds != null) return ds;
                }
                // Chưa có phiên hoặc phiên hết hạn: đăng nhập lại đúng 1 lần
                await HuaweiDangNhapAsync(congTy, ct);
                return await HuaweiDanhSachAsync(congTy, ct)
                       ?? throw new InvalidOperationException("Controller từ chối phiên vừa đăng nhập.");
            }
            finally { khoa.Release(); }
        }

        /// <summary>
        /// Đưa danh sách AP trên controller vào KK_AccessPoint của công ty, khoá theo MAC:
        ///  - AP đã có (cùng MAC, còn hiệu lực): ghi đè các cột controller + IP/model/serial/firmware.
        ///    Tên, bộ phận, vị trí, tình trạng... do người dùng nhập thì giữ nguyên.
        ///  - AP chưa có: thêm mới, tên = tên trên controller.
        /// Trạng thái UP/DOWN KHÔNG ghi ở đây — job theo dõi ghi để còn sinh lịch sử đổi trạng thái.
        /// </summary>
        public async Task<KetQuaDongBo> DongBoAsync(ITFormContext db, string congTy, List<ApController> dsAc, CancellationToken ct)
        {
            var idCongTy = await db.KkCongTies.Where(x => x.TenCongTy == congTy).Select(x => (int?)x.IdcongTy).FirstOrDefaultAsync(ct)
                           ?? throw new InvalidOperationException($"Chưa có công ty {congTy} trong danh mục công ty.");
            var tenController = TenController(congTy);
            var dsMac = dsAc.Select(x => x.Mac).ToList();

            // Unique MAC ở DB tính trên mọi công ty: AP chuyển công ty thì không tự cướp, để người dùng xử lý
            var dsDb = await db.KkAccessPoints
                .Where(x => x.NgayXoa == null && x.DiaChiMac != null && dsMac.Contains(x.DiaChiMac))
                .ToListAsync(ct);
            var theoMac = dsDb.ToDictionary(x => x.DiaChiMac!, StringComparer.OrdinalIgnoreCase);

            // IP đang được AP khác (nhập tay, chưa có MAC) giữ thì không ghi IP, tránh nổ unique (công ty, IP)
            var ipDaDung = await db.KkAccessPoints
                .Where(x => x.NgayXoa == null && x.IdcongTy == idCongTy && x.DiaChiIp != null
                            && (x.DiaChiMac == null || !dsMac.Contains(x.DiaChiMac)))
                .Select(x => x.DiaChiIp!).ToListAsync(ct);
            var ipBan = new HashSet<string>(ipDaDung);

            var bayGio = DateTime.Now;
            int them = 0, capNhat = 0;
            foreach (var ac in dsAc)
            {
                var ip = ac.Ip != null && !ipBan.Contains(ac.Ip) ? ac.Ip : null;
                if (theoMac.TryGetValue(ac.Mac, out var ap))
                {
                    if (ap.IdcongTy != idCongTy) continue;

                    // IP do AC cấp lại (DHCP) thì cập nhật; đổi IP thì trạng thái cũ không còn đúng như khi sửa tay
                    if (ip != null && ap.DiaChiIp != ip)
                    {
                        ap.DiaChiIp = ip;
                        ap.TrangThaiKetNoi = null;
                        ap.DoiTrangThaiLuc = null;
                    }
                    ap.Model = ac.Model ?? ap.Model;
                    ap.Serial = ac.Serial ?? ap.Serial;
                    ap.HangSanXuat ??= "Huawei";
                    ap.TenController = tenController;
                    GhiCotController(ap, ac);
                    if (db.Entry(ap).State == EntityState.Modified)
                    {
                        ap.NgayCapNhat = bayGio;
                        capNhat++;
                    }
                }
                else
                {
                    var moi = new KkAccessPoint
                    {
                        TenAp = Cat(ac.Ten, 255)!,
                        DiaChiIp = ip,
                        DiaChiMac = ac.Mac,
                        HangSanXuat = "Huawei",
                        Model = Cat(ac.Model, 100),
                        Serial = Cat(ac.Serial, 100),
                        TenController = tenController,
                        IdcongTy = idCongTy,
                        TinhTrang = "Đang hoạt động",
                        NguoiTao = NguoiDongBo,
                        NgayTao = bayGio
                    };
                    GhiCotController(moi, ac);
                    db.KkAccessPoints.Add(moi);
                    if (ip != null) ipBan.Add(ip);
                    them++;
                }
            }

            try
            {
                await db.SaveChangesAsync(ct);
            }
            catch (DbUpdateException ex) when (ex.InnerException is SqlException sql && (sql.Number == 2601 || sql.Number == 2627))
            {
                // Máy chủ kia vừa đồng bộ cùng lúc: bỏ lượt này, lượt sau đọc lại là khớp
                db.ChangeTracker.Clear();
                return new KetQuaDongBo(dsAc.Count, 0, 0);
            }
            return new KetQuaDongBo(dsAc.Count, them, capNhat);
        }

        private static void GhiCotController(KkAccessPoint ap, ApController ac)
        {
            ap.TrangThaiController = Cat(ac.TrangThai, 30);
            ap.NhomAp = Cat(ac.Nhom, 100);
            ap.PhienBan = Cat(ac.PhienBan, 100);
            ap.SoClient = ac.SoClient;
            ap.ThoiGianChayGiay = ac.ThoiGianChayGiay;
        }

        // ---------------- Huawei WLAN AC ----------------

        private string GocUrl(string congTy)
            => (CauHinh(congTy, "Url") ?? throw new InvalidOperationException($"Chưa cấu hình AccessPointController__{congTy}__Url.")).TrimEnd('/');

        /// <summary>Header giống trình duyệt: AC chặn request thiếu Origin/Referer bằng mã 450 "Return Login Page".</summary>
        private static HttpRequestMessage TaoRequest(HttpMethod method, string goc, string duongDan, string? sessionId, string? token)
        {
            var req = new HttpRequestMessage(method, goc + duongDan);
            req.Headers.TryAddWithoutValidation("Origin", goc);
            req.Headers.TryAddWithoutValidation("Referer", goc + "/view/main/default.html");
            req.Headers.TryAddWithoutValidation("X-Requested-With", "XMLHttpRequest");
            if (sessionId != null) req.Headers.TryAddWithoutValidation("Cookie", "SessionID=" + sessionId);
            if (token != null) req.Headers.TryAddWithoutValidation("Token", token);
            return req;
        }

        private static StringContent NoiDungForm(string body)
        {
            var c = new StringContent(body, Encoding.UTF8);
            c.Headers.Remove("Content-Type");
            c.Headers.TryAddWithoutValidation("Content-Type", "application/x-www-form-urlencoded; text/xml; charset=utf-8");
            return c;
        }

        private async Task HuaweiDangNhapAsync(string congTy, CancellationToken ct)
        {
            _phien.TryRemove(congTy, out _);
            var goc = GocUrl(congTy);
            var http = _httpFactory.CreateClient(TenHttpClient);

            // Web AC gửi mật khẩu NGUYÊN VĂN, không urlencode (login.js nối chuỗi "UserName=..&Password=..")
            // nên mật khẩu có ký tự '&' sẽ không đăng nhập được.
            var taiKhoan = CauHinh(congTy, "TaiKhoan") ?? "";
            var matKhau = CauHinh(congTy, "MatKhau") ?? "";
            using (var req = TaoRequest(HttpMethod.Post, goc, "/login.cgi", null, null))
            {
                req.Headers.Remove("Referer");
                req.Headers.TryAddWithoutValidation("Referer", goc + "/view/login.html");
                req.Content = NoiDungForm($"UserName={taiKhoan}&Password={matKhau}&LanguageType=0");
                using var res = await http.SendAsync(req, ct);
                var body = (await res.Content.ReadAsStringAsync(ct)).Trim();
                if (body.StartsWith("ErrorMsg=", StringComparison.Ordinal))
                    throw new InvalidOperationException($"Controller từ chối đăng nhập ({body}).");

                // Mã đầu: 0/4/6 = vào thẳng trang chính; mã khác = AC bắt đổi mật khẩu
                var ma = body.Split('&')[0];
                if (ma != "0" && ma != "4" && ma != "6")
                    throw new InvalidOperationException($"Tài khoản controller cần đổi mật khẩu trên web AC (mã {ma}).");

                var sessionId = res.Headers.TryGetValues("Set-Cookie", out var cookies)
                    ? cookies.Select(c => Regex.Match(c, @"SessionID=([^;]+)")).Where(m => m.Success).Select(m => m.Groups[1].Value).FirstOrDefault()
                    : null;
                if (string.IsNullOrEmpty(sessionId))
                    throw new InvalidOperationException("Controller không trả SessionID sau khi đăng nhập.");

                // Token chống CSRF nằm trong biến tTag của trang chính, mọi lời gọi config.cgi phải kèm
                using var reqTrang = TaoRequest(HttpMethod.Get, goc, "/view/main/default.html?language=en", sessionId, null);
                using var resTrang = await http.SendAsync(reqTrang, ct);
                var html = await resTrang.Content.ReadAsStringAsync(ct);
                var token = Regex.Match(html, "tTag\\s*=\\s*\"([^\"]+)\"").Groups[1].Value;
                if (string.IsNullOrEmpty(token))
                    throw new InvalidOperationException("Không đọc được token phiên từ trang chính của controller.");

                _phien[congTy] = (sessionId, token);
            }
        }

        private const string ViTriBangAp =
            "iso.org.dod.internet.private.enterprises.huawei.huaweiUtility.hwWlan.hwWlanAp.hwWlanApObjects.hwWlanIDIndexedApTable";

        // Trang tối đa mỗi lần đọc; AC trả đúng bằng số này thì còn trang sau (cách web AC phân trang)
        private const int SoDongMoiTrang = 300;

        /// <summary>Đọc bảng hwWlanIDIndexedApTable. Trả null khi phiên hết hạn (để đăng nhập lại).</summary>
        private async Task<List<ApController>?> HuaweiDanhSachAsync(string congTy, CancellationToken ct)
        {
            if (!_phien.TryGetValue(congTy, out var phien)) return null;
            var goc = GocUrl(congTy);
            var http = _httpFactory.CreateClient(TenHttpClient);
            XNamespace ns = "urn:ietf:params:xml:ns:netconf:base:1.0";

            var ketQua = new List<ApController>();
            string? apIdSau = null;
            for (var trang = 0; trang < 50; trang++)
            {
                var tiepTu = apIdSau == null ? "" : $" hwWlanIDIndexedApId=\"{apIdSau}\"";
                var loc = $"<hwWlanIDIndexedApEntry position=\"{ViTriBangAp}\" number=\"{SoDongMoiTrang}\"{tiepTu}>"
                        + "<hwWlanIDIndexedApId/><hwWlanIDIndexedApName/><hwWlanIDIndexedApMac/><hwWlanIDIndexedApGroup/>"
                        + "<hwWlanIDIndexedApIpAddress/><hwWlanIDIndexedApTypeInfo/><hwWlanIDIndexedApSoftwareVersion/>"
                        + "<hwWlanIDIndexedApSn/><hwWlanIDIndexedApOnlineUserNum/><hwWlanIDIndexedApOnlineTime/>"
                        + "<hwWlanIDIndexedApRunState/></hwWlanIDIndexedApEntry>";
                var body = $"htmlID=1000&MessageID={trang + 1}&<rpc message-id=\"{trang + 1}\" xmlns=\"{ns.NamespaceName}\">\n"
                         + $"<get><filter type=\"subtree\"><featurename istop=\"true\" type=\"mib\">{loc}</featurename></filter></get></rpc>]]>]]>";

                using var req = TaoRequest(HttpMethod.Post, goc, "/config.cgi", phien.SessionId, phien.Token);
                req.Content = NoiDungForm(body);
                using var res = await http.SendAsync(req, ct);

                // Phiên chết: AC chuyển hướng về login.html hoặc trả 400/403/450 hoặc ErrorMsg=1016
                if ((int)res.StatusCode is >= 300 and < 400 or 400 or 403 or 450) { _phien.TryRemove(congTy, out _); return null; }
                res.EnsureSuccessStatusCode();
                var xml = await res.Content.ReadAsStringAsync(ct);
                if (xml.TrimStart().StartsWith("ErrorMsg=", StringComparison.Ordinal)) { _phien.TryRemove(congTy, out _); return null; }

                var doc = XDocument.Parse(xml);
                var loi = doc.Descendants(ns + "error-message").FirstOrDefault();
                if (loi != null) throw new InvalidOperationException("Controller báo lỗi: " + loi.Value.Trim());

                var dsDong = doc.Descendants(ns + "hwWlanIDIndexedApEntry").ToList();
                foreach (var e in dsDong)
                {
                    string? G(string ten) { var v = e.Element(ns + ten)?.Value.Trim(); return string.IsNullOrEmpty(v) ? null : v; }
                    var mac = ChuanHoaMac(G("hwWlanIDIndexedApMac"));
                    if (mac == null || mac == "FF:FF:FF:FF:FF:FF") continue;

                    var ip = G("hwWlanIDIndexedApIpAddress");
                    if (ip == "0.0.0.0" || !IPAddress.TryParse(ip ?? "", out _)) ip = null;
                    int.TryParse(G("hwWlanIDIndexedApRunState"), out var runState);
                    ketQua.Add(new ApController(
                        mac, G("hwWlanIDIndexedApName") ?? mac, ip, G("hwWlanIDIndexedApGroup"),
                        G("hwWlanIDIndexedApTypeInfo"), G("hwWlanIDIndexedApSoftwareVersion"), G("hwWlanIDIndexedApSn"),
                        TenTrangThaiHuawei(runState), LaOnlineHuawei(runState),
                        int.TryParse(G("hwWlanIDIndexedApOnlineUserNum"), out var soClient) ? soClient : null,
                        long.TryParse(G("hwWlanIDIndexedApOnlineTime"), out var giay) ? giay : null));
                }

                if (dsDong.Count < SoDongMoiTrang) break;
                apIdSau = dsDong[^1].Element(ns + "hwWlanIDIndexedApId")?.Value.Trim();
                if (string.IsNullOrEmpty(apIdSau)) break;
            }
            return ketQua.GroupBy(x => x.Mac).Select(g => g.First()).ToList();
        }

        /// <summary>hwWlanIDIndexedApRunState (HUAWEI-WLAN-AP-MIB).</summary>
        private static string TenTrangThaiHuawei(int v) => v switch
        {
            1 => "idle", 2 => "autofind", 3 => "typeNotMatch", 4 => "fault", 5 => "config", 6 => "configFailed",
            7 => "download", 8 => "normal", 9 => "committing", 10 => "commitFailed", 11 => "standby",
            12 => "verMismatch", 13 => "nameConflicted", 14 => "invalid", 15 => "countryCodeMismatch",
            _ => "unknown(" + v + ")"
        };

        /// <summary>AP đang nối với AC: normal, hoặc đang nhận cấu hình / tải firmware (vẫn kết nối, sắp về normal).</summary>
        private static bool LaOnlineHuawei(int v) => v is 5 or 7 or 8 or 9;

        private static string? ChuanHoaMac(string? v)
        {
            if (v == null) return null;
            var hex = Regex.Replace(v, "[^0-9A-Fa-f]", "");
            return hex.Length != 12 ? null
                : string.Join(":", Enumerable.Range(0, 6).Select(i => hex.Substring(i * 2, 2))).ToUpperInvariant();
        }

        private static string? Cat(string? s, int toiDa) => s == null ? null : (s.Length > toiDa ? s[..toiDa] : s);
    }
}
