namespace CleanTenant.Shared.DTOs.Users;

/// <summary>
/// Kullanıcı listeleme DTO'su.
/// Hassas bilgiler (PasswordHash, AuthenticatorKey) ASLA DTO'da yer almaz.
/// </summary>
public class UserDto
{
    public Guid Id { get; set; }
    public string Email { get; set; } = default!;
    public string FullName { get; set; } = default!;
    public string? PhoneNumber { get; set; }
    public bool IsActive { get; set; }
    public bool EmailConfirmed { get; set; }
    public bool TwoFactorEnabled { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? LastLoginAt { get; set; }
    public string? AvatarUrl { get; set; }
}

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

/// <summary>Kullanıcının bir tenant'taki rolü.</summary>
public class UserTenantRoleDto
{
    public Guid TenantId { get; set; }
    public string TenantName { get; set; } = default!;
    public string RoleName { get; set; } = default!;
    public DateTime AssignedAt { get; set; }
}

/// <summary>Kullanıcının bir şirketteki rolü.</summary>
public class UserCompanyRoleDto
{
    public Guid CompanyId { get; set; }
    public string CompanyName { get; set; } = default!;
    public string RoleName { get; set; } = default!;
    public DateTime AssignedAt { get; set; }
}

/// <summary>Kullanıcının bir şirketteki üyeliği.</summary>
public class UserCompanyMembershipDto
{
    public Guid CompanyId { get; set; }
    public string CompanyName { get; set; } = default!;
    public string MembershipType { get; set; } = default!;
    public bool IsActive { get; set; }
}

/// <summary>
/// Kullanıcı oluşturma isteği.
/// E-posta ile arama yapılır — varsa mevcut kullanıcıya rol eklenir.
/// </summary>
public class CreateUserDto
{
    public string Email { get; set; } = default!;
    public string FullName { get; set; } = default!;
    public string? PhoneNumber { get; set; }
    public string? Password { get; set; }
}

/// <summary>Kullanıcı profil güncelleme.</summary>
public class UpdateUserProfileDto
{
    public string FullName { get; set; } = default!;
    public string? PhoneNumber { get; set; }
    public string? AvatarUrl { get; set; }
    public string PreferredLanguage { get; set; } = "tr";
    public string TimeZone { get; set; } = "Europe/Istanbul";
}
