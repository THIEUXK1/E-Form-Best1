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

    [ForeignKey("IdFormHr")]
    [InverseProperty("HrDonTiepKhac5s")]
    public virtual FormHr? IdFormHrNavigation { get; set; }
}
