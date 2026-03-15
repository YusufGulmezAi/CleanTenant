using CleanTenant.Domain.Common;

namespace CleanTenant.Domain.Tenancy;

// ============================================================================
// DOMAIN EVENTS
// Bu record'lar Tenant entity'si ile ilgili iş olaylarını temsil eder.
// Application katmanında handler'lar tarafından dinlenir ve işlenir.
// ============================================================================

/// <summary>
/// Yeni bir Tenant oluşturulduğunda tetiklenir.
/// Handler'lar: Varsayılan rolleri oluştur, hoş geldin bildirimi gönder vb.
/// </summary>
public record TenantCreatedEvent(Guid TenantId, string TenantName) : IDomainEvent;
