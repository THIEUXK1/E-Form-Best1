-- Bat doc chi so tu dong cho 2 may vua tim duoc duong doc khac (18/09/2026).
--
-- Chay tren: 10.0.60.33 / ITForm (PRODUCTION). Cham DUY NHAT cot theo_doi_tu_dong cua bang MayIn.
-- Rollback: mayin-bat-theodoi-dot2-20260918-rollback.sql
--
--   id 48  10.0.59.142 FX3205 101018    -> CentreWare nhung may nay CHI mo HTTPS (cong 80 khong tra loi),
--                                          doc duoc https://10.0.59.142/prcnt.htm = 28.445 trang.
--   id 18  10.0.59.10  C325 DW TR7-004226 -> khong co web API lan CentreWare, nhung tra loi PJL cong 9100:
--                                          @PJL INFO PAGECOUNT = 5.137; INFO ID = "FUJIFILM ApeosPrint C325/328 dw".
--
-- Ca hai duong nay da duoc bo sung vao MayInApiService (thu lan luot: /home/api -> CentreWare http
-- -> CentreWare https -> PJL 9100).

SET NOCOUNT ON;
SET XACT_ABORT ON;

UPDATE dbo.MayIn
SET    theo_doi_tu_dong = 1,
       ngay_cap_nhat = GETDATE()
WHERE  id_may_in IN (48, 18)
  AND  theo_doi_tu_dong = 0;

PRINT N'So may vua bat doc tu dong: ' + CAST(@@ROWCOUNT AS varchar);
GO

-- Doi soat (ky vong: 57 may doc tu dong)
SELECT COUNT(*) AS may_doc_tu_dong FROM dbo.MayIn WHERE theo_doi_tu_dong = 1;
