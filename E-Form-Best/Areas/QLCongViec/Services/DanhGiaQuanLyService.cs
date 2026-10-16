using ClosedXML.Excel;
using E_Form_Best.Context;
using E_Form_Best.Models.ITForm;
using Microsoft.EntityFrameworkCore;

namespace E_Form_Best.Areas.QLCongViec.Services
{
    /// <summary>
    /// Phía quản lý của module Đánh giá (chuyển từ /manager/employees + /manager/evaluation của app evaluation).
    /// Danh sách nhân viên = người đã vào hệ thống đánh giá (có hồ sơ / được giao KPI / có phiếu), trừ người đã
    /// bị "Ngừng đánh giá". Phiếu quản lý là phiếu loại 'manager' riêng, không ghi đè phiếu tự đánh giá.
    /// Trạng thái: chỉ tính "đã gửi" khi NgayNop có giá trị (app cũ đoán theo việc có điểm hay chưa).
    /// </summary>
    public class DanhGiaQuanLyService
    {
        private readonly ITFormContext _context;
        private readonly DanhGiaService _danhGia;

        public DanhGiaQuanLyService(ITFormContext context, DanhGiaService danhGia)
        {
            _context = context;
            _danhGia = danhGia;
        }

        #region DTO

        public class NhanVienTomTat
        {
            public string MaNv { get; set; } = "";
            public string HoTen { get; set; } = "";
            public string? PhongBan { get; set; }
            public string CapBac { get; set; } = DanhGiaService.CapBacMacDinh;
            public bool CoHoSo { get; set; }
            public Guid? IdNhomCongViec { get; set; }
            public string? TenNhomCongViec { get; set; }
            public int SoKpi { get; set; }
            public bool DaTuDanhGia { get; set; }
            public bool DaQuanLyDanhGia { get; set; }
            // completed | awaiting_manager | awaiting_self | not_started
            public string TrangThai { get; set; } = "not_started";
            public decimal? DiemKpi { get; set; }
            public decimal? DiemNangLuc { get; set; }
            public decimal? ChenhLech { get; set; }
            public string? XepLoai { get; set; }
            public DateTime? NgayCapNhat { get; set; }
        }

        public class KpiQuanLyDto
        {
            public Guid IdKpiMau { get; set; }
            public string CongViec { get; set; } = "";
            public string? ChiSoKpi { get; set; }
            public string? KetQua { get; set; }
            public decimal? DiemTuDanhGia { get; set; }
            public bool CoAnh { get; set; }
            public bool LaAnh { get; set; }
            public decimal TrongSo { get; set; }
            public decimal? DiemQuanLy { get; set; }
        }

        public class DiemQuanLyVao
        {
            public Guid IdNangLuc { get; set; }
            public decimal Diem { get; set; }
        }

        public class KpiQuanLyVao
        {
            public Guid IdKpiMau { get; set; }
            public decimal TrongSo { get; set; }
            public decimal? DiemQuanLy { get; set; }
        }

        public class LuuDanhGiaVao
        {
            public string? MaNv { get; set; }
            public string? CapBac { get; set; }
            public Guid? IdNhomCongViec { get; set; }
            public List<DiemQuanLyVao>? DsNangLuc { get; set; }
            public List<KpiQuanLyVao>? DsKpi { get; set; }
            public bool HoanThanh { get; set; }
        }

        public class LuuHoSoVao
        {
            public string? MaNv { get; set; }
            public string? CapBac { get; set; }
            public Guid? IdNhomCongViec { get; set; }
            public List<Guid>? DsKpiGiao { get; set; }
        }

        #endregion

        #region Danh sách nhân viên

        /// <summary>Mã NV đang trong hệ thống đánh giá (chưa bị ngừng).</summary>
        private async Task<List<string>> DsMaNvAsync()
        {
            var ngung = await _context.DgHoSoNhanViens.Where(x => x.NgayNgung != null).Select(x => x.MaNhanVien).ToListAsync();
            var ds = await _context.DgHoSoNhanViens.Where(x => x.NgayNgung == null).Select(x => x.MaNhanVien)
                .Union(_context.DgKpiPhanCongs.Where(x => x.TrangThai == "active").Select(x => x.MaNhanVien))
                .Union(_context.DgPhieuDanhGias.Select(x => x.MaNhanVien))
                .ToListAsync();
            return ds.Except(ngung, StringComparer.OrdinalIgnoreCase).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        }

        public async Task<bool> TrongHeThongAsync(string maNv)
            => (await DsMaNvAsync()).Contains(maNv, StringComparer.OrdinalIgnoreCase);

        public async Task<List<NhanVienTomTat>> DanhSachAsync()
        {
            var dsMa = await DsMaNvAsync();
            if (dsMa.Count == 0) return new();

            var nguoiDung = await _context.Users.AsNoTracking()
                .Where(u => u.Tk != null && dsMa.Contains(u.Tk))
                .Select(u => new { u.Tk, u.HoTen, u.PhongBan })
                .ToListAsync();
            var hoSo = await _context.DgHoSoNhanViens.AsNoTracking().Where(x => dsMa.Contains(x.MaNhanVien)).ToListAsync();
            var nhomCv = await _context.DgNhomCongViecs.AsNoTracking().ToDictionaryAsync(x => x.IdNhomCongViec, x => x.TenNhom);
            var phieu = await _context.DgPhieuDanhGias.AsNoTracking().Where(x => dsMa.Contains(x.MaNhanVien)).ToListAsync();
            var soKpi = await _context.DgKpiPhanCongs.AsNoTracking()
                .Where(x => x.TrangThai == "active" && dsMa.Contains(x.MaNhanVien))
                .GroupBy(x => x.MaNhanVien).Select(g => new { g.Key, SoLuong = g.Count() })
                .ToDictionaryAsync(x => x.Key, x => x.SoLuong, StringComparer.OrdinalIgnoreCase);

            return dsMa.Select(ma =>
            {
                var u = nguoiDung.FirstOrDefault(x => string.Equals(x.Tk, ma, StringComparison.OrdinalIgnoreCase));
                var hs = hoSo.FirstOrDefault(x => string.Equals(x.MaNhanVien, ma, StringComparison.OrdinalIgnoreCase));
                var tu = phieu.FirstOrDefault(x => string.Equals(x.MaNhanVien, ma, StringComparison.OrdinalIgnoreCase) && x.Loai == DanhGiaService.LoaiTuDanhGia);
                var ql = phieu.FirstOrDefault(x => string.Equals(x.MaNhanVien, ma, StringComparison.OrdinalIgnoreCase) && x.Loai == DanhGiaService.LoaiQuanLy);
                bool daTu = tu?.NgayNop != null, daQl = ql?.NgayNop != null;
                // Số liệu hiển thị: ưu tiên phiếu quản lý (kết quả chính thức), chưa có thì phiếu tự đánh giá
                var hien = ql?.NgayNop != null ? ql : (tu ?? ql);
                return new NhanVienTomTat
                {
                    MaNv = ma,
                    HoTen = u?.HoTen ?? ma,
                    PhongBan = u?.PhongBan,
                    CapBac = string.IsNullOrWhiteSpace(hs?.CapBac) ? DanhGiaService.CapBacMacDinh : hs!.CapBac!,
                    CoHoSo = !string.IsNullOrWhiteSpace(hs?.CapBac),
                    IdNhomCongViec = hs?.IdNhomCongViec,
                    TenNhomCongViec = hs?.IdNhomCongViec != null && nhomCv.TryGetValue(hs.IdNhomCongViec.Value, out var ten) ? ten : null,
                    SoKpi = soKpi.TryGetValue(ma, out var n) ? n : 0,
                    DaTuDanhGia = daTu,
                    DaQuanLyDanhGia = daQl,
                    TrangThai = daTu && daQl ? "completed" : daTu ? "awaiting_manager" : daQl ? "awaiting_self" : "not_started",
                    DiemKpi = hien?.DiemKpi,
                    DiemNangLuc = hien?.DiemNangLuc,
                    ChenhLech = hien?.ChenhLech,
                    XepLoai = hien?.XepLoai,
                    NgayCapNhat = new[] { tu?.NgayCapNhat, ql?.NgayCapNhat }.Max()
                };
            }).OrderBy(x => x.MaNv).ToList();
        }

        #endregion

        #region Hồ sơ + giao KPI

        public async Task<string?> TenNguoiDungAsync(string maNv)
            => await _context.Users.AsNoTracking().Where(u => u.Tk == maNv).Select(u => u.HoTen).FirstOrDefaultAsync();

        /// <summary>Thêm người vào hệ thống đánh giá (hoặc mở lại người đã ngừng). Trả lỗi hoặc null.</summary>
        public async Task<string?> ThemNhanVienAsync(string maNv, string nguoiThucHien)
        {
            if (!await _context.Users.AnyAsync(u => u.Tk == maNv))
                return $"Không tìm thấy tài khoản E-Form có mã {maNv}.";

            var hs = await _context.DgHoSoNhanViens.FirstOrDefaultAsync(x => x.MaNhanVien == maNv);
            if (hs == null)
            {
                _context.DgHoSoNhanViens.Add(new DgHoSoNhanVien
                {
                    MaNhanVien = maNv,
                    CapBac = DanhGiaService.CapBacMacDinh,
                    NgayCapNhat = DateTime.Now,
                    NguoiCapNhat = nguoiThucHien
                });
            }
            else if (hs.NgayNgung != null)
            {
                hs.NgayNgung = null;
                hs.NguoiNgung = null;
                hs.NgayCapNhat = DateTime.Now;
                hs.NguoiCapNhat = nguoiThucHien;
            }
            else return $"{maNv} đã có trong danh sách đánh giá.";

            await _context.SaveChangesAsync();
            return null;
        }

        public async Task NgungDanhGiaAsync(string maNv, string nguoiThucHien)
        {
            var hs = await _context.DgHoSoNhanViens.FirstOrDefaultAsync(x => x.MaNhanVien == maNv);
            if (hs == null)
            {
                hs = new DgHoSoNhanVien { MaNhanVien = maNv };
                _context.DgHoSoNhanViens.Add(hs);
            }
            hs.NgayNgung = DateTime.Now;
            hs.NguoiNgung = nguoiThucHien;
            await _context.SaveChangesAsync();
        }

        public async Task<object> LayHoSoSuaAsync(string maNv)
        {
            var hs = await _danhGia.LayHoSoAsync(maNv);
            var kpiGiao = await _context.DgKpiPhanCongs.AsNoTracking()
                .Where(x => x.MaNhanVien == maNv && x.TrangThai == "active")
                .Select(x => x.IdKpiMau).ToListAsync();
            var dsKpiMau = await _context.DgKpiMaus.AsNoTracking()
                .Where(x => x.TrangThai == "active")
                .OrderBy(x => x.ThuTu).ThenBy(x => x.CongViec)
                .Select(x => new { id = x.IdKpiMau, idNhom = x.IdNhomCongViec, congViec = x.CongViec, kpiGoiY = x.KpiGoiY })
                .ToListAsync();
            return new
            {
                maNv,
                hoTen = await TenNguoiDungAsync(maNv) ?? maNv,
                capBac = hs.CapBac,
                idNhomCongViec = hs.IdNhomCongViec,
                dsKpiGiao = kpiGiao,
                dsKpiMau,
                dsCapBac = await _danhGia.DsCapBacAsync(),
                dsNhomCongViec = (await _danhGia.DsNhomCongViecAsync()).Select(x => new { id = x.IdNhomCongViec, ten = x.TenNhom })
            };
        }

        /// <summary>
        /// Lưu cấp bậc / nhóm công việc + bộ KPI được giao. Bỏ giao = chuyển trang_thai 'inactive' (không xoá,
        /// phiếu cũ vẫn tham chiếu KPI mẫu); giao lại thì bật dòng cũ lên — UNIQUE (ma_nhan_vien, id_kpi_mau).
        /// </summary>
        public async Task<string?> LuuHoSoAsync(LuuHoSoVao vao, string nguoiThucHien)
        {
            var maNv = vao.MaNv!.Trim();
            var capBac = string.IsNullOrWhiteSpace(vao.CapBac) ? null : vao.CapBac.Trim();
            if (capBac != null && capBac.Length > 10) return "Cấp bậc tối đa 10 ký tự.";
            if (vao.IdNhomCongViec != null && !await _context.DgNhomCongViecs.AnyAsync(x => x.IdNhomCongViec == vao.IdNhomCongViec))
                return "Nhóm công việc không tồn tại.";

            var kpiMoi = (vao.DsKpiGiao ?? new()).Distinct().ToList();
            var kpiHopLe = await _context.DgKpiMaus.Where(x => kpiMoi.Contains(x.IdKpiMau) && x.TrangThai == "active").Select(x => x.IdKpiMau).ToListAsync();
            if (kpiHopLe.Count != kpiMoi.Count) return "Có KPI mẫu không tồn tại hoặc đã ngừng dùng.";

            var bayGio = DateTime.Now;
            await using var tx = await _context.Database.BeginTransactionAsync();

            var hs = await _context.DgHoSoNhanViens.FirstOrDefaultAsync(x => x.MaNhanVien == maNv);
            if (hs == null)
            {
                hs = new DgHoSoNhanVien { MaNhanVien = maNv };
                _context.DgHoSoNhanViens.Add(hs);
            }
            hs.CapBac = capBac;
            hs.IdNhomCongViec = vao.IdNhomCongViec;
            hs.NgayCapNhat = bayGio;
            hs.NguoiCapNhat = nguoiThucHien;

            var dsCu = await _context.DgKpiPhanCongs.Where(x => x.MaNhanVien == maNv).ToListAsync();
            foreach (var pc in dsCu)
                pc.TrangThai = kpiMoi.Contains(pc.IdKpiMau) ? "active" : "inactive";
            foreach (var id in kpiMoi.Where(id => dsCu.All(x => x.IdKpiMau != id)))
            {
                _context.DgKpiPhanCongs.Add(new DgKpiPhanCong
                {
                    IdPhanCong = Guid.NewGuid(),
                    MaNhanVien = maNv,
                    IdKpiMau = id,
                    NguoiGiao = nguoiThucHien,
                    NgayGiao = bayGio,
                    TrangThai = "active"
                });
            }

            await _context.SaveChangesAsync();
            await tx.CommitAsync();
            return null;
        }

        #endregion

        #region Đánh giá nhân viên

        public async Task<object> DuLieuDanhGiaAsync(string maNv)
        {
            var hoSo = await _danhGia.LayHoSoAsync(maNv);
            var tu = await _context.DgPhieuDanhGias.AsNoTracking().FirstOrDefaultAsync(x => x.MaNhanVien == maNv && x.Loai == DanhGiaService.LoaiTuDanhGia);
            var ql = await _context.DgPhieuDanhGias.AsNoTracking().FirstOrDefaultAsync(x => x.MaNhanVien == maNv && x.Loai == DanhGiaService.LoaiQuanLy);

            // Bộ lọc năng lực: theo phiếu quản lý đã lưu → hồ sơ
            var capBac = ql?.CapBac ?? hoSo.CapBac;
            var idNhom = ql != null ? ql.IdNhomCongViec : hoSo.IdNhomCongViec;

            var idPhieu = new[] { tu?.IdPhieu, ql?.IdPhieu }.Where(x => x != null).Select(x => x!.Value).ToList();
            var diem = await _context.DgPhieuNangLucs.AsNoTracking()
                .Where(x => idPhieu.Contains(x.IdPhieu))
                .Select(x => new { x.IdPhieu, x.IdNangLuc, x.DiemThucTe })
                .ToListAsync();

            return new
            {
                maNv,
                hoTen = await TenNguoiDungAsync(maNv) ?? maNv,
                phongBan = await _context.Users.AsNoTracking().Where(u => u.Tk == maNv).Select(u => u.PhongBan).FirstOrDefaultAsync(),
                hoSo,
                dsCapBac = await _danhGia.DsCapBacAsync(),
                dsNhomCongViec = (await _danhGia.DsNhomCongViecAsync()).Select(x => new { id = x.IdNhomCongViec, ten = x.TenNhom }),
                boLoc = new { capBac, idNhomCongViec = idNhom },
                phieuTu = TomTatPhieu(tu),
                phieuQuanLy = TomTatPhieu(ql),
                diemTu = diem.Where(x => x.IdPhieu == tu?.IdPhieu).ToDictionary(x => x.IdNangLuc, x => x.DiemThucTe),
                diemQuanLy = diem.Where(x => x.IdPhieu == ql?.IdPhieu).ToDictionary(x => x.IdNangLuc, x => x.DiemThucTe),
                dsKpi = await LayKpiQuanLyAsync(maNv, tu?.IdPhieu, ql?.IdPhieu),
                dsNhomNangLuc = await _danhGia.LayNangLucAsync(capBac, idNhom)
            };
        }

        private static object? TomTatPhieu(DgPhieuDanhGia? p) => p == null ? null : new
        {
            p.TrangThai, p.CapBac, p.DiemKpi, p.DiemNangLuc, p.TongYeuCau, p.DiemThucTe, p.ChenhLech, p.XepLoai,
            p.NgayNop, p.NgayCapNhat, p.NguoiDanhGia
        };

        private async Task<List<KpiQuanLyDto>> LayKpiQuanLyAsync(string maNv, Guid? idPhieuTu, Guid? idPhieuQl)
        {
            var dsMau = await (
                from pc in _context.DgKpiPhanCongs
                join m in _context.DgKpiMaus on pc.IdKpiMau equals m.IdKpiMau
                where pc.MaNhanVien == maNv && pc.TrangThai == "active" && m.TrangThai == "active"
                orderby m.ThuTu, m.CongViec
                select new { m.IdKpiMau, m.CongViec, m.TrongSoBoPhan }
            ).AsNoTracking().ToListAsync();

            var ids = new[] { idPhieuTu, idPhieuQl }.Where(x => x != null).Select(x => x!.Value).ToList();
            var dong = await _context.DgPhieuKpis.AsNoTracking().Where(x => ids.Contains(x.IdPhieu)).ToListAsync();

            return dsMau.Select(m =>
            {
                var tu = dong.FirstOrDefault(x => x.IdPhieu == idPhieuTu && x.IdKpiMau == m.IdKpiMau);
                var ql = dong.FirstOrDefault(x => x.IdPhieu == idPhieuQl && x.IdKpiMau == m.IdKpiMau);
                return new KpiQuanLyDto
                {
                    IdKpiMau = m.IdKpiMau,
                    CongViec = m.CongViec,
                    ChiSoKpi = tu?.ChiSoKpi,
                    KetQua = tu?.KetQua,
                    DiemTuDanhGia = tu?.DiemTuDanhGia,
                    CoAnh = !string.IsNullOrEmpty(tu?.AnhMinhChung),
                    LaAnh = DanhGiaService.LaFileAnh(tu?.AnhMinhChung),
                    TrongSo = ql?.TrongSo ?? tu?.TrongSo ?? m.TrongSoBoPhan ?? 0,
                    DiemQuanLy = ql?.DiemQuanLy
                };
            }).ToList();
        }

        /// <summary>
        /// Lưu phiếu quản lý. Trọng số KPI chép sang phiếu tự đánh giá (trọng số do quản lý quyết, nhân viên
        /// chỉ xem) rồi tính lại điểm KPI phiếu tự đánh giá. Hoàn thành khi nhân viên đã gửi → khoá cả 2 phiếu.
        /// </summary>
        public async Task<string?> LuuDanhGiaAsync(LuuDanhGiaVao vao, string maQuanLy)
        {
            var maNv = vao.MaNv!.Trim();
            var capBac = string.IsNullOrWhiteSpace(vao.CapBac) ? DanhGiaService.CapBacMacDinh : vao.CapBac.Trim();
            if (vao.IdNhomCongViec != null && !await _context.DgNhomCongViecs.AnyAsync(x => x.IdNhomCongViec == vao.IdNhomCongViec))
                return "Nhóm công việc không tồn tại.";

            var dsNangLuc = (await _danhGia.LayNangLucAsync(capBac, vao.IdNhomCongViec)).SelectMany(x => x.DsNangLuc).ToDictionary(x => x.IdNangLuc);
            if (dsNangLuc.Count == 0) return $"Chưa có ma trận năng lực cho cấp bậc {capBac}.";

            var diemVao = (vao.DsNangLuc ?? new()).Where(x => dsNangLuc.ContainsKey(x.IdNangLuc))
                .GroupBy(x => x.IdNangLuc).Select(g => g.Last()).ToList();
            if (diemVao.Any(x => x.Diem < 0 || x.Diem > DanhGiaService.DiemNangLucToiDa))
                return $"Điểm năng lực phải trong khoảng 0–{DanhGiaService.DiemNangLucToiDa:0}.";

            var kpiGiao = await _context.DgKpiPhanCongs
                .Where(x => x.MaNhanVien == maNv && x.TrangThai == "active")
                .Join(_context.DgKpiMaus.Where(m => m.TrangThai == "active"), pc => pc.IdKpiMau, m => m.IdKpiMau, (pc, m) => new { m.IdKpiMau, m.CongViec })
                .ToDictionaryAsync(x => x.IdKpiMau, x => x.CongViec);
            var kpiVao = (vao.DsKpi ?? new()).Where(x => kpiGiao.ContainsKey(x.IdKpiMau))
                .GroupBy(x => x.IdKpiMau).Select(g => g.Last()).ToList();
            if (kpiVao.Any(x => x.TrongSo < 0 || x.TrongSo > 100)) return "Trọng số mỗi KPI phải trong khoảng 0–100.";
            if (kpiVao.Sum(x => x.TrongSo) > 100) return "Tổng trọng số KPI vượt quá 100.";
            if (kpiVao.Any(x => x.DiemQuanLy is < 0 or > DanhGiaService.DiemKpiToiDa))
                return $"Điểm quản lý đánh giá KPI phải trong khoảng 0–{DanhGiaService.DiemKpiToiDa:0}%.";

            var phieuQl = await _danhGia.LayHoacTaoPhieuAsync(maNv, DanhGiaService.LoaiQuanLy);
            var phieuTu = await _context.DgPhieuDanhGias.FirstOrDefaultAsync(x => x.MaNhanVien == maNv && x.Loai == DanhGiaService.LoaiTuDanhGia);
            var bayGio = DateTime.Now;

            await using var tx = await _context.Database.BeginTransactionAsync();

            await _context.DgPhieuNangLucs.Where(x => x.IdPhieu == phieuQl.IdPhieu).ExecuteDeleteAsync();
            foreach (var d in diemVao)
            {
                var yc = dsNangLuc[d.IdNangLuc].DiemYeuCau;
                _context.DgPhieuNangLucs.Add(new DgPhieuNangLuc
                {
                    IdPhieuNangLuc = Guid.NewGuid(),
                    IdPhieu = phieuQl.IdPhieu,
                    IdNangLuc = d.IdNangLuc,
                    DiemThucTe = Math.Round(d.Diem, 2),
                    DiemYeuCau = yc,
                    ChenhLech = Math.Round(d.Diem - yc, 2)
                });
            }

            var kpiQl = await _context.DgPhieuKpis.Where(x => x.IdPhieu == phieuQl.IdPhieu).ToDictionaryAsync(x => x.IdKpiMau);
            var kpiTu = phieuTu == null ? new Dictionary<Guid, DgPhieuKpi>()
                : await _context.DgPhieuKpis.Where(x => x.IdPhieu == phieuTu.IdPhieu).ToDictionaryAsync(x => x.IdKpiMau);
            foreach (var k in kpiVao)
            {
                if (!kpiQl.TryGetValue(k.IdKpiMau, out var dong))
                {
                    dong = new DgPhieuKpi
                    {
                        IdPhieuKpi = Guid.NewGuid(),
                        IdPhieu = phieuQl.IdPhieu,
                        IdKpiMau = k.IdKpiMau,
                        CongViec = kpiGiao[k.IdKpiMau],
                        NgayTao = bayGio
                    };
                    _context.DgPhieuKpis.Add(dong);
                    kpiQl[k.IdKpiMau] = dong;
                }
                dong.TrongSo = k.TrongSo;
                dong.DiemQuanLy = k.DiemQuanLy;
                dong.NgayCapNhat = bayGio;

                if (kpiTu.TryGetValue(k.IdKpiMau, out var dongTu))
                {
                    dongTu.TrongSo = k.TrongSo;
                    dongTu.DiemQuanLy = k.DiemQuanLy;
                    dongTu.NgayCapNhat = bayGio;
                }
            }

            decimal diemNangLuc = diemVao.Sum(x => x.Diem);
            decimal diemKpi = kpiQl.Values.Sum(x => x.TrongSo * (x.DiemQuanLy ?? 0) / 100m);
            decimal tongYeuCau = dsNangLuc.Values.Sum(x => x.DiemYeuCau);
            decimal thucTe = diemKpi + diemNangLuc;

            phieuQl.CapBac = capBac;
            phieuQl.IdNhomCongViec = vao.IdNhomCongViec;
            phieuQl.DiemNangLuc = diemNangLuc;
            phieuQl.DiemKpi = Math.Round(diemKpi, 4);
            phieuQl.TongYeuCau = tongYeuCau;
            phieuQl.DiemThucTe = Math.Round(thucTe, 4);
            phieuQl.ChenhLech = Math.Round(thucTe - tongYeuCau, 4);
            phieuQl.XepLoai = DanhGiaService.XepLoai(thucTe - tongYeuCau);
            phieuQl.NguoiDanhGia = maQuanLy;
            phieuQl.NgayCapNhat = bayGio;
            if (vao.HoanThanh) phieuQl.NgayNop = bayGio;

            if (phieuTu != null)
            {
                // Trọng số đổi → điểm KPI và tổng của phiếu tự đánh giá đổi theo
                var kpiTuMoi = kpiTu.Values.Sum(x => x.TrongSo * (x.DiemTuDanhGia ?? 0) / 100m);
                var thucTeTu = kpiTuMoi + (phieuTu.DiemNangLuc ?? 0);
                phieuTu.DiemKpi = Math.Round(kpiTuMoi, 4);
                phieuTu.DiemThucTe = Math.Round(thucTeTu, 4);
                if (phieuTu.TongYeuCau != null)
                {
                    phieuTu.ChenhLech = Math.Round(thucTeTu - phieuTu.TongYeuCau.Value, 4);
                    phieuTu.XepLoai = DanhGiaService.XepLoai(thucTeTu - phieuTu.TongYeuCau.Value);
                }
            }

            // Cả hai đã gửi → hoàn tất, khoá phiếu tự đánh giá (nhân viên không sửa sau khi quản lý chốt)
            bool hoanTat = phieuQl.NgayNop != null && phieuTu?.NgayNop != null;
            phieuQl.TrangThai = hoanTat ? "completed" : "pending";
            if (phieuTu != null) phieuTu.TrangThai = hoanTat ? "completed" : "pending";

            await _context.SaveChangesAsync();
            await tx.CommitAsync();
            return null;
        }

        /// <summary>Mở lại phiếu đã hoàn tất để nhân viên sửa tự đánh giá (bỏ ngày gửi của quản lý).</summary>
        public async Task MoLaiAsync(string maNv)
        {
            var ds = await _context.DgPhieuDanhGias.Where(x => x.MaNhanVien == maNv).ToListAsync();
            foreach (var p in ds)
            {
                p.TrangThai = "pending";
                if (p.Loai == DanhGiaService.LoaiQuanLy) p.NgayNop = null;
                p.NgayCapNhat = DateTime.Now;
            }
            await _context.SaveChangesAsync();
        }

        #endregion

        #region Excel

        public async Task<byte[]> XuatExcelAsync(string maNv)
        {
            var hoTen = await TenNguoiDungAsync(maNv) ?? maNv;
            var hoSo = await _danhGia.LayHoSoAsync(maNv);
            var tu = await _context.DgPhieuDanhGias.AsNoTracking().FirstOrDefaultAsync(x => x.MaNhanVien == maNv && x.Loai == DanhGiaService.LoaiTuDanhGia);
            var ql = await _context.DgPhieuDanhGias.AsNoTracking().FirstOrDefaultAsync(x => x.MaNhanVien == maNv && x.Loai == DanhGiaService.LoaiQuanLy);
            var capBac = ql?.CapBac ?? tu?.CapBac ?? hoSo.CapBac;
            var idNhom = ql != null ? ql.IdNhomCongViec : tu != null ? tu.IdNhomCongViec : hoSo.IdNhomCongViec;
            var dsNhom = await _danhGia.LayNangLucAsync(capBac, idNhom);
            var ids = new[] { tu?.IdPhieu, ql?.IdPhieu }.Where(x => x != null).Select(x => x!.Value).ToList();
            var diem = await _context.DgPhieuNangLucs.AsNoTracking().Where(x => ids.Contains(x.IdPhieu)).ToListAsync();
            var dsKpi = await LayKpiQuanLyAsync(maNv, tu?.IdPhieu, ql?.IdPhieu);

            using var wb = new XLWorkbook();
            var ws = wb.Worksheets.Add("Danh gia");
            int r = 1;
            ws.Cell(r, 1).Value = "PHIẾU ĐÁNH GIÁ HIỆU SUẤT";
            ws.Range(r, 1, r, 6).Merge().Style.Font.SetBold().Font.SetFontSize(14).Alignment.SetHorizontal(XLAlignmentHorizontalValues.Center);
            r += 2;
            void Dong(string nhan, object? giaTri)
            {
                ws.Cell(r, 1).Value = nhan;
                ws.Cell(r, 1).Style.Font.SetBold();
                ws.Cell(r, 2).Value = giaTri?.ToString() ?? "";
                r++;
            }
            Dong("Mã nhân viên", maNv);
            Dong("Họ tên", hoTen);
            Dong("Cấp bậc", capBac);
            Dong("Nhóm công việc", hoSo.TenNhomCongViec ?? "—");
            Dong("Trạng thái", ql?.TrangThai == "completed" ? "Hoàn thành" : "Chưa hoàn thành (số liệu tạm tính)");
            Dong("Ngày xuất", DateTime.Now.ToString("dd/MM/yyyy HH:mm"));
            r++;

            void TieuDe(params string[] cot)
            {
                for (int i = 0; i < cot.Length; i++) ws.Cell(r, i + 1).Value = cot[i];
                ws.Range(r, 1, r, cot.Length).Style.Font.SetBold().Fill.SetBackgroundColor(XLColor.FromHtml("#DCEFF7"))
                    .Border.SetOutsideBorder(XLBorderStyleValues.Thin);
                r++;
            }

            ws.Cell(r++, 1).Value = "I. KPI";
            TieuDe("Công việc", "Chỉ số KPI", "Kết quả", "Trọng số", "Tự đánh giá (%)", "Quản lý (%)");
            foreach (var k in dsKpi)
            {
                ws.Cell(r, 1).Value = k.CongViec;
                ws.Cell(r, 2).Value = k.ChiSoKpi ?? "";
                ws.Cell(r, 3).Value = k.KetQua ?? "";
                ws.Cell(r, 4).Value = k.TrongSo;
                if (k.DiemTuDanhGia != null) ws.Cell(r, 5).Value = k.DiemTuDanhGia.Value;
                if (k.DiemQuanLy != null) ws.Cell(r, 6).Value = k.DiemQuanLy.Value;
                r++;
            }
            if (dsKpi.Count == 0) ws.Cell(r++, 1).Value = "(Chưa được giao KPI)";
            r++;

            ws.Cell(r++, 1).Value = "II. NĂNG LỰC";
            TieuDe("Nhóm", "Năng lực", "Yêu cầu", "Tự đánh giá", "Quản lý", "GAP (QL − YC)");
            foreach (var g in dsNhom)
            foreach (var n in g.DsNangLuc)
            {
                var dTu = diem.FirstOrDefault(x => x.IdPhieu == tu?.IdPhieu && x.IdNangLuc == n.IdNangLuc)?.DiemThucTe;
                var dQl = diem.FirstOrDefault(x => x.IdPhieu == ql?.IdPhieu && x.IdNangLuc == n.IdNangLuc)?.DiemThucTe;
                ws.Cell(r, 1).Value = g.Ten;
                ws.Cell(r, 2).Value = $"{n.Ma} - {n.Ten}";
                ws.Cell(r, 3).Value = n.DiemYeuCau;
                if (dTu != null) ws.Cell(r, 4).Value = dTu.Value;
                if (dQl != null) { ws.Cell(r, 5).Value = dQl.Value; ws.Cell(r, 6).Value = dQl.Value - n.DiemYeuCau; }
                r++;
            }
            r++;

            ws.Cell(r++, 1).Value = "III. TỔNG HỢP";
            TieuDe("", "Điểm KPI", "Điểm năng lực", "Tổng yêu cầu", "Chênh lệch", "Xếp loại");
            foreach (var (nhan, p) in new[] { ("Tự đánh giá", tu), ("Quản lý", ql) })
            {
                ws.Cell(r, 1).Value = nhan;
                if (p != null)
                {
                    ws.Cell(r, 2).Value = p.DiemKpi ?? 0;
                    ws.Cell(r, 3).Value = p.DiemNangLuc ?? 0;
                    ws.Cell(r, 4).Value = p.TongYeuCau ?? 0;
                    ws.Cell(r, 5).Value = p.ChenhLech ?? 0;
                    ws.Cell(r, 6).Value = p.XepLoai ?? "";
                }
                else ws.Cell(r, 2).Value = "(chưa đánh giá)";
                r++;
            }

            ws.Column(1).Width = 32;
            ws.Column(2).Width = 40;
            ws.Column(3).Width = 30;
            ws.Columns(4, 6).Width = 16;
            ws.Columns(1, 3).Style.Alignment.SetWrapText();

            using var ms = new MemoryStream();
            wb.SaveAs(ms);
            return ms.ToArray();
        }

        #endregion
    }
}
