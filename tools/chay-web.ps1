# Chạy lại web dev với code mới nhất rồi mở trình duyệt — gói trọn quy trình vào một lệnh:
#   .\tools\chay-web.ps1            (build + chạy + mở trình duyệt)
#   .\tools\chay-web.ps1 -KhongMo   (không mở tab mới, chỉ chạy lại)
# Dùng watch-supervisor.ps1 ở gốc repo nên vẫn có hot reload sau khi lên.
param([switch]$KhongMo)

$ErrorActionPreference = "Continue"
$goc   = Split-Path -Parent $PSScriptRoot
$proj  = Join-Path $goc "E-Form-Best"
$sup   = Join-Path $goc "watch-supervisor.ps1"
$cong  = 5274
$url   = "http://localhost:$cong/"
$dongHo = [Diagnostics.Stopwatch]::StartNew()

function DungTienTrinh($ds) { foreach ($p in @($ds)) { try { Stop-Process -Id $p -Force -ErrorAction Stop } catch {} } }

# 1. Dừng bản cũ. Lọc theo CommandLine thì phải bỏ chính tiến trình đang chạy script này ra.
DungTienTrinh (Get-CimInstance Win32_Process -Filter "Name='powershell.exe'" |
    Where-Object { $_.CommandLine -like "*watch-supervisor.ps1*" -and $_.ProcessId -ne $PID }).ProcessId
DungTienTrinh (Get-Process -Name "E-Form-Best" -ErrorAction SilentlyContinue).Id
DungTienTrinh (Get-CimInstance Win32_Process -Filter "Name='dotnet.exe'" |
    Where-Object { $_.CommandLine -like "*E-Form-Best*" }).ProcessId
# watch bị dừng giữa lúc build sẽ để lại VBCSCompiler giữ E-Form-Best.dll -> build sau lỗi CS2012.
# Chờ một nhịp cho nó kịp sinh ra rồi mới dọn.
Start-Sleep -Seconds 2
DungTienTrinh (Get-Process -Name "VBCSCompiler" -ErrorAction SilentlyContinue).Id
Start-Sleep -Seconds 1

# 2. Build trước để thấy lỗi ngay, khỏi phải lục watch_log.txt
Push-Location $proj
$loi = dotnet build -v q -clp:ErrorsOnly --nologo 2>&1 | Select-String ": error " | Select-Object -First 10
$maThoat = $LASTEXITCODE
Pop-Location
if ($maThoat -ne 0) {
    Write-Host "BUILD LOI:" -ForegroundColor Red
    $loi | ForEach-Object { Write-Host "  $($_.Line.Trim())" }
    exit 1
}

# 3. Chạy supervisor tách rời khỏi phiên hiện tại (đóng terminal không kéo app chết theo)
$p = Start-Process -FilePath "powershell.exe" -ArgumentList "-NoProfile","-ExecutionPolicy","Bypass","-File",$sup `
    -WorkingDirectory $proj -PassThru -WindowStyle Hidden

# 4. Chờ cổng mở (tối đa 150s)
$len = $false
for ($i = 0; $i -lt 75; $i++) {
    if (Get-NetTCPConnection -LocalPort $cong -State Listen -ErrorAction SilentlyContinue) { $len = $true; break }
    Start-Sleep -Seconds 2
}
if (-not $len) {
    Write-Host "App khong len sau 150s. Xem: Get-Content $goc\watch_log.txt -Tail 30" -ForegroundColor Red
    exit 1
}

try { $ma = (Invoke-WebRequest -Uri "$($url)Logo" -UseBasicParsing -TimeoutSec 30).StatusCode } catch { $ma = "loi: $($_.Exception.Message)" }
$app = (Get-NetTCPConnection -LocalPort $cong -State Listen).OwningProcess | Select-Object -First 1
if (-not $KhongMo) { Start-Process $url }

Write-Host ("OK  {0}  HTTP {1}  app PID {2}  supervisor PID {3}  ({4:N0}s)" -f $url, $ma, $app, $p.Id, $dongHo.Elapsed.TotalSeconds) -ForegroundColor Green
