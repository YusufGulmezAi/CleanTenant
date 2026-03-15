using CleanTenant.Domain.Common;
using CleanTenant.Domain.Enums;

namespace CleanTenant.Domain.Identity;

public record UserTwoFactorChangedEvent(Guid UserId, bool IsEnabled, TwoFactorMethod Method) : IDomainEvent;
