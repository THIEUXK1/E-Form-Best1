-- Dot 2: cap nhat vi tri dia ly + nhu cau cai Office tu cac phieu nhan sau dot 1 (thu muc 'Sua chua 5')
--   SD.xlsx          (18/09 13:03) - VN-GL, VN-SD    : 110 may
--   QA.xlsx          (17/09 16:01) - VN-QA           : 42 may
--   CV+WD.xlsx       (17/09 15:57) - VN-CV, VN-WD    : 20 may
--   TL.xlsx          (17/09 14:59) - VN-TL           : 5 may
--   SHD.xlsx         (18/09 09:11) - VN-SHD          : 1 may
-- Quy uoc giu nguyen nhu dot 1: o 'Can cai Office' khong tich = KHONG can -> can_cai_office = 0.
-- DA LOAI TRU 28 may ma nguoi dung nhap tren web SAU khi phieu duoc luu (ngay_tra_loi_office > gio luu file):
--   VN-IT 24, VN-WH 2, VN-YDF 1, VN-AC 1 -> du lieu tren web moi hon, khong ghi de.
-- Chay lai lan hai an toan (chi UPDATE dong con lech). Rollback: kk-capnhat-vitri-office-dot2-20260918-rollback.sql

SET NOCOUNT ON;
SET XACT_ABORT ON;
BEGIN TRAN;

-- Dia diem moi: 4 may cua VN-QA ghi 'HAI PHONG' - danh muc KK_ViTriDiaLy chua co.
-- Them mot lan, chay lai khong tao trung.
IF NOT EXISTS (SELECT 1 FROM KK_ViTriDiaLy WHERE ten_vi_tri_dia_ly = N'Hải Phòng')
BEGIN
    INSERT INTO KK_ViTriDiaLy (ten_vi_tri_dia_ly, mo_ta, thu_tu, dang_su_dung, ngay_tao)
    VALUES (N'Hải Phòng', N'Bổ sung theo phiếu xác nhận VN-QA ngày 17/09/2026', 4, 1, GETDATE());
END;
DECLARE @idHP INT = (SELECT TOP 1 id_vi_tri_dia_ly FROM KK_ViTriDiaLy WHERE ten_vi_tri_dia_ly = N'Hải Phòng');
PRINT N'id_vi_tri_dia_ly Hai Phong = ' + CAST(@idHP AS varchar);

DECLARE @phieu TABLE (id_thiet_bi INT PRIMARY KEY, id_vi_tri_dia_ly INT NULL, can_cai_office BIT NULL);
INSERT INTO @phieu (id_thiet_bi, id_vi_tri_dia_ly, can_cai_office) VALUES
(339,1,1) /* VN-SD156 - BPVN - VN-SD - SD.xlsx */,
(373,1,1) /* VN-LD049 - BPVN - VN-TL - TL.xlsx */,
(402,1,0) /* VN-CV006 - BPVN - VN-CV - CV+WD.xlsx */,
(403,1,1) /* VN-CV005 - BPVN - VN-CV - CV+WD.xlsx */,
(412,1,1) /* VN-PQC002 - BPVN - VN-WD - CV+WD.xlsx */,
(428,1,1) /* VN-QA014 - BPVN - VN-QA - QA.xlsx */,
(430,1,1) /* VN-QA044 - BPVN - VN-QA - QA.xlsx */,
(431,1,1) /* VN-QA007 - BPVN - VN-QA - QA.xlsx */,
(433,1,1) /* VN-QA011 - BPVN - VN-QA - QA.xlsx */,
(434,1,1) /* VN-QA042 - BPVN - VN-QA - QA.xlsx */,
(435,1,1) /* VN-QA047 - BPVN - VN-QA - QA.xlsx */,
(436,1,1) /* VN-QA046 - BPVN - VN-QA - QA.xlsx */,
(438,1,1) /* VN-QA032 - BPVN - VN-QA - QA.xlsx */,
(439,1,1) /* VN-QA031 - BPVN - VN-QA - QA.xlsx */,
(440,1,1) /* VN-QA051 - BPVN - VN-QA - QA.xlsx */,
(478,1,1) /* VN-SD110 - BPVN - VN-SD - SD.xlsx */,
(519,1,0) /* VN-WD013 - BPVN - VN-WD - CV+WD.xlsx */,
(520,1,0) /* VN-WD009 - BPVN - VN-WD - CV+WD.xlsx */,
(521,1,0) /* VN-WD011 - BPVN - VN-WD - CV+WD.xlsx */,
(522,1,0) /* VN-WD012 - BPVN - VN-WD - CV+WD.xlsx */,
(523,1,1) /* VN-WD002 - BPVN - VN-WD - CV+WD.xlsx */,
(524,1,0) /* VN-WD008 - BPVN - VN-WD - CV+WD.xlsx */,
(525,1,1) /* VN-WD005 - BPVN - VN-WD - CV+WD.xlsx */,
(615,1,1) /* VN-SD001 - BPVN - VN-SD - SD.xlsx */,
(617,1,1) /* VN-SD019 - BPVN - VN-SD - SD.xlsx */,
(621,1,1) /* VN-SD040 - BPVN - VN-SD - SD.xlsx */,
(625,1,1) /* VN-SD088 - BPVN - VN-SD - SD.xlsx */,
(692,1,1) /* VN-CV002 - BPVN - VN-WD - CV+WD.xlsx */,
(694,1,1) /* VN-CV001 - BPVN - VN-CV - CV+WD.xlsx */,
(714,1,1) /* VN-IMD005 - BPVN - VN-SD - SD.xlsx */,
(731,1,1) /* VN-QA025 - BPVN - VN-QA - QA.xlsx */,
(732,1,1) /* VN-QA006 - BPVN - VN-QA - QA.xlsx */,
(734,1,1) /* VN-QA049 - BPVN - VN-QA - QA.xlsx */,
(735,1,1) /* VN-QA003 - BPVN - VN-QA - QA.xlsx */,
(736,1,1) /* VN-QA012 - BPVN - VN-QA - QA.xlsx */,
(737,1,1) /* VN-QA002 - BPVN - VN-QA - QA.xlsx */,
(738,1,1) /* VN-QA048 - BPVN - VN-QA - QA.xlsx */,
(739,1,1) /* VN-QA037 - BPVN - VN-QA - QA.xlsx */,
(740,1,1) /* VN-QA041 - BPVN - VN-QA - QA.xlsx */,
(741,1,1) /* VN-QA056 - BPVN - VN-QA - QA.xlsx */,
(742,1,1) /* VN-QA059 - BPVN - VN-QA - QA.xlsx */,
(743,1,1) /* VN-QA099 - BPVN - VN-QA - QA.xlsx */,
(754,1,1) /* VN-SD143 - BPVN - VN-SD - SD.xlsx */,
(755,1,1) /* VN-SD141 - BPVN - VN-SD - SD.xlsx */,
(758,1,1) /* VN-SD114 - BPVN - VN-SD - SD.xlsx */,
(760,1,1) /* VN-SD134 - BPVN - VN-SD - SD.xlsx */,
(761,1,1) /* VN-SD057 - BPVN - VN-SD - SD.xlsx */,
(762,1,1) /* VN-SD116 - BPVN - VN-SD - SD.xlsx */,
(765,1,1) /* VN-SD016 - BPVN - VN-SD - SD.xlsx */,
(766,1,1) /* VN-SD083 - BPVN - VN-SD - SD.xlsx */,
(769,1,1) /* VN-SD018 - BPVN - VN-SD - SD.xlsx */,
(770,1,1) /* VN-SD149 - BPVN - VN-SD - SD.xlsx */,
(771,1,1) /* VN-SD138 - BPVN - VN-SD - SD.xlsx */,
(773,1,1) /* VN-SD092 - BPVN - VN-SD - SD.xlsx */,
(774,1,1) /* VN-SD155 - BPVN - VN-SD - SD.xlsx */,
(775,1,1) /* VN-SD148 - BPVN - VN-SD - SD.xlsx */,
(776,1,1) /* VN-SD091 - BPVN - VN-SD - SD.xlsx */,
(778,1,1) /* VN-SD102 - BPVN - VN-SD - SD.xlsx */,
(780,1,1) /* VN-SD097 - BPVN - VN-SD - SD.xlsx */,
(782,1,1) /* VN-SD004 - BPVN - VN-SD - SD.xlsx */,
(783,1,1) /* VN-SD151 - BPVN - VN-SD - SD.xlsx */,
(784,1,1) /* VN-SD017 - BPVN - VN-SD - SD.xlsx */,
(785,1,1) /* VN-SD076 - BPVN - VN-SD - SD.xlsx */,
(786,1,1) /* VN-SD031 - BPVN - VN-SD - SD.xlsx */,
(787,1,1) /* VN-SD105 - BPVN - VN-SD - SD.xlsx */,
(789,1,1) /* VN-SD008 - BPVN - VN-SD - SD.xlsx */,
(790,1,1) /* VN-SD106 - BPVN - VN-SD - SD.xlsx */,
(791,1,1) /* VN-SD109 - BPVN - VN-SD - SD.xlsx */,
(793,1,1) /* VN-SD000 - BPVN - VN-SD - SD.xlsx */,
(795,1,1) /* VN-SD006 - BPVN - VN-SD - SD.xlsx */,
(796,1,1) /* VN-SD122 - BPVN - VN-SD - SD.xlsx */,
(797,1,1) /* VN-SD074 - BPVN - VN-SD - SD.xlsx */,
(798,1,1) /* VN-SD135 - BPVN - VN-SD - SD.xlsx */,
(799,1,1) /* VN-SD153 - BPVN - VN-SD - SD.xlsx */,
(800,1,1) /* VN-SD060 - BPVN - VN-SD - SD.xlsx */,
(802,1,1) /* VN-SD129 - BPVN - VN-SD - SD.xlsx */,
(803,1,1) /* VN-SD036 - BPVN - VN-SD - SD.xlsx */,
(804,1,1) /* VN-SD152 - BPVN - VN-SD - SD.xlsx */,
(807,1,1) /* VN-SD007 - BPVN - VN-SD - SD.xlsx */,
(810,1,1) /* VN-SD084 - BPVN - VN-SD - SD.xlsx */,
(811,1,1) /* VN-SD025 - BPVN - VN-SD - SD.xlsx */,
(812,1,1) /* VN-SD098 - BPVN - VN-SD - SD.xlsx */,
(813,1,1) /* VN-SD103 - BPVN - VN-SD - SD.xlsx */,
(814,1,1) /* VN-SD142 - BPVN - VN-SD - SD.xlsx */,
(816,1,1) /* VN-SD145 - BPVN - VN-SD - SD.xlsx */,
(817,1,1) /* VN-SD079 - BPVN - VN-SD - SD.xlsx */,
(818,1,1) /* VN-SD080 - BPVN - VN-SD - SD.xlsx */,
(819,1,1) /* VN-SD063 - BPVN - VN-SD - SD.xlsx */,
(852,1,1) /* VN-WD004 - BPVN - VN-WD - CV+WD.xlsx */,
(872,1,1) /* VN-WV030 - BPVN - VN-WD - CV+WD.xlsx */,
(906,1,1) /* VN-TMC003 - BPVN - VN-SD - SD.xlsx */,
(909,1,1) /* VN-TL001 - BPVN - VN-TL - TL.xlsx */,
(926,1,1) /* VN-PMC021 - BPVN - VN-SD - SD.xlsx */,
(927,1,1) /* VN-SD104 - BPVN - VN-SD - SD.xlsx */,
(929,1,1) /* VN-SD147 - BPVN - VN-SD - SD.xlsx */,
(941,1,1) /* VN-GL011 - BPVN - VN-GL - SD.xlsx */,
(976,1,1) /* VN-QA038 - BPVN - VN-QA - QA.xlsx */,
(977,1,1) /* VN-QA039 - BPVN - VN-QA - QA.xlsx */,
(978,1,1) /* VN-QA045 - BPVN - VN-QA - QA.xlsx */,
(979,1,1) /* VN-QA050 - BPVN - VN-QA - QA.xlsx */,
(980,1,1) /* VN-SD096 - BPVN - VN-SD - SD.xlsx */,
(981,1,1) /* VN-SD090 - BPVN - VN-SD - SD.xlsx */,
(982,1,1) /* VN-SD127 - BPVN - VN-SD - SD.xlsx */,
(984,1,1) /* VN-SD140 - BPVN - VN-SD - SD.xlsx */,
(985,1,1) /* VN-SD100 - BPVN - VN-SD - SD.xlsx */,
(986,1,1) /* VN-SD154 - BPVN - VN-SD - SD.xlsx */,
(987,1,1) /* VN-SD150 - BPVN - VN-SD - SD.xlsx */,
(997,1,1) /* VN-WD003 - BPVN - VN-WD - CV+WD.xlsx */,
(1011,1,1) /* VN-QA030 - BPVN - VN-QA - QA.xlsx */,
(1012,1,1) /* VN-QA024 - BPVN - VN-QA - QA.xlsx */,
(1013,1,1) /* VN-QA053 - BPVN - VN-QA - QA.xlsx */,
(1014,1,1) /* VN-QA009 - BPVN - VN-QA - QA.xlsx */,
(1015,1,1) /* VN-SD050 - BPVN - VN-SD - SD.xlsx */,
(1017,1,1) /* VN-SD095 - BPVN - VN-SD - SD.xlsx */,
(1020,1,1) /* VN-SD120 - BPVN - VN-SD - SD.xlsx */,
(1023,1,1) /* VN-SD023 - BPVN - VN-SD - SD.xlsx */,
(1024,1,1) /* VN-SD075 - BPVN - VN-SD - SD.xlsx */,
(1026,1,1) /* VN-SD011 - BPVN - VN-SD - SD.xlsx */,
(1069,1,1) /* VN-SD137 - BPVN - VN-SD - SD.xlsx */,
(1098,1,1) /* VN-GL004 - BPVN - VN-GL - SD.xlsx */,
(1106,1,1) /* VN-LD018 - BPVN - VN-TL - TL.xlsx */,
(1107,1,1) /* VN-GM021 - BPVN - VN-GL - SD.xlsx */,
(1108,1,1) /* VN-LD011 - BPVN - VN-TL - TL.xlsx */,
(1168,1,1) /* ZP-SW018 - BPVN - VN-GL - SD.xlsx */,
(1170,1,1) /* VN-GL018 - BPVN - VN-GL - SD.xlsx */,
(1176,1,1) /* VN-GL013 - BPVN - VN-GL - SD.xlsx */,
(1185,1,1) /* VN-GL017 - BPVN - VN-GL - SD.xlsx */,
(1193,1,1) /* VN-GL006 - BPVN - VN-GL - SD.xlsx */,
(1234,3,1) /* VN-WD017 - BPVN - VN-WD - CV+WD.xlsx */,
(1352,1,1) /* VN-GL016 - BPVN - VN-GL - SD.xlsx */,
(1417,1,1) /* VN-GL014 - BPVN - VN-GL - SD.xlsx */,
(1629,1,1) /* VN-SD029 - BPVN - VN-SD - SD.xlsx */,
(1688,1,1) /* VN-QA055 - BPVN - VN-QA - QA.xlsx */,
(1705,1,1) /* VN-QA052 - BPVN - VN-QA - QA.xlsx */,
(1735,1,1) /* VN-SD118 - BPVN - VN-SD - SD.xlsx */,
(1880,1,1) /* VN-GL003 - BPVN - VN-GL - SD.xlsx */,
(1881,1,1) /* VN-GL025 - BPVN - VN-GL - SD.xlsx */,
(1882,1,1) /* VN-GL001 - BPVN - VN-GL - SD.xlsx */,
(1883,1,1) /* VN-GL020 - BPVN - VN-GL - SD.xlsx */,
(1884,1,1) /* VN-GL002 - BPVN - VN-GL - SD.xlsx */,
(1885,1,1) /* VN-GL022 - BPVN - VN-GL - SD.xlsx */,
(1886,1,1) /* VN-GL008 - BPVN - VN-GL - SD.xlsx */,
(1893,1,1) /* VN-QA058 - BPVN - VN-QA - QA.xlsx */,
(1900,1,1) /* VN-QA013 - BPVN - VN-QA - QA.xlsx */,
(1906,1,1) /* VN-QA043 - BPVN - VN-QA - QA.xlsx */,
(1909,1,1) /* VN-QA015 - BPVN - VN-QA - QA.xlsx */,
(1926,1,1) /* VN-GL015 - BPVN - VN-GL - SD.xlsx */,
(1928,1,1) /* VN-GL009 - BPVN - VN-GL - SD.xlsx */,
(1931,1,1) /* VN-GL019 - BPVN - VN-GL - SD.xlsx */,
(1939,1,1) /* VN-GL021 - BPVN - VN-GL - SD.xlsx */,
(1943,1,1) /* VN-GL012 - BPVN - VN-GL - SD.xlsx */,
(1946,1,1) /* VN-ITV003 - BPVN - VN-GL - SD.xlsx */,
(1947,1,1) /* VN-GL010 - BPVN - VN-GL - SD.xlsx */,
(1952,1,1) /* VN-SD030 - BPVN - VN-SD - SD.xlsx */,
(1978,1,1) /* RD005 - BPVN - VN-GL - SD.xlsx */,
(1979,1,1) /* VN-GL005 - BPVN - VN-GL - SD.xlsx */,
(1981,1,1) /* VN-GL023 - BPVN - VN-GL - SD.xlsx */,
(1986,1,1) /* VN-GL024 - BPVN - VN-GL - SD.xlsx */,
(2107,@idHP,1) /* VN-QA010 - BPVN - VN-QA - QA.xlsx */,
(2154,3,0) /* VN-WD016 - BPVN - VN-WD - CV+WD.xlsx */,
(2158,3,0) /* VN-WD019 - BPVN - VN-WD - CV+WD.xlsx */,
(2159,3,0) /* VN-WD018 - BPVN - VN-WD - CV+WD.xlsx */,
(2164,1,1) /* VN-SD124 - BPVN - VN-SD - SD.xlsx */,
(2170,1,1) /* VN-QA057 - BPVN - VN-QA - QA.xlsx */,
(2173,@idHP,1) /* VN-SD059 - BPVN - VN-QA - QA.xlsx */,
(2181,1,1) /* VN-GL007 - BPVN - VN-GL - SD.xlsx */,
(2198,1,1) /* VN-TL005 - BPVN - VN-TL - TL.xlsx */,
(2202,1,1) /* N/A - BPVN - VN-SD - SD.xlsx */,
(2225,1,1) /* VN-SD012 - BPVN - VN-SD - SD.xlsx */,
(2238,1,1) /* VN-SD003 - BPVN - VN-SD - SD.xlsx */,
(2247,1,0) /* VN-WD006 - BPVN - VN-WD - CV+WD.xlsx */,
(2318,1,1) /* VN-SD056 - BPVN - VN-SD - SD.xlsx */,
(2338,1,1) /* VN-SD094 - BPVN - VN-SD - SD.xlsx */,
(2340,1,1) /* VN-SD061 - BPVN - VN-SD - SD.xlsx */,
(2348,@idHP,1) /* N/A - BPVN - VN-QA - QA.xlsx */,
(2351,@idHP,1) /* VN-QA035 - BPVN - VN-QA - QA.xlsx */,
(2352,1,1) /* VN-QA054 - BPVN - VN-QA - QA.xlsx */,
(2398,1,0) /* VN-SHD001 - BPVN - VN-SHD - SHD.xlsx */;

SELECT N'TRUOC' AS Moc, COUNT(*) AS SoDongKhop,
       SUM(CASE WHEN ISNULL(tb.id_vi_tri_dia_ly,-1) = ISNULL(p.id_vi_tri_dia_ly, ISNULL(tb.id_vi_tri_dia_ly,-1)) THEN 1 ELSE 0 END) AS ViTriDaDung,
       SUM(CASE WHEN ISNULL(tb.can_cai_office,-1) = ISNULL(p.can_cai_office, ISNULL(tb.can_cai_office,-1)) THEN 1 ELSE 0 END) AS OfficeDaDung
FROM @phieu p JOIN KK_ThietBi tb ON tb.id_thiet_bi = p.id_thiet_bi AND tb.NgayXoa IS NULL;

UPDATE tb
SET tb.id_vi_tri_dia_ly    = ISNULL(p.id_vi_tri_dia_ly, tb.id_vi_tri_dia_ly),
    tb.can_cai_office      = ISNULL(p.can_cai_office, tb.can_cai_office),
    tb.ngay_tra_loi_office = GETDATE(),
    tb.ngay_cap_nhat       = GETDATE()
FROM KK_ThietBi tb JOIN @phieu p ON p.id_thiet_bi = tb.id_thiet_bi
WHERE tb.NgayXoa IS NULL
  AND (ISNULL(tb.id_vi_tri_dia_ly,-1) <> ISNULL(p.id_vi_tri_dia_ly, ISNULL(tb.id_vi_tri_dia_ly,-1))
       OR ISNULL(tb.can_cai_office,-1) <> ISNULL(p.can_cai_office, ISNULL(tb.can_cai_office,-1)));
PRINT N'So dong da cap nhat: ' + CAST(@@ROWCOUNT AS varchar);

SELECT N'SAU' AS Moc, COUNT(*) AS SoDongKhop,
       SUM(CASE WHEN ISNULL(tb.id_vi_tri_dia_ly,-1) = ISNULL(p.id_vi_tri_dia_ly, ISNULL(tb.id_vi_tri_dia_ly,-1)) THEN 1 ELSE 0 END) AS ViTriDaDung,
       SUM(CASE WHEN ISNULL(tb.can_cai_office,-1) = ISNULL(p.can_cai_office, ISNULL(tb.can_cai_office,-1)) THEN 1 ELSE 0 END) AS OfficeDaDung
FROM @phieu p JOIN KK_ThietBi tb ON tb.id_thiet_bi = p.id_thiet_bi AND tb.NgayXoa IS NULL;

SELECT COUNT(*) AS TongThietBi,
       SUM(CASE WHEN id_vi_tri_dia_ly IS NOT NULL THEN 1 ELSE 0 END) AS CoViTri,
       SUM(CASE WHEN can_cai_office = 1 THEN 1 ELSE 0 END) AS CanOffice,
       SUM(CASE WHEN can_cai_office = 0 THEN 1 ELSE 0 END) AS KhongCanOffice
FROM KK_ThietBi WHERE NgayXoa IS NULL;

-- Xem so lieu doi soat roi bo chu thich dong duoi de ghi that. Sai thi ROLLBACK;
-- COMMIT;
-- ROLLBACK;


