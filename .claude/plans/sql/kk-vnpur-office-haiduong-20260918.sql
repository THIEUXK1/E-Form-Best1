-- VN-PUR : bo phan xac nhan 100% may dung Office va dong tai Hai Duong (yeu cau 18/09/2026).
-- Nhom A: 10 Laptop/May tinh -> id_vi_tri_dia_ly = 1 + can_cai_office = 1
-- Nhom B: 12 thiet bi khac -> CHI dien vi tri Hai Duong vao o dang trong, khong dong vao can_cai_office
-- Chay lai lan hai an toan. Rollback: kk-vnpur-office-haiduong-20260918-rollback.sql

SET NOCOUNT ON;
SET XACT_ABORT ON;
BEGIN TRAN;

DECLARE @may TABLE (id_thiet_bi INT PRIMARY KEY);
INSERT INTO @may (id_thiet_bi) VALUES
(344) /* VN-PUR030 - Laptop */,
(720) /* VN-PUR028 - Laptop */,
(721) /* VN-PUR032 - Laptop */,
(722) /* VN-PUR018 - Laptop */,
(723) /* VN-PUR019 - Laptop */,
(724) /* VN-PUR033 - Laptop */,
(726) /* VN-PUR001 - Laptop */,
(730) /* VN-PUR027 - Laptop */,
(1010) /* VN-PUR040 - Laptop */,
(1072) /* VN-PUR031 - Laptop */;
UPDATE tb SET tb.id_vi_tri_dia_ly = 1, tb.can_cai_office = 1, tb.ngay_tra_loi_office = GETDATE(), tb.ngay_cap_nhat = GETDATE()
FROM KK_ThietBi tb JOIN @may m ON m.id_thiet_bi = tb.id_thiet_bi
WHERE tb.NgayXoa IS NULL AND (ISNULL(tb.id_vi_tri_dia_ly,-1) <> 1 OR ISNULL(tb.can_cai_office,-1) <> 1);
PRINT N'Nhom A da cap nhat: ' + CAST(@@ROWCOUNT AS varchar);

DECLARE @khac TABLE (id_thiet_bi INT PRIMARY KEY);
INSERT INTO @khac (id_thiet_bi) VALUES
(1075) /*  - Điện thoại bàn */,
(1076) /*  - Tivi */,
(1077) /*  - Máy in */,
(1079) /*  - Điện thoại bàn */,
(1080) /*  - Điện thoại bàn */,
(1081) /*  - Điện thoại bàn */,
(1082) /*  - Điện thoại bàn */,
(1083) /*  - Điện thoại bàn */,
(1084) /* VN-PUR027 - Màn hình */,
(1085) /*  - Điện thoại bàn */,
(1086) /*  - Điện thoại bàn */,
(1087) /*  - Điện thoại bàn */;
UPDATE tb SET tb.id_vi_tri_dia_ly = 1, tb.ngay_cap_nhat = GETDATE()
FROM KK_ThietBi tb JOIN @khac k ON k.id_thiet_bi = tb.id_thiet_bi
WHERE tb.NgayXoa IS NULL AND tb.id_vi_tri_dia_ly IS NULL;
PRINT N'Nhom B da cap nhat: ' + CAST(@@ROWCOUNT AS varchar);

SELECT COUNT(*) AS Tong,
       SUM(CASE WHEN tb.id_vi_tri_dia_ly = 1 THEN 1 ELSE 0 END) AS O_HaiDuong,
       SUM(CASE WHEN tb.id_vi_tri_dia_ly IS NULL THEN 1 ELSE 0 END) AS ChuaCoViTri,
       SUM(CASE WHEN tb.can_cai_office = 1 THEN 1 ELSE 0 END) AS CanOffice
FROM KK_ThietBi tb JOIN KK_BoPhan bp ON bp.IDBoPhan = tb.IDBoPhan
WHERE bp.TenBoPhan = N'VN-PUR' AND tb.NgayXoa IS NULL;

-- COMMIT;
-- ROLLBACK;

