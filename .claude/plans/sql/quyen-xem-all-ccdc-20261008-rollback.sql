/* Rollback quyen-xem-all-ccdc-20261008.sql: bỏ quyền XemAllCCDC.
   Xoá cả dòng gán User_Quyen của quyền này (bảng gán quyền, không phải bảng LichSu*).
   Chạy: sqlcmd ... -f 65001 */

USE ITForm;
GO

SET NOCOUNT ON;

BEGIN TRAN;

DELETE uq
FROM dbo.User_Quyen uq
JOIN dbo.Quyen q ON q.id_quyen = uq.id_quyen
WHERE q.ten_quyen = N'XemAllCCDC';

DELETE FROM dbo.Quyen WHERE ten_quyen = N'XemAllCCDC';

COMMIT;
GO
