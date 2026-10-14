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

        /// <summary>
        /// Một mục sức khoẻ. ChiTiet: bảng phụ khi bấm mở (SMART từng ổ...). Khoa: gắn với bộ đếm lưu lượng
        /// để ServerSnmpWorker tính tốc độ so với lần đọc trước.
        /// </summary>
        public record Muc(string Nhom, string Ten, string TrangThai, string? GiaTri,
            Dictionary<string, string>? ChiTiet = null, string? Khoa = null);

        /// <summary>Bộ đếm cộng dồn (byte) của cổng mạng / ổ đĩa — tốc độ = chênh lệch giữa 2 lần đọc.</summary>
        public record BoDem(string Khoa, string Loai, ulong Vao, ulong Ra);

        /// <summary>Sự kiện trong nhật ký thiết bị (MSA). MucDo: Lỗi / Cảnh báo / Thông tin.</summary>
        public record SuKien(DateTime? ThoiGian, string MucDo, string NoiDung);

        public class KetQua
        {
            public string? Ip { get; set; }
            public string? Nguon { get; set; }
            public string? TenHeThong { get; set; }
            public string? MoTa { get; set; }
            /// <summary>Thời gian chạy liên tục (giây) từ lần khởi động gần nhất.</summary>
            public long? ThoiGianChay { get; set; }
            public List<Muc> DsMuc { get; } = new();
            public List<BoDem> DsBoDem { get; } = new();
            public List<SuKien> DsSuKien { get; } = new();
            /// <summary>Thông tin tĩnh không mang trạng thái (firmware, BIOS, vị trí SNMP, load, phiên kết nối...) — hiện thành khối riêng.</summary>
            public Dictionary<string, string> ThongTin { get; } = new();
            /// <summary>Dung lượng đọc được (TB nhị phân, như DSM / Windows hiển thị); null = thiết bị không cung cấp.</summary>
            public decimal? DungLuongTb { get; set; }
            public decimal? DaDungTb { get; set; }
            public string? GhiChu { get; set; }
        }

        private const string SysDescr = "1.3.6.1.2.1.1.1.0", SysObjectId = "1.3.6.1.2.1.1.2.0", SysName = "1.3.6.1.2.1.1.5.0";
        private const string SysUpTime = "1.3.6.1.2.1.1.3.0", HrUptime = "1.3.6.1.2.1.25.1.1.0";
        private const string SysContact = "1.3.6.1.2.1.1.4.0", SysLocation = "1.3.6.1.2.1.1.6.0";
        private const string HrSoNguoiDung = "1.3.6.1.2.1.25.1.5.0", HrSoTienTrinh = "1.3.6.1.2.1.25.1.6.0";
        // UCD-SNMP laLoad 1 / 5 / 15 phút (Linux, Synology); Windows không có
        private static readonly string[] LaLoad = { "1.3.6.1.4.1.2021.10.1.3.1", "1.3.6.1.4.1.2021.10.1.3.2", "1.3.6.1.4.1.2021.10.1.3.3" };
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
                    new[] { SysDescr, SysObjectId, SysName, SynoTrangThai, HpeDieuKien, DellTrangThai, SysUpTime, HrUptime, SysLocation, SysContact }, 2500, 2, ct);
            }
            catch (TimeoutException) { return null; }
            catch (System.Net.Sockets.SocketException) { return null; }

            var kq = new KetQua
            {
                Ip = ip,
                // iLO trả FQDN rỗng miền dạng "BPVN-HyperV03." — bỏ dấu chấm cuối
                TenHeThong = SnmpClient.Chuoi(nen.GetValueOrDefault(SysName))?.TrimEnd('.'),
                MoTa = Cat(SnmpClient.Chuoi(nen.GetValueOrDefault(SysDescr)), 500),
                // hrSystemUptime là thời gian chạy của máy; sysUpTime chỉ là của dịch vụ SNMP (reset khi restart snmpd)
                ThoiGianChay = (SnmpClient.So(nen.GetValueOrDefault(HrUptime)) ?? SnmpClient.So(nen.GetValueOrDefault(SysUpTime))) / 100
            };
            ThemTt(kq, "Vị trí (SNMP)", SnmpClient.Chuoi(nen.GetValueOrDefault(SysLocation)));
            ThemTt(kq, "Liên hệ (SNMP)", SnmpClient.Chuoi(nen.GetValueOrDefault(SysContact)));

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
                g + "1.0", g + "2.0", g + "3.0", g + "4.1.0", g + "4.2.0", g + "5.1.0", g + "5.2.0", g + "5.3.0", g + "5.4.0",
                "1.3.6.1.4.1.2021.4.5.0", "1.3.6.1.4.1.2021.4.6.0", "1.3.6.1.4.1.2021.4.14.0", "1.3.6.1.4.1.2021.4.15.0",
                "1.3.6.1.4.1.2021.11.11.0", "1.3.6.1.4.1.2021.4.3.0", "1.3.6.1.4.1.2021.4.4.0", HrSoTienTrinh, LaLoad[0], LaLoad[1], LaLoad[2]
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
            // Swap (KB): dùng nhiều nghĩa là RAM không đủ, NAS sẽ chậm rõ
            var swapTong = SnmpClient.So(ht.GetValueOrDefault("1.3.6.1.4.1.2021.4.3.0"));
            var swapTrong = SnmpClient.So(ht.GetValueOrDefault("1.3.6.1.4.1.2021.4.4.0"));
            if (swapTong > 0 && swapTrong != null)
            {
                var tyLe = Math.Clamp(1 - (double)swapTrong.Value / swapTong.Value, 0, 1);
                kq.DsMuc.Add(new Muc("RAM", "Swap", tyLe >= 0.8 ? CanhBao : Ok, $"{swapTong.Value / 1024d / 1024:0.#} GB · dùng {tyLe:P0}"));
            }
            var cpuRanh = SnmpClient.So(ht.GetValueOrDefault("1.3.6.1.4.1.2021.11.11.0"));
            if (cpuRanh != null)
                kq.DsMuc.Add(new Muc("CPU", "Tải CPU", 100 - cpuRanh >= 90 ? CanhBao : Ok, $"{100 - cpuRanh}%"));
            ThemTaiHeThong(kq, ht);

            // Phiên kết nối theo dịch vụ (bảng 6574.6): 2 tên dịch vụ, 3 số kết nối
            var dv = await BangAsync(ip, com, "1.3.6.1.4.1.6574.6.1.1", new[] { 2, 3 }, ct);
            if (dv.Count > 0)
            {
                var dsDv = dv.Values.Select(c => (ten: SnmpClient.Chuoi(c.GetValueOrDefault(2)), so: SnmpClient.So(c.GetValueOrDefault(3)) ?? 0))
                    .Where(x => x.so > 0).Select(x => $"{x.ten} {x.so}").ToList();
                ThemTt(kq, "Kết nối đang mở", dsDv.Count > 0 ? string.Join(" · ", dsDv) : "Không có");
            }

            // Phiên bản DSM: upgradeAvailable 1 = có bản mới, 2 = đã mới nhất (3-5: không kiểm được)
            var capNhat = SnmpClient.So(ht.GetValueOrDefault(g + "5.4.0"));
            var phienBan = SnmpClient.Chuoi(ht.GetValueOrDefault(g + "5.3.0")) ?? "DSM";
            kq.DsMuc.Add(new Muc("Phần mềm", phienBan, capNhat == 1 ? CanhBao : capNhat == 2 ? Ok : KhongRo,
                capNhat switch { 1 => "Có bản cập nhật DSM mới", 2 => "Đã là bản mới nhất", 3 => "Đang kiểm tra cập nhật", _ => "Không kiểm tra được cập nhật" }));

            // SMART từng ổ (bảng 6574.5): 2 thiết bị "/dev/sataN", 3 tên chỉ số, 4 id, 8 giá trị thô, 9 trạng thái
            var smart = await BangAsync(ip, com, "1.3.6.1.4.1.6574.5.1.1", new[] { 2, 3, 4, 8, 9 }, ct);
            var smartTheoO = smart.Values
                .GroupBy(c => SoTrongChuoi(SnmpClient.Chuoi(c.GetValueOrDefault(2))))
                .ToDictionary(x => x.Key, x => x.ToList());
            // Tải IO từng ổ (bảng 6574.101): 2 thiết bị "sataN", 8 tải %, 12 / 13 byte đọc / ghi cộng dồn
            var io = await BangAsync(ip, com, "1.3.6.1.4.1.6574.101.1.1", new[] { 2, 8, 12, 13 }, ct);
            var ioTheoO = io.Values
                .GroupBy(c => SoTrongChuoi(SnmpClient.Chuoi(c.GetValueOrDefault(2))))
                .ToDictionary(x => x.Key, x => x.First());

            // Ổ cứng: 2 id, 3 model, 4 giao tiếp, 5 status, 6 nhiệt độ, 8 số lần thử lại, 9 bad sector,
            // 10 lỗi nhận diện, 11 tuổi thọ còn lại, 13 sức khoẻ
            var o = await BangAsync(ip, com, "1.3.6.1.4.1.6574.2.1.1", new[] { 2, 3, 4, 5, 6, 8, 9, 10, 11, 13 }, ct);
            foreach (var (_, c) in o.OrderBy(x => SoTrongChuoi(SnmpClient.Chuoi(x.Value.GetValueOrDefault(2)))))
            {
                var ten = SnmpClient.Chuoi(c.GetValueOrDefault(2)) ?? "Ổ";
                // "Disk 5" ứng với /dev/sata5 (SMART) và sata5 (IO) — cùng số khe
                var soKhe = SoTrongChuoi(ten);
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

                // Bảng phụ: chỉ số SMART quan trọng + chỉ số nào ổ tự báo khác "OK" + tải IO
                var chiTiet = new Dictionary<string, string>();
                if (SnmpClient.Chuoi(c.GetValueOrDefault(4)) is { Length: > 0 } giaoTiep) chiTiet["Giao tiếp"] = giaoTiep;
                // Thử lại / lỗi nhận diện > 0: ổ hoặc khe / cáp backplane chập chờn, DSM vẫn báo Normal
                var thuLai = SnmpClient.So(c.GetValueOrDefault(8)) ?? 0;
                var loiNhanDien = SnmpClient.So(c.GetValueOrDefault(10)) ?? 0;
                if (thuLai > 0) chiTiet["Số lần thử lại"] = thuLai.ToString();
                if (loiNhanDien > 0) chiTiet["Lỗi nhận diện ổ"] = loiNhanDien.ToString();
                if ((thuLai > 0 || loiNhanDien > 0) && tt == Ok)
                {
                    tt = CanhBao;
                    giaTri.Add(thuLai > 0 ? $"thử lại {thuLai} lần" : $"lỗi nhận diện {loiNhanDien} lần");
                }
                if (smartTheoO.TryGetValue(soKhe, out var dsSmart))
                {
                    foreach (var s in dsSmart.OrderBy(s => SnmpClient.So(s.GetValueOrDefault(4)) ?? 0))
                    {
                        var id = SnmpClient.So(s.GetValueOrDefault(4)) ?? 0;
                        var stSmart = SnmpClient.Chuoi(s.GetValueOrDefault(9)) ?? "";
                        var tho = SnmpClient.Chuoi(s.GetValueOrDefault(8)) ?? "";
                        var khongOk = stSmart.Length > 0 && !stSmart.Equals("OK", StringComparison.OrdinalIgnoreCase);
                        if (!SmartQuanTrong.TryGetValue(id, out var tenViet) && !khongOk) continue;
                        tenViet ??= SnmpClient.Chuoi(s.GetValueOrDefault(3)) ?? ("SMART " + id);
                        chiTiet[tenViet] = tho + (khongOk ? $" ({stSmart})" : "");
                        // Sector chờ xử lý / không sửa được > 0 là dấu hiệu ổ sắp hỏng, nặng hơn sector đã thay thế
                        if (id is 197 or 198 && long.TryParse(tho, out var n) && n > 0 && tt == Ok) tt = CanhBao;
                        if (khongOk && tt == Ok) tt = CanhBao;
                    }
                }
                string? khoaIo = null;
                if (ioTheoO.TryGetValue(soKhe, out var dongIo))
                {
                    if (SnmpClient.So(dongIo.GetValueOrDefault(8)) is long taiIo) chiTiet["Tải IO"] = taiIo + "%";
                    var doc = SnmpClient.So(dongIo.GetValueOrDefault(12));
                    var ghi = SnmpClient.So(dongIo.GetValueOrDefault(13));
                    if (doc != null && ghi != null)
                    {
                        khoaIo = "o:" + ten;
                        kq.DsBoDem.Add(new BoDem(khoaIo, "o", (ulong)doc.Value, (ulong)ghi.Value));
                    }
                }
                kq.DsMuc.Add(new Muc("Ổ cứng", ten, tt, Noi(giaTri), chiTiet.Count > 0 ? chiTiet : null, khoaIo));
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

            await CongMangAsync(kq, ip, com, ct);
        }

        /// <summary>Chỉ số SMART đáng xem (id ATA) -> tên hiển thị.</summary>
        private static readonly Dictionary<long, string> SmartQuanTrong = new()
        {
            [5] = "Sector đã thay thế (5)",
            [9] = "Số giờ chạy (9)",
            [12] = "Số lần bật nguồn (12)",
            [187] = "Lỗi không sửa được (187)",
            [188] = "Lệnh quá hạn (188)",
            [194] = "Nhiệt độ (194)",
            [197] = "Sector chờ xử lý (197)",
            [198] = "Sector không đọc được (198)",
            [199] = "Lỗi CRC cáp (199)"
        };

        // ================= Cổng mạng (IF-MIB) =================

        /// <summary>
        /// Cổng mạng vật lý / bond: trạng thái, tốc độ; lưu lượng tính ở worker từ bộ đếm 64-bit.
        /// Cổng bật mà không cắm dây hiện "Không kết nối" (xám), không tính là lỗi — nhiều máy để thừa cổng.
        /// </summary>
        private static async Task CongMangAsync(KetQua kq, string ip, string com, CancellationToken ct)
        {
            // ifXTable: 1 tên, 6 / 10 byte vào / ra (64-bit), 15 tốc độ Mbps, 18 alias
            var x = await BangAsync(ip, com, "1.3.6.1.2.1.31.1.1.1", new[] { 1, 6, 10, 15, 18 }, ct);
            if (x.Count == 0) return;
            // ifTable: 7 admin, 8 oper (1 up, 2 down)
            var t = await BangAsync(ip, com, "1.3.6.1.2.1.2.2.1", new[] { 7, 8 }, ct);

            foreach (var (chiSo, c) in x.OrderBy(v => SnmpClient.Chuoi(v.Value.GetValueOrDefault(1))))
            {
                var ten = SnmpClient.Chuoi(c.GetValueOrDefault(1)) ?? ("if" + chiSo);
                if (Regex.IsMatch(ten, @"^(lo|sit|docker|veth|br-|virbr|ovs|tun|tap|teql|ip6|gre|dummy|vmk|Loopback|isatap|Teredo)", RegexOptions.IgnoreCase)
                    || ten.Contains("Pseudo", StringComparison.OrdinalIgnoreCase) || ten.Contains("Filter", StringComparison.OrdinalIgnoreCase))
                    continue;

                var tt = t.GetValueOrDefault(chiSo);
                var admin = SnmpClient.So(tt?.GetValueOrDefault(7));
                var oper = SnmpClient.So(tt?.GetValueOrDefault(8));
                var mbps = SnmpClient.So(c.GetValueOrDefault(15)) ?? 0;
                var alias = SnmpClient.Chuoi(c.GetValueOrDefault(18));

                string trangThai, moTa;
                if (admin == 2) { trangThai = KhongRo; moTa = "Tắt"; }
                else if (oper == 1) { trangThai = Ok; moTa = mbps >= 1000 ? $"{mbps / 1000d:0.#} Gbps" : $"{mbps} Mbps"; }
                else { trangThai = KhongRo; moTa = "Không kết nối"; }

                string? khoa = null;
                if (oper == 1 && SnmpClient.So(c.GetValueOrDefault(6)) is long vao && SnmpClient.So(c.GetValueOrDefault(10)) is long ra)
                {
                    khoa = "mang:" + ten;
                    kq.DsBoDem.Add(new BoDem(khoa, "mang", (ulong)vao, (ulong)ra));
                }
                kq.DsMuc.Add(new Muc("Cổng mạng", string.IsNullOrWhiteSpace(alias) ? ten : $"{ten} ({alias})", trangThai, moTa, null, khoa));
            }
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
            var ma = await SnmpClient.GetAsync(ip, com, new[] { "1.3.6.1.4.1.232.2.2.2.6.0" }, 2500, 2, ct);
            ThemTt(kq, "Mã sản phẩm", SnmpClient.Chuoi(ma.GetValueOrDefault("1.3.6.1.4.1.232.2.2.2.6.0")));

            // connUnitRevsTable: 3 phiên bản, 4 mô tả "Bundle Version (I/O Manager-A)" — mỗi controller 1 khối
            var banSua = (await BangAsync(ip, com, "1.3.6.1.3.94.1.7.1", new[] { 3, 4 }, ct)).Values
                .Select(c => (moTa: SnmpClient.Chuoi(c.GetValueOrDefault(4)) ?? "", gt: SnmpClient.Chuoi(c.GetValueOrDefault(3)) ?? ""))
                .ToList();
            var bundle = banSua.Select(x => (m: Regex.Match(x.moTa, @"^Bundle Version \(I/O Manager-(\w)\)$"), x.gt))
                .Where(x => x.m.Success && x.gt.Length > 0).ToDictionary(x => x.m.Groups[1].Value, x => x.gt);
            foreach (var (ctl, gt) in bundle.OrderBy(x => x.Key)) ThemTt(kq, "Firmware controller " + ctl, gt);
            if (bundle.Count > 0)
            {
                // 2 controller khác bản firmware = nâng cấp dở dang, MSA có thể tắt failover
                var khac = bundle.Values.Distinct().Count() > 1;
                kq.DsMuc.Add(new Muc("Phần mềm", "Firmware controller", khac ? CanhBao : Ok,
                    khac ? string.Join(" ≠ ", bundle.OrderBy(x => x.Key).Select(x => $"{x.Key}: {x.Value}")) : bundle.Values.First()));
            }
            foreach (var x in banSua.Where(x => x.moTa.StartsWith("Storage Controller Code Version (I/O Manager-") && x.gt.Length > 0))
                ThemTt(kq, "Mã controller " + x.moTa[^2..^1], x.gt);

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

            // Cổng host (connUnitPortTable): 6 state (2 online, 3 offline), 7 status (2 unused, 3 ready, 4 warning, 5 failure), 17 tên
            foreach (var (_, c) in (await BangAsync(ip, com, "1.3.6.1.3.94.1.10.1", new[] { 6, 7, 17 }, ct)).OrderBy(x => SoCuoi(x.Key)))
            {
                var st = SnmpClient.So(c.GetValueOrDefault(7));
                var state = SnmpClient.So(c.GetValueOrDefault(6));
                var (tt, moTa) = st switch
                {
                    2 => (KhongRo, "Không dùng"),
                    3 when state == 2 => (Ok, "Hoạt động"),
                    3 => (CanhBao, "Sẵn sàng nhưng offline"),
                    4 => (CanhBao, "Cảnh báo"),
                    5 => (Loi, "Lỗi"),
                    6 => (KhongRo, "Không tham gia"),
                    7 => (CanhBao, "Đang khởi tạo"),
                    _ => (KhongRo, "Trạng thái " + st)
                };
                kq.DsMuc.Add(new Muc("Cổng host", (SnmpClient.Chuoi(c.GetValueOrDefault(17)) ?? "Cổng").Replace("hostport_", "Cổng "), tt, moTa));
            }

            await NhatKyMsaAsync(kq, ip, com, ct);
        }

        /// <summary>
        /// Nhật ký sự kiện MSA (connUnitEventTable, ~40 dòng mới nhất): 4 thời gian "ddMMyyyy HHmmss", 6 mức độ, 9 nội dung.
        /// Lỗi / nghiêm trọng trong 24 giờ -> LOI; cảnh báo hoặc cổng host rớt ≥ 3 lần trong 24 giờ -> CANH_BAO.
        /// </summary>
        private static async Task NhatKyMsaAsync(KetQua kq, string ip, string com, CancellationToken ct)
        {
            var bang = await BangAsync(ip, com, "1.3.6.1.3.94.1.11.1", new[] { 4, 6, 9 }, ct);
            var moc24h = DateTime.Now.AddHours(-24);

            foreach (var (_, c) in bang.OrderByDescending(x => SoCuoi(x.Key)))
            {
                DateTime? luc = DateTime.TryParseExact(SnmpClient.Chuoi(c.GetValueOrDefault(4)), "ddMMyyyy HHmmss",
                    System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.None, out var d) ? d : null;
                // connUnitEventSeverity: 2 emergency 3 alert 4 critical 5 error 6 warning 7 notify 8 info 9 debug
                var mucDo = SnmpClient.So(c.GetValueOrDefault(6)) switch { >= 2 and <= 5 => "Lỗi", 6 => "Cảnh báo", _ => "Thông tin" };
                kq.DsSuKien.Add(new SuKien(luc, mucDo, SnmpClient.Chuoi(c.GetValueOrDefault(9)) ?? ""));
            }

            var gan = kq.DsSuKien.Where(x => x.ThoiGian >= moc24h).ToList();
            var soLoi = gan.Count(x => x.MucDo == "Lỗi");
            var soCanhBao = gan.Count(x => x.MucDo == "Cảnh báo");
            kq.DsMuc.Add(new Muc("Nhật ký", "Sự kiện 24 giờ qua", soLoi > 0 ? Loi : soCanhBao > 0 ? CanhBao : Ok,
                soLoi + soCanhBao == 0 ? "Không có lỗi / cảnh báo" : $"{soLoi} lỗi · {soCanhBao} cảnh báo"));

            // "Host link down. (port: 3)" lặp nhiều lần = cổng chập chờn (cáp / SFP / switch phía host)
            foreach (var g in gan.Select(x => Regex.Match(x.NoiDung, @"Host link down\. \(port: (\d+)\)"))
                         .Where(m => m.Success).GroupBy(m => m.Groups[1].Value).Where(g => g.Count() >= 3))
                kq.DsMuc.Add(new Muc("Nhật ký", $"Cổng host {g.Key} chập chờn", CanhBao, $"rớt kết nối {g.Count()} lần / 24 giờ"));
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
                g + "6.1.3.0", g + "2.2.4.2.0", g + "2.2.2.1.0", g + "6.2.14.4.0", g + "6.2.9.1.0", g + "6.2.6.4.0", g + "6.2.6.1.0", g + "3.1.3.0",
                // mã sản phẩm, BIOS (cpqSeSysRomVer), firmware iLO (cpqSm2CntlrRomRevision), công suất hiện tại W (cpqHePowerMeterCurrReading)
                g + "2.2.2.6.0", g + "1.2.6.1.0", g + "9.2.2.2.0", g + "6.2.15.3.0"
            }, 2500, 2, ct);
            var model = SnmpClient.Chuoi(ht.GetValueOrDefault(g + "2.2.4.2.0"));
            var serial = SnmpClient.Chuoi(ht.GetValueOrDefault(g + "2.2.2.1.0"));
            if (!string.IsNullOrWhiteSpace(model)) kq.MoTa = Noi(new[] { model, string.IsNullOrWhiteSpace(serial) ? null : "SN " + serial });
            ThemTt(kq, "Mã sản phẩm", SnmpClient.Chuoi(ht.GetValueOrDefault(g + "2.2.2.6.0")));
            ThemTt(kq, "BIOS", SnmpClient.Chuoi(ht.GetValueOrDefault(g + "1.2.6.1.0")));
            ThemTt(kq, "Firmware iLO", SnmpClient.Chuoi(ht.GetValueOrDefault(g + "9.2.2.2.0")));

            kq.DsMuc.Add(new Muc("Tổng thể", "Tình trạng máy chủ", TrangThaiCpq(SnmpClient.So(ht.GetValueOrDefault(g + "6.1.3.0"))), null));
            if (SnmpClient.So(ht.GetValueOrDefault(g + "6.2.15.3.0")) is long congSuat and > 0)
                kq.DsMuc.Add(new Muc("Nguồn", "Công suất tiêu thụ", Ok, $"{congSuat} W"));

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

            // Quạt: cpqHeFltTolFanTable 3 vị trí, 4 có lắp (2 không, 3 có), 9 điều kiện — Gen10+ có khe quạt trống
            var quat = await BangAsync(ip, com, g + "6.2.6.7.1", new[] { 3, 4, 9 }, ct);
            foreach (var (k, c) in quat.Where(x => SnmpClient.So(x.Value.GetValueOrDefault(4)) != 2).OrderBy(x => SoCuoi(x.Key)))
                kq.DsMuc.Add(new Muc("Quạt", "Quạt " + SoCuoi(k) + ViTriCpq(SnmpClient.So(c.GetValueOrDefault(3))),
                    TrangThaiCpq(SnmpClient.So(c.GetValueOrDefault(9))), null));
            if (quat.Count == 0)
                kq.DsMuc.Add(new Muc("Quạt", "Quạt hệ thống", TrangThaiCpq(SnmpClient.So(ht.GetValueOrDefault(g + "6.2.6.4.0"))), null));

            // Nguồn: cpqHeFltTolPowerSupplyTable 3 có lắp, 4 điều kiện, 5 lỗi chi tiết, 7 công suất (W)
            var nguon = await BangAsync(ip, com, g + "6.2.9.3.1", new[] { 3, 4, 5, 7 }, ct);
            foreach (var (k, c) in nguon.Where(x => SnmpClient.So(x.Value.GetValueOrDefault(3)) != 2).OrderBy(x => SoCuoi(x.Key)))
            {
                var loiNguon = SnmpClient.So(c.GetValueOrDefault(5)) switch
                {
                    17 => "Mất điện đầu vào / rút dây nguồn", 13 => "Sụt áp đầu vào", 4 => "Hỏng quạt nguồn", 5 => "Quá nhiệt",
                    null or 1 => null, var x => "Lỗi bộ nguồn (mã " + x + ")"
                };
                kq.DsMuc.Add(new Muc("Nguồn", "Bộ nguồn " + SoCuoi(k), TrangThaiCpq(SnmpClient.So(c.GetValueOrDefault(4))),
                    loiNguon ?? (SnmpClient.So(c.GetValueOrDefault(7)) is long w and > 0 ? $"{w} W" : null)));
            }
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

            // Bộ điều khiển Smart Array: cpqDaCntlrTable 6 điều kiện
            foreach (var (k, c) in (await BangAsync(ip, com, g + "3.2.2.1.1", new[] { 6 }, ct)).OrderBy(x => SoCuoi(x.Key)))
                kq.DsMuc.Add(new Muc("RAID", "Bộ điều khiển " + SoCuoi(k), TrangThaiCpq(SnmpClient.So(c.GetValueOrDefault(6))), null));
            // Cache Smart Array: cpqDaAccelTable 6 pin (2 ok, 3 đang sạc, 4 hỏng, 5 suy giảm, 6 không có, 7 tụ hỏng)
            // — pin / tụ hỏng thì controller tắt cache ghi, IO chậm hẳn
            foreach (var (k, c) in (await BangAsync(ip, com, g + "3.2.2.2.1", new[] { 6 }, ct)).OrderBy(x => SoCuoi(x.Key)))
            {
                var pin = SnmpClient.So(c.GetValueOrDefault(6));
                if (pin is null or 1 or 6) continue;
                kq.DsMuc.Add(new Muc("RAID", "Pin cache " + SoCuoi(k), pin switch { 2 => Ok, 3 => CanhBao, _ => Loi },
                    pin switch { 2 => "Bình thường", 3 => "Đang sạc", 4 => "Hỏng", 5 => "Suy giảm", 7 => "Tụ hỏng", _ => "trạng thái " + pin }));
            }

            await NicHpeAsync(kq, ip, com, ct);
            await ImlHpeAsync(kq, ip, com, ct);
        }

        /// <summary>
        /// Card mạng qua iLO (CPQNIC): điều kiện tổng 232.18.1.3 + từng cổng vật lý (cpqNicIfPhysAdapterTable
        /// 4 MAC, 12 điều kiện, 14 trạng thái 2 ok / 3 lỗi chung / 4 mất link). Điều kiện tổng có thể "failed" do cổng
        /// từng có link nay rút dây, trong khi từng cổng vẫn OK — giữ mục tổng để trạng thái máy khớp với iLO.
        /// </summary>
        private static async Task NicHpeAsync(KetQua kq, string ip, string com, CancellationToken ct)
        {
            var tong = await SnmpClient.GetAsync(ip, com, new[] { "1.3.6.1.4.1.232.18.1.3.0" }, 2500, 2, ct);
            var dk = SnmpClient.So(tong.GetValueOrDefault("1.3.6.1.4.1.232.18.1.3.0"));
            if (dk == null) return;
            kq.DsMuc.Add(new Muc("Cổng mạng", "Card mạng (iLO tổng hợp)", TrangThaiCpq(dk),
                dk is 3 or 4 ? "iLO báo lỗi card mạng — xem iLO › Network / IML" : null));

            foreach (var (k, c) in (await BangAsync(ip, com, "1.3.6.1.4.1.232.18.2.3.1.1", new[] { 4, 12, 14 }, ct)).OrderBy(x => SoCuoi(x.Key)))
            {
                var st = SnmpClient.So(c.GetValueOrDefault(14));
                var mac = c.GetValueOrDefault(4) is byte[] { Length: 6 } b ? string.Join(":", b.Select(x => x.ToString("X2"))) : null;
                var tt = st switch { 3 => Loi, 4 => CanhBao, _ => TrangThaiCpq(SnmpClient.So(c.GetValueOrDefault(12))) };
                kq.DsMuc.Add(new Muc("Cổng mạng", "NIC " + SoCuoi(k) + (mac != null ? $" ({mac})" : ""), tt,
                    // điều kiện "other": cổng không dùng / chưa từng có link — xám, không tính lỗi
                    st switch { 3 => "Lỗi card", 4 => "Mất link", _ => tt == KhongRo ? "Không dùng" : null }));
            }
        }

        /// <summary>
        /// Integrated Management Log của iLO: điều kiện 232.6.2.11.2 (failed khi còn entry nghiêm trọng chưa xoá) +
        /// bảng cpqHeEventLogEntry 2 mức độ (2 info, 3 info+alert, 6 đã khắc phục, 9 cảnh báo, 15 nghiêm trọng),
        /// 5 số lần lặp, 7 lần cuối (6 byte: năm 2 byte, tháng, ngày, giờ, phút), 8 nội dung.
        /// </summary>
        private static async Task ImlHpeAsync(KetQua kq, string ip, string com, CancellationToken ct)
        {
            var dkGet = await SnmpClient.GetAsync(ip, com, new[] { "1.3.6.1.4.1.232.6.2.11.2.0" }, 2500, 2, ct);
            var dk = SnmpClient.So(dkGet.GetValueOrDefault("1.3.6.1.4.1.232.6.2.11.2.0"));
            var bang = await BangAsync(ip, com, "1.3.6.1.4.1.232.6.2.11.3.1", new[] { 2, 5, 7, 8 }, ct);

            foreach (var (_, c) in bang.OrderByDescending(x => SoCuoi(x.Key)).Take(40))
            {
                var mucDo = SnmpClient.So(c.GetValueOrDefault(2)) switch { 15 => "Lỗi", 9 => "Cảnh báo", _ => "Thông tin" };
                var lap = SnmpClient.So(c.GetValueOrDefault(5)) ?? 1;
                var noiDung = (SnmpClient.Chuoi(c.GetValueOrDefault(8)) ?? "").Trim()
                              + (SnmpClient.So(c.GetValueOrDefault(2)) == 6 ? " [đã khắc phục]" : "") + (lap > 1 ? $" (×{lap})" : "");
                kq.DsSuKien.Add(new SuKien(NgayHpe(c.GetValueOrDefault(7)), mucDo, noiDung));
            }
            if (dk == null && bang.Count == 0) return;

            // Trạng thái theo điều kiện iLO (entry cũ đã được đánh dấu xử lý thì iLO trả OK dù vẫn còn trong log)
            var soLoi = kq.DsSuKien.Count(x => x.MucDo == "Lỗi");
            var soCanhBao = kq.DsSuKien.Count(x => x.MucDo == "Cảnh báo");
            var tt = dk != null ? TrangThaiCpq(dk) : soLoi > 0 ? Loi : soCanhBao > 0 ? CanhBao : Ok;
            var moTa = soLoi + soCanhBao == 0 ? "Không có lỗi / cảnh báo gần đây"
                : $"{soLoi} nghiêm trọng · {soCanhBao} cảnh báo trong {kq.DsSuKien.Count} sự kiện gần nhất";
            if (tt is Loi or CanhBao && soLoi + soCanhBao == 0) moTa = "iLO báo còn sự kiện chưa xử lý — xem IML";
            kq.DsMuc.Add(new Muc("Nhật ký", "IML (nhật ký iLO)", tt, moTa));
        }

        private static DateTime? NgayHpe(object? v)
        {
            if (v is not byte[] { Length: >= 6 } b) return null;
            try { return new DateTime(b[0] << 8 | b[1], b[2], b[3], b[4], b[5], 0); }
            catch (ArgumentOutOfRangeException) { return null; }
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
            // 1.1.8.0 racFirmwareVersion (iDRAC)
            var ht = await SnmpClient.GetAsync(ip, com, new[] { g + "2.1.0", g + "1.3.12.0", g + "1.3.2.0", g + "1.1.8.0" }, 2500, 2, ct);
            var model = SnmpClient.Chuoi(ht.GetValueOrDefault(g + "1.3.12.0"));
            var tag = SnmpClient.Chuoi(ht.GetValueOrDefault(g + "1.3.2.0"));
            if (!string.IsNullOrWhiteSpace(model)) kq.MoTa = Noi(new[] { model, string.IsNullOrWhiteSpace(tag) ? null : "Service Tag " + tag });
            ThemTt(kq, "Firmware iDRAC", SnmpClient.Chuoi(ht.GetValueOrDefault(g + "1.1.8.0")));
            // systemBIOSTable 8 tên phiên bản
            foreach (var (_, c) in await BangAsync(ip, com, g + "4.300.50.1", new[] { 8 }, ct))
                ThemTt(kq, "BIOS", SnmpClient.Chuoi(c.GetValueOrDefault(8)));
            kq.DsMuc.Add(new Muc("Tổng thể", "Tình trạng máy chủ", TrangThaiDell(SnmpClient.So(ht.GetValueOrDefault(g + "2.1.0"))), null));

            // amperageProbeTable: 5 trạng thái, 6 giá trị, 7 loại (26 = công suất tiêu thụ, đơn vị W), 8 vị trí
            foreach (var (_, c) in await BangAsync(ip, com, g + "4.600.30.1", new[] { 5, 6, 7, 8 }, ct))
                if (SnmpClient.So(c.GetValueOrDefault(7)) == 26 && SnmpClient.So(c.GetValueOrDefault(6)) is long w)
                    kq.DsMuc.Add(new Muc("Nguồn", "Công suất tiêu thụ", TrangThaiDell(SnmpClient.So(c.GetValueOrDefault(5))), $"{w} W"));

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
            // Bộ điều khiển RAID: controllerTable 2 tên, 38 componentStatus
            foreach (var (_, c) in (await BangAsync(ip, com, g + "5.1.20.130.1.1", new[] { 2, 38 }, ct)).OrderBy(x => SoCuoi(x.Key)))
                kq.DsMuc.Add(new Muc("RAID", SnmpClient.Chuoi(c.GetValueOrDefault(2)) ?? "Bộ điều khiển",
                    TrangThaiDell(SnmpClient.So(c.GetValueOrDefault(38))), null));
            // Pin cache controller: batteryTable 6 componentStatus — pin hỏng thì PERC chuyển write-through, IO chậm
            foreach (var (k, c) in (await BangAsync(ip, com, g + "5.1.20.130.15.1", new[] { 6 }, ct)).OrderBy(x => SoCuoi(x.Key)))
                kq.DsMuc.Add(new Muc("RAID", "Pin cache " + SoCuoi(k), TrangThaiDell(SnmpClient.So(c.GetValueOrDefault(6))), null));
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
            await CongMangAsync(kq, ip, com, ct);

            // Số tiến trình / phiên đăng nhập / load của hệ điều hành — lấy cả khi đi kèm iLO / iDRAC (phần cứng không biết)
            try
            {
                var ht = await SnmpClient.GetAsync(ip, com, new[] { HrSoTienTrinh, HrSoNguoiDung, LaLoad[0], LaLoad[1], LaLoad[2] }, 2500, 2, ct);
                ThemTaiHeThong(kq, ht);
                ThemTt(kq, "Phiên đăng nhập", SnmpClient.So(ht.GetValueOrDefault(HrSoNguoiDung))?.ToString());
            }
            catch (TimeoutException) { /* thiếu số liệu phụ, không làm hỏng cả lượt đọc */ }
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

        private static void ThemTt(KetQua kq, string ten, string? giaTri)
        {
            if (!string.IsNullOrWhiteSpace(giaTri)) kq.ThongTin[ten] = Cat(giaTri.Trim(), 200)!;
        }

        /// <summary>Số tiến trình + load average 1 / 5 / 15 phút từ kết quả GET đã có (OID nào thiếu thì bỏ).</summary>
        private static void ThemTaiHeThong(KetQua kq, Dictionary<string, object?> ht)
        {
            ThemTt(kq, "Tiến trình", SnmpClient.So(ht.GetValueOrDefault(HrSoTienTrinh))?.ToString());
            var load = LaLoad.Select(o => SnmpClient.Chuoi(ht.GetValueOrDefault(o))).ToList();
            if (load.All(x => !string.IsNullOrWhiteSpace(x))) ThemTt(kq, "Load 1 / 5 / 15 phút", string.Join(" / ", load));
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
