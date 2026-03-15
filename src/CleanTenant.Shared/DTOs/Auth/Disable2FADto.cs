

namespace CleanTenant.Shared.DTOs.Auth;

/// <summary>2FA devre dışı bırakma isteği.</summary>
public class Disable2FADto
{
    public string CurrentPassword { get; set; } = default!;
}
