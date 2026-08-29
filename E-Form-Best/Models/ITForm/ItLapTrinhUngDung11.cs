using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace E_Form_Best.Models.ITForm;

/// <summary>
/// Chi tiết đơn số 11 — Yêu cầu lập trình ứng dụng / tool: viết mới hoặc nâng cấp
/// web app, tool desktop, macro Excel, script tự động hoá, dashboard.
/// Mỗi dòng gắn với một đơn tổng trong <c>FormIT</c>.
/// </summary>
[Table("IT_LapTrinhUngDung_11")]
[Index("IdFormIt", Name = "IX_IT_LapTrinhUngDung_11_idFormIT")]
public partial class ItLapTrinhUngDung11
{
    [Key]
    [Column("id")]
    public int Id { get; set; }

    [Column("id_FormIT")]
    public int? IdFormIt { get; set; }

    [StringLength(100)]
    public string? LoaiYeuCau { get; set; }

    [StringLength(200)]
    public string? TenUngDung { get; set; }

    /// <summary>Bài toán nghiệp vụ cần giải quyết — phần quan trọng nhất của đơn.</summary>
    public string? MoTaYeuCau { get; set; }

    /// <summary>Nguồn dữ liệu đầu vào, file mẫu người dùng sẽ cung cấp.</summary>
    public string? DuLieuDauVao { get; set; }

    public string? KetQuaMongMuon { get; set; }

    /// <summary>Ai sẽ dùng và khoảng bao nhiêu người.</summary>
    [StringLength(200)]
    public string? NguoiSuDung { get; set; }

    [StringLength(200)]
    public string? BoPhanSuDung { get; set; }

    /// <summary>Hệ thống sẵn có mà ứng dụng phải kết nối: ERP, E-Form, file server...</summary>
    [StringLength(200)]
    public string? HeThongLienQuan { get; set; }

    [StringLength(100)]
    public string? TanSuatSuDung { get; set; }

    [StringLength(50)]
    public string? MucDoUuTien { get; set; }

    public DateOnly? NgayMongMuon { get; set; }

    /// <summary>Tên file ảnh minh hoạ lưu trên file server, không lưu byte[] để nhẹ CSDL.</summary>
    [StringLength(255)]
    public string? DuonDanAnh { get; set; }

    public string? GhiChu { get; set; }

    [ForeignKey("IdFormIt")]
    [InverseProperty("ItLapTrinhUngDung11s")]
    public virtual FormIt? IdFormItNavigation { get; set; }
}
