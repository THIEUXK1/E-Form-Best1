-- Rollback cho kk-vnss-20260919-dot3.sql (gia tri cu doc tu DB 19/09/2026)
SET NOCOUNT ON;
SET XACT_ABORT ON;
BEGIN TRAN;
DECLARE @cu TABLE (id_thiet_bi INT PRIMARY KEY, id_vi_tri_dia_ly INT NULL, can_cai_office BIT NULL);
INSERT INTO @cu (id_thiet_bi, id_vi_tri_dia_ly, can_cai_office) VALUES
(2228,1,1) /* VN-SS046 */,
(2274,1,0) /* VN-SS051 */;
UPDATE tb SET tb.id_vi_tri_dia_ly = c.id_vi_tri_dia_ly, tb.can_cai_office = c.can_cai_office, tb.ngay_cap_nhat = GETDATE()
FROM KK_ThietBi tb JOIN @cu c ON c.id_thiet_bi = tb.id_thiet_bi;
PRINT N'So dong da tra lai: ' + CAST(@@ROWCOUNT AS varchar);
-- COMMIT;
-- ROLLBACK;

