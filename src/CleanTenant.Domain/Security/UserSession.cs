using CleanTenant.Domain.Common;
using CleanTenant.Domain.Enums;
using CleanTenant.Domain.Identity;

namespace CleanTenant.Domain.Security;

/// <summary>
/// Aktif kullanıcı oturumu.
/// 
/// <para><b>TEK OTURUM KURALI:</b></para>
/// Parametrik olarak (appsettings.json) bir kullanıcının aynı anda
/// sadece bir IP ve Browser'dan login olması zorunlu kılınabilir.
/// Yeni oturum açıldığında eski oturum otomatik sonlandırılır.
/// 
/// <para><b>REDIS + DB DUAL STORAGE:</b></para>
/// Oturum bilgileri hem Redis'te (hızlı erişim) hem DB'de (kalıcılık) tutulur.
/// <list type="bullet">
///   <item>Redis: Her API isteğinde hızlı doğrulama (oturum geçerli mi?)</item>
///   <item>DB: Audit trail ve raporlama (kim, ne zaman, nereden login oldu?)</item>
/// </list>
/// </summary>
public class UserSession : BaseEntity
{
    private UserSession() { }

    /// <summary>Oturum sahibi kullanıcı.</summary>
    public Guid UserId { get; private set; }

    /// <summary>
    /// JWT token'ın SHA-256 hash'i.
    /// 
    /// <para><b>NEDEN HASH?</b></para>
    /// Token'ın kendisini veritabanında saklamak güvenlik riski oluşturur.
    /// Veritabanı sızıntısında token'lar çalınabilir. Hash saklayarak:
    /// - Token doğrulama yapılabilir (gelen token'ın hash'i karşılaştırılır)
    /// - Ama hash'ten token üretilemez (tek yönlü)
    /// </para>
    /// </summary>
    public string TokenHash { get; private set; } = default!;

    /// <summary>
    /// Refresh token hash'i.
    /// Access token süresi dolduğunda yeni token almak için kullanılır.
    /// </summary>
    public string RefreshTokenHash { get; private set; } = default!;

    /// <summary>Login yapılan IP adresi.</summary>
    public string IpAddress { get; private set; } = default!;

    /// <summary>Login yapılan tarayıcı bilgisi (User-Agent header).</summary>
    public string UserAgent { get; private set; } = default!;

    /// <summary>
    /// Cihaz parmak izi hash'i.
    /// IP + UserAgent + ek bilgilerden üretilen benzersiz hash.
    /// Her API isteğinde bu hash kontrol edilir.
    /// Farklı bir cihazdan aynı token kullanılamaz.
    /// </summary>
    public string DeviceHash { get; private set; } = default!;

    /// <summary>Oturum oluşturulma zamanı.</summary>
    public DateTime CreatedAt { get; private set; }

    /// <summary>Access token'ın son kullanma zamanı.</summary>
    public DateTime AccessTokenExpiresAt { get; private set; }

    /// <summary>Refresh token'ın son kullanma zamanı.</summary>
    public DateTime RefreshTokenExpiresAt { get; private set; }

    /// <summary>
    /// Oturum iptal edildi mi?
    /// true ise: Token geçerli olsa bile reddedilir.
    /// Force logout veya kullanıcı bloke işlemlerinde true yapılır.
    /// </summary>
    public bool IsRevoked { get; private set; }

    /// <summary>İptal eden kullanıcı (admin force logout durumunda).</summary>
    public string? RevokedBy { get; private set; }

    /// <summary>İptal zamanı.</summary>
    public DateTime? RevokedAt { get; private set; }

    // Navigation
    public ApplicationUser User { get; private set; } = default!;

    // ========================================================================
    // FACTORY & DOMAIN METHODS
    // ========================================================================

    public static UserSession Create(
        Guid userId,
        string tokenHash,
        string refreshTokenHash,
        string ipAddress,
        string userAgent,
        string deviceHash,
        DateTime accessTokenExpiresAt,
        DateTime refreshTokenExpiresAt)
    {
        return new UserSession
        {
            Id = Guid.CreateVersion7(),
            UserId = userId,
            TokenHash = tokenHash,
            RefreshTokenHash = refreshTokenHash,
            IpAddress = ipAddress,
            UserAgent = userAgent,
            DeviceHash = deviceHash,
            CreatedAt = DateTime.UtcNow,
            AccessTokenExpiresAt = accessTokenExpiresAt,
            RefreshTokenExpiresAt = refreshTokenExpiresAt,
            IsRevoked = false
        };
    }

    /// <summary>Oturumu iptal eder (force logout).</summary>
    public void Revoke(string? revokedBy = null)
    {
        IsRevoked = true;
        RevokedBy = revokedBy;
        RevokedAt = DateTime.UtcNow;
    }

    /// <summary>Oturum geçerli mi? (Süre dolmamış ve iptal edilmemiş)</summary>
    public bool IsValid() => !IsRevoked && AccessTokenExpiresAt > DateTime.UtcNow;
}
