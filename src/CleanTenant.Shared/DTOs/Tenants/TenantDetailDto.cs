

namespace CleanTenant.Shared.DTOs.Tenants;

/// <summary>
/// Tenant detay DTO'su — ayarlar dahil, sadece yetkili kullanıcılara döner.
/// </summary>
public class TenantDetailDto : TenantDto
{
    public string? Settings { get; set; }
    public string? UpdatedBy { get; set; }
    public DateTime? UpdatedAt { get; set; }
}
