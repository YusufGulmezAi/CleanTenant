

namespace CleanTenant.Shared.DTOs.Auth;

/// <summary>
/// 2FA fallback isteği — "Kodumu alamıyorum" butonuna basıldığında.
/// TempToken gönderilir, e-posta ile yeni kod gönderilir.
/// </summary>
public class TwoFactorFallbackRequestDto
{
    public string TempToken { get; set; } = default!;
}
