using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace E_Form_Best.Models.ITForm;

[Table("SHD_QuanLyDuyetB2_UyQuyen")]
public partial class ShdQuanLyDuyetB2UyQuyen
{
    [Key]
    [Column("id")]
    public int Id { get; set; }

    [Column("id_SHD_QuanLyDuyetB2")]
    public int IdShdQuanLyDuyetB2 { get; set; }

    [Column("MaNVUyQuyen")]
    [StringLength(50)]
    [Unicode(false)]
    public string? MaNvuyQuyen { get; set; }

    [StringLength(255)]
    public string? HoTenUyQuyen { get; set; }

    [ForeignKey("IdShdQuanLyDuyetB2")]
    [InverseProperty("ShdQuanLyDuyetB2UyQuyens")]
    public virtual ShdQuanLyDuyetB2 IdShdQuanLyDuyetB2Navigation { get; set; } = null!;
}
