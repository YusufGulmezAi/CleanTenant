using CleanTenant.Application.Common.Interfaces;
using CleanTenant.Application.Common.Models;
using CleanTenant.Domain.Tenancy;
using Microsoft.EntityFrameworkCore;

namespace CleanTenant.Application.Common.Rules;

/// <summary>
/// Tenant iş kuralları — tekrar eden kontrolleri merkezileştirir.
/// 
/// <para><b>NEDEN BUSINESS RULES SINIFI?</b></para>
/// Handler'larda sürekli tekrar eden kontroller vardır:
/// <code>
/// // ❌ HER HANDLER'DA TEKRAR:
/// var tenant = await db.Tenants.FindAsync(id);
/// if (tenant is null) return Result.NotFound("Tenant bulunamadı.");
/// if (!tenant.IsActive) return Result.Failure("Tenant aktif değil.");
/// if (tenant.IsDeleted) return Result.Failure("Tenant silinmiş.");
/// </code>
/// 
/// Business Rules sınıfı ile:
/// <code>
/// // ✅ HANDLER'DA TEK SATIR:
/// var tenantResult = await _tenantRules.GetActiveOrFailAsync(id, ct);
/// if (tenantResult.IsFailure) return tenantResult.ToResult&lt;X&gt;();
/// var tenant = tenantResult.Value!;
/// </code>
/// 
/// <para><b>DI İLE INJECT EDİLİR:</b></para>
/// <code>
/// public class CreateCompanyHandler(
///     IApplicationDbContext db,
///     TenantRules tenantRules)  // ← Constructor injection
/// </code>
/// </para>
/// </summary>
public class TenantRules
{
    private readonly IApplicationDbContext _db;

    public TenantRules(IApplicationDbContext db)
    {
        _db = db;
    }

    /// <summary>
    /// Tenant'ı ID ile bulur. Bulamazsa NotFound döner.
    /// </summary>
    public async Task<Result<Tenant>> GetOrFailAsync(Guid tenantId, CancellationToken ct)
    {
        var tenant = await _db.Tenants
            .FirstOrDefaultAsync(t => t.Id == tenantId, ct);

        if (tenant is null)
            return Result<Tenant>.NotFound($"Tenant bulunamadı. (ID: {tenantId})");

        return Result<Tenant>.Success(tenant);
    }

    /// <summary>
    /// Tenant'ı bulur VE aktif olduğunu doğrular.
    /// Handler'larda en sık kullanılan kural.
    /// </summary>
    public async Task<Result<Tenant>> GetActiveOrFailAsync(Guid tenantId, CancellationToken ct)
    {
        var result = await GetOrFailAsync(tenantId, ct);
        if (result.IsFailure) return result;

        if (!result.Value!.IsActive)
            return Result<Tenant>.Failure("Bu tenant aktif değildir. İşlem yapılamaz.", 403);

        return result;
    }

    /// <summary>
    /// Identifier'ın benzersiz olduğunu doğrular.
    /// Yeni tenant oluştururken ve güncellerken kullanılır.
    /// </summary>
    /// <param name="identifier">Kontrol edilecek identifier</param>
    /// <param name="excludeId">Güncelleme durumunda mevcut tenant'ın ID'si (kendini hariç tut)</param>
    public async Task<Result<bool>> EnsureIdentifierUniqueAsync(
        string identifier, Guid? excludeId = null, CancellationToken ct = default)
    {
        var normalizedIdentifier = identifier.Trim().ToLowerInvariant();

        var exists = await _db.Tenants
            .AnyAsync(t =>
                t.Identifier == normalizedIdentifier &&
                (excludeId == null || t.Id != excludeId),
                ct);

        if (exists)
            return Result<bool>.Failure($"'{identifier}' tanımlayıcısı zaten kullanılıyor.");

        return Result<bool>.Success(true);
    }

    /// <summary>
    /// Tenant'ın altında aktif şirket olup olmadığını kontrol eder.
    /// Tenant silme işleminden önce kullanılır.
    /// </summary>
    public async Task<Result<bool>> EnsureNoDependenciesAsync(Guid tenantId, CancellationToken ct)
    {
        var hasCompanies = await _db.Companies
            .AnyAsync(c => c.TenantId == tenantId, ct);

        if (hasCompanies)
            return Result<bool>.Failure(
                "Bu tenant altında şirketler bulunmaktadır. Önce şirketleri siliniz.");

        return Result<bool>.Success(true);
    }
}
