/* ============================================================================
   Rollback cho kk-camera-ghi-chu-20261006.sql — xoá bảng KK_CameraGhiChu.
   CẢNH BÁO: mất toàn bộ ghi chú camera. Có dữ liệu thì backup trước:
       SELECT * INTO dbo.KK_CameraGhiChu_bak_YYYYMMDD FROM dbo.KK_CameraGhiChu;
   Trước khi chạy: gỡ code cột ghi chú của /QLCamera, không thì tab Giám sát báo lỗi ghi chú.
   ========================================================================== */

USE ITForm;
GO

SELECT COUNT(*) AS so_ghi_chu_se_mat FROM dbo.KK_CameraGhiChu;
GO

IF EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.TABLES
           WHERE TABLE_SCHEMA = 'dbo' AND TABLE_NAME = 'KK_CameraGhiChu')
    DROP TABLE dbo.KK_CameraGhiChu;
GO

SELECT OBJECT_ID('dbo.KK_CameraGhiChu') AS phai_la_null;
GO
