using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace E_Form_Best.Models.ITForm;

/// <summary>Điểm một năng lực trong phiếu (UNIQUE id_phieu + id_nang_luc).</summary>
[Table("DG_PhieuNangLuc")]
public partial class DgPhieuNangLuc
{
    [Key]
    [Column("id_phieu_nang_luc")]
    public Guid IdPhieuNangLuc { get; set; }

    [Column("id_phieu")]
    public Guid IdPhieu { get; set; }

    [Column("id_nang_luc")]
    public Guid IdNangLuc { get; set; }

    [Column("diem_thuc_te", TypeName = "decimal(5, 2)")]
    public decimal DiemThucTe { get; set; }

    [Column("diem_yeu_cau", TypeName = "decimal(5, 2)")]
    public decimal DiemYeuCau { get; set; }

    [Column("chenh_lech", TypeName = "decimal(5, 2)")]
    public decimal ChenhLech { get; set; }
}
