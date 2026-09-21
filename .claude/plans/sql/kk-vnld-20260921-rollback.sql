-- Rollback cho kk-vnld-20260921.sql (gia tri cu doc tu DB 19/09/2026)
SET NOCOUNT ON;
SET XACT_ABORT ON;
BEGIN TRAN;
DECLARE @cu TABLE (id_thiet_bi INT PRIMARY KEY, id_vi_tri_dia_ly INT NULL, can_cai_office BIT NULL);
INSERT INTO @cu (id_thiet_bi, id_vi_tri_dia_ly, can_cai_office) VALUES
(376,1,0) /* VN-LD022 */,
(1924,NULL,0) /* VN-LD031 */,
(2151,NULL,0) /* LD-COPOWER 2 */,
(2207,NULL,0) /* VN-LD061 */,
(2210,NULL,0) /* LD-COPOWER */,
(2213,NULL,0) /* DOSORAMA */,
(2215,NULL,0) /* DOSORAMA 2 */,
(2217,NULL,0) /* DOSORAMA 3 */;
UPDATE tb SET tb.id_vi_tri_dia_ly = c.id_vi_tri_dia_ly, tb.can_cai_office = c.can_cai_office, tb.ngay_cap_nhat = GETDATE()
FROM KK_ThietBi tb JOIN @cu c ON c.id_thiet_bi = tb.id_thiet_bi;
PRINT N'So dong da tra lai: ' + CAST(@@ROWCOUNT AS varchar);
DECLARE @gccu TABLE (id_thiet_bi INT PRIMARY KEY, ghi_chu NVARCHAR(MAX) NULL);
INSERT INTO @gccu (id_thiet_bi, ghi_chu) VALUES
(397, NULL),
(1058, N'chưa biết chính xác người sử dụng'),
(1924, NULL),
(2151, N'hệ thống CoPower'),
(2207, NULL),
(2210, NULL),
(2213, NULL),
(2215, NULL),
(2217, NULL);
UPDATE tb SET tb.ghi_chu = c.ghi_chu FROM KK_ThietBi tb JOIN @gccu c ON c.id_thiet_bi = tb.id_thiet_bi;
PRINT N'Da tra lai ghi chu: ' + CAST(@@ROWCOUNT AS varchar);
-- COMMIT;
-- ROLLBACK;



