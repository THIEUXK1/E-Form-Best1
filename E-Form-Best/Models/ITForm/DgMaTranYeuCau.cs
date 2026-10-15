using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace E_Form_Best.Models.ITForm;

/// <summary>Điểm yêu cầu của một năng lực ở một cấp bậc (UNIQUE id_nang_luc + cap_bac).</summary>
[Table("DG_MaTranYeuCau")]
public partial class DgMaTranYeuCau
{
    [Key]
    [Column("id_ma_tran")]
    public Guid IdMaTran { get; set; }

    [Column("id_nang_luc")]
    public Guid IdNangLuc { get; set; }

    [Column("cap_bac")]
    [StringLength(10)]
    public string CapBac { get; set; } = null!;

    [Column("diem_yeu_cau", TypeName = "decimal(5, 2)")]
    public decimal DiemYeuCau { get; set; }
}
