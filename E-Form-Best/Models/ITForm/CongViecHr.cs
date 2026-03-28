using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace E_Form_Best.Models.ITForm;

[Table("CongViecHR")]
public partial class CongViecHr
{
    [Key]
    [Column("id")]
    public int Id { get; set; }

    [Column("id_HR_NguoiHoTro")]
    public int? IdHrNguoiHoTro { get; set; }

    [StringLength(255)]
    public string? Ten { get; set; }

    [StringLength(50)]
    public string? TrangThai { get; set; }

    [ForeignKey("IdHrNguoiHoTro")]
    [InverseProperty("CongViecHrs")]
    public virtual HrNguoiHoTro? IdHrNguoiHoTroNavigation { get; set; }
}
