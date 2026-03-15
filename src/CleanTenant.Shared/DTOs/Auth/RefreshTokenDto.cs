

namespace CleanTenant.Shared.DTOs.Auth;

/// <summary>
/// Token yenileme isteği.
/// 
/// <para><b>REFRESH TOKEN ROTATION:</b></para>
/// Her refresh işleminde eski RefreshToken revoke edilir, yeni üretilir.
/// Bu, çalınmış refresh token'ın tekrar kullanılmasını engeller.
/// 
/// <para><b>DUAL STORAGE:</b></para>
/// RefreshToken hem Redis'te hem DB'de tutulur.
/// Redis silinirse → kullanıcı otomatik logout olur.
/// DB kaydı audit trail için kalır.
/// </summary>
public class RefreshTokenDto
{
    /// <summary>Süresi dolmuş (veya dolmak üzere olan) Access Token.</summary>
    public string AccessToken { get; set; } = default!;

    /// <summary>Geçerli Refresh Token.</summary>
    public string RefreshToken { get; set; } = default!;
}
