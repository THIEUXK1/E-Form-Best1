using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace E_Form_Best.Models.ITForm;

[Table("CT_HoTroTienDienThoai_7")]
public partial class CtHoTroTienDienThoai7
{
    [Key]
    [Column("id")]
    public int Id { get; set; }

    [Column("id_FormHR")]
    public int? IdFormHr { get; set; }

    public byte[]? Anh { get; set; }

    [ForeignKey("IdFormHr")]
    [InverseProperty("CtHoTroTienDienThoai7s")]
    public virtual FormHr? IdFormHrNavigation { get; set; }
}
