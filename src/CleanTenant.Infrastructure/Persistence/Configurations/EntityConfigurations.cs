using CleanTenant.Domain.Identity;
using CleanTenant.Domain.Security;
using CleanTenant.Domain.Tenancy;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CleanTenant.Infrastructure.Persistence.Configurations;

// ============================================================================
// EF CORE FLUENT API KONFIGÜRASYONLARI
// 
// NEDEN DATA ANNOTATIONS DEĞİL?
// [Required], [MaxLength] gibi attribute'lar entity'lere eklenmez çünkü:
// 1. Domain katmanı EF Core'a bağımlı olmamalıdır
// 2. Fluent API daha fazla kontrol sağlar (composite key, index, ilişki vb.)
// 3. Konfigürasyon tek bir yerde merkezi olarak yönetilir
// 4. Entity sınıfları temiz ve sade kalır
// ============================================================================

/// <summary>Tenant tablosu konfigürasyonu.</summary>
public class TenantConfiguration : IEntityTypeConfiguration<Tenant>
{
    public void Configure(EntityTypeBuilder<Tenant> builder)
    {
        builder.ToTable("Tenants");

        builder.HasKey(t => t.Id);

        builder.Property(t => t.Name)
            .IsRequired()
            .HasMaxLength(200);

        builder.Property(t => t.Identifier)
            .IsRequired()
            .HasMaxLength(100);

        // Unique constraint: Aynı identifier'a sahip iki tenant olamaz
        builder.HasIndex(t => t.Identifier)
            .IsUnique()
            .HasDatabaseName("IX_Tenants_Identifier");

        builder.Property(t => t.TaxNumber)
            .HasMaxLength(20);

        builder.Property(t => t.ContactEmail)
            .HasMaxLength(256);

        builder.Property(t => t.ContactPhone)
            .HasMaxLength(20);

        // Settings alanı PostgreSQL JSONB olarak saklanır
        builder.Property(t => t.Settings)
            .HasColumnType("jsonb");

        // Soft delete index: Silinmemiş kayıtları hızlı sorgulama
        builder.HasIndex(t => t.IsDeleted)
            .HasDatabaseName("IX_Tenants_IsDeleted")
            .HasFilter("\"IsDeleted\" = false");

        // Tenant → Companies ilişkisi (1:N)
        builder.HasMany(t => t.Companies)
            .WithOne(c => c.Tenant)
            .HasForeignKey(c => c.TenantId)
            .OnDelete(DeleteBehavior.Restrict);  // Cascade delete yok — iş kuralı ile kontrol
    }
}

/// <summary>Company tablosu konfigürasyonu.</summary>
public class CompanyConfiguration : IEntityTypeConfiguration<Company>
{
    public void Configure(EntityTypeBuilder<Company> builder)
    {
        builder.ToTable("Companies");

        builder.HasKey(c => c.Id);

        builder.Property(c => c.Name)
            .IsRequired()
            .HasMaxLength(200);

        builder.Property(c => c.Code)
            .IsRequired()
            .HasMaxLength(50);

        // Composite unique: Aynı tenant içinde aynı kod olamaz
        builder.HasIndex(c => new { c.TenantId, c.Code })
            .IsUnique()
            .HasDatabaseName("IX_Companies_TenantId_Code");

        builder.Property(c => c.TaxNumber).HasMaxLength(20);
        builder.Property(c => c.TaxOffice).HasMaxLength(100);
        builder.Property(c => c.ContactEmail).HasMaxLength(256);
        builder.Property(c => c.ContactPhone).HasMaxLength(20);
        builder.Property(c => c.Address).HasMaxLength(500);
        builder.Property(c => c.Settings).HasColumnType("jsonb");

        // Performans: TenantId üzerinde index (en sık filtrelenen alan)
        builder.HasIndex(c => c.TenantId)
            .HasDatabaseName("IX_Companies_TenantId");
    }
}

/// <summary>ApplicationUser tablosu konfigürasyonu.</summary>
public class ApplicationUserConfiguration : IEntityTypeConfiguration<ApplicationUser>
{
    public void Configure(EntityTypeBuilder<ApplicationUser> builder)
    {
        builder.ToTable("Users");

        builder.HasKey(u => u.Id);

        builder.Property(u => u.Email)
            .IsRequired()
            .HasMaxLength(256);

        // E-posta benzersiz olmalı — çapraz kimlik sistemi buna dayanır
        builder.HasIndex(u => u.Email)
            .IsUnique()
            .HasDatabaseName("IX_Users_Email");

        builder.Property(u => u.FullName)
            .IsRequired()
            .HasMaxLength(200);

        builder.Property(u => u.PhoneNumber).HasMaxLength(20);
        builder.Property(u => u.PasswordHash).IsRequired().HasMaxLength(500);
        builder.Property(u => u.AuthenticatorKey).HasMaxLength(500);
        builder.Property(u => u.AvatarUrl).HasMaxLength(500);
        builder.Property(u => u.PreferredLanguage).HasMaxLength(10).HasDefaultValue("tr");
        builder.Property(u => u.TimeZone).HasMaxLength(50).HasDefaultValue("Europe/Istanbul");
        builder.Property(u => u.LastLoginIp).HasMaxLength(50);

        // Soft delete filtresi
        builder.HasIndex(u => u.IsDeleted)
            .HasDatabaseName("IX_Users_IsDeleted")
            .HasFilter("\"IsDeleted\" = false");
    }
}

/// <summary>SystemRole konfigürasyonu.</summary>
public class SystemRoleConfiguration : IEntityTypeConfiguration<SystemRole>
{
    public void Configure(EntityTypeBuilder<SystemRole> builder)
    {
        builder.ToTable("SystemRoles");
        builder.HasKey(r => r.Id);
        builder.Property(r => r.Name).IsRequired().HasMaxLength(100);
        builder.HasIndex(r => r.Name).IsUnique().HasDatabaseName("IX_SystemRoles_Name");
        builder.Property(r => r.Description).HasMaxLength(500);
        builder.Property(r => r.Permissions).HasColumnType("jsonb").HasDefaultValue("[]");
    }
}

/// <summary>TenantRole konfigürasyonu.</summary>
public class TenantRoleConfiguration : IEntityTypeConfiguration<TenantRole>
{
    public void Configure(EntityTypeBuilder<TenantRole> builder)
    {
        builder.ToTable("TenantRoles");
        builder.HasKey(r => r.Id);
        builder.Property(r => r.Name).IsRequired().HasMaxLength(100);
        // Aynı tenant içinde benzersiz rol adı
        builder.HasIndex(r => new { r.TenantId, r.Name })
            .IsUnique().HasDatabaseName("IX_TenantRoles_TenantId_Name");
        builder.Property(r => r.Description).HasMaxLength(500);
        builder.Property(r => r.Permissions).HasColumnType("jsonb").HasDefaultValue("[]");
        builder.HasIndex(r => r.TenantId).HasDatabaseName("IX_TenantRoles_TenantId");
    }
}

/// <summary>CompanyRole konfigürasyonu.</summary>
public class CompanyRoleConfiguration : IEntityTypeConfiguration<CompanyRole>
{
    public void Configure(EntityTypeBuilder<CompanyRole> builder)
    {
        builder.ToTable("CompanyRoles");
        builder.HasKey(r => r.Id);
        builder.Property(r => r.Name).IsRequired().HasMaxLength(100);
        // Aynı şirket içinde benzersiz rol adı
        builder.HasIndex(r => new { r.CompanyId, r.Name })
            .IsUnique().HasDatabaseName("IX_CompanyRoles_CompanyId_Name");
        builder.Property(r => r.Description).HasMaxLength(500);
        builder.Property(r => r.Permissions).HasColumnType("jsonb").HasDefaultValue("[]");
        builder.HasIndex(r => new { r.TenantId, r.CompanyId })
            .HasDatabaseName("IX_CompanyRoles_TenantId_CompanyId");
    }
}

// ============================================================================
// PIVOT TABLE (JUNCTION TABLE) KONFIGÜRASYONLARI
// Kullanıcı ↔ Rol atamaları için çoktan-çoğa ilişki tabloları.
// ============================================================================

/// <summary>UserSystemRole pivot tablosu.</summary>
public class UserSystemRoleConfiguration : IEntityTypeConfiguration<UserSystemRole>
{
    public void Configure(EntityTypeBuilder<UserSystemRole> builder)
    {
        builder.ToTable("UserSystemRoles");
        builder.HasKey(usr => usr.Id);
        builder.HasIndex(usr => new { usr.UserId, usr.SystemRoleId })
            .IsUnique().HasDatabaseName("IX_UserSystemRoles_UserId_RoleId");
        builder.Property(usr => usr.AssignedBy).IsRequired().HasMaxLength(256);

        builder.HasOne(usr => usr.User).WithMany(u => u.SystemRoles)
            .HasForeignKey(usr => usr.UserId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne(usr => usr.SystemRole).WithMany()
            .HasForeignKey(usr => usr.SystemRoleId).OnDelete(DeleteBehavior.Restrict);
    }
}

/// <summary>UserTenantRole pivot tablosu.</summary>
public class UserTenantRoleConfiguration : IEntityTypeConfiguration<UserTenantRole>
{
    public void Configure(EntityTypeBuilder<UserTenantRole> builder)
    {
        builder.ToTable("UserTenantRoles");
        builder.HasKey(utr => utr.Id);
        // Bir kullanıcı aynı tenant'ta aynı role iki kez atanamaz
        builder.HasIndex(utr => new { utr.UserId, utr.TenantId, utr.TenantRoleId })
            .IsUnique().HasDatabaseName("IX_UserTenantRoles_User_Tenant_Role");
        builder.Property(utr => utr.AssignedBy).IsRequired().HasMaxLength(256);

        builder.HasOne(utr => utr.User).WithMany(u => u.TenantRoles)
            .HasForeignKey(utr => utr.UserId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne(utr => utr.TenantRole).WithMany()
            .HasForeignKey(utr => utr.TenantRoleId).OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(utr => utr.TenantId).HasDatabaseName("IX_UserTenantRoles_TenantId");
    }
}

/// <summary>UserCompanyRole pivot tablosu.</summary>
public class UserCompanyRoleConfiguration : IEntityTypeConfiguration<UserCompanyRole>
{
    public void Configure(EntityTypeBuilder<UserCompanyRole> builder)
    {
        builder.ToTable("UserCompanyRoles");
        builder.HasKey(ucr => ucr.Id);
        builder.HasIndex(ucr => new { ucr.UserId, ucr.CompanyId, ucr.CompanyRoleId })
            .IsUnique().HasDatabaseName("IX_UserCompanyRoles_User_Company_Role");
        builder.Property(ucr => ucr.AssignedBy).IsRequired().HasMaxLength(256);

        builder.HasOne(ucr => ucr.User).WithMany(u => u.CompanyRoles)
            .HasForeignKey(ucr => ucr.UserId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne(ucr => ucr.CompanyRole).WithMany()
            .HasForeignKey(ucr => ucr.CompanyRoleId).OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(ucr => ucr.CompanyId).HasDatabaseName("IX_UserCompanyRoles_CompanyId");
    }
}

/// <summary>UserCompanyMembership pivot tablosu.</summary>
public class UserCompanyMembershipConfiguration : IEntityTypeConfiguration<UserCompanyMembership>
{
    public void Configure(EntityTypeBuilder<UserCompanyMembership> builder)
    {
        builder.ToTable("UserCompanyMemberships");
        builder.HasKey(ucm => ucm.Id);
        builder.HasIndex(ucm => new { ucm.UserId, ucm.CompanyId })
            .IsUnique().HasDatabaseName("IX_UserCompanyMemberships_User_Company");
        builder.Property(ucm => ucm.MembershipType).IsRequired().HasMaxLength(50);
        builder.Property(ucm => ucm.AssignedBy).IsRequired().HasMaxLength(256);

        builder.HasOne(ucm => ucm.User).WithMany(u => u.CompanyMemberships)
            .HasForeignKey(ucm => ucm.UserId).OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(ucm => ucm.CompanyId).HasDatabaseName("IX_UserCompanyMemberships_CompanyId");
    }
}

// ============================================================================
// GÜVENLİK TABLOLARI KONFIGÜRASYONLARI
// ============================================================================

/// <summary>UserSession konfigürasyonu.</summary>
public class UserSessionConfiguration : IEntityTypeConfiguration<UserSession>
{
    public void Configure(EntityTypeBuilder<UserSession> builder)
    {
        builder.ToTable("UserSessions");
        builder.HasKey(s => s.Id);
        builder.Property(s => s.TokenHash).IsRequired().HasMaxLength(500);
        builder.Property(s => s.RefreshTokenHash).IsRequired().HasMaxLength(500);
        builder.Property(s => s.IpAddress).IsRequired().HasMaxLength(50);
        builder.Property(s => s.UserAgent).IsRequired().HasMaxLength(500);
        builder.Property(s => s.DeviceHash).IsRequired().HasMaxLength(500);
        builder.Property(s => s.RevokedBy).HasMaxLength(256);

        // Aktif oturumu hızlı bulmak için
        builder.HasIndex(s => new { s.UserId, s.IsRevoked })
            .HasDatabaseName("IX_UserSessions_UserId_IsRevoked");

        builder.HasOne(s => s.User).WithMany()
            .HasForeignKey(s => s.UserId).OnDelete(DeleteBehavior.Cascade);
    }
}

/// <summary>UserAccessPolicy konfigürasyonu.</summary>
public class UserAccessPolicyConfiguration : IEntityTypeConfiguration<UserAccessPolicy>
{
    public void Configure(EntityTypeBuilder<UserAccessPolicy> builder)
    {
        builder.ToTable("UserAccessPolicies");
        builder.HasKey(p => p.Id);
        builder.Property(p => p.AllowedIpRanges).HasColumnType("jsonb").HasDefaultValue("[]");
        builder.Property(p => p.AllowedDays).HasColumnType("jsonb").HasDefaultValue("[]");

        builder.HasIndex(p => p.UserId)
            .IsUnique()  // Her kullanıcının tek bir erişim politikası olabilir
            .HasDatabaseName("IX_UserAccessPolicies_UserId");

        builder.HasOne(p => p.User).WithMany()
            .HasForeignKey(p => p.UserId).OnDelete(DeleteBehavior.Cascade);
    }
}

/// <summary>UserBlock konfigürasyonu.</summary>
public class UserBlockConfiguration : IEntityTypeConfiguration<UserBlock>
{
    public void Configure(EntityTypeBuilder<UserBlock> builder)
    {
        builder.ToTable("UserBlocks");
        builder.HasKey(b => b.Id);
        builder.Property(b => b.BlockedBy).IsRequired().HasMaxLength(256);
        builder.Property(b => b.Reason).HasMaxLength(500);
        builder.Property(b => b.LiftedBy).HasMaxLength(256);

        // Aktif blokları hızlı bulmak için
        builder.HasIndex(b => new { b.UserId, b.IsLifted })
            .HasDatabaseName("IX_UserBlocks_UserId_IsLifted");

        builder.HasOne(b => b.User).WithMany()
            .HasForeignKey(b => b.UserId).OnDelete(DeleteBehavior.Cascade);
    }
}

/// <summary>IpBlacklist konfigürasyonu.</summary>
public class IpBlacklistConfiguration : IEntityTypeConfiguration<IpBlacklist>
{
    public void Configure(EntityTypeBuilder<IpBlacklist> builder)
    {
        builder.ToTable("IpBlacklists");
        builder.HasKey(b => b.Id);
        builder.Property(b => b.IpAddressOrRange).IsRequired().HasMaxLength(50);
        builder.Property(b => b.Reason).HasMaxLength(500);

        builder.HasIndex(b => b.IpAddressOrRange)
            .HasDatabaseName("IX_IpBlacklists_IpAddress");

        builder.HasIndex(b => b.IsActive)
            .HasDatabaseName("IX_IpBlacklists_IsActive");
    }
}
