/*
  Rollback cho kk-tach-mayin-gop-20260925.sql
    (1) Trỏ 4 bằng chứng 3021, 3022, 3026, 2743 về lại bản gốc.
    (2) Xoá mềm 4 máy in đã tách (KHÔNG xoá cứng, giữ vết).
    (3) Trả serial giả, quy_cach cũ, ghi_chu NULL cho 3 bản gốc (giá trị chụp lúc 2026-09-25).
  KK_LichSuThaoTac là append-only → không xoá log của lần tách, chỉ ghi thêm dòng rollback.
  Ngày: 2026-09-25
*/

SET NOCOUNT ON;
SET XACT_ABORT ON;

BEGIN TRAN;

DECLARE @now datetime = GETDATE();
DECLARE @map TABLE (id_goc int, id_bang_chung int);
INSERT INTO @map VALUES (1954, 3021), (1954, 3022), (1957, 3026), (1770, 2743);

DECLARE @tach TABLE (id_thiet_bi int PRIMARY KEY);
INSERT INTO @tach (id_thiet_bi)
SELECT id_thiet_bi FROM KK_ThietBi
WHERE NgayXoa IS NULL
  AND (ghi_chu LIKE N'Tách từ #1954 %' OR ghi_chu LIKE N'Tách từ #1957 %' OR ghi_chu LIKE N'Tách từ #1770 %');

/* (1) */
UPDATE b SET b.id_thiet_bi = m.id_goc
FROM KK_BangChungCheck b
JOIN @map m ON m.id_bang_chung = b.id_bang_chung
JOIN @tach t ON t.id_thiet_bi = b.id_thiet_bi;
PRINT N'Số bằng chứng trả về bản gốc: ' + CAST(@@ROWCOUNT AS nvarchar(10));

/* (2) */
UPDATE k SET k.NgayXoa = @now, k.LyDoXoa = N'Rollback tách máy in gộp ngày 2026-09-25', k.ngay_cap_nhat = @now
FROM KK_ThietBi k JOIN @tach t ON t.id_thiet_bi = k.id_thiet_bi;
PRINT N'Số máy in tách đã xoá mềm: ' + CAST(@@ROWCOUNT AS nvarchar(10));

/* (3) */
DECLARE @cu TABLE (id int PRIMARY KEY, serial nvarchar(255), quy_cach nvarchar(max));
INSERT INTO @cu VALUES
    (1954, N'APEOS PRINT 3360S', N'APEOS PRINT 3360S'),
    (1957, N'TTP-244PRO',        N'TTP-244PRO'),
    (1770, N'TTP-244 PRO',       N'tsp');

UPDATE t SET t.seribacode = c.serial, t.quy_cach = c.quy_cach, t.ghi_chu = NULL, t.ngay_cap_nhat = @now
FROM KK_ThietBi t JOIN @cu c ON c.id = t.id_thiet_bi
WHERE t.seribacode IS NULL;
PRINT N'Số bản gốc trả lại serial: ' + CAST(@@ROWCOUNT AS nvarchar(10));

INSERT INTO KK_LichSuThaoTac (HanhDong, DoiTuong, IdDoiTuong, ChiTiet, ThoiGian, NguoiThaoTac)
SELECT N'Cập nhật', N'Thiết Bị', id, N'[Rollback] Gộp lại máy in đã tách về bản gốc', @now, N'Script SQL' FROM @cu;

COMMIT;
