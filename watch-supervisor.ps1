$ErrorActionPreference = "Continue"
Set-Location "C:\laragon\www\E-Form-Best1\E-Form-Best"

# --no-launch-profile bỏ qua launchSettings.json, nên ASPNETCORE_ENVIRONMENT không được set
# -> app chạy ở Production -> Program.cs BỎ QUA AddRazorRuntimeCompilation -> sửa .cshtml xong
# F5 vẫn ra bản cũ (phải rebuild mới thấy). Đặt tường minh ở đây cho đúng môi trường dev.
$env:ASPNETCORE_ENVIRONMENT = "Development"

while ($true) {
    & dotnet watch run --non-interactive --no-launch-profile --urls http://localhost:5274 *>> "C:\laragon\www\E-Form-Best1\watch_log.txt"
    Start-Sleep -Seconds 3
}
