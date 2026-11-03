using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace E_Form_Best.Models.ITForm;

/// <summary>
/// Lần quét danh sách MAC gần nhất của từng WLC (nút "Kiểm tra trên WLC"). ThanhCongLuc NULL = chưa quét được lần nào,
/// khi đó trang chi tiết đơn Wi-Fi coi WLC là "chưa tra được" chứ không phải "thiếu".
/// </summary>
[Table("KK_WlcQuet")]
public partial class KkWlcQuet
{
    [Key]
    [Column("wlc_ip")]
    [StringLength(50)]
    [Unicode(false)]
    public string WlcIp { get; set; } = null!;

    [Column("cong_ty")]
    [StringLength(50)]
    public string CongTy { get; set; } = null!;

    [Column("ten_wlc")]
    [StringLength(100)]
    public string? TenWlc { get; set; }

    [Column("quet_luc", TypeName = "datetime")]
    public DateTime? QuetLuc { get; set; }

    [Column("thanh_cong_luc", TypeName = "datetime")]
    public DateTime? ThanhCongLuc { get; set; }

    [Column("loi")]
    [StringLength(500)]
    public string? Loi { get; set; }

    [Column("so_mac")]
    public int? SoMac { get; set; }
}
