using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using ClosedXML.Excel;
using E_Form_Best.Context;
using E_Form_Best.Models.ITForm;
using Microsoft.EntityFrameworkCore;

namespace E_Form_Best.Areas.QAForm.Services
{
    /// <summary>
    /// Bảng khiếu nại khách hàng (QA_KhieuNai) quản lý trên web như file Excel CUSTOMER COMPLAINT SUMMARY:
    /// sửa từng ô (lưu ngay, ghi QA_KhieuNaiLichSu), nhập mã lot thì tự điền các cột lấy từ NH-MES/NH-WMS,
    /// nhập/xuất Excel đúng bố cục file gốc. Xoá = xoá mềm.
    /// </summary>
    public class QaKhieuNaiService
    {
        public enum KieuO { Chuoi, So, Ngay, TrangThai }

        // 1 cột của bảng: khoá gửi/nhận với trình duyệt, cột Excel, tiêu đề (dòng 2 của file), kiểu, độ dài tối đa,
        // TuDong = lấy từ MES/WMS khi nhập mã lot, Vang = tiêu đề tô vàng trong file gốc
        public record Cot(string Khoa, string CotExcel, string TieuDe, KieuO Kieu, int DoDai, bool TuDong, bool Vang,
            Func<QaKhieuNai, object?> Lay, Action<QaKhieuNai, object?> Gan);

        public static readonly IReadOnlyList<Cot> DsCot = new List<Cot>
        {
            new("ngayKhieuNai", "A", "投诉日期\nNgày khiếu nại", KieuO.Ngay, 0, false, true, x => x.NgayKhieuNai, (x, v) => x.NgayKhieuNai = (DateTime?)v),
            new("nhaMay", "B", "制衣厂\nNhà máy", KieuO.Chuoi, 100, true, true, x => x.NhaMay, (x, v) => x.NhaMay = (string?)v),
            new("khachCuoi", "C", "终客\nKhách hàng cuối", KieuO.Chuoi, 100, true, true, x => x.KhachCuoi, (x, v) => x.KhachCuoi = (string?)v),
            new("quocGia", "D", "国家\nQuốc gia", KieuO.Chuoi, 50, false, true, x => x.QuocGia, (x, v) => x.QuocGia = (string?)v),
            new("po", "E", "PO", KieuO.Chuoi, 100, true, true, x => x.Po, (x, v) => x.Po = (string?)v),
            new("maLot", "F", "制单号\nmã lot", KieuO.Chuoi, 50, false, true, x => x.MaLot, (x, v) => x.MaLot = (string?)v),
            new("maHang", "G", "货号\nmã hàng", KieuO.Chuoi, 100, true, false, x => x.MaHang, (x, v) => x.MaHang = (string?)v),
            new("khoRong", "H", "宽度\nkhổ rộng", KieuO.Chuoi, 100, true, true, x => x.KhoRong, (x, v) => x.KhoRong = (string?)v),
            new("mauKhach", "I", "颜色\n(mã màu KH)", KieuO.Chuoi, 200, true, true, x => x.MauKhach, (x, v) => x.MauKhach = (string?)v),
            new("maMauNoiBo", "J", "内部色号\nMã màu nội bộ", KieuO.Chuoi, 100, true, true, x => x.MaMauNoiBo, (x, v) => x.MaMauNoiBo = (string?)v),
            new("slXuat", "K", "出货数量 (Y)\nTổng số lượng xuất(Y)", KieuO.So, 0, true, true, x => x.SlXuat, (x, v) => x.SlXuat = (decimal?)v),
            new("slKhieuNai", "L", "投诉数量 (Y)\nSố lượng khiếu nại", KieuO.So, 0, false, true, x => x.SlKhieuNai, (x, v) => x.SlKhieuNai = (decimal?)v),
            new("chungLoai", "M", "类别\nchủng loại", KieuO.Chuoi, 100, false, true, x => x.ChungLoai, (x, v) => x.ChungLoai = (string?)v),
            new("vanDe", "N", "问题描叙\nVấn đề", KieuO.Chuoi, 2000, false, true, x => x.VanDe, (x, v) => x.VanDe = (string?)v),
            new("qaBocKiem", "O", "内部抽查记录\nNội bộ QA bốc kiểm", KieuO.Chuoi, 2000, false, false, x => x.QaBocKiem, (x, v) => x.QaBocKiem = (string?)v),
            new("yKienBp", "P", "BP意见\nÝ kiến của BP", KieuO.Chuoi, 2000, false, true, x => x.YKienBp, (x, v) => x.YKienBp = (string?)v),
            new("yKienKhach", "Q", "客户要求\nÝ kiến của khách hàng", KieuO.Chuoi, 2000, false, true, x => x.YKienKhach, (x, v) => x.YKienKhach = (string?)v),
            new("slBu", "R", "补货数量 (Y)\nSố lượng yêu cầu", KieuO.So, 0, false, false, x => x.SlBu, (x, v) => x.SlBu = (decimal?)v),
            new("tiLeBu", "S", "补货率 (%)\nTỉ lệ", KieuO.So, 0, false, false, x => x.TiLeBu, (x, v) => x.TiLeBu = (decimal?)v),
            new("slTonKho", "T", "库存 (Y)\nSố lượng tồn kho", KieuO.So, 0, false, false, x => x.SlTonKho, (x, v) => x.SlTonKho = (decimal?)v),
            new("foc", "U", "FOC (Y)", KieuO.So, 0, false, false, x => x.Foc, (x, v) => x.Foc = (decimal?)v),
            new("ngayXuatTp", "V", "成衣货期\nngày xuất thành phẩm (Ext)", KieuO.Ngay, 0, false, false, x => x.NgayXuatTp, (x, v) => x.NgayXuatTp = (DateTime?)v),
            new("ngayXacNhan", "W", "确认日期\nNgày xác nhận", KieuO.Ngay, 0, false, false, x => x.NgayXacNhan, (x, v) => x.NgayXacNhan = (DateTime?)v),
            new("carNo", "X", "CAR No.", KieuO.Chuoi, 100, false, false, x => x.CarNo, (x, v) => x.CarNo = (string?)v),
            new("tonThatCuoi", "Y", "最后损耗\nTổn thất cuối cùng", KieuO.So, 0, false, true, x => x.TonThatCuoi, (x, v) => x.TonThatCuoi = (decimal?)v),
            new("ghiChuTraDoi", "Z", "备注\n(Return/replace)", KieuO.Chuoi, 1000, false, true, x => x.GhiChuTraDoi, (x, v) => x.GhiChuTraDoi = (string?)v),
            new("maNvQa", "AA", "Mã nhân viên QC", KieuO.Chuoi, 50, false, false, x => x.MaNvQa, (x, v) => x.MaNvQa = (string?)v),
            new("qaPhuTrach", "AB", "QA phụ trách", KieuO.Chuoi, 200, false, false, x => x.QaPhuTrach, (x, v) => x.QaPhuTrach = (string?)v),
            new("trangThai", "AC", "status", KieuO.TrangThai, 20, false, false, x => x.TrangThai, (x, v) => x.TrangThai = (string)v!),
            new("ghiChu", "AD", "Ghi chú", KieuO.Chuoi, 2000, false, false, x => x.GhiChu, (x, v) => x.GhiChu = (string?)v),
        };

        private static readonly Dictionary<string, Cot> CotTheoKhoa = DsCot.ToDictionary(c => c.Khoa, StringComparer.OrdinalIgnoreCase);

        public record KetQua(bool ThanhCong, string ThongBao, Dictionary<string, object?>? Dong = null);
        public record LocDs(DateTime? TuNgay, DateTime? DenNgay, string? TrangThai, string? TuKhoa);

        private const int TranDong = 5000;

        private readonly ITFormContext _db;
        private readonly CheckXuatHangService _xuatHang;
        private readonly ILogger<QaKhieuNaiService> _logger;

        public QaKhieuNaiService(ITFormContext db, CheckXuatHangService xuatHang, ILogger<QaKhieuNaiService> logger)
        {
            _db = db;
            _xuatHang = xuatHang;
            _logger = logger;
        }

        // ---------- Đọc ----------

        public static Dictionary<string, object?> RaDong(QaKhieuNai x)
        {
            var d = new Dictionary<string, object?> { ["id"] = x.IdKhieuNai };
            foreach (var c in DsCot)
            {
                var v = c.Lay(x);
                d[c.Khoa] = v is DateTime t ? t.ToString("yyyy-MM-dd") : v;
            }
            d["dienTuDongLuc"] = x.DienTuDongLuc?.ToString("yyyy-MM-dd HH:mm");
            d["nguoiSua"] = x.NguoiSua ?? x.NguoiTao;
            d["ngaySua"] = (x.NgaySua ?? x.NgayTao).ToString("yyyy-MM-dd HH:mm");
            return d;
        }

        private IQueryable<QaKhieuNai> Loc(LocDs loc)
        {
            var q = _db.QaKhieuNais.AsNoTracking().Where(x => x.NgayXoa == null);
            if (loc.TuNgay != null) q = q.Where(x => x.NgayKhieuNai >= loc.TuNgay);
            if (loc.DenNgay != null) q = q.Where(x => x.NgayKhieuNai <= loc.DenNgay);
            if (loc.TrangThai is "pending" or "close") q = q.Where(x => x.TrangThai == loc.TrangThai);
            var k = (loc.TuKhoa ?? "").Trim();
            if (k.Length > 0)
                q = q.Where(x => x.MaLot!.Contains(k) || x.Po!.Contains(k) || x.NhaMay!.Contains(k) || x.KhachCuoi!.Contains(k)
                    || x.MaHang!.Contains(k) || x.KhoRong!.Contains(k) || x.MauKhach!.Contains(k) || x.MaMauNoiBo!.Contains(k)
                    || x.ChungLoai!.Contains(k) || x.VanDe!.Contains(k) || x.GhiChu!.Contains(k) || x.CarNo!.Contains(k));
            // Giữ thứ tự như file: theo ngày khiếu nại rồi thứ tự nhập; dòng chưa có ngày (vừa thêm) xuống cuối
            return q.OrderBy(x => x.NgayKhieuNai == null).ThenBy(x => x.NgayKhieuNai).ThenBy(x => x.IdKhieuNai);
        }

        public async Task<(List<Dictionary<string, object?>> ds, int tong)> DanhSachAsync(LocDs loc, CancellationToken ct)
        {
            var q = Loc(loc);
            var tong = await q.CountAsync(ct);
            var ds = await q.Take(TranDong).ToListAsync(ct);
            return (ds.Select(RaDong).ToList(), tong);
        }

        // Gợi ý cho cột chủng loại (danh sách sheet "GHI CHÚ" của file + giá trị đã dùng)
        public async Task<List<string>> GoiYChungLoaiAsync(CancellationToken ct)
            => await _db.QaKhieuNais.AsNoTracking().Where(x => x.NgayXoa == null && x.ChungLoai != null)
                .GroupBy(x => x.ChungLoai!).OrderByDescending(g => g.Count()).Select(g => g.Key).Take(100).ToListAsync(ct);

        // ---------- Ghi ----------

        /// <summary>Thêm dòng trống (khoaTao = GUID trình duyệt, bấm 2 lần không ra 2 dòng).</summary>
        public async Task<KetQua> ThemAsync(string? khoaTao, string? nguoi, CancellationToken ct)
        {
            if (string.IsNullOrWhiteSpace(khoaTao) || khoaTao.Length > 60) return new(false, "Thiếu khoá tạo dòng.");
            var khoa = "web:" + khoaTao.Trim();
            var cu = await _db.QaKhieuNais.AsNoTracking().FirstOrDefaultAsync(x => x.KhoaTao == khoa, ct);
            if (cu != null) return new(true, "", RaDong(cu));

            var bayGio = DateTime.Now;
            var x = new QaKhieuNai
            {
                NgayKhieuNai = bayGio.Date, TrangThai = "pending", KhoaTao = khoa, MaNvQa = Cat(nguoi, 50),
                NgayTao = bayGio, NguoiTao = nguoi
            };
            _db.QaKhieuNais.Add(x);
            try
            {
                await _db.SaveChangesAsync(ct);
            }
            catch (DbUpdateException)
            {
                // 2 request cùng khoá chạy song song: unique index chặn bản thứ hai -> trả dòng đã có
                _db.ChangeTracker.Clear();
                var daCo = await _db.QaKhieuNais.AsNoTracking().FirstOrDefaultAsync(y => y.KhoaTao == khoa, ct);
                if (daCo == null) throw;
                return new(true, "", RaDong(daCo));
            }
            GhiLichSu(x.IdKhieuNai, nguoi, "Tao", null, null, null);
            await _db.SaveChangesAsync(ct);
            return new(true, "", RaDong(x));
        }

        /// <summary>Sửa 1 ô. Sửa mã lot thì tự điền các cột hệ thống từ NH-MES/NH-WMS.</summary>
        public async Task<KetQua> SuaAsync(int id, string? truong, string? giaTri, string? nguoi, CancellationToken ct)
        {
            if (truong == null || !CotTheoKhoa.TryGetValue(truong, out var cot)) return new(false, "Cột không hợp lệ.");
            var (ok, loi, moi) = DocGiaTri(cot, giaTri);
            if (!ok) return new(false, loi);

            var x = await _db.QaKhieuNais.FirstOrDefaultAsync(y => y.IdKhieuNai == id && y.NgayXoa == null, ct);
            if (x == null) return new(false, "Không tìm thấy dòng (có thể đã bị xoá).");

            var cu = cot.Lay(x);
            string? thongBao = null;
            if (!Bang(cu, moi))
            {
                cot.Gan(x, moi);
                GhiLichSu(id, nguoi, "Sua", cot.Khoa, cu, moi);
                x.NgaySua = DateTime.Now;
                x.NguoiSua = nguoi;
                if (cot.Khoa == "maLot" && moi is string lot)
                    thongBao = await DienTuDongAsync(x, lot, nguoi, ct);
                await _db.SaveChangesAsync(ct);
            }
            return new(true, thongBao ?? "", RaDong(x));
        }

        /// <summary>Lấy lại các cột hệ thống theo mã lot hiện tại của dòng.</summary>
        public async Task<KetQua> DienLaiAsync(int id, string? nguoi, CancellationToken ct)
        {
            var x = await _db.QaKhieuNais.FirstOrDefaultAsync(y => y.IdKhieuNai == id && y.NgayXoa == null, ct);
            if (x == null) return new(false, "Không tìm thấy dòng (có thể đã bị xoá).");
            if (string.IsNullOrWhiteSpace(x.MaLot)) return new(false, "Dòng chưa có mã lot.");
            var loi = await DienTuDongAsync(x, x.MaLot, nguoi, ct);
            await _db.SaveChangesAsync(ct);
            return new(loi == null, loi ?? "", RaDong(x));
        }

        /// <summary>
        /// Gọi Check xuất hàng theo lot rồi ghi đè các cột tự động có giá trị (hệ thống trả trống thì giữ giá trị đang có).
        /// Trả null nếu điền được, ngược lại là thông báo (không chặn việc lưu mã lot).
        /// </summary>
        private async Task<string?> DienTuDongAsync(QaKhieuNai x, string lot, string? nguoi, CancellationToken ct)
        {
            var kq = await _xuatHang.TraCuuAsync(lot, CheckXuatHangService.LoaiMa.MaLot, ct);
            if (!kq.ThanhCong) return "Đã lưu mã lot, chưa tự điền được: " + kq.ThongBao;
            var d = kq.DsLot?.FirstOrDefault(l => string.Equals(l.MaLot, lot, StringComparison.OrdinalIgnoreCase));
            if (d == null || (!d.CoDonHang && d.SoCuon == 0)) return $"Không thấy lot {lot} trên NH-MES/NH-WMS, các cột hệ thống giữ nguyên.";

            var moi = new Dictionary<string, object?>
            {
                ["nhaMay"] = d.NhaMay, ["khachCuoi"] = d.KhachCuoi, ["po"] = d.Po, ["maHang"] = d.MaHangGoc,
                ["khoRong"] = d.MaHang, ["mauKhach"] = d.MauKhach, ["maMauNoiBo"] = d.MaMauNoiBo,
                ["slXuat"] = d.SoCuon > 0 ? d.SlXuat : null
            };
            foreach (var (khoa, v) in moi)
            {
                var cot = CotTheoKhoa[khoa];
                var gt = v is string s ? Cat(s.Trim(), cot.DoDai) : v;
                if (gt == null) continue;
                var cu = cot.Lay(x);
                if (Bang(cu, gt)) continue;
                cot.Gan(x, gt);
                GhiLichSu(x.IdKhieuNai, nguoi, "DienTuDong", khoa, cu, gt);
            }
            x.DienTuDongLuc = DateTime.Now;
            x.NgaySua = DateTime.Now;
            x.NguoiSua = nguoi;
            var canhBao = new List<string>();
            if (!d.CoDonHang) canhBao.Add("không thấy đơn trên NH-MES");
            if (d.SoCuon == 0) canhBao.Add("lot chưa xuất kho trên NH-WMS");
            return canhBao.Count > 0 ? "Đã điền một phần (" + string.Join(", ", canhBao) + ")." : null;
        }

        public async Task<KetQua> XoaAsync(int id, string? nguoi, CancellationToken ct)
        {
            var x = await _db.QaKhieuNais.FirstOrDefaultAsync(y => y.IdKhieuNai == id && y.NgayXoa == null, ct);
            if (x == null) return new(false, "Không tìm thấy dòng (có thể đã bị xoá).");
            x.NgayXoa = DateTime.Now;
            x.NguoiXoa = nguoi;
            GhiLichSu(id, nguoi, "Xoa", null, $"{x.NgayKhieuNai:dd/MM/yyyy} {x.MaLot} {x.VanDe}".Trim(), null);
            await _db.SaveChangesAsync(ct);
            return new(true, "Đã xoá dòng.");
        }

        // ---------- Nhập Excel ----------

        /// <summary>
        /// Nhập sheet chi tiết của file CUSTOMER COMPLAINT SUMMARY (tiêu đề ở dòng có ô "制单号", dữ liệu từ dòng sau).
        /// Khoá chống trùng = SHA-256 các cột nhận diện (ngày, nhà máy, khách cuối, PO, lot, SL khiếu nại, chủng loại, vấn đề)
        /// + số thứ tự nếu file có nhiều dòng giống hệt.
        /// Dòng đã có (nhập lại / file tháng sau chép lại dòng cũ): chỉ điền các ô đang TRỐNG trong DB, không ghi đè
        /// chỗ đã sửa trên web, không đổi trạng thái.
        /// Cột A..Z theo vị trí (giống mọi file); các cột sau Z khác nhau giữa các tháng nên nhận theo tên tiêu đề:
        /// "Mã nhân viên QC/QA" -> ma_nv_qa, "QA phụ trách", "status", "Ghi chú" (file xuất từ web).
        /// </summary>
        public async Task<KetQua> NhapExcelAsync(Stream tep, string? nguoi, CancellationToken ct)
        {
            using var wb = new XLWorkbook(tep);
            var ws = wb.Worksheets.FirstOrDefault(s => s.Name.Trim().Equals("detail", StringComparison.OrdinalIgnoreCase)) ?? wb.Worksheet(1);
            var dongTieuDe = ws.RowsUsed().Take(10).FirstOrDefault(r => (r.Cell(6).GetString() ?? "").Contains("制单号"));
            if (dongTieuDe == null) return new(false, $"Sheet \"{ws.Name}\" không có dòng tiêu đề (cột F phải là 制单号).");

            // Số cột Excel -> cột bảng
            var banDo = DsCot.Where(c => c.CotExcel.Length == 1).ToDictionary(c => XLHelper.GetColumnNumberFromLetter(c.CotExcel), c => c);
            int cotCuoi = dongTieuDe.LastCellUsed()?.Address.ColumnNumber ?? 26;
            for (int c = 27; c <= cotCuoi; c++)
            {
                var td = dongTieuDe.Cell(c).GetString().Trim().ToLowerInvariant();
                string? khoa = td.Contains("status") ? "trangThai"
                    : td.Contains("phụ trách") ? "qaPhuTrach"
                    : td.Contains("mã nhân viên") ? "maNvQa"
                    : td.StartsWith("ghi chú") ? "ghiChu" : null;
                if (khoa != null && !banDo.ContainsValue(CotTheoKhoa[khoa])) banDo[c] = CotTheoKhoa[khoa];
            }

            var dsMoi = new List<QaKhieuNai>();
            var demKhoa = new Dictionary<string, int>();
            var bayGio = DateTime.Now;
            int dongCuoi = ws.LastRowUsed()?.RowNumber() ?? 0, loiDong = 0;
            for (int r = dongTieuDe.RowNumber() + 1; r <= dongCuoi; r++)
            {
                var row = ws.Row(r);
                if (Enumerable.Range(1, 14).All(c => row.Cell(c).IsEmpty())) continue;
                var x = new QaKhieuNai { TrangThai = "pending", NgayTao = bayGio, NguoiTao = nguoi };
                foreach (var (soCot, cot) in banDo)
                {
                    var o = row.Cell(soCot);
                    if (o.IsEmpty()) continue;
                    var (ok, _, v) = DocO(cot, o);
                    if (ok && v != null) cot.Gan(x, v);
                    else if (!ok) loiDong++;
                }
                var nhanDien = string.Join("|", x.NgayKhieuNai?.ToString("yyyy-MM-dd"), x.NhaMay, x.KhachCuoi, x.Po, x.MaLot,
                    x.SlKhieuNai?.ToString(CultureInfo.InvariantCulture), x.ChungLoai, x.VanDe);
                var bam = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(nhanDien)))[..40].ToLowerInvariant();
                demKhoa[bam] = demKhoa.GetValueOrDefault(bam) + 1;
                x.KhoaTao = $"xlsx:{bam}:{demKhoa[bam]}";
                dsMoi.Add(x);
            }
            if (dsMoi.Count == 0) return new(false, "File không có dòng dữ liệu nào.");

            // Dòng đã có (kể cả đã xoá mềm — xoá rồi thì không nhập lại)
            var daCo = new Dictionary<string, QaKhieuNai>();
            foreach (var phan in dsMoi.Select(x => x.KhoaTao!).Chunk(500))
                foreach (var x in await _db.QaKhieuNais.Where(x => phan.Contains(x.KhoaTao!)).ToListAsync(ct))
                    daCo[x.KhoaTao!] = x;
            var canThem = dsMoi.Where(x => !daCo.ContainsKey(x.KhoaTao!)).ToList();

            // Điền ô trống của dòng đã có (trạng thái luôn có giá trị nên không bao giờ bị đổi ở đây)
            int soDongDien = 0, soODien = 0;
            foreach (var moi in dsMoi)
            {
                if (!daCo.TryGetValue(moi.KhoaTao!, out var cu) || cu.NgayXoa != null) continue;
                bool coDien = false;
                foreach (var cot in DsCot)
                {
                    var v = cot.Lay(moi);
                    if (v == null || cot.Lay(cu) != null) continue;
                    cot.Gan(cu, v);
                    GhiLichSu(cu.IdKhieuNai, nguoi, "DongBoExcel", cot.Khoa, null, v);
                    coDien = true;
                    soODien++;
                }
                if (coDien) { soDongDien++; cu.NgaySua = bayGio; cu.NguoiSua = nguoi; }
            }

            await using (var tx = await _db.Database.BeginTransactionAsync(ct))
            {
                _db.QaKhieuNais.AddRange(canThem);
                await _db.SaveChangesAsync(ct);
                foreach (var x in canThem) GhiLichSu(x.IdKhieuNai, nguoi, "NhapExcel", null, null, x.KhoaTao);
                await _db.SaveChangesAsync(ct);
                await tx.CommitAsync(ct);
            }
            var tb = $"{canThem.Count} dòng mới, {daCo.Count} dòng đã có" + (soDongDien > 0 ? $" (điền thêm {soODien} ô trống ở {soDongDien} dòng)" : "") + ".";
            if (loiDong > 0) tb += $" {loiDong} ô sai kiểu dữ liệu để trống.";
            _logger.LogInformation("Nhập Excel khiếu nại QA: {Them} mới, {Co} đã có, điền {O} ô, người {Nguoi}", canThem.Count, daCo.Count, soODien, nguoi);
            return new(true, tb);
        }

        // ---------- Đồng bộ thư mục QA trên file server ----------

        // Thư mục QA lưu các file khiếu nại theo tháng; đổi năm / đổi chỗ thì đặt QaKhieuNai__ThuMucDongBo trong .env
        public const string ThuMucMacDinh = @"\\10.0.60.30\BPVN-Fileserver\Public\QA-Quality Assurance Dept\ELASTIC - 织带\REPORT - 报告\2026\Tousu khách hàng 2026 biểu theo dõi issue log";

        // File đã đọc (đường dẫn -> lúc sửa + kích thước) trong tiến trình này: lượt sau chỉ đọc lại file đã đổi
        private static readonly System.Collections.Concurrent.ConcurrentDictionary<string, (DateTime, long)> FileDaDoc = new(StringComparer.OrdinalIgnoreCase);
        private static readonly SemaphoreSlim KhoaDongBo = new(1, 1);

        public static string ThuMucDongBo(IConfiguration cfg)
        {
            var s = cfg["QaKhieuNai:ThuMucDongBo"];
            return string.IsNullOrWhiteSpace(s) ? ThuMucMacDinh : s.Trim();
        }

        /// <summary>
        /// Đọc mọi file "CUSTOMER COMPLAINT SUMMARY*.xlsx" trong thư mục và nhập như nút Nhập Excel
        /// (dòng mới thêm vào, dòng đã có chỉ điền ô trống). tatCa=false: bỏ qua file chưa đổi từ lần đọc trước.
        /// File đang mở trong Excel vẫn đọc được (FileShare.ReadWrite); file lỗi ghi vào kết quả, không chặn file khác.
        /// </summary>
        public async Task<KetQua> DongBoThuMucAsync(string thuMuc, bool tatCa, string? nguoi, CancellationToken ct)
        {
            if (!Directory.Exists(thuMuc)) return new(false, $"Không truy cập được thư mục {thuMuc}.");
            if (!await KhoaDongBo.WaitAsync(0, ct)) return new(false, "Đang có lượt đồng bộ khác chạy, thử lại sau.");
            try
            {
                var dsFile = Directory.GetFiles(thuMuc, "CUSTOMER COMPLAINT SUMMARY*.xlsx")
                    .Where(f => !Path.GetFileName(f).StartsWith("~$")).OrderBy(f => f, StringComparer.OrdinalIgnoreCase).ToList();
                if (dsFile.Count == 0) return new(false, "Thư mục không có file CUSTOMER COMPLAINT SUMMARY*.xlsx nào.");

                var ketQua = new List<string>();
                int soDoc = 0;
                bool coLoi = false;
                foreach (var f in dsFile)
                {
                    var tt = new FileInfo(f);
                    var dau = (tt.LastWriteTime, tt.Length);
                    if (!tatCa && FileDaDoc.TryGetValue(f, out var cu) && cu == dau) continue;
                    soDoc++;
                    try
                    {
                        using var ms = new MemoryStream();
                        await using (var fs = new FileStream(f, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete))
                            await fs.CopyToAsync(ms, ct);
                        ms.Position = 0;
                        _db.ChangeTracker.Clear();
                        var kq = await NhapExcelAsync(ms, nguoi, ct);
                        ketQua.Add($"{tt.Name}: {kq.ThongBao}");
                        if (kq.ThanhCong) FileDaDoc[f] = dau; else coLoi = true;
                    }
                    catch (Exception ex) when (ex is not OperationCanceledException)
                    {
                        coLoi = true;
                        _db.ChangeTracker.Clear();
                        _logger.LogError(ex, "Lỗi đồng bộ file khiếu nại {File}", f);
                        ketQua.Add($"{tt.Name}: lỗi {ex.Message}");
                    }
                }
                if (soDoc == 0) return new(true, $"{dsFile.Count} file đều chưa đổi từ lần đồng bộ trước.");
                return new(!coLoi, string.Join("\n", ketQua));
            }
            finally
            {
                KhoaDongBo.Release();
            }
        }

        // ---------- Xuất Excel ----------

        /// <summary>Xuất đúng bố cục file gốc: dòng 1 tiêu đề, dòng 2 tên cột (tô vàng như file), dữ liệu từ dòng 3.</summary>
        public async Task<byte[]> XuatExcelAsync(LocDs loc, CancellationToken ct)
        {
            var ds = await Loc(loc).Take(TranDong).ToListAsync(ct);
            using var wb = new XLWorkbook();
            var ws = wb.Worksheets.Add("detail");
            ws.Cell(1, 1).Value = "客户投诉(织带 )Biểu báo cáo Khiếu nại khách hàng đai";
            ws.Range(1, 1, 1, DsCot.Count).Merge().Style.Font.SetBold().Font.SetFontSize(14);
            for (int i = 0; i < DsCot.Count; i++)
            {
                var o = ws.Cell(2, i + 1);
                o.Value = DsCot[i].TieuDe;
                o.Style.Font.SetBold().Alignment.SetWrapText().Alignment.SetHorizontal(XLAlignmentHorizontalValues.Center)
                    .Alignment.SetVertical(XLAlignmentVerticalValues.Center)
                    .Fill.SetBackgroundColor(DsCot[i].Vang ? XLColor.Yellow : XLColor.FromHtml("#DDEBF7"))
                    .Border.SetOutsideBorder(XLBorderStyleValues.Thin);
                ws.Column(i + 1).Width = DsCot[i].Kieu == KieuO.Chuoi && DsCot[i].DoDai >= 1000 ? 30 : 14;
            }
            ws.Row(2).Height = 42;
            for (int r = 0; r < ds.Count; r++)
                for (int i = 0; i < DsCot.Count; i++)
                {
                    var v = DsCot[i].Lay(ds[r]);
                    var o = ws.Cell(r + 3, i + 1);
                    switch (v)
                    {
                        case null: break;
                        case DateTime t: o.Value = t; o.Style.DateFormat.Format = "yyyy-mm-dd"; break;
                        case decimal so: o.Value = so; break;
                        default: o.Value = v.ToString(); break;
                    }
                    o.Style.Border.SetOutsideBorder(XLBorderStyleValues.Thin);
                }
            ws.SheetView.FreezeRows(2);
            ws.Range(2, 1, Math.Max(2, ds.Count + 2), DsCot.Count).SetAutoFilter();
            using var ms = new MemoryStream();
            wb.SaveAs(ms);
            return ms.ToArray();
        }

        // ---------- Tiện ích ----------

        private void GhiLichSu(int id, string? nguoi, string hanhDong, string? truong, object? cu, object? moi)
            => _db.QaKhieuNaiLichSus.Add(new QaKhieuNaiLichSu
            {
                IdKhieuNai = id, Ngay = DateTime.Now, Nguoi = Cat(nguoi, 100), HanhDong = hanhDong, Truong = truong,
                GiaTriCu = Cat(ChuoiGiaTri(cu), 2000), GiaTriMoi = Cat(ChuoiGiaTri(moi), 2000)
            });

        private static string? ChuoiGiaTri(object? v) => v switch
        {
            null => null,
            DateTime t => t.ToString("yyyy-MM-dd"),
            decimal d => d.ToString(CultureInfo.InvariantCulture),
            _ => v.ToString()
        };

        private static bool Bang(object? a, object? b) => a switch
        {
            null => b == null,
            decimal d => b is decimal e && d == e,
            _ => a.Equals(b)
        };

        private static string? Cat(string? s, int n) => s == null ? null : s.Length > n ? s[..n] : s;

        /// <summary>Đọc chuỗi trình duyệt gửi theo kiểu cột. Chuỗi rỗng = xoá ô (trừ trạng thái).</summary>
        public static (bool ok, string loi, object? v) DocGiaTri(Cot cot, string? giaTri)
        {
            var s = (giaTri ?? "").Trim();
            if (cot.Kieu == KieuO.TrangThai)
            {
                s = s.ToLowerInvariant();
                return s is "pending" or "close" ? (true, "", s) : (false, "Trạng thái chỉ được pending hoặc close.", null);
            }
            if (s.Length == 0) return (true, "", null);
            switch (cot.Kieu)
            {
                case KieuO.So:
                    var so = DocSo(s);
                    return so == null ? (false, $"\"{s}\" không phải số.", null) : (true, "", so);
                case KieuO.Ngay:
                    var ngay = DocNgay(s);
                    return ngay == null ? (false, $"\"{s}\" không phải ngày (dd/MM/yyyy).", null) : (true, "", ngay);
                default:
                    if (cot.Khoa == "maLot") s = s.ToUpperInvariant();
                    return s.Length > cot.DoDai ? (false, $"Tối đa {cot.DoDai} ký tự.", null) : (true, "", s);
            }
        }

        private static (bool ok, string loi, object? v) DocO(Cot cot, IXLCell o)
        {
            XLCellValue gt;
            try { gt = o.Value; } catch { return (false, "", null); }
            if (gt.IsNumber || gt.IsDateTime || gt.IsTimeSpan)
            {
                // Lấy số sê-ri gốc: file có ô số bị định dạng ngày (PO 8120288330, tồn kho 40 hiện 1900-02-09),
                // đọc thành DateTime sẽ lỗi/sai -> chỉ cột ngày mới đổi sê-ri sang ngày
                double so;
                try { so = gt.GetUnifiedNumber(); } catch { return (false, "", null); }
                switch (cot.Kieu)
                {
                    case KieuO.Ngay:
                        return so is >= 1 and < 2958466 ? (true, "", DateTime.FromOADate(so).Date) : (false, "", null);
                    case KieuO.So:
                        return (true, "", Math.Round((decimal)so, 2));
                    case KieuO.Chuoi:
                        return (true, "", Cat(so.ToString("0.##", CultureInfo.InvariantCulture), cot.DoDai));
                }
            }
            string s;
            try { s = (gt.IsText ? gt.GetText() : o.GetFormattedString()).Trim(); } catch { return (false, "", null); }
            if (cot.Kieu == KieuO.Chuoi) return (true, "", s.Length == 0 ? null : Cat(s, cot.DoDai));
            if (cot.Kieu == KieuO.TrangThai) return (true, "", s.ToLowerInvariant().StartsWith("close") ? "close" : "pending");
            return DocGiaTri(cot, s);
        }

        // Nhận cả 1.234,5 (vi) và 1,234.5 (en); một dấu chấm/phẩy mà đúng nhóm 3 số thì là phân cách nghìn
        private static decimal? DocSo(string s)
        {
            s = s.Replace(" ", "").Replace(" ", "").Replace("%", "");
            int cham = s.LastIndexOf('.'), phay = s.LastIndexOf(',');
            if (cham >= 0 && phay >= 0)
                s = cham > phay ? s.Replace(",", "") : s.Replace(".", "").Replace(',', '.');
            else if (phay >= 0)
                s = Regex.IsMatch(s, @"^-?\d{1,3}(,\d{3})+$") ? s.Replace(",", "") : s.Replace(',', '.');
            else if (cham >= 0 && Regex.IsMatch(s, @"^-?\d{1,3}(\.\d{3})+$"))
                s = s.Replace(".", "");
            return decimal.TryParse(s, NumberStyles.Number, CultureInfo.InvariantCulture, out var d) ? Math.Round(d, 2) : null;
        }

        private static readonly string[] DinhDangNgay = { "yyyy-MM-dd", "d/M/yyyy", "dd/MM/yyyy", "d-M-yyyy", "d.M.yyyy", "yyyy/M/d", "d/M/yy" };

        private static DateTime? DocNgay(string s)
            => DateTime.TryParseExact(s, DinhDangNgay, CultureInfo.InvariantCulture, DateTimeStyles.None, out var d) ? d.Date : null;
    }
}
