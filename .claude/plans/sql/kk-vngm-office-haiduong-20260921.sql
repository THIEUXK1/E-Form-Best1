-- VN-GM: bo phan xac nhan mieng ngay 21/09/2026 - 2 Laptop deu o Hai Duong va deu can cai Office.
-- Truoc do ca 2 may chua co vi tri va dang bi ghi can_cai_office = 0 theo mac dinh dot 1 (chua tra loi phieu).
-- Thiet bi con lai cua VN-GM (2388 - Tivi) khong thuoc pham vi cau hoi nay, khong dong toi.
-- Chay lai lan hai an toan. Rollback: kk-vngm-office-haiduong-20260921-rollback.sql

SET NOCOUNT ON;
SET XACT_ABORT ON;
BEGIN TRAN;

DECLARE @may TABLE (id_thiet_bi INT PRIMARY KEY);
INSERT INTO @may (id_thiet_bi) VALUES
(719) /* VN-GM029 - Bui Thi Hong Nhung V160008 */,
(2161) /* VN-GM030 - Nguyen Van Bach V260766 */;

UPDATE tb SET tb.id_vi_tri_dia_ly = 1, tb.can_cai_office = 1,
              tb.ngay_tra_loi_office = GETDATE(), tb.ngay_cap_nhat = GETDATE()
FROM KK_ThietBi tb JOIN @may m ON m.id_thiet_bi = tb.id_thiet_bi
WHERE tb.NgayXoa IS NULL AND (ISNULL(tb.id_vi_tri_dia_ly,-1) <> 1 OR ISNULL(CAST(tb.can_cai_office AS int),-1) <> 1);
PRINT N'Da cap nhat: ' + CAST(@@ROWCOUNT AS varchar);

SELECT COUNT(*) AS TongVNGM,
       SUM(CASE WHEN tb.id_vi_tri_dia_ly IS NULL THEN 1 ELSE 0 END) AS ChuaCoViTri,
       SUM(CASE WHEN tb.id_vi_tri_dia_ly = 1 THEN 1 ELSE 0 END) AS HaiDuong,
       SUM(CASE WHEN tb.can_cai_office = 1 THEN 1 ELSE 0 END) AS CanOffice
FROM KK_ThietBi tb JOIN KK_BoPhan bp ON bp.IDBoPhan = tb.IDBoPhan
WHERE bp.TenBoPhan = N'VN-GM' AND tb.NgayXoa IS NULL;

-- COMMIT;
-- ROLLBACK;

