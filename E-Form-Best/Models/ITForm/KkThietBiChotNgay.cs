using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace E_Form_Best.Models.ITForm;

/// <summary>
/// Số lượng thiết bị theo (ngày, công ty, chỉ tiêu, giá trị) do ThietBiChotNgayWorker chụp mỗi ngày —
/// để so sánh bản quyền / Office / trạng thái giữa các kỳ (KK_ThietBi chỉ giữ giá trị hiện tại).
/// APPEND-ONLY như các bảng LichSu*: không sửa, không xoá. Unique (ngay, cong_ty, chi_tieu, gia_tri).
/// </summary>
[Table("KK_ThietBiChotNgay")]
public partial class KkThietBiChotNgay
{
    [Key]
    [Column("id_chot")]
    public long IdChot { get; set; }

    [Column("ngay")]
    public DateOnly Ngay { get; set; }

    [Column("cong_ty")]
    [StringLength(255)]
    public string CongTy { get; set; } = null!;

    /// <summary>win / office / can_cai_office / trang_thai</summary>
    [Column("chi_tieu")]
    [StringLength(30)]
    [Unicode(false)]
    public string ChiTieu { get; set; } = null!;

    [Column("gia_tri")]
    [StringLength(255)]
    public string GiaTri { get; set; } = null!;

    [Column("so_luong")]
    public int SoLuong { get; set; }

    [Column("ghi_nhan_luc", TypeName = "datetime")]
    public DateTime GhiNhanLuc { get; set; }
}
