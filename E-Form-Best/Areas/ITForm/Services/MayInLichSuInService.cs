using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using E_Form_Best.Context;
using E_Form_Best.Models.ITForm;
using Microsoft.EntityFrameworkCore;

namespace E_Form_Best.Areas.ITForm.Services
{
    /// <summary>
    /// Kéo lịch sử lệnh in (ai in, tài liệu gì, bao nhiêu trang) từ trang Job History của máy in
    /// về bảng MayIn_LichSuIn. Chỉ GET, không đăng nhập, không ghi gì lên máy.
    ///
    /// Dòng Fuji Xerox CentreWare (DocuPrint 3105/3205, ApeosPort/DocuCentre đời cũ) nhúng danh sách
    /// trong biến JavaScript của /jbhist.htm:
    ///   var jHst=[['Tên tài liệu','Owner',trạng thái,loại,số trang,'Khay ra','Cổng','dd/MM/yyyy HH:mm'],...];
    /// Máy chỉ giữ ~60–200 lệnh gần nhất (máy bận thì khoảng một ngày) nên job nền kéo theo chu kỳ
    /// đọc chỉ số; lệnh đã cuộn khỏi máy thì chỉ còn trong bảng này.
    ///
    /// Dòng đời mới (ApeosPrint/ApeosPort 3360S, 3570, 4570, 4830, 3530) không có jbhist.htm mà có
    /// API JSON /jobs/api/job-list — xem LayJobListAsync.
    /// </summary>
    public class MayInLichSuInService
    {
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly ITFormContext _context;

        static MayInLichSuInService()
        {
            // Tên tài liệu gửi từ Windows tiếng Trung đi qua LPD dạng GBK (code page 936)
            Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
        }

        public MayInLichSuInService(IHttpClientFactory httpClientFactory, ITFormContext context)
        {
            _httpClientFactory = httpClientFactory;
            _context = context;
        }

        public class KetQuaDongBo
        {
            public bool ThanhCong { get; set; }
            public string ThongBao { get; set; } = "";

            /// <summary>false = máy không có trang Job History (dòng đời mới, máy tắt...).</summary>
            public bool CoJobHistory { get; set; }
            public int SoDocDuoc { get; set; }
            public int SoThemMoi { get; set; }
        }

        private record DongJob(DateTime ThoiDiem, string? NguoiIn, string? TenTaiLieu, string? Loai,
            string? TrangThai, int? SoTrang, string? KhayRa, string? CongNhan);

        public async Task<KetQuaDongBo> DongBoAsync(MayIn may, CancellationToken ct = default)
        {
            if (string.IsNullOrWhiteSpace(may.DiaChiIp))
                return new KetQuaDongBo { ThongBao = "Máy không có IP (cắm USB) — không đọc được lịch sử in" };

            // Có máy bật "chỉ HTTPS": cổng 80 trả 307 sang https mà HttpClient không tự theo
            // (đổi scheme), nên thiếu dữ liệu ở http thì thử lại thẳng https
            var trang = await LayTrangAsync($"http://{may.DiaChiIp}/jbhist.htm", ct);
            if (trang is null || !trang.Contains("jHst"))
                trang = await LayTrangAsync($"https://{may.DiaChiIp}/jbhist.htm", ct);

            List<MayInLichSuIn> dsMoi;
            if (trang is not null && trang.Contains("jHst"))
            {
                dsMoi = TaoDongTuJbhist(may.IdMayIn, PhanTichTrang(trang));
            }
            else
            {
                // Dòng đời mới (ApeosPrint/ApeosPort 3360S, 3570, 4570, 4830...) không có jbhist.htm
                var dsJobList = await LayJobListAsync(may, ct);
                if (dsJobList is null)
                    return new KetQuaDongBo { ThongBao = "Máy không có danh sách lệnh in (Job History / Jobs) hoặc đang tắt" };
                dsMoi = dsJobList;
            }

            var ketQua = new KetQuaDongBo { CoJobHistory = true, SoDocDuoc = dsMoi.Count };
            if (dsMoi.Count == 0)
            {
                ketQua.ThanhCong = true;
                ketQua.ThongBao = "Không có lệnh in mới";
                return ketQua;
            }

            // Thử lại một lần nếu đụng khoá UNIQUE: máy chủ kia vừa ghi đúng những dòng này
            for (var lan = 1; ; lan++)
            {
                try
                {
                    ketQua.SoThemMoi = await GhiDongChuaCoAsync(may.IdMayIn, dsMoi, ct);
                    break;
                }
                catch (DbUpdateException) when (lan < 2)
                {
                    _context.ChangeTracker.Clear();
                }
            }

            ketQua.ThanhCong = true;
            ketQua.ThongBao = ketQua.SoThemMoi > 0
                ? $"Đã thêm {ketQua.SoThemMoi} lệnh in mới (đọc được {ketQua.SoDocDuoc} lệnh)"
                : $"Không có lệnh in mới (đọc được {ketQua.SoDocDuoc} lệnh)";
            return ketQua;
        }

        /// <summary>
        /// Trang jbhist.htm không có mã lệnh và nhiều dòng giống hệt nhau (in lại cùng tài liệu trong một
        /// phút), nên khoá = băm nội dung + thứ tự xuất hiện. Khoá UNIQUE trên DB chặn nhân bản khi hai
        /// máy chủ cùng kéo một lúc hoặc job nền trùng lúc người dùng bấm nút.
        /// </summary>
        private static List<MayInLichSuIn> TaoDongTuJbhist(int idMayIn, List<DongJob> dsDong)
        {
            var dsMoi = new List<MayInLichSuIn>();
            var demTheoKhoa = new Dictionary<string, short>();
            foreach (var d in dsDong)
            {
                var khoa = TinhKhoa(d);
                demTheoKhoa[khoa] = (short)(demTheoKhoa.GetValueOrDefault(khoa) + 1);
                dsMoi.Add(TaoDong(idMayIn, d, khoa, demTheoKhoa[khoa]));
            }
            return dsMoi;
        }

        private static MayInLichSuIn TaoDong(int idMayIn, DongJob d, string khoa, short soLan) => new()
        {
            IdMayIn = idMayIn,
            ThoiDiemIn = d.ThoiDiem,
            NguoiIn = d.NguoiIn,
            TenTaiLieu = d.TenTaiLieu,
            Loai = d.Loai,
            TrangThai = d.TrangThai,
            SoTrang = d.SoTrang,
            KhayRa = d.KhayRa,
            CongNhan = d.CongNhan,
            Khoa = khoa,
            SoLan = soLan,
            NgayDongBo = DateTime.Now
        };

        // ---------- Dòng đời mới: /jobs/api/job-list ----------

        // Giao diện web của máy chỉ xin 25 lệnh/lần; xin nhiều hơn máy trả danh sách rỗng
        private const int SoLenhMoiTrang = 25;

        // Máy bận giữ cả nghìn lệnh (~5 ngày); lần đầu kéo hết, các lần sau dừng khi gặp trang đã có đủ
        private const int SoTrangToiDa = 120;

        // Lệnh còn đang chạy/chờ thì chưa ghi: khoá theo JobID nên ghi rồi sẽ không cập nhật trạng thái nữa
        private static readonly string[] TrangThaiChuaXong = { "PROCESSING", "PENDING", "HELD" };

        /// <summary>
        /// Máy đời mới (WebUI /home, /jobs) có API JSON danh sách lệnh, không cần đăng nhập. Gọi như
        /// giao diện web: POST /jobs/api/job-list?methodName=GET&amp;limit=25&amp;typeFilter=ALL, trang sau
        /// thêm offsetJobID = JobID cuối + offsetJobIDType = COMPLETED/UNCOMPLETE. Mỗi lệnh có JobID
        /// duy nhất nên khoá = JobID (không băm như jbhist). Trả null khi máy không có API này.
        ///
        /// Giờ "Created/Completed" có hậu tố Z nhưng thực tế là giờ đồng hồ máy (giờ VN), đối chiếu ngày
        /// 09/11/2026 trên nhiều máy — không đổi múi giờ. Có máy để đồng hồ nhanh vài tiếng.
        /// </summary>
        private async Task<List<MayInLichSuIn>?> LayJobListAsync(MayIn may, CancellationToken ct)
        {
            var ketQua = new List<MayInLichSuIn>();
            string? goc = null;
            long? offsetId = null;
            string? offsetLoai = null;
            var dangKeoPhanCu = false;
            var daGap = new HashSet<string>();

            for (var soTrang = 0; soTrang < SoTrangToiDa; soTrang++)
            {
                var truyVan = $"/jobs/api/job-list?methodName=GET&limit={SoLenhMoiTrang}&typeFilter=ALL";
                if (offsetId is not null) truyVan += $"&offsetJobID={offsetId}&offsetJobIDType={offsetLoai}";

                JsonDocument? doc = null;
                if (goc is null)
                {
                    // Lần đầu dò https trước (dòng này mặc định bật HTTPS), rồi http
                    foreach (var thu in new[] { $"https://{may.DiaChiIp}", $"http://{may.DiaChiIp}" })
                    {
                        doc = await PostJsonAsync(thu + truyVan, ct);
                        if (doc is not null) { goc = thu; break; }
                    }
                    if (doc is null) return null;
                }
                else
                {
                    doc = await PostJsonAsync(goc + truyVan, ct);
                    if (doc is null) break; // máy rớt giữa chừng: giữ những trang đã đọc
                }

                using (doc)
                {
                    var goc0 = doc.RootElement;
                    if (goc0.TryGetProperty("Supported", out var ht) && ht.ValueKind == JsonValueKind.False)
                        return soTrang == 0 ? null : ketQua;
                    if (!goc0.TryGetProperty("Jobs", out var dsJob) || dsJob.ValueKind != JsonValueKind.Array
                        || dsJob.GetArrayLength() == 0)
                        break;

                    var trangNay = new List<MayInLichSuIn>();
                    var soMoiTrongLan = 0;
                    foreach (var muc in dsJob.EnumerateArray())
                    {
                        if (!muc.TryGetProperty("JobInfo", out var j)) continue;
                        if (!j.TryGetProperty("JobID", out var idEl) || !idEl.TryGetInt64(out var jobId)) continue;

                        var trangThai = ChuoiJson(j, "State");
                        offsetId = jobId;
                        offsetLoai = trangThai == "COMPLETED" ? "COMPLETED" : "UNCOMPLETE";

                        if (trangThai is not null && TrangThaiChuaXong.Any(t => trangThai.StartsWith(t, StringComparison.Ordinal)))
                            continue;
                        if (!TryDocGioJob(ChuoiJson(j, "Completed"), out var thoiDiem)
                            && !TryDocGioJob(ChuoiJson(j, "Created"), out thoiDiem))
                            continue;

                        var nguoiIn = ChuoiJson(j, "UserName") ?? ChuoiJson(j, "UserID");
                        var dong = TaoDong(may.IdMayIn, new DongJob(
                            thoiDiem,
                            string.IsNullOrEmpty(nguoiIn) || nguoiIn == "-" ? null : Cat(nguoiIn, 100),
                            Cat(TenTaiLieuJob(j), 500),
                            Cat(ChuoiJson(j, "UserJobType"), 30),
                            Cat(trangThai, 30),
                            SoJson(j, "PrintImpressions"),
                            Cat(ChuoiJson(j, "OutputTrayNameId"), 100),
                            Cat(ChuoiJson(j, "NetInProtocol"), 50)),
                            // Kèm giờ để JobID có quay vòng (reset máy) cũng không đụng lệnh cũ
                            $"job:{jobId}:{thoiDiem:yyyyMMddHHmmss}", 1);
                        trangNay.Add(dong);

                        // Trang sau có thể lặp lại lệnh của trang trước (mốc offset rơi vào lệnh chưa
                        // COMPLETED) — trùng trong cùng một lần ghi là đụng khoá UNIQUE cả lô
                        if (daGap.Add(dong.Khoa))
                        {
                            ketQua.Add(dong);
                            soMoiTrongLan++;
                        }
                    }

                    // Cả trang toàn lệnh vừa gặp ở trang trước → máy đang trả vòng lại, dừng
                    if (trangNay.Count > 0 && soMoiTrongLan == 0) break;

                    if (!goc0.TryGetProperty("Next", out var tiep) || tiep.ValueKind != JsonValueKind.True)
                        break;

                    // Cả trang đã có trong DB → phần mới đã kéo đủ. Lần trước có thể dừng ở trần
                    // SoTrangToiDa trước khi tới lệnh cũ nhất máy giữ, nên nhảy xuống dưới lệnh cũ nhất
                    // đã lưu và kéo tiếp (mỗi chu kỳ thêm một đoạn, tới khi máy hết lệnh)
                    if (!dangKeoPhanCu && trangNay.Count > 0)
                    {
                        var dsKhoa = trangNay.Select(x => x.Khoa).ToList();
                        var soDaCo = await _context.MayInLichSuIns.AsNoTracking()
                            .CountAsync(x => x.IdMayIn == may.IdMayIn && dsKhoa.Contains(x.Khoa), ct);
                        if (soDaCo >= dsKhoa.Count)
                        {
                            var idCuNhat = await LayJobIdCuNhatAsync(may.IdMayIn, ct);
                            if (idCuNhat is null || idCuNhat >= offsetId) break;
                            offsetId = idCuNhat;
                            offsetLoai = "COMPLETED";
                            dangKeoPhanCu = true;
                        }
                    }
                }
            }

            return ketQua;
        }

        /// <summary>
        /// JobID nhỏ nhất đã lưu của máy (khoá "job:{JobID}:{giờ}"). JobID tăng dần theo thời gian
        /// nên lấy vài dòng giờ cũ nhất rồi chọn số nhỏ nhất, tránh quét cả bảng.
        /// </summary>
        private async Task<long?> LayJobIdCuNhatAsync(int idMayIn, CancellationToken ct)
        {
            var dsKhoa = await _context.MayInLichSuIns.AsNoTracking()
                .Where(x => x.IdMayIn == idMayIn && x.Khoa.StartsWith("job:"))
                .OrderBy(x => x.ThoiDiemIn)
                .Select(x => x.Khoa)
                .Take(50)
                .ToListAsync(ct);

            long? nhoNhat = null;
            foreach (var khoa in dsKhoa)
            {
                var phan = khoa.Split(':');
                if (phan.Length >= 2 && long.TryParse(phan[1], out var id) && (nhoNhat is null || id < nhoNhat))
                    nhoNhat = id;
            }
            return nhoNhat;
        }

        /// <summary>Tên file máy nhận qua LPD thường là GUID do Windows sinh — vô nghĩa, bỏ.</summary>
        private static string? TenTaiLieuJob(JsonElement j)
        {
            if (j.TryGetProperty("NetInFilenameHide", out var an) && an.ValueKind == JsonValueKind.True) return null;
            var ten = ChuoiJson(j, "NetInFilename");
            return string.IsNullOrWhiteSpace(ten) || Guid.TryParse(ten, out _) ? null : ten.Trim();
        }

        private static string? ChuoiJson(JsonElement e, string ten) =>
            e.TryGetProperty(ten, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() : null;

        private static int? SoJson(JsonElement e, string ten) =>
            e.TryGetProperty(ten, out var v) && v.ValueKind == JsonValueKind.Number && v.TryGetInt32(out var so) ? so : null;

        private static bool TryDocGioJob(string? chuoi, out DateTime ketQua) =>
            DateTime.TryParseExact(chuoi?.TrimEnd('Z'), "yyyy-MM-dd'T'HH:mm:ss", CultureInfo.InvariantCulture,
                DateTimeStyles.None, out ketQua);

        private async Task<JsonDocument?> PostJsonAsync(string url, CancellationToken ct)
        {
            try
            {
                var client = _httpClientFactory.CreateClient(MayInApiService.TenHttpClient);
                using var phanHoi = await client.PostAsync(url, content: null, ct);
                if (!phanHoi.IsSuccessStatusCode) return null;
                var noiDung = await phanHoi.Content.ReadAsStringAsync(ct);
                if (string.IsNullOrWhiteSpace(noiDung) || noiDung.TrimStart()[0] != '{') return null;
                return JsonDocument.Parse(noiDung);
            }
            catch (HttpRequestException)
            {
                return null;
            }
            catch (JsonException)
            {
                return null;
            }
            catch (TaskCanceledException) when (!ct.IsCancellationRequested)
            {
                return null;
            }
        }

        private async Task<int> GhiDongChuaCoAsync(int idMayIn, List<MayInLichSuIn> dsMoi, CancellationToken ct)
        {
            var dsKhoa = dsMoi.Select(x => x.Khoa).Distinct().ToList();
            var daCo = (await _context.MayInLichSuIns.AsNoTracking()
                    .Where(x => x.IdMayIn == idMayIn && dsKhoa.Contains(x.Khoa))
                    .Select(x => new { x.Khoa, x.SoLan })
                    .ToListAsync(ct))
                .Select(x => (x.Khoa, x.SoLan))
                .ToHashSet();

            var canThem = dsMoi.Where(x => !daCo.Contains((x.Khoa, x.SoLan))).ToList();
            if (canThem.Count == 0) return 0;

            _context.MayInLichSuIns.AddRange(canThem);
            await _context.SaveChangesAsync(ct);
            return canThem.Count;
        }

        private async Task<string?> LayTrangAsync(string url, CancellationToken ct)
        {
            try
            {
                var client = _httpClientFactory.CreateClient(MayInApiService.TenHttpClient);
                using var phanHoi = await client.GetAsync(url, ct);
                if (!phanHoi.IsSuccessStatusCode) return null;

                // Đọc từng byte thành một ký tự Latin-1 để giữ nguyên byte gốc của tên tài liệu,
                // giải mã UTF-8/GBK sau (trang khai UTF-8 nhưng thực tế byte tuỳ máy gửi lệnh)
                var bytes = await phanHoi.Content.ReadAsByteArrayAsync(ct);
                return Encoding.Latin1.GetString(bytes);
            }
            catch (HttpRequestException)
            {
                return null;
            }
            catch (TaskCanceledException) when (!ct.IsCancellationRequested)
            {
                return null;
            }
        }

        private static List<DongJob> PhanTichTrang(string trang)
        {
            var dsTrangThai = DocMangChu(trang, "stsAry");
            var dsLoai = DocMangChu(trang, "types");
            var ketQua = new List<DongJob>();

            var viTri = trang.IndexOf("var jHst=", StringComparison.Ordinal);
            if (viTri < 0) return ketQua;

            var i = viTri + "var jHst=".Length;
            if (DocGiaTriJs(trang, ref i) is not List<object?> dsJob) return ketQua;

            foreach (var muc in dsJob)
            {
                if (muc is not List<object?> o || o.Count < 8) continue;
                if (!TryDocThoiGian(o[7] as string, out var thoiDiem)) continue;

                var nguoiIn = (o[1] as string)?.Trim();
                ketQua.Add(new DongJob(
                    thoiDiem,
                    string.IsNullOrEmpty(nguoiIn) || nguoiIn == "-" ? null : Cat(nguoiIn, 100),
                    Cat(GiaiMaTen(o[0] as string), 500),
                    Cat(TraNhan(dsLoai, o[3]), 30),
                    Cat(TraNhan(dsTrangThai, o[2]), 30),
                    o[4] is double soTrang ? (int)soTrang : null,
                    Cat(o[5] as string, 100),
                    Cat(o[6] as string, 50)));
            }

            return ketQua;
        }

        private static string? TraNhan(List<string> dsNhan, object? giaTri)
        {
            if (giaTri is not double so) return giaTri as string;
            var chiSo = (int)so;
            return chiSo >= 0 && chiSo < dsNhan.Count ? dsNhan[chiSo] : chiSo.ToString();
        }

        private static List<string> DocMangChu(string trang, string tenBien)
        {
            var viTri = trang.IndexOf($"var {tenBien}=", StringComparison.Ordinal);
            if (viTri < 0) return new List<string>();

            var i = viTri + tenBien.Length + 5;
            return DocGiaTriJs(trang, ref i) is List<object?> ds
                ? ds.Select(x => x as string ?? "").ToList()
                : new List<string>();
        }

        /// <summary>
        /// Đọc một giá trị JavaScript đơn giản (mảng lồng nhau, chuỗi nháy đơn/kép, số) bắt đầu tại i.
        /// Trang máy in chỉ dùng chừng đó nên không cần bộ phân tích JS đầy đủ.
        /// </summary>
        private static object? DocGiaTriJs(string s, ref int i)
        {
            while (i < s.Length && char.IsWhiteSpace(s[i])) i++;
            if (i >= s.Length) return null;

            var c = s[i];
            if (c == '[')
            {
                i++;
                var ds = new List<object?>();
                while (i < s.Length)
                {
                    while (i < s.Length && (char.IsWhiteSpace(s[i]) || s[i] == ',')) i++;
                    if (i >= s.Length) break;
                    if (s[i] == ']') { i++; return ds; }
                    var truoc = i;
                    ds.Add(DocGiaTriJs(s, ref i));
                    if (i == truoc) i++; // ký tự lạ: bỏ qua cho khỏi lặp vô hạn
                }
                return ds;
            }

            if (c == '\'' || c == '"')
            {
                i++;
                var sb = new StringBuilder();
                while (i < s.Length && s[i] != c)
                {
                    if (s[i] == '\\' && i + 1 < s.Length) i++;
                    sb.Append(s[i]);
                    i++;
                }
                i++;
                return sb.ToString();
            }

            var dau = i;
            while (i < s.Length && (char.IsDigit(s[i]) || s[i] == '-' || s[i] == '.')) i++;
            return double.TryParse(s.AsSpan(dau, i - dau), NumberStyles.Float, CultureInfo.InvariantCulture, out var so)
                ? so : null;
        }

        /// <summary>
        /// Byte gốc đúng UTF-8 thì giải UTF-8, không thì coi là GBK (Windows tiếng Trung).
        /// Máy in còn mã hoá hai lần: coi byte GBK gốc là Latin-1 rồi đổi sang UTF-8 (trang khai
        /// charset UTF-8). Giải UTF-8 xong mà vẫn chỉ toàn ký tự ≤ 0xFF thì đó là lớp vỏ đó — giải tiếp.
        /// </summary>
        private static string? GiaiMaTen(string? latin1)
        {
            var ketQua = latin1;
            for (var lop = 0; lop < 2 && !string.IsNullOrEmpty(ketQua); lop++)
            {
                if (ketQua.Any(c => c > 0xFF) || ketQua.All(c => c < 0x80)) break;

                var bytes = Encoding.Latin1.GetBytes(ketQua);
                try
                {
                    ketQua = new UTF8Encoding(false, true).GetString(bytes);
                }
                catch (DecoderFallbackException)
                {
                    // Lớp thứ hai mà GBK cũng hỏng thì chữ Latin-1 kia là thật (vd "café"), giữ nguyên
                    var gbk = Encoding.GetEncoding(936).GetString(bytes);
                    if (lop == 0 || !gbk.Contains('�')) ketQua = gbk;
                    break;
                }
            }
            return ketQua;
        }

        private static readonly string[] DinhDangThoiGian =
        {
            "dd/MM/yyyy HH:mm", "dd/MM/yyyy HH:mm:ss", "yyyy/MM/dd HH:mm", "yyyy/MM/dd HH:mm:ss"
        };

        private static bool TryDocThoiGian(string? chuoi, out DateTime ketQua) =>
            DateTime.TryParseExact(chuoi?.Trim(), DinhDangThoiGian, CultureInfo.InvariantCulture,
                DateTimeStyles.None, out ketQua);

        private static string TinhKhoa(DongJob d)
        {
            var chuoi = string.Join('\u001f', d.ThoiDiem.ToString("yyyyMMddHHmmss"), d.NguoiIn, d.TenTaiLieu,
                d.Loai, d.TrangThai, d.SoTrang, d.KhayRa, d.CongNhan);
            return Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(chuoi)));
        }

        private static string? Cat(string? s, int toiDa) =>
            s is null ? null : (s.Length <= toiDa ? s : s[..toiDa]);
    }
}
