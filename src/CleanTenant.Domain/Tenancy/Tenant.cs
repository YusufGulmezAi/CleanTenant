using CleanTenant.Domain.Common;

namespace CleanTenant.Domain.Tenancy;

/// <summary>
/// Tenant (Kiracı) entity'si — Hiyerarşinin ikinci katmanı.
/// 
/// <para><b>SENARYO:</b></para>
/// Bir mali müşavirlik firması = 1 Tenant.
/// Bu firma altında birden fazla müşteri şirketi (Company) bulunur.
/// Tenant, kendi kullanıcılarını, rollerini ve izinlerini yönetir.
/// 
/// <para><b>İZOLASYON:</b></para>
/// Her Tenant tamamen izole bir ortamda çalışır:
/// <list type="bullet">
///   <item>Kendi kullanıcı havuzu (ama kullanıcılar çapraz olabilir)</item>
///   <item>Kendi rol ve izin tanımları</item>
///   <item>Kendi şirketleri ve şirket verileri</item>
///   <item>Kendi ayarları (Settings — JSONB ile esnek yapı)</item>
/// </list>
/// 
/// <para><b>FACTORY METHOD PATTERN:</b></para>
/// Entity oluşturma işlemi static factory method ile yapılır.
/// Neden? Constructor yerine factory method kullanmanın avantajları:
/// <list type="bullet">
///   <item>İş kuralları oluşturma anında doğrulanır</item>
///   <item>Domain event otomatik tetiklenir</item>
///   <item>Invalid state (geçersiz durum) oluşması engellenir</item>
///   <item>Nesne oluşturma süreci kapsüllenir</item>
/// </list>
/// </summary>
public class Tenant : BaseAuditableEntity, ISoftDeletable
{
    // ========================================================================
    // Private constructor — doğrudan new Tenant() yapılamaz.
    // Factory method (Create) kullanılmalıdır.
    // Bu, entity'nin her zaman geçerli bir durumda oluşmasını garanti eder.
    // ========================================================================
    private Tenant() { }

    /// <summary>
    /// Tenant adı. Örnek: "ABC Mali Müşavirlik"
    /// </summary>
    public string Name { get; private set; } = default!;

    /// <summary>
    /// Tenant'ın benzersiz alt alan adı veya tanımlayıcısı.
    /// URL routing ve tenant çözümlemede kullanılabilir.
    /// Örnek: "abc-mali" → abc-mali.cleantenant.com
    /// 
    /// <para>
    /// Unique constraint uygulanır — iki tenant aynı identifier'a sahip olamaz.
    /// </para>
    /// </summary>
    public string Identifier { get; private set; } = default!;

    /// <summary>
    /// Vergi numarası — yasal gereklilik.
    /// Tenant seviyesinde tutulur çünkü firmanın kendi vergi numarasıdır.
    /// </summary>
    public string? TaxNumber { get; private set; }

    /// <summary>
    /// Tenant aktif mi?
    /// false ise: Tenant'a ait hiçbir kullanıcı login olamaz.
    /// Bu, abonelik süresi dolan veya askıya alınan tenant'lar için kullanılır.
    /// </summary>
    public bool IsActive { get; private set; } = true;

    /// <summary>
    /// Tenant'a özel ayarlar — PostgreSQL JSONB formatında.
    /// 
    /// <para><b>NEDEN JSONB?</b></para>
    /// Her tenant'ın farklı ayar ihtiyaçları olabilir. Relational model ile
    /// bunu yönetmek çok sayıda nullable kolon veya EAV (Entity-Attribute-Value)
    /// pattern gerektirir. JSONB ile:
    /// <list type="bullet">
    ///   <item>Esnek şema — her tenant farklı ayar alanları olabilir</item>
    ///   <item>PostgreSQL JSONB operatörleri ile sorgulama yapılabilir</item>
    ///   <item>Migration gerekmeden yeni ayar alanları eklenebilir</item>
    /// </list>
    /// 
    /// Örnek JSON:
    /// <code>
    /// {
    ///   "maxCompanies": 50,
    ///   "maxUsersPerCompany": 100,
    ///   "features": ["audit", "backup", "2fa"],
    ///   "branding": { "primaryColor": "#1976D2", "logo": "..." }
    /// }
    /// </code>
    /// </para>
    /// </summary>
    public string? Settings { get; private set; }

    /// <summary>
    /// İletişim e-posta adresi — tenant yönetimi bildirimleri için.
    /// </summary>
    public string? ContactEmail { get; private set; }

    /// <summary>
    /// İletişim telefon numarası.
    /// </summary>
    public string? ContactPhone { get; private set; }

    // ========================================================================
    // Navigation Properties (İlişkisel Bağlantılar)
    // EF Core bu property'leri kullanarak ilişkileri otomatik çözümler.
    // ========================================================================

    /// <summary>
    /// Bu tenant'a ait şirketlerin koleksiyonu.
    /// Lazy loading kullanılmaz — her zaman explicit Include ile yüklenir.
    /// </summary>
    public ICollection<Company> Companies { get; private set; } = [];

    // ========================================================================
    // ISoftDeletable implementasyonu
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
    // FACTORY METHOD — Tenant oluşturma
    // ========================================================================

    /// <summary>
    /// Yeni bir Tenant oluşturur.
    /// 
    /// <para><b>NEDEN STATIC FACTORY METHOD?</b></para>
    /// <list type="number">
    ///   <item>İş kuralları burada doğrulanır (boş isim, geçersiz identifier)</item>
    ///   <item>TenantCreatedEvent domain event'i otomatik tetiklenir</item>
    ///   <item>Guid.CreateVersion7() ile zamana dayalı sıralı ID üretilir</item>
    ///   <item>Entity her zaman geçerli durumda (valid state) oluşur</item>
    /// </list>
    /// </para>
    /// </summary>
    /// <param name="name">Tenant adı (zorunlu)</param>
    /// <param name="identifier">Benzersiz tanımlayıcı (zorunlu, URL-safe)</param>
    /// <param name="taxNumber">Vergi numarası (opsiyonel)</param>
    /// <param name="contactEmail">İletişim e-postası (opsiyonel)</param>
    /// <param name="contactPhone">İletişim telefonu (opsiyonel)</param>
    /// <returns>Yeni oluşturulmuş Tenant entity'si</returns>
    /// <exception cref="ArgumentException">Name veya Identifier boş ise</exception>
    public static Tenant Create(
        string name,
        string identifier,
        string? taxNumber = null,
        string? contactEmail = null,
        string? contactPhone = null)
    {
        // İş kuralı: Tenant adı boş olamaz
        ArgumentException.ThrowIfNullOrWhiteSpace(name, nameof(name));

        // İş kuralı: Identifier boş olamaz
        ArgumentException.ThrowIfNullOrWhiteSpace(identifier, nameof(identifier));

        var tenant = new Tenant
        {
            Id = Guid.CreateVersion7(),  // .NET 9+ : Zamana dayalı sıralı UUID
            Name = name.Trim(),
            Identifier = identifier.Trim().ToLowerInvariant(),
            TaxNumber = taxNumber?.Trim(),
            ContactEmail = contactEmail?.Trim(),
            ContactPhone = contactPhone?.Trim(),
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        };

        // Domain Event: Tenant oluşturulduğunda tetiklenir.
        // Handler'lar varsayılan rolleri, ayarları vb. oluşturabilir.
        tenant.AddDomainEvent(new TenantCreatedEvent(tenant.Id, tenant.Name));

        return tenant;
    }

    // ========================================================================
    // DOMAIN METHODS — İş kurallarını entity içinde kapsülle
    // ========================================================================

    /// <summary>
    /// Tenant bilgilerini günceller.
    /// Güncelleme işlemi de factory method gibi entity üzerinden yapılır.
    /// Böylece iş kuralları her zaman entity tarafından kontrol edilir.
    /// </summary>
    public void Update(string name, string? taxNumber, string? contactEmail, string? contactPhone)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name, nameof(name));

        Name = name.Trim();
        TaxNumber = taxNumber?.Trim();
        ContactEmail = contactEmail?.Trim();
        ContactPhone = contactPhone?.Trim();
    }

    /// <summary>
    /// Tenant'ı aktif veya pasif yapar.
    /// Pasif edilen tenant'ın tüm kullanıcıları login olamaz.
    /// </summary>
    public void SetActiveStatus(bool isActive)
    {
        if (IsActive == isActive) return;  // Gereksiz event tetiklemeyi önle

        IsActive = isActive;
        AddDomainEvent(new TenantStatusChangedEvent(Id, isActive));
    }

    /// <summary>
    /// Tenant ayarlarını günceller (JSONB).
    /// </summary>
    public void UpdateSettings(string? settingsJson)
    {
        Settings = settingsJson;
    }
}
