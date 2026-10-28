using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace E_Form_Best.Models.ITForm;

// Lịch sử thao tác tủ điện thoại — append-only, không sửa/xoá dòng nào; xoá nhầm thì khôi phục từ đây.
[Table("HR_TuDienThoaiLichSu")]
public partial class HrTuDienThoaiLichSu
{
    [Key]
    [Column("id_lich_su")]
    public int IdLichSu { get; set; }

    [Column("id_tu")]
    public int IdTu { get; set; }

    // Them | Sua | Xoa | KhoiPhuc
    [Column("hanh_dong")]
    [StringLength(20)]
    public string HanhDong { get; set; } = null!;

    // null = thao tác tay, "Excel", "XoaNhieu", "DuLieuCu" (tủ xoá trước khi có lịch sử)
    [Column("nguon")]
    [StringLength(20)]
    public string? Nguon { get; set; }

    [Column("du_lieu_cu")]
    public string? DuLieuCu { get; set; }

    [Column("du_lieu_moi")]
    public string? DuLieuMoi { get; set; }

    [Column("ngay", TypeName = "datetime")]
    public DateTime Ngay { get; set; }

    [Column("nguoi")]
    [StringLength(100)]
    public string? Nguoi { get; set; }
}
