using E_Form_Best.Areas.ITForm.Controllers;
using E_Form_Best.Context;
using Microsoft.EntityFrameworkCore;

namespace E_Form_Best.Areas.ITForm.Services
{
    /// <summary>
    /// Số liệu báo cáo Server &amp; lưu trữ (/QLServer/TongQuan + Excel): trạng thái hiện tại theo lần ping / SNMP gần nhất,
    /// thống kê mất kết nối trong kỳ từ KK_ServerLichSu và sự cố phần cứng từ KK_ServerSucKhoeLichSu.
    /// Cùng cách tính với SwitchBaoCaoService để 2 báo cáo so được với nhau.
    /// </summary>
    public class ServerBaoCaoService
    {
        private readonly ITFormContext _context;

        /// <summary>Ngưỡng % dung lượng đã dùng bị đánh dấu sắp đầy.</summary>
        public const decimal NguongDay = 0.85m;
        /// <summary>Bảo hành còn ≤ số ngày này thì đánh dấu sắp hết.</summary>
        public const int NgaySapHetBh = 90;

        public ServerBaoCaoService(ITFormContext context) => _context = context;

        public class BaoCao
        {
            public DateTime TuNgay { get; set; }
            public DateTime DenNgay { get; set; }
            public DateTime TaoLuc { get; set; }
            public DateTime KyTruocTu { get; set; }
            public DateTime KyTruocDen { get; set; }
            /// <summary>Sự kiện sớm nhất đang có — kỳ bắt đầu trước mốc này thì số liệu trong kỳ chưa đủ.</summary>
            public DateTime? LichSuTu { get; set; }
            public decimal NguongDay { get; set; }
            public List<CongTyDong> CongTy { get; set; } = new();
            public List<LoaiDong> TheoLoai { get; set; } = new();
            public List<LuuTruDong> LuuTru { get; set; } = new();
            public List<ChuYDong> CanChuY { get; set; } = new();
            public List<ThietBiDong> DangMat { get; set; } = new();
            public List<ThietBiDong> MatNhieu { get; set; } = new();
        }

        public class CongTyDong
        {
            public string CongTy { get; set; } = "";
            public DateTime? KiemTraLuc { get; set; }
            /// <summary>Mọi thiết bị còn hiệu lực, kể cả trong kho / ngừng dùng / chưa khai IP.</summary>
            public int TongTaiSan { get; set; }
            public int SoServer { get; set; }
            public int SoLuuTru { get; set; }
            /// <summary>Đang theo dõi: có IP và không ở tình trạng "Ngừng sử dụng" / "Trong kho".</summary>
            public int Tong { get; set; }
            public int HoatDong { get; set; }
            public int MatKetNoi { get; set; }
            public int ChuaKiemTra { get; set; }
            public int ChuaKhaiIp { get; set; }
            public int HetBaoHanh { get; set; }
            public int SapHetBaoHanh { get; set; }
            public int SucKhoeLoi { get; set; }
            public int SucKhoeCanhBao { get; set; }
            public int SucKhoeOk { get; set; }
            public decimal DungLuongTb { get; set; }
            public decimal DaDungTb { get; set; }
            public int SoSapDay { get; set; }
            public int SoLanMat { get; set; }
            /// <summary>Tên giữ "SoSwitchBiMat" như các báo cáo khác để bao-cao-so-sanh.js / Excel dùng chung.</summary>
            public int SoSwitchBiMat { get; set; }
            public long GiayMat { get; set; }
            public double? TyLeTrongKy { get; set; }
            /// <summary>Số lần phần cứng chuyển sang Lỗi / Cảnh báo trong kỳ (SNMP).</summary>
            public int SuCoPhanCung { get; set; }
            public int SoLanMatTruoc { get; set; }
            public int SoSwitchBiMatTruoc { get; set; }
            public long GiayMatTruoc { get; set; }
            public double? TyLeTruoc { get; set; }
            public int SuCoPhanCungTruoc { get; set; }
        }

        public class LoaiDong
        {
            public string CongTy { get; set; } = "";
            public string Loai { get; set; } = "";
            public int TongTaiSan { get; set; }
            public int Tong { get; set; }
            public int HoatDong { get; set; }
            public int MatKetNoi { get; set; }
            public int SucKhoeLoi { get; set; }
            public int SoLanMat { get; set; }
            public int SoLanMatTruoc { get; set; }
            public long GiayMat { get; set; }
        }

        public class LuuTruDong
        {
            public string CongTy { get; set; } = "";
            public string Ten { get; set; } = "";
            public string Loai { get; set; } = "";
            public string? Ip { get; set; }
            public string? Raid { get; set; }
            public decimal? DungLuongTb { get; set; }
            public decimal? DaDungTb { get; set; }
            public string? KetNoi { get; set; }
            public string? SucKhoe { get; set; }
        }

        public class ChuYDong
        {
            public string CongTy { get; set; } = "";
            public string Ten { get; set; } = "";
            public string Loai { get; set; } = "";
            public string? Ip { get; set; }
            public bool DangMatKetNoi { get; set; }
            public List<string> LyDo { get; set; } = new();
        }

        public class ThietBiDong
        {
            public int IdServer { get; set; }
            public string CongTy { get; set; } = "";
            public string Ten { get; set; } = "";
            public string Loai { get; set; } = "";
            public string? Ip { get; set; }
            public string? ChucNang { get; set; }
            public string? ViTri { get; set; }
            public DateTime? MatTu { get; set; }
            public int SoLan { get; set; }
            public long GiayMat { get; set; }
            public int SoLanTruoc { get; set; }
        }

        /// <summary>Máy nạp hàng loạt mang tên mặc định "Server &lt;IP&gt;": hiện sysName SNMP thay thế (cùng QLServerController).</summary>
        public static string TenHienThi(string ten, string? ip, string? tenHeThong)
            => !string.IsNullOrWhiteSpace(tenHeThong) && (ten == "Server " + ip || ten == ip) ? tenHeThong.Trim() : ten;

        /// <param name="tuNgay">Ngày đầu kỳ (gồm trọn ngày).</param>
        /// <param name="denNgay">Ngày cuối kỳ (gồm trọn ngày).</param>
        public async Task<BaoCao> TaoAsync(IReadOnlyList<string> dsCongTy, DateTime tuNgay, DateTime denNgay, CancellationToken ct)
        {
            var bayGio = DateTime.Now;
            var tu = tuNgay.Date;
            var den = denNgay.Date.AddDays(1);
            var cuoiKy = den < bayGio ? den : bayGio;
            var homNay = DateOnly.FromDateTime(bayGio);
            var (tuTruoc, denTruoc) = CameraBaoCaoService.KyTruoc(tu, denNgay);
            // Kỳ này chưa hết thì kỳ trước cũng chỉ tính tới cùng mốc, để số lần mất so được với nhau
            var cuoiTruoc = denTruoc.AddDays(1);
            if (cuoiKy < den && tuTruoc + (cuoiKy - tu) < cuoiTruoc) cuoiTruoc = tuTruoc + (cuoiKy - tu);
            var bc = new BaoCao { TuNgay = tu, DenNgay = denNgay.Date, TaoLuc = bayGio, KyTruocTu = tuTruoc, KyTruocDen = denTruoc, NguongDay = NguongDay };

            var ds = await _context.KkServers
                .Where(x => x.NgayXoa == null && x.IdcongTyNavigation != null && dsCongTy.Contains(x.IdcongTyNavigation.TenCongTy))
                .Select(x => new
                {
                    x.IdServer, x.TenServer, x.Loai, x.ChucNang, x.DiaChiIp, x.ViTri, x.TinhTrang, x.Raid,
                    x.DungLuongTb, x.DaDungTb, x.HanBaoHanh, x.TrangThaiKetNoi, x.DoiTrangThaiLuc, x.KiemTraLuc,
                    CongTy = x.IdcongTyNavigation!.TenCongTy,
                    SucKhoe = _context.KkServerSucKhoes.Where(s => s.IdServer == x.IdServer)
                        .Select(s => new { s.TrangThai, s.TomTat, s.TenHeThong }).FirstOrDefault()
                })
                .ToListAsync(ct);
            ds = ds.Select(x => x with { TenServer = TenHienThi(x.TenServer, x.DiaChiIp, x.SucKhoe?.TenHeThong) }).ToList();

            bool TheoDoi(string? ip, string? tinhTrang)
                => !string.IsNullOrWhiteSpace(ip) && (tinhTrang == null || !ServerPingWorker.DsTinhTrangKhongTheoDoi.Contains(tinhTrang));
            bool LaLuuTru(string loai) => QLServerController.DsLoaiLuuTru.Contains(loai);
            var theoDoi = ds.Where(x => TheoDoi(x.DiaChiIp, x.TinhTrang)).ToList();
            var dsId = theoDoi.Select(x => x.IdServer).ToList();
            var dsIdTatCa = ds.Select(x => x.IdServer).ToList();

            // Sự kiện DOWN trong kỳ -> số lần mất; sự kiện UP mang thời lượng -> cắt phần nằm trong kỳ (cùng SwitchBaoCaoService)
            var suKien = await _context.KkServerLichSus
                .Where(x => dsId.Contains(x.IdServer) && x.ThoiGian >= tuTruoc
                            && ((x.SangTrangThai == "DOWN" && x.ThoiGian < den) || (x.SangTrangThai == "UP" && x.ThoiLuongGiay != null)))
                .Select(x => new { x.IdServer, x.ThoiGian, x.SangTrangThai, x.ThoiLuongGiay })
                .ToListAsync(ct);
            bc.LichSuTu = dsId.Count == 0 ? null
                : await _context.KkServerLichSus.Where(x => dsId.Contains(x.IdServer)).MinAsync(x => (DateTime?)x.ThoiGian, ct);

            // Sự cố phần cứng: mỗi lần chuyển sang Lỗi / Cảnh báo là một lần
            var suCo = await _context.KkServerSucKhoeLichSus
                .Where(x => dsIdTatCa.Contains(x.IdServer) && x.ThoiGian >= tuTruoc && x.ThoiGian < den
                            && (x.SangTrangThai == ServerSnmpService.Loi || x.SangTrangThai == ServerSnmpService.CanhBao))
                .Select(x => new { x.IdServer, x.ThoiGian })
                .ToListAsync(ct);

            Dictionary<int, (int soLan, long giay)> ThongKeKy(DateTime kyTu, DateTime kyCuoi)
            {
                var kq = new Dictionary<int, (int soLan, long giay)>();
                void Cong(int id, int lan, long giay)
                {
                    var cu = kq.GetValueOrDefault(id);
                    kq[id] = (cu.soLan + lan, cu.giay + giay);
                }
                long GiayTrongKy(DateTime batDau, DateTime ketThuc)
                {
                    var a = batDau < kyTu ? kyTu : batDau;
                    var b = ketThuc > kyCuoi ? kyCuoi : ketThuc;
                    return b > a ? (long)(b - a).TotalSeconds : 0;
                }

                foreach (var s in suKien)
                {
                    if (s.SangTrangThai == "DOWN") { if (s.ThoiGian >= kyTu && s.ThoiGian < kyCuoi) Cong(s.IdServer, 1, 0); }
                    else Cong(s.IdServer, 0, GiayTrongKy(s.ThoiGian.AddSeconds(-s.ThoiLuongGiay!.Value), s.ThoiGian));
                }
                // Đang mất kết nối: chưa có sự kiện UP nên tính từ lúc rớt tới cuối kỳ
                foreach (var sv in theoDoi.Where(x => x.TrangThaiKetNoi == "DOWN" && x.DoiTrangThaiLuc != null))
                    Cong(sv.IdServer, 0, GiayTrongKy(sv.DoiTrangThaiLuc!.Value, bayGio));
                return kq;
            }

            var thongKe = ThongKeKy(tu, cuoiKy);
            var thongKeTruoc = ThongKeKy(tuTruoc, cuoiTruoc);
            var suCoNay = suCo.Where(x => x.ThoiGian >= tu && x.ThoiGian < cuoiKy).GroupBy(x => x.IdServer).ToDictionary(g => g.Key, g => g.Count());
            var suCoTruoc = suCo.Where(x => x.ThoiGian >= tuTruoc && x.ThoiGian < cuoiTruoc).GroupBy(x => x.IdServer).ToDictionary(g => g.Key, g => g.Count());

            double? TyLe(int tong, long giayMat, double giayKy)
                => tong > 0 && giayKy > 0 ? Math.Round(100 * Math.Max(0, 1 - giayMat / (tong * giayKy)), 2) : null;
            var giayKy = Math.Max(0, (cuoiKy - tu).TotalSeconds);
            var giayKyTruoc = Math.Max(0, (cuoiTruoc - tuTruoc).TotalSeconds);
            bool SapDay(decimal? dungLuong, decimal? daDung) => dungLuong > 0 && daDung != null && daDung / dungLuong >= NguongDay;

            foreach (var cty in dsCongTy)
            {
                var cuaCty = ds.Where(x => x.CongTy == cty).ToList();
                var tdCty = theoDoi.Where(x => x.CongTy == cty).ToList();
                var tk = tdCty.Select(x => thongKe.GetValueOrDefault(x.IdServer)).ToList();
                var tkTruoc = tdCty.Select(x => thongKeTruoc.GetValueOrDefault(x.IdServer)).ToList();
                var luuTru = cuaCty.Where(x => LaLuuTru(x.Loai) && x.DungLuongTb > 0).ToList();
                var dong = new CongTyDong
                {
                    CongTy = cty,
                    KiemTraLuc = tdCty.Max(x => x.KiemTraLuc),
                    TongTaiSan = cuaCty.Count,
                    SoServer = cuaCty.Count(x => !LaLuuTru(x.Loai)),
                    SoLuuTru = cuaCty.Count(x => LaLuuTru(x.Loai)),
                    Tong = tdCty.Count,
                    HoatDong = tdCty.Count(x => x.TrangThaiKetNoi == "UP"),
                    MatKetNoi = tdCty.Count(x => x.TrangThaiKetNoi == "DOWN"),
                    ChuaKiemTra = tdCty.Count(x => x.TrangThaiKetNoi == null),
                    ChuaKhaiIp = cuaCty.Count(x => string.IsNullOrWhiteSpace(x.DiaChiIp)),
                    HetBaoHanh = cuaCty.Count(x => x.HanBaoHanh < homNay),
                    SapHetBaoHanh = cuaCty.Count(x => x.HanBaoHanh >= homNay && x.HanBaoHanh <= homNay.AddDays(NgaySapHetBh)),
                    SucKhoeLoi = cuaCty.Count(x => x.SucKhoe?.TrangThai == ServerSnmpService.Loi),
                    SucKhoeCanhBao = cuaCty.Count(x => x.SucKhoe?.TrangThai == ServerSnmpService.CanhBao),
                    SucKhoeOk = cuaCty.Count(x => x.SucKhoe?.TrangThai == ServerSnmpService.Ok),
                    DungLuongTb = luuTru.Sum(x => x.DungLuongTb ?? 0),
                    DaDungTb = luuTru.Sum(x => x.DaDungTb ?? 0),
                    SoSapDay = cuaCty.Count(x => SapDay(x.DungLuongTb, x.DaDungTb)),
                    SoLanMat = tk.Sum(x => x.soLan),
                    SoSwitchBiMat = tk.Count(x => x.soLan > 0 || x.giay > 0),
                    GiayMat = tk.Sum(x => x.giay),
                    SuCoPhanCung = cuaCty.Sum(x => suCoNay.GetValueOrDefault(x.IdServer)),
                    SoLanMatTruoc = tkTruoc.Sum(x => x.soLan),
                    SoSwitchBiMatTruoc = tkTruoc.Count(x => x.soLan > 0 || x.giay > 0),
                    GiayMatTruoc = tkTruoc.Sum(x => x.giay),
                    SuCoPhanCungTruoc = cuaCty.Sum(x => suCoTruoc.GetValueOrDefault(x.IdServer))
                };
                dong.TyLeTrongKy = TyLe(dong.Tong, dong.GiayMat, giayKy);
                dong.TyLeTruoc = TyLe(dong.Tong, dong.GiayMatTruoc, giayKyTruoc);
                bc.CongTy.Add(dong);

                // Mỗi công ty đủ 4 loại (kể cả 0) để bảng dễ đối chiếu giữa các công ty
                foreach (var loai in QLServerController.DsLoai)
                {
                    var l = cuaCty.Where(x => x.Loai == loai).ToList();
                    if (l.Count == 0) continue;
                    var td = l.Where(x => TheoDoi(x.DiaChiIp, x.TinhTrang)).ToList();
                    bc.TheoLoai.Add(new LoaiDong
                    {
                        CongTy = cty, Loai = loai, TongTaiSan = l.Count, Tong = td.Count,
                        HoatDong = td.Count(x => x.TrangThaiKetNoi == "UP"),
                        MatKetNoi = td.Count(x => x.TrangThaiKetNoi == "DOWN"),
                        SucKhoeLoi = l.Count(x => x.SucKhoe?.TrangThai == ServerSnmpService.Loi),
                        SoLanMat = td.Sum(x => thongKe.GetValueOrDefault(x.IdServer).soLan),
                        SoLanMatTruoc = td.Sum(x => thongKeTruoc.GetValueOrDefault(x.IdServer).soLan),
                        GiayMat = td.Sum(x => thongKe.GetValueOrDefault(x.IdServer).giay)
                    });
                }
            }

            // Thiết bị có khai dung lượng (NAS/SAN, cả server có ổ lưu trữ lớn) — đầy nhất lên đầu
            bc.LuuTru = ds.Where(x => x.DungLuongTb > 0)
                .Select(x => new LuuTruDong
                {
                    CongTy = x.CongTy, Ten = x.TenServer, Loai = x.Loai, Ip = x.DiaChiIp, Raid = x.Raid,
                    DungLuongTb = x.DungLuongTb, DaDungTb = x.DaDungTb, KetNoi = x.TrangThaiKetNoi, SucKhoe = x.SucKhoe?.TrangThai
                })
                .OrderByDescending(x => x.DaDungTb != null ? x.DaDungTb / x.DungLuongTb : -1).ThenBy(x => x.CongTy).ThenBy(x => x.Ten)
                .ToList();

            // Cần chú ý: mỗi thiết bị một dòng, gộp các lý do
            bc.CanChuY = ds
                .Select(x =>
                {
                    var lyDo = new List<string>();
                    var dangMat = TheoDoi(x.DiaChiIp, x.TinhTrang) && x.TrangThaiKetNoi == "DOWN";
                    if (dangMat) lyDo.Add("Mất kết nối");
                    if (SapDay(x.DungLuongTb, x.DaDungTb))
                        lyDo.Add($"Dung lượng {Math.Round(x.DaDungTb!.Value / x.DungLuongTb!.Value * 100)}%");
                    if (x.HanBaoHanh < homNay) lyDo.Add("Hết bảo hành");
                    else if (x.HanBaoHanh <= homNay.AddDays(NgaySapHetBh)) lyDo.Add("Sắp hết bảo hành");
                    var soLan = thongKe.GetValueOrDefault(x.IdServer).soLan;
                    if (soLan >= 3) lyDo.Add($"Mất kết nối {soLan} lần trong kỳ");
                    // "Lỗi:" / "Cảnh báo:" để giao diện tô màu
                    if (x.SucKhoe?.TrangThai is ServerSnmpService.Loi or ServerSnmpService.CanhBao && !string.IsNullOrWhiteSpace(x.SucKhoe.TomTat))
                        lyDo.AddRange(x.SucKhoe.TomTat.Split("; ", StringSplitOptions.RemoveEmptyEntries)
                            .Select(m => (x.SucKhoe.TrangThai == ServerSnmpService.Loi ? "Lỗi: " : "Cảnh báo: ") + m));
                    return new ChuYDong { CongTy = x.CongTy, Ten = x.TenServer, Loai = x.Loai, Ip = x.DiaChiIp, DangMatKetNoi = dangMat, LyDo = lyDo };
                })
                .Where(x => x.LyDo.Count > 0)
                .OrderByDescending(x => x.DangMatKetNoi).ThenByDescending(x => x.LyDo.Any(l => l.StartsWith("Lỗi:")))
                .ThenBy(x => Array.IndexOf(ServerPingWorker.DsCongTy, x.CongTy)).ThenBy(x => x.Ten)
                .ToList();

            var dsDong = theoDoi.Select(x =>
            {
                var tk = thongKe.GetValueOrDefault(x.IdServer);
                return new ThietBiDong
                {
                    IdServer = x.IdServer, CongTy = x.CongTy, Ten = x.TenServer, Loai = x.Loai, Ip = x.DiaChiIp,
                    ChucNang = x.ChucNang, ViTri = x.ViTri,
                    MatTu = x.TrangThaiKetNoi == "DOWN" ? x.DoiTrangThaiLuc : null,
                    SoLan = tk.soLan, GiayMat = tk.giay, SoLanTruoc = thongKeTruoc.GetValueOrDefault(x.IdServer).soLan
                };
            }).ToList();

            var dangMatIds = theoDoi.Where(x => x.TrangThaiKetNoi == "DOWN").Select(x => x.IdServer).ToHashSet();
            bc.DangMat = dsDong.Where(x => dangMatIds.Contains(x.IdServer))
                .OrderBy(x => x.MatTu ?? DateTime.MaxValue).ThenBy(x => x.Ten).ToList();
            bc.MatNhieu = dsDong.Where(x => x.SoLan > 0 || x.GiayMat > 0)
                .OrderByDescending(x => x.SoLan).ThenByDescending(x => x.GiayMat).Take(20).ToList();

            return bc;
        }
    }
}
