using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace E_Form_Best.Models.ITForm;

[Table("KK_CongTy")]
public partial class KkCongTy
{
    [Key]
    [Column("IDCongTy")]
    public int IdcongTy { get; set; }

    [StringLength(255)]
    public string TenCongTy { get; set; } = null!;

    [StringLength(500)]
    public string? GhiChu { get; set; }

    [Column(TypeName = "datetime")]
    public DateTime? NgayTao { get; set; }

    public bool? TrangThai { get; set; }

    [InverseProperty("IdcongTyNavigation")]
    public virtual ICollection<KkBoPhan> KkBoPhans { get; set; } = new List<KkBoPhan>();

    [InverseProperty("IdcongTyNavigation")]
    public virtual ICollection<KkThietBi> KkThietBis { get; set; } = new List<KkThietBi>();
}
