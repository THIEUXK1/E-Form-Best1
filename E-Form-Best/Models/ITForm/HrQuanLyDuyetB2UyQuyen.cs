using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace E_Form_Best.Models.ITForm;

[Table("HR_QuanLyDuyetB2_UyQuyen")]
public partial class HrQuanLyDuyetB2UyQuyen
{
    [Key]
    [Column("id")]
    public int Id { get; set; }

    [Column("id_HR_QuanLyDuyetB2")]
    public int IdHrQuanLyDuyetB2 { get; set; }

    [Column("MaNVUyQuyen")]
    [StringLength(50)]
    [Unicode(false)]
    public string? MaNvuyQuyen { get; set; }

    [StringLength(255)]
    public string? HoTenUyQuyen { get; set; }

    [ForeignKey("IdHrQuanLyDuyetB2")]
    [InverseProperty("HrQuanLyDuyetB2UyQuyens")]
    public virtual HrQuanLyDuyetB2 IdHrQuanLyDuyetB2Navigation { get; set; } = null!;
}
