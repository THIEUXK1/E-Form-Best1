-- VN-SD: bo phan xac nhan 100% may dung Office va dong tai Hai Duong (yeu cau ngay 18/09/2026).
-- Nhom A: 81 Laptop/May tinh -> id_vi_tri_dia_ly = 1 (Hai Duong) + can_cai_office = 1
-- Nhom B: 62 thiet bi khac (man hinh/may in/switch...) -> CHI dien vi tri Hai Duong vao o dang trong,
--         khong dong vao can_cai_office vi cau hoi Office khong ap dung cho chung.
-- DA LOAI TRU 0 may co nguoi tra loi trong app SAU khi phieu SD.xlsx duoc luu (2026-09-18 13:03:47).
-- Chay lai lan hai an toan. Rollback: kk-vnsd-office-haiduong-20260918-rollback.sql

SET NOCOUNT ON;
SET XACT_ABORT ON;
BEGIN TRAN;

DECLARE @may TABLE (id_thiet_bi INT PRIMARY KEY);
INSERT INTO @may (id_thiet_bi) VALUES
(339) /* VN-SD156 - Máy tính */,
(478) /* VN-SD110 - Máy tính */,
(615) /* VN-SD001 - Máy tính */,
(617) /* VN-SD019 - Máy tính */,
(621) /* VN-SD040 - Máy tính */,
(625) /* VN-SD088 - Laptop */,
(714) /* VN-IMD005 - Laptop */,
(754) /* VN-SD143 - Laptop */,
(755) /* VN-SD141 - Laptop */,
(758) /* VN-SD114 - Laptop */,
(760) /* VN-SD134 - Laptop */,
(761) /* VN-SD057 - Máy tính */,
(762) /* VN-SD116 - Máy tính */,
(765) /* VN-SD016 - Máy tính */,
(766) /* VN-SD083 - Máy tính */,
(769) /* VN-SD018 - Máy tính */,
(770) /* VN-SD149 - Laptop */,
(771) /* VN-SD138 - Laptop */,
(773) /* VN-SD092 - Laptop */,
(774) /* VN-SD155 - Máy tính */,
(775) /* VN-SD148 - Laptop */,
(776) /* VN-SD091 - Máy tính */,
(778) /* VN-SD102 - Máy tính */,
(780) /* VN-SD097 - Laptop */,
(782) /* VN-SD004 - Laptop */,
(783) /* VN-SD151 - Máy tính */,
(784) /* VN-SD017 - Máy tính */,
(785) /* VN-SD076 - Laptop */,
(786) /* VN-SD031 - Máy tính */,
(787) /* VN-SD105 - Máy tính */,
(789) /* VN-SD008 - Máy tính */,
(790) /* VN-SD106 - Máy tính */,
(791) /* VN-SD109 - Máy tính */,
(793) /* VN-SD000 - Máy tính */,
(795) /* VN-SD006 - Máy tính */,
(796) /* VN-SD122 - Máy tính */,
(797) /* VN-SD074 - Laptop */,
(798) /* VN-SD135 - Laptop */,
(799) /* VN-SD153 - Máy tính */,
(800) /* VN-SD060 - Máy tính */,
(802) /* VN-SD129 - Laptop */,
(803) /* VN-SD036 - Máy tính */,
(804) /* VN-SD152 - Laptop */,
(807) /* VN-SD007 - Máy tính */,
(810) /* VN-SD084 - Máy tính */,
(811) /* VN-SD025 - Máy tính */,
(812) /* VN-SD098 - Máy tính */,
(813) /* VN-SD103 - Máy tính */,
(814) /* VN-SD142 - Laptop */,
(816) /* VN-SD145 - Máy tính */,
(817) /* VN-SD079 - Laptop */,
(818) /* VN-SD080 - Laptop */,
(819) /* VN-SD063 - Máy tính */,
(906) /* VN-TMC003 - Máy tính */,
(926) /* VN-PMC021 - Máy tính */,
(927) /* VN-SD104 - Máy tính */,
(929) /* VN-SD147 - Laptop */,
(980) /* VN-SD096 - Máy tính */,
(981) /* VN-SD090 - Laptop */,
(982) /* VN-SD127 - Laptop */,
(984) /* VN-SD140 - Máy tính */,
(985) /* VN-SD100 - Máy tính */,
(986) /* VN-SD154 - Máy tính */,
(987) /* VN-SD150 - Laptop */,
(1015) /* VN-SD050 - Máy tính */,
(1017) /* VN-SD095 - Máy tính */,
(1020) /* VN-SD120 - Laptop */,
(1023) /* VN-SD023 - Laptop */,
(1024) /* VN-SD075 - Máy tính */,
(1026) /* VN-SD011 - Máy tính */,
(1069) /* VN-SD137 - Laptop */,
(1629) /* VN-SD029 - Máy tính */,
(1735) /* VN-SD118 - Máy tính */,
(1952) /* VN-SD030 - Laptop */,
(2164) /* VN-SD124 - Laptop */,
(2202) /*  - Laptop */,
(2225) /* VN-SD012 - Máy tính */,
(2238) /* VN-SD003 - Máy tính */,
(2318) /* VN-SD056 - Máy tính */,
(2338) /* VN-SD094 - Máy tính */,
(2340) /* VN-SD061 - Máy tính */;

UPDATE tb SET tb.id_vi_tri_dia_ly = 1, tb.can_cai_office = 1,
              tb.ngay_tra_loi_office = GETDATE(), tb.ngay_cap_nhat = GETDATE()
FROM KK_ThietBi tb JOIN @may m ON m.id_thiet_bi = tb.id_thiet_bi
WHERE tb.NgayXoa IS NULL AND (ISNULL(tb.id_vi_tri_dia_ly,-1) <> 1 OR ISNULL(tb.can_cai_office,-1) <> 1);
PRINT N'Nhom A da cap nhat: ' + CAST(@@ROWCOUNT AS varchar);

DECLARE @khac TABLE (id_thiet_bi INT PRIMARY KEY);
INSERT INTO @khac (id_thiet_bi) VALUES
(1279) /*  - Máy in */,
(1390) /*  - Màn hình */,
(1396) /*  - Màn hình */,
(1423) /*  - Điện thoại bàn */,
(1424) /*  - Màn hình */,
(1431) /*  - Màn hình */,
(1441) /*  - Điện thoại bàn */,
(1442) /*  - Màn hình */,
(1445) /*  - Màn hình */,
(1572) /*  - Điện thoại bàn */,
(1573) /*  - Điện thoại bàn */,
(1574) /*  - Điện thoại bàn */,
(1581) /*  - Màn hình */,
(1587) /*  - Màn hình */,
(1589) /*  - Màn hình */,
(1595) /*  - Điện thoại bàn */,
(1612) /*  - Điện thoại bàn */,
(1616) /*  - Màn hình */,
(1618) /*  - Màn hình */,
(1619) /*  - Màn hình */,
(1622) /*  - Màn hình */,
(1623) /*  - Điện thoại bàn */,
(1626) /*  - Màn hình */,
(1627) /*  - Màn hình */,
(1628) /*  - Màn hình */,
(1630) /*  - Màn hình */,
(1632) /*  - Màn hình */,
(1720) /*  - Màn hình */,
(1723) /*  - Màn hình */,
(1724) /*  - Màn hình */,
(1736) /*  - Màn hình */,
(1741) /*  - Màn hình */,
(1746) /*  - Màn hình */,
(1781) /*  - Điện thoại bàn */,
(1782) /*  - Màn hình */,
(1797) /*  - Máy in */,
(1798) /*  - Máy in */,
(1799) /*  - Điện thoại bàn */,
(1800) /*  - Màn hình */,
(1803) /*  - Màn hình */,
(1820) /*  - Màn hình */,
(1831) /*  - Màn hình */,
(1834) /*  - Màn hình */,
(1835) /*  - Điện thoại bàn */,
(1842) /*  - Màn hình */,
(1844) /*  - Điện thoại bàn */,
(1846) /*  - Màn hình */,
(1849) /*  - Màn hình */,
(1853) /*  - Màn hình */,
(1858) /*  - Màn hình */,
(1859) /*  - Màn hình */,
(1860) /*  - Màn hình */,
(1874) /*  - Máy in */,
(1876) /*  - Điện thoại bàn */,
(1877) /*  - Điện thoại bàn */,
(2087) /*  - Màn hình */,
(2088) /*  - Màn hình */,
(2165) /* VN-SD124 - Màn hình */,
(2197) /*  - Màn hình */,
(2226) /*  - Màn hình */,
(2239) /*  - Màn hình */,
(2341) /* VN-SD061 - Màn hình */;

UPDATE tb SET tb.id_vi_tri_dia_ly = 1, tb.ngay_cap_nhat = GETDATE()
FROM KK_ThietBi tb JOIN @khac k ON k.id_thiet_bi = tb.id_thiet_bi
WHERE tb.NgayXoa IS NULL AND tb.id_vi_tri_dia_ly IS NULL;
PRINT N'Nhom B da cap nhat: ' + CAST(@@ROWCOUNT AS varchar);

-- Doi soat: toan bo VN-SD sau khi chay
SELECT COUNT(*) AS TongVNSD,
       SUM(CASE WHEN tb.id_vi_tri_dia_ly = 1 THEN 1 ELSE 0 END) AS O_HaiDuong,
       SUM(CASE WHEN tb.id_vi_tri_dia_ly IS NULL THEN 1 ELSE 0 END) AS ChuaCoViTri,
       SUM(CASE WHEN tb.can_cai_office = 1 THEN 1 ELSE 0 END) AS CanOffice
FROM KK_ThietBi tb JOIN KK_BoPhan bp ON bp.IDBoPhan = tb.IDBoPhan
WHERE bp.TenBoPhan = N'VN-SD' AND tb.NgayXoa IS NULL;

-- Kiem so lieu roi bo chu thich dong duoi. Sai thi ROLLBACK;
-- COMMIT;
-- ROLLBACK;

