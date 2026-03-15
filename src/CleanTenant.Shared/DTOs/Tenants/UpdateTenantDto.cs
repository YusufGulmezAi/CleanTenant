

namespace CleanTenant.Shared.DTOs.Tenants;

/// <summary>
/// Tenant güncelleme isteği DTO'su.
/// Identifier değiştirilemez — sadece Name ve iletişim bilgileri güncellenebilir.
/// </summary>
public class UpdateTenantDto
{
    public string Name { get; set; } = default!;
    public string? TaxNumber { get; set; }
    public string? ContactEmail { get; set; }
    public string? ContactPhone { get; set; }
}
