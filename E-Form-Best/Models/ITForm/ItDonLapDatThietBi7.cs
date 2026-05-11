using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace E_Form_Best.Models.ITForm;

[Table("IT_DonLapDatThietBi_7")]
public partial class ItDonLapDatThietBi7
{
    [Key]
    [Column("id")]
    public int Id { get; set; }

    [Column("id_FormIT")]
    public int? IdFormIt { get; set; }

    [StringLength(250)]
    public string? TenThietBi { get; set; }

    [StringLength(500)]
    public string? ViTri { get; set; }

    public string? MucDich { get; set; }

    [ForeignKey("IdFormIt")]
    [InverseProperty("ItDonLapDatThietBi7s")]
    public virtual FormIt? IdFormItNavigation { get; set; }
}
