using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace E_Form_Best.Models.ITForm;

[Table("HR_HoTroTienDienThoai_7")]
public partial class HrHoTroTienDienThoai7
{
    [Key]
    [Column("id")]
    public int Id { get; set; }

    [Column("id_FormHR")]
    public int? IdFormHr { get; set; }

    public byte[]? Anh { get; set; }

    public string? DuongDanAnh { get; set; }

    [StringLength(255)]
    public string? ThongTinNhanVien { get; set; }

    [Column(TypeName = "money")]
    public decimal? MucHoTro { get; set; }

    public string? MucDich { get; set; }

    [StringLength(20)]
    public string? SoDienThoai { get; set; }

    [ForeignKey("IdFormHr")]
    [InverseProperty("HrHoTroTienDienThoai7s")]
    public virtual FormHr? IdFormHrNavigation { get; set; }
}
