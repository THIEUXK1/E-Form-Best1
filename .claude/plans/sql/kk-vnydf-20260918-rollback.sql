-- Rollback cho kk-vnydf-20260918.sql (gia tri cu doc tu DB 18/09/2026: ca 4 may deu chua co vi tri, can_cai_office = 0)
SET NOCOUNT ON;
SET XACT_ABORT ON;
BEGIN TRAN;
UPDATE KK_ThietBi SET id_vi_tri_dia_ly = NULL, can_cai_office = 0, ngay_cap_nhat = GETDATE()
WHERE id_thiet_bi IN (1919,1923,2114,2353);
PRINT N'So dong da tra lai: ' + CAST(@@ROWCOUNT AS varchar);
-- COMMIT;
-- ROLLBACK;

