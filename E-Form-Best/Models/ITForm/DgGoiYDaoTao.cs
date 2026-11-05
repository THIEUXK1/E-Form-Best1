using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace E_Form_Best.Models.ITForm;

/// <summary>Nội dung đào tạo gợi ý khi năng lực có GAP âm, lấy từ "IT_Nội Dung Đào tạo.xlsx" (UNIQUE id_nang_luc + noi_dung).</summary>
[Table("DG_GoiYDaoTao")]
public partial class DgGoiYDaoTao
{
    [Key]
    [Column("id_goi_y")]
    public Guid IdGoiY { get; set; }

    [Column("id_nang_luc")]
    public Guid IdNangLuc { get; set; }

    [Column("noi_dung")]
    [StringLength(400)]
    public string NoiDung { get; set; } = null!;

    [Column("nguoi_dao_tao")]
    [StringLength(100)]
    public string? NguoiDaoTao { get; set; }
}
