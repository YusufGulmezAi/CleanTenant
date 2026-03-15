using CleanTenant.Domain.Common;
using CleanTenant.Domain.Enums;

namespace CleanTenant.Domain.Identity;

public record UserStatusChangedEvent(Guid UserId, bool IsActive) : IDomainEvent;
