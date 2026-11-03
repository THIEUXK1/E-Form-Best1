# Đối chiếu entity EF (Models/**/*.cs) với bảng/cột thật trên DB prod — CHỈ SELECT INFORMATION_SCHEMA.
# Lý do: DB-first không có Migrations, deploy code có cột mới mà prod chưa chạy DDL thì trang lỗi 500 ngay.
# Exit 0 = đủ, 1 = thiếu bảng/cột (in danh sách), 2 = không kết nối được DB.
# Chuỗi kết nối lấy từ ConnectionStrings__* trong .env máy dev (đang trỏ prod 10.0.60.33); không in mật khẩu.
$ErrorActionPreference = 'Stop'
$goc = Split-Path $PSScriptRoot -Parent
$duAn = Join-Path $goc 'E-Form-Best'

# ---- 1. Gom bảng/cột từ entity: [Table("x")] trên class, [Column("y")] hoặc tên property kiểu đơn giản ----
$kieuDon = '^(int|long|short|byte|bool|decimal|double|float|string|DateTime|DateOnly|TimeOnly|TimeSpan|Guid|byte\[\])\??$'
$canCo = @{}   # bảng -> danh sách cột
# Chỉ entity có DbSet trong ITFormContext — file model mồ côi (vd CongViecOrder1) EF không đụng tới
$dbSet = @{}
foreach ($m in [regex]::Matches([IO.File]::ReadAllText((Join-Path $duAn 'Context\ITFormContext.cs')), 'DbSet<(\w+)>')) { $dbSet[$m.Groups[1].Value] = 1 }
foreach ($f in Get-ChildItem (Join-Path $duAn 'Models') -Recurse -Filter *.cs) {
    $bang = $null; $boQua = $false; $tenBang = $null
    foreach ($dong in [IO.File]::ReadAllLines($f.FullName)) {
        if ($dong -match '\[Table\("([^"]+)"') { $tenBang = $Matches[1]; continue }
        if ($tenBang -and $dong -match '\bclass\s+(\w+)') {
            if ($dbSet.ContainsKey($Matches[1])) { $bang = $tenBang; $canCo[$bang] = New-Object System.Collections.Generic.List[string] }
            $tenBang = $null; continue
        }
        if (-not $bang) { continue }
        if ($dong -match '\[NotMapped') { $boQua = $true; continue }
        if ($dong -match '\[Column\("([^"]+)"') { $cot = $Matches[1]; continue }
        if ($dong -match '^\s*public\s+(?!virtual)([\w\[\]?]+)\s+(\w+)\s*\{\s*get;') {
            # Lưu nhóm bắt trước: phép -match kế tiếp ghi đè $Matches
            $kieu = $Matches[1]; $tenProp = $Matches[2]
            if (-not $boQua -and $kieu -match $kieuDon) { $canCo[$bang].Add($(if ($cot) { $cot } else { $tenProp })) }
            $cot = $null; $boQua = $false
        }
    }
}

# ---- 2. Đọc schema prod ----
$envF = Join-Path $duAn '.env'
$dongCs = [IO.File]::ReadAllLines($envF) | Where-Object { $_ -like 'ConnectionStrings__*=*' } | Select-Object -First 1
if (-not $dongCs) { Write-Host 'SCHEMA: khong thay ConnectionStrings__* trong .env' -ForegroundColor Red; exit 2 }
$cs = $dongCs.Substring($dongCs.IndexOf('=') + 1).Trim().Trim('"')
# SqlClient của .NET Framework không hiểu dạng có dấu cách
$cs = $cs -replace '(?i)trust server certificate', 'TrustServerCertificate' -replace '(?i)multiple active result sets', 'MultipleActiveResultSets'
$thuc = @{}
try {
    $cn = New-Object System.Data.SqlClient.SqlConnection($cs); $cn.Open()
    $cmd = $cn.CreateCommand(); $cmd.CommandTimeout = 30
    $cmd.CommandText = 'SELECT TABLE_NAME, COLUMN_NAME FROM INFORMATION_SCHEMA.COLUMNS'
    $r = $cmd.ExecuteReader()
    while ($r.Read()) { $thuc[($r.GetString(0) + '|' + $r.GetString(1)).ToLower()] = 1; $thuc[$r.GetString(0).ToLower()] = 1 }
    $nguon = $cn.DataSource; $cn.Close()
} catch { Write-Host ('SCHEMA: khong ket noi duoc DB - ' + $_.Exception.Message) -ForegroundColor Red; exit 2 }

# ---- 3. So ----
$thieu = New-Object System.Collections.Generic.List[string]
foreach ($b in $canCo.Keys | Sort-Object) {
    if (-not $thuc.ContainsKey($b.ToLower())) { $thieu.Add("BANG $b"); continue }
    foreach ($c in $canCo[$b]) { if (-not $thuc.ContainsKey(($b + '|' + $c).ToLower())) { $thieu.Add("COT  $b.$c") } }
}
$soCot = ($canCo.Values | ForEach-Object { $_.Count } | Measure-Object -Sum).Sum
if ($thieu.Count -eq 0) { Write-Host "SCHEMA OK: $($canCo.Count) bang / $soCot cot deu co tren $nguon" -ForegroundColor Green; exit 0 }
Write-Host "SCHEMA THIEU tren ${nguon}: $($thieu.Count) muc (chay DDL truoc khi deploy)" -ForegroundColor Red
$thieu | ForEach-Object { Write-Host "  $_" }
exit 1
