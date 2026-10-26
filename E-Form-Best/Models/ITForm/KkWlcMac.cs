using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace E_Form_Best.Models.ITForm;

/// <summary>
/// MAC đang khai trên 1 WLC, chụp từ WLC (WlcMacQuetWorker) hoặc ghi ngay sau khi thêm/gỡ ở trang đơn Wi-Fi.
/// MAC không còn trên WLC thì ghi MatLuc, không xoá dòng.
/// </summary>
[Table("KK_WlcMac")]
public partial class KkWlcMac
{
    [Key]
    [Column("id_wlc_mac")]
    public int IdWlcMac { get; set; }

    [Column("wlc_ip")]
    [StringLength(50)]
    [Unicode(false)]
    public string WlcIp { get; set; } = null!;

    /// <summary>12 hex thường liền (WlcMacService.ChuanHoaMac).</summary>
    [Column("mac")]
    [StringLength(12)]
    [Unicode(false)]
    public string Mac { get; set; } = null!;

    [Column("wlan")]
    [StringLength(100)]
    public string? Wlan { get; set; }

    [Column("aaa_list")]
    [StringLength(100)]
    public string? AaaList { get; set; }

    [Column("mo_ta")]
    [StringLength(200)]
    public string? MoTa { get; set; }

    [Column("lan_dau_thay", TypeName = "datetime")]
    public DateTime LanDauThay { get; set; }

    [Column("lan_cuoi_thay", TypeName = "datetime")]
    public DateTime LanCuoiThay { get; set; }

    /// <summary>NULL = đang có trên WLC.</summary>
    [Column("mat_luc", TypeName = "datetime")]
    public DateTime? MatLuc { get; set; }
}
