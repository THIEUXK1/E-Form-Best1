using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace E_Form_Best.Models.ITForm;

[Table("IT_DangkiTaiKhoanMayTinh_6")]
[Index("IdFormIt", Name = "IX_IT_DangkiTaiKhoanMayTinh_6_idFormIT")]
public partial class ItDangkiTaiKhoanMayTinh6
{
    [Key]
    [Column("id")]
    public int Id { get; set; }

    [StringLength(500)]
    public string? HoTenVaMaNhanVienNguoiSuDung { get; set; }

    [StringLength(100)]
    public string? ChucVu { get; set; }

    [StringLength(50)]
    public string? SoNoiBo { get; set; }

    [StringLength(20)]
    public string? SoDienThoai { get; set; }

    public string? MucDich { get; set; }

    [StringLength(250)]
    public string? TenMayCanCai { get; set; }

    [Column("TenOChungCanAddQuyen")]
    public string? TenOchungCanAddQuyen { get; set; }

    public string? GhiChu { get; set; }

    public string? DuongDanAnh { get; set; }

    [Column("id_FormIT")]
    public int? IdFormIt { get; set; }

    [StringLength(250)]
    public string? Loai { get; set; }

    [ForeignKey("IdFormIt")]
    [InverseProperty("ItDangkiTaiKhoanMayTinh6s")]
    public virtual FormIt? IdFormItNavigation { get; set; }
}
