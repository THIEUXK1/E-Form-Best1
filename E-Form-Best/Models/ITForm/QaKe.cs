using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace E_Form_Best.Models.ITForm;

// Kệ chứa thùng QA, chia ô theo SoTang × SoO; mã vị trí = MaKe-tầng-ô. Xoá mềm (NgayXoa) — mọi truy vấn lọc NgayXoa == null.
// Script: .claude/plans/sql/qa-ke-20261112.sql
[Table("QA_Ke")]
public partial class QaKe
{
    [Key]
    [Column("id_ke")]
    public int IdKe { get; set; }

    [Column("ma_ke")]
    [StringLength(20)]
    public string MaKe { get; set; } = null!;

    [Column("ten_ke")]
    [StringLength(100)]
    public string? TenKe { get; set; }

    [Column("khu_vuc")]
    [StringLength(100)]
    public string? KhuVuc { get; set; }

    [Column("so_tang")]
    public int SoTang { get; set; }

    [Column("so_o")]
    public int SoO { get; set; }

    [Column("ghi_chu")]
    [StringLength(500)]
    public string? GhiChu { get; set; }

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
