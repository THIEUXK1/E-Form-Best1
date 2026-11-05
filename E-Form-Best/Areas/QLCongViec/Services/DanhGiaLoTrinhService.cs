using E_Form_Best.Context;
using E_Form_Best.Models.ITForm;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace E_Form_Best.Areas.QLCongViec.Services
{
    /// <summary>
    /// Lộ trình thăng tiến theo tài liệu IT 25090401 (BP-HR-PR-026 A/2). Bậc lấy từ DG_HoSoNhanVien vì GAP tính theo
    /// bậc đó; bậc HRM chỉ hiện để so. Không đụng lương hay HRM: lên bậc chỉ đổi hồ sơ đánh giá.
    /// </summary>
    public class DanhGiaLoTrinhService
    {
        private const string MaBacChuNhiem = "4C-4A";

        private readonly ITFormContext _context;

        public DanhGiaLoTrinhService(ITFormContext context)
        {
            _context = context;
        }

        #region DTO

        public class BacDto
        {
            public string MaBac { get; set; } = "";
            public byte ThuTu { get; set; }
            public string ChucDanh { get; set; } = "";
            public string ChucDanhEn { get; set; } = "";
            public byte ThangToiThieu { get; set; }
            public byte ThangToiDa { get; set; }
            public List<string> DsKyNang { get; set; } = new();
            public bool LaHienTai { get; set; }
            public bool DaQua { get; set; }
        }

        public class ThiDto
        {
            public Guid IdThi { get; set; }
            public string? BacHienTai { get; set; }
            public string BacDeXuat { get; set; } = "";
            public DateOnly NgayThi { get; set; }
            public decimal? Diem { get; set; }
            public string KetQua { get; set; } = "";
            public string? GhiChu { get; set; }
            public string? TenNguoiGhi { get; set; }
            public DateOnly? NgayLenBac { get; set; }
            public bool CoTheLenBac { get; set; }
            public bool CoTheXoa { get; set; }
        }

        public class LichSuDto
        {
            public string? BacCu { get; set; }
            public string? BacMoi { get; set; }
            public DateOnly NgayHieuLuc { get; set; }
            public string? GhiChu { get; set; }
            public string? TenNguoi { get; set; }
        }

        public class LoTrinhDto
        {
            public string MaNv { get; set; } = "";
            public string HoTen { get; set; } = "";
            public string? CapBac { get; set; }
            public string? CapBacHrm { get; set; }
            public bool NgoaiLoTrinh { get; set; }
            public DateOnly? NgayGiuBac { get; set; }
            public int? SoThangGiu { get; set; }
            /// <summary>du / chua_du / chua_ro. Chỉ để tham khảo: tài liệu cho xét trước hạn nếu qua bài kiểm tra.</summary>
            public string ThoiGian { get; set; } = "chua_ro";
            public string? BacKeTiep { get; set; }
            public List<BacDto> DsBac { get; set; } = new();
            public List<ThiDto> DsThi { get; set; } = new();
            public List<LichSuDto> DsLichSu { get; set; } = new();
        }

        public class ThemThiVao
        {
            public string? MaNv { get; set; }
            public string? BacDeXuat { get; set; }
            public DateOnly? NgayThi { get; set; }
            public decimal? Diem { get; set; }
            public string? KetQua { get; set; }
            public string? GhiChu { get; set; }
            public Guid? MaGui { get; set; }
        }

        public class ThiVao
        {
            public string? MaNv { get; set; }
            public Guid IdThi { get; set; }
            public DateOnly? NgayHieuLuc { get; set; }
        }

        public class NgayGiuBacVao
        {
            public string? MaNv { get; set; }
            public DateOnly? Ngay { get; set; }
        }

        #endregion

        // Tài liệu gộp 4C–4A thành một dòng nên mã bậc 4x đều rơi vào dòng Chủ nhiệm
        private static string? MaTrenLoTrinh(string? capBac)
            => capBac == null ? null : capBac.StartsWith('4') ? MaBacChuNhiem : capBac;

        private static bool CungBac(string? a, string? b) => string.Equals(a, b, StringComparison.OrdinalIgnoreCase);

        private static int SoThangDu(DateOnly tu, DateOnly den)
            => (den.Year - tu.Year) * 12 + den.Month - tu.Month - (den.Day < tu.Day ? 1 : 0);

        private Task<List<DgBacLoTrinh>> DsBacAsync()
            => _context.DgBacLoTrinhs.AsNoTracking().OrderBy(x => x.ThuTu).ToListAsync();

        public async Task<LoTrinhDto> LayAsync(string maNv, bool laQuanLy)
        {
            var hoSo = await _context.DgHoSoNhanViens.AsNoTracking().FirstOrDefaultAsync(x => x.MaNhanVien == maNv);
            var hrm = await _context.HrmNhanViens.AsNoTracking().Where(x => x.MaNhanVien == maNv)
                .Select(x => new { x.HoTen, x.CapBac }).FirstOrDefaultAsync();
            var tenTk = await _context.Users.AsNoTracking().Where(u => u.Tk == maNv).Select(u => u.HoTen).FirstOrDefaultAsync();
            var dsBac = await DsBacAsync();

            var capBac = hoSo?.CapBac;
            var maTren = MaTrenLoTrinh(capBac);
            var hienTai = dsBac.FirstOrDefault(x => CungBac(x.MaBac, maTren));
            var keTiep = hienTai == null ? null : dsBac.FirstOrDefault(x => x.ThuTu > hienTai.ThuTu);

            var homNay = DateOnly.FromDateTime(DateTime.Now);
            int? soThang = hoSo?.NgayGiuBac == null ? null : SoThangDu(hoSo.NgayGiuBac.Value, homNay);

            var dsThi = await _context.DgThiThangBacs.AsNoTracking()
                .Where(x => x.MaNhanVien == maNv && x.NgayXoa == null)
                .OrderByDescending(x => x.NgayThi).ThenByDescending(x => x.NgayTao)
                .ToListAsync();
            var dsLichSu = await _context.DgLichSuBacs.AsNoTracking()
                .Where(x => x.MaNhanVien == maNv)
                .OrderByDescending(x => x.NgayHieuLuc).ThenByDescending(x => x.NgayTao)
                .ToListAsync();

            var maNguoi = dsThi.Select(x => x.NguoiGhi).Concat(dsLichSu.Select(x => x.NguoiThucHien)).Where(x => x != null).Distinct().ToList();
            var tenNguoi = await _context.Users.AsNoTracking().Where(u => u.Tk != null && maNguoi.Contains(u.Tk))
                .Select(u => new { u.Tk, u.HoTen }).ToListAsync();
            string? Ten(string? ma) => tenNguoi.FirstOrDefault(x => string.Equals(x.Tk, ma, StringComparison.OrdinalIgnoreCase))?.HoTen ?? ma;

            return new LoTrinhDto
            {
                MaNv = maNv,
                HoTen = tenTk ?? hrm?.HoTen ?? maNv,
                CapBac = capBac,
                CapBacHrm = hrm?.CapBac,
                NgoaiLoTrinh = capBac != null && hienTai == null,
                NgayGiuBac = hoSo?.NgayGiuBac,
                SoThangGiu = soThang,
                ThoiGian = soThang == null || hienTai == null ? "chua_ro" : soThang >= hienTai.ThangToiThieu ? "du" : "chua_du",
                BacKeTiep = keTiep?.MaBac,
                DsBac = dsBac.Select(b => new BacDto
                {
                    MaBac = b.MaBac, ThuTu = b.ThuTu, ChucDanh = b.ChucDanh, ChucDanhEn = b.ChucDanhEn,
                    ThangToiThieu = b.ThangToiThieu, ThangToiDa = b.ThangToiDa,
                    DsKyNang = b.KyNang.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToList(),
                    LaHienTai = b == hienTai,
                    DaQua = hienTai != null && b.ThuTu < hienTai.ThuTu
                }).ToList(),
                DsThi = dsThi.Select(t => new ThiDto
                {
                    IdThi = t.IdThi, BacHienTai = t.BacHienTai, BacDeXuat = t.BacDeXuat, NgayThi = t.NgayThi, Diem = t.Diem,
                    KetQua = t.KetQua, GhiChu = t.GhiChu, TenNguoiGhi = Ten(t.NguoiGhi), NgayLenBac = t.NgayLenBac,
                    CoTheLenBac = laQuanLy && t.KetQua == DgThiThangBac.Dat && t.NgayLenBac == null && CungBac(t.BacHienTai, capBac),
                    CoTheXoa = laQuanLy && t.NgayLenBac == null
                }).ToList(),
                DsLichSu = dsLichSu.Select(l => new LichSuDto
                {
                    BacCu = l.BacCu, BacMoi = l.BacMoi, NgayHieuLuc = l.NgayHieuLuc, GhiChu = l.GhiChu, TenNguoi = Ten(l.NguoiThucHien)
                }).ToList()
            };
        }

        public async Task<string?> ThemThiAsync(ThemThiVao vao, string nguoi)
        {
            var maNv = vao.MaNv!.Trim();
            if (vao.MaGui == null) return "Thiếu khoá gửi, tải lại trang rồi thử lại.";
            if (vao.NgayThi == null) return "Chưa chọn ngày thi.";
            if (vao.NgayThi > DateOnly.FromDateTime(DateTime.Now)) return "Ngày thi không được ở tương lai.";
            if (vao.KetQua is not (DgThiThangBac.Dat or DgThiThangBac.KhongDat)) return "Chưa chọn kết quả.";
            if (vao.Diem is < 0 or > 1000) return "Điểm phải trong khoảng 0–1000.";
            var ghiChu = string.IsNullOrWhiteSpace(vao.GhiChu) ? null : vao.GhiChu.Trim();
            if (ghiChu?.Length > 1000) return "Ghi chú tối đa 1000 ký tự.";

            var dsBac = await DsBacAsync();
            var deXuat = dsBac.FirstOrDefault(x => x.MaBac == vao.BacDeXuat);
            if (deXuat == null) return "Bậc đề xuất không có trong lộ trình.";

            var capBac = await _context.DgHoSoNhanViens.AsNoTracking().Where(x => x.MaNhanVien == maNv).Select(x => x.CapBac).FirstOrDefaultAsync();
            var maTren = MaTrenLoTrinh(capBac);
            var hienTai = dsBac.FirstOrDefault(x => CungBac(x.MaBac, maTren));
            if (hienTai != null && deXuat.ThuTu <= hienTai.ThuTu) return "Bậc đề xuất phải cao hơn bậc hiện tại.";

            _context.DgThiThangBacs.Add(new DgThiThangBac
            {
                IdThi = Guid.NewGuid(),
                MaNhanVien = maNv,
                BacHienTai = capBac,
                BacDeXuat = deXuat.MaBac,
                NgayThi = vao.NgayThi.Value,
                Diem = vao.Diem,
                KetQua = vao.KetQua,
                GhiChu = ghiChu,
                MaGui = vao.MaGui,
                NguoiGhi = nguoi,
                NgayTao = DateTime.Now
            });
            try
            {
                await _context.SaveChangesAsync();
            }
            catch (DbUpdateException ex) when (ex.InnerException is SqlException sql && (sql.Number == 2601 || sql.Number == 2627))
            {
                // Trùng ma_gui nghĩa là lần bấm trước đã ghi xong
                _context.ChangeTracker.Clear();
            }
            return null;
        }

        /// <summary>Xoá mềm kết quả ghi nhầm. Đã xác nhận lên bậc thì giữ lại vì lịch sử bậc trỏ tới nó.</summary>
        public async Task<string?> XoaThiAsync(ThiVao vao, string nguoi)
        {
            var maNv = vao.MaNv!.Trim();
            var bayGio = DateTime.Now;
            var soDong = await _context.DgThiThangBacs
                .Where(x => x.IdThi == vao.IdThi && x.MaNhanVien == maNv && x.NgayXoa == null && x.NgayLenBac == null)
                .ExecuteUpdateAsync(s => s.SetProperty(x => x.NgayXoa, bayGio).SetProperty(x => x.NguoiXoa, nguoi));
            return soDong == 0 ? "Không xoá được: kết quả đã xác nhận lên bậc hoặc không còn tồn tại." : null;
        }

        /// <summary>
        /// Xác nhận lên bậc từ kết quả Đạt. Bậc lúc thi phải trùng bậc hiện tại trong hồ sơ, nếu không kết quả đã lỗi thời.
        /// Cập nhật có điều kiện để 2 lần bấm chỉ sinh 1 dòng lịch sử.
        /// </summary>
        public async Task<string?> XacNhanLenBacAsync(ThiVao vao, string nguoi)
        {
            var maNv = vao.MaNv!.Trim();
            var homNay = DateOnly.FromDateTime(DateTime.Now);
            var ngay = vao.NgayHieuLuc ?? homNay;
            if (ngay > homNay) return "Ngày hiệu lực không được ở tương lai.";

            var thi = await _context.DgThiThangBacs.AsNoTracking()
                .FirstOrDefaultAsync(x => x.IdThi == vao.IdThi && x.MaNhanVien == maNv && x.NgayXoa == null);
            if (thi == null) return "Không tìm thấy kết quả thi.";
            if (thi.KetQua != DgThiThangBac.Dat) return "Chỉ xác nhận lên bậc từ kết quả Đạt.";
            if (thi.NgayLenBac != null) return "Kết quả này đã được xác nhận lên bậc.";
            if (ngay < thi.NgayThi) return "Ngày hiệu lực không được trước ngày thi.";
            // Bậc chưa có ma trận yêu cầu thì GAP và chuẩn năng lực của người đó sẽ mất
            if (!await _context.DgMaTranYeuCaus.AnyAsync(x => x.CapBac == thi.BacDeXuat))
                return $"Bậc {thi.BacDeXuat} chưa có ma trận yêu cầu năng lực, thêm ma trận trước khi lên bậc.";

            await using var tx = await _context.Database.BeginTransactionAsync();
            var hs = await _context.DgHoSoNhanViens.FirstOrDefaultAsync(x => x.MaNhanVien == maNv);
            if (hs == null) return "Nhân viên chưa có hồ sơ đánh giá.";
            if (!CungBac(hs.CapBac, thi.BacHienTai))
                return $"Bậc trong hồ sơ đã đổi ({hs.CapBac ?? "chưa có"}) sau lần thi này, cần ghi kết quả mới.";

            var soDong = await _context.DgThiThangBacs
                .Where(x => x.IdThi == thi.IdThi && x.NgayLenBac == null && x.NgayXoa == null)
                .ExecuteUpdateAsync(s => s.SetProperty(x => x.NgayLenBac, ngay).SetProperty(x => x.NguoiXacNhan, nguoi));
            if (soDong == 0) return "Kết quả này vừa được xử lý ở nơi khác.";

            var bayGio = DateTime.Now;
            _context.DgLichSuBacs.Add(new DgLichSuBac
            {
                IdLichSu = Guid.NewGuid(),
                MaNhanVien = maNv,
                BacCu = hs.CapBac,
                BacMoi = thi.BacDeXuat,
                NgayHieuLuc = ngay,
                IdThi = thi.IdThi,
                GhiChu = "Lên bậc sau thi",
                NguoiThucHien = nguoi,
                NgayTao = bayGio
            });
            hs.CapBac = thi.BacDeXuat;
            hs.NgayGiuBac = ngay;
            hs.NgayCapNhat = bayGio;
            hs.NguoiCapNhat = nguoi;
            await _context.SaveChangesAsync();
            await tx.CommitAsync();
            return null;
        }

        public async Task<string?> LuuNgayGiuBacAsync(NgayGiuBacVao vao, string nguoi)
        {
            var maNv = vao.MaNv!.Trim();
            if (vao.Ngay > DateOnly.FromDateTime(DateTime.Now)) return "Ngày giữ bậc không được ở tương lai.";
            var hs = await _context.DgHoSoNhanViens.FirstOrDefaultAsync(x => x.MaNhanVien == maNv);
            if (hs == null) return "Nhân viên chưa có hồ sơ đánh giá.";
            hs.NgayGiuBac = vao.Ngay;
            hs.NgayCapNhat = DateTime.Now;
            hs.NguoiCapNhat = nguoi;
            await _context.SaveChangesAsync();
            return null;
        }
    }
}
