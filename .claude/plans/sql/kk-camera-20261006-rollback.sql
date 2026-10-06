/* ============================================================================
   Rollback cho kk-camera-20261006.sql — xoá bảng KK_Camera.
   CẢNH BÁO: mất toàn bộ camera đã nhập. Có dữ liệu thì backup trước:
       SELECT * INTO dbo.KK_Camera_bak_YYYYMMDD FROM dbo.KK_Camera;
   Trước khi chạy: gỡ code QLCamera (hoặc đặt app_offline), không thì trang camera lỗi.
   ========================================================================== */

USE ITForm;
GO

SELECT COUNT(*) AS so_camera_se_mat FROM dbo.KK_Camera;
GO

IF EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.TABLES
           WHERE TABLE_SCHEMA = 'dbo' AND TABLE_NAME = 'KK_Camera')
    DROP TABLE dbo.KK_Camera;   -- index + FK đi theo bảng
GO

SELECT OBJECT_ID('dbo.KK_Camera') AS phai_la_null;
GO
