using CleanTenant.Domain.Common;

namespace CleanTenant.Domain.Identity;

/// <summary>
/// Kullanıcı ↔ Tenant Rolü ataması.
/// Bir kullanıcı birden fazla tenant'ta farklı rollerle çalışabilir.
/// TenantAdmin veya üst seviye atayabilir.
/// </summary>
public class UserTenantRole : BaseEntity
{
    /// <summary>Kullanıcı ID'si.</summary>
    public Guid UserId { get; set; }

    /// <summary>Hangi tenant'ta geçerli?</summary>
    public Guid TenantId { get; set; }

    /// <summary>Tenant rolü ID'si.</summary>
    public Guid TenantRoleId { get; set; }

    /// <summary>Atamayı yapan kullanıcının ID'si.</summary>
    public string AssignedBy { get; set; } = default!;

    /// <summary>Atama zamanı.</summary>
    public DateTime AssignedAt { get; set; } = DateTime.UtcNow;

    // Navigation properties
    public ApplicationUser User { get; set; } = default!;
    public TenantRole TenantRole { get; set; } = default!;
}
