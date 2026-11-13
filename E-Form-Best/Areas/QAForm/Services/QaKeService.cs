using System.Data;
using System.Text.RegularExpressions;
using E_Form_Best.Context;
using E_Form_Best.Models.ITForm;
using Microsoft.EntityFrameworkCore;

namespace E_Form_Best.Areas.QAForm.Services
{
    /// <summary>
    /// Kệ chứa thùng QA (QA_Ke) + vị trí thùng trên kệ (QA_Thung.id_ke/tang/o).
    /// Kệ chia ô theo tầng × ô, mã vị trí = MaKe-tầng-ô; kệ 1 tầng: kệ cha A, ô con A1…A6, vị trí "A-A1".
    /// 1 ô chứa được nhiều thùng nên không chặn trùng ô.
    /// Tầng đánh số từ dưới lên (tầng 1 sát sàn), ô từ trái sang.
    /// </summary>
    public class QaKeService
    {
        public record KetQua(bool ThanhCong, string ThongBao, int? IdKe = null);

        public record KeTomTat(int IdKe, string MaKe, string? TenKe, string? KhuVuc, int SoTang, int SoO, string? GhiChu, int SoThung);

        public record ThungTrenKe(int IdThung, string MaThung, string TrangThai, int Tang, int O, int SoDong);

        public record LuuKeVao(int? IdKe, string? MaKe, string? TenKe, string? KhuVuc, int SoTang, int SoO, string? GhiChu);

        // Tạo nhanh dãy kệ 1 chữ cái TuMa..DenMa (vd A..Z), cùng kích thước
        public record TaoDayKeVao(string? TuMa, string? DenMa, int SoTang, int SoO, string? KhuVuc);

        public const int TranTangO = 50;

        // Mã kệ in lên tem/vị trí nên chỉ cho chữ, số, gạch dưới, chấm
        private static readonly Regex MaKeHopLe = new("^[A-Z0-9_.]{1,20}$", RegexOptions.Compiled);

        private readonly ITFormContext _db;

        public QaKeService(ITFormContext db) => _db = db;

        /// <summary>
        /// Mã vị trí hiển thị. Kệ 1 tầng: "A-A3" (kệ cha A, ô con A3); kệ nhiều tầng "A-2-3".
        /// soTang = 0: không rõ số tầng -> dạng đầy đủ.
        /// </summary>
        public static string? MaViTri(string? maKe, int? tang, int? o, int soTang = 0)
        {
            if (maKe == null || tang == null || o == null) return null;
            if (soTang == 1) return $"{maKe}-{maKe}{o}";
            return $"{maKe}-{tang}-{o}";
        }

        // "A3" / "AB12" (chữ + số dính liền) = ô con của kệ 1 tầng
        private static readonly Regex ViTriGon = new("^([A-Z_.]*[A-Z_.])([0-9]+)$", RegexOptions.Compiled);

        public Task<List<KeTomTat>> DsKeAsync(CancellationToken ct)
            => _db.QaKes.Where(k => k.NgayXoa == null)
                .OrderBy(k => k.MaKe)
                .Select(k => new KeTomTat(k.IdKe, k.MaKe, k.TenKe, k.KhuVuc, k.SoTang, k.SoO, k.GhiChu,
                    _db.QaThungs.Count(t => t.IdKe == k.IdKe && t.NgayXoa == null)))
                .ToListAsync(ct);

        public async Task<(QaKe? ke, List<ThungTrenKe> thung)> SoDoAsync(int idKe, CancellationToken ct)
        {
            var ke = await _db.QaKes.AsNoTracking().FirstOrDefaultAsync(k => k.IdKe == idKe && k.NgayXoa == null, ct);
            if (ke == null) return (null, new());
            var thung = await _db.QaThungs
                .Where(t => t.IdKe == idKe && t.NgayXoa == null && t.Tang != null && t.O != null)
                .OrderBy(t => t.MaThung)
                .Select(t => new ThungTrenKe(t.IdThung, t.MaThung, t.TrangThai, t.Tang!.Value, t.O!.Value, t.Dongs.Count(d => d.NgayBo == null)))
                .ToListAsync(ct);
            return (ke, thung);
        }

        public async Task<KetQua> LuuKeAsync(LuuKeVao vao, string? nguoi, CancellationToken ct)
        {
            var maKe = (vao.MaKe ?? "").Trim().ToUpperInvariant();
            if (!MaKeHopLe.IsMatch(maKe)) return new(false, "Mã kệ 1–20 ký tự, chỉ gồm chữ không dấu, số, dấu _ hoặc . (không dùng dấu - vì dùng để ngăn tầng/ô).");
            if (vao.SoTang < 1 || vao.SoTang > TranTangO || vao.SoO < 1 || vao.SoO > TranTangO)
                return new(false, $"Số tầng và số ô mỗi tầng phải từ 1 đến {TranTangO}.");
            var tenKe = Gon(vao.TenKe, 100);
            var khuVuc = Gon(vao.KhuVuc, 100);
            var ghiChu = Gon(vao.GhiChu, 500);
            var bayGio = DateTime.Now;

            // Serializable để 2 người tạo cùng mã kệ cùng lúc không lọt qua bước kiểm trùng
            await using var tx = await _db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
            if (await _db.QaKes.AnyAsync(k => k.MaKe == maKe && k.NgayXoa == null && k.IdKe != (vao.IdKe ?? 0), ct))
                return new(false, $"Đã có kệ mã {maKe}.");

            QaKe ke;
            if (vao.IdKe == null)
            {
                ke = new QaKe { MaKe = maKe, NgayTao = bayGio, NguoiTao = nguoi };
                _db.QaKes.Add(ke);
            }
            else
            {
                var tim = await _db.QaKes.FirstOrDefaultAsync(k => k.IdKe == vao.IdKe && k.NgayXoa == null, ct);
                if (tim == null) return new(false, "Không tìm thấy kệ (có thể đã bị xoá).");
                // Thu nhỏ kệ: không được để thùng nằm ở ô không còn tồn tại
                var ngoai = await _db.QaThungs
                    .Where(t => t.IdKe == tim.IdKe && t.NgayXoa == null && (t.Tang > vao.SoTang || t.O > vao.SoO))
                    .Select(t => t.MaThung).Take(10).ToListAsync(ct);
                if (ngoai.Count > 0)
                    return new(false, $"Còn thùng nằm ngoài kích thước mới ({string.Join(", ", ngoai)}) — chuyển các thùng đó trước.");
                ke = tim;
                ke.MaKe = maKe;
                ke.NgaySua = bayGio;
                ke.NguoiSua = nguoi;
            }
            ke.TenKe = tenKe;
            ke.KhuVuc = khuVuc;
            ke.SoTang = vao.SoTang;
            ke.SoO = vao.SoO;
            ke.GhiChu = ghiChu;
            await _db.SaveChangesAsync(ct);
            await tx.CommitAsync(ct);
            return new(true, vao.IdKe == null ? $"Đã thêm kệ {maKe}." : $"Đã lưu kệ {maKe}.", ke.IdKe);
        }

        /// <summary>Tạo các kệ mã TuMa..DenMa (1 chữ cái A–Z) chưa có; kệ đã có thì bỏ qua, không sửa.</summary>
        public async Task<KetQua> TaoDayKeAsync(TaoDayKeVao vao, string? nguoi, CancellationToken ct)
        {
            var tu = (vao.TuMa ?? "").Trim().ToUpperInvariant();
            var den = (vao.DenMa ?? "").Trim().ToUpperInvariant();
            if (tu.Length != 1 || den.Length != 1 || tu[0] < 'A' || tu[0] > 'Z' || den[0] < 'A' || den[0] > 'Z' || tu[0] > den[0])
                return new(false, "Nhập từ kệ và đến kệ là 1 chữ cái A–Z (vd A đến Z).");
            if (vao.SoTang < 1 || vao.SoTang > TranTangO || vao.SoO < 1 || vao.SoO > TranTangO)
                return new(false, $"Số tầng và số ô mỗi tầng phải từ 1 đến {TranTangO}.");
            var khuVuc = Gon(vao.KhuVuc, 100);
            var dsMa = Enumerable.Range(tu[0], den[0] - tu[0] + 1).Select(c => ((char)c).ToString()).ToList();
            var bayGio = DateTime.Now;

            await using var tx = await _db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
            var daCo = await _db.QaKes.Where(k => dsMa.Contains(k.MaKe) && k.NgayXoa == null).Select(k => k.MaKe).ToListAsync(ct);
            var canTao = dsMa.Except(daCo).ToList();
            foreach (var ma in canTao)
                _db.QaKes.Add(new QaKe { MaKe = ma, KhuVuc = khuVuc, SoTang = vao.SoTang, SoO = vao.SoO, NgayTao = bayGio, NguoiTao = nguoi });
            await _db.SaveChangesAsync(ct);
            await tx.CommitAsync(ct);
            if (canTao.Count == 0) return new(true, $"Các kệ {tu}–{den} đều đã có, không tạo thêm.");
            return new(true, $"Đã tạo {canTao.Count} kệ ({string.Join(", ", canTao)})"
                + (daCo.Count > 0 ? $"; bỏ qua {daCo.Count} kệ đã có ({string.Join(", ", daCo.OrderBy(x => x))})." : "."));
        }

        /// <summary>Xoá mềm kệ; chỉ xoá được khi không còn thùng nào trên kệ.</summary>
        public async Task<KetQua> XoaKeAsync(int idKe, string? nguoi, CancellationToken ct)
        {
            var ke = await _db.QaKes.FirstOrDefaultAsync(k => k.IdKe == idKe && k.NgayXoa == null, ct);
            if (ke == null) return new(false, "Không tìm thấy kệ.");
            int soThung = await _db.QaThungs.CountAsync(t => t.IdKe == idKe && t.NgayXoa == null, ct);
            if (soThung > 0) return new(false, $"Kệ {ke.MaKe} còn {soThung} thùng — chuyển hoặc gỡ thùng khỏi kệ trước.");
            ke.NgayXoa = DateTime.Now;
            ke.NguoiXoa = nguoi;
            await _db.SaveChangesAsync(ct);
            return new(true, $"Đã xoá kệ {ke.MaKe}.", ke.IdKe);
        }

        public const int TranThungMoiLan = 500;

        /// <summary>
        /// Gán theo vị trí "MaKe-tầng-ô", hoặc kệ 1 tầng "A-A3" / "A3" / "A-3" (chấp nhận khoảng trắng thay dấu -,
        /// không phân biệt hoa thường). Mã kệ không chứa dấu - nên tách được chắc chắn. viTri trống = gỡ khỏi kệ.
        /// </summary>
        public async Task<KetQua> XepTheoViTriAsync(List<int>? dsIdThung, string? viTri, string? nguoi, CancellationToken ct)
        {
            if (string.IsNullOrWhiteSpace(viTri)) return await XepThungAsync(dsIdThung, null, null, null, nguoi, ct);
            var phan = viTri.Trim().ToUpperInvariant().Split(new[] { '-', ' ' }, StringSplitOptions.RemoveEmptyEntries);
            string maKe;
            int tang, o;
            bool dangGon = false;   // chỉ ghi ô, ngầm hiểu tầng 1 -> kệ phải đúng 1 tầng
            Match m;
            if (phan.Length == 3 && int.TryParse(phan[1], out tang) && int.TryParse(phan[2], out o)) maKe = phan[0];
            else if (phan.Length == 2 && int.TryParse(phan[1], out o)) { maKe = phan[0]; tang = 1; dangGon = true; }
            // "A-A3": kệ cha + ô con, ô con phải thuộc đúng kệ cha
            else if (phan.Length == 2 && (m = ViTriGon.Match(phan[1])).Success && m.Groups[1].Value == phan[0] && int.TryParse(m.Groups[2].Value, out o)) { maKe = phan[0]; tang = 1; dangGon = true; }
            else if (phan.Length == 1 && (m = ViTriGon.Match(phan[0])).Success && int.TryParse(m.Groups[2].Value, out o)) { maKe = m.Groups[1].Value; tang = 1; dangGon = true; }
            else return new(false, $"Vị trí \"{viTri.Trim()}\" không đúng dạng: A-A3 (kệ 1 tầng) hoặc Mã kệ-tầng-ô (vd A-2-3).");
            var ke = await _db.QaKes.Where(k => k.MaKe == maKe && k.NgayXoa == null).Select(k => new { k.IdKe, k.SoTang }).FirstOrDefaultAsync(ct);
            if (ke == null) return new(false, $"Chưa có kệ {maKe} — thêm kệ ở trang Quản lý kệ trước.");
            if (dangGon && ke.SoTang != 1) return new(false, $"Kệ {maKe} có {ke.SoTang} tầng — ghi đủ dạng {maKe}-tầng-ô (vd {maKe}-1-{o}).");
            return await XepThungAsync(dsIdThung, ke.IdKe, tang, o, nguoi, ct);
        }

        /// <summary>
        /// Đặt các thùng vào ô (idKe, tang, o); idKe null = gỡ khỏi kệ. Làm trong 1 transaction, ghi nhật ký từng thùng.
        /// Thùng đã ở đúng ô / chưa xếp kệ (khi gỡ) thì bỏ qua — bấm 2 lần không ghi nhật ký 2 lần.
        /// </summary>
        public async Task<KetQua> XepThungAsync(List<int>? dsIdThung, int? idKe, int? tang, int? o, string? nguoi, CancellationToken ct)
        {
            var ds = (dsIdThung ?? new()).Where(i => i > 0).Distinct().ToList();
            if (ds.Count == 0) return new(false, "Chưa chọn thùng nào.");
            if (ds.Count > TranThungMoiLan) return new(false, $"Mỗi lần chỉ xếp tối đa {TranThungMoiLan} thùng.");

            await using var tx = await _db.Database.BeginTransactionAsync(IsolationLevel.ReadCommitted, ct);
            var dsThung = await _db.QaThungs.Include(t => t.Ke).Where(t => ds.Contains(t.IdThung) && t.NgayXoa == null).ToListAsync(ct);
            if (dsThung.Count != ds.Count)
                return new(false, $"{ds.Count - dsThung.Count} thùng không còn (có thể vừa bị xoá) — tải lại danh sách rồi chọn lại.");
            var bayGio = DateTime.Now;
            string tenThung(List<QaThung> x) => x.Count == 1 ? $"thùng {x[0].MaThung}" : $"{x.Count} thùng";

            if (idKe == null)
            {
                var canGo = dsThung.Where(t => t.IdKe != null).ToList();
                if (canGo.Count == 0) return new(true, dsThung.Count == 1 ? $"Thùng {dsThung[0].MaThung} chưa xếp kệ." : "Các thùng đã chọn đều chưa xếp kệ.");
                foreach (var t in canGo)
                {
                    GhiLichSu(t.IdThung, nguoi, "GoKe", $"Gỡ khỏi vị trí {MaViTri(t.Ke?.MaKe, t.Tang, t.O, t.Ke?.SoTang ?? 0)}", bayGio);
                    t.IdKe = null; t.Tang = null; t.O = null; t.Ke = null;
                }
                await _db.SaveChangesAsync(ct);
                await tx.CommitAsync(ct);
                return new(true, $"Đã gỡ {tenThung(canGo)} khỏi kệ.");
            }

            var ke = await _db.QaKes.FirstOrDefaultAsync(k => k.IdKe == idKe && k.NgayXoa == null, ct);
            if (ke == null) return new(false, "Không tìm thấy kệ (có thể đã bị xoá).");
            if (tang == null || o == null || tang < 1 || tang > ke.SoTang || o < 1 || o > ke.SoO)
                return new(false, ke.SoTang == 1
                    ? $"Kệ {ke.MaKe} có {ke.SoO} ô ({MaViTri(ke.MaKe, 1, 1, 1)}–{MaViTri(ke.MaKe, 1, ke.SoO, 1)})."
                    : $"Kệ {ke.MaKe} có {ke.SoTang} tầng × {ke.SoO} ô — chọn tầng và ô trong khoảng đó.");
            var moi = MaViTri(ke.MaKe, tang, o, ke.SoTang);
            var canXep = dsThung.Where(t => !(t.IdKe == ke.IdKe && t.Tang == tang && t.O == o)).ToList();
            if (canXep.Count == 0) return new(true, dsThung.Count == 1 ? $"Thùng {dsThung[0].MaThung} đã ở {moi}." : $"Các thùng đã chọn đều đã ở {moi}.");
            foreach (var t in canXep)
            {
                var cu = MaViTri(t.Ke?.MaKe, t.Tang, t.O, t.Ke?.SoTang ?? 0);
                GhiLichSu(t.IdThung, nguoi, "XepKe", cu == null ? $"Xếp vào {moi}" : $"Chuyển từ {cu} sang {moi}", bayGio);
                t.IdKe = ke.IdKe; t.Tang = tang; t.O = o; t.Ke = ke;
            }
            await _db.SaveChangesAsync(ct);
            await tx.CommitAsync(ct);
            int boQua = dsThung.Count - canXep.Count;
            return new(true, $"Đã xếp {tenThung(canXep)} vào {moi}." + (boQua > 0 ? $" ({boQua} thùng đã ở sẵn ô này)" : ""), ke.IdKe);
        }

        private void GhiLichSu(int idThung, string? nguoi, string hanhDong, string? noiDung, DateTime ngay)
            => _db.QaThungLichSus.Add(new QaThungLichSu { IdThung = idThung, Nguoi = nguoi, HanhDong = hanhDong, NoiDung = noiDung, Ngay = ngay });

        private static string? Gon(string? s, int max)
        {
            if (string.IsNullOrWhiteSpace(s)) return null;
            s = s.Trim();
            return s.Length > max ? s[..max] : s;
        }
    }
}
