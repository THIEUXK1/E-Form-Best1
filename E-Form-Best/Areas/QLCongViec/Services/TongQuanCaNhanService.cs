using E_Form_Best.Context;
using E_Form_Best.Models.ITForm;
using Microsoft.EntityFrameworkCore;

namespace E_Form_Best.Areas.QLCongViec.Services
{
    /// <summary>
    /// Dashboard cá nhân ở /QLCongViec/Logo. Trang này ai vào Form Công việc cũng thấy nên chỉ người trong danh sách
    /// đánh giá mới có phần đánh giá. Điểm và GAP theo đúng quy tắc của các trang gốc: nhân viên chưa tới lượt ký thì không thấy.
    /// </summary>
    public class TongQuanCaNhanService
    {
        private const int SoDaoTaoToiDa = 6;
        private const int SoNangLucYeuToiDa = 3;

        private readonly ITFormContext _context;
        private readonly DanhGiaService _danhGia;
        private readonly DanhGiaQuanLyService _quanLy;
        private readonly DanhGiaNangLucService _nangLuc;
        private readonly DanhGiaPhanCongService _phanCong;

        public TongQuanCaNhanService(ITFormContext context, DanhGiaService danhGia, DanhGiaQuanLyService quanLy,
            DanhGiaNangLucService nangLuc, DanhGiaPhanCongService phanCong)
        {
            _context = context;
            _danhGia = danhGia;
            _quanLy = quanLy;
            _nangLuc = nangLuc;
            _phanCong = phanCong;
        }

        #region DTO

        public class ViecDto
        {
            public string TieuDe { get; set; } = "";
            public string? MoTa { get; set; }
            public string Url { get; set; } = "";
            public string Icon { get; set; } = "";
            public bool Gap { get; set; }
        }

        public class DiemDto
        {
            public string TenKy { get; set; } = "";
            public decimal? DiemKpi { get; set; }
            public decimal? DiemNangLuc { get; set; }
            public decimal? DiemThucTe { get; set; }
            public decimal? ChenhLech { get; set; }
            public string? XepLoai { get; set; }
        }

        public class NhomGapDto
        {
            public string TenNhom { get; set; } = "";
            public decimal? Gap { get; set; }
        }

        public class NangLucYeuDto
        {
            public string Ten { get; set; } = "";
            public decimal Gap { get; set; }
        }

        public class GapDto
        {
            public string TenKy { get; set; } = "";
            public decimal? TbGap { get; set; }
            public string? XepLoai { get; set; }
            public List<NhomGapDto> DsNhom { get; set; } = new();
            public List<NangLucYeuDto> DsYeu { get; set; } = new();
        }

        public class DaoTaoDto
        {
            public int Id { get; set; }
            public string Ten { get; set; } = "";
            public DateOnly? Ngay { get; set; }
            public string? NguoiDaoTao { get; set; }
            public string TrangThai { get; set; } = "";
            public bool LaGiangVien { get; set; }
        }

        public class PhuTrachDto
        {
            public int SoChinh { get; set; }
            public int SoDuPhong { get; set; }
            public int SoCanhBao { get; set; }
        }

        public class TongQuanDto
        {
            public string? HoTen { get; set; }
            public bool CoDanhGia { get; set; }
            public string? CapBac { get; set; }
            public List<ViecDto> DsViec { get; set; } = new();
            public DiemDto? Diem { get; set; }
            public GapDto? Gap { get; set; }
            public List<DaoTaoDto> DsDaoTao { get; set; } = new();
            public PhuTrachDto? PhuTrach { get; set; }
        }

        #endregion

        public async Task<TongQuanDto> LayAsync(string maNv, bool laQuanLy, bool laCapTren)
        {
            var nguoi = await _context.Users.AsNoTracking().Where(u => u.Tk == maNv)
                .Select(u => new { u.IdNguoiDung, u.HoTen }).FirstOrDefaultAsync();
            var kq = new TongQuanDto
            {
                HoTen = nguoi?.HoTen,
                CoDanhGia = await _quanLy.TrongHeThongAsync(maNv),
                DsDaoTao = await LayDaoTaoAsync(maNv, nguoi?.IdNguoiDung)
            };

            var dsKy = await _danhGia.DsKyAsync();
            var kyMo = dsKy.FirstOrDefault(x => x.DangMo);
            if (kq.CoDanhGia)
            {
                // Đọc thẳng hồ sơ: LayHoSoAsync điền bậc mặc định cho người chưa có bậc
                kq.CapBac = await _context.DgHoSoNhanViens.AsNoTracking().Where(x => x.MaNhanVien == maNv).Select(x => x.CapBac).FirstOrDefaultAsync();
                await ThemViecNhanVienAsync(kq, maNv, kyMo, dsKy);
                kq.Diem = await LayDiemAsync(maNv, dsKy);
                kq.Gap = await LayGapAsync(maNv, dsKy);
            }
            if (laQuanLy && kyMo != null) await ThemViecQuanLyAsync(kq, kyMo);
            if (laCapTren) await ThemViecCapTrenAsync(kq);

            // Trang này cả công ty vào nên chỉ dựng ma trận cho người thật sự có việc được giao
            if (await _context.DgDauViecPhanCongs.AsNoTracking().AnyAsync(x => x.MaNhanVien == maNv && x.NgayGo == null))
            {
                var maTran = await _phanCong.MaTranAsync();
                var cuaToi = maTran.DsNguoi.First(x => x.MaNv.Equals(maNv, StringComparison.OrdinalIgnoreCase));
                kq.PhuTrach = new PhuTrachDto
                {
                    SoChinh = cuaToi.SoChinh,
                    SoDuPhong = cuaToi.SoDuPhong,
                    SoCanhBao = maTran.DsDauViec.Count(v => v.DsCanhBao.Count > 0 && v.DsGan.Any(g => g.MaNv.Equals(maNv, StringComparison.OrdinalIgnoreCase)))
                };
            }
            return kq;
        }

        private async Task ThemViecNhanVienAsync(TongQuanDto kq, string maNv, DanhGiaService.KyDto? kyMo, List<DanhGiaService.KyDto> dsKy)
        {
            if (kyMo != null)
            {
                var phieuTu = await _danhGia.LayPhieuTuDanhGiaAsync(maNv, kyMo.IdKy);
                if (phieuTu?.NgayNop == null)
                {
                    var quaHan = kyMo.HanNop != null && kyMo.HanNop < DateOnly.FromDateTime(DateTime.Today);
                    kq.DsViec.Add(new ViecDto
                    {
                        TieuDe = "Tự đánh giá " + kyMo.Ten,
                        MoTa = kyMo.HanNop != null ? "Hạn nộp " + kyMo.HanNop.Value.ToString("dd/MM/yyyy") : "Chưa gửi bản tự đánh giá",
                        Url = "/QLCongViec/DanhGia/TuDanhGia",
                        Icon = "fa-star-half-o",
                        Gap = quaHan
                    });
                }
            }

            var choKy = await _context.DgPhieuDanhGias.AsNoTracking()
                .Where(x => x.MaNhanVien == maNv && x.Loai == DanhGiaService.LoaiQuanLy && x.BuocKy == DgPhieuDanhGia.BuocChoKy)
                .Select(x => x.IdKy).ToListAsync();
            foreach (var idKy in choKy)
            {
                kq.DsViec.Add(new ViecDto
                {
                    TieuDe = "Ký xác nhận phiếu " + (dsKy.FirstOrDefault(k => k.IdKy == idKy)?.Ten ?? ""),
                    MoTa = "Cấp trên đã duyệt, đang chờ bạn ký",
                    Url = "/QLCongViec/DanhGia/TuDanhGia",
                    Icon = "fa-pencil",
                    Gap = true
                });
            }

            var homNay = DateOnly.FromDateTime(DateTime.Today);
            var quaHanMucTieu = await _context.DgMucTieuPhatTriens.AsNoTracking().CountAsync(x => x.MaNhanVien == maNv && x.NgayXoa == null
                && x.TrangThai == DgMucTieuPhatTrien.DangLam && x.HanHoanThanh != null && x.HanHoanThanh < homNay);
            if (quaHanMucTieu > 0)
            {
                kq.DsViec.Add(new ViecDto
                {
                    TieuDe = quaHanMucTieu + " mục tiêu phát triển quá hạn",
                    Url = "/QLCongViec/DanhGia/KeHoachCuaToi",
                    Icon = "fa-flag-checkered",
                    Gap = true
                });
            }
        }

        private async Task ThemViecQuanLyAsync(TongQuanDto kq, DanhGiaService.KyDto kyMo)
        {
            var choCham = (await _quanLy.DanhSachAsync(kyMo)).Count(x => x.TrangThai == "awaiting_manager");
            if (choCham > 0)
            {
                kq.DsViec.Add(new ViecDto
                {
                    TieuDe = choCham + " nhân viên chờ chấm",
                    MoTa = kyMo.Ten,
                    Url = "/QLCongViec/DanhGia/DanhGiaNhanVien",
                    Icon = "fa-pencil-square-o"
                });
            }
            var biTraVe = await _context.DgPhieuDanhGias.AsNoTracking().CountAsync(x => x.IdKy == kyMo.IdKy
                && x.Loai == DanhGiaService.LoaiQuanLy && x.BuocKy == DgPhieuDanhGia.BuocTuChoi);
            if (biTraVe > 0)
            {
                kq.DsViec.Add(new ViecDto
                {
                    TieuDe = biTraVe + " phiếu cấp trên trả về",
                    MoTa = "Cần chấm lại",
                    Url = "/QLCongViec/DanhGia/DanhGiaNhanVien",
                    Icon = "fa-reply",
                    Gap = true
                });
            }
        }

        private async Task ThemViecCapTrenAsync(TongQuanDto kq)
        {
            var choDuyet = await _context.DgPhieuDanhGias.AsNoTracking().CountAsync(x => x.Loai == DanhGiaService.LoaiQuanLy && x.BuocKy == DgPhieuDanhGia.BuocChoDuyet);
            if (choDuyet > 0)
            {
                kq.DsViec.Add(new ViecDto
                {
                    TieuDe = choDuyet + " phiếu chờ duyệt",
                    Url = "/QLCongViec/DanhGia/DuyetPhieu",
                    Icon = "fa-check-square-o"
                });
            }
        }

        // Kỳ thang cũ không so được với chuẩn 60. Kỳ đang mở chỉ hiện khi đã tới lượt nhân viên ký, giống khung ký xác nhận
        private async Task<DiemDto?> LayDiemAsync(string maNv, List<DanhGiaService.KyDto> dsKy)
        {
            var dsId = dsKy.Where(k => !k.ThangCu).Select(k => k.IdKy).ToList();
            var dsPhieu = await _context.DgPhieuDanhGias.AsNoTracking()
                .Where(x => x.MaNhanVien == maNv && x.Loai == DanhGiaService.LoaiQuanLy && dsId.Contains(x.IdKy) && x.DiemThucTe != null)
                .ToListAsync();
            foreach (var k in dsKy.Where(k => !k.ThangCu))
            {
                var p = dsPhieu.FirstOrDefault(x => x.IdKy == k.IdKy);
                if (p == null) continue;
                var duocXem = p.BuocKy is DgPhieuDanhGia.BuocChoKy or DgPhieuDanhGia.BuocDaKy || (p.BuocKy == null && !k.DangMo);
                if (!duocXem) continue;
                return new DiemDto
                {
                    TenKy = k.Ten, DiemKpi = p.DiemKpi, DiemNangLuc = p.DiemNangLuc, DiemThucTe = p.DiemThucTe,
                    ChenhLech = p.ChenhLech, XepLoai = p.XepLoai
                };
            }
            return null;
        }

        private async Task<GapDto?> LayGapAsync(string maNv, List<DanhGiaService.KyDto> dsKy)
        {
            foreach (var k in dsKy.Where(k => !k.ThangCu))
            {
                var nl = await _nangLuc.LayCuaToiAsync(maNv, k);
                if (nl.TbGap == null) continue;
                return new GapDto
                {
                    TenKy = k.Ten,
                    TbGap = nl.TbGap,
                    XepLoai = nl.XepLoai,
                    DsNhom = nl.TheoNhom.Where(x => x.Gap != null).Select(x => new NhomGapDto { TenNhom = x.TenNhom, Gap = x.Gap }).ToList(),
                    DsYeu = nl.DsNangLuc.Where(x => x.YeuCau > 0 && x.DiemQuanLy != null && x.DiemQuanLy < x.YeuCau)
                        .OrderBy(x => x.DiemQuanLy - x.YeuCau).Take(SoNangLucYeuToiDa)
                        .Select(x => new NangLucYeuDto { Ten = x.Ten, Gap = x.DiemQuanLy!.Value - x.YeuCau }).ToList()
                };
            }
            return null;
        }

        // NguoiDaoTao là chữ tự do dạng "V240298 - Bùi Văn Thiều". Buổi chờ duyệt vẫn hiện vì người đó cần biết lịch dự kiến
        private async Task<List<DaoTaoDto>> LayDaoTaoAsync(string maNv, int? idNguoiDung)
        {
            var homNay = DateOnly.FromDateTime(DateTime.Today);
            var ds = await _context.DaoTaos.AsNoTracking()
                .Where(d => d.NgayDaoTao != null && d.NgayDaoTao >= homNay && (d.TrangThai == "ChoDuyet" || d.TrangThai == "DaDuyet")
                    && ((d.NguoiDaoTao != null && d.NguoiDaoTao.Contains(maNv))
                        || (idNguoiDung != null && _context.DaoTaoNguoiThamGias.Any(t => t.IdDaoTao == d.Id && t.IdNguoiDung == idNguoiDung))))
                .OrderBy(d => d.NgayDaoTao)
                .Take(SoDaoTaoToiDa)
                .Select(d => new DaoTaoDto { Id = d.Id, Ten = d.TenDaoTao, Ngay = d.NgayDaoTao, NguoiDaoTao = d.NguoiDaoTao, TrangThai = d.TrangThai })
                .ToListAsync();
            foreach (var d in ds) d.LaGiangVien = d.NguoiDaoTao?.Contains(maNv) == true;
            return ds;
        }
    }
}
