using E_Form_Best.Context;
using E_Form_Best.Models.ITForm;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace E_Form_Best.Areas.ITForm.Services
{
    /// <summary>
    /// Bản chụp MAC trên các WLC (KK_WlcQuet + KK_WlcMac) để trang chi tiết đơn Wi-Fi đánh dấu sẵn nút "+":
    /// job WlcMacQuetWorker ghi cả danh sách mỗi chu kỳ, còn tra/thêm/gỡ trên trang ghi ngay từng MAC.
    /// </summary>
    public class WlcMacQuetService
    {
        private readonly ITFormContext _context;
        private readonly WlcMacService _wlc;
        private readonly ILogger<WlcMacQuetService> _logger;

        public WlcMacQuetService(ITFormContext context, WlcMacService wlc, ILogger<WlcMacQuetService> logger)
        {
            _context = context;
            _wlc = wlc;
            _logger = logger;
        }

        /// <summary>Trạng thái MAC trên 1 WLC. Co: true có / false thiếu / null chưa quét được WLC này lần nào.</summary>
        public record TrangThaiWlc(string Wlc, string Ip, bool? Co, DateTime? QuetLuc);

        /// <summary>
        /// Ghi kết quả 1 lượt quét của 1 WLC. Lỗi thì chỉ ghi lỗi, giữ nguyên danh sách cũ.
        /// Danh sách rỗng trong khi lần trước còn nhiều MAC thì coi là đọc hỏng, không đánh dấu mất hàng loạt.
        /// </summary>
        public async Task GhiKetQuaQuetAsync(string congTy, WlcMacService.Wlc wlc, List<WlcMacService.CauHinhMac>? ds, string? loi, CancellationToken ct)
        {
            var bayGio = DateTime.Now;
            var quet = await _context.KkWlcQuets.FirstOrDefaultAsync(x => x.WlcIp == wlc.Ip, ct);
            if (quet == null)
            {
                quet = new KkWlcQuet { WlcIp = wlc.Ip };
                _context.KkWlcQuets.Add(quet);
            }
            quet.CongTy = congTy;
            quet.TenWlc = wlc.Ten.Length > 100 ? wlc.Ten[..100] : wlc.Ten;
            quet.QuetLuc = bayGio;

            var cu = await _context.KkWlcMacs.Where(x => x.WlcIp == wlc.Ip).ToListAsync(ct);
            var moi = (ds ?? new())
                .Select(c => (Mac: WlcMacService.ChuanHoaMac(c.Mac), CauHinh: c))
                .Where(x => x.Mac != null)
                .GroupBy(x => x.Mac!)
                .ToDictionary(g => g.Key, g => g.First().CauHinh);

            var soDangCo = cu.Count(x => x.MatLuc == null);
            if (loi == null && moi.Count == 0 && soDangCo >= 5)
                loi = $"WLC trả danh sách rỗng trong khi lần trước có {soDangCo} MAC — bỏ qua lượt này.";
            if (loi != null)
            {
                quet.Loi = loi.Length > 500 ? loi[..500] : loi;
                await LuuAsync(ct);
                return;
            }

            foreach (var dong in cu)
            {
                if (moi.TryGetValue(dong.Mac, out var ch))
                {
                    GanCauHinh(dong, ch.WlanProfile, ch.AaaList, ch.MoTa);
                    dong.LanCuoiThay = bayGio;
                    dong.MatLuc = null;
                }
                else if (dong.MatLuc == null) dong.MatLuc = bayGio;
            }
            var daCo = cu.Select(x => x.Mac).ToHashSet();
            foreach (var (mac, ch) in moi.Where(x => !daCo.Contains(x.Key)))
            {
                var dong = new KkWlcMac { WlcIp = wlc.Ip, Mac = mac, LanDauThay = bayGio, LanCuoiThay = bayGio };
                GanCauHinh(dong, ch.WlanProfile, ch.AaaList, ch.MoTa);
                _context.KkWlcMacs.Add(dong);
            }

            quet.ThanhCongLuc = bayGio;
            quet.Loi = null;
            quet.SoMac = moi.Count;
            await LuuAsync(ct);
        }

        /// <summary>
        /// Ghi ngay 1 MAC sau khi tra/thêm/gỡ trên trang đơn, để không phải đợi lượt quét sau.
        /// Bảng chưa tạo / lỗi DB thì chỉ log — không được làm hỏng thao tác chính trên WLC.
        /// </summary>
        public async Task GhiNhanAsync(string ip, string mac, bool co, string? wlan = null, string? aaaList = null, string? moTa = null)
        {
            try
            {
                var bayGio = DateTime.Now;
                var dong = await _context.KkWlcMacs.FirstOrDefaultAsync(x => x.WlcIp == ip && x.Mac == mac);
                if (dong == null)
                {
                    if (!co) return;
                    dong = new KkWlcMac { WlcIp = ip, Mac = mac, LanDauThay = bayGio, LanCuoiThay = bayGio };
                    _context.KkWlcMacs.Add(dong);
                }
                if (co)
                {
                    GanCauHinh(dong, wlan, aaaList, moTa);
                    dong.LanCuoiThay = bayGio;
                    dong.MatLuc = null;
                }
                else if (dong.MatLuc == null) dong.MatLuc = bayGio;
                await LuuAsync(default);
            }
            catch (Exception ex)
            {
                _logger.LogWarning("KK_WlcMac: không ghi được {Mac} trên {Ip}: {Loi}", mac, ip, ex.Message);
            }
        }

        /// <summary>Trạng thái từng MAC (12 hex) trên từng WLC của công ty, theo bản chụp trong DB.</summary>
        public async Task<Dictionary<string, List<TrangThaiWlc>>> TrangThaiAsync(string congTy, IEnumerable<string> dsMac)
        {
            var macs = dsMac.Distinct().ToList();
            var dsWlc = _wlc.DsWlc(congTy);
            var ips = dsWlc.Select(w => w.Ip).ToList();

            var quet = await _context.KkWlcQuets.AsNoTracking()
                .Where(x => ips.Contains(x.WlcIp))
                .Select(x => new { x.WlcIp, x.ThanhCongLuc })
                .ToDictionaryAsync(x => x.WlcIp, x => x.ThanhCongLuc);
            var co = (await _context.KkWlcMacs.AsNoTracking()
                    .Where(x => ips.Contains(x.WlcIp) && macs.Contains(x.Mac) && x.MatLuc == null)
                    .Select(x => new { x.WlcIp, x.Mac })
                    .ToListAsync())
                .Select(x => (x.WlcIp, x.Mac)).ToHashSet();

            return macs.ToDictionary(m => m, m => dsWlc.Select(w =>
            {
                var luc = quet.GetValueOrDefault(w.Ip);
                // Có dòng đang hoạt động thì chắc chắn có (kể cả ghi từ thao tác trên trang khi job chưa quét lần nào)
                bool? trangThai = co.Contains((w.Ip, m)) ? true : luc.HasValue ? false : null;
                return new TrangThaiWlc(w.Ten, w.Ip, trangThai, luc);
            }).ToList());
        }

        private static void GanCauHinh(KkWlcMac dong, string? wlan, string? aaaList, string? moTa)
        {
            static string? Cat(string? s, int n) => string.IsNullOrWhiteSpace(s) ? null : s.Length > n ? s[..n] : s;
            dong.Wlan = Cat(wlan, 100) ?? dong.Wlan;
            dong.AaaList = Cat(aaaList, 100) ?? dong.AaaList;
            dong.MoTa = Cat(moTa, 200) ?? dong.MoTa;
        }

        /// <summary>2 máy chủ cùng quét có thể chèn trùng (wlc_ip, mac): bỏ lượt ghi này, lượt sau đọc lại là khớp.</summary>
        private async Task LuuAsync(CancellationToken ct)
        {
            try { await _context.SaveChangesAsync(ct); }
            catch (DbUpdateException ex) when (ex.InnerException is SqlException sql && (sql.Number == 2601 || sql.Number == 2627))
            {
                _context.ChangeTracker.Clear();
            }
        }
    }
}
