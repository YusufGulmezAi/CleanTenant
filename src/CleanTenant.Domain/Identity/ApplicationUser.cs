using CleanTenant.Domain.Common;
using CleanTenant.Domain.Enums;

namespace CleanTenant.Domain.Identity;

/// <summary>
/// Uygulama kullanıcısı — Sistemdeki TEK kullanıcı tablosu.
/// 
/// <para><b>ÇAPRAZ KİMLİK (Cross-Cutting Identity):</b></para>
/// CleanTenant'ın en önemli mimari kararlarından biri: Tek kullanıcı tablosu.
/// Bir kullanıcı (tek e-posta) aynı anda birden fazla rolde olabilir:
/// <list type="bullet">
///   <item>Sistem kullanıcısı olabilir</item>
///   <item>Birden fazla tenant'ta farklı rollerle çalışabilir</item>
///   <item>Birden fazla şirkette kullanıcı veya üye olabilir</item>
/// </list>
/// 
/// <para><b>KULLANICI EKLEME AKIŞI:</b></para>
/// <code>
/// 1. Tenant Admin "kullanıcı ekle" der, e-posta adresini girer
/// 2. Sistem e-postayı arar:
///    → BULURSA: Mevcut kullanıcıya UserTenantRole eklenir
///    → BULAMAZSA: Yeni kullanıcı oluşturulur, davet e-postası gönderilir
/// 3. Kullanıcı login olduğunda tüm rolleri Redis cache'ten yüklenir
/// </code>
/// 
/// <para><b>ASP.NET IDENTITY İLE İLİŞKİ:</b></para>
/// Bu sınıf IdentityUser'dan MİRAS ALMAZ.
/// Neden? Domain katmanının ASP.NET Identity'ye bağımlı olmasını istemiyoruz.
/// Infrastructure katmanında Identity ile eşleme (mapping) yapılır.
/// Domain katmanı saf kalır.
/// </summary>
public class ApplicationUser : BaseAuditableEntity, ISoftDeletable
{
    private ApplicationUser() { }

    // ========================================================================
    // KİMLİK BİLGİLERİ
    // ========================================================================

    /// <summary>
    /// E-posta adresi — sistemdeki benzersiz kimlik.
    /// Tüm tenant ve şirketlerde aynı e-posta ile tek kullanıcı.
    /// Login, 2FA fallback ve şifre sıfırlama bu adrese yapılır.
    /// Unique constraint uygulanır.
    /// </summary>
    public string Email { get; private set; } = default!;

    /// <summary>
    /// E-posta doğrulandı mı?
    /// true olması 2FA'nın temel şartıdır.
    /// Şifre sıfırlama linkleri sadece doğrulanmış e-postalara gönderilir.
    /// </summary>
    public bool EmailConfirmed { get; private set; }

    /// <summary>
    /// Kullanıcının tam adı.
    /// </summary>
    public string FullName { get; private set; } = default!;

    /// <summary>
    /// Cep telefonu numarası — SMS 2FA için.
    /// Uluslararası format: +905551234567
    /// </summary>
    public string? PhoneNumber { get; private set; }

    /// <summary>
    /// Telefon numarası doğrulandı mı?
    /// SMS 2FA aktif edebilmek için true olmalıdır.
    /// </summary>
    public bool PhoneNumberConfirmed { get; private set; }

    // ========================================================================
    // ŞİFRE YÖNETİMİ
    // Şifre hash'leri Infrastructure katmanında (Identity service) yönetilir.
    // Domain katmanı şifre hash'ini doğrudan tutmaz.
    // ========================================================================

    /// <summary>
    /// Şifre hash'i — BCrypt veya PBKDF2 ile hash'lenmiş.
    /// Asla düz metin (plaintext) saklanmaz!
    /// </summary>
    public string PasswordHash { get; set; } = default!;

    /// <summary>
    /// Son şifre değiştirme zamanı.
    /// Şifre politikası: X günden eski şifreler değiştirilmeli (parametrik).
    /// </summary>
    public DateTime? LastPasswordChangedAt { get; private set; }

    // ========================================================================
    // İKİ FAKTÖRLÜ DOĞRULAMA (2FA)
    // ========================================================================

    /// <summary>2FA aktif mi?</summary>
    public bool TwoFactorEnabled { get; private set; }

    /// <summary>Birincil 2FA metodu — login'de varsayılan olarak bu kullanılır.</summary>
    public TwoFactorMethod PrimaryTwoFactorMethod { get; private set; } = TwoFactorMethod.None;

    /// <summary>
    /// Aktif 2FA yöntemleri (JSON array). Birden fazla yöntem aynı anda aktif olabilir.
    /// Örnek: ["Email","Authenticator"] veya ["Email","Sms","Authenticator"]
    /// </summary>
    public string EnabledTwoFactorMethods { get; private set; } = "[]";

    /// <summary>TOTP (Authenticator) gizli anahtarı.</summary>
    public string? AuthenticatorKey { get; private set; }

    // ========================================================================
    // HESAP DURUMU
    // ========================================================================

    /// <summary>
    /// Hesap aktif mi?
    /// false ise: Hiçbir seviyede login olamaz.
    /// Admin tarafından pasif edilen hesaplar.
    /// </summary>
    public bool IsActive { get; private set; } = true;

    /// <summary>
    /// Ardışık başarısız login denemesi sayısı.
    /// Belirli bir sayıyı aştığında hesap geçici olarak kilitlenir.
    /// Başarılı login'de sıfırlanır.
    /// </summary>
    public int AccessFailedCount { get; set; }

    /// <summary>
    /// Hesap kilitleme bitiş zamanı.
    /// Bu zamana kadar login yapılamaz. null ise kilit yok.
    /// </summary>
    public DateTime? LockoutEnd { get; set; }

    /// <summary>
    /// Son başarılı login zamanı.
    /// Kullanıcı izleme ve raporlama için.
    /// </summary>
    public DateTime? LastLoginAt { get; private set; }

    /// <summary>
    /// Son login IP adresi.
    /// </summary>
    public string? LastLoginIp { get; private set; }

    /// <summary>
    /// Profil fotoğrafı URL'i.
    /// </summary>
    public string? AvatarUrl { get; private set; }

    /// <summary>
    /// Kullanıcının tercih ettiği dil (ISO 639-1).
    /// Örnek: "tr", "en"
    /// UI dil seçimi için kullanılır.
    /// </summary>
    public string PreferredLanguage { get; private set; } = "tr";

    /// <summary>
    /// Kullanıcının tercih ettiği zaman dilimi (IANA).
    /// Örnek: "Europe/Istanbul"
    /// Tarih/saat gösterimi için kullanılır.
    /// </summary>
    public string TimeZone { get; private set; } = "Europe/Istanbul";

    // ========================================================================
    // ISoftDeletable
    // ========================================================================

    /// <inheritdoc />
    public bool IsDeleted { get; set; }

    /// <inheritdoc />
    public DateTime? DeletedAt { get; set; }

    /// <inheritdoc />
    public string? DeletedBy { get; set; }

    /// <inheritdoc />
    public string? DeletedFromIp { get; set; }

    // ========================================================================
    // NAVIGATION PROPERTIES
    // Kullanıcının farklı seviyelerdeki rol atamaları.
    // Bir kullanıcının tüm rollerini yüklemek için:
    // Include(u => u.SystemRoles)
    //   .Include(u => u.TenantRoles)
    //   .Include(u => u.CompanyRoles)
    //   .Include(u => u.CompanyMemberships)
    // ========================================================================

    /// <summary>Sistem seviyesi rol atamaları</summary>
    public ICollection<UserSystemRole> SystemRoles { get; private set; } = [];

    /// <summary>Tenant seviyesi rol atamaları</summary>
    public ICollection<UserTenantRole> TenantRoles { get; private set; } = [];

    /// <summary>Şirket seviyesi rol atamaları</summary>
    public ICollection<UserCompanyRole> CompanyRoles { get; private set; } = [];

    /// <summary>Şirket üyelikleri (sınırlı erişim)</summary>
    public ICollection<UserCompanyMembership> CompanyMemberships { get; private set; } = [];

    // ========================================================================
    // FACTORY METHOD
    // ========================================================================

    /// <summary>
    /// Yeni bir kullanıcı oluşturur.
    /// Şifre hash'i Infrastructure katmanında (Identity service) atanır.
    /// </summary>
    public static ApplicationUser Create(string email, string fullName, string? phoneNumber = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(email, nameof(email));
        ArgumentException.ThrowIfNullOrWhiteSpace(fullName, nameof(fullName));

        var user = new ApplicationUser
        {
            Id = Guid.CreateVersion7(),
            Email = email.Trim().ToLowerInvariant(),
            FullName = fullName.Trim(),
            PhoneNumber = phoneNumber?.Trim(),
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        };

        user.AddDomainEvent(new UserCreatedEvent(user.Id, user.Email));
        return user;
    }

    // ========================================================================
    // DOMAIN METHODS
    // ========================================================================

    /// <summary>Profil bilgilerini günceller.</summary>
    public void UpdateProfile(string fullName, string? phoneNumber, string? avatarUrl)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(fullName, nameof(fullName));
        FullName = fullName.Trim();
        PhoneNumber = phoneNumber?.Trim();
        AvatarUrl = avatarUrl?.Trim();
    }

    /// <summary>E-posta doğrulamasını tamamlar.</summary>
    public void ConfirmEmail()
    {
        EmailConfirmed = true;
    }

    /// <summary>Telefon numarası doğrulamasını tamamlar.</summary>
    public void ConfirmPhoneNumber()
    {
        PhoneNumberConfirmed = true;
    }

    /// <summary>Telefon numarasını günceller (yeni numara doğrulanmamış olur).</summary>
    public void UpdatePhoneNumber(string phoneNumber)
    {
        PhoneNumber = phoneNumber?.Trim();
        PhoneNumberConfirmed = false;
    }

    /// <summary>
    /// Belirli bir 2FA yöntemini aktif eder.
    /// Birden fazla yöntem aynı anda aktif olabilir.
    /// İlk aktif edilen yöntem otomatik Primary olur.
    /// </summary>
    public void EnableTwoFactor(TwoFactorMethod method, string? authenticatorKey = null)
    {
        if (!EmailConfirmed)
            throw new InvalidOperationException("2FA aktif edilmeden önce e-posta doğrulanmalıdır.");

        if (method == TwoFactorMethod.Sms && !PhoneNumberConfirmed)
            throw new InvalidOperationException("SMS 2FA için telefon numarası doğrulanmalıdır.");

        if (method == TwoFactorMethod.Authenticator && string.IsNullOrEmpty(authenticatorKey))
            throw new ArgumentException("Authenticator için gizli anahtar zorunludur.");

        if (method == TwoFactorMethod.Authenticator)
            AuthenticatorKey = authenticatorKey;

        // Mevcut listeye ekle (duplicate önle)
        var methods = GetEnabledMethodsList();
        var methodName = method.ToString();
        if (!methods.Contains(methodName))
            methods.Add(methodName);

        EnabledTwoFactorMethods = System.Text.Json.JsonSerializer.Serialize(methods);
        TwoFactorEnabled = true;

        // İlk yöntem veya daha güvenli yöntem Primary olur
        // Öncelik: Authenticator > Sms > Email
        PrimaryTwoFactorMethod = GetHighestPriorityMethod(methods);

        AddDomainEvent(new UserTwoFactorChangedEvent(Id, true, method));
    }

    /// <summary>Belirli bir 2FA yöntemini devre dışı bırakır.</summary>
    public void DisableTwoFactorMethod(TwoFactorMethod method)
    {
        var methods = GetEnabledMethodsList();
        methods.Remove(method.ToString());
        EnabledTwoFactorMethods = System.Text.Json.JsonSerializer.Serialize(methods);

        if (method == TwoFactorMethod.Authenticator)
            AuthenticatorKey = null;

        if (methods.Count == 0)
        {
            TwoFactorEnabled = false;
            PrimaryTwoFactorMethod = TwoFactorMethod.None;
        }
        else
        {
            PrimaryTwoFactorMethod = GetHighestPriorityMethod(methods);
        }

        AddDomainEvent(new UserTwoFactorChangedEvent(Id, TwoFactorEnabled, PrimaryTwoFactorMethod));
    }

    /// <summary>Tüm 2FA yöntemlerini devre dışı bırakır.</summary>
    public void DisableTwoFactor()
    {
        TwoFactorEnabled = false;
        PrimaryTwoFactorMethod = TwoFactorMethod.None;
        EnabledTwoFactorMethods = "[]";
        AuthenticatorKey = null;

        AddDomainEvent(new UserTwoFactorChangedEvent(Id, false, TwoFactorMethod.None));
    }

    /// <summary>Primary 2FA yöntemini değiştirir (aktif yöntemler arasından).</summary>
    public void SetPrimaryTwoFactorMethod(TwoFactorMethod method)
    {
        var methods = GetEnabledMethodsList();
        if (!methods.Contains(method.ToString()))
            throw new InvalidOperationException($"{method} yöntemi aktif değil. Önce aktif ediniz.");

        PrimaryTwoFactorMethod = method;
    }

    /// <summary>Aktif 2FA yöntemlerinin listesini döner.</summary>
    public List<string> GetEnabledMethodsList()
    {
        try
        {
            return System.Text.Json.JsonSerializer.Deserialize<List<string>>(EnabledTwoFactorMethods ?? "[]") ?? [];
        }
        catch { return []; }
    }

    /// <summary>Belirli bir 2FA yöntemi aktif mi?</summary>
    public bool IsTwoFactorMethodEnabled(TwoFactorMethod method)
        => GetEnabledMethodsList().Contains(method.ToString());

    private static TwoFactorMethod GetHighestPriorityMethod(List<string> methods)
    {
        if (methods.Contains("Authenticator")) return TwoFactorMethod.Authenticator;
        if (methods.Contains("Sms")) return TwoFactorMethod.Sms;
        if (methods.Contains("Email")) return TwoFactorMethod.Email;
        return TwoFactorMethod.None;
    }

    /// <summary>Başarılı login bilgilerini kaydeder.</summary>
    public void RecordLogin(string ipAddress)
    {
        LastLoginAt = DateTime.UtcNow;
        LastLoginIp = ipAddress;
        AccessFailedCount = 0;  // Başarılı login → sayacı sıfırla
        LockoutEnd = null;       // Kilit varsa kaldır
    }

    /// <summary>Başarısız login denemesini kaydeder.</summary>
    public void RecordFailedLogin(int maxAttempts, TimeSpan lockoutDuration)
    {
        AccessFailedCount++;

        if (AccessFailedCount >= maxAttempts)
        {
            LockoutEnd = DateTime.UtcNow.Add(lockoutDuration);
            AddDomainEvent(new UserLockedOutEvent(Id, LockoutEnd.Value));
        }
    }

    /// <summary>Hesabı aktif/pasif yapar.</summary>
    public void SetActiveStatus(bool isActive)
    {
        if (IsActive == isActive) return;
        IsActive = isActive;
        AddDomainEvent(new UserStatusChangedEvent(Id, isActive));
    }

    /// <summary>Şifre değişikliğini kaydeder.</summary>
    public void RecordPasswordChange()
    {
        LastPasswordChangedAt = DateTime.UtcNow;
    }

    /// <summary>Tercih ayarlarını günceller.</summary>
    public void UpdatePreferences(string language, string timeZone)
    {
        PreferredLanguage = language;
        TimeZone = timeZone;
    }
}

// ============================================================================
// DOMAIN EVENTS
// ============================================================================

public record UserCreatedEvent(Guid UserId, string Email) : IDomainEvent;
public record UserStatusChangedEvent(Guid UserId, bool IsActive) : IDomainEvent;
public record UserTwoFactorChangedEvent(Guid UserId, bool IsEnabled, TwoFactorMethod Method) : IDomainEvent;
public record UserLockedOutEvent(Guid UserId, DateTime LockoutEnd) : IDomainEvent;
