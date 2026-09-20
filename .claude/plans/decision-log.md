# Decision Log — lịch sử quyết định cũ

> Chỉ tra khi cần biết "vì sao hồi đó làm thế". **5 quyết định gần nhất** nằm ở
> [`00-context-memory.md`](00-context-memory.md); quá 5 dòng thì đẩy xuống đây.
> Mỗi dòng: **ngày — quyết định — lý do — hệ quả**, ngày tuyệt đối (dd/mm/yyyy).

| Ngày | Quyết định | Lý do | Hệ quả |
|---|---|---|---|
| — (từ trước) | Dùng **EF Core DB-first**, không dùng `Migrations/` | Schema có sẵn/được quản trên SQL Server; nhiều bảng dùng chung với hệ thống khác | Mọi thay đổi schema là DDL thủ công + sửa model tay. Không chạy `dotnet ef migrations` |
| — (từ trước) | Một `ITFormContext` duy nhất cho cả 5 Area | Chung một CSDL, tránh trùng entity | Context rất lớn; thêm bảng = thêm `DbSet` vào đúng file này |
| — (từ trước) | Route bằng **attribute tuyệt đối** (`[HttpGet("/FormIT/...")]`) thay vì convention | URL nghiệp vụ không khớp `{area}/{controller}/{action}` | Đổi URL = sửa attribute; route convention trong `Program.cs` chỉ là fallback |
| 22/07/2026 (`8cdfa54`) | Cache dropdown Công ty/Bộ phận bằng `IMemoryCache`; cache static file 7 ngày, riêng `sw.js` `no-cache` | Giảm truy vấn lặp và tải lại file tĩnh | Sửa danh mục Công ty/Bộ phận **không hiện ngay** — phải chờ cache hết hạn hoặc invalidate |
| (`f56705c`) | Bật `UseForwardedHeaders`, xoá `KnownProxies/KnownIPNetworks` | nginx nằm máy khác nên không thuộc loopback tin cậy | An toàn **chỉ khi** Kestrel không mở trực tiếp ra Internet — giữ nguyên ràng buộc này |
| (`f56705c`) | Quy ước bản quyền Windows: chỉ MAK công ty + OEM là Đạt chuẩn; GVLK/Retail generic không tính | Quy định nội bộ | Mua key MAK mới → thêm 5 ký tự cuối vào `BanQuyenWindows:MakKeyCongTy` trong `appsettings.json`, **không sửa code** |
| (`599ba5f`) | Bật Razor RuntimeCompilation **chỉ ở Development** | Sửa `.cshtml` thấy ngay khi F5 | Production vẫn phải build lại khi đổi view |
| 27/08/2026 | Thêm đơn số 9 **Thiết kế tem in** (`IT_ThietKeTemIn_9`), phụ trách V200887 (Nguyễn Văn Phúc, `CongViecIT.id=1026`) | Nghiệp vụ mới của IT; bám đúng khuôn `FormIt + chi tiết + LichSu + NguoiHoTro` | Đổi người phụ trách = sửa dòng `CongViecIT` có `Ten = N'Thiết kế tem in'`, **không sửa code**. Đây là đơn đầu tiên đạt chuẩn không-reload ngay từ đầu |
| 27/08/2026 | Cho phép Claude chạy `sqlcmd` qua `.claude/settings.local.json` (không commit) | Chạy DDL đã duyệt mà không bị auto mode chặn | Quyền này là **toàn quyền ghi** trên CSDL bằng tài khoản `sa` — gắn liền với blocker B2. Gỡ quyền = xoá file đó |
| 27/08/2026 | Thêm đơn số 10 **Cài đặt phần mềm** (`IT_CaiDatPhanMem_10`), bộ trường gọn 8 cột, phụ trách V200887 (`CongViecIT.id=1027`) | Nghiệp vụ IT còn thiếu; chọn bộ gọn để phát hành nhanh, phần bản quyền/nguồn cài gộp vào ô Ghi chú | Muốn tách riêng bản quyền/nguồn cài về sau thì phải `ALTER TABLE` thêm cột, không sửa được bằng cấu hình |
