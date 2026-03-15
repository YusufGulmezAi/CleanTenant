using CleanTenant.Domain.Common;

namespace CleanTenant.Domain.Email;

public enum EmailStatus
{
    Queued = 0,
    Sending = 1,
    Sent = 2,
    Failed = 3,
    Cancelled = 4
}
