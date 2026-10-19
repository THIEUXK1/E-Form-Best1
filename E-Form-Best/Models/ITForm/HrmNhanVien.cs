using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace E_Form_Best.Models.ITForm;

/// <summary>
/// Nhân viên từ HRM (HRM_AU2.dbo.hrms_employees), CHỈ ĐỌC. Không mở DbContext thứ hai vì HRM_AU2 cùng server
/// với ITForm: ITFormContext map entity này bằng ToSqlQuery đọc chéo database (xem ITFormContext.OnModelCreating).
/// Khoá theo mã NV, khớp DG_HoSoNhanVien.ma_nhan_vien / User.Tk.
/// </summary>
public partial class HrmNhanVien
{
    [Key]
    [Column("employee_code")]
    [StringLength(50)]
    public string MaNhanVien { get; set; } = null!;

    [Column("full_name")]
    [StringLength(200)]
    public string? HoTen { get; set; }

    [Column("dept_name")]
    [StringLength(200)]
    public string? PhongBan { get; set; }

    [Column("dept_name_en")]
    [StringLength(200)]
    public string? PhongBanEn { get; set; }

    [Column("job_name")]
    [StringLength(200)]
    public string? ChucVu { get; set; }

    [Column("job_grade_code")]
    [StringLength(50)]
    public string? CapBac { get; set; }

    [Column("hire_date")]
    public DateOnly? NgayVaoLam { get; set; }

    [Column("termination_date")]
    public DateOnly? NgayNghiViec { get; set; }

    [Column("is_active")]
    public bool? DangLamViec { get; set; }

    [Column("ltd_name")]
    [StringLength(200)]
    public string? CongTy { get; set; }
}
