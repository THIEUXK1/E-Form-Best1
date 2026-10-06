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
    [pscustomobject]@{ Ps = $ps; Kq = $ps.BeginInvoke() }
}
$ok = 0; $loi = New-Object System.Collections.Generic.List[string]
foreach ($v in $viec) {
    $kq = [string]($v.Ps.EndInvoke($v.Kq) | Select-Object -Last 1)
    $v.Ps.Dispose()
    if ($kq -eq 'OK') { $ok++ } else { $loi.Add($kq) }
}
$pool.Close()
GhiLog ("Chup xong: OK=$ok LOI=" + $loi.Count)
$loi | Select-Object -First 10 | ForEach-Object { GhiLog ("  " + $_) }

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
}

# Giữ log gọn: 500 dòng cuối
$dong = Get-Content $fileLog -Encoding UTF8
if ($dong.Count -gt 500) { $dong | Select-Object -Last 500 | Set-Content $fileLog -Encoding UTF8 }

if ($coLoi) { exit 1 } else { exit 0 }
