namespace CleanTenant.Domain.Common;

/// <summary>
/// Şirket kapsamlı entity'lerin temel sınıfı.
/// 
/// <para><b>ÇİFT KATMANLI İZOLASYON:</b></para>
/// Bu sınıf hem TenantId hem CompanyId taşır. EF Core'da iki ayrı
/// Global Query Filter uygulanır:
/// <code>
/// builder.HasQueryFilter(e => 
///     e.TenantId == _currentTenantId &amp;&amp; 
///     e.CompanyId == _currentCompanyId);
/// </code>
/// 
/// <para><b>NEDEN HEM TenantId HEM CompanyId?</b></para>
/// "Company zaten Tenant'a bağlı, CompanyId yeterli değil mi?" sorusu
/// sıkça sorulur. Cevap: Performans ve güvenlik.
/// <list type="bullet">
///   <item>
///     <b>Performans:</b> TenantId + CompanyId composite index ile sorgular
///     çok daha hızlı çalışır. Sadece CompanyId ile sorgu yapıldığında
///     önce Company tablosundan TenantId'yi çözümlemek gerekir (JOIN).
///   </item>
///   <item>
///     <b>Güvenlik:</b> Çift filtre ile bir tenant, başka bir tenant'ın
///     şirket verisine erişemez — CompanyId'yi bilse bile TenantId uyuşmaz.
///   </item>
///   <item>
///     <b>Yedekleme:</b> Şirket bazlı filtered backup yaparken CompanyId
///     ile filtreleme doğrudan yapılabilir, ekstra JOIN gerekmez.
///   </item>
/// </list>
/// 
/// <para><b>DENORMALIZATION MI?</b></para>
/// Evet, teknik olarak TenantId burada denormalize edilmiş veridir
/// (Company tablosunda zaten var). Ancak bu bilinçli bir tasarım kararıdır.
/// Enterprise sistemlerde performans ve güvenlik, normalizasyon kurallarından
/// önce gelir. Bu yaklaşım "pragmatik denormalization" olarak bilinir.
/// </summary>
public abstract class BaseCompanyEntity : BaseAuditableEntity, ISoftDeletable
{
    /// <summary>
    /// Bu kaydın ait olduğu Tenant'ın ID'si.
    /// Global Query Filter'ın birinci katmanı.
    /// </summary>
    public Guid TenantId { get; set; }

    /// <summary>
    /// Bu kaydın ait olduğu Şirketin ID'si.
    /// Global Query Filter'ın ikinci katmanı.
    /// 
    /// <para>
    /// Veritabanında (TenantId, CompanyId) composite index oluşturulmalıdır.
    /// Bu, şirket bazlı sorguları dramatik şekilde hızlandırır.
    /// </para>
    /// </summary>
    public Guid CompanyId { get; set; }

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
}
