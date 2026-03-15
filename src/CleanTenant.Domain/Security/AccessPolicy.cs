using CleanTenant.Domain.Common;

namespace CleanTenant.Domain.Security;

/// <summary>
/// Erişim Politikası — hiyerarşik IP ve zaman kısıtlama.
/// 
/// <para><b>KURALLAR:</b></para>
/// <list type="bullet">
///   <item>Her seviyede (System/Tenant/Company) 1 DEFAULT politika (silinemez)</item>
///   <item>Default = "Hiçbir IP'den, hiçbir zaman giremez"</item>
///   <item>Kullanıcı oluşturulunca → default politika otomatik atanır</item>
///   <item>Özel politika silinince → default geri atanır</item>
///   <item>Politika YOKSA → GİRİŞ YASAK (açık kapı yok!)</item>
/// </list>
/// </summary>
public class AccessPolicy : BaseEntity
{
    private AccessPolicy() { }

    /// <summary>Politika adı. Örnek: "Ofis Erişim", "VPN Politikası"</summary>
    public string Name { get; private set; } = default!;

    /// <summary>Açıklama.</summary>
    public string? Description { get; private set; }

    /// <summary>Politika seviyesi: System, Tenant, Company.</summary>
    public PolicyLevel Level { get; private set; }

    /// <summary>İlişkili Tenant ID (System seviyesinde null).</summary>
    public Guid? TenantId { get; private set; }

    /// <summary>İlişkili Company ID (System/Tenant seviyesinde null).</summary>
    public Guid? CompanyId { get; private set; }

    /// <summary>Default politika mı? true ise silinemez.</summary>
    public bool IsDefault { get; private set; }

    /// <summary>Aktif mi?</summary>
    public bool IsActive { get; private set; } = true;

    // ── IP Kuralları ────────────────────────────────────────────────────

    /// <summary>
    /// true ise TÜM IP'lerden erişim reddedilir (AllowedIpRanges yok sayılır).
    /// Default politikada true olur.
    /// </summary>
    public bool DenyAllIps { get; private set; } = true;

    /// <summary>
    /// İzinli IP adresleri/aralıkları (JSON array). CIDR destekli.
    /// DenyAllIps = false ise bu liste kontrol edilir.
    /// Örnek: ["192.168.1.0/24", "10.0.0.1", "0.0.0.0/0"]
    /// "0.0.0.0/0" = tüm IPv4 adresleri
    /// </summary>
    public string AllowedIpRanges { get; private set; } = "[]";

    // ── Zaman Kuralları ─────────────────────────────────────────────────

    /// <summary>
    /// true ise TÜM zaman dilimlerinde erişim reddedilir.
    /// Default politikada true olur.
    /// </summary>
    public bool DenyAllTimes { get; private set; } = true;

    /// <summary>
    /// İzinli günler (JSON array). Pazartesi=1, Pazar=7.
    /// DenyAllTimes = false ise kontrol edilir.
    /// Örnek: [1,2,3,4,5] = Hafta içi
    /// </summary>
    public string AllowedDays { get; private set; } = "[]";

    /// <summary>İzinli saat başlangıcı (UTC).</summary>
    public TimeOnly? AllowedTimeStart { get; private set; }

    /// <summary>İzinli saat bitişi (UTC).</summary>
    public TimeOnly? AllowedTimeEnd { get; private set; }

    // ── Audit ───────────────────────────────────────────────────────────

    public DateTime CreatedAt { get; private set; } = DateTime.UtcNow;
    public string? CreatedBy { get; private set; }
    public DateTime? UpdatedAt { get; private set; }
    public string? UpdatedBy { get; private set; }

    // ── Navigation ──────────────────────────────────────────────────────

    public ICollection<UserPolicyAssignment> Assignments { get; private set; } = [];

    // ====================================================================
    // FACTORY METHODS
    // ====================================================================

    /// <summary>Default politika oluşturur (silinemez, her şeyi reddeder).</summary>
    public static AccessPolicy CreateDefault(PolicyLevel level, Guid? tenantId = null, Guid? companyId = null, string? createdBy = null)
    {
        var levelName = level switch
        {
            PolicyLevel.System => "Sistem",
            PolicyLevel.Tenant => "Tenant",
            PolicyLevel.Company => "Şirket",
            _ => "Bilinmeyen"
        };

        return new AccessPolicy
        {
            Id = Guid.CreateVersion7(),
            Name = $"Varsayılan {levelName} Politikası",
            Description = $"Otomatik oluşturulan {levelName.ToLower()} varsayılan politikası. Tüm erişim reddedilir. Silinemez.",
            Level = level,
            TenantId = tenantId,
            CompanyId = companyId,
            IsDefault = true,
            IsActive = true,
            DenyAllIps = true,
            DenyAllTimes = true,
            AllowedIpRanges = "[]",
            AllowedDays = "[]",
            AllowedTimeStart = null,
            AllowedTimeEnd = null,
            CreatedAt = DateTime.UtcNow,
            CreatedBy = createdBy ?? "SYSTEM"
        };
    }

    /// <summary>Özel politika oluşturur (düzenlenebilir, silinebilir).</summary>
    public static AccessPolicy CreateCustom(
        string name, PolicyLevel level,
        Guid? tenantId = null, Guid? companyId = null,
        string? description = null, string? createdBy = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name, nameof(name));

        return new AccessPolicy
        {
            Id = Guid.CreateVersion7(),
            Name = name.Trim(),
            Description = description,
            Level = level,
            TenantId = tenantId,
            CompanyId = companyId,
            IsDefault = false,
            IsActive = true,
            DenyAllIps = false,
            DenyAllTimes = false,
            AllowedIpRanges = "[]",
            AllowedDays = "[]",
            CreatedAt = DateTime.UtcNow,
            CreatedBy = createdBy
        };
    }

    /// <summary>Tam erişim politikası (SuperAdmin seed için).</summary>
    public static AccessPolicy CreateFullAccess(PolicyLevel level, Guid? tenantId = null, Guid? companyId = null, string? createdBy = null)
    {
        return new AccessPolicy
        {
            Id = Guid.CreateVersion7(),
            Name = "Tam Erişim Politikası",
            Description = "Tüm IP'lerden, tüm zaman dilimlerinde erişim izni verir.",
            Level = level,
            TenantId = tenantId,
            CompanyId = companyId,
            IsDefault = false,
            IsActive = true,
            DenyAllIps = false,
            DenyAllTimes = false,
            AllowedIpRanges = "[\"0.0.0.0/0\"]",
            AllowedDays = "[1,2,3,4,5,6,7]",
            AllowedTimeStart = new TimeOnly(0, 0),
            AllowedTimeEnd = new TimeOnly(23, 59),
            CreatedAt = DateTime.UtcNow,
            CreatedBy = createdBy ?? "SYSTEM"
        };
    }

    // ====================================================================
    // UPDATE METHODS
    // ====================================================================

    public void UpdateIpRules(bool denyAll, string allowedIpRangesJson, string updatedBy)
    {
        if (IsDefault && !denyAll)
            throw new InvalidOperationException("Default politikanın IP kuralları gevşetilemez. Özel politika oluşturunuz.");

        DenyAllIps = denyAll;
        AllowedIpRanges = allowedIpRangesJson;
        UpdatedAt = DateTime.UtcNow;
        UpdatedBy = updatedBy;
    }

    public void UpdateTimeRules(bool denyAll, string allowedDaysJson, TimeOnly? start, TimeOnly? end, string updatedBy)
    {
        if (IsDefault && !denyAll)
            throw new InvalidOperationException("Default politikanın zaman kuralları gevşetilemez. Özel politika oluşturunuz.");

        DenyAllTimes = denyAll;
        AllowedDays = allowedDaysJson;
        AllowedTimeStart = start;
        AllowedTimeEnd = end;
        UpdatedAt = DateTime.UtcNow;
        UpdatedBy = updatedBy;
    }

    public void UpdateInfo(string name, string? description, string updatedBy)
    {
        if (IsDefault)
            throw new InvalidOperationException("Default politikanın adı ve açıklaması değiştirilemez.");

        Name = name.Trim();
        Description = description;
        UpdatedAt = DateTime.UtcNow;
        UpdatedBy = updatedBy;
    }

    public void SetActive(bool isActive, string updatedBy)
    {
        if (IsDefault && !isActive)
            throw new InvalidOperationException("Default politika devre dışı bırakılamaz.");

        IsActive = isActive;
        UpdatedAt = DateTime.UtcNow;
        UpdatedBy = updatedBy;
    }
}

/// <summary>Kullanıcı ↔ Politika ataması.</summary>
public class UserPolicyAssignment : BaseEntity
{
    public Guid UserId { get; set; }
    public Guid AccessPolicyId { get; set; }
    public string AssignedBy { get; set; } = default!;
    public DateTime AssignedAt { get; set; } = DateTime.UtcNow;

    // Navigation
    public AccessPolicy AccessPolicy { get; set; } = default!;
    public CleanTenant.Domain.Identity.ApplicationUser User { get; set; } = default!;
}

/// <summary>Politika seviyesi.</summary>
public enum PolicyLevel
{
    System = 0,
    Tenant = 1,
    Company = 2
}
