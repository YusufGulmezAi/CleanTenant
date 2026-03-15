

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
