using CleanTenant.Application.Common.Interfaces;
using CleanTenant.Application.Common.Models;
using CleanTenant.Shared.DTOs.Auth;
using CleanTenant.Shared.DTOs.Users;
using CleanTenant.Shared.Constants;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace CleanTenant.Application.Features.Auth.Queries;

// ============================================================================
// GET CURRENT USER — Login sonrası "ben kimim?" sorgusu
// ============================================================================

public record GetCurrentUserQuery : IRequest<Result<UserDto>>;

public class GetCurrentUserHandler : IRequestHandler<GetCurrentUserQuery, Result<UserDto>>
{
    private readonly IApplicationDbContext _db;
    private readonly ICurrentUserService _currentUser;

    public GetCurrentUserHandler(IApplicationDbContext db, ICurrentUserService currentUser)
    {
        _db = db;
        _currentUser = currentUser;
    }

    public async Task<Result<UserDto>> Handle(GetCurrentUserQuery request, CancellationToken ct)
    {
        if (_currentUser.UserId is null)
            return Result<UserDto>.Unauthorized();

        var user = await _db.Users
            .AsNoTracking()
            .Where(u => u.Id == _currentUser.UserId)
            .Select(u => new UserDto
            {
                Id = u.Id,
                Email = u.Email,
                FullName = u.FullName,
                PhoneNumber = u.PhoneNumber,
                IsActive = u.IsActive,
                EmailConfirmed = u.EmailConfirmed,
                TwoFactorEnabled = u.TwoFactorEnabled,
                CreatedAt = u.CreatedAt,
                LastLoginAt = u.LastLoginAt,
                AvatarUrl = u.AvatarUrl
            })
            .FirstOrDefaultAsync(ct);

        if (user is null)
            return Result<UserDto>.NotFound("Kullanıcı bulunamadı.");

        return Result<UserDto>.Success(user);
    }
}

// ============================================================================
// GET USER CONTEXT — Kullanıcının erişebildiği tüm tenant/şirket bağlamları
// ============================================================================

/// <summary>
/// Login sonrası kullanıcının hangi tenant ve şirketlere erişebildiğini döner.
/// UI'da Context Switching dropdown'ını doldurmak için kullanılır.
/// </summary>
public record GetUserContextQuery : IRequest<Result<UserContextDto>>;

public class GetUserContextHandler : IRequestHandler<GetUserContextQuery, Result<UserContextDto>>
{
    private readonly IApplicationDbContext _db;
    private readonly ICurrentUserService _currentUser;

    public GetUserContextHandler(IApplicationDbContext db, ICurrentUserService currentUser)
    {
        _db = db;
        _currentUser = currentUser;
    }

    public async Task<Result<UserContextDto>> Handle(GetUserContextQuery request, CancellationToken ct)
    {
        if (_currentUser.UserId is null)
            return Result<UserContextDto>.Unauthorized();

        var userId = _currentUser.UserId.Value;

        // Kullanıcı temel bilgileri
        var user = await _db.Users
            .AsNoTracking()
            .FirstOrDefaultAsync(u => u.Id == userId, ct);

        if (user is null)
            return Result<UserContextDto>.NotFound("Kullanıcı bulunamadı.");

        // Sistem rolleri
        var isSuperAdmin = await _db.UserSystemRoles.AnyAsync(usr =>
            usr.UserId == userId && usr.SystemRole.Name == SystemRoles.SuperAdmin, ct);

        var isSystemUser = await _db.UserSystemRoles.AnyAsync(usr =>
            usr.UserId == userId, ct);

        // Tenant rolleri ile erişilebilir tenant'lar
        var tenantRoles = await _db.UserTenantRoles
            .AsNoTracking()
            .Where(utr => utr.UserId == userId)
            .Select(utr => new
            {
                utr.TenantId,
                TenantName = _db.Tenants
                    .Where(t => t.Id == utr.TenantId)
                    .Select(t => t.Name)
                    .FirstOrDefault() ?? "",
                RoleName = utr.TenantRole.Name
            })
            .ToListAsync(ct);

        // Şirket rolleri
        var companyRoles = await _db.UserCompanyRoles
            .AsNoTracking()
            .Where(ucr => ucr.UserId == userId)
            .Select(ucr => new
            {
                ucr.CompanyId,
                CompanyName = _db.Companies
                    .Where(c => c.Id == ucr.CompanyId)
                    .Select(c => c.Name)
                    .FirstOrDefault() ?? "",
                CompanyCode = _db.Companies
                    .Where(c => c.Id == ucr.CompanyId)
                    .Select(c => c.Code)
                    .FirstOrDefault() ?? "",
                TenantId = _db.Companies
                    .Where(c => c.Id == ucr.CompanyId)
                    .Select(c => c.TenantId)
                    .FirstOrDefault(),
                RoleName = ucr.CompanyRole.Name
            })
            .ToListAsync(ct);

        // Şirket üyelikleri
        var memberships = await _db.UserCompanyMemberships
            .AsNoTracking()
            .Where(ucm => ucm.UserId == userId && ucm.IsActive)
            .Select(ucm => new
            {
                ucm.CompanyId,
                CompanyName = _db.Companies
                    .Where(c => c.Id == ucm.CompanyId)
                    .Select(c => c.Name)
                    .FirstOrDefault() ?? "",
                CompanyCode = _db.Companies
                    .Where(c => c.Id == ucm.CompanyId)
                    .Select(c => c.Code)
                    .FirstOrDefault() ?? "",
                TenantId = _db.Companies
                    .Where(c => c.Id == ucm.CompanyId)
                    .Select(c => c.TenantId)
                    .FirstOrDefault()
            })
            .ToListAsync(ct);

        // SuperAdmin veya SystemUser ise tüm tenant'ları görebilir
        var availableTenants = new List<UserContextTenantDto>();

        if (isSuperAdmin || isSystemUser)
        {
            var allTenants = await _db.Tenants
                .AsNoTracking()
                .Where(t => t.IsActive)
                .Select(t => new { t.Id, t.Name })
                .ToListAsync(ct);

            foreach (var t in allTenants)
            {
                var companies = await _db.Companies
                    .AsNoTracking()
                    .Where(c => c.TenantId == t.Id && c.IsActive)
                    .Select(c => new UserContextCompanyDto
                    {
                        CompanyId = c.Id,
                        CompanyName = c.Name,
                        CompanyCode = c.Code,
                        RoleName = isSuperAdmin ? "SuperAdmin" : "SystemUser",
                        IsCompanyAdmin = isSuperAdmin,
                        IsMember = false
                    })
                    .ToListAsync(ct);

                availableTenants.Add(new UserContextTenantDto
                {
                    TenantId = t.Id,
                    TenantName = t.Name,
                    RoleName = isSuperAdmin ? "SuperAdmin" : "SystemUser",
                    IsTenantAdmin = isSuperAdmin,
                    AvailableCompanies = companies
                });
            }
        }
        else
        {
            // Normal kullanıcı — sadece atanmış tenant'lar
            var tenantIds = tenantRoles.Select(tr => tr.TenantId)
                .Union(companyRoles.Select(cr => cr.TenantId))
                .Union(memberships.Select(m => m.TenantId))
                .Distinct();

            foreach (var tenantId in tenantIds)
            {
                var tenantRole = tenantRoles.FirstOrDefault(tr => tr.TenantId == tenantId);
                var tenantCompanyRoles = companyRoles.Where(cr => cr.TenantId == tenantId);
                var tenantMemberships = memberships.Where(m => m.TenantId == tenantId);

                var companies = new List<UserContextCompanyDto>();

                // Tenant kullanıcısıysa tüm şirketleri görebilir
                if (tenantRole is not null)
                {
                    var allCompanies = await _db.Companies
                        .AsNoTracking()
                        .Where(c => c.TenantId == tenantId && c.IsActive)
                        .Select(c => new UserContextCompanyDto
                        {
                            CompanyId = c.Id,
                            CompanyName = c.Name,
                            CompanyCode = c.Code,
                            RoleName = tenantRole.RoleName,
                            IsCompanyAdmin = false,
                            IsMember = false
                        })
                        .ToListAsync(ct);
                    companies.AddRange(allCompanies);
                }
                else
                {
                    // Sadece atanmış şirketler
                    foreach (var cr in tenantCompanyRoles)
                    {
                        companies.Add(new UserContextCompanyDto
                        {
                            CompanyId = cr.CompanyId,
                            CompanyName = cr.CompanyName,
                            CompanyCode = cr.CompanyCode,
                            RoleName = cr.RoleName,
                            IsCompanyAdmin = false,
                            IsMember = false
                        });
                    }

                    foreach (var m in tenantMemberships.Where(m =>
                        !companies.Any(c => c.CompanyId == m.CompanyId)))
                    {
                        companies.Add(new UserContextCompanyDto
                        {
                            CompanyId = m.CompanyId,
                            CompanyName = m.CompanyName,
                            CompanyCode = m.CompanyCode,
                            RoleName = "Member",
                            IsCompanyAdmin = false,
                            IsMember = true
                        });
                    }
                }

                availableTenants.Add(new UserContextTenantDto
                {
                    TenantId = tenantId,
                    TenantName = tenantRole?.TenantName ?? companies.FirstOrDefault()?.CompanyName ?? "",
                    RoleName = tenantRole?.RoleName ?? "CompanyUser",
                    IsTenantAdmin = false,
                    AvailableCompanies = companies
                });
            }
        }

        return Result<UserContextDto>.Success(new UserContextDto
        {
            UserId = userId,
            Email = user.Email,
            FullName = user.FullName,
            IsSuperAdmin = isSuperAdmin,
            IsSystemUser = isSystemUser,
            AvailableTenants = availableTenants
        });
    }
}
