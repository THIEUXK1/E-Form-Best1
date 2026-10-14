using System.Text.RegularExpressions;

namespace E_Form_Best.Areas.ITForm.Services
{
    /// <summary>
    /// Đọc sức khoẻ phần cứng 1 thiết bị qua SNMP v2c, tự nhận diện theo MIB đang trả lời:
    ///  - Synology (1.3.6.1.4.1.6574): hệ thống, quạt, nguồn, từng ổ cứng (bad sector, sức khoẻ), volume / storage pool, RAM.
    ///  - HPE MSA (FC-MGMT 1.3.6.1.3.94): trạng thái tổng + cảm biến nhiệt độ / điện áp / nguồn / tụ cache.
    ///    MSA không đưa ổ cứng / dung lượng pool ra SNMP.
    ///  - HPE ProLiant qua iLO (1.3.6.1.4.1.232): CPU, RAM, quạt, nguồn, nhiệt độ, ổ vật lý / logic Smart Array.
    ///  - Dell iDRAC (1.3.6.1.4.1.674.10892.5): CPU, RAM, quạt, nguồn, nhiệt độ, ổ vật lý / ổ ảo.
    ///  - Host chung (HOST-RESOURCES-MIB: Windows / Linux / ESXi): dung lượng ổ, RAM, tải CPU, thiết bị lỗi.
    /// Không đụng DB — ServerSnmpWorker / controller lưu kết quả.
    /// </summary>
    public static class ServerSnmpService
    {
        public const string Ok = "OK", CanhBao = "CANH_BAO", Loi = "LOI", KhongRo = "KHONG_RO", KhongDocDuoc = "KHONG_DOC_DUOC";

        public record Muc(string Nhom, string Ten, string TrangThai, string? GiaTri);

        public class KetQua
        {
            public string? Ip { get; set; }
            public string? Nguon { get; set; }
            public string? TenHeThong { get; set; }
            public string? MoTa { get; set; }
            public List<Muc> DsMuc { get; } = new();
            /// <summary>Dung lượng đọc được (TB nhị phân, như DSM / Windows hiển thị); null = thiết bị không cung cấp.</summary>
            public decimal? DungLuongTb { get; set; }
            public decimal? DaDungTb { get; set; }
            public string? GhiChu { get; set; }
        }

        private const string SysDescr = "1.3.6.1.2.1.1.1.0", SysObjectId = "1.3.6.1.2.1.1.2.0", SysName = "1.3.6.1.2.1.1.5.0";
        private const string SynoTrangThai = "1.3.6.1.4.1.6574.1.1.0";
        private const string HpeDieuKien = "1.3.6.1.4.1.232.6.1.3.0";
        private const string DellTrangThai = "1.3.6.1.4.1.674.10892.5.2.1.0";
        private const string HrStorage = "1.3.6.1.2.1.25.2.3.1";
        private const decimal MotTb = 1024m * 1024 * 1024 * 1024;

        private static readonly string[] ThuTuMuc = { Loi, CanhBao, KhongRo, Ok };

        /// <summary>Trạng thái nặng nhất trong các mục (LOI > CANH_BAO > OK); không có mục nào thì KHONG_RO.</summary>
        public static string TrangThaiTong(IEnumerable<Muc> ds)
        {
            var l = ds.Select(x => x.TrangThai).ToList();
            if (l.Contains(Loi)) return Loi;
            if (l.Contains(CanhBao)) return CanhBao;
            return l.Contains(Ok) ? Ok : KhongRo;
        }

        /// <summary>
        /// Đọc 1 IP; null nếu IP không trả lời SNMP (community sai / tắt SNMP / không thông mạng).
        /// </summary>
        public static async Task<KetQua?> DocAsync(string ip, string community, CancellationToken ct)
        {
            Dictionary<string, object?> nen;
            try
            {
                nen = await SnmpClient.GetAsync(ip, community,
                    new[] { SysDescr, SysObjectId, SysName, SynoTrangThai, HpeDieuKien, DellTrangThai }, 2500, 2, ct);
            }
            catch (TimeoutException) { return null; }
            catch (System.Net.Sockets.SocketException) { return null; }

            var kq = new KetQua
            {
                Ip = ip,
                TenHeThong = SnmpClient.Chuoi(nen.GetValueOrDefault(SysName)),
                MoTa = Cat(SnmpClient.Chuoi(nen.GetValueOrDefault(SysDescr)), 500)
            };

            if (SnmpClient.So(nen.GetValueOrDefault(SynoTrangThai)) != null) await SynologyAsync(kq, ip, community, ct);
            else if ((kq.MoTa ?? "").Contains("MSA", StringComparison.OrdinalIgnoreCase)) await MsaAsync(kq, ip, community, ct);
            else if (SnmpClient.So(nen.GetValueOrDefault(DellTrangThai)) != null) await DellAsync(kq, ip, community, ct);
            else if (SnmpClient.So(nen.GetValueOrDefault(HpeDieuKien)) != null) await HpeAsync(kq, ip, community, ct);
            else await HostAsync(kq, ip, community, ct, chiDungLuong: false);

            return kq;
        }

        // ================= Synology =================

        private static async Task SynologyAsync(KetQua kq, string ip, string com, CancellationToken ct)
        {
            kq.Nguon = "Synology";
            const string g = "1.3.6.1.4.1.6574.1.";
            var ht = await SnmpClient.GetAsync(ip, com, new[]
            {
                g + "1.0", g + "2.0", g + "3.0", g + "4.1.0", g + "4.2.0", g + "5.1.0", g + "5.2.0", g + "5.3.0",
                "1.3.6.1.4.1.2021.4.5.0", "1.3.6.1.4.1.2021.4.6.0", "1.3.6.1.4.1.2021.4.14.0", "1.3.6.1.4.1.2021.4.15.0",
                "1.3.6.1.4.1.2021.11.11.0"
            }, 2500, 2, ct);

            kq.MoTa = string.Join(" · ", new[] { SnmpClient.Chuoi(ht.GetValueOrDefault(g + "5.1.0")),
                "SN " + SnmpClient.Chuoi(ht.GetValueOrDefault(g + "5.2.0")), SnmpClient.Chuoi(ht.GetValueOrDefault(g + "5.3.0")) }
                .Where(s => !string.IsNullOrWhiteSpace(s) && s != "SN "));

            // 1 = Normal, 2 = Failed
            string BinhThuong(object? v) => SnmpClient.So(v) switch { 1 => Ok, 2 => Loi, _ => KhongRo };
            kq.DsMuc.Add(new Muc("Tổng thể", "Hệ thống", BinhThuong(ht.GetValueOrDefault(g + "1.0")), null));
            var nhietDo = SnmpClient.So(ht.GetValueOrDefault(g + "2.0"));
            kq.DsMuc.Add(new Muc("Nhiệt độ", "Hệ thống", nhietDo >= 60 ? CanhBao : nhietDo != null ? Ok : KhongRo, nhietDo + " °C"));
            kq.DsMuc.Add(new Muc("Nguồn", "Bộ nguồn", BinhThuong(ht.GetValueOrDefault(g + "3.0")), null));
            kq.DsMuc.Add(new Muc("Quạt", "Quạt hệ thống", BinhThuong(ht.GetValueOrDefault(g + "4.1.0")), null));
            kq.DsMuc.Add(new Muc("Quạt", "Quạt CPU", BinhThuong(ht.GetValueOrDefault(g + "4.2.0")), null));

            // RAM (UCD-SNMP, KB): dùng = tổng - trống - buffer - cache
            var ramTong = SnmpClient.So(ht.GetValueOrDefault("1.3.6.1.4.1.2021.4.5.0"));
            if (ramTong > 0)
            {
                var trong = (SnmpClient.So(ht.GetValueOrDefault("1.3.6.1.4.1.2021.4.6.0")) ?? 0)
                            + (SnmpClient.So(ht.GetValueOrDefault("1.3.6.1.4.1.2021.4.14.0")) ?? 0)
                            + (SnmpClient.So(ht.GetValueOrDefault("1.3.6.1.4.1.2021.4.15.0")) ?? 0);
                var tyLe = Math.Clamp(1 - (double)trong / ramTong.Value, 0, 1);
                kq.DsMuc.Add(new Muc("RAM", "Bộ nhớ", tyLe >= 0.95 ? CanhBao : Ok,
                    $"{ramTong.Value / 1024d / 1024:0.#} GB · dùng {tyLe:P0}"));
            }
            var cpuRanh = SnmpClient.So(ht.GetValueOrDefault("1.3.6.1.4.1.2021.11.11.0"));
            if (cpuRanh != null)
                kq.DsMuc.Add(new Muc("CPU", "Tải CPU", 100 - cpuRanh >= 90 ? CanhBao : Ok, $"{100 - cpuRanh}%"));

            // Ổ cứng: 2 id, 3 model, 5 status, 6 nhiệt độ, 9 bad sector, 11 tuổi thọ còn lại, 13 sức khoẻ
            var o = await BangAsync(ip, com, "1.3.6.1.4.1.6574.2.1.1", new[] { 2, 3, 5, 6, 9, 11, 13 }, ct);
            foreach (var (_, c) in o.OrderBy(x => SoTrongChuoi(SnmpClient.Chuoi(x.Value.GetValueOrDefault(2)))))
            {
                var ten = SnmpClient.Chuoi(c.GetValueOrDefault(2)) ?? "Ổ";
                var tinhTrang = SnmpClient.So(c.GetValueOrDefault(5));     // 1 Normal 2 Initialized 3 NotInit 4 SysPartFailed 5 Crashed
                var sucKhoe = SnmpClient.So(c.GetValueOrDefault(13));      // 1 Normal 2 Warning 3 Critical 4 Failing
                var bad = SnmpClient.So(c.GetValueOrDefault(9)) ?? 0;
                var tuoiTho = SnmpClient.So(c.GetValueOrDefault(11));
                var nd = SnmpClient.So(c.GetValueOrDefault(6));

                var tt = tinhTrang is 4 or 5 || sucKhoe is 3 or 4 ? Loi
                    : sucKhoe == 2 || bad > 0 || tuoiTho is >= 0 and < 10 ? CanhBao
                    : tinhTrang is 1 or 2 ? Ok : KhongRo;
                var giaTri = new List<string?> { SnmpClient.Chuoi(c.GetValueOrDefault(3)), nd != null ? nd + " °C" : null };
                if (bad > 0) giaTri.Add($"{bad} bad sector");
                if (tuoiTho >= 0) giaTri.Add($"tuổi thọ còn {tuoiTho}%");
                if (sucKhoe is 2 or 3 or 4) giaTri.Add(sucKhoe == 2 ? "SMART cảnh báo" : sucKhoe == 3 ? "SMART nghiêm trọng" : "Sắp hỏng");
                if (tinhTrang is 3 or 4 or 5) giaTri.Add(tinhTrang == 3 ? "chưa khởi tạo" : tinhTrang == 4 ? "lỗi phân vùng hệ thống" : "hỏng");
                kq.DsMuc.Add(new Muc("Ổ cứng", ten, tt, Noi(giaTri)));
            }

            // Volume / Storage pool: 2 tên, 3 trạng thái, 4 trống (byte), 5 tổng (byte)
            var raid = await BangAsync(ip, com, "1.3.6.1.4.1.6574.3.1.1", new[] { 2, 3, 4, 5 }, ct);
            decimal tong = 0, dung = 0;
            foreach (var (_, c) in raid.OrderBy(x => SnmpClient.Chuoi(x.Value.GetValueOrDefault(2))))
            {
                var ten = SnmpClient.Chuoi(c.GetValueOrDefault(2)) ?? "";
                var st = SnmpClient.So(c.GetValueOrDefault(3));
                var tt = st switch
                {
                    1 => Ok,
                    11 or 12 or 15 or 16 or 17 or 18 or 19 or 20 or 21 => Loi,   // Degrade, Crashed, Unknown/lỗi
                    null => KhongRo,
                    _ => CanhBao                                                // đang sửa / đồng bộ / mở rộng...
                };
                var tenTt = st switch
                {
                    1 => null, 2 => "đang sửa", 3 => "đang di chuyển", 4 => "đang mở rộng", 7 => "đang đồng bộ RAID",
                    8 => "đang kiểm parity", 11 => "SUY GIẢM (Degrade)", 12 => "HỎNG (Crashed)", 13 => "đang data scrubbing",
                    _ => st != null ? "trạng thái " + st : null
                };
                if (tenTt == "đang data scrubbing") tt = Ok;

                var t = SnmpClient.So(c.GetValueOrDefault(5)) ?? 0;
                var tr = SnmpClient.So(c.GetValueOrDefault(4)) ?? 0;
                // Chỉ volume mới có dung lượng dùng thật; storage pool chỉ lấy trạng thái
                if (ten.StartsWith("Volume", StringComparison.OrdinalIgnoreCase) && t > 0)
                {
                    tong += t;
                    dung += t - tr;
                    var tyLe = (double)(t - tr) / t;
                    if (tt == Ok && tyLe >= 0.9) tt = CanhBao;
                    kq.DsMuc.Add(new Muc("Dung lượng", ten, tt,
                        Noi(new[] { $"{(t - tr) / MotTb:0.##} / {t / MotTb:0.##} TB ({tyLe:P0})", tenTt })));
                }
                else kq.DsMuc.Add(new Muc("RAID", ten, tt, tenTt ?? "Bình thường"));
            }
            if (tong > 0) { kq.DungLuongTb = Math.Round(tong / MotTb, 2); kq.DaDungTb = Math.Round(dung / MotTb, 2); }
        }

        // ================= HPE MSA (FC-MGMT) =================

        private static async Task MsaAsync(KetQua kq, string ip, string com, CancellationToken ct)
        {
            kq.Nguon = "HPE MSA";
            kq.GhiChu = "MSA không cung cấp tình trạng từng ổ cứng / dung lượng pool qua SNMP — xem trên giao diện quản trị MSA.";

            // connUnitStatus: 1 unknown 2 unused 3 ok 4 warning 5 failed
            var unit = await BangAsync(ip, com, "1.3.6.1.3.94.1.6.1", new[] { 6, 7, 8 }, ct);
            foreach (var (_, c) in unit)
            {
                kq.MoTa = Noi(new[] { SnmpClient.Chuoi(c.GetValueOrDefault(7)), "SN " + SnmpClient.Chuoi(c.GetValueOrDefault(8)) });
                kq.DsMuc.Add(new Muc("Tổng thể", "Trạng thái tủ đĩa", TrangThaiFc(SnmpClient.So(c.GetValueOrDefault(6))), null));
            }

            // connUnitSensorTable: 3 tên, 4 trạng thái (1 unknown 2 other 3 ok 4 warning 5 failed), 6 thông điệp "Tên: giá trị"
            var cb = await BangAsync(ip, com, "1.3.6.1.3.94.1.8.1", new[] { 3, 4, 6 }, ct);
            var daCo = new HashSet<string>();
            foreach (var (_, c) in cb.OrderBy(x => SoCuoi(x.Key)))
            {
                var ten = SnmpClient.Chuoi(c.GetValueOrDefault(3)) ?? "";
                var tn = SnmpClient.Chuoi(c.GetValueOrDefault(6)) ?? "";
                var giaTri = tn.StartsWith(ten + ":") ? tn[(ten.Length + 1)..].Trim() : tn;
                var nhom = NhomCamBien(ten);
                // Bảng cảm biến lặp lại khối enclosure cho từng controller: bỏ dòng trùng tên + giá trị
                if (!daCo.Add(ten + "|" + giaTri + "|" + SnmpClient.So(c.GetValueOrDefault(4)))) continue;
                kq.DsMuc.Add(new Muc(nhom, ten, TrangThaiFc(SnmpClient.So(c.GetValueOrDefault(4))), giaTri));
            }
        }

        private static string TrangThaiFc(long? v) => v switch { 3 => Ok, 4 => CanhBao, 5 => Loi, _ => KhongRo };

        private static string NhomCamBien(string ten)
        {
            if (ten.Contains("Overall", StringComparison.OrdinalIgnoreCase)) return "Tổng thể";
            if (ten.Contains("Fan", StringComparison.OrdinalIgnoreCase)) return "Quạt";
            if (ten.Contains("Temp", StringComparison.OrdinalIgnoreCase)) return "Nhiệt độ";
            if (ten.Contains("Capacitor", StringComparison.OrdinalIgnoreCase)) return "Tụ cache";
            // "Voltage 12V Loc: left-PSU" là số đo điện áp, xét trước "PSU"
            if (ten.Contains("Voltage", StringComparison.OrdinalIgnoreCase) || ten.Contains("Current", StringComparison.OrdinalIgnoreCase)) return "Điện áp";
            if (ten.Contains("PSU", StringComparison.OrdinalIgnoreCase) || ten.Contains("Power", StringComparison.OrdinalIgnoreCase)) return "Nguồn";
            return "Khác";
        }

        // ================= HPE ProLiant (iLO, CPQ MIB) =================

        // Điều kiện CPQ: 1 other, 2 ok, 3 degraded, 4 failed
        private static string TrangThaiCpq(long? v) => v switch { 2 => Ok, 3 => CanhBao, 4 => Loi, _ => KhongRo };

        private static async Task HpeAsync(KetQua kq, string ip, string com, CancellationToken ct)
        {
            kq.Nguon = "HPE ProLiant";
            const string g = "1.3.6.1.4.1.232.";
            var ht = await SnmpClient.GetAsync(ip, com, new[]
            {
                g + "6.1.3.0", g + "2.2.4.2.0", g + "2.2.2.1.0", g + "6.2.14.4.0", g + "6.2.9.1.0", g + "6.2.6.4.0", g + "6.2.6.1.0", g + "3.1.3.0"
            }, 2500, 2, ct);
            var model = SnmpClient.Chuoi(ht.GetValueOrDefault(g + "2.2.4.2.0"));
            var serial = SnmpClient.Chuoi(ht.GetValueOrDefault(g + "2.2.2.1.0"));
            if (!string.IsNullOrWhiteSpace(model)) kq.MoTa = Noi(new[] { model, string.IsNullOrWhiteSpace(serial) ? null : "SN " + serial });

            kq.DsMuc.Add(new Muc("Tổng thể", "Tình trạng máy chủ", TrangThaiCpq(SnmpClient.So(ht.GetValueOrDefault(g + "6.1.3.0"))), null));

            // CPU: cpqSeCpuTable 3 tên, 4 MHz, 6 trạng thái (2 ok, 3 degraded, 4 failed, 5 disabled)
            foreach (var (_, c) in (await BangAsync(ip, com, g + "1.2.2.1.1", new[] { 3, 4, 6 }, ct)).OrderBy(x => SoCuoi(x.Key)))
            {
                var st = SnmpClient.So(c.GetValueOrDefault(6));
                kq.DsMuc.Add(new Muc("CPU", SnmpClient.Chuoi(c.GetValueOrDefault(3)) ?? "CPU",
                    st switch { 2 => Ok, 3 => CanhBao, 4 => Loi, 5 => CanhBao, _ => KhongRo },
                    SnmpClient.So(c.GetValueOrDefault(4)) is long mhz and > 0 ? $"{mhz} MHz" : null));
            }

            // RAM: cpqHeResMem2ModuleTable 13 vị trí, 6 dung lượng KB, 20 điều kiện; không có bảng thì lấy điều kiện chung
            var ram = await BangAsync(ip, com, g + "6.2.14.13.1", new[] { 6, 13, 20 }, ct);
            var coModule = false;
            foreach (var (_, c) in ram.OrderBy(x => SoCuoi(x.Key)))
            {
                var kb = SnmpClient.So(c.GetValueOrDefault(6)) ?? 0;
                if (kb <= 0) continue; // khe trống
                coModule = true;
                kq.DsMuc.Add(new Muc("RAM", SnmpClient.Chuoi(c.GetValueOrDefault(13)) ?? "DIMM",
                    TrangThaiCpq(SnmpClient.So(c.GetValueOrDefault(20))), $"{kb / 1024d / 1024:0.#} GB"));
            }
            if (!coModule)
                kq.DsMuc.Add(new Muc("RAM", "Bộ nhớ", TrangThaiCpq(SnmpClient.So(ht.GetValueOrDefault(g + "6.2.14.4.0"))), null));

            // Quạt: cpqHeFltTolFanTable 3 vị trí, 9 điều kiện
            var quat = await BangAsync(ip, com, g + "6.2.6.7.1", new[] { 3, 9 }, ct);
            foreach (var (k, c) in quat.OrderBy(x => SoCuoi(x.Key)))
                kq.DsMuc.Add(new Muc("Quạt", "Quạt " + SoCuoi(k) + ViTriCpq(SnmpClient.So(c.GetValueOrDefault(3))),
                    TrangThaiCpq(SnmpClient.So(c.GetValueOrDefault(9))), null));
            if (quat.Count == 0)
                kq.DsMuc.Add(new Muc("Quạt", "Quạt hệ thống", TrangThaiCpq(SnmpClient.So(ht.GetValueOrDefault(g + "6.2.6.4.0"))), null));

            // Nguồn: cpqHeFltTolPowerSupplyTable 4 điều kiện, 7 công suất (W)
            var nguon = await BangAsync(ip, com, g + "6.2.9.3.1", new[] { 4, 7 }, ct);
            foreach (var (k, c) in nguon.OrderBy(x => SoCuoi(x.Key)))
                kq.DsMuc.Add(new Muc("Nguồn", "Bộ nguồn " + SoCuoi(k), TrangThaiCpq(SnmpClient.So(c.GetValueOrDefault(4))),
                    SnmpClient.So(c.GetValueOrDefault(7)) is long w and > 0 ? $"{w} W" : null));
            if (nguon.Count == 0)
                kq.DsMuc.Add(new Muc("Nguồn", "Bộ nguồn", TrangThaiCpq(SnmpClient.So(ht.GetValueOrDefault(g + "6.2.9.1.0"))), null));

            // Nhiệt độ: cpqHeTemperatureTable 3 vị trí, 4 °C, 5 ngưỡng, 6 điều kiện
            foreach (var (k, c) in (await BangAsync(ip, com, g + "6.2.6.8.1", new[] { 3, 4, 5, 6 }, ct)).OrderBy(x => SoCuoi(x.Key)))
            {
                var nd = SnmpClient.So(c.GetValueOrDefault(4));
                if (nd is null or <= 0) continue; // cảm biến không lắp
                var nguong = SnmpClient.So(c.GetValueOrDefault(5));
                kq.DsMuc.Add(new Muc("Nhiệt độ", "Cảm biến " + SoCuoi(k) + ViTriCpq(SnmpClient.So(c.GetValueOrDefault(3))),
                    TrangThaiCpq(SnmpClient.So(c.GetValueOrDefault(6))), $"{nd} °C" + (nguong > 0 ? $" (ngưỡng {nguong} °C)" : "")));
            }

            // Ổ vật lý Smart Array: cpqDaPhyDrvTable 3 model, 37 điều kiện, 45 dung lượng MB, 64 vị trí
            var oVatLy = await BangAsync(ip, com, g + "3.2.5.1.1", new[] { 3, 37, 45, 64 }, ct);
            foreach (var (k, c) in oVatLy.OrderBy(x => SnmpClient.Chuoi(x.Value.GetValueOrDefault(64)) ?? x.Key))
            {
                var mb = SnmpClient.So(c.GetValueOrDefault(45)) ?? 0;
                kq.DsMuc.Add(new Muc("Ổ cứng", SnmpClient.Chuoi(c.GetValueOrDefault(64)) ?? "Ổ " + SoCuoi(k),
                    TrangThaiCpq(SnmpClient.So(c.GetValueOrDefault(37))),
                    Noi(new[] { SnmpClient.Chuoi(c.GetValueOrDefault(3))?.Trim(), mb > 0 ? $"{mb / 1024d:0} GB" : null })));
            }
            // Ổ logic (RAID): cpqDaLogDrvTable 4 trạng thái (2 ok, 3 failed, 7 rebuilding, ...), 9 dung lượng MB, 11 điều kiện
            foreach (var (k, c) in (await BangAsync(ip, com, g + "3.2.3.1.1", new[] { 4, 9, 11 }, ct)).OrderBy(x => SoCuoi(x.Key)))
            {
                var st = SnmpClient.So(c.GetValueOrDefault(4));
                var mb = SnmpClient.So(c.GetValueOrDefault(9)) ?? 0;
                kq.DsMuc.Add(new Muc("RAID", "Ổ logic " + SoCuoi(k), TrangThaiCpq(SnmpClient.So(c.GetValueOrDefault(11))),
                    Noi(new[] { mb > 0 ? $"{mb / 1024d / 1024:0.##} TB" : null,
                        st switch { 2 => "Bình thường", 3 => "Hỏng", 5 => "Đang phục hồi", 6 => "Chờ rebuild", 7 => "Đang rebuild", 10 => "Đang mở rộng", null => null, _ => "trạng thái " + st } })));
            }
            if (oVatLy.Count == 0)
                kq.DsMuc.Add(new Muc("Ổ cứng", "Mảng đĩa", TrangThaiCpq(SnmpClient.So(ht.GetValueOrDefault(g + "3.1.3.0"))), null));
        }

        // cpqHeTemperatureLocale / cpqHeFltTolFanLocale
        private static string ViTriCpq(long? v) => v switch
        {
            3 => " · hệ thống", 4 => " · bo mạch", 5 => " · khay I/O", 6 => " · CPU", 7 => " · RAM", 8 => " · ổ đĩa",
            9 => " · nguồn", 10 => " · môi trường", 11 => " · chassis", _ => ""
        };

        // ================= Dell iDRAC =================

        // ObjectStatusEnum: 1 other, 2 unknown, 3 ok, 4 nonCritical, 5 critical, 6 nonRecoverable
        private static string TrangThaiDell(long? v) => v switch { 3 => Ok, 4 => CanhBao, 5 or 6 => Loi, _ => KhongRo };

        private static async Task DellAsync(KetQua kq, string ip, string com, CancellationToken ct)
        {
            kq.Nguon = "Dell iDRAC";
            const string g = "1.3.6.1.4.1.674.10892.5.";
            var ht = await SnmpClient.GetAsync(ip, com, new[] { g + "2.1.0", g + "1.3.12.0", g + "1.3.2.0" }, 2500, 2, ct);
            var model = SnmpClient.Chuoi(ht.GetValueOrDefault(g + "1.3.12.0"));
            var tag = SnmpClient.Chuoi(ht.GetValueOrDefault(g + "1.3.2.0"));
            if (!string.IsNullOrWhiteSpace(model)) kq.MoTa = Noi(new[] { model, string.IsNullOrWhiteSpace(tag) ? null : "Service Tag " + tag });
            kq.DsMuc.Add(new Muc("Tổng thể", "Tình trạng máy chủ", TrangThaiDell(SnmpClient.So(ht.GetValueOrDefault(g + "2.1.0"))), null));

            // CPU: processorDeviceTable 5 trạng thái, 23 tên
            foreach (var (k, c) in (await BangAsync(ip, com, g + "4.1100.30.1", new[] { 5, 23 }, ct)).OrderBy(x => SoCuoi(x.Key)))
                kq.DsMuc.Add(new Muc("CPU", SnmpClient.Chuoi(c.GetValueOrDefault(23))?.Trim() ?? "CPU " + SoCuoi(k),
                    TrangThaiDell(SnmpClient.So(c.GetValueOrDefault(5))), null));

            // RAM: memoryDeviceTable 5 trạng thái, 8 vị trí, 14 dung lượng KB
            foreach (var (_, c) in (await BangAsync(ip, com, g + "4.1100.50.1", new[] { 5, 8, 14 }, ct)).OrderBy(x => SoCuoi(x.Key)))
            {
                var kb = SnmpClient.So(c.GetValueOrDefault(14)) ?? 0;
                kq.DsMuc.Add(new Muc("RAM", SnmpClient.Chuoi(c.GetValueOrDefault(8)) ?? "DIMM",
                    TrangThaiDell(SnmpClient.So(c.GetValueOrDefault(5))), kb > 0 ? $"{kb / 1024d / 1024:0.#} GB" : null));
            }

            // Quạt: coolingDeviceTable 5 trạng thái, 6 RPM, 8 vị trí
            foreach (var (_, c) in (await BangAsync(ip, com, g + "4.700.12.1", new[] { 5, 6, 8 }, ct)).OrderBy(x => SoCuoi(x.Key)))
                kq.DsMuc.Add(new Muc("Quạt", SnmpClient.Chuoi(c.GetValueOrDefault(8)) ?? "Quạt",
                    TrangThaiDell(SnmpClient.So(c.GetValueOrDefault(5))),
                    SnmpClient.So(c.GetValueOrDefault(6)) is long rpm and > 0 ? $"{rpm} RPM" : null));

            // Nguồn: powerSupplyTable 5 trạng thái, 8 vị trí
            foreach (var (_, c) in (await BangAsync(ip, com, g + "4.600.12.1", new[] { 5, 8 }, ct)).OrderBy(x => SoCuoi(x.Key)))
                kq.DsMuc.Add(new Muc("Nguồn", SnmpClient.Chuoi(c.GetValueOrDefault(8)) ?? "Bộ nguồn",
                    TrangThaiDell(SnmpClient.So(c.GetValueOrDefault(5))), null));

            // Nhiệt độ: temperatureProbeTable 5 trạng thái, 6 giá trị (0.1 °C), 8 vị trí
            foreach (var (_, c) in (await BangAsync(ip, com, g + "4.700.20.1", new[] { 5, 6, 8 }, ct)).OrderBy(x => SoCuoi(x.Key)))
                kq.DsMuc.Add(new Muc("Nhiệt độ", SnmpClient.Chuoi(c.GetValueOrDefault(8)) ?? "Cảm biến",
                    TrangThaiDell(SnmpClient.So(c.GetValueOrDefault(5))),
                    SnmpClient.So(c.GetValueOrDefault(6)) is long nd ? $"{nd / 10d:0.#} °C" : null));

            // Ổ vật lý: physicalDiskTable 2 tên, 11 dung lượng MB, 24 componentStatus
            foreach (var (_, c) in (await BangAsync(ip, com, g + "5.1.20.130.4.1", new[] { 2, 11, 24 }, ct)).OrderBy(x => SoCuoi(x.Key)))
            {
                var mb = SnmpClient.So(c.GetValueOrDefault(11)) ?? 0;
                kq.DsMuc.Add(new Muc("Ổ cứng", SnmpClient.Chuoi(c.GetValueOrDefault(2)) ?? "Ổ",
                    TrangThaiDell(SnmpClient.So(c.GetValueOrDefault(24))), mb > 0 ? $"{mb / 1024d:0} GB" : null));
            }
            // Ổ ảo (RAID): virtualDiskTable 2 tên, 6 dung lượng MB, 20 componentStatus
            foreach (var (_, c) in (await BangAsync(ip, com, g + "5.1.20.140.1.1", new[] { 2, 6, 20 }, ct)).OrderBy(x => SoCuoi(x.Key)))
            {
                var mb = SnmpClient.So(c.GetValueOrDefault(6)) ?? 0;
                kq.DsMuc.Add(new Muc("RAID", SnmpClient.Chuoi(c.GetValueOrDefault(2)) ?? "Ổ ảo",
                    TrangThaiDell(SnmpClient.So(c.GetValueOrDefault(20))), mb > 0 ? $"{mb / 1024d / 1024:0.##} TB" : null));
            }
        }

        // ================= Host chung (HOST-RESOURCES-MIB) =================

        /// <summary>
        /// Windows / Linux / ESXi: dung lượng từng ổ, RAM, tải CPU, thiết bị báo lỗi.
        /// chiDungLuong = true khi đã có số liệu phần cứng từ IP quản trị, chỉ bổ sung dung lượng.
        /// </summary>
        public static async Task HostAsync(KetQua kq, string ip, string com, CancellationToken ct, bool chiDungLuong)
        {
            kq.Nguon ??= (kq.MoTa ?? "").Contains("VMkernel", StringComparison.OrdinalIgnoreCase) ? "VMware ESXi"
                       : (kq.MoTa ?? "").Contains("Windows", StringComparison.OrdinalIgnoreCase) ? "Windows" : "Host";

            // hrStorageTable: 2 loại, 3 mô tả, 4 đơn vị (byte), 5 tổng, 6 đã dùng
            var kho = await BangAsync(ip, com, HrStorage, new[] { 2, 3, 4, 5, 6 }, ct);
            decimal tong = 0, dung = 0;
            foreach (var (_, c) in kho.OrderBy(x => SnmpClient.Chuoi(x.Value.GetValueOrDefault(3))))
            {
                var loai = c.GetValueOrDefault(2) as string;
                var donVi = SnmpClient.So(c.GetValueOrDefault(4)) ?? 0;
                var t = (SnmpClient.So(c.GetValueOrDefault(5)) ?? 0) * (decimal)donVi;
                var d = (SnmpClient.So(c.GetValueOrDefault(6)) ?? 0) * (decimal)donVi;
                if (t <= 0) continue;
                var ten = SnmpClient.Chuoi(c.GetValueOrDefault(3)) ?? "";
                var tyLe = (double)(d / t);

                if (loai == "1.3.6.1.2.1.25.2.1.4")                        // hrStorageFixedDisk
                {
                    // Linux liệt kê cả /dev, /run, /boot... nhỏ: bỏ phân vùng dưới 1 GB
                    if (t < 1024m * 1024 * 1024) continue;
                    tong += t;
                    dung += d;
                    kq.DsMuc.Add(new Muc("Dung lượng", RutGonO(ten), tyLe >= 0.95 ? Loi : tyLe >= 0.9 ? CanhBao : Ok,
                        $"{DinhDangByte(d)} / {DinhDangByte(t)} ({tyLe:P0})"));
                }
                else if (loai == "1.3.6.1.2.1.25.2.1.2" && !chiDungLuong) // hrStorageRam
                    kq.DsMuc.Add(new Muc("RAM", "Bộ nhớ", tyLe >= 0.95 ? CanhBao : Ok, $"{DinhDangByte(t)} · dùng {tyLe:P0}"));
            }
            if (tong > 0) { kq.DungLuongTb = Math.Round(tong / MotTb, 2); kq.DaDungTb = Math.Round(dung / MotTb, 2); }
            if (chiDungLuong) return;

            // Tải CPU trung bình (hrProcessorLoad)
            var tai = await SnmpClient.WalkAsync(ip, com, "1.3.6.1.2.1.25.3.3.1.2", 2500, 2, 512, ct);
            var dsTai = tai.Select(x => SnmpClient.So(x.GiaTri)).Where(x => x != null).Select(x => x!.Value).ToList();
            if (dsTai.Count > 0)
            {
                var tb = dsTai.Average();
                kq.DsMuc.Add(new Muc("CPU", $"{dsTai.Count} nhân", tb >= 90 ? CanhBao : Ok, $"tải {tb:0}%"));
            }

            // hrDeviceTable: 3 mô tả, 5 trạng thái (1 unknown 2 running 3 warning 4 testing 5 down) — chỉ báo thiết bị bất thường
            foreach (var (_, c) in await BangAsync(ip, com, "1.3.6.1.2.1.25.3.2.1", new[] { 3, 5 }, ct))
            {
                var st = SnmpClient.So(c.GetValueOrDefault(5));
                if (st is 3 or 5)
                    kq.DsMuc.Add(new Muc("Thiết bị", SnmpClient.Chuoi(c.GetValueOrDefault(3)) ?? "Thiết bị", st == 5 ? Loi : CanhBao,
                        st == 5 ? "down" : "warning"));
            }
        }

        // ================= Tiện ích =================

        /// <summary>Đi từng cột của bảng SNMP, gom theo chỉ số dòng: chỉ số -> (cột -> giá trị).</summary>
        private static async Task<Dictionary<string, Dictionary<int, object?>>> BangAsync(string ip, string com, string goc, int[] cot, CancellationToken ct)
        {
            var kq = new Dictionary<string, Dictionary<int, object?>>();
            foreach (var c in cot)
            {
                var tienTo = $"{goc}.{c}.";
                List<SnmpClient.Bien> ds;
                try { ds = await SnmpClient.WalkAsync(ip, com, $"{goc}.{c}", 2500, 2, 2000, ct); }
                catch (TimeoutException) { continue; }
                foreach (var b in ds)
                {
                    var chiSo = b.Oid[tienTo.Length..];
                    if (!kq.TryGetValue(chiSo, out var dong)) kq[chiSo] = dong = new Dictionary<int, object?>();
                    dong[c] = b.GiaTri;
                }
            }
            return kq;
        }

        private static long SoCuoi(string chiSo) => long.TryParse(chiSo[(chiSo.LastIndexOf('.') + 1)..], out var n) ? n : 0;

        private static int SoTrongChuoi(string? s)
        {
            var m = Regex.Match(s ?? "", @"\d+");
            return m.Success ? int.Parse(m.Value) : int.MaxValue;
        }

        private static string? Noi(IEnumerable<string?> ds)
        {
            var s = string.Join(" · ", ds.Where(x => !string.IsNullOrWhiteSpace(x)));
            return s.Length == 0 ? null : s;
        }

        private static string RutGonO(string ten)
        {
            // Windows: "C:\ Label:OS  Serial Number 1a2b3c" -> "C:\ OS"
            var m = Regex.Match(ten, @"^([A-Z]:\\)\s*Label:(.*?)\s+Serial Number", RegexOptions.IgnoreCase);
            return m.Success ? (m.Groups[1].Value + " " + m.Groups[2].Value).Trim() : ten;
        }

        private static string DinhDangByte(decimal b)
            => b >= MotTb ? $"{b / MotTb:0.##} TB" : $"{b / (1024m * 1024 * 1024):0.#} GB";

        private static string? Cat(string? s, int toiDa) => s == null ? null : s.Length > toiDa ? s[..toiDa] : s;
    }
}
