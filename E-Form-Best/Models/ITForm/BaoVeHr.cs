using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace E_Form_Best.Models.ITForm;

[Table("BaoVeHr")]
public partial class BaoVeHr
{
    [Key]
    [Column("id")]
    public int Id { get; set; }

    [Column("idFormHR")]
    public int? IdFormHr { get; set; }

    public string? GhiChu { get; set; }

    [Column("idBaoVe")]
    [StringLength(50)]
    public string? IdBaoVe { get; set; }

    [StringLength(250)]
    public string? TenBaoVe { get; set; }

    [Column(TypeName = "datetime")]
    public DateTime? TimeBaoVe { get; set; }

    public int? TrangThai { get; set; }

    [ForeignKey("IdFormHr")]
    [InverseProperty("BaoVeHrs")]
    public virtual FormHr? IdFormHrNavigation { get; set; }
}
