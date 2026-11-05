using E_Form_Best.Context;
using E_Form_Best.Models.ITForm;
using Microsoft.EntityFrameworkCore;

namespace E_Form_Best.Areas.QLCongViec.Services
{
    /// <summary>
    /// Số liệu thực tế hiện cạnh Part 1 để người chấm đối chiếu. Chỉ tham khảo, không tự vào điểm:
    /// đơn được giao nhiều hay ít còn tuỳ phân công, người chấm mới biết cách đọc.
    /// Đơn, hài lòng, đào tạo tính theo từng người; sự cố và uptime là số toàn hệ thống vì
    /// KK_HeThongSuCo ghi trách nhiệm theo nhóm, KK_Server không có người phụ trách.
    /// </summary>
    public class DanhGiaSoLieuService
    {
        // Tên AutoRatingWorker ghi: đơn quá 1 tháng chưa ai chấm thì tự cho 5 sao, không phản ánh ý kiến người dùng
        private const string NguoiChamTuDong = "Hệ thống (Auto)";
        private const string DonDaHuy = "DaHuy";
        private static readonly string[] TrangThaiDaoTaoHopLe = { "DaDuyet", "HoanTat", "DaHoanTat" };

        // Máy dev ghi DOWN giả cho NAS (02/11 → 05/11 09:40, NAS không lỗi). LichSu là append-only nên loại khi tính
        private const string ServerGhiGia = "BPVN-NAS";
        private static readonly DateTime GhiGiaTu = new(2026, 11, 2);
        private static readonly DateTime GhiGiaDen = new(2026, 11, 5, 9, 41, 0);

        private readonly ITFormContext _context;

        public DanhGiaSoLieuService(ITFormContext context)
        {
            _context = context;
        }

        #region DTO

        public class DonDto
        {
            public bool LaNguoiHoTro { get; set; }
            public int DuocGiao { get; set; }
            public int HoanTat { get; set; }
            public int ChuaXong { get; set; }
            public decimal? GioTrungVi { get; set; }
            public decimal? TyLeTrong24Gio { get; set; }
        }

        public class HaiLongDto
        {
            public int SoDanhGia { get; set; }
            public decimal? DiemTrungBinh { get; set; }
            public int SoTuDong { get; set; }
        }

        public class DaoTaoDto
        {
            public int SoBuoiGiangDay { get; set; }
            public int SoBuoiThamGia { get; set; }
        }

        public class ServerThapDto
        {
            public string Ten { get; set; } = "";
            public decimal Uptime { get; set; }
        }

        public class HeThongDto
        {
            public int SoSuCo { get; set; }
            public int DangMo { get; set; }
            public decimal? GioKhacPhucTrungBinh { get; set; }
            public decimal? Uptime { get; set; }
            public int SoServer { get; set; }
            public DateTime? DoTuNgay { get; set; }
            public List<ServerThapDto> ThapNhat { get; set; } = new();
        }

        public class SoLieuDto
        {
            public DateOnly TuNgay { get; set; }
            public DateOnly DenNgay { get; set; }
            public DonDto Don { get; set; } = new();
            public HaiLongDto HaiLong { get; set; } = new();
            public DaoTaoDto DaoTao { get; set; } = new();
            public HeThongDto HeThong { get; set; } = new();
        }

        #endregion

        /// <summary>Mặc định cả năm dương lịch của kỳ, tính đến hôm nay. Ngày bắt đầu kỳ là lúc mở phiếu, không phải đầu giai đoạn được đánh giá.</summary>
        public static (DateOnly Tu, DateOnly Den) KhoangMacDinh(DanhGiaService.KyDto ky)
        {
            var homNay = DateOnly.FromDateTime(DateTime.Today);
            var nam = ky.NgayBatDau?.Year ?? homNay.Year;
            var cuoiNam = new DateOnly(nam, 12, 31);
            return (new DateOnly(nam, 1, 1), cuoiNam < homNay ? cuoiNam : homNay);
        }

        public async Task<SoLieuDto> LayAsync(string maNv, DateOnly tuNgay, DateOnly denNgay)
        {
            var tu = tuNgay.ToDateTime(TimeOnly.MinValue);
            var den = denNgay.ToDateTime(TimeOnly.MaxValue);
            var (don, hoTro) = await LayDonVaIdAsync(maNv, tu, den);
            return new SoLieuDto
            {
                TuNgay = tuNgay,
                DenNgay = denNgay,
                Don = don,
                HaiLong = await LayHaiLongAsync(hoTro),
                DaoTao = await LayDaoTaoAsync(maNv, tuNgay, denNgay, tu, den),
                HeThong = await LayHeThongAsync(tu, den)
            };
        }

        public async Task<DonDto> LayDonAsync(string maNv, DateTime tu, DateTime den)
            => (await LayDonVaIdAsync(maNv, tu, den)).Don;

        private static decimal? TrungVi(List<double> ds)
        {
            if (ds.Count == 0) return null;
            ds.Sort();
            var giua = ds.Count / 2;
            var v = ds.Count % 2 == 1 ? ds[giua] : (ds[giua - 1] + ds[giua]) / 2;
            return Math.Round((decimal)v, 1);
        }

        // Đơn nhiều người hỗ trợ được tính cho từng người. Thời gian đo từ lúc duyệt vì đơn không có mốc "tiếp nhận"
        private async Task<(DonDto Don, List<int> DsIdDon)> LayDonVaIdAsync(string maNv, DateTime tu, DateTime den)
        {
            var kq = new DonDto
            {
                // MaNV trong IT_NguoiHoTro có dòng thừa dấu cách đầu
                LaNguoiHoTro = await _context.ItNguoiHoTros.AsNoTracking().AnyAsync(n => n.MaNv != null && n.MaNv.Trim() == maNv)
            };
            if (!kq.LaNguoiHoTro) return (kq, new());

            var ds = await (
                from c in _context.ItCtNguoiHoTros
                join f in _context.FormIts on c.IdFormIt equals f.Id
                join n in _context.ItNguoiHoTros on c.IdItNguoiHoTro equals n.Id
                where n.MaNv != null && n.MaNv.Trim() == maNv
                      && f.TimeNguoiTao >= tu && f.TimeNguoiTao <= den
                      && f.TrangThai != DonDaHuy
                select new { f.Id, f.IdAdmin, f.TimeNguoiDuyet, f.TimeAdmin }
            ).AsNoTracking().Distinct().ToListAsync();

            kq.DuocGiao = ds.Count;
            kq.HoanTat = ds.Count(x => x.IdAdmin != null);
            kq.ChuaXong = kq.DuocGiao - kq.HoanTat;
            var gio = ds.Where(x => x.IdAdmin != null && x.TimeNguoiDuyet != null && x.TimeAdmin != null)
                .Select(x => (x.TimeAdmin!.Value - x.TimeNguoiDuyet!.Value).TotalHours)
                .Where(x => x >= 0).ToList();
            if (gio.Count > 0)
                kq.TyLeTrong24Gio = Math.Round((decimal)gio.Count(x => x <= 24) * 100 / gio.Count, 1);
            kq.GioTrungVi = TrungVi(gio);
            return (kq, ds.Select(x => x.Id).ToList());
        }

        private async Task<HaiLongDto> LayHaiLongAsync(List<int> dsIdDon)
        {
            var kq = new HaiLongDto();
            if (dsIdDon.Count == 0) return kq;
            var ds = await _context.DanhGiaFormIts.AsNoTracking()
                .Where(x => x.IdFormIt != null && dsIdDon.Contains(x.IdFormIt.Value))
                .Select(x => new { x.TenNguoiDanhGia, x.MucDo })
                .ToListAsync();
            var that = ds.Where(x => x.TenNguoiDanhGia != NguoiChamTuDong && x.MucDo != null).ToList();
            kq.SoTuDong = ds.Count - that.Count;
            kq.SoDanhGia = that.Count;
            if (that.Count > 0) kq.DiemTrungBinh = Math.Round((decimal)that.Average(x => x.MucDo!.Value), 2);
            return kq;
        }

        private async Task<DaoTaoDto> LayDaoTaoAsync(string maNv, DateOnly tuNgay, DateOnly denNgay, DateTime tu, DateTime den)
        {
            var trongKhoang = _context.DaoTaos.AsNoTracking().Where(d => TrangThaiDaoTaoHopLe.Contains(d.TrangThai)
                && (d.NgayDaoTao != null ? d.NgayDaoTao >= tuNgay && d.NgayDaoTao <= denNgay : d.TimeNguoiTao >= tu && d.TimeNguoiTao <= den));

            // NguoiDaoTao là chữ tự do dạng "V240298 - Bùi Văn Thiều"
            var kq = new DaoTaoDto { SoBuoiGiangDay = await trongKhoang.CountAsync(d => d.NguoiDaoTao != null && d.NguoiDaoTao.Contains(maNv)) };
            var idNguoiDung = await _context.Users.AsNoTracking().Where(u => u.Tk == maNv).Select(u => (int?)u.IdNguoiDung).FirstOrDefaultAsync();
            if (idNguoiDung != null)
                kq.SoBuoiThamGia = await trongKhoang.CountAsync(d => _context.DaoTaoNguoiThamGias.Any(t => t.IdDaoTao == d.Id && t.IdNguoiDung == idNguoiDung && t.CoMat));
            return kq;
        }

        private async Task<HeThongDto> LayHeThongAsync(DateTime tu, DateTime den)
        {
            var kq = new HeThongDto();
            var suCo = await _context.KkHeThongSuCos.AsNoTracking()
                .Where(x => x.NgayXoa == null && x.BatDauLuc >= tu && x.BatDauLuc <= den)
                .Select(x => new { x.TrangThai, x.BatDauLuc, x.KetThucLuc })
                .ToListAsync();
            kq.SoSuCo = suCo.Count;
            kq.DangMo = suCo.Count(x => x.TrangThai != KkHeThongSuCo.DaKhacPhuc);
            var dong = suCo.Where(x => x.KetThucLuc != null).Select(x => (x.KetThucLuc!.Value - x.BatDauLuc).TotalHours).ToList();
            if (dong.Count > 0) kq.GioKhacPhucTrungBinh = Math.Round((decimal)dong.Average(), 1);

            var dsServer = await _context.KkServers.AsNoTracking()
                .Where(x => x.NgayXoa == null && x.TrangThaiKetNoi != null)
                .Select(x => new { x.IdServer, x.TenServer })
                .ToListAsync();
            var lichSu = await _context.KkServerLichSus.AsNoTracking()
                .Where(x => x.ThoiGian >= tu && x.ThoiGian <= den)
                .OrderBy(x => x.ThoiGian)
                .Select(x => new { x.IdServer, x.TenServer, x.ThoiGian, x.TuTrangThai, x.SangTrangThai, x.ThoiLuongGiay })
                .ToListAsync();
            lichSu = lichSu.Where(x => !(x.TenServer == ServerGhiGia && x.ThoiGian >= GhiGiaTu && x.ThoiGian < GhiGiaDen)).ToList();
            kq.SoServer = dsServer.Count;
            if (dsServer.Count == 0) return kq;

            // Nhật ký chỉ có từ 15/10/2026: mẫu số tính từ dòng đầu tiên, không phải đầu khoảng chọn
            var dauDo = await _context.KkServerLichSus.AsNoTracking().MinAsync(x => (DateTime?)x.ThoiGian);
            if (dauDo == null) return kq;
            var batDau = dauDo > tu ? dauDo.Value : tu;
            var ketThuc = den < DateTime.Now ? den : DateTime.Now;
            var tongGiay = (ketThuc - batDau).TotalSeconds;
            if (tongGiay <= 0) return kq;
            kq.DoTuNgay = batDau;

            double tongDown = 0;
            var theoServer = new List<ServerThapDto>();
            foreach (var s in dsServer)
            {
                var ds = lichSu.Where(x => x.IdServer == s.IdServer).ToList();
                double down = ds.Where(x => x.TuTrangThai == "DOWN").Sum(x => (double)(x.ThoiLuongGiay ?? 0));
                // Đang DOWN ở cuối khoảng thì chưa có dòng UP ghi thời lượng
                var cuoi = ds.LastOrDefault();
                if (cuoi?.SangTrangThai == "DOWN") down += (ketThuc - cuoi.ThoiGian).TotalSeconds;
                tongDown += down;
                if (down > 0) theoServer.Add(new ServerThapDto { Ten = s.TenServer, Uptime = Math.Round((decimal)(100 - down * 100 / tongGiay), 2) });
            }
            kq.Uptime = Math.Round((decimal)(100 - tongDown * 100 / (tongGiay * dsServer.Count)), 2);
            kq.ThapNhat = theoServer.OrderBy(x => x.Uptime).Take(3).ToList();
            return kq;
        }
    }
}
