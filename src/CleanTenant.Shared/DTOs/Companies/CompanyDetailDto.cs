

namespace CleanTenant.Shared.DTOs.Companies;

/// <summary>Şirket detay DTO'su — ayarlar dahil.</summary>
public class CompanyDetailDto : CompanyDto
{
    public string? Settings { get; set; }
    public string? TenantName { get; set; }
    public string? UpdatedBy { get; set; }
    public DateTime? UpdatedAt { get; set; }
}
