using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace E_Form_Best.Models.ITForm;

/// <summary>
/// Đầu ghi camera (NVR/DVR) ở chi nhánh / site ngoài, nhập tay. Đầu ghi trong mạng BPVN do hệ thống
/// ISAPI theo dõi nên không nằm ở đây. Cố ý KHÔNG có cột mật khẩu: secret không được vào DB.
/// Soft-delete: NgayXoa == null là còn hiệu lực.
/// </summary>
[Table("KK_DauGhi")]
public partial class KkDauGhi
{
    [Key]
    [Column("id_dau_ghi")]
    public int IdDauGhi { get; set; }

    [Column("dia_diem")]
    [StringLength(255)]
    public string DiaDiem { get; set; } = null!;

    [Column("ip_public")]
    [StringLength(255)]
    public string? IpPublic { get; set; }

    [Column("ip_local")]
    [StringLength(50)]
    [Unicode(false)]
    public string? IpLocal { get; set; }

    [Column("port_sv")]
    public int? PortSv { get; set; }

    [Column("port_web")]
    public int? PortWeb { get; set; }

    [Column("tong_camera")]
    public int? TongCamera { get; set; }

    [Column("raid")]
    [StringLength(50)]
    public string? Raid { get; set; }

    [Column("trang_thai")]
    [StringLength(100)]
    public string? TrangThai { get; set; }

    [Column("ghi_chu")]
    public string? GhiChu { get; set; }

    /// <summary>Công ty sở hữu đầu ghi (KK_CongTy). NULL = BPVN (dòng cũ trước khi tách công ty).</summary>
    [Column("IDCongTy")]
    public int? IdcongTy { get; set; }

    [Column("nguoi_tao")]
    [StringLength(255)]
    public string? NguoiTao { get; set; }

    [Column("ngay_tao", TypeName = "datetime")]
    public DateTime? NgayTao { get; set; }

    [Column("ngay_cap_nhat", TypeName = "datetime")]
    public DateTime? NgayCapNhat { get; set; }

    [Column("ngay_xoa", TypeName = "datetime")]
    public DateTime? NgayXoa { get; set; }

    [Column("ly_do_xoa")]
    [StringLength(500)]
    public string? LyDoXoa { get; set; }
}
