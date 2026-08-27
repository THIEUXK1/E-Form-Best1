using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace E_Form_Best.Models.ITForm;

/// <summary>
/// Chi tiết đơn số 9 — Yêu cầu thiết kế tem in (tem sản phẩm, tem thùng carton,
/// tem cảnh báo, tem QR/barcode). Mỗi dòng gắn với một đơn tổng trong <c>FormIT</c>.
/// </summary>
[Table("IT_ThietKeTemIn_9")]
[Index("IdFormIt", Name = "IX_IT_ThietKeTemIn_9_idFormIT")]
public partial class ItThietKeTemIn9
{
    [Key]
    [Column("id")]
    public int Id { get; set; }

    [Column("id_FormIT")]
    public int? IdFormIt { get; set; }

    [StringLength(100)]
    public string? LoaiTem { get; set; }

    /// <summary>Kích thước tem người dùng nhập tự do, ví dụ "50 x 30 mm".</summary>
    [StringLength(50)]
    public string? KichThuoc { get; set; }

    [StringLength(100)]
    public string? ChatLieu { get; set; }

    public int? SoLuong { get; set; }

    public string? NoiDungTem { get; set; }

    [StringLength(100)]
    public string? MaHang { get; set; }

    public DateOnly? NgayCanCo { get; set; }

    public string? MucDich { get; set; }

    /// <summary>Tên file ảnh mẫu lưu trên file server, không lưu byte[] để nhẹ CSDL.</summary>
    [StringLength(255)]
    public string? DuonDanAnh { get; set; }

    public string? GhiChu { get; set; }

    [ForeignKey("IdFormIt")]
    [InverseProperty("ItThietKeTemIn9s")]
    public virtual FormIt? IdFormItNavigation { get; set; }
}
