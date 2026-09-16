/* ============================================================================
   Mục đích : Thêm "Vị trí địa lý thực tế" (Hải Dương, Nghệ An, Hưng Yên...) cho
              thiết bị kiểm kê, quản lý bằng MỘT BẢNG DANH MỤC riêng để sau này
              tự thêm/sửa địa điểm trên web, không phải sửa code.

   Phạm vi   : - TẠO MỚI 1 bảng : dbo.KK_ViTriDiaLy          (không đụng dữ liệu cũ)
               - THÊM 1 cột     : dbo.KK_ThietBi.id_vi_tri_dia_ly  (NULL được)
               - THÊM 1 khoá ngoại KK_ThietBi -> KK_ViTriDiaLy
               - NẠP 3 dòng danh mục mẫu (chỉ nạp nếu chưa có)

   An toàn   : Cột mới cho phép NULL nên toàn bộ ~ dữ liệu KK_ThietBi hiện có
               KHÔNG bị ảnh hưởng, không cần backfill, không khoá bảng lâu.
               Toàn bộ script idempotent - chạy lại lần hai không nổ, không nhân đôi.
   Rollback  : có ở cuối file (gỡ FK -> gỡ cột -> xoá bảng).
   Lưu ý chạy: file có tiếng Việt có dấu -> nếu chạy bằng sqlcmd phải thêm -f 65001,
               không thì dữ liệu vào DB bị lỗi font.
   ========================================================================== */

USE ITForm;
GO

/* ---------- 1. ĐỐI SOÁT TRƯỚC ---------- */
SELECT COUNT(*) AS tong_thiet_bi_truoc FROM dbo.KK_ThietBi;

SELECT COUNT(*) AS bang_danh_muc_da_ton_tai
FROM INFORMATION_SCHEMA.TABLES
WHERE TABLE_SCHEMA = 'dbo' AND TABLE_NAME = 'KK_ViTriDiaLy';
-- Kỳ vọng lần chạy đầu: tong_thiet_bi_truoc = số thiết bị hiện có, bang_danh_muc_da_ton_tai = 0
GO


/* ---------- 2. TẠO BẢNG DANH MỤC VỊ TRÍ ĐỊA LÝ ---------- */
IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.TABLES
               WHERE TABLE_SCHEMA = 'dbo' AND TABLE_NAME = 'KK_ViTriDiaLy')
BEGIN
    CREATE TABLE dbo.KK_ViTriDiaLy
    (
        id_vi_tri_dia_ly  int            IDENTITY(1,1) NOT NULL,
        ten_vi_tri_dia_ly nvarchar(150)  NOT NULL,          -- VD: Hải Dương, Nghệ An, Hưng Yên
        mo_ta             nvarchar(500)  NULL,              -- Ghi chú thêm (địa chỉ nhà máy, chi nhánh...)
        thu_tu            int            NULL,              -- Thứ tự hiển thị trong dropdown, NULL xếp cuối
        dang_su_dung      bit            NOT NULL CONSTRAINT DF_KK_ViTriDiaLy_DangSuDung DEFAULT (1),
        ngay_tao          datetime       NULL CONSTRAINT DF_KK_ViTriDiaLy_NgayTao DEFAULT (GETDATE()),
        ngay_cap_nhat     datetime       NULL,
        CONSTRAINT PK_KK_ViTriDiaLy PRIMARY KEY CLUSTERED (id_vi_tri_dia_ly)
    );
END
GO

/* Không cho trùng tên địa điểm (chặn ngay ở tầng DB, không chỉ tin validation ứng dụng) */
IF NOT EXISTS (SELECT 1 FROM sys.indexes
               WHERE name = 'UQ_KK_ViTriDiaLy_Ten'
                 AND object_id = OBJECT_ID('dbo.KK_ViTriDiaLy'))
BEGIN
    CREATE UNIQUE INDEX UQ_KK_ViTriDiaLy_Ten
        ON dbo.KK_ViTriDiaLy (ten_vi_tri_dia_ly);
END
GO


/* ---------- 3. NẠP DANH MỤC MẪU (chỉ thêm dòng còn thiếu) ---------- */
INSERT INTO dbo.KK_ViTriDiaLy (ten_vi_tri_dia_ly, thu_tu, dang_su_dung, ngay_tao)
SELECT ds.ten, ds.thu_tu, 1, GETDATE()
FROM (VALUES
        (N'Hải Dương', 1),
        (N'Nghệ An',   2),
        (N'Hưng Yên',  3)
     ) AS ds (ten, thu_tu)
WHERE NOT EXISTS (SELECT 1 FROM dbo.KK_ViTriDiaLy v
                  WHERE v.ten_vi_tri_dia_ly = ds.ten);
GO


/* ---------- 4. THÊM CỘT KHOÁ NGOẠI VÀO KK_ThietBi ---------- */
IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS
               WHERE TABLE_SCHEMA = 'dbo' AND TABLE_NAME = 'KK_ThietBi'
                 AND COLUMN_NAME = 'id_vi_tri_dia_ly')
BEGIN
    -- NULL được: thiết bị cũ chưa khai vị trí địa lý vẫn hợp lệ, không cần backfill
    ALTER TABLE dbo.KK_ThietBi ADD id_vi_tri_dia_ly int NULL;
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys
               WHERE name = 'FK_KK_ThietBi_ViTriDiaLy'
                 AND parent_object_id = OBJECT_ID('dbo.KK_ThietBi'))
BEGIN
    ALTER TABLE dbo.KK_ThietBi WITH CHECK
        ADD CONSTRAINT FK_KK_ThietBi_ViTriDiaLy
        FOREIGN KEY (id_vi_tri_dia_ly) REFERENCES dbo.KK_ViTriDiaLy (id_vi_tri_dia_ly);
END
GO

/* Lọc/thống kê theo vị trí địa lý sẽ chạy thường xuyên -> đánh index cho cột mới */
IF NOT EXISTS (SELECT 1 FROM sys.indexes
               WHERE name = 'IX_KK_ThietBi_ViTriDiaLy'
                 AND object_id = OBJECT_ID('dbo.KK_ThietBi'))
BEGIN
    CREATE INDEX IX_KK_ThietBi_ViTriDiaLy
        ON dbo.KK_ThietBi (id_vi_tri_dia_ly);
END
GO


/* ---------- 5. ĐỐI SOÁT SAU ---------- */
SELECT id_vi_tri_dia_ly, ten_vi_tri_dia_ly, thu_tu, dang_su_dung
FROM dbo.KK_ViTriDiaLy
ORDER BY thu_tu;
-- Kỳ vọng: đúng 3 dòng Hải Dương / Nghệ An / Hưng Yên, chữ có dấu hiển thị đúng.

SELECT COLUMN_NAME, DATA_TYPE, IS_NULLABLE
FROM INFORMATION_SCHEMA.COLUMNS
WHERE TABLE_NAME = 'KK_ThietBi' AND COLUMN_NAME = 'id_vi_tri_dia_ly';
-- Kỳ vọng: int / YES

SELECT COUNT(*) AS tong_thiet_bi_sau FROM dbo.KK_ThietBi;
-- Kỳ vọng: bằng đúng tong_thiet_bi_truoc ở bước 1 (không mất/không thêm dòng nào).
GO


/* ============================== ROLLBACK ==================================
   Chạy theo đúng thứ tự dưới đây (gỡ ràng buộc trước, xoá bảng sau).
   CẢNH BÁO: bước xoá cột sẽ mất toàn bộ vị trí địa lý đã khai cho thiết bị.

   IF EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_KK_ThietBi_ViTriDiaLy'
              AND object_id = OBJECT_ID('dbo.KK_ThietBi'))
       DROP INDEX IX_KK_ThietBi_ViTriDiaLy ON dbo.KK_ThietBi;

   IF EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = 'FK_KK_ThietBi_ViTriDiaLy')
       ALTER TABLE dbo.KK_ThietBi DROP CONSTRAINT FK_KK_ThietBi_ViTriDiaLy;

   IF EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS
              WHERE TABLE_NAME = 'KK_ThietBi' AND COLUMN_NAME = 'id_vi_tri_dia_ly')
       ALTER TABLE dbo.KK_ThietBi DROP COLUMN id_vi_tri_dia_ly;

   IF EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.TABLES
              WHERE TABLE_SCHEMA = 'dbo' AND TABLE_NAME = 'KK_ViTriDiaLy')
       DROP TABLE dbo.KK_ViTriDiaLy;
   ========================================================================== */
