namespace CleanTenant.Domain.Common;

/// <summary>
/// Denetlenebilir (auditable) entity'lerin temel sınıfı.
/// BaseEntity'yi genişleterek "Kim, Ne Zaman, Nereden" bilgilerini ekler.
/// 
/// <para><b>NEDEN HER ENTITY'DE DEĞİL?</b></para>
/// Bazı entity'ler (örneğin: lookup tabloları, ayar tabloları) için
/// audit bilgisi gereksiz olabilir. Bu yüzden audit bilgisi opsiyonel
/// bir miras olarak sunulur. İhtiyacı olan entity'ler BaseAuditableEntity'den,
/// olmayanlar BaseEntity'den miras alır.
/// 
/// <para><b>GENİŞLETİLMİŞ AUDIT:</b></para>
/// Klasik CreatedBy/UpdatedBy'ın ötesinde, her işlemin yapıldığı
/// IP adresi de saklanır. Bu, güvenlik denetimi ve yasal uyumluluk
/// (KVKK, GDPR) açısından kritiktir.
/// 
/// <para><b>OTOMATİK DOLDURMA:</b></para>
/// Bu alanlar kod içinde elle set edilmez! EF Core SaveChanges 
/// interceptor'ı (AuditInterceptor) bu alanları otomatik olarak doldurur.
/// Geliştirici sadece entity'yi oluşturur/günceller, interceptor gerisini halleder.
/// </summary>
public abstract class BaseAuditableEntity : BaseEntity
{
    // ========================================================================
    // OLUŞTURMA BİLGİLERİ (Creation Audit)
    // Bu alanlar entity ilk kez veritabanına kaydedildiğinde doldurulur.
    // Bir kez yazılır, bir daha DEĞİŞMEZ (immutable creation record).
    // ========================================================================

    /// <summary>
    /// Entity'yi oluşturan kullanıcının ID'si.
    /// Seed data veya sistem işlemleri için "SYSTEM" değeri kullanılır.
    /// </summary>
    public string? CreatedBy { get; set; }

    /// <summary>
    /// Entity'nin oluşturulma zamanı (UTC).
    /// Her zaman UTC kullanırız — timezone dönüşümü UI katmanında yapılır.
    /// </summary>
    public DateTime CreatedAt { get; set; }

    /// <summary>
    /// Entity'yi oluşturan kullanıcının IP adresi.
    /// Seed data için null olabilir, bu yüzden nullable.
    /// </summary>
    public string? CreatedFromIp { get; set; }

    // ========================================================================
    // GÜNCELLEME BİLGİLERİ (Update Audit)
    // Entity her güncellendiğinde bu alanlar yeniden yazılır.
    // Nullable çünkü yeni oluşturulan entity henüz güncellenmemiştir.
    // ========================================================================

    /// <summary>
    /// Entity'yi son güncelleyen kullanıcının ID'si.
    /// </summary>
    public string? UpdatedBy { get; set; }

    /// <summary>
    /// Son güncelleme zamanı (UTC).
    /// </summary>
    public DateTime? UpdatedAt { get; set; }

    /// <summary>
    /// Entity'yi son güncelleyen kullanıcının IP adresi.
    /// </summary>
    public string? UpdatedFromIp { get; set; }
}
