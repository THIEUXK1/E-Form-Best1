using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace E_Form_Best.Models.ITForm;

public partial class Mail
{
    [Key]
    public int Id { get; set; }

    [StringLength(255)]
    public string? TenForm { get; set; }

    public DateOnly? Ngay { get; set; }

    [StringLength(255)]
    public string? BoPhan { get; set; }

    [StringLength(50)]
    public string? SoNhanVien { get; set; }

    [StringLength(255)]
    public string? Email { get; set; }

    [StringLength(255)]
    public string? ViTri { get; set; }

    [StringLength(50)]
    public string? SoNoiBo { get; set; }

    public bool? GuiRaNgoai { get; set; }

    public bool? SuDungTrenDienThoai { get; set; }

    public bool? SuDungWedMail { get; set; }

    public string? MucDichSuDung { get; set; }

    [Column("idNguoiTao")]
    public int? IdNguoiTao { get; set; }

    [StringLength(255)]
    public string? TenNguoiTao { get; set; }

    [Column("idNguoiDuyet")]
    public int? IdNguoiDuyet { get; set; }

    [StringLength(255)]
    public string? TenNguoiDuyet { get; set; }

    [Column("idAdmin")]
    public int? IdAdmin { get; set; }

    [StringLength(255)]
    public string? TenAdmin { get; set; }

    [Column(TypeName = "datetime")]
    public DateTime? TimeNguoiTao { get; set; }

    [Column(TypeName = "datetime")]
    public DateTime? TimeNguoiDuyet { get; set; }

    [Column(TypeName = "datetime")]
    public DateTime? TimeAdmin { get; set; }

    [Column(TypeName = "datetime")]
    public DateTime? TimeTao { get; set; }

    [StringLength(255)]
    public string? HoVaTen { get; set; }
}
