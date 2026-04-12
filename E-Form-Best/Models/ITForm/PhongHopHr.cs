using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace E_Form_Best.Models.ITForm;

[Table("PhongHopHR")]
public partial class PhongHopHr
{
    [Key]
    [Column("id")]
    public int Id { get; set; }

    [StringLength(255)]
    public string? TenPhongHop { get; set; }

    public string? GhiChu { get; set; }
}
