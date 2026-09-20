# core.md — quy tắc lõi E-Form-Best (tự nạp mỗi phiên)

> File DUY NHẤT trong `.claude/rules/`. Mọi thứ dài hơn nằm ở `.claude/docs/` — xem bảng mục 9.

## 1. Dự án là gì

Hệ thống nội bộ **ASP.NET Core MVC (.NET 10), Razor server-rendered**, chạy trong mạng công ty
(nginx reverse proxy đứng trước Kestrel), phục vụ ~615 người dùng đang hoạt động. Số hoá 5 nhóm
nghiệp vụ theo Area: `ITForm`, `HRform`, `SHDForm`, `QLCongViec`, `AdminForm`.

- EF Core 10 **DB-first**, SQL Server, **một `ITFormContext` duy nhất**, **không có `Migrations/`**.
- Frontend: jQuery + thư viện trong `wwwroot/lib`. **Không SPA, không build step, không Node.**
- Không có test tự động → kiểm chứng bằng luồng thật trên trình duyệt.

## 2. Phân bổ file (rút gọn — bảng đầy đủ: `docs/architecture-workflow.md` mục 2)

| Nội dung | Nơi duy nhất |
|---|---|
| Giao diện Razor | `Areas/<Area>/Views/<Controller>/*.cshtml` |
| Layout/partial dùng chung trong Area | `Areas/<Area>/Views/Shared/` |
| Nghiệp vụ, xử lý request | `Areas/<Area>/Controllers/*Controller.cs` |
| Logic tái sử dụng, job nền, tích hợp ngoài | `Areas/<Area>/Services/` |
| Entity / truy vấn | `Models/ITForm/` + `Context/ITFormContext.cs` |
| JS tự viết | `wwwroot/js/<ten-tinh-nang>.js` (kebab-case) |
| CSS tự viết | `wwwroot/css/` |
| Cấu hình, hằng số nghiệp vụ | `appsettings.json` · secret → `.env` |

Trước khi tạo file mới: đã có file đúng vai trò chưa? thuộc ô nào? không thuộc ô nào → **hỏi**.

## 3. Cấm tuyệt đối

- ❌ Chạy DDL/DML trên CSDL (`10.0.60.33` production, `10.0.55.3` chi nhánh chỉ-đọc). Chỉ **soạn
  script** rồi để người dùng chạy. Đọc (`SELECT`) thì tự do. → `docs/database-safety.md`
- ❌ Trộn UI với nghiệp vụ: truy vấn EF / tính toán nghiệp vụ trong `.cshtml`.
- ❌ JS mới inline trong `<script>` của view.
- ❌ Tạo `DbContext` thứ hai · chuyển sang EF Migrations · thêm SPA/Node/ORM khác.
- ❌ Thêm package NuGet, đổi thứ tự middleware trong `Program.cs`, đặt route mới **dưới**
  `homeActions`, đổi cấu hình `ForwardedHeaders`.
- ❌ Hardcode mật khẩu/API key/connection string vào `.cs`, `.cshtml`, `.js`, `appsettings.json`,
  `.sql`, comment. Secret **chỉ** ở `.env` (key `Section__Key`), thêm biến mới phải thêm vào
  `.env.example`. Không phơi key backend ra client.
- ❌ Sửa/xoá bản ghi bảng `LichSu*` (append-only) · xoá cứng dữ liệu cần truy vết.
- ❌ Sửa `wwwroot/lib/`, `bin/`, `obj/` · tạo file/thư mục ngoài cấu trúc mục 2.
- ❌ Refactor ồ ạt `ITFormController` (8.4k dòng) / `HRFormController` (6k dòng); "dọn dẹp" code
  không liên quan; tự mở rộng yêu cầu; tự commit/push khi chưa được yêu cầu.

Vùng xám **phải hỏi trước**: đổi luồng duyệt phiếu, đổi phân quyền theo Bộ phận/Vai trò, đổi định
nghĩa "đơn đã hoàn tất" (`idAdmin IS NOT NULL`), chạm `SecurityStamp`/cookie, sửa `_Layout.cshtml`.
→ `docs/project-scope.md`

## 4. View & tương tác web

**View là Dumb UI.** `.cshtml` chỉ đổ dữ liệu Controller/Service đã chuẩn bị (Model/ViewBag) ra
HTML; được dùng `@Model`, vòng lặp render, `@Html.*`, `data-*`, `<script src="~/js/...">`.

**100% thao tác KHÔNG reload trang** — kể cả view cũ, không có ngoại lệ "view cũ được miễn":

- `event.preventDefault()` cho mọi submit/hành động; giao tiếp bằng `fetch()` (hoặc `$.ajax` theo
  lệ file cũ), action trả `Json(new { thanhCong, thongBao, duLieu })`, **không** trả View/Redirect.
- Cấm `location.reload()`, gán `window.location` sau khi lưu, `<form method="post">` submit thẳng,
  `RedirectToAction` sau khi lưu. `Redirect` chỉ còn hợp lệ cho: chưa đăng nhập → trang đăng nhập.
- Ngoại lệ điều hướng thật: đăng nhập/đăng xuất, mở trang chi tiết có URL bookmark được, tải file xuất.
- Cập nhật DOM **cục bộ** đúng vùng bị ảnh hưởng; event delegation trên `document` cho nội dung vẽ
  động; ưu tiên `textContent` khi chèn dữ liệu; `fetch` lỗi thì kiểm `res.ok` trước `res.json()`.
- Khoá nút + loading khi đang gửi; lỗi hiện tại chỗ, không mất dữ liệu đã nhập.
- **Chuyển form sang AJAX là mất lá chắn POST-Redirect-GET** → bắt buộc kèm chốt idempotency phía
  server (`docs/database-safety.md` mục 4).
- Chạm view nào còn submit đồng bộ → chuyển view đó sang AJAX ngay trong lần sửa đó.

## 5. Code style tóm tắt (đầy đủ: `docs/coding-standards.md`)

| Quy tắc | Ghi chú |
|---|---|
| Naming | Entity/Controller/Action: PascalCase **tiếng Việt không dấu** (`KkThietBi`, `DonMail`); biến local camelCase; file JS kebab-case |
| SQL mapping | Luôn khai `[Table("KK_ThietBi")]` / `[Column("id_thiet_bi")]` tường minh; cột snake_case |
| Routing | **Attribute tuyệt đối** `[HttpGet("/FormIT/ChiTiet/{id}")]`; route mới đặt **trên** `homeActions` |
| EF | `async`/`await` mọi truy vấn; `.Select(...)` đúng cột; tránh N+1; truy vấn phải dịch được sang SQL |
| Raw SQL | **Chỉ tham số hoá** (`SqlParameter`), không nối chuỗi giá trị người dùng |
| Soft delete | Chỉ `KkThietBi` có `NgayXoa` — mọi truy vấn chạm bảng này **phải lọc `NgayXoa == null`** |
| Phân quyền | Kiểm ở **tầng server**; ẩn nút trên UI không phải phân quyền; coi chừng IDOR với `id` từ URL |
| DI | Code mới nhận `ITFormContext` qua constructor injection; `new ITFormContext()` là nợ kỹ thuật |
| Lỗi & dữ liệu rỗng | Không nuốt exception im lặng; trả JSON `thanhCong=false` + thông báo; danh sách rỗng phải có trạng thái trống rõ ràng |
| Comment | Tiếng Việt **có dấu**, giải thích *tại sao*; mật độ bám file xung quanh |
| Ngày giờ | `datetime`, giờ máy chủ VN; không trộn `DateTimeOffset` |

## 6. Zero-fluff — cách trả lời

- Trả lời trực diện: không chào hỏi, không mở bài, không tóm tắt lại yêu cầu, không khen.
- Chỉ xuất **đoạn đã thay đổi** kèm `file:line`; không in lại code không đổi; file đã sửa bằng công
  cụ edit thì không dán lại nội dung.
- Không tự thêm doc/changelog/format lại file/"dọn dẹp" nếu không được yêu cầu.
- Báo đúng sự thật: cái gì đã build/chạy thử, cái gì **chưa kiểm** thì ghi "chưa kiểm".
- Độ dài bám việc: sửa một dòng → trả lời vài dòng.

## 7. Output clamping — bắt buộc kẹp output

- Build: `dotnet build -v quiet --nologo | grep -E "error|Error" | head -n 20`.
- `grep`/`find`/`ls`/`cat` **luôn** `| head -n 20`; log dài dùng `tail -n 30`, không `cat` cả file.
- Git: `git status --short | head -n 20`, `git log --oneline -10`, `git diff --stat` trước khi diff
  từng file.
- Chỉ chạy lại verbose khi thất bại, và chỉ đúng phần lỗi. Cần xem thêm thì tăng dần (`head -n 50`),
  không bỏ pipe.

## 8. Hot reload

- Dev server chạy **nền**: `cd E-Form-Best && dotnet watch run` (không bao giờ `dotnet run` trần),
  ghi output ra `watch_log.txt` / `watch_err.txt`, đọc lại bằng `tail -n 30` — không stream log vào
  phiên. `dotnet watch` .NET 10 hay tự chết khi hot-reload → bọc supervisor (`watch-supervisor.ps1`).
- `dotnet watch` không có cờ giữ lịch sử màn hình → **bù bằng ghi log ra file**. Watcher tự clear
  màn hình sẽ nuốt log lỗi của lần nạp trước.
- Sửa giao diện/cấu hình mà chưa thấy đổi → **chủ động restart ngay**, không hỏi. Đặc biệt: thêm cột
  vào entity EF thì hot reload **không** ăn, bắt buộc restart.
- Không kéo build step mới (Vite/Node) vào chỉ để có hot reload.

## 9. Lazy-load — task nào mở file nào

| Task đang làm | Đọc |
|---|---|
| Chạm schema, viết DDL, nhập liệu hàng loạt, truy vấn `KkThietBi` | `docs/database-safety.md` |
| Sửa `.cshtml`/JS/layout, chuyển form sang AJAX | `docs/architecture-workflow.md` mục 5 |
| Tạo file mới, đặt tên, chọn thư mục, git flow, commit | `docs/architecture-workflow.md` mục 2–3 |
| Nghi việc được giao vượt phạm vi / đổi stack / thêm package | `docs/project-scope.md` |
| Viết C#/Razor/JS thông thường, naming, design pattern | `docs/coding-standards.md` |
| Chạm `Program.cs`, package, routing, gặp lỗi lạ khi build/chạy | `docs/dotnet-architecture.md` (mục 4: cạm bẫy đã cắn) |
| Đang vướng gì, đã quyết gì | `plans/00-context-memory.md` · cũ hơn: `plans/decision-log.md` |
| Lộ trình theo phase | `plans/00-master-plan.md`, `plans/phase*.md` |

Một task chạm mấy miền thì đọc bấy nhiêu file. Đọc rồi thì không đọc lại trong cùng phiên.

## 10. Subagent (`~/.claude/agents/`)

| Agent | Gọi khi |
|---|---|
| `system-architect` | Thẩm định thiết kế, đề xuất đổi kiến trúc/stack, tách Service |
| `database-auditor` | Rà script DDL/migration, soft-delete, idempotency, audit log |
| `code-reviewer` | Review bảo mật/chất lượng trước khi coi một thay đổi là xong |

Cả ba **chỉ đọc & đề xuất**, không tự sửa code/dữ liệu. Chỉ gọi khi người dùng yêu cầu.
