using System.Data;
using System.Text.RegularExpressions;
using E_Form_Best.Context;
using E_Form_Best.Models.ITForm;
using Microsoft.EntityFrameworkCore;

namespace E_Form_Best.Areas.QAForm.Services
{
    /// <summary>
    /// Kệ chứa thùng QA (QA_Ke) + vị trí thùng trên kệ (QA_Thung.id_ke/tang/o).
    /// Kệ chia ô theo tầng × ô, mã vị trí = MaKe-tầng-ô; 1 ô chứa được nhiều thùng nên không chặn trùng ô.
    /// Tầng đánh số từ dưới lên (tầng 1 sát sàn), ô từ trái sang.
    /// </summary>
    public class QaKeService
    {
        public record KetQua(bool ThanhCong, string ThongBao, int? IdKe = null);

        public record KeTomTat(int IdKe, string MaKe, string? TenKe, string? KhuVuc, int SoTang, int SoO, string? GhiChu, int SoThung);

        public record ThungTrenKe(int IdThung, string MaThung, string TrangThai, int Tang, int O, int SoDong);

        public record LuuKeVao(int? IdKe, string? MaKe, string? TenKe, string? KhuVuc, int SoTang, int SoO, string? GhiChu);

        public const int TranTangO = 50;

        // Mã kệ in lên tem/vị trí nên chỉ cho chữ, số, gạch dưới, chấm
        private static readonly Regex MaKeHopLe = new("^[A-Z0-9_.]{1,20}$", RegexOptions.Compiled);

        private readonly ITFormContext _db;

        public QaKeService(ITFormContext db) => _db = db;

        public static string? MaViTri(string? maKe, int? tang, int? o)
            => maKe == null || tang == null || o == null ? null : $"{maKe}-{tang}-{o}";

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
        /// Gán theo vị trí gõ tay "MaKe-tầng-ô" (chấp nhận khoảng trắng thay dấu -, không phân biệt hoa thường).
        /// Mã kệ không chứa dấu - nên tách được chắc chắn 3 phần. viTri trống = gỡ khỏi kệ.
        /// </summary>
        public async Task<KetQua> XepTheoViTriAsync(List<int>? dsIdThung, string? viTri, string? nguoi, CancellationToken ct)
        {
            if (string.IsNullOrWhiteSpace(viTri)) return await XepThungAsync(dsIdThung, null, null, null, nguoi, ct);
            var phan = viTri.Trim().ToUpperInvariant().Split(new[] { '-', ' ' }, StringSplitOptions.RemoveEmptyEntries);
            if (phan.Length != 3 || !int.TryParse(phan[1], out var tang) || !int.TryParse(phan[2], out var o))
                return new(false, $"Vị trí \"{viTri.Trim()}\" không đúng dạng Mã kệ-tầng-ô (vd A-2-3).");
            var maKe = phan[0];
            var idKe = await _db.QaKes.Where(k => k.MaKe == maKe && k.NgayXoa == null).Select(k => (int?)k.IdKe).FirstOrDefaultAsync(ct);
            if (idKe == null) return new(false, $"Chưa có kệ {maKe} — bấm nút Kệ để thêm kệ trước.");
            return await XepThungAsync(dsIdThung, idKe, tang, o, nguoi, ct);
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
                    GhiLichSu(t.IdThung, nguoi, "GoKe", $"Gỡ khỏi vị trí {MaViTri(t.Ke?.MaKe, t.Tang, t.O)}", bayGio);
                    t.IdKe = null; t.Tang = null; t.O = null; t.Ke = null;
                }
                await _db.SaveChangesAsync(ct);
                await tx.CommitAsync(ct);
                return new(true, $"Đã gỡ {tenThung(canGo)} khỏi kệ.");
            }

            var ke = await _db.QaKes.FirstOrDefaultAsync(k => k.IdKe == idKe && k.NgayXoa == null, ct);
            if (ke == null) return new(false, "Không tìm thấy kệ (có thể đã bị xoá).");
            if (tang == null || o == null || tang < 1 || tang > ke.SoTang || o < 1 || o > ke.SoO)
                return new(false, $"Kệ {ke.MaKe} có {ke.SoTang} tầng × {ke.SoO} ô — chọn tầng và ô trong khoảng đó.");
            var moi = MaViTri(ke.MaKe, tang, o);
            var canXep = dsThung.Where(t => !(t.IdKe == ke.IdKe && t.Tang == tang && t.O == o)).ToList();
            if (canXep.Count == 0) return new(true, dsThung.Count == 1 ? $"Thùng {dsThung[0].MaThung} đã ở {moi}." : $"Các thùng đã chọn đều đã ở {moi}.");
            foreach (var t in canXep)
            {
                var cu = MaViTri(t.Ke?.MaKe, t.Tang, t.O);
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
