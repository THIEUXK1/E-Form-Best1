using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace E_Form_Best.Models.ITForm;

[Table("HR_NhaThauQuaCong_6")]
public partial class HrNhaThauQuaCong6
{
    [Key]
    [Column("id")]
    public int Id { get; set; }

    [Column("id_FormHR")]
    public int? IdFormHr { get; set; }

    public byte[]? Anh { get; set; }

    public string? DuongDanAnh { get; set; }

    [StringLength(250)]
    public string? TenNhaThau { get; set; }

    public int? SoNguoi { get; set; }

    [StringLength(100)]
    public string? NguoiDangKy { get; set; }

    public string? MucDichCongViec { get; set; }

    [ForeignKey("IdFormHr")]
    [InverseProperty("HrNhaThauQuaCong6s")]
    public virtual FormHr? IdFormHrNavigation { get; set; }
}
