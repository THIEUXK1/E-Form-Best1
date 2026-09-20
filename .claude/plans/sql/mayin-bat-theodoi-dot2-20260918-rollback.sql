-- Rollback cho mayin-bat-theodoi-dot2-20260918.sql: tat doc tu dong cua 2 may do.
SET NOCOUNT ON;
UPDATE dbo.MayIn SET theo_doi_tu_dong = 0, ngay_cap_nhat = GETDATE() WHERE id_may_in IN (48, 18);
PRINT N'So may vua tat: ' + CAST(@@ROWCOUNT AS varchar);
SELECT COUNT(*) AS may_doc_tu_dong FROM dbo.MayIn WHERE theo_doi_tu_dong = 1;
