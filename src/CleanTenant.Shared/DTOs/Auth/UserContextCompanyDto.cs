

namespace CleanTenant.Shared.DTOs.Auth;

/// <summary>Kullanıcının erişebildiği bir şirket.</summary>
public class UserContextCompanyDto
{
    public Guid CompanyId { get; set; }
    public string CompanyName { get; set; } = default!;
    public string CompanyCode { get; set; } = default!;
    public string RoleName { get; set; } = default!;
    public bool IsCompanyAdmin { get; set; }
    public bool IsMember { get; set; }
}
