using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace E_Form_Best.Models.ITForm;

[Table("HR_DangKySuDungXeCongTac_3")]
public partial class HrDangKySuDungXeCongTac3
{
    [Key]
    [Column("id")]
    public int Id { get; set; }

    [Column("id_FormHR")]
    public int? IdFormHr { get; set; }

    public byte[]? Anh { get; set; }

    [Column(TypeName = "datetime")]
    public DateTime? TimeDuTinh { get; set; }

    [StringLength(255)]
    public string? LiDo { get; set; }

    public string? DuongDanAnh { get; set; }

    [StringLength(20)]
    public string? SoDienThoai { get; set; }

    [Column(TypeName = "datetime")]
    public DateTime? ThoiGianVe { get; set; }

    public int? SoLuong { get; set; }

    public string? GhiChu { get; set; }

    [ForeignKey("IdFormHr")]
    [InverseProperty("HrDangKySuDungXeCongTac3s")]
    public virtual FormHr? IdFormHrNavigation { get; set; }
}
