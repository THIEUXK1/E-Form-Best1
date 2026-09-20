-- Rollback cho kk-vndh-20260919.sql (gia tri cu doc tu DB 19/09/2026)
SET NOCOUNT ON;
SET XACT_ABORT ON;
BEGIN TRAN;
DECLARE @cu TABLE (id_thiet_bi INT PRIMARY KEY, id_vi_tri_dia_ly INT NULL, can_cai_office BIT NULL);
INSERT INTO @cu (id_thiet_bi, id_vi_tri_dia_ly, can_cai_office) VALUES
(413,NULL,0) /* VN-DH021 */,
(414,NULL,0) /* VN-DH013 */,
(415,NULL,0) /* VN-DH038 */,
(416,NULL,0) /* VN-DH005 */,
(417,NULL,0) /* VN-DH006 */,
(418,NULL,0) /* VN-DH012 */,
(419,NULL,0) /* VN-DH031 */,
(420,NULL,0) /* VN-DH032 */,
(421,NULL,0) /* VN-DH024 */,
(422,NULL,0) /* VN-DH026 */,
(423,NULL,0) /* VN-DH011 */,
(701,NULL,0) /* VN-DH040 */,
(702,NULL,0) /* VN-DH020 */,
(703,NULL,0) /* VN-DH022 */,
(704,NULL,0) /* VN-DH007 */,
(705,NULL,0) /* VN-DH010 */,
(706,NULL,0) /* VN-DH008 */,
(707,NULL,0) /* VN-DH002 */,
(708,NULL,0) /* VN-DH034 */,
(709,NULL,0) /* VN-DH003 */,
(911,NULL,0) /* VN-DH023 */,
(921,NULL,0) /* VN-DH025 */,
(1066,NULL,0) /* VN-DH037 */,
(1918,NULL,0) /* SM-4 */,
(1920,NULL,0) /* SM-3 */,
(1921,NULL,0) /* DH02 */,
(1963,NULL,0) /* TRS */,
(1964,NULL,0) /* DAPG */,
(2116,NULL,0) /*  */,
(2118,NULL,0) /* VN-DH035 */,
(2141,NULL,0) /* VN-DH041 */,
(2344,NULL,0) /* CÂN NHÀ KÍNH */;
UPDATE tb SET tb.id_vi_tri_dia_ly = c.id_vi_tri_dia_ly, tb.can_cai_office = c.can_cai_office, tb.ngay_cap_nhat = GETDATE()
FROM KK_ThietBi tb JOIN @cu c ON c.id_thiet_bi = tb.id_thiet_bi;
PRINT N'So dong da tra lai: ' + CAST(@@ROWCOUNT AS varchar);
-- COMMIT;
-- ROLLBACK;

