

namespace CleanTenant.Shared.DTOs.Tenants;

/// <summary>
/// Tenant listeleme ve detay görüntüleme için DTO.
/// Entity'den farklı olarak sadece UI'ın ihtiyacı olan alanları taşır.
/// Hassas bilgiler (Settings JSON vb.) burada yer almaz.
/// </summary>
public class TenantDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = default!;
    public string Identifier { get; set; } = default!;
    public string? TaxNumber { get; set; }
    public bool IsActive { get; set; }
    public string? ContactEmail { get; set; }
    public string? ContactPhone { get; set; }
    public DateTime CreatedAt { get; set; }
    public string? CreatedBy { get; set; }

    /// <summary>Bu tenant'a ait şirket sayısı (listeleme için).</summary>
    public int CompanyCount { get; set; }
}
