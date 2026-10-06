/* Rollback kk-trang-thai-kho-it-20261006.sql — xoá trạng thái "Kho IT".
   Chỉ xoá khi CHƯA có thiết bị nào mang trạng thái này; nếu có thì dừng để người dùng tự chuyển
   các thiết bị đó sang trạng thái khác trước (tránh mất dấu thiết bị đang ở kho).
   Lưu ý chạy: sqlcmd phải thêm -f 65001. */

USE ITForm;
GO

DECLARE @id INT = (SELECT TOP 1 id_trang_thai FROM dbo.KK_TrangThai WHERE LTRIM(RTRIM(LOWER(ten_trang_thai))) = N'kho it');
DECLARE @soThietBi INT = (SELECT COUNT(*) FROM dbo.KK_ThietBi WHERE id_trang_thai = @id);

IF @id IS NULL
    PRINT N'Không có trạng thái Kho IT — không cần rollback.';
ELSE IF @soThietBi > 0
    PRINT N'DỪNG: còn ' + CAST(@soThietBi AS NVARCHAR(10)) + N' thiết bị đang ở trạng thái Kho IT.';
ELSE
    DELETE FROM dbo.KK_TrangThai WHERE id_trang_thai = @id;
GO
