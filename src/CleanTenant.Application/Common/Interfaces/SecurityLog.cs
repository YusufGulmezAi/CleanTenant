using Microsoft.EntityFrameworkCore;

namespace CleanTenant.Application.Common.Interfaces;

/// <summary>
/// Güvenlik olayı kaydı — login, 2FA, bloke, şifre değişikliği vb.
/// </summary>
public class SecurityLog
{
    public Guid Id { get; set; }
    public DateTime Timestamp { get; set; }

    // Kim?
    public Guid? UserId { get; set; }
    public string? UserEmail { get; set; }

    // Nereden?
    public string IpAddress { get; set; } = default!;
    public string? UserAgent { get; set; }

    /// <summary>Olay türü — SecurityEventType enum'unun string hali.</summary>
    public string EventType { get; set; } = default!;

    /// <summary>Olay açıklaması.</summary>
    public string? Description { get; set; }

    /// <summary>Ek detaylar (JSONB). Örnek: { "blockedBy": "admin@test.com", "reason": "..." }</summary>
    public string? Details { get; set; }

    /// <summary>İşlem başarılı mı?</summary>
    public bool IsSuccess { get; set; }
}
