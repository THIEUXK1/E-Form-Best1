using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace E_Form_Best.Models.ITForm;

[Table("HR_DonTiepKhac_5")]
public partial class HrDonTiepKhac5
{
    [Key]
    [Column("id")]
    public int Id { get; set; }

    [Column("id_FormHR")]
    public int? IdFormHr { get; set; }

    public string? DuongDanAnh { get; set; }

    [StringLength(255)]
    public string? NguoiBook { get; set; }

    [StringLength(50)]
    [Unicode(false)]
    public string? SoMayBan { get; set; }

    [StringLength(255)]
    public string? TenCongTyKhach { get; set; }

    public int? SoLuongKhach { get; set; }

    public string? YeuCauTiepKhach { get; set; }

    [Column(TypeName = "datetime")]
    public DateTime? NgayYeuCau { get; set; }

    [StringLength(255)]
    public string? TenPhongHop { get; set; }

    public string? NhuCauPhongHop { get; set; }

    [Column(TypeName = "datetime")]
    public DateTime? ThoiGianBatDau { get; set; }

    [Column(TypeName = "datetime")]
    public DateTime? ThoiGianKetThuc { get; set; }

    public string? GhiChuPhongHop { get; set; }

    [StringLength(255)]
    public string? LoaiSuatAn { get; set; }

    public int? SoLuongSuat { get; set; }

    [StringLength(50)]
    public string? AnChay { get; set; }

    public int? SoLuongSuatAnChay { get; set; }

    public string? GhiChuSuatAn { get; set; }

    [StringLength(100)]
    public string? TrangThaiPhong { get; set; }

    [ForeignKey("IdFormHr")]
    [InverseProperty("HrDonTiepKhac5s")]
    public virtual FormHr? IdFormHrNavigation { get; set; }
}
