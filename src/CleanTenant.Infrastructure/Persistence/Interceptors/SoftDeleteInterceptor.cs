using CleanTenant.Application.Common.Interfaces;
using CleanTenant.Domain.Common;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace CleanTenant.Infrastructure.Persistence.Interceptors;

/// <summary>
/// Soft Delete interceptor'ı — fiziksel silme yerine "silinmiş" olarak işaretler.
/// 
/// <para><b>NASIL ÇALIŞIR?</b></para>
/// <code>
/// // Geliştirici normal silme kodu yazar:
/// dbContext.Remove(tenant);
/// await dbContext.SaveChangesAsync();
/// 
/// // Interceptor bunu şuna dönüştürür:
/// tenant.IsDeleted = true;
/// tenant.DeletedAt = DateTime.UtcNow;
/// tenant.DeletedBy = currentUserId;
/// tenant.DeletedFromIp = currentIp;
/// // EntityState: Deleted → Modified
/// </code>
/// 
/// Geliştirici farkı bile hissetmez. Kod aynı kalır, davranış değişir.
/// </summary>
public class SoftDeleteInterceptor : SaveChangesInterceptor
{
    private readonly ICurrentUserService _currentUser;

    public SoftDeleteInterceptor(ICurrentUserService currentUser)
    {
        _currentUser = currentUser;
    }

    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData,
        InterceptionResult<int> result,
        CancellationToken cancellationToken = default)
    {
        if (eventData.Context is null)
            return base.SavingChangesAsync(eventData, result, cancellationToken);

        var entries = eventData.Context.ChangeTracker
            .Entries<ISoftDeletable>()
            .Where(e => e.State == EntityState.Deleted);

        var userId = _currentUser.UserId?.ToString() ?? "SYSTEM";
        var ipAddress = _currentUser.IpAddress;

        foreach (var entry in entries)
        {
            // EntityState'i Deleted'dan Modified'a çevir
            // Böylece EF Core DELETE yerine UPDATE çalıştırır
            entry.State = EntityState.Modified;

            // Soft delete alanlarını doldur
            entry.Entity.IsDeleted = true;
            entry.Entity.DeletedAt = DateTime.UtcNow;
            entry.Entity.DeletedBy = userId;
            entry.Entity.DeletedFromIp = ipAddress;
        }

        return base.SavingChangesAsync(eventData, result, cancellationToken);
    }
}
