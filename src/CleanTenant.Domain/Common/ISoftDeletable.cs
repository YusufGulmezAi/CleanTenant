namespace CleanTenant.Domain.Common;

/// <summary>
/// Soft Delete (Yumuşak Silme) sözleşmesi.
/// 
/// <para><b>SOFT DELETE NEDİR?</b></para>
/// Veriyi veritabanından fiziksel olarak silmek yerine, "silinmiş" olarak
/// işaretleriz. Bu yaklaşımın avantajları:
/// <list type="bullet">
///   <item>Yanlışlıkla silinen veri kurtarılabilir</item>
///   <item>Audit trail bozulmaz (referanslar geçerli kalır)</item>
///   <item>Yasal uyumluluk: Bazı veriler yasal süre boyunca tutulmalıdır</item>
///   <item>Raporlama: Geçmiş veriler analiz için erişilebilir kalır</item>
/// </list>
/// 
/// <para><b>NEDEN INTERFACE (ABSTRACT CLASS DEĞİL)?</b></para>
/// C#'ta çoklu miras yoktur. Bir entity hem BaseAuditableEntity'den
/// hem BaseTenantEntity'den miras alabilir ama üçüncü bir sınıftan alamaz.
/// Interface kullanarak soft delete özelliğini herhangi bir entity'ye
/// esnek şekilde ekleyebiliriz.
/// 
/// <para><b>OTOMATİK ÇALIŞMA:</b></para>
/// EF Core'da Global Query Filter ile IsDeleted == false koşulu
/// tüm sorgulara otomatik eklenir. Silinmiş kayıtlar sorgu sonuçlarında
/// görünmez. Silinmiş kayıtlara erişmek için:
/// <code>
/// dbContext.Tenants.IgnoreQueryFilters().Where(t => t.IsDeleted)
/// </code>
/// 
/// SoftDeleteInterceptor, Delete işlemini otomatik olarak Update'e çevirir.
/// Geliştirici <c>dbContext.Remove(entity)</c> çağırır, interceptor bunu
/// <c>entity.IsDeleted = true</c> olarak dönüştürür.
/// </summary>
public interface ISoftDeletable
{
    /// <summary>
    /// Kayıt silinmiş olarak işaretli mi?
    /// true ise normal sorgularda görünmez (Global Query Filter).
    /// </summary>
    bool IsDeleted { get; set; }

    /// <summary>
    /// Silme işleminin zamanı (UTC).
    /// Nullable çünkü silinmemiş kayıtlarda null olacaktır.
    /// </summary>
    DateTime? DeletedAt { get; set; }

    /// <summary>
    /// Silme işlemini yapan kullanıcının ID'si.
    /// </summary>
    string? DeletedBy { get; set; }

    /// <summary>
    /// Silme işleminin yapıldığı IP adresi.
    /// Güvenlik denetimi için saklanır.
    /// </summary>
    string? DeletedFromIp { get; set; }
}
