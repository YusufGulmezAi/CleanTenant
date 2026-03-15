using CleanTenant.Domain.Common;
using CleanTenant.Domain.Enums;
using CleanTenant.Domain.Identity;

namespace CleanTenant.Domain.Security;

/// <summary>
/// Kullanıcı bloklama kaydı.
/// Yetkili bir admin tarafından geçici veya kalıcı olarak bloklanan kullanıcılar.
/// </summary>
public class UserBlock : BaseEntity
{
    private UserBlock() { }

    /// <summary>Bloklanan kullanıcı.</summary>
    public Guid UserId { get; private set; }

    /// <summary>Bloklama türü.</summary>
    public BlockType BlockType { get; private set; }

    /// <summary>Bloklayan admin.</summary>
    public string BlockedBy { get; private set; } = default!;

    /// <summary>Bloklama zamanı.</summary>
    public DateTime BlockedAt { get; private set; }

    /// <summary>Bloklama nedeni.</summary>
    public string? Reason { get; private set; }

    /// <summary>
    /// Blok bitiş zamanı (sadece Temporary bloklar için).
    /// null ise: Permanent blok veya ForceLogout.
    /// </summary>
    public DateTime? ExpiresAt { get; private set; }

    /// <summary>Blok kaldırıldı mı?</summary>
    public bool IsLifted { get; private set; }

    /// <summary>Bloğu kaldıran kullanıcı.</summary>
    public string? LiftedBy { get; private set; }

    /// <summary>Blok kaldırma zamanı.</summary>
    public DateTime? LiftedAt { get; private set; }

    // Navigation
    public ApplicationUser User { get; private set; } = default!;

    public static UserBlock Create(Guid userId, BlockType blockType, string blockedBy, string? reason, DateTime? expiresAt = null)
    {
        return new UserBlock
        {
            Id = Guid.CreateVersion7(),
            UserId = userId,
            BlockType = blockType,
            BlockedBy = blockedBy,
            BlockedAt = DateTime.UtcNow,
            Reason = reason,
            ExpiresAt = expiresAt,
            IsLifted = false
        };
    }

    /// <summary>Bloğu kaldırır.</summary>
    public void Lift(string liftedBy)
    {
        IsLifted = true;
        LiftedBy = liftedBy;
        LiftedAt = DateTime.UtcNow;
    }

    /// <summary>Blok aktif mi? (Süresi dolmamış, kaldırılmamış)</summary>
    public bool IsActive() => !IsLifted && (ExpiresAt == null || ExpiresAt > DateTime.UtcNow);
}
