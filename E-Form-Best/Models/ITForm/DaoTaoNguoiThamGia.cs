using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace E_Form_Best.Models.ITForm;

[Table("DaoTao_NguoiThamGia")]
public partial class DaoTaoNguoiThamGia
{
    [Key]
    [Column("id")]
    public int Id { get; set; }

    [Column("id_DaoTao")]
    public int IdDaoTao { get; set; }

    public int IdNguoiDung { get; set; }

    public bool CoMat { get; set; }

    [Column(TypeName = "datetime")]
    public DateTime? ThoiGianDiemDanh { get; set; }

    [ForeignKey("IdDaoTao")]
    [InverseProperty("DaoTaoNguoiThamGias")]
    public virtual DaoTao IdDaoTaoNavigation { get; set; } = null!;

    [ForeignKey("IdNguoiDung")]
    public virtual User? IdNguoiDungNavigation { get; set; }
}
