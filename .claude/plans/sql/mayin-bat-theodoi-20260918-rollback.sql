-- Rollback cho mayin-bat-theodoi-20260918.sql: tat doc tu dong dung 28 dong da bat.
-- Khong dung cac may von da bat tu truoc.
SET NOCOUNT ON;
SET XACT_ABORT ON;

UPDATE dbo.MayIn
SET    theo_doi_tu_dong = 0,
       ngay_cap_nhat = GETDATE()
WHERE  id_may_in IN (110,19,67,69,84,89,90,
                     1,102,111,13,17,2,22,25,30,32,34,38,40,42,49,65,74,75,83,9,97);

PRINT N'So may vua tat doc tu dong: ' + CAST(@@ROWCOUNT AS varchar);
SELECT COUNT(*) AS may_doc_tu_dong FROM dbo.MayIn WHERE theo_doi_tu_dong = 1;
