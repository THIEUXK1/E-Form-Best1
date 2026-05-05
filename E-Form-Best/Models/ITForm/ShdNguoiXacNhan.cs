using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace E_Form_Best.Models.ITForm;

[Table("SHD_NguoiXacNhan")]
public partial class ShdNguoiXacNhan
{
    [Key]
    [Column("id")]
    public int Id { get; set; }

    [Column("idFormSHD")]
    public int? IdFormShd { get; set; }

    [Column("IDNguoiXacNhan")]
    public int? IdnguoiXacNhan { get; set; }

    public int? ThuTuXacNhan { get; set; }

    public int? TrangThaiXacNhan { get; set; }

    [Column(TypeName = "datetime")]
    public DateTime? ThoiGianXacNhan { get; set; }

    public string? GhiChu { get; set; }

    [StringLength(50)]
    public string? MaNguoiXacNhan { get; set; }

    [StringLength(255)]
    public string? TenNguoiXacNhan { get; set; }

    [ForeignKey("IdFormShd")]
    [InverseProperty("ShdNguoiXacNhans")]
    public virtual FormShd? IdFormShdNavigation { get; set; }

    [ForeignKey("IdnguoiXacNhan")]
    [InverseProperty("ShdNguoiXacNhans")]
    public virtual DmNguoiXacNhan? IdnguoiXacNhanNavigation { get; set; }
}
