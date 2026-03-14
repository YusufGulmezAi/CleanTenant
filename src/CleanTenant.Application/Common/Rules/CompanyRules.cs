using CleanTenant.Application.Common.Interfaces;
using CleanTenant.Application.Common.Models;
using CleanTenant.Domain.Tenancy;
using Microsoft.EntityFrameworkCore;

namespace CleanTenant.Application.Common.Rules;

/// <summary>
/// Company iş kuralları — şirket işlemlerinde tekrar eden kontroller.
/// </summary>
public class CompanyRules
{
    private readonly IApplicationDbContext _db;

    public CompanyRules(IApplicationDbContext db)
    {
        _db = db;
    }

    /// <summary>Company'yi ID ile bulur.</summary>
    public async Task<Result<Company>> GetOrFailAsync(Guid companyId, CancellationToken ct)
    {
        var company = await _db.Companies
            .Include(c => c.Tenant)
            .FirstOrDefaultAsync(c => c.Id == companyId, ct);

        if (company is null)
            return Result<Company>.NotFound($"Şirket bulunamadı. (ID: {companyId})");

        return Result<Company>.Success(company);
    }

    /// <summary>Company'yi bulur VE aktif olduğunu doğrular. Tenant'ın da aktif olduğunu kontrol eder.</summary>
    public async Task<Result<Company>> GetActiveOrFailAsync(Guid companyId, CancellationToken ct)
    {
        var result = await GetOrFailAsync(companyId, ct);
        if (result.IsFailure) return result;

        var company = result.Value!;

        // Üst hiyerarşi kontrolü: Tenant aktif mi?
        if (!company.Tenant.IsActive)
            return Result<Company>.Failure("Bu şirketin bağlı olduğu tenant aktif değildir.", 403);

        if (!company.IsActive)
            return Result<Company>.Failure("Bu şirket aktif değildir. İşlem yapılamaz.", 403);

        return result;
    }

    /// <summary>
    /// Aynı tenant içinde şirket kodunun benzersiz olduğunu doğrular.
    /// </summary>
    public async Task<Result<bool>> EnsureCodeUniqueInTenantAsync(
        Guid tenantId, string code, Guid? excludeId = null, CancellationToken ct = default)
    {
        var normalizedCode = code.Trim().ToUpperInvariant();

        var exists = await _db.Companies
            .AnyAsync(c =>
                c.TenantId == tenantId &&
                c.Code == normalizedCode &&
                (excludeId == null || c.Id != excludeId),
                ct);

        if (exists)
            return Result<bool>.Failure(
                $"'{code}' şirket kodu bu tenant içinde zaten kullanılıyor.");

        return Result<bool>.Success(true);
    }

    /// <summary>
    /// Şirketin belirli bir tenant'a ait olduğunu doğrular.
    /// Yetkisiz tenant'tan başka tenant'ın şirketine erişimi engeller.
    /// </summary>
    public async Task<Result<Company>> GetInTenantOrFailAsync(
        Guid companyId, Guid tenantId, CancellationToken ct)
    {
        var result = await GetOrFailAsync(companyId, ct);
        if (result.IsFailure) return result;

        if (result.Value!.TenantId != tenantId)
            return Result<Company>.Forbidden("Bu şirket sizin tenant'ınıza ait değildir.");

        return result;
    }

    /// <summary>
    /// Şirket altında aktif kullanıcı/üye olup olmadığını kontrol eder.
    /// Şirket silme öncesi kullanılır.
    /// </summary>
    public async Task<Result<bool>> EnsureNoDependenciesAsync(Guid companyId, CancellationToken ct)
    {
        var hasUsers = await _db.UserCompanyRoles
            .AnyAsync(ucr => ucr.CompanyId == companyId, ct);

        var hasMembers = await _db.UserCompanyMemberships
            .AnyAsync(ucm => ucm.CompanyId == companyId, ct);

        if (hasUsers || hasMembers)
            return Result<bool>.Failure(
                "Bu şirket altında kullanıcılar veya üyeler bulunmaktadır. Önce bunları kaldırınız.");

        return Result<bool>.Success(true);
    }
}
