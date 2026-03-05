using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace E_Form_Best.Models.ITForm;

[Table("IT_DangKiTaiKhoanHeThong_5")]
public partial class ItDangKiTaiKhoanHeThong5
{
    [Key]
    [Column("id")]
    public int Id { get; set; }

    [StringLength(255)]
    public string? TenNguoiDangKi { get; set; }

    [StringLength(50)]
    public string? MaNguoiDangKi { get; set; }

    [StringLength(255)]
    public string? DangKiChoAi { get; set; }

    [StringLength(255)]
    public string? HeThongNao { get; set; }

    [StringLength(255)]
    public string? CapQuyenGiongAi { get; set; }

    [StringLength(255)]
    public string? DungTrenMayNao { get; set; }

    [StringLength(100)]
    public string? LoaiDon { get; set; }

    public string? MoTaChiTiet { get; set; }

    public string? MucDich { get; set; }

    public byte[]? Anh { get; set; }

    public string? DuongDanAnh { get; set; }

    [Column("id_FormIT")]
    public int? IdFormIt { get; set; }

    [ForeignKey("IdFormIt")]
    [InverseProperty("ItDangKiTaiKhoanHeThong5s")]
    public virtual FormIt? IdFormItNavigation { get; set; }
}
