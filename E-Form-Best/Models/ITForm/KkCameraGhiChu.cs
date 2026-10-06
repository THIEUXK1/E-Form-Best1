using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace E_Form_Best.Models.ITForm;

/// <summary>
/// Ghi chú người dùng tự ghi cho camera ở tab Giám sát /QLCamera. Camera đến từ hệ thống ISAPI
/// (không có trong DB) nên khoá theo đầu ghi + kênh — unique (nvr_ip, kenh) ở tầng DB.
/// Xoá trắng ghi chú = đặt GhiChu NULL, không xoá dòng; lịch sử trước/sau nằm ở KK_LichSuThaoTac.
/// </summary>
[Table("KK_CameraGhiChu")]
public partial class KkCameraGhiChu
{
    [Key]
    [Column("id_ghi_chu")]
    public int IdGhiChu { get; set; }

    [Column("nvr_ip")]
    [StringLength(50)]
    [Unicode(false)]
    public string NvrIp { get; set; } = null!;

    [Column("kenh")]
    public int Kenh { get; set; }

    [Column("ten_camera")]
    [StringLength(255)]
    public string? TenCamera { get; set; }

    [Column("ghi_chu")]
    [StringLength(1000)]
    public string? GhiChu { get; set; }

    [Column("nguoi_cap_nhat")]
    [StringLength(255)]
    public string? NguoiCapNhat { get; set; }

    [Column("ngay_cap_nhat", TypeName = "datetime")]
    public DateTime? NgayCapNhat { get; set; }
}
