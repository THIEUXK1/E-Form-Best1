# An toàn dữ liệu & quy trình thay đổi schema

> **Bối cảnh sống còn:** CSDL `ITForm` trên `10.0.60.33` là **production đang chạy thật**
> (~1 GB, 6.838 tài khoản, hơn 1.170 đơn nghiệp vụ). Không có môi trường staging riêng và
> **chưa xác minh có backup tự động**. Mọi sai sót trên đó là mất dữ liệu thật.

## 1. Luật tuyệt đối

- ❌ **KHÔNG tự ý chạy DDL/DML trên bất kỳ CSDL nào**: không `ALTER`, `DROP`, `TRUNCATE`,
  `UPDATE`, `DELETE`, không backfill "vô hại".
- ✅ Đọc thì tự do: `SELECT`, `EXPLAIN`, xem schema, đọc model.
- ⚠️ **Luôn hỏi xác nhận trước mọi thao tác ghi.** Trước khi hỏi phải nêu rõ: **lệnh gì**,
  **chạm bảng/số hàng nào**, **có rollback không**.
- ⚠️ Không chắc là production hay không → **coi như production**.
- ❌ Không đụng `10.0.55.3` (chi nhánh) bằng bất cứ lệnh ghi nào — quan hệ là **đồng bộ một chiều,
  chỉ đọc từ đó về**.
- ❌ **Không commit connection string / mật khẩu** vào repo.
  *(Hiện trạng đang vi phạm: `appsettings.json` chứa tài khoản `sa` của cả 2 server — blocker B2.)*

## 2. Repo KHÔNG dùng EF Migrations

**Không có thư mục `Migrations/`. Không chạy `dotnet ef migrations add` / `database update`.**
Mô hình là **DB-first**: schema được quản trực tiếp trên SQL Server, model C# sinh/sửa theo sau.

### Quy trình đổi schema — đúng thứ tự

1. **Soạn script DDL** ra file `.sql` (không chạy), nêu rõ mục đích và bảng bị chạm.
2. **Trình script cho người dùng duyệt.** Kèm: ảnh hưởng gì, có rollback không, có cần backup trước không.
3. **Người dùng (hoặc DBA) tự chạy** trên SQL Server. Không tự chạy hộ.
4. **Sau khi DDL đã chạy xong**, mới sửa/thêm model trong `Models/ITForm/` cho khớp
   (`[Table]`, `[Column]`, `[StringLength]`, `[Key]` tường minh).
5. Thêm `DbSet<T>` vào `Context/ITFormContext.cs`, cấu hình thêm trong `OnModelCreating` nếu cần.
6. Ghi vào Decision Log của [`../plans/00-context-memory.md`](../plans/00-context-memory.md).

> ⚠️ **Sai thứ tự = production nổ:** deploy code có model mới trước khi bảng tồn tại → mọi trang
> chạm bảng đó lỗi runtime. Đây chính là blocker **B1** hiện tại với `KK_ThietBiChan`.

### Yêu cầu với script DDL

| Yêu cầu | Vì sao |
|---|---|
| Cột mới phải `NULL` được hoặc có `DEFAULT` | Bảng đang có dữ liệu; `NOT NULL` không default sẽ fail |
| `IF NOT EXISTS` khi `CREATE TABLE` / `ADD COLUMN` | Chạy lại lần hai không được nổ (idempotent) |
| Kèm script rollback | Mỗi `up` phải có `down` tương ứng |
| Kèm câu `SELECT` đối soát | Chạy trước và sau, so số dòng / checksum để xác minh |
| Đổi/xoá cột hoặc backfill → **yêu cầu backup trước** | Không hồi phục được nếu thiếu |

## 3. Soft delete

**Hiện trạng: chỉ `KkThietBi` có soft-delete** (cột `ngay_xoa` / `NgayXoa`). Các bảng khác xoá cứng.

- Mọi truy vấn chạm `KkThietBi` **phải lọc `NgayXoa == null`** — không có global query filter,
  quên lọc là hiện lại thiết bị đã xoá trong danh sách/thống kê/export.
- Thêm truy vấn mới trên `KkThietBi` → kiểm lại điều kiện lọc trước khi coi là xong.
- Nghiệp vụ cần khôi phục được (đơn từ, tài sản, nhật ký) → **đề xuất soft-delete**, đừng xoá cứng.
- `KkThietBiChan` là **danh sách chặn**, không phải soft-delete — hai cơ chế khác nhau, đừng gộp.

## 4. Idempotency

Chỗ dễ nhân đôi dữ liệu, phải kiểm khi sửa:

| Luồng | Rủi ro |
|---|---|
| Nhập Excel/CSV hàng loạt (Switch, key Office, bản quyền Windows) | Người dùng bấm nhập 2 lần → trùng bản ghi. Cần đối chiếu theo Serial/IdMay trước khi insert |
| Đồng bộ thiết bị từ chi nhánh `10.0.55.3` | Chạy lại phải **update** bản ghi cũ, không tạo bản mới. Khớp theo `IdMay`/`Serial` (đã xử lý ở commit `e1d16fc`) |
| Quét cấu hình máy (TSCN) | Cùng một máy quét nhiều lần → phải upsert |
| `AutoRatingWorker` (0h) | Chạy lại/khởi động lại app trong ngày không được chấm điểm hai lần |
| Submit form đơn | Double-submit tạo hai đơn |

Kiểm tra ràng buộc `UNIQUE` ở **tầng DB** thay vì chỉ tin validation ở tầng ứng dụng.

## 5. Nhật ký / audit

Các bảng lịch sử — coi là **append-only**, không sửa/xoá bản ghi cũ:

`LichSuFormIt`, `LichSuFormHr`, `LichSuFormShd`, `LichSuFormCongViec`, `LichSuFormDaoTao`,
`LichSuTruyCap`, `KkLichSuThaoTac`, `TscnLichSuThayDoi`, `TscnLichSuXacThucAdmin`,
`TscnLichSuXacThucNguoiDung`.

Mỗi bản ghi cần đủ: **ai (actor) — lúc nào — giá trị trước/sau**. Thao tác nghiệp vụ quan trọng
(duyệt, huỷ, hoàn tất, xoá đơn, chặn thiết bị) **phải ghi log**, không im lặng.

## 6. Trước khi coi một thay đổi dữ liệu là xong

- [ ] Script DDL đã được người dùng duyệt và **tự tay chạy**?
- [ ] Có script rollback?
- [ ] Đã chạy `SELECT` đối soát trước/sau, số liệu khớp kỳ vọng?
- [ ] Truy vấn liên quan đã lọc soft-delete (nếu chạm `KkThietBi`)?
- [ ] Chạy lại lần hai có an toàn không?
- [ ] Thao tác đã được ghi vào bảng lịch sử tương ứng?
