using CleanTenant.Domain.Identity;
using CleanTenant.Domain.Security;
using CleanTenant.Domain.Tenancy;
using Microsoft.EntityFrameworkCore;

namespace CleanTenant.Application.Common.Interfaces;

/// <summary>
/// Ana veritabanı sözleşmesi — Application katmanında tanımlanır.
/// 
/// <para><b>DEPENDENCY INVERSION PRENSİBİ:</b></para>
/// Application katmanı bu interface'i tanımlar.
/// Infrastructure katmanı ApplicationDbContext ile implemente eder.
/// Böylece Application katmanı EF Core'un somut sınıflarını bilmez,
/// sadece kendi tanımladığı sözleşmeyi kullanır.
/// 
/// <para><b>NEDEN DbSet PROPERTY'LERİ BURADA?</b></para>
/// Handler'lar ve Business Rules sınıfları sorgulama yapabilmesi için
/// DbSet'lere erişmesi gerekir. Ancak bu erişim interface üzerinden
/// sağlanır — somut DbContext'e bağımlılık oluşmaz.
/// </para>
/// </summary>
public interface IApplicationDbContext
{
    // ========================================================================
    // TENANCY
    // ========================================================================
    DbSet<Tenant> Tenants { get; }
    DbSet<Company> Companies { get; }

    // ========================================================================
    // IDENTITY
    // ========================================================================
    DbSet<ApplicationUser> Users { get; }
    DbSet<SystemRole> SystemRoles { get; }
    DbSet<TenantRole> TenantRoles { get; }
    DbSet<CompanyRole> CompanyRoles { get; }
    DbSet<UserSystemRole> UserSystemRoles { get; }
    DbSet<UserTenantRole> UserTenantRoles { get; }
    DbSet<UserCompanyRole> UserCompanyRoles { get; }
    DbSet<UserCompanyMembership> UserCompanyMemberships { get; }

    // ========================================================================
    // SECURITY
    // ========================================================================
    DbSet<UserSession> UserSessions { get; }
    DbSet<UserAccessPolicy> UserAccessPolicies { get; }
    DbSet<UserBlock> UserBlocks { get; }
    DbSet<IpBlacklist> IpBlacklists { get; }

    // ========================================================================
    // SAVE CHANGES
    // ========================================================================

    /// <summary>
    /// Değişiklikleri veritabanına kaydeder.
    /// Interceptor'lar SaveChanges sırasında otomatik çalışır:
    /// - AuditInterceptor: Audit trail kaydı
    /// - SoftDeleteInterceptor: Delete → IsDeleted dönüşümü
    /// - TenantInterceptor: TenantId/CompanyId otomatik atama
    /// </summary>
    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
