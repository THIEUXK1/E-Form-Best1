-- Phieu 'EG.xlsx' (VN-EG, luu 18/09/2026 14:53).
-- Nhom 1: 20 Laptop/May tinh -> dien vi tri dia ly theo phieu + can_cai_office = 1 (phieu tich ☑ het).
--         Truoc do ca 20 may deu chua co vi tri va dang bi ghi 0 tu dot 1 (mac dinh khi phieu chua tra loi).
-- Nhom 2: 1 may doi nguoi su dung theo cot 'Nguoi su dung' cua phieu (974 VN-EG020).
-- Cot Ghi chu cua phieu de trong het -> khong dong vao ghi_chu cua DB.
-- Moc ngay_tra_loi_office cua ca 20 may la 17/09 11:27, cu hon file -> ghi de an toan.
-- Chay lai lan hai an toan. Rollback: kk-vneg-20260918-rollback.sql

SET NOCOUNT ON;
SET XACT_ABORT ON;
BEGIN TRAN;

DECLARE @tb TABLE (id_thiet_bi INT PRIMARY KEY, id_vi_tri_dia_ly INT, can_cai_office BIT);
INSERT INTO @tb (id_thiet_bi, id_vi_tri_dia_ly, can_cai_office) VALUES
(209, 2, 1) /* VN-EG019 - Nghệ An */,
(213, 2, 1) /* VN-EG029 - Nghệ An */,
(424, 1, 1) /* VN-EG009 - Hải Dương */,
(425, 1, 1) /* VN-EG015 - Hải Dương */,
(426, 1, 1) /* VN-EG012 - Hải Dương */,
(715, 1, 1) /* VN-EG026 - Hải Dương */,
(716, 1, 1) /* VN-EG030 - Hải Dương */,
(717, 1, 1) /* VN-EG022 - Hải Dương */,
(718, 1, 1) /* VN-EG013 - Hải Dương */,
(826, 1, 1) /* VN-SHD015 - Hải Dương */,
(922, 1, 1) /* VN-EG017 - Hải Dương */,
(923, 1, 1) /* VN-IE020 - Hải Dương */,
(940, 1, 1) /* VN-EG016 - Hải Dương */,
(973, 2, 1) /* VN-EG007 - Nghệ An */,
(974, 1, 1) /* VN-EG020 - Hải Dương */,
(1090, 2, 1) /* VN-IE012 - Nghệ An */,
(2185, 1, 1) /* VN-IE009 - Hải Dương */,
(2193, 1, 1) /* VN-EG027 - Hải Dương */,
(2320, 1, 1) /* PC-EG - Hải Dương */,
(2342, 1, 1) /* N/A - Hải Dương */;

UPDATE tb SET tb.id_vi_tri_dia_ly = p.id_vi_tri_dia_ly, tb.can_cai_office = p.can_cai_office,
              tb.ngay_tra_loi_office = GETDATE(), tb.ngay_cap_nhat = GETDATE()
FROM KK_ThietBi tb JOIN @tb p ON p.id_thiet_bi = tb.id_thiet_bi
WHERE tb.NgayXoa IS NULL
  AND (ISNULL(tb.id_vi_tri_dia_ly,-1) <> p.id_vi_tri_dia_ly OR ISNULL(CAST(tb.can_cai_office AS int),-1) <> CAST(p.can_cai_office AS int));
PRINT N'Nhom 1 - da cap nhat vi tri + Office: ' + CAST(@@ROWCOUNT AS varchar);

-- Nhom 2: VN-EG020 - phieu ghi nguoi su dung la Bui Nguyen Hung (V260888, id 5384),
--         DB dang de Tran Van Cao (V261365, id 6601).
UPDATE KK_ThietBi SET id_nguoi_dung = 5384, ngay_cap_nhat = GETDATE()
WHERE id_thiet_bi = 974 AND NgayXoa IS NULL AND ISNULL(id_nguoi_dung,-1) <> 5384;
PRINT N'Nhom 2 - da doi nguoi su dung: ' + CAST(@@ROWCOUNT AS varchar);

SELECT COUNT(*) AS Tong,
       SUM(CASE WHEN tb.id_vi_tri_dia_ly IS NULL THEN 1 ELSE 0 END) AS ChuaCoViTri,
       SUM(CASE WHEN tb.id_vi_tri_dia_ly = 1 THEN 1 ELSE 0 END) AS HaiDuong,
       SUM(CASE WHEN tb.id_vi_tri_dia_ly = 2 THEN 1 ELSE 0 END) AS NgheAn,
       SUM(CASE WHEN tb.can_cai_office = 1 THEN 1 ELSE 0 END) AS CanOffice
FROM KK_ThietBi tb JOIN KK_BoPhan bp ON bp.IDBoPhan = tb.IDBoPhan
WHERE bp.TenBoPhan = N'VN-EG' AND tb.NgayXoa IS NULL;

-- COMMIT;
-- ROLLBACK;


