using E_Form_Best.Context;
using E_Form_Best.Models.ITForm;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace E_Form_Best.Areas.QLCongViec.Services
{
    /// <summary>
    /// Tự đánh giá hiệu suất theo mẫu "IT_Biểu đánh giá nhân viên 2025.xlsx" (từ 02/11/2026; trước đó theo app evaluation cũ):
    ///   Part 1 (50đ): KPI được giao, chấm mức 1–5, điểm dòng = trọng số × mức / 5, tổng trọng số do quản lý đặt = 50.
    ///   Part 2 (50đ): tiêu chí năng lực riêng từng người (DG_TieuChiNangLuc), chấm 0 → điểm tối đa của tiêu chí.
    ///   Part 3: điểm mạnh, điểm yếu, lộ trình 1–3 năm, kế hoạch phát triển 1 năm (chữ, không tính điểm).
    ///   Tổng = Part 1 + Part 2 (thang 100). Chuẩn = mức 3 "As a standard" = 60/100 → chênh lệch = tổng − 60 → xếp loại.
    /// Cột cũ (diem_tu_danh_gia %, DG_PhieuNangLuc, ma trận cấp bậc) giữ nguyên trong DB, code không ghi nữa.
    /// </summary>
    public class DanhGiaService
    {
        public const string LoaiTuDanhGia = "self";
        public const string LoaiQuanLy = "manager";
        // App cũ dùng 6B khi nhân viên chưa được gán cấp bậc
        public const string CapBacMacDinh = "6B";
        public const decimal DiemMoiPhan = 50m;
        public const int MucToiDa = 5;
        public const decimal DiemChuan = 60m;
        public const int DoDaiChuToiDa = 8000;

        private readonly ITFormContext _context;

        public DanhGiaService(ITFormContext context)
        {
            _context = context;
        }

        #region DTO

        public class HoSoDanhGia
        {
            public string CapBac { get; set; } = CapBacMacDinh;
            public Guid? IdNhomCongViec { get; set; }
            public string? TenNhomCongViec { get; set; }
            // false = chưa được gán cấp bậc hoặc nhóm công việc
            public bool DayDu { get; set; }
        }

        /// <summary>Đầu phiếu như mẫu Excel: Name / Employee ID / Position / Section / Department.</summary>
        public class ThongTinNhanVien
        {
            public string MaNv { get; set; } = "";
            public string HoTen { get; set; } = "";
            public string? ChucVu { get; set; }
            public string? PhongBan { get; set; }
        }

        public class KpiDto
        {
            public Guid IdKpiMau { get; set; }
            public string CongViec { get; set; } = "";
            public string? KpiGoiY { get; set; }
            public string? KetQuaGoiY { get; set; }
            public string? ChiSoKpi { get; set; }
            public string? KetQua { get; set; }
            public decimal TrongSo { get; set; }
            public byte? MucTuDanhGia { get; set; }
            public bool CoAnh { get; set; }
            public bool LaAnh { get; set; }
        }

        public class TieuChiDto
        {
            public Guid IdTieuChi { get; set; }
            public string Ten { get; set; } = "";
            public string? MoTa { get; set; }
            public decimal DiemToiDa { get; set; }
            public string? TomTat { get; set; }
            public decimal? Diem { get; set; }
        }

        // Minh chứng KPI nhận cả tài liệu: client dựa vào đây để chọn hiện ảnh thu nhỏ hay link tải
        public static bool LaFileAnh(string? tenFile)
        {
            var duoi = Path.GetExtension(tenFile ?? "").ToLowerInvariant();
            return duoi is ".jpg" or ".jpeg" or ".png" or ".webp" or ".gif" or ".bmp";
        }

        public class PhieuDto
        {
            public Guid IdPhieu { get; set; }
            public string TrangThai { get; set; } = "";
            public decimal? DiemKpi { get; set; }
            public decimal? DiemNangLuc { get; set; }
            public decimal? DiemThucTe { get; set; }
            public decimal? ChenhLech { get; set; }
            public string? XepLoai { get; set; }
            public DateTime? NgayNop { get; set; }
            public DateTime? NgayCapNhat { get; set; }
            public string? DiemManh { get; set; }
            public string? DiemYeu { get; set; }
            public string? LoTrinh { get; set; }
            public string? KeHoach { get; set; }
        }

        public class KpiVao
        {
            public Guid IdKpiMau { get; set; }
            public string? ChiSoKpi { get; set; }
            public string? KetQua { get; set; }
            public byte? MucTuDanhGia { get; set; }
        }

        public class TieuChiVao
        {
            public Guid IdTieuChi { get; set; }
            public string? TomTat { get; set; }
            public decimal? Diem { get; set; }
        }

        public class LuuTuDanhGiaVao
        {
            public List<KpiVao>? DsKpi { get; set; }
            public List<TieuChiVao>? DsTieuChi { get; set; }
            public string? DiemManh { get; set; }
            public string? DiemYeu { get; set; }
            public string? LoTrinh { get; set; }
            public string? KeHoach { get; set; }
            // true = bấm "Hoàn thành" (ghi ngày nộp); false = lưu nháp khi chuyển bước
            public bool HoanThanh { get; set; }
        }

        #endregion

        #region Tính điểm

        public static decimal DiemKpi(IEnumerable<(decimal TrongSo, byte? Muc)> ds)
            => Math.Round(ds.Sum(x => x.TrongSo * (x.Muc ?? 0) / MucToiDa), 2);

        public static string XepLoai(decimal chenhLech)
        {
            // Mốc theo thang mức của mẫu: ≥ mức 4 (80đ) vượt chuẩn, ≥ mức 3 (60đ) đạt, ≥ mức 2 (40đ) cần cải thiện
            if (chenhLech >= 20) return "Vượt chuẩn";
            if (chenhLech >= 0) return "Đạt chuẩn";
            if (chenhLech >= -20) return "Cần cải thiện";
            return "Chưa đạt";
        }

        /// <summary>Ghi điểm tổng hợp vào phiếu (Part 1 + Part 2, so với chuẩn 60).</summary>
        public static void GanDiem(DgPhieuDanhGia p, decimal diemKpi, decimal diemNangLuc)
        {
            var tong = diemKpi + diemNangLuc;
            p.DiemKpi = diemKpi;
            p.DiemNangLuc = diemNangLuc;
            p.TongYeuCau = DiemChuan;
            p.DiemThucTe = tong;
            p.ChenhLech = tong - DiemChuan;
            p.XepLoai = XepLoai(tong - DiemChuan);
        }

        public static string? CatChu(string? s)
        {
            var t = s?.Trim();
            if (string.IsNullOrEmpty(t)) return null;
            return t.Length > DoDaiChuToiDa ? t[..DoDaiChuToiDa] : t;
        }

        #endregion

        #region Đọc

        public async Task<HoSoDanhGia> LayHoSoAsync(string maNv)
        {
            var hoSo = await _context.DgHoSoNhanViens.AsNoTracking()
                .Where(x => x.MaNhanVien == maNv)
                .Select(x => new { x.CapBac, x.IdNhomCongViec })
                .FirstOrDefaultAsync();

            var kq = new HoSoDanhGia
            {
                CapBac = string.IsNullOrWhiteSpace(hoSo?.CapBac) ? CapBacMacDinh : hoSo!.CapBac!,
                IdNhomCongViec = hoSo?.IdNhomCongViec,
                DayDu = !string.IsNullOrWhiteSpace(hoSo?.CapBac) && hoSo?.IdNhomCongViec != null
            };
            if (kq.IdNhomCongViec != null)
            {
                kq.TenNhomCongViec = await _context.DgNhomCongViecs.AsNoTracking()
                    .Where(x => x.IdNhomCongViec == kq.IdNhomCongViec)
                    .Select(x => x.TenNhom)
                    .FirstOrDefaultAsync();
            }
            return kq;
        }

        /// <summary>Họ tên E-Form (kèm tên HRM khi khác, vd tên tiếng Trung như mẫu Excel), chức vụ + phòng ban theo HRM.</summary>
        public async Task<ThongTinNhanVien> LayThongTinAsync(string maNv)
        {
            var u = await _context.Users.AsNoTracking().Where(x => x.Tk == maNv)
                .Select(x => new { x.HoTen, x.PhongBan }).FirstOrDefaultAsync();
            var h = await _context.HrmNhanViens.AsNoTracking().Where(x => x.MaNhanVien == maNv)
                .Select(x => new { x.HoTen, x.ChucVu, x.PhongBan, x.PhongBanEn }).FirstOrDefaultAsync();
            var ten = u?.HoTen ?? h?.HoTen ?? maNv;
            if (u?.HoTen != null && !string.IsNullOrWhiteSpace(h?.HoTen) && !string.Equals(h.HoTen.Trim(), u.HoTen.Trim(), StringComparison.OrdinalIgnoreCase))
                ten += " " + h.HoTen.Trim();
            return new ThongTinNhanVien
            {
                MaNv = maNv,
                HoTen = ten,
                ChucVu = h?.ChucVu,
                PhongBan = h?.PhongBanEn ?? h?.PhongBan ?? u?.PhongBan
            };
        }

        public Task<List<string>> DsCapBacAsync()
            => _context.DgMaTranYeuCaus.AsNoTracking()
                .Select(x => x.CapBac).Distinct().OrderBy(x => x).ToListAsync();

        public Task<List<DgNhomCongViec>> DsNhomCongViecAsync()
            => _context.DgNhomCongViecs.AsNoTracking()
                .OrderBy(x => x.TenNhom)
                .Select(x => new DgNhomCongViec { IdNhomCongViec = x.IdNhomCongViec, MaNhom = x.MaNhom, TenNhom = x.TenNhom })
                .ToListAsync();

        public static PhieuDto? SangDto(DgPhieuDanhGia? p) => p == null ? null : new PhieuDto
        {
            IdPhieu = p.IdPhieu, TrangThai = p.TrangThai,
            DiemKpi = p.DiemKpi, DiemNangLuc = p.DiemNangLuc, DiemThucTe = p.DiemThucTe,
            ChenhLech = p.ChenhLech, XepLoai = p.XepLoai, NgayNop = p.NgayNop, NgayCapNhat = p.NgayCapNhat,
            DiemManh = p.DiemManh, DiemYeu = p.DiemYeu, LoTrinh = p.LoTrinh, KeHoach = p.KeHoach
        };

        public async Task<PhieuDto?> LayPhieuTuDanhGiaAsync(string maNv)
            => SangDto(await _context.DgPhieuDanhGias.AsNoTracking()
                .FirstOrDefaultAsync(x => x.MaNhanVien == maNv && x.Loai == LoaiTuDanhGia));

        /// <summary>KPI được giao (còn hiệu lực), ghép phần đã nhập trong phiếu nếu có.</summary>
        public async Task<List<KpiDto>> LayKpiAsync(string maNv, Guid? idPhieu)
        {
            var dsMau = await (
                from pc in _context.DgKpiPhanCongs
                join m in _context.DgKpiMaus on pc.IdKpiMau equals m.IdKpiMau
                where pc.MaNhanVien == maNv && pc.TrangThai == "active" && m.TrangThai == "active"
                orderby m.ThuTu, m.CongViec
                select new { m.IdKpiMau, m.CongViec, m.KpiGoiY, m.KetQuaGoiY, m.TrongSoBoPhan }
            ).AsNoTracking().ToListAsync();

            var daNhap = idPhieu == null
                ? new Dictionary<Guid, DgPhieuKpi>()
                : await _context.DgPhieuKpis.AsNoTracking()
                    .Where(x => x.IdPhieu == idPhieu)
                    .ToDictionaryAsync(x => x.IdKpiMau);

            return dsMau.Select(m =>
            {
                daNhap.TryGetValue(m.IdKpiMau, out var k);
                return new KpiDto
                {
                    IdKpiMau = m.IdKpiMau,
                    CongViec = m.CongViec,
                    KpiGoiY = m.KpiGoiY,
                    KetQuaGoiY = m.KetQuaGoiY,
                    // Chưa nhập thì điền sẵn gợi ý như app cũ
                    ChiSoKpi = k != null ? k.ChiSoKpi : m.KpiGoiY,
                    KetQua = k != null ? k.KetQua : m.KetQuaGoiY,
                    TrongSo = k?.TrongSo ?? m.TrongSoBoPhan ?? 0,
                    MucTuDanhGia = k?.MucTuDanhGia,
                    CoAnh = !string.IsNullOrEmpty(k?.AnhMinhChung),
                    LaAnh = LaFileAnh(k?.AnhMinhChung)
                };
            }).ToList();
        }

        /// <summary>Tiêu chí Part 2 đang dùng của maNv, ghép tóm tắt + điểm trong phiếu idPhieu (nếu có).</summary>
        public async Task<List<TieuChiDto>> LayTieuChiAsync(string maNv, Guid? idPhieu)
        {
            var ds = await _context.DgTieuChiNangLucs.AsNoTracking()
                .Where(x => x.MaNhanVien == maNv && x.TrangThai == "active")
                .OrderBy(x => x.ThuTu).ThenBy(x => x.Ten)
                .Select(x => new TieuChiDto { IdTieuChi = x.IdTieuChi, Ten = x.Ten, MoTa = x.MoTa, DiemToiDa = x.DiemToiDa })
                .ToListAsync();
            if (idPhieu == null || ds.Count == 0) return ds;

            var diem = await _context.DgPhieuTieuChis.AsNoTracking()
                .Where(x => x.IdPhieu == idPhieu)
                .ToDictionaryAsync(x => x.IdTieuChi);
            foreach (var t in ds)
            {
                if (!diem.TryGetValue(t.IdTieuChi, out var d)) continue;
                t.TomTat = d.TomTat;
                t.Diem = d.Diem;
            }
            return ds;
        }

        #endregion

        #region Ghi

        /// <summary>
        /// Lấy phiếu theo loại (self/manager), chưa có thì tạo. UNIQUE (ma_nhan_vien, loai) ở DB: hai request song song
        /// cùng tạo thì request thua nhận lỗi trùng khoá → đọc lại phiếu bên thắng, không sinh phiếu thứ hai.
        /// </summary>
        public async Task<DgPhieuDanhGia> LayHoacTaoPhieuAsync(string maNv, string loai = LoaiTuDanhGia)
        {
            var p = await _context.DgPhieuDanhGias.FirstOrDefaultAsync(x => x.MaNhanVien == maNv && x.Loai == loai);
            if (p != null) return p;

            var hoSo = await LayHoSoAsync(maNv);
            p = new DgPhieuDanhGia
            {
                IdPhieu = Guid.NewGuid(),
                MaNhanVien = maNv,
                Loai = loai,
                TrangThai = "pending",
                CapBac = hoSo.CapBac,
                IdNhomCongViec = hoSo.IdNhomCongViec,
                NgayTao = DateTime.Now
            };
            _context.DgPhieuDanhGias.Add(p);
            try
            {
                await _context.SaveChangesAsync();
                return p;
            }
            catch (DbUpdateException ex) when (ex.InnerException is SqlException sql && (sql.Number == 2601 || sql.Number == 2627))
            {
                _context.Entry(p).State = EntityState.Detached;
                return await _context.DgPhieuDanhGias.FirstAsync(x => x.MaNhanVien == maNv && x.Loai == loai);
            }
        }

        /// <summary>
        /// Upsert điểm tiêu chí Part 2 của 1 phiếu theo (phiếu, tiêu chí). ghiTomTat=false (phiếu quản lý) thì không đụng tóm tắt.
        /// Trả tổng điểm các tiêu chí đang dùng.
        /// </summary>
        public async Task<decimal> GhiTieuChiAsync(Guid idPhieu, IReadOnlyDictionary<Guid, DgTieuChiNangLuc> dsTieuChi,
            IEnumerable<TieuChiVao> vao, bool ghiTomTat, DateTime bayGio)
        {
            var cu = await _context.DgPhieuTieuChis.Where(x => x.IdPhieu == idPhieu).ToDictionaryAsync(x => x.IdTieuChi);
            foreach (var t in vao)
            {
                if (!cu.TryGetValue(t.IdTieuChi, out var dong))
                {
                    dong = new DgPhieuTieuChi { IdPhieuTieuChi = Guid.NewGuid(), IdPhieu = idPhieu, IdTieuChi = t.IdTieuChi };
                    _context.DgPhieuTieuChis.Add(dong);
                    cu[t.IdTieuChi] = dong;
                }
                if (ghiTomTat) dong.TomTat = CatChu(t.TomTat);
                dong.Diem = t.Diem == null ? null : Math.Round(t.Diem.Value, 2);
                dong.NgayCapNhat = bayGio;
            }
            return cu.Values.Where(x => dsTieuChi.ContainsKey(x.IdTieuChi)).Sum(x => x.Diem ?? 0);
        }

        /// <summary>Trả null nếu hợp lệ, ngược lại là thông báo lỗi cho người dùng.</summary>
        public async Task<string?> LuuTuDanhGiaAsync(string maNv, LuuTuDanhGiaVao vao)
        {
            // KPI: chỉ ghi cho KPI đang được giao, nhân viên không tự thêm KPI ngoài phân công
            var kpiDuocGiao = await _context.DgKpiPhanCongs
                .Where(x => x.MaNhanVien == maNv && x.TrangThai == "active")
                .Join(_context.DgKpiMaus.Where(m => m.TrangThai == "active"), pc => pc.IdKpiMau, m => m.IdKpiMau,
                      (pc, m) => new { m.IdKpiMau, m.CongViec, m.TrongSoBoPhan })
                .ToDictionaryAsync(x => x.IdKpiMau);
            var kpiVao = (vao.DsKpi ?? new())
                .Where(x => kpiDuocGiao.ContainsKey(x.IdKpiMau))
                .GroupBy(x => x.IdKpiMau).Select(g => g.Last()).ToList();
            if (kpiVao.Any(x => x.MucTuDanhGia is < 1 or > MucToiDa))
                return $"Mức tự đánh giá KPI phải từ 1 đến {MucToiDa}.";

            // Part 2: chỉ nhận tiêu chí đang dùng của chính người này — id lạ gửi lên bị bỏ qua
            var dsTieuChi = await _context.DgTieuChiNangLucs
                .Where(x => x.MaNhanVien == maNv && x.TrangThai == "active")
                .ToDictionaryAsync(x => x.IdTieuChi);
            var tcVao = (vao.DsTieuChi ?? new())
                .Where(x => dsTieuChi.ContainsKey(x.IdTieuChi))
                .GroupBy(x => x.IdTieuChi).Select(g => g.Last()).ToList();
            var tcSai = tcVao.FirstOrDefault(x => x.Diem is { } d && (d < 0 || d > dsTieuChi[x.IdTieuChi].DiemToiDa));
            if (tcSai != null)
                return $"Điểm \"{dsTieuChi[tcSai.IdTieuChi].Ten}\" phải trong khoảng 0–{dsTieuChi[tcSai.IdTieuChi].DiemToiDa:0.##}.";

            if (vao.HoanThanh)
            {
                if (kpiDuocGiao.Keys.Any(id => kpiVao.FirstOrDefault(x => x.IdKpiMau == id)?.MucTuDanhGia == null))
                    return "Chưa chọn mức tự đánh giá cho đủ các nhiệm vụ ở Part 1.";
                if (dsTieuChi.Keys.Any(id => tcVao.FirstOrDefault(x => x.IdTieuChi == id)?.Diem == null))
                    return "Chưa chấm đủ các tiêu chí năng lực ở Part 2.";
            }

            var phieu = await LayHoacTaoPhieuAsync(maNv);
            if (phieu.TrangThai == "completed")
                return "Phiếu đã được quản lý đánh giá xong, không sửa được nữa.";

            var bayGio = DateTime.Now;
            await using var tx = await _context.Database.BeginTransactionAsync();

            // KPI: upsert theo (phiếu, KPI mẫu); giữ nguyên trọng số/điểm quản lý/ảnh đã có
            var kpiCu = await _context.DgPhieuKpis.Where(x => x.IdPhieu == phieu.IdPhieu).ToDictionaryAsync(x => x.IdKpiMau);
            // Quản lý chấm trước thì trọng số lấy theo phiếu quản lý, không lấy trọng số gợi ý của KPI mẫu
            var phieuQl = await _context.DgPhieuDanhGias.FirstOrDefaultAsync(x => x.MaNhanVien == maNv && x.Loai == LoaiQuanLy);
            var trongSoQl = phieuQl == null ? new Dictionary<Guid, decimal>()
                : await _context.DgPhieuKpis.Where(x => x.IdPhieu == phieuQl.IdPhieu).ToDictionaryAsync(x => x.IdKpiMau, x => x.TrongSo);
            foreach (var k in kpiVao)
            {
                if (!kpiCu.TryGetValue(k.IdKpiMau, out var dong))
                {
                    dong = new DgPhieuKpi
                    {
                        IdPhieuKpi = Guid.NewGuid(),
                        IdPhieu = phieu.IdPhieu,
                        IdKpiMau = k.IdKpiMau,
                        CongViec = kpiDuocGiao[k.IdKpiMau].CongViec,
                        TrongSo = trongSoQl.TryGetValue(k.IdKpiMau, out var ts) ? ts : kpiDuocGiao[k.IdKpiMau].TrongSoBoPhan ?? 0,
                        NgayTao = bayGio
                    };
                    _context.DgPhieuKpis.Add(dong);
                    kpiCu[k.IdKpiMau] = dong;
                }
                dong.ChiSoKpi = k.ChiSoKpi?.Trim();
                dong.KetQua = k.KetQua?.Trim();
                dong.MucTuDanhGia = k.MucTuDanhGia;
                dong.NgayCapNhat = bayGio;
            }

            var diemNangLuc = await GhiTieuChiAsync(phieu.IdPhieu, dsTieuChi, tcVao, true, bayGio);
            var diemKpi = DiemKpi(kpiCu.Values.Where(x => kpiDuocGiao.ContainsKey(x.IdKpiMau)).Select(x => (x.TrongSo, x.MucTuDanhGia)));

            var hoSo = await LayHoSoAsync(maNv);
            phieu.CapBac = hoSo.CapBac;
            phieu.IdNhomCongViec = hoSo.IdNhomCongViec;
            GanDiem(phieu, diemKpi, diemNangLuc);
            phieu.DiemManh = CatChu(vao.DiemManh);
            phieu.DiemYeu = CatChu(vao.DiemYeu);
            phieu.LoTrinh = CatChu(vao.LoTrinh);
            phieu.KeHoach = CatChu(vao.KeHoach);
            phieu.NgayCapNhat = bayGio;
            if (vao.HoanThanh) phieu.NgayNop = bayGio;
            // Quản lý đã gửi đánh giá từ trước → nhân viên gửi xong là hoàn tất cả hai phiếu
            if (phieu.NgayNop != null && phieuQl?.NgayNop != null)
            {
                phieu.TrangThai = "completed";
                phieuQl.TrangThai = "completed";
            }

            await _context.SaveChangesAsync();
            await tx.CommitAsync();
            return null;
        }

        /// <summary>
        /// Dòng KPI để gắn ảnh minh chứng. App cũ bắt "lưu KPI trước khi upload"; ở đây tự tạo phiếu + dòng
        /// KPI nếu chưa có. Trả null nếu KPI không được giao cho người này (chống IDOR theo id gửi lên).
        /// </summary>
        public async Task<DgPhieuKpi?> LayHoacTaoDongKpiAsync(string maNv, Guid idKpiMau)
        {
            var mau = await _context.DgKpiPhanCongs
                .Where(x => x.MaNhanVien == maNv && x.IdKpiMau == idKpiMau && x.TrangThai == "active")
                .Join(_context.DgKpiMaus, pc => pc.IdKpiMau, m => m.IdKpiMau, (pc, m) => new { m.CongViec, m.TrongSoBoPhan, m.KpiGoiY, m.KetQuaGoiY })
                .FirstOrDefaultAsync();
            if (mau == null) return null;

            var phieu = await LayHoacTaoPhieuAsync(maNv);
            var dong = await _context.DgPhieuKpis.FirstOrDefaultAsync(x => x.IdPhieu == phieu.IdPhieu && x.IdKpiMau == idKpiMau);
            if (dong != null) return dong;

            dong = new DgPhieuKpi
            {
                IdPhieuKpi = Guid.NewGuid(),
                IdPhieu = phieu.IdPhieu,
                IdKpiMau = idKpiMau,
                CongViec = mau.CongViec,
                ChiSoKpi = mau.KpiGoiY,
                KetQua = mau.KetQuaGoiY,
                TrongSo = mau.TrongSoBoPhan ?? 0,
                NgayTao = DateTime.Now
            };
            _context.DgPhieuKpis.Add(dong);
            try
            {
                await _context.SaveChangesAsync();
                return dong;
            }
            catch (DbUpdateException ex) when (ex.InnerException is SqlException sql && (sql.Number == 2601 || sql.Number == 2627))
            {
                _context.Entry(dong).State = EntityState.Detached;
                return await _context.DgPhieuKpis.FirstAsync(x => x.IdPhieu == phieu.IdPhieu && x.IdKpiMau == idKpiMau);
            }
        }

        /// <summary>Dòng KPI trong phiếu tự đánh giá của chính maNv (null nếu chưa có / không phải của người này).</summary>
        public Task<DgPhieuKpi?> LayDongKpiAsync(string maNv, Guid idKpiMau)
            => _context.DgPhieuKpis
                .Where(k => k.IdKpiMau == idKpiMau
                            && _context.DgPhieuDanhGias.Any(p => p.IdPhieu == k.IdPhieu && p.MaNhanVien == maNv && p.Loai == LoaiTuDanhGia))
                .FirstOrDefaultAsync();

        public async Task GanAnhKpiAsync(DgPhieuKpi dong, string? tenFile)
        {
            dong.AnhMinhChung = tenFile;
            dong.NgayCapNhat = DateTime.Now;
            await _context.SaveChangesAsync();
        }

        public async Task<bool> PhieuDaKhoaAsync(string maNv)
            => await _context.DgPhieuDanhGias.AnyAsync(x => x.MaNhanVien == maNv && x.Loai == LoaiTuDanhGia && x.TrangThai == "completed");

        #endregion
    }
}
