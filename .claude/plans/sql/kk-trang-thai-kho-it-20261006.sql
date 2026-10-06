/* ============================================================================
   Mục đích : Thêm trạng thái "Kho IT" cho nút "Kho IT" ở trang Quản lý Thiết bị (/QLKiemKe/ThietBi)
              và mục menu Quản lý Thiết bị > Kho IT. Code tra trạng thái theo TÊN ('kho it').

   Phạm vi   : - INSERT 1 dòng vào dbo.KK_TrangThai (không đổi cấu trúc bảng, không đụng KK_ThietBi)
   An toàn   : Idempotent — đã có "Kho IT" thì không thêm nữa.
   Rollback  : kk-trang-thai-kho-it-20261006-rollback.sql
   Lưu ý chạy: sqlcmd phải thêm -f 65001.
   Cách khác : Có thể thêm tay ở Cấu hình Danh mục > Trạng thái, tên đúng "Kho IT".
   ========================================================================== */

USE ITForm;
GO

SELECT id_trang_thai, ten_trang_thai FROM dbo.KK_TrangThai
WHERE LTRIM(RTRIM(LOWER(ten_trang_thai))) = N'kho it';
-- Kỳ vọng lần chạy đầu: 0 dòng
GO

IF NOT EXISTS (SELECT 1 FROM dbo.KK_TrangThai WHERE LTRIM(RTRIM(LOWER(ten_trang_thai))) = N'kho it')
    INSERT INTO dbo.KK_TrangThai (ten_trang_thai, mo_ta)
    VALUES (N'Kho IT', N'Thiết bị đang nằm ở kho IT');
GO

SELECT id_trang_thai, ten_trang_thai, mo_ta FROM dbo.KK_TrangThai ORDER BY id_trang_thai;
-- Kỳ vọng: có thêm 1 dòng "Kho IT"
GO
