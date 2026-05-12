using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace E_Form_Best.Models.ITForm;

[Table("KK_BoPhan")]
public partial class KkBoPhan
{
    [Key]
    [Column("IDBoPhan")]
    public int IdboPhan { get; set; }

    [StringLength(255)]
    public string TenBoPhan { get; set; } = null!;

    [Column("IDCongTy")]
    public int? IdcongTy { get; set; }

    [StringLength(500)]
    public string? GhiChu { get; set; }

    [Column(TypeName = "datetime")]
    public DateTime? NgayTao { get; set; }

    public bool? TrangThai { get; set; }

    [ForeignKey("IdcongTy")]
    [InverseProperty("KkBoPhans")]
    public virtual KkCongTy? IdcongTyNavigation { get; set; }

    [InverseProperty("IdboPhanNavigation")]
    public virtual ICollection<KkThietBi> KkThietBis { get; set; } = new List<KkThietBi>();
}
