using CleanTenant.Domain.Common;
using CleanTenant.Domain.Enums;

namespace CleanTenant.Domain.Identity;

// ============================================================================
// DOMAIN EVENTS
// ============================================================================

public record UserCreatedEvent(Guid UserId, string Email) : IDomainEvent;
