using CleanTenant.Domain.Common;

namespace CleanTenant.Domain.Tenancy;

/// <summary>
/// Tenant'ın aktif/pasif durumu değiştiğinde tetiklenir.
/// Handler'lar: Pasif edilmişse tüm aktif oturumları sonlandır vb.
/// </summary>
public record TenantStatusChangedEvent(Guid TenantId, bool IsActive) : IDomainEvent;
