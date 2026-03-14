using CleanTenant.Application.Common.Interfaces;
using CleanTenant.Application.Common.Models;
using CleanTenant.Domain.Identity;
using Microsoft.EntityFrameworkCore;

namespace CleanTenant.Application.Common.Rules;

/// <summary>
/// Kullanıcı iş kuralları — kullanıcı işlemlerinde tekrar eden kontroller.
/// </summary>
public class UserRules
{
    private readonly IApplicationDbContext _db;

    public UserRules(IApplicationDbContext db)
    {
        _db = db;
    }

    /// <summary>Kullanıcıyı ID ile bulur.</summary>
    public async Task<Result<ApplicationUser>> GetOrFailAsync(Guid userId, CancellationToken ct)
    {
        var user = await _db.Users
            .FirstOrDefaultAsync(u => u.Id == userId, ct);

        if (user is null)
            return Result<ApplicationUser>.NotFound($"Kullanıcı bulunamadı. (ID: {userId})");

        return Result<ApplicationUser>.Success(user);
    }

    /// <summary>Kullanıcıyı bulur VE aktif olduğunu doğrular.</summary>
    public async Task<Result<ApplicationUser>> GetActiveOrFailAsync(Guid userId, CancellationToken ct)
    {
        var result = await GetOrFailAsync(userId, ct);
        if (result.IsFailure) return result;

        if (!result.Value!.IsActive)
            return Result<ApplicationUser>.Failure("Bu kullanıcı hesabı aktif değildir.", 403);

        return result;
    }

    /// <summary>
    /// E-posta ile kullanıcı arar.
    /// Tenant/Company kullanıcı ekleme akışında kullanılır:
    /// - Bulursa: Mevcut kullanıcıya rol eklenir
    /// - Bulamazsa: Yeni kullanıcı oluşturulur
    /// </summary>
    public async Task<ApplicationUser?> FindByEmailAsync(string email, CancellationToken ct)
    {
        var normalizedEmail = email.Trim().ToLowerInvariant();
        return await _db.Users
            .FirstOrDefaultAsync(u => u.Email == normalizedEmail, ct);
    }

    /// <summary>
    /// E-posta adresinin sistemde benzersiz olduğunu doğrular.
    /// Yeni kullanıcı oluştururken kullanılır.
    /// </summary>
    public async Task<Result<bool>> EnsureEmailUniqueAsync(
        string email, Guid? excludeId = null, CancellationToken ct = default)
    {
        var normalizedEmail = email.Trim().ToLowerInvariant();

        var exists = await _db.Users
            .AnyAsync(u =>
                u.Email == normalizedEmail &&
                (excludeId == null || u.Id != excludeId),
                ct);

        if (exists)
            return Result<bool>.Failure($"'{email}' e-posta adresi zaten kayıtlıdır.");

        return Result<bool>.Success(true);
    }

    /// <summary>
    /// Kullanıcının belirli bir tenant'ta rolü olup olmadığını kontrol eder.
    /// </summary>
    public async Task<bool> HasTenantRoleAsync(Guid userId, Guid tenantId, CancellationToken ct)
    {
        return await _db.UserTenantRoles
            .AnyAsync(utr => utr.UserId == userId && utr.TenantId == tenantId, ct);
    }

    /// <summary>
    /// Kullanıcının belirli bir şirkette rolü veya üyeliği olup olmadığını kontrol eder.
    /// </summary>
    public async Task<bool> HasCompanyAccessAsync(Guid userId, Guid companyId, CancellationToken ct)
    {
        var hasRole = await _db.UserCompanyRoles
            .AnyAsync(ucr => ucr.UserId == userId && ucr.CompanyId == companyId, ct);

        if (hasRole) return true;

        var hasMembership = await _db.UserCompanyMemberships
            .AnyAsync(ucm => ucm.UserId == userId && ucm.CompanyId == companyId && ucm.IsActive, ct);

        return hasMembership;
    }

    /// <summary>
    /// Kullanıcının hesabının kilitli olup olmadığını kontrol eder.
    /// </summary>
    public async Task<Result<bool>> EnsureNotLockedAsync(Guid userId, CancellationToken ct)
    {
        var user = await _db.Users
            .FirstOrDefaultAsync(u => u.Id == userId, ct);

        if (user is null)
            return Result<bool>.NotFound("Kullanıcı bulunamadı.");

        if (user.LockoutEnd.HasValue && user.LockoutEnd > DateTime.UtcNow)
            return Result<bool>.Failure(
                $"Hesap kilitli. Kilit bitiş zamanı: {user.LockoutEnd:yyyy-MM-dd HH:mm} UTC");

        return Result<bool>.Success(true);
    }
}
