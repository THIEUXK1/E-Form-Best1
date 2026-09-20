-- Rollback cho kk-wkna-bosung-20260917.sql
-- Tra lai bo phan va gia tri vi tri / can_cai_office nhu truoc khi chay script do (doc tu DB 17/09/2026).
-- CHI CHAY NEU chua co ai sua tay cac thiet bi nay sau do.
-- Luu y: bo phan VN-WK NA KHONG bi xoa (co the da duoc dung cho viec khac); neu chac chan muon xoa
-- thi bo chu thich khoi cuoi cung.

SET NOCOUNT ON;
SET XACT_ABORT ON;
BEGIN TRAN;

-- 1) Tra bo phan cu: 8 may ve VN-WK (IDBoPhan = 22), 4 may ve VN-WK S (IDBoPhan = 1049)
UPDATE KK_ThietBi SET IDBoPhan = 22, ngay_cap_nhat = GETDATE()
WHERE id_thiet_bi IN (178, 181, 182, 188, 192, 194, 196, 2367);

UPDATE KK_ThietBi SET IDBoPhan = 1049, ngay_cap_nhat = GETDATE()
WHERE id_thiet_bi IN (186, 191, 201, 203);

-- 2) Tra lai vi tri / can_cai_office
DECLARE @cu TABLE (id_thiet_bi INT PRIMARY KEY, id_vi_tri_dia_ly INT NULL, can_cai_office BIT NULL);
INSERT INTO @cu (id_thiet_bi, id_vi_tri_dia_ly, can_cai_office) VALUES
(186, NULL, 0),  /* VN-WK070 */
(191, NULL, 0),  /* VN-WK046 */
(201, NULL, 0),  /* VN-WK071 */
(203, NULL, 0),  /* VN-WK048 */
(188, 2, 1),     /* VN-WK068 */
(548, 1, 1),     /* VN-WK014 */
(550, 1, 1),     /* VN-WK054 */
(552, 1, 1),     /* VN-WK033 */
(857, 1, 1),     /* VN-WK012 */
(1089, 1, 1),    /* VN-WK061 */
(1404, 1, 1);    /* VN-WK060 */

UPDATE tb
SET tb.id_vi_tri_dia_ly = c.id_vi_tri_dia_ly,
    tb.can_cai_office = c.can_cai_office,
    tb.ngay_cap_nhat = GETDATE()
FROM KK_ThietBi tb JOIN @cu c ON c.id_thiet_bi = tb.id_thiet_bi;
PRINT N'So dong da tra lai: ' + CAST(@@ROWCOUNT AS varchar);

-- 3) Xoa bo phan VN-WK NA (chi khi khong con thiet bi nao tro toi)
-- DELETE FROM KK_BoPhan
-- WHERE TenBoPhan = N'VN-WK NA'
--   AND NOT EXISTS (SELECT 1 FROM KK_ThietBi t WHERE t.IDBoPhan = KK_BoPhan.IDBoPhan);

COMMIT;
