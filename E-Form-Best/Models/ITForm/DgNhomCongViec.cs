using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace E_Form_Best.Models.ITForm;

/// <summary>Nhóm công việc: lọc năng lực riêng của nhóm và KPI mẫu.</summary>
[Table("DG_NhomCongViec")]
public partial class DgNhomCongViec
{
    [Key]
    [Column("id_nhom_cong_viec")]
    public Guid IdNhomCongViec { get; set; }

    [Column("ma_nhom")]
    [StringLength(50)]
    public string MaNhom { get; set; } = null!;

    [Column("ten_nhom")]
    [StringLength(200)]
    public string TenNhom { get; set; } = null!;

    [Column("ten_nhom_en")]
    [StringLength(200)]
    public string? TenNhomEn { get; set; }

    [Column("mo_ta")]
    public string? MoTa { get; set; }
}
