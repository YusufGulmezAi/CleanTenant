

namespace CleanTenant.Shared.DTOs.Auth;

/// <summary>Şifre değiştirme isteği (login durumunda).</summary>
public class ChangePasswordDto
{
    public string CurrentPassword { get; set; } = default!;
    public string NewPassword { get; set; } = default!;
    public string ConfirmPassword { get; set; } = default!;
}
