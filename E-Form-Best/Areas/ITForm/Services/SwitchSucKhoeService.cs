using System.Net.Sockets;
using System.Text.RegularExpressions;

namespace E_Form_Best.Areas.ITForm.Services
{
    /// <summary>
    /// Đọc sức khoẻ phần cứng switch / WLC Cisco qua SNMP v2c (chỉ đọc, không đụng DB):
    /// CPU (CISCO-PROCESS-MIB), RAM, nhiệt độ / quạt / nguồn (CISCO-ENVMON-MIB), thành viên stack (CISCO-STACKWISE-MIB),
    /// tình trạng cổng + lỗi cổng (IF-MIB), số AP / client (WLC). Đã kiểm thật 10/11/2026 trên 4507R+E VSS,
    /// C9300X stack, 3850 stack, C9800-L. Hãng khác không có các MIB Cisco thì chỉ còn phần cổng + thời gian chạy.
    /// </summary>
    public static class SwitchSucKhoeService
    {
        public const string Ok = ServerSnmpService.Ok, CanhBao = ServerSnmpService.CanhBao, Loi = ServerSnmpService.Loi;
        public const string KhongRo = ServerSnmpService.KhongRo, KhongDocDuoc = ServerSnmpService.KhongDocDuoc, ChuaDoc = "CHUA_DOC";

        /// <summary>Nhom: CPU / Bộ nhớ / Nhiệt độ / Quạt / Nguồn / Stack. PhanTram: vẽ thanh cho CPU, RAM, nhiệt độ so ngưỡng.</summary>
        public record Muc(string Nhom, string Ten, string TrangThai, string? GiaTri, double? PhanTram = null);

        /// <summary>Cổng có bộ đếm lỗi khác 0. TangVao / TangRa: tăng so với lần đọc trước (null = chưa có lần trước).</summary>
        public record CongLoi(int IfIndex, string Ten, string? MoTa, long LoiVao, long LoiRa, long? TangVao = null, long? TangRa = null);

        public class KetQua
        {
            public string Ip { get; set; } = "";
            public string? TenHeThong { get; set; }
            public string? MoTa { get; set; }
            /// <summary>Giây chạy liên tục từ lần khởi động (snmpEngineTime — sysUpTime quay vòng sau 497 ngày).</summary>
            public long? ThoiGianChay { get; set; }
            public List<Muc> DsMuc { get; } = new();
            public int? SoCong { get; set; }
            public int? SoCongUp { get; set; }
            public int? SoCongDown { get; set; }
            public int? SoCongTat { get; set; }
            public List<CongLoi> DsCongLoi { get; set; } = new();
            public int? SoAp { get; set; }
            public int? SoClient { get; set; }
        }

        private const string SysDescr = "1.3.6.1.2.1.1.1.0", SysName = "1.3.6.1.2.1.1.5.0", SysUpTime = "1.3.6.1.2.1.1.3.0";
        private const string EngineTime = "1.3.6.1.6.3.10.2.1.3.0";
        private const string EntName = "1.3.6.1.2.1.47.1.1.1.1.7";
        // CISCO-PROCESS-MIB cpmCPUTotalTable
        private const string Cpm = "1.3.6.1.4.1.9.9.109.1.1.1.1";
        // CISCO-MEMORY-POOL-MIB (thiết bị không có cpmCPUMemoryUsed)
        private const string MemPool = "1.3.6.1.4.1.9.9.48.1.1.1";
        // CISCO-ENVMON-MIB
        private const string EnvTemp = "1.3.6.1.4.1.9.9.13.1.3.1", EnvQuat = "1.3.6.1.4.1.9.9.13.1.4.1", EnvNguon = "1.3.6.1.4.1.9.9.13.1.5.1";
        // CISCO-STACKWISE-MIB cswSwitchInfoTable (index = entPhysicalIndex)
        private const string Stack = "1.3.6.1.4.1.9.9.500.1.2.1.1";
        // IF-MIB
        private const string IfType = "1.3.6.1.2.1.2.2.1.3", IfAdmin = "1.3.6.1.2.1.2.2.1.7", IfOper = "1.3.6.1.2.1.2.2.1.8";
        private const string IfInErr = "1.3.6.1.2.1.2.2.1.14", IfOutErr = "1.3.6.1.2.1.2.2.1.20";
        private const string IfName = "1.3.6.1.2.1.31.1.1.1.1", IfAlias = "1.3.6.1.2.1.31.1.1.1.18";
        // WLC: cLApName (CISCO-LWAPP-AP-MIB), bsnMobileStationMacAddress (AIRESPACE-WIRELESS-MIB)
        private const string WlcAp = "1.3.6.1.4.1.9.9.513.1.1.1.1.5", WlcClient = "1.3.6.1.4.1.14179.2.1.4.1.1";

        private const int TimeoutMs = 3000;

        /// <summary>Đọc 1 IP; null nếu không trả lời SNMP (community sai / tắt SNMP / không thông mạng).</summary>
        public static async Task<KetQua?> DocAsync(string ip, string community, CancellationToken ct)
        {
            Dictionary<string, object?> nen;
            try { nen = await SnmpClient.GetAsync(ip, community, new[] { SysDescr, SysName, SysUpTime, EngineTime }, TimeoutMs, 2, ct); }
            catch (Exception ex) when (ex is TimeoutException or SocketException or InvalidOperationException) { return null; }

            var kq = new KetQua
            {
                Ip = ip,
                TenHeThong = KhongRong(SnmpClient.Chuoi(nen.GetValueOrDefault(SysName)))?.TrimEnd('.'),
                MoTa = KhongRong(SnmpClient.Chuoi(nen.GetValueOrDefault(SysDescr))),
                ThoiGianChay = SnmpClient.So(nen.GetValueOrDefault(EngineTime)) is long g and > 0 ? g
                    : SnmpClient.So(nen.GetValueOrDefault(SysUpTime)) / 100
            };

            // Mỗi phần đọc riêng: thiết bị thiếu 1 MIB (hoặc trả lỗi) vẫn có các phần còn lại
            var stack = await Thu(() => DocStackAsync(kq, ip, community, ct)) ?? new Dictionary<string, int>();
            await Thu(() => DocCpuRamAsync(kq, ip, community, stack, ct));
            await Thu(() => DocNhietDoAsync(kq, ip, community, ct));
            await Thu(() => DocQuatNguonAsync(kq, ip, community, EnvQuat, "Quạt", ct));
            await Thu(() => DocQuatNguonAsync(kq, ip, community, EnvNguon, "Nguồn", ct));
            await Thu(() => DocCongAsync(kq, ip, community, ct));
            await Thu(() => DocWlcAsync(kq, ip, community, ct));
            return kq;
        }

        /// <summary>Trạng thái nặng nhất (LOI > CANH_BAO > OK); mục KHONG_RO (khe trống) không tính.</summary>
        public static string TrangThaiTong(IEnumerable<Muc> ds)
        {
            var l = ds.Select(x => x.TrangThai).ToList();
            if (l.Contains(Loi)) return Loi;
            if (l.Contains(CanhBao)) return CanhBao;
            return l.Contains(Ok) ? Ok : KhongRo;
        }

        private static async Task<T?> Thu<T>(Func<Task<T>> f) where T : class
        {
            try { return await f(); }
            catch (Exception ex) when (ex is TimeoutException or InvalidOperationException or IndexOutOfRangeException) { return null; }
        }

        private static async Task Thu(Func<Task> f)
        {
            try { await f(); }
            catch (Exception ex) when (ex is TimeoutException or InvalidOperationException or IndexOutOfRangeException) { }
        }

        private static Task<List<SnmpClient.Bien>> Walk(string ip, string com, string goc, CancellationToken ct, int toiDa = 5000)
            => SnmpClient.WalkAsync(ip, com, goc, TimeoutMs, 2, toiDa, ct);

        /// <summary>Gom bảng SNMP theo index: cột -> (index -> giá trị). Index là phần OID sau "goc.cot.".</summary>
        private static Dictionary<int, Dictionary<string, object?>> Bang(List<SnmpClient.Bien> ds, string goc)
        {
            var kq = new Dictionary<int, Dictionary<string, object?>>();
            var tienTo = goc + ".";
            foreach (var b in ds)
            {
                if (!b.Oid.StartsWith(tienTo)) continue;
                var phan = b.Oid[tienTo.Length..];
                var cham = phan.IndexOf('.');
                if (cham <= 0 || !int.TryParse(phan[..cham], out var cot)) continue;
                var idx = phan[(cham + 1)..];
                if (!kq.TryGetValue(cot, out var d)) kq[cot] = d = new Dictionary<string, object?>();
                d[idx] = b.GiaTri;
            }
            return kq;
        }

        private static object? O(Dictionary<int, Dictionary<string, object?>> bang, int cot, string idx)
            => bang.TryGetValue(cot, out var d) ? d.GetValueOrDefault(idx) : null;

        // ---------------- Stack ----------------

        private static readonly Dictionary<long, string> VaiTroStack = new()
        {
            [1] = "Master", [2] = "Member", [3] = "Không thuộc stack", [4] = "Standby"
        };

        private static readonly Dictionary<long, string> TrangThaiStack = new()
        {
            [1] = "Đang chờ", [2] = "Đang khởi động", [3] = "Vừa thêm", [4] = "Sẵn sàng", [5] = "Lệch SDM", [6] = "Lệch phiên bản",
            [7] = "Lệch tính năng", [8] = "Master mới khởi tạo", [9] = "Khai báo nhưng không có mặt", [10] = "Không hợp lệ", [11] = "Đã gỡ"
        };

        /// <summary>Trả map entPhysicalIndex -> số thứ tự switch trong stack (để đặt tên CPU "Switch N").</summary>
        private static async Task<Dictionary<string, int>> DocStackAsync(KetQua kq, string ip, string com, CancellationToken ct)
        {
            var bang = Bang(await Walk(ip, com, Stack, ct, 500), Stack);
            var map = new Dictionary<string, int>();
            if (!bang.TryGetValue(1, out var dsSo) || dsSo.Count == 0) return map;
            foreach (var (idx, v) in dsSo.OrderBy(x => SnmpClient.So(x.Value)))
            {
                var so = (int)(SnmpClient.So(v) ?? 0);
                map[idx] = so;
                var vaiTro = SnmpClient.So(O(bang, 3, idx)) is long r ? VaiTroStack.GetValueOrDefault(r, $"Vai trò {r}") : null;
                var st = SnmpClient.So(O(bang, 6, idx));
                var trangThai = st == null ? KhongRo : st == 4 ? Ok : Loi;
                var chu = st is long s ? TrangThaiStack.GetValueOrDefault(s, $"Trạng thái {s}") : "Không rõ";
                kq.DsMuc.Add(new Muc("Stack", $"Switch {so}", trangThai, string.Join(" · ", new[] { vaiTro, chu }.Where(x => x != null))));
            }
            return map;
        }

        // ---------------- CPU / RAM ----------------

        private static async Task DocCpuRamAsync(KetQua kq, string ip, string com, Dictionary<string, int> stack, CancellationToken ct)
        {
            var bang = Bang(await Walk(ip, com, Cpm, ct, 500), Cpm);
            var dsIdx = bang.TryGetValue(8, out var d8) ? d8.Keys.ToList() : new List<string>();
            var coRamCpm = false;

            // Tên CPU: số stack, không có thì entPhysicalName của khung chứa CPU
            var dsVatLy = dsIdx.Select(i => SnmpClient.So(O(bang, 2, i))?.ToString()).Where(x => x != null && x != "0").Distinct().ToList();
            var tenVatLy = new Dictionary<string, string>();
            var canTen = dsVatLy.Where(x => !stack.ContainsKey(x!)).ToList();
            if (canTen.Count > 0)
            {
                try
                {
                    var g = await SnmpClient.GetAsync(ip, com, canTen.Select(x => $"{EntName}.{x}"), TimeoutMs, 1, ct);
                    foreach (var x in canTen)
                        if (KhongRong(SnmpClient.Chuoi(g.GetValueOrDefault($"{EntName}.{x}"))) is string t) tenVatLy[x!] = t;
                }
                catch (Exception ex) when (ex is TimeoutException or InvalidOperationException) { }
            }

            foreach (var i in dsIdx.OrderBy(x => x))
            {
                var vl = SnmpClient.So(O(bang, 2, i))?.ToString() ?? "";
                var ten = stack.TryGetValue(vl, out var so) ? $"Switch {so}"
                    : tenVatLy.GetValueOrDefault(vl) ?? (dsIdx.Count > 1 ? $"CPU {i}" : "CPU");
                var p5 = SnmpClient.So(O(bang, 8, i));
                var p1 = SnmpClient.So(O(bang, 7, i));
                var s5 = SnmpClient.So(O(bang, 6, i));
                if (p5 != null)
                    kq.DsMuc.Add(new Muc("CPU", ten, p5 >= 95 ? Loi : p5 >= 80 ? CanhBao : Ok,
                        $"{p5}% (5 phút) · {p1}% (1 phút) · {s5}% (5 giây)", p5));

                // cpmCPUMemoryUsed / Free (KB); bản HC (.17/.19) khi 32 bit không đủ
                var dung = SnmpClient.So(O(bang, 17, i)) is long h1 and > 0 ? h1 : SnmpClient.So(O(bang, 12, i));
                var trong = SnmpClient.So(O(bang, 19, i)) is long h2 and > 0 ? h2 : SnmpClient.So(O(bang, 13, i));
                if (dung > 0 && trong >= 0)
                {
                    coRamCpm = true;
                    ThemRam(kq, ten, dung.Value * 1024, (dung.Value + trong!.Value) * 1024);
                }
            }

            if (coRamCpm) return;
            // Thiết bị cũ: lấy pool "Processor" của CISCO-MEMORY-POOL-MIB
            var pool = Bang(await Walk(ip, com, MemPool, ct, 200), MemPool);
            if (!pool.TryGetValue(2, out var dsTen)) return;
            foreach (var (idx, v) in dsTen)
            {
                var ten = SnmpClient.Chuoi(v);
                if (!string.Equals(ten, "Processor", StringComparison.OrdinalIgnoreCase)) continue;
                var dung = SnmpClient.So(O(pool, 5, idx));
                var trong = SnmpClient.So(O(pool, 6, idx));
                if (dung > 0 && trong >= 0) ThemRam(kq, "Processor", dung.Value, dung.Value + trong!.Value);
            }
        }

        private static void ThemRam(KetQua kq, string ten, long dungByte, long tongByte)
        {
            if (tongByte <= 0) return;
            var pt = Math.Round(dungByte * 100.0 / tongByte, 1);
            kq.DsMuc.Add(new Muc("Bộ nhớ", ten, pt >= 97 ? Loi : pt >= 90 ? CanhBao : Ok,
                $"{pt:0.#}% · {Gb(dungByte)} / {Gb(tongByte)}", pt));
        }

        private static string Gb(long b) => b >= 1L << 30 ? $"{b / (double)(1L << 30):0.#} GB" : $"{b / (double)(1L << 20):0} MB";

        // ---------------- Nhiệt độ / quạt / nguồn ----------------

        // ciscoEnvMonState: 1 normal, 2 warning, 3 critical, 4 shutdown, 5 notPresent, 6 notFunctioning
        private static (string trangThai, string chu) TrangThaiEnv(long? s) => s switch
        {
            1 => (Ok, "Bình thường"),
            2 => (CanhBao, "Cảnh báo"),
            3 => (Loi, "Nguy hiểm"),
            4 => (Loi, "Đã tắt"),
            5 => (KhongRo, "Không lắp"),
            6 => (Loi, "Không hoạt động"),
            _ => (KhongRo, "Không rõ")
        };

        /// <summary>Cisco 3850 gắn trạng thái vào cuối tên ("..., GREEN" / "..., Normal") — bỏ đi, trạng thái đã có cột riêng.</summary>
        private static string TenSach(string? ten, string idx)
        {
            var t = KhongRong(ten) ?? $"#{idx}";
            return Regex.Replace(t, @",\s*[A-Za-z]+$", "").Trim();
        }

        /// <summary>Tên trùng (4500 ghi "Air inlet" cho từng module) -> gắn thêm index để phân biệt.</summary>
        private static List<(string idx, string ten)> DatTen(Dictionary<string, object?>? dsTen)
        {
            var ds = (dsTen ?? new()).Select(x => (idx: x.Key, ten: TenSach(SnmpClient.Chuoi(x.Value), x.Key))).ToList();
            var trung = ds.GroupBy(x => x.ten).Where(g => g.Count() > 1).Select(g => g.Key).ToHashSet();
            return ds.Select(x => (x.idx, trung.Contains(x.ten) ? $"{x.ten} #{x.idx}" : x.ten))
                .OrderBy(x => int.TryParse(x.idx, out var n) ? n : 0).ToList();
        }

        private static async Task DocNhietDoAsync(KetQua kq, string ip, string com, CancellationToken ct)
        {
            var bang = Bang(await Walk(ip, com, EnvTemp, ct, 1000), EnvTemp);
            foreach (var (idx, ten) in DatTen(bang.GetValueOrDefault(2)))
            {
                var gt = SnmpClient.So(O(bang, 3, idx));
                var nguong = SnmpClient.So(O(bang, 4, idx));
                var st = SnmpClient.So(O(bang, 6, idx));
                if (gt == null) continue;
                var (tt, _) = TrangThaiEnv(st);
                // Thiết bị không báo trạng thái thì tự so ngưỡng; còn ≤ 5°C tới ngưỡng cũng cảnh báo sớm
                if (st == null && nguong > 0) tt = gt >= nguong ? Loi : Ok;
                if (tt == Ok && nguong > 0 && gt >= nguong - 5) tt = CanhBao;
                double? pt = nguong > 0 ? Math.Min(100, Math.Round(gt.Value * 100.0 / nguong.Value)) : null;
                kq.DsMuc.Add(new Muc("Nhiệt độ", ten, tt, nguong > 0 ? $"{gt}°C / ngưỡng {nguong}°C" : $"{gt}°C", pt));
            }
        }

        private static async Task DocQuatNguonAsync(KetQua kq, string ip, string com, string goc, string nhom, CancellationToken ct)
        {
            var bang = Bang(await Walk(ip, com, goc, ct, 500), goc);
            foreach (var (idx, ten) in DatTen(bang.GetValueOrDefault(2)))
            {
                var (tt, chu) = TrangThaiEnv(SnmpClient.So(O(bang, 3, idx)));
                // ciscoEnvMonSupplySource: 2 AC, 3 DC
                if (nhom == "Nguồn" && SnmpClient.So(O(bang, 4, idx)) is long src && src is 2 or 3 && tt != KhongRo)
                    chu += src == 2 ? " · AC" : " · DC";
                kq.DsMuc.Add(new Muc(nhom, ten, tt, chu));
            }
        }

        // ---------------- Cổng ----------------

        private static async Task DocCongAsync(KetQua kq, string ip, string com, CancellationToken ct)
        {
            // Chỉ cổng ethernet vật lý (ifType 6); bỏ Vlan, loopback, Port-channel, tunnel
            var dsType = await Walk(ip, com, IfType, ct);
            var dsIdx = dsType.Where(x => SnmpClient.So(x.GiaTri) == 6).Select(x => x.Oid[(IfType.Length + 1)..]).ToHashSet();
            if (dsIdx.Count == 0) return;

            Dictionary<string, long?> Cot(List<SnmpClient.Bien> ds, string goc)
                => ds.Where(x => x.Oid.Length > goc.Length + 1).ToDictionary(x => x.Oid[(goc.Length + 1)..], x => SnmpClient.So(x.GiaTri));

            var admin = Cot(await Walk(ip, com, IfAdmin, ct), IfAdmin);
            var oper = Cot(await Walk(ip, com, IfOper, ct), IfOper);
            // Bỏ cổng quản trị out-of-band / ảo của 9800 (oper = notPresent 6) khỏi tổng
            var dsCong = dsIdx.Where(i => oper.GetValueOrDefault(i) != 6).ToList();
            kq.SoCong = dsCong.Count;
            kq.SoCongUp = dsCong.Count(i => oper.GetValueOrDefault(i) == 1);
            kq.SoCongTat = dsCong.Count(i => admin.GetValueOrDefault(i) == 2);
            kq.SoCongDown = dsCong.Count - kq.SoCongUp - kq.SoCongTat;

            var vao = Cot(await Walk(ip, com, IfInErr, ct), IfInErr);
            var ra = Cot(await Walk(ip, com, IfOutErr, ct), IfOutErr);
            var coLoi = dsCong.Where(i => (vao.GetValueOrDefault(i) ?? 0) + (ra.GetValueOrDefault(i) ?? 0) > 0).ToList();
            if (coLoi.Count == 0) return;

            var g = await SnmpClient.GetAsync(ip, com, coLoi.SelectMany(i => new[] { $"{IfName}.{i}", $"{IfAlias}.{i}" }), TimeoutMs, 1, ct);
            kq.DsCongLoi = coLoi.Select(i => new CongLoi(int.Parse(i),
                    KhongRong(SnmpClient.Chuoi(g.GetValueOrDefault($"{IfName}.{i}"))) ?? $"if {i}",
                    KhongRong(SnmpClient.Chuoi(g.GetValueOrDefault($"{IfAlias}.{i}"))),
                    vao.GetValueOrDefault(i) ?? 0, ra.GetValueOrDefault(i) ?? 0))
                .OrderByDescending(x => x.LoiVao + x.LoiRa).ToList();
        }

        // ---------------- WLC ----------------

        private static async Task DocWlcAsync(KetQua kq, string ip, string com, CancellationToken ct)
        {
            var dsAp = await Walk(ip, com, WlcAp, ct, 2000);
            if (dsAp.Count == 0) return; // không phải WLC
            kq.SoAp = dsAp.Count;
            kq.SoClient = (await Walk(ip, com, WlcClient, ct, 20000)).Count;
        }

        private static string? KhongRong(string? v) => string.IsNullOrWhiteSpace(v) ? null : v.Trim();
    }
}
