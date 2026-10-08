/* Rollback quyen-camera-cong-ty-20261007.sql: bỏ 3 quyền camera theo công ty.
   Xoá cả dòng gán User_Quyen của 3 quyền này (bảng gán quyền, không phải bảng LichSu*).
   Chạy: sqlcmd ... -f 65001 */

USE ITForm;
GO

SET NOCOUNT ON;

BEGIN TRAN;

DELETE uq
FROM dbo.User_Quyen uq
JOIN dbo.Quyen q ON q.id_quyen = uq.id_quyen
WHERE q.ten_quyen IN (N'CamBPVN', N'CamPFVN', N'CamMEGA');

DELETE FROM dbo.Quyen WHERE ten_quyen IN (N'CamBPVN', N'CamPFVN', N'CamMEGA');

COMMIT;
GO
