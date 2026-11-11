<#
  Đồng bộ file đính kèm giữa 2 máy chủ E-Form (chạy trên 10.0.60.39, task "EForm-DongBoFile" 5 phút/lần, tài khoản SYSTEM).

  Vì sao: 2 site chạy song song (.39 qua e-form.bestpacific.vn, .52 qua 10.0.60.52:8002) nhưng file người dùng
  tải lên chỉ nằm trên máy nhận request — đơn tạo ở máy này mở ở máy kia sẽ thiếu file.

  - Chỉ THÊM / cập nhật file mới hơn (robocopy /E /XO), KHÔNG xoá (không /MIR): file bị xoá ở một máy vẫn còn ở máy kia,
    chấp nhận được hơn là mất file đính kèm của đơn.
  - SYSTEM của .39 ra mạng bằng tài khoản máy BESTPACIFIC\VN-WEB$ → share ẩn trên .52 chỉ cấp Change cho tài khoản đó,
    không lưu mật khẩu nào.
  - Thư mục nằm trong /XD của deploy-2-server.ps1 nên deploy không đụng tới.
#>
$ErrorActionPreference = 'Continue'
$goc39 = 'C:\inetpub\wed\E-Form-Best\wwwroot'
$may52 = '\\10.0.60.52'
$dsThuMuc = @(
    @{ Ten = 'FileIT';     Share = 'EFormFileIT$' },
    @{ Ten = 'FileHR';     Share = 'EFormFileHR$' },
    @{ Ten = 'HuongDanIT'; Share = 'EFormHuongDanIT$' }
)
$log = 'C:\inetpub\wed\dong-bo-file-eform.log'
$tuyChon = @('/E', '/XO', '/R:1', '/W:2', '/NP', '/NDL', '/NJH', '/FFT')

$dong = @("== $(Get-Date -Format 'dd/MM/yyyy HH:mm:ss')")
foreach ($t in $dsThuMuc) {
    $local = Join-Path $goc39 $t.Ten
    $xa = "$may52\$($t.Share)"
    if (-not (Test-Path $local)) { New-Item -ItemType Directory $local -Force | Out-Null }
    if (-not (Test-Path $xa)) { $dong += "$($t.Ten): KHONG VAO DUOC $xa"; continue }

    foreach ($chieu in @(@($local, $xa, '.39->.52'), @($xa, $local, '.52->.39'))) {
        $kq = & robocopy.exe $chieu[0] $chieu[1] @tuyChon
        $ma = $LASTEXITCODE
        # robocopy: 0-7 là thành công (1 = có chép file), >= 8 là lỗi
        $soFile = @($kq | Where-Object { $_ -match '^\s+(New File|Newer)\s' }).Count
        if ($ma -ge 8) { $dong += "$($t.Ten) $($chieu[2]): LOI ma=$ma"; $dong += @($kq | Where-Object { $_ -match 'ERROR' } | Select-Object -First 5) }
        elseif ($soFile -gt 0) { $dong += "$($t.Ten) $($chieu[2]): chep $soFile file" }
    }
}

# Giữ log gọn: chỉ 500 dòng cuối
$cu = if (Test-Path $log) { @(Get-Content $log -Tail 500) } else { @() }
Set-Content -Path $log -Value ($cu + $dong) -Encoding UTF8
