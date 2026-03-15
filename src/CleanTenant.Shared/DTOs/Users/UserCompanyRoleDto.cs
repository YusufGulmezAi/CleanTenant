

namespace CleanTenant.Shared.DTOs.Users;

/// <summary>Kullanıcının bir şirketteki rolü.</summary>
public class UserCompanyRoleDto
{
    public Guid CompanyId { get; set; }
    public string CompanyName { get; set; } = default!;
    public string RoleName { get; set; } = default!;
    public DateTime AssignedAt { get; set; }
}
