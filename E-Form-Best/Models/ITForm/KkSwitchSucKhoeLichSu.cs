using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace E_Form_Best.Models.ITForm;

/// <summary>
/// Nhật ký đổi trạng thái sức khoẻ switch (OK ⇄ CANH_BAO ⇄ LOI ⇄ KHONG_DOC_DUOC) do SwitchSucKhoeWorker ghi.
/// APPEND-ONLY: không sửa, không xoá.
/// </summary>
[Table("KK_SwitchSucKhoeLichSu")]
public partial class KkSwitchSucKhoeLichSu
{
    [Key]
    [Column("id_lich_su")]
    public long IdLichSu { get; set; }

    [Column("id_switch")]
    public int IdSwitch { get; set; }

    [Column("IDCongTy")]
    public int IdcongTy { get; set; }

    [Column("thoi_gian", TypeName = "datetime")]
    public DateTime ThoiGian { get; set; }

    [Column("ten_switch")]
    [StringLength(255)]
    public string? TenSwitch { get; set; }

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
