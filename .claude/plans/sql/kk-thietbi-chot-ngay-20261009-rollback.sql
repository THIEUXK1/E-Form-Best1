/* ============================================================================
   Rollback của kk-thietbi-chot-ngay-20261009.sql
   Xoá bảng dbo.KK_ThietBiChotNgay — MẤT TOÀN BỘ các bản chụp số liệu thiết bị đã ghi
   (không tạo lại được: số của các ngày đã qua không còn nguồn nào khác).
   Chỉ chạy khi gỡ hẳn tính năng; trước đó tắt job (ThietBiChotNgay__Bat=false) hoặc gỡ code.
   Lưu ý chạy: sqlcmd -f 65001.
   ========================================================================== */

USE ITForm;
GO

/* Xem trước sẽ mất bao nhiêu dòng / từ ngày nào */
IF OBJECT_ID('dbo.KK_ThietBiChotNgay', 'U') IS NOT NULL
    SELECT COUNT(*) AS so_dong, MIN(ngay) AS tu_ngay, MAX(ngay) AS den_ngay FROM dbo.KK_ThietBiChotNgay;
GO

IF OBJECT_ID('dbo.KK_ThietBiChotNgay', 'U') IS NOT NULL
    DROP TABLE dbo.KK_ThietBiChotNgay;
GO

SELECT TABLE_NAME FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME = 'KK_ThietBiChotNgay';
-- Kỳ vọng: 0 dòng
GO
