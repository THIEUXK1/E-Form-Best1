using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace E_Form_Best.Models.ITForm;

/// <summary>
/// Nhật ký AP đổi trạng thái (UP ⇄ DOWN) do AccessPointPingWorker ghi.
/// APPEND-ONLY như các bảng LichSu* khác: không sửa, không xoá.
/// Unique (id_ap, thoi_gian, sang_trang_thai) chặn ghi trùng.
/// </summary>
[Table("KK_AccessPointLichSu")]
public partial class KkAccessPointLichSu
{
    [Key]
    [Column("id_lich_su")]
    public long IdLichSu { get; set; }

    [Column("id_ap")]
    public int IdAp { get; set; }

    [Column("IDCongTy")]
    public int IdcongTy { get; set; }

    [Column("thoi_gian", TypeName = "datetime")]
    public DateTime ThoiGian { get; set; }

    [Column("ten_ap")]
    [StringLength(255)]
    public string? TenAp { get; set; }

    [Column("dia_chi_ip")]
    [StringLength(50)]
    [Unicode(false)]
    public string? DiaChiIp { get; set; }

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
