using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace E_Form_Best.Models.ITForm;

[Table("LichSuFormHR")]
public partial class LichSuFormHr
{
    [Key]
    [Column("id")]
    public int Id { get; set; }

    [Column("idFormHR")]
    public int? IdFormHr { get; set; }

    [StringLength(255)]
    public string? TieuDe { get; set; }

    [Column("mota")]
    public string? Mota { get; set; }

    [Column(TypeName = "datetime")]
    public DateTime? Time { get; set; }

    public bool? IsRead { get; set; }

    public bool TrangThaiAnHien { get; set; }

    [ForeignKey("IdFormHr")]
    [InverseProperty("LichSuFormHrs")]
    public virtual FormHr? IdFormHrNavigation { get; set; }
}
