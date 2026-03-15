using CleanTenant.Domain.Common;

namespace CleanTenant.Domain.Identity;

/// <summary>
/// Kullanıcı ↔ Şirket Üyeliği.
/// Üyeler, kullanıcılardan farklı olarak sınırlı erişime sahiptir.
/// Bir kullanıcı aynı şirkette hem kullanıcı hem üye olabilir
/// (farklı bağlamlarda farklı yetki — Context Switching).
/// </summary>
public class UserCompanyMembership : BaseEntity
{
    /// <summary>Kullanıcı ID'si.</summary>
    public Guid UserId { get; set; }

    /// <summary>Hangi şirkette üye?</summary>
    public Guid CompanyId { get; set; }

    /// <summary>
    /// Üyelik türü. Gelecekte farklı üyelik tipleri eklenebilir.
    /// Örnek: "External", "Consultant", "Auditor"
    /// </summary>
    public string MembershipType { get; set; } = "Standard";

    /// <summary>Atamayı yapan kullanıcının ID'si.</summary>
    public string AssignedBy { get; set; } = default!;

    /// <summary>Atama zamanı.</summary>
    public DateTime AssignedAt { get; set; } = DateTime.UtcNow;

    /// <summary>Üyelik aktif mi?</summary>
    public bool IsActive { get; set; } = true;

    // Navigation properties
    public ApplicationUser User { get; set; } = default!;
}
