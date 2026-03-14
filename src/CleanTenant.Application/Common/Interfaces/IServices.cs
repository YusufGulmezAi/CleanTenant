namespace CleanTenant.Application.Common.Interfaces;

/// <summary>
/// Mevcut oturumdaki kullanıcı bilgilerini sağlayan servis.
/// 
/// <para><b>DEPENDENCY INVERSION:</b></para>
/// Bu interface Application katmanında tanımlanır.
/// Infrastructure katmanı HTTP context'ten bilgileri okuyarak implemente eder.
/// Böylece Application katmanı HttpContext'i hiç bilmez.
/// </summary>
public interface ICurrentUserService
{
    /// <summary>Mevcut kullanıcının ID'si. Login değilse null.</summary>
    Guid? UserId { get; }

    /// <summary>Mevcut kullanıcının e-posta adresi.</summary>
    string? Email { get; }

    /// <summary>Mevcut kullanıcının IP adresi.</summary>
    string? IpAddress { get; }

    /// <summary>Mevcut kullanıcının tarayıcı bilgisi.</summary>
    string? UserAgent { get; }

    /// <summary>Aktif tenant ID (Context Switching header'ından).</summary>
    Guid? ActiveTenantId { get; }

    /// <summary>Aktif company ID (Context Switching header'ından).</summary>
    Guid? ActiveCompanyId { get; }

    /// <summary>Kullanıcı kimlik doğrulaması yapılmış mı?</summary>
    bool IsAuthenticated { get; }
}

/// <summary>
/// Redis cache servis sözleşmesi.
/// Tüm cache operasyonları bu interface üzerinden yapılır.
/// </summary>
public interface ICacheService
{
    /// <summary>Cache'ten değer okur. Yoksa null döner.</summary>
    Task<T?> GetAsync<T>(string key, CancellationToken ct = default);

    /// <summary>Cache'e değer yazar. TTL ile süre belirtilir.</summary>
    Task SetAsync<T>(string key, T value, TimeSpan? expiration = null, CancellationToken ct = default);

    /// <summary>Cache'ten değer siler.</summary>
    Task RemoveAsync(string key, CancellationToken ct = default);

    /// <summary>Belirli bir pattern'a uyan tüm key'leri siler.</summary>
    Task RemoveByPrefixAsync(string prefix, CancellationToken ct = default);

    /// <summary>Key var mı kontrol eder.</summary>
    Task<bool> ExistsAsync(string key, CancellationToken ct = default);

    /// <summary>
    /// Redis SET'e eleman ekler (IP blacklist için).
    /// SET tipi: Eleman benzersizliği garantiler, O(1) lookup.
    /// </summary>
    Task SetAddAsync(string key, string value, CancellationToken ct = default);

    /// <summary>Redis SET'ten eleman siler.</summary>
    Task SetRemoveAsync(string key, string value, CancellationToken ct = default);

    /// <summary>Değer SET'te var mı? (O(1) kontrol)</summary>
    Task<bool> SetContainsAsync(string key, string value, CancellationToken ct = default);
}

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

/// <summary>
/// Oturum bilgisi — CreateSessionAsync dönüş tipi.
/// </summary>
public record SessionInfo(
    string AccessToken,
    string RefreshToken,
    DateTime AccessTokenExpiresAt,
    DateTime RefreshTokenExpiresAt
);

/// <summary>
/// Şirket bazlı yedekleme servis sözleşmesi.
/// Background job tarafından çalıştırılır.
/// </summary>
public interface IBackupService
{
    /// <summary>Belirli bir şirketin verilerini yedekler.</summary>
    Task<string> BackupCompanyAsync(Guid companyId, CancellationToken ct = default);

    /// <summary>Yedekten geri yükleme yapar.</summary>
    Task RestoreCompanyAsync(Guid companyId, string backupPath, CancellationToken ct = default);

    /// <summary>Süresi geçmiş yedekleri temizler.</summary>
    Task CleanupOldBackupsAsync(int retentionDays, CancellationToken ct = default);
}

/// <summary>
/// SMS gönderici servis sözleşmesi.
/// Twilio, Vonage veya yerli SMS provider'lar için implementasyon yazılır.
/// 
/// <para><b>NEDEN INTERFACE?</b></para>
/// SMS provider değiştiğinde (Twilio → İleti Merkezi gibi) sadece
/// Infrastructure katmanındaki implementasyon değişir. Application
/// katmanı ve Domain katmanı hiç etkilenmez.
/// </para>
/// </summary>
public interface ISmsProvider
{
    /// <summary>SMS gönderir.</summary>
    /// <param name="phoneNumber">Alıcı telefon numarası (uluslararası format)</param>
    /// <param name="message">SMS içeriği</param>
    Task<bool> SendAsync(string phoneNumber, string message, CancellationToken ct = default);
}

/// <summary>
/// E-posta gönderici servis sözleşmesi.
/// </summary>
public interface IEmailService
{
    /// <summary>E-posta gönderir.</summary>
    Task<bool> SendAsync(string to, string subject, string htmlBody, CancellationToken ct = default);

    /// <summary>2FA doğrulama kodu gönderir.</summary>
    Task<bool> SendTwoFactorCodeAsync(string to, string code, CancellationToken ct = default);

    /// <summary>Şifre sıfırlama linki gönderir.</summary>
    Task<bool> SendPasswordResetAsync(string to, string resetLink, CancellationToken ct = default);
}
