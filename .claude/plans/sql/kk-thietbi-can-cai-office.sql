/* ============================================================================
   Mục đích : Ghi nhận câu hỏi bắt buộc khi kiểm kê "Máy này có cần cài Office
              hay không?". Kết quả dùng để quyết định máy có được cài / nâng cấp
              Office trong thời gian tới.

   Phạm vi   : THÊM 2 cột vào dbo.KK_ThietBi (đều NULL được):
                 can_cai_office          bit       - Có (1) / Không (0), NULL = chưa từng hỏi
                 ngay_tra_loi_office     datetime  - Lần trả lời gần nhất, để đối chiếu độ mới
               KHÔNG đụng tới dữ liệu sẵn có, không backfill.

   An toàn   : Cột NULL được nên ~2.4k thiết bị hiện có vẫn hợp lệ (hiểu là "chưa
               trả lời"). Idempotent - chạy lại lần hai không nổ.
   Rollback  : có ở cuối file (xoá 2 cột -> mất toàn bộ câu trả lời đã thu).
   Lưu ý chạy: file có tiếng Việt; dùng sqlcmd thì thêm -f 65001.
   ========================================================================== */

USE ITForm;
GO

/* ---------- 1. ĐỐI SOÁT TRƯỚC ---------- */
SELECT COUNT(*) AS tong_thiet_bi_truoc FROM dbo.KK_ThietBi;

SELECT COUNT(*) AS so_cot_da_co
FROM INFORMATION_SCHEMA.COLUMNS
WHERE TABLE_NAME = 'KK_ThietBi'
  AND COLUMN_NAME IN ('can_cai_office', 'ngay_tra_loi_office');
-- Kỳ vọng lần chạy đầu: so_cot_da_co = 0
GO


/* ---------- 2. THÊM CỘT ---------- */
IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS
               WHERE TABLE_SCHEMA = 'dbo' AND TABLE_NAME = 'KK_ThietBi'
                 AND COLUMN_NAME = 'can_cai_office')
BEGIN
    -- KHÔNG đặt DEFAULT: phải phân biệt được "chưa từng hỏi" (NULL) với "trả lời Không" (0)
    ALTER TABLE dbo.KK_ThietBi ADD can_cai_office bit NULL;
END
GO

IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS
               WHERE TABLE_SCHEMA = 'dbo' AND TABLE_NAME = 'KK_ThietBi'
                 AND COLUMN_NAME = 'ngay_tra_loi_office')
BEGIN
    ALTER TABLE dbo.KK_ThietBi ADD ngay_tra_loi_office datetime NULL;
END
GO


/* ---------- 3. ĐỐI SOÁT SAU ---------- */
SELECT COLUMN_NAME, DATA_TYPE, IS_NULLABLE
FROM INFORMATION_SCHEMA.COLUMNS
WHERE TABLE_NAME = 'KK_ThietBi'
  AND COLUMN_NAME IN ('can_cai_office', 'ngay_tra_loi_office');
-- Kỳ vọng: can_cai_office / bit / YES  và  ngay_tra_loi_office / datetime / YES

SELECT COUNT(*) AS tong_thiet_bi_sau FROM dbo.KK_ThietBi;
-- Kỳ vọng: bằng đúng tong_thiet_bi_truoc ở bước 1.
GO


/* ============================== ROLLBACK ==================================
   CẢNH BÁO: mất toàn bộ câu trả lời "có cần cài Office" đã thu được.

   IF EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS
              WHERE TABLE_NAME = 'KK_ThietBi' AND COLUMN_NAME = 'can_cai_office')
       ALTER TABLE dbo.KK_ThietBi DROP COLUMN can_cai_office;

   IF EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS
              WHERE TABLE_NAME = 'KK_ThietBi' AND COLUMN_NAME = 'ngay_tra_loi_office')
       ALTER TABLE dbo.KK_ThietBi DROP COLUMN ngay_tra_loi_office;
   ========================================================================== */
