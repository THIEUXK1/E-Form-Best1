/*
  Mục đích : Tách 3 bản ghi máy in bị gộp ở luồng "Tài sản khác" (khớp theo Serial + Loại) do người nhập
             điền TÊN MODEL vào ô Serial → các máy in cùng model dồn vào một dòng. Cùng lỗi với PDA #1961
             (kk-tach-pda-1961-20260925.sql).
               #1954 "APEOS PRINT 3360S" : 3 máy (WA ROOM, WA VW101, VA VW009) - V260608, cùng lô 2026-07-10 10:56
               #1957 "TTP-244PRO"        : 2 máy (WA ROOM, WA KHO)              - V260608, cùng lô 2026-07-10 10:56
               #1770 "TTP-244 PRO"       : 2 máy ở H2 tầng 1 - 2 người khác nhau khai, 2 ảnh khác nhau:
                                           Lò Văn Đức (V220669, 14:51) và Bạc Cầm Dương (V240961, 14:54)
  Không tách (đã soát, là GỬI LẠI cùng 1 máy): #1077 #1102 #1261 #1276 #1279 #1305 #1380 #1525 #1602 #1615
             #1787 #1788 #1973 — serial thật / mã tài sản riêng, hoặc cùng người bấm gửi nhiều lần.
  Cách tách: bản ghi gốc giữ bằng chứng CUỐI (đúng với vị trí đang lưu); mỗi bằng chứng trước đó sinh 1 máy in mới
             (copy công ty/bộ phận/trạng thái của bản gốc, vị trí = ghi chú bằng chứng, ảnh = ảnh bằng chứng),
             rồi trỏ bằng chứng sang máy mới. Máy tách từ #1770 đứng tên Lò Văn Đức (id 784).
             Bỏ serial giả ở cả 7 máy (để trống, chờ serial thật); tên model chuyển sang quy_cach.
             Từ 2026-09-25 máy in nhận diện trùng theo IP (appsettings KiemKe:TrungTaiSanKhac) → 7 máy này chưa có IP,
             lần kiểm kê/sửa tới phải nhập IP; không còn bị dồn về 1 dòng theo tên model nữa.
  Ngày     : 2026-09-25
  Chạy     : sqlcmd -S <ip> -d ITForm -U <user> -P <pass> -C -f 65001 -i kk-tach-mayin-gop-20260925.sql
  Chạm     : KK_ThietBi (+4 dòng mới, sửa seribacode/quy_cach của 3 bản gốc), KK_BangChungCheck (4 dòng đổi id_thiet_bi),
             KK_LichSuThaoTac (+7 dòng ghi vết, append-only).
  An toàn  : idempotent — chỉ xử lý bằng chứng còn trỏ về bản gốc; chạy lần hai không sinh thêm dòng.
             Rollback: kk-tach-mayin-gop-20260925-rollback.sql cùng thư mục.
*/

SET NOCOUNT ON;
SET XACT_ABORT ON;

-- Bằng chứng cần tách ra máy mới: (bản gốc, id bằng chứng, người đứng tên máy mới - NULL = giữ như bản gốc)
DECLARE @tach TABLE (id_goc int, id_bang_chung int, id_nguoi_dung int NULL, tk nvarchar(50) NULL);
INSERT INTO @tach (id_goc, id_bang_chung, id_nguoi_dung, tk) VALUES
    (1954, 3021, NULL, NULL),        -- WA ROOM
    (1954, 3022, NULL, NULL),        -- WA VW101      (#1954 giữ 3023 - VA VW009)
    (1957, 3026, NULL, NULL),        -- WA ROOM       (#1957 giữ 3027 - WA KHO)
    (1770, 2743, 784, N'V220669');   -- Lò Văn Đức    (#1770 giữ 2746 - Bạc Cầm Dương)

-- Đối soát trước khi chạy
SELECT b.id_thiet_bi, b.id_bang_chung, b.thoi_gian_check, b.ghi_chu, b.duong_dan_anh
FROM KK_BangChungCheck b WHERE b.id_thiet_bi IN (1770, 1954, 1957) ORDER BY b.id_thiet_bi, b.id_bang_chung;

BEGIN TRAN;

DECLARE @now datetime = GETDATE();
DECLARE @idGoc int, @idBc int, @idNd int, @tk nvarchar(50), @tg datetime, @viTri nvarchar(max), @anh nvarchar(max), @idMoi int, @soTach int = 0;

DECLARE cur CURSOR LOCAL FAST_FORWARD FOR
    SELECT t.id_goc, t.id_bang_chung, t.id_nguoi_dung, t.tk, b.thoi_gian_check, b.ghi_chu, b.duong_dan_anh
    FROM @tach t
    JOIN KK_BangChungCheck b ON b.id_bang_chung = t.id_bang_chung
                            AND b.id_thiet_bi = t.id_goc      -- chốt idempotency: đã tách thì không còn trỏ về bản gốc
    ORDER BY t.id_bang_chung;

OPEN cur;
FETCH NEXT FROM cur INTO @idGoc, @idBc, @idNd, @tk, @tg, @viTri, @anh;
WHILE @@FETCH_STATUS = 0
BEGIN
    INSERT INTO KK_ThietBi
    (
        ten_vi_tri, ten_may_tinh, ten_dang_nhap, id_nguoi_dung,
        IDCongTy, IDBoPhan, loai_thiet_bi, id_trang_thai,
        quy_cach, seribacode, duong_dan_anh, ghi_chu,
        ngay_tao, ngay_cap_nhat, thoi_gian_check
    )
    SELECT
        ISNULL(@viTri, N''), NULL, ISNULL(@tk, g.ten_dang_nhap), ISNULL(@idNd, g.id_nguoi_dung),
        g.IDCongTy, g.IDBoPhan, g.loai_thiet_bi, g.id_trang_thai,
        ISNULL(NULLIF(g.seribacode, N''), g.quy_cach),   -- serial giả chính là tên model
        NULL, @anh,
        N'Tách từ #' + CAST(@idGoc AS nvarchar(10)) + N' (bằng chứng #' + CAST(@idBc AS nvarchar(10)) + N') - chờ bổ sung serial thật',
        @tg, @now, @tg
    FROM KK_ThietBi g
    WHERE g.id_thiet_bi = @idGoc;

    SET @idMoi = SCOPE_IDENTITY();

    UPDATE KK_BangChungCheck SET id_thiet_bi = @idMoi WHERE id_bang_chung = @idBc;

    INSERT INTO KK_LichSuThaoTac (HanhDong, DoiTuong, IdDoiTuong, ChiTiet, ThoiGian, NguoiThaoTac)
    VALUES (N'Thêm mới', N'Thiết Bị', @idMoi,
            N'[Tách bản ghi] Máy in tách từ #' + CAST(@idGoc AS nvarchar(10)) + N' (bị đè do serial giả) | Vị trí: ' + ISNULL(@viTri, N'')
            + N' | Bằng chứng #' + CAST(@idBc AS nvarchar(10)) + ISNULL(N' | Account: ' + @tk, N''),
            @now, N'Script SQL');

    SET @soTach += 1;
    FETCH NEXT FROM cur INTO @idGoc, @idBc, @idNd, @tk, @tg, @viTri, @anh;
END
CLOSE cur;
DEALLOCATE cur;

PRINT N'Số máy in tách mới: ' + CAST(@soTach AS nvarchar(10));

-- Bản gốc: dời tên model sang quy_cach (#1770 đang ghi "tsp"), bỏ serial giả. Chỉ chạm khi serial vẫn là giá trị giả.
DECLARE @goc TABLE (id int PRIMARY KEY, serial_gia nvarchar(255));
INSERT INTO @goc VALUES (1954, N'APEOS PRINT 3360S'), (1957, N'TTP-244PRO'), (1770, N'TTP-244 PRO');

DECLARE @daSua TABLE (id int, quy_cach_cu nvarchar(max));

UPDATE t
SET t.quy_cach      = g.serial_gia,
    t.seribacode    = NULL,
    t.ghi_chu       = N'Đã tách máy in cùng model bị gộp ra bản ghi riêng - chờ bổ sung serial thật',
    t.ngay_cap_nhat = @now
OUTPUT inserted.id_thiet_bi, deleted.quy_cach INTO @daSua (id, quy_cach_cu)
FROM KK_ThietBi t JOIN @goc g ON g.id = t.id_thiet_bi
WHERE t.seribacode = g.serial_gia;

-- Ghi vết kèm quy_cach cũ để rollback trả đúng giá trị
INSERT INTO KK_LichSuThaoTac (HanhDong, DoiTuong, IdDoiTuong, ChiTiet, ThoiGian, NguoiThaoTac)
SELECT N'Cập nhật', N'Thiết Bị', d.id,
       N'[Tách bản ghi] Bỏ serial giả "' + g.serial_gia + N'", quy cách "' + ISNULL(d.quy_cach_cu, N'') + N'" → "' + g.serial_gia + N'"',
       @now, N'Script SQL'
FROM @daSua d JOIN @goc g ON g.id = d.id;

PRINT N'Số bản gốc đã bỏ serial giả: ' + CAST(@@ROWCOUNT AS nvarchar(10));

COMMIT;

-- Đối soát sau khi chạy: 7 máy in, mỗi máy 1 bằng chứng
SELECT t.id_thiet_bi, t.ten_vi_tri, t.ten_dang_nhap, t.seribacode, t.quy_cach, t.duong_dan_anh,
       (SELECT COUNT(*) FROM KK_BangChungCheck b WHERE b.id_thiet_bi = t.id_thiet_bi) so_bang_chung
FROM KK_ThietBi t
WHERE t.id_thiet_bi IN (1770, 1954, 1957)
   OR t.ghi_chu LIKE N'Tách từ #1770 %' OR t.ghi_chu LIKE N'Tách từ #1954 %' OR t.ghi_chu LIKE N'Tách từ #1957 %'
ORDER BY t.quy_cach, t.id_thiet_bi;
