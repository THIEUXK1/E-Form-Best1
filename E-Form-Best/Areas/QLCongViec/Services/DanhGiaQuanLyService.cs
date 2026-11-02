using ClosedXML.Excel;
using E_Form_Best.Context;
using E_Form_Best.Models.ITForm;
using Microsoft.EntityFrameworkCore;

namespace E_Form_Best.Areas.QLCongViec.Services
{
    /// <summary>
    /// Phía quản lý của module Đánh giá (chuyển từ /manager/employees + /manager/evaluation của app evaluation),
    /// chấm theo mẫu "IT_Biểu đánh giá nhân viên 2025.xlsx" (xem DanhGiaService).
    /// Danh sách nhân viên = người đã vào hệ thống đánh giá (có hồ sơ / được giao KPI / có phiếu) + mọi AdminIT mã V/M/Y, trừ người đã
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
            // Lấy từ HRM (HRM_AU2) — null khi mã NV không có trong HRM (vd tài khoản thử nghiệm)
            public string? ChucVu { get; set; }
            public string? CapBacHrm { get; set; }
            public DateOnly? NgayVaoLam { get; set; }
            public bool? DangLamViec { get; set; }
            public string CapBac { get; set; } = DanhGiaService.CapBacMacDinh;
            public bool CoHoSo { get; set; }
            public Guid? IdNhomCongViec { get; set; }
            public string? TenNhomCongViec { get; set; }
            public int SoKpi { get; set; }
            public int SoTieuChi { get; set; }
            public bool DaTuDanhGia { get; set; }
            public bool DaQuanLyDanhGia { get; set; }
            // completed | awaiting_manager | awaiting_self | not_started
            public string TrangThai { get; set; } = "not_started";
            public decimal? DiemKpi { get; set; }
            public decimal? DiemNangLuc { get; set; }
            public decimal? DiemThucTe { get; set; }
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
            public byte? MucTuDanhGia { get; set; }
            public bool CoAnh { get; set; }
            public bool LaAnh { get; set; }
            public decimal TrongSo { get; set; }
            public byte? MucQuanLy { get; set; }
        }

        public class TieuChiQuanLyDto
        {
            public Guid IdTieuChi { get; set; }
            public string Ten { get; set; } = "";
            public string? MoTa { get; set; }
            public decimal DiemToiDa { get; set; }
            // Tóm tắt / ví dụ nhân viên ghi trong phiếu tự đánh giá
            public string? TomTat { get; set; }
            public decimal? DiemTu { get; set; }
            public decimal? DiemQuanLy { get; set; }
        }

        public class KpiQuanLyVao
        {
            public Guid IdKpiMau { get; set; }
            public decimal TrongSo { get; set; }
            public byte? MucQuanLy { get; set; }
        }

        public class LuuDanhGiaVao
        {
            public string? MaNv { get; set; }
            public List<KpiQuanLyVao>? DsKpi { get; set; }
            public List<DanhGiaService.TieuChiVao>? DsTieuChi { get; set; }
            public string? NhanXet { get; set; }
            public bool HoanThanh { get; set; }
        }

        public class TieuChiHoSoVao
        {
            // null = tiêu chí mới
            public Guid? IdTieuChi { get; set; }
            public string? Ten { get; set; }
            public string? MoTa { get; set; }
            public decimal DiemToiDa { get; set; }
        }

        public class LuuHoSoVao
        {
            public string? MaNv { get; set; }
            public string? CapBac { get; set; }
            public Guid? IdNhomCongViec { get; set; }
            public List<Guid>? DsKpiGiao { get; set; }
            // null = không đổi tiêu chí Part 2
            public List<TieuChiHoSoVao>? DsTieuChi { get; set; }
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
                // Người có quyền AdminIT đều là nhân viên được đánh giá; chỉ lấy mã NV thật (đầu V/M/Y),
                // bỏ tài khoản hệ thống/mã số trần. Quyền AdminIT hiện chỉ cấp cá nhân qua User_Quyen.
                .Union(_context.Users
                    .Where(u => u.Tk != null
                        && (u.Tk.StartsWith("V") || u.Tk.StartsWith("M") || u.Tk.StartsWith("Y"))
                        && u.UserQuyens.Any(q => q.IdQuyenNavigation != null && q.IdQuyenNavigation.TenQuyen == "AdminIT"))
                    .Select(u => u.Tk!))
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
            var hrm = await _context.HrmNhanViens.AsNoTracking()
                .Where(x => dsMa.Contains(x.MaNhanVien))
                .Select(x => new { x.MaNhanVien, x.HoTen, x.PhongBan, x.PhongBanEn, x.ChucVu, x.CapBac, x.NgayVaoLam, x.DangLamViec })
                .ToListAsync();
            var hoSo = await _context.DgHoSoNhanViens.AsNoTracking().Where(x => dsMa.Contains(x.MaNhanVien)).ToListAsync();
            var nhomCv = await _context.DgNhomCongViecs.AsNoTracking().ToDictionaryAsync(x => x.IdNhomCongViec, x => x.TenNhom);
            var phieu = await _context.DgPhieuDanhGias.AsNoTracking().Where(x => dsMa.Contains(x.MaNhanVien)).ToListAsync();
            var soKpi = await _context.DgKpiPhanCongs.AsNoTracking()
                .Where(x => x.TrangThai == "active" && dsMa.Contains(x.MaNhanVien))
                .GroupBy(x => x.MaNhanVien).Select(g => new { g.Key, SoLuong = g.Count() })
                .ToDictionaryAsync(x => x.Key, x => x.SoLuong, StringComparer.OrdinalIgnoreCase);
            var soTieuChi = await _context.DgTieuChiNangLucs.AsNoTracking()
                .Where(x => x.TrangThai == "active" && dsMa.Contains(x.MaNhanVien))
                .GroupBy(x => x.MaNhanVien).Select(g => new { g.Key, SoLuong = g.Count() })
                .ToDictionaryAsync(x => x.Key, x => x.SoLuong, StringComparer.OrdinalIgnoreCase);

            return dsMa.Select(ma =>
            {
                var u = nguoiDung.FirstOrDefault(x => string.Equals(x.Tk, ma, StringComparison.OrdinalIgnoreCase));
                var hs = hoSo.FirstOrDefault(x => string.Equals(x.MaNhanVien, ma, StringComparison.OrdinalIgnoreCase));
                var h = hrm.FirstOrDefault(x => string.Equals(x.MaNhanVien, ma, StringComparison.OrdinalIgnoreCase));
                var tu = phieu.FirstOrDefault(x => string.Equals(x.MaNhanVien, ma, StringComparison.OrdinalIgnoreCase) && x.Loai == DanhGiaService.LoaiTuDanhGia);
                var ql = phieu.FirstOrDefault(x => string.Equals(x.MaNhanVien, ma, StringComparison.OrdinalIgnoreCase) && x.Loai == DanhGiaService.LoaiQuanLy);
                bool daTu = tu?.NgayNop != null, daQl = ql?.NgayNop != null;
                // Số liệu hiển thị: ưu tiên phiếu quản lý (kết quả chính thức), chưa có thì phiếu tự đánh giá
                var hien = ql?.NgayNop != null ? ql : (tu ?? ql);
                return new NhanVienTomTat
                {
                    MaNv = ma,
                    HoTen = u?.HoTen ?? h?.HoTen ?? ma,
                    // Phòng ban chuẩn theo HRM; tên tiếng Trung là gốc nên ưu tiên bản tiếng Anh cho dễ đọc
                    PhongBan = h?.PhongBanEn ?? h?.PhongBan ?? u?.PhongBan,
                    ChucVu = h?.ChucVu,
                    CapBacHrm = h?.CapBac,
                    NgayVaoLam = h?.NgayVaoLam,
                    DangLamViec = h?.DangLamViec,
                    CapBac = string.IsNullOrWhiteSpace(hs?.CapBac) ? DanhGiaService.CapBacMacDinh : hs!.CapBac!,
                    CoHoSo = !string.IsNullOrWhiteSpace(hs?.CapBac),
                    IdNhomCongViec = hs?.IdNhomCongViec,
                    TenNhomCongViec = hs?.IdNhomCongViec != null && nhomCv.TryGetValue(hs.IdNhomCongViec.Value, out var ten) ? ten : null,
                    SoKpi = soKpi.TryGetValue(ma, out var n) ? n : 0,
                    SoTieuChi = soTieuChi.TryGetValue(ma, out var t) ? t : 0,
                    DaTuDanhGia = daTu,
                    DaQuanLyDanhGia = daQl,
                    TrangThai = daTu && daQl ? "completed" : daTu ? "awaiting_manager" : daQl ? "awaiting_self" : "not_started",
                    DiemKpi = hien?.DiemKpi,
                    DiemNangLuc = hien?.DiemNangLuc,
                    DiemThucTe = hien?.DiemThucTe,
                    ChenhLech = hien?.ChenhLech,
                    XepLoai = hien?.XepLoai,
                    NgayCapNhat = new[] { tu?.NgayCapNhat, ql?.NgayCapNhat }.Max()
                };
            }).OrderBy(x => x.MaNv).ToList();
        }

        #endregion

        #region Hồ sơ + giao KPI + tiêu chí

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
                dsTieuChi = (await _danhGia.LayTieuChiAsync(maNv, null)).Select(x => new { id = x.IdTieuChi, ten = x.Ten, moTa = x.MoTa, diemToiDa = x.DiemToiDa }),
                dsCapBac = await _danhGia.DsCapBacAsync(),
                dsNhomCongViec = (await _danhGia.DsNhomCongViecAsync()).Select(x => new { id = x.IdNhomCongViec, ten = x.TenNhom })
            };
        }

        /// <summary>
        /// Thêm nhanh cấp bậc: danh sách cấp bậc lấy từ DG_MaTranYeuCau nên cấp bậc mới phải có ma trận
        /// → sao chép điểm yêu cầu từ một cấp bậc có sẵn. Đã tồn tại thì coi là thành công — bấm 2 lần không nhân đôi
        /// (UNIQUE id_nang_luc + cap_bac). Trả (lỗi, đãCó).
        /// </summary>
        public async Task<(string? Loi, bool DaCo)> ThemCapBacAsync(string? capBacVao, string? saoChepTuVao)
        {
            var capBac = capBacVao?.Trim().ToUpperInvariant();
            var saoChepTu = saoChepTuVao?.Trim();
            if (string.IsNullOrEmpty(capBac) || capBac.Length > 10) return ("Nhập mã cấp bậc (tối đa 10 ký tự).", false);
            if (await _context.DgMaTranYeuCaus.AnyAsync(x => x.CapBac == capBac)) return (null, true);
            if (string.IsNullOrEmpty(saoChepTu)) return ("Chọn cấp bậc để sao chép điểm yêu cầu.", false);

            var nguon = await _context.DgMaTranYeuCaus.AsNoTracking()
                .Where(x => x.CapBac == saoChepTu)
                .Select(x => new { x.IdNangLuc, x.DiemYeuCau })
                .ToListAsync();
            if (nguon.Count == 0) return ($"Cấp bậc {saoChepTu} chưa có ma trận để sao chép.", false);

            _context.DgMaTranYeuCaus.AddRange(nguon.Select(x => new DgMaTranYeuCau
            {
                IdMaTran = Guid.NewGuid(), IdNangLuc = x.IdNangLuc, CapBac = capBac, DiemYeuCau = x.DiemYeuCau
            }));
            try
            {
                await _context.SaveChangesAsync();
            }
            catch (DbUpdateException)
            {
                // Người khác vừa thêm cùng mã (vướng UNIQUE) → cấp bậc đã có, không phải lỗi
                _context.ChangeTracker.Clear();
                if (await _context.DgMaTranYeuCaus.AsNoTracking().AnyAsync(x => x.CapBac == capBac)) return (null, true);
                throw;
            }
            return (null, false);
        }

        /// <summary>
        /// Lưu cấp bậc / nhóm công việc + bộ KPI được giao + tiêu chí Part 2. Bỏ giao KPI / bỏ tiêu chí = chuyển
        /// trang_thai 'inactive' (không xoá, phiếu cũ vẫn tham chiếu); giao lại KPI thì bật dòng cũ lên — UNIQUE (ma_nhan_vien, id_kpi_mau).
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

            var tcVao = vao.DsTieuChi;
            if (tcVao != null)
            {
                foreach (var t in tcVao) t.Ten = t.Ten?.Trim();
                if (tcVao.Any(x => string.IsNullOrEmpty(x.Ten))) return "Tiêu chí năng lực phải có tên.";
                if (tcVao.Any(x => x.Ten!.Length > 300)) return "Tên tiêu chí tối đa 300 ký tự.";
                if (tcVao.Any(x => x.DiemToiDa <= 0 || x.DiemToiDa > DanhGiaService.DiemMoiPhan))
                    return $"Điểm tối đa mỗi tiêu chí phải lớn hơn 0 và không quá {DanhGiaService.DiemMoiPhan:0}.";
                if (tcVao.Sum(x => x.DiemToiDa) > DanhGiaService.DiemMoiPhan)
                    return $"Tổng điểm tối đa các tiêu chí năng lực vượt {DanhGiaService.DiemMoiPhan:0}.";
            }

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

            if (tcVao != null)
            {
                // Chỉ sửa tiêu chí của đúng người này — id của người khác gửi lên coi như tiêu chí mới
                var tcCu = await _context.DgTieuChiNangLucs.Where(x => x.MaNhanVien == maNv).ToDictionaryAsync(x => x.IdTieuChi);
                var giuLai = new HashSet<Guid>();
                for (int i = 0; i < tcVao.Count; i++)
                {
                    var t = tcVao[i];
                    if (t.IdTieuChi == null || !tcCu.TryGetValue(t.IdTieuChi.Value, out var dong))
                    {
                        dong = new DgTieuChiNangLuc { IdTieuChi = Guid.NewGuid(), MaNhanVien = maNv, NgayTao = bayGio };
                        _context.DgTieuChiNangLucs.Add(dong);
                    }
                    dong.Ten = t.Ten!;
                    dong.MoTa = DanhGiaService.CatChu(t.MoTa);
                    dong.DiemToiDa = t.DiemToiDa;
                    dong.ThuTu = i + 1;
                    dong.TrangThai = "active";
                    dong.NguoiCapNhat = nguoiThucHien;
                    dong.NgayCapNhat = bayGio;
                    giuLai.Add(dong.IdTieuChi);
                }
                foreach (var dong in tcCu.Values.Where(x => !giuLai.Contains(x.IdTieuChi) && x.TrangThai == "active"))
                {
                    dong.TrangThai = "inactive";
                    dong.NguoiCapNhat = nguoiThucHien;
                    dong.NgayCapNhat = bayGio;
                }
            }

            await _context.SaveChangesAsync();
            await tx.CommitAsync();
            return null;
        }

        #endregion

        #region Đánh giá nhân viên

        public async Task<object> DuLieuDanhGiaAsync(string maNv)
        {
            var tu = await _context.DgPhieuDanhGias.AsNoTracking().FirstOrDefaultAsync(x => x.MaNhanVien == maNv && x.Loai == DanhGiaService.LoaiTuDanhGia);
            var ql = await _context.DgPhieuDanhGias.AsNoTracking().FirstOrDefaultAsync(x => x.MaNhanVien == maNv && x.Loai == DanhGiaService.LoaiQuanLy);
            return new
            {
                maNv,
                thongTin = await _danhGia.LayThongTinAsync(maNv),
                hoSo = await _danhGia.LayHoSoAsync(maNv),
                phieuTu = DanhGiaService.SangDto(tu),
                phieuQuanLy = TomTatPhieuQl(ql, ql?.NguoiDanhGia == null ? null : await TenNguoiDungAsync(ql.NguoiDanhGia)),
                dsKpi = await LayKpiQuanLyAsync(maNv, tu?.IdPhieu, ql?.IdPhieu),
                dsTieuChi = await LayTieuChiQuanLyAsync(maNv, tu?.IdPhieu, ql?.IdPhieu)
            };
        }

        private static object? TomTatPhieuQl(DgPhieuDanhGia? p, string? tenNguoiDanhGia) => p == null ? null : new
        {
            p.TrangThai, p.DiemKpi, p.DiemNangLuc, p.DiemThucTe, p.ChenhLech, p.XepLoai,
            p.NgayNop, p.NgayCapNhat, p.NguoiDanhGia, tenNguoiDanhGia, p.NhanXet
        };

        private async Task<List<KpiQuanLyDto>> LayKpiQuanLyAsync(string maNv, Guid? idPhieuTu, Guid? idPhieuQl)
        {
            var dsMau = await (
                from pc in _context.DgKpiPhanCongs
                join m in _context.DgKpiMaus on pc.IdKpiMau equals m.IdKpiMau
                where pc.MaNhanVien == maNv && pc.TrangThai == "active" && m.TrangThai == "active"
                orderby m.ThuTu, m.CongViec
                select new { m.IdKpiMau, m.CongViec, m.KpiGoiY, m.KetQuaGoiY, m.TrongSoBoPhan }
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
                    // Nhân viên chưa khai thì hiện gợi ý của KPI mẫu (giống trang tự đánh giá điền sẵn)
                    ChiSoKpi = tu != null ? tu.ChiSoKpi : m.KpiGoiY,
                    KetQua = tu != null ? tu.KetQua : m.KetQuaGoiY,
                    MucTuDanhGia = tu?.MucTuDanhGia,
                    CoAnh = !string.IsNullOrEmpty(tu?.AnhMinhChung),
                    LaAnh = DanhGiaService.LaFileAnh(tu?.AnhMinhChung),
                    TrongSo = ql?.TrongSo ?? tu?.TrongSo ?? m.TrongSoBoPhan ?? 0,
                    MucQuanLy = ql?.MucQuanLy
                };
            }).ToList();
        }

        private async Task<List<TieuChiQuanLyDto>> LayTieuChiQuanLyAsync(string maNv, Guid? idPhieuTu, Guid? idPhieuQl)
        {
            var ds = await _danhGia.LayTieuChiAsync(maNv, null);
            var ids = new[] { idPhieuTu, idPhieuQl }.Where(x => x != null).Select(x => x!.Value).ToList();
            var diem = await _context.DgPhieuTieuChis.AsNoTracking().Where(x => ids.Contains(x.IdPhieu)).ToListAsync();
            return ds.Select(t =>
            {
                var tu = diem.FirstOrDefault(x => x.IdPhieu == idPhieuTu && x.IdTieuChi == t.IdTieuChi);
                var ql = diem.FirstOrDefault(x => x.IdPhieu == idPhieuQl && x.IdTieuChi == t.IdTieuChi);
                return new TieuChiQuanLyDto
                {
                    IdTieuChi = t.IdTieuChi, Ten = t.Ten, MoTa = t.MoTa, DiemToiDa = t.DiemToiDa,
                    TomTat = tu?.TomTat, DiemTu = tu?.Diem, DiemQuanLy = ql?.Diem
                };
            }).ToList();
        }

        /// <summary>
        /// Lưu phiếu quản lý. Trọng số KPI chép sang phiếu tự đánh giá (trọng số do quản lý quyết, nhân viên
        /// chỉ xem) rồi tính lại điểm phiếu tự đánh giá. Hoàn thành khi nhân viên đã gửi → khoá cả 2 phiếu.
        /// </summary>
        public async Task<string?> LuuDanhGiaAsync(LuuDanhGiaVao vao, string maQuanLy)
        {
            var maNv = vao.MaNv!.Trim();

            var kpiGiao = await _context.DgKpiPhanCongs
                .Where(x => x.MaNhanVien == maNv && x.TrangThai == "active")
                .Join(_context.DgKpiMaus.Where(m => m.TrangThai == "active"), pc => pc.IdKpiMau, m => m.IdKpiMau, (pc, m) => new { m.IdKpiMau, m.CongViec })
                .ToDictionaryAsync(x => x.IdKpiMau, x => x.CongViec);
            var kpiVao = (vao.DsKpi ?? new()).Where(x => kpiGiao.ContainsKey(x.IdKpiMau))
                .GroupBy(x => x.IdKpiMau).Select(g => g.Last()).ToList();
            if (kpiVao.Any(x => x.TrongSo < 0 || x.TrongSo > DanhGiaService.DiemMoiPhan))
                return $"Trọng số mỗi nhiệm vụ phải trong khoảng 0–{DanhGiaService.DiemMoiPhan:0}.";
            var tongTrongSo = kpiVao.Sum(x => x.TrongSo);
            if (tongTrongSo > DanhGiaService.DiemMoiPhan) return $"Tổng trọng số Part 1 vượt quá {DanhGiaService.DiemMoiPhan:0}.";
            if (kpiVao.Any(x => x.MucQuanLy is < 1 or > DanhGiaService.MucToiDa))
                return $"Mức đánh giá KPI phải từ 1 đến {DanhGiaService.MucToiDa}.";

            var dsTieuChi = await _context.DgTieuChiNangLucs
                .Where(x => x.MaNhanVien == maNv && x.TrangThai == "active")
                .ToDictionaryAsync(x => x.IdTieuChi);
            var tcVao = (vao.DsTieuChi ?? new()).Where(x => dsTieuChi.ContainsKey(x.IdTieuChi))
                .GroupBy(x => x.IdTieuChi).Select(g => g.Last()).ToList();
            var tcSai = tcVao.FirstOrDefault(x => x.Diem is { } d && (d < 0 || d > dsTieuChi[x.IdTieuChi].DiemToiDa));
            if (tcSai != null)
                return $"Điểm \"{dsTieuChi[tcSai.IdTieuChi].Ten}\" phải trong khoảng 0–{dsTieuChi[tcSai.IdTieuChi].DiemToiDa:0.##}.";

            if (vao.HoanThanh)
            {
                // Chốt phiếu phải đúng thang mẫu: Part 1 đủ 50 trọng số + đủ mức, Part 2 chấm đủ
                if (kpiGiao.Count == 0) return "Nhân viên chưa được giao nhiệm vụ Part 1 — giao KPI ở “Hồ sơ & KPI”.";
                if (Math.Abs(tongTrongSo - DanhGiaService.DiemMoiPhan) > 0.05m)
                    return $"Tổng trọng số Part 1 phải bằng {DanhGiaService.DiemMoiPhan:0} (hiện {tongTrongSo:0.##}).";
                if (kpiGiao.Keys.Any(id => kpiVao.FirstOrDefault(x => x.IdKpiMau == id)?.MucQuanLy == null))
                    return "Chưa chọn mức đánh giá cho đủ các nhiệm vụ ở Part 1.";
                if (dsTieuChi.Count == 0) return "Nhân viên chưa có tiêu chí năng lực Part 2 — thêm ở “Hồ sơ & KPI”.";
                if (dsTieuChi.Keys.Any(id => tcVao.FirstOrDefault(x => x.IdTieuChi == id)?.Diem == null))
                    return "Chưa chấm đủ các tiêu chí năng lực ở Part 2.";
            }

            var phieuQl = await _danhGia.LayHoacTaoPhieuAsync(maNv, DanhGiaService.LoaiQuanLy);
            var phieuTu = await _context.DgPhieuDanhGias.FirstOrDefaultAsync(x => x.MaNhanVien == maNv && x.Loai == DanhGiaService.LoaiTuDanhGia);
            var bayGio = DateTime.Now;

            await using var tx = await _context.Database.BeginTransactionAsync();

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
                dong.MucQuanLy = k.MucQuanLy;
                dong.NgayCapNhat = bayGio;

                if (kpiTu.TryGetValue(k.IdKpiMau, out var dongTu))
                {
                    dongTu.TrongSo = k.TrongSo;
                    dongTu.MucQuanLy = k.MucQuanLy;
                    dongTu.NgayCapNhat = bayGio;
                }
            }

            var diemNangLuc = await _danhGia.GhiTieuChiAsync(phieuQl.IdPhieu, dsTieuChi, tcVao, false, bayGio);
            var diemKpi = DanhGiaService.DiemKpi(kpiQl.Values.Where(x => kpiGiao.ContainsKey(x.IdKpiMau)).Select(x => (x.TrongSo, x.MucQuanLy)));

            var hoSo = await _danhGia.LayHoSoAsync(maNv);
            phieuQl.CapBac = hoSo.CapBac;
            phieuQl.IdNhomCongViec = hoSo.IdNhomCongViec;
            DanhGiaService.GanDiem(phieuQl, diemKpi, diemNangLuc);
            phieuQl.NhanXet = DanhGiaService.CatChu(vao.NhanXet);
            phieuQl.NguoiDanhGia = maQuanLy;
            phieuQl.NgayCapNhat = bayGio;
            if (vao.HoanThanh) phieuQl.NgayNop = bayGio;

            if (phieuTu != null)
            {
                // Trọng số đổi → Part 1 và tổng của phiếu tự đánh giá đổi theo; Part 2 tự đánh giá giữ nguyên
                var kpiTuMoi = DanhGiaService.DiemKpi(kpiTu.Values.Where(x => kpiGiao.ContainsKey(x.IdKpiMau)).Select(x => (x.TrongSo, x.MucTuDanhGia)));
                DanhGiaService.GanDiem(phieuTu, kpiTuMoi, phieuTu.DiemNangLuc ?? 0);
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

        #region Excel (bố cục theo mẫu IT_Biểu đánh giá nhân viên 2025.xlsx, mỗi nhân viên 1 sheet)

        private const string MoTaMuc = "Level :   5 = Higher above the standard (Excellent)   •   4 = Above the standard (Good)   •   "
            + "3 = As a standard / Often present   •   2 = Below the standard (Poor)   •   1 = (Very Poor) Very Low the circumstance";

        // Độ rộng cột A–H của file mẫu
        private static readonly double[] DoRongCot = { 29.6, 50.9, 51.9, 30.3, 16.8, 9, 14.4, 9.4 };

        public async Task<byte[]> XuatExcelAsync(string maNv)
        {
            using var wb = new XLWorkbook();
            await VeSheetAsync(wb, maNv, new HashSet<string>(StringComparer.OrdinalIgnoreCase));
            return LuuWorkbook(wb);
        }

        /// <summary>Mọi nhân viên đang trong danh sách đánh giá, mỗi người 1 sheet (giống file mẫu 5 sheet).</summary>
        public async Task<byte[]> XuatExcelTatCaAsync()
        {
            var ds = (await DsMaNvAsync()).OrderBy(x => x).ToList();
            using var wb = new XLWorkbook();
            var tenDaDung = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var ma in ds) await VeSheetAsync(wb, ma, tenDaDung);
            if (ds.Count == 0) wb.Worksheets.Add("Trong").Cell(1, 1).Value = "Chưa có nhân viên nào trong danh sách đánh giá.";
            return LuuWorkbook(wb);
        }

        private static byte[] LuuWorkbook(XLWorkbook wb)
        {
            using var ms = new MemoryStream();
            wb.SaveAs(ms);
            return ms.ToArray();
        }

        // Tên sheet như mẫu: tên gọi (từ cuối họ tên), trùng thì thêm mã NV; bỏ ký tự Excel cấm, tối đa 31 ký tự
        private static string TenSheet(string hoTen, string maNv, HashSet<string> daDung)
        {
            static string Sach(string s)
            {
                foreach (var c in new[] { ':', '\\', '/', '?', '*', '[', ']' }) s = s.Replace(c, ' ');
                s = s.Trim();
                return s.Length > 31 ? s[..31] : s;
            }
            var ten = Sach(hoTen.Split(' ', StringSplitOptions.RemoveEmptyEntries).LastOrDefault() ?? maNv);
            if (ten.Length == 0 || daDung.Contains(ten)) ten = Sach($"{ten} {maNv}");
            if (daDung.Contains(ten)) ten = Sach(maNv);
            daDung.Add(ten);
            return ten;
        }

        // ClosedXML không tự giãn dòng cho ô gộp xuống dòng → ước lượng theo số ký tự / độ rộng
        private static double ChieuCao(double toiThieu, params (string? Chu, double DoRong)[] o)
        {
            int dong = 1;
            foreach (var (chu, doRong) in o)
            {
                if (string.IsNullOrEmpty(chu)) continue;
                var moiDong = Math.Max(1, (int)(doRong * 1.15));
                dong = Math.Max(dong, chu.Replace("\r", "").Split('\n').Sum(x => Math.Max(1, (int)Math.Ceiling(x.Length / (double)moiDong))));
            }
            return Math.Min(409, Math.Max(toiThieu, dong * 15.5 + 4));
        }

        private async Task VeSheetAsync(XLWorkbook wb, string maNv, HashSet<string> tenDaDung)
        {
            var tt = await _danhGia.LayThongTinAsync(maNv);
            var tu = await _context.DgPhieuDanhGias.AsNoTracking().FirstOrDefaultAsync(x => x.MaNhanVien == maNv && x.Loai == DanhGiaService.LoaiTuDanhGia);
            var ql = await _context.DgPhieuDanhGias.AsNoTracking().FirstOrDefaultAsync(x => x.MaNhanVien == maNv && x.Loai == DanhGiaService.LoaiQuanLy);
            var dsKpi = await LayKpiQuanLyAsync(maNv, tu?.IdPhieu, ql?.IdPhieu);
            var dsTc = await LayTieuChiQuanLyAsync(maNv, tu?.IdPhieu, ql?.IdPhieu);
            var tenQl = ql?.NguoiDanhGia == null ? null : await TenNguoiDungAsync(ql.NguoiDanhGia) ?? ql.NguoiDanhGia;
            var ngayDanhGia = ql?.NgayNop ?? tu?.NgayNop;

            var ws = wb.Worksheets.Add(TenSheet(tt.HoTen, maNv, tenDaDung));
            ws.Style.Font.FontName = "Times New Roman";
            ws.Style.Font.FontSize = 12;
            ws.Style.Alignment.WrapText = true;
            ws.Style.Alignment.Vertical = XLAlignmentVerticalValues.Center;
            for (int c = 0; c < DoRongCot.Length; c++) ws.Column(c + 1).Width = DoRongCot[c];

            IXLRange Gop(int r1, int c1, int r2, int c2) => ws.Range(r1, c1, r2, c2).Merge();
            void Khung(int r1, int c1, int r2, int c2)
                => ws.Range(r1, c1, r2, c2).Style.Border.SetOutsideBorder(XLBorderStyleValues.Thin).Border.SetInsideBorder(XLBorderStyleValues.Thin);
            void TieuDeBang(IXLRange rg)
                => rg.Style.Font.SetBold().Fill.SetBackgroundColor(XLColor.FromHtml("#D9E1F2")).Alignment.SetHorizontal(XLAlignmentHorizontalValues.Center);

            // --- Đầu phiếu ---
            var nam = (ngayDanhGia ?? DateTime.Now).Year;
            ws.Cell(1, 1).Value = $"{nam} Performance Evaluation Form for Staff, Senior Staff \nĐánh giá hiệu xuất công việc năm {nam}(IT)";
            Gop(1, 1, 1, 8).Style.Font.SetBold().Font.SetFontSize(16).Alignment.SetHorizontal(XLAlignmentHorizontalValues.Center);
            ws.Row(1).Height = 43.8;
            void DauPhieu(int r, string nhan, string? giaTri, string ben)
            {
                ws.Cell(r, 1).Value = nhan;
                ws.Cell(r, 1).Style.Font.SetBold();
                ws.Cell(r, 2).Value = giaTri ?? "";
                ws.Cell(r, 3).Value = ben;
                ws.Cell(r, 3).Style.Font.SetBold();
                ws.Row(r).Height = 21;
            }
            DauPhieu(2, "Name :", tt.HoTen, $"Employee ID : {maNv}");
            DauPhieu(3, "Position : ", tt.ChucVu, $"Section : {tt.PhongBan}");
            DauPhieu(4, "Evaluation Date :", null, $"Department : {tt.PhongBan}");
            if (ngayDanhGia != null)
            {
                ws.Cell(4, 2).Value = ngayDanhGia.Value.Date;
                ws.Cell(4, 2).Style.DateFormat.Format = "dd/MM/yyyy";
                ws.Cell(4, 2).Style.Alignment.SetHorizontal(XLAlignmentHorizontalValues.Left);
            }

            // --- Part 1 ---
            int r = 5;
            ws.Cell(r, 1).Value = "Part 1 : Work Performance Measurement (50 Points)-Hiệu xuất";
            Gop(r, 1, r, 8).Style.Font.SetBold().Font.SetFontSize(13);
            ws.Row(r++).Height = 25.5;
            ws.Cell(r, 1).Value = "Please fill your Key Performance Index, Result, Weight, and Performance Level. Vui lòng điền Chỉ số hiệu suất, Kết quả, Trọng số và Mức hiệu suất của bạn.";
            Gop(r, 1, r, 8);
            ws.Row(r++).Height = 30;
            ws.Cell(r, 1).Value = MoTaMuc;
            Gop(r, 1, r, 8).Style.Font.SetItalic();
            ws.Row(r++).Height = 36;

            int dauBang1 = r;
            ws.Cell(r, 1).Value = "Job Assignment \nNhiệm vụ chính";
            ws.Cell(r, 2).Value = "Key Performance Index\nHiệu xuất";
            ws.Cell(r, 3).Value = "Result\nKết quả";
            ws.Cell(r, 4).Value = "Weight\nTrọng Số";
            ws.Cell(r, 5).Value = "Self-Evaluation";
            ws.Cell(r, 7).Value = "Supervisor Evaluation";
            for (int c = 1; c <= 4; c++) Gop(r, c, r + 1, c);
            Gop(r, 5, r, 6);
            Gop(r, 7, r, 8);
            ws.Cell(r + 1, 5).Value = "Performance Level";
            ws.Cell(r + 1, 6).Value = "Score";
            ws.Cell(r + 1, 7).Value = "Performance Level";
            ws.Cell(r + 1, 8).Value = "Score";
            TieuDeBang(ws.Range(r, 1, r + 1, 8));
            ws.Row(r).Height = 27;
            ws.Row(r + 1).Height = 33;
            r += 2;

            int dauKpi = r;
            foreach (var k in dsKpi)
            {
                ws.Cell(r, 1).Value = k.CongViec;
                ws.Cell(r, 2).Value = k.ChiSoKpi ?? "";
                ws.Cell(r, 3).Value = k.KetQua ?? "";
                ws.Cell(r, 4).Value = k.TrongSo;
                if (k.MucTuDanhGia != null) ws.Cell(r, 5).Value = (int)k.MucTuDanhGia.Value;
                ws.Cell(r, 6).FormulaA1 = $"D{r}*E{r}/5";
                if (k.MucQuanLy != null) ws.Cell(r, 7).Value = (int)k.MucQuanLy.Value;
                ws.Cell(r, 8).FormulaA1 = $"D{r}*G{r}/5";
                ws.Range(r, 1, r, 3).Style.Alignment.SetVertical(XLAlignmentVerticalValues.Top);
                ws.Range(r, 4, r, 8).Style.Alignment.SetHorizontal(XLAlignmentHorizontalValues.Center);
                ws.Row(r).Height = ChieuCao(30, (k.CongViec, DoRongCot[0]), (k.ChiSoKpi, DoRongCot[1]), (k.KetQua, DoRongCot[2]));
                r++;
            }
            if (dsKpi.Count == 0)
            {
                ws.Cell(r, 1).Value = "(Chưa được giao nhiệm vụ / KPI)";
                Gop(r, 1, r, 8).Style.Font.SetItalic();
                r++;
            }
            int cuoiKpi = r - 1;
            ws.Cell(r, 3).Value = "Total";
            ws.Cell(r, 3).Style.Alignment.SetHorizontal(XLAlignmentHorizontalValues.Right);
            if (dsKpi.Count > 0)
            {
                ws.Cell(r, 4).FormulaA1 = $"SUM(D{dauKpi}:D{cuoiKpi})";
                ws.Cell(r, 6).FormulaA1 = $"SUM(F{dauKpi}:F{cuoiKpi})";
                ws.Cell(r, 8).FormulaA1 = $"SUM(H{dauKpi}:H{cuoiKpi})";
            }
            ws.Range(r, 1, r, 8).Style.Font.SetBold();
            ws.Range(r, 4, r, 8).Style.Alignment.SetHorizontal(XLAlignmentHorizontalValues.Center);
            int dongTong1 = r;
            Khung(dauBang1, 1, r, 8);
            r += 2;

            // --- Part 2 ---
            ws.Cell(r, 1).Value = "Part 2 : Competency Measurement (50 Points)-Năng lực";
            Gop(r, 1, r, 8).Style.Font.SetBold().Font.SetFontSize(13);
            ws.Row(r++).Height = 21;
            ws.Cell(r, 1).Value = "Please evaluate the work ability by following criteria below and put a summary or some examples.";
            Gop(r, 1, r, 8);
            ws.Row(r++).Height = 30;
            if (dsTc.Count > 0)
            {
                ws.Cell(r, 1).Value = string.Join("     •     ", dsTc.Select(t => $"{t.Ten} →  {t.DiemToiDa:0.##} điểm"));
                Gop(r, 1, r, 8).Style.Font.SetItalic();
                ws.Row(r++).Height = ChieuCao(24, (ws.Cell(r - 1, 1).GetString(), DoRongCot.Sum()));
            }

            int dauBang2 = r;
            ws.Cell(r, 1).Value = "Job Assignment \nNhiệm vụ chính";
            ws.Cell(r, 3).Value = "Summary and some examples";
            ws.Cell(r, 5).Value = "Self-Evaluation";
            ws.Cell(r, 7).Value = "Supervisor Evaluation";
            Gop(r, 1, r, 2); Gop(r, 3, r, 4); Gop(r, 5, r, 6); Gop(r, 7, r, 8);
            TieuDeBang(ws.Range(r, 1, r, 8));
            ws.Row(r++).Height = 36;

            int dauTc = r;
            foreach (var t in dsTc)
            {
                var tomTat = !string.IsNullOrWhiteSpace(t.TomTat) ? t.TomTat : t.MoTa;
                ws.Cell(r, 1).Value = $"{t.Ten}\n(tối đa {t.DiemToiDa:0.##} điểm)";
                ws.Cell(r, 3).Value = tomTat ?? "";
                if (t.DiemTu != null) ws.Cell(r, 5).Value = t.DiemTu.Value;
                if (t.DiemQuanLy != null) ws.Cell(r, 7).Value = t.DiemQuanLy.Value;
                Gop(r, 1, r, 2); Gop(r, 3, r, 4); Gop(r, 5, r, 6); Gop(r, 7, r, 8);
                ws.Range(r, 1, r, 4).Style.Alignment.SetVertical(XLAlignmentVerticalValues.Top);
                ws.Range(r, 5, r, 8).Style.Alignment.SetHorizontal(XLAlignmentHorizontalValues.Center);
                ws.Row(r).Height = ChieuCao(30, (t.Ten, DoRongCot[0] + DoRongCot[1]), (tomTat, DoRongCot[2] + DoRongCot[3]));
                r++;
            }
            if (dsTc.Count == 0)
            {
                ws.Cell(r, 1).Value = "(Chưa có tiêu chí năng lực)";
                Gop(r, 1, r, 8).Style.Font.SetItalic();
                r++;
            }
            int cuoiTc = r - 1;
            ws.Cell(r, 1).Value = "Total";
            Gop(r, 1, r, 4).Style.Alignment.SetHorizontal(XLAlignmentHorizontalValues.Right);
            if (dsTc.Count > 0)
            {
                ws.Cell(r, 5).FormulaA1 = $"SUM(E{dauTc}:E{cuoiTc})";
                ws.Cell(r, 7).FormulaA1 = $"SUM(G{dauTc}:G{cuoiTc})";
            }
            Gop(r, 5, r, 6); Gop(r, 7, r, 8);
            ws.Range(r, 1, r, 8).Style.Font.SetBold();
            ws.Range(r, 5, r, 8).Style.Alignment.SetHorizontal(XLAlignmentHorizontalValues.Center);
            ws.Row(r).Height = 29.4;
            int dongTong2 = r;
            Khung(dauBang2, 1, r, 8);
            r += 2;

            // --- Summary (theo mẫu: lấy điểm quản lý) ---
            ws.Cell(r, 1).Value = "Summary";
            ws.Cell(r, 2).Value = "Part I";
            ws.Cell(r, 3).Value = "Part II";
            ws.Cell(r, 4).Value = "Total";
            Gop(r, 4, r, 8);
            TieuDeBang(ws.Range(r, 1, r, 8));
            ws.Row(r).Height = 30;
            r++;
            ws.Cell(r, 1).Value = "Score";
            ws.Cell(r, 1).Style.Font.SetBold();
            ws.Cell(r, 2).FormulaA1 = $"H{dongTong1}";
            ws.Cell(r, 3).FormulaA1 = $"G{dongTong2}";
            ws.Cell(r, 4).FormulaA1 = $"B{r}+C{r}";
            Gop(r, 4, r, 8);
            ws.Range(r, 2, r, 8).Style.Font.SetBold().Alignment.SetHorizontal(XLAlignmentHorizontalValues.Center);
            ws.Row(r).Height = 27.6;
            Khung(r - 1, 1, r, 8);
            r++;
            if (!string.IsNullOrEmpty(ql?.XepLoai ?? tu?.XepLoai))
            {
                ws.Cell(r, 1).Value = "Xếp loại";
                ws.Cell(r, 2).Value = ql?.XepLoai ?? tu?.XepLoai;
                ws.Cell(r, 3).Value = ql?.NgayNop != null ? "" : "(tạm tính — quản lý chưa gửi đánh giá)";
                ws.Range(r, 1, r, 2).Style.Font.SetBold();
                r++;
            }
            r++;

            // --- Part 3 ---
            ws.Cell(r, 1).Value = "Part 3 : Career and Improvement Plan for Employee - Kế hoạch phát triển-Thăng tiến";
            Gop(r, 1, r, 6).Style.Font.SetBold().Font.SetFontSize(13);
            ws.Row(r++).Height = 24;
            int dauP3 = r;
            void HaiO(string? trai, string? phai, bool tieuDe)
            {
                ws.Cell(r, 1).Value = trai ?? "";
                ws.Cell(r, 3).Value = phai ?? "";
                Gop(r, 1, r, 2); Gop(r, 3, r, 5);
                if (tieuDe) ws.Range(r, 1, r, 5).Style.Font.SetBold().Fill.SetBackgroundColor(XLColor.FromHtml("#F2F2F2"));
                else ws.Range(r, 1, r, 5).Style.Alignment.SetVertical(XLAlignmentVerticalValues.Top);
                ws.Row(r).Height = tieuDe ? 26.25 : ChieuCao(60, (trai, DoRongCot[0] + DoRongCot[1]), (phai, DoRongCot[2] + DoRongCot[3] + DoRongCot[4]));
                r++;
            }
            HaiO("Strength Điểm mạnh:", "Career path and duration (1 - 3 year) :", true);
            HaiO(tu?.DiemManh, tu?.LoTrinh, false);
            HaiO("Weakness (Need to improve) :", "Development plan (1 year) :", true);
            HaiO(tu?.DiemYeu, tu?.KeHoach, false);
            Khung(dauP3, 1, r - 1, 5);

            ws.Cell(r, 1).Value = $"Evaluated by____{tenQl}______________________";
            ws.Cell(r, 3).Value = "Approved by__________________________";
            Gop(r, 1, r, 2); Gop(r, 3, r, 5);
            ws.Row(r++).Height = 24;
            var ngayQl = ql?.NgayNop;
            ws.Cell(r, 1).Value = "Immediate Supervisor " + (ngayQl != null ? ngayQl.Value.ToString("dd/MM/yyyy") : "______/______/______");
            ws.Cell(r, 3).Value = "Next Level Supervisor ______/______/______";
            Gop(r, 1, r, 2); Gop(r, 3, r, 5);
            ws.Row(r++).Height = 24;
            ws.Cell(r, 1).Value = "Employee signed__________________________________         "
                + (tu?.NgayNop != null ? tu.NgayNop.Value.ToString("dd/MM/yyyy") : "______/______/______");
            Gop(r, 1, r, 5);
            ws.Row(r++).Height = 24;
            ws.Cell(r, 1).Value = "Comment:" + (string.IsNullOrWhiteSpace(ql?.NhanXet) ? "" : "\n" + ql!.NhanXet);
            Gop(r, 1, r, 5).Style.Alignment.SetVertical(XLAlignmentVerticalValues.Top);
            ws.Row(r).Height = ChieuCao(97.5, (ws.Cell(r, 1).GetString(), DoRongCot.Take(5).Sum()));

            ws.PageSetup.PageOrientation = XLPageOrientation.Landscape;
            ws.PageSetup.PaperSize = XLPaperSize.A4Paper;
            ws.PageSetup.FitToPages(1, 0);
            ws.PageSetup.PrintAreas.Add(1, 1, r, 8);
            ws.PageSetup.Margins.SetLeft(0.4).SetRight(0.4).SetTop(0.5).SetBottom(0.5);
        }

        #endregion
    }
}
