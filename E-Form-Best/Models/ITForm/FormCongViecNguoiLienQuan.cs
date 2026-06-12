using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace E_Form_Best.Models.ITForm;

[Table("FormCongViec_NguoiLienQuan")]
public partial class FormCongViecNguoiLienQuan
{
    [Key]
    [Column("id")]
    public int Id { get; set; }

    [Column("id_form_cong_viec")]
    public int IdFormCongViec { get; set; }

    [Column("id_nguoi_dung")]
    public int IdNguoiDung { get; set; }

    [StringLength(100)]
    public string? VaiTroLienQuan { get; set; }

    [Column(TypeName = "datetime")]
    public DateTime? NgayGan { get; set; }

    [StringLength(255)]
    public string? GhiChu { get; set; }

    [ForeignKey("IdFormCongViec")]
    [InverseProperty("FormCongViecNguoiLienQuans")]
    public virtual FormCongViec IdFormCongViecNavigation { get; set; } = null!;

    [ForeignKey("IdNguoiDung")]
    [InverseProperty("FormCongViecNguoiLienQuans")]
    public virtual User IdNguoiDungNavigation { get; set; } = null!;
}
