-- Rollback cho kk-vneg-20260918.sql (gia tri cu doc tu DB 18/09/2026)
SET NOCOUNT ON;
SET XACT_ABORT ON;
BEGIN TRAN;
DECLARE @cu TABLE (id_thiet_bi INT PRIMARY KEY, id_vi_tri_dia_ly INT NULL, can_cai_office BIT NULL, id_nguoi_dung INT NULL);
INSERT INTO @cu (id_thiet_bi, id_vi_tri_dia_ly, can_cai_office, id_nguoi_dung) VALUES
(209,NULL,0,359),
(213,NULL,0,2467),
(424,NULL,0,379),
(425,NULL,0,383),
(426,NULL,0,355),
(715,NULL,0,355),
(716,NULL,0,2378),
(717,NULL,0,356),
(718,NULL,0,4911),
(826,NULL,0,367),
(922,NULL,0,2381),
(923,NULL,0,354),
(940,NULL,0,5384),
(973,NULL,0,5673),
(974,NULL,0,6601),
(1090,NULL,0,5679),
(2185,NULL,0,5384),
(2193,NULL,0,379),
(2320,NULL,0,383),
(2342,NULL,0,383);
UPDATE tb SET tb.id_vi_tri_dia_ly = c.id_vi_tri_dia_ly, tb.can_cai_office = c.can_cai_office,
              tb.id_nguoi_dung = c.id_nguoi_dung, tb.ngay_cap_nhat = GETDATE()
FROM KK_ThietBi tb JOIN @cu c ON c.id_thiet_bi = tb.id_thiet_bi;
PRINT N'So dong da tra lai: ' + CAST(@@ROWCOUNT AS varchar);
-- COMMIT;
-- ROLLBACK;

