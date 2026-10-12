# Dò camera PFVN cắm ở switch nào, cổng nào — chạy TRÊN ZPVN-WEBSRV (C:\EForm-Camera), task SYSTEM 30 phút/lần
# ("EForm - Do cong switch camera PFVN"). Máy chủ E-Form không vào được mạng PFVN nên không dò trực tiếp được.
#
# Cách dò (SNMP v2c, chỉ đọc):
#   1. IP camera -> MAC: bảng ARP (ipNetToMediaPhysAddress) của mọi thiết bị trong danh sách (core/L3 có ARP dải 10.0.200.x).
#   2. MAC -> cổng: bảng MAC từng switch (Huawei hwDynFdbPort, không có thì dot1q/dot1d chuẩn).
#   3. Cổng nối sang switch khác (có láng giềng LLDP) hoặc học nhiều MAC là đường trục; cổng truy cập là cổng
#      không có LLDP và ít MAC nhất -> đó là chỗ camera cắm.
# Kết quả ghi "_cong-switch.json"; ghi-trang-thai.ps1 gắn vào "_trang-thai.json" để 2 máy chủ kéo về như cũ.
#
# .env cùng thư mục:
#   SwitchPfvn__SnmpCommunity=<community chỉ-đọc>     (bắt buộc, không in ra)
#   SwitchPfvn__DsSwitch=10.0.193.1,10.0.198.200,...  (IP quản lý switch / thiết bị L3, cách nhau dấu phẩy)

$ErrorActionPreference = 'Stop'
$fileKq = Join-Path $PSScriptRoot '_cong-switch.json'
$fileLog = Join-Path $PSScriptRoot '_cong-switch.log'
function GhiLog([string]$s) { Add-Content -Path $fileLog -Value ((Get-Date -Format 'yyyy-MM-dd HH:mm:ss') + ' ' + $s) -Encoding UTF8 }

Add-Type -TypeDefinition @'
using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Text;

public class SnmpMuc { public uint[] Oid; public byte Tag; public byte[] Gia; }

public static class SnmpDoc {
    static void Len(List<byte> b, int n) {
        if (n < 128) b.Add((byte)n);
        else if (n < 256) { b.Add(0x81); b.Add((byte)n); }
        else { b.Add(0x82); b.Add((byte)(n >> 8)); b.Add((byte)n); }
    }
    static byte[] Tlv(byte tag, byte[] v) { var b = new List<byte>(); b.Add(tag); Len(b, v.Length); b.AddRange(v); return b.ToArray(); }
    static byte[] Cat(params byte[][] a) { var b = new List<byte>(); foreach (var x in a) b.AddRange(x); return b.ToArray(); }
    static byte[] Int(int x) {
        var by = BitConverter.GetBytes(x); Array.Reverse(by);
        int i = 0;
        while (i < 3 && ((by[i] == 0 && (by[i + 1] & 0x80) == 0) || (by[i] == 0xFF && (by[i + 1] & 0x80) != 0))) i++;
        var v = new byte[4 - i]; Array.Copy(by, i, v, 0, v.Length);
        return Tlv(0x02, v);
    }
    static byte[] OidBer(uint[] o) {
        var v = new List<byte>(); v.Add((byte)(o[0] * 40 + o[1]));
        for (int i = 2; i < o.Length; i++) {
            uint x = o[i]; var t = new List<byte>(); t.Insert(0, (byte)(x & 0x7F)); x >>= 7;
            while (x > 0) { t.Insert(0, (byte)((x & 0x7F) | 0x80)); x >>= 7; }
            v.AddRange(t);
        }
        return Tlv(0x06, v.ToArray());
    }
    static int ReadLen(byte[] d, ref int p) {
        int n = d[p++]; if ((n & 0x80) == 0) return n;
        int k = n & 0x7F; n = 0; for (int i = 0; i < k; i++) n = (n << 8) | d[p++]; return n;
    }
    static uint[] ParseOid(byte[] d, int p, int len) {
        var o = new List<uint>(); int end = p + len; byte f = d[p++];
        o.Add((uint)(f / 40)); o.Add((uint)(f % 40)); uint x = 0;
        while (p < end) { byte c = d[p++]; x = (x << 7) | (uint)(c & 0x7F); if ((c & 0x80) == 0) { o.Add(x); x = 0; } }
        return o.ToArray();
    }
    static long ReadInt(byte[] d, ref int p) { p++; int l = ReadLen(d, ref p); long n = 0; for (int i = 0; i < l; i++) n = (n << 8) | d[p + i]; p += l; return n; }
    static void Skip(byte[] d, ref int p) { p++; int l = ReadLen(d, ref p); p += l; }
    public static uint[] Oid(string s) { var a = s.Trim('.').Split('.'); var o = new uint[a.Length]; for (int i = 0; i < a.Length; i++) o[i] = uint.Parse(a[i]); return o; }
    static bool Under(uint[] r, uint[] o) { if (o.Length <= r.Length) return false; for (int i = 0; i < r.Length; i++) if (r[i] != o[i]) return false; return true; }

    // Walk 1 nhanh OID bang GETBULK v2c (25 muc/lan); thiet bi khong tra loi ngay lan dau thi nem loi
    public static List<SnmpMuc> Walk(string host, string community, string rootOid, int timeoutMs, int maxMuc) {
        var root = Oid(rootOid); var kq = new List<SnmpMuc>(); var cur = root;
        int reqId = new Random().Next(1, 1 << 30);
        using (var udp = new UdpClient()) {
            udp.Client.ReceiveTimeout = timeoutMs; udp.Connect(host, 161);
            while (kq.Count < maxMuc) {
                reqId++;
                var vb = Tlv(0x30, Tlv(0x30, Cat(OidBer(cur), new byte[] { 0x05, 0x00 })));
                var pdu = Tlv(0xA5, Cat(Int(reqId), Int(0), Int(25), vb));
                var msg = Tlv(0x30, Cat(Int(1), Tlv(0x04, Encoding.ASCII.GetBytes(community)), pdu));
                byte[] resp = null;
                for (int lan = 0; lan < 3 && resp == null; lan++) {
                    udp.Send(msg, msg.Length);
                    try {
                        while (true) {
                            IPEndPoint ep = null; var r = udp.Receive(ref ep);
                            int q = 0; q++; ReadLen(r, ref q); Skip(r, ref q); Skip(r, ref q); q++; ReadLen(r, ref q);
                            if (ReadInt(r, ref q) == reqId) { resp = r; break; }   // bo goi tra loi cu
                        }
                    } catch (SocketException) { }
                }
                if (resp == null) { if (kq.Count == 0) throw new Exception("SNMP khong tra loi"); break; }

                int p = 0; p++; ReadLen(resp, ref p); Skip(resp, ref p); Skip(resp, ref p); p++; ReadLen(resp, ref p);
                ReadInt(resp, ref p);
                long err = ReadInt(resp, ref p); ReadInt(resp, ref p);
                if (err != 0) break;
                p++; int vbLen = ReadLen(resp, ref p); int vbEnd = p + vbLen;
                bool het = false; uint[] last = null;
                while (p < vbEnd) {
                    p++; ReadLen(resp, ref p);
                    p++; int l = ReadLen(resp, ref p); var o = ParseOid(resp, p, l); p += l;
                    byte tag = resp[p++]; l = ReadLen(resp, ref p); var g = new byte[l]; Array.Copy(resp, p, g, 0, l); p += l;
                    if (tag == 0x80 || tag == 0x81 || tag == 0x82 || !Under(root, o)) { het = true; break; }
                    var m = new SnmpMuc(); m.Oid = o; m.Tag = tag; m.Gia = g; kq.Add(m); last = o;
                }
                if (het || last == null) break;
                cur = last;
            }
        }
        return kq;
    }
}
'@

# ---- Cấu hình ----
$cauHinh = @{}
foreach ($l in [IO.File]::ReadAllLines((Join-Path $PSScriptRoot '.env'))) {
    if ($l -match '^\s*([A-Za-z0-9_]+)=(.*)$') { $cauHinh[$matches[1]] = $matches[2].Trim() }
}
$community = $cauHinh['SwitchPfvn__SnmpCommunity']
$dsIp = @(([string]$cauHinh['SwitchPfvn__DsSwitch']) -split '[,; ]+' | Where-Object { $_ -match '^\d{1,3}(\.\d{1,3}){3}$' } | Select-Object -Unique)

function GhiKetQua($doc) {
    $json = ConvertTo-Json $doc -Depth 6 -Compress
    [IO.File]::WriteAllText("$fileKq.tmp", $json, (New-Object Text.UTF8Encoding($false)))
    Move-Item "$fileKq.tmp" $fileKq -Force
}

if (-not $community -or $dsIp.Count -eq 0) {
    GhiLog 'LOI: chua khai SwitchPfvn__SnmpCommunity / SwitchPfvn__DsSwitch trong .env'
    GhiKetQua @{ capNhat = (Get-Date -Format 'yyyy-MM-ddTHH:mm:ss'); loi = 'Chua cau hinh SNMP switch PFVN'; thietBi = @(); camera = @() }
    exit 1
}

function Walk([string]$ip, [string]$oid, [int]$toiDa = 50000) { , [SnmpDoc]::Walk($ip, $community, $oid, 2500, $toiDa) }
function SoNguyen([byte[]]$g) { [uint64]$n = 0; foreach ($b in $g) { $n = $n * 256 + $b }; $n }
function Chu([byte[]]$g) { [Text.Encoding]::UTF8.GetString($g).Trim([char]0).Trim() }
function Mac([uint32[]]$a) { ($a | ForEach-Object { ([int]$_).ToString('x2') }) -join ':' }
function MacByte([byte[]]$g) { ($g | ForEach-Object { $_.ToString('x2') }) -join ':' }
function Duoi($m, [int]$n) { $m.Oid[$n..($m.Oid.Length - 1)] }

# ---- Camera cần dò: lấy từ lượt trạng thái gần nhất ----
$dsCamera = @()
$fileTt = Join-Path $PSScriptRoot '_trang-thai.json'
if (Test-Path $fileTt) {
    $dsCamera = @(([IO.File]::ReadAllText($fileTt) | ConvertFrom-Json).kenh | Where-Object { $_.ipCamera -match '^\d{1,3}(\.\d{1,3}){3}$' })
}

# ---- Đọc từng thiết bị ----
$arp = @{}          # ip camera -> mac
$thietBi = @()
$fdbTatCa = @()     # mỗi dòng: ip thiết bị, mac, vlan, ifIndex
$demMac = @{}       # "ip|ifIndex" -> số MAC học trên cổng
$cong = @{}         # "ip|ifIndex" -> tên cổng
$trucLldp = @{}     # "ip|tên cổng" -> tên láng giềng LLDP
$tenTb = @{}

$rootHw = '1.3.6.1.4.1.2011.5.25.42.2.1.3.1.4'   # hwDynFdbPort (15 số)
foreach ($ip in $dsIp) {
    try {
        $ten = ''
        foreach ($m in (Walk $ip '1.3.6.1.2.1.1.5' 1)) { $ten = Chu $m.Gia }
        $tenTb[$ip] = $ten

        $ifName = @{}
        foreach ($m in (Walk $ip '1.3.6.1.2.1.31.1.1.1.1')) { $ifName[[int]$m.Oid[-1]] = Chu $m.Gia }
        $bpIf = @{}
        foreach ($m in (Walk $ip '1.3.6.1.2.1.17.1.4.1.2')) { $bpIf[[int]$m.Oid[-1]] = [int](SoNguyen $m.Gia) }

        # Bảng MAC: Huawei (index mac.vlan.vsi, giá trị ifIndex) -> dot1q (fdbId.mac -> bridge port) -> dot1d (mac -> bridge port)
        $fdb = @()
        foreach ($m in (Walk $ip $rootHw)) {
            $x = Duoi $m 15
            if ($x.Count -ge 7) { $fdb += , @((Mac $x[0..5]), [int]$x[6], [int](SoNguyen $m.Gia)) }
        }
        if ($fdb.Count -eq 0) {
            foreach ($m in (Walk $ip '1.3.6.1.2.1.17.7.1.2.2.1.2')) {
                $x = Duoi $m 13
                if ($x.Count -ge 7) { $fdb += , @((Mac $x[1..6]), [int]$x[0], $bpIf[[int](SoNguyen $m.Gia)]) }
            }
        }
        if ($fdb.Count -eq 0) {
            foreach ($m in (Walk $ip '1.3.6.1.2.1.17.4.3.1.2')) {
                $x = Duoi $m 11
                if ($x.Count -ge 6) { $fdb += , @((Mac $x[0..5]), $null, $bpIf[[int](SoNguyen $m.Gia)]) }
            }
        }
        foreach ($f in $fdb) {
            if ($null -eq $f[2]) { continue }
            $k = "$ip|$($f[2])"
            $demMac[$k] = 1 + [int]$demMac[$k]
            if (-not $cong.ContainsKey($k)) { $cong[$k] = $(if ($ifName.ContainsKey($f[2])) { $ifName[$f[2]] } else { "ifIndex $($f[2])" }) }
            $fdbTatCa += , @($ip, $f[0], $f[1], $f[2])
        }

        # ARP: index ifIndex.a.b.c.d, giá trị MAC 6 byte (chỉ thiết bị L3 có dải camera)
        foreach ($m in (Walk $ip '1.3.6.1.2.1.4.22.1.2')) {
            $x = Duoi $m 10
            if ($x.Count -ge 5 -and $m.Gia.Length -eq 6) { $arp[($x[-4..-1] -join '.')] = MacByte $m.Gia }
        }

        # LLDP: cổng cục bộ (lldpLocPortDesc / PortId) có láng giềng = đường trục
        $locTen = @{}
        foreach ($m in (Walk $ip '1.0.8802.1.1.2.1.3.7.1.4')) { $locTen[[int]$m.Oid[-1]] = Chu $m.Gia }
        foreach ($m in (Walk $ip '1.0.8802.1.1.2.1.3.7.1.3')) { if (-not $locTen[[int]$m.Oid[-1]]) { $locTen[[int]$m.Oid[-1]] = Chu $m.Gia } }
        foreach ($m in (Walk $ip '1.0.8802.1.1.2.1.4.1.1.9')) {
            $x = Duoi $m 11   # timeMark.localPortNum.remIndex
            if ($x.Count -ge 2) {
                $tenCong = $locTen[[int]$x[1]]
                if ($tenCong) { $trucLldp["$ip|$tenCong"] = Chu $m.Gia }
            }
        }
        $thietBi += [pscustomobject]@{ ip = $ip; ten = $ten; soMac = $fdb.Count; loi = $null }
    } catch {
        $thietBi += [pscustomobject]@{ ip = $ip; ten = $tenTb[$ip]; soMac = 0; loi = $_.Exception.Message }
    }
}

# ---- Ghép từng camera ----
$theoMac = @{}
foreach ($f in $fdbTatCa) { if (-not $theoMac.ContainsKey($f[1])) { $theoMac[$f[1]] = @() }; $theoMac[$f[1]] += , $f }

$ketQua = @()
foreach ($c in $dsCamera) {
    $mac = $arp[$c.ipCamera]
    $dong = [ordered]@{ nvr = $c.nvr; kenh = $c.kenh; ipCamera = $c.ipCamera; mac = $mac; vlan = $null
                        switchIp = $null; switchTen = $null; cong = $null; soMacTrenCong = $null; chacChan = $false; duongDi = @() }
    if ($mac -and $theoMac.ContainsKey($mac)) {
        $thay = foreach ($f in $theoMac[$mac]) {
            $k = "$($f[0])|$($f[3])"
            [pscustomobject]@{ ip = $f[0]; ten = $tenTb[$f[0]]; cong = $cong[$k]; vlan = $f[2]; soMac = [int]$demMac[$k]
                               truc = [bool]$trucLldp.ContainsKey("$($f[0])|$($cong[$k])") }
        }
        # Cổng camera cắm: không phải đường trục LLDP, ít MAC nhất; đường đi: nhiều MAC (gần core) -> ít MAC
        $canh = $thay | Sort-Object @{ e = { $_.truc } }, @{ e = { $_.soMac } } | Select-Object -First 1
        $dong.vlan = $canh.vlan; $dong.switchIp = $canh.ip; $dong.switchTen = $canh.ten; $dong.cong = $canh.cong
        $dong.soMacTrenCong = $canh.soMac
        $dong.chacChan = (-not $canh.truc) -and $canh.soMac -le 2
        $dong.duongDi = @($thay | Sort-Object @{ e = { $_.soMac }; Descending = $true } | ForEach-Object { [ordered]@{ ip = $_.ip; ten = $_.ten; cong = $_.cong } })
    }
    $ketQua += [pscustomobject]$dong
}

GhiKetQua @{ capNhat = (Get-Date -Format 'yyyy-MM-ddTHH:mm:ss'); loi = $null; thietBi = $thietBi; camera = $ketQua }
$soThay = @($ketQua | Where-Object { $_.cong }).Count
$soLoi = @($thietBi | Where-Object { $_.loi }).Count
GhiLog "camera=$($ketQua.Count) tim thay cong=$soThay thiet bi=$($thietBi.Count) loi=$soLoi arp=$($arp.Count)"
$dong = Get-Content $fileLog -Encoding UTF8
if ($dong.Count -gt 1000) { $dong | Select-Object -Last 1000 | Set-Content $fileLog -Encoding UTF8 }
