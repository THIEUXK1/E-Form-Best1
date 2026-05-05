using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace E_Form_Best.Models.ITForm;

[Table("CongViecSHD")]
public partial class CongViecShd
{
    [Key]
    [Column("id")]
    public int Id { get; set; }

    [Column("id_SHD_NguoiHoTro")]
    public int? IdShdNguoiHoTro { get; set; }

    public string? Ten { get; set; }

    [StringLength(250)]
    public string? TrangThai { get; set; }
}
