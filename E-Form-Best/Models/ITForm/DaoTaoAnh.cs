using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace E_Form_Best.Models.ITForm;

[Table("DaoTao_Anh")]
public partial class DaoTaoAnh
{
    [Key]
    [Column("id")]
    public int Id { get; set; }

    [Column("id_DaoTao")]
    public int IdDaoTao { get; set; }

    [StringLength(255)]
    public string TenFile { get; set; } = null!;

    [Column(TypeName = "datetime")]
    public DateTime? TimeUpload { get; set; }

    [ForeignKey("IdDaoTao")]
    [InverseProperty("DaoTaoAnhs")]
    public virtual DaoTao IdDaoTaoNavigation { get; set; } = null!;
}
