using E_Form_Best.Context;
using Microsoft.EntityFrameworkCore;

namespace E_Form_Best.Areas.ITForm.Services
{
    /// <summary>
    /// Số liệu báo cáo Switch (/QLSwitch/TongQuan + Excel): trạng thái hiện tại theo lần ping gần nhất và
    /// thống kê trong kỳ từ KK_SwitchLichSu. Cùng cách tính với CameraBaoCaoService.
    /// </summary>
    public class SwitchBaoCaoService
    {
        private readonly ITFormContext _context;

        public SwitchBaoCaoService(ITFormContext context) => _context = context;

        public class BaoCao
        {
            public DateTime TuNgay { get; set; }
            public DateTime DenNgay { get; set; }
            public DateTime TaoLuc { get; set; }
            /// <summary>Sự kiện sớm nhất đang có — kỳ bắt đầu trước mốc này thì số liệu trong kỳ chưa đủ.</summary>
            public DateTime? LichSuTu { get; set; }
            public List<CongTyDong> CongTy { get; set; } = new();
            public List<BoPhanDong> BoPhan { get; set; } = new();
            public List<SwitchDong> DangMat { get; set; } = new();
            public List<SwitchDong> MatNhieu { get; set; } = new();
        }

        public class CongTyDong
        {
            public string CongTy { get; set; } = "";
            public DateTime? KiemTraLuc { get; set; }
            /// <summary>Mọi switch còn hiệu lực, kể cả trong kho / ngừng dùng / chưa khai IP.</summary>
            public int TongTaiSan { get; set; }
            /// <summary>Switch đang theo dõi: có IP và không ở tình trạng "Ngừng sử dụng" / "Trong kho".</summary>
            public int Tong { get; set; }
            public int HoatDong { get; set; }
            public int MatKetNoi { get; set; }
            public int ChuaKiemTra { get; set; }
            public int ChuaKhaiIp { get; set; }
            public int HetBaoHanh { get; set; }
            public int SoLanMat { get; set; }
            public int SoSwitchBiMat { get; set; }
            public long GiayMat { get; set; }
            public double? TyLeTrongKy { get; set; }
        }

        public class BoPhanDong
        {
            public string CongTy { get; set; } = "";
            public string BoPhan { get; set; } = "";
            public int Tong { get; set; }
            public int HoatDong { get; set; }
            public int MatKetNoi { get; set; }
            public int SoLanMat { get; set; }
            public long GiayMat { get; set; }
        }

        public class SwitchDong
        {
            public int IdSwitch { get; set; }
            public string CongTy { get; set; } = "";
            public string Ten { get; set; } = "";
            public string? Ip { get; set; }
            public string? BoPhan { get; set; }
            public string? ViTri { get; set; }
            /// <summary>Đang mất kết nối từ lúc nào (null = đang hoạt động).</summary>
            public DateTime? MatTu { get; set; }
            public int SoLan { get; set; }
            public long GiayMat { get; set; }
        }

        /// <param name="tuNgay">Ngày đầu kỳ (gồm trọn ngày).</param>
        /// <param name="denNgay">Ngày cuối kỳ (gồm trọn ngày).</param>
        public async Task<BaoCao> TaoAsync(IReadOnlyList<string> dsCongTy, DateTime tuNgay, DateTime denNgay, CancellationToken ct)
        {
            var bayGio = DateTime.Now;
            var tu = tuNgay.Date;
            var den = denNgay.Date.AddDays(1);
            // Kỳ chưa hết thì chỉ tính tới hiện tại
            var cuoiKy = den < bayGio ? den : bayGio;
            var homNay = DateOnly.FromDateTime(bayGio);
            var bc = new BaoCao { TuNgay = tu, DenNgay = denNgay.Date, TaoLuc = bayGio };

            var dsSw = await _context.KkSwitches
                .Where(x => x.NgayXoa == null && x.IdcongTyNavigation != null && dsCongTy.Contains(x.IdcongTyNavigation.TenCongTy))
                .Select(x => new
                {
                    x.IdSwitch, x.TenSwitch, x.DiaChiIp, x.ViTri, x.TinhTrang, x.HanBaoHanh,
                    x.TrangThaiKetNoi, x.DoiTrangThaiLuc, x.KiemTraLuc,
                    CongTy = x.IdcongTyNavigation!.TenCongTy,
                    BoPhan = x.IdboPhanNavigation != null ? x.IdboPhanNavigation.TenBoPhan : null
                })
                .ToListAsync(ct);

            bool TheoDoi(string? ip, string? tinhTrang)
                => ip != null && (tinhTrang == null || !SwitchPingWorker.DsTinhTrangKhongTheoDoi.Contains(tinhTrang));
            var theoDoi = dsSw.Where(x => TheoDoi(x.DiaChiIp, x.TinhTrang)).ToList();
            var dsId = theoDoi.Select(x => x.IdSwitch).ToList();

            // Sự kiện DOWN trong kỳ -> số lần mất. Sự kiện UP từ đầu kỳ trở đi (kể cả sau kỳ) mang thời lượng mất
            // -> cắt phần nằm trong kỳ: switch rớt trước kỳ và lên lại sau kỳ vẫn được tính đủ.
            var suKien = await _context.KkSwitchLichSus
                .Where(x => dsId.Contains(x.IdSwitch) && x.ThoiGian >= tu
                            && ((x.SangTrangThai == "DOWN" && x.ThoiGian < den) || (x.SangTrangThai == "UP" && x.ThoiLuongGiay != null)))
                .Select(x => new { x.IdSwitch, x.ThoiGian, x.SangTrangThai, x.ThoiLuongGiay })
                .ToListAsync(ct);
            bc.LichSuTu = dsId.Count == 0 ? null
                : await _context.KkSwitchLichSus.Where(x => dsId.Contains(x.IdSwitch)).MinAsync(x => (DateTime?)x.ThoiGian, ct);

            var thongKe = new Dictionary<int, (int soLan, long giay)>();
            void Cong(int idSwitch, int lan, long giay)
            {
                var cu = thongKe.GetValueOrDefault(idSwitch);
                thongKe[idSwitch] = (cu.soLan + lan, cu.giay + giay);
            }
            long GiayTrongKy(DateTime batDau, DateTime ketThuc)
            {
                var a = batDau < tu ? tu : batDau;
                var b = ketThuc > cuoiKy ? cuoiKy : ketThuc;
                return b > a ? (long)(b - a).TotalSeconds : 0;
            }

            foreach (var s in suKien)
            {
                if (s.SangTrangThai == "DOWN") Cong(s.IdSwitch, 1, 0);
                else Cong(s.IdSwitch, 0, GiayTrongKy(s.ThoiGian.AddSeconds(-s.ThoiLuongGiay!.Value), s.ThoiGian));
            }
            // Đang mất kết nối: chưa có sự kiện UP nên tính từ lúc rớt tới cuối kỳ
            foreach (var sw in theoDoi.Where(x => x.TrangThaiKetNoi == "DOWN" && x.DoiTrangThaiLuc != null))
                Cong(sw.IdSwitch, 0, GiayTrongKy(sw.DoiTrangThaiLuc!.Value, bayGio));

            var giayKy = Math.Max(0, (cuoiKy - tu).TotalSeconds);
            foreach (var cty in dsCongTy)
            {
                var cuaCty = dsSw.Where(x => x.CongTy == cty).ToList();
                var tdCty = theoDoi.Where(x => x.CongTy == cty).ToList();
                var tk = tdCty.Select(x => thongKe.GetValueOrDefault(x.IdSwitch)).ToList();
                var dong = new CongTyDong
                {
                    CongTy = cty,
                    KiemTraLuc = tdCty.Max(x => x.KiemTraLuc),
                    TongTaiSan = cuaCty.Count,
                    Tong = tdCty.Count,
                    HoatDong = tdCty.Count(x => x.TrangThaiKetNoi == "UP"),
                    MatKetNoi = tdCty.Count(x => x.TrangThaiKetNoi == "DOWN"),
                    ChuaKiemTra = tdCty.Count(x => x.TrangThaiKetNoi == null),
                    ChuaKhaiIp = cuaCty.Count(x => x.DiaChiIp == null),
                    HetBaoHanh = cuaCty.Count(x => x.HanBaoHanh != null && x.HanBaoHanh < homNay),
                    SoLanMat = tk.Sum(x => x.soLan),
                    SoSwitchBiMat = tk.Count(x => x.soLan > 0 || x.giay > 0),
                    GiayMat = tk.Sum(x => x.giay)
                };
                dong.TyLeTrongKy = dong.Tong > 0 && giayKy > 0
                    ? Math.Round(100 * Math.Max(0, 1 - dong.GiayMat / (dong.Tong * giayKy)), 2)
                    : null;
                bc.CongTy.Add(dong);
            }

            bc.BoPhan = theoDoi
                .GroupBy(x => (x.CongTy, BoPhan: x.BoPhan ?? "(Chưa gán bộ phận)"))
                .Select(g =>
                {
                    var tk = g.Select(x => thongKe.GetValueOrDefault(x.IdSwitch)).ToList();
                    return new BoPhanDong
                    {
                        CongTy = g.Key.CongTy, BoPhan = g.Key.BoPhan,
                        Tong = g.Count(), HoatDong = g.Count(x => x.TrangThaiKetNoi == "UP"),
                        MatKetNoi = g.Count(x => x.TrangThaiKetNoi == "DOWN"),
                        SoLanMat = tk.Sum(x => x.soLan), GiayMat = tk.Sum(x => x.giay)
                    };
                })
                .OrderBy(x => Array.IndexOf(SwitchPingWorker.DsCongTy, x.CongTy)).ThenByDescending(x => x.MatKetNoi).ThenBy(x => x.BoPhan)
                .ToList();

            var dsDong = theoDoi.Select(x =>
            {
                var tk = thongKe.GetValueOrDefault(x.IdSwitch);
                return new SwitchDong
                {
                    IdSwitch = x.IdSwitch, CongTy = x.CongTy, Ten = x.TenSwitch, Ip = x.DiaChiIp, BoPhan = x.BoPhan, ViTri = x.ViTri,
                    MatTu = x.TrangThaiKetNoi == "DOWN" ? x.DoiTrangThaiLuc : null,
                    SoLan = tk.soLan, GiayMat = tk.giay
                };
            }).ToList();

            var dangMatIds = theoDoi.Where(x => x.TrangThaiKetNoi == "DOWN").Select(x => x.IdSwitch).ToHashSet();
            bc.DangMat = dsDong.Where(x => dangMatIds.Contains(x.IdSwitch))
                .OrderBy(x => x.MatTu ?? DateTime.MaxValue).ThenBy(x => x.Ten).ToList();
            bc.MatNhieu = dsDong.Where(x => x.SoLan > 0 || x.GiayMat > 0)
                .OrderByDescending(x => x.SoLan).ThenByDescending(x => x.GiayMat).Take(20).ToList();

            return bc;
        }
    }
}
