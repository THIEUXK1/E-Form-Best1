using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace E_Form_Best.Models.ITForm;

[Table("HR_NguoiXacNhan")]
public partial class HrNguoiXacNhan
{
    [Key]
    [Column("id")]
    public int Id { get; set; }

    [Column("idFormHR")]
    public int IdFormHr { get; set; }

    [Column("IDNguoiXacNhan")]
    public int IdnguoiXacNhan { get; set; }

    public int? ThuTuXacNhan { get; set; }

    public int? TrangThaiXacNhan { get; set; }

    [Column(TypeName = "datetime")]
    public DateTime? ThoiGianXacNhan { get; set; }

    public string? GhiChu { get; set; }

    [ForeignKey("IdFormHr")]
    [InverseProperty("HrNguoiXacNhans")]
    public virtual FormHr IdFormHrNavigation { get; set; } = null!;

    [ForeignKey("IdnguoiXacNhan")]
    [InverseProperty("HrNguoiXacNhans")]
    public virtual DmNguoiXacNhan IdnguoiXacNhanNavigation { get; set; } = null!;
}
