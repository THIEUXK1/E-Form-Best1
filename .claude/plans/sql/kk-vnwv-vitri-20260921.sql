-- VN-WV: xac nhan mieng ngay 21/09/2026 - 4 May tinh o Hai Duong va KHONG can cai Office.
-- can_cai_office cua ca 4 hien da la 0 (mac dinh dot 1), nay duoc xac nhan chinh thuc
-- nen van ghi lai 0 kem ngay_tra_loi_office moi de phan biet voi may chua tra loi.
-- Chua bao gom 564 VN-WV035, 562 VN-WV028 - 2 may con lai cua VN-WV van thieu vi tri.
-- Chay lai lan hai an toan. Rollback: kk-vnwv-vitri-20260921-rollback.sql

SET NOCOUNT ON;
SET XACT_ABORT ON;
BEGIN TRAN;

DECLARE @may TABLE (id_thiet_bi INT PRIMARY KEY);
INSERT INTO @may (id_thiet_bi) VALUES
(560) /* VN-WV004 - Nguyen Thi Ly V250792 */,
(566) /* VN-WV005 - Nguyen Thi Ly V250792 */,
(568) /* VN-WV008 - Nguyen Thi Ly V250792 */,
(1427) /* VN-WV009 - Tran Thi Trang V240826 */;

UPDATE tb SET tb.id_vi_tri_dia_ly = 1, tb.can_cai_office = 0,
              tb.ngay_tra_loi_office = GETDATE(), tb.ngay_cap_nhat = GETDATE()
FROM KK_ThietBi tb JOIN @may m ON m.id_thiet_bi = tb.id_thiet_bi
WHERE tb.NgayXoa IS NULL AND (ISNULL(tb.id_vi_tri_dia_ly,-1) <> 1 OR ISNULL(CAST(tb.can_cai_office AS int),-1) <> 0);
PRINT N'Da cap nhat: ' + CAST(@@ROWCOUNT AS varchar);

SELECT COUNT(*) AS TongVNWV,
       SUM(CASE WHEN tb.id_vi_tri_dia_ly IS NULL THEN 1 ELSE 0 END) AS ChuaCoViTri,
       SUM(CASE WHEN tb.id_vi_tri_dia_ly = 1 THEN 1 ELSE 0 END) AS HaiDuong,
       SUM(CASE WHEN tb.can_cai_office = 1 THEN 1 ELSE 0 END) AS CanOffice
FROM KK_ThietBi tb JOIN KK_BoPhan bp ON bp.IDBoPhan = tb.IDBoPhan
WHERE bp.TenBoPhan = N'VN-WV' AND tb.NgayXoa IS NULL;

-- COMMIT;
-- ROLLBACK;

