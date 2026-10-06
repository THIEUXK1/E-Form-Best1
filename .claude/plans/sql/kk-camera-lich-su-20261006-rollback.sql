/* ============================================================================
   Rollback cho kk-camera-lich-su-20261006.sql — xoá KK_CameraLichSu và KK_CameraTrangThai.
   CẢNH BÁO: mất toàn bộ lịch sử đổi trạng thái camera. Backup trước:
       SELECT * INTO dbo.KK_CameraLichSu_bak_YYYYMMDD FROM dbo.KK_CameraLichSu;
   Trước khi chạy: gỡ CameraLichSuWorker + tab Lịch sử, không thì job nền báo lỗi mỗi lượt.
   ========================================================================== */

USE ITForm;
GO

SELECT (SELECT COUNT(*) FROM dbo.KK_CameraLichSu) AS su_kien_se_mat,
       (SELECT COUNT(*) FROM dbo.KK_CameraTrangThai) AS trang_thai_se_mat;
GO

IF OBJECT_ID('dbo.KK_CameraLichSu') IS NOT NULL DROP TABLE dbo.KK_CameraLichSu;
IF OBJECT_ID('dbo.KK_CameraTrangThai') IS NOT NULL DROP TABLE dbo.KK_CameraTrangThai;
GO

SELECT OBJECT_ID('dbo.KK_CameraLichSu') AS phai_null_1, OBJECT_ID('dbo.KK_CameraTrangThai') AS phai_null_2;
GO
