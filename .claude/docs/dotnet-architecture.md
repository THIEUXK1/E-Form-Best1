# Kiến trúc .NET — E-Form-Best

ASP.NET Core MVC monolith trên **.NET 10**, chia theo **Areas**. Một CSDL SQL Server, một `DbContext`.
Quy tắc phân bổ file & git flow: [`architecture-workflow.md`](architecture-workflow.md).

## 1. Cây thư mục

```
E-Form-Best/
├─ Program.cs              ← DI, middleware pipeline, route. Sửa cẩn thận, thứ tự có chủ đích
├─ appsettings.json        ← config + hằng số nghiệp vụ (vd BanQuyenWindows:MakKeyCongTy)
├─ .env / .env.example     ← secret (connection string…), nạp trước CreateBuilder
├─ Context/ITFormContext.cs← DbContext DUY NHẤT. Thêm bảng = thêm DbSet ở đây
├─ Models/ITForm/          ← entity EF (DB-first). MỘT file = MỘT bảng
├─ Areas/<Ten>/            ← ITForm · HRform · SHDForm · QLCongViec · AdminForm
│  ├─ Controllers/         ← business logic + action
│  ├─ Services/            ← background worker / logic tái sử dụng (hiện chỉ ITForm có)
│  └─ Views/
│     ├─ <Controller>/     ← view của Area
│     └─ Shared/_Layout.cshtml  ← layout riêng từng Area (5 layout độc lập)
└─ wwwroot/
   ├─ js/, css/            ← JS/CSS tự viết
   ├─ lib/                 ← thư viện bên thứ ba — KHÔNG sửa tay
   ├─ FileIT/, HuongDanIT/ ← file người dùng upload / tài liệu
   └─ sw.js                ← service worker (Web Push)
```

## 2. Phụ thuộc — mỗi gói một lý do

| Gói | Vì sao có mặt |
|---|---|
| `Microsoft.EntityFrameworkCore` + `.SqlServer` (10.0.2) | ORM chính, DB-first tới SQL Server `10.0.60.33` |
| `Microsoft.EntityFrameworkCore.Design` / `.Tools` | Chỉ để **scaffold** entity từ DB khi schema đổi. **Không** dùng cho Migrations |
| `Microsoft.AspNetCore.Mvc.Razor.RuntimeCompilation` | Sửa `.cshtml` thấy ngay khi F5 — bật **chỉ ở Development** |
| `ClosedXML` (0.105.0) | Xuất/nhập Excel (biên bản tài sản, nhập bản quyền, báo cáo) |
| `System.Management` + `System.Diagnostics.PerformanceCounter` | Thu thập cấu hình máy (TSCN) qua WMI — **chỉ chạy được trên Windows** |
| `System.DirectoryServices.AccountManagement` | Xác thực / tra cứu tài khoản Active Directory |
| `WebPush` (1.0.12) | Web Push notification, cặp khoá VAPID (khoá riêng ở `.env`, chỉ public key ra client) |
| `Newtonsoft.Json` | Còn dùng ở vài chỗ cũ; code mới ưu tiên `System.Text.Json` |

Thêm gói mới phải hỏi trước — xem [`project-scope.md`](project-scope.md).

## 3. Quy ước bắt buộc

- **Attribute routing tuyệt đối**: `[HttpGet("/FormIT/ChiTiet/{id}")]`. URL nghiệp vụ không khớp
  `{area}/{controller}/{action}`.
- Route convention trong `Program.cs` chỉ là fallback; `homeActions` bắt `{action}/{id?}` rất "tham"
  → **route mới phải đặt trên nó**.
- Một `ITFormContext` cho cả 5 Area; thêm bảng = thêm `DbSet` vào đúng file đó.
- Mỗi Area một `_Layout.cshtml` riêng — sửa layout nhớ kiểm cả 5.
- Action AJAX trả `Json(...)`; `RedirectToAction` chỉ còn cho luồng chưa đăng nhập.
- `IHostedService` cho job nền (`AutoRatingWorker`, chạy 0h) — phải idempotent khi app restart.
- Cookie Auth + `SecurityStamp`: đổi stamp trong DB = đá toàn bộ phiên của user đó.

## 4. Cạm bẫy đã thực sự cắn

| Cạm bẫy | Biểu hiện | Cách tránh |
|---|---|---|
| Hot reload **không** cập nhật model EF | Thêm cột vào entity rồi hot reload → EF vẫn ghi theo model cũ, không báo lỗi | **Restart app** sau mỗi lần đổi entity, không đoán |
| PowerShell 5.1 ghép code tiếng Việt | `Get-Content` đọc UTF-8 không BOM như ANSI → `"Cài đặt phần mềm"` thành `"C脿i 膽岷穞…"`, build 0 lỗi nhưng lọc sai lúc chạy | `[System.IO.File]::ReadAllText/WriteAllLines` với `UTF8Encoding` tường minh; quét lại `[一-鿿]` sau khi ghép |
| `sqlcmd` với file `.sql` tiếng Việt | Dữ liệu vào DB lỗi font | Luôn thêm `-f 65001` |
| Truy vấn EF không dịch được sang SQL | Nổ **runtime**, build vẫn sạch (đã dính ở `355993a`) | Tránh hàm .NET lạ trong `Where`; cẩn thận `Contains` trên list ngoài |
| `IMemoryCache` cho dropdown Công ty/Bộ phận | Sửa danh mục **không hiện ngay** | Chờ cache hết hạn hoặc invalidate tường minh |
| Cache file tĩnh 7 ngày | Sửa `.js`/`.css` cũ mà trình duyệt vẫn dùng bản cũ | Thêm query version hoặc đổi tên file |
| `UseForwardedHeaders` đã xoá `KnownProxies` | An toàn **chỉ khi** Kestrel không mở thẳng ra Internet | Giữ nguyên nginx đứng trước; không đổi cấu hình này |
| `dotnet watch` .NET 10 tự chết khi hot-reload | App tắt im lặng, tưởng vẫn chạy | Bọc `watch-supervisor.ps1`, log ra `watch_log.txt` |
| Quên lọc soft-delete `KkThietBi` | Thiết bị đã xoá hiện lại trong danh sách/thống kê/export | Mọi truy vấn chạm bảng này lọc `NgayXoa == null` — [`database-safety.md`](database-safety.md) |

## 5. Lệnh hay dùng

Luôn kẹp output theo [`../rules/core.md`](../rules/core.md) mục 7.

```bash
dotnet build -v quiet --nologo | grep -E "error|Error" | head -n 20
dotnet restore --nologo -v quiet
cd E-Form-Best && dotnet watch run          # chạy NỀN, log ra watch_log.txt
tail -n 30 watch_log.txt
grep -E "error|fail|Exception" watch_log.txt | tail -n 20
```

Health check: `/health/ready` (chưa có uptime monitor nào gọi — blocker B3).
