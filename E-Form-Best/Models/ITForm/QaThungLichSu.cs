using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace E_Form_Best.Models.ITForm;

// Nhật ký thùng QA — append-only, không sửa/xoá. HanhDong: TaoThung | ThemDong | DongThung | MoLai | XoaThung.
// Script: .claude/plans/sql/qa-thung-20261112.sql
[Table("QA_ThungLichSu")]
public partial class QaThungLichSu
{
    [Key]
    [Column("id_lich_su")]
    public int IdLichSu { get; set; }

    [Column("id_thung")]
    public int IdThung { get; set; }

    [Column("ngay", TypeName = "datetime")]
    public DateTime Ngay { get; set; }

    [Column("nguoi")]
    [StringLength(100)]
    public string? Nguoi { get; set; }

    [Column("hanh_dong")]
    [StringLength(30)]
    public string HanhDong { get; set; } = null!;

    [Column("noi_dung")]
    public string? NoiDung { get; set; }
}
