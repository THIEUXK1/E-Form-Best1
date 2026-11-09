using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace E_Form_Best.Models.ITForm;

// Lịch sử in tem — append-only, mỗi lần bấm In 1 dòng; noi_dung giữ chữ/mã vạch thực tế từng tem để tra "đã in gì".
// Script: .claude/plans/sql/hr-tem-in-lich-su-20261109.sql
[Table("HR_TemInLichSu")]
public partial class HrTemInLichSu
{
    [Key]
    [Column("id_lan_in")]
    public int IdLanIn { get; set; }

    [Column("ngay", TypeName = "datetime")]
    public DateTime Ngay { get; set; }

    [Column("nguoi")]
    [StringLength(100)]
    public string? Nguoi { get; set; }

    [Column("ten_mau")]
    [StringLength(100)]
    public string? TenMau { get; set; }

    [Column("trang_nhan")]
    [StringLength(100)]
    public string? TrangNhan { get; set; }

    // null = hộp thoại in của trình duyệt
    [Column("may_in")]
    [StringLength(200)]
    public string? MayIn { get; set; }

    [Column("so_ban")]
    public int SoBan { get; set; }

    [Column("so_nhan")]
    public int SoNhan { get; set; }

    // Tu | Excel | KhongDuLieu
    [Column("nguon_du_lieu")]
    [StringLength(20)]
    public string NguonDuLieu { get; set; } = null!;

    [Column("ten_file")]
    [StringLength(260)]
    public string? TenFile { get; set; }

    [Column("ds_id_tu")]
    public string? DsIdTu { get; set; }

    // JSON [["chữ", "mã vạch", ...], ...] — mỗi phần tử 1 nhãn
    [Column("noi_dung")]
    public string NoiDung { get; set; } = null!;
}
