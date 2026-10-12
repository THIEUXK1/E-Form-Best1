using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace E_Form_Best.Models.ITForm;

/// <summary>
/// IP trong dải quét (VD PFVN 10.0.193.x — dải máy chủ) đã từng trả lời ping, do QuetIpWorker tự thêm.
/// Không nhập tay; tab "Máy chủ" trên /QLSwitch/{công ty} đọc để thống kê server online/offline.
/// </summary>
[Table("KK_QuetIp")]
public partial class KkQuetIp
{
    [Key]
    [Column("id_quet_ip")]
    public int IdQuetIp { get; set; }

    [Column("IDCongTy")]
    public int IdcongTy { get; set; }

    /// <summary>3 octet đầu, VD "10.0.193".</summary>
    [Column("dai_mang")]
    [StringLength(20)]
    [Unicode(false)]
    public string DaiMang { get; set; } = null!;

    [Column("dia_chi_ip")]
    [StringLength(50)]
    [Unicode(false)]
    public string DiaChiIp { get; set; } = null!;

    /// <summary>Tên DNS ngược; NULL = chưa phân giải được.</summary>
    [Column("ten_may")]
    [StringLength(255)]
    public string? TenMay { get; set; }

    [Column("trang_thai_ket_noi")]
    [StringLength(20)]
    [Unicode(false)]
    public string? TrangThaiKetNoi { get; set; }

    [Column("doi_trang_thai_luc", TypeName = "datetime")]
    public DateTime? DoiTrangThaiLuc { get; set; }

    [Column("kiem_tra_luc", TypeName = "datetime")]
    public DateTime? KiemTraLuc { get; set; }

    [Column("lan_dau_thay", TypeName = "datetime")]
    public DateTime LanDauThay { get; set; }

    [Column("lan_cuoi_song", TypeName = "datetime")]
    public DateTime? LanCuoiSong { get; set; }
}
