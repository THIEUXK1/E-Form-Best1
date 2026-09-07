using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace E_Form_Best.Models.ITForm;

/// <summary>
/// Chi tiết đơn số 12 — Trả thiết bị về bộ phận IT khi thiết bị gặp trục trặc,
/// hư hỏng hoặc không còn nhu cầu sử dụng.
/// Mỗi dòng gắn với một đơn tổng trong <c>FormIT</c>.
/// </summary>
[Table("IT_TraThietBi_12")]
[Index("IdFormIt", Name = "IX_IT_TraThietBi_12_idFormIT")]
[Index("SerialTaiSan", Name = "IX_IT_TraThietBi_12_SerialTaiSan")]
public partial class ItTraThietBi12
{
    [Key]
    [Column("id")]
    public int Id { get; set; }

    [Column("id_FormIT")]
    public int? IdFormIt { get; set; }

    [StringLength(100)]
    public string? LoaiThietBi { get; set; }

    [StringLength(200)]
    public string? TenThietBi { get; set; }

    /// <summary>Serial hoặc mã tài sản — dùng để đối chiếu với module Kiểm kê (<c>KkThietBi</c>).</summary>
    [StringLength(100)]
    public string? SerialTaiSan { get; set; }

    [StringLength(100)]
    public string? TinhTrang { get; set; }

    /// <summary>Triệu chứng trục trặc người dùng gặp phải — phần quan trọng nhất để IT chẩn đoán.</summary>
    public string? MoTaLoi { get; set; }

    /// <summary>Phòng/bàn đang để thiết bị, giúp IT biết chỗ tới nhận.</summary>
    [StringLength(200)]
    public string? ViTriHienTai { get; set; }

    [StringLength(200)]
    public string? PhuKienKemTheo { get; set; }

    public DateOnly? NgayTra { get; set; }

    /// <summary>Tên file ảnh lưu trên file server, không lưu byte[] để nhẹ CSDL.</summary>
    [StringLength(255)]
    public string? DuonDanAnh { get; set; }

    public string? GhiChu { get; set; }

    [ForeignKey("IdFormIt")]
    [InverseProperty("ItTraThietBi12s")]
    public virtual FormIt? IdFormItNavigation { get; set; }
}
