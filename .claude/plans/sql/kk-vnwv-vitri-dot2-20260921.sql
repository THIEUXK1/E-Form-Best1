-- VN-WV dot 2: xac nhan mieng ngay 21/09/2026 - 2 May tinh con lai o Hai Duong va KHONG can cai Office.
-- Ghi lai can_cai_office = 0 kem ngay_tra_loi_office moi de phan biet voi may chua ai tra loi.
-- Sau dot nay VN-WV het may tinh/laptop thieu vi tri.
-- Chay lai lan hai an toan. Rollback: kk-vnwv-vitri-dot2-20260921-rollback.sql

SET NOCOUNT ON;
SET XACT_ABORT ON;
BEGIN TRAN;

DECLARE @may TABLE (id_thiet_bi INT PRIMARY KEY);
INSERT INTO @may (id_thiet_bi) VALUES
(562) /* VN-WV028 - Do Thi Thoa V240766 */,
(564) /* VN-WV035 - Nguyen Thi Ly V250792 */;

UPDATE tb SET tb.id_vi_tri_dia_ly = 1, tb.can_cai_office = 0,
              tb.ngay_tra_loi_office = GETDATE(), tb.ngay_cap_nhat = GETDATE()
FROM KK_ThietBi tb JOIN @may m ON m.id_thiet_bi = tb.id_thiet_bi
WHERE tb.NgayXoa IS NULL AND (ISNULL(tb.id_vi_tri_dia_ly,-1) <> 1 OR ISNULL(CAST(tb.can_cai_office AS int),-1) <> 0);
PRINT N'Da cap nhat: ' + CAST(@@ROWCOUNT AS varchar);

SELECT COUNT(*) AS TongVNWV,
       SUM(CASE WHEN tb.id_vi_tri_dia_ly IS NULL THEN 1 ELSE 0 END) AS ChuaCoViTri,
       SUM(CASE WHEN tb.id_vi_tri_dia_ly = 1 THEN 1 ELSE 0 END) AS HaiDuong
FROM KK_ThietBi tb JOIN KK_BoPhan bp ON bp.IDBoPhan = tb.IDBoPhan
WHERE bp.TenBoPhan = N'VN-WV' AND tb.NgayXoa IS NULL;

-- COMMIT;
-- ROLLBACK;

