-- Rollback cho kk-vnpur-office-haiduong-20260918.sql (gia tri cu doc tu DB 18/09/2026)
SET NOCOUNT ON;
SET XACT_ABORT ON;
BEGIN TRAN;
DECLARE @cu TABLE (id_thiet_bi INT PRIMARY KEY, id_vi_tri_dia_ly INT NULL, can_cai_office BIT NULL);
INSERT INTO @cu (id_thiet_bi, id_vi_tri_dia_ly, can_cai_office) VALUES
(344,1,0) /* VN-PUR030 */,
(720,NULL,0) /* VN-PUR028 */,
(721,1,0) /* VN-PUR032 */,
(722,NULL,0) /* VN-PUR018 */,
(723,NULL,0) /* VN-PUR019 */,
(724,1,0) /* VN-PUR033 */,
(726,NULL,0) /* VN-PUR001 */,
(730,NULL,0) /* VN-PUR027 */,
(1010,NULL,0) /* VN-PUR040 */,
(1072,NULL,0) /* VN-PUR031 */,
(1075,NULL,NULL) /*  */,
(1076,NULL,NULL) /*  */,
(1077,NULL,NULL) /*  */,
(1079,NULL,NULL) /*  */,
(1080,NULL,NULL) /*  */,
(1081,NULL,NULL) /*  */,
(1082,NULL,NULL) /*  */,
(1083,NULL,NULL) /*  */,
(1084,NULL,NULL) /* VN-PUR027 */,
(1085,NULL,NULL) /*  */,
(1086,NULL,NULL) /*  */,
(1087,NULL,NULL) /*  */;
UPDATE tb SET tb.id_vi_tri_dia_ly = c.id_vi_tri_dia_ly, tb.can_cai_office = c.can_cai_office, tb.ngay_cap_nhat = GETDATE()
FROM KK_ThietBi tb JOIN @cu c ON c.id_thiet_bi = tb.id_thiet_bi;
PRINT N'So dong da tra lai: ' + CAST(@@ROWCOUNT AS varchar);
-- COMMIT;
-- ROLLBACK;

