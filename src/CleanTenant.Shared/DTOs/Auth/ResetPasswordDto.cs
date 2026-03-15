

namespace CleanTenant.Shared.DTOs.Auth;

/// <summary>Şifre sıfırlama isteği (e-postadaki link ile).</summary>
public class ResetPasswordDto
{
    public string Email { get; set; } = default!;
    public string Token { get; set; } = default!;
    public string NewPassword { get; set; } = default!;
    public string ConfirmPassword { get; set; } = default!;
}
