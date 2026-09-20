-- Rollback cho kk-ghichu-tu-phieu-20260918.sql: tra lai dung ghi chu cu doc tu DB ngay 18/09/2026.
SET NOCOUNT ON;
SET XACT_ABORT ON;
BEGIN TRAN;

DECLARE @cu TABLE (id_thiet_bi INT PRIMARY KEY, ghi_chu NVARCHAR(MAX) NULL);
INSERT INTO @cu (id_thiet_bi, ghi_chu) VALUES
(343, N'check camera') /* VN-DCV002 */,
(385, NULL) /* VN-LD012 */,
(402, NULL) /* VN-CV006 */,
(433, NULL) /* VN-QA011 */,
(434, NULL) /* VN-QA042 */,
(477, N'Gu Min nghỉ việc trả về kho IT') /* VN-SD101 */,
(519, NULL) /* VN-WD013 */,
(520, NULL) /* VN-WD009 */,
(521, NULL) /* VN-WD011 */,
(522, NULL) /* VN-WD012 */,
(524, N'V240774') /* VN-WD008 */,
(570, NULL) /* VN-PW011 */,
(572, NULL) /* VN-PW005 */,
(573, NULL) /* VN-PW004 */,
(600, NULL) /* VN-PT015 */,
(731, N'REGINA') /* VN-QA025 */,
(735, NULL) /* VN-QA003 */,
(736, NULL) /* VN-QA012 */,
(739, NULL) /* VN-QA037 */,
(909, NULL) /* VN-TL001 */,
(939, NULL) /* VN-LTBD005-1 */,
(978, NULL) /* VN-QA045 */,
(1009, NULL) /* VN-DF019 */,
(1011, N'REGINA-MR BELL') /* VN-QA030 */,
(1013, NULL) /* VN-QA053 */,
(1014, NULL) /* VN-QA009 */,
(1205, NULL) /* VN-QC072 */,
(1567, N'ĐỂ CHECK CAMERA PHÒNG CCTV') /* VN-HR005 */,
(1705, NULL) /* VN-QA052 */,
(1885, NULL) /* VN-GL022 */,
(1900, NULL) /* VN-QA013 */,
(1947, NULL) /* VN-GL010 */,
(2154, NULL) /* VN-WD016 */,
(2158, N'WD Hưng Yên') /* VN-WD019 */,
(2159, N'WD Hưng Yên') /* VN-WD018 */,
(2162, N'Kho IT') /* VN-SD132 */,
(2247, NULL) /* VN-WD006 */,
(2348, NULL) /* N/A */,
(2354, N'đã chuyển cho MR.OW Mega') /*  */,
(2373, NULL) /* VN-PMC032 */;

UPDATE tb SET tb.ghi_chu = c.ghi_chu, tb.ngay_cap_nhat = GETDATE()
FROM KK_ThietBi tb JOIN @cu c ON c.id_thiet_bi = tb.id_thiet_bi;
PRINT N'So dong da tra lai: ' + CAST(@@ROWCOUNT AS varchar);

-- COMMIT;
-- ROLLBACK;

