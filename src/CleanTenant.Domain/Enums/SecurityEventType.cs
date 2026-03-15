

namespace CleanTenant.Domain.Enums;

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
