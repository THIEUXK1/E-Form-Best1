using System;
using System.Collections.Generic;
using E_Form_Best.Models.ITForm;
using Microsoft.EntityFrameworkCore;

namespace E_Form_Best.Context;

public partial class ITFormContext : DbContext
{
    public ITFormContext()
    {
    }

    public ITFormContext(DbContextOptions<ITFormContext> options)
        : base(options)
    {
    }

    public virtual DbSet<BinhLuanFormHr> BinhLuanFormHrs { get; set; }

    public virtual DbSet<BinhLuanFormIt> BinhLuanFormIts { get; set; }

    public virtual DbSet<BoPhan> BoPhans { get; set; }

    public virtual DbSet<CongViecIt> CongViecIts { get; set; }

    public virtual DbSet<DanhGiaFormIt> DanhGiaFormIts { get; set; }

    public virtual DbSet<FormHr> FormHrs { get; set; }

    public virtual DbSet<FormIt> FormIts { get; set; }

    public virtual DbSet<HrBaoVeXacNhan> HrBaoVeXacNhans { get; set; }

    public virtual DbSet<HrCtNguoiHoTro> HrCtNguoiHoTros { get; set; }

    public virtual DbSet<HrDangKySuDungXeCongTac3> HrDangKySuDungXeCongTac3s { get; set; }

    public virtual DbSet<HrDangKySuDungXeDaily4> HrDangKySuDungXeDaily4s { get; set; }

    public virtual DbSet<HrDoiCaLam8> HrDoiCaLam8s { get; set; }

    public virtual DbSet<HrDonHoTroCongTac9> HrDonHoTroCongTac9s { get; set; }

    public virtual DbSet<HrDonTiepKhac5> HrDonTiepKhac5s { get; set; }

    public virtual DbSet<HrHoTroTienDienThoai7> HrHoTroTienDienThoai7s { get; set; }

    public virtual DbSet<HrMangHangHoaRaCong2> HrMangHangHoaRaCong2s { get; set; }

    public virtual DbSet<HrNguoiHoTro> HrNguoiHoTros { get; set; }

    public virtual DbSet<HrNhaThauQuaCong6> HrNhaThauQuaCong6s { get; set; }

    public virtual DbSet<HrXinRaNgoai1> HrXinRaNgoai1s { get; set; }

    public virtual DbSet<ItCtNguoiHoTro> ItCtNguoiHoTros { get; set; }

    public virtual DbSet<ItDangKiSuDungDtban4> ItDangKiSuDungDtban4s { get; set; }

    public virtual DbSet<ItDangKiSuDungWifi3> ItDangKiSuDungWifi3s { get; set; }

    public virtual DbSet<ItMail1> ItMail1s { get; set; }

    public virtual DbSet<ItNguoiHoTro> ItNguoiHoTros { get; set; }

    public virtual DbSet<ItOrderIt2> ItOrderIt2s { get; set; }

    public virtual DbSet<LichSuFormHr> LichSuFormHrs { get; set; }

    public virtual DbSet<LichSuFormIt> LichSuFormIts { get; set; }

    public virtual DbSet<Quyen> Quyens { get; set; }

    public virtual DbSet<User> Users { get; set; }

    public virtual DbSet<UserBoPhan> UserBoPhans { get; set; }

    public virtual DbSet<UserDevice> UserDevices { get; set; }

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
#warning To protect potentially sensitive information in your connection string, you should move it out of source code. You can avoid scaffolding the connection string by using the Name= syntax to read it from configuration - see https://go.microsoft.com/fwlink/?linkid=2131148. For more guidance on storing connection strings, see https://go.microsoft.com/fwlink/?LinkId=723263.
        => optionsBuilder.UseSqlServer("Data Source=10.0.60.33;Initial Catalog=ITForm;Persist Security Info=True;User ID=sa;Password=BestP@cific;Encrypt=True;Trust Server Certificate=True");

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<BinhLuanFormHr>(entity =>
        {
            entity.Property(e => e.ThoiGian).HasDefaultValueSql("(getdate())");
        });

        modelBuilder.Entity<BinhLuanFormIt>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PK__BinhLuan__3213E83F980A0B9F");

            entity.HasOne(d => d.IdFormNavigation).WithMany(p => p.BinhLuanFormIts).HasConstraintName("FK_BinhLuan_Form");
        });

        modelBuilder.Entity<BoPhan>(entity =>
        {
            entity.HasKey(e => e.IdBoPhan).HasName("PK__BoPhan__E66DCED5E953B918");
        });

        modelBuilder.Entity<CongViecIt>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PK__CongViec__3213E83F5D615D57");

            entity.HasOne(d => d.IdItNguoiHoTroNavigation).WithMany(p => p.CongViecIts).HasConstraintName("FK_CongViec_NguoiHoTro");
        });

        modelBuilder.Entity<DanhGiaFormIt>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PK__DanhGia__3213E83F747EAFE3");

            entity.HasOne(d => d.IdFormItNavigation).WithMany(p => p.DanhGiaFormIts).HasConstraintName("FK_DanhGia_FormIT");
        });

        modelBuilder.Entity<FormHr>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PK__FormHR__3213E83F63FD39F1");
        });

        modelBuilder.Entity<FormIt>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PK__FormIT__3213E83F25A63261");
        });

        modelBuilder.Entity<HrBaoVeXacNhan>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PK__HR_CT_Ba__3213E83F2C627C82");

            entity.Property(e => e.ThoiGianHeThong).HasDefaultValueSql("(getdate())");

            entity.HasOne(d => d.IdFormHrNavigation).WithMany(p => p.HrBaoVeXacNhans).HasConstraintName("FK_BaoVeXacNhan_FormHR");
        });

        modelBuilder.Entity<HrCtNguoiHoTro>(entity =>
        {
            entity.HasOne(d => d.IdFormHrNavigation).WithMany(p => p.HrCtNguoiHoTros).HasConstraintName("FK_HR_CT_NguoiHoTro_FormHR");

            entity.HasOne(d => d.IdHrNguoiHoTroNavigation).WithMany(p => p.HrCtNguoiHoTros).HasConstraintName("FK_HR_CT_NguoiHoTro_NguoiHoTro");
        });

        modelBuilder.Entity<HrDangKySuDungXeCongTac3>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PK__CT_DangK__3213E83F55A9ACA6");

            entity.HasOne(d => d.IdFormHrNavigation).WithMany(p => p.HrDangKySuDungXeCongTac3s)
                .OnDelete(DeleteBehavior.SetNull)
                .HasConstraintName("FK_CT_DangKy_FormHR");
        });

        modelBuilder.Entity<HrDangKySuDungXeDaily4>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PK__CT_DangK__3213E83F3E15E888");

            entity.HasOne(d => d.IdFormHrNavigation).WithMany(p => p.HrDangKySuDungXeDaily4s)
                .OnDelete(DeleteBehavior.SetNull)
                .HasConstraintName("FK_CT_Daily_FormHR");
        });

        modelBuilder.Entity<HrDoiCaLam8>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PK__CT_DoiCa__3213E83F1067B20A");

            entity.HasOne(d => d.IdFormHrNavigation).WithMany(p => p.HrDoiCaLam8s)
                .OnDelete(DeleteBehavior.Cascade)
                .HasConstraintName("FK_CT_DoiCaLam_8_FormHR");
        });

        modelBuilder.Entity<HrDonHoTroCongTac9>(entity =>
        {
            entity.Property(e => e.BookXeCtyDuaDon).HasDefaultValue(false);
            entity.Property(e => e.DatBuaAn).HasDefaultValue(false);
            entity.Property(e => e.DatChoO).HasDefaultValue(false);
            entity.Property(e => e.DatVeMayBay).HasDefaultValue(false);
            entity.Property(e => e.NgayTao).HasDefaultValueSql("(getdate())");

            entity.HasOne(d => d.IdFormHrNavigation).WithMany(p => p.HrDonHoTroCongTac9s)
                .OnDelete(DeleteBehavior.Cascade)
                .HasConstraintName("FK_DonHoTro_FormHR");
        });

        modelBuilder.Entity<HrDonTiepKhac5>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PK__CT_DonTi__3213E83FDE6F0C24");

            entity.HasOne(d => d.IdFormHrNavigation).WithMany(p => p.HrDonTiepKhac5s)
                .OnDelete(DeleteBehavior.SetNull)
                .HasConstraintName("FK_CT_DonTiep_FormHR");
        });

        modelBuilder.Entity<HrHoTroTienDienThoai7>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PK_CT_HoTroTienDienThoai_7");

            entity.HasOne(d => d.IdFormHrNavigation).WithMany(p => p.HrHoTroTienDienThoai7s)
                .OnDelete(DeleteBehavior.Cascade)
                .HasConstraintName("FK_CT_HoTroTienDienThoai_7_FormHR");
        });

        modelBuilder.Entity<HrMangHangHoaRaCong2>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PK__CT_MangH__3213E83F50B03DD7");

            entity.HasOne(d => d.IdFormHrNavigation).WithMany(p => p.HrMangHangHoaRaCong2s).HasConstraintName("FK_CT_MangHangHoa_FormHR");
        });

        modelBuilder.Entity<HrNhaThauQuaCong6>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PK__CT_NhaTh__3213E83F93B09EB6");

            entity.HasOne(d => d.IdFormHrNavigation).WithMany(p => p.HrNhaThauQuaCong6s)
                .OnDelete(DeleteBehavior.SetNull)
                .HasConstraintName("FK_CT_NhaThau_FormHR");
        });

        modelBuilder.Entity<HrXinRaNgoai1>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PK__CT_XinRa__3213E83F517B9771");

            entity.HasOne(d => d.IdFormHrNavigation).WithMany(p => p.HrXinRaNgoai1s).HasConstraintName("FK_CT_XinRaNgoai_FormHR");
        });

        modelBuilder.Entity<ItCtNguoiHoTro>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PK__IT_CT_Ng__3213E83F994EF380");

            entity.HasOne(d => d.IdFormItNavigation).WithMany(p => p.ItCtNguoiHoTros).HasConstraintName("FK_CT_FormIT");

            entity.HasOne(d => d.IdItNguoiHoTroNavigation).WithMany(p => p.ItCtNguoiHoTros).HasConstraintName("FK_CT_NguoiHoTro");
        });

        modelBuilder.Entity<ItDangKiSuDungDtban4>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PK__IT_DangK__3213E83F76CB6499");

            entity.HasOne(d => d.IdFormItNavigation).WithMany(p => p.ItDangKiSuDungDtban4s)
                .OnDelete(DeleteBehavior.SetNull)
                .HasConstraintName("FK_ITDTBan_FormIT");
        });

        modelBuilder.Entity<ItDangKiSuDungWifi3>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PK__IT_DangK__3213E83F8C22D6FB");

            entity.HasOne(d => d.IdFormItNavigation).WithMany(p => p.ItDangKiSuDungWifi3s)
                .OnDelete(DeleteBehavior.SetNull)
                .HasConstraintName("FK_ITWifi_FormIT");
        });

        modelBuilder.Entity<ItMail1>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PK__IT_Mail___3213E83F52160549");

            entity.HasOne(d => d.IdFormItNavigation).WithMany(p => p.ItMail1s)
                .OnDelete(DeleteBehavior.SetNull)
                .HasConstraintName("FK_ITMail_FormIT");
        });

        modelBuilder.Entity<ItNguoiHoTro>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PK__IT_Nguoi__3213E83F4E28386C");
        });

        modelBuilder.Entity<ItOrderIt2>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PK__IT_Order__3213E83F4A9C39E3");

            entity.HasOne(d => d.IdFormItNavigation).WithMany(p => p.ItOrderIt2s)
                .OnDelete(DeleteBehavior.SetNull)
                .HasConstraintName("FK_ITOrder_FormIT");
        });

        modelBuilder.Entity<LichSuFormHr>(entity =>
        {
            entity.Property(e => e.IsRead).HasDefaultValue(false);
            entity.Property(e => e.Time).HasDefaultValueSql("(getdate())");
        });

        modelBuilder.Entity<LichSuFormIt>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PK__LichSu__3213E83F7913614E");

            entity.HasOne(d => d.IdFormItNavigation).WithMany(p => p.LichSuFormIts).HasConstraintName("FK_LichSu_FormIT");
        });

        modelBuilder.Entity<Quyen>(entity =>
        {
            entity.HasKey(e => e.IdQuyen).HasName("PK__Quyen__AE8CD30F6693B373");
        });

        modelBuilder.Entity<User>(entity =>
        {
            entity.HasKey(e => e.IdNguoiDung).HasName("PK__User__75D6A11EB2C54D7B");

            entity.Property(e => e.NgayCapNhat).HasDefaultValueSql("(sysutcdatetime())");
            entity.Property(e => e.NgayTao).HasDefaultValueSql("(sysutcdatetime())");

            entity.HasMany(d => d.IdQuyens).WithMany(p => p.IdNguoiDungs)
                .UsingEntity<Dictionary<string, object>>(
                    "UserQuyen",
                    r => r.HasOne<Quyen>().WithMany()
                        .HasForeignKey("IdQuyen")
                        .OnDelete(DeleteBehavior.ClientSetNull)
                        .HasConstraintName("FK_UserQuyen_Quyen_Current"),
                    l => l.HasOne<User>().WithMany()
                        .HasForeignKey("IdNguoiDung")
                        .OnDelete(DeleteBehavior.ClientSetNull)
                        .HasConstraintName("FK_UserQuyen_User_Current"),
                    j =>
                    {
                        j.HasKey("IdNguoiDung", "IdQuyen").HasName("PK__User_Quy__9F3E6C2E8D999CB5");
                        j.ToTable("User_Quyen");
                        j.IndexerProperty<int>("IdNguoiDung").HasColumnName("id_nguoi_dung");
                        j.IndexerProperty<int>("IdQuyen").HasColumnName("id_quyen");
                    });
        });

        modelBuilder.Entity<UserBoPhan>(entity =>
        {
            entity.HasKey(e => new { e.IdNguoiDung, e.IdBoPhan }).HasName("PK__User_BoP__3BB07DF3A33D7C1A");

            entity.HasOne(d => d.IdBoPhanNavigation).WithMany(p => p.UserBoPhans)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_BoPhan");

            entity.HasOne(d => d.IdNguoiDungNavigation).WithMany(p => p.UserBoPhans)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_User");
        });

        modelBuilder.Entity<UserDevice>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PK__UserDevi__3214EC07321181E5");

            entity.Property(e => e.IsTrusted).HasDefaultValue(true);
            entity.Property(e => e.LastLogin).HasDefaultValueSql("(getdate())");

            entity.HasOne(d => d.IdNguoiDungNavigation).WithMany(p => p.UserDevices).HasConstraintName("FK_UserDevice_User");
        });

        OnModelCreatingPartial(modelBuilder);
    }

    partial void OnModelCreatingPartial(ModelBuilder modelBuilder);
}
