using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace E_Form_Best.Models.ITForm;

// Số paiban nhập tay cho dòng MES chưa vào thùng. Chờ = IdDongThung null; bỏ vào thùng thì mang số sang
// QA_ThungDong và đặt IdDongThung (không xoá). Script: .claude/plans/sql/qa-mes-so-ban-20261113.sql
[Table("QA_MesSoBan")]
public partial class QaMesSoBan
{
    [Key]
    [Column("id_so_ban")]
    public int IdSoBan { get; set; }

    [Column("dau_van_tay")]
    [StringLength(64)]
    public string DauVanTay { get; set; } = null!;

    // Chỉ để sắp xếp các dòng MES giống hệt nhau (cùng vân tay)
    [Column("thu_tu")]
    public int ThuTu { get; set; }

    [Column("so_ban")]
    public int? SoBan { get; set; }

    [Column("ngay_sua", TypeName = "datetime")]
    public DateTime NgaySua { get; set; }

    [Column("nguoi_sua")]
    [StringLength(50)]
    public string? NguoiSua { get; set; }

    [Column("id_dong_thung")]
    public int? IdDongThung { get; set; }
}
