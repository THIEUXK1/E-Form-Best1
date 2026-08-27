# Phase 0 — Khung dự án & CSDL

Mục tiêu: **ai cũng chạy được dự án và hiểu đúng hợp đồng schema** trước khi viết tính năng.
Phase này phần lớn **đã xong** (dự án đang chạy production); phần chưa xong ghi rõ ở mục 4.

## 1. Yêu cầu môi trường

| Thành phần | Yêu cầu |
|---|---|
| .NET SDK | 10.x (`<TargetFramework>net10.0</TargetFramework>`) |
| SQL Server | Truy cập được `10.0.60.33` (chính) và `10.0.55.3` (chi nhánh, chỉ đọc để đồng bộ) |
| OS | Windows — bắt buộc: dùng `System.DirectoryServices.AccountManagement` (xác thực AD), `System.Management` (WMI quét cấu hình máy) |
| Mạng | Nằm trong mạng nội bộ hoặc VPN mới nối được tới 2 IP SQL trên |

## 2. Chạy dự án

```bash
cd E-Form-Best && dotnet watch run
```

- Luôn dùng `dotnet watch run`, **không** `dotnet run` trần — môi trường Dev phải **auto hot reload**
  (đổi `.cs` → rebuild/áp nóng, đổi `.cshtml` → RuntimeCompilation áp ngay).
- Chạy ở **nền**, ghi ra `watch_log.txt` / `watch_err.txt` để **không mất lịch sử terminal**
  (tương đương cờ `--clearScreen false` của Vite; `dotnet watch` không có cờ này).
- **Bọc supervisor** (vòng lặp chạy lại khi tiến trình chết) — SDK .NET 10 có lúc làm
  `dotnet watch` tự thoát sau một rude edit.
- Chi tiết cờ thu gọn output: [`../rules/coding-standards.md`](../rules/coding-standards.md) mục 6.
- Development: `https://localhost:7200` / `http://localhost:5200`; RuntimeCompilation bật nên
  sửa `.cshtml` chỉ cần F5.
- Kiểm tra nhanh: `GET /health` (app sống), `GET /health/ready` (nối được SQL Server).

## 3. Khung đã dựng sẵn (đọc trước khi sửa `Program.cs`)

Thứ tự middleware trong `E-Form-Best/Program.cs` là **có chủ đích**, đừng đảo:

```
UseForwardedHeaders  →  ExceptionHandler/HSTS (non-Dev)  →  UseHttpsRedirection
→  UseStaticFiles (Cache-Control 7 ngày, riêng sw.js no-cache)
→  UseRouting  →  UseAuthentication  →  UseAuthorization  →  UseSession
→  /health, /health/ready  →  route "area"  →  route "default"  →  route "homeActions"
```

- `UseForwardedHeaders` **phải đứng đầu**, nếu không Cookie Auth và HttpsRedirection nhìn sai scheme.
- Route `homeActions` (`{action}/{id?}`) rất "tham" — luôn đặt **cuối cùng**, thêm route mới phải
  chèn *trên* nó.
- Đăng ký sẵn: `ITFormContext` (DI), `AutoRatingWorker` (`IHostedService`, chạy 0h), `IMemoryCache`,
  Cookie Auth 365 ngày + kiểm `SecurityStamp`, Session timeout 3h.

## 4. CSDL — hợp đồng và việc còn lại

**Mô hình: DB-first, KHÔNG có `Migrations/`.** Chi tiết quy trình đổi schema:
[`../rules/database-safety.md`](../rules/database-safety.md).

Quy ước entity (bắt buộc theo, xem `Models/ITForm/KkThietBi.cs` làm mẫu):
- `namespace E_Form_Best.Models.ITForm;`, `public partial class`
- `[Table("ten_bang_that")]`, `[Key]`, `[Column("ten_cot_that")]`, `[StringLength(n)]`,
  `[Column(TypeName = "datetime")]` cho cột ngày
- Tên property PascalCase (`IdThietBi`), tên cột SQL snake_case (`id_thiet_bi`) —
  **luôn khai báo `[Column]` tường minh**, không dựa vào convention.

Việc **còn lại** của Phase 0:

- [ ] **B2** — Tạo tài khoản SQL riêng quyền tối thiểu thay `sa`; đưa connection string ra biến môi
      trường / user-secrets, bỏ khỏi `appsettings.json` đã commit. *(Lưu ý: `ITFormContext.OnConfiguring`
      hiện đọc thẳng `appsettings.json` vì các controller `new ITFormContext()` ngoài DI — sửa chỗ này
      phải xử lý cả đường đó.)*
- [ ] **B3** — Trỏ uptime check vào `/health/ready`.
- [ ] Thiết lập backup định kỳ + **restore test** cho CSDL production (~1 GB).
- [ ] Bổ sung `.gitignore` cho `watch_log.txt`, `dotnet-watch*.log` ở gốc repo.
