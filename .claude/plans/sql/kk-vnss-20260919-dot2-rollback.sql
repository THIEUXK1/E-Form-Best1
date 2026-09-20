-- Rollback cho kk-vnss-20260919-dot2.sql (gia tri cu doc tu DB 19/09/2026)
SET NOCOUNT ON;
SET XACT_ABORT ON;
BEGIN TRAN;
DECLARE @cu TABLE (id_thiet_bi INT PRIMARY KEY, id_vi_tri_dia_ly INT NULL, can_cai_office BIT NULL);
INSERT INTO @cu (id_thiet_bi, id_vi_tri_dia_ly, can_cai_office) VALUES
(484,NULL,0) /* VN-SS037 */,
(485,NULL,0) /* VN-SS022 */,
(489,NULL,0) /* VN-SS075 */,
(490,NULL,0) /* VN-SS025 */,
(491,NULL,0) /* VN-SS078 */,
(492,NULL,0) /* VN-SS079 */,
(493,NULL,0) /* VN-SS052 */,
(494,NULL,0) /* VN-SS080 */,
(495,NULL,0) /* VN-SS053 */,
(496,NULL,0) /* VN-SS076 */,
(500,NULL,0) /* VN-SS029 */,
(501,NULL,0) /* VN-SS077 */,
(502,NULL,0) /* VN-SS055 */,
(504,NULL,0) /* VN-SS033 */,
(505,NULL,0) /* VN-SS031 */,
(833,NULL,0) /* VN-SS054 */,
(834,NULL,0) /* VN-SS008 */,
(838,NULL,0) /* VN-SS048 */,
(843,NULL,0) /* VN-SS030-1 */,
(1027,NULL,0) /* VN-SS021 */,
(1029,NULL,0) /* VN-SS073 */,
(1030,NULL,0) /* VN-SS068 */,
(1468,NULL,0) /* VN-SS072 */,
(1639,NULL,0) /* VN-SS020 */,
(1738,NULL,0) /* VN-SS026 */,
(1764,NULL,0) /* VN-SS071 */,
(2228,3,1) /* VN-SS046 */,
(2274,NULL,0) /* VN-SS051 */;
UPDATE tb SET tb.id_vi_tri_dia_ly = c.id_vi_tri_dia_ly, tb.can_cai_office = c.can_cai_office, tb.ngay_cap_nhat = GETDATE()
FROM KK_ThietBi tb JOIN @cu c ON c.id_thiet_bi = tb.id_thiet_bi;
PRINT N'So dong da tra lai: ' + CAST(@@ROWCOUNT AS varchar);
-- COMMIT;
-- ROLLBACK;

