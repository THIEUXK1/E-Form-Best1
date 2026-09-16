using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace E_Form_Best.Models.ITForm;

/// <summary>
/// Danh mục Vị trí địa lý thực tế của thiết bị (Hải Dương, Nghệ An, Hưng Yên...).
/// Khác với <c>KkThietBi.TenViTri</c> - đó là vị trí đặt máy trong nhà xưởng/phòng ban,
/// còn bảng này là địa bàn/tỉnh nơi thiết bị thực sự đang nằm.
/// </summary>
[Table("KK_ViTriDiaLy")]
public partial class KkViTriDiaLy
{
    [Key]
    [Column("id_vi_tri_dia_ly")]
    public int IdViTriDiaLy { get; set; }

    [Column("ten_vi_tri_dia_ly")]
    [StringLength(150)]
    public string TenViTriDiaLy { get; set; } = null!;

    [Column("mo_ta")]
    [StringLength(500)]
    public string? MoTa { get; set; }

    /// <summary>Thứ tự hiển thị trong dropdown; NULL xếp cuối.</summary>
    [Column("thu_tu")]
    public int? ThuTu { get; set; }

    /// <summary>Tắt cờ này để ẩn địa điểm khỏi dropdown mà vẫn giữ dữ liệu thiết bị cũ đang trỏ tới.</summary>
    [Column("dang_su_dung")]
    public bool DangSuDung { get; set; }

    [Column("ngay_tao", TypeName = "datetime")]
    public DateTime? NgayTao { get; set; }

    [Column("ngay_cap_nhat", TypeName = "datetime")]
    public DateTime? NgayCapNhat { get; set; }

    [InverseProperty("IdViTriDiaLyNavigation")]
    public virtual ICollection<KkThietBi> KkThietBis { get; set; } = new List<KkThietBi>();
}
