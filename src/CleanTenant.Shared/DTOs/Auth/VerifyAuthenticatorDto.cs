

namespace CleanTenant.Shared.DTOs.Auth;

/// <summary>Authenticator 2FA doğrulama isteği (kurulum tamamlama).</summary>
public class VerifyAuthenticatorDto
{
    /// <summary>Setup'tan alınan secret key.</summary>
    public string SecretKey { get; set; } = default!;

    /// <summary>Authenticator uygulamasından okunan 6 haneli kod.</summary>
    public string Code { get; set; } = default!;
}
