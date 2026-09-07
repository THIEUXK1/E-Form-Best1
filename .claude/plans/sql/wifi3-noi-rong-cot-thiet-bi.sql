/* ============================================================================
   Mục đích : Sửa lỗi đơn "Đăng ký sử dụng Wifi" (IT_DangKiSuDungWifi_3) không
              lưu được khi người dùng khai từ 4 thiết bị trở lên.

   Nguyên nhân:
     Controller ghép TẤT CẢ mã thiết bị và địa chỉ MAC của các dòng vào MỘT ô,
     ngăn cách bằng " | " (ITFormController.cs, action POST /FormIT/TaoIT_Wifi).
     Hai cột đích lại chỉ là nvarchar(100):
        MaThietBi  nvarchar(100)  -- mỗi mã ~23 ký tự  => tràn từ 4 thiết bị
        MacTB      nvarchar(100)  -- mỗi MAC 17 ký tự  => tràn từ 6 thiết bị
     Chuỗi vượt 100 ký tự -> SQL báo "String or binary data would be truncated"
     -> transaction rollback -> KHÔNG có đơn nào được tạo, nhưng identity vẫn bị
     tiêu tốn (đó là các khoảng trống id 2187, 2188, 2193 trong bảng FormIT).

   Ảnh hưởng : 1 bảng, 2 cột. Chỉ NỚI RỘNG kiểu dữ liệu, KHÔNG đụng dữ liệu cũ.
               Không mất dữ liệu, không khoá bảng lâu (bảng ~1.4k dòng).
   Rollback  : có ở cuối file — nhưng chỉ chạy được khi mọi giá trị hiện có
               vẫn <= 100 ký tự (câu SELECT đối soát bên dưới kiểm tra giúp).
   ========================================================================== */

USE ITForm;
GO

/* ---------- 1. ĐỐI SOÁT TRƯỚC ---------- */
SELECT
    COUNT(*)                        AS tong_dong,
    MAX(LEN(MaThietBi))             AS max_len_MaThietBi,
    MAX(LEN(MacTB))                 AS max_len_MacTB
FROM dbo.IT_DangKiSuDungWifi_3;

SELECT COLUMN_NAME, DATA_TYPE, CHARACTER_MAXIMUM_LENGTH
FROM INFORMATION_SCHEMA.COLUMNS
WHERE TABLE_NAME = 'IT_DangKiSuDungWifi_3'
  AND COLUMN_NAME IN ('MaThietBi', 'MacTB');
GO

/* ---------- 2. NỚI RỘNG CỘT (idempotent) ---------- */
IF EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS
           WHERE TABLE_NAME = 'IT_DangKiSuDungWifi_3'
             AND COLUMN_NAME = 'MaThietBi'
             AND CHARACTER_MAXIMUM_LENGTH <> 4000)
BEGIN
    ALTER TABLE dbo.IT_DangKiSuDungWifi_3 ALTER COLUMN MaThietBi nvarchar(4000) NULL;
END
GO

IF EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS
           WHERE TABLE_NAME = 'IT_DangKiSuDungWifi_3'
             AND COLUMN_NAME = 'MacTB'
             AND CHARACTER_MAXIMUM_LENGTH <> 4000)
BEGIN
    ALTER TABLE dbo.IT_DangKiSuDungWifi_3 ALTER COLUMN MacTB nvarchar(4000) NULL;
END
GO

/* ---------- 3. ĐỐI SOÁT SAU ---------- */
SELECT COLUMN_NAME, DATA_TYPE, CHARACTER_MAXIMUM_LENGTH
FROM INFORMATION_SCHEMA.COLUMNS
WHERE TABLE_NAME = 'IT_DangKiSuDungWifi_3'
  AND COLUMN_NAME IN ('MaThietBi', 'MacTB');
-- Kỳ vọng: cả hai cột trả về CHARACTER_MAXIMUM_LENGTH = 4000.

SELECT COUNT(*) AS tong_dong_sau FROM dbo.IT_DangKiSuDungWifi_3;
-- Kỳ vọng: bằng đúng tong_dong ở bước 1.
GO


/* ============================== ROLLBACK ==================================
   Chỉ chạy khi cần quay lui VÀ mọi giá trị hiện có đều <= 100 ký tự.
   Kiểm tra trước:
       SELECT COUNT(*) FROM dbo.IT_DangKiSuDungWifi_3
       WHERE LEN(MaThietBi) > 100 OR LEN(MacTB) > 100;   -- phải = 0

   ALTER TABLE dbo.IT_DangKiSuDungWifi_3 ALTER COLUMN MaThietBi nvarchar(100) NULL;
   ALTER TABLE dbo.IT_DangKiSuDungWifi_3 ALTER COLUMN MacTB     nvarchar(100) NULL;
   ========================================================================== */
