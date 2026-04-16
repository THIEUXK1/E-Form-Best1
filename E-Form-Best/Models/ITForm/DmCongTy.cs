using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace E_Form_Best.Models.ITForm;

[Table("DM_CongTy")]
public partial class DmCongTy
{
    [Key]
    [Column("IDCongTy")]
    public int IdcongTy { get; set; }

    [StringLength(255)]
    public string? TenCongTy { get; set; }

    public string? GhiChu { get; set; }

    [Column(TypeName = "datetime")]
    public DateTime? NgayTao { get; set; }

    public bool? TrangThai { get; set; }

    [InverseProperty("IdcongTyNavigation")]
    public virtual ICollection<DmNguoiDuyetLoaiDonBoPhan> DmNguoiDuyetLoaiDonBoPhans { get; set; } = new List<DmNguoiDuyetLoaiDonBoPhan>();
}
