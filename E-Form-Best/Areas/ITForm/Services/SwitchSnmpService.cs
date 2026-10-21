using System.Net.Sockets;
using System.Text.RegularExpressions;

namespace E_Form_Best.Areas.ITForm.Services
{
    /// <summary>
    /// Đọc thông tin tài sản 1 switch qua SNMP v2c (MIB chuẩn, không phụ thuộc hãng): tên, hãng, model, serial,
    /// firmware, MAC, số cổng vật lý. Dùng cho nút "Kiểm tra kết nối" ở form và "Điền từ SNMP" hàng loạt.
    /// Khác số liệu CPU/RAM ở cột SNMP (do hệ thống giám sát 10.0.60.238 poll) — cái đó không có model/serial/firmware.
    /// Không đụng DB.
    /// </summary>
    public static class SwitchSnmpService
    {
        public record ThongTin(string Ip, string? TenHeThong, string? MoTa, string? Hang, string? Model, string? Serial,
            string? Firmware, string? Mac, int? SoCong);

        private const string SysDescr = "1.3.6.1.2.1.1.1.0", SysObjectId = "1.3.6.1.2.1.1.2.0", SysName = "1.3.6.1.2.1.1.5.0";
        private const string BridgeMac = "1.3.6.1.2.1.17.1.1.0";
        private const string EntClass = "1.3.6.1.2.1.47.1.1.1.1.5", EntGoc = "1.3.6.1.2.1.47.1.1.1.1.";
        private const string IfType = "1.3.6.1.2.1.2.2.1.3", IfMac = "1.3.6.1.2.1.2.2.1.6";

        // Mã doanh nghiệp IANA trong sysObjectID -> hãng, khi ENTITY-MIB không có entPhysicalMfgName
        private static readonly Dictionary<string, string> DsHang = new()
        {
            ["9"] = "Cisco", ["2011"] = "Huawei", ["11"] = "HPE", ["25506"] = "H3C", ["14823"] = "Aruba", ["47196"] = "Aruba",
            ["4526"] = "Netgear", ["171"] = "D-Link", ["11863"] = "TP-Link", ["890"] = "Zyxel", ["12356"] = "Fortinet",
            ["2636"] = "Juniper", ["1916"] = "Extreme", ["6486"] = "Alcatel-Lucent", ["207"] = "Allied Telesis",
            ["4881"] = "Ruijie", ["25053"] = "Ruckus", ["14988"] = "MikroTik", ["41112"] = "Ubiquiti", ["674"] = "Dell",
            ["232"] = "HPE", ["6574"] = "Synology", ["311"] = "Microsoft", ["6876"] = "VMware", ["8072"] = "Net-SNMP"
        };

        /// <summary>Đọc 1 IP; null nếu không trả lời SNMP (community sai / tắt SNMP / không thông mạng).</summary>
        public static async Task<ThongTin?> DocAsync(string ip, string community, CancellationToken ct)
        {
            Dictionary<string, object?> nen;
            try { nen = await SnmpClient.GetAsync(ip, community, new[] { SysDescr, SysObjectId, SysName, BridgeMac }, 2500, 2, ct); }
            catch (Exception ex) when (ex is TimeoutException or SocketException or InvalidOperationException) { return null; }

            var moTa = SnmpClient.Chuoi(nen.GetValueOrDefault(SysDescr));
            var oid = SnmpClient.Chuoi(nen.GetValueOrDefault(SysObjectId));
            string? hang = null, model = null, serial = null, firmware = null;
            int? soCong = null;

            // ENTITY-MIB: lấy khung máy (class 3) đầu tiên — stack nhiều switch thì là thành viên 1
            try
            {
                var dsLop = await SnmpClient.WalkAsync(ip, community, EntClass, 2500, 1, 2000, ct);
                var chiSo = dsLop.Where(x => SnmpClient.So(x.GiaTri) == 3).Select(x => x.Oid[(EntClass.Length + 1)..]).FirstOrDefault();
                var soPort = dsLop.Count(x => SnmpClient.So(x.GiaTri) == 10);
                if (soPort > 0) soCong = soPort;
                if (chiSo != null)
                {
                    var tt = await SnmpClient.GetAsync(ip, community,
                        new[] { 2, 9, 10, 11, 12, 13 }.Select(c => $"{EntGoc}{c}.{chiSo}"), 2500, 1, ct);
                    string? Lay(int c) => KhongRong(SnmpClient.Chuoi(tt.GetValueOrDefault($"{EntGoc}{c}.{chiSo}")));
                    hang = Lay(12);
                    model = Lay(13) ?? Lay(2);
                    serial = Lay(11);
                    firmware = Lay(10) ?? Lay(9);
                }
            }
            catch (Exception ex) when (ex is TimeoutException or InvalidOperationException) { }

            // Không có cổng trong ENTITY-MIB: đếm interface ethernetCsmacd (6), bỏ Vlanif / loopback / port-channel
            var dsIfType = new List<SnmpClient.Bien>();
            if (soCong == null)
            {
                try
                {
                    dsIfType = await SnmpClient.WalkAsync(ip, community, IfType, 2500, 1, 2000, ct);
                    var n = dsIfType.Count(x => SnmpClient.So(x.GiaTri) == 6);
                    if (n > 0) soCong = n;
                }
                catch (Exception ex) when (ex is TimeoutException or InvalidOperationException) { }
            }

            // MAC: địa chỉ bridge; switch không bật BRIDGE-MIB thì lấy MAC interface đầu tiên khác 0
            var mac = Mac(nen.GetValueOrDefault(BridgeMac));
            if (mac == null)
            {
                try
                {
                    var dsMac = await SnmpClient.WalkAsync(ip, community, IfMac, 2500, 1, 500, ct);
                    mac = dsMac.Select(x => Mac(x.GiaTri)).FirstOrDefault(x => x != null);
                }
                catch (Exception ex) when (ex is TimeoutException or InvalidOperationException) { }
            }

            // entPhysicalMfgName mỗi dòng ghi một kiểu ("Cisco Systems Inc", "Cisco") -> quy về tên hãng chuẩn để lọc / nhóm được
            if (hang != null)
                hang = DsHang.Values.FirstOrDefault(v => hang.Contains(v, StringComparison.OrdinalIgnoreCase)) ?? hang;
            if (hang == null && oid != null)
            {
                var m = Regex.Match(oid, @"^\.?1\.3\.6\.1\.4\.1\.(\d+)");
                if (m.Success) hang = DsHang.GetValueOrDefault(m.Groups[1].Value);
            }
            // Firmware không có trong ENTITY-MIB: lấy "Version ..." trong sysDescr (Cisco IOS, Huawei VRP, Aruba...)
            if (firmware == null && moTa != null)
            {
                var m = Regex.Match(moTa, @"Version\s+[\d.]+\s*\(([^)]+)\)|[Vv]ersion[:\s]+([^\s,;]+)");
                if (m.Success) firmware = m.Groups[1].Success ? m.Groups[1].Value : m.Groups[2].Value;
            }
            model ??= KhongRong(moTa?.Split('\n', '\r', ',').FirstOrDefault());

            return new ThongTin(ip, KhongRong(SnmpClient.Chuoi(nen.GetValueOrDefault(SysName)))?.TrimEnd('.'), Cat(moTa, 500),
                Cat(hang, 100), Cat(model, 100), Cat(serial, 100), Cat(firmware, 100), mac, soCong is > 0 and <= 1000 ? soCong : null);
        }

        private static string? Mac(object? v)
            => v is byte[] { Length: 6 } b && b.Any(x => x != 0) ? string.Join(":", b.Select(x => x.ToString("X2"))) : null;

        private static string? KhongRong(string? v) => string.IsNullOrWhiteSpace(v) ? null : v.Trim();

        private static string? Cat(string? v, int n) => v == null ? null : v.Length <= n ? v : v[..n];
    }
}
