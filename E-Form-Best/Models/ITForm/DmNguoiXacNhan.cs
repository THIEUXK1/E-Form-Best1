using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace E_Form_Best.Models.ITForm;

[Table("DM_NguoiXacNhan")]
[Index("MaNv", Name = "UQ__DM_Nguoi__2725D70B585FDF38", IsUnique = true)]
public partial class DmNguoiXacNhan
{
    [Key]
    [Column("IDNguoiXacNhan")]
    public int IdnguoiXacNhan { get; set; }

    [Column("MaNV")]
    [StringLength(50)]
    public string MaNv { get; set; } = null!;

    [StringLength(255)]
    public string HoTen { get; set; } = null!;

    [StringLength(255)]
    public string? Email { get; set; }

    [StringLength(100)]
    public string? PhongBan { get; set; }

    [StringLength(100)]
    public string? ChucVu { get; set; }

    public bool? TrangThai { get; set; }

    [InverseProperty("IdnguoiXacNhanNavigation")]
    public virtual ICollection<DmNguoiDuyetLoaiDonBoPhan> DmNguoiDuyetLoaiDonBoPhans { get; set; } = new List<DmNguoiDuyetLoaiDonBoPhan>();

    [InverseProperty("IdnguoiXacNhanNavigation")]
    public virtual ICollection<DmNguoiXacNhanLoaiDon> DmNguoiXacNhanLoaiDons { get; set; } = new List<DmNguoiXacNhanLoaiDon>();

    [InverseProperty("IdnguoiXacNhanNavigation")]
    public virtual ICollection<HrNguoiXacNhan> HrNguoiXacNhans { get; set; } = new List<HrNguoiXacNhan>();

    [InverseProperty("IdnguoiXacNhanNavigation")]
    public virtual ICollection<ShdNguoiXacNhan> ShdNguoiXacNhans { get; set; } = new List<ShdNguoiXacNhan>();
}
