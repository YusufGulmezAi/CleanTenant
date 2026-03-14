using Microsoft.EntityFrameworkCore;

namespace CleanTenant.Application.Common.Interfaces;

/// <summary>
/// Audit veritabanı sözleşmesi.
/// Üç tablo içerir:
/// <list type="bullet">
///   <item>AuditLogs: Entity değişiklikleri (eski/yeni değerler)</item>
///   <item>ApplicationLogs: Serilog yapısal logları</item>
///   <item>SecurityLogs: Güvenlik olayları (login, 2FA, bloke vb.)</item>
/// </list>
/// </summary>
public interface IAuditDbContext
{
    DbSet<AuditLog> AuditLogs { get; }
    DbSet<SecurityLog> SecurityLogs { get; }
    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}

/// <summary>
/// Entity değişiklik kaydı — eski ve yeni değerler JSONB olarak saklanır.
/// </summary>
public class AuditLog
{
    public Guid Id { get; set; }
    public DateTime Timestamp { get; set; }

    // Kim?
    public string UserId { get; set; } = default!;
    public string UserEmail { get; set; } = default!;

    // Nereden?
    public string IpAddress { get; set; } = default!;
    public string UserAgent { get; set; } = default!;

    // Hangi bağlamda?
    public Guid? TenantId { get; set; }
    public Guid? CompanyId { get; set; }

    // Ne değişti?
    /// <summary>Entity sınıf adı. Örnek: "Tenant", "ApplicationUser"</summary>
    public string EntityName { get; set; } = default!;

    /// <summary>Entity'nin ID'si (string — farklı ID tipleri desteklenir).</summary>
    public string EntityId { get; set; } = default!;

    /// <summary>İşlem türü: "Create", "Update", "Delete"</summary>
    public string Action { get; set; } = default!;

    /// <summary>Değişiklik öncesi değerler (JSONB). Create işleminde null.</summary>
    public string? OldValues { get; set; }

    /// <summary>Değişiklik sonrası değerler (JSONB). Delete işleminde null.</summary>
    public string? NewValues { get; set; }

    /// <summary>Değiştirilen kolon adları listesi.</summary>
    public List<string> AffectedColumns { get; set; } = [];
}

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
