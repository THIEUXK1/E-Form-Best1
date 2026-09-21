-- Rollback cho kk-vnwv-vitri-20260921.sql
-- Gia tri cu doc tu DB 21/09/2026: ca 4 may deu id_vi_tri_dia_ly = NULL, can_cai_office = 0.
SET NOCOUNT ON;
SET XACT_ABORT ON;
BEGIN TRAN;
UPDATE KK_ThietBi SET id_vi_tri_dia_ly = NULL, can_cai_office = 0, ngay_cap_nhat = GETDATE()
WHERE id_thiet_bi IN (560,566,568,1427);
PRINT N'So dong da tra lai: ' + CAST(@@ROWCOUNT AS varchar);
-- COMMIT;
-- ROLLBACK;

