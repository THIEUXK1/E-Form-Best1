-- Rollback cho kk-vnac-20260918.sql (gia tri cu doc tu DB 18/09/2026)
SET NOCOUNT ON;
SET XACT_ABORT ON;
BEGIN TRAN;
UPDATE KK_ThietBi SET id_vi_tri_dia_ly = NULL, can_cai_office = 0, ngay_cap_nhat = GETDATE()
WHERE id_thiet_bi IN (634,912,917,967,1887);
PRINT N'Da tra lai vi tri/Office: ' + CAST(@@ROWCOUNT AS varchar);
UPDATE KK_ThietBi SET id_nguoi_dung = 2252, ngay_cap_nhat = GETDATE() WHERE id_thiet_bi = 912;
PRINT N'Da tra lai nguoi su dung: ' + CAST(@@ROWCOUNT AS varchar);
-- COMMIT;
-- ROLLBACK;

