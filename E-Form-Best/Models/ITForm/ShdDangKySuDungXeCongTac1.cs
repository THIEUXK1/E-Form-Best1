using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace E_Form_Best.Models.ITForm;

[Table("SHD_DangKySuDungXeCongTac_1")]
public partial class ShdDangKySuDungXeCongTac1
{
    [Key]
    [Column("id")]
    public int Id { get; set; }

    [Column("id_FormSHD")]
    public int? IdFormShd { get; set; }

    public string? Anh { get; set; }

    [Column(TypeName = "datetime")]
    public DateTime? TimeDuTinh { get; set; }

    public string? LiDo { get; set; }

    public string? DuongDanAnh { get; set; }

    [StringLength(50)]
    public string? SoDienThoai { get; set; }

    [Column(TypeName = "datetime")]
    public DateTime? ThoiGianVe { get; set; }

    public int? SoLuong { get; set; }

    public string? GhiChu { get; set; }

    [ForeignKey("IdFormShd")]
    [InverseProperty("ShdDangKySuDungXeCongTac1s")]
    public virtual FormShd? IdFormShdNavigation { get; set; }
}
