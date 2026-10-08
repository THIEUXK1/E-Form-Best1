using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace E_Form_Best.Models.ITForm;

/// <summary>
/// Access Point Wi-Fi do IT quản lý, chia theo 3 công ty (trang /QLAP/{công ty}). Nhập tay như KK_Camera;
/// 3 cột trạng thái kết nối do AccessPointPingWorker ghi, người dùng không sửa.
/// Soft-delete: NgayXoa == null là còn hiệu lực.
/// </summary>
[Table("KK_AccessPoint")]
public partial class KkAccessPoint
{
    [Key]
    [Column("id_ap")]
    public int IdAp { get; set; }

    [Column("ma_ap")]
    [StringLength(100)]
    public string? MaAp { get; set; }

    [Column("ten_ap")]
    [StringLength(255)]
    public string TenAp { get; set; } = null!;

    [Column("dia_chi_ip")]
    [StringLength(50)]
    [Unicode(false)]
    public string? DiaChiIp { get; set; }

    [Column("dia_chi_mac")]
    [StringLength(20)]
    [Unicode(false)]
    public string? DiaChiMac { get; set; }

    [Column("hang_san_xuat")]
    [StringLength(100)]
    public string? HangSanXuat { get; set; }

    [Column("model")]
    [StringLength(100)]
    public string? Model { get; set; }

    [Column("serial")]
    [StringLength(100)]
    public string? Serial { get; set; }

    [Column("ssid")]
    [StringLength(255)]
    public string? Ssid { get; set; }

    /// <summary>Controller quản lý AP (tên/IP). Không đặt tên thuộc tính là "Controller": trùng route value
    /// "controller" khi model binding.</summary>
    [Column("controller")]
    [StringLength(255)]
    public string? TenController { get; set; }

    [Column("IDCongTy")]
    public int IdcongTy { get; set; }

    [Column("IDBoPhan")]
    public int? IdboPhan { get; set; }

    [Column("vi_tri")]
    [StringLength(500)]
    public string? ViTri { get; set; }

    [Column("tinh_trang")]
    [StringLength(100)]
    public string? TinhTrang { get; set; }

    [Column("ngay_lap_dat")]
    public DateOnly? NgayLapDat { get; set; }

    [Column("han_bao_hanh")]
    public DateOnly? HanBaoHanh { get; set; }

    [Column("ghi_chu")]
    public string? GhiChu { get; set; }

    /// <summary>UP / DOWN theo lần ping gần nhất; NULL = chưa kiểm (chưa khai IP hoặc job chưa chạy tới).</summary>
    [Column("trang_thai_ket_noi")]
    [StringLength(20)]
    [Unicode(false)]
    public string? TrangThaiKetNoi { get; set; }

    [Column("doi_trang_thai_luc", TypeName = "datetime")]
    public DateTime? DoiTrangThaiLuc { get; set; }

    [Column("kiem_tra_luc", TypeName = "datetime")]
    public DateTime? KiemTraLuc { get; set; }

    /// <summary>"AC" = trạng thái đọc từ AP controller, "PING" = máy chủ tự ping.</summary>
    [Column("nguon_trang_thai")]
    [StringLength(10)]
    [Unicode(false)]
    public string? NguonTrangThai { get; set; }

    // ---- Các cột dưới do job đọc AP controller ghi đè mỗi lượt, không nhập tay ----

    [Column("trang_thai_controller")]
    [StringLength(30)]
    [Unicode(false)]
    public string? TrangThaiController { get; set; }

    [Column("nhom_ap")]
    [StringLength(100)]
    public string? NhomAp { get; set; }

    [Column("phien_ban")]
    [StringLength(100)]
    public string? PhienBan { get; set; }

    [Column("so_client")]
    public int? SoClient { get; set; }

    [Column("thoi_gian_chay_giay")]
    public long? ThoiGianChayGiay { get; set; }

    [Column("nguoi_tao")]
    [StringLength(255)]
    public string? NguoiTao { get; set; }

    [Column("ngay_tao", TypeName = "datetime")]
    public DateTime? NgayTao { get; set; }

    [Column("ngay_cap_nhat", TypeName = "datetime")]
    public DateTime? NgayCapNhat { get; set; }

    [Column("ngay_xoa", TypeName = "datetime")]
    public DateTime? NgayXoa { get; set; }

    [Column("ly_do_xoa")]
    [StringLength(500)]
    public string? LyDoXoa { get; set; }

    [ForeignKey("IdcongTy")]
    public virtual KkCongTy? IdcongTyNavigation { get; set; }

    [ForeignKey("IdboPhan")]
    public virtual KkBoPhan? IdboPhanNavigation { get; set; }
}
