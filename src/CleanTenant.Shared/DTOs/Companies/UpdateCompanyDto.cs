

namespace CleanTenant.Shared.DTOs.Companies;

/// <summary>Şirket güncelleme isteği. Code değiştirilemez.</summary>
public class UpdateCompanyDto
{
    public string Name { get; set; } = default!;
    public string? TaxNumber { get; set; }
    public string? TaxOffice { get; set; }
    public string? ContactEmail { get; set; }
    public string? ContactPhone { get; set; }
    public string? Address { get; set; }
}
