# Bộ nhớ bối cảnh — cập nhật liên tục

> File này là **trạng thái sống**, tự nạp mỗi phiên → giữ ngắn.
> Sửa trực tiếp mỗi khi đổi việc / gặp blocker / chốt quyết định.
> Decision Log chỉ giữ **5 dòng gần nhất**, cũ hơn đẩy xuống [`decision-log.md`](decision-log.md).
> **Luôn ghi ngày tuyệt đối (dd/mm/yyyy)**, không viết "tuần trước", "hôm qua".

---

## Đang làm (cập nhật: 17/09/2026)

**Việc:** Kiểm kê IT — bộ lọc vị trí địa lý / nhu cầu cài Office, bổ sung dữ liệu WKNA, và dán ảnh
vào bình luận đơn IT.

**File đang chạm (theo `git status` ngày 17/09/2026):**

| File | Trạng thái |
|---|---|
| `.claude/plans/sql/kk-wkna-bosung-20260917.sql` + `-rollback.sql` | mới — **chưa chạy**, chờ duyệt |
| `.claude/plans/sql/kk-capnhat-vitri-office-20260917*` (`.sql`, `.csv`) | sửa |
| `E-Form-Best/wwwroot/js/kiemke-loc-can-cai-office.js` | mới |
| `E-Form-Best/wwwroot/js/formit-binhluan-dan-anh.js` | mới |
| `E-Form-Best/wwwroot/js/kiemke-loc-vitri-dialy.js` | sửa |
| `E-Form-Best/Areas/ITForm/Views/ITForm/IndexThietBi.cshtml` | sửa |
| `E-Form-Best/Areas/ITForm/Views/ITForm/ChiTiet.cshtml` | sửa |
| `watch-supervisor.ps1` | mới — supervisor cho `dotnet watch` |

**Nhánh:** `master` (commit gần nhất `fe78d82` — lọc vị trí địa lý, đổi đơn vị đơn IT,
bắt phiên hết hạn sớm).

---

## Blockers

| # | Vấn đề | Ảnh hưởng | Cần ai/cái gì để gỡ |
|---|---|---|---|
| B1 | Bảng `KK_ThietBiChan` phải được tạo **thủ công** trên SQL Server production trước khi deploy — repo không có Migration | Deploy code mà chưa chạy DDL → lỗi runtime khi mở trang Kiểm kê | Người có quyền trên `10.0.60.33`; script DDL phải được duyệt trước ([`../docs/database-safety.md`](../docs/database-safety.md)) |
| B2 | Connection string **đã** ra `.env` (`c3d22a1`), nhưng tài khoản dùng vẫn là `sa` | Lộ file `.env` = toàn quyền ghi trên cả 2 server SQL | Cấp tài khoản SQL riêng quyền tối thiểu, đổi mật khẩu `sa` (đã từng nằm trong lịch sử git) |
| B3 | Không có công cụ giám sát production; `/health/ready` đã có nhưng chưa có gì gọi nó | Sự cố chỉ biết khi người dùng báo | Cấu hình uptime check trỏ vào `/health/ready` |

*(B4 — bảng `IT_ThietKeTemIn_9` — đã gỡ 27/08/2026: DDL đã chạy trên `10.0.60.33`, đối soát
0/0/0 → 1/1/1, FK `FK_ITThietKeTemIn_FormIT` SET_NULL đã có.)*

---

## Decision Log — 5 quyết định gần nhất

Cũ hơn: [`decision-log.md`](decision-log.md).

| Ngày | Quyết định | Lý do | Hệ quả |
|---|---|---|---|
| 21/09/2026 | Tách **số tờ in màu / đen trắng**: thêm 2 cột `counter_in_mau`, `counter_in_den_trang` vào `MayIn_ChiSo` (script `mayin-counter-mau-dentrang-20260921.sql`, **đã chạy** trên `10.0.60.33` ngày 21/09 khi người dùng xác nhận trực tiếp trong phiên) | Máy Apeos trả sẵn `PRINT_TOTAL_COLOR_IMPRESSION` / `PRINT_TOTAL_BW_IMPRESSION` qua `/home/api/billing-counter`, trước đó `MayInApiService` đọc lên rồi vứt đi vì sợ cộng trùng vào `counter_tong` | Hai cột là **đồng hồ tích luỹ**, số tờ trong kỳ = hiệu hai mốc chốt, giống `counter_tong`. Chỉ nhóm máy có `/home/api` mới có số — máy đọc qua `prcnt.htm` hoặc PJL để NULL vĩnh viễn. Dữ liệu quá khứ không backfill được nên phải qua ≥2 kỳ chốt mới ra số đầu tiên. **Chỉ tách phần IN, chưa tách COPY** |
| 18/09/2026 | Module **Quản lý máy in** (`/QLMayIn`, quyền `All`): 2 bảng mới `MayIn` + `MayIn_ChiSo`, mỗi máy mỗi ngày **một** dòng chỉ số, job nền `MayInPollWorker` chốt lúc 23h | Số hoá bảng Excel "Print out information2026.xlsx"; ràng buộc 1 dòng/ngày làm việc đọc lại nhiều lần trong ngày vẫn idempotent | Chỉ **24/83** máy có IP trả lời `/home/api/*` (dòng Apeos đời mới) — số còn lại là Fuji Xerox cũ, SNMP tắt, phải nhập chỉ số tay. Trang in theo ngày = chênh lệch `counter_tong`, counter tụt (thay main board) bị bỏ qua |
| 17/09/2026 | `.claude/rules/` rút còn **một** file `core.md`; 4 file dài chuyển sang `.claude/docs/`, decision log cũ tách ra `plans/decision-log.md` | `rules/` bị tự nạp mỗi phiên — 34 KB rules + 10 KB context-memory tốn ngữ cảnh cho cả task không chạm tới | Thêm quy tắc mới: ngắn & áp dụng mọi task → `core.md`; dài/chuyên đề → `docs/` rồi thêm dòng vào bảng lazy-load mục 9 của `core.md` |
| 07/09/2026 | Thêm đơn số 12 **Trả thiết bị** (`IT_TraThietBi_12`), 12 cột, gán **cả 3** nhân sự V200887 + V200888 + V210817 (`CongViecIT.id` 1032–1034) | Thiết bị hỏng trả về cần cả tổ IT cùng nắm | Controller **bỏ qua** `SelectedCongViecIds`, luôn gán toàn bộ người đảm nhận "Trả thiết bị". Checkbox trên form chỉ để người tạo xác nhận đã đọc danh sách |
| 27/08/2026 | **Không ghép code tiếng Việt vào file bằng `Get-Content` của PowerShell 5.1** | PS 5.1 đọc file UTF-8 **không BOM** như ANSI → hằng số `"Cài đặt phần mềm"` thành `"C脿i 膽岷穞..."`, build 0 lỗi nhưng lọc sai ở runtime | Dùng `[System.IO.File]::ReadAllText/WriteAllLines` với `UTF8Encoding` tường minh. Sau mỗi lần ghép, quét `[一-鿿]`: controller chỉ được có **163** ký tự CJK hợp lệ, nhiều hơn là hỏng mã |
| 27/08/2026 | Thêm đơn số 11 **Lập trình ứng dụng** (`IT_LapTrinhUngDung_11`), bộ đầy đủ 15 cột, phụ trách V240298 (`CongViecIT.id=1028`) | Nhu cầu viết tool/macro/dashboard nội bộ; đơn này cần mô tả kỹ đầu vào–đầu ra | JS bắt buộc `MoTaYeuCau` tối thiểu 30 ký tự — mô tả sơ sài là nguồn gốc của hỏi lại nhiều vòng |
| 27/08/2026 | **Nơi đăng ký loại đơn IT là `ITFormController.DangKyDon()`**, không phải submenu trong `_Layout` | Từ `a019bd6` menu sidebar bỏ danh sách con, gom về trang thẻ chọn `/FormIT/DangKyDon` | Thêm đơn mới = thêm 1 dòng `LoaiDonIt` (Stt/Ten/Icon/Mau/Url/MoTa) + thêm action vào `itSubPages` ở `_Layout` dòng 22. Sửa submenu trong layout là sai chỗ |

**Lưu ý còn hiệu lực:** bảng màu thẻ đơn IT đã dùng 10 hue (21°, 43°, 78°, 142°, 189°, 221°, 245°,
258°, 293°, 333°) — khoảng hở lớn nhất chỉ còn ~64° (giữa 78° và 142°); đơn mới nên phân biệt bằng
độ đậm/icon thay vì tìm hue mới.

---

## Ghi chú vận hành

- Cách chạy dự án + quy tắc hot reload: [`../rules/core.md`](../rules/core.md) mục 8.
- Log `dotnet watch` đổ ra `watch_log.txt`, `dotnet-watch.log`, `dotnet-watch.err.log` ở gốc repo —
  **file rác, đã nằm trong `.claudeignore`, đừng commit.**
- Chạy file `.sql` tiếng Việt bằng `sqlcmd` phải thêm `-f 65001`, không thì dữ liệu vào DB lỗi font.
- Deploy: production ở `10.0.60.39`, IIS `C:\inetpub\wed\E-Form-Best`, dùng `app_offline` + `robocopy /E`.
- Ràng buộc thường trực (view là Dumb UI, không reload trang, JS ra file riêng, secret trong `.env`)
  nằm ở [`../rules/core.md`](../rules/core.md) mục 3–4 — **không chép lại ở đây**.

## Ghi chú cho phiên sau

- `kk-wkna-bosung-20260917.sql` **chưa chạy** — cần duyệt rồi chạy tay; đã có file rollback kèm theo.
- Danh sách form còn submit đồng bộ (phải về 0): [`00-master-plan.md`](00-master-plan.md) mục 4.
