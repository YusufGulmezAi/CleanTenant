using CleanTenant.Domain.Common;

namespace CleanTenant.Domain.Identity;

// ============================================================================
// ROL TANIMLARI
// Her seviye kendi rol havuzunu yönetir.
// Roller izin listesi (Permissions) taşır — JSONB olarak saklanır.
// ============================================================================

/// <summary>
/// Sistem seviyesi rol tanımı.
/// Sadece SuperAdmin tarafından yönetilir.
/// Bu roller tüm tenant'larda geçerlidir.
/// Örnek: "TechnicalSupport", "Auditor", "ReadOnlyAdmin"
/// </summary>
public class SystemRole : BaseAuditableEntity
{
    private SystemRole() { }

    /// <summary>Rol adı (benzersiz). Örnek: "TechnicalSupport"</summary>
    public string Name { get; private set; } = default!;

    /// <summary>Rol açıklaması.</summary>
    public string? Description { get; private set; }

    /// <summary>
    /// Bu role ait izin listesi — PostgreSQL JSONB.
    /// 
    /// <para><b>NEDEN JSONB?</b></para>
    /// İzinler modüler ve genişletilebilir olmalıdır. Yeni bir modül
    /// eklendiğinde migration yapmadan yeni izinler tanımlanabilir.
    /// 
    /// Örnek JSON:
    /// <code>
    /// ["tenants.read", "tenants.write", "users.read", "audit.read"]
    /// </code>
    /// </para>
    /// </summary>
    public string Permissions { get; private set; } = "[]";

    /// <summary>
    /// Sistem tarafından oluşturulan yerleşik rol mü?
    /// true ise: Silinemez ve adı değiştirilemez.
    /// Örnek: "SuperAdmin" rolü yerleşiktir.
    /// </summary>
    public bool IsSystem { get; private set; }

    /// <summary>Rol aktif mi?</summary>
    public bool IsActive { get; private set; } = true;

    public static SystemRole Create(string name, string? description, string permissions, bool isSystem = false)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name, nameof(name));
        return new SystemRole
        {
            Id = Guid.CreateVersion7(),
            Name = name.Trim(),
            Description = description?.Trim(),
            Permissions = permissions,
            IsSystem = isSystem,
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        };
    }

    public void Update(string name, string? description, string permissions)
    {
        if (IsSystem)
            throw new InvalidOperationException("Yerleşik (system) roller düzenlenemez.");

        Name = name.Trim();
        Description = description?.Trim();
        Permissions = permissions;
    }
}

/// <summary>
/// Tenant seviyesi rol tanımı.
/// TenantAdmin tarafından yönetilir — her tenant kendi rol havuzuna sahiptir.
/// Bu roller sadece tanımlandığı tenant içinde geçerlidir.
/// Örnek: "SeniorAccountant", "JuniorAccountant", "Viewer"
/// </summary>
public class TenantRole : BaseTenantEntity
{
    private TenantRole() { }

    /// <summary>Rol adı (tenant içinde benzersiz).</summary>
    public string Name { get; private set; } = default!;

    /// <summary>Rol açıklaması.</summary>
    public string? Description { get; private set; }

    /// <summary>Bu role ait izin listesi (JSONB).</summary>
    public string Permissions { get; private set; } = "[]";

    /// <summary>Rol aktif mi?</summary>
    public bool IsActive { get; private set; } = true;

    public static TenantRole Create(Guid tenantId, string name, string? description, string permissions)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name, nameof(name));
        return new TenantRole
        {
            Id = Guid.CreateVersion7(),
            TenantId = tenantId,
            Name = name.Trim(),
            Description = description?.Trim(),
            Permissions = permissions,
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        };
    }

    public void Update(string name, string? description, string permissions)
    {
        Name = name.Trim();
        Description = description?.Trim();
        Permissions = permissions;
    }
}

/// <summary>
/// Şirket seviyesi rol tanımı.
/// CompanyAdmin tarafından yönetilir — her şirket kendi rol havuzuna sahiptir.
/// Örnek: "Cashier", "StockManager", "ReportViewer"
/// </summary>
public class CompanyRole : BaseCompanyEntity
{
    private CompanyRole() { }

    /// <summary>Rol adı (şirket içinde benzersiz).</summary>
    public string Name { get; private set; } = default!;

    /// <summary>Rol açıklaması.</summary>
    public string? Description { get; private set; }

    /// <summary>Bu role ait izin listesi (JSONB).</summary>
    public string Permissions { get; private set; } = "[]";

    /// <summary>Rol aktif mi?</summary>
    public bool IsActive { get; private set; } = true;

    public static CompanyRole Create(
        Guid tenantId, Guid companyId, string name, string? description, string permissions)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name, nameof(name));
        return new CompanyRole
        {
            Id = Guid.CreateVersion7(),
            TenantId = tenantId,
            CompanyId = companyId,
            Name = name.Trim(),
            Description = description?.Trim(),
            Permissions = permissions,
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        };
    }

    public void Update(string name, string? description, string permissions)
    {
        Name = name.Trim();
        Description = description?.Trim();
        Permissions = permissions;
    }
}

// ============================================================================
// KULLANICI-ROL ATAMA TABLOLARI (Pivot Tables / Junction Tables)
// 
// Her tablo bir kullanıcının belirli bir seviyedeki rolünü temsil eder.
// Composite key: (UserId + RoleId) veya (UserId + TenantId/CompanyId + RoleId)
// 
// NEDEN AYRI TABLOLAR?
// Tek bir "UserRoles" tablosu yerine seviye bazlı ayrım yapıyoruz çünkü:
// 1. Her seviyenin farklı kısıtlamaları var (TenantId, CompanyId)
// 2. Global Query Filter seviyeye göre farklı çalışır
// 3. Sorgular daha performanslı (gereksiz JOIN'ler yok)
// 4. Kod okunabilirliği daha yüksek
// ============================================================================

/// <summary>
/// Kullanıcı ↔ Sistem Rolü ataması.
/// Sistem rolleri tüm tenant'larda geçerlidir.
/// Sadece SuperAdmin atayabilir.
/// </summary>
public class UserSystemRole : BaseEntity
{
    /// <summary>Kullanıcı ID'si.</summary>
    public Guid UserId { get; set; }

    /// <summary>Sistem rolü ID'si.</summary>
    public Guid SystemRoleId { get; set; }

    /// <summary>Atamayı yapan kullanıcının ID'si.</summary>
    public string AssignedBy { get; set; } = default!;

    /// <summary>Atama zamanı.</summary>
    public DateTime AssignedAt { get; set; } = DateTime.UtcNow;

    // Navigation properties
    public ApplicationUser User { get; set; } = default!;
    public SystemRole SystemRole { get; set; } = default!;
}

/// <summary>
/// Kullanıcı ↔ Tenant Rolü ataması.
/// Bir kullanıcı birden fazla tenant'ta farklı rollerle çalışabilir.
/// TenantAdmin veya üst seviye atayabilir.
/// </summary>
public class UserTenantRole : BaseEntity
{
    /// <summary>Kullanıcı ID'si.</summary>
    public Guid UserId { get; set; }

    /// <summary>Hangi tenant'ta geçerli?</summary>
    public Guid TenantId { get; set; }

    /// <summary>Tenant rolü ID'si.</summary>
    public Guid TenantRoleId { get; set; }

    /// <summary>Atamayı yapan kullanıcının ID'si.</summary>
    public string AssignedBy { get; set; } = default!;

    /// <summary>Atama zamanı.</summary>
    public DateTime AssignedAt { get; set; } = DateTime.UtcNow;

    // Navigation properties
    public ApplicationUser User { get; set; } = default!;
    public TenantRole TenantRole { get; set; } = default!;
}

/// <summary>
/// Kullanıcı ↔ Şirket Rolü ataması.
/// Bir kullanıcı birden fazla şirkette farklı rollerle çalışabilir.
/// CompanyAdmin veya üst seviye atayabilir.
/// </summary>
public class UserCompanyRole : BaseEntity
{
    /// <summary>Kullanıcı ID'si.</summary>
    public Guid UserId { get; set; }

    /// <summary>Hangi şirkette geçerli?</summary>
    public Guid CompanyId { get; set; }

    /// <summary>Şirket rolü ID'si.</summary>
    public Guid CompanyRoleId { get; set; }

    /// <summary>Atamayı yapan kullanıcının ID'si.</summary>
    public string AssignedBy { get; set; } = default!;

    /// <summary>Atama zamanı.</summary>
    public DateTime AssignedAt { get; set; } = DateTime.UtcNow;

    // Navigation properties
    public ApplicationUser User { get; set; } = default!;
    public CompanyRole CompanyRole { get; set; } = default!;
}

/// <summary>
/// Kullanıcı ↔ Şirket Üyeliği.
/// Üyeler, kullanıcılardan farklı olarak sınırlı erişime sahiptir.
/// Bir kullanıcı aynı şirkette hem kullanıcı hem üye olabilir
/// (farklı bağlamlarda farklı yetki — Context Switching).
/// </summary>
public class UserCompanyMembership : BaseEntity
{
    /// <summary>Kullanıcı ID'si.</summary>
    public Guid UserId { get; set; }

    /// <summary>Hangi şirkette üye?</summary>
    public Guid CompanyId { get; set; }

    /// <summary>
    /// Üyelik türü. Gelecekte farklı üyelik tipleri eklenebilir.
    /// Örnek: "External", "Consultant", "Auditor"
    /// </summary>
    public string MembershipType { get; set; } = "Standard";

    /// <summary>Atamayı yapan kullanıcının ID'si.</summary>
    public string AssignedBy { get; set; } = default!;

    /// <summary>Atama zamanı.</summary>
    public DateTime AssignedAt { get; set; } = DateTime.UtcNow;

    /// <summary>Üyelik aktif mi?</summary>
    public bool IsActive { get; set; } = true;

    // Navigation properties
    public ApplicationUser User { get; set; } = default!;
}
