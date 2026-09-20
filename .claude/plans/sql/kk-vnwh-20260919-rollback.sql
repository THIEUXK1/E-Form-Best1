-- Rollback cho kk-vnwh-20260919.sql (gia tri cu doc tu DB 19/09/2026)
SET NOCOUNT ON;
SET XACT_ABORT ON;
BEGIN TRAN;
DECLARE @cu TABLE (id_thiet_bi INT PRIMARY KEY, id_vi_tri_dia_ly INT NULL, can_cai_office BIT NULL);
INSERT INTO @cu (id_thiet_bi, id_vi_tri_dia_ly, can_cai_office) VALUES
(205,NULL,0) /* VN-WH047 */,
(527,NULL,0) /* VN-WH010 */,
(528,NULL,0) /* VN-WH012 */,
(530,NULL,0) /* VN-WH004 */,
(531,NULL,0) /* VN-WH036 */,
(532,NULL,0) /* VN-WH037 */,
(534,NULL,0) /* VN-WH038 */,
(535,NULL,0) /* VN-WH021 */,
(536,NULL,0) /* VN-WH040 */,
(537,NULL,0) /* VN-WH029 */,
(538,NULL,0) /* VN-WH009 */,
(853,1,0) /* VN-WH002 */,
(854,NULL,0) /* VN-WH005 */,
(936,NULL,0) /* VN-WH056 */,
(1049,NULL,0) /* VN-WH055 */,
(1050,NULL,0) /* VN-WH027 */,
(1051,1,1) /* VN-WH039 */,
(1092,NULL,0) /* VN-WH051 */,
(1549,NULL,0) /* VN-WH035 */,
(1559,1,1) /* VN-WH049 */,
(1566,NULL,0) /* VN-WH001 */,
(1569,3,0) /* VN-WH062 */,
(1637,1,1) /* VN-WH043 */,
(1649,1,1) /* VN-WH048 */,
(1716,NULL,0) /* VN-WH060 */,
(1793,1,1) /* VN-WH057 */,
(1862,NULL,0) /* VN-WH019 */,
(2176,NULL,0) /* VN-WH033 */,
(2177,NULL,0) /* VN-WH031 */,
(2280,NULL,0) /* VN-WH061 */;
UPDATE tb SET tb.id_vi_tri_dia_ly = c.id_vi_tri_dia_ly, tb.can_cai_office = c.can_cai_office, tb.ngay_cap_nhat = GETDATE()
FROM KK_ThietBi tb JOIN @cu c ON c.id_thiet_bi = tb.id_thiet_bi;
PRINT N'So dong da tra lai: ' + CAST(@@ROWCOUNT AS varchar);
-- COMMIT;
-- ROLLBACK;

