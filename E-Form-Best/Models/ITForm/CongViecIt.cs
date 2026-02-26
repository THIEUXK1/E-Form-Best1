using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace E_Form_Best.Models.ITForm;

[Table("CongViecIT")]
public partial class CongViecIt
{
    [Key]
    [Column("id")]
    public int Id { get; set; }

    [Column("id_IT_NguoiHoTro")]
    public int? IdItNguoiHoTro { get; set; }

    [StringLength(255)]
    public string? Ten { get; set; }

    [StringLength(100)]
    public string? TrangThai { get; set; }

    [ForeignKey("IdItNguoiHoTro")]
    [InverseProperty("CongViecIts")]
    public virtual ItNguoiHoTro? IdItNguoiHoTroNavigation { get; set; }
}
