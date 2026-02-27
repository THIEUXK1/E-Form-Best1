using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace E_Form_Best.Models.ITForm;

[PrimaryKey("IdNguoiDung", "IdQuyen")]
[Table("User_Quyen")]
public partial class UserQuyen
{
    [Key]
    [Column("id_nguoi_dung")]
    public int IdNguoiDung { get; set; }

    [Key]
    [Column("id_quyen")]
    public int IdQuyen { get; set; }

    [Column("ghi_chu")]
    public string? GhiChu { get; set; }

    [ForeignKey("IdNguoiDung")]
    [InverseProperty("UserQuyens")]
    public virtual User IdNguoiDungNavigation { get; set; } = null!;

    [ForeignKey("IdQuyen")]
    [InverseProperty("UserQuyens")]
    public virtual Quyen IdQuyenNavigation { get; set; } = null!;
}
