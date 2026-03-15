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
