using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace E_Form_Best.Models.ITForm;

[Table("DM_LoaiDon")]
[Index("MaLoaiDon", Name = "UQ__DM_LoaiD__919182506802A02E", IsUnique = true)]
public partial class DmLoaiDon
{
    [Key]
    [Column("IDLoaiDon")]
    public int IdloaiDon { get; set; }

    [StringLength(50)]
    public string MaLoaiDon { get; set; } = null!;

    [StringLength(255)]
    public string TenLoaiDon { get; set; } = null!;

    public string? MoTa { get; set; }

    [Column(TypeName = "datetime")]
    public DateTime? NgayTao { get; set; }

    public bool? TrangThai { get; set; }

    [InverseProperty("IdloaiDonNavigation")]
    public virtual ICollection<DmNguoiDuyetLoaiDonBoPhan> DmNguoiDuyetLoaiDonBoPhans { get; set; } = new List<DmNguoiDuyetLoaiDonBoPhan>();

    [InverseProperty("IdloaiDonNavigation")]
    public virtual ICollection<DmNguoiXacNhanLoaiDon> DmNguoiXacNhanLoaiDons { get; set; } = new List<DmNguoiXacNhanLoaiDon>();
}
