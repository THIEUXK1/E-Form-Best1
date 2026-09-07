using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace E_Form_Best.Models.ITForm;

/// <summary>
/// Trạng thái bật/tắt hiệu ứng nền (mùa, ngày lễ) của từng người dùng.
/// Trước đây chỉ nằm ở localStorage nên đổi máy là mất và quản trị không biết ai đang dùng;
/// lưu xuống DB để giữ lựa chọn theo tài khoản và thống kê được mức độ sử dụng.
/// </summary>
[Table("User_HieuUngNen")]
public partial class UserHieuUngNen
{
    [Key]
    [Column("id_nguoi_dung")]
    public int IdNguoiDung { get; set; }

    [Column("bat")]
    public bool Bat { get; set; }

    [Column("ngay_cap_nhat", TypeName = "datetime")]
    public DateTime? NgayCapNhat { get; set; }

    /// <summary>Máy/trình duyệt của lần đổi gần nhất — để truy vết khi cần.</summary>
    [Column("ten_may")]
    [StringLength(255)]
    public string? TenMay { get; set; }

    /// <summary>
    /// Lần cuối trang người dùng báo còn đang mở với hiệu ứng bật (ping 30 phút/lần).
    /// Khác <see cref="NgayCapNhat"/> — cái đó chỉ là lần cuối bấm nút bật/tắt.
    /// </summary>
    [Column("lan_cuoi_ping", TypeName = "datetime")]
    public DateTime? LanCuoiPing { get; set; }

    [ForeignKey("IdNguoiDung")]
    public virtual User? IdNguoiDungNavigation { get; set; }
}
