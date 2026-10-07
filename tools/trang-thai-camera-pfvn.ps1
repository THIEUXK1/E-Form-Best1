# Trạng thái online/offline camera PFVN — chạy TRÊN ZPVN-WEBSRV (C:\EForm-Camera), vài giây/lần, không chụp ảnh.
#
# Máy dev gọi qua SSH 5 phút/lần (tools/dong-bo-trang-thai-pfvn.ps1), đọc JSON in ra stdout rồi đẩy lên
# máy chủ E-Form thành "_trang-thai.json"; E-Form so với lần trước để ghi lịch sử đổi trạng thái (KK_CameraLichSu).
# Đọc .env cùng thư mục (CameraPfvn__DauGhi__N=gốc|tài khoản|mật khẩu), không in mật khẩu.
# "dauGhi" có địa chỉ gốc web (goc, không mật khẩu) để trang E-Form dựng link mở đầu ghi; đầu ghi không trả
# lời thì kèm lỗi và không có kênh nào của nó trong "kenh".

$ErrorActionPreference = 'Stop'
$ProgressPreference = 'SilentlyContinue'
[Net.ServicePointManager]::SecurityProtocol = [Net.SecurityProtocolType]'Tls,Tls11,Tls12'
[Net.ServicePointManager]::ServerCertificateValidationCallback = { $true }

# WebClient không có timeout -> lớp con đặt 8 giây, đầu ghi treo không làm cả lượt chậm theo
Add-Type -TypeDefinition @'
public class WebClientHetGio : System.Net.WebClient {
    protected override System.Net.WebRequest GetWebRequest(System.Uri address) {
        var r = base.GetWebRequest(address); r.Timeout = 8000; return r;
    }
}
'@

$dsKenh = New-Object System.Collections.Generic.List[object]
$dsDauGhi = New-Object System.Collections.Generic.List[object]
foreach ($l in [IO.File]::ReadAllLines((Join-Path $PSScriptRoot '.env'))) {
    if ($l -notmatch '^\s*CameraPfvn__DauGhi__\d+=(.+)$') { continue }
    $p = $matches[1].Trim().Split('|')
    if ($p.Count -lt 3) { continue }
    $goc = $p[0].TrimEnd('/'); $ip = ([Uri]$goc).Host
    try {
        $wc = New-Object WebClientHetGio
        $wc.Credentials = New-Object Net.NetworkCredential($p[1], ($p[2..($p.Count - 1)] -join '|'))
        $wc.Encoding = [Text.Encoding]::UTF8
        $online = @{}
        foreach ($s in ([xml]$wc.DownloadString("$goc/ISAPI/ContentMgmt/InputProxy/channels/status")).InputProxyChannelStatusList.InputProxyChannelStatus) {
            $online[[string]$s.id] = ($s.online -eq 'true')
        }
        foreach ($k in ([xml]$wc.DownloadString("$goc/ISAPI/ContentMgmt/InputProxy/channels")).InputProxyChannelList.InputProxyChannel) {
            $dsKenh.Add([pscustomobject]@{
                nvr = $ip; kenh = [int]$k.id; ten = [string]$k.name; ipCamera = [string]$k.sourceInputPortDescriptor.ipAddress
                online = [bool]$online[[string]$k.id]
            })
        }
        $dsDauGhi.Add([pscustomobject]@{ nvr = $ip; goc = $goc; loi = $null })
    } catch {
        $dsDauGhi.Add([pscustomobject]@{ nvr = $ip; goc = $goc; loi = $_.Exception.InnerException.Message })
    }
}

$json = ConvertTo-Json @{ capNhat = (Get-Date -Format 'yyyy-MM-ddTHH:mm:ss'); dauGhi = $dsDauGhi.ToArray(); kenh = $dsKenh.ToArray() } -Depth 4 -Compress
[Console]::Out.Write($json)
