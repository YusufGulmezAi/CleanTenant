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

/// <summary>
/// Kullanıcı erişim politikası.
/// IP whitelist ve zaman kısıtlamaları burada tanımlanır.
/// Her kullanıcı için ayrı ayrı konfigüre edilebilir.
/// 
/// <para><b>PARAMETRİK YAPILANDIRMA:</b></para>
/// Bu özellikler appsettings.json'dan global olarak aktif/pasif edilir.
/// Aktif edildiğinde kullanıcı bazında detay tanımlanır.
/// </summary>
public class UserAccessPolicy : BaseAuditableEntity
{
    private UserAccessPolicy() { }

    /// <summary>Politika sahibi kullanıcı.</summary>
    public Guid UserId { get; private set; }

    /// <summary>Politika aktif mi?</summary>
    public bool IsEnabled { get; private set; } = true;

    /// <summary>
    /// İzin verilen IP adresleri veya aralıkları (JSONB).
    /// Boş ise tüm IP'lerden erişim serbest.
    /// 
    /// Örnek JSON:
    /// <code>
    /// ["192.168.1.0/24", "10.0.0.1", "203.0.113.50"]
    /// </code>
    /// 
    /// CIDR notasyonu desteklenir (192.168.1.0/24 = 192.168.1.0 - 192.168.1.255)
    /// </summary>
    public string AllowedIpRanges { get; private set; } = "[]";

    /// <summary>
    /// İzin verilen günler (JSONB).
    /// Haftanın hangi günlerinde login olunabilir?
    /// Boş ise her gün izinli.
    /// 
    /// Örnek JSON:
    /// <code>
    /// [1, 2, 3, 4, 5]  // Pazartesi-Cuma (DayOfWeek: 1=Monday)
    /// </code>
    /// </summary>
    public string AllowedDays { get; private set; } = "[]";

    /// <summary>
    /// İzin verilen saat başlangıcı.
    /// Örnek: 08:30 → saat 08:30'dan önce login yapılamaz.
    /// null ise saat kısıtlaması yok.
    /// </summary>
    public TimeOnly? AllowedTimeStart { get; private set; }

    /// <summary>
    /// İzin verilen saat bitişi.
    /// Örnek: 18:00 → saat 18:00'den sonra login yapılamaz.
    /// null ise saat kısıtlaması yok.
    /// </summary>
    public TimeOnly? AllowedTimeEnd { get; private set; }

    // Navigation
    public ApplicationUser User { get; private set; } = default!;

    public static UserAccessPolicy Create(Guid userId)
    {
        return new UserAccessPolicy
        {
            Id = Guid.CreateVersion7(),
            UserId = userId,
            IsEnabled = true,
            CreatedAt = DateTime.UtcNow
        };
    }

    public void UpdateIpRanges(string allowedIpRangesJson)
    {
        AllowedIpRanges = allowedIpRangesJson;
    }

    public void UpdateTimeRestrictions(string allowedDaysJson, TimeOnly? start, TimeOnly? end)
    {
        AllowedDays = allowedDaysJson;
        AllowedTimeStart = start;
        AllowedTimeEnd = end;
    }
}

/// <summary>
/// Kullanıcı bloklama kaydı.
/// Yetkili bir admin tarafından geçici veya kalıcı olarak bloklanan kullanıcılar.
/// </summary>
public class UserBlock : BaseEntity
{
    private UserBlock() { }

    /// <summary>Bloklanan kullanıcı.</summary>
    public Guid UserId { get; private set; }

    /// <summary>Bloklama türü.</summary>
    public BlockType BlockType { get; private set; }

    /// <summary>Bloklayan admin.</summary>
    public string BlockedBy { get; private set; } = default!;

    /// <summary>Bloklama zamanı.</summary>
    public DateTime BlockedAt { get; private set; }

    /// <summary>Bloklama nedeni.</summary>
    public string? Reason { get; private set; }

    /// <summary>
    /// Blok bitiş zamanı (sadece Temporary bloklar için).
    /// null ise: Permanent blok veya ForceLogout.
    /// </summary>
    public DateTime? ExpiresAt { get; private set; }

    /// <summary>Blok kaldırıldı mı?</summary>
    public bool IsLifted { get; private set; }

    /// <summary>Bloğu kaldıran kullanıcı.</summary>
    public string? LiftedBy { get; private set; }

    /// <summary>Blok kaldırma zamanı.</summary>
    public DateTime? LiftedAt { get; private set; }

    // Navigation
    public ApplicationUser User { get; private set; } = default!;

    public static UserBlock Create(Guid userId, BlockType blockType, string blockedBy, string? reason, DateTime? expiresAt = null)
    {
        return new UserBlock
        {
            Id = Guid.CreateVersion7(),
            UserId = userId,
            BlockType = blockType,
            BlockedBy = blockedBy,
            BlockedAt = DateTime.UtcNow,
            Reason = reason,
            ExpiresAt = expiresAt,
            IsLifted = false
        };
    }

    /// <summary>Bloğu kaldırır.</summary>
    public void Lift(string liftedBy)
    {
        IsLifted = true;
        LiftedBy = liftedBy;
        LiftedAt = DateTime.UtcNow;
    }

    /// <summary>Blok aktif mi? (Süresi dolmamış, kaldırılmamış)</summary>
    public bool IsActive() => !IsLifted && (ExpiresAt == null || ExpiresAt > DateTime.UtcNow);
}

/// <summary>
/// Sistem geneli IP kara listesi.
/// Bu listedeki IP'lerden HİÇBİR kullanıcı login olamaz.
/// Middleware seviyesinde ilk kontrol olarak çalışır (en yüksek öncelik).
/// </summary>
public class IpBlacklist : BaseAuditableEntity
{
    private IpBlacklist() { }

    /// <summary>
    /// Kara listeye alınan IP adresi veya CIDR aralığı.
    /// Örnek: "192.168.1.100" veya "10.0.0.0/8"
    /// </summary>
    public string IpAddressOrRange { get; private set; } = default!;

    /// <summary>Kara listeye alma nedeni.</summary>
    public string? Reason { get; private set; }

    /// <summary>
    /// Kara liste süresi dolma zamanı.
    /// null ise kalıcı kara liste.
    /// </summary>
    public DateTime? ExpiresAt { get; private set; }

    /// <summary>Kara liste aktif mi?</summary>
    public bool IsActive { get; private set; } = true;

    public static IpBlacklist Create(string ipAddressOrRange, string? reason, DateTime? expiresAt = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(ipAddressOrRange, nameof(ipAddressOrRange));
        return new IpBlacklist
        {
            Id = Guid.CreateVersion7(),
            IpAddressOrRange = ipAddressOrRange.Trim(),
            Reason = reason,
            ExpiresAt = expiresAt,
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        };
    }

    public void Deactivate() => IsActive = false;
    public void Activate() => IsActive = true;
}
