using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace E_Form_Best.Models.ITForm;

/// <summary>
/// Chi tiết đơn số 13 — Cấp quyền Internet cho một máy tính công ty
/// (nhiều máy bị chặn trình duyệt, cần chủ quản duyệt rồi IT mới mở).
/// Mỗi đơn một máy, gắn với một đơn tổng trong <c>FormIT</c>.
/// </summary>
[Table("IT_CapQuyenInternet_13")]
[Index("IdFormIt", Name = "IX_IT_CapQuyenInternet_13_idFormIT")]
public partial class ItCapQuyenInternet13
{
    [Key]
    [Column("id")]
    public int Id { get; set; }

    [Column("id_FormIT")]
    public int? IdFormIt { get; set; }

    /// <summary>"Cấp mới" hoặc "Thu hồi".</summary>
    [StringLength(50)]
    public string? LoaiYeuCau { get; set; }

    /// <summary>Tên máy hoặc mã tài sản của máy cần mở Internet.</summary>
    [StringLength(200)]
    public string? TenMayTinh { get; set; }

    [StringLength(50)]
    public string? DiaChiIp { get; set; }

    [StringLength(250)]
    public string? NguoiSuDung { get; set; }

    /// <summary>"Toàn bộ Internet" hoặc "Website cụ thể".</summary>
    [StringLength(50)]
    public string? PhamVi { get; set; }

    /// <summary>Danh sách website, mỗi dòng một trang; chỉ có khi PhamVi = "Website cụ thể".</summary>
    public string? DanhSachWebsite { get; set; }

    /// <summary>"Vĩnh viễn" hoặc "Có thời hạn".</summary>
    [StringLength(50)]
    public string? ThoiHan { get; set; }

    /// <summary>Ngày IT phải đóng lại quyền; chỉ có khi ThoiHan = "Có thời hạn".</summary>
    public DateOnly? NgayHetHan { get; set; }

    public string? LyDo { get; set; }

    public string? GhiChu { get; set; }

    /// <summary>Tên file ảnh minh hoạ lưu trên file server, không lưu byte[] để nhẹ CSDL.</summary>
    [StringLength(255)]
    public string? DuongDanAnh { get; set; }

    [ForeignKey("IdFormIt")]
    [InverseProperty("ItCapQuyenInternet13s")]
    public virtual FormIt? IdFormItNavigation { get; set; }
}
