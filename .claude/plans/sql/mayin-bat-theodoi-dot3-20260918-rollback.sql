-- Rollback cua mayin-bat-theodoi-dot3-20260918.sql: tat lai doc tu dong cho 10.0.59.128.
-- Khong xoa chi so da doc duoc trong thoi gian bat.

SET NOCOUNT ON;
SET XACT_ABORT ON;

UPDATE dbo.MayIn
SET theo_doi_tu_dong = 0,
    ngay_cap_nhat    = GETDATE()
WHERE dia_chi_ip = '10.0.59.128'
  AND serial     = '506801';

PRINT N'So dong da tat theo doi: ' + CAST(@@ROWCOUNT AS varchar(10));
GO
