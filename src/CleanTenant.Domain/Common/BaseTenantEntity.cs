namespace CleanTenant.Domain.Common;

/// <summary>
/// Tenant kapsamlı entity'lerin temel sınıfı.
/// 
/// <para><b>HİYERARŞİK İZOLASYON:</b></para>
/// CleanTenant'ın 3 katmanlı hiyerarşisinde (System → Tenant → Company)
/// bu sınıf Tenant seviyesindeki verileri temsil eder.
/// 
/// TenantId alanı ile Global Query Filter uygulanır:
/// <code>
/// // EF Core konfigürasyonunda:
/// builder.HasQueryFilter(e => e.TenantId == _currentTenantId);
/// </code>
/// 
/// Bu sayede bir tenant'ın verileri, başka bir tenant tarafından
/// KESİNLİKLE görüntülenemez. Geliştirici Where(x => x.TenantId == ...) 
/// yazmak zorunda kalmaz — filtre otomatik uygulanır.
/// 
/// <para><b>OTOMATİK TenantId ATAMASI:</b></para>
/// Yeni kayıt eklerken TenantId'yi elle set etmeye gerek yoktur.
/// TenantInterceptor, SaveChanges sırasında mevcut kullanıcının
/// tenant bilgisini otomatik olarak atar.
/// </summary>
public abstract class BaseTenantEntity : BaseAuditableEntity, ISoftDeletable
{
    /// <summary>
    /// Bu kaydın ait olduğu Tenant'ın ID'si.
    /// 
    /// <para>
    /// Global Query Filter ile tüm sorgulara otomatik WHERE koşulu eklenir.
    /// Interceptor ile yeni kayıtlara otomatik atanır.
    /// Bu alan üzerinde veritabanı index'i oluşturulmalıdır (performance).
    /// </para>
    /// </summary>
    public Guid TenantId { get; set; }

    // ========================================================================
    // ISoftDeletable implementasyonu
    // Tenant kapsamlı entity'ler varsayılan olarak soft delete destekler.
    // Silinen kayıtlar fiziksel olarak kaldırılmaz, IsDeleted ile işaretlenir.
    // ========================================================================

    /// <inheritdoc />
    public bool IsDeleted { get; set; }

    /// <inheritdoc />
    public DateTime? DeletedAt { get; set; }

    /// <inheritdoc />
    public string? DeletedBy { get; set; }

    /// <inheritdoc />
    public string? DeletedFromIp { get; set; }
}
