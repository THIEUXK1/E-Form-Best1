using E_Form_Best.Context;
using E_Form_Best.Models.ITForm;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace E_Form_Best.Areas.QLCongViec.Services
{
    /// <summary>
    /// Tự đánh giá hiệu suất (chuyển từ app evaluation NestJS/PostgreSQL về E-Form).
    /// Cách tính giữ như app cũ (evaluation.service.ts saveSelfEvaluation):
    ///   điểm năng lực = tổng điểm tự chấm; điểm thực tế = điểm KPI + điểm năng lực;
    ///   chênh lệch = thực tế − tổng điểm yêu cầu của cấp bậc/nhóm công việc → xếp loại.
    /// Riêng điểm KPI: app cũ luôn gửi 0; ở đây tính Σ(trọng số × % tự đánh giá / 100), trọng số do quản lý
    /// điền nên khi chưa có trọng số kết quả vẫn là 0 như cũ.
    /// </summary>
    public class DanhGiaService
    {
        public const string LoaiTuDanhGia = "self";
        public const string LoaiQuanLy = "manager";
        // App cũ dùng 6B khi nhân viên chưa được gán cấp bậc
        public const string CapBacMacDinh = "6B";
        public const decimal DiemNangLucToiDa = 10m;
        public const decimal DiemKpiToiDa = 120m;

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
            // false = chưa được gán cấp bậc hoặc nhóm công việc → giao diện báo dùng mặc định
            public bool DayDu { get; set; }
        }

        public class NangLucDto
        {
            public Guid IdNangLuc { get; set; }
            public string Ma { get; set; } = "";
            public string Ten { get; set; } = "";
            public string? MoTa { get; set; }
            public decimal DiemYeuCau { get; set; }
        }

        public class NhomNangLucDto
        {
            public Guid IdNhom { get; set; }
            public string Ma { get; set; } = "";
            public string Ten { get; set; } = "";
            public List<NangLucDto> DsNangLuc { get; set; } = new();
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
            public decimal? DiemTuDanhGia { get; set; }
            public bool CoAnh { get; set; }
        }

        public class PhieuDto
        {
            public Guid IdPhieu { get; set; }
            public string TrangThai { get; set; } = "";
            public string? CapBac { get; set; }
            public Guid? IdNhomCongViec { get; set; }
            public decimal? DiemKpi { get; set; }
            public decimal? DiemNangLuc { get; set; }
            public decimal? TongYeuCau { get; set; }
            public decimal? DiemThucTe { get; set; }
            public decimal? ChenhLech { get; set; }
            public string? XepLoai { get; set; }
            public DateTime? NgayNop { get; set; }
            public DateTime? NgayCapNhat { get; set; }
            public Dictionary<Guid, decimal> DiemTheoNangLuc { get; set; } = new();
        }

        public class DiemNangLucVao
        {
            public Guid IdNangLuc { get; set; }
            public decimal Diem { get; set; }
        }

        public class KpiVao
        {
            public Guid IdKpiMau { get; set; }
            public string? ChiSoKpi { get; set; }
            public string? KetQua { get; set; }
            public decimal? DiemTuDanhGia { get; set; }
        }

        public class LuuTuDanhGiaVao
        {
            public string? CapBac { get; set; }
            public Guid? IdNhomCongViec { get; set; }
            public List<DiemNangLucVao>? DsNangLuc { get; set; }
            public List<KpiVao>? DsKpi { get; set; }
            // true = bấm "Hoàn thành" (ghi ngày nộp); false = lưu nháp khi chuyển bước
            public bool HoanThanh { get; set; }
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

        public Task<List<string>> DsCapBacAsync()
            => _context.DgMaTranYeuCaus.AsNoTracking()
                .Select(x => x.CapBac).Distinct().OrderBy(x => x).ToListAsync();

        public Task<List<DgNhomCongViec>> DsNhomCongViecAsync()
            => _context.DgNhomCongViecs.AsNoTracking()
                .OrderBy(x => x.TenNhom)
                .Select(x => new DgNhomCongViec { IdNhomCongViec = x.IdNhomCongViec, MaNhom = x.MaNhom, TenNhom = x.TenNhom })
                .ToListAsync();

        /// <summary>
        /// Năng lực theo cấp bậc; có nhóm công việc thì lấy năng lực chung + năng lực riêng của nhóm đó
        /// (giống getCompetencesByGradeAndJobGroup của app cũ). Không tự sinh ma trận khi thiếu cấp bậc —
        /// app cũ ghi DB ngay trong lệnh GET, ở đây trả rỗng để giao diện báo.
        /// </summary>
        public async Task<List<NhomNangLucDto>> LayNangLucAsync(string capBac, Guid? idNhomCongViec)
        {
            var dong = await (
                from mt in _context.DgMaTranYeuCaus
                join nl in _context.DgNangLucs on mt.IdNangLuc equals nl.IdNangLuc
                join nh in _context.DgNhomNangLucs on nl.IdNhomNangLuc equals nh.IdNhomNangLuc
                where mt.CapBac == capBac
                      && (idNhomCongViec == null || nl.IdNhomCongViec == null || nl.IdNhomCongViec == idNhomCongViec)
                orderby nh.MaNhom, nl.MaNangLuc
                select new
                {
                    nh.IdNhomNangLuc, MaNhom = nh.MaNhom, TenNhom = nh.TenNhom,
                    nl.IdNangLuc, nl.MaNangLuc, nl.TenNangLuc, nl.MoTa, mt.DiemYeuCau
                }).AsNoTracking().ToListAsync();

            return dong
                .GroupBy(x => new { x.IdNhomNangLuc, x.MaNhom, x.TenNhom })
                .Select(g => new NhomNangLucDto
                {
                    IdNhom = g.Key.IdNhomNangLuc,
                    Ma = g.Key.MaNhom,
                    Ten = g.Key.TenNhom,
                    DsNangLuc = g.Select(x => new NangLucDto
                    {
                        IdNangLuc = x.IdNangLuc, Ma = x.MaNangLuc, Ten = x.TenNangLuc, MoTa = x.MoTa, DiemYeuCau = x.DiemYeuCau
                    }).ToList()
                }).ToList();
        }

        public async Task<PhieuDto?> LayPhieuTuDanhGiaAsync(string maNv)
        {
            var p = await _context.DgPhieuDanhGias.AsNoTracking()
                .FirstOrDefaultAsync(x => x.MaNhanVien == maNv && x.Loai == LoaiTuDanhGia);
            if (p == null) return null;

            var diem = await _context.DgPhieuNangLucs.AsNoTracking()
                .Where(x => x.IdPhieu == p.IdPhieu)
                .Select(x => new { x.IdNangLuc, x.DiemThucTe })
                .ToListAsync();

            return new PhieuDto
            {
                IdPhieu = p.IdPhieu, TrangThai = p.TrangThai, CapBac = p.CapBac, IdNhomCongViec = p.IdNhomCongViec,
                DiemKpi = p.DiemKpi, DiemNangLuc = p.DiemNangLuc, TongYeuCau = p.TongYeuCau, DiemThucTe = p.DiemThucTe,
                ChenhLech = p.ChenhLech, XepLoai = p.XepLoai, NgayNop = p.NgayNop, NgayCapNhat = p.NgayCapNhat,
                DiemTheoNangLuc = diem.ToDictionary(x => x.IdNangLuc, x => x.DiemThucTe)
            };
        }

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
                    DiemTuDanhGia = k?.DiemTuDanhGia,
                    CoAnh = !string.IsNullOrEmpty(k?.AnhMinhChung)
                };
            }).ToList();
        }

        #endregion

        #region Ghi

        public static string XepLoai(decimal chenhLech)
        {
            if (chenhLech >= 1) return "Vượt chuẩn";
            if (chenhLech >= 0) return "Đạt chuẩn";
            if (chenhLech >= -1) return "Cần cải thiện";
            return "Chưa đạt";
        }

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

        /// <summary>Trả null nếu hợp lệ, ngược lại là thông báo lỗi cho người dùng.</summary>
        public async Task<string?> LuuTuDanhGiaAsync(string maNv, LuuTuDanhGiaVao vao)
        {
            var capBac = string.IsNullOrWhiteSpace(vao.CapBac) ? CapBacMacDinh : vao.CapBac.Trim();
            if (vao.IdNhomCongViec != null && !await _context.DgNhomCongViecs.AnyAsync(x => x.IdNhomCongViec == vao.IdNhomCongViec))
                return "Nhóm công việc không tồn tại.";

            // Chỉ nhận điểm cho năng lực thuộc đúng bộ lọc đang chấm — id lạ gửi lên bị bỏ qua
            var dsNangLuc = (await LayNangLucAsync(capBac, vao.IdNhomCongViec)).SelectMany(x => x.DsNangLuc).ToDictionary(x => x.IdNangLuc);
            if (dsNangLuc.Count == 0)
                return $"Chưa có ma trận năng lực cho cấp bậc {capBac}.";

            var diemVao = (vao.DsNangLuc ?? new())
                .Where(x => dsNangLuc.ContainsKey(x.IdNangLuc))
                .GroupBy(x => x.IdNangLuc).Select(g => g.Last()).ToList();
            if (diemVao.Any(x => x.Diem < 0 || x.Diem > DiemNangLucToiDa))
                return $"Điểm năng lực phải trong khoảng 0–{DiemNangLucToiDa:0}.";

            // KPI: chỉ ghi cho KPI đang được giao, nhân viên không tự thêm KPI ngoài phân công
            var kpiDuocGiao = await _context.DgKpiPhanCongs
                .Where(x => x.MaNhanVien == maNv && x.TrangThai == "active")
                .Join(_context.DgKpiMaus.Where(m => m.TrangThai == "active"), pc => pc.IdKpiMau, m => m.IdKpiMau,
                      (pc, m) => new { m.IdKpiMau, m.CongViec, m.TrongSoBoPhan })
                .ToDictionaryAsync(x => x.IdKpiMau);
            var kpiVao = (vao.DsKpi ?? new())
                .Where(x => kpiDuocGiao.ContainsKey(x.IdKpiMau))
                .GroupBy(x => x.IdKpiMau).Select(g => g.Last()).ToList();
            if (kpiVao.Any(x => x.DiemTuDanhGia is < 0 or > DiemKpiToiDa))
                return $"Điểm tự đánh giá KPI phải trong khoảng 0–{DiemKpiToiDa:0}%.";

            var phieu = await LayHoacTaoPhieuAsync(maNv);
            if (phieu.TrangThai == "completed")
                return "Phiếu đã được quản lý đánh giá xong, không sửa được nữa.";

            var bayGio = DateTime.Now;
            await using var tx = await _context.Database.BeginTransactionAsync();

            // Điểm năng lực: thay toàn bộ bằng bộ điểm vừa gửi (giống app cũ xoá rồi tạo lại item self)
            await _context.DgPhieuNangLucs.Where(x => x.IdPhieu == phieu.IdPhieu).ExecuteDeleteAsync();
            foreach (var d in diemVao)
            {
                var yeuCau = dsNangLuc[d.IdNangLuc].DiemYeuCau;
                _context.DgPhieuNangLucs.Add(new DgPhieuNangLuc
                {
                    IdPhieuNangLuc = Guid.NewGuid(),
                    IdPhieu = phieu.IdPhieu,
                    IdNangLuc = d.IdNangLuc,
                    DiemThucTe = Math.Round(d.Diem, 2),
                    DiemYeuCau = yeuCau,
                    ChenhLech = Math.Round(d.Diem - yeuCau, 2)
                });
            }

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
                dong.DiemTuDanhGia = k.DiemTuDanhGia;
                dong.NgayCapNhat = bayGio;
            }

            decimal diemNangLuc = diemVao.Sum(x => x.Diem);
            decimal diemKpi = kpiCu.Values.Sum(x => x.TrongSo * (x.DiemTuDanhGia ?? 0) / 100m);
            decimal tongYeuCau = dsNangLuc.Values.Sum(x => x.DiemYeuCau);
            decimal thucTe = diemKpi + diemNangLuc;

            phieu.CapBac = capBac;
            phieu.IdNhomCongViec = vao.IdNhomCongViec;
            phieu.DiemNangLuc = diemNangLuc;
            phieu.DiemKpi = Math.Round(diemKpi, 4);
            phieu.TongYeuCau = tongYeuCau;
            phieu.DiemThucTe = Math.Round(thucTe, 4);
            phieu.ChenhLech = Math.Round(thucTe - tongYeuCau, 4);
            phieu.XepLoai = XepLoai(thucTe - tongYeuCau);
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
