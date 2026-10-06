/*
  Mục đích : Tách bản ghi PDA #1961 (serial giả "IDATA 50P") thành 18 PDA riêng.
             Ngày 2026-07-10 10:56 tài khoản V260608 gửi 1 lô 18 PDA ở luồng "Tài sản khác" nhưng điền
             tên model vào ô Serial → luồng này khớp theo Serial + Loại nên 17 dòng sau đè lên #1961.
             Mỗi dòng gửi vẫn để lại 1 dòng KK_BangChungCheck (3031..3048) kèm vị trí → dựng lại từ đó.
  Cách tách: - #1961 giữ bằng chứng 3031 (WA DỆT H1) + 3211 (kiểm lại WA DỆT H1 ngày 2026-07-15).
             - Bằng chứng 3032..3048 → mỗi dòng sinh 1 PDA mới (copy công ty/bộ phận/người dùng/trạng thái
               của #1961, ten_vi_tri = ghi chú của bằng chứng) rồi trỏ bằng chứng sang PDA mới.
             - Bỏ serial giả "IDATA 50P" ở cả 18 PDA (để trống, chờ bổ sung serial thật) — nếu giữ, lần
               kiểm kê sau lại dồn hết về một dòng. Model giữ ở quy_cach.
             Kết quả: WA DỆT H1 = 6, WA DỆT H3 = 6, WA CBS H1 = 3, WA CBS H3 = 3.
  Ngày     : 2026-09-25
  Chạy     : sqlcmd -S <ip> -d ITForm -U <user> -P <pass> -C -f 65001 -i kk-tach-pda-1961-20260925.sql
  Chạm     : KK_ThietBi (+17 dòng mới, sửa seribacode của #1961), KK_BangChungCheck (17 dòng đổi id_thiet_bi),
             KK_LichSuThaoTac (+18 dòng ghi vết, append-only).
  An toàn  : idempotent — chỉ xử lý bằng chứng còn trỏ về #1961; chạy lần hai không sinh thêm dòng.
             Rollback: kk-tach-pda-1961-20260925-rollback.sql cùng thư mục.
*/

SET NOCOUNT ON;
SET XACT_ABORT ON;

-- Đối soát trước khi chạy: phải thấy 19 bằng chứng trỏ về #1961
SELECT id_bang_chung, id_thiet_bi, thoi_gian_check, ghi_chu FROM KK_BangChungCheck WHERE id_thiet_bi = 1961 ORDER BY id_bang_chung;

BEGIN TRAN;

DECLARE @now datetime = GETDATE();
DECLARE @idBc int, @tg datetime, @viTri nvarchar(max), @idMoi int, @soTach int = 0;

DECLARE cur CURSOR LOCAL FAST_FORWARD FOR
    SELECT id_bang_chung, thoi_gian_check, ghi_chu
    FROM KK_BangChungCheck
    WHERE id_thiet_bi = 1961               -- chốt idempotency: đã tách thì bằng chứng không còn trỏ về 1961
      AND id_bang_chung BETWEEN 3032 AND 3048
    ORDER BY id_bang_chung;

OPEN cur;
FETCH NEXT FROM cur INTO @idBc, @tg, @viTri;
WHILE @@FETCH_STATUS = 0
BEGIN
    INSERT INTO KK_ThietBi
    (
        ten_vi_tri, ten_may_tinh, ten_dang_nhap, id_nguoi_dung,
        IDCongTy, IDBoPhan, loai_thiet_bi, id_trang_thai,
        quy_cach, seribacode, ghi_chu,
        ngay_tao, ngay_cap_nhat, thoi_gian_check
    )
    SELECT
        @viTri, NULL, g.ten_dang_nhap, g.id_nguoi_dung,
        g.IDCongTy, g.IDBoPhan, g.loai_thiet_bi, g.id_trang_thai,
        g.quy_cach, NULL,
        N'Tách từ #1961 (bằng chứng #' + CAST(@idBc AS nvarchar(10)) + N') - chờ bổ sung serial thật',
        @tg, @now, @tg                     -- giữ đúng mốc lúc người dùng gửi kiểm kê
    FROM KK_ThietBi g
    WHERE g.id_thiet_bi = 1961;

    SET @idMoi = SCOPE_IDENTITY();

    UPDATE KK_BangChungCheck SET id_thiet_bi = @idMoi WHERE id_bang_chung = @idBc;

    INSERT INTO KK_LichSuThaoTac (HanhDong, DoiTuong, IdDoiTuong, ChiTiet, ThoiGian, NguoiThaoTac)
    VALUES (N'Thêm mới', N'Thiết Bị', @idMoi,
            N'[Tách bản ghi] PDA IDATA 50P tách từ #1961 (bị đè do serial giả) | Vị trí: ' + ISNULL(@viTri, N'')
            + N' | Bằng chứng #' + CAST(@idBc AS nvarchar(10)),
            @now, N'Script SQL');

    SET @soTach += 1;
    FETCH NEXT FROM cur INTO @idBc, @tg, @viTri;
END
CLOSE cur;
DEALLOCATE cur;

PRINT N'Số PDA tách mới: ' + CAST(@soTach AS nvarchar(10));

-- #1961: bỏ serial giả, chỉ khi còn là giá trị giả (chạy lại không đụng serial thật đã bổ sung)
UPDATE KK_ThietBi
SET seribacode    = NULL,
    ghi_chu       = N'Đã tách 17 PDA cùng lô ra bản ghi riêng - chờ bổ sung serial thật',
    ngay_cap_nhat = @now
WHERE id_thiet_bi = 1961 AND seribacode = N'IDATA 50P';

IF @@ROWCOUNT > 0
    INSERT INTO KK_LichSuThaoTac (HanhDong, DoiTuong, IdDoiTuong, ChiTiet, ThoiGian, NguoiThaoTac)
    VALUES (N'Cập nhật', N'Thiết Bị', 1961,
            N'[Tách bản ghi] Bỏ serial giả "IDATA 50P", tách 17 PDA cùng lô 2026-07-10 ra bản ghi riêng',
            @now, N'Script SQL');

COMMIT;

-- Đối soát sau khi chạy: 18 PDA, mỗi dòng 1 bằng chứng (riêng #1961 có 2)
SELECT t.id_thiet_bi, t.ten_vi_tri, t.seribacode, t.quy_cach, t.ngay_tao,
       (SELECT COUNT(*) FROM KK_BangChungCheck b WHERE b.id_thiet_bi = t.id_thiet_bi) so_bang_chung
FROM KK_ThietBi t
WHERE t.id_thiet_bi = 1961 OR t.ghi_chu LIKE N'Tách từ #1961 %'
ORDER BY t.ten_vi_tri, t.id_thiet_bi;
