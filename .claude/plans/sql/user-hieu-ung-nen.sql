/*
    Mục đích: lưu theo từng người dùng việc BẬT/TẮT hiệu ứng nền (mùa / ngày lễ),
    để biết ai đang bật và dùng hiệu ứng — hiện trạng chỉ lưu ở localStorage của trình duyệt
    nên đổi máy là mất, và phía máy chủ không thống kê được.

    Bảng: dbo.User_HieuUngNen   (1 người dùng = 1 dòng)
    Chạm dữ liệu cũ: KHÔNG (chỉ tạo bảng mới, không sửa bảng [User]).
    Chạy lại lần hai: an toàn (có IF NOT EXISTS).
    Rollback: xem phần DOWN ở cuối file.
*/

-- ===================== UP =====================
IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE [name] = 'User_HieuUngNen' AND SCHEMA_NAME([schema_id]) = 'dbo')
BEGIN
    CREATE TABLE dbo.User_HieuUngNen
    (
        id_nguoi_dung  INT           NOT NULL,
        bat            BIT           NOT NULL CONSTRAINT DF_User_HieuUngNen_bat DEFAULT (0),
        ngay_cap_nhat  DATETIME      NULL,
        ten_may        NVARCHAR(255) NULL,   -- máy/trình duyệt đổi lần gần nhất, để truy vết
        CONSTRAINT PK_User_HieuUngNen PRIMARY KEY (id_nguoi_dung),
        CONSTRAINT FK_User_HieuUngNen_User FOREIGN KEY (id_nguoi_dung)
            REFERENCES dbo.[User] (id_nguoi_dung)
    );
END
GO

-- Lọc nhanh "ai đang bật"
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE [name] = 'IX_User_HieuUngNen_bat' AND object_id = OBJECT_ID('dbo.User_HieuUngNen'))
    CREATE INDEX IX_User_HieuUngNen_bat ON dbo.User_HieuUngNen (bat) INCLUDE (ngay_cap_nhat);
GO

-- ===================== ĐỐI SOÁT =====================
-- Chạy trước và sau khi deploy code để xác minh
SELECT COUNT(*) AS TongDong, SUM(CASE WHEN bat = 1 THEN 1 ELSE 0 END) AS DangBat
FROM dbo.User_HieuUngNen;

-- Danh sách người đang bật hiệu ứng
SELECT u.id_nguoi_dung, u.ho_ten, u.TK, u.phong_ban, h.ngay_cap_nhat, h.ten_may
FROM dbo.User_HieuUngNen h
JOIN dbo.[User] u ON u.id_nguoi_dung = h.id_nguoi_dung
WHERE h.bat = 1
ORDER BY h.ngay_cap_nhat DESC;
GO

-- ===================== DOWN (rollback) =====================
/*
IF EXISTS (SELECT 1 FROM sys.tables WHERE [name] = 'User_HieuUngNen' AND SCHEMA_NAME([schema_id]) = 'dbo')
    DROP TABLE dbo.User_HieuUngNen;
GO
*/
