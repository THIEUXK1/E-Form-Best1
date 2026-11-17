using System.Data;
using System.Text.Json;
using E_Form_Best.Context;
using E_Form_Best.Models.ITForm;
using Microsoft.EntityFrameworkCore;

namespace E_Form_Best.Areas.QAForm.Services
{
    /// <summary>
    /// Thùng QA: gom các dòng đóng gói NH-MES vào thùng (QA_Thung / QA_ThungDong / QA_ThungLichSu).
    ///
    /// MES không có id dòng → nhận diện dòng bằng dấu vân tay (SHA-256 JSON dòng). Có thể có nhiều dòng MES
    /// giống hệt nhau nên KHÔNG chặn bằng UNIQUE mà đếm: số dòng cùng dấu vân tay đã ở thùng (chưa xoá)
    /// không được vượt số dòng đó MES đang có. Khi lưu, server tự gọi lại MES lấy dữ liệu gốc — không tin dữ liệu
    /// trình duyệt gửi lên — nên bấm 2 lần / 2 người bỏ cùng dòng cũng không nhân đôi.
    /// </summary>
    public class QaThungService
    {
        public record KetQua(bool ThanhCong, string ThongBao, int? IdThung = null, string? MaThung = null);

        public record ThungTomTat(int IdThung, string MaThung, string? TenThung, string TrangThai, string? GhiChu, DateTime NgayTao,
            string? NguoiTao, int SoDong, decimal TongMet, decimal TongKg, decimal TongHop, int? IdKe, string? ViTri,
            string? Khop, int SoDongKhop);

        // Trần số dòng mỗi lần bỏ vào thùng, tránh request quá lớn
        private const int TranDongMoiLan = 2000;

        private readonly ITFormContext _db;
        private readonly MesKhoService _kho;
        private readonly ILogger<QaThungService> _logger;

        public QaThungService(ITFormContext db, MesKhoService kho, ILogger<QaThungService> logger)
        {
            _db = db;
            _kho = kho;
            _logger = logger;
        }

        /// <summary>Điền MaThung cho các dòng MES đã nằm trong thùng (chưa xoá), đếm theo dấu vân tay.</summary>
        public async Task GanMaThungAsync(List<MesApiService.DongDongGoi> dong, CancellationToken ct)
        {
            if (dong.Count == 0) return;
            var dsVanTay = dong.Select(d => d.DauVanTay).Distinct().ToList();
            var daCo = await _db.QaThungDongs
                .Where(x => dsVanTay.Contains(x.DauVanTay) && x.NgayBo == null && x.Thung.NgayXoa == null)
                .OrderBy(x => x.IdDong)
                .Select(x => new { x.IdDong, x.DauVanTay, x.SoBan, x.Thung.MaThung, x.Thung.TrangThai,
                    // Kệ đã xoá mềm thì coi như chưa xếp kệ
                    MaKe = x.Thung.Ke != null && x.Thung.Ke.NgayXoa == null ? x.Thung.Ke.MaKe : null,
                    SoTang = x.Thung.Ke != null ? x.Thung.Ke.SoTang : 0, x.Thung.Tang, x.Thung.O })
                .ToListAsync(ct);
            var hang = daCo.GroupBy(x => x.DauVanTay)
                .ToDictionary(g => g.Key, g => new Queue<(int id, string ma, string tt, string? viTri, int? soBan)>(g.Select(x =>
                    (x.IdDong, x.MaThung, x.TrangThai, QaKeService.MaViTri(x.MaKe, x.Tang, x.O, x.SoTang), x.SoBan))));
            foreach (var d in dong)
                if (hang.TryGetValue(d.DauVanTay, out var q) && q.Count > 0)
                {
                    var (id, ma, tt, viTri, soBan) = q.Dequeue();
                    d.MaThung = ma;
                    d.ViTri = viTri;
                    d.SoBan = soBan;
                    d.IdDongThung = id;
                    d.ThungDaDong = tt != QaThung.TrangThaiMo;
                }

            // Dòng chưa vào thùng: đánh thứ tự trong nhóm giống hệt nhau + lấy số paiban đang chờ (QA_MesSoBan)
            var demChuaVao = new Dictionary<string, int>();
            foreach (var d in dong.Where(x => x.IdDongThung == null))
            {
                d.ThuTu = demChuaVao.GetValueOrDefault(d.DauVanTay);
                demChuaVao[d.DauVanTay] = d.ThuTu.Value + 1;
            }
            if (demChuaVao.Count == 0 || !await CoBangSoBanAsync(ct)) return;
            var cho = new List<(string VanTay, int? SoBan)>();
            // Chia lô vì IN (...) quá dài khi xem toàn bộ dữ liệu (hàng nghìn vân tay)
            foreach (var lo in demChuaVao.Keys.Chunk(2000))
                cho.AddRange((await _db.QaMesSoBans
                    .Where(x => lo.Contains(x.DauVanTay) && x.IdDongThung == null)
                    .OrderBy(x => x.ThuTu).ThenBy(x => x.IdSoBan)
                    .Select(x => new { x.DauVanTay, x.SoBan })
                    .ToListAsync(ct)).Select(x => (x.DauVanTay, x.SoBan)));
            var theoVanTay = cho.GroupBy(x => x.VanTay).ToDictionary(g => g.Key, g => g.Select(x => x.SoBan).ToList());
            foreach (var d in dong.Where(x => x.IdDongThung == null))
                if (theoVanTay.TryGetValue(d.DauVanTay, out var ds) && d.ThuTu < ds.Count)
                    d.SoBan = ds[d.ThuTu!.Value];
        }

        // Bảng QA_MesSoBan có thể chưa tạo trên DB (script chạy tay) -> kiểm 1 lần, chưa có thì 5 phút kiểm lại
        private static bool? _coBangSoBan;
        private static DateTime _lucKiemBangSoBan;
        private async Task<bool> CoBangSoBanAsync(CancellationToken ct)
        {
            if (_coBangSoBan == true) return true;
            if (_coBangSoBan == false && DateTime.Now - _lucKiemBangSoBan < TimeSpan.FromMinutes(5)) return false;
            var id = await _db.Database.SqlQueryRaw<int?>("SELECT OBJECT_ID('dbo.QA_MesSoBan', 'U') AS Value").FirstOrDefaultAsync(ct);
            _coBangSoBan = id != null;
            _lucKiemBangSoBan = DateTime.Now;
            return _coBangSoBan.Value;
        }

        /// <summary>
        /// Lưu số paiban cho dòng MES CHƯA vào thùng: dòng thứ thuTu (trong các dòng giống hệt nhau chưa vào thùng)
        /// của vân tay đó. Bản ghi chờ thiếu thì thêm bản ghi trống cho đủ để giữ đúng vị trí.
        /// </summary>
        public async Task<KetQua> LuuSoBanMesAsync(string? dauVanTay, int thuTu, int? soBan, string? nguoi, CancellationToken ct)
        {
            dauVanTay = (dauVanTay ?? "").Trim();
            if (dauVanTay.Length == 0 || dauVanTay.Length > 64 || thuTu < 0 || thuTu > 100) return new(false, "Dòng không hợp lệ, bấm Tra cứu lại.");
            if (soBan < 0 || soBan > TranSoBan) return new(false, $"Số paiban phải từ 0 đến {TranSoBan:N0}.");
            if (!await CoBangSoBanAsync(ct)) return new(false, "Chưa tạo bảng QA_MesSoBan trên DB — báo IT chạy script qa-mes-so-ban-20261113.sql.");
            if (!await _db.QaMesDongGois.AnyAsync(x => x.DauVanTay == dauVanTay && x.NgayMat == null, ct))
                return new(false, "Dòng không còn trong dữ liệu MES, bấm Tra cứu lại.");

            await using var tx = await _db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
            var cho = await _db.QaMesSoBans.Where(x => x.DauVanTay == dauVanTay && x.IdDongThung == null)
                .OrderBy(x => x.ThuTu).ThenBy(x => x.IdSoBan).ToListAsync(ct);
            var bayGio = DateTime.Now;
            int thuTuMoi = cho.Count == 0 ? 0 : cho.Max(x => x.ThuTu) + 1;
            while (cho.Count <= thuTu)
            {
                var them = new QaMesSoBan { DauVanTay = dauVanTay, ThuTu = thuTuMoi++, NgaySua = bayGio, NguoiSua = nguoi };
                _db.QaMesSoBans.Add(them);
                cho.Add(them);
            }
            var dongCho = cho[thuTu];
            dongCho.SoBan = soBan;
            dongCho.NgaySua = bayGio;
            dongCho.NguoiSua = nguoi;
            await _db.SaveChangesAsync(ct);
            await tx.CommitAsync(ct);
            return new(true, "Đã lưu số paiban.");
        }

        /// <summary>
        /// Danh sách thùng (mới nhất trước, tối đa 500). tuKhoa: khớp mã thùng HOẶC hàng đang nằm trong thùng
        /// (mã phối màu, mã hàng, mã màu, số phiếu đóng gói, số phiếu nhuộm) — để tìm "mã phối màu X đang ở thùng nào".
        /// Dòng đã bỏ ra (NgayBo) không tính: hàng đã chuyển đi thì thùng cũ không còn chứa nó.
        /// </summary>
        public async Task<List<ThungTomTat>> DsThungAsync(bool chiThungMo, string? tuKhoa, CancellationToken ct)
        {
            var q = _db.QaThungs.Where(t => t.NgayXoa == null);
            if (chiThungMo) q = q.Where(t => t.TrangThai == QaThung.TrangThaiMo);

            var k = tuKhoa?.Trim();
            if (k?.Length > 100) k = k[..100];
            var coTim = !string.IsNullOrEmpty(k);
            if (coTim)
                q = q.Where(t => t.MaThung.Contains(k!) || t.TenThung!.Contains(k!) || t.Dongs.Any(d => d.NgayBo == null &&
                    (d.MaPhoiMau!.Contains(k!) || d.MaHang!.Contains(k!) || d.MaMau!.Contains(k!)
                     || d.SoPhieuDongGoi!.Contains(k!) || d.SoPhieuNhuom!.Contains(k!))));

            var ds = await q.OrderByDescending(t => t.NgayTao)
                .Take(500)
                .Select(t => new ThungTomTat(t.IdThung, t.MaThung, t.TenThung, t.TrangThai, t.GhiChu, t.NgayTao, t.NguoiTao,
                    t.Dongs.Count(d => d.NgayBo == null),
                    t.Dongs.Where(d => d.NgayBo == null).Sum(d => d.SlDongGoi) ?? 0,
                    t.Dongs.Where(d => d.NgayBo == null).Sum(d => d.KlDongGoi) ?? 0,
                    t.Dongs.Where(d => d.NgayBo == null).Sum(d => d.SoHop) ?? 0,
                    t.IdKe, QaKeService.MaViTri(t.Ke != null ? t.Ke.MaKe : null, t.Tang, t.O, t.Ke != null ? t.Ke.SoTang : 0),
                    null, 0))
                .ToListAsync(ct);
            if (!coTim || ds.Count == 0) return ds;

            // Ghi rõ thùng khớp vì hàng nào (vd mã phối màu đầy đủ khi chỉ gõ một phần) + số dòng khớp
            var dsId = ds.Select(t => t.IdThung).ToList();
            var dongKhop = await _db.QaThungDongs
                .Where(d => dsId.Contains(d.IdThung) && d.NgayBo == null &&
                    (d.MaPhoiMau!.Contains(k!) || d.MaHang!.Contains(k!) || d.MaMau!.Contains(k!)
                     || d.SoPhieuDongGoi!.Contains(k!) || d.SoPhieuNhuom!.Contains(k!)))
                .Select(d => new { d.IdThung, d.MaPhoiMau, d.MaHang, d.MaMau, d.SoPhieuDongGoi, d.SoPhieuNhuom })
                .ToListAsync(ct);
            var theoThung = dongKhop.GroupBy(d => d.IdThung).ToDictionary(g => g.Key, g =>
            {
                // Lấy đúng giá trị chứa từ khoá, ưu tiên mã phối màu
                var giaTri = g.Select(d => new[] { d.MaPhoiMau, d.MaHang, d.MaMau, d.SoPhieuDongGoi, d.SoPhieuNhuom }
                        .FirstOrDefault(v => v != null && v.Contains(k!, StringComparison.OrdinalIgnoreCase)))
                    .Where(v => v != null).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
                var chu = string.Join(", ", giaTri.Take(3)) + (giaTri.Count > 3 ? $" +{giaTri.Count - 3}" : "");
                return (chu, g.Count());
            });
            return ds.Select(t => theoThung.TryGetValue(t.IdThung, out var x) ? t with { Khop = x.chu, SoDongKhop = x.Item2 } : t).ToList();
        }

        public async Task<(QaThung? thung, List<QaThungDong> dong, List<QaThungLichSu> lichSu)> ChiTietAsync(int idThung, CancellationToken ct)
        {
            var thung = await _db.QaThungs.AsNoTracking().Include(t => t.Ke).FirstOrDefaultAsync(t => t.IdThung == idThung && t.NgayXoa == null, ct);
            if (thung == null) return (null, new(), new());
            var dong = await _db.QaThungDongs.AsNoTracking().Where(d => d.IdThung == idThung && d.NgayBo == null).OrderBy(d => d.IdDong).ToListAsync(ct);
            var lichSu = await _db.QaThungLichSus.AsNoTracking().Where(l => l.IdThung == idThung).OrderByDescending(l => l.Ngay).Take(100).ToListAsync(ct);
            return (thung, dong, lichSu);
        }

        /// <summary>
        /// Bỏ các dòng MES (theo dấu vân tay, có thể lặp nếu chọn nhiều dòng giống hệt) vào thùng.
        /// idThung null = tạo thùng mới mã QA-yyMMdd-NNN.
        /// dsIdDongChuyen: các dòng ĐANG ở thùng khác (id QA_ThungDong) cần chuyển sang thùng đích — làm cùng transaction.
        /// </summary>
        public async Task<KetQua> BoVaoThungAsync(string maPhoiMau, List<string> dsVanTay, List<int>? dsIdDongChuyen, int? idThung, string? ghiChu, string? nguoi, CancellationToken ct, List<int>? dsThuTu = null, string? tenThung = null)
        {
            maPhoiMau = (maPhoiMau ?? "").Trim();
            // dsThuTu song song dsVanTay: thứ tự dòng chưa vào thùng đã tích -> mang số paiban đang chờ của đúng dòng đó sang thùng
            var thuTuTheoVanTay = new Dictionary<string, List<int>>();
            if (dsThuTu != null && dsVanTay != null && dsThuTu.Count == dsVanTay.Count)
                for (int i = 0; i < dsVanTay.Count; i++)
                {
                    if (string.IsNullOrWhiteSpace(dsVanTay[i])) continue;
                    if (!thuTuTheoVanTay.TryGetValue(dsVanTay[i], out var l)) thuTuTheoVanTay[dsVanTay[i]] = l = new();
                    l.Add(dsThuTu[i]);
                }
            dsVanTay = (dsVanTay ?? new()).Where(v => !string.IsNullOrWhiteSpace(v)).ToList();
            var dsChuyen = (dsIdDongChuyen ?? new()).Where(i => i > 0).Distinct().ToList();
            if (dsVanTay.Count == 0 && dsChuyen.Count == 0) return new(false, "Chưa chọn dòng nào.");
            if (dsVanTay.Count + dsChuyen.Count > TranDongMoiLan) return new(false, $"Mỗi lần chỉ bỏ tối đa {TranDongMoiLan} dòng.");
            ghiChu = string.IsNullOrWhiteSpace(ghiChu) ? null : ghiChu.Trim();
            if (ghiChu?.Length > 500) ghiChu = ghiChu[..500];
            // Tên thùng người nhập (ten_thung) bắt buộc khi tạo thùng mới; mã thùng vẫn tự sinh QA-yyMMdd-NNN
            tenThung = string.IsNullOrWhiteSpace(tenThung) ? null : tenThung.Trim();
            if (idThung == null && tenThung == null) return new(false, "Nhập tên thùng trước khi tạo thùng mới.");
            if (idThung == null && tenThung!.Length > 100) return new(false, "Tên thùng tối đa 100 ký tự.");

            // 1) Lấy lại dữ liệu gốc từ MES (ngoài transaction vì gọi mạng chậm) — chỉ cần khi có dòng mới
            var theoVanTay = new Dictionary<string, List<MesApiService.DongDongGoi>>();
            if (dsVanTay.Count > 0)
            {
                // Cùng nguồn trang tra cứu vừa hiện: tìm thẳng theo vân tay trong bản sao DB (kết quả tra trống có nhiều mã);
                // thiếu dòng nào (DB chưa có / chưa tạo bảng) thì mới tra theo mã phối màu (kho tự gọi MES)
                var tuDb = new List<MesApiService.DongDongGoi>();
                try { tuDb = await _kho.TimTheoVanTayAsync(dsVanTay.Distinct().ToList(), ct); }
                catch (Exception ex) when (ex is not OperationCanceledException) { _logger.LogError(ex, "Không đọc được QA_MesDongGoi khi bỏ vào thùng"); }
                theoVanTay = tuDb.GroupBy(d => d.DauVanTay).ToDictionary(g => g.Key, g => g.ToList());
                if (dsVanTay.Any(v => !theoVanTay.ContainsKey(v)))
                {
                    if (maPhoiMau.Length < 4) return new(false, "Không tìm thấy dòng đã chọn trong dữ liệu đồng bộ, tra lại rồi thử lại.");
                    var mes = (await _kho.TraCuuAsync(maPhoiMau, ct)).Kq;
                    if (!mes.ThanhCong) return new(false, mes.ThongBao);
                    theoVanTay = mes.Dong!.GroupBy(d => d.DauVanTay).ToDictionary(g => g.Key, g => g.ToList());
                }
            }

            // 2) Khoá đọc-ghi trong transaction serializable để 2 người bấm cùng lúc không cùng lấy 1 dòng / cùng 1 mã thùng
            await using var tx = await _db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
            try
            {
                var vanTayCanXet = dsVanTay.Distinct().ToList();
                var daDung = await _db.QaThungDongs
                    .Where(x => vanTayCanXet.Contains(x.DauVanTay) && x.NgayBo == null && x.Thung.NgayXoa == null)
                    .GroupBy(x => x.DauVanTay)
                    .Select(g => new { g.Key, SoLuong = g.Count() })
                    .ToDictionaryAsync(x => x.Key, x => x.SoLuong, ct);

                int khongCon = 0, daOThung = 0;
                var dongBoVao = new List<MesApiService.DongDongGoi>();
                foreach (var g in dsVanTay.GroupBy(v => v))
                {
                    if (!theoVanTay.TryGetValue(g.Key, out var dongMes)) { khongCon += g.Count(); continue; }
                    int conLai = dongMes.Count - daDung.GetValueOrDefault(g.Key);
                    int lay = Math.Min(conLai, g.Count());
                    daOThung += g.Count() - Math.Max(lay, 0);
                    if (lay > 0) dongBoVao.AddRange(dongMes.Take(lay));
                }
                if (khongCon > 0 || daOThung > 0)
                {
                    var loi = new List<string>();
                    if (daOThung > 0) loi.Add($"{daOThung} dòng đã nằm trong thùng khác");
                    if (khongCon > 0) loi.Add($"{khongCon} dòng không còn trên MES (dữ liệu MES đã đổi)");
                    return new(false, string.Join("; ", loi) + ". Bấm Tra cứu lại rồi chọn lại.");
                }

                // Dòng cần chuyển: phải còn ở thùng (chưa bị người khác chuyển/bỏ) và thùng nguồn đang mở
                var dongChuyen = dsChuyen.Count == 0 ? new List<QaThungDong>() : await _db.QaThungDongs
                    .Include(x => x.Thung)
                    .Where(x => dsChuyen.Contains(x.IdDong) && x.NgayBo == null && x.Thung.NgayXoa == null)
                    .ToListAsync(ct);
                if (dongChuyen.Count != dsChuyen.Count)
                    return new(false, $"{dsChuyen.Count - dongChuyen.Count} dòng không còn ở thùng cũ (có thể vừa bị người khác chuyển/bỏ). Bấm Tra cứu lại rồi chọn lại.");
                var thungNguonDong = dongChuyen.Where(x => x.Thung.TrangThai != QaThung.TrangThaiMo).Select(x => x.Thung.MaThung).Distinct().ToList();
                if (thungNguonDong.Count > 0)
                    return new(false, $"Thùng {string.Join(", ", thungNguonDong)} đã đóng — mở lại ở Danh sách thùng rồi mới chuyển dòng ra được.");
                // Dòng đã ở đúng thùng đích thì bỏ qua
                if (idThung != null) dongChuyen = dongChuyen.Where(x => x.IdThung != idThung).ToList();
                if (dongBoVao.Count == 0 && dongChuyen.Count == 0)
                    return new(false, "Các dòng đã chọn đã nằm sẵn trong thùng này.");

                QaThung thung;
                var bayGio = DateTime.Now;
                if (idThung == null)
                {
                    // Tên không trùng thùng đang dùng (thùng đã xoá thì tên được dùng lại)
                    if (await _db.QaThungs.AnyAsync(t => t.TenThung == tenThung && t.NgayXoa == null, ct))
                        return new(false, $"Đã có thùng tên \"{tenThung}\", đặt tên khác.");
                    // Lấy số lớn nhất parse được trong ngày (mã cấp lại bởi script cũng theo dạng này)
                    var tienTo = $"QA-{bayGio:yyMMdd}-";
                    var dsMa = await _db.QaThungs.Where(t => t.MaThung.StartsWith(tienTo))
                        .Select(t => t.MaThung).ToListAsync(ct);
                    int so = dsMa.Select(m => int.TryParse(m[tienTo.Length..], out var n) ? n : 0).DefaultIfEmpty(0).Max() + 1;
                    thung = new QaThung
                    {
                        MaThung = tienTo + so.ToString("000"),
                        TenThung = tenThung,
                        TrangThai = QaThung.TrangThaiMo,
                        GhiChu = ghiChu,
                        NgayTao = bayGio,
                        NguoiTao = nguoi
                    };
                    _db.QaThungs.Add(thung);
                    await _db.SaveChangesAsync(ct);
                    GhiLichSu(thung.IdThung, nguoi, "TaoThung", ghiChu == null ? null : "Ghi chú: " + ghiChu, bayGio);
                }
                else
                {
                    var tim = await _db.QaThungs.FirstOrDefaultAsync(t => t.IdThung == idThung && t.NgayXoa == null, ct);
                    if (tim == null) return new(false, "Không tìm thấy thùng (có thể đã bị xoá).");
                    if (tim.TrangThai != QaThung.TrangThaiMo) return new(false, $"Thùng {tim.MaThung} đã đóng, không thêm được.");
                    thung = tim;
                }

                var dongMoi = new List<QaThungDong>();
                foreach (var d in dongBoVao)
                {
                    var moi = new QaThungDong
                    {
                        IdThung = thung.IdThung,
                        DauVanTay = d.DauVanTay,
                        NgayThem = bayGio,
                        NguoiThem = nguoi,
                        MaPhoiMau = Cat(d.OrderUcode, 50),
                        MaKhachCuoi = Cat(d.EndCustCode, 50),
                        MaKhachHang = Cat(d.CustCode, 50),
                        MaHang = Cat(d.ArtCode, 100),
                        MaMau = Cat(d.ColorCode, 100),
                        SlDonHang = d.PmcNum,
                        SlDongGoi = d.PbNum,
                        KlDongGoi = d.PbNw,
                        SoHop = d.PbBoxnum,
                        Loai = Cat(d.PbLv, 20),
                        MoTaBatThuong = Cat(d.PbQcdes, 500),
                        YKienQc = Cat(d.HandleMethod, 500),
                        CongDoan = Cat(d.ManuStep, 50),
                        SoPhieuNhuom = Cat(d.ClNo, 50),
                        NgayNhanHang = Cat(d.GetDate, 30),
                        NgayDongGoi = Cat(d.PbDate, 30),
                        SoPhieuDongGoi = Cat(d.PackclNo, 50),
                        MaNvDongGoi = Cat(d.PbGh, 50),
                        GhiChuMes = Cat(d.Remark, 500),
                        DuLieuMes = d.Raw
                    };
                    _db.QaThungDongs.Add(moi);
                    dongMoi.Add(moi);
                }
                // Mang số paiban đang chờ (nhập lúc dòng chưa vào thùng) sang dòng thùng mới
                var daMang = new List<(QaMesSoBan Cho, QaThungDong Dong)>();
                if (dongMoi.Count > 0 && await CoBangSoBanAsync(ct))
                {
                    var vt = dongMoi.Select(x => x.DauVanTay).Distinct().ToList();
                    var choTatCa = await _db.QaMesSoBans.Where(x => vt.Contains(x.DauVanTay) && x.IdDongThung == null)
                        .OrderBy(x => x.ThuTu).ThenBy(x => x.IdSoBan).ToListAsync(ct);
                    foreach (var g in dongMoi.GroupBy(x => x.DauVanTay))
                    {
                        var cho = choTatCa.Where(x => x.DauVanTay == g.Key).ToList();
                        // Không có thứ tự từ trình duyệt thì lấy theo thứ tự đầu tiên
                        var thuTu = thuTuTheoVanTay.TryGetValue(g.Key, out var l) ? l.Distinct().OrderBy(x => x).ToList() : Enumerable.Range(0, g.Count()).ToList();
                        foreach (var (dongThung, t) in g.Zip(thuTu))
                            if (t >= 0 && t < cho.Count)
                            {
                                dongThung.SoBan = cho[t].SoBan;
                                daMang.Add((cho[t], dongThung));
                            }
                    }
                }
                if (dongBoVao.Count > 0)
                    GhiLichSu(thung.IdThung, nguoi, "ThemDong",
                        $"Thêm {dongBoVao.Count} dòng của mã phối màu {maPhoiMau}; tổng {dongBoVao.Sum(d => d.PbNum ?? 0):0.###} m, {dongBoVao.Sum(d => d.PbNw ?? 0):0.###} kg",
                        bayGio);

                // Chuyển dòng từ thùng khác: đổi id_thung (giữ ngày/người thêm gốc), ghi lịch sử ở cả thùng nguồn và đích
                if (dongChuyen.Count > 0)
                {
                    var tuThung = new List<string>();
                    foreach (var g in dongChuyen.GroupBy(x => x.Thung))
                    {
                        GhiLichSu(g.Key.IdThung, nguoi, "ChuyenDi",
                            $"Chuyển {g.Count()} dòng sang thùng {thung.MaThung}; tổng {g.Sum(d => d.SlDongGoi ?? 0):0.###} m, {g.Sum(d => d.KlDongGoi ?? 0):0.###} kg",
                            bayGio);
                        tuThung.Add($"{g.Key.MaThung} ({g.Count()} dòng)");
                    }
                    foreach (var d in dongChuyen) { d.IdThung = thung.IdThung; d.Thung = thung; }
                    GhiLichSu(thung.IdThung, nguoi, "ChuyenDen",
                        $"Nhận {dongChuyen.Count} dòng từ thùng {string.Join(", ", tuThung)}; tổng {dongChuyen.Sum(d => d.SlDongGoi ?? 0):0.###} m, {dongChuyen.Sum(d => d.KlDongGoi ?? 0):0.###} kg",
                        bayGio);
                }
                await _db.SaveChangesAsync(ct);
                if (daMang.Count > 0)
                {
                    foreach (var (cho, dongThung) in daMang) { cho.IdDongThung = dongThung.IdDong; cho.NgaySua = bayGio; cho.NguoiSua = nguoi; }
                    await _db.SaveChangesAsync(ct);
                }
                await tx.CommitAsync(ct);

                var phan = new List<string>();
                if (dongBoVao.Count > 0) phan.Add($"thêm {dongBoVao.Count} dòng");
                if (dongChuyen.Count > 0) phan.Add($"chuyển {dongChuyen.Count} dòng từ thùng khác");
                return new(true, (idThung == null ? $"Đã tạo thùng {thung.MaThung} \"{thung.TenThung}\": " : $"Thùng {thung.MaThung}: ") + string.Join(", ", phan) + ".",
                    thung.IdThung, thung.MaThung);
            }
            catch (DbUpdateException ex)
            {
                // Thường do 2 người tạo thùng cùng giây trùng mã (UQ_QA_Thung_MaThung) -> báo bấm lại
                _logger.LogWarning(ex, "Lỗi lưu thùng QA");
                return new(false, "Không lưu được (có thể trùng thao tác với người khác), bấm lại lần nữa.");
            }
        }

        /// <summary>
        /// Bỏ các dòng (id QA_ThungDong) ra khỏi thùng = xoá mềm (ngay_bo/nguoi_bo), dòng MES được trả lại để bỏ vào thùng khác.
        /// Thùng đã đóng thì phải mở lại trước.
        /// </summary>
        public async Task<KetQua> BoRaKhoiThungAsync(List<int>? dsIdDong, string? nguoi, CancellationToken ct)
        {
            var ds = (dsIdDong ?? new()).Where(i => i > 0).Distinct().ToList();
            if (ds.Count == 0) return new(false, "Chưa chọn dòng nào đang ở thùng.");
            if (ds.Count > TranDongMoiLan) return new(false, $"Mỗi lần chỉ bỏ tối đa {TranDongMoiLan} dòng.");

            await using var tx = await _db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
            var dong = await _db.QaThungDongs.Include(x => x.Thung)
                .Where(x => ds.Contains(x.IdDong) && x.NgayBo == null && x.Thung.NgayXoa == null)
                .ToListAsync(ct);
            if (dong.Count != ds.Count)
                return new(false, $"{ds.Count - dong.Count} dòng không còn ở thùng (có thể vừa bị người khác chuyển/bỏ). Bấm Tra cứu lại rồi chọn lại.");
            var thungDong = dong.Where(x => x.Thung.TrangThai != QaThung.TrangThaiMo).Select(x => x.Thung.MaThung).Distinct().ToList();
            if (thungDong.Count > 0)
                return new(false, $"Thùng {string.Join(", ", thungDong)} đã đóng — mở lại ở Danh sách thùng rồi mới bỏ dòng ra được.");

            var bayGio = DateTime.Now;
            foreach (var d in dong) { d.NgayBo = bayGio; d.NguoiBo = nguoi; }
            // Số paiban đã nhập đi theo dòng: trả về bản ghi chờ để dòng chưa vào thùng vẫn thấy số
            var coSo = dong.Where(x => x.SoBan != null).ToList();
            if (coSo.Count > 0 && await CoBangSoBanAsync(ct))
            {
                var vt = coSo.Select(x => x.DauVanTay).Distinct().ToList();
                var maxThuTu = await _db.QaMesSoBans.Where(x => vt.Contains(x.DauVanTay) && x.IdDongThung == null)
                    .GroupBy(x => x.DauVanTay).Select(g => new { g.Key, Max = g.Max(x => x.ThuTu) })
                    .ToDictionaryAsync(x => x.Key, x => x.Max, ct);
                foreach (var d in coSo)
                {
                    int t = maxThuTu.TryGetValue(d.DauVanTay, out var m) ? m + 1 : 0;
                    maxThuTu[d.DauVanTay] = t;
                    _db.QaMesSoBans.Add(new QaMesSoBan { DauVanTay = d.DauVanTay, ThuTu = t, SoBan = d.SoBan, NgaySua = bayGio, NguoiSua = nguoi });
                }
            }
            foreach (var g in dong.GroupBy(x => x.Thung))
                GhiLichSu(g.Key.IdThung, nguoi, "BoDong",
                    $"Bỏ {g.Count()} dòng ra khỏi thùng; tổng {g.Sum(d => d.SlDongGoi ?? 0):0.###} m, {g.Sum(d => d.KlDongGoi ?? 0):0.###} kg",
                    bayGio);
            await _db.SaveChangesAsync(ct);
            await tx.CommitAsync(ct);
            var tenThung = string.Join(", ", dong.Select(x => x.Thung.MaThung).Distinct());
            return new(true, $"Đã bỏ {dong.Count} dòng ra khỏi thùng {tenThung}.");
        }

        public const int TranSoBan = 1_000_000;

        /// <summary>
        /// Sửa số bản nhập tay của 1 dòng đang ở thùng (soBan null = xoá số đã nhập). Thùng đã đóng vẫn sửa được
        /// vì số bản chỉ là số đếm ghi thêm, không đổi hàng trong thùng. Không đổi thì không ghi nhật ký.
        /// </summary>
        public async Task<KetQua> SuaSoBanAsync(int idDong, int? soBan, string? nguoi, CancellationToken ct)
        {
            if (soBan < 0 || soBan > TranSoBan) return new(false, $"Số paiban phải từ 0 đến {TranSoBan:N0}.");
            var dong = await _db.QaThungDongs.Include(x => x.Thung)
                .FirstOrDefaultAsync(x => x.IdDong == idDong && x.NgayBo == null && x.Thung.NgayXoa == null, ct);
            if (dong == null) return new(false, "Dòng không còn ở thùng (có thể vừa bị bỏ ra/chuyển đi) — mở lại chi tiết thùng.");
            if (dong.SoBan == soBan) return new(true, "Không có gì thay đổi.", dong.IdThung, dong.Thung.MaThung);

            var cu = dong.SoBan?.ToString() ?? "trống";
            dong.SoBan = soBan;
            GhiLichSu(dong.IdThung, nguoi, "SuaSoBan",
                $"Dòng {dong.MaPhoiMau} · phiếu đóng gói {dong.SoPhieuDongGoi} · {dong.SlDongGoi:0.###} m: số paiban {cu} → {soBan?.ToString() ?? "trống"}",
                DateTime.Now);
            await _db.SaveChangesAsync(ct);
            return new(true, "Đã lưu số paiban.", dong.IdThung, dong.Thung.MaThung);
        }

        public async Task<KetQua> DoiTrangThaiAsync(int idThung, bool dong, string? nguoi, CancellationToken ct)
        {
            var thung = await _db.QaThungs.FirstOrDefaultAsync(t => t.IdThung == idThung && t.NgayXoa == null, ct);
            if (thung == null) return new(false, "Không tìm thấy thùng.");
            var moi = dong ? QaThung.TrangThaiDaDong : QaThung.TrangThaiMo;
            if (thung.TrangThai == moi) return new(true, "Không có gì thay đổi.", thung.IdThung, thung.MaThung);

            var bayGio = DateTime.Now;
            thung.TrangThai = moi;
            thung.NgayDong = dong ? bayGio : null;
            thung.NguoiDong = dong ? nguoi : null;
            GhiLichSu(thung.IdThung, nguoi, dong ? "DongThung" : "MoLai", null, bayGio);
            await _db.SaveChangesAsync(ct);
            return new(true, dong ? $"Đã đóng thùng {thung.MaThung}." : $"Đã mở lại thùng {thung.MaThung}.", thung.IdThung, thung.MaThung);
        }

        /// <summary>Đổi tên thùng (ten_thung; mã thùng giữ nguyên); ghi lịch sử tên cũ → mới.</summary>
        public async Task<KetQua> DoiTenThungAsync(int idThung, string? tenMoi, string? nguoi, CancellationToken ct)
        {
            tenMoi = tenMoi?.Trim();
            if (string.IsNullOrEmpty(tenMoi)) return new(false, "Nhập tên thùng.");
            if (tenMoi.Length > 100) return new(false, "Tên thùng tối đa 100 ký tự.");
            var thung = await _db.QaThungs.FirstOrDefaultAsync(t => t.IdThung == idThung && t.NgayXoa == null, ct);
            if (thung == null) return new(false, "Không tìm thấy thùng.");
            if (thung.TenThung == tenMoi) return new(true, "Không có gì thay đổi.", thung.IdThung, thung.MaThung);
            if (await _db.QaThungs.AnyAsync(t => t.TenThung == tenMoi && t.IdThung != idThung && t.NgayXoa == null, ct))
                return new(false, $"Đã có thùng tên \"{tenMoi}\", đặt tên khác.");

            var tenCu = thung.TenThung;
            thung.TenThung = tenMoi;
            GhiLichSu(thung.IdThung, nguoi, "DoiTen", $"Đổi tên: {tenCu ?? "(trống)"} → {tenMoi}", DateTime.Now);
            await _db.SaveChangesAsync(ct);
            return new(true, $"Đã đổi tên thùng {thung.MaThung} thành \"{tenMoi}\".", thung.IdThung, thung.MaThung);
        }

        /// <summary>Sửa ghi chú thùng (rỗng = xoá ghi chú); ghi lịch sử nội dung cũ → mới.</summary>
        public async Task<KetQua> SuaGhiChuAsync(int idThung, string? ghiChu, string? nguoi, CancellationToken ct)
        {
            ghiChu = string.IsNullOrWhiteSpace(ghiChu) ? null : ghiChu.Trim();
            if (ghiChu?.Length > 500) return new(false, "Ghi chú tối đa 500 ký tự.");
            var thung = await _db.QaThungs.FirstOrDefaultAsync(t => t.IdThung == idThung && t.NgayXoa == null, ct);
            if (thung == null) return new(false, "Không tìm thấy thùng.");
            if (thung.GhiChu == ghiChu) return new(true, "Không có gì thay đổi.", thung.IdThung, thung.MaThung);

            var cu = thung.GhiChu;
            thung.GhiChu = ghiChu;
            GhiLichSu(thung.IdThung, nguoi, "SuaGhiChu", Cat($"Ghi chú: {cu ?? "(trống)"} → {ghiChu ?? "(trống)"}", 1000), DateTime.Now);
            await _db.SaveChangesAsync(ct);
            return new(true, $"Đã lưu ghi chú thùng {thung.MaThung}.", thung.IdThung, thung.MaThung);
        }

        /// <summary>Xoá mềm: các dòng trong thùng được "trả lại" (không còn tính là đã vào thùng).</summary>
        public async Task<KetQua> XoaThungAsync(int idThung, string? nguoi, CancellationToken ct)
        {
            var thung = await _db.QaThungs.FirstOrDefaultAsync(t => t.IdThung == idThung && t.NgayXoa == null, ct);
            if (thung == null) return new(false, "Không tìm thấy thùng.");
            var bayGio = DateTime.Now;
            thung.NgayXoa = bayGio;
            thung.NguoiXoa = nguoi;
            int soDong = await _db.QaThungDongs.CountAsync(d => d.IdThung == idThung && d.NgayBo == null, ct);
            GhiLichSu(thung.IdThung, nguoi, "XoaThung", $"Xoá thùng {thung.MaThung} ({soDong} dòng)", bayGio);
            await _db.SaveChangesAsync(ct);
            return new(true, $"Đã xoá thùng {thung.MaThung}.", thung.IdThung, thung.MaThung);
        }

        private void GhiLichSu(int idThung, string? nguoi, string hanhDong, string? noiDung, DateTime ngay)
            => _db.QaThungLichSus.Add(new QaThungLichSu { IdThung = idThung, Nguoi = nguoi, HanhDong = hanhDong, NoiDung = noiDung, Ngay = ngay });

        private static string? Cat(string? s, int max) => s == null ? null : (s.Length > max ? s[..max] : s);
    }
}
