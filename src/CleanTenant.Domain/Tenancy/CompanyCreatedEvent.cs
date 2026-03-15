using CleanTenant.Domain.Common;

namespace CleanTenant.Domain.Tenancy;

// ============================================================================
// DOMAIN EVENTS
// ============================================================================

public record CompanyCreatedEvent(Guid CompanyId, Guid TenantId, string CompanyName) : IDomainEvent;
