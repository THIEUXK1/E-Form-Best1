using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace E_Form_Best.Models.ITForm;

/// <summary>
/// Nhật ký đổi trạng thái sức khoẻ SNMP (OK ⇄ CANH_BAO ⇄ LOI ⇄ KHONG_DOC_DUOC) do ServerSnmpWorker ghi.
/// APPEND-ONLY: không sửa, không xoá.
/// </summary>
[Table("KK_ServerSucKhoeLichSu")]
public partial class KkServerSucKhoeLichSu
{
    [Key]
    [Column("id_lich_su")]
    public long IdLichSu { get; set; }

    [Column("id_server")]
    public int IdServer { get; set; }

    [Column("IDCongTy")]
    public int IdcongTy { get; set; }

    [Column("thoi_gian", TypeName = "datetime")]
    public DateTime ThoiGian { get; set; }

    [Column("ten_server")]
    [StringLength(255)]
    public string? TenServer { get; set; }

    [Column("tu_trang_thai")]
    [StringLength(20)]
    [Unicode(false)]
    public string? TuTrangThai { get; set; }

    [Column("sang_trang_thai")]
    [StringLength(20)]
    [Unicode(false)]
    public string SangTrangThai { get; set; } = null!;

    [Column("tom_tat")]
    [StringLength(1000)]
    public string? TomTat { get; set; }

    [Column("ghi_nhan_luc", TypeName = "datetime")]
    public DateTime GhiNhanLuc { get; set; }
}
