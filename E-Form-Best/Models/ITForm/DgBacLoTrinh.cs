using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace E_Form_Best.Models.ITForm;

/// <summary>Bậc trong lộ trình thăng tiến IT, theo tài liệu IT 25090401 (BP-HR-PR-026 A/2). Dòng Chủ nhiệm gộp 4C–4A.</summary>
[Table("DG_BacLoTrinh")]
public partial class DgBacLoTrinh
{
    [Key]
    [Column("ma_bac")]
    [StringLength(10)]
    public string MaBac { get; set; } = null!;

    [Column("thu_tu")]
    public byte ThuTu { get; set; }

    [Column("chuc_danh")]
    [StringLength(100)]
    public string ChucDanh { get; set; } = null!;

    [Column("chuc_danh_en")]
    [StringLength(100)]
    public string ChucDanhEn { get; set; } = null!;

    [Column("thang_toi_thieu")]
    public byte ThangToiThieu { get; set; }

    [Column("thang_toi_da")]
    public byte ThangToiDa { get; set; }

    /// <summary>Mỗi dòng một kỹ năng.</summary>
    [Column("ky_nang")]
    public string KyNang { get; set; } = null!;
}
