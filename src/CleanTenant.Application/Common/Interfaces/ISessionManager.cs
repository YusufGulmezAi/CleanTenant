

namespace CleanTenant.Application.Common.Interfaces;

/// <summary>
/// Oturum yönetimi servis sözleşmesi.
/// Login, logout, bloke ve force logout işlemleri.
/// </summary>
public interface ISessionManager
{
    /// <summary>Yeni oturum oluşturur (login sonrası).</summary>
    Task<SessionInfo> CreateSessionAsync(Guid userId, string ipAddress, string userAgent, CancellationToken ct = default);

    /// <summary>Oturum geçerli mi kontrol eder (her istekte).</summary>
    Task<bool> ValidateSessionAsync(Guid userId, string tokenHash, string deviceHash, CancellationToken ct = default);

    /// <summary>Oturumu sonlandırır (logout).</summary>
    Task RevokeSessionAsync(Guid userId, CancellationToken ct = default);

    /// <summary>Kullanıcının tüm oturumlarını sonlandırır (force logout / şifre değişikliği).</summary>
    Task RevokeAllSessionsAsync(Guid userId, string? revokedBy = null, CancellationToken ct = default);

    /// <summary>Kullanıcıyı bloke eder (anlık veya süresiz).</summary>
    Task BlockUserAsync(Guid userId, string blockedBy, string? reason, DateTime? expiresAt = null, CancellationToken ct = default);

    /// <summary>Kullanıcı bloke mi kontrol eder.</summary>
    Task<bool> IsUserBlockedAsync(Guid userId, CancellationToken ct = default);
}
