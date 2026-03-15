

namespace CleanTenant.Shared.DTOs.Users;

/// <summary>Kullanıcının bir şirketteki üyeliği.</summary>
public class UserCompanyMembershipDto
{
    public Guid CompanyId { get; set; }
    public string CompanyName { get; set; } = default!;
    public string MembershipType { get; set; } = default!;
    public bool IsActive { get; set; }
}
