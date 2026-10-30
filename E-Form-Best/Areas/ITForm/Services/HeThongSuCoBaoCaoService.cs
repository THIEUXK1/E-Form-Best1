using E_Form_Best.Context;
using E_Form_Best.Models.ITForm;
using Microsoft.EntityFrameworkCore;

namespace E_Form_Best.Areas.ITForm.Services
{
    /// <summary>
    /// Báo cáo sự cố hệ thống nội bộ (/TheoDoiHeThong) theo kỳ: tổng hợp từng hệ thống, nguyên nhân hay gặp
    /// và danh sách sự cố. Dùng chung cho màn hình (JSON) và file Excel (HeThongSuCoBaoCaoExcel).
    /// Sự cố thuộc kỳ = có khoảng thời gian giao với kỳ (bắt đầu trước khi kỳ hết, chưa kết thúc hoặc kết thúc sau khi kỳ bắt đầu).
    /// </summary>
    public class HeThongSuCoBaoCaoService
    {
        private readonly ITFormContext _context;

        public HeThongSuCoBaoCaoService(ITFormContext context) => _context = context;

        public static readonly Dictionary<string, string> TenTrangThai = new()
        {
            [KkHeThongSuCo.DangXuLy] = "Đang xử lý",
            [KkHeThongSuCo.TheoDoi] = "Đang theo dõi",
            [KkHeThongSuCo.DaKhacPhuc] = "Đã khắc phục",
        };

        public record DongSuCo(int IdSuCo, string MaHeThong, string TieuDe, string TrangThai, string TenTrangThai,
            DateTime BatDauLuc, DateTime? KetThucLuc, double SoGio, double SoGioTrongKy,
            string? NguyenNhan, string? NguoiChiuTrachNhiem, string? MoTa, string? NguoiTao, string? GhiChuCuoi);

        public record TongHopHeThong(string MaHeThong, int SoSuCo, int DaKhacPhuc, int DangMo,
            double SoGioTrongKy, double? SoGioKhacPhucTrungBinh, double SoGioDaiNhat, DateTime? SuCoGanNhat);

        public record NhomNguyenNhan(string NguyenNhan, int SoLan, string HeThong);

        public record BaoCao(DateTime TuNgay, DateTime DenNgay, DateTime TaoLuc,
            List<TongHopHeThong> HeThong, List<NhomNguyenNhan> NguyenNhan, List<DongSuCo> SuCo);

        /// <param name="tuNgay">Ngày đầu kỳ (lấy từ 00:00).</param>
        /// <param name="denNgay">Ngày cuối kỳ (tính hết ngày đó).</param>
        public async Task<BaoCao> TaoAsync(List<string> dsMa, DateTime tuNgay, DateTime denNgay, CancellationToken ct = default)
        {
            var tu = tuNgay.Date;
            var denHet = denNgay.Date.AddDays(1);
            var bayGio = DateTime.Now;

            var dsGoc = await _context.KkHeThongSuCos.AsNoTracking()
                .Where(x => x.NgayXoa == null && dsMa.Contains(x.MaHeThong)
                            && x.BatDauLuc < denHet && (x.KetThucLuc == null || x.KetThucLuc >= tu))
                .OrderBy(x => x.MaHeThong).ThenByDescending(x => x.BatDauLuc)
                .Select(x => new
                {
                    x.IdSuCo, x.MaHeThong, x.TieuDe, x.TrangThai, x.BatDauLuc, x.KetThucLuc,
                    x.NguyenNhan, x.NguoiChiuTrachNhiem, x.MoTa, x.NguoiTao
                })
                .ToListAsync(ct);

            // Ghi chú tiến độ gần nhất của từng sự cố (thường là cách khắc phục khi đóng)
            var dsId = dsGoc.Select(x => x.IdSuCo).ToList();
            var ghiChuCuoi = (await _context.KkHeThongSuCoLichSus.AsNoTracking()
                    .Where(x => dsId.Contains(x.IdSuCo) && x.GhiChu != null)
                    .Select(x => new { x.IdSuCo, x.Id, x.GhiChu })
                    .ToListAsync(ct))
                .GroupBy(x => x.IdSuCo)
                .ToDictionary(g => g.Key, g => g.OrderByDescending(x => x.Id).First().GhiChu);

            var suCo = dsGoc.Select(x =>
            {
                var ketThuc = x.KetThucLuc ?? bayGio;   // chưa đóng thì tính tới hiện tại
                var trongKyTu = x.BatDauLuc > tu ? x.BatDauLuc : tu;
                var trongKyDen = ketThuc < denHet ? ketThuc : denHet;
                return new DongSuCo(x.IdSuCo, x.MaHeThong, x.TieuDe, x.TrangThai, TenTrangThai.GetValueOrDefault(x.TrangThai, x.TrangThai),
                    x.BatDauLuc, x.KetThucLuc,
                    Math.Round(Math.Max(0, (ketThuc - x.BatDauLuc).TotalHours), 2),
                    Math.Round(Math.Max(0, (trongKyDen - trongKyTu).TotalHours), 2),
                    x.NguyenNhan, x.NguoiChiuTrachNhiem, x.MoTa, x.NguoiTao, ghiChuCuoi.GetValueOrDefault(x.IdSuCo));
            }).ToList();

            // Mọi hệ thống được chọn đều có dòng, kể cả không có sự cố, để thấy hệ thống nào ổn định
            var heThong = dsMa.Select(ma =>
            {
                var ds = suCo.Where(x => x.MaHeThong == ma).ToList();
                var daDong = ds.Where(x => x.TrangThai == KkHeThongSuCo.DaKhacPhuc).ToList();
                return new TongHopHeThong(ma, ds.Count, daDong.Count, ds.Count - daDong.Count,
                    Math.Round(ds.Sum(x => x.SoGioTrongKy), 2),
                    daDong.Count > 0 ? Math.Round(daDong.Average(x => x.SoGio), 2) : null,
                    ds.Count > 0 ? ds.Max(x => x.SoGio) : 0,
                    ds.Count > 0 ? ds.Max(x => x.BatDauLuc) : null);
            }).ToList();

            var nguyenNhan = suCo
                .GroupBy(x => string.IsNullOrWhiteSpace(x.NguyenNhan) ? "(Chưa xác định)" : x.NguyenNhan.Trim(), StringComparer.OrdinalIgnoreCase)
                .Select(g => new NhomNguyenNhan(g.Key, g.Count(), string.Join(", ", g.Select(x => x.MaHeThong).Distinct())))
                .OrderByDescending(x => x.SoLan).ThenBy(x => x.NguyenNhan)
                .ToList();

            return new BaoCao(tu, denNgay.Date, bayGio, heThong, nguyenNhan, suCo);
        }
    }
}
