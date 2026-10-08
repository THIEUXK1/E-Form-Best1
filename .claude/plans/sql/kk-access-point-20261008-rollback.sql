/* ============================================================================
   Rollback cho kk-access-point-20261008.sql — xoá KK_AccessPointLichSu và KK_AccessPoint.
   CẢNH BÁO: mất toàn bộ danh sách AP và lịch sử đổi trạng thái. Backup trước:
       SELECT * INTO dbo.KK_AccessPoint_bak_YYYYMMDD FROM dbo.KK_AccessPoint;
       SELECT * INTO dbo.KK_AccessPointLichSu_bak_YYYYMMDD FROM dbo.KK_AccessPointLichSu;
   Trước khi chạy: gỡ code QLAP + AccessPointPingWorker, không thì trang /QLAP và job nền báo lỗi.
   ========================================================================== */

USE ITForm;
GO

SELECT (SELECT COUNT(*) FROM dbo.KK_AccessPoint) AS ap_se_mat,
       (SELECT COUNT(*) FROM dbo.KK_AccessPointLichSu) AS su_kien_se_mat;
GO

-- Bảng lịch sử có khoá ngoại sang KK_AccessPoint nên xoá trước
IF OBJECT_ID('dbo.KK_AccessPointLichSu') IS NOT NULL DROP TABLE dbo.KK_AccessPointLichSu;
IF OBJECT_ID('dbo.KK_AccessPoint') IS NOT NULL DROP TABLE dbo.KK_AccessPoint;
GO

SELECT OBJECT_ID('dbo.KK_AccessPointLichSu') AS phai_null_1, OBJECT_ID('dbo.KK_AccessPoint') AS phai_null_2;
GO
