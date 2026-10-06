using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace E_Form_Best.Models.ITForm;

/// <summary>
/// Trạng thái gần nhất của từng kênh camera mà CameraLichSuWorker đã thấy, dùng để so với lần đọc
/// sau và phát hiện đổi trạng thái. Một dòng / (đầu ghi, kênh) — unique ở DB.
/// </summary>
[Table("KK_CameraTrangThai")]
public partial class KkCameraTrangThai
{
    [Key]
    [Column("id_trang_thai")]
    public int IdTrangThai { get; set; }

    [Column("nvr_ip")]
    [StringLength(50)]
    [Unicode(false)]
    public string NvrIp { get; set; } = null!;

    [Column("kenh")]
    public int Kenh { get; set; }

    [Column("trang_thai")]
    [StringLength(20)]
    [Unicode(false)]
    public string TrangThai { get; set; } = null!;

    [Column("ten_camera")]
    [StringLength(255)]
    public string? TenCamera { get; set; }

    [Column("ip_camera")]
    [StringLength(50)]
    [Unicode(false)]
    public string? IpCamera { get; set; }

    [Column("doi_luc", TypeName = "datetime")]
    public DateTime? DoiLuc { get; set; }

    [Column("cap_nhat_luc", TypeName = "datetime")]
    public DateTime CapNhatLuc { get; set; }
}
