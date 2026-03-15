

namespace CleanTenant.Shared.DTOs.Users;

/// <summary>
/// Kullanıcı detay DTO'su — roller ve atamalar dahil.
/// </summary>
public class UserDetailDto : UserDto
{
    public bool PhoneNumberConfirmed { get; set; }
    public string PreferredLanguage { get; set; } = "tr";
    public string TimeZone { get; set; } = "Europe/Istanbul";

    /// <summary>Kullanıcının sistem rollerinin adları.</summary>
    public List<string> SystemRoles { get; set; } = [];

    /// <summary>Kullanıcının tenant bazlı rollerinin özeti.</summary>
    public List<UserTenantRoleDto> TenantRoles { get; set; } = [];

    /// <summary>Kullanıcının şirket bazlı rollerinin özeti.</summary>
    public List<UserCompanyRoleDto> CompanyRoles { get; set; } = [];

    /// <summary>Kullanıcının şirket üyelikleri.</summary>
    public List<UserCompanyMembershipDto> CompanyMemberships { get; set; } = [];
}
