-- Rollback cho kk-wkhy-ghichu-nguoidung-20260918.sql (gia tri cu doc tu DB 18/09/2026)
SET NOCOUNT ON;
SET XACT_ABORT ON;
BEGIN TRAN;
DECLARE @cu TABLE (id_thiet_bi INT PRIMARY KEY, id_nguoi_dung INT NULL, ghi_chu NVARCHAR(MAX) NULL);
INSERT INTO @cu (id_thiet_bi, id_nguoi_dung, ghi_chu) VALUES
(340,2799,NULL),
(1293,2851,NULL),
(1353,2851,NULL),
(1773,2799,NULL),
(1821,2851,NULL),
(1825,2850,NULL),
(1838,2850,NULL),
(2243,2799,NULL),
(2371,2799,NULL);
UPDATE tb SET tb.id_nguoi_dung = c.id_nguoi_dung, tb.ghi_chu = c.ghi_chu, tb.ngay_cap_nhat = GETDATE()
FROM KK_ThietBi tb JOIN @cu c ON c.id_thiet_bi = tb.id_thiet_bi;
PRINT N'So dong da tra lai: ' + CAST(@@ROWCOUNT AS varchar);
-- COMMIT;
-- ROLLBACK;

