using CleanTenant.Application.Common.Interfaces;
using CleanTenant.Domain.Common;
using CleanTenant.Domain.Identity;
using CleanTenant.Domain.Security;
using CleanTenant.Domain.Tenancy;
using Microsoft.EntityFrameworkCore;

namespace CleanTenant.Infrastructure.Persistence;

/// <summary>
/// Ana veritabanı DbContext'i — IApplicationDbContext implementasyonu.
/// 
/// <para><b>EF CORE INTERCEPTOR'LAR:</b></para>
/// SaveChanges çağrıldığında aşağıdaki interceptor'lar sırasıyla çalışır:
/// <list type="number">
///   <item>TenantInterceptor: TenantId/CompanyId otomatik atama</item>
///   <item>AuditableInterceptor: CreatedBy/UpdatedBy otomatik doldurma</item>
///   <item>SoftDeleteInterceptor: Delete → IsDeleted = true dönüşümü</item>
///   <item>AuditTrailInterceptor: Eski/yeni değerleri Audit DB'ye yazma</item>
/// </list>
/// 
/// <para><b>GLOBAL QUERY FILTERS:</b></para>
/// Soft delete ve tenant izolasyonu için global filtreler uygulanır.
/// Her sorguya otomatik WHERE koşulu eklenir. Geliştirici bunları
/// düşünmek zorunda kalmaz.
/// </summary>
public class ApplicationDbContext : DbContext, IApplicationDbContext
{
    private readonly ICurrentUserService _currentUser;

    public ApplicationDbContext(
        DbContextOptions<ApplicationDbContext> options,
        ICurrentUserService currentUser)
        : base(options)
    {
        _currentUser = currentUser;
    }

    // ========================================================================
    // DbSet TANIMLARI
    // ========================================================================

    // Tenancy
    public DbSet<Tenant> Tenants => Set<Tenant>();
    public DbSet<Company> Companies => Set<Company>();

    // Identity
    public DbSet<ApplicationUser> Users => Set<ApplicationUser>();
    public DbSet<SystemRole> SystemRoles => Set<SystemRole>();
    public DbSet<TenantRole> TenantRoles => Set<TenantRole>();
    public DbSet<CompanyRole> CompanyRoles => Set<CompanyRole>();
    public DbSet<UserSystemRole> UserSystemRoles => Set<UserSystemRole>();
    public DbSet<UserTenantRole> UserTenantRoles => Set<UserTenantRole>();
    public DbSet<UserCompanyRole> UserCompanyRoles => Set<UserCompanyRole>();
    public DbSet<UserCompanyMembership> UserCompanyMemberships => Set<UserCompanyMembership>();

    // Security
    public DbSet<UserSession> UserSessions => Set<UserSession>();
    public DbSet<UserAccessPolicy> UserAccessPolicies => Set<UserAccessPolicy>();
    public DbSet<UserBlock> UserBlocks => Set<UserBlock>();
    public DbSet<IpBlacklist> IpBlacklists => Set<IpBlacklist>();

    // ========================================================================
    // MODEL CONFIGURATION
    // ========================================================================

    /// <summary>
    /// Convention yapılandırması — tüm DateTime property'lerine uygulanır.
    /// EF Core 7+ özelliği: Entity bazlı değil, global convention.
    /// </summary>
    protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
    {
        base.ConfigureConventions(configurationBuilder);

        // Tüm DateTime property'leri: DB'den okunurken Kind = Utc olarak işaretlenir.
        // Bu, Npgsql'in "DateTime.Kind must be Utc" kuralını karşılar.
        // Yazarken: DateTime.UtcNow zaten Kind = Utc olarak gelir.
        // Okurken: PostgreSQL timestamptz'den gelen değer Kind = Utc olarak işaretlenir.
        configurationBuilder.Properties<DateTime>()
            .HaveConversion<UtcDateTimeConverter>();

        configurationBuilder.Properties<DateTime?>()
            .HaveConversion<NullableUtcDateTimeConverter>();
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Tüm Fluent API konfigürasyonlarını Configurations klasöründen yükle
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(ApplicationDbContext).Assembly);

        // ================================================================
        // GLOBAL QUERY FILTERS
        // ================================================================

        // Soft Delete filtresi: IsDeleted == true olan kayıtlar otomatik filtrelenir
        // ISoftDeletable interface'ini implemente eden TÜM entity'ler için geçerli
        foreach (var entityType in modelBuilder.Model.GetEntityTypes())
        {
            if (typeof(ISoftDeletable).IsAssignableFrom(entityType.ClrType))
            {
                // EF Core'da global query filter dinamik olarak uygulanır
                // Lambda expression tree ile her entity tipi için filtre oluşturulur
                var method = typeof(ApplicationDbContext)
                    .GetMethod(nameof(ApplySoftDeleteFilter),
                        System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static)!
                    .MakeGenericMethod(entityType.ClrType);

                method.Invoke(null, [modelBuilder]);
            }
        }

        // Tenant filtresi: BaseTenantEntity türevleri için TenantId filtresi
        // _currentUser.ActiveTenantId null ise filtre uygulanmaz (SuperAdmin/SystemUser)
        foreach (var entityType in modelBuilder.Model.GetEntityTypes())
        {
            if (typeof(BaseTenantEntity).IsAssignableFrom(entityType.ClrType) &&
                entityType.ClrType != typeof(BaseTenantEntity))
            {
                var method = typeof(ApplicationDbContext)
                    .GetMethod(nameof(ApplyTenantFilter),
                        System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!
                    .MakeGenericMethod(entityType.ClrType);

                method.Invoke(this, [modelBuilder]);
            }
        }
    }

    /// <summary>
    /// Soft delete global query filter'ı uygular.
    /// Bu metod reflection ile her ISoftDeletable entity için çağrılır.
    /// </summary>
    private static void ApplySoftDeleteFilter<TEntity>(ModelBuilder modelBuilder)
        where TEntity : class, ISoftDeletable
    {
        modelBuilder.Entity<TEntity>().HasQueryFilter(e => !e.IsDeleted);
    }

    /// <summary>
    /// Tenant izolasyon filtresi uygular.
    /// ActiveTenantId null ise (SuperAdmin/SystemUser) filtre uygulanmaz.
    /// </summary>
    private void ApplyTenantFilter<TEntity>(ModelBuilder modelBuilder)
        where TEntity : BaseTenantEntity
    {
        modelBuilder.Entity<TEntity>().HasQueryFilter(e =>
            !e.IsDeleted &&
            (_currentUser.ActiveTenantId == null || e.TenantId == _currentUser.ActiveTenantId));
    }
}
