using System.Globalization;
using System.Security.Cryptography;
using System.Text;
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
            if (trang is null || !trang.Contains("jHst"))
                return new KetQuaDongBo { ThongBao = "Máy không có trang Job History hoặc đang tắt (chỉ dòng CentreWare đời cũ mới có)" };

            var dsDong = PhanTichTrang(trang);
            var ketQua = new KetQuaDongBo { CoJobHistory = true, SoDocDuoc = dsDong.Count };
            if (dsDong.Count == 0)
            {
                ketQua.ThanhCong = true;
                ketQua.ThongBao = "Máy chưa có lệnh nào trong Job History";
                return ketQua;
            }

            // Trang không có mã lệnh và nhiều dòng giống hệt nhau (in lại cùng tài liệu trong một phút),
            // nên khoá = băm nội dung + thứ tự xuất hiện. Khoá UNIQUE trên DB chặn nhân bản khi hai
            // máy chủ cùng kéo một lúc hoặc job nền trùng lúc người dùng bấm nút.
            var dsMoi = new List<MayInLichSuIn>();
            var demTheoKhoa = new Dictionary<string, short>();
            foreach (var d in dsDong)
            {
                var khoa = TinhKhoa(d);
                demTheoKhoa[khoa] = (short)(demTheoKhoa.GetValueOrDefault(khoa) + 1);
                dsMoi.Add(new MayInLichSuIn
                {
                    IdMayIn = may.IdMayIn,
                    ThoiDiemIn = d.ThoiDiem,
                    NguoiIn = d.NguoiIn,
                    TenTaiLieu = d.TenTaiLieu,
                    Loai = d.Loai,
                    TrangThai = d.TrangThai,
                    SoTrang = d.SoTrang,
                    KhayRa = d.KhayRa,
                    CongNhan = d.CongNhan,
                    Khoa = khoa,
                    SoLan = demTheoKhoa[khoa],
                    NgayDongBo = DateTime.Now
                });
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
                ? $"Đã thêm {ketQua.SoThemMoi} lệnh in mới (máy đang giữ {ketQua.SoDocDuoc} lệnh)"
                : $"Không có lệnh in mới (máy đang giữ {ketQua.SoDocDuoc} lệnh)";
            return ketQua;
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
