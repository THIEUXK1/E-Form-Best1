using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace E_Form_Best.Models.ITForm;

public partial class UserDevice
{
    [Key]
    public int Id { get; set; }

    [Column("id_nguoi_dung")]
    public int IdNguoiDung { get; set; }

    public string? DeviceFingerprint { get; set; }

    [StringLength(255)]
    public string? DeviceName { get; set; }

    public string? DeviceToken { get; set; }

    [Column(TypeName = "datetime")]
    public DateTime? LastLogin { get; set; }

    public bool? IsTrusted { get; set; }

    public string? PushEndpoint { get; set; }

    public string? PushP256dh { get; set; }

    public string? PushAuth { get; set; }

    [ForeignKey("IdNguoiDung")]
    [InverseProperty("UserDevices")]
    public virtual User IdNguoiDungNavigation { get; set; } = null!;
}
