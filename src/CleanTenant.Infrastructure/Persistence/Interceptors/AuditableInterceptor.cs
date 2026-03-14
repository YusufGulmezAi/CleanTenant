using CleanTenant.Application.Common.Interfaces;
using CleanTenant.Domain.Common;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace CleanTenant.Infrastructure.Persistence.Interceptors;

/// <summary>
/// Auditable alanları otomatik dolduran SaveChanges interceptor'ı.
/// 
/// <para><b>INTERCEPTOR NEDİR?</b></para>
/// EF Core interceptor'ları, SaveChanges çağrıldığında otomatik olarak
/// devreye giren pipeline'lardır. Geliştiricinin her Create/Update
/// işleminde elle CreatedBy, UpdatedBy vb. alanları doldurmasına
/// gerek kalmaz — interceptor bunu otomatik yapar.
/// 
/// <para><b>ÇALIŞMA SIRASI:</b></para>
/// <code>
/// dbContext.SaveChangesAsync() çağrılır
///     → AuditableInterceptor: CreatedBy/UpdatedBy doldurur
///     → SoftDeleteInterceptor: Delete → IsDeleted dönüştürür
///     → AuditTrailInterceptor: Eski/yeni değerleri Audit DB'ye yazar
///     → Veritabanına kaydedilir
/// </code>
/// </summary>
public class AuditableInterceptor : SaveChangesInterceptor
{
    private readonly ICurrentUserService _currentUser;

    public AuditableInterceptor(ICurrentUserService currentUser)
    {
        _currentUser = currentUser;
    }

    /// <summary>
    /// SaveChanges çağrılmadan hemen önce tetiklenir.
    /// ChangeTracker üzerindeki tüm entity'leri tarar ve
    /// BaseAuditableEntity olanların audit alanlarını doldurur.
    /// </summary>
    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData,
        InterceptionResult<int> result,
        CancellationToken cancellationToken = default)
    {
        if (eventData.Context is null)
            return base.SavingChangesAsync(eventData, result, cancellationToken);

        var entries = eventData.Context.ChangeTracker
            .Entries<BaseAuditableEntity>();

        var userId = _currentUser.UserId?.ToString() ?? "SYSTEM";
        var ipAddress = _currentUser.IpAddress;
        var utcNow = DateTime.UtcNow;

        foreach (var entry in entries)
        {
            switch (entry.State)
            {
                case EntityState.Added:
                    // Yeni kayıt: Oluşturma bilgilerini doldur
                    entry.Entity.CreatedBy = userId;
                    entry.Entity.CreatedAt = utcNow;
                    entry.Entity.CreatedFromIp = ipAddress;
                    break;

                case EntityState.Modified:
                    // Güncelleme: Güncelleme bilgilerini doldur
                    // Oluşturma bilgileri DEĞİŞTİRİLMEZ (immutable)
                    entry.Entity.UpdatedBy = userId;
                    entry.Entity.UpdatedAt = utcNow;
                    entry.Entity.UpdatedFromIp = ipAddress;

                    // Oluşturma alanlarının değiştirilmesini engelle
                    entry.Property(nameof(BaseAuditableEntity.CreatedBy)).IsModified = false;
                    entry.Property(nameof(BaseAuditableEntity.CreatedAt)).IsModified = false;
                    entry.Property(nameof(BaseAuditableEntity.CreatedFromIp)).IsModified = false;
                    break;
            }
        }

        return base.SavingChangesAsync(eventData, result, cancellationToken);
    }
}
