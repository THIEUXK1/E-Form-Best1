using E_Form_Best.Context;
using E_Form_Best.Models.ITForm;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace E_Form_Best.Areas.QLCongViec.Services
{
    /// <summary>
    /// Kế hoạch phát triển theo dõi được: Part 3 tách thành mục tiêu có hạn, tiến độ, kết quả; ô chữ Part 3 trong phiếu giữ nguyên.
    /// Mục tiêu kéo dài sang năm sau nên nội dung chỉ sửa khi kỳ tạo ra nó còn mở, còn tiến độ/kết quả cập nhật được cả sau khi đóng kỳ.
    /// Kỳ đang mở hiện kèm mục tiêu chưa xong của kỳ trước để xem lại kết quả.
    /// Gợi ý: năng lực có GAP âm → nội dung đào tạo trong DG_GoiYDaoTao. Buổi đào tạo vẫn tạo và duyệt ở module Đào tạo, ở đây chỉ gắn vào.
    /// </summary>
    public class DanhGiaMucTieuService
    {
        private const int DoDaiToiDa = 500;
        private const int SoDaoTaoToiDa = 100;

        private readonly ITFormContext _context;
        private readonly DanhGiaService _danhGia;
        private readonly DanhGiaNangLucService _nangLuc;

        public DanhGiaMucTieuService(ITFormContext context, DanhGiaService danhGia, DanhGiaNangLucService nangLuc)
        {
            _context = context;
            _danhGia = danhGia;
            _nangLuc = nangLuc;
        }

        #region DTO

        public class MucTieuDto
        {
            public Guid IdMucTieu { get; set; }
            public Guid IdKy { get; set; }
            public string? TenKy { get; set; }
            public bool TuKyTruoc { get; set; }
            public bool SuaNoiDung { get; set; }
            public string NoiDung { get; set; } = "";
            public string? KetQuaMongDoi { get; set; }
            public DateOnly? HanHoanThanh { get; set; }
            public int TienDo { get; set; }
            public string TrangThai { get; set; } = "";
            public string? KetQuaThucTe { get; set; }
            public Guid? IdNangLuc { get; set; }
            public string? TenNangLuc { get; set; }
            public int? IdDaoTao { get; set; }
            public string? TenDaoTao { get; set; }
            public string? TrangThaiDaoTao { get; set; }
            public bool DaDuDaoTao { get; set; }
        }

        public class NoiDungGoiY
        {
            public string NoiDung { get; set; } = "";
            public string? NguoiDaoTao { get; set; }
        }

        public class GoiYDto
        {
            public Guid IdNangLuc { get; set; }
            public string Ma { get; set; } = "";
            public string Ten { get; set; } = "";
            public decimal Gap { get; set; }
            public bool DaCoMucTieu { get; set; }
            public List<NoiDungGoiY> DsNoiDung { get; set; } = new();
        }

        public class LuaChonDaoTao
        {
            public int Id { get; set; }
            public string Ten { get; set; } = "";
            public DateOnly? Ngay { get; set; }
            public string TrangThai { get; set; } = "";
        }

        public class KeHoachDto
        {
            public DanhGiaService.KyDto Ky { get; set; } = null!;
            public string MaNv { get; set; } = "";
            public string HoTen { get; set; } = "";
            public List<MucTieuDto> DsMucTieu { get; set; } = new();
            public List<GoiYDto> DsGoiY { get; set; } = new();
            // Tên kỳ cũ làm nguồn gợi ý khi kỳ đang xem chưa có điểm; null = gợi ý theo chính kỳ này
            public string? GoiYTuKy { get; set; }
            public List<LuaChonDaoTao> DsDaoTao { get; set; } = new();
        }

        public class LuuVao
        {
            public string? MaNv { get; set; }
            public Guid IdKy { get; set; }
            // null = mục tiêu mới
            public Guid? IdMucTieu { get; set; }
            // Sinh ở trình duyệt lúc mở form, mục tiêu mới mới dùng
            public Guid? MaGui { get; set; }
            public string? NoiDung { get; set; }
            public string? KetQuaMongDoi { get; set; }
            public DateOnly? HanHoanThanh { get; set; }
            public int TienDo { get; set; }
            public string? TrangThai { get; set; }
            public string? KetQuaThucTe { get; set; }
            public Guid? IdNangLuc { get; set; }
            public int? IdDaoTao { get; set; }
        }

        public class XoaVao
        {
            public string? MaNv { get; set; }
            public Guid IdMucTieu { get; set; }
        }

        #endregion

        #region Đọc

        public async Task<KeHoachDto> LayAsync(string maNv, DanhGiaService.KyDto ky, DanhGiaNangLucService.KetQuaNguoi nangLuc)
        {
            var idKyMo = await _danhGia.IdKyDangMoAsync();
            var ds = await _context.DgMucTieuPhatTriens.AsNoTracking()
                .Where(x => x.MaNhanVien == maNv && x.NgayXoa == null
                            && (x.IdKy == ky.IdKy || (ky.DangMo && x.TrangThai == DgMucTieuPhatTrien.DangLam)))
                .OrderBy(x => x.TrangThai == DgMucTieuPhatTrien.DangLam ? 0 : 1).ThenBy(x => x.HanHoanThanh == null).ThenBy(x => x.HanHoanThanh).ThenBy(x => x.NgayTao)
                .ToListAsync();

            var dsIdKy = ds.Select(x => x.IdKy).Distinct().ToList();
            var tenKy = await _context.DgKyDanhGias.AsNoTracking().Where(x => dsIdKy.Contains(x.IdKy)).ToDictionaryAsync(x => x.IdKy, x => x.Ten);
            var dsIdNl = ds.Where(x => x.IdNangLuc != null).Select(x => x.IdNangLuc!.Value).Distinct().ToList();
            var tenNl = await _context.DgNangLucs.AsNoTracking().Where(x => dsIdNl.Contains(x.IdNangLuc)).ToDictionaryAsync(x => x.IdNangLuc, x => x.MaNangLuc + " " + x.TenNangLuc);
            var dsIdDt = ds.Where(x => x.IdDaoTao != null).Select(x => x.IdDaoTao!.Value).Distinct().ToList();
            var daoTao = await _context.DaoTaos.AsNoTracking().Where(x => dsIdDt.Contains(x.Id)).Select(x => new { x.Id, x.TenDaoTao, x.TrangThai }).ToDictionaryAsync(x => x.Id);
            var idNguoiDung = await _context.Users.AsNoTracking().Where(u => u.Tk == maNv).Select(u => (int?)u.IdNguoiDung).FirstOrDefaultAsync();
            var daDu = idNguoiDung == null ? new List<int>()
                : await _context.DaoTaoNguoiThamGias.AsNoTracking().Where(x => x.IdNguoiDung == idNguoiDung && x.CoMat && dsIdDt.Contains(x.IdDaoTao)).Select(x => x.IdDaoTao).ToListAsync();

            var kq = new KeHoachDto
            {
                Ky = ky,
                MaNv = maNv,
                HoTen = (await _danhGia.LayThongTinAsync(maNv)).HoTen,
                DsMucTieu = ds.Select(x => new MucTieuDto
                {
                    IdMucTieu = x.IdMucTieu, IdKy = x.IdKy, TenKy = tenKy.GetValueOrDefault(x.IdKy), TuKyTruoc = x.IdKy != ky.IdKy,
                    SuaNoiDung = x.IdKy == idKyMo,
                    NoiDung = x.NoiDung, KetQuaMongDoi = x.KetQuaMongDoi, HanHoanThanh = x.HanHoanThanh, TienDo = x.TienDo, TrangThai = x.TrangThai,
                    KetQuaThucTe = x.KetQuaThucTe, IdNangLuc = x.IdNangLuc,
                    TenNangLuc = x.IdNangLuc != null ? tenNl.GetValueOrDefault(x.IdNangLuc.Value) : null,
                    IdDaoTao = x.IdDaoTao,
                    TenDaoTao = x.IdDaoTao != null && daoTao.TryGetValue(x.IdDaoTao.Value, out var dt) ? dt.TenDaoTao : null,
                    TrangThaiDaoTao = x.IdDaoTao != null && daoTao.TryGetValue(x.IdDaoTao.Value, out var dt2) ? dt2.TrangThai : null,
                    DaDuDaoTao = x.IdDaoTao != null && daDu.Contains(x.IdDaoTao.Value)
                }).ToList(),
                DsDaoTao = await _context.DaoTaos.AsNoTracking()
                    .Where(x => x.TrangThai != "Huy")
                    .OrderByDescending(x => x.Id)
                    .Take(SoDaoTaoToiDa)
                    .Select(x => new LuaChonDaoTao { Id = x.Id, Ten = x.TenDaoTao, Ngay = x.NgayDaoTao, TrangThai = x.TrangThai })
                    .ToListAsync()
            };
            if (!ky.ThangCu) kq.DsGoiY = await LayGoiYAsync(nangLuc, ds);
            if (ky.DangMo && !nangLuc.DsNangLuc.Any(x => x.DiemQuanLy != null || x.DiemTu != null))
                await GoiYTheoKyTruocAsync(kq, ds);
            return kq;
        }

        // Đầu kỳ chưa ai chấm: lập kế hoạch dựa trên kết quả kỳ đã đóng gần nhất, không thì trang trống đúng lúc cần đặt mục tiêu
        private async Task GoiYTheoKyTruocAsync(KeHoachDto kq, List<DgMucTieuPhatTrien> ds)
        {
            foreach (var k in (await _danhGia.DsKyAsync()).Where(x => !x.DangMo && !x.ThangCu))
            {
                var cu = await _nangLuc.LayCuaNguoiAsync(kq.MaNv, k);
                if (!cu.DsNangLuc.Any(x => x.DiemQuanLy != null || x.DiemTu != null)) continue;
                kq.DsGoiY = await LayGoiYAsync(cu, ds);
                kq.GoiYTuKy = k.Ten;
                return;
            }
        }

        /// <summary>Năng lực có GAP âm, thấp nhất lên đầu. Dùng điểm quản lý, chưa có thì điểm tự chấm để nhân viên vẫn có gợi ý khi đang chấm.</summary>
        private async Task<List<GoiYDto>> LayGoiYAsync(DanhGiaNangLucService.KetQuaNguoi nangLuc, List<DgMucTieuPhatTrien> dsMucTieu)
        {
            var am = nangLuc.DsNangLuc
                .Select(x => new { x.IdNangLuc, x.Ma, x.Ten, Diem = x.DiemQuanLy ?? x.DiemTu, x.YeuCau })
                .Where(x => x.Diem != null && x.Diem < x.YeuCau)
                .ToList();
            if (am.Count == 0) return new();

            var dsId = am.Select(x => x.IdNangLuc).ToList();
            var noiDung = (await _context.DgGoiYDaoTaos.AsNoTracking().Where(x => dsId.Contains(x.IdNangLuc)).ToListAsync()).ToLookup(x => x.IdNangLuc);
            var daCo = dsMucTieu.Where(x => x.IdNangLuc != null && x.TrangThai != DgMucTieuPhatTrien.DaHuy).Select(x => x.IdNangLuc!.Value).ToHashSet();
            return am
                .Select(x => new GoiYDto
                {
                    IdNangLuc = x.IdNangLuc, Ma = x.Ma, Ten = x.Ten, Gap = x.Diem!.Value - x.YeuCau, DaCoMucTieu = daCo.Contains(x.IdNangLuc),
                    DsNoiDung = noiDung[x.IdNangLuc].Select(n => new NoiDungGoiY { NoiDung = n.NoiDung, NguoiDaoTao = n.NguoiDaoTao }).ToList()
                })
                .OrderBy(x => x.Gap).ThenBy(x => x.Ma)
                .ToList();
        }

        #endregion

        #region Ghi

        /// <summary>Trả null nếu hợp lệ, ngược lại là thông báo lỗi. Mục tiêu phải thuộc đúng maNv (chống IDOR theo id gửi lên).</summary>
        public async Task<string?> LuuAsync(string maNv, LuuVao vao, string nguoiThucHien)
        {
            var noiDung = vao.NoiDung?.Trim();
            var mongDoi = string.IsNullOrWhiteSpace(vao.KetQuaMongDoi) ? null : vao.KetQuaMongDoi.Trim();
            if (string.IsNullOrEmpty(noiDung)) return "Nhập nội dung mục tiêu.";
            if (noiDung.Length > DoDaiToiDa || mongDoi?.Length > DoDaiToiDa) return $"Nội dung tối đa {DoDaiToiDa} ký tự.";
            if (vao.TienDo is < 0 or > 100) return "Tiến độ phải từ 0 đến 100.";
            var trangThai = vao.TrangThai ?? DgMucTieuPhatTrien.DangLam;
            if (trangThai is not (DgMucTieuPhatTrien.DangLam or DgMucTieuPhatTrien.HoanThanh or DgMucTieuPhatTrien.DaHuy)) return "Trạng thái không hợp lệ.";
            if (vao.IdNangLuc != null && !await _context.DgNangLucs.AnyAsync(x => x.IdNangLuc == vao.IdNangLuc)) return "Năng lực không tồn tại.";
            if (vao.IdDaoTao != null && !await _context.DaoTaos.AnyAsync(x => x.Id == vao.IdDaoTao)) return "Buổi đào tạo không tồn tại.";

            // Hoàn thành và 100% luôn đi cùng nhau, thống kê không gặp mục tiêu 100% vẫn "đang làm"
            var tienDo = (byte)vao.TienDo;
            if (trangThai == DgMucTieuPhatTrien.HoanThanh) tienDo = 100;
            else if (tienDo == 100 && trangThai == DgMucTieuPhatTrien.DangLam) trangThai = DgMucTieuPhatTrien.HoanThanh;

            var idKyMo = await _danhGia.IdKyDangMoAsync();
            var bayGio = DateTime.Now;
            DgMucTieuPhatTrien? mt;
            if (vao.IdMucTieu == null)
            {
                if (vao.IdKy != idKyMo) return DanhGiaService.LoiKyDong;
                if (vao.MaGui != null && await _context.DgMucTieuPhatTriens.AnyAsync(x => x.MaGui == vao.MaGui)) return null;
                mt = new DgMucTieuPhatTrien
                {
                    IdMucTieu = Guid.NewGuid(), IdKy = vao.IdKy, MaNhanVien = maNv, MaGui = vao.MaGui, NguoiTao = nguoiThucHien, NgayTao = bayGio
                };
                _context.DgMucTieuPhatTriens.Add(mt);
            }
            else
            {
                mt = await _context.DgMucTieuPhatTriens.FirstOrDefaultAsync(x => x.IdMucTieu == vao.IdMucTieu && x.MaNhanVien == maNv && x.NgayXoa == null);
                if (mt == null) return "Không tìm thấy mục tiêu.";
                mt.NguoiCapNhat = nguoiThucHien;
                mt.NgayCapNhat = bayGio;
            }

            if (vao.IdMucTieu == null || mt.IdKy == idKyMo)
            {
                mt.NoiDung = noiDung;
                mt.KetQuaMongDoi = mongDoi;
                mt.HanHoanThanh = vao.HanHoanThanh;
                mt.IdNangLuc = vao.IdNangLuc;
            }
            mt.IdDaoTao = vao.IdDaoTao;
            mt.TienDo = tienDo;
            mt.TrangThai = trangThai;
            mt.KetQuaThucTe = DanhGiaService.CatChu(vao.KetQuaThucTe);

            try
            {
                await _context.SaveChangesAsync();
            }
            catch (DbUpdateException ex) when (ex.InnerException is SqlException sql && (sql.Number == 2601 || sql.Number == 2627))
            {
                // Hai request cùng MaGui: request thua coi như thành công
                _context.Entry(mt).State = EntityState.Detached;
            }
            return null;
        }

        public async Task<string?> XoaAsync(string maNv, Guid idMucTieu, string nguoiThucHien)
        {
            var mt = await _context.DgMucTieuPhatTriens.FirstOrDefaultAsync(x => x.IdMucTieu == idMucTieu && x.MaNhanVien == maNv && x.NgayXoa == null);
            if (mt == null) return "Không tìm thấy mục tiêu.";
            if (mt.IdKy != await _danhGia.IdKyDangMoAsync()) return "Mục tiêu của kỳ đã đóng không xoá được — chuyển trạng thái Huỷ nếu không làm nữa.";
            mt.NgayXoa = DateTime.Now;
            mt.NguoiXoa = nguoiThucHien;
            await _context.SaveChangesAsync();
            return null;
        }

        #endregion
    }
}
