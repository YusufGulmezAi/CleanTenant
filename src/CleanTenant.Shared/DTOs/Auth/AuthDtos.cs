namespace CleanTenant.Shared.DTOs.Auth;

/// <summary>Login isteği.</summary>
public class LoginRequestDto
{
    public string Email { get; set; } = default!;
    public string Password { get; set; } = default!;

    /// <summary>
    /// Cihaz parmak izi — UI tarafından oluşturulur.
    /// IP + UserAgent + ekran çözünürlüğü vb. hash'lenir.
    /// </summary>
    public string? DeviceFingerprint { get; set; }
}

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

/// <summary>
/// 2FA fallback isteği — "Kodumu alamıyorum" butonuna basıldığında.
/// TempToken gönderilir, e-posta ile yeni kod gönderilir.
/// </summary>
public class TwoFactorFallbackRequestDto
{
    public string TempToken { get; set; } = default!;
}

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

/// <summary>Şifremi unuttum isteği.</summary>
public class ForgotPasswordDto
{
    public string Email { get; set; } = default!;
}

/// <summary>Şifre sıfırlama isteği (e-postadaki link ile).</summary>
public class ResetPasswordDto
{
    public string Email { get; set; } = default!;
    public string Token { get; set; } = default!;
    public string NewPassword { get; set; } = default!;
    public string ConfirmPassword { get; set; } = default!;
}

/// <summary>Şifre değiştirme isteği (login durumunda).</summary>
public class ChangePasswordDto
{
    public string CurrentPassword { get; set; } = default!;
    public string NewPassword { get; set; } = default!;
    public string ConfirmPassword { get; set; } = default!;
}

// ============================================================================
// 2FA Yönetim DTO'ları
// ============================================================================

/// <summary>E-posta ile 2FA aktifleştirme isteği.</summary>
public class Enable2FAEmailDto
{
    public string CurrentPassword { get; set; } = default!;
}

/// <summary>Authenticator 2FA kurulum yanıtı — QR kod bilgileri.</summary>
public class SetupAuthenticatorResponseDto
{
    /// <summary>Base32 encoded secret key — elle girmek için.</summary>
    public string SecretKey { get; set; } = default!;

    /// <summary>otpauth:// URI — QR kod oluşturmak için.</summary>
    public string QrCodeUri { get; set; } = default!;
	/// <summary>Tarayıcıda açılabilir QR kod resmi URL'si.</summary>
	public string QrCodeImageUrl { get; set; } = default!;
}

/// <summary>Authenticator 2FA doğrulama isteği (kurulum tamamlama).</summary>
public class VerifyAuthenticatorDto
{
    /// <summary>Setup'tan alınan secret key.</summary>
    public string SecretKey { get; set; } = default!;

    /// <summary>Authenticator uygulamasından okunan 6 haneli kod.</summary>
    public string Code { get; set; } = default!;
}

/// <summary>2FA devre dışı bırakma isteği.</summary>
public class Disable2FADto
{
    public string CurrentPassword { get; set; } = default!;
}

/// <summary>E-posta doğrulama kodu onaylama isteği.</summary>
public class ConfirmEmailDto
{
	public string Email { get; set; } = default!;
	public string Code { get; set; } = default!;
}

/// <summary>2FA durum bilgisi.</summary>
public class TwoFactorStatusDto
{
    public bool IsEnabled { get; set; }
    public string Method { get; set; } = "None";
    public bool HasAuthenticator { get; set; }
    public bool EmailConfirmed { get; set; }
    public bool PhoneConfirmed { get; set; }
}

/// <summary>
/// Login sonrası kullanıcının mevcut bağlamları.
/// Kullanıcı hangi tenant/şirketlerde yetkili olduğunu görür
/// ve Context Switching ile aktif bağlamını seçer.
/// </summary>
public class UserContextDto
{
    public Guid UserId { get; set; }
    public string Email { get; set; } = default!;
    public string FullName { get; set; } = default!;
    public bool IsSuperAdmin { get; set; }
    public bool IsSystemUser { get; set; }

    /// <summary>Kullanıcının erişebildiği tenant listesi.</summary>
    public List<UserContextTenantDto> AvailableTenants { get; set; } = [];
}

/// <summary>Kullanıcının erişebildiği bir tenant ve altındaki şirketler.</summary>
public class UserContextTenantDto
{
    public Guid TenantId { get; set; }
    public string TenantName { get; set; } = default!;
    public string RoleName { get; set; } = default!;
    public bool IsTenantAdmin { get; set; }

    /// <summary>Bu tenant altında erişebildiği şirketler.</summary>
    public List<UserContextCompanyDto> AvailableCompanies { get; set; } = [];
}

/// <summary>Kullanıcının erişebildiği bir şirket.</summary>
public class UserContextCompanyDto
{
    public Guid CompanyId { get; set; }
    public string CompanyName { get; set; } = default!;
    public string CompanyCode { get; set; } = default!;
    public string RoleName { get; set; } = default!;
    public bool IsCompanyAdmin { get; set; }
    public bool IsMember { get; set; }
}
