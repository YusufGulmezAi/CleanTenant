using CleanTenant.Application.Common.Behaviors;
using CleanTenant.Application.Common.Interfaces;
using CleanTenant.Application.Common.Models;
using CleanTenant.Domain.Identity;
using CleanTenant.Shared.Constants;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace CleanTenant.Application.Features.Roles;

// ============================================================================
// CREATE TENANT ROLE
// ============================================================================

[RequireTenantAccess]
[RequirePermission(Permissions.Roles.Create)]
public record CreateTenantRoleCommand : IRequest<Result<TenantRoleDto>>, ICacheInvalidator
{
    public Guid TenantId { get; init; }
    public string Name { get; init; } = default!;
    public string? Description { get; init; }
    public string PermissionList { get; init; } = "[]";
    public string[] CacheKeysToInvalidate => [$"tenant:{TenantId}:roles"];
}

public class CreateTenantRoleValidator : AbstractValidator<CreateTenantRoleCommand>
{
    public CreateTenantRoleValidator()
    {
        RuleFor(x => x.TenantId).NotEmpty();
        RuleFor(x => x.Name).NotEmpty().MaximumLength(100);
    }
}

public class CreateTenantRoleHandler : IRequestHandler<CreateTenantRoleCommand, Result<TenantRoleDto>>
{
    private readonly IApplicationDbContext _db;

    public CreateTenantRoleHandler(IApplicationDbContext db) => _db = db;

    public async Task<Result<TenantRoleDto>> Handle(CreateTenantRoleCommand request, CancellationToken ct)
    {
        // Aynı isimde rol var mı kontrolü
        var exists = await _db.TenantRoles.AnyAsync(r =>
            r.TenantId == request.TenantId && r.Name == request.Name.Trim(), ct);

        if (exists)
            return Result<TenantRoleDto>.Failure($"'{request.Name}' adında bir rol zaten mevcut.");

        var role = TenantRole.Create(request.TenantId, request.Name, request.Description, request.PermissionList);
        _db.TenantRoles.Add(role);
        await _db.SaveChangesAsync(ct);

        return Result<TenantRoleDto>.Created(new TenantRoleDto
        {
            Id = role.Id, TenantId = role.TenantId,
            Name = role.Name, Description = role.Description,
            Permissions = role.Permissions, IsActive = role.IsActive
        });
    }
}

// ============================================================================
// CREATE COMPANY ROLE
// ============================================================================

[RequireCompanyAccess]
[RequirePermission(Permissions.Roles.Create)]
public record CreateCompanyRoleCommand : IRequest<Result<CompanyRoleDto>>, ICacheInvalidator
{
    public Guid TenantId { get; init; }
    public Guid CompanyId { get; init; }
    public string Name { get; init; } = default!;
    public string? Description { get; init; }
    public string PermissionList { get; init; } = "[]";
    public string[] CacheKeysToInvalidate => [$"company:{CompanyId}:roles"];
}

public class CreateCompanyRoleHandler : IRequestHandler<CreateCompanyRoleCommand, Result<CompanyRoleDto>>
{
    private readonly IApplicationDbContext _db;

    public CreateCompanyRoleHandler(IApplicationDbContext db) => _db = db;

    public async Task<Result<CompanyRoleDto>> Handle(CreateCompanyRoleCommand request, CancellationToken ct)
    {
        var exists = await _db.CompanyRoles.AnyAsync(r =>
            r.CompanyId == request.CompanyId && r.Name == request.Name.Trim(), ct);

        if (exists)
            return Result<CompanyRoleDto>.Failure($"'{request.Name}' adında bir rol zaten mevcut.");

        var role = CompanyRole.Create(
            request.TenantId, request.CompanyId, request.Name, request.Description, request.PermissionList);
        _db.CompanyRoles.Add(role);
        await _db.SaveChangesAsync(ct);

        return Result<CompanyRoleDto>.Created(new CompanyRoleDto
        {
            Id = role.Id, CompanyId = role.CompanyId,
            Name = role.Name, Description = role.Description,
            Permissions = role.Permissions, IsActive = role.IsActive
        });
    }
}

// ============================================================================
// ASSIGN ROLE TO USER — Kullanıcıya tenant/company rolü ata
// ============================================================================

[RequirePermission(Permissions.Roles.Assign)]
public record AssignTenantRoleCommand : IRequest<Result<object>>, ICacheInvalidator
{
    public Guid UserId { get; init; }
    public Guid TenantId { get; init; }
    public Guid TenantRoleId { get; init; }
    public string[] CacheKeysToInvalidate => [$"user:{UserId}", $"ct:user:{UserId}:roles", $"ct:user:{UserId}:permissions"];
}

public class AssignTenantRoleHandler : IRequestHandler<AssignTenantRoleCommand, Result<object>>
{
    private readonly IApplicationDbContext _db;
    private readonly ICurrentUserService _currentUser;
    private readonly ICacheService _cache;
    private readonly ILogger<AssignTenantRoleHandler> _logger;

    public AssignTenantRoleHandler(
        IApplicationDbContext db, ICurrentUserService currentUser,
        ICacheService cache, ILogger<AssignTenantRoleHandler> logger)
    {
        _db = db; _currentUser = currentUser; _cache = cache; _logger = logger;
    }

    public async Task<Result<object>> Handle(AssignTenantRoleCommand request, CancellationToken ct)
    {
        // Kullanıcı var mı?
        var userExists = await _db.Users.AnyAsync(u => u.Id == request.UserId, ct);
        if (!userExists)
            return Result<object>.NotFound("Kullanıcı bulunamadı.");

        // Rol var mı?
        var roleExists = await _db.TenantRoles.AnyAsync(r =>
            r.Id == request.TenantRoleId && r.TenantId == request.TenantId, ct);
        if (!roleExists)
            return Result<object>.NotFound("Tenant rolü bulunamadı.");

        // Zaten atanmış mı?
        var alreadyAssigned = await _db.UserTenantRoles.AnyAsync(utr =>
            utr.UserId == request.UserId &&
            utr.TenantId == request.TenantId &&
            utr.TenantRoleId == request.TenantRoleId, ct);

        if (alreadyAssigned)
            return Result<object>.Failure("Bu rol zaten kullanıcıya atanmış.");

        var assignment = new UserTenantRole
        {
            Id = Guid.CreateVersion7(),
            UserId = request.UserId,
            TenantId = request.TenantId,
            TenantRoleId = request.TenantRoleId,
            AssignedBy = _currentUser.UserId?.ToString() ?? "SYSTEM",
            AssignedAt = DateTime.UtcNow
        };

        _db.UserTenantRoles.Add(assignment);
        await _db.SaveChangesAsync(ct);

        // Cache'teki kullanıcı rol/izin bilgilerini temizle — yeniden hesaplanacak
        await _cache.RemoveAsync(CacheKeys.UserRoles(request.UserId), ct);
        await _cache.RemoveAsync(CacheKeys.UserPermissions(request.UserId), ct);

        _logger.LogInformation("[ROLE] Tenant rolü atandı: User={UserId}, Role={RoleId}",
            request.UserId, request.TenantRoleId);

        return Result.Success();
    }
}

// ============================================================================
// ASSIGN COMPANY ROLE
// ============================================================================

[RequirePermission(Permissions.Roles.Assign)]
public record AssignCompanyRoleCommand : IRequest<Result<object>>, ICacheInvalidator
{
    public Guid UserId { get; init; }
    public Guid CompanyId { get; init; }
    public Guid CompanyRoleId { get; init; }
    public string[] CacheKeysToInvalidate => [$"user:{UserId}", $"ct:user:{UserId}:roles", $"ct:user:{UserId}:permissions"];
}

public class AssignCompanyRoleHandler : IRequestHandler<AssignCompanyRoleCommand, Result<object>>
{
    private readonly IApplicationDbContext _db;
    private readonly ICurrentUserService _currentUser;
    private readonly ICacheService _cache;

    public AssignCompanyRoleHandler(IApplicationDbContext db, ICurrentUserService currentUser, ICacheService cache)
    {
        _db = db; _currentUser = currentUser; _cache = cache;
    }

    public async Task<Result<object>> Handle(AssignCompanyRoleCommand request, CancellationToken ct)
    {
        var userExists = await _db.Users.AnyAsync(u => u.Id == request.UserId, ct);
        if (!userExists) return Result<object>.NotFound("Kullanıcı bulunamadı.");

        var roleExists = await _db.CompanyRoles.AnyAsync(r =>
            r.Id == request.CompanyRoleId && r.CompanyId == request.CompanyId, ct);
        if (!roleExists) return Result<object>.NotFound("Şirket rolü bulunamadı.");

        var alreadyAssigned = await _db.UserCompanyRoles.AnyAsync(ucr =>
            ucr.UserId == request.UserId &&
            ucr.CompanyId == request.CompanyId &&
            ucr.CompanyRoleId == request.CompanyRoleId, ct);
        if (alreadyAssigned) return Result<object>.Failure("Bu rol zaten kullanıcıya atanmış.");

        _db.UserCompanyRoles.Add(new UserCompanyRole
        {
            Id = Guid.CreateVersion7(),
            UserId = request.UserId,
            CompanyId = request.CompanyId,
            CompanyRoleId = request.CompanyRoleId,
            AssignedBy = _currentUser.UserId?.ToString() ?? "SYSTEM",
            AssignedAt = DateTime.UtcNow
        });

        await _db.SaveChangesAsync(ct);
        await _cache.RemoveAsync(CacheKeys.UserRoles(request.UserId), ct);
        await _cache.RemoveAsync(CacheKeys.UserPermissions(request.UserId), ct);

        return Result.Success();
    }
}

// ============================================================================
// GET TENANT ROLES
// ============================================================================

[RequireTenantAccess]
[RequirePermission(Permissions.Roles.Read)]
public record GetTenantRolesQuery(Guid TenantId) : IRequest<Result<List<TenantRoleDto>>>, ICacheableQuery
{
    public string CacheKey => $"tenant:{TenantId}:roles";
    public TimeSpan? CacheDuration => TimeSpan.FromMinutes(10);
}

public class GetTenantRolesHandler : IRequestHandler<GetTenantRolesQuery, Result<List<TenantRoleDto>>>
{
    private readonly IApplicationDbContext _db;

    public GetTenantRolesHandler(IApplicationDbContext db) => _db = db;

    public async Task<Result<List<TenantRoleDto>>> Handle(GetTenantRolesQuery request, CancellationToken ct)
    {
        var roles = await _db.TenantRoles
            .AsNoTracking()
            .Where(r => r.TenantId == request.TenantId && r.IsActive)
            .Select(r => new TenantRoleDto
            {
                Id = r.Id, TenantId = r.TenantId,
                Name = r.Name, Description = r.Description,
                Permissions = r.Permissions, IsActive = r.IsActive
            })
            .OrderBy(r => r.Name)
            .ToListAsync(ct);

        return Result<List<TenantRoleDto>>.Success(roles);
    }
}

// ============================================================================
// GET COMPANY ROLES
// ============================================================================

[RequireCompanyAccess]
[RequirePermission(Permissions.Roles.Read)]
public record GetCompanyRolesQuery(Guid CompanyId) : IRequest<Result<List<CompanyRoleDto>>>, ICacheableQuery
{
    public Guid TenantId { get; init; }
    public string CacheKey => $"company:{CompanyId}:roles";
    public TimeSpan? CacheDuration => TimeSpan.FromMinutes(10);
}

public class GetCompanyRolesHandler : IRequestHandler<GetCompanyRolesQuery, Result<List<CompanyRoleDto>>>
{
    private readonly IApplicationDbContext _db;

    public GetCompanyRolesHandler(IApplicationDbContext db) => _db = db;

    public async Task<Result<List<CompanyRoleDto>>> Handle(GetCompanyRolesQuery request, CancellationToken ct)
    {
        var roles = await _db.CompanyRoles
            .AsNoTracking()
            .Where(r => r.CompanyId == request.CompanyId && r.IsActive)
            .Select(r => new CompanyRoleDto
            {
                Id = r.Id, CompanyId = r.CompanyId,
                Name = r.Name, Description = r.Description,
                Permissions = r.Permissions, IsActive = r.IsActive
            })
            .OrderBy(r => r.Name)
            .ToListAsync(ct);

        return Result<List<CompanyRoleDto>>.Success(roles);
    }
}

// ============================================================================
// ROLE DTOs (Feature-specific — Shared katmanına da taşınabilir)
// ============================================================================

public class TenantRoleDto
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public string Name { get; set; } = default!;
    public string? Description { get; set; }
    public string Permissions { get; set; } = "[]";
    public bool IsActive { get; set; }
}

public class CompanyRoleDto
{
    public Guid Id { get; set; }
    public Guid CompanyId { get; set; }
    public string Name { get; set; } = default!;
    public string? Description { get; set; }
    public string Permissions { get; set; } = "[]";
    public bool IsActive { get; set; }
}
