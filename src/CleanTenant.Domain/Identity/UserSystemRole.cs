using CleanTenant.Domain.Common;

namespace CleanTenant.Domain.Identity;

// ============================================================================
// KULLANICI-ROL ATAMA TABLOLARI (Pivot Tables / Junction Tables)
// 
// Her tablo bir kullanıcının belirli bir seviyedeki rolünü temsil eder.
// Composite key: (UserId + RoleId) veya (UserId + TenantId/CompanyId + RoleId)
// 
// NEDEN AYRI TABLOLAR?
// Tek bir "UserRoles" tablosu yerine seviye bazlı ayrım yapıyoruz çünkü:
// 1. Her seviyenin farklı kısıtlamaları var (TenantId, CompanyId)
// 2. Global Query Filter seviyeye göre farklı çalışır
// 3. Sorgular daha performanslı (gereksiz JOIN'ler yok)
// 4. Kod okunabilirliği daha yüksek
// ============================================================================

/// <summary>
/// Kullanıcı ↔ Sistem Rolü ataması.
/// Sistem rolleri tüm tenant'larda geçerlidir.
/// Sadece SuperAdmin atayabilir.
/// </summary>
public class UserSystemRole : BaseEntity
{
    /// <summary>Kullanıcı ID'si.</summary>
    public Guid UserId { get; set; }

    /// <summary>Sistem rolü ID'si.</summary>
    public Guid SystemRoleId { get; set; }

    /// <summary>Atamayı yapan kullanıcının ID'si.</summary>
    public string AssignedBy { get; set; } = default!;

    /// <summary>Atama zamanı.</summary>
    public DateTime AssignedAt { get; set; } = DateTime.UtcNow;

    // Navigation properties
    public ApplicationUser User { get; set; } = default!;
    public SystemRole SystemRole { get; set; } = default!;
}
