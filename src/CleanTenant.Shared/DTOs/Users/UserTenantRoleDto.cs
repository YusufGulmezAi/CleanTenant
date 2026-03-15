

namespace CleanTenant.Shared.DTOs.Users;

/// <summary>Kullanıcının bir tenant'taki rolü.</summary>
public class UserTenantRoleDto
{
    public Guid TenantId { get; set; }
    public string TenantName { get; set; } = default!;
    public string RoleName { get; set; } = default!;
    public DateTime AssignedAt { get; set; }
}
