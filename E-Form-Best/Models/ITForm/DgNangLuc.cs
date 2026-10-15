using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace E_Form_Best.Models.ITForm;

/// <summary>Năng lực. IdNhomCongViec null = năng lực chung, áp cho mọi nhóm công việc.</summary>
[Table("DG_NangLuc")]
public partial class DgNangLuc
{
    [Key]
    [Column("id_nang_luc")]
    public Guid IdNangLuc { get; set; }

    [Column("id_nhom_nang_luc")]
    public Guid IdNhomNangLuc { get; set; }

    [Column("id_nhom_cong_viec")]
    public Guid? IdNhomCongViec { get; set; }

    [Column("ma_nang_luc")]
    [StringLength(10)]
    public string MaNangLuc { get; set; } = null!;

    [Column("ten_nang_luc")]
    [StringLength(200)]
    public string TenNangLuc { get; set; } = null!;

    [Column("ten_nang_luc_en")]
    [StringLength(200)]
    public string? TenNangLucEn { get; set; }

    [Column("mo_ta")]
    public string? MoTa { get; set; }

    [Column("diem_toi_da")]
    public int DiemToiDa { get; set; }
}
