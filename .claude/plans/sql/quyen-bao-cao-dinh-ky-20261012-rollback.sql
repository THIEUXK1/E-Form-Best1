/* Rollback quyen-bao-cao-dinh-ky-20261012.sql: gỡ quyền "BaoCaoDinhKy" khỏi mọi user rồi xoá quyền.
   Lưu ý chạy: sqlcmd phải thêm -f 65001. */

USE ITForm;
GO

SET NOCOUNT ON;
SET XACT_ABORT ON;

BEGIN TRAN;

DELETE uq
FROM dbo.User_Quyen uq
JOIN dbo.Quyen q ON q.id_quyen = uq.id_quyen
WHERE q.ten_quyen = N'BaoCaoDinhKy';
PRINT CONCAT('User_Quyen đã gỡ: ', @@ROWCOUNT);

DELETE FROM dbo.Quyen WHERE ten_quyen = N'BaoCaoDinhKy';
PRINT CONCAT('Quyen đã xoá: ', @@ROWCOUNT);

COMMIT;
GO
