using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace E_Form_Best.Models.ITForm;

[Table("SHD_DangKySuDungXeDaily_2")]
public partial class ShdDangKySuDungXeDaily2
{
    [Key]
    [Column("id")]
    public int Id { get; set; }

    [Column("id_FormSHD")]
    public int? IdFormShd { get; set; }

    public string? Anh { get; set; }

    [Column(TypeName = "datetime")]
    public DateTime? TimeDuTinh { get; set; }

    public string? DiemDon { get; set; }

    public string? LiDo { get; set; }

    public string? DuongDanAnh { get; set; }

    [ForeignKey("IdFormShd")]
    [InverseProperty("ShdDangKySuDungXeDaily2s")]
    public virtual FormShd? IdFormShdNavigation { get; set; }
}
