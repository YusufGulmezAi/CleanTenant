

namespace CleanTenant.Shared.DTOs.Auth;

/// <summary>Kullanıcının erişebildiği bir tenant ve altındaki şirketler.</summary>
public class UserContextTenantDto
{
    public Guid TenantId { get; set; }
    public string TenantName { get; set; } = default!;
    public string RoleName { get; set; } = default!;
    public bool IsTenantAdmin { get; set; }

    /// <summary>Bu tenant altında erişebildiği şirketler.</summary>
    public List<UserContextCompanyDto> AvailableCompanies { get; set; } = [];
}
