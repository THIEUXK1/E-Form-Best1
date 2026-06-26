using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace E_Form_Best.Models.ITForm;

[Table("TSCN_LichSuThayDoi")]
public partial class TscnLichSuThayDoi
{
    [Key]
    public int IdLog { get; set; }

    public int IdMay { get; set; }

    [StringLength(100)]
    public string TenTruongThayDoi { get; set; } = null!;

    public string? GiaTriCu { get; set; }

    public string? GiaTriMoi { get; set; }

    [Column(TypeName = "datetime")]
    public DateTime? ThoiGianThayDoi { get; set; }

    [StringLength(250)]
    public string? GhiChu { get; set; }

    [ForeignKey("IdMay")]
    [InverseProperty("TscnLichSuThayDois")]
    public virtual TscnThongTinMay IdMayNavigation { get; set; } = null!;
}
