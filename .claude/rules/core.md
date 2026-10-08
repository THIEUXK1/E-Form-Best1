# core.md — E-Form-Best (tự nạp; quy tắc chung ở `~/.claude/CLAUDE.md`)

## 1. Dự án
- ASP.NET Core MVC .NET 10, Razor server-rendered, nội bộ (nginx → Kestrel), ~615 user.
- Area: `ITForm`, `HRform`, `SHDForm`, `QLCongViec`, `AdminForm`.
- EF Core 10 DB-first, SQL Server, MỘT `ITFormContext`, không `Migrations/`.
- jQuery + `wwwroot/lib`; không SPA/Node/build step. Không test tự động → kiểm trên trình duyệt.

## 2. Phân bổ file (đầy đủ: `docs/architecture-workflow.md` mục 2)
| Nội dung | Nơi |
|---|---|
| View · partial | `Areas/<A>/Views/<Ctrl>/` · `Areas/<A>/Views/Shared/` |
| Xử lý request | `Areas/<A>/Controllers/` |
| Logic tái dùng, job nền, tích hợp ngoài | `Areas/<A>/Services/` |
| Entity · truy vấn | `Models/ITForm/` + `Context/ITFormContext.cs` |
| JS · CSS | `wwwroot/js/<kebab>.js` · `wwwroot/css/` |
| Cấu hình · secret | `appsettings.json` · `.env` (`Section__Key`, thêm key vào `.env.example`) |
- Không thuộc ô nào → hỏi.

## 3. Cấm
- DDL/DML trên DB (`10.0.60.33` prod, `10.0.55.3` chỉ-đọc) — chỉ soạn script; SELECT tự do.
- Query EF/nghiệp vụ trong `.cshtml`; JS inline mới trong view.
- DbContext thứ hai · EF Migrations · SPA/Node/ORM khác · thêm NuGet.
- Đổi thứ tự middleware `Program.cs`, đổi `ForwardedHeaders`, route mới dưới `homeActions`.
- Hardcode secret (`.cs/.cshtml/.js/appsettings.json/.sql`/comment); phơi key backend ra client.
- Sửa/xoá `LichSu*` (append-only); xoá cứng dữ liệu cần truy vết.
- Sửa `wwwroot/lib/`, `bin/`, `obj/`; tạo file ngoài mục 2.
- Refactor ồ ạt `ITFormController` (8.4k dòng) / `HRFormController` (6k); dọn code không liên quan; tự commit/push.
- Hỏi trước: luồng duyệt phiếu · phân quyền Bộ phận/Vai trò · định nghĩa hoàn tất (`idAdmin IS NOT NULL`) ·
  `SecurityStamp`/cookie · `_Layout.cshtml`. → `docs/project-scope.md`

## 4. View & AJAX (chi tiết: `docs/architecture-workflow.md` mục 5)
- View = Dumb UI: `@Model`/ViewBag, vòng lặp render, `@Html.*`, `data-*`, `<script src="~/js/...">`.
- 100% không reload, kể cả view cũ: `preventDefault` + `fetch`/`$.ajax`; trả `Json(new { thanhCong, thongBao, duLieu })`.
- Cấm `location.reload()`, gán `window.location` sau lưu, form POST đồng bộ, `RedirectToAction` sau lưu.
- Điều hướng thật chỉ: login/logout, chưa đăng nhập, trang chi tiết bookmark được, tải file.
- DOM cục bộ; delegation trên `document`; `textContent`; kiểm `res.ok` trước `res.json()`.
- Khoá nút + loading; lỗi hiện tại chỗ, giữ dữ liệu đã nhập.
- AJAX hoá form → bắt buộc chốt idempotency server (`docs/database-safety.md` mục 4).
- Chạm view còn submit đồng bộ → chuyển AJAX ngay lần sửa đó.

## 5. Code style (đầy đủ: `docs/coding-standards.md`)
- Entity/Controller/Action PascalCase tiếng Việt không dấu (`KkThietBi`); local camelCase; JS kebab-case.
- Mapping tường minh `[Table("KK_ThietBi")]` / `[Column("id_thiet_bi")]`, cột snake_case.
- Route attribute tuyệt đối `[HttpGet("/FormIT/ChiTiet/{id}")]`, đặt trên `homeActions`.
- EF async, `.Select` đúng cột, tránh N+1, dịch được sang SQL. Raw SQL chỉ `SqlParameter`.
- `KkThietBi` xoá mềm → mọi truy vấn lọc `NgayXoa == null`.
- Phân quyền ở server; chống IDOR với `id` từ URL.
- Code mới inject `ITFormContext` qua constructor (`new ITFormContext()` là nợ).
- Không nuốt exception → `thanhCong=false` + thông báo; danh sách rỗng có trạng thái trống.
- Comment tiếng Việt có dấu, nói tại sao. Ngày giờ `datetime` giờ VN, không `DateTimeOffset`.

## 6. Build & hot reload
- Build: `dotnet build -v q -clp:ErrorsOnly --nologo`.
- Dev: `dotnet watch run` trong `E-Form-Best/`, chạy NỀN qua `./watch-supervisor.ps1` (watch .NET 10 hay tự chết);
  log `watch_log.txt` / `watch_err.txt`. Không `dotnet run` trần.
- Thêm cột entity EF → bắt buộc restart (hot reload không ăn).

## 7. Task → đọc (`.claude/`)
| Task | Đọc |
|---|---|
| Schema, DDL, nhập liệu hàng loạt, `KkThietBi` | `docs/database-safety.md` |
| `.cshtml`/JS/layout, chuyển AJAX | `docs/architecture-workflow.md` mục 5 |
| File mới, đặt tên, git flow, commit | `docs/architecture-workflow.md` mục 2–3 |
| Vượt phạm vi, đổi stack, thêm package | `docs/project-scope.md` |
| C#/Razor/JS, naming, pattern | `docs/coding-standards.md` |
| `Program.cs`, routing, lỗi lạ build/chạy, deploy | `docs/dotnet-architecture.md` |
| Đầu task: việc dở, quyết định gần | `plans/00-context-memory.md` · cũ: `plans/decision-log.md` |
| Lộ trình | `plans/00-master-plan.md`, `plans/phase*.md` |

## 8. Subagent (`~/.claude/agents/`, chỉ đọc & đề xuất)
- `system-architect` kiến trúc/stack · `database-auditor` script DDL, xoá mềm, idempotency · `code-reviewer` bảo mật/chất lượng.
