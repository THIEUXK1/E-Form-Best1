# Chụp ảnh lưu sẵn camera PFVN — chạy TRÊN máy ZPVN-WEBSRV (10.0.193.240), đặt ở C:\EForm-Camera.
#
# Vì sao chạy ở đó: dải đầu ghi PFVN 10.0.200.x chỉ thông từ mạng PFVN; máy dev và máy chủ E-Form
# (10.0.60.39/.52) không gọi thẳng được. Script chup-anh-camera.ps1 trên máy dev SSH sang chạy script này,
# kéo thư mục anh\ về rồi đẩy lên máy chủ E-Form (C:\inetpub\CameraAnhLuu\PFVN).
#
# Cần có cùng thư mục: ffmpeg.exe và .env, mỗi đầu ghi 1 dòng (không đưa mật khẩu vào script):
#   CameraPfvn__DauGhi__1=http://10.0.200.249|admin|<mật khẩu>
# Kết quả trong anh\: "{ip}_{kênh}.jpg" + "_kenh.json" (danh sách kênh, không có mật khẩu) cho trang /QLCamera/PFVN.

param(
    [int]$SoLuong = 6,
    [int]$TimeoutMs = 8000
)

$ErrorActionPreference = 'Stop'
$ProgressPreference = 'SilentlyContinue'
$thuMucAnh = Join-Path $PSScriptRoot 'anh'
$fileLog = Join-Path $PSScriptRoot '_chup-anh.log'
$ffmpeg = Join-Path $PSScriptRoot 'ffmpeg.exe'
New-Item -ItemType Directory -Force $thuMucAnh | Out-Null

function GhiLog([string]$s) {
    $dong = (Get-Date -Format 'yyyy-MM-dd HH:mm:ss') + ' ' + $s
    Write-Output $dong
    Add-Content -Path $fileLog -Value $dong -Encoding UTF8
}

# Đầu ghi https dùng chứng thư tự ký, firmware cũ chỉ có TLS 1.0/1.1 — chỉ trong tiến trình script này
[Net.ServicePointManager]::SecurityProtocol = [Net.SecurityProtocolType]'Tls,Tls11,Tls12'
[Net.ServicePointManager]::ServerCertificateValidationCallback = { $true }

# ---- Đọc .env (không in giá trị ra màn hình/log) ----
$dsDauGhi = New-Object System.Collections.Generic.List[object]
foreach ($l in [IO.File]::ReadAllLines((Join-Path $PSScriptRoot '.env'))) {
    if ($l -match '^\s*CameraPfvn__DauGhi__\d+=(.+)$') {
        $p = $matches[1].Trim().Split('|')
        if ($p.Count -lt 3) { continue }
        $dsDauGhi.Add([pscustomobject]@{ Goc = $p[0].TrimEnd('/'); Ip = ([Uri]$p[0]).Host; Tk = $p[1]; Mk = ($p[2..($p.Count - 1)] -join '|') })
    }
}
if ($dsDauGhi.Count -eq 0) { GhiLog 'LOI: .env chua khai CameraPfvn__DauGhi__N'; exit 1 }

# ---- Danh sách kênh, trạng thái online, cổng RTSP của từng đầu ghi (ISAPI) ----
$dsKenh = New-Object System.Collections.Generic.List[object]
foreach ($d in $dsDauGhi) {
    try {
        $wc = New-Object Net.WebClient
        $wc.Credentials = New-Object Net.NetworkCredential($d.Tk, $d.Mk)
        $wc.Encoding = [Text.Encoding]::UTF8
        $tenDauGhi = ([xml]$wc.DownloadString("$($d.Goc)/ISAPI/System/deviceInfo")).DeviceInfo.deviceName

        # Cổng RTSP thật (mặc định 554); hỏi không được thì giữ 554
        $congRtsp = 554
        try {
            $c = ([xml]$wc.DownloadString("$($d.Goc)/ISAPI/Security/adminAccesses")).AdminAccessProtocolList.AdminAccessProtocol |
                Where-Object { $_.protocol -eq 'RTSP' } | Select-Object -First 1
            if ($c -and [int]$c.portNo -gt 0) { $congRtsp = [int]$c.portNo }
        } catch { }

        $online = @{}
        try {
            foreach ($s in ([xml]$wc.DownloadString("$($d.Goc)/ISAPI/ContentMgmt/InputProxy/channels/status")).InputProxyChannelStatusList.InputProxyChannelStatus) {
                $online[[string]$s.id] = ($s.online -eq 'true')
            }
        } catch { }

        $soKenh = 0
        foreach ($k in ([xml]$wc.DownloadString("$($d.Goc)/ISAPI/ContentMgmt/InputProxy/channels")).InputProxyChannelList.InputProxyChannel) {
            $dsKenh.Add([pscustomobject]@{
                Goc = $d.Goc; Nvr = $d.Ip; Tk = $d.Tk; Mk = $d.Mk; CongRtsp = $congRtsp; TenDauGhi = $tenDauGhi
                Kenh = [int]$k.id; Ten = [string]$k.name; IpCamera = [string]$k.sourceInputPortDescriptor.ipAddress
                # Không đọc được trạng thái thì coi như online: vẫn thử chụp trực tiếp trước
                Online = $(if ($online.ContainsKey([string]$k.id)) { $online[[string]$k.id] } else { $true })
            })
            $soKenh++
        }
        GhiLog "Dau ghi $($d.Ip): $soKenh kenh, RTSP $congRtsp"
    } catch { GhiLog "LOI dau ghi $($d.Ip): $($_.Exception.Message)" }
}

# ---- Xử lý 1 kênh: chụp trực tiếp; camera mất kết nối / chụp lỗi thì lấy khung hình cuối từ playback ----
# Trả 'OK' (ảnh trực tiếp), 'PLAYBACK' (ảnh từ bản ghi), 'BOQUA' (ảnh playback đã có, bản ghi không mới hơn),
# 'KHONG ...' (đầu ghi không có bản ghi) hoặc 'LOI ...'.
$xuLy = {
    param($k, $thuMuc, $ffmpeg, $timeout)
    [Net.ServicePointManager]::SecurityProtocol = [Net.SecurityProtocolType]'Tls,Tls11,Tls12'
    [Net.ServicePointManager]::ServerCertificateValidationCallback = { $true }
    $dich = Join-Path $thuMuc "$($k.Nvr)_$($k.Kenh).jpg"

    # Camera đang mất kết nối thì đầu ghi trả ảnh "No Video"/lỗi -> bỏ qua chụp, sang thẳng playback
    if ($k.Online) {
        foreach ($duong in "/ISAPI/Streaming/channels/$($k.Kenh)02/picture", "/ISAPI/ContentMgmt/StreamingProxy/channels/$($k.Kenh)02/picture") {
            try {
                $req = [Net.HttpWebRequest]::Create("$($k.Goc)$duong")
                $req.Credentials = New-Object Net.NetworkCredential($k.Tk, $k.Mk)   # đầu ghi Hikvision dùng Digest
                $req.Timeout = $timeout; $req.ReadWriteTimeout = $timeout
                $res = $req.GetResponse()
                try {
                    if ($res.ContentType -notlike 'image/*') { continue }
                    $ms = New-Object IO.MemoryStream
                    $res.GetResponseStream().CopyTo($ms)
                } finally { $res.Close() }
                if ($ms.Length -lt 1000) { continue }
                $tam = "$dich.tmp"
                [IO.File]::WriteAllBytes($tam, $ms.ToArray())
                Move-Item $tam $dich -Force
                return 'OK'
            } catch {
                $we = $_.Exception.InnerException
                if (-not ($we -is [Net.WebException] -and $we.Response)) { break }
            }
        }
    }

    # Một trang kết quả tìm kiếm; giờ đầu ghi có đuôi Z nhưng thực chất là giờ VN -> giữ nguyên, không đổi múi giờ
    function TimKiem($tu, $den, $viTri) {
        $xml = '<?xml version="1.0" encoding="UTF-8"?><CMSearchDescription>' +
            "<searchID>$([guid]::NewGuid())</searchID><trackList><trackID>$($k.Kenh)01</trackID></trackList>" +
            "<timeSpanList><timeSpan><startTime>$($tu.ToString('yyyy-MM-ddTHH:mm:ss'))Z</startTime><endTime>$($den.ToString('yyyy-MM-ddTHH:mm:ss'))Z</endTime></timeSpan></timeSpanList>" +
            "<maxResults>50</maxResults><searchResultPostion>$viTri</searchResultPostion>" +
            '<metadataList><metadataDescriptor>//recordType.meta.std-cgi.com</metadataDescriptor></metadataList></CMSearchDescription>'
        $req = [Net.HttpWebRequest]::Create("$($k.Goc)/ISAPI/ContentMgmt/search")
        $req.Method = 'POST'; $req.ContentType = 'application/xml'; $req.Timeout = 15000
        $req.Credentials = New-Object Net.NetworkCredential($k.Tk, $k.Mk)
        $b = [Text.Encoding]::UTF8.GetBytes($xml)
        $s = $req.GetRequestStream(); $s.Write($b, 0, $b.Length); $s.Close()
        $res = $req.GetResponse()
        try { $doc = New-Object Xml.XmlDocument; $doc.Load($res.GetResponseStream()); return $doc } finally { $res.Close() }
    }

    # Dò lùi từng khoảng (1, 7, 30, 90, 365 ngày); đầu ghi đang bị gọi dồn hay trả 403 -> thử lại 3 lần
    $cuoi = $null; $timDuoc = $false
    $bayGio = Get-Date
    $moc = 0, 1, 7, 30, 90, 365
    for ($lan = 0; $lan -lt 3 -and -not $timDuoc; $lan++) {
        if ($lan -gt 0) { Start-Sleep -Seconds 3 }
        $cuoi = $null
        try {
            for ($i = 1; $i -lt $moc.Count -and -not $cuoi; $i++) {
                $viTri = 0
                for ($trang = 0; $trang -lt 20; $trang++) {
                    $doc = TimKiem ($bayGio.AddDays(-$moc[$i])) ($bayGio.AddDays(-$moc[$i - 1])) $viTri
                    foreach ($m in $doc.GetElementsByTagName('searchMatchItem')) {
                        $bd = [datetime]::ParseExact($m.timeSpan.startTime.TrimEnd('Z'), 'yyyy-MM-ddTHH:mm:ss', $null)
                        $kt = [datetime]::ParseExact($m.timeSpan.endTime.TrimEnd('Z'), 'yyyy-MM-ddTHH:mm:ss', $null)
                        if (-not $cuoi -or $kt -gt $cuoi.KetThuc) { $cuoi = @{ BatDau = $bd; KetThuc = $kt } }
                    }
                    $goc0 = $doc.DocumentElement
                    if ($goc0.responseStatusStrg -ne 'MORE') { break }
                    $soTrang = [int]$goc0.numOfMatches; $tong = [int]$goc0.totalMatches
                    if ($soTrang -le 0) { break }
                    # Kết quả tăng dần theo giờ: biết tổng thì nhảy thẳng trang cuối
                    $viTri = if ($tong -gt $viTri + $soTrang) { [Math]::Max($viTri + $soTrang, $tong - 50) } else { $viTri + $soTrang }
                }
            }
            $timDuoc = $true
        } catch { }
    }
    if (-not $timDuoc) { return "LOI $($k.Nvr) k$($k.Kenh) khong tim duoc ban ghi" }
    if (-not $cuoi) { return "KHONG $($k.Nvr) k$($k.Kenh) dau ghi khong con ban ghi trong 1 nam" }

    $tu = $cuoi.KetThuc.AddSeconds(-10)
    if ($tu -lt $cuoi.BatDau) { $tu = $cuoi.BatDau }
    if ((Test-Path $dich) -and (Get-Item $dich).LastWriteTime -ge $tu) { return 'BOQUA' }
    if (-not (Test-Path $ffmpeg)) { return "LOI $($k.Nvr) k$($k.Kenh) khong co ffmpeg" }

    $tam = "$dich.pb.tmp"
    $loi = ''
    # Luồng H.265 đang ghi dở đôi khi hỏng ở đoạn sát cuối (ffmpeg INVALIDDATA) -> thử lại ở 60 giây trước điểm kết thúc
    foreach ($lui in 10, 60) {
        $tuThu = $cuoi.KetThuc.AddSeconds(-$lui)
        if ($tuThu -lt $cuoi.BatDau) { $tuThu = $cuoi.BatDau }
        $rtsp = "rtsp://$([Uri]::EscapeDataString($k.Tk)):$([Uri]::EscapeDataString($k.Mk))@$($k.Nvr):$($k.CongRtsp)" +
            "/Streaming/tracks/$($k.Kenh)01?starttime=$($tuThu.ToString('yyyyMMddTHHmmss'))Z&endtime=$($cuoi.KetThuc.ToString('yyyyMMddTHHmmss'))Z"
        $pi = New-Object Diagnostics.ProcessStartInfo $ffmpeg
        # Thu về tối đa 1280 ngang cho nhẹ; không ghi URL (có mật khẩu) ra log
        $pi.Arguments = "-hide_banner -loglevel error -rtsp_transport tcp -timeout 10000000 -i `"$rtsp`" -frames:v 1 " +
            "-vf `"scale='min(1280,iw)':-2`" -q:v 4 -f image2 -vcodec mjpeg -y `"$tam`""
        $pi.UseShellExecute = $false; $pi.CreateNoWindow = $true
        $pi.RedirectStandardError = $true; $pi.RedirectStandardOutput = $true
        $p = [Diagnostics.Process]::Start($pi)
        [void]$p.StandardError.ReadToEndAsync()   # đọc bỏ stderr để ffmpeg không nghẽn vì đầy bộ đệm
        if (-not $p.WaitForExit(40000)) { $p.Kill(); $loi = 'ffmpeg qua 40 giay'; continue }
        if ($p.ExitCode -ne 0 -or -not (Test-Path $tam) -or (Get-Item $tam).Length -lt 1000) {
            Remove-Item $tam -ErrorAction SilentlyContinue
            $loi = "ffmpeg exit=$($p.ExitCode)"
            continue
        }
        Move-Item $tam $dich -Force
        (Get-Item $dich).LastWriteTime = $tuThu
        return 'PLAYBACK'
    }
    return "LOI $($k.Nvr) k$($k.Kenh) $loi"
}

# ---- Lượt 1 song song; lượt 2 chạy lại lần lượt các kênh còn LOI (đầu ghi bị gọi dồn hay trả 403/luồng hỏng) ----
GhiLog ("Bat dau chup " + $dsKenh.Count + " camera")
$ketQua = @{}
$conLam = $dsKenh
foreach ($soLuong in $SoLuong, 1) {
    if ($conLam.Count -eq 0) { break }
    $pool = [RunspaceFactory]::CreateRunspacePool(1, $soLuong)
    $pool.Open()
    $viec = foreach ($k in $conLam) {
        $ps = [PowerShell]::Create().AddScript($xuLy).AddArgument($k).AddArgument($thuMucAnh).AddArgument($ffmpeg).AddArgument($TimeoutMs)
        $ps.RunspacePool = $pool
        [pscustomobject]@{ Ps = $ps; Kq = $ps.BeginInvoke(); K = $k }
    }
    $conLam = New-Object System.Collections.Generic.List[object]
    foreach ($v in $viec) {
        $kq = [string]($v.Ps.EndInvoke($v.Kq) | Select-Object -Last 1)
        $v.Ps.Dispose()
        $ketQua["$($v.K.Nvr)_$($v.K.Kenh)"] = $kq
        if ($kq -like 'LOI *') { $conLam.Add($v.K) }
    }
    $pool.Close()
}

$dem = @{ OK = 0; PLAYBACK = 0; BOQUA = 0; KHAC = 0 }
foreach ($kq in $ketQua.Values) { if ($dem.ContainsKey($kq)) { $dem[$kq]++ } else { $dem.KHAC++ } }
GhiLog ("Chup xong: truc tiep=$($dem.OK) playback=$($dem.PLAYBACK) da co=$($dem.BOQUA) khong duoc=$($dem.KHAC)")
$ketQua.Values | Where-Object { $_ -like 'LOI *' -or $_ -like 'KHONG *' } | ForEach-Object { GhiLog ("  " + $_) }

# ---- Danh sách kênh cho trang /QLCamera/PFVN (không có tài khoản/mật khẩu) ----
$dsXuat = foreach ($k in $dsKenh) {
    [pscustomobject]@{
        nvr = $k.Nvr; tenDauGhi = $k.TenDauGhi; kenh = $k.Kenh; ten = $k.Ten; ipCamera = $k.IpCamera
        online = [bool]$k.Online; ketQua = ($ketQua["$($k.Nvr)_$($k.Kenh)"] -split ' ')[0]
    }
}
$json = ConvertTo-Json @{ capNhat = (Get-Date -Format 'yyyy-MM-ddTHH:mm:ss'); kenh = @($dsXuat) } -Depth 4
[IO.File]::WriteAllText((Join-Path $thuMucAnh '_kenh.json'), $json, (New-Object Text.UTF8Encoding($false)))

# Giữ log gọn: 500 dòng cuối
$dong = Get-Content $fileLog -Encoding UTF8
if ($dong.Count -gt 500) { $dong | Select-Object -Last 500 | Set-Content $fileLog -Encoding UTF8 }
