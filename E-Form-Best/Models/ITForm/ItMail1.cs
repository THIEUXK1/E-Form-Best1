using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace E_Form_Best.Models.ITForm;

[Table("IT_Mail_1")]
public partial class ItMail1
{
    [Key]
    [Column("id")]
    public int Id { get; set; }

    [Column("id_FormIT")]
    public int? IdFormIt { get; set; }

    [StringLength(255)]
    public string? Email { get; set; }

    [StringLength(100)]
    public string? ViTri { get; set; }

    [StringLength(50)]
    public string? SoNoiBo { get; set; }

    [StringLength(50)]
    public string? GuiRaNgoai { get; set; }

    [StringLength(50)]
    public string? SuDungTrenDienThoai { get; set; }

    [StringLength(50)]
    public string? SuDungWebMail { get; set; }

    public string? MucDich { get; set; }

    public byte[]? Anh { get; set; }

    public string? DuonDanAnh { get; set; }

    [StringLength(20)]
    public string? SoDienThoai { get; set; }

    [StringLength(255)]
    public string? NguoiSuDung { get; set; }

    [StringLength(255)]
    public string? LoaiYeuCau { get; set; }

    [StringLength(255)]
    public string? NhomEmail { get; set; }

    [StringLength(50)]
    public string? SuDungTrenMayTinh { get; set; }

    [ForeignKey("IdFormIt")]
    [InverseProperty("ItMail1s")]
    public virtual FormIt? IdFormItNavigation { get; set; }
}
