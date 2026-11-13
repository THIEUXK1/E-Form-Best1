using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace E_Form_Best.Models.ITForm;

// Thùng QA gom các dòng đóng gói tra từ NH-MES. Mã tự sinh QA-yyMMdd-NNN; xoá mềm (NgayXoa) — mọi truy vấn lọc NgayXoa == null.
// Script: .claude/plans/sql/qa-thung-20261112.sql
[Table("QA_Thung")]
public partial class QaThung
{
    public const string TrangThaiMo = "Mo";
    public const string TrangThaiDaDong = "DaDong";

    [Key]
    [Column("id_thung")]
    public int IdThung { get; set; }

    [Column("ma_thung")]
    [StringLength(30)]
    public string MaThung { get; set; } = null!;

    // Mo | DaDong
    [Column("trang_thai")]
    [StringLength(20)]
    public string TrangThai { get; set; } = TrangThaiMo;

    [Column("ghi_chu")]
    [StringLength(500)]
    public string? GhiChu { get; set; }

    [Column("ngay_tao", TypeName = "datetime")]
    public DateTime NgayTao { get; set; }

    [Column("nguoi_tao")]
    [StringLength(100)]
    public string? NguoiTao { get; set; }

    [Column("ngay_dong", TypeName = "datetime")]
    public DateTime? NgayDong { get; set; }

    [Column("nguoi_dong")]
    [StringLength(100)]
    public string? NguoiDong { get; set; }

    [Column("ngay_xoa", TypeName = "datetime")]
    public DateTime? NgayXoa { get; set; }

    [Column("nguoi_xoa")]
    [StringLength(100)]
    public string? NguoiXoa { get; set; }

    // Vị trí đặt thùng trên kệ (null = chưa xếp kệ); 1 ô chứa được nhiều thùng. Script: qa-ke-20261112.sql
    [Column("id_ke")]
    public int? IdKe { get; set; }

    [Column("tang")]
    public int? Tang { get; set; }

    [Column("o")]
    public int? O { get; set; }

    [ForeignKey(nameof(IdKe))]
    public virtual QaKe? Ke { get; set; }

    public virtual ICollection<QaThungDong> Dongs { get; set; } = new List<QaThungDong>();
}
