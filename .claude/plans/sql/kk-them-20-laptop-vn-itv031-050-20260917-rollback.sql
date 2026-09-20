/*
  Rollback cho kk-them-20-laptop-vn-itv031-050-20260917.sql
  Gồm 2 phần:
    (1) Xoá mềm 20 laptop VN-ITV031..050 vừa thêm (KHÔNG xoá cứng, giữ vết truy xuất).
    (2) Trả can_cai_office / ngay_tra_loi_office của 26 máy cũ về đúng giá trị trước khi chạy.
    (3) Trả IDBoPhan của 6 máy cũ (VN-ITV003/005/008/009/020/024) về bộ phận gốc.
        (ảnh chụp lúc 2026-09-17; 4 máy VN-ITV005/008/009/020 vốn đã = 1 nên không có trong danh sách).
  Ngày: 2026-09-17
*/

SET NOCOUNT ON;
SET XACT_ABORT ON;

BEGIN TRAN;

/* (1) Xoá mềm 20 máy mới thêm */
UPDATE KK_ThietBi
SET NgayXoa = GETDATE(),
    LyDoXoa = N'Rollback lô thêm mới VN-ITV031..050 ngày 2026-09-17'
WHERE NgayXoa IS NULL
  AND seribacode IN (
      '5CG5270T97','5CG5270T2W','5CG5270T24','5CG5270T2P','5CG5270T5K',
      '5CG5270T15','5CG5270T1S','5CG5270T0P','5CG5270T02','5CG5270T9X',
      '5CG5270T65','5CG5270T09','5CG5270T0S','5CG5270T5Q','5CG5270T98',
      '5CG5270T96','5CG5270T66','5CG5270T5G','5CG5270T9L','5CG5270T8Y'
  )
  AND IdMay IS NULL            -- chưa được liên kết sang máy tài sản
  AND id_nguoi_dung = 449;     -- vẫn còn đứng tên phòng IT, chưa bàn giao

PRINT N'Số dòng đã xoá mềm: ' + CAST(@@ROWCOUNT AS nvarchar(10));

/* (2) Trả cờ Office của 26 máy cũ về giá trị trước khi chạy */
DECLARE @cu TABLE (id_thiet_bi int PRIMARY KEY, cco bit, ntl datetime);
INSERT INTO @cu (id_thiet_bi, cco, ntl) VALUES
    (2119, 0, '2026-09-17T11:27:28.473'),  -- VN-ITV001
    (2120, 0, '2026-09-17T11:27:28.473'),  -- VN-ITV002
    (1946, 0, '2026-09-17T11:27:28.473'),  -- VN-ITV003
    (2121, 0, '2026-09-17T11:27:28.473'),  -- VN-ITV004
    (2122, 0, '2026-09-17T11:27:28.473'),  -- VN-ITV006
    (2123, 0, '2026-09-17T11:27:28.473'),  -- VN-ITV007
    ( 904, 0, '2026-09-17T11:27:28.473'),  -- VN-ITV010
    (1861, 0, '2026-09-17T11:27:28.473'),  -- VN-ITV011
    (2124, 0, '2026-09-17T11:27:28.473'),  -- VN-ITV012
    (2081, 0, '2026-09-17T11:27:28.473'),  -- VN-ITV013
    ( 905, 0, '2026-09-17T11:27:28.473'),  -- VN-ITV014
    ( 345, 0, '2026-09-17T11:27:28.473'),  -- VN-ITV015
    (2125, 0, '2026-09-17T11:27:28.473'),  -- VN-ITV016
    (1942, 0, '2026-09-17T11:27:28.473'),  -- VN-ITV017
    (1063, 0, '2026-09-17T11:27:28.473'),  -- VN-ITV018
    (2126, 0, '2026-09-17T11:27:28.473'),  -- VN-ITV019
    (2080, 0, '2026-09-17T11:27:28.473'),  -- VN-ITV021
    (2127, 0, '2026-09-17T11:27:28.473'),  -- VN-ITV022
    (2128, 0, '2026-09-17T11:27:28.473'),  -- VN-ITV023
    (2129, 0, '2026-09-16T13:30:44.123'),  -- VN-ITV024
    (2130, 0, '2026-09-17T11:27:28.473'),  -- VN-ITV025
    ( 901, 0, '2026-09-17T11:27:28.473'),  -- VN-ITV026
    (2131, 0, '2026-09-17T11:27:28.473'),  -- VN-ITV027
    (2132, 0, '2026-09-17T11:27:28.473'),  -- VN-ITV028
    (2133, 0, '2026-09-17T11:27:28.473'),  -- VN-ITV029
    (2134, 0, '2026-09-17T11:27:28.473');  -- VN-ITV030

UPDATE t
SET t.can_cai_office      = c.cco,
    t.ngay_tra_loi_office = c.ntl,
    t.ngay_cap_nhat       = GETDATE()
FROM KK_ThietBi t
JOIN @cu c ON c.id_thiet_bi = t.id_thiet_bi
WHERE t.NgayXoa IS NULL;

PRINT N'Số máy cũ đã trả cờ Office về giá trị cũ: ' + CAST(@@ROWCOUNT AS nvarchar(10));


/* (3) Trả bộ phận của 6 máy cũ về giá trị trước khi chạy (công ty vốn đã là BPVN=1) */
DECLARE @bp TABLE (id_thiet_bi int PRIMARY KEY, idcongty int, idbophan int);
INSERT INTO @bp (id_thiet_bi, idcongty, idbophan) VALUES
    (1946, 1, 1007),   -- VN-ITV003 -> VN-GL
    ( 902, 1, 1020),   -- VN-ITV005 -> VN-AC
    (1912, 1, 1031),   -- VN-ITV008 -> VN-SD
    (1714, 1, 1020),   -- VN-ITV009 -> VN-AC
    ( 903, 1, 1031),   -- VN-ITV020 -> VN-SD
    (2129, 1, 1031);   -- VN-ITV024 -> VN-SD

UPDATE t
SET t.IDCongTy      = p.idcongty,
    t.IDBoPhan      = p.idbophan,
    t.ngay_cap_nhat = GETDATE()
FROM KK_ThietBi t
JOIN @bp p ON p.id_thiet_bi = t.id_thiet_bi
WHERE t.NgayXoa IS NULL;

PRINT N'Số máy cũ đã trả bộ phận về giá trị cũ: ' + CAST(@@ROWCOUNT AS nvarchar(10));
COMMIT;
