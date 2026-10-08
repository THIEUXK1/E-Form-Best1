/* ============================================================================
   Mục đích : Tách đầu ghi chi nhánh (KK_DauGhi) theo công ty để tab "Đầu ghi chi nhánh" của
              /QLCamera/PFVN (và MEGA) chỉ hiện đầu ghi của công ty đó.

   Phạm vi   : - THÊM 1 cột vào dbo.KK_DauGhi : IDCongTy INT NULL (khoá ngoại tới KK_CongTy.IDCongTy)
               - GÁN công ty cho 15 đầu ghi đang có (chỉ dòng IDCongTy còn NULL):
                   ip_local 10.0.200.x ("Nghệ An", 5 đầu ghi) -> PFVN ; còn lại -> BPVN ; MEGA chưa có
               - Code hiểu NULL = BPVN (phòng dòng thêm bằng tay ngoài web)
   An toàn   : Idempotent (chạy lại không lỗi, không gán đè dòng đã có công ty).
               Có gán dữ liệu -> bước 0 sao lưu bảng trước (KK_DauGhi_bak_20261007).
               Rollback: kk-dau-ghi-cong-ty-20261007-rollback.sql
   Thứ tự    : Chạy script này XONG rồi mới deploy code có KkDauGhi.IdcongTy —
               ngược lại tab Đầu ghi lỗi "Invalid column name 'IDCongTy'".
   Lưu ý chạy: sqlcmd -f 65001.
   ========================================================================== */

USE ITForm;
GO

/* ---------- 0. SAO LƯU (bảng nhỏ, ~15 dòng) ---------- */
IF OBJECT_ID('dbo.KK_DauGhi_bak_20261007') IS NULL
    SELECT * INTO dbo.KK_DauGhi_bak_20261007 FROM dbo.KK_DauGhi;
GO

/* ---------- 1. ĐỐI SOÁT TRƯỚC ---------- */
SELECT COUNT(*) AS so_dau_ghi,
       (SELECT COUNT(*) FROM INFORMATION_SCHEMA.COLUMNS
        WHERE TABLE_SCHEMA = 'dbo' AND TABLE_NAME = 'KK_DauGhi' AND COLUMN_NAME = 'IDCongTy') AS cot_da_co
FROM dbo.KK_DauGhi;
-- Kỳ vọng lần đầu: cot_da_co = 0
GO

/* ---------- 2. THÊM CỘT ---------- */
IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS
               WHERE TABLE_SCHEMA = 'dbo' AND TABLE_NAME = 'KK_DauGhi' AND COLUMN_NAME = 'IDCongTy')
    ALTER TABLE dbo.KK_DauGhi ADD IDCongTy INT NULL;
GO

IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = 'FK_KK_DauGhi_KK_CongTy')
    ALTER TABLE dbo.KK_DauGhi WITH CHECK
        ADD CONSTRAINT FK_KK_DauGhi_KK_CongTy FOREIGN KEY (IDCongTy) REFERENCES dbo.KK_CongTy (IDCongTy);
GO

/* ---------- 3. GÁN CÔNG TY CHO ĐẦU GHI ĐANG CÓ (chỉ dòng chưa gán) ---------- */
-- Lấy id theo tên, không viết cứng số id
UPDATE dg SET dg.IDCongTy = ct.IDCongTy
FROM dbo.KK_DauGhi dg
JOIN dbo.KK_CongTy ct ON ct.TenCongTy = 'PFVN'
WHERE dg.IDCongTy IS NULL AND dg.ip_local LIKE '10.0.200.%';

UPDATE dg SET dg.IDCongTy = ct.IDCongTy
FROM dbo.KK_DauGhi dg
JOIN dbo.KK_CongTy ct ON ct.TenCongTy = 'BPVN'
WHERE dg.IDCongTy IS NULL;
GO

/* ---------- 4. ĐỐI SOÁT SAU ---------- */
SELECT COUNT(*) AS so_dau_ghi FROM dbo.KK_DauGhi;               -- phải bằng số ở bước 1
SELECT ISNULL(ct.TenCongTy, '(chưa gán)') AS cong_ty, COUNT(*) AS so_dau_ghi   -- kỳ vọng: BPVN 10, PFVN 5
FROM dbo.KK_DauGhi dg LEFT JOIN dbo.KK_CongTy ct ON ct.IDCongTy = dg.IDCongTy
GROUP BY ct.TenCongTy;
SELECT name AS khoa_ngoai FROM sys.foreign_keys WHERE name = 'FK_KK_DauGhi_KK_CongTy';
GO
