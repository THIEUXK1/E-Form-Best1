-- Rollback cho kk-vnss-20260919.sql (gia tri cu doc tu DB 19/09/2026)
SET NOCOUNT ON;
SET XACT_ABORT ON;
BEGIN TRAN;
DECLARE @cu TABLE (id_thiet_bi INT PRIMARY KEY, id_vi_tri_dia_ly INT NULL, can_cai_office BIT NULL);
INSERT INTO @cu (id_thiet_bi, id_vi_tri_dia_ly, can_cai_office) VALUES
(481,NULL,0) /* VN-SS049 */,
(482,NULL,0) /* VN-SS064 */,
(483,NULL,0) /* VN-SS065 */,
(486,NULL,0) /* VN-SS43 */,
(487,NULL,0) /* VN-SS057 */,
(488,NULL,0) /* VN-SS043 */,
(497,NULL,0) /* VN-SS056 */,
(498,NULL,0) /* VN-SS034 */,
(499,NULL,0) /* VN-SS059 */,
(503,NULL,0) /* VN-SS061 */,
(832,NULL,0) /* VN-SS011 */,
(835,NULL,0) /* VN-SS066 */,
(836,NULL,0) /* VN-SS058 */,
(837,NULL,0) /* VN-SS074 */,
(839,NULL,0) /* VN-SS003 */,
(840,NULL,0) /* VN-SS081 */,
(841,NULL,0) /* VN-SS060 */,
(842,NULL,0) /* VN-SS047 */,
(933,NULL,0) /* VN-SS063 */,
(990,NULL,0) /* VN-SS062 */,
(1028,NULL,0) /* VN-SS069 */,
(2228,NULL,0) /* VN-SS046 */,
(2350,NULL,0) /* VN-SS42 */;
UPDATE tb SET tb.id_vi_tri_dia_ly = c.id_vi_tri_dia_ly, tb.can_cai_office = c.can_cai_office, tb.ngay_cap_nhat = GETDATE()
FROM KK_ThietBi tb JOIN @cu c ON c.id_thiet_bi = tb.id_thiet_bi;
PRINT N'So dong da tra lai: ' + CAST(@@ROWCOUNT AS varchar);
UPDATE KK_ThietBi SET id_nguoi_dung = 1225, ngay_cap_nhat = GETDATE() WHERE id_thiet_bi = 483;
UPDATE KK_ThietBi SET id_nguoi_dung = 1303, ngay_cap_nhat = GETDATE() WHERE id_thiet_bi = 498;
UPDATE KK_ThietBi SET id_nguoi_dung = 4978, ngay_cap_nhat = GETDATE() WHERE id_thiet_bi = 837;
PRINT N'Da tra lai nguoi su dung cho 3 may.';
-- COMMIT;
-- ROLLBACK;


