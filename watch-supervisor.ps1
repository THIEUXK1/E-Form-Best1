$ErrorActionPreference = "Continue"
Set-Location "C:\laragon\www\E-Form-Best1\E-Form-Best"

# --no-launch-profile bỏ qua launchSettings.json, nên ASPNETCORE_ENVIRONMENT không được set
# -> app chạy ở Production -> Program.cs BỎ QUA AddRazorRuntimeCompilation -> sửa .cshtml xong
# F5 vẫn ra bản cũ (phải rebuild mới thấy). Đặt tường minh ở đây cho đúng môi trường dev.
$env:ASPNETCORE_ENVIRONMENT = "Development"
# Máy dev trỏ DB thật nhưng không thông mọi dải (vd 10.0.63 BPVN) -> job quét IP ở đây ghi DOWN giả.
# Việc quét để 2 máy chủ làm; biến môi trường được ưu tiên hơn .env.
$env:QuetIp__Bat = "false"
# Tương tự cho job ping Switch: máy dev không ping được 10.0.63/10.0.5 (BPVN) nên ghi DOWN, máy chủ 60.52 ghi UP
# -> 13/10/2026 KK_SwitchLichSu BPVN lật UP/DOWN mỗi phút (112 switch). Để máy chủ theo dõi.
$env:Switch__GhiLichSu = "false"

while ($true) {
    & dotnet watch run --non-interactive --no-launch-profile --urls http://localhost:5274 *>> "C:\laragon\www\E-Form-Best1\watch_log.txt"
    Start-Sleep -Seconds 3
}
