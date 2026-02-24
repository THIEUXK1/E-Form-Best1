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

    public virtual DbSet<BinhLuanFormIt> BinhLuanFormIts { get; set; }

    public virtual DbSet<BoPhan> BoPhans { get; set; }

    public virtual DbSet<CongViec> CongViecs { get; set; }

    public virtual DbSet<CtDangKySuDungXeCongTac3> CtDangKySuDungXeCongTac3s { get; set; }

    public virtual DbSet<CtDangKySuDungXeDaily4> CtDangKySuDungXeDaily4s { get; set; }

    public virtual DbSet<CtDoiCaLam8> CtDoiCaLam8s { get; set; }

    public virtual DbSet<CtDonTiepKhac5> CtDonTiepKhac5s { get; set; }

    public virtual DbSet<CtHoTroTienDienThoai7> CtHoTroTienDienThoai7s { get; set; }

    public virtual DbSet<CtMangHangHoaRaCong2> CtMangHangHoaRaCong2s { get; set; }

    public virtual DbSet<CtNhaThauQuaCong6> CtNhaThauQuaCong6s { get; set; }

    public virtual DbSet<CtXinRaNgoai1> CtXinRaNgoai1s { get; set; }

    public virtual DbSet<DanhGium> DanhGia { get; set; }

    public virtual DbSet<FormHr> FormHrs { get; set; }

    public virtual DbSet<FormIt> FormIts { get; set; }

    public virtual DbSet<ItCtNguoiHoTro> ItCtNguoiHoTros { get; set; }

    public virtual DbSet<ItDangKiSuDungDtban4> ItDangKiSuDungDtban4s { get; set; }

    public virtual DbSet<ItDangKiSuDungWifi3> ItDangKiSuDungWifi3s { get; set; }

    public virtual DbSet<ItMail1> ItMail1s { get; set; }

    public virtual DbSet<ItNguoiHoTro> ItNguoiHoTros { get; set; }

    public virtual DbSet<ItOrderIt2> ItOrderIt2s { get; set; }

    public virtual DbSet<LichSu> LichSus { get; set; }

    public virtual DbSet<User> Users { get; set; }

    public virtual DbSet<UserBoPhan> UserBoPhans { get; set; }

    public virtual DbSet<UserDevice> UserDevices { get; set; }

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
#warning To protect potentially sensitive information in your connection string, you should move it out of source code. You can avoid scaffolding the connection string by using the Name= syntax to read it from configuration - see https://go.microsoft.com/fwlink/?linkid=2131148. For more guidance on storing connection strings, see https://go.microsoft.com/fwlink/?LinkId=723263.
        => optionsBuilder.UseSqlServer("Data Source=10.0.60.33;Initial Catalog=ITForm;Persist Security Info=True;User ID=sa;Password=BestP@cific;Encrypt=True;Trust Server Certificate=True");

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<BinhLuanFormIt>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PK__BinhLuan__3213E83F980A0B9F");

            entity.HasOne(d => d.IdFormNavigation).WithMany(p => p.BinhLuanFormIts).HasConstraintName("FK_BinhLuan_Form");
        });

        modelBuilder.Entity<BoPhan>(entity =>
        {
            entity.HasKey(e => e.IdBoPhan).HasName("PK__BoPhan__E66DCED5E953B918");
        });

        modelBuilder.Entity<CongViec>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PK__CongViec__3213E83F5D615D57");

            entity.HasOne(d => d.IdItNguoiHoTroNavigation).WithMany(p => p.CongViecs).HasConstraintName("FK_CongViec_NguoiHoTro");
        });

        modelBuilder.Entity<CtDangKySuDungXeCongTac3>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PK__CT_DangK__3213E83F55A9ACA6");

            entity.HasOne(d => d.IdFormHrNavigation).WithMany(p => p.CtDangKySuDungXeCongTac3s)
                .OnDelete(DeleteBehavior.SetNull)
                .HasConstraintName("FK_CT_DangKy_FormHR");
        });

        modelBuilder.Entity<CtDangKySuDungXeDaily4>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PK__CT_DangK__3213E83F3E15E888");

            entity.HasOne(d => d.IdFormHrNavigation).WithMany(p => p.CtDangKySuDungXeDaily4s)
                .OnDelete(DeleteBehavior.SetNull)
                .HasConstraintName("FK_CT_Daily_FormHR");
        });

        modelBuilder.Entity<CtDoiCaLam8>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PK__CT_DoiCa__3213E83F1067B20A");

            entity.HasOne(d => d.IdFormHrNavigation).WithMany(p => p.CtDoiCaLam8s)
                .OnDelete(DeleteBehavior.Cascade)
                .HasConstraintName("FK_CT_DoiCaLam_8_FormHR");
        });

        modelBuilder.Entity<CtDonTiepKhac5>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PK__CT_DonTi__3213E83FDE6F0C24");

            entity.HasOne(d => d.IdFormHrNavigation).WithMany(p => p.CtDonTiepKhac5s)
                .OnDelete(DeleteBehavior.SetNull)
                .HasConstraintName("FK_CT_DonTiep_FormHR");
        });

        modelBuilder.Entity<CtHoTroTienDienThoai7>(entity =>
        {
            entity.HasOne(d => d.IdFormHrNavigation).WithMany(p => p.CtHoTroTienDienThoai7s)
                .OnDelete(DeleteBehavior.Cascade)
                .HasConstraintName("FK_CT_HoTroTienDienThoai_7_FormHR");
        });

        modelBuilder.Entity<CtMangHangHoaRaCong2>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PK__CT_MangH__3213E83F50B03DD7");

            entity.HasOne(d => d.IdFormHrNavigation).WithMany(p => p.CtMangHangHoaRaCong2s).HasConstraintName("FK_CT_MangHangHoa_FormHR");
        });

        modelBuilder.Entity<CtNhaThauQuaCong6>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PK__CT_NhaTh__3213E83F93B09EB6");

            entity.HasOne(d => d.IdFormHrNavigation).WithMany(p => p.CtNhaThauQuaCong6s)
                .OnDelete(DeleteBehavior.SetNull)
                .HasConstraintName("FK_CT_NhaThau_FormHR");
        });

        modelBuilder.Entity<CtXinRaNgoai1>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PK__CT_XinRa__3213E83F517B9771");

            entity.HasOne(d => d.IdFormHrNavigation).WithMany(p => p.CtXinRaNgoai1s).HasConstraintName("FK_CT_XinRaNgoai_FormHR");
        });

        modelBuilder.Entity<DanhGium>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PK__DanhGia__3213E83F747EAFE3");

            entity.HasOne(d => d.IdFormItNavigation).WithMany(p => p.DanhGia).HasConstraintName("FK_DanhGia_FormIT");
        });

        modelBuilder.Entity<FormHr>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PK__FormHR__3213E83F63FD39F1");
        });

        modelBuilder.Entity<FormIt>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PK__FormIT__3213E83F25A63261");
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

        modelBuilder.Entity<LichSu>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PK__LichSu__3213E83F7913614E");

            entity.HasOne(d => d.IdFormItNavigation).WithMany(p => p.LichSus).HasConstraintName("FK_LichSu_FormIT");
        });

        modelBuilder.Entity<User>(entity =>
        {
            entity.HasKey(e => e.IdNguoiDung).HasName("PK__User__75D6A11EB2C54D7B");

            entity.Property(e => e.NgayCapNhat).HasDefaultValueSql("(sysutcdatetime())");
            entity.Property(e => e.NgayTao).HasDefaultValueSql("(sysutcdatetime())");
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
