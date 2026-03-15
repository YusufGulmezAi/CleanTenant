using CleanTenant.Domain.Common;

namespace CleanTenant.Domain.Identity;

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
