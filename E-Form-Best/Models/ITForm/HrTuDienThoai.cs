using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace E_Form_Best.Models.ITForm;

// Tủ điện thoại do HR quản lý. Xoá mềm: mọi truy vấn phải lọc NgayXoa == null.
[Table("HR_TuDienThoai")]
public partial class HrTuDienThoai
{
    [Key]
    [Column("id_tu")]
    public int IdTu { get; set; }

    [Column("id_bo_phan")]
    public int IdBoPhan { get; set; }

    [Column("ten_tu")]
    [StringLength(100)]
    public string TenTu { get; set; } = null!;

    [Column("vi_tri")]
    [StringLength(200)]
    public string? ViTri { get; set; }

    [Column("ma_nhan_vien")]
    [StringLength(50)]
    public string? MaNhanVien { get; set; }

    [Column("nguoi_su_dung")]
    [StringLength(200)]
    public string? NguoiSuDung { get; set; }

    [Column("ngay_tao", TypeName = "datetime")]
    public DateTime NgayTao { get; set; }

    [Column("nguoi_tao")]
    [StringLength(100)]
    public string? NguoiTao { get; set; }

    [Column("ngay_sua", TypeName = "datetime")]
    public DateTime? NgaySua { get; set; }

    [Column("nguoi_sua")]
    [StringLength(100)]
    public string? NguoiSua { get; set; }

    [Column("ngay_xoa", TypeName = "datetime")]
    public DateTime? NgayXoa { get; set; }

    [Column("nguoi_xoa")]
    [StringLength(100)]
    public string? NguoiXoa { get; set; }
}
