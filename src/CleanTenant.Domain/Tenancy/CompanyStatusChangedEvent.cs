using CleanTenant.Domain.Common;

namespace CleanTenant.Domain.Tenancy;

public record CompanyStatusChangedEvent(Guid CompanyId, Guid TenantId, bool IsActive) : IDomainEvent;
