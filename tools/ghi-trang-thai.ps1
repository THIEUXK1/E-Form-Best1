# Chạy trang-thai-camera-pfvn.ps1 rồi ghi kết quả ra _trang-thai.json — task SYSTEM 5 phút/lần
# ("EForm - Trang thai camera PFVN"). 10.0.60.39/.52 kéo file này về bằng SSH key chỉ-đọc
# (administrators_authorized_keys: command=... chỉ trả nội dung file, base64).

$ErrorActionPreference = 'Continue'
$file = Join-Path $PSScriptRoot '_trang-thai.json'
$fileLog = Join-Path $PSScriptRoot '_trang-thai.log'
function GhiLog([string]$s) { Add-Content -Path $fileLog -Value ((Get-Date -Format 'yyyy-MM-dd HH:mm:ss') + ' ' + $s) -Encoding UTF8 }

# Script gốc in JSON bằng [Console]::Out.Write -> bắt trong cùng process, không qua bảng mã console
$sw = New-Object IO.StringWriter
$cu = [Console]::Out
[Console]::SetOut($sw)
try { & (Join-Path $PSScriptRoot 'trang-thai-camera-pfvn.ps1') | Out-Null }
catch { [Console]::SetOut($cu); GhiLog "LOI: $($_.Exception.Message)"; exit 1 }
finally { [Console]::SetOut($cu) }

$json = $sw.ToString().Trim()
try { $doc = $json | ConvertFrom-Json } catch { $doc = $null }
if (-not $doc -or -not $doc.capNhat) { GhiLog 'LOI: JSON khong hop le'; exit 1 }

# Gắn kết quả dò switch/cổng (do-cong-switch-pfvn.ps1, 30 phút/lần) vào cùng file để 2 máy chủ kéo về
# theo đường cũ, không phải thêm key/lệnh SSH mới. File dò hỏng thì bỏ qua, trạng thái vẫn ghi bình thường.
$fileCs = Join-Path $PSScriptRoot '_cong-switch.json'
if (Test-Path $fileCs) {
    try {
        $cs = [IO.File]::ReadAllText($fileCs).Trim()
        if (($cs | ConvertFrom-Json).capNhat) { $json = $json.Substring(0, $json.LastIndexOf('}')) + ',"congSwitch":' + $cs + '}' }
    } catch { GhiLog "LOI doc _cong-switch.json: $($_.Exception.Message)" }
}

# Ghi file tạm rồi đổi tên: lần kéo trùng lúc không gặp file ghi dở
[IO.File]::WriteAllText("$file.tmp", $json, (New-Object Text.UTF8Encoding($false)))
Move-Item "$file.tmp" $file -Force
$down = @($doc.kenh | Where-Object { -not $_.online }).Count
$loiDauGhi = @($doc.dauGhi | Where-Object { $_.loi }).Count
GhiLog "kenh=$(@($doc.kenh).Count) mat ket noi=$down dau ghi loi=$loiDauGhi"

$dong = Get-Content $fileLog -Encoding UTF8
if ($dong.Count -gt 2000) { $dong | Select-Object -Last 2000 | Set-Content $fileLog -Encoding UTF8 }
