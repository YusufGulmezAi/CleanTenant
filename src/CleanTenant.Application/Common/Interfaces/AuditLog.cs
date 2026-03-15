using Microsoft.EntityFrameworkCore;

namespace CleanTenant.Application.Common.Interfaces;

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
