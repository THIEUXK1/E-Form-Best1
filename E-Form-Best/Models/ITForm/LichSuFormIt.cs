using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace E_Form_Best.Models.ITForm;

[Table("LichSuFormIT")]
public partial class LichSuFormIt
{
    [Key]
    [Column("id")]
    public int Id { get; set; }

    [Column("idFormIT")]
    public int? IdFormIt { get; set; }

    [StringLength(255)]
    public string? TieuDe { get; set; }

    [Column("mota")]
    public string? Mota { get; set; }

    [Column(TypeName = "datetime")]
    public DateTime? Time { get; set; }

    public bool? IsRead { get; set; }

    [ForeignKey("IdFormIt")]
    [InverseProperty("LichSuFormIts")]
    public virtual FormIt? IdFormItNavigation { get; set; }
}
