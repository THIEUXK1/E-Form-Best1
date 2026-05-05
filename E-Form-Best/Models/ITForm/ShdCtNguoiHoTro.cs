using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace E_Form_Best.Models.ITForm;

[Table("SHD_CT_NguoiHoTro")]
public partial class ShdCtNguoiHoTro
{
    [Key]
    [Column("id")]
    public int Id { get; set; }

    [Column("idFormSHD")]
    public int? IdFormShd { get; set; }

    [Column("idSHD_NguoiHoTro")]
    public int? IdShdNguoiHoTro { get; set; }

    public int? Stt { get; set; }

    [ForeignKey("IdFormShd")]
    [InverseProperty("ShdCtNguoiHoTros")]
    public virtual FormShd? IdFormShdNavigation { get; set; }

    [ForeignKey("IdShdNguoiHoTro")]
    [InverseProperty("ShdCtNguoiHoTros")]
    public virtual ShdNguoiHoTro? IdShdNguoiHoTroNavigation { get; set; }
}
