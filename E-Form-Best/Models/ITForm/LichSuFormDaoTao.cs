using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace E_Form_Best.Models.ITForm;

[Table("LichSuFormDaoTao")]
public partial class LichSuFormDaoTao
{
    [Key]
    [Column("id")]
    public int Id { get; set; }

    public int IdDaoTao { get; set; }

    [StringLength(255)]
    public string? TieuDe { get; set; }

    public string? Mota { get; set; }

    [Column(TypeName = "datetime")]
    public DateTime? Time { get; set; }

    public bool? IsRead { get; set; }

    public bool TrangThaiAnHien { get; set; } = true;

    [ForeignKey("IdDaoTao")]
    [InverseProperty("LichSuFormDaoTaos")]
    public virtual DaoTao IdDaoTaoNavigation { get; set; } = null!;
}
