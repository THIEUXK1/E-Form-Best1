using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace E_Form_Best.Models.ITForm;

[Table("LichSuFormSHD")]
public partial class LichSuFormShd
{
    [Key]
    [Column("id")]
    public int Id { get; set; }

    [Column("idFormSHD")]
    public int? IdFormShd { get; set; }

    [StringLength(255)]
    public string? TieuDe { get; set; }

    [Column("mota")]
    public string? Mota { get; set; }

    [Column(TypeName = "datetime")]
    public DateTime? Time { get; set; }

    public bool? IsRead { get; set; }

    [ForeignKey("IdFormShd")]
    [InverseProperty("LichSuFormShds")]
    public virtual FormShd? IdFormShdNavigation { get; set; }
}
