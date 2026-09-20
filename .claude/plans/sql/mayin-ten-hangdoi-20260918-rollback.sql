-- Rollback cho mayin-ten-hangdoi-20260918.sql: xoa cot ten_hang_doi khoi bang MayIn.
-- Mat ten hang doi da nap, khong anh huong du lieu chi so.
SET XACT_ABORT ON;
IF EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('dbo.MayIn') AND name = 'ten_hang_doi')
    ALTER TABLE dbo.MayIn DROP COLUMN ten_hang_doi;
PRINT N'Da xoa cot ten_hang_doi.';