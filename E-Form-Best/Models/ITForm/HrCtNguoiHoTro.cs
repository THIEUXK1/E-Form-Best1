using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace E_Form_Best.Models.ITForm;

[Table("HR_CT_NguoiHoTro")]
public partial class HrCtNguoiHoTro
{
    [Key]
    [Column("id")]
    public int Id { get; set; }

    [Column("idFormHR")]
    public int? IdFormHr { get; set; }

    [Column("idHR_NguoiHoTro")]
    public int? IdHrNguoiHoTro { get; set; }

    public int? Stt { get; set; }

    [ForeignKey("IdFormHr")]
    [InverseProperty("HrCtNguoiHoTros")]
    public virtual FormHr? IdFormHrNavigation { get; set; }

    [ForeignKey("IdHrNguoiHoTro")]
    [InverseProperty("HrCtNguoiHoTros")]
    public virtual HrNguoiHoTro? IdHrNguoiHoTroNavigation { get; set; }
}
