/* Rollback xoa-ccdc-test-20261008.sql: khôi phục 6 CCDC test đã xoá mềm.
   Chạy: sqlcmd ... -f 65001 */

USE ITForm;
GO

SET NOCOUNT ON;

BEGIN TRAN;

UPDATE dbo.KK_CongCuDungCu
SET ngay_xoa = NULL,
    ly_do_xoa = NULL
WHERE id_ccdc BETWEEN 7 AND 12
  AND nguoi_tao = N'Script test'
  AND ly_do_xoa = N'Dữ liệu test';

COMMIT;
GO
