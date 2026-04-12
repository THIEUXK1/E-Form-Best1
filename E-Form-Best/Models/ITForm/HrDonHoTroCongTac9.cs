using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace E_Form_Best.Models.ITForm;

[Table("HR_DonHoTroCongTac_9")]
public partial class HrDonHoTroCongTac9
{
    [Key]
    [Column("id")]
    public int Id { get; set; }

    [Column("id_FormHR")]
    public int? IdFormHr { get; set; }

    public string? Anh { get; set; }

    public string? DuongDanAnh { get; set; }

    [StringLength(50)]
    public string? MaNhanVien { get; set; }

    [StringLength(250)]
    public string? TenKhachHang { get; set; }

    public bool? DatVeMayBay { get; set; }

    public bool? DatChoO { get; set; }

    public bool? BookXeCtyDuaDon { get; set; }

    public bool? DatBuaAn { get; set; }

    public string? NoiDungYeuCauChiTiet { get; set; }

    [Column(TypeName = "datetime")]
    public DateTime? NgayTao { get; set; }

    [StringLength(10)]
    public string? GioiTinh { get; set; }

    [ForeignKey("IdFormHr")]
    [InverseProperty("HrDonHoTroCongTac9s")]
    public virtual FormHr? IdFormHrNavigation { get; set; }
}
