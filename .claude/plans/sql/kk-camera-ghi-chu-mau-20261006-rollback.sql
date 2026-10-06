/* ============================================================================
   Rollback cho kk-camera-ghi-chu-mau-20261006.sql — xoá bảng KK_CameraGhiChuMau.
   CẢNH BÁO: mất danh sách ghi chú có sẵn (ghi chú đã gán cho camera ở KK_CameraGhiChu KHÔNG mất).
   Có dữ liệu thì backup trước:
       SELECT * INTO dbo.KK_CameraGhiChuMau_bak_YYYYMMDD FROM dbo.KK_CameraGhiChuMau;
   Trước khi chạy: gỡ code ghi chú mẫu của /QLCamera.
   ========================================================================== */

USE ITForm;
GO

SELECT COUNT(*) AS so_mau_se_mat FROM dbo.KK_CameraGhiChuMau;
GO

IF EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.TABLES
           WHERE TABLE_SCHEMA = 'dbo' AND TABLE_NAME = 'KK_CameraGhiChuMau')
    DROP TABLE dbo.KK_CameraGhiChuMau;
GO

SELECT OBJECT_ID('dbo.KK_CameraGhiChuMau') AS phai_la_null;
GO
