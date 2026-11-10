using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace E_Form_Best.Models.ITForm;

/// <summary>
/// Switch / WLC được theo dõi sức khoẻ phần cứng qua SNMP (tab "Thông tin thiết bị" trang /QLSwitch/{công ty}).
/// Có dòng ở đây = đang theo dõi; SwitchSucKhoeWorker ghi đè ảnh chụp mỗi lượt. Chi tiết nằm trong DuLieu (JSON).
/// </summary>
[Table("KK_SwitchSucKhoe")]
public partial class KkSwitchSucKhoe
{
    [Key]
    [Column("id_switch")]
    [DatabaseGenerated(DatabaseGeneratedOption.None)]
    public int IdSwitch { get; set; }

    [Column("ten_hien_thi")]
    [StringLength(100)]
    public string? TenHienThi { get; set; }

    [Column("thu_tu")]
    public int ThuTu { get; set; }

    [Column("kiem_tra_luc", TypeName = "datetime")]
    public DateTime? KiemTraLuc { get; set; }

    [Column("doc_duoc_luc", TypeName = "datetime")]
    public DateTime? DocDuocLuc { get; set; }

    [Column("ip_snmp")]
    [StringLength(100)]
    [Unicode(false)]
    public string? IpSnmp { get; set; }

    [Column("ten_he_thong")]
    [StringLength(255)]
    public string? TenHeThong { get; set; }

    [Column("mo_ta")]
    [StringLength(500)]
    public string? MoTa { get; set; }

    /// <summary>OK / CANH_BAO / LOI / KHONG_DOC_DUOC / CHUA_DOC.</summary>
    [Column("trang_thai")]
    [StringLength(20)]
    [Unicode(false)]
    public string TrangThai { get; set; } = null!;

    [Column("tom_tat")]
    [StringLength(1000)]
    public string? TomTat { get; set; }

    [Column("du_lieu")]
    public string? DuLieu { get; set; }

    [Column("loi")]
    [StringLength(500)]
    public string? Loi { get; set; }

    [ForeignKey(nameof(IdSwitch))]
    public virtual KkSwitch? IdSwitchNavigation { get; set; }
}
