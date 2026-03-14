using CleanTenant.Domain.Common;

namespace CleanTenant.Domain.Tenancy;

/// <summary>
/// Şirket entity'si — Hiyerarşinin üçüncü ve son katmanı.
/// 
/// <para><b>SENARYO:</b></para>
/// Bir mali müşavirlik firması (Tenant) altında birden fazla müşteri 
/// şirketi bulunur. Her şirketin kendi kullanıcıları, rolleri ve
/// operasyonel verileri vardır.
/// 
/// <para><b>VERİ İZOLASYONU VE YEDEKLEME:</b></para>
/// Tek veritabanı kullanıyoruz (Shared Database), ancak her operasyonel
/// kayıtta CompanyId alanı bulunur. Şirket bazlı yedekleme:
/// <code>
/// -- PostgreSQL filtered backup örneği (Background Job tarafından çalıştırılır)
/// COPY (SELECT * FROM invoices WHERE company_id = '{companyId}') 
/// TO '/backups/{tenant}/{company}/invoices.csv' WITH CSV HEADER;
/// </code>
/// 
/// <para><b>BaseTenantEntity'DEN MİRAS:</b></para>
/// Company, bir tenant'a aittir. TenantId alanı BaseTenantEntity'den gelir.
/// Global Query Filter ile bir tenant, başka bir tenant'ın şirketlerini göremez.
/// </summary>
public class Company : BaseTenantEntity
{
    private Company() { }

    /// <summary>
    /// Şirket adı. Örnek: "ABC Gıda Sanayi A.Ş."
    /// </summary>
    public string Name { get; private set; } = default!;

    /// <summary>
    /// Şirketin kısa kodu veya tanımlayıcısı.
    /// Raporlama ve hızlı referans için kullanılır.
    /// Aynı tenant içinde benzersiz olmalıdır (Unique per Tenant).
    /// Örnek: "ABC-GIDA"
    /// </summary>
    public string Code { get; private set; } = default!;

    /// <summary>
    /// Vergi numarası — yasal zorunluluk.
    /// Türkiye'de 10 haneli vergi numarası veya 11 haneli TC kimlik no.
    /// </summary>
    public string? TaxNumber { get; private set; }

    /// <summary>
    /// Vergi dairesi adı.
    /// </summary>
    public string? TaxOffice { get; private set; }

    /// <summary>
    /// Şirket aktif mi?
    /// false ise: Bu şirkete atanmış kullanıcılar şirket bağlamında çalışamaz.
    /// Ancak diğer şirketlerdeki rolleri etkilenmez.
    /// </summary>
    public bool IsActive { get; private set; } = true;

    /// <summary>
    /// Şirkete özel ayarlar (PostgreSQL JSONB).
    /// 
    /// Örnek JSON:
    /// <code>
    /// {
    ///   "fiscalYearStart": "01-01",
    ///   "currency": "TRY",
    ///   "timezone": "Europe/Istanbul",
    ///   "features": { "invoicing": true, "payroll": true }
    /// }
    /// </code>
    /// </summary>
    public string? Settings { get; private set; }

    /// <summary>
    /// Şirketin iletişim e-posta adresi.
    /// </summary>
    public string? ContactEmail { get; private set; }

    /// <summary>
    /// Şirketin iletişim telefon numarası.
    /// </summary>
    public string? ContactPhone { get; private set; }

    /// <summary>
    /// Şirketin adresi.
    /// </summary>
    public string? Address { get; private set; }

    // ========================================================================
    // Navigation Properties
    // ========================================================================

    /// <summary>
    /// Bu şirketin ait olduğu Tenant.
    /// EF Core navigation property — JOIN işlemlerinde kullanılır.
    /// </summary>
    public Tenant Tenant { get; private set; } = default!;

    // ========================================================================
    // FACTORY METHOD
    // ========================================================================

    /// <summary>
    /// Yeni bir Şirket oluşturur.
    /// TenantId zorunludur — bir şirket mutlaka bir tenant'a ait olmalıdır.
    /// </summary>
    public static Company Create(
        Guid tenantId,
        string name,
        string code,
        string? taxNumber = null,
        string? taxOffice = null,
        string? contactEmail = null,
        string? contactPhone = null,
        string? address = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name, nameof(name));
        ArgumentException.ThrowIfNullOrWhiteSpace(code, nameof(code));

        if (tenantId == Guid.Empty)
            throw new ArgumentException("TenantId boş olamaz.", nameof(tenantId));

        var company = new Company
        {
            Id = Guid.CreateVersion7(),
            TenantId = tenantId,
            Name = name.Trim(),
            Code = code.Trim().ToUpperInvariant(),
            TaxNumber = taxNumber?.Trim(),
            TaxOffice = taxOffice?.Trim(),
            ContactEmail = contactEmail?.Trim(),
            ContactPhone = contactPhone?.Trim(),
            Address = address?.Trim(),
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        };

        company.AddDomainEvent(new CompanyCreatedEvent(company.Id, tenantId, company.Name));

        return company;
    }

    // ========================================================================
    // DOMAIN METHODS
    // ========================================================================

    public void Update(
        string name,
        string? taxNumber,
        string? taxOffice,
        string? contactEmail,
        string? contactPhone,
        string? address)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name, nameof(name));

        Name = name.Trim();
        TaxNumber = taxNumber?.Trim();
        TaxOffice = taxOffice?.Trim();
        ContactEmail = contactEmail?.Trim();
        ContactPhone = contactPhone?.Trim();
        Address = address?.Trim();
    }

    public void SetActiveStatus(bool isActive)
    {
        if (IsActive == isActive) return;

        IsActive = isActive;
        AddDomainEvent(new CompanyStatusChangedEvent(Id, TenantId, isActive));
    }

    public void UpdateSettings(string? settingsJson)
    {
        Settings = settingsJson;
    }
}

// ============================================================================
// DOMAIN EVENTS
// ============================================================================

public record CompanyCreatedEvent(Guid CompanyId, Guid TenantId, string CompanyName) : IDomainEvent;
public record CompanyStatusChangedEvent(Guid CompanyId, Guid TenantId, bool IsActive) : IDomainEvent;
