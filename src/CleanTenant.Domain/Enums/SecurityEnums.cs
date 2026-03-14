namespace CleanTenant.Domain.Enums;

/// <summary>
/// Kullanıcı bloklama türü.
/// Yetkili bir kullanıcı, başka bir kullanıcıyı farklı şekillerde engelleyebilir.
/// </summary>
public enum BlockType
{
    /// <summary>
    /// Zorunlu çıkış — Kullanıcının mevcut token'ı geçersiz kılınır.
    /// Kullanıcı tekrar login olabilir, ama mevcut oturumu sonlanır.
    /// Kullanım: Yetki değişikliği sonrası güncel rollerin yüklenmesi için.
    /// </summary>
    ForceLogout = 1,

    /// <summary>
    /// Geçici bloke — Belirli bir süre boyunca login yapılamaz.
    /// ExpiresAt alanı ile süre belirlenir.
    /// Kullanım: Şüpheli aktivite tespit edildiğinde.
    /// </summary>
    Temporary = 2,

    /// <summary>
    /// Kalıcı bloke — Süresiz olarak login yapılamaz.
    /// Sadece üst seviye yönetici kaldırabilir.
    /// Kullanım: Kötüye kullanım veya güvenlik ihlali durumunda.
    /// </summary>
    Permanent = 3
}

/// <summary>
/// İki faktörlü doğrulama (2FA) metod türü.
/// 
/// <para><b>FALLBACK MEKANİZMASI:</b></para>
/// Kullanıcı birincil metoda (SMS veya Authenticator) erişemediğinde
/// her zaman e-posta ile fallback yapılabilir. Bu yüzden e-posta
/// doğrulaması her kullanıcı için ZORUNLUDUR.
/// </summary>
public enum TwoFactorMethod
{
    /// <summary>
    /// 2FA kapalı — sadece şifre ile giriş.
    /// </summary>
    None = 0,

    /// <summary>
    /// E-posta ile doğrulama kodu.
    /// Temel 2FA metodu ve diğer metodların fallback'i.
    /// Avantaj: Ek cihaz veya uygulama gerektirmez.
    /// Dezavantaj: E-posta hesabı ele geçirilmişse güvenli değildir.
    /// </summary>
    Email = 1,

    /// <summary>
    /// SMS ile doğrulama kodu.
    /// ISmsProvider interface'i üzerinden (Twilio vb.) gönderilir.
    /// Avantaj: Telefonun fiziksel olarak elde olması gerekir.
    /// Dezavantaj: SIM swap saldırılarına açıktır.
    /// </summary>
    Sms = 2,

    /// <summary>
    /// TOTP (Time-based One-Time Password) — Google Authenticator, Authy vb.
    /// QR kod ile kurulur, 30 saniyelik değişen kodlar üretir.
    /// Avantaj: En güvenli yöntem — internet bağlantısı gerektirmez.
    /// Dezavantaj: Telefon kaybedilirse kurtarma kodu gerekir.
    /// </summary>
    Authenticator = 3
}

/// <summary>
/// Güvenlik log olay türleri.
/// SecurityLogs tablosunda hangi güvenlik olayının gerçekleştiğini belirtir.
/// </summary>
public enum SecurityEventType
{
    // ========================================================================
    // Oturum olayları
    // ========================================================================
    LoginSuccess = 100,
    LoginFailed = 101,
    LoginBlocked = 102,
    Logout = 103,
    ForceLogout = 104,
    SessionExpired = 105,

    // ========================================================================
    // 2FA olayları
    // ========================================================================
    TwoFactorEnabled = 200,
    TwoFactorDisabled = 201,
    TwoFactorSuccess = 202,
    TwoFactorFailed = 203,
    TwoFactorFallbackUsed = 204,

    // ========================================================================
    // Şifre olayları
    // ========================================================================
    PasswordChanged = 300,
    PasswordResetRequested = 301,
    PasswordResetCompleted = 302,
    PasswordResetFailed = 303,

    // ========================================================================
    // Kullanıcı yönetim olayları
    // ========================================================================
    UserBlocked = 400,
    UserUnblocked = 401,
    UserCreated = 402,
    UserUpdated = 403,
    RoleAssigned = 404,
    RoleRevoked = 405,

    // ========================================================================
    // Erişim politikası olayları
    // ========================================================================
    IpBlacklisted = 500,
    IpWhitelisted = 501,
    AccessPolicyViolation = 502,
    RateLimitExceeded = 503,
    DeviceFingerprintMismatch = 504
}
