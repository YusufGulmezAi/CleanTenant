

namespace CleanTenant.Shared.DTOs.Auth;

/// <summary>
/// Login sonrası kullanıcının mevcut bağlamları.
/// Kullanıcı hangi tenant/şirketlerde yetkili olduğunu görür
/// ve Context Switching ile aktif bağlamını seçer.
/// </summary>
public class UserContextDto
{
    public Guid UserId { get; set; }
    public string Email { get; set; } = default!;
    public string FullName { get; set; } = default!;
    public bool IsSuperAdmin { get; set; }
    public bool IsSystemUser { get; set; }

    /// <summary>Kullanıcının erişebildiği tenant listesi.</summary>
    public List<UserContextTenantDto> AvailableTenants { get; set; } = [];
}
