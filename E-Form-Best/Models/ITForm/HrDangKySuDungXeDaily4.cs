using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace E_Form_Best.Models.ITForm;

[Table("HR_DangKySuDungXeDaily_4")]
public partial class HrDangKySuDungXeDaily4
{
    [Key]
    [Column("id")]
    public int Id { get; set; }

    [Column("id_FormHR")]
    public int? IdFormHr { get; set; }

    public byte[]? Anh { get; set; }

    [Column(TypeName = "datetime")]
    public DateTime? TimeDuTinh { get; set; }

    [StringLength(500)]
    public string? DiemDon { get; set; }

    public string? LiDo { get; set; }

    public string? DuongDanAnh { get; set; }

    [StringLength(250)]
    public string? HoTenNguoiDangKy { get; set; }

    [ForeignKey("IdFormHr")]
    [InverseProperty("HrDangKySuDungXeDaily4s")]
    public virtual FormHr? IdFormHrNavigation { get; set; }
}
