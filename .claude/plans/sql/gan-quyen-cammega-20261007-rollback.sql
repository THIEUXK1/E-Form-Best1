/* Rollback gan-quyen-cammega-20261007.sql: gỡ CamMEGA của đúng 5 tài khoản đã gán (V170192 giữ nguyên). */

USE ITForm;
GO

SET NOCOUNT ON;

BEGIN TRAN;

DELETE uq
FROM dbo.User_Quyen uq
JOIN dbo.[User] u ON u.id_nguoi_dung = uq.id_nguoi_dung
JOIN dbo.Quyen q ON q.id_quyen = uq.id_quyen
WHERE q.ten_quyen = N'CamMEGA'
  AND u.ma_nhan_vien IN (N'M250813', N'V250813', N'M260001', N'V260154', N'M250785');

PRINT CONCAT('Da go: ', @@ROWCOUNT);

COMMIT;
GO
