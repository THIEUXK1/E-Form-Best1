using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace E_Form_Best.Models.ITForm;

[Table("DM_BoPhan")]
public partial class DmBoPhan
{
    [Key]
    [Column("IDBoPhan")]
    public int IdboPhan { get; set; }

    [StringLength(255)]
    public string? TenBoPhan { get; set; }

    public string? GhiChu { get; set; }

    [Column(TypeName = "datetime")]
    public DateTime? NgayTao { get; set; }

    public bool? TrangThai { get; set; }

    [InverseProperty("IdboPhanNavigation")]
    public virtual ICollection<DmNguoiDuyetLoaiDonBoPhan> DmNguoiDuyetLoaiDonBoPhans { get; set; } = new List<DmNguoiDuyetLoaiDonBoPhan>();
}
