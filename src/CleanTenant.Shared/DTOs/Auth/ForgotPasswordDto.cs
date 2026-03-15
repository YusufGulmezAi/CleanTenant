

namespace CleanTenant.Shared.DTOs.Auth;

/// <summary>Şifremi unuttum isteği.</summary>
public class ForgotPasswordDto
{
    public string Email { get; set; } = default!;
}
