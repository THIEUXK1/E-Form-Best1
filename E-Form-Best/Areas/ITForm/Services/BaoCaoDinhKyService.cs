using E_Form_Best.Context;
using Microsoft.EntityFrameworkCore;

namespace E_Form_Best.Areas.ITForm.Services
{
    /// <summary>
    /// Số liệu cho file Excel tổng hợp của "Báo cáo định kì" (/BaoCaoDinhKy) ở các mục chưa có service báo cáo riêng:
    /// thống kê đơn IT, thống kê đơn công việc, tổng quan thiết bị kiểm kê.
    /// Cách tính khớp với trang đang hiển thị (BaoCaoThongKe.cshtml của 2 loại đơn, kiemke-tong-quan.js).
    /// </summary>
    public class BaoCaoDinhKyService
    {
        private readonly ITFormContext _context;

        public BaoCaoDinhKyService(ITFormContext context) => _context = context;

        // Thứ tự trạng thái giống biểu đồ trên trang thống kê
        public static readonly string[] DsTrangThaiDon = { "CHỜ QL", "ĐANG XỬ LÝ", "ĐÁNH GIÁ", "HOÀN TẤT", "HỦY" };

        public record Dem(string Ten, int SoLuong);

        public record NguoiHoTroDong(string Ten, int Tong, IReadOnlyDictionary<string, int> TheoTrangThai, double? PhutTrungBinh);

        public class ThongKeDon
        {
            public int Tong { get; set; }
            /// <summary>Chỉ đơn công việc có hạn hoàn thành; đơn IT = null.</summary>
            public int? QuaHan { get; set; }
            public List<Dem> TheoTrangThai { get; set; } = new();
            public List<Dem> TheoBoPhan { get; set; } = new();
            public List<Dem> TheoDanhMuc { get; set; } = new();
            public List<Dem> TheoMaForm { get; set; } = new();
            public List<NguoiHoTroDong> TheoNguoiHoTro { get; set; } = new();
        }

        public class ThietBiCongTy
        {
            public string CongTy { get; set; } = "";
            public int Tong { get; set; }
            public int HoatDong { get; set; }
            public int Hong { get; set; }
            public int BaoTri { get; set; }
            public int KhoIt { get; set; }
            public int Khac { get; set; }
            public int ChuaCapPhat { get; set; }
            public int HetBaoHanh { get; set; }
            public int SapHetBaoHanh { get; set; }
            public int ChuaKiem { get; set; }
            public int LauChuaKiem { get; set; }
        }

        public class ThongKeThietBi
        {
            public List<ThietBiCongTy> CongTy { get; set; } = new();
            public List<Dem> TheoLoai { get; set; } = new();
            public List<(string CongTy, string BoPhan, int Tong, int HoatDong, int Hong)> TheoBoPhan { get; set; } = new();
        }

        private static string TrangThaiDon(string? tenForm, int? idAdmin, int? idNguoiDuyet, bool daDanhGia) =>
            (tenForm ?? "").Contains("[ĐÃ HỦY]") ? "HỦY" :
            idAdmin != null && daDanhGia ? "HOÀN TẤT" :
            idAdmin != null ? "ĐÁNH GIÁ" :
            idNguoiDuyet != null ? "ĐANG XỬ LÝ" : "CHỜ QL";

        private static List<Dem> DemTheo<T>(IEnumerable<T> ds, Func<T, string?> khoa) => ds
            .GroupBy(x => string.IsNullOrWhiteSpace(khoa(x)) ? "Chưa xác định" : khoa(x)!.Trim())
            .Select(g => new Dem(g.Key, g.Count()))
            .OrderByDescending(x => x.SoLuong).ThenBy(x => x.Ten)
            .ToList();

        private record DonTho(int Id, string? IdForm, string? DanhMuc, string? BoPhan, string? TenForm, int? IdNguoiDuyet, int? IdAdmin,
            bool DaDanhGia, bool QuaHan);

        private record HoTroTho(int? IdForm, int? Stt, string Ten, string? TenForm, int? IdNguoiDuyet, int? IdAdmin,
            DateTime? TimeNguoiDuyet, DateTime? TimeAdmin, bool DaDanhGia);

        private static ThongKeDon GomDon(List<DonTho> don, List<HoTroTho> hoTro, bool coQuaHan)
        {
            var kq = new ThongKeDon
            {
                Tong = don.Count,
                QuaHan = coQuaHan ? don.Count(x => x.QuaHan) : null,
                TheoBoPhan = DemTheo(don, x => x.BoPhan),
                TheoDanhMuc = DemTheo(don, x => x.DanhMuc),
                TheoMaForm = DemTheo(don, x => x.IdForm),
            };
            var demTt = don.GroupBy(x => TrangThaiDon(x.TenForm, x.IdAdmin, x.IdNguoiDuyet, x.DaDanhGia))
                .ToDictionary(g => g.Key, g => g.Count());
            kq.TheoTrangThai = DsTrangThaiDon.Select(t => new Dem(t, demTt.GetValueOrDefault(t))).ToList();

            // Mỗi đơn chỉ tính cho người hỗ trợ được gán sau cùng (Stt lớn nhất) — giống GetDataNguoiHoTro
            var cuoi = hoTro.GroupBy(x => x.IdForm).Select(g => g.OrderByDescending(x => x.Stt).First()).ToList();
            kq.TheoNguoiHoTro = cuoi
                .GroupBy(x => string.IsNullOrWhiteSpace(x.Ten) ? "Chưa xác định" : x.Ten.Trim())
                .Select(g =>
                {
                    var phut = g.Where(x => x.TimeAdmin != null && x.TimeNguoiDuyet != null)
                        .Select(x => (x.TimeAdmin!.Value - x.TimeNguoiDuyet!.Value).TotalMinutes).ToList();
                    var theoTt = g.GroupBy(x => TrangThaiDon(x.TenForm, x.IdAdmin, x.IdNguoiDuyet, x.DaDanhGia))
                        .ToDictionary(t => t.Key, t => t.Count());
                    return new NguoiHoTroDong(g.Key, g.Count(), theoTt, phut.Count > 0 ? phut.Average() : null);
                })
                .OrderByDescending(x => x.Tong).ThenBy(x => x.Ten)
                .ToList();
            return kq;
        }

        /// <summary>Kỳ này và kỳ trước (CameraBaoCaoService.KyTruoc) của cùng một loại số liệu.</summary>
        public class SoSanh<T>
        {
            public DateTime TuNgay { get; set; }
            public DateTime DenNgay { get; set; }
            public DateTime KyTruocTu { get; set; }
            public DateTime KyTruocDen { get; set; }
            public T Nay { get; set; } = default!;
            public T Truoc { get; set; } = default!;
        }

        /// <summary>Biến động thiết bị trong một kỳ — chỉ các số có mốc thời gian lưu trên KK_ThietBi.</summary>
        public class BienDongThietBi
        {
            public string CongTy { get; set; } = "";
            /// <summary>Thêm mới (NgayTao trong kỳ).</summary>
            public int ThemMoi { get; set; }
            /// <summary>Xoá (NgayXoa trong kỳ).</summary>
            public int DaXoa { get; set; }
            /// <summary>Lần kiểm kê gần nhất rơi vào kỳ — máy kiểm lại ở kỳ sau thì không còn tính cho kỳ trước.</summary>
            public int DaKiem { get; set; }
        }

        private static SoSanh<T> TaoSoSanh<T>(DateTime tuNgay, DateTime denNgay, T nay, T truoc)
        {
            var (tuTruoc, denTruoc) = CameraBaoCaoService.KyTruoc(tuNgay, denNgay);
            return new SoSanh<T> { TuNgay = tuNgay.Date, DenNgay = denNgay.Date, KyTruocTu = tuTruoc, KyTruocDen = denTruoc, Nay = nay, Truoc = truoc };
        }

        // Chạy tuần tự: dùng chung một ITFormContext (scoped)
        public async Task<SoSanh<ThongKeDon>> SoSanhDonItAsync(DateTime tuNgay, DateTime denNgay, CancellationToken ct)
        {
            var m = CameraBaoCaoService.MocSoSanh(tuNgay, denNgay, DateTime.Now);
            var nay = await DonItTheoMocAsync(m.tu, denNgay.Date.AddDays(1), ct);
            var truoc = await DonItTheoMocAsync(m.tuTruoc, m.cuoiTruoc, ct);
            return TaoSoSanh(tuNgay, denNgay, nay, truoc);
        }

        public async Task<SoSanh<ThongKeDon>> SoSanhDonCongViecAsync(DateTime tuNgay, DateTime denNgay, CancellationToken ct)
        {
            var m = CameraBaoCaoService.MocSoSanh(tuNgay, denNgay, DateTime.Now);
            var nay = await DonCongViecTheoMocAsync(m.tu, denNgay.Date.AddDays(1), ct);
            var truoc = await DonCongViecTheoMocAsync(m.tuTruoc, m.cuoiTruoc, ct);
            return TaoSoSanh(tuNgay, denNgay, nay, truoc);
        }

        public async Task<SoSanh<List<BienDongThietBi>>> SoSanhThietBiAsync(DateTime tuNgay, DateTime denNgay, CancellationToken ct)
        {
            var m = CameraBaoCaoService.MocSoSanh(tuNgay, denNgay, DateTime.Now);
            var ds = await BienDongThietBiAsync(m.tuTruoc, m.cuoi, ct);
            List<BienDongThietBi> Dem(DateTime tu, DateTime cuoi) => ds
                .GroupBy(x => x.CongTy)
                .Select(g => new BienDongThietBi
                {
                    CongTy = g.Key,
                    ThemMoi = g.Count(x => x.NgayTao >= tu && x.NgayTao < cuoi),
                    DaXoa = g.Count(x => x.NgayXoa >= tu && x.NgayXoa < cuoi),
                    DaKiem = g.Count(x => x.NgayXoa == null && x.ThoiGianCheck >= tu && x.ThoiGianCheck < cuoi),
                })
                .OrderBy(x => x.CongTy)
                .ToList();
            // Cả 2 kỳ liệt kê cùng danh sách công ty để ghép dòng
            return TaoSoSanh(tuNgay, denNgay, Dem(m.tu, m.cuoi), Dem(m.tuTruoc, m.cuoiTruoc));
        }

        private record ThietBiMoc(string CongTy, DateTime? NgayTao, DateTime? NgayXoa, DateTime? ThoiGianCheck);

        /// <summary>Thiết bị có ngày tạo / xoá / kiểm kê trong [tu, cuoi), bỏ thiết bị trong danh sách chặn (giống ThietBiAsync).</summary>
        private async Task<List<ThietBiMoc>> BienDongThietBiAsync(DateTime tu, DateTime cuoi, CancellationToken ct)
        {
            var ds = await _context.KkThietBis.AsNoTracking()
                .Where(x => (x.NgayTao >= tu && x.NgayTao < cuoi) || (x.NgayXoa >= tu && x.NgayXoa < cuoi)
                            || (x.ThoiGianCheck >= tu && x.ThoiGianCheck < cuoi))
                .Select(x => new
                {
                    x.TenMayTinh, x.Seribacode, x.NgayTao, x.NgayXoa, x.ThoiGianCheck,
                    CongTy = x.IdcongTyNavigation != null ? x.IdcongTyNavigation.TenCongTy : null,
                })
                .ToListAsync(ct);
            var biChan = await HamBiChanAsync(ct);
            return ds.Where(x => !biChan(x.Seribacode, x.TenMayTinh))
                .Select(x => new ThietBiMoc(string.IsNullOrWhiteSpace(x.CongTy) ? "Chưa xác định" : x.CongTy.Trim(),
                    x.NgayTao, x.NgayXoa, x.ThoiGianCheck))
                .ToList();
        }

        /// <summary>Đơn IT có thời điểm quản lý duyệt trong kỳ (cùng mốc TimeNguoiDuyet với trang /FormIT/BaoCaoThongKe).</summary>
        public Task<ThongKeDon> DonItAsync(DateTime tuNgay, DateTime denNgay, CancellationToken ct)
            => DonItTheoMocAsync(tuNgay.Date, denNgay.Date.AddDays(1), ct);

        /// <param name="tu">Mốc đầu (gồm).</param>
        /// <param name="den">Mốc cuối (loại trừ).</param>
        private async Task<ThongKeDon> DonItTheoMocAsync(DateTime tu, DateTime den, CancellationToken ct)
        {
            var don = await _context.FormIts.AsNoTracking()
                .Where(x => x.TimeNguoiDuyet >= tu && x.TimeNguoiDuyet < den)
                .Select(x => new DonTho(x.Id, x.IdForm, x.Danhmuc, x.BoPhan, x.TenForm, x.IdNguoiDuyet, x.IdAdmin,
                    x.DanhGiaFormIts.Any(), false))
                .ToListAsync(ct);

            var hoTro = await _context.ItCtNguoiHoTros.AsNoTracking()
                .Where(x => x.IdFormItNavigation != null && x.IdFormItNavigation.TimeNguoiDuyet >= tu && x.IdFormItNavigation.TimeNguoiDuyet < den)
                .Select(x => new HoTroTho(x.IdFormIt, x.Stt,
                    x.IdItNguoiHoTroNavigation != null ? x.IdItNguoiHoTroNavigation.Ten ?? "" : "Chưa xác định",
                    x.IdFormItNavigation!.TenForm, x.IdFormItNavigation.IdNguoiDuyet, x.IdFormItNavigation.IdAdmin,
                    x.IdFormItNavigation.TimeNguoiDuyet, x.IdFormItNavigation.TimeAdmin, x.IdFormItNavigation.DanhGiaFormIts.Any()))
                .ToListAsync(ct);

            return GomDon(don, hoTro, coQuaHan: false);
        }

        /// <summary>Đơn công việc duyệt trong kỳ (cùng cách tính với /FormCongViec/BaoCaoThongKeCongViec).</summary>
        public Task<ThongKeDon> DonCongViecAsync(DateTime tuNgay, DateTime denNgay, CancellationToken ct)
            => DonCongViecTheoMocAsync(tuNgay.Date, denNgay.Date.AddDays(1), ct);

        /// <param name="tu">Mốc đầu (gồm).</param>
        /// <param name="den">Mốc cuối (loại trừ).</param>
        private async Task<ThongKeDon> DonCongViecTheoMocAsync(DateTime tu, DateTime den, CancellationToken ct)
        {
            var bayGio = DateTime.Now;

            var don = await _context.FormCongViecs.AsNoTracking()
                .Where(x => x.TimeNguoiDuyet >= tu && x.TimeNguoiDuyet < den)
                .Select(x => new DonTho(x.Id, x.IdForm, x.Danhmuc, x.BoPhan, x.TenForm, x.IdNguoiDuyet, x.IdAdmin,
                    _context.DanhGiaFormCongViecs.Any(dg => dg.IdFormCongViec == x.Id),
                    x.IdAdmin == null
                        && (x.TenForm == null || !x.TenForm.Contains("[ĐÃ HỦY]"))
                        && x.CvCongViecOrder1s.Any(o => o.ThoiHanHoanThanh != null && o.ThoiHanHoanThanh < bayGio)))
                .ToListAsync(ct);

            var hoTro = await _context.FormCongViecNguoiLienQuans.AsNoTracking()
                .Where(x => x.IdFormCongViecNavigation != null && x.IdFormCongViecNavigation.TimeNguoiDuyet >= tu && x.IdFormCongViecNavigation.TimeNguoiDuyet < den)
                .Select(x => new HoTroTho(x.IdFormCongViec, x.Id,
                    x.IdNguoiDungNavigation != null ? x.IdNguoiDungNavigation.HoTen ?? "" : "Chưa xác định",
                    x.IdFormCongViecNavigation!.TenForm, x.IdFormCongViecNavigation.IdNguoiDuyet, x.IdFormCongViecNavigation.IdAdmin,
                    x.IdFormCongViecNavigation.TimeNguoiDuyet, x.IdFormCongViecNavigation.TimeAdmin,
                    _context.DanhGiaFormCongViecs.Any(dg => dg.IdFormCongViec == x.IdFormCongViec)))
                .ToListAsync(ct);

            return GomDon(don, hoTro, coQuaHan: true);
        }

        // Serial rác của BIOS không dùng để đối chiếu danh sách chặn (cùng danh sách ITFormController.SeriRacKhongDoiChieu)
        private static readonly string[] SeriRac = { "to be filled by o.e.m.", "default string", "system serial number", "none", "0" };
        private static string KhoaChan(string? s) => (s ?? "").Trim().ToLowerInvariant();

        // ---------- Bản chụp hằng ngày (KK_ThietBiChotNgay): bản quyền Windows / Office / Cần cài Office / trạng thái ----------

        public const string ChiTieuWin = "win", ChiTieuOffice = "office", ChiTieuCanCaiOffice = "can_cai_office", ChiTieuTrangThai = "trang_thai";

        // Bản quyền chỉ có nghĩa với máy tính — cùng danh sách loại với chức năng nhập nhanh Key Office từ Excel
        private static readonly string[] LoaiMayTinh = { "máy tính", "laptop" };

        /// <summary>
        /// Gom giá trị bản quyền về 4 nhóm. Windows lưu dạng "Professional - Có bản quyền" (TrichPhienBanWindows),
        /// Office dạng "Có bản quyền"/"Chưa có bản quyền"/"Không xác định" — để nguyên thì mỗi phiên bản Windows thành 1 dòng.
        /// Xét "chưa có" trước vì "có bản quyền" nằm trong "chưa có bản quyền".
        /// </summary>
        public static string NhomBanQuyen(string? giaTri)
        {
            var s = (giaTri ?? "").Trim().ToLowerInvariant();
            if (s.Length == 0) return "Trống / Chưa rõ";
            if (s.Contains("chưa có bản quyền")) return "Chưa có bản quyền";
            if (s.Contains("có bản quyền")) return "Có bản quyền";
            return "Không xác định";
        }

        public record DongChot(string CongTy, string ChiTieu, string GiaTri, int SoLuong);

        /// <summary>
        /// Số liệu tại thời điểm gọi, cùng tập thiết bị với ThietBiAsync (bỏ đã xoá, bị chặn, trạng thái "xóa").
        /// win / office / can_cai_office chỉ tính Máy tính/Laptop; trang_thai tính mọi thiết bị.
        /// </summary>
        public async Task<List<DongChot>> TinhChotAsync(CancellationToken ct)
        {
            var ds = await _context.KkThietBis.AsNoTracking()
                .Where(x => x.NgayXoa == null)
                .Select(x => new
                {
                    x.TenMayTinh, x.Seribacode, x.LoaiThietBi, x.WinLicense, x.OfficeLicense, x.CanCaiOffice,
                    CongTy = x.IdcongTyNavigation != null ? x.IdcongTyNavigation.TenCongTy : null,
                    TrangThai = x.IdTrangThaiNavigation != null ? x.IdTrangThaiNavigation.TenTrangThai : null,
                })
                .ToListAsync(ct);
            var biChan = await HamBiChanAsync(ct);

            var dong = ds
                .Where(x => !biChan(x.Seribacode, x.TenMayTinh))
                .Where(x => !(x.TrangThai ?? "").ToLowerInvariant().Contains("xóa"))
                .Select(x => new
                {
                    CongTy = string.IsNullOrWhiteSpace(x.CongTy) ? "Chưa xác định" : x.CongTy.Trim(),
                    LaMayTinh = LoaiMayTinh.Contains((x.LoaiThietBi ?? "").Trim().ToLowerInvariant()),
                    x.WinLicense, x.OfficeLicense, x.CanCaiOffice,
                    TrangThai = NhomTrangThai(x.TrangThai)
                })
                .ToList();

            var kq = new List<DongChot>();
            void Dem(string chiTieu, IEnumerable<(string CongTy, string GiaTri)> ds2)
                => kq.AddRange(ds2.GroupBy(x => x).Select(g => new DongChot(g.Key.CongTy, chiTieu, g.Key.GiaTri, g.Count())));

            var mayTinh = dong.Where(x => x.LaMayTinh).ToList();
            Dem(ChiTieuWin, mayTinh.Select(x => (x.CongTy, NhomBanQuyen(x.WinLicense))));
            Dem(ChiTieuOffice, mayTinh.Select(x => (x.CongTy, NhomBanQuyen(x.OfficeLicense))));
            Dem(ChiTieuCanCaiOffice, mayTinh.Select(x => (x.CongTy, x.CanCaiOffice == true ? "Cần cài" : x.CanCaiOffice == false ? "Không cần" : "Chưa trả lời")));
            Dem(ChiTieuTrangThai, dong.Select(x => (x.CongTy, x.TrangThai)));
            return kq;
        }

        // Cùng cách nhóm với ThietBiAsync / kiemke-tong-quan.js
        private static string NhomTrangThai(string? trangThai)
        {
            var tt = (trangThai ?? "").ToLowerInvariant();
            return tt.Contains("hoạt động") ? "Hoạt động" : tt.Contains("hỏng") ? "Hỏng"
                : tt.Contains("bảo trì") ? "Bảo trì" : tt.Trim() == "kho it" ? "Kho IT" : "Khác";
        }

        /// <summary>So sánh bản quyền / Office / trạng thái: kỳ này và kỳ trước, mỗi kỳ lấy 1 bản chụp.</summary>
        public class SoSanhChot
        {
            /// <summary>Kỳ này chưa hết: số tính trực tiếp lúc xem, không lấy bản chụp.</summary>
            public bool NayLaHienTai { get; set; }
            /// <summary>Ngày của bản chụp kỳ này (kỳ đã qua); null khi NayLaHienTai hoặc kỳ không có bản chụp nào.</summary>
            public DateOnly? NgayNay { get; set; }
            /// <summary>Ngày của bản chụp kỳ trước; null = chưa có bản chụp nào trong kỳ trước.</summary>
            public DateOnly? NgayTruoc { get; set; }
            /// <summary>Bản chụp sớm nhất đang có (null = chưa chụp lần nào).</summary>
            public DateOnly? ChupTu { get; set; }
            public List<DongChot> Nay { get; set; } = new();
            public List<DongChot>? Truoc { get; set; }
        }

        /// <summary>
        /// Kỳ chưa hết → số hiện tại; kỳ đã qua → bản chụp ngày cuối cùng trong kỳ. Kỳ không có bản chụp nào → null
        /// (không lấy bản chụp trước đầu kỳ: số đó không phải của kỳ này).
        /// </summary>
        public async Task<SoSanhChot> SoSanhChotAsync(DateTime tuNgay, DateTime denNgay, CancellationToken ct)
        {
            var homNay = DateOnly.FromDateTime(DateTime.Today);
            var (tuTruoc, denTruoc) = CameraBaoCaoService.KyTruoc(tuNgay, denNgay);
            var kq = new SoSanhChot
            {
                ChupTu = await _context.KkThietBiChotNgays.MinAsync(x => (DateOnly?)x.Ngay, ct)
            };

            async Task<(DateOnly? ngay, List<DongChot>? ds)> BanChupAsync(DateTime tu, DateTime den)
            {
                DateOnly a = DateOnly.FromDateTime(tu), b = DateOnly.FromDateTime(den);
                var ngay = await _context.KkThietBiChotNgays.Where(x => x.Ngay >= a && x.Ngay <= b)
                    .MaxAsync(x => (DateOnly?)x.Ngay, ct);
                if (ngay == null) return (null, null);
                var ds = await _context.KkThietBiChotNgays.AsNoTracking().Where(x => x.Ngay == ngay)
                    .Select(x => new DongChot(x.CongTy, x.ChiTieu, x.GiaTri, x.SoLuong)).ToListAsync(ct);
                return (ngay, ds);
            }

            if (DateOnly.FromDateTime(denNgay) >= homNay)
            {
                kq.NayLaHienTai = true;
                kq.Nay = await TinhChotAsync(ct);
            }
            else
            {
                var (ngay, ds) = await BanChupAsync(tuNgay, denNgay);
                kq.NgayNay = ngay;
                kq.Nay = ds ?? new List<DongChot>();
            }
            (kq.NgayTruoc, kq.Truoc) = await BanChupAsync(tuTruoc, denTruoc);
            return kq;
        }

        /// <summary>Danh sách chặn: bản ghi đủ Serial + Tên máy phải khớp cả hai (cùng quy tắc ITFormController.BiChan).</summary>
        private async Task<Func<string?, string?, bool>> HamBiChanAsync(CancellationToken ct)
        {
            var chan = await _context.KkThietBiChans.AsNoTracking().Select(x => new { x.Seri, x.TenMay }).ToListAsync(ct);
            var cap = new HashSet<string>(); var seri = new HashSet<string>(); var ten = new HashSet<string>();
            foreach (var c in chan)
            {
                var s = KhoaChan(c.Seri);
                if (SeriRac.Contains(s)) s = "";
                var t = KhoaChan(c.TenMay);
                if (s.Length > 0 && t.Length > 0) cap.Add(s + "|" + t);
                else if (s.Length > 0) seri.Add(s);
                else if (t.Length > 0) ten.Add(t);
            }
            return (sr, tm) =>
            {
                var s = KhoaChan(sr); var t = KhoaChan(tm);
                if (s.Length > 0 && t.Length > 0 && cap.Contains(s + "|" + t)) return true;
                if (s.Length > 0 && seri.Contains(s)) return true;
                return t.Length > 0 && ten.Contains(t);
            };
        }

        private const int SapHetBhNgay = 90;    // cùng ngưỡng kiemke-tong-quan.js
        private const int LauChuaKiemNgay = 30;

        /// <summary>
        /// Hiện trạng thiết bị kiểm kê (không theo kỳ). Bỏ thiết bị đã xoá mềm và thiết bị trong KK_ThietBiChan
        /// — đúng tập trang Quản lý Thiết bị đang hiện, để số trong file khớp màn hình.
        /// </summary>
        public async Task<ThongKeThietBi> ThietBiAsync(CancellationToken ct)
        {
            var ds = await _context.KkThietBis.AsNoTracking()
                .Where(x => x.NgayXoa == null)
                .Select(x => new
                {
                    x.TenMayTinh,
                    x.Seribacode,
                    x.LoaiThietBi,
                    x.IdNguoiDung,
                    x.HanBaoHanh,
                    x.ThoiGianCheck,
                    CongTy = x.IdcongTyNavigation != null ? x.IdcongTyNavigation.TenCongTy : null,
                    BoPhan = x.IdboPhanNavigation != null ? x.IdboPhanNavigation.TenBoPhan : null,
                    TrangThai = x.IdTrangThaiNavigation != null ? x.IdTrangThaiNavigation.TenTrangThai : null,
                })
                .ToListAsync(ct);

            var biChan = await HamBiChanAsync(ct);

            var homNay = DateTime.Today;
            var dong = ds
                .Where(x => !biChan(x.Seribacode, x.TenMayTinh))
                .Where(x => !(x.TrangThai ?? "").ToLowerInvariant().Contains("xóa"))
                .Select(x =>
                {
                    var tt = (x.TrangThai ?? "").ToLowerInvariant();
                    var nhom = tt.Contains("hoạt động") ? "hoatDong" : tt.Contains("hỏng") ? "hong"
                        : tt.Contains("bảo trì") ? "baoTri" : tt.Trim() == "kho it" ? "khoIt" : "khac";
                    int? conBh = x.HanBaoHanh != null ? x.HanBaoHanh.Value.DayNumber - DateOnly.FromDateTime(homNay).DayNumber : null;
                    int? ngayKiem = x.ThoiGianCheck != null ? (int)(homNay - x.ThoiGianCheck.Value.Date).TotalDays : null;
                    return new
                    {
                        CongTy = string.IsNullOrWhiteSpace(x.CongTy) ? "Chưa xác định" : x.CongTy.Trim(),
                        BoPhan = string.IsNullOrWhiteSpace(x.BoPhan) ? "Chưa xác định" : x.BoPhan.Trim(),
                        Loai = string.IsNullOrWhiteSpace(x.LoaiThietBi) ? "Chưa rõ" : x.LoaiThietBi.Trim(),
                        Nhom = nhom,
                        CoNguoiDung = x.IdNguoiDung != null,
                        ConBh = conBh,
                        NgayKiem = ngayKiem,
                    };
                })
                .ToList();

            var kq = new ThongKeThietBi
            {
                CongTy = dong.GroupBy(x => x.CongTy).Select(g => new ThietBiCongTy
                {
                    CongTy = g.Key,
                    Tong = g.Count(),
                    HoatDong = g.Count(x => x.Nhom == "hoatDong"),
                    Hong = g.Count(x => x.Nhom == "hong"),
                    BaoTri = g.Count(x => x.Nhom == "baoTri"),
                    KhoIt = g.Count(x => x.Nhom == "khoIt"),
                    Khac = g.Count(x => x.Nhom == "khac"),
                    ChuaCapPhat = g.Count(x => !x.CoNguoiDung),
                    HetBaoHanh = g.Count(x => x.ConBh < 0),
                    SapHetBaoHanh = g.Count(x => x.ConBh >= 0 && x.ConBh <= SapHetBhNgay),
                    ChuaKiem = g.Count(x => x.NgayKiem == null),
                    LauChuaKiem = g.Count(x => x.NgayKiem > LauChuaKiemNgay),
                }).OrderByDescending(x => x.Tong).ToList(),
                TheoLoai = DemTheo(dong, x => x.Loai),
                TheoBoPhan = dong.GroupBy(x => (x.CongTy, x.BoPhan))
                    .Select(g => (g.Key.CongTy, g.Key.BoPhan, g.Count(), g.Count(x => x.Nhom == "hoatDong"), g.Count(x => x.Nhom == "hong")))
                    .OrderBy(x => x.CongTy).ThenByDescending(x => x.Item3).ToList(),
            };
            return kq;
        }
    }
}
