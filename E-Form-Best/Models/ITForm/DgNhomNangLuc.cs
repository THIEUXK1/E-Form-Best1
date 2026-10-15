using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace E_Form_Best.Models.ITForm;

/// <summary>Nhóm năng lực đánh giá (Kiến thức, Kỹ năng, Thái độ, Ngoại ngữ). Bảng DG_* tạo bằng dg-danh-gia-20261015.sql.</summary>
[Table("DG_NhomNangLuc")]
public partial class DgNhomNangLuc
{
    [Key]
    [Column("id_nhom_nang_luc")]
    public Guid IdNhomNangLuc { get; set; }

    [Column("ma_nhom")]
    [StringLength(10)]
    public string MaNhom { get; set; } = null!;

    [Column("ten_nhom")]
    [StringLength(100)]
    public string TenNhom { get; set; } = null!;

    [Column("ten_nhom_en")]
    [StringLength(100)]
    public string? TenNhomEn { get; set; }

    [Column("mo_ta")]
    public string? MoTa { get; set; }
}
