# Chụp ảnh tất cả camera rồi đẩy lên 2 máy chủ E-Form.
#
# Vì sao có script này: máy chủ E-Form (10.0.60.39 / 10.0.60.52) không thông mạng tới dải đầu ghi
# (10.0.21.x, 10.0.28.x... cổng 80/554 bị chặn), nên trang /QLCamera không tự lấy ảnh trực tiếp được.
# Máy chạy script này phải nằm trong mạng thấy được đầu ghi; trang camera sẽ hiện ảnh lưu kèm giờ chụp.
#
# Cần có: E-Form-Best\.env (ConnectionStrings__DefaultConnection, CameraNvr__TaiKhoan, CameraNvr__MatKhau)
#         và SSH key tới 2 máy chủ trong %USERPROFILE%\.ssh.
# Chạy tay:  powershell -ExecutionPolicy Bypass -File tools\chup-anh-camera.ps1
# Task Scheduler gọi đúng lệnh trên (08:30 và 14:00 mỗi ngày).

param(
    [int]$SoLuong = 6,         # số camera chụp song song (10 thì đầu ghi 10.0.21.251 bắt đầu trả 403)
    [int]$TimeoutMs = 8000
)

$ErrorActionPreference = 'Stop'
$thuMucAnh = Join-Path $PSScriptRoot 'anh-camera'
$fileLog = Join-Path $thuMucAnh '_chup-anh.log'
New-Item -ItemType Directory -Force $thuMucAnh | Out-Null

function GhiLog([string]$s) {
    $dong = (Get-Date -Format 'yyyy-MM-dd HH:mm:ss') + ' ' + $s
    Write-Output $dong
    Add-Content -Path $fileLog -Value $dong -Encoding UTF8
}

# ---- Đọc .env (không in giá trị ra màn hình/log) ----
$fileEnv = Join-Path (Split-Path -Parent $PSScriptRoot) 'E-Form-Best\.env'
$cauHinh = @{}
foreach ($l in [IO.File]::ReadAllLines($fileEnv)) {
    if ($l -match '^\s*([A-Za-z0-9_]+)=(.*)$') { $cauHinh[$matches[1]] = $matches[2].Trim() }
}
$taiKhoan = $cauHinh['CameraNvr__TaiKhoan']
$matKhau = $cauHinh['CameraNvr__MatKhau']
$chuoiKetNoi = $cauHinh['ConnectionStrings__DefaultConnection']
if (-not $taiKhoan -or -not $matKhau -or -not $chuoiKetNoi) { GhiLog 'LOI: .env thieu CameraNvr__TaiKhoan/MatKhau hoac DefaultConnection'; exit 1 }
# SqlClient của .NET Framework không hiểu khoá có dấu cách
$chuoiKetNoi = $chuoiKetNoi -replace '(?i)Trust Server Certificate', 'TrustServerCertificate'

# ---- Danh sách camera: chỉ đọc bảng trạng thái do worker trên máy chủ cập nhật ----
$dsCamera = New-Object System.Collections.Generic.List[object]
$cn = New-Object System.Data.SqlClient.SqlConnection $chuoiKetNoi
try {
    $cn.Open()
    $cmd = $cn.CreateCommand()
    $cmd.CommandText = 'SELECT DISTINCT nvr_ip, kenh FROM dbo.KK_CameraTrangThai WHERE nvr_ip IS NOT NULL'
    $r = $cmd.ExecuteReader()
    while ($r.Read()) { $dsCamera.Add([pscustomobject]@{ Nvr = [string]$r[0]; Kenh = [int]$r[1] }) }
    $r.Close()
} finally { $cn.Close() }
GhiLog ("Bat dau chup " + $dsCamera.Count + " camera")

# ---- Cổng/giao thức ISAPI của từng đầu ghi (giống CameraXemTrucTiepService.DsDiaChiNvrAsync) ----
# Vài đầu ghi không mở cổng 80: 10.0.28.254 dùng http:8001, 10.0.29.254 dùng https:8003.
# Lấy từ inventory của hệ thống giám sát; lỗi thì chỉ thử http:80 như cũ.
$gocNvr = @{}
try {
    $isapi = $cauHinh['CameraIsapi__BaseUrl'].TrimEnd('/')
    $dangNhap = @{ username = $cauHinh['CameraIsapi__TaiKhoan']; password = $cauHinh['CameraIsapi__MatKhau'] } | ConvertTo-Json
    $token = (Invoke-RestMethod -Method Post -Uri "$isapi/auth/login" -Body $dangNhap -ContentType 'application/json' -TimeoutSec 15).access_token
    $kho = Invoke-RestMethod -Uri "$isapi/api/nvr-inventory" -Headers @{ Authorization = "Bearer $token" } -TimeoutSec 15
    foreach ($n in $kho.nvrs) {
        if (-not $n.ip) { continue }
        $https = ($n.https -eq $true)
        $cong = if ($n.api_port) { [int]$n.api_port } elseif ($https) { 443 } else { 80 }
        $gocNvr[[string]$n.ip] = $(if ($https) { 'https' } else { 'http' }) + "://$($n.ip):$cong"
    }
    GhiLog ("Inventory: " + $gocNvr.Count + " dau ghi")
} catch { GhiLog ("Khong doc duoc inventory, chi thu cong 80: " + $_.Exception.Message) }

# ---- Chụp song song ----
$chup = {
    param($nvr, $kenh, $tk, $mk, $thuMuc, $timeout, $dsGoc)
    # Đầu ghi https dùng chứng thư tự ký, firmware cũ chỉ có TLS 1.0/1.1 — chỉ trong tiến trình script này
    [Net.ServicePointManager]::SecurityProtocol = [Net.SecurityProtocolType]'Tls,Tls11,Tls12'
    [Net.ServicePointManager]::ServerCertificateValidationCallback = { $true }
    $loiCuoi = 'khong co dia chi'
    foreach ($goc in $dsGoc) {
        # Firmware NVR cũ (10.0.28.5) trả 400 cho /Streaming/..., chỉ chụp được qua /ContentMgmt/StreamingProxy/...
        foreach ($duong in "/ISAPI/Streaming/channels/${kenh}02/picture", "/ISAPI/ContentMgmt/StreamingProxy/channels/${kenh}02/picture") {
            try {
                $req = [Net.HttpWebRequest]::Create("$goc$duong")
                $req.Credentials = New-Object Net.NetworkCredential($tk, $mk)   # đầu ghi Hikvision dùng Digest
                $req.Timeout = $timeout
                $req.ReadWriteTimeout = $timeout
                $res = $req.GetResponse()
                try {
                    if ($res.ContentType -notlike 'image/*') { $loiCuoi = "content-type=$($res.ContentType)"; continue }
                    $ms = New-Object IO.MemoryStream
                    $res.GetResponseStream().CopyTo($ms)
                } finally { $res.Close() }
                if ($ms.Length -lt 1000) { $loiCuoi = 'anh qua nho'; continue }
                # Ghi file tạm rồi đổi tên: lỗi giữa chừng thì ảnh cũ vẫn nguyên
                $dich = Join-Path $thuMuc "${nvr}_${kenh}.jpg"
                $tam = "$dich.tmp"
                [IO.File]::WriteAllBytes($tam, $ms.ToArray())
                Move-Item $tam $dich -Force
                return 'OK'
            } catch {
                $we = $_.Exception.InnerException
                $loiCuoi = "$goc $($_.Exception.Message)"
                # Đầu ghi có trả lời (4xx/5xx) -> thử đường dẫn kế tiếp; không trả lời -> bỏ cổng này
                if (-not ($we -is [Net.WebException] -and $we.Response)) { break }
            }
        }
    }
    return "LOI $nvr k$kenh $loiCuoi"
}

$pool = [RunspaceFactory]::CreateRunspacePool(1, $SoLuong)
$pool.Open()
$viec = foreach ($c in $dsCamera) {
    $dsGoc = @()
    if ($gocNvr.ContainsKey($c.Nvr)) { $dsGoc += $gocNvr[$c.Nvr] }
    if ($dsGoc -notcontains "http://$($c.Nvr):80") { $dsGoc += "http://$($c.Nvr):80" }
    $ps = [PowerShell]::Create().AddScript($chup).AddArgument($c.Nvr).AddArgument($c.Kenh).AddArgument($taiKhoan).AddArgument($matKhau).AddArgument($thuMucAnh).AddArgument($TimeoutMs).AddArgument($dsGoc)
    $ps.RunspacePool = $pool
    [pscustomobject]@{ Ps = $ps; Kq = $ps.BeginInvoke(); Cam = $c }
}
$ok = 0; $loi = New-Object System.Collections.Generic.List[string]
$dsLoi = New-Object System.Collections.Generic.List[object]
foreach ($v in $viec) {
    $kq = [string]($v.Ps.EndInvoke($v.Kq) | Select-Object -Last 1)
    $v.Ps.Dispose()
    if ($kq -eq 'OK') { $ok++ } else { $loi.Add($kq); $dsLoi.Add($v.Cam) }
}
$pool.Close()
GhiLog ("Chup xong: OK=$ok LOI=" + $loi.Count)
$loi | Select-Object -First 10 | ForEach-Object { GhiLog ("  " + $_) }

# ---- Kênh chụp trực tiếp không được (camera mất kết nối, đầu ghi trả 403...): lấy khung hình cuối từ playback ----
# Hỏi đầu ghi đoạn ghi cuối của kênh (ISAPI ContentMgmt/search), rồi ffmpeg đọc 10 giây cuối đoạn đó lấy 1 khung hình.
# Giờ file đặt đúng giờ khung hình (scp -p giữ nguyên) để trang /QLCamera ghi "Ảnh lưu lúc ..." đúng thời điểm.
# Ảnh lưu đã mới bằng/hơn đoạn ghi cuối thì bỏ qua, nên camera rớt lâu chỉ tốn ffmpeg ở lượt đầu.
$ffmpeg = $cauHinh['CameraNvr__FfmpegPath']
if (-not $ffmpeg) { $ffmpeg = 'C:\go2rtc\ffmpeg.exe' }
$congRtsp = if ($cauHinh['CameraNvr__RtspPort']) { [int]$cauHinh['CameraNvr__RtspPort'] } else { 554 }

$chupPlayback = {
    param($nvr, $kenh, $tk, $mk, $thuMuc, $dsGoc, $ffmpeg, $congRtsp)
    [Net.ServicePointManager]::SecurityProtocol = [Net.SecurityProtocolType]'Tls,Tls11,Tls12'
    [Net.ServicePointManager]::ServerCertificateValidationCallback = { $true }

    # Một trang kết quả tìm kiếm; giờ đầu ghi có đuôi Z nhưng thực chất là giờ VN -> giữ nguyên, không đổi múi giờ
    function TimKiem($goc, $tu, $den, $viTri) {
        $xml = '<?xml version="1.0" encoding="UTF-8"?><CMSearchDescription>' +
            "<searchID>$([guid]::NewGuid())</searchID><trackList><trackID>${kenh}01</trackID></trackList>" +
            "<timeSpanList><timeSpan><startTime>$($tu.ToString('yyyy-MM-ddTHH:mm:ss'))Z</startTime><endTime>$($den.ToString('yyyy-MM-ddTHH:mm:ss'))Z</endTime></timeSpan></timeSpanList>" +
            "<maxResults>50</maxResults><searchResultPostion>$viTri</searchResultPostion>" +
            '<metadataList><metadataDescriptor>//recordType.meta.std-cgi.com</metadataDescriptor></metadataList></CMSearchDescription>'
        $req = [Net.HttpWebRequest]::Create("$goc/ISAPI/ContentMgmt/search")
        $req.Method = 'POST'; $req.ContentType = 'application/xml'; $req.Timeout = 15000
        $req.Credentials = New-Object Net.NetworkCredential($tk, $mk)
        $b = [Text.Encoding]::UTF8.GetBytes($xml)
        $s = $req.GetRequestStream(); $s.Write($b, 0, $b.Length); $s.Close()
        $res = $req.GetResponse()
        try { $doc = New-Object Xml.XmlDocument; $doc.Load($res.GetResponseStream()); return $doc } finally { $res.Close() }
    }

    # Dò lùi từng khoảng (1, 7, 30, 90, 365 ngày): camera mới rớt chỉ tốn 1-2 lượt gọi
    $cuoi = $null; $gocDung = $null
    $bayGio = Get-Date
    $moc = 0, 1, 7, 30, 90, 365
    # Đầu ghi 10.0.21.251 hay trả 403 khi đang bị gọi dồn (lượt chụp vừa xong, ffmpeg khác đang kéo) -> thử lại 3 lần
    for ($lan = 0; $lan -lt 3 -and -not $gocDung; $lan++) {
        if ($lan -gt 0) { Start-Sleep -Seconds 3 }
        $cuoi = $null
        foreach ($goc in $dsGoc) {
            try {
                for ($i = 1; $i -lt $moc.Count -and -not $cuoi; $i++) {
                    $viTri = 0
                    for ($trang = 0; $trang -lt 20; $trang++) {
                        $doc = TimKiem $goc ($bayGio.AddDays(-$moc[$i])) ($bayGio.AddDays(-$moc[$i - 1])) $viTri
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
                $gocDung = $goc
                break
            } catch { continue }   # cổng này không trả lời -> thử cổng kế tiếp
        }
    }
    if (-not $gocDung) { return "LOI $nvr k$kenh khong tim duoc ban ghi" }
    if (-not $cuoi) { return "KHONG $nvr k$kenh dau ghi khong con ban ghi trong 1 nam" }

    # Lùi 10 giây khỏi điểm kết thúc: sát mép đoạn ghi đầu ghi hay trả luồng rỗng
    $tu = $cuoi.KetThuc.AddSeconds(-10)
    if ($tu -lt $cuoi.BatDau) { $tu = $cuoi.BatDau }
    $dich = Join-Path $thuMuc "${nvr}_${kenh}.jpg"
    if ((Test-Path $dich) -and (Get-Item $dich).LastWriteTime -ge $tu) { return "BOQUA" }

    # Cổng RTSP thật của từng đầu ghi (10.0.28.254 dùng 8002, không mở 554); hỏi không được thì dùng cổng cấu hình
    try {
        $wc = New-Object Net.WebClient
        $wc.Credentials = New-Object Net.NetworkCredential($tk, $mk)
        $dsCong = ([xml]$wc.DownloadString("$gocDung/ISAPI/Security/adminAccesses")).AdminAccessProtocolList.AdminAccessProtocol
        $rtspCong = $dsCong | Where-Object { $_.protocol -eq 'RTSP' } | Select-Object -First 1
        if ($rtspCong -and [int]$rtspCong.portNo -gt 0) { $congRtsp = [int]$rtspCong.portNo }
    } catch { }

    $tam = Join-Path $thuMuc "${nvr}_${kenh}.pb.tmp"
    $loi = ''
    # Luồng H.265 đang ghi dở đôi khi hỏng ở đoạn sát cuối (ffmpeg INVALIDDATA) -> thử lại ở 60 giây trước điểm kết thúc
    foreach ($lui in 10, 60) {
        $tuThu = $cuoi.KetThuc.AddSeconds(-$lui)
        if ($tuThu -lt $cuoi.BatDau) { $tuThu = $cuoi.BatDau }
        $rtsp = "rtsp://$([Uri]::EscapeDataString($tk)):$([Uri]::EscapeDataString($mk))@${nvr}:$congRtsp" +
            "/Streaming/tracks/${kenh}01?starttime=$($tuThu.ToString('yyyyMMddTHHmmss'))Z&endtime=$($cuoi.KetThuc.ToString('yyyyMMddTHHmmss'))Z"
        $pi = New-Object Diagnostics.ProcessStartInfo $ffmpeg
        # Luồng chính 2688x1520 -> thu về tối đa 1280 ngang cho nhẹ; không ghi URL (có mật khẩu) ra log
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
        return "OK"
    }
    return "LOI $nvr k$kenh $loi"
}

if ($dsLoi.Count -gt 0 -and -not (Test-Path $ffmpeg)) {
    GhiLog "Bo qua lay anh tu playback: khong co ffmpeg ($ffmpeg)"
} elseif ($dsLoi.Count -gt 0) {
    $pbOk = 0; $pbBoQua = 0; $pbKhac = New-Object System.Collections.Generic.List[string]
    $conLoi = $dsLoi
    # Lượt 1: 3 ffmpeg song song (mỗi cái kéo 1 luồng playback luồng chính, nhiều hơn dễ bị đầu ghi trả 403).
    # Lượt 2: kênh còn LOI chạy lần lượt từng kênh — lỗi lượt 1 phần lớn do đầu ghi (10.0.21.251, 10.0.29.254)
    # đang bị gọi dồn, chạy riêng lại thì lấy được. KHONG (đầu ghi không có bản ghi) thì không thử lại.
    foreach ($soLuong in 3, 1) {
        if ($conLoi.Count -eq 0) { break }
        $pool = [RunspaceFactory]::CreateRunspacePool(1, $soLuong)
        $pool.Open()
        $viec = foreach ($c in $conLoi) {
            $dsGoc = @()
            if ($gocNvr.ContainsKey($c.Nvr)) { $dsGoc += $gocNvr[$c.Nvr] }
            if ($dsGoc -notcontains "http://$($c.Nvr):80") { $dsGoc += "http://$($c.Nvr):80" }
            $ps = [PowerShell]::Create().AddScript($chupPlayback).AddArgument($c.Nvr).AddArgument($c.Kenh).AddArgument($taiKhoan).AddArgument($matKhau).AddArgument($thuMucAnh).AddArgument($dsGoc).AddArgument($ffmpeg).AddArgument($congRtsp)
            $ps.RunspacePool = $pool
            [pscustomobject]@{ Ps = $ps; Kq = $ps.BeginInvoke(); Cam = $c }
        }
        $conLoi = New-Object System.Collections.Generic.List[object]
        $pbLoi = New-Object System.Collections.Generic.List[string]
        foreach ($v in $viec) {
            $kq = [string]($v.Ps.EndInvoke($v.Kq) | Select-Object -Last 1)
            $v.Ps.Dispose()
            if ($kq -eq 'OK') { $pbOk++ }
            elseif ($kq -eq 'BOQUA') { $pbBoQua++ }
            elseif ($kq -like 'LOI *') { $pbLoi.Add($kq); $conLoi.Add($v.Cam) }
            else { $pbKhac.Add($kq) }
        }
        $pool.Close()
    }
    # Chỉ LOI của lượt cuối mới là không lấy được thật
    $pbKhac.AddRange($pbLoi)
    GhiLog ("Playback: lay moi=$pbOk da co=$pbBoQua khong duoc=" + $pbKhac.Count)
    $pbKhac | ForEach-Object { GhiLog ("  " + $_) }
}

# ---- PFVN: đầu ghi 10.0.200.x chỉ thông từ máy ZPVN-WEBSRV (10.0.193.240) ----
# Chạy C:\EForm-Camera\chup-anh-camera-pfvn.ps1 trên máy đó qua SSH (mật khẩu đầu ghi nằm ở .env bên đó),
# rồi kéo thư mục anh\ về đây để đẩy lên máy chủ cùng ảnh BPVN. Lỗi PFVN không làm hỏng phần BPVN.
$thuMucPfvn = Join-Path $PSScriptRoot 'anh-camera-pfvn'
$coPfvn = $false
& {
    # ssh/scp ghi cảnh báo ra stderr: để Stop thì PowerShell 5.1 coi là lỗi và dừng cả script
    $ErrorActionPreference = 'Continue'
    $mayPfvn = 'Administrator@10.0.193.240'
    $keyPfvn = Join-Path $env:USERPROFILE '.ssh\id_ed25519_10_0_193_240'
    $kq = & ssh -i $keyPfvn -o BatchMode=yes -o ConnectTimeout=15 $mayPfvn 'powershell -NoProfile -ExecutionPolicy Bypass -File C:\EForm-Camera\chup-anh-camera-pfvn.ps1' 2>&1
    $kq | ForEach-Object { [string]$_ } | Where-Object { $_ -match 'Chup xong|LOI|KHONG' } | ForEach-Object { GhiLog ("PFVN " + ($_ -replace '^\S+ \S+ ', '')) }
    New-Item -ItemType Directory -Force $thuMucPfvn | Out-Null
    & scp -i $keyPfvn -o BatchMode=yes -o ConnectTimeout=15 -q -p "${mayPfvn}:C:/EForm-Camera/anh/*" "$thuMucPfvn\" 2>&1 | Out-Null
    if ($LASTEXITCODE -eq 0) { GhiLog 'PFVN keo anh ve: OK'; $script:coPfvn = $true } else { GhiLog "PFVN keo anh ve: LOI scp exit=$LASTEXITCODE" }
}

# ---- Đẩy lên 2 máy chủ (scp -p giữ giờ chụp làm "Ảnh lưu lúc ...") ----
$mayChu = @(
    @{ Ip = '10.0.60.39'; Key = 'id_ed25519_vnsuperman' },
    @{ Ip = '10.0.60.52'; Key = 'df_server_key' }
)
$coLoi = $false
foreach ($m in $mayChu) {
    $key = Join-Path $env:USERPROFILE ".ssh\$($m.Key)"
    $dichScp = "BESTPACIFIC\vnsuperman@$($m.Ip):C:/inetpub/CameraAnhLuu/"
    & scp -i $key -o BatchMode=yes -o StrictHostKeyChecking=no -o ConnectTimeout=15 -q -p "$thuMucAnh\*.jpg" $dichScp 2>&1 | Out-Null
    if ($LASTEXITCODE -eq 0) { GhiLog "Day len $($m.Ip): OK" } else { GhiLog "Day len $($m.Ip): LOI scp exit=$LASTEXITCODE"; $coLoi = $true }

    # Ảnh PFVN vào thư mục con PFVN (trang /QLCamera/PFVN đọc ở đó); scp không tự tạo thư mục nên tạo trước
    if ($coPfvn) {
        & {
            $ErrorActionPreference = 'Continue'
            & ssh -i $key -o BatchMode=yes -o StrictHostKeyChecking=no -o ConnectTimeout=15 "BESTPACIFIC\vnsuperman@$($m.Ip)" 'powershell -NoProfile -Command New-Item -ItemType Directory -Force C:\inetpub\CameraAnhLuu\PFVN' 2>&1 | Out-Null
            & scp -i $key -o BatchMode=yes -o StrictHostKeyChecking=no -o ConnectTimeout=15 -q -p "$thuMucPfvn\*" "BESTPACIFIC\vnsuperman@$($m.Ip):C:/inetpub/CameraAnhLuu/PFVN/" 2>&1 | Out-Null
            if ($LASTEXITCODE -eq 0) { GhiLog "Day PFVN len $($m.Ip): OK" } else { GhiLog "Day PFVN len $($m.Ip): LOI scp exit=$LASTEXITCODE"; $script:coLoi = $true }
        }
    }
}

# Giữ log gọn: 500 dòng cuối
$dong = Get-Content $fileLog -Encoding UTF8
if ($dong.Count -gt 500) { $dong | Select-Object -Last 500 | Set-Content $fileLog -Encoding UTF8 }

if ($coLoi) { exit 1 } else { exit 0 }
