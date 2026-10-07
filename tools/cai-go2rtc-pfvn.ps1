# Cài / cập nhật go2rtc trên ZPVN-WEBSRV (10.0.193.240) để E-Form xem trực tiếp camera PFVN.
#
# Vì sao: máy chủ E-Form (10.0.60.39/.52) không thông mạng tới đầu ghi PFVN 10.0.200.x, chỉ ZPVN-WEBSRV thấy được.
# go2rtc trên máy này kéo RTSP luồng phụ từ đầu ghi, trả ảnh (frame.jpeg) và video fMP4 cho E-Form qua cổng 1984.
#
# Đặt ở C:\EForm-Camera cùng go2rtc.exe, ffmpeg.exe (bản 6.1.1 — bản 8.x/9.x không chạy trên Server 2016) và .env:
#   CameraPfvn__DauGhi__N=<gốc ISAPI>|<tài khoản>|<mật khẩu>     (như chup-anh-camera-pfvn.ps1)
#   CameraPfvn__Go2rtcTaiKhoan=... / CameraPfvn__Go2rtcMatKhau=...  (E-Form gọi API go2rtc bằng tài khoản này)
#   CameraPfvn__Go2rtcChoPhep=10.0.60.39,10.0.60.52                 (IP được mở tường lửa cổng 1984)
# Chạy lại khi đầu ghi thêm/bớt kênh: sinh lại go2rtc.yaml rồi khởi động lại go2rtc.

$ErrorActionPreference = 'Stop'
$ProgressPreference = 'SilentlyContinue'
$thuMuc = $PSScriptRoot
[Net.ServicePointManager]::SecurityProtocol = [Net.SecurityProtocolType]'Tls,Tls11,Tls12'
[Net.ServicePointManager]::ServerCertificateValidationCallback = { $true }

$cauHinh = @{}
$dsDauGhi = New-Object System.Collections.Generic.List[object]
foreach ($l in [IO.File]::ReadAllLines((Join-Path $thuMuc '.env'))) {
    if ($l -match '^\s*CameraPfvn__DauGhi__\d+=(.+)$') {
        $p = $matches[1].Trim().Split('|')
        if ($p.Count -ge 3) { $dsDauGhi.Add([pscustomobject]@{ Goc = $p[0].TrimEnd('/'); Ip = ([Uri]$p[0]).Host; Tk = $p[1]; Mk = ($p[2..($p.Count - 1)] -join '|') }) }
    } elseif ($l -match '^\s*([A-Za-z0-9_]+)=(.*)$') { $cauHinh[$matches[1]] = $matches[2].Trim() }
}
$tkApi = $cauHinh['CameraPfvn__Go2rtcTaiKhoan']; $mkApi = $cauHinh['CameraPfvn__Go2rtcMatKhau']
$choPhep = @(($cauHinh['CameraPfvn__Go2rtcChoPhep'] -split ',') | ForEach-Object { $_.Trim() } | Where-Object { $_ })
if (-not $tkApi -or -not $mkApi -or $choPhep.Count -eq 0) { throw '.env thieu CameraPfvn__Go2rtcTaiKhoan / Go2rtcMatKhau / Go2rtcChoPhep' }

# Chuỗi trong YAML để trong nháy đơn; nháy đơn bên trong nhân đôi
function Yaml([string]$s) { "'" + $s.Replace("'", "''") + "'" }

# ---- Sinh go2rtc.yaml: mỗi kênh 1 luồng "pfvn_<ip>_<kênh>" = RTSP luồng phụ + bản ffmpeg chuyển H.264 ----
$dong = New-Object System.Collections.Generic.List[string]
$dong.Add('# Sinh tự động bởi cai-go2rtc-pfvn.ps1 — không sửa tay, chạy lại script khi đầu ghi đổi kênh')
$dong.Add('api:')
$dong.Add('  listen: ":1984"')
$dong.Add("  username: $(Yaml $tkApi)")
$dong.Add("  password: $(Yaml $mkApi)")
# Chỉ cần API (ảnh + fMP4 qua HTTP). RTSP server không tắt được (nguồn ffmpeg chuyển mã kéo qua nó)
# nên chỉ nghe nội bộ 127.0.0.1; WebRTC tắt hẳn để không mở thêm cổng
$dong.Add('rtsp:')
$dong.Add('  listen: "127.0.0.1:8554"')
$dong.Add('webrtc:')
$dong.Add('  listen: ""')
$dong.Add('ffmpeg:')
$dong.Add("  bin: $(Yaml (Join-Path $thuMuc 'ffmpeg.exe'))")
$dong.Add('log:')
$dong.Add('  level: warn')
$dong.Add('streams:')

$soLuong = 0
foreach ($d in $dsDauGhi) {
    $wc = New-Object Net.WebClient
    $wc.Credentials = New-Object Net.NetworkCredential($d.Tk, $d.Mk)
    $congRtsp = 554
    try {
        $c = ([xml]$wc.DownloadString("$($d.Goc)/ISAPI/Security/adminAccesses")).AdminAccessProtocolList.AdminAccessProtocol |
            Where-Object { $_.protocol -eq 'RTSP' } | Select-Object -First 1
        if ($c -and [int]$c.portNo -gt 0) { $congRtsp = [int]$c.portNo }
    } catch { }
    $kenh = ([xml]$wc.DownloadString("$($d.Goc)/ISAPI/ContentMgmt/InputProxy/channels")).InputProxyChannelList.InputProxyChannel
    foreach ($k in $kenh) {
        $ten = "pfvn_$($d.Ip.Replace('.', '_'))_$($k.id)"
        $rtsp = "rtsp://$([Uri]::EscapeDataString($d.Tk)):$([Uri]::EscapeDataString($d.Mk))@$($d.Ip):$congRtsp/Streaming/Channels/$($k.id)02"
        $dong.Add("  ${ten}:")
        $dong.Add("    - $(Yaml $rtsp)")
        # Luồng phụ nhiều camera là H.265, trình duyệt không phát được -> go2rtc tự dùng nguồn ffmpeg khi cần
        $dong.Add("    - $(Yaml "ffmpeg:$ten#video=h264")")
        $soLuong++
    }
    Write-Output "Dau ghi $($d.Ip): $(@($kenh).Count) kenh"
}
[IO.File]::WriteAllLines((Join-Path $thuMuc 'go2rtc.yaml'), $dong, (New-Object Text.UTF8Encoding($false)))
Write-Output "go2rtc.yaml: $soLuong luong"

# ---- Tường lửa: cổng 1984 chỉ mở cho máy chủ E-Form ----
$tenRule = 'EForm go2rtc 1984'
Get-NetFirewallRule -DisplayName $tenRule -ErrorAction SilentlyContinue | Remove-NetFirewallRule
New-NetFirewallRule -DisplayName $tenRule -Direction Inbound -Protocol TCP -LocalPort 1984 -RemoteAddress $choPhep -Action Allow | Out-Null
Write-Output "Firewall 1984 cho phep: $($choPhep -join ', ')"

# ---- Chạy go2rtc khi khởi động máy (Task Scheduler, tài khoản SYSTEM, tự chạy lại khi chết) ----
$tenTask = 'EForm go2rtc'
$exe = Join-Path $thuMuc 'go2rtc.exe'
$hanhDong = New-ScheduledTaskAction -Execute $exe -Argument "-config `"$(Join-Path $thuMuc 'go2rtc.yaml')`"" -WorkingDirectory $thuMuc
$kichHoat = New-ScheduledTaskTrigger -AtStartup
$caiDat = New-ScheduledTaskSettingsSet -ExecutionTimeLimit ([TimeSpan]::Zero) -RestartCount 999 -RestartInterval (New-TimeSpan -Minutes 1) -AllowStartIfOnBatteries
Register-ScheduledTask -TaskName $tenTask -Action $hanhDong -Trigger $kichHoat -Settings $caiDat -User 'SYSTEM' -RunLevel Highest -Force | Out-Null

# Khởi động lại để nạp go2rtc.yaml mới
Stop-ScheduledTask -TaskName $tenTask -ErrorAction SilentlyContinue
Get-Process go2rtc -ErrorAction SilentlyContinue | Stop-Process -Force
Start-ScheduledTask -TaskName $tenTask
Start-Sleep -Seconds 3
$chay = Get-Process go2rtc -ErrorAction SilentlyContinue
Write-Output ("go2rtc dang chay: " + [bool]$chay)
