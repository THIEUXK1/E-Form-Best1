/*
    Mục đích: biết tài khoản nào ĐANG THỰC SỰ dùng hiệu ứng nền, không chỉ "lần cuối bấm nút".
    Trang người dùng cứ 30 phút gửi một ping khi hiệu ứng đang bật; quản trị coi là đang dùng
    nếu ping nằm trong 35 phút gần nhất (đệm 5 phút cho trễ mạng / máy ngủ).

    Bảng: dbo.User_HieuUngNen   (thêm 1 cột, không đụng dữ liệu cũ)
    Chạm dữ liệu cũ: KHÔNG. Cột mới NULL được nên hàng đang có vẫn hợp lệ.
    Chạy lại lần hai: an toàn (có IF NOT EXISTS).
    Rollback: xem phần DOWN ở cuối file.
    Yêu cầu backup trước: không bắt buộc (chỉ thêm cột NULL, không sửa/xoá dữ liệu).
*/

-- ===================== ĐỐI SOÁT TRƯỚC =====================
SELECT COUNT(*) AS TongDong_Truoc FROM dbo.User_HieuUngNen;
GO

-- ===================== UP =====================
IF NOT EXISTS (
    SELECT 1 FROM sys.columns
    WHERE object_id = OBJECT_ID('dbo.User_HieuUngNen') AND [name] = 'lan_cuoi_ping'
)
    ALTER TABLE dbo.User_HieuUngNen ADD lan_cuoi_ping DATETIME NULL;
GO

-- Lọc "ai đang dùng" theo mốc thời gian
IF NOT EXISTS (
    SELECT 1 FROM sys.indexes
    WHERE [name] = 'IX_User_HieuUngNen_lan_cuoi_ping' AND object_id = OBJECT_ID('dbo.User_HieuUngNen')
)
    CREATE INDEX IX_User_HieuUngNen_lan_cuoi_ping
        ON dbo.User_HieuUngNen (lan_cuoi_ping) INCLUDE (bat);
GO

-- ===================== ĐỐI SOÁT SAU =====================
-- Số dòng phải KHÔNG đổi so với TongDong_Truoc; cột mới toàn NULL cho tới khi có ping đầu tiên.
SELECT COUNT(*) AS TongDong_Sau,
       SUM(CASE WHEN bat = 1 THEN 1 ELSE 0 END) AS DangBat,
       SUM(CASE WHEN lan_cuoi_ping IS NOT NULL THEN 1 ELSE 0 END) AS DaCoPing
FROM dbo.User_HieuUngNen;
GO

-- Ai đang thực sự dùng (ping trong 35 phút gần nhất)
SELECT u.id_nguoi_dung, u.ho_ten, u.TK, h.bat, h.lan_cuoi_ping
FROM dbo.User_HieuUngNen h
JOIN dbo.[User] u ON u.id_nguoi_dung = h.id_nguoi_dung
WHERE h.bat = 1 AND h.lan_cuoi_ping > DATEADD(MINUTE, -35, GETDATE())
ORDER BY h.lan_cuoi_ping DESC;
GO

/* ===================== DOWN (rollback) =====================
IF EXISTS (
    SELECT 1 FROM sys.indexes
    WHERE [name] = 'IX_User_HieuUngNen_lan_cuoi_ping' AND object_id = OBJECT_ID('dbo.User_HieuUngNen')
)
    DROP INDEX IX_User_HieuUngNen_lan_cuoi_ping ON dbo.User_HieuUngNen;
GO

IF EXISTS (
    SELECT 1 FROM sys.columns
    WHERE object_id = OBJECT_ID('dbo.User_HieuUngNen') AND [name] = 'lan_cuoi_ping'
)
    ALTER TABLE dbo.User_HieuUngNen DROP COLUMN lan_cuoi_ping;
GO
*/
