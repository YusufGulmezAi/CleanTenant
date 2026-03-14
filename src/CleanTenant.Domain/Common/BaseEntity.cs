namespace CleanTenant.Domain.Common;

/// <summary>
/// Tüm entity'lerin miras aldığı temel sınıf.
/// 
/// <para><b>NEDEN ABSTRACT?</b></para>
/// Doğrudan örneklenemez (new BaseEntity() yapılamaz).
/// Sadece miras alınarak kullanılabilir. Bu, her entity'nin
/// kendine özgü bir kimliğe sahip olmasını zorunlu kılar.
/// 
/// <para><b>GUID KULLANIMI:</b></para>
/// Neden int veya long değil de Guid kullanıyoruz?
/// <list type="bullet">
///   <item>Dağıtık sistemlerde çakışma riski sıfır (UUID v7 ile sıralı)</item>
///   <item>Veritabanı insert'ten önce ID biliniyor (client-side generation)</item>
///   <item>Multi-tenant yapıda tenant'lar arası ID çakışması imkansız</item>
///   <item>Güvenlik: Sequential int ile kayıt sayısı tahmin edilemez</item>
/// </list>
/// 
/// <para><b>DOMAIN EVENTS:</b></para>
/// Domain Driven Design'ın temel kavramlarından biri. Bir entity üzerinde
/// önemli bir iş olayı gerçekleştiğinde (örn: Tenant oluşturuldu, Kullanıcı
/// bloke edildi) bu olaylar domain event olarak kaydedilir ve ilgili
/// handler'lar tarafından işlenir. Örneğin:
/// - TenantCreatedEvent → Varsayılan roller oluşturulur
/// - UserBlockedEvent → Redis cache güncellenir, aktif oturum sonlandırılır
/// </summary>
public abstract class BaseEntity
{
    /// <summary>
    /// Entity'nin benzersiz kimliği.
    /// Guid.CreateVersion7() ile üretilir — zamana dayalı sıralama sağlar.
    /// PostgreSQL'de uuid tipine karşılık gelir.
    /// </summary>
    public Guid Id { get; set; }

    /// <summary>
    /// Bu entity üzerinde henüz yayınlanmamış domain event'lerin listesi.
    /// 
    /// <para><b>NEDEN PRIVATE SET?</b></para>
    /// Dışarıdan doğrudan liste ataması yapılmasını engeller.
    /// Event eklemek için <see cref="AddDomainEvent"/> metodu kullanılmalıdır.
    /// Bu, kontrollü erişim (encapsulation) prensibidir.
    /// </summary>
    private readonly List<IDomainEvent> _domainEvents = [];

    /// <summary>
    /// Yayınlanmayı bekleyen domain event'lerin salt okunur koleksiyonu.
    /// 
    /// <para>
    /// IReadOnlyCollection ile dışarıya sadece okuma izni veriyoruz.
    /// Listeye eleman eklemek için <see cref="AddDomainEvent"/> kullanılmalıdır.
    /// Bu, Encapsulation (kapsülleme) prensibinin somut bir uygulamasıdır.
    /// </para>
    /// </summary>
    public IReadOnlyCollection<IDomainEvent> DomainEvents => _domainEvents.AsReadOnly();

    /// <summary>
    /// Entity üzerinde gerçekleşen bir domain olayını kaydeder.
    /// Bu event'ler SaveChanges sırasında MediatR ile yayınlanır.
    /// 
    /// <para><b>KULLANIM ÖRNEĞİ:</b></para>
    /// <code>
    /// // Tenant entity'si içinde:
    /// public static Tenant Create(string name, string domain)
    /// {
    ///     var tenant = new Tenant { Name = name, Domain = domain };
    ///     tenant.AddDomainEvent(new TenantCreatedEvent(tenant.Id));
    ///     return tenant;
    /// }
    /// </code>
    /// </summary>
    /// <param name="domainEvent">Kaydedilecek domain event</param>
    public void AddDomainEvent(IDomainEvent domainEvent)
    {
        _domainEvents.Add(domainEvent);
    }

    /// <summary>
    /// Belirli bir domain event'i listeden kaldırır.
    /// Genellikle event yayınlandıktan sonra çağrılır.
    /// </summary>
    public void RemoveDomainEvent(IDomainEvent domainEvent)
    {
        _domainEvents.Remove(domainEvent);
    }

    /// <summary>
    /// Tüm domain event'leri temizler.
    /// SaveChanges interceptor'ı event'leri yayınladıktan sonra bu metodu çağırır.
    /// Böylece aynı event birden fazla kez yayınlanmaz.
    /// </summary>
    public void ClearDomainEvents()
    {
        _domainEvents.Clear();
    }
}
