using CleanTenant.Domain.Common;
using CleanTenant.Domain.Enums;

namespace CleanTenant.Domain.Identity;

public record UserLockedOutEvent(Guid UserId, DateTime LockoutEnd) : IDomainEvent;
