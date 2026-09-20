-- Cap nhat cot ghi_chu (KK_ThietBi) tu cot 'Ghi chu' cua cac phieu xac nhan trong thu muc 'Sua chua 5'.
-- Doc ngay 18/09/2026. Dong nao nhieu file cung dien thi lay file luu MOI NHAT.
-- Bo qua cac o ghi chu chi ghi ten tinh (da xu ly o cot vi tri dia ly) va cac dong DB da trung noi dung.
-- Quy tac da chot: DB dang trong -> ghi thang; DB da co noi dung khac -> NOI THEM dang 'cu | moi', khong xoa cai cu.
-- Chay lai lan hai an toan: da chua noi dung phieu thi khong noi them lan nua.
-- Rollback: kk-ghichu-tu-phieu-20260918-rollback.sql

SET NOCOUNT ON;
SET XACT_ABORT ON;
BEGIN TRAN;

DECLARE @gc TABLE (id_thiet_bi INT PRIMARY KEY, ghi_chu NVARCHAR(MAX));
INSERT INTO @gc (id_thiet_bi, ghi_chu) VALUES
(343, N'check camera phòng it') /* VN-DCV002 - VN-IT */,
(385, N'Nguyễn Thị Minh (V170112)') /* VN-LD012 - VN-TL */,
(402, N'máy tính ngoài xưởng, xin dùng bản miễn phí') /* VN-CV006 - VN-CV */,
(433, N'Phạm Thị Thanh Huyền (V261535 )') /* VN-QA011 - VN-QA */,
(434, N'Ngô Bá Quý (V260019 )') /* VN-QA042 - VN-QA */,
(477, N'kho IT') /* VN-SD101 - VN-IT */,
(519, N'máy tính ngoài xưởng, xin dùng bản miễn phí') /* VN-WD013 - VN-WD */,
(520, N'máy tính ngoài xưởng, xin dùng bản miễn phí') /* VN-WD009 - VN-WD */,
(521, N'máy tính ngoài xưởng, xin dùng bản miễn phí') /* VN-WD011 - VN-WD */,
(522, N'máy tính ngoài xưởng, xin dùng bản miễn phí') /* VN-WD012 - VN-WD */,
(524, N'máy tính ngoài xưởng, xin dùng bản miễn phí') /* VN-WD008 - VN-WD */,
(570, N'máy tính ngoài xưởng, xin dùng bản miễn phí') /* VN-PW011 - VN-PW */,
(572, N'máy tính ngoài xưởng, xin dùng bản miễn phí') /* VN-PW005 - VN-PW */,
(573, N'máy tính ngoài xưởng, xin dùng bản miễn phí') /* VN-PW004 - VN-PW */,
(600, N'đã chuyển cho máy data bên LTB vào tháng 7.2023') /* VN-PT015 - VN-PT */,
(731, N'KH RM (BIAO) -bpvn') /* VN-QA025 - VN-QA */,
(735, N'Nguyễn Thị Thảo (V260813)') /* VN-QA003 - VN-QA */,
(736, N'Phan Thị Hiếu Ngân - M260017 (TẠM THỜI)') /* VN-QA012 - VN-QA */,
(739, N'nghỉ việc đợi người mới') /* VN-QA037 - VN-QA */,
(909, N'Người sd tte hiện tại Vũ Hải Khánh Huyền (V261614)') /* VN-TL001 - VN-TL */,
(939, N'Nguyễn Thị Huệ, V200736') /* VN-LTBD005-1 - VN-LTB DAI */,
(978, N'Vũ Thị Nga(V261647 )') /* VN-QA045 - VN-QA */,
(1009, N'HỜ THỊ CÚC, V261372') /* VN-DF019 - VN-LTB DAI */,
(1011, N'KH RM (BIAO) -bpvn') /* VN-QA030 - VN-QA */,
(1013, N'Lương Thị Thuận (V180375)') /* VN-QA053 - VN-QA */,
(1014, N'nghỉ việc đợi người mới') /* VN-QA009 - VN-QA */,
(1205, N'Giàng A Hù (V261574)') /* VN-QC072 - VN-QC VẢI */,
(1567, N'phòng CCTV') /* VN-HR005 - VN-IT */,
(1705, N'伏云 (B260845)') /* VN-QA052 - VN-QA */,
(1885, N'Trần Thị Huệ (V261758)') /* VN-GL022 - VN-GL */,
(1900, N'KH RM (VPQA)') /* VN-QA013 - VN-QA */,
(1947, N'Trần Thị Tuyết Phượng ( V261673)') /* VN-GL010 - VN-GL */,
(2154, N'máy tính ngoài xưởng, xin dùng bản miễn phí') /* VN-WD016 - VN-WD */,
(2158, N'máy tính ngoài xưởng, xin dùng bản miễn phí') /* VN-WD019 - VN-WD */,
(2159, N'máy tính ngoài xưởng, xin dùng bản miễn phí') /* VN-WD018 - VN-WD */,
(2162, N'đã chuyển cho MIA') /* VN-SD132 - VN-IT */,
(2247, N'máy tính ngoài xưởng, xin dùng bản miễn phí') /* VN-WD006 - VN-WD */,
(2348, N'NHÀ MÁY RM HẢI PHÒNG') /* N/A - VN-QA */,
(2354, N'đã chuyển cho MR.OW') /*  - VN-IT */,
(2373, N'theo dõi hệ thống') /* VN-PMC032 - VN-IT */;

SELECT N'TRUOC' AS Moc, COUNT(*) AS SoDong,
       SUM(CASE WHEN ISNULL(LTRIM(RTRIM(tb.ghi_chu)),'') = '' THEN 1 ELSE 0 END) AS DangTrong,
       SUM(CASE WHEN CHARINDEX(g.ghi_chu, ISNULL(tb.ghi_chu,'')) > 0 THEN 1 ELSE 0 END) AS DaChuaNoiDungPhieu
FROM @gc g JOIN KK_ThietBi tb ON tb.id_thiet_bi = g.id_thiet_bi AND tb.NgayXoa IS NULL;

UPDATE tb
SET tb.ghi_chu = CASE WHEN ISNULL(LTRIM(RTRIM(tb.ghi_chu)),'') = '' THEN g.ghi_chu
                      ELSE LTRIM(RTRIM(tb.ghi_chu)) + N' | ' + g.ghi_chu END,
    tb.ngay_cap_nhat = GETDATE()
FROM KK_ThietBi tb JOIN @gc g ON g.id_thiet_bi = tb.id_thiet_bi
WHERE tb.NgayXoa IS NULL
  AND CHARINDEX(g.ghi_chu, ISNULL(tb.ghi_chu,'')) = 0;   -- da co san noi dung phieu thi khong noi them
PRINT N'So dong ghi chu da cap nhat: ' + CAST(@@ROWCOUNT AS varchar);

SELECT N'SAU' AS Moc, tb.id_thiet_bi, tb.ten_may_tinh, tb.ghi_chu
FROM @gc g JOIN KK_ThietBi tb ON tb.id_thiet_bi = g.id_thiet_bi AND tb.NgayXoa IS NULL
ORDER BY tb.id_thiet_bi;

-- Kiem so lieu roi bo chu thich dong duoi. Sai thi ROLLBACK;
-- COMMIT;
-- ROLLBACK;

