

namespace CleanTenant.Shared.DTOs.Auth;

/// <summary>
/// Login yanıtı.
/// 
/// <para><b>İKİ SENARYO:</b></para>
/// <list type="bullet">
///   <item>
///     <b>2FA Kapalı:</b> Gerçek AccessToken + RefreshToken döner.
///     Requires2FA = false, TempToken = null.
///   </item>
///   <item>
///     <b>2FA Açık:</b> Gerçek token VERİLMEZ. TempToken döner.
///     Requires2FA = true, TempToken dolu, AccessToken = null.
///     UI, TempToken ile /verify-2fa endpoint'ine yönlenir.
///   </item>
/// </list>
/// </summary>
public class LoginResponseDto
{
    /// <summary>2FA gerekli mi? true ise AccessToken null olur, TempToken dolu olur.</summary>
    public bool Requires2FA { get; set; }

    /// <summary>
    /// 2FA geçici token'ı — SADECE /verify-2fa endpoint'inde kullanılabilir.
    /// Kısa ömürlü (5dk), Redis'te saklanır, başka hiçbir endpoint'te geçerli değil.
    /// 2FA kapalıysa null.
    /// </summary>
    public string? TempToken { get; set; }

    /// <summary>2FA birincil metodu (2FA aktifse). "SMS", "Email", "Authenticator"</summary>
    public string? TwoFactorMethod { get; set; }

    /// <summary>JWT Access Token — SADECE 2FA kapalıysa veya 2FA başarılıysa dolu.</summary>
    public string? AccessToken { get; set; }

    /// <summary>Refresh Token — AccessToken ile birlikte gelir.</summary>
    public string? RefreshToken { get; set; }

    /// <summary>Access Token süresi.</summary>
    public DateTime? AccessTokenExpiresAt { get; set; }

    /// <summary>Refresh Token süresi.</summary>
    public DateTime? RefreshTokenExpiresAt { get; set; }
}
