using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace E_Form_Best.Models.ITForm;

[PrimaryKey("IdNguoiDung", "IdBoPhan")]
[Table("User_BoPhan")]
public partial class UserBoPhan
{
    [Key]
    [Column("id_nguoi_dung")]
    public int IdNguoiDung { get; set; }

    [Key]
    [Column("id_bo_phan")]
    public int IdBoPhan { get; set; }

    [Column("loai")]
    [StringLength(255)]
    public string? Loai { get; set; }

    [Column("ngay_chi_dinh", TypeName = "datetime")]
    public DateTime? NgayChiDinh { get; set; }

    [ForeignKey("IdBoPhan")]
    [InverseProperty("UserBoPhans")]
    public virtual BoPhan IdBoPhanNavigation { get; set; } = null!;

    [ForeignKey("IdNguoiDung")]
    [InverseProperty("UserBoPhans")]
    public virtual User IdNguoiDungNavigation { get; set; } = null!;
}
