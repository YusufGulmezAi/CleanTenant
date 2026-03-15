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
    DbSet<CleanTenant.Domain.Email.EmailLog> EmailLogs { get; }
    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
