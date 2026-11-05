using E_Form_Best.Context;
using E_Form_Best.Models.ITForm;
using Microsoft.EntityFrameworkCore;

namespace E_Form_Best.Areas.QLCongViec.Services
{
    /// <summary>
    /// Ký xác nhận 3 cấp theo ô ký của mẫu Excel: quản lý chấm (Evaluated by) → cấp trên duyệt (Approved by)
    /// → nhân viên ký (Employee signed + Comment). Không đổi nghĩa trang_thai = completed (thống kê và khoá phiếu cũ
    /// vẫn dựa vào đó), luồng ký là lớp riêng ở buoc_ky trên phiếu manager.
    /// Mọi bước chuyển dùng cập nhật có điều kiện theo bước hiện tại để 2 thao tác đồng thời không chồng nhau.
    /// </summary>
    public class DanhGiaKyXacNhanService
    {
        private const string LoiSaiBuoc = "Phiếu đã chuyển sang bước khác, tải lại để xem trạng thái mới.";

        private readonly ITFormContext _context;

        public DanhGiaKyXacNhanService(ITFormContext context)
        {
            _context = context;
        }

        #region DTO

        public class DongChoDuyetDto
        {
            public string MaNv { get; set; } = "";
            public string HoTen { get; set; } = "";
            public string? CapBac { get; set; }
            public decimal? DiemThucTe { get; set; }
            public string? XepLoai { get; set; }
            public string? BuocKy { get; set; }
            public string? TenNguoiCham { get; set; }
            public DateTime? NgayCham { get; set; }
            public DateTime? NgayDuyet { get; set; }
            public DateTime? NgayNvKy { get; set; }
            public bool? NvDongY { get; set; }
        }

        public class KetQuaDto
        {
            public decimal? DiemKpi { get; set; }
            public decimal? DiemNangLuc { get; set; }
            public decimal? DiemThucTe { get; set; }
            public decimal? ChenhLech { get; set; }
            public string? XepLoai { get; set; }
            public string? NhanXet { get; set; }
        }

        public class LichSuDto
        {
            public string HanhDong { get; set; } = "";
            public string? TenNguoi { get; set; }
            public string? GhiChu { get; set; }
            public DateTime Ngay { get; set; }
        }

        public class TrangThaiKyDto
        {
            public bool CoPhieu { get; set; }
            public string? BuocKy { get; set; }
            /// <summary>Phiếu chốt trước khi có luồng ký (kỳ cũ), không ký bổ sung.</summary>
            public bool ChotTruocLuongKy { get; set; }
            public bool KyDangMo { get; set; }
            public string? TenNguoiCham { get; set; }
            public DateTime? NgayCham { get; set; }
            public string? TenNguoiDuyet { get; set; }
            public DateTime? NgayDuyet { get; set; }
            public string? LyDoTuChoi { get; set; }
            public string? TenNhanVien { get; set; }
            public DateTime? NgayNvKy { get; set; }
            public bool? NvDongY { get; set; }
            public string? NhanXetNv { get; set; }
            public KetQuaDto? KetQua { get; set; }
            public List<LichSuDto> DsLichSu { get; set; } = new();
            public bool CoTheDuyet { get; set; }
            public bool CoTheTuChoi { get; set; }
            public bool CoTheKy { get; set; }
        }

        public class PhieuVao
        {
            public string? MaNv { get; set; }
            public Guid IdKy { get; set; }
        }

        public class TuChoiVao : PhieuVao
        {
            public string? LyDo { get; set; }
        }

        public class KyVao
        {
            public Guid IdKy { get; set; }
            public bool? DongY { get; set; }
            public string? NhanXet { get; set; }
        }

        #endregion

        private Task<DgPhieuDanhGia?> LayPhieuQlAsync(string maNv, Guid idKy)
            => _context.DgPhieuDanhGias.AsNoTracking()
                .FirstOrDefaultAsync(x => x.IdKy == idKy && x.MaNhanVien == maNv && x.Loai == DanhGiaService.LoaiQuanLy);

        private async Task<Dictionary<string, string?>> TenAsync(IEnumerable<string?> dsMa)
        {
            var ma = dsMa.Where(x => !string.IsNullOrEmpty(x)).Select(x => x!).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
            var ds = await _context.Users.AsNoTracking().Where(u => u.Tk != null && ma.Contains(u.Tk))
                .Select(u => new { u.Tk, u.HoTen }).ToListAsync();
            var kq = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);
            foreach (var u in ds) kq[u.Tk!] = u.HoTen;
            return kq;
        }

        private static string? TenCua(Dictionary<string, string?> ten, string? ma)
            => ma == null ? null : ten.TryGetValue(ma, out var t) ? t ?? ma : ma;

        /// <summary>Hàng đợi của cấp trên: mọi phiếu đã vào luồng ký của kỳ, việc cần làm lên đầu.</summary>
        public async Task<List<DongChoDuyetDto>> DanhSachAsync(DanhGiaService.KyDto ky)
        {
            var ds = await _context.DgPhieuDanhGias.AsNoTracking()
                .Where(x => x.IdKy == ky.IdKy && x.Loai == DanhGiaService.LoaiQuanLy && (x.BuocKy != null || x.TrangThai == "completed"))
                .ToListAsync();
            var ten = await TenAsync(ds.Select(x => x.MaNhanVien).Concat(ds.Select(x => x.NguoiDanhGia)));
            return ds.Select(p => new DongChoDuyetDto
            {
                MaNv = p.MaNhanVien,
                HoTen = TenCua(ten, p.MaNhanVien) ?? p.MaNhanVien,
                CapBac = p.CapBac,
                // Điểm kỳ thang cũ không so được với chuẩn 60 nên không hiện để khỏi ra xếp loại giả
                DiemThucTe = ky.ThangCu ? null : p.DiemThucTe,
                XepLoai = ky.ThangCu ? null : p.XepLoai,
                BuocKy = p.BuocKy,
                TenNguoiCham = TenCua(ten, p.NguoiDanhGia),
                NgayCham = p.NgayNop,
                NgayDuyet = p.NgayDuyet,
                NgayNvKy = p.NgayNvKy,
                NvDongY = p.NvDongY
            })
            .OrderBy(x => x.BuocKy switch
            {
                DgPhieuDanhGia.BuocChoDuyet => 0,
                DgPhieuDanhGia.BuocTuChoi => 1,
                DgPhieuDanhGia.BuocChoKy => 2,
                DgPhieuDanhGia.BuocDaKy => 3,
                _ => 4
            })
            .ThenBy(x => x.HoTen)
            .ToList();
        }

        /// <summary>
        /// Trạng thái ký của 1 phiếu. Nhân viên chỉ thấy điểm quản lý từ lúc tới lượt mình ký, và không thấy
        /// lịch sử nội bộ (lý do cấp trên từ chối là trao đổi giữa 2 cấp quản lý).
        /// </summary>
        public async Task<TrangThaiKyDto> LayAsync(string maNv, DanhGiaService.KyDto ky, bool laNhanVien, bool laCapTren, string nguoiXem)
        {
            var p = await LayPhieuQlAsync(maNv, ky.IdKy);
            if (p == null) return new TrangThaiKyDto { KyDangMo = ky.DangMo };

            var coKetQua = !laNhanVien || p.BuocKy is DgPhieuDanhGia.BuocChoKy or DgPhieuDanhGia.BuocDaKy;
            var lichSu = laNhanVien ? new List<DgPhieuKyLichSu>() : await _context.DgPhieuKyLichSus.AsNoTracking()
                .Where(x => x.IdPhieu == p.IdPhieu).OrderBy(x => x.Ngay).ToListAsync();
            var ten = await TenAsync(lichSu.Select(x => x.Nguoi).Concat(new[] { p.NguoiDanhGia, p.NguoiDuyet, maNv }));
            var khongPhaiChinhMinh = !string.Equals(maNv, nguoiXem, StringComparison.OrdinalIgnoreCase);

            return new TrangThaiKyDto
            {
                CoPhieu = true,
                BuocKy = p.BuocKy,
                ChotTruocLuongKy = p.BuocKy == null && p.TrangThai == "completed",
                KyDangMo = ky.DangMo,
                TenNguoiCham = TenCua(ten, p.NguoiDanhGia),
                NgayCham = p.NgayNop,
                TenNguoiDuyet = TenCua(ten, p.NguoiDuyet),
                NgayDuyet = p.NgayDuyet,
                LyDoTuChoi = laNhanVien ? null : p.LyDoTuChoi,
                TenNhanVien = TenCua(ten, maNv),
                NgayNvKy = p.NgayNvKy,
                NvDongY = p.NvDongY,
                NhanXetNv = p.NhanXetNv,
                KetQua = !coKetQua ? null : new KetQuaDto
                {
                    DiemKpi = p.DiemKpi, DiemNangLuc = p.DiemNangLuc, DiemThucTe = p.DiemThucTe,
                    ChenhLech = p.ChenhLech, XepLoai = p.XepLoai, NhanXet = p.NhanXet
                },
                DsLichSu = lichSu.Select(x => new LichSuDto { HanhDong = x.HanhDong, TenNguoi = TenCua(ten, x.Nguoi), GhiChu = x.GhiChu, Ngay = x.Ngay }).ToList(),
                CoTheDuyet = laCapTren && khongPhaiChinhMinh && p.BuocKy == DgPhieuDanhGia.BuocChoDuyet,
                // Từ chối trả phiếu về người chấm, mà kỳ đóng thì không chấm lại được
                CoTheTuChoi = laCapTren && khongPhaiChinhMinh && p.BuocKy == DgPhieuDanhGia.BuocChoDuyet && ky.DangMo,
                CoTheKy = laNhanVien && p.BuocKy == DgPhieuDanhGia.BuocChoKy
            };
        }

        private void GhiLichSu(Guid idPhieu, string hanhDong, string nguoi, string? ghiChu, DateTime ngay)
            => _context.DgPhieuKyLichSus.Add(new DgPhieuKyLichSu
            {
                IdLichSu = Guid.NewGuid(), IdPhieu = idPhieu, HanhDong = hanhDong, Nguoi = nguoi, GhiChu = ghiChu, Ngay = ngay
            });

        public async Task<string?> DuyetAsync(string maNv, Guid idKy, string nguoi)
        {
            if (string.Equals(maNv, nguoi, StringComparison.OrdinalIgnoreCase)) return "Không tự duyệt phiếu của chính mình.";
            var p = await LayPhieuQlAsync(maNv, idKy);
            if (p == null) return "Không tìm thấy phiếu.";

            var bayGio = DateTime.Now;
            await using var tx = await _context.Database.BeginTransactionAsync();
            var soDong = await _context.DgPhieuDanhGias
                .Where(x => x.IdPhieu == p.IdPhieu && x.BuocKy == DgPhieuDanhGia.BuocChoDuyet)
                .ExecuteUpdateAsync(s => s
                    .SetProperty(x => x.BuocKy, DgPhieuDanhGia.BuocChoKy)
                    .SetProperty(x => x.NguoiDuyet, nguoi)
                    .SetProperty(x => x.NgayDuyet, bayGio));
            if (soDong == 0) return LoiSaiBuoc;
            GhiLichSu(p.IdPhieu, DgPhieuKyLichSu.Duyet, nguoi, null, bayGio);
            await _context.SaveChangesAsync();
            await tx.CommitAsync();
            return null;
        }

        /// <summary>
        /// Trả phiếu về người chấm. Phiếu tự đánh giá giữ nguyên trạng thái khoá vì nhân viên không cần sửa;
        /// bỏ ngày gửi của quản lý để quản lý phải gửi lại mới vào luồng ký lần nữa.
        /// </summary>
        public async Task<string?> TuChoiAsync(string maNv, Guid idKy, string? lyDo, string nguoi)
        {
            lyDo = lyDo?.Trim();
            if (string.IsNullOrEmpty(lyDo)) return "Cần nêu lý do từ chối.";
            if (lyDo.Length > 1000) return "Lý do tối đa 1000 ký tự.";
            if (string.Equals(maNv, nguoi, StringComparison.OrdinalIgnoreCase)) return "Không tự duyệt phiếu của chính mình.";
            if (idKy != await _context.DgKyDanhGias.AsNoTracking().Where(x => x.TrangThai == DanhGiaService.KyDangMo).Select(x => x.IdKy).FirstOrDefaultAsync())
                return "Kỳ đã đóng nên không chấm lại được. Mở lại kỳ trước khi từ chối.";
            var p = await LayPhieuQlAsync(maNv, idKy);
            if (p == null) return "Không tìm thấy phiếu.";

            var bayGio = DateTime.Now;
            await using var tx = await _context.Database.BeginTransactionAsync();
            var soDong = await _context.DgPhieuDanhGias
                .Where(x => x.IdPhieu == p.IdPhieu && x.BuocKy == DgPhieuDanhGia.BuocChoDuyet)
                .ExecuteUpdateAsync(s => s
                    .SetProperty(x => x.BuocKy, DgPhieuDanhGia.BuocTuChoi)
                    .SetProperty(x => x.LyDoTuChoi, lyDo)
                    .SetProperty(x => x.NgayNop, (DateTime?)null)
                    .SetProperty(x => x.TrangThai, "pending"));
            if (soDong == 0) return LoiSaiBuoc;
            GhiLichSu(p.IdPhieu, DgPhieuKyLichSu.TuChoi, nguoi, lyDo, bayGio);
            await _context.SaveChangesAsync();
            await tx.CommitAsync();
            return null;
        }

        /// <summary>Nhân viên ký. Không đồng ý vẫn hoàn tất phiếu, ý kiến hiện cho cấp trên; không đồng ý thì bắt buộc có nhận xét.</summary>
        public async Task<string?> NhanVienKyAsync(string maNv, KyVao vao)
        {
            if (vao.DongY == null) return "Chọn Đồng ý hoặc Không đồng ý.";
            var nhanXet = DanhGiaService.CatChu(vao.NhanXet);
            if (vao.DongY == false && nhanXet == null) return "Không đồng ý thì cần ghi nhận xét.";
            var p = await LayPhieuQlAsync(maNv, vao.IdKy);
            if (p == null) return "Không tìm thấy phiếu.";

            var bayGio = DateTime.Now;
            await using var tx = await _context.Database.BeginTransactionAsync();
            var soDong = await _context.DgPhieuDanhGias
                .Where(x => x.IdPhieu == p.IdPhieu && x.BuocKy == DgPhieuDanhGia.BuocChoKy)
                .ExecuteUpdateAsync(s => s
                    .SetProperty(x => x.BuocKy, DgPhieuDanhGia.BuocDaKy)
                    .SetProperty(x => x.NgayNvKy, bayGio)
                    .SetProperty(x => x.NvDongY, vao.DongY)
                    .SetProperty(x => x.NhanXetNv, nhanXet));
            if (soDong == 0) return LoiSaiBuoc;
            GhiLichSu(p.IdPhieu, DgPhieuKyLichSu.NvKy, maNv, (vao.DongY == true ? "Đồng ý" : "Không đồng ý") + (nhanXet == null ? "" : ": " + nhanXet), bayGio);
            await _context.SaveChangesAsync();
            await tx.CommitAsync();
            return null;
        }
    }
}
