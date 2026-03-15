

namespace CleanTenant.Shared.DTOs.Auth;

// ============================================================================
// 2FA Yönetim DTO'ları
// ============================================================================

/// <summary>E-posta ile 2FA aktifleştirme isteği.</summary>
public class Enable2FAEmailDto
{
    public string CurrentPassword { get; set; } = default!;
}
