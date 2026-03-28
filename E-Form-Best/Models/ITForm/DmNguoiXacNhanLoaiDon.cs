using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace E_Form_Best.Models.ITForm;

[Table("DM_NguoiXacNhan_LoaiDon")]
public partial class DmNguoiXacNhanLoaiDon
{
    [Key]
    [Column("IDRel")]
    public int Idrel { get; set; }

    [Column("IDNguoiXacNhan")]
    public int IdnguoiXacNhan { get; set; }

    [Column("IDLoaiDon")]
    public int IdloaiDon { get; set; }

    public int? CapDoXacNhan { get; set; }

    [StringLength(255)]
    public string? GhiChu { get; set; }

    [ForeignKey("IdloaiDon")]
    [InverseProperty("DmNguoiXacNhanLoaiDons")]
    public virtual DmLoaiDon IdloaiDonNavigation { get; set; } = null!;

    [ForeignKey("IdnguoiXacNhan")]
    [InverseProperty("DmNguoiXacNhanLoaiDons")]
    public virtual DmNguoiXacNhan IdnguoiXacNhanNavigation { get; set; } = null!;
}
