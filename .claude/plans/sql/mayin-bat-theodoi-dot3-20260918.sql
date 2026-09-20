-- Bat doc tu dong cho may 10.0.59.128 (Apeos C3570, serial 506801).
--
-- Chay tren: 10.0.60.33 / ITForm (PRODUCTION). Chi UPDATE 1 dong, khong dung du lieu chi so.
-- Rollback: mayin-bat-theodoi-dot3-20260918-rollback.sql
-- Chay bang: sqlcmd -S 10.0.60.33 -U sa -P *** -d ITForm -C -f 65001 -i mayin-bat-theodoi-dot3-20260918.sql
--
-- LY DO:
--   Truoc day may nay bi xep vao nhom "ping duoc nhung khong doc duoc". Do thuc te 18/09/2026:
--   may CO /home/api/billing-counter day du (PRINT 718.420 / COPY 30.430) nhung CHI mo tren
--   cong 80 (http), trong khi code chi goi https nen nhan 404 va bo qua.
--   Da sua MayInApiService/MayInQuetService de thu https truoc roi ha xuong http.
--   Bat theo doi cho may nay => so may doc tu dong: 57 -> 58.

SET NOCOUNT ON;
SET XACT_ABORT ON;

UPDATE dbo.MayIn
SET theo_doi_tu_dong = 1,
    ngay_cap_nhat    = GETDATE()
WHERE dia_chi_ip = '10.0.59.128'
  AND serial     = '506801'
  AND theo_doi_tu_dong = 0;

PRINT N'So dong duoc bat theo doi: ' + CAST(@@ROWCOUNT AS varchar(10));
GO

-- Doi soat
SELECT id_may_in, model, serial, dia_chi_ip, theo_doi_tu_dong
FROM dbo.MayIn
WHERE dia_chi_ip = '10.0.59.128';

SELECT COUNT(*) AS tong_may_doc_tu_dong
FROM dbo.MayIn
WHERE theo_doi_tu_dong = 1;
GO
