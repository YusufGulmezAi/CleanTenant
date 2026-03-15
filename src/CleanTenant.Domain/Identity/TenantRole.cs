using CleanTenant.Domain.Common;

namespace CleanTenant.Domain.Identity;

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
