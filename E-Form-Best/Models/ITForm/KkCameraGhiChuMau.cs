using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace E_Form_Best.Models.ITForm;

/// <summary>
/// Câu ghi chú có sẵn cho camera (/QLCamera): bấm "+" để lưu thành mẫu, lần sau chỉ cần chọn.
/// Dùng chung cho mọi người. Không trùng nội dung trong các mẫu còn hiệu lực (unique lọc ở DB).
/// Soft-delete: NgayXoa == null là còn hiệu lực.
/// </summary>
[Table("KK_CameraGhiChuMau")]
public partial class KkCameraGhiChuMau
{
    [Key]
    [Column("id_mau")]
    public int IdMau { get; set; }

    [Column("noi_dung")]
    [StringLength(200)]
    public string NoiDung { get; set; } = null!;

    [Column("nguoi_tao")]
    [StringLength(255)]
    public string? NguoiTao { get; set; }

    [Column("ngay_tao", TypeName = "datetime")]
    public DateTime? NgayTao { get; set; }

    [Column("ngay_xoa", TypeName = "datetime")]
    public DateTime? NgayXoa { get; set; }

    [Column("nguoi_xoa")]
    [StringLength(255)]
    public string? NguoiXoa { get; set; }
}
