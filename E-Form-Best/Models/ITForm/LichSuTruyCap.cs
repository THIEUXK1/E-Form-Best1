using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace E_Form_Best.Models.ITForm;

[Table("LichSuTruyCap")]
public partial class LichSuTruyCap
{
    [Key]
    [Column("id_lich_su")]
    public int IdLichSu { get; set; }

    [Column("id_nguoi_dung")]
    public int IdNguoiDung { get; set; }

    [Column("thoi_gian_dang_nhap", TypeName = "datetime")]
    public DateTime? ThoiGianDangNhap { get; set; }

    [Column("thoi_gian_dang_xuat", TypeName = "datetime")]
    public DateTime? ThoiGianDangXuat { get; set; }

    [Column("ten_may_tinh")]
    [StringLength(255)]
    public string? TenMayTinh { get; set; }

    [Column("dia_chi_ip")]
    [StringLength(50)]
    [Unicode(false)]
    public string? DiaChiIp { get; set; }

    [Column("trinh_duyet")]
    [StringLength(500)]
    public string? TrinhDuyet { get; set; }

    [Column("trang_thai")]
    [StringLength(50)]
    public string? TrangThai { get; set; }

    [ForeignKey("IdNguoiDung")]
    [InverseProperty("LichSuTruyCaps")]
    public virtual User IdNguoiDungNavigation { get; set; } = null!;
}
