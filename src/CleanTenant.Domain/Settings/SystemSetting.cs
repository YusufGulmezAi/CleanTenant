using CleanTenant.Domain.Common;

namespace CleanTenant.Domain.Settings;

/// <summary>
/// Sistem ayarı — hiyerarşik key-value yapıda DB'de saklanır.
/// 
/// <para><b>ÖNCELİK SIRASI (en yüksekten düşüğe):</b></para>
/// <code>
/// [1] Company ayarı  → CompanyAdmin belirledi (CompanyId dolu)
/// [2] Tenant ayarı   → TenantAdmin belirledi (TenantId dolu, CompanyId null)
/// [3] System ayarı   → SuperAdmin belirledi (TenantId null, CompanyId null)
/// [4] appsettings.json → Kod içi fallback
/// </code>
/// 
/// <para><b>KEY FORMAT:</b></para>
/// Nokta ile ayrılmış, appsettings.json ile uyumlu.
/// <code>
/// "Jwt.AccessTokenExpirationMinutes"  → 15
/// "Jwt.RefreshTokenExpirationDays"    → 7
/// "Session.EnforceSingleSession"      → "true"
/// "PasswordPolicy.MinimumLength"      → "8"
/// "Email.Enabled"                     → "true"
/// "AccessPolicy.EnableRateLimit"      → "true"
/// </code>
/// </summary>
public class SystemSetting : BaseEntity
{
    private SystemSetting() { }

    /// <summary>Ayar anahtarı. Örnek: "Jwt.AccessTokenExpirationMinutes"</summary>
    public string Key { get; private set; } = default!;

    /// <summary>Ayar değeri (string — tüm tipler string olarak saklanır).</summary>
    public string Value { get; private set; } = default!;

    /// <summary>Ayar açıklaması (UI'da görünür).</summary>
    public string? Description { get; private set; }

    /// <summary>Değer tipi: String, Int, Bool, Decimal, Json.</summary>
    public SettingValueType ValueType { get; private set; } = SettingValueType.String;

    /// <summary>Kategori grubu (UI'da gruplama için). Örnek: "Güvenlik", "Oturum", "E-posta"</summary>
    public string Category { get; private set; } = default!;

    /// <summary>Ayar seviyesi: System, Tenant, Company.</summary>
    public SettingLevel Level { get; private set; }

    /// <summary>İlişkili Tenant (Tenant/Company seviyesinde).</summary>
    public Guid? TenantId { get; private set; }

    /// <summary>İlişkili Company (Company seviyesinde).</summary>
    public Guid? CompanyId { get; private set; }

    /// <summary>Salt okunur mı? (Kod ile oluşturulan, UI'dan değiştirilemeyen ayarlar)</summary>
    public bool IsReadOnly { get; private set; }

    /// <summary>Gizli mi? (Şifre gibi değerler — UI'da maskelenir)</summary>
    public bool IsSecret { get; private set; }

    /// <summary>Son güncelleyen.</summary>
    public string? UpdatedBy { get; private set; }
    public DateTime? UpdatedAt { get; private set; }
    public DateTime CreatedAt { get; private set; } = DateTime.UtcNow;

    // ====================================================================
    // FACTORY METHODS
    // ====================================================================

    public static SystemSetting Create(
        string key, string value, string category,
        SettingValueType valueType = SettingValueType.String,
        SettingLevel level = SettingLevel.System,
        Guid? tenantId = null, Guid? companyId = null,
        string? description = null, bool isReadOnly = false, bool isSecret = false)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key, nameof(key));

        return new SystemSetting
        {
            Id = Guid.CreateVersion7(),
            Key = key.Trim(),
            Value = value,
            Category = category,
            ValueType = valueType,
            Level = level,
            TenantId = tenantId,
            CompanyId = companyId,
            Description = description,
            IsReadOnly = isReadOnly,
            IsSecret = isSecret,
            CreatedAt = DateTime.UtcNow
        };
    }

    public void UpdateValue(string newValue, string updatedBy)
    {
        if (IsReadOnly)
            throw new InvalidOperationException($"'{Key}' ayarı salt okunurdur, değiştirilemez.");

        Value = newValue;
        UpdatedBy = updatedBy;
        UpdatedAt = DateTime.UtcNow;
    }
}
