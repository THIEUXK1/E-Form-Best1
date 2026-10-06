/*
  Rollback cho kk-tach-pda-1961-20260925.sql
    (1) Trỏ 17 bằng chứng 3032..3048 về lại #1961.
    (2) Xoá mềm 17 PDA đã tách (KHÔNG xoá cứng, giữ vết).
    (3) Trả serial "IDATA 50P" và ghi_chu NULL cho #1961.
  KK_LichSuThaoTac là append-only → không xoá log của lần tách, chỉ ghi thêm 1 dòng rollback.
  Ngày: 2026-09-25
*/

SET NOCOUNT ON;
SET XACT_ABORT ON;

BEGIN TRAN;

DECLARE @now datetime = GETDATE();
DECLARE @tach TABLE (id_thiet_bi int PRIMARY KEY);
INSERT INTO @tach (id_thiet_bi)
SELECT id_thiet_bi FROM KK_ThietBi WHERE ghi_chu LIKE N'Tách từ #1961 %' AND NgayXoa IS NULL;

/* (1) */
UPDATE b SET b.id_thiet_bi = 1961
FROM KK_BangChungCheck b JOIN @tach t ON t.id_thiet_bi = b.id_thiet_bi
WHERE b.id_bang_chung BETWEEN 3032 AND 3048;
PRINT N'Số bằng chứng trả về #1961: ' + CAST(@@ROWCOUNT AS nvarchar(10));

/* (2) */
UPDATE k SET k.NgayXoa = @now, k.LyDoXoa = N'Rollback tách PDA #1961 ngày 2026-09-25', k.ngay_cap_nhat = @now
FROM KK_ThietBi k JOIN @tach t ON t.id_thiet_bi = k.id_thiet_bi;
PRINT N'Số PDA tách đã xoá mềm: ' + CAST(@@ROWCOUNT AS nvarchar(10));

/* (3) */
UPDATE KK_ThietBi
SET seribacode = N'IDATA 50P', ghi_chu = NULL, ngay_cap_nhat = @now
WHERE id_thiet_bi = 1961 AND seribacode IS NULL;

INSERT INTO KK_LichSuThaoTac (HanhDong, DoiTuong, IdDoiTuong, ChiTiet, ThoiGian, NguoiThaoTac)
VALUES (N'Cập nhật', N'Thiết Bị', 1961, N'[Rollback] Gộp lại 17 PDA đã tách về #1961', @now, N'Script SQL');

COMMIT;
