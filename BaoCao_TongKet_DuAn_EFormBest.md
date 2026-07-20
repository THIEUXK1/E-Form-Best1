# BÁO CÁO TỔNG KẾT DỰ ÁN CÔNG NGHỆ — PHÂN TÍCH GIÁ TRỊ DOANH NGHIỆP
## Hệ thống: E-Form-Best (Nền tảng quản lý phiếu/đơn nội bộ đa phân hệ)

**Kỳ báo cáo:** 01/01/2026 – 15/07/2026

---

## PHẠM VI VÀ PHƯƠNG PHÁP LUẬN

Báo cáo này phân tích dựa trên dữ liệu thực tế thu thập trực tiếp từ mã nguồn và hệ thống đang chạy, phân biệt rõ hai loại số liệu:

1. **Số liệu xác minh được (verifiable):** Lấy trực tiếp từ `git log` (lịch sử phát triển) và từ truy vấn thực tế trên cơ sở dữ liệu sản xuất (qua API nội bộ của ứng dụng, không dùng quyền truy cập trực tiếp cấp quản trị CSDL).
2. **Số liệu không xác minh được trong phạm vi phiên làm việc này:** Các chỉ số hạ tầng vận hành (uptime máy chủ, tải CPU/RAM, lịch sử incident) — vì hệ thống không triển khai theo kiến trúc container (Docker) mà chạy dạng ứng dụng ASP.NET Core lưu trú trực tiếp trên máy chủ Windows nội bộ, và phiên làm việc này không có quyền truy cập vào máy chủ production thực tế để đo các chỉ số đó.

Khác với các dự án nhiều container độc lập, **E-Form-Best là một hệ thống hợp nhất duy nhất** (1 mã nguồn, 1 cơ sở dữ liệu SQL Server) được tổ chức thành **5 phân hệ nghiệp vụ** theo mô hình Areas của ASP.NET Core MVC, nên báo cáo trình bày theo phân hệ thay vì theo dự án/container riêng biệt.

Nguồn số liệu:
- `git log --since="2026-01-01" --until="2026-07-15"` thực thi trên repository tại `C:\laragon\www\E-Form-Best1`
- Truy vấn qua endpoint nội bộ `/AdminForm/ThongKeHeThong` (mới bổ sung riêng cho báo cáo này, đọc trực tiếp từ SQL Server qua Entity Framework) thực thi ngày 15/07/2026, 08:06

---

## TÓM TẮT CHIẾN LƯỢC

Trong giai đoạn từ 20/01/2026 đến 14/07/2026 (~6 tháng), đội ngũ phát triển đã tập trung xây dựng và hoàn thiện 5 phân hệ nghiệp vụ cốt lõi phục vụ vận hành nội bộ doanh nghiệp: quản lý phiếu yêu cầu IT, quản lý phiếu yêu cầu Nhân sự (HR), quản lý phiếu Xuất Nhập Khẩu/An toàn (SHD), giao việc chỉ định nội bộ (QLCongViec), và kiểm kê tài sản/thiết bị công nghệ. Các giải pháp tập trung vào số hóa quy trình phê duyệt nhiều cấp, tự động hóa thu thập dữ liệu tài sản, và xây dựng hệ thống báo cáo/thống kê tập trung.

Tổng cộng **289 commit** phát triển đã được thực hiện trong kỳ, tương ứng với **217,213 dòng code thêm mới** và **60,461 dòng code bị xóa/thay thế**, trải rộng trên **1,400 lượt thay đổi tệp** (tính gộp qua các commit).

---

## THỐNG KÊ TỔNG QUAN — SỐ LIỆU THỰC TẾ

### 1. Bảng tổng hợp hoạt động phát triển (verifiable — từ `git log`)

| Chỉ số | Giá trị | Ghi chú |
|---|---|---|
| Tổng số commit trong kỳ | **289** | 20/01/2026 → 14/07/2026 |
| Số ngày có hoạt động commit | **91 / 196 ngày** (~46%) | Tần suất phát triển gần như liên tục |
| Tổng lượt tệp thay đổi (cộng dồn qua các commit) | **1,400** | |
| Tổng số dòng code thêm mới | **217,213** | |
| Tổng số dòng code xóa/thay thế | **60,461** | |
| Số người đóng góp (contributor) | **1** (THIEUXK1) | Xem mục Rủi ro |

*Nguồn: `git log --since="2026-01-01" --until="2026-07-15 23:59:59" --shortstat` trên repository chính.*

### 2. Phân bổ hoạt động phát triển theo phân hệ (ước tính theo đường dẫn thư mục — estimated)

| Phân hệ | Số commit chạm tới thư mục | Tỷ trọng |
|---|---|---|
| ITForm (phiếu IT + Kiểm kê thiết bị + TSCN) | 212 | ~73% |
| HRform (phiếu Nhân sự) | 79 | ~27% |
| AdminForm (đăng nhập, tài khoản, hạ tầng dùng chung) | 56 | ~19% |
| SHDForm (Xuất Nhập Khẩu/An toàn) | 39 | ~13% |
| QLCongViec (giao việc chỉ định — module mới nhất) | 15 | ~5% |

*Ghi chú: tổng tỷ trọng vượt 100% vì một commit có thể chạm nhiều phân hệ cùng lúc (ví dụ sửa layout dùng chung). Đây là số liệu ước tính theo đường dẫn tệp thay đổi, không phải phân loại nghiệp vụ chính thức.*

### 3. Trạng thái hạ tầng vận hành

| Chỉ số | Giá trị | Trạng thái |
|---|---|---|
| Kiến trúc triển khai | ASP.NET Core (Kestrel/IIS), không dùng container | Xác minh được (từ cấu hình dự án) |
| Uptime máy chủ production | **Không xác định được** | Cần quyền truy cập máy chủ production thực tế |
| Tải CPU/RAM production | **Không xác định được** | Cần công cụ giám sát hạ tầng (hiện chưa triển khai — xem Rủi ro) |
| Dung lượng cơ sở dữ liệu | **1,040 MB (~1.02 GB)** | Xác minh được (truy vấn `sys.master_files`) |

---

## BUSINESS METRICS THỰC TẾ TỪ DATABASE

*(Truy vấn lúc 15/07/2026, 08:06 qua endpoint nội bộ `/AdminForm/ThongKeHeThong`, dùng kết nối cấu hình sẵn của ứng dụng — không truy cập trực tiếp bằng quyền quản trị CSDL)*

### 1. Người dùng hệ thống

*Riêng 2 chỉ số "đã từng sử dụng" / "chưa từng sử dụng" bên dưới được xác minh qua truy vấn `COUNT`/`COUNT DISTINCT` trực tiếp, đơn giản, chỉ đọc (không lộ dữ liệu chi tiết), thực hiện ngày 15/07/2026 sau khi phát hiện định nghĩa "đang làm việc" theo cột `TrangThai` trong bản đầu không phản ánh đúng mức độ sử dụng thực tế.*

*Lưu ý về định nghĩa: "Tổng số tài khoản" là số tài khoản được cấp (thường theo danh sách nhân sự/Domain), không đồng nghĩa với số người **đang sử dụng E-Form**. Việc có/không đăng nhập chỉ phản ánh mức độ sử dụng phần mềm — KHÔNG phải tình trạng còn làm việc hay đã nghỉ việc (đó là dữ liệu nhân sự, không được suy ra từ lịch sử đăng nhập).*

| Chỉ số | Giá trị | Đơn vị | Nguồn xác minh |
|---|---|---|---|
| Tổng số tài khoản được cấp | 6,838 | Users | `SELECT COUNT(*) FROM User` |
| Số tài khoản **đã từng sử dụng** E-Form (≥1 lần đăng nhập) | 679 (~9.9%) | Users | `SELECT COUNT(DISTINCT id_nguoi_dung) FROM LichSuTruyCap` |
| Số tài khoản **chưa từng sử dụng** E-Form | 6,159 (~90.1%) | Users | Tổng tài khoản − đã từng sử dụng |
| Tổng lượt đăng nhập ghi nhận | 3,055 | Lượt | `SELECT COUNT(*) FROM LichSuTruyCap` |
| Lượt đăng nhập 30 ngày gần nhất | 1,554 | Lượt | Lọc theo `ThoiGianDangNhap >= Now-30d` |
| Người dùng có đăng nhập trong 30 ngày gần nhất | 615 | Users | Distinct `IdNguoiDung` trong 30 ngày |

*Nhận xét: trong số 679 tài khoản từng dùng E-Form, có tới 615 tài khoản (~90.6%) vẫn còn hoạt động trong 30 ngày gần nhất — cho thấy nhóm người dùng thực tế của hệ thống khá nhỏ (~10% tổng số tài khoản được cấp) nhưng gắn bó/dùng thường xuyên. Đây là điểm cần lưu ý khi diễn giải "6,838 tài khoản" — con số đó phản ánh quy mô danh sách nhân sự được cấp quyền, không phải quy mô người dùng thực tế của E-Form.*

### 2. Phân hệ Giao việc chỉ định (QLCongViec)

| Chỉ số | Giá trị | Nguồn xác minh |
|---|---|---|
| Tổng số đơn | 18 | `SELECT COUNT(*) FROM FormCongViec` |
| Số đơn tạo trong kỳ báo cáo | 18 (100%) | Lọc `TimeNguoiTao >= 01/01/2026` |
| Số đơn đã hoàn tất | 15 (83.3%) | `WHERE idAdmin IS NOT NULL` |

### 3. Phân hệ Phiếu yêu cầu IT (ITForm)

| Chỉ số | Giá trị | Nguồn xác minh |
|---|---|---|
| Tổng số đơn (8 loại phiếu) | 989 | `SELECT COUNT(*) FROM FormIT` |
| Số đơn tạo trong kỳ báo cáo | 989 (100%) | Lọc `TimeNguoiTao >= 01/01/2026` |
| Số đơn đã hoàn tất | 855 (86.5%) | `WHERE idAdmin IS NOT NULL` |
| Tổng thiết bị đang quản lý (Kiểm kê) | 2,142 | `SELECT COUNT(*) FROM KK_ThietBi WHERE ngay_xoa IS NULL` |
| Tổng máy tính đã thu thập cấu hình tự động (TSCN) | 892 | `SELECT COUNT(*) FROM TSCN_ThongTinMay` |

### 4. Phân hệ Phiếu yêu cầu Nhân sự (HRform)

| Chỉ số | Giá trị | Nguồn xác minh |
|---|---|---|
| Tổng số đơn (12 loại phiếu) | 107 | `SELECT COUNT(*) FROM FormHR` |
| Số đơn tạo trong kỳ báo cáo | 107 (100%) | Lọc `TimeNguoiTao >= 01/01/2026` |
| Số đơn đã hoàn tất | 79 (73.8%) | `WHERE idAdmin IS NOT NULL` |

### 5. Phân hệ Xuất Nhập Khẩu / An toàn (SHDForm)

| Chỉ số | Giá trị | Nguồn xác minh |
|---|---|---|
| Tổng số đơn | 56 | `SELECT COUNT(*) FROM FormSHD` |
| Số đơn tạo trong kỳ báo cáo | 56 (100%) | Lọc `TimeNguoiTao >= 01/01/2026` |
| Số đơn đã hoàn tất | 33 (58.9%) | `WHERE idAdmin IS NOT NULL` |

### 6. Tương tác & trao đổi trên toàn hệ thống

| Chỉ số | Giá trị | Nguồn xác minh |
|---|---|---|
| Tổng số bình luận/thảo luận (4 phân hệ đơn từ) | 687 | Cộng dồn `BinhLuanFormCongViec/It/Hr/Shd` |

### 7. Tổng hợp dung lượng dữ liệu

| Loại dữ liệu | Giá trị | Nguồn xác minh |
|---|---|---|
| Dung lượng database (toàn bộ) | ~1.02 GB (1,040 MB) | `sys.master_files` |
| Tổng số bản ghi nghiệp vụ (4 phân hệ đơn từ) | 1,170 đơn | Tổng CongViec+IT+HR+SHD |
| Tổng số tài khoản người dùng | 6,838 | `Users` |

---

## PHÂN TÍCH CHI TIẾT TỪNG PHÂN HỆ

### 1. PHÂN HỆ GIAO VIỆC CHỈ ĐỊNH (QLCongViec)

**Bài toán nghiệp vụ:** Cho phép quản lý/điều phối viên lập phiếu giao việc trực tiếp cho một hoặc nhiều nhân sự cùng bộ phận, theo dõi tiến độ, đặt mức ưu tiên/thời hạn và đánh giá chất lượng sau khi hoàn tất — thay thế việc giao việc miệng/qua chat không có truy vết.

**Giải pháp công nghệ:**
- Bộ chọn nhân sự dạng tìm kiếm-và-chọn (autocomplete), hiển thị người đã chọn dưới dạng thẻ (chip) có thể bỏ chọn trực tiếp
- Luồng duyệt/hủy/hoàn tất/đánh giá đầy đủ, có ghi log lịch sử theo mốc thời gian
- Bình luận/thảo luận hỗ trợ đính kèm và **kéo-thả ảnh** để gửi nhanh
- Xuất biên bản Excel/Word/PDF kèm khối chữ ký điện tử 3 bên
- Chức năng xóa đơn (transaction xóa toàn bộ dữ liệu liên quan) mới bổ sung trong kỳ

**Số liệu vận hành:**

| Chỉ số | Giá trị |
|---|---|
| Tổng số đơn | 18 |
| Tỷ lệ hoàn tất | 83.3% |
| Số commit trong kỳ (theo thư mục) | 15 |

**Đánh giá kỹ thuật:** Đây là phân hệ mới nhất (100% dữ liệu phát sinh trong kỳ báo cáo), đang trong giai đoạn triển khai ban đầu với quy mô sử dụng còn nhỏ so với các phân hệ IT/HR/SHD đã vận hành lâu hơn.

**Giá trị doanh nghiệp:** Chuẩn hóa việc giao và theo dõi công việc chỉ định nội bộ, có truy vết đầy đủ (ai giao, ai nhận, khi nào hoàn tất, đánh giá chất lượng), giảm thất lạc yêu cầu công việc qua kênh phi chính thức.

---

### 2. PHÂN HỆ QUẢN LÝ PHIẾU YÊU CẦU IT & KIỂM KÊ THIẾT BỊ (ITForm)

**Bài toán nghiệp vụ:** Số hóa toàn bộ quy trình yêu cầu hỗ trợ IT (8 loại phiếu: mail, sự cố kỹ thuật, wifi, điện thoại bàn, tài khoản hệ thống/máy tính, lắp đặt thiết bị, cấp quyền ổ chung) và quản lý tập trung vòng đời tài sản CNTT toàn công ty.

**Giải pháp công nghệ:**
- Xác thực Hybrid Domain/DB linh hoạt theo cấu hình từng tài khoản
- Quy trình duyệt phân cấp theo Công ty/Bộ phận, có bước xác nhận riêng cho phiếu Cấp quyền ổ chung
- Module Kiểm kê thiết bị đầy đủ: CRUD, nhập nhanh hàng loạt từ Excel/CSV (Switch, bản quyền Office), tự động phân loại thiết bị, tra cứu chủ sở hữu theo Serial
- Công cụ quét cấu hình máy tự động (TSCN): tự thu thập RAM/ổ cứng/màn hình/MAC Wifi/bản quyền, đồng bộ 1 chiều từ chi nhánh khác
- Dashboard thống kê, Gantt Timeline tiến trình xử lý, xuất báo cáo Excel đa dạng (số lượng, tuân thủ bản quyền, danh sách thông thường)

**Số liệu vận hành:**

| Chỉ số | Giá trị |
|---|---|
| Tổng số đơn (8 loại phiếu) | 989 |
| Tỷ lệ hoàn tất | 86.5% |
| Tổng thiết bị đang quản lý | 2,142 |
| Máy đã thu thập cấu hình tự động | 892 |
| Số commit trong kỳ (theo thư mục) | 212 (module lớn nhất, ~73% hoạt động phát triển) |

**Đánh giá kỹ thuật:** Phân hệ lớn và trưởng thành nhất hệ thống — cả về khối lượng nghiệp vụ (989 đơn, 2,142 thiết bị) lẫn cường độ phát triển. Phát hiện 1 view thừa (`TaoMail.cshtml`) không còn được route gọi tới, nên dọn dẹp.

**Giá trị doanh nghiệp:** Chuẩn hóa toàn bộ vòng đời yêu cầu IT và tài sản công nghệ, giảm thời gian phản hồi sự cố, tăng độ chính xác kiểm kê tài sản, hỗ trợ ra quyết định đầu tư/thay thế thiết bị dựa trên dữ liệu thực tế thay vì kiểm đếm thủ công.

---

### 3. PHÂN HỆ QUẢN LÝ PHIẾU YÊU CẦU NHÂN SỰ (HRform)

**Bài toán nghiệp vụ:** Số hóa 12 loại yêu cầu hành chính-nhân sự phổ biến (ra ngoài, mang hàng ra cổng, xe công tác, tiếp khách, nhà thầu qua cổng, ký túc xá, làm lại thẻ nhân viên...) với quy trình duyệt nhiều cấp và phối hợp với đội Bảo vệ kiểm soát cổng.

**Giải pháp công nghệ:**
- Quy trình duyệt tối đa 4 cấp: Quản lý trực tiếp → Bước 2 theo bộ phận (hỗ trợ AND/OR + ủy quyền) → Giám đốc → HR/Admin hoàn tất
- Tích hợp kiểm soát cổng: đẩy phiếu sang đội Bảo vệ xác nhận ra/vào
- Quản lý phòng họp tích hợp (kiểm tra trùng lịch tự động cho đơn tiếp khách)
- Mẫu xuất song ngữ Việt-Trung riêng cho đơn thẻ nhân viên và đơn điện thoại

**Số liệu vận hành:**

| Chỉ số | Giá trị |
|---|---|
| Tổng số đơn (12 loại phiếu) | 107 |
| Tỷ lệ hoàn tất | 73.8% |
| Số commit trong kỳ (theo thư mục) | 79 |

**Đánh giá kỹ thuật:** Quy trình phê duyệt phức tạp nhất hệ thống (4 cấp + kiểm soát cổng), tỷ lệ hoàn tất thấp hơn IT/QLCongViec — có thể do số cấp duyệt nhiều hơn kéo dài thời gian xử lý, cần theo dõi thêm để xác định điểm nghẽn.

**Giá trị doanh nghiệp:** Thay thế quy trình giấy tờ thủ công cho các thủ tục hành chính lặp lại hàng ngày, tăng minh bạch trong kiểm soát ra/vào cổng, giảm xung đột lịch phòng họp.

---

### 4. PHÂN HỆ XUẤT NHẬP KHẨU / AN TOÀN (SHDForm)

**Bài toán nghiệp vụ:** Số hóa quy trình đăng ký và phê duyệt sử dụng xe công tác/xe đưa đón, với luồng xác nhận nhiều cấp tương tự HR.

**Giải pháp công nghệ:**
- Quy trình duyệt tương tự HR (Quản lý → B2 → Giám đốc → Admin hoàn tất), có ủy quyền duyệt thay
- Xuất báo cáo Excel theo khoảng ngày/loại đơn, xuất chứng từ từng phiếu ra Excel/Word/PDF

**Số liệu vận hành:**

| Chỉ số | Giá trị |
|---|---|
| Tổng số đơn | 56 |
| Tỷ lệ hoàn tất | 58.9% |
| Số commit trong kỳ (theo thư mục) | 39 |

**Đánh giá kỹ thuật:** Tỷ lệ hoàn tất thấp nhất trong 4 phân hệ đơn từ (58.9%) — cần rà soát xem là do quy trình duyệt còn chậm hay do dữ liệu thử nghiệm (model `ShdDangKySuDungXeDaily2` đã có nhưng chưa có giao diện tạo đơn tương ứng, cho thấy phân hệ này vẫn đang mở rộng dở dang).

**Giá trị doanh nghiệp:** Kiểm soát và tối ưu hóa việc sử dụng xe công ty, giảm thất thoát/tranh chấp lịch xe, có dữ liệu lịch sử phục vụ quyết toán chi phí vận hành xe.

---

### 5. NỀN TẢNG DÙNG CHUNG (Đăng nhập, Tài khoản, Hạ tầng chia sẻ)

**Bài toán nghiệp vụ:** Cung cấp lớp xác thực, phân quyền và tiện ích dùng chung cho toàn bộ 4 phân hệ nghiệp vụ phía trên, tránh trùng lặp code và đảm bảo trải nghiệm nhất quán.

**Giải pháp công nghệ:**
- Xác thực Hybrid (Domain/DB nội bộ), tự khóa tài khoản tạm thời sau nhiều lần đăng nhập sai
- Phân quyền tự động theo Bộ phận/Vai trò khi đăng nhập
- Cổng điều hướng trung tâm (Menu chính), quản lý tài khoản cá nhân/ảnh đại diện/đổi mật khẩu
- Cache HTTP (Cache-Control/ETag) cho ảnh đính kèm bình luận — mới bổ sung trong kỳ để giảm thời gian tải lại trang

**Số liệu vận hành:**

| Chỉ số | Giá trị |
|---|---|
| Tổng số tài khoản được cấp | 6,838 |
| Số tài khoản đã từng sử dụng E-Form | 679 (~9.9%) |
| Người dùng có đăng nhập trong 30 ngày gần nhất | 615 |
| Tổng lượt đăng nhập ghi nhận | 3,055 |
| Số commit trong kỳ (theo thư mục) | 56 |

**Giá trị doanh nghiệp:** Một điểm đăng nhập duy nhất (Single Sign-On nội bộ) cho toàn bộ 4 phân hệ nghiệp vụ, giảm chi phí vận hành tài khoản và tăng tính nhất quán trải nghiệm người dùng.

---

## TỔNG HỢP CHỈ SỐ HIỆU QUẢ

### A. Chỉ số người dùng (verifiable)

| Chỉ số | Giá trị |
|---|---|
| Tổng số tài khoản được cấp trong hệ thống | 6,838 |
| Số tài khoản đã từng sử dụng E-Form (≥1 lần đăng nhập) | 679 (~9.9%) |
| Số tài khoản chưa từng sử dụng E-Form | 6,159 (~90.1%) |
| Người dùng có đăng nhập trong 30 ngày gần nhất | 615 (~90.6% trong nhóm đã từng dùng) |
| Tổng lượt đăng nhập ghi nhận toàn hệ thống | 3,055 |

*Ghi chú: "Tổng số tài khoản được cấp" phản ánh quy mô danh sách nhân sự/Domain được cấp quyền truy cập, không phải số người thực sự dùng E-Form — không dùng để suy luận về tình trạng còn làm việc/đã nghỉ của nhân sự.*

### B. Chỉ số khối lượng nghiệp vụ theo phân hệ (verifiable)

| Phân hệ | Tổng số đơn | Tỷ lệ hoàn tất |
|---|---|---|
| ITForm | 989 | 86.5% |
| HRform | 107 | 73.8% |
| SHDForm | 56 | 58.9% |
| QLCongViec | 18 | 83.3% |
| **Tổng cộng** | **1,170 đơn** | **~82.5%** (trung bình có trọng số) |

*Nhận xét: phân hệ ITForm chiếm 84.5% tổng khối lượng đơn từ toàn hệ thống — phản ánh đúng tỷ trọng ~73% hoạt động phát triển (commit) dồn vào phân hệ này.*

---

## RỦI RO VÀ ĐỀ XUẤT CHIẾN LƯỢC

### Rủi ro hiện tại (quan sát trực tiếp từ mã nguồn/cấu hình trong quá trình làm việc)

1. **Bus factor = 1:** Toàn bộ 289 commit trong kỳ chỉ do 1 người thực hiện (THIEUXK1). Rủi ro vận hành cao nếu nhân sự này vắng mặt/nghỉ việc — không có ai khác nắm toàn bộ logic hệ thống.
2. **Kết nối cơ sở dữ liệu dùng tài khoản `sa`:** Chuỗi kết nối trong `appsettings.json` dùng tài khoản quản trị cấp cao nhất của SQL Server thay vì tài khoản ứng dụng có quyền giới hạn — rủi ro bảo mật nếu file cấu hình bị lộ.
3. **Chưa có kiến trúc giám sát hạ tầng:** Không có công cụ theo dõi uptime/hiệu năng production (khác biệt với các hệ thống dùng Prometheus/Grafana) — khó phát hiện sớm sự cố hoặc downtime.
4. **Nợ kỹ thuật rải rác:** Phát hiện 1 view thừa không còn route gọi tới (`TaoMail.cshtml`); một số route xuất Excel/Word/PDF tồn tại ở tầng controller nhưng chưa chắc chắn đã gắn đầy đủ nút bấm trên giao diện.
5. **Tỷ lệ hoàn tất không đồng đều giữa các phân hệ:** SHDForm (58.9%) và HRform (73.8%) thấp hơn đáng kể so với ITForm (86.5%) — có thể phản ánh quy trình duyệt nhiều cấp gây tồn đọng, cần phân tích sâu hơn về thời gian xử lý trung bình từng cấp.
6. **Chưa có automated backup / disaster recovery được xác minh:** Chưa quan sát thấy cấu hình backup tự động cho cơ sở dữ liệu 1.02 GB đang chứa dữ liệu vận hành của 6,838 tài khoản.

### Đề xuất cải tiến

1. **Giảm rủi ro bus factor:** Xây dựng tài liệu kiến trúc/vận hành (runbook) và đào tạo chéo ít nhất 1 nhân sự dự phòng.
2. **Thu hẹp quyền kết nối CSDL:** Tạo tài khoản SQL Server riêng cho ứng dụng với quyền tối thiểu cần thiết (principle of least privilege), thay thế tài khoản `sa`.
3. **Triển khai giám sát cơ bản:** Thêm health-check endpoint và log tập trung tối thiểu để phát hiện sớm lỗi production.
4. **Dọn dẹp nợ kỹ thuật:** Rà soát và xóa view/route không còn sử dụng; xác minh và hoàn thiện UI cho các route xuất báo cáo đã tồn tại sẵn ở tầng controller.
5. **Phân tích điểm nghẽn quy trình duyệt:** Đo thời gian xử lý trung bình theo từng cấp duyệt ở HRform/SHDForm để xác định nguyên nhân tỷ lệ hoàn tất thấp.
6. **Thiết lập chính sách backup định kỳ** cho cơ sở dữ liệu sản xuất, kèm kiểm thử khôi phục (restore test) định kỳ.

### Roadmap đề xuất

1. **Q3-2026:** Thu hẹp quyền kết nối CSDL, dọn dẹp nợ kỹ thuật đã phát hiện, thiết lập backup tự động có kiểm thử khôi phục.
2. **Q4-2026:** Triển khai giám sát hạ tầng cơ bản (health-check, log tập trung), phân tích và tối ưu điểm nghẽn quy trình duyệt HR/SHD.
3. **Q1-2027:** Đào tạo chéo/tài liệu hóa vận hành để giảm rủi ro bus factor; mở rộng module QLCongViec (đang ở giai đoạn đầu triển khai).
4. **Q2-2027:** Đánh giá nhu cầu mở rộng lên kiến trúc có khả năng chịu tải/dự phòng cao hơn nếu quy mô 6,838 tài khoản tiếp tục tăng.

---

## KẾT LUẬN

Trong gần 6 tháng đầu năm 2026, hệ thống E-Form-Best đã được phát triển liên tục (91/196 ngày có commit) và đưa vào vận hành thực tế với khối lượng nghiệp vụ đáng kể: **1,170 đơn từ** được xử lý qua 4 phân hệ chính. Trong số **6,838 tài khoản** được cấp quyền truy cập, có **679 tài khoản (~9.9%)** đã thực sự sử dụng E-Form, trong đó **615 người dùng** vẫn hoạt động trong 30 ngày gần nhất — cho thấy nhóm người dùng lõi tuy nhỏ nhưng gắn bó thường xuyên với hệ thống. Phân hệ ITForm (kết hợp quản lý phiếu yêu cầu và kiểm kê tài sản CNTT) là trụ cột lớn nhất cả về khối lượng nghiệp vụ (989 đơn, 2,142 thiết bị) lẫn cường độ đầu tư phát triển (73% hoạt động commit).

Điểm cần lưu ý nhất đối với ban lãnh đạo là **rủi ro vận hành do phụ thuộc vào một cá nhân duy nhất** trong toàn bộ quá trình phát triển, cùng với **khoảng trống về giám sát hạ tầng và chính sách bảo mật kết nối cơ sở dữ liệu** — đây là các hạng mục nên ưu tiên xử lý trước khi tiếp tục mở rộng quy mô hệ thống.

---

*Báo cáo được tạo tự động dựa trên dữ liệu xác minh trực tiếp từ `git log` và truy vấn cơ sở dữ liệu qua endpoint nội bộ của ứng dụng, thực hiện ngày 15/07/2026.*
