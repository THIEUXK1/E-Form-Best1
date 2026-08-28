using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace E_Form_Best.Models.ITForm;

/// <summary>
/// Chi tiết đơn số 10 — Yêu cầu cài đặt phần mềm lên máy tính.
/// Mỗi dòng gắn với một đơn tổng trong <c>FormIT</c>.
/// </summary>
[Table("IT_CaiDatPhanMem_10")]
[Index("IdFormIt", Name = "IX_IT_CaiDatPhanMem_10_idFormIT")]
public partial class ItCaiDatPhanMem10
{
    [Key]
    [Column("id")]
    public int Id { get; set; }

    [Column("id_FormIT")]
    public int? IdFormIt { get; set; }

    [StringLength(200)]
    public string? TenPhanMem { get; set; }

    /// <summary>Phiên bản người dùng nhập tự do, ví dụ "2021", "v24.3".</summary>
    [StringLength(100)]
    public string? PhienBan { get; set; }

    /// <summary>Tên máy hoặc mã tài sản của máy cần cài.</summary>
    [StringLength(200)]
    public string? MayCaiDat { get; set; }

    public DateOnly? NgayCanCo { get; set; }

    public string? LyDoSuDung { get; set; }

    public string? GhiChu { get; set; }

    /// <summary>Tên file ảnh minh hoạ lưu trên file server, không lưu byte[] để nhẹ CSDL.</summary>
    [StringLength(255)]
    public string? DuonDanAnh { get; set; }

    [ForeignKey("IdFormIt")]
    [InverseProperty("ItCaiDatPhanMem10s")]
    public virtual FormIt? IdFormItNavigation { get; set; }
}
