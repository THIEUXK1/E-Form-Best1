# Phạm vi dự án — ranh giới Do's / Don'ts

## 1. Dự án này LÀ gì

Hệ thống nội bộ, chạy trong mạng công ty (hoặc VPN), phục vụ **~615 người dùng thực sự hoạt động**.
Số hoá 5 nhóm nghiệp vụ: phiếu IT, phiếu Nhân sự, phiếu Xuất Nhập Khẩu/An toàn, giao việc nội bộ,
và kiểm kê tài sản CNTT. Chi tiết: [`../plans/phase1-core-features.md`](../plans/phase1-core-features.md).

## 2. Dự án này KHÔNG phải

- Không phải sản phẩm public trên Internet (Kestrel không mở trực tiếp ra ngoài — nginx đứng trước).
- Không phải microservice / đa container — **một mã nguồn, một CSDL**.
- Không phải SPA — Razor server-rendered, không có build step frontend.
- Không phải hệ thống đa ngôn ngữ hoá đầy đủ (chỉ vài mẫu xuất song ngữ Việt–Trung riêng lẻ).

## 3. DO — được làm, khuyến khích

- ✅ Thêm loại phiếu/đơn mới theo đúng khuôn `Form* + chi tiết + BinhLuan + LichSu`
  (checklist ở [`../plans/phase1-core-features.md`](../plans/phase1-core-features.md)).
- ✅ Mở rộng module Kiểm kê (`KK_*`) và Thu thập cấu hình máy (`TSCN_*`).
- ✅ Thêm báo cáo/thống kê, xuất Excel/Word/PDF (ClosedXML đã có sẵn).
- ✅ Tối ưu hiệu năng: cache dữ liệu tra cứu, giảm N+1, chọn đúng cột.
- ✅ Tách dần logic khổng lồ trong controller sang `Areas/<Area>/Services/` **khi đang sửa module đó**.
- ✅ Tách dần JS inline trong view sang `wwwroot/js/` **khi đang sửa view đó**.
- ✅ Sửa lỗi bảo mật, dọn view/route chết, bổ sung ghi log nghiệp vụ.

## 4. DON'T — không làm nếu chưa được người dùng đồng ý rõ ràng

### Stack & kiến trúc
- ❌ Đổi framework/ORM/CSDL. Không thêm React/Vue/Angular, không thêm Node build step,
  không đổi sang Dapper/PostgreSQL, không tách microservice, không container hoá.
- ❌ Thêm `DbContext` thứ hai, hoặc chuyển sang EF Migrations
  (xem [`database-safety.md`](database-safety.md)).
- ❌ Thêm package NuGet mới chỉ để tiện — hỏi trước, mỗi package là gánh nặng bảo trì lâu dài.
- ❌ Đảo thứ tự middleware trong `Program.cs`, hoặc đưa route mới xuống dưới `homeActions`.
- ❌ Đổi cấu hình `ForwardedHeaders` (đang cố ý xoá `KnownProxies` vì nginx ở máy khác).

### Dữ liệu
- ❌ Tự chạy DDL/DML trên production hoặc trên chi nhánh `10.0.55.3`.
- ❌ Ghi ngược lên `10.0.55.3` — quan hệ là đồng bộ **một chiều, chỉ đọc**.
- ❌ Xoá cứng dữ liệu nghiệp vụ cần truy vết.
- ❌ Sửa/xoá bản ghi trong các bảng `LichSu*`.

### Refactor
- ❌ Refactor ồ ạt toàn bộ `ITFormController` (8.4k dòng) / `HRFormController` (6k dòng) trong một lần.
  Dự án **không có test tự động** — refactor lớn không có lưới an toàn. Chỉ tách theo từng module
  đang thực sự sửa.
- ❌ Đổi tên bảng/cột SQL, đổi URL route đang chạy, đổi tên entity — người dùng và bookmark đang phụ thuộc.
- ❌ "Dọn dẹp" code không liên quan tới việc đang làm.

### Phạm vi công việc
- ❌ Tự mở rộng yêu cầu. Làm đúng việc được giao; thấy vấn đề khác thì **báo**, không tự sửa luôn.
- ❌ Tự commit/push khi chưa được yêu cầu.
- ❌ Tạo file/thư mục ngoài cấu trúc chuẩn
  (xem [`architecture-workflow.md`](architecture-workflow.md) mục 2).

## 5. Ranh giới xám — luôn hỏi trước

| Tình huống | Vì sao phải hỏi |
|---|---|
| Thay đổi luồng duyệt của một loại phiếu | Ảnh hưởng trực tiếp quy trình đang chạy của cả phòng ban |
| Đổi quy tắc phân quyền theo Bộ phận/Vai trò | Có thể mở nhầm quyền xem dữ liệu người khác |
| Đổi định nghĩa "đơn đã hoàn tất" (`idAdmin IS NOT NULL`) | Toàn bộ báo cáo/thống kê đang dựa vào nó |
| Đổi quy ước bản quyền Windows | Đó là quy định nội bộ công ty, không phải lựa chọn kỹ thuật. Thêm key MAK chỉ sửa `appsettings.json` |
| Chạm `SecurityStamp` / cấu hình cookie | Sai một chút là đá toàn bộ người dùng ra khỏi hệ thống |
| Sửa `_Layout.cshtml` | Mỗi Area một layout riêng — dễ sửa một nơi quên bốn nơi còn lại |
