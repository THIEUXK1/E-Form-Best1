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
    [int]$SoLuong = 10,        # số camera chụp song song
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

# ---- Chụp song song ----
$chup = {
    param($nvr, $kenh, $tk, $mk, $thuMuc, $timeout)
    try {
        $req = [Net.HttpWebRequest]::Create("http://$nvr/ISAPI/Streaming/channels/${kenh}02/picture")
        $req.Credentials = New-Object Net.NetworkCredential($tk, $mk)   # đầu ghi Hikvision dùng Digest
        $req.Timeout = $timeout
        $req.ReadWriteTimeout = $timeout
        $res = $req.GetResponse()
        try {
            if ($res.ContentType -notlike 'image/*') { return "LOI $nvr k$kenh content-type=$($res.ContentType)" }
            $ms = New-Object IO.MemoryStream
            $res.GetResponseStream().CopyTo($ms)
        } finally { $res.Close() }
        if ($ms.Length -lt 1000) { return "LOI $nvr k$kenh anh qua nho" }
        # Ghi file tạm rồi đổi tên: lỗi giữa chừng thì ảnh cũ vẫn nguyên
        $dich = Join-Path $thuMuc "${nvr}_${kenh}.jpg"
        $tam = "$dich.tmp"
        [IO.File]::WriteAllBytes($tam, $ms.ToArray())
        Move-Item $tam $dich -Force
        return 'OK'
    } catch { return "LOI $nvr k$kenh $($_.Exception.Message)" }
}

$pool = [RunspaceFactory]::CreateRunspacePool(1, $SoLuong)
$pool.Open()
$viec = foreach ($c in $dsCamera) {
    $ps = [PowerShell]::Create().AddScript($chup).AddArgument($c.Nvr).AddArgument($c.Kenh).AddArgument($taiKhoan).AddArgument($matKhau).AddArgument($thuMucAnh).AddArgument($TimeoutMs)
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
