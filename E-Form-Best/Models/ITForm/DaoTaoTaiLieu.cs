using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace E_Form_Best.Models.ITForm;

[Table("DaoTao_TaiLieu")]
public partial class DaoTaoTaiLieu
{
    [Key]
    [Column("id")]
    public int Id { get; set; }

    [Column("id_DaoTao")]
    public int IdDaoTao { get; set; }

    [StringLength(255)]
    public string TenFile { get; set; } = null!;

    [StringLength(255)]
    public string? TenGoc { get; set; }

    public int SoPhienBan { get; set; } = 1;

    public bool IsCurrent { get; set; } = true;

    [Column(TypeName = "datetime")]
    public DateTime? TimeUpload { get; set; }

    public int? IdNguoiUpload { get; set; }

    [ForeignKey("IdDaoTao")]
    [InverseProperty("DaoTaoTaiLieus")]
    public virtual DaoTao IdDaoTaoNavigation { get; set; } = null!;
}
