

namespace CleanTenant.Shared.DTOs.Tenants;

/// <summary>
/// Tenant oluşturma isteği DTO'su.
/// API endpoint'ine gelen JSON body bu sınıfa deserialize edilir.
/// FluentValidation ile doğrulanır.
/// </summary>
public class CreateTenantDto
{
    public string Name { get; set; } = default!;
    public string Identifier { get; set; } = default!;
    public string? TaxNumber { get; set; }
    public string? ContactEmail { get; set; }
    public string? ContactPhone { get; set; }
}
