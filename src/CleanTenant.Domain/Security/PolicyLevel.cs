using CleanTenant.Domain.Common;

namespace CleanTenant.Domain.Security;

/// <summary>Politika seviyesi.</summary>
public enum PolicyLevel
{
    System = 0,
    Tenant = 1,
    Company = 2
}
