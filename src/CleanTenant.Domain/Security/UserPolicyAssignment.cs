using CleanTenant.Domain.Common;

namespace CleanTenant.Domain.Security;

/// <summary>Kullanıcı ↔ Politika ataması.</summary>
public class UserPolicyAssignment : BaseEntity
{
    public Guid UserId { get; set; }
    public Guid AccessPolicyId { get; set; }
    public string AssignedBy { get; set; } = default!;
    public DateTime AssignedAt { get; set; } = DateTime.UtcNow;

    // Navigation
    public AccessPolicy AccessPolicy { get; set; } = default!;
    public CleanTenant.Domain.Identity.ApplicationUser User { get; set; } = default!;
}
