using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace E_Form_Best.Models.ITForm;

/// <summary>
/// Danh sách CHẶN thiết bị: các máy nằm trong bảng này sẽ bị ẩn khỏi toàn bộ danh sách/thống kê kiểm kê
/// (KK_ThietBi và TSCN_ThongTinMay) và không được đồng bộ/thêm mới trở lại.
/// Đối chiếu theo Serial (ưu tiên) hoặc Tên máy khi Serial rỗng/rác.
/// </summary>
[Table("KK_ThietBiChan")]
public partial class KkThietBiChan
{
    [Key]
    [Column("id_chan")]
    public int IdChan { get; set; }

    [Column("seri")]
    [StringLength(255)]
    public string? Seri { get; set; }

    [Column("ten_may")]
    [StringLength(255)]
    public string? TenMay { get; set; }

    [Column("ly_do")]
    [StringLength(500)]
    public string? LyDo { get; set; }

    [Column("nguoi_chan")]
    [StringLength(255)]
    public string? NguoiChan { get; set; }

    [Column("ngay_chan", TypeName = "datetime")]
    public DateTime NgayChan { get; set; }
}
