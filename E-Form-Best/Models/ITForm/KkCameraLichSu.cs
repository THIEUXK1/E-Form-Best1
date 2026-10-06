using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace E_Form_Best.Models.ITForm;

/// <summary>
/// Nhật ký camera đổi trạng thái (Hoạt động ⇄ Mất kết nối) do CameraLichSuWorker ghi.
/// APPEND-ONLY như các bảng LichSu* khác: không sửa, không xoá.
/// Unique (nvr_ip, kenh, thoi_gian, sang_trang_thai) chặn ghi trùng khi job chạy lại / chạy 2 máy chủ.
/// </summary>
[Table("KK_CameraLichSu")]
public partial class KkCameraLichSu
{
    [Key]
    [Column("id_lich_su")]
    public long IdLichSu { get; set; }

    [Column("thoi_gian", TypeName = "datetime")]
    public DateTime ThoiGian { get; set; }

    [Column("nvr_ip")]
    [StringLength(50)]
    [Unicode(false)]
    public string NvrIp { get; set; } = null!;

    [Column("ten_dau_ghi")]
    [StringLength(255)]
    public string? TenDauGhi { get; set; }

    [Column("khu_vuc")]
    [StringLength(100)]
    public string? KhuVuc { get; set; }

    [Column("kenh")]
    public int Kenh { get; set; }

    [Column("ten_camera")]
    [StringLength(255)]
    public string? TenCamera { get; set; }

    [Column("ip_camera")]
    [StringLength(50)]
    [Unicode(false)]
    public string? IpCamera { get; set; }

    [Column("tu_trang_thai")]
    [StringLength(20)]
    [Unicode(false)]
    public string? TuTrangThai { get; set; }

    [Column("sang_trang_thai")]
    [StringLength(20)]
    [Unicode(false)]
    public string SangTrangThai { get; set; } = null!;

    [Column("thoi_luong_giay")]
    public int? ThoiLuongGiay { get; set; }

    [Column("ghi_nhan_luc", TypeName = "datetime")]
    public DateTime GhiNhanLuc { get; set; }
}
