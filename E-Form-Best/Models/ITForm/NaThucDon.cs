using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace E_Form_Best.Models.ITForm;

/// <summary>Thực đơn nhà ăn theo ngày + ca (ca khớp "Ma" trong appsettings NhaAn:Ca). Xoá mềm qua NgayXoa.</summary>
[Table("NA_ThucDon")]
public partial class NaThucDon
{
    [Key]
    [Column("id_thuc_don")]
    public int IdThucDon { get; set; }

    [Column("ngay", TypeName = "date")]
    public DateTime Ngay { get; set; }

    [Column("ca")]
    [StringLength(20)]
    [Unicode(false)]
    public string Ca { get; set; } = null!;

    /// <summary>Mỗi dòng một món.</summary>
    [Column("mon_an")]
    [StringLength(1000)]
    public string MonAn { get; set; } = null!;

    [Column("ghi_chu")]
    [StringLength(500)]
    public string? GhiChu { get; set; }

    [Column("nguoi_tao")]
    [StringLength(100)]
    public string? NguoiTao { get; set; }

    [Column("ngay_tao", TypeName = "datetime")]
    public DateTime NgayTao { get; set; }

    [Column("nguoi_cap_nhat")]
    [StringLength(100)]
    public string? NguoiCapNhat { get; set; }

    [Column("ngay_cap_nhat", TypeName = "datetime")]
    public DateTime? NgayCapNhat { get; set; }

    [Column("ngay_xoa", TypeName = "datetime")]
    public DateTime? NgayXoa { get; set; }

    [Column("nguoi_xoa")]
    [StringLength(100)]
    public string? NguoiXoa { get; set; }
}
