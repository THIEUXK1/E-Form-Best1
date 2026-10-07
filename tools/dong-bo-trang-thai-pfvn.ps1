# Đồng bộ trạng thái camera PFVN lên máy chủ E-Form — chạy trên máy dev 5 phút/lần (Task Scheduler
# "EForm - Trang thai camera PFVN").
#
# Máy chủ E-Form không thông mạng đầu ghi PFVN, chỉ ZPVN-WEBSRV (10.0.193.240) thấy: SSH sang chạy
# C:\EForm-Camera\trang-thai-camera-pfvn.ps1, nhận JSON trạng thái, đẩy lên C:\inetpub\CameraAnhLuu\PFVN\_trang-thai.json
# của 10.0.60.39 và .52. Job CameraLichSuWorker trong E-Form đọc file đó để ghi lịch sử đổi trạng thái.

$ErrorActionPreference = 'Continue'   # ssh/scp ghi cảnh báo ra stderr, để Stop thì PowerShell 5.1 dừng script
$thuMuc = Join-Path $PSScriptRoot 'anh-camera-pfvn'
$fileLog = Join-Path $PSScriptRoot 'anh-camera-pfvn\_trang-thai.log'
New-Item -ItemType Directory -Force $thuMuc | Out-Null

function GhiLog([string]$s) {
    Add-Content -Path $fileLog -Value ((Get-Date -Format 'yyyy-MM-dd HH:mm:ss') + ' ' + $s) -Encoding UTF8
}

$keyPfvn = Join-Path $env:USERPROFILE '.ssh\id_ed25519_10_0_193_240'
$kq = & ssh -i $keyPfvn -o BatchMode=yes -o ConnectTimeout=15 Administrator@10.0.193.240 `
    'powershell -NoProfile -ExecutionPolicy Bypass -File C:\EForm-Camera\trang-thai-camera-pfvn.ps1' 2>$null
$json = ($kq | Where-Object { $_ -like '{*' } | Select-Object -Last 1)
try { $doc = $json | ConvertFrom-Json } catch { $doc = $null }
if (-not $doc -or -not $doc.capNhat) { GhiLog 'LOI: khong nhan duoc trang thai tu ZPVN-WEBSRV'; exit 1 }

# Ghi file tạm rồi đổi tên: E-Form đọc cùng lúc không bao giờ gặp file ghi dở
$file = Join-Path $thuMuc '_trang-thai.json'
[IO.File]::WriteAllText("$file.tmp", $json, (New-Object Text.UTF8Encoding($false)))
Move-Item "$file.tmp" $file -Force

# Máy dev chạy E-Form đọc ảnh lưu ở thư mục khai trong .env (CameraNvr__ThuMucAnhLuu) -> chép thêm vào đó
$dongThuMuc = Get-Content (Join-Path (Split-Path -Parent $PSScriptRoot) 'E-Form-Best\.env') -Encoding UTF8 |
    Where-Object { $_ -match '^\s*CameraNvr__ThuMucAnhLuu=' } | Select-Object -First 1
if ($dongThuMuc) {
    $thuMucDev = Join-Path ($dongThuMuc -replace '^\s*CameraNvr__ThuMucAnhLuu=', '').Trim() 'PFVN'
    try { New-Item -ItemType Directory -Force $thuMucDev | Out-Null; Copy-Item $file (Join-Path $thuMucDev '_trang-thai.json') -Force }
    catch { GhiLog "LOI chep vao $thuMucDev : $($_.Exception.Message)" }
}

$mayChu = @(
    @{ Ip = '10.0.60.39'; Key = 'id_ed25519_vnsuperman' },
    @{ Ip = '10.0.60.52'; Key = 'df_server_key' }
)
$loi = @()
foreach ($m in $mayChu) {
    $key = Join-Path $env:USERPROFILE ".ssh\$($m.Key)"
    & scp -i $key -o BatchMode=yes -o StrictHostKeyChecking=no -o ConnectTimeout=15 -q $file "BESTPACIFIC\vnsuperman@$($m.Ip):C:/inetpub/CameraAnhLuu/PFVN/_trang-thai.json" 2>$null
    if ($LASTEXITCODE -ne 0) { $loi += "$($m.Ip) scp exit=$LASTEXITCODE" }
}
$down = @($doc.kenh | Where-Object { -not $_.online }).Count
$loiDauGhi = @($doc.dauGhi | Where-Object { $_.loi }).Count
GhiLog ("kenh=$(@($doc.kenh).Count) mat ket noi=$down dau ghi loi=$loiDauGhi" + $(if ($loi) { ' | LOI day: ' + ($loi -join ', ') } else { '' }))

# Giữ log gọn: 2000 dòng cuối (~1 tuần)
$dong = Get-Content $fileLog -Encoding UTF8
if ($dong.Count -gt 2000) { $dong | Select-Object -Last 2000 | Set-Content $fileLog -Encoding UTF8 }
if ($loi) { exit 1 } else { exit 0 }
