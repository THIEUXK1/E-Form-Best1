-- Phieu 'SS.xlsx' (VN-SS, luu 19/09/2026 10:53).
-- 23 thiet bi con lech so voi DB -> ghi vi tri dia ly + can_cai_office theo phieu.
-- Khong dong nao co nguoi tra loi tren web sau khi file duoc luu, nen ghi de toan bo phan lech.
-- Cot Nguoi su dung cua phieu trung khop DB; cot Ghi chu de trong -> khong dong vao ghi_chu.
-- Chay lai lan hai an toan. Rollback: kk-vnss-20260919-rollback.sql

SET NOCOUNT ON;
SET XACT_ABORT ON;
BEGIN TRAN;

DECLARE @p TABLE (id_thiet_bi INT PRIMARY KEY, id_vi_tri_dia_ly INT, can_cai_office BIT NULL);
INSERT INTO @p (id_thiet_bi, id_vi_tri_dia_ly, can_cai_office) VALUES
(481, 1, 1) /* VN-SS049 - Hai Duong */,
(482, 1, 1) /* VN-SS064 - Hai Duong */,
(483, 1, 1) /* VN-SS065 - Hai Duong */,
(486, 1, 1) /* VN-SS43 - Hai Duong */,
(487, 1, 1) /* VN-SS057 - Hai Duong */,
(488, 1, 1) /* VN-SS043 - Hai Duong */,
(497, 1, 1) /* VN-SS056 - Hai Duong */,
(498, 1, 1) /* VN-SS034 - Hai Duong */,
(499, 1, 1) /* VN-SS059 - Hai Duong */,
(503, 1, 1) /* VN-SS061 - Hai Duong */,
(832, 1, 1) /* VN-SS011 - Hai Duong */,
(835, 1, 1) /* VN-SS066 - Hai Duong */,
(836, 1, 1) /* VN-SS058 - Hai Duong */,
(837, 1, 1) /* VN-SS074 - Hai Duong */,
(839, 1, 1) /* VN-SS003 - Hai Duong */,
(840, 1, 1) /* VN-SS081 - Hai Duong */,
(841, 1, 1) /* VN-SS060 - Hai Duong */,
(842, 1, 1) /* VN-SS047 - Hai Duong */,
(933, 1, 1) /* VN-SS063 - Hai Duong */,
(990, 1, 1) /* VN-SS062 - Hai Duong */,
(1028, 1, 1) /* VN-SS069 - Hai Duong */,
(2228, 3, 1) /* VN-SS046 - Hung Yen */,
(2350, 1, 1) /* VN-SS42 - Hai Duong */;

UPDATE tb SET tb.id_vi_tri_dia_ly = p.id_vi_tri_dia_ly,
              tb.can_cai_office = ISNULL(p.can_cai_office, tb.can_cai_office),
              tb.ngay_tra_loi_office = GETDATE(), tb.ngay_cap_nhat = GETDATE()
FROM KK_ThietBi tb JOIN @p p ON p.id_thiet_bi = tb.id_thiet_bi
WHERE tb.NgayXoa IS NULL
  AND (ISNULL(tb.id_vi_tri_dia_ly,-1) <> p.id_vi_tri_dia_ly
       OR (p.can_cai_office IS NOT NULL AND ISNULL(CAST(tb.can_cai_office AS int),-1) <> CAST(p.can_cai_office AS int)));
PRINT N'Da cap nhat: ' + CAST(@@ROWCOUNT AS varchar);

-- Nhom 2: 3 may co cot Ghi chu ghi ten nguoi tiep nhan -> doi nguoi su dung.
DECLARE @nd TABLE (id_thiet_bi INT PRIMARY KEY, id_nguoi_dung INT);
INSERT INTO @nd (id_thiet_bi, id_nguoi_dung) VALUES
(483, 1201) /* VN-SS065: Nguyen Thi Ngoc Anh V201182 -> Hoang Van Thi V180248 */,
(498, 1204) /* VN-SS034: Tho Van Vu V240388 -> Nguyen Tien Nam V180535 */,
(837, 1240) /* VN-SS074: Nguyen Van Thang V260559 -> Cu Seo Nha V220482 */;
UPDATE tb SET tb.id_nguoi_dung = n.id_nguoi_dung, tb.ngay_cap_nhat = GETDATE()
FROM KK_ThietBi tb JOIN @nd n ON n.id_thiet_bi = tb.id_thiet_bi
WHERE tb.NgayXoa IS NULL AND ISNULL(tb.id_nguoi_dung,-1) <> n.id_nguoi_dung;
PRINT N'Nhom 2 - da doi nguoi su dung: ' + CAST(@@ROWCOUNT AS varchar);

SELECT COUNT(*) AS Tong,
       SUM(CASE WHEN tb.id_vi_tri_dia_ly IS NULL THEN 1 ELSE 0 END) AS ChuaCoViTri,
       SUM(CASE WHEN tb.id_vi_tri_dia_ly = 1 THEN 1 ELSE 0 END) AS HaiDuong,
       SUM(CASE WHEN tb.id_vi_tri_dia_ly = 2 THEN 1 ELSE 0 END) AS NgheAn,
       SUM(CASE WHEN tb.id_vi_tri_dia_ly = 3 THEN 1 ELSE 0 END) AS HungYen,
       SUM(CASE WHEN tb.can_cai_office = 1 THEN 1 ELSE 0 END) AS CanOffice
FROM KK_ThietBi tb JOIN KK_BoPhan bp ON bp.IDBoPhan = tb.IDBoPhan
WHERE bp.TenBoPhan = N'VN-SS' AND tb.NgayXoa IS NULL;

-- COMMIT;
-- ROLLBACK;


