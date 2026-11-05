using E_Form_Best.Context;
using E_Form_Best.Models.ITForm;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace E_Form_Best.Areas.QLCongViec.Services
{
    /// <summary>
    /// Ma trận đầu việc × người. Overload giữ đúng cách tính của file phân công: số lượng chia số người phụ trách chính
    /// rồi so với chuẩn mỗi người (800 / 3 > 200). Đầu việc chưa có chuẩn thì không kết luận, không tự đặt ngưỡng.
    /// </summary>
    public class DanhGiaPhanCongService
    {
        public const string CbOverload = "overload";
        public const string CbMotNguoi = "mot_nguoi";
        public const string CbChuaCoNguoi = "chua_co_nguoi";
        public const string CbKhongChinh = "khong_chinh";
        public const string CbKhongDuPhong = "khong_du_phong";

        private readonly ITFormContext _context;
        private readonly DanhGiaQuanLyService _quanLy;
        private readonly DanhGiaSoLieuService _soLieu;

        public DanhGiaPhanCongService(ITFormContext context, DanhGiaQuanLyService quanLy, DanhGiaSoLieuService soLieu)
        {
            _context = context;
            _quanLy = quanLy;
            _soLieu = soLieu;
        }

        #region DTO

        public class GanDto
        {
            public string MaNv { get; set; } = "";
            public string VaiTro { get; set; } = "";
        }

        public class DauViecDto
        {
            public Guid IdDauViec { get; set; }
            public string Nhom { get; set; } = "";
            public int ThuTu { get; set; }
            public string Ten { get; set; } = "";
            public string? TenEn { get; set; }
            public string? MoTa { get; set; }
            public int? SoLuong { get; set; }
            public int? ChuanMoiNguoi { get; set; }
            public List<GanDto> DsGan { get; set; } = new();
            public int SoChinh { get; set; }
            public int SoDuPhong { get; set; }
            public decimal? TaiMoiNguoi { get; set; }
            public List<string> DsCanhBao { get; set; } = new();
        }

        public class NguoiDto
        {
            public string MaNv { get; set; } = "";
            public string HoTen { get; set; } = "";
            /// <summary>Đã ngừng đánh giá hoặc nghỉ nhưng còn giữ việc: phải bàn giao.</summary>
            public bool ConTrongDanhSach { get; set; }
            public int SoChinh { get; set; }
            public int SoDuPhong { get; set; }
            public int SoOverload { get; set; }
            public int SoChiMinhNam { get; set; }
        }

        public class MaTranDto
        {
            public List<DauViecDto> DsDauViec { get; set; } = new();
            public List<NguoiDto> DsNguoi { get; set; } = new();
            public int SoOverload { get; set; }
            public int SoMotNguoi { get; set; }
            public int SoKhongDuPhong { get; set; }
            public int SoChuaCoNguoi { get; set; }
            public int SoChuaCoChuan { get; set; }
        }

        public class DonNguoiDto
        {
            public string MaNv { get; set; } = "";
            public bool LaNguoiHoTro { get; set; }
            public int DuocGiao { get; set; }
            public int HoanTat { get; set; }
        }

        public class LuuOVao
        {
            public Guid IdDauViec { get; set; }
            public string? MaNv { get; set; }
            /// <summary>Rỗng = gỡ khỏi đầu việc.</summary>
            public string? VaiTro { get; set; }
        }

        public class LuuDauViecVao
        {
            public Guid? IdDauViec { get; set; }
            public string? Nhom { get; set; }
            public string? Ten { get; set; }
            public string? TenEn { get; set; }
            public string? MoTa { get; set; }
            public int? SoLuong { get; set; }
            public int? ChuanMoiNguoi { get; set; }
        }

        public class XoaDauViecVao
        {
            public Guid IdDauViec { get; set; }
        }

        #endregion

        /// <summary>Tính tải và cảnh báo của 1 đầu việc. Overload độc lập với các cảnh báo về số người nắm việc.</summary>
        public static void TinhCanhBao(DauViecDto d)
        {
            d.SoChinh = d.DsGan.Count(x => x.VaiTro == DgDauViecPhanCong.Chinh);
            d.SoDuPhong = d.DsGan.Count - d.SoChinh;
            d.TaiMoiNguoi = d.SoLuong != null && d.SoChinh > 0 ? Math.Round((decimal)d.SoLuong.Value / d.SoChinh, 1) : null;
            d.DsCanhBao.Clear();

            if (d.DsGan.Count == 0) d.DsCanhBao.Add(CbChuaCoNguoi);
            else if (d.DsGan.Count == 1) d.DsCanhBao.Add(CbMotNguoi);
            else if (d.SoChinh == 0) d.DsCanhBao.Add(CbKhongChinh);
            else if (d.SoDuPhong == 0) d.DsCanhBao.Add(CbKhongDuPhong);

            if (d.TaiMoiNguoi != null && d.ChuanMoiNguoi > 0 && d.TaiMoiNguoi > d.ChuanMoiNguoi) d.DsCanhBao.Add(CbOverload);
        }

        public async Task<MaTranDto> MaTranAsync()
        {
            var dsViec = await _context.DgDauViecs.AsNoTracking().Where(x => x.NgayXoa == null)
                .OrderBy(x => x.Nhom).ThenBy(x => x.ThuTu).ThenBy(x => x.Ten).ToListAsync();
            var idViec = dsViec.Select(x => x.IdDauViec).ToList();
            var dsGan = await _context.DgDauViecPhanCongs.AsNoTracking()
                .Where(x => x.NgayGo == null && idViec.Contains(x.IdDauViec))
                .Select(x => new { x.IdDauViec, x.MaNhanVien, x.VaiTro }).ToListAsync();

            var viec = dsViec.Select(v =>
            {
                var d = new DauViecDto
                {
                    IdDauViec = v.IdDauViec, Nhom = v.Nhom, ThuTu = v.ThuTu, Ten = v.Ten, TenEn = v.TenEn, MoTa = v.MoTa,
                    SoLuong = v.SoLuong, ChuanMoiNguoi = v.ChuanMoiNguoi,
                    DsGan = dsGan.Where(g => g.IdDauViec == v.IdDauViec).Select(g => new GanDto { MaNv = g.MaNhanVien, VaiTro = g.VaiTro }).ToList()
                };
                TinhCanhBao(d);
                return d;
            }).ToList();

            var dangDanhGia = (await _quanLy.DsMaNvAsync()).ToHashSet(StringComparer.OrdinalIgnoreCase);
            var dsMa = dangDanhGia.Concat(dsGan.Select(x => x.MaNhanVien)).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
            var ten = await _context.Users.AsNoTracking().Where(u => u.Tk != null && dsMa.Contains(u.Tk))
                .Select(u => new { u.Tk, u.HoTen }).ToListAsync();

            var nguoi = dsMa.Select(ma =>
            {
                var cua = viec.Where(v => v.DsGan.Any(g => g.MaNv.Equals(ma, StringComparison.OrdinalIgnoreCase))).ToList();
                bool LaChinh(DauViecDto v) => v.DsGan.Any(g => g.MaNv.Equals(ma, StringComparison.OrdinalIgnoreCase) && g.VaiTro == DgDauViecPhanCong.Chinh);
                return new NguoiDto
                {
                    MaNv = ma,
                    HoTen = ten.FirstOrDefault(x => ma.Equals(x.Tk, StringComparison.OrdinalIgnoreCase))?.HoTen ?? ma,
                    ConTrongDanhSach = dangDanhGia.Contains(ma),
                    SoChinh = cua.Count(LaChinh),
                    SoDuPhong = cua.Count(v => !LaChinh(v)),
                    SoOverload = cua.Count(v => LaChinh(v) && v.DsCanhBao.Contains(CbOverload)),
                    SoChiMinhNam = cua.Count(v => v.DsGan.Count == 1)
                };
            })
            .OrderByDescending(x => x.SoChinh + x.SoDuPhong > 0).ThenByDescending(x => x.SoChinh).ThenBy(x => x.HoTen)
            .ToList();

            return new MaTranDto
            {
                DsDauViec = viec,
                DsNguoi = nguoi,
                SoOverload = viec.Count(x => x.DsCanhBao.Contains(CbOverload)),
                SoMotNguoi = viec.Count(x => x.DsCanhBao.Contains(CbMotNguoi)),
                SoKhongDuPhong = viec.Count(x => x.DsCanhBao.Contains(CbKhongDuPhong)),
                SoChuaCoNguoi = viec.Count(x => x.DsCanhBao.Contains(CbChuaCoNguoi)),
                SoChuaCoChuan = viec.Count(x => x.SoLuong == null || x.ChuanMoiNguoi == null)
            };
        }

        /// <summary>Đơn IT thực tế từ đầu năm tới nay để đối chiếu với phân công trên giấy. Tách khỏi ma trận vì chạy chậm hơn nhiều.</summary>
        public async Task<List<DonNguoiDto>> DonTheoNguoiAsync(IEnumerable<string> dsMa)
        {
            var homNay = DateTime.Today;
            var tu = new DateTime(homNay.Year, 1, 1);
            var kq = new List<DonNguoiDto>();
            foreach (var ma in dsMa)
            {
                var don = await _soLieu.LayDonAsync(ma, tu, homNay.AddDays(1).AddTicks(-1));
                kq.Add(new DonNguoiDto { MaNv = ma, LaNguoiHoTro = don.LaNguoiHoTro, DuocGiao = don.DuocGiao, HoanTat = don.HoanTat });
            }
            return kq;
        }

        public async Task<string?> LuuOAsync(LuuOVao vao, string nguoi)
        {
            var ma = vao.MaNv?.Trim();
            if (string.IsNullOrEmpty(ma) || ma.Length > 20) return "Thiếu mã nhân viên.";
            var vaiTro = string.IsNullOrWhiteSpace(vao.VaiTro) ? null : vao.VaiTro.Trim();
            if (vaiTro is not (null or DgDauViecPhanCong.Chinh or DgDauViecPhanCong.DuPhong1 or DgDauViecPhanCong.DuPhong2))
                return "Vai trò không hợp lệ.";
            if (!await _context.DgDauViecs.AnyAsync(x => x.IdDauViec == vao.IdDauViec && x.NgayXoa == null)) return "Đầu việc không còn tồn tại.";

            await using var tx = await _context.Database.BeginTransactionAsync();
            var cu = await _context.DgDauViecPhanCongs.FirstOrDefaultAsync(x => x.IdDauViec == vao.IdDauViec && x.MaNhanVien == ma && x.NgayGo == null);
            if (cu?.VaiTro == vaiTro) return null;
            // Người đã rời danh sách vẫn gỡ được để bàn giao, nhưng không giao thêm việc mới
            if (vaiTro != null && !await _quanLy.TrongHeThongAsync(ma)) return $"{ma} không có trong danh sách đánh giá.";

            var bayGio = DateTime.Now;
            if (cu != null)
            {
                cu.NgayGo = bayGio;
                cu.NguoiGo = nguoi;
                await _context.SaveChangesAsync();
            }
            if (vaiTro != null)
            {
                _context.DgDauViecPhanCongs.Add(new DgDauViecPhanCong
                {
                    IdPhanCong = Guid.NewGuid(), IdDauViec = vao.IdDauViec, MaNhanVien = ma, VaiTro = vaiTro, NguoiGiao = nguoi, NgayGiao = bayGio
                });
                try
                {
                    await _context.SaveChangesAsync();
                }
                catch (DbUpdateException ex) when (ex.InnerException is SqlException sql && (sql.Number == 2601 || sql.Number == 2627))
                {
                    return "Ô này vừa được sửa ở nơi khác, tải lại để xem.";
                }
            }
            await tx.CommitAsync();
            return null;
        }

        public async Task<string?> LuuDauViecAsync(LuuDauViecVao vao, string nguoi)
        {
            var nhom = vao.Nhom?.Trim();
            var ten = vao.Ten?.Trim();
            if (string.IsNullOrEmpty(nhom)) return "Chưa nhập nhóm.";
            if (nhom.Length > 50) return "Nhóm tối đa 50 ký tự.";
            if (string.IsNullOrEmpty(ten)) return "Chưa nhập tên đầu việc.";
            if (ten.Length > 300) return "Tên đầu việc tối đa 300 ký tự.";
            var tenEn = string.IsNullOrWhiteSpace(vao.TenEn) ? null : vao.TenEn.Trim();
            if (tenEn?.Length > 300) return "Tên tiếng Anh tối đa 300 ký tự.";
            if (vao.SoLuong < 0 || vao.ChuanMoiNguoi <= 0) return "Số lượng không được âm, chuẩn mỗi người phải lớn hơn 0.";

            var bayGio = DateTime.Now;
            DgDauViec? d;
            if (vao.IdDauViec == null)
            {
                d = new DgDauViec
                {
                    IdDauViec = Guid.NewGuid(), NguoiTao = nguoi, NgayTao = bayGio,
                    ThuTu = (await _context.DgDauViecs.Where(x => x.Nhom == nhom && x.NgayXoa == null).MaxAsync(x => (int?)x.ThuTu) ?? 0) + 1
                };
                _context.DgDauViecs.Add(d);
            }
            else
            {
                d = await _context.DgDauViecs.FirstOrDefaultAsync(x => x.IdDauViec == vao.IdDauViec && x.NgayXoa == null);
                if (d == null) return "Đầu việc không còn tồn tại.";
                d.NguoiCapNhat = nguoi;
                d.NgayCapNhat = bayGio;
            }
            d.Nhom = nhom;
            d.Ten = ten;
            d.TenEn = tenEn;
            d.MoTa = string.IsNullOrWhiteSpace(vao.MoTa) ? null : vao.MoTa.Trim();
            d.SoLuong = vao.SoLuong;
            d.ChuanMoiNguoi = vao.ChuanMoiNguoi;
            try
            {
                await _context.SaveChangesAsync();
            }
            catch (DbUpdateException ex) when (ex.InnerException is SqlException sql && (sql.Number == 2601 || sql.Number == 2627))
            {
                _context.ChangeTracker.Clear();
                return "Nhóm này đã có đầu việc cùng tên.";
            }
            return null;
        }

        /// <summary>Xoá mềm đầu việc và gỡ luôn người đang giữ, để không còn tính vào tải của ai.</summary>
        public async Task<string?> XoaDauViecAsync(Guid idDauViec, string nguoi)
        {
            var bayGio = DateTime.Now;
            await using var tx = await _context.Database.BeginTransactionAsync();
            var soDong = await _context.DgDauViecs.Where(x => x.IdDauViec == idDauViec && x.NgayXoa == null)
                .ExecuteUpdateAsync(s => s.SetProperty(x => x.NgayXoa, bayGio).SetProperty(x => x.NguoiXoa, nguoi));
            if (soDong == 0) return "Đầu việc không còn tồn tại.";
            await _context.DgDauViecPhanCongs.Where(x => x.IdDauViec == idDauViec && x.NgayGo == null)
                .ExecuteUpdateAsync(s => s.SetProperty(x => x.NgayGo, bayGio).SetProperty(x => x.NguoiGo, nguoi));
            await tx.CommitAsync();
            return null;
        }
    }
}
