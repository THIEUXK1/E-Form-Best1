-- Rollback cho kk-vnit-vitri-haiduong-20260918.sql: ca 20 may truoc do deu chua co vi tri (NULL).
SET NOCOUNT ON;
SET XACT_ABORT ON;
BEGIN TRAN;
UPDATE KK_ThietBi SET id_vi_tri_dia_ly = NULL, ngay_cap_nhat = GETDATE()
WHERE id_thiet_bi BETWEEN 2468 AND 2487;
PRINT N'So dong da tra lai: ' + CAST(@@ROWCOUNT AS varchar);
-- COMMIT;
-- ROLLBACK;

