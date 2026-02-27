using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace E_Form_Best.Models.ITForm;

[Table("HR_DoiCaLam_8")]
public partial class HrDoiCaLam8
{
    [Key]
    [Column("id")]
    public int Id { get; set; }

    [Column("id_FormHR")]
    public int? IdFormHr { get; set; }

    public byte[]? Anh { get; set; }

    public string? DuongDanAnh { get; set; }

    public DateOnly? NgayCanDoi { get; set; }

    [StringLength(50)]
    public string? CaGoc { get; set; }

    [StringLength(50)]
    public string? CaMuonDoi { get; set; }

    public string? LyDoDoiCa { get; set; }

    [ForeignKey("IdFormHr")]
    [InverseProperty("HrDoiCaLam8s")]
    public virtual FormHr? IdFormHrNavigation { get; set; }
}
