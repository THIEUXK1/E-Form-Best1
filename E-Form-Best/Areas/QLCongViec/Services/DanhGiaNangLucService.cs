using E_Form_Best.Context;
using E_Form_Best.Models.ITForm;
using Microsoft.EntityFrameworkCore;

namespace E_Form_Best.Areas.QLCongViec.Services
{
    /// <summary>
    /// Bản đồ năng lực + GAP theo "IT_Tổng hợp đánh giá chung.xlsx". Không tính vào điểm 100 của phiếu.
    ///   Năng lực của 1 người = năng lực thuộc nhóm công việc (DG_NhomCongViecNangLuc) có yêu cầu &gt; 0 ở cấp bậc của họ.
    ///   GAP từng năng lực = điểm thực tế − yêu cầu; GAP tổng = TRUNG BÌNH các năng lực đã chấm → xếp loại.
    ///   Khác file Excel: file so tổng 47 năng lực với yêu cầu của riêng nhóm nên ai cũng "Vượt chuẩn", ở đây chỉ so đúng năng lực của nhóm.
    /// Điểm lưu ở DG_PhieuNangLuc, kèm yêu cầu tại thời điểm chấm: kỳ đã đóng đọc từ đó, không phụ thuộc hồ sơ/ma trận hiện tại.
    /// Điểm chính thức là của quản lý; điểm tự chấm chỉ để đối chiếu.
    /// </summary>
    public class DanhGiaNangLucService
    {
        // Yêu cầu cao nhất của ma trận (bậc 5A = 8). DG_NangLuc.diem_toi_da = 5 là số của app cũ, không dùng
        public const decimal ThangDiemToiDa = 8m;

        private readonly ITFormContext _context;
        private readonly DanhGiaService _danhGia;
        private readonly DanhGiaQuanLyService _quanLy;

        public DanhGiaNangLucService(ITFormContext context, DanhGiaService danhGia, DanhGiaQuanLyService quanLy)
        {
            _context = context;
            _danhGia = danhGia;
            _quanLy = quanLy;
        }

        #region DTO

        public class NangLucDto
        {
            public Guid IdNangLuc { get; set; }
            public string Ma { get; set; } = "";
            public string Ten { get; set; } = "";
            public string? MoTa { get; set; }
            public string MaNhom { get; set; } = "";
            public string TenNhom { get; set; } = "";
            public decimal YeuCau { get; set; }
            public decimal? DiemTu { get; set; }
            public decimal? DiemQuanLy { get; set; }
        }

        public class TheoNhomDto
        {
            public string MaNhom { get; set; } = "";
            public string TenNhom { get; set; } = "";
            public decimal YeuCau { get; set; }
            public decimal? ThucTe { get; set; }
            public decimal? TuCham { get; set; }
            public decimal? Gap { get; set; }
        }

        public class KetQuaNguoi
        {
            public string MaNv { get; set; } = "";
            public string? CapBac { get; set; }
            public Guid? IdNhomCongViec { get; set; }
            public string? TenNhomCongViec { get; set; }
            public List<NangLucDto> DsNangLuc { get; set; } = new();
            public List<TheoNhomDto> TheoNhom { get; set; } = new();
            public int SoDaCham { get; set; }
            public decimal? TbGap { get; set; }
            public string? XepLoai { get; set; }
        }

        public class DiemVao
        {
            public Guid IdNangLuc { get; set; }
            public decimal? Diem { get; set; }
        }

        public class LuuVao
        {
            public string? MaNv { get; set; }
            public Guid IdKy { get; set; }
            public List<DiemVao>? DsDiem { get; set; }
        }

        #endregion

        #region Tính GAP

        public static string XepLoai(decimal tbGap)
        {
            // Mốc của sheet "Giải thích GAP & Xếp loại"
            if (tbGap >= 1) return "Vượt chuẩn";
            if (tbGap >= 0) return "Đạt chuẩn";
            if (tbGap >= -1) return "Cần cải thiện";
            return "Chưa đạt";
        }

        private static decimal? TrungBinh(IEnumerable<decimal> ds)
        {
            var l = ds.ToList();
            return l.Count == 0 ? null : Math.Round(l.Average(), 2);
        }

        /// <summary>Tổng hợp theo nhóm năng lực + GAP chung, chỉ tính năng lực quản lý đã chấm.</summary>
        private static void TongHop(KetQuaNguoi kq)
        {
            var daCham = kq.DsNangLuc.Where(x => x.DiemQuanLy != null).ToList();
            kq.SoDaCham = daCham.Count;
            kq.TbGap = TrungBinh(daCham.Select(x => x.DiemQuanLy!.Value - x.YeuCau));
            kq.XepLoai = kq.TbGap == null ? null : XepLoai(kq.TbGap.Value);
            kq.TheoNhom = kq.DsNangLuc
                .GroupBy(x => new { x.MaNhom, x.TenNhom })
                .OrderBy(g => ThuTuNhom(g.Key.MaNhom))
                .Select(g =>
                {
                    var cham = g.Where(x => x.DiemQuanLy != null).ToList();
                    return new TheoNhomDto
                    {
                        MaNhom = g.Key.MaNhom,
                        TenNhom = g.Key.TenNhom,
                        YeuCau = Math.Round(g.Average(x => x.YeuCau), 2),
                        ThucTe = TrungBinh(cham.Select(x => x.DiemQuanLy!.Value)),
                        TuCham = TrungBinh(g.Where(x => x.DiemTu != null).Select(x => x.DiemTu!.Value)),
                        Gap = TrungBinh(cham.Select(x => x.DiemQuanLy!.Value - x.YeuCau))
                    };
                }).ToList();
        }

        // Thứ tự cột như sheet giải thích: Kiến thức, Kỹ năng, Thái độ, Ngoại ngữ
        private static int ThuTuNhom(string maNhom) => maNhom switch { "KN" => 1, "KS" => 2, "TD" => 3, "NN" => 4, _ => 9 };

        #endregion

        #region Đọc

        /// <summary>Kết quả của nhiều người trong 1 kỳ. Đọc gộp một lượt để bản đồ không phát sinh N+1.</summary>
        private async Task<Dictionary<string, KetQuaNguoi>> TinhAsync(IReadOnlyCollection<string> dsMa, DanhGiaService.KyDto ky)
        {
            var kq = new Dictionary<string, KetQuaNguoi>(StringComparer.OrdinalIgnoreCase);
            if (dsMa.Count == 0) return kq;

            var phieu = await _context.DgPhieuDanhGias.AsNoTracking()
                .Where(x => x.IdKy == ky.IdKy && dsMa.Contains(x.MaNhanVien))
                .Select(x => new { x.IdPhieu, x.MaNhanVien, x.Loai, x.CapBac, x.IdNhomCongViec })
                .ToListAsync();
            var idPhieu = phieu.Select(x => x.IdPhieu).ToList();
            var dong = await _context.DgPhieuNangLucs.AsNoTracking().Where(x => idPhieu.Contains(x.IdPhieu)).ToListAsync();
            var hoSo = await _context.DgHoSoNhanViens.AsNoTracking().Where(x => dsMa.Contains(x.MaNhanVien))
                .Select(x => new { x.MaNhanVien, x.CapBac, x.IdNhomCongViec }).ToListAsync();
            var tenNhomCv = await _context.DgNhomCongViecs.AsNoTracking().ToDictionaryAsync(x => x.IdNhomCongViec, x => x.TenNhom);
            var nangLuc = await (
                from n in _context.DgNangLucs
                join g in _context.DgNhomNangLucs on n.IdNhomNangLuc equals g.IdNhomNangLuc
                select new { n.IdNangLuc, n.MaNangLuc, n.TenNangLuc, n.MoTa, MaNhom = g.MaNhom, TenNhom = g.TenNhom }
            ).AsNoTracking().ToDictionaryAsync(x => x.IdNangLuc);
            var nhomCvNl = (await _context.DgNhomCongViecNangLucs.AsNoTracking().ToListAsync()).ToLookup(x => x.IdNhomCongViec, x => x.IdNangLuc);
            var yeuCau = (await _context.DgMaTranYeuCaus.AsNoTracking().Select(x => new { x.CapBac, x.IdNangLuc, x.DiemYeuCau }).ToListAsync())
                .ToDictionary(x => (x.CapBac, x.IdNangLuc), x => x.DiemYeuCau);

            foreach (var ma in dsMa)
            {
                var tu = phieu.FirstOrDefault(x => string.Equals(x.MaNhanVien, ma, StringComparison.OrdinalIgnoreCase) && x.Loai == DanhGiaService.LoaiTuDanhGia);
                var ql = phieu.FirstOrDefault(x => string.Equals(x.MaNhanVien, ma, StringComparison.OrdinalIgnoreCase) && x.Loai == DanhGiaService.LoaiQuanLy);
                var hs = hoSo.FirstOrDefault(x => string.Equals(x.MaNhanVien, ma, StringComparison.OrdinalIgnoreCase));
                var diemTu = dong.Where(x => x.IdPhieu == tu?.IdPhieu).ToDictionary(x => x.IdNangLuc);
                var diemQl = dong.Where(x => x.IdPhieu == ql?.IdPhieu).ToDictionary(x => x.IdNangLuc);

                var nguoi = new KetQuaNguoi { MaNv = ma };
                // id năng lực → yêu cầu áp dụng cho người này trong kỳ
                Dictionary<Guid, decimal> dsYeuCau;
                if (ky.DangMo)
                {
                    nguoi.CapBac = hs?.CapBac;
                    nguoi.IdNhomCongViec = hs?.IdNhomCongViec;
                    dsYeuCau = new();
                    if (hs?.CapBac != null && hs.IdNhomCongViec != null)
                        foreach (var id in nhomCvNl[hs.IdNhomCongViec.Value])
                            if (yeuCau.TryGetValue((hs.CapBac, id), out var yc) && yc > 0) dsYeuCau[id] = yc;
                }
                else
                {
                    nguoi.CapBac = ql?.CapBac ?? tu?.CapBac;
                    nguoi.IdNhomCongViec = ql?.IdNhomCongViec ?? tu?.IdNhomCongViec;
                    dsYeuCau = diemQl.Values.Concat(diemTu.Values)
                        .Where(x => x.DiemYeuCau > 0)
                        .GroupBy(x => x.IdNangLuc).ToDictionary(g => g.Key, g => g.First().DiemYeuCau);
                }
                if (nguoi.IdNhomCongViec != null && tenNhomCv.TryGetValue(nguoi.IdNhomCongViec.Value, out var ten))
                    nguoi.TenNhomCongViec = ten;

                nguoi.DsNangLuc = dsYeuCau
                    .Where(x => nangLuc.ContainsKey(x.Key))
                    .Select(x =>
                    {
                        var n = nangLuc[x.Key];
                        return new NangLucDto
                        {
                            IdNangLuc = x.Key, Ma = n.MaNangLuc, Ten = n.TenNangLuc, MoTa = n.MoTa, MaNhom = n.MaNhom, TenNhom = n.TenNhom,
                            YeuCau = x.Value,
                            DiemTu = diemTu.TryGetValue(x.Key, out var a) ? a.DiemThucTe : null,
                            DiemQuanLy = diemQl.TryGetValue(x.Key, out var b) ? b.DiemThucTe : null
                        };
                    })
                    .OrderBy(x => ThuTuNhom(x.MaNhom)).ThenBy(x => x.Ma)
                    .ToList();
                TongHop(nguoi);
                if (ky.ThangCu)
                {
                    // Kỳ thang cũ chấm tới 10 trên ma trận 0–8 của app cũ: GAP ra số dương giả, không xếp loại
                    nguoi.TbGap = null;
                    nguoi.XepLoai = null;
                    foreach (var n in nguoi.TheoNhom) n.Gap = null;
                }
                kq[ma] = nguoi;
            }
            return kq;
        }

        public async Task<KetQuaNguoi> LayCuaNguoiAsync(string maNv, DanhGiaService.KyDto ky)
            => (await TinhAsync(new[] { maNv }, ky))[maNv];

        /// <summary>
        /// Điểm quản lý chỉ cho nhân viên thấy khi kỳ đã đóng hoặc phiếu đã hoàn tất — giống Tự đánh giá,
        /// để bản tự chấm không bị lái theo điểm quản lý đang chấm dở.
        /// </summary>
        public async Task<KetQuaNguoi> LayCuaToiAsync(string maNv, DanhGiaService.KyDto ky)
        {
            var kq = await LayCuaNguoiAsync(maNv, ky);
            if (ky.DangMo && !await PhieuTuDaChotAsync(maNv, ky.IdKy))
            {
                foreach (var n in kq.DsNangLuc) n.DiemQuanLy = null;
                TongHop(kq);
            }
            return kq;
        }

        private Task<bool> PhieuTuDaChotAsync(string maNv, Guid idKy) => _danhGia.PhieuDaKhoaAsync(maNv, idKy);

        /// <summary>Bản đồ cả danh sách của kỳ, kèm tên + phòng ban để client lọc.</summary>
        public async Task<List<object>> BanDoAsync(DanhGiaService.KyDto ky)
        {
            var dsMa = await _quanLy.DsMaNvTheoKyAsync(ky);
            var kq = await TinhAsync(dsMa, ky);
            var ten = await _context.Users.AsNoTracking().Where(u => u.Tk != null && dsMa.Contains(u.Tk))
                .Select(u => new { u.Tk, u.HoTen }).ToListAsync();
            return kq.Values
                .OrderBy(x => x.TenNhomCongViec == null).ThenBy(x => x.TbGap ?? decimal.MaxValue).ThenBy(x => x.MaNv)
                .Select(x => (object)new
                {
                    x.MaNv,
                    hoTen = ten.FirstOrDefault(u => string.Equals(u.Tk, x.MaNv, StringComparison.OrdinalIgnoreCase))?.HoTen ?? x.MaNv,
                    x.CapBac,
                    x.TenNhomCongViec,
                    soNangLuc = x.DsNangLuc.Count,
                    x.SoDaCham,
                    x.TbGap,
                    x.XepLoai,
                    theoNhom = x.TheoNhom.Select(n => new { n.MaNhom, n.Gap })
                }).ToList();
        }

        #endregion

        #region Ghi

        /// <summary>Ghi điểm vào phiếu theo loại. Trả null nếu hợp lệ, ngược lại là thông báo lỗi. Ô bỏ trống = xoá điểm năng lực đó.</summary>
        public async Task<string?> LuuAsync(string maNv, Guid idKy, string loai, IEnumerable<DiemVao>? dsDiem)
        {
            if (idKy != await _danhGia.IdKyDangMoAsync()) return DanhGiaService.LoiKyDong;
            if (loai == DanhGiaService.LoaiTuDanhGia && await PhieuTuDaChotAsync(maNv, idKy))
                return "Phiếu đã được quản lý đánh giá xong, không sửa được nữa.";

            var ky = await _danhGia.LayKyAsync(idKy);
            var khung = (await LayCuaNguoiAsync(maNv, ky!)).DsNangLuc.ToDictionary(x => x.IdNangLuc);
            if (khung.Count == 0) return "Chưa có khung năng lực — gán cấp bậc và nhóm công việc ở “Hồ sơ & KPI”.";

            // Chỉ nhận năng lực trong khung của chính người này — id lạ gửi lên bị bỏ qua
            var vao = (dsDiem ?? Enumerable.Empty<DiemVao>())
                .Where(x => khung.ContainsKey(x.IdNangLuc))
                .GroupBy(x => x.IdNangLuc).Select(g => g.Last()).ToList();
            var sai = vao.FirstOrDefault(x => x.Diem is { } d && (d < 0 || d > ThangDiemToiDa));
            if (sai != null) return $"Điểm \"{khung[sai.IdNangLuc].Ten}\" phải trong khoảng 0–{ThangDiemToiDa:0}.";

            var hoSo = await _danhGia.LayHoSoAsync(maNv);
            var phieu = await _danhGia.LayHoacTaoPhieuAsync(maNv, idKy, loai);
            var cu = await _context.DgPhieuNangLucs.Where(x => x.IdPhieu == phieu.IdPhieu).ToDictionaryAsync(x => x.IdNangLuc);
            foreach (var d in vao)
            {
                if (d.Diem == null)
                {
                    if (cu.TryGetValue(d.IdNangLuc, out var bo)) _context.DgPhieuNangLucs.Remove(bo);
                    continue;
                }
                if (!cu.TryGetValue(d.IdNangLuc, out var dong))
                {
                    dong = new DgPhieuNangLuc { IdPhieuNangLuc = Guid.NewGuid(), IdPhieu = phieu.IdPhieu, IdNangLuc = d.IdNangLuc };
                    _context.DgPhieuNangLucs.Add(dong);
                }
                var diem = Math.Round(d.Diem.Value, 2);
                dong.DiemThucTe = diem;
                dong.DiemYeuCau = khung[d.IdNangLuc].YeuCau;
                dong.ChenhLech = diem - dong.DiemYeuCau;
            }
            // Giữ cấp bậc/nhóm lúc chấm để đọc lại kỳ đã đóng đúng như khi chấm
            phieu.CapBac = hoSo.CapBac;
            phieu.IdNhomCongViec = hoSo.IdNhomCongViec;
            phieu.NgayCapNhat = DateTime.Now;
            await _context.SaveChangesAsync();
            return null;
        }

        #endregion
    }
}
