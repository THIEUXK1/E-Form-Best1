-- Phieu 'WH.xlsx' (VN-WH, luu 19/09/2026 10:47).
-- 30 thiet bi con lech so voi DB -> ghi vi tri dia ly + can_cai_office theo phieu.
-- Khong dong nao co nguoi tra loi tren web sau khi file duoc luu, nen ghi de toan bo phan lech.
-- Cot Nguoi su dung cua phieu trung khop DB; cot Ghi chu de trong -> khong dong vao ghi_chu.
-- Chay lai lan hai an toan. Rollback: kk-vnwh-20260919-rollback.sql

SET NOCOUNT ON;
SET XACT_ABORT ON;
BEGIN TRAN;

DECLARE @p TABLE (id_thiet_bi INT PRIMARY KEY, id_vi_tri_dia_ly INT, can_cai_office BIT NULL);
INSERT INTO @p (id_thiet_bi, id_vi_tri_dia_ly, can_cai_office) VALUES
(205, 2, 0) /* VN-WH047 - Nghe An */,
(527, 1, 1) /* VN-WH010 - Hai Duong */,
(528, 1, 1) /* VN-WH012 - Hai Duong */,
(530, 1, 0) /* VN-WH004 - Hai Duong */,
(531, 1, 0) /* VN-WH036 - Hai Duong */,
(532, 1, 0) /* VN-WH037 - Hai Duong */,
(534, 1, 0) /* VN-WH038 - Hai Duong */,
(535, 1, 0) /* VN-WH021 - Hai Duong */,
(536, 1, 0) /* VN-WH040 - Hai Duong */,
(537, 1, 0) /* VN-WH029 - Hai Duong */,
(538, 1, 1) /* VN-WH009 - Hai Duong */,
(853, 1, 1) /* VN-WH002 - Hai Duong */,
(854, 1, 1) /* VN-WH005 - Hai Duong */,
(936, 1, 1) /* VN-WH056 - Hai Duong */,
(1049, 1, 0) /* VN-WH055 - Hai Duong */,
(1050, 1, 0) /* VN-WH027 - Hai Duong */,
(1051, 1, 0) /* VN-WH039 - Hai Duong */,
(1092, 1, 0) /* VN-WH051 - Hai Duong */,
(1549, 1, 1) /* VN-WH035 - Hai Duong */,
(1559, 1, 0) /* VN-WH049 - Hai Duong */,
(1566, 1, 1) /* VN-WH001 - Hai Duong */,
(1569, 3, 1) /* VN-WH062 - Hung Yen */,
(1637, 1, 0) /* VN-WH043 - Hai Duong */,
(1649, 3, 0) /* VN-WH048 - Hung Yen */,
(1716, 1, 0) /* VN-WH060 - Hai Duong */,
(1793, 3, 0) /* VN-WH057 - Hung Yen */,
(1862, 1, 0) /* VN-WH019 - Hai Duong */,
(2176, 1, 1) /* VN-WH033 - Hai Duong */,
(2177, 1, 1) /* VN-WH031 - Hai Duong */,
(2280, 1, 0) /* VN-WH061 - Hai Duong */;

UPDATE tb SET tb.id_vi_tri_dia_ly = p.id_vi_tri_dia_ly,
              tb.can_cai_office = ISNULL(p.can_cai_office, tb.can_cai_office),
              tb.ngay_tra_loi_office = GETDATE(), tb.ngay_cap_nhat = GETDATE()
FROM KK_ThietBi tb JOIN @p p ON p.id_thiet_bi = tb.id_thiet_bi
WHERE tb.NgayXoa IS NULL
  AND (ISNULL(tb.id_vi_tri_dia_ly,-1) <> p.id_vi_tri_dia_ly
       OR (p.can_cai_office IS NOT NULL AND ISNULL(CAST(tb.can_cai_office AS int),-1) <> CAST(p.can_cai_office AS int)));
PRINT N'Da cap nhat: ' + CAST(@@ROWCOUNT AS varchar);

SELECT COUNT(*) AS Tong,
       SUM(CASE WHEN tb.id_vi_tri_dia_ly IS NULL THEN 1 ELSE 0 END) AS ChuaCoViTri,
       SUM(CASE WHEN tb.id_vi_tri_dia_ly = 1 THEN 1 ELSE 0 END) AS HaiDuong,
       SUM(CASE WHEN tb.id_vi_tri_dia_ly = 2 THEN 1 ELSE 0 END) AS NgheAn,
       SUM(CASE WHEN tb.id_vi_tri_dia_ly = 3 THEN 1 ELSE 0 END) AS HungYen,
       SUM(CASE WHEN tb.can_cai_office = 1 THEN 1 ELSE 0 END) AS CanOffice
FROM KK_ThietBi tb JOIN KK_BoPhan bp ON bp.IDBoPhan = tb.IDBoPhan
WHERE bp.TenBoPhan = N'VN-WH' AND tb.NgayXoa IS NULL;

-- COMMIT;
-- ROLLBACK;

