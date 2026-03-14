using System.Text.Json;
using CleanTenant.Application.Common.Interfaces;
using CleanTenant.Domain.Common;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace CleanTenant.Infrastructure.Persistence.Interceptors;

/// <summary>
/// Audit Trail interceptor'ı — tüm entity değişikliklerini audit veritabanına yazar.
/// 
/// <para><b>NE KAYDEDER?</b></para>
/// Her Create/Update/Delete işleminde:
/// <list type="bullet">
///   <item>Kim yaptı? (UserId, UserEmail)</item>
///   <item>Nereden yaptı? (IP adresi, Browser bilgisi)</item>
///   <item>Ne zaman yaptı? (UTC timestamp)</item>
///   <item>Hangi modülde? (Entity adı)</item>
///   <item>Ne değişti? (Eski değerler JSON, Yeni değerler JSON)</item>
///   <item>Hangi alanlar değişti? (Affected columns listesi)</item>
/// </list>
/// 
/// <para><b>AYRI VERİTABANI:</b></para>
/// Audit kayıtları ana veritabanına DEĞİL, ayrı audit veritabanına yazılır.
/// Bu sayede:
/// - Ana veritabanı yavaşlamaz
/// - Farklı retention (saklama) politikası uygulanabilir
/// - Yasal uyumluluk gereksinimleri karşılanır
/// </para>
/// </summary>
public class AuditTrailInterceptor : SaveChangesInterceptor
{
    private readonly ICurrentUserService _currentUser;
    private readonly IAuditDbContext _auditDb;

    // SaveChanges öncesi yakalanan değişiklikleri geçici olarak saklar
    private List<AuditEntry> _pendingAuditEntries = [];

    public AuditTrailInterceptor(ICurrentUserService currentUser, IAuditDbContext auditDb)
    {
        _currentUser = currentUser;
        _auditDb = auditDb;
    }

    /// <summary>
    /// SaveChanges ÖNCESİ: Değişiklikleri yakala (eski değerler burada okunur).
    /// </summary>
    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData,
        InterceptionResult<int> result,
        CancellationToken cancellationToken = default)
    {
        if (eventData.Context is null)
            return base.SavingChangesAsync(eventData, result, cancellationToken);

        _pendingAuditEntries = CollectAuditEntries(eventData.Context);

        return base.SavingChangesAsync(eventData, result, cancellationToken);
    }

    /// <summary>
    /// SaveChanges SONRASI: Audit kayıtlarını audit veritabanına yaz.
    /// Yeni eklenen entity'lerin ID'leri artık biliniyor (SaveChanges sonrası).
    /// </summary>
    public override async ValueTask<int> SavedChangesAsync(
        SaveChangesCompletedEventData eventData,
        int result,
        CancellationToken cancellationToken = default)
    {
        if (_pendingAuditEntries.Count > 0)
        {
            foreach (var entry in _pendingAuditEntries)
            {
                // Yeni eklenen entity'lerin ID'lerini güncelle
                if (entry.Action == "Create")
                {
                    entry.EntityId = entry.EntityEntry?.Property("Id").CurrentValue?.ToString() ?? "";
                    entry.NewValues = SerializeCurrentValues(entry.EntityEntry!);
                }
            }

            // Audit DB'ye yaz
            await _auditDb.AuditLogs.AddRangeAsync(
                _pendingAuditEntries.Select(e => e.ToAuditLog()),
                cancellationToken);

            await _auditDb.SaveChangesAsync(cancellationToken);

            _pendingAuditEntries.Clear();
        }

        return await base.SavedChangesAsync(eventData, result, cancellationToken);
    }

    /// <summary>
    /// ChangeTracker'daki tüm değişiklikleri tarar ve audit entry'lerini oluşturur.
    /// </summary>
    private List<AuditEntry> CollectAuditEntries(DbContext context)
    {
        var entries = new List<AuditEntry>();
        var userId = _currentUser.UserId?.ToString() ?? "SYSTEM";
        var userEmail = _currentUser.Email ?? "SYSTEM";
        var ipAddress = _currentUser.IpAddress ?? "127.0.0.1";
        var userAgent = _currentUser.UserAgent ?? "";
        var tenantId = _currentUser.ActiveTenantId;
        var companyId = _currentUser.ActiveCompanyId;

        foreach (var entry in context.ChangeTracker.Entries())
        {
            // Sadece domain entity'leri takip et (audit entity'leri hariç)
            if (entry.Entity is not BaseEntity)
                continue;

            // Değişiklik yoksa atla
            if (entry.State is EntityState.Detached or EntityState.Unchanged)
                continue;

            var auditEntry = new AuditEntry
            {
                EntityEntry = entry,
                UserId = userId,
                UserEmail = userEmail,
                IpAddress = ipAddress,
                UserAgent = userAgent,
                TenantId = tenantId,
                CompanyId = companyId,
                EntityName = entry.Entity.GetType().Name,
                Timestamp = DateTime.UtcNow
            };

            switch (entry.State)
            {
                case EntityState.Added:
                    auditEntry.Action = "Create";
                    auditEntry.EntityId = ""; // SaveChanges sonrası doldurulacak
                    // NewValues SaveChanges sonrası doldurulacak (ID belli olunca)
                    break;

                case EntityState.Modified:
                    auditEntry.Action = "Update";
                    auditEntry.EntityId = entry.Property("Id").CurrentValue?.ToString() ?? "";
                    auditEntry.OldValues = SerializeOriginalValues(entry);
                    auditEntry.NewValues = SerializeCurrentValues(entry);
                    auditEntry.AffectedColumns = GetModifiedProperties(entry);
                    break;

                case EntityState.Deleted:
                    auditEntry.Action = "Delete";
                    auditEntry.EntityId = entry.Property("Id").CurrentValue?.ToString() ?? "";
                    auditEntry.OldValues = SerializeOriginalValues(entry);
                    break;
            }

            entries.Add(auditEntry);
        }

        return entries;
    }

    // ========================================================================
    // YARDIMCI METODLAR
    // ========================================================================

    /// <summary>Entity'nin orijinal (değişiklik öncesi) değerlerini JSON olarak serialize eder.</summary>
    private static string SerializeOriginalValues(EntityEntry entry)
    {
        var values = new Dictionary<string, object?>();
        foreach (var prop in entry.Properties.Where(p => p.IsModified || entry.State == EntityState.Deleted))
        {
            values[prop.Metadata.Name] = prop.OriginalValue;
        }
        return JsonSerializer.Serialize(values, _jsonOptions);
    }

    /// <summary>Entity'nin mevcut (değişiklik sonrası) değerlerini JSON olarak serialize eder.</summary>
    private static string SerializeCurrentValues(EntityEntry entry)
    {
        var values = new Dictionary<string, object?>();
        foreach (var prop in entry.Properties)
        {
            // Hassas alanları hariç tut
            if (prop.Metadata.Name is "PasswordHash" or "AuthenticatorKey")
                continue;

            values[prop.Metadata.Name] = prop.CurrentValue;
        }
        return JsonSerializer.Serialize(values, _jsonOptions);
    }

    /// <summary>Değiştirilen property isimlerini döndürür.</summary>
    private static List<string> GetModifiedProperties(EntityEntry entry)
    {
        return entry.Properties
            .Where(p => p.IsModified)
            .Select(p => p.Metadata.Name)
            .ToList();
    }

    private static readonly JsonSerializerOptions _jsonOptions = new()
    {
        WriteIndented = false,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };
}

/// <summary>
/// Geçici audit entry — SaveChanges öncesi/sonrası bilgi taşır.
/// </summary>
internal class AuditEntry
{
    public EntityEntry? EntityEntry { get; set; }
    public string UserId { get; set; } = default!;
    public string UserEmail { get; set; } = default!;
    public string IpAddress { get; set; } = default!;
    public string UserAgent { get; set; } = default!;
    public Guid? TenantId { get; set; }
    public Guid? CompanyId { get; set; }
    public string EntityName { get; set; } = default!;
    public string EntityId { get; set; } = default!;
    public string Action { get; set; } = default!;
    public string? OldValues { get; set; }
    public string? NewValues { get; set; }
    public List<string> AffectedColumns { get; set; } = [];
    public DateTime Timestamp { get; set; }

    /// <summary>AuditLog entity'sine dönüştürür.</summary>
    public AuditLog ToAuditLog() => new()
    {
        Id = Guid.CreateVersion7(),
        Timestamp = Timestamp,
        UserId = UserId,
        UserEmail = UserEmail,
        IpAddress = IpAddress,
        UserAgent = UserAgent,
        TenantId = TenantId,
        CompanyId = CompanyId,
        EntityName = EntityName,
        EntityId = EntityId,
        Action = Action,
        OldValues = OldValues,
        NewValues = NewValues,
        AffectedColumns = AffectedColumns
    };
}
