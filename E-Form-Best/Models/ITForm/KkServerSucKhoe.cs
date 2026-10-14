using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace E_Form_Best.Models.ITForm;

/// <summary>
/// Ảnh chụp sức khoẻ phần cứng đọc qua SNMP lần gần nhất, 1 dòng / thiết bị (ServerSnmpWorker ghi đè mỗi lượt).
/// Chi tiết từng mục (ổ cứng, RAM, quạt, nguồn...) nằm trong DuLieu dạng JSON.
/// </summary>
[Table("KK_ServerSucKhoe")]
public partial class KkServerSucKhoe
{
    [Key]
    [Column("id_server")]
    [DatabaseGenerated(DatabaseGeneratedOption.None)]
    public int IdServer { get; set; }

    [Column("kiem_tra_luc", TypeName = "datetime")]
    public DateTime KiemTraLuc { get; set; }

    [Column("doc_duoc_luc", TypeName = "datetime")]
    public DateTime? DocDuocLuc { get; set; }

    [Column("ip_snmp")]
    [StringLength(100)]
    [Unicode(false)]
    public string? IpSnmp { get; set; }

    /// <summary>Synology / HPE MSA / HPE ProLiant / Dell iDRAC / Host.</summary>
    [Column("nguon")]
    [StringLength(50)]
    public string? Nguon { get; set; }

    [Column("ten_he_thong")]
    [StringLength(255)]
    public string? TenHeThong { get; set; }

    [Column("mo_ta")]
    [StringLength(500)]
    public string? MoTa { get; set; }

    /// <summary>OK / CANH_BAO / LOI / KHONG_DOC_DUOC.</summary>
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
}
