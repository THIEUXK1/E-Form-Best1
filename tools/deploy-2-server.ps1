<#
.SYNOPSIS
  Deploy E-Form-Best lên 2 máy chủ IIS (10.0.60.39, 10.0.60.52), mỗi máy gián đoạn ~10 giây.

.DESCRIPTION
  Một lệnh làm hết, không phụ thuộc file tạm nào khác:
    1. Đọc commit đang chạy trên máy chủ (_deploy_info.txt trong thư mục site).
    2. Kiểm ?v= của JS/CSS đã sửa kể từ commit đó; view nào còn ?v cũ thì tự nâng (tắt bằng -KhongTuNangV).
    3. dotnet publish ra thư mục mới trong %TEMP%\efb-deploy (không xoá thư mục cũ — máy dev chặn xoá đệ quy).
    4. Đẩy lên staging của các máy SONG SONG (site vẫn chạy bình thường).
    5. Lần lượt từng máy: app_offline -> robocopy (KHÔNG /MIR, giữ .env/FileIT/FileHR/HuongDanIT) -> gỡ app_offline
       -> gọi thử /QLKiemKe/GetKkViTriDiaLys phải ra success:true. Máy đầu lỗi thì DỪNG, không đụng máy sau.
    6. Dọn staging + script tạm trên máy chủ, in bảng kết quả.
  Không tự commit/push — làm riêng sau khi kiểm.

.EXAMPLE
  .\tools\deploy-2-server.ps1                 # cả 2 máy
  .\tools\deploy-2-server.ps1 -May 52         # chỉ 1 máy
  .\tools\deploy-2-server.ps1 -ChiStaging     # chạy thử: build + đẩy staging, không thay site đang chạy
#>
param(
    [ValidateSet('39', '52')][string[]]$May = @('39', '52'),
    [switch]$ChiStaging,
    [switch]$KhongTuNangV,
    [string]$SoVoi,      # so ?v với commit này thay vì commit đang chạy trên máy chủ (tên khác $coso: PowerShell không phân biệt hoa thường)
    [switch]$ChiKiemV    # chỉ chạy bước kiểm ?v= rồi dừng
)

# Lệnh ngoài (ssh/scp/git/dotnet) ghi stderr là chuyện thường: không để PowerShell 5.1 coi là lỗi dừng script
$ErrorActionPreference = 'Continue'
$dongHo = [Diagnostics.Stopwatch]::StartNew()

$goc = Split-Path $PSScriptRoot -Parent
$duAn = Join-Path $goc 'E-Form-Best'
$User = 'BESTPACIFIC\vnsuperman'
$ThuMucMayChu = 'C:\inetpub\Web'
# Site chỉ bind IP:8002 (không bind localhost) nên gọi thử từ máy dev qua IP
$DsMay = [ordered]@{
    '39' = @{ Ip = '10.0.60.39'; Key = "$env:USERPROFILE\.ssh\id_ed25519_vnsuperman"; Site = 'C:\inetpub\wed\E-Form-Best' }
    '52' = @{ Ip = '10.0.60.52'; Key = "$env:USERPROFILE\.ssh\df_server_key"; Site = 'C:\inetpub\Web\E-Form-Best' }
}
$SshOpt = @('-o', 'BatchMode=yes', '-o', 'StrictHostKeyChecking=no', '-o', 'ConnectTimeout=15', '-q')

function Buoc([string]$s) { Write-Host ''; Write-Host "== $s" -ForegroundColor Cyan }
function Dung([string]$s) { Write-Host "DUNG: $s" -ForegroundColor Red; exit 1 }
function ChayTuXa($cfg, [string]$lenh) { & ssh.exe -i $cfg.Key @SshOpt "$User@$($cfg.Ip)" $lenh }
function ChayScript($cfg, [string]$ten, [string]$thamSo = '') {
    ChayTuXa $cfg "powershell -NoProfile -ExecutionPolicy Bypass -File $ThuMucMayChu\$ten $thamSo"
}
function GiaTri($dong, [string]$khoa) {
    $d = @($dong) | Where-Object { $_ -like "$khoa=*" } | Select-Object -First 1
    if ($d) { $d.Substring($khoa.Length + 1).Trim() } else { $null }
}

foreach ($m in $May) { if (-not (Test-Path $DsMay[$m].Key)) { Dung "Thiếu SSH key $($DsMay[$m].Key)" } }

# ---------------- Script chạy trên máy chủ (sinh lại mỗi lần, không cần giữ file ở đâu) ----------------
$tam = Join-Path $env:TEMP ('efb-deploy\run-' + (Get-Date -Format 'yyyyMMdd-HHmmss'))
New-Item -ItemType Directory -Force $tam | Out-Null
$utf8Bom = New-Object Text.UTF8Encoding $true

$scriptMayChu = @{
    '_efb_prep.ps1'  = @'
Remove-Item 'C:\inetpub\Web\_efb_staging' -Recurse -Force -ErrorAction SilentlyContinue
New-Item -ItemType Directory 'C:\inetpub\Web\_efb_staging' -Force | Out-Null
Write-Output 'READY'
'@
    '_efb_check.ps1' = @'
$s = 'C:\inetpub\Web\_efb_staging'
Write-Output ('STG_DLL=' + (Get-Item (Join-Path $s 'E-Form-Best.dll')).Length)
Write-Output ('STG_FILES=' + (Get-ChildItem $s -Recurse -File).Count)
'@
    '_efb_info.ps1'  = @'
param([string]$site)
$f = Join-Path $site '_deploy_info.txt'
if (Test-Path $f) { Write-Output ('INFO=' + [IO.File]::ReadAllText($f).Trim()) } else { Write-Output 'INFO=' }
'@
    '_efb_deploy.ps1' = @'
param([string]$site, [string]$info)
$ErrorActionPreference = 'Continue'
$staging = 'C:\inetpub\Web\_efb_staging'
$offline = 'C:\inetpub\Web\_efb_offline.htm'
if (-not (Test-Path $site))    { Write-Output 'ERR=NO_SITE'; exit 1 }
if (-not (Test-Path (Join-Path $staging 'E-Form-Best.dll'))) { Write-Output 'ERR=NO_STAGING'; exit 1 }
if (-not (Test-Path $offline)) { Write-Output 'ERR=NO_OFFLINE'; exit 1 }
$envF = Join-Path $site '.env'
$envTruoc = ''; if (Test-Path $envF) { $envTruoc = (Get-FileHash $envF -Algorithm SHA256).Hash }
$t0 = Get-Date
Copy-Item $offline (Join-Path $site 'app_offline.htm') -Force
# Chờ IIS tắt app (giải phóng DLL) trước khi chép đè
Start-Sleep -Seconds 7
robocopy $staging $site /E /XF appsettings.Development.json app_offline.htm .env .env.example /XD FileIT FileHR HuongDanIT /NFL /NDL /NJH /NP /R:2 /W:2 > C:\inetpub\Web\_efb_rc.txt
$rc = $LASTEXITCODE
if ($rc -lt 8) { [IO.File]::WriteAllText((Join-Path $site '_deploy_info.txt'), $info) }
Remove-Item (Join-Path $site 'app_offline.htm') -Force -ErrorAction SilentlyContinue
$t1 = Get-Date
$envSau = ''; if (Test-Path $envF) { $envSau = (Get-FileHash $envF -Algorithm SHA256).Hash }
Write-Output ('ROBOCOPY_RC=' + $rc)
Write-Output ('DOWNTIME_SEC=' + [int]($t1 - $t0).TotalSeconds)
Write-Output ('OFFLINE_LEFT=' + (Test-Path (Join-Path $site 'app_offline.htm')))
Write-Output ('DLL_SIZE=' + (Get-Item (Join-Path $site 'E-Form-Best.dll')).Length)
Write-Output ('ENV_GIU_NGUYEN=' + ($envTruoc -eq $envSau))
$dong = Get-Content C:\inetpub\Web\_efb_rc.txt | Where-Object { $_ -match '^\s*Files\s*:' } | Select-Object -First 1
if ($dong) { $so = ($dong -split '\s+') | Where-Object { $_ -match '^\d+$' }; Write-Output ('COPIED=' + $so[1]); Write-Output ('FAILED=' + $so[4]) }
'@
    '_efb_clean.ps1' = @'
foreach ($p in @('C:\inetpub\Web\_efb_staging','C:\inetpub\Web\_efb_offline.htm','C:\inetpub\Web\_efb_rc.txt',
                 'C:\inetpub\Web\_efb_prep.ps1','C:\inetpub\Web\_efb_check.ps1','C:\inetpub\Web\_efb_info.ps1',
                 'C:\inetpub\Web\_efb_deploy.ps1')) {
  Remove-Item $p -Recurse -Force -ErrorAction SilentlyContinue
}
Write-Output 'CLEANED'
'@
}
foreach ($k in $scriptMayChu.Keys) { [IO.File]::WriteAllText((Join-Path $tam $k), $scriptMayChu[$k], $utf8Bom) }
# Trang bảo trì: chỉ dùng ký tự ASCII + thực thể HTML để không lệ thuộc bảng mã
[IO.File]::WriteAllText((Join-Path $tam '_efb_offline.htm'), @'
<!doctype html>
<html lang="vi"><head><meta charset="utf-8"><meta name="viewport" content="width=device-width, initial-scale=1">
<meta http-equiv="refresh" content="5"><title>E-Form &#273;ang c&#7853;p nh&#7853;t</title>
<style>body{margin:0;min-height:100vh;display:flex;align-items:center;justify-content:center;background:#f1f5f9;font-family:Segoe UI,Arial,sans-serif;color:#0f172a}
.hop{background:#fff;padding:32px 40px;border-radius:12px;box-shadow:0 4px 20px rgba(15,23,42,.08);text-align:center;max-width:420px;margin:16px}
h1{font-size:20px;margin:0 0 8px}p{margin:0;color:#475569;font-size:15px}</style></head>
<body><div class="hop"><h1>H&#7879; th&#7889;ng &#273;ang c&#7853;p nh&#7853;t</h1>
<p>Vui l&ograve;ng ch&#7901; kho&#7843;ng 10 gi&acirc;y, trang s&#7869; t&#7921; t&#7843;i l&#7841;i.</p></div></body></html>
'@, (New-Object Text.UTF8Encoding $false))

# ---------------- 1. Commit đang chạy trên máy chủ ----------------
Buoc 'Đọc phiên bản đang chạy trên máy chủ'
$mayDau = $DsMay[$May[0]]
& scp.exe -i $mayDau.Key @SshOpt (Join-Path $tam '_efb_info.ps1') "$User@$($mayDau.Ip):$ThuMucMayChu/_efb_info.ps1"
$info = GiaTri (ChayScript $mayDau '_efb_info.ps1' "-site $($mayDau.Site)") 'INFO'
Push-Location $goc
$head = (git rev-parse --short HEAD).Trim()
$dirty = [bool](git status --porcelain -- E-Form-Best)
$coso = $head
if ($info) {
    $c = ($info -split '~')[0] -replace '\+dirty$', ''
    git cat-file -e "$c^{commit}" 2>$null
    if ($LASTEXITCODE -eq 0) { $coso = $c }
}
if ($SoVoi) { $coso = $SoVoi }
Write-Host "Máy chủ: $(if ($info) { $info } else { '(chưa có _deploy_info.txt)' })   |   Máy dev: $head$(if ($dirty) { ' + thay đổi chưa commit' })"

# ---------------- 2. Kiểm / nâng ?v= ----------------
Buoc "Kiểm ?v= của JS/CSS đã sửa so với $coso"
$dsDoi = @(git diff --name-only $coso -- E-Form-Best/wwwroot/js E-Form-Best/wwwroot/css) +
         @(git ls-files --others --exclude-standard -- E-Form-Best/wwwroot/js E-Form-Best/wwwroot/css) |
         Where-Object { $_ -and $_ -notmatch '\.min\.' } | Sort-Object -Unique
$dsView = Get-ChildItem $duAn -Recurse -Include *.cshtml -File | Where-Object { $_.FullName -notmatch '\\(bin|obj)\\' }
$vMoi = Get-Date -Format 'yyyyMMddHHmm'
$soNang = 0
foreach ($f in $dsDoi) {
    $ten = Split-Path $f -Leaf
    $mau = [regex]::Escape($ten) + '\?v=([\w.-]+)'
    $vCu = @(git grep -h -o -E ($([regex]::Escape($ten)) + '\?v=[A-Za-z0-9._-]+') $coso -- E-Form-Best 2>$null |
             ForEach-Object { ($_ -split '\?v=')[1] }) | Sort-Object -Unique
    $coThamChieu = $false
    foreach ($v in $dsView) {
        $noiDung = [IO.File]::ReadAllText($v.FullName)
        $ms = [regex]::Matches($noiDung, $mau)
        if ($ms.Count -eq 0) { continue }
        $coThamChieu = $true
        $cu = @($ms | Where-Object { $vCu -contains $_.Groups[1].Value })
        if ($cu.Count -eq 0) { continue }
        $tenView = $v.FullName.Substring($goc.Length + 1)
        if ($KhongTuNangV) { Write-Host "  CANH BAO: $tenView van dung ${ten}?v=$($cu[0].Groups[1].Value)" -ForegroundColor Yellow; continue }
        $coBom = ([IO.File]::ReadAllBytes($v.FullName)[0] -eq 0xEF)
        # ${ten} bắt buộc có ngoặc: PowerShell cho phép "?" trong tên biến, "$ten?v" sẽ thành biến rỗng và xoá mất tên file (lỗi 24/10/2026)
        $thayMoi = "${ten}?v=$vMoi"
        $noiDungMoi = [regex]::Replace($noiDung, $mau, { param($x) $thayMoi })
        if (-not $noiDungMoi.Contains($thayMoi)) { Dung "Nang ?v cho $ten trong $tenView khong ra dung chuoi, khong ghi file." }
        [IO.File]::WriteAllText($v.FullName, $noiDungMoi, (New-Object Text.UTF8Encoding $coBom))
        Write-Host "  Nang $ten -> ?v=$vMoi trong $tenView" -ForegroundColor Yellow
        $soNang++
    }
    if (-not $coThamChieu) { Write-Host "  (bo qua) $ten khong duoc view nao nap" -ForegroundColor DarkGray }
}
Write-Host "  $(@($dsDoi).Count) file JS/CSS doi, $soNang cho nang ?v"
Pop-Location
if ($ChiKiemV) { Write-Host 'Chi kiem ?v: dung.'; exit 0 }

# ---------------- 3. Build ----------------
Buoc 'dotnet publish'
$pub = Join-Path $env:TEMP ('efb-deploy\pub-' + (Get-Date -Format 'yyyyMMdd-HHmmss'))
& dotnet publish (Join-Path $duAn 'E-Form-Best.csproj') -c Release -o $pub -v q -clp:ErrorsOnly --nologo
if ($LASTEXITCODE -ne 0) { Dung 'Build lỗi, không deploy.' }
if (Test-Path (Join-Path $pub '.env')) { Dung 'Bản publish có .env — kiểm lại csproj, không đẩy secret lên.' }
$dllMay = (Get-Item (Join-Path $pub 'E-Form-Best.dll')).Length
$soFile = (Get-ChildItem $pub -Recurse -File).Count
Write-Host "  DLL $dllMay byte, $soFile file"

# ---------------- 4. Đẩy staging song song ----------------
Buoc "Đẩy lên staging: $($May -join ', ') (song song, site vẫn chạy)"
$jobs = foreach ($m in $May) {
    $cfg = $DsMay[$m]
    Start-Job -Name "efb-$m" -ArgumentList $cfg, $User, $ThuMucMayChu, $tam, $pub, $SshOpt -ScriptBlock {
        param($cfg, $User, $dir, $tam, $pub, $SshOpt)
        $dich = "$User@$($cfg.Ip)"
        & scp.exe -i $cfg.Key @SshOpt (Get-ChildItem $tam -File).FullName "${dich}:$($dir -replace '\\','/')/"
        & ssh.exe -i $cfg.Key @SshOpt $dich "powershell -NoProfile -ExecutionPolicy Bypass -File $dir\_efb_prep.ps1"
        & scp.exe -i $cfg.Key @SshOpt -r "$pub\*" "${dich}:$($dir -replace '\\','/')/_efb_staging/"
        & ssh.exe -i $cfg.Key @SshOpt $dich "powershell -NoProfile -ExecutionPolicy Bypass -File $dir\_efb_check.ps1"
    }
}
$jobs | Wait-Job | Out-Null
$stg = @{}
foreach ($m in $May) {
    $ra = Receive-Job -Name "efb-$m" 2>$null
    Remove-Job -Name "efb-$m" -Force
    $d = GiaTri $ra 'STG_DLL'; $n = GiaTri $ra 'STG_FILES'
    $stg[$m] = "$n file"
    Write-Host "  .$m staging: DLL=$d, $n file"
    if ("$d" -ne "$dllMay" -or "$n" -ne "$soFile") { Dung ".$m staging không khớp bản build (DLL $dllMay, $soFile file). Site chưa bị đụng." }
}

if ($ChiStaging) {
    Buoc 'Chỉ staging: dọn và dừng (site không bị đụng)'
    foreach ($m in $May) { ChayScript $DsMay[$m] '_efb_clean.ps1' | Out-Null }
    Write-Host "Xong sau $([int]$dongHo.Elapsed.TotalSeconds) giây." -ForegroundColor Green
    exit 0
}

# ---------------- 5. Deploy lần lượt ----------------
if ($ChiKiemV -or $ChiStaging) { Dung 'Che do thu khong duoc toi buoc deploy.' }
# Không dùng | ; , : vì đi qua cmd (.39) / PowerShell (.52) trên dòng lệnh ssh
$infoMoi = "$head$(if ($dirty) { '+dirty' })~$(Get-Date -Format 'yyyy-MM-dd_HHmm')~$env:USERNAME"
$ketQua = @()
foreach ($m in $May) {
    $cfg = $DsMay[$m]
    Buoc "Deploy .$m ($($cfg.Site))"
    $ra = ChayScript $cfg '_efb_deploy.ps1' "-site $($cfg.Site) -info $infoMoi"
    $loi = GiaTri $ra 'ERR'
    if ($loi) { Dung ".$m báo $loi" }

    # Gọi thử: endpoint đọc DB không cần đăng nhập, phải trả success:true
    $dbOk = $false
    for ($i = 0; $i -lt 6 -and -not $dbOk; $i++) {
        $body = & curl.exe -s -m 60 "http://$($cfg.Ip):8002/QLKiemKe/GetKkViTriDiaLys"
        $dbOk = "$body" -match '"success":true'
        if (-not $dbOk) { Start-Sleep -Seconds 3 }
    }
    $trangChu = & curl.exe -s -o NUL -w '%{http_code}' -m 60 "http://$($cfg.Ip):8002/"

    $dong = [pscustomobject]@{
        May = ".$m"; GianDoanGiay = GiaTri $ra 'DOWNTIME_SEC'; FileCopy = GiaTri $ra 'COPIED'; Loi = GiaTri $ra 'FAILED'
        DLL = GiaTri $ra 'DLL_SIZE'; EnvGiuNguyen = GiaTri $ra 'ENV_GIU_NGUYEN'; DocDB = $(if ($dbOk) { 'OK' } else { 'LOI' }); TrangChu = $trangChu
    }
    $ketQua += $dong
    $dong | Format-List | Out-String | Write-Host

    $hong = -not $dbOk -or $dong.DLL -ne "$dllMay" -or $dong.EnvGiuNguyen -ne 'True' -or $dong.Loi -ne '0' -or (GiaTri $ra 'OFFLINE_LEFT') -ne 'False'
    if ($hong) { Dung ".$m sau deploy không đạt — KHÔNG deploy máy tiếp theo. Staging còn trên máy chủ để kiểm." }
}

# ---------------- 6. Dọn ----------------
Buoc 'Dọn staging + script tạm trên máy chủ'
foreach ($m in $May) { Write-Host "  .$m $(ChayScript $DsMay[$m] '_efb_clean.ps1')" }

Buoc "KẾT QUẢ ($([int]$dongHo.Elapsed.TotalSeconds) giây)"
$ketQua | Format-Table -AutoSize | Out-String | Write-Host
Write-Host "Đã chạy: $infoMoi" -ForegroundColor Green
Write-Host 'Chưa commit/push — tự làm sau khi kiểm trên trình duyệt.'
