

namespace CleanTenant.Shared.DTOs.Auth;

/// <summary>
/// 2FA doğrulama isteği.
/// TempToken + Doğrulama Kodu gönderilir.
/// Başarılı olursa gerçek AccessToken + RefreshToken döner.
/// </summary>
public class TwoFactorVerifyDto
{
    /// <summary>Login'den alınan geçici token.</summary>
    public string TempToken { get; set; } = default!;

    /// <summary>2FA doğrulama kodu (6 haneli).</summary>
    public string Code { get; set; } = default!;

    /// <summary>true ise e-posta fallback ile doğrulama yapılıyor.</summary>
    public bool IsFallback { get; set; }
}
