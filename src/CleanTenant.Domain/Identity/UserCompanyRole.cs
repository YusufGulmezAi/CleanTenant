using CleanTenant.Domain.Common;

namespace CleanTenant.Domain.Identity;

/// <summary>
/// Kullanıcı ↔ Şirket Rolü ataması.
/// Bir kullanıcı birden fazla şirkette farklı rollerle çalışabilir.
/// CompanyAdmin veya üst seviye atayabilir.
/// </summary>
public class UserCompanyRole : BaseEntity
{
    /// <summary>Kullanıcı ID'si.</summary>
    public Guid UserId { get; set; }

    /// <summary>Hangi şirkette geçerli?</summary>
    public Guid CompanyId { get; set; }

    /// <summary>Şirket rolü ID'si.</summary>
    public Guid CompanyRoleId { get; set; }

    /// <summary>Atamayı yapan kullanıcının ID'si.</summary>
    public string AssignedBy { get; set; } = default!;

    /// <summary>Atama zamanı.</summary>
    public DateTime AssignedAt { get; set; } = DateTime.UtcNow;

    // Navigation properties
    public ApplicationUser User { get; set; } = default!;
    public CompanyRole CompanyRole { get; set; } = default!;
}
