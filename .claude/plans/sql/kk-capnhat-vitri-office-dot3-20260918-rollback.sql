-- Rollback cho kk-capnhat-vitri-office-dot3-20260918.sql
-- Gia tri cu doc tu DB ngay 18/09/2026 truoc khi chay script dot 2.
-- CHI CHAY NEU chua co ai sua tay cac thiet bi nay sau do.

SET NOCOUNT ON;
SET XACT_ABORT ON;
BEGIN TRAN;

DECLARE @cu TABLE (id_thiet_bi INT PRIMARY KEY, id_vi_tri_dia_ly INT NULL, can_cai_office BIT NULL);
INSERT INTO @cu (id_thiet_bi, id_vi_tri_dia_ly, can_cai_office) VALUES
(342,NULL,0) /* VN-IT086 */,
(343,NULL,0) /* VN-DCV002 */,
(345,NULL,1) /* VN-ITV015 */,
(358,NULL,0) /* VN-IT004 */,
(359,NULL,0) /* VN-IT168 */,
(360,NULL,0) /* VN-IT018 */,
(427,NULL,0) /* VN-MAXHUB-360 */,
(477,NULL,0) /* VN-SD101 */,
(631,NULL,0) /* VN-IT001 */,
(633,NULL,0) /* VN-IT011 */,
(901,NULL,1) /* VN-ITV026 */,
(904,NULL,1) /* VN-ITV010 */,
(905,NULL,1) /* VN-ITV014 */,
(995,NULL,0) /* VN-IT091 */,
(1063,NULL,1) /* VN-ITV018 */,
(1567,NULL,0) /* VN-HR005 */,
(1861,NULL,1) /* VN-ITV011 */,
(1942,NULL,1) /* VN-ITV017 */,
(2080,NULL,1) /* VN-ITV021 */,
(2081,NULL,1) /* VN-ITV013 */,
(2119,NULL,1) /* VN-ITV001 */,
(2120,NULL,1) /* VN-ITV002 */,
(2121,NULL,1) /* VN-ITV004 */,
(2122,NULL,1) /* VN-ITV006 */,
(2123,NULL,1) /* VN-ITV007 */,
(2124,NULL,1) /* VN-ITV012 */,
(2125,NULL,1) /* VN-ITV016 */,
(2126,NULL,1) /* VN-ITV019 */,
(2127,NULL,1) /* VN-ITV022 */,
(2128,NULL,1) /* VN-ITV023 */,
(2129,1,1) /* VN-ITV024 */,
(2130,NULL,1) /* VN-ITV025 */,
(2131,NULL,1) /* VN-ITV027 */,
(2132,NULL,1) /* VN-ITV028 */,
(2133,NULL,1) /* VN-ITV029 */,
(2134,NULL,1) /* VN-ITV030 */,
(2163,NULL,0) /* VN-PUR021 */,
(2178,NULL,0) /* VN-IT088 */,
(2179,NULL,0) /* VN-IT1001 */,
(2180,NULL,0) /* VN-IT090 */,
(2354,NULL,0) /*  */,
(2356,NULL,0) /* N/A */,
(2357,NULL,0) /* N/A */,
(2360,NULL,0) /* N/A */,
(2361,NULL,0) /* N/A */,
(2364,NULL,0) /* GL-SRV */,
(2365,NULL,0) /* YMDC */,
(2373,NULL,0) /* VN-PMC032 */;

UPDATE tb SET tb.id_vi_tri_dia_ly = c.id_vi_tri_dia_ly, tb.can_cai_office = c.can_cai_office,
              tb.ngay_cap_nhat = GETDATE()
FROM KK_ThietBi tb JOIN @cu c ON c.id_thiet_bi = tb.id_thiet_bi;
PRINT N'So dong da tra lai: ' + CAST(@@ROWCOUNT AS varchar);

-- COMMIT;
-- ROLLBACK;


