using System.Globalization;
using System.Text.Json;
using E_Form_Best.Context;
using Microsoft.EntityFrameworkCore;

namespace E_Form_Best.Areas.ITForm.Services
{
    /// <summary>
    /// Báo cáo tổng quan camera cả 3 công ty (/QLCamera/TongQuan) — dùng chung cho trang, bản in và file Excel.
    ///  - Hiện tại: BPVN đọc hệ thống giám sát ISAPI (bỏ camera đã loại trừ, giống tab Giám sát), PFVN/MEGA đọc
    ///    file trạng thái của công ty đó.
    ///  - Theo kỳ: đếm từ KK_CameraLichSu (do CameraLichSuWorker ghi), chỉ tính camera đang có trong danh sách
    ///    hiện tại để số theo kỳ khớp số hiện tại (camera đã loại trừ / đã gỡ không làm lệch báo cáo).
    /// Một nguồn lỗi (ISAPI sập, thiếu file) chỉ làm công ty đó báo lỗi, các công ty khác vẫn có số.
    /// </summary>
    public class CameraBaoCaoService
    {
        private readonly ITFormContext _context;
        private readonly CameraGiamSatService _giamSat;
        private readonly CameraXemTrucTiepService _xem;

        public CameraBaoCaoService(ITFormContext context, CameraGiamSatService giamSat, CameraXemTrucTiepService xem)
        {
            _context = context;
            _giamSat = giamSat;
            _xem = xem;
        }

        public static readonly string[] DsCongTy = { "BPVN", "PFVN", "MEGA" };

        public class CongTyBaoCao
        {
            public string CongTy { get; set; } = "";
            /// <summary>Không đọc được nguồn dữ liệu của công ty này (các số còn lại = 0).</summary>
            public string? Loi { get; set; }
            public DateTime? CapNhat { get; set; }
            public int Tong { get; set; }
            public int HoatDong { get; set; }
            public int MatKetNoi { get; set; }
            public int SoDauGhi { get; set; }
            /// <summary>Camera chưa lấy được ảnh nào (cả chụp trực tiếp lẫn playback); null = không xác định được.</summary>
            public int? ChuaCoAnh { get; set; }
            public int SoLanMat { get; set; }
            public int SoCameraBiMat { get; set; }
            public long GiayMat { get; set; }
            /// <summary>% thời gian hoạt động trong kỳ = 1 - giây mất / (số camera × độ dài kỳ).</summary>
            public double? TyLeTrongKy { get; set; }
            // Cùng các chỉ số ở kỳ trước (BaoCao.KyTruocTu..KyTruocDen) để so sánh tăng/giảm
            public int SoLanMatTruoc { get; set; }
            public int SoCameraBiMatTruoc { get; set; }
            public long GiayMatTruoc { get; set; }
            public double? TyLeTruoc { get; set; }
        }

        public class CameraDong
        {
            public string CongTy { get; set; } = "";
            public string NvrIp { get; set; } = "";
            public string? DauGhi { get; set; }
            public int Kenh { get; set; }
            public string? Ten { get; set; }
            public string? Ip { get; set; }
            public DateTime? MatTu { get; set; }
            public int SoLan { get; set; }
            public long GiayMat { get; set; }
            public int SoLanTruoc { get; set; }
            public string? GhiChu { get; set; }
        }

        public class BaoCao
        {
            public DateTime TuNgay { get; set; }
            public DateTime DenNgay { get; set; }
            public DateTime TaoLuc { get; set; }
            /// <summary>Kỳ đem ra so sánh (xem KyTruoc).</summary>
            public DateTime KyTruocTu { get; set; }
            public DateTime KyTruocDen { get; set; }
            /// <summary>Sự kiện lịch sử sớm nhất — trước mốc này chưa có dữ liệu theo kỳ.</summary>
            public DateTime? LichSuTu { get; set; }
            public List<CongTyBaoCao> CongTy { get; set; } = new();
            public List<DauGhiDong> DauGhi { get; set; } = new();
            public List<CameraDong> DangMat { get; set; } = new();
            public List<CameraDong> MatNhieu { get; set; } = new();
        }

        public class DauGhiDong
        {
            public string CongTy { get; set; } = "";
            public string NvrIp { get; set; } = "";
            public string? Ten { get; set; }
            public int Tong { get; set; }
            public int HoatDong { get; set; }
            public int MatKetNoi { get; set; }
            public int SoLanMat { get; set; }
            public long GiayMat { get; set; }
            public int SoLanMatTruoc { get; set; }
            public long GiayMatTruoc { get; set; }
        }

        /// <summary>
        /// Kỳ đem so sánh với kỳ [tu, den] (gồm trọn ngày):
        ///  - Kỳ ≤ 7 ngày (tuần này, 7 ngày gần nhất): lùi đúng 7 ngày — cùng các thứ trong tuần.
        ///  - Kỳ bắt đầu ngày 1 và nằm trong 1 tháng: cùng khoảng ngày của tháng trước (1→9/10 so với 1→9/9);
        ///    tháng trọn so với tháng trước trọn (dài ngắn khác nhau nên so % hoạt động là chính).
        ///  - Còn lại: khoảng liền trước, dài bằng kỳ.
        /// </summary>
        public static (DateTime tu, DateTime den) KyTruoc(DateTime tuNgay, DateTime denNgay)
        {
            var tu = tuNgay.Date;
            var den = denNgay.Date;
            var soNgay = (den - tu).Days + 1;
            if (soNgay <= 7) return (tu.AddDays(-7), den.AddDays(-7));

            if (tu.Day == 1 && tu.Year == den.Year && tu.Month == den.Month)
            {
                var dauThangTruoc = tu.AddMonths(-1);
                var cuoiThangTruoc = tu.AddDays(-1);
                var tronThang = den == tu.AddMonths(1).AddDays(-1);
                var denTruoc = tronThang ? cuoiThangTruoc : dauThangTruoc.AddDays(den.Day - 1);
                return (dauThangTruoc, denTruoc > cuoiThangTruoc ? cuoiThangTruoc : denTruoc);
            }
            return (tu.AddDays(-soNgay), tu.AddDays(-1));
        }

        /// <summary>
        /// Mốc thời gian (cận trên loại trừ) của kỳ này và kỳ trước. Kỳ này chưa hết thì tính tới bây giờ, và kỳ trước
        /// cũng chỉ tính tới cùng mốc tương ứng (tuần này tới thứ Năm 10h ↔ tuần trước tới thứ Năm 10h) để số đếm so được.
        /// </summary>
        public static (DateTime tu, DateTime cuoi, DateTime tuTruoc, DateTime denTruoc, DateTime cuoiTruoc) MocSoSanh(
            DateTime tuNgay, DateTime denNgay, DateTime bayGio)
        {
            var tu = tuNgay.Date;
            var den = denNgay.Date.AddDays(1);
            var cuoi = den < bayGio ? den : bayGio;
            var (tuTruoc, denTruoc) = KyTruoc(tu, denNgay);
            var cuoiTruoc = denTruoc.AddDays(1);
            if (cuoi < den && tuTruoc + (cuoi - tu) < cuoiTruoc) cuoiTruoc = tuTruoc + (cuoi - tu);
            return (tu, cuoi, tuTruoc, denTruoc, cuoiTruoc);
        }

        private record CamHienTai(string CongTy, string NvrIp, string? DauGhi, int Kenh, string? Ten, string? Ip,
            bool Online, DateTime? MatTu);

        /// <param name="dsCongTy">Chỉ những công ty người xem có quyền (controller lọc trước).</param>
        /// <param name="tuNgay">Ngày đầu kỳ (gồm trọn ngày).</param>
        /// <param name="denNgay">Ngày cuối kỳ (gồm trọn ngày).</param>
        public async Task<BaoCao> TaoAsync(IReadOnlyList<string> dsCongTy, DateTime tuNgay, DateTime denNgay, CancellationToken ct)
        {
            var bayGio = DateTime.Now;
            var tu = tuNgay.Date;
            var den = denNgay.Date.AddDays(1);
            // Kỳ chưa hết thì chỉ tính tới hiện tại
            var cuoiKy = den < bayGio ? den : bayGio;
            var (tuTruoc, denTruoc) = KyTruoc(tu, denNgay);
            // Kỳ này chưa hết (vd tuần này tới giờ) thì kỳ trước cũng chỉ tính tới cùng mốc, để số lần mất so được với nhau
            var cuoiTruoc = denTruoc.AddDays(1);
            if (cuoiKy < den && tuTruoc + (cuoiKy - tu) < cuoiTruoc) cuoiTruoc = tuTruoc + (cuoiKy - tu);
            var bc = new BaoCao { TuNgay = tu, DenNgay = denNgay.Date, TaoLuc = bayGio, KyTruocTu = tuTruoc, KyTruocDen = denTruoc };

            // ---- Trạng thái hiện tại từng công ty ----
            var dsCam = new List<CamHienTai>();
            foreach (var cty in dsCongTy)
            {
                var dong = new CongTyBaoCao { CongTy = cty };
                bc.CongTy.Add(dong);
                try
                {
                    var (capNhat, cams, chuaCoAnh) = cty == "BPVN" ? await HienTaiBpvnAsync(ct) : await HienTaiCongTyAsync(cty, ct);
                    dong.CapNhat = capNhat;
                    dong.Tong = cams.Count;
                    dong.HoatDong = cams.Count(x => x.Online);
                    dong.MatKetNoi = cams.Count(x => !x.Online);
                    dong.SoDauGhi = cams.Select(x => x.NvrIp).Distinct().Count();
                    dong.ChuaCoAnh = chuaCoAnh;
                    dsCam.AddRange(cams);
                }
                catch (Exception ex) when (ex is HttpRequestException or IOException or UnauthorizedAccessException
                                           or JsonException or InvalidOperationException
                                           || (ex is TaskCanceledException && !ct.IsCancellationRequested))
                {
                    dong.Loi = cty == "BPVN" ? "Không đọc được hệ thống giám sát camera BPVN: " + ex.Message
                                             : "Không đọc được dữ liệu camera " + cty + ": " + ex.Message;
                }
            }

            var theoKhoa = dsCam.GroupBy(x => (x.NvrIp, x.Kenh)).ToDictionary(g => g.Key, g => g.First());
            var dsIp = theoKhoa.Keys.Select(k => k.NvrIp).Distinct().ToList();

            // ---- Lịch sử trong kỳ (và kỳ trước) ----
            // Sự kiện DOWN trong kỳ -> số lần mất. Sự kiện UP từ đầu kỳ trở đi (kể cả sau kỳ) mang thời lượng mất
            // -> cắt phần nằm trong kỳ: camera rớt trước kỳ và lên lại sau kỳ vẫn được tính đủ.
            // Đọc một lần từ đầu kỳ trước, rồi chia cho từng kỳ.
            var suKien = await _context.KkCameraLichSus
                .Where(x => dsIp.Contains(x.NvrIp) && x.ThoiGian >= tuTruoc
                            && ((x.SangTrangThai == "DOWN" && x.ThoiGian < den) || (x.SangTrangThai == "UP" && x.ThoiLuongGiay != null)))
                .Select(x => new { x.NvrIp, x.Kenh, x.ThoiGian, x.SangTrangThai, x.ThoiLuongGiay })
                .ToListAsync(ct);
            bc.LichSuTu = await _context.KkCameraLichSus.Where(x => dsIp.Contains(x.NvrIp)).MinAsync(x => (DateTime?)x.ThoiGian, ct);

            Dictionary<(string, int), (int soLan, long giay)> ThongKeKy(DateTime kyTu, DateTime kyCuoi)
            {
                var kq = new Dictionary<(string, int), (int soLan, long giay)>();
                void Cong((string, int) khoa, int lan, long giay)
                {
                    var cu = kq.GetValueOrDefault(khoa);
                    kq[khoa] = (cu.soLan + lan, cu.giay + giay);
                }
                long GiayTrongKy(DateTime batDau, DateTime ketThuc)
                {
                    var a = batDau < kyTu ? kyTu : batDau;
                    var b = ketThuc > kyCuoi ? kyCuoi : ketThuc;
                    return b > a ? (long)(b - a).TotalSeconds : 0;
                }

                foreach (var s in suKien)
                {
                    var khoa = (s.NvrIp, s.Kenh);
                    if (!theoKhoa.ContainsKey(khoa)) continue;
                    if (s.SangTrangThai == "DOWN") { if (s.ThoiGian >= kyTu && s.ThoiGian < kyCuoi) Cong(khoa, 1, 0); }
                    else Cong(khoa, 0, GiayTrongKy(s.ThoiGian.AddSeconds(-s.ThoiLuongGiay!.Value), s.ThoiGian));
                }
                // Đang mất kết nối: chưa có sự kiện UP nên tính từ lúc rớt tới cuối kỳ
                foreach (var c in theoKhoa.Values.Where(x => !x.Online && x.MatTu != null))
                    Cong((c.NvrIp, c.Kenh), 0, GiayTrongKy(c.MatTu!.Value, bayGio));
                return kq;
            }

            var thongKe = ThongKeKy(tu, cuoiKy);
            var thongKeTruoc = ThongKeKy(tuTruoc, cuoiTruoc);

            // Cả 2 kỳ dùng chung danh sách camera hiện tại làm mẫu số
            double? TyLe(int tong, long giayMat, double giayKy)
                => tong > 0 && giayKy > 0 ? Math.Round(100 * Math.Max(0, 1 - giayMat / (tong * giayKy)), 2) : null;
            var giayKy = Math.Max(0, (cuoiKy - tu).TotalSeconds);
            var giayKyTruoc = Math.Max(0, (cuoiTruoc - tuTruoc).TotalSeconds);
            foreach (var dong in bc.CongTy.Where(x => x.Loi == null))
            {
                var cuaCty = thongKe.Where(x => theoKhoa[x.Key].CongTy == dong.CongTy).ToList();
                dong.SoLanMat = cuaCty.Sum(x => x.Value.soLan);
                dong.SoCameraBiMat = cuaCty.Count(x => x.Value.soLan > 0 || x.Value.giay > 0);
                dong.GiayMat = cuaCty.Sum(x => x.Value.giay);
                dong.TyLeTrongKy = TyLe(dong.Tong, dong.GiayMat, giayKy);

                var truoc = thongKeTruoc.Where(x => theoKhoa[x.Key].CongTy == dong.CongTy).ToList();
                dong.SoLanMatTruoc = truoc.Sum(x => x.Value.soLan);
                dong.SoCameraBiMatTruoc = truoc.Count(x => x.Value.soLan > 0 || x.Value.giay > 0);
                dong.GiayMatTruoc = truoc.Sum(x => x.Value.giay);
                dong.TyLeTruoc = TyLe(dong.Tong, dong.GiayMatTruoc, giayKyTruoc);
            }

            // ---- Theo đầu ghi ----
            bc.DauGhi = theoKhoa.Values
                .GroupBy(x => (x.CongTy, x.NvrIp))
                .Select(g =>
                {
                    var tk = g.Select(c => thongKe.GetValueOrDefault((c.NvrIp, c.Kenh))).ToList();
                    var tkTruoc = g.Select(c => thongKeTruoc.GetValueOrDefault((c.NvrIp, c.Kenh))).ToList();
                    return new DauGhiDong
                    {
                        CongTy = g.Key.CongTy, NvrIp = g.Key.NvrIp,
                        Ten = g.Select(x => x.DauGhi).FirstOrDefault(x => !string.IsNullOrWhiteSpace(x)),
                        Tong = g.Count(), HoatDong = g.Count(x => x.Online), MatKetNoi = g.Count(x => !x.Online),
                        SoLanMat = tk.Sum(x => x.soLan), GiayMat = tk.Sum(x => x.giay),
                        SoLanMatTruoc = tkTruoc.Sum(x => x.soLan), GiayMatTruoc = tkTruoc.Sum(x => x.giay)
                    };
                })
                .OrderBy(x => Array.IndexOf(DsCongTy, x.CongTy)).ThenByDescending(x => x.MatKetNoi).ThenBy(x => x.Ten)
                .ToList();

            // ---- Danh sách camera ----
            var ghiChu = (await _context.KkCameraGhiChus
                    .Where(x => x.GhiChu != null && dsIp.Contains(x.NvrIp))
                    .Select(x => new { x.NvrIp, x.Kenh, x.GhiChu })
                    .ToListAsync(ct))
                .GroupBy(x => (x.NvrIp, x.Kenh)).ToDictionary(g => g.Key, g => g.First().GhiChu);

            CameraDong TaoDong(CamHienTai c)
            {
                var tk = thongKe.GetValueOrDefault((c.NvrIp, c.Kenh));
                return new CameraDong
                {
                    CongTy = c.CongTy, NvrIp = c.NvrIp, DauGhi = c.DauGhi, Kenh = c.Kenh, Ten = c.Ten, Ip = c.Ip,
                    MatTu = c.Online ? null : c.MatTu, SoLan = tk.soLan, GiayMat = tk.giay,
                    SoLanTruoc = thongKeTruoc.GetValueOrDefault((c.NvrIp, c.Kenh)).soLan,
                    GhiChu = ghiChu.GetValueOrDefault((c.NvrIp, c.Kenh))
                };
            }

            // Rớt lâu nhất lên đầu; không rõ mốc rớt xếp cuối
            bc.DangMat = theoKhoa.Values.Where(x => !x.Online).Select(TaoDong)
                .OrderBy(x => x.MatTu ?? DateTime.MaxValue).ThenBy(x => x.CongTy).ThenBy(x => x.NvrIp).ThenBy(x => x.Kenh)
                .ToList();
            bc.MatNhieu = thongKe.Where(x => x.Value.soLan > 0 || x.Value.giay > 0)
                .Select(x => TaoDong(theoKhoa[x.Key]))
                .OrderByDescending(x => x.SoLan).ThenByDescending(x => x.GiayMat)
                .Take(20)
                .ToList();

            return bc;
        }

        private async Task<(DateTime?, List<CamHienTai>, int?)> HienTaiBpvnAsync(CancellationToken ct)
        {
            // Không gồm camera đã loại trừ — đúng số "Tổng camera" ở tab Giám sát
            var duLieu = await _giamSat.DanhSachCameraAsync(null, null, null, false, false, ct);
            var ds = new List<CamHienTai>();
            if (duLieu.TryGetProperty("cameras", out var cameras) && cameras.ValueKind == JsonValueKind.Array)
            {
                foreach (var c in cameras.EnumerateArray())
                {
                    var nvr = Chuoi(c, "nvr_ip");
                    if (nvr == null || !int.TryParse(Chuoi(c, "cam_id"), out var kenh)) continue;
                    var online = Chuoi(c, "status") == "UP";
                    ds.Add(new CamHienTai("BPVN", nvr, Chuoi(c, "nvr_name"), kenh, Chuoi(c, "name"), Chuoi(c, "ip"), online,
                        online ? null : Ngay(Chuoi(c, "down_since_at")) ?? Ngay(Chuoi(c, "status_changed_at"))));
                }
            }
            // Một kênh có thể xuất hiện 2 lần (khoá ISAPI kèm IP camera) -> bỏ trùng theo đầu ghi + kênh
            ds = ds.GroupBy(x => (x.NvrIp, x.Kenh)).Select(g => g.First()).ToList();

            DateTime? capNhat = null;
            try
            {
                var tq = await _giamSat.TongQuanAsync(ct);
                capNhat = Ngay(Chuoi(tq, "last_poll_at"));
            }
            catch (HttpRequestException) { /* thiếu giờ kiểm tra không làm hỏng báo cáo */ }

            var coAnh = _xem.DanhSachKenhCoAnhLuu();
            int? chuaCoAnh = coAnh == null ? null : ds.Count(x => !coAnh.Contains(x.NvrIp + "|" + x.Kenh));
            return (capNhat, ds, chuaCoAnh);
        }

        private async Task<(DateTime?, List<CamHienTai>, int?)> HienTaiCongTyAsync(string cty, CancellationToken ct)
        {
            var (capNhat, kenh) = await _xem.DanhSachKenhCongTyAsync(cty, ct);
            var dsIp = kenh.Select(x => x.Nvr).Distinct().ToList();
            // Mốc rớt do CameraLichSuWorker ghi (giống tab Giám sát PFVN/MEGA)
            var doiLuc = (await _context.KkCameraTrangThais.Where(x => dsIp.Contains(x.NvrIp))
                    .Select(x => new { x.NvrIp, x.Kenh, x.DoiLuc }).ToListAsync(ct))
                .GroupBy(x => (x.NvrIp, x.Kenh)).ToDictionary(g => g.Key, g => g.First().DoiLuc);

            var ds = kenh.Select(x => new CamHienTai(cty, x.Nvr,
                    CameraXemTrucTiepService.TenDauGhiCongTy(cty, x.Nvr, x.TenDauGhi), x.Kenh,
                    string.IsNullOrWhiteSpace(x.Ten) ? $"Kênh {x.Kenh}" : x.Ten, x.IpCamera, x.Online,
                    x.Online ? null : doiLuc.GetValueOrDefault((x.Nvr, x.Kenh))))
                .ToList();
            // Công ty chưa có dữ liệu (chưa kết nối) thì không tính "chưa có ảnh"
            return (capNhat, ds, kenh.Count == 0 ? null : kenh.Count(x => x.AnhLuc == null));
        }

        private static string? Chuoi(JsonElement e, string ten)
        {
            if (e.ValueKind != JsonValueKind.Object || !e.TryGetProperty(ten, out var v)) return null;
            return v.ValueKind switch
            {
                JsonValueKind.String => v.GetString(),
                JsonValueKind.Number => v.GetRawText(),
                _ => null
            };
        }

        private static DateTime? Ngay(string? s)
            => DateTime.TryParse(s, CultureInfo.InvariantCulture, DateTimeStyles.None, out var d) ? d : null;
    }
}
