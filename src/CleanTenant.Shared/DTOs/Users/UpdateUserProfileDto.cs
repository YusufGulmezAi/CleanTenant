

namespace CleanTenant.Shared.DTOs.Users;

/// <summary>Kullanıcı profil güncelleme.</summary>
public class UpdateUserProfileDto
{
    public string FullName { get; set; } = default!;
    public string? PhoneNumber { get; set; }
    public string? AvatarUrl { get; set; }
    public string PreferredLanguage { get; set; } = "tr";
    public string TimeZone { get; set; } = "Europe/Istanbul";
}
