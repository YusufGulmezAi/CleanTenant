namespace CleanTenant.Domain.Common;

/// <summary>
/// Domain Event marker interface'i.
/// 
/// <para><b>NEDEN MediatR BAĞIMLILIĞI YOK?</b></para>
/// Domain katmanının hiçbir dış bağımlılığı olmamalıdır.
/// MediatR'ın INotification interface'i Application katmanında
/// bir adapter (DomainEventNotification) ile sarmalanır.
/// Böylece Domain katmanı saf kalır ve MediatR'a bağımlı olmaz.
/// 
/// <para><b>EĞER MediatR KALDIRILIRSA?</b></para>
/// Domain katmanı hiç etkilenmez. Sadece Application katmanındaki
/// adapter değişir. Bu, Clean Architecture'ın gücüdür.
/// 
/// <para><b>KULLANIM:</b></para>
/// <code>
/// // Domain event tanımı — saf C# record, hiçbir paket bağımlılığı yok
/// public record TenantCreatedEvent(Guid TenantId) : IDomainEvent;
/// 
/// // Application katmanında adapter:
/// public class DomainEventNotification&lt;T&gt; : INotification where T : IDomainEvent
/// {
///     public T DomainEvent { get; }
///     public DomainEventNotification(T domainEvent) => DomainEvent = domainEvent;
/// }
/// </code>
/// </summary>
public interface IDomainEvent
{
    /// <summary>
    /// Event'in oluşturulma zamanı (UTC).
    /// Default interface implementation ile her event otomatik olarak
    /// oluşturulma zamanını taşır — ekstra kod yazmaya gerek yoktur.
    /// </summary>
    DateTime OccurredOn => DateTime.UtcNow;
}
