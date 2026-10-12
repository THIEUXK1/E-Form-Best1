/* ============================================================================
   Rollback cho kk-switch-20261008.sql — xoá KK_SwitchLichSu và KK_Switch.
   CẢNH BÁO: mất toàn bộ danh sách switch và lịch sử đổi trạng thái. Backup trước:
       SELECT * INTO dbo.KK_Switch_bak_YYYYMMDD FROM dbo.KK_Switch;
       SELECT * INTO dbo.KK_SwitchLichSu_bak_YYYYMMDD FROM dbo.KK_SwitchLichSu;
   Trước khi chạy: gỡ code QLSwitch + SwitchPingWorker, không thì trang /QLSwitch và job nền báo lỗi.
   ========================================================================== */

USE ITForm;
GO

SELECT (SELECT COUNT(*) FROM dbo.KK_Switch) AS switch_se_mat,
       (SELECT COUNT(*) FROM dbo.KK_SwitchLichSu) AS su_kien_se_mat;
GO

-- Bảng lịch sử có khoá ngoại sang KK_Switch nên xoá trước
IF OBJECT_ID('dbo.KK_SwitchLichSu') IS NOT NULL DROP TABLE dbo.KK_SwitchLichSu;
IF OBJECT_ID('dbo.KK_Switch') IS NOT NULL DROP TABLE dbo.KK_Switch;
GO

SELECT OBJECT_ID('dbo.KK_SwitchLichSu') AS phai_null_1, OBJECT_ID('dbo.KK_Switch') AS phai_null_2;
GO
