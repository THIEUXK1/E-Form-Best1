using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace E_Form_Best.Models.ITForm;

[Table("IT_CT_NguoiHoTro")]
public partial class ItCtNguoiHoTro
{
    [Key]
    [Column("id")]
    public int Id { get; set; }

    [Column("idFormIT")]
    public int? IdFormIt { get; set; }

    [Column("idIT_NguoiHoTro")]
    public int? IdItNguoiHoTro { get; set; }

    public int? Stt { get; set; }

    [ForeignKey("IdFormIt")]
    [InverseProperty("ItCtNguoiHoTros")]
    public virtual FormIt? IdFormItNavigation { get; set; }

    [ForeignKey("IdItNguoiHoTro")]
    [InverseProperty("ItCtNguoiHoTros")]
    public virtual ItNguoiHoTro? IdItNguoiHoTroNavigation { get; set; }
}
