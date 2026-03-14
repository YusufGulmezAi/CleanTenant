using CleanTenant.Application.Common.Behaviors;
using CleanTenant.Application.Common.Interfaces;
using CleanTenant.Application.Common.Mappings;
using CleanTenant.Application.Common.Models;
using CleanTenant.Shared.Constants;
using CleanTenant.Shared.DTOs.Common;
using CleanTenant.Shared.DTOs.Users;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace CleanTenant.Application.Features.Users.Queries;

// ============================================================================
// GET ALL USERS (Paginated)
// ============================================================================

[RequirePermission(Permissions.Users.Read)]
public record GetUsersQuery : IRequest<Result<PaginatedResult<UserDto>>>, ICacheableQuery
{
    public int PageNumber { get; init; } = 1;
    public int PageSize { get; init; } = 20;
    public string? Search { get; init; }
    public bool? IsActive { get; init; }

    public string CacheKey => $"users:all:{PageNumber}:{PageSize}:{Search}:{IsActive}";
    public TimeSpan? CacheDuration => TimeSpan.FromMinutes(3);
}

public class GetUsersHandler : IRequestHandler<GetUsersQuery, Result<PaginatedResult<UserDto>>>
{
    private readonly IApplicationDbContext _db;

    public GetUsersHandler(IApplicationDbContext db) => _db = db;

    public async Task<Result<PaginatedResult<UserDto>>> Handle(GetUsersQuery request, CancellationToken ct)
    {
        var query = _db.Users.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(request.Search))
        {
            var search = request.Search.ToLowerInvariant();
            query = query.Where(u =>
                u.Email.ToLower().Contains(search) ||
                u.FullName.ToLower().Contains(search));
        }

        if (request.IsActive.HasValue)
            query = query.Where(u => u.IsActive == request.IsActive.Value);

        var totalCount = await query.CountAsync(ct);

        var items = await query
            .OrderBy(u => u.FullName)
            .Skip((request.PageNumber - 1) * request.PageSize)
            .Take(request.PageSize)
            .ProjectToDto()
            .ToListAsync(ct);

        return Result<PaginatedResult<UserDto>>.Success(
            new PaginatedResult<UserDto>(items, totalCount, request.PageNumber, request.PageSize));
    }
}

// ============================================================================
// GET USER BY ID (with roles)
// ============================================================================

[RequirePermission(Permissions.Users.Read)]
public record GetUserByIdQuery(Guid Id) : IRequest<Result<UserDetailDto>>, ICacheableQuery
{
    public string CacheKey => $"user:{Id}";
    public TimeSpan? CacheDuration => TimeSpan.FromMinutes(5);
}

public class GetUserByIdHandler : IRequestHandler<GetUserByIdQuery, Result<UserDetailDto>>
{
    private readonly IApplicationDbContext _db;

    public GetUserByIdHandler(IApplicationDbContext db) => _db = db;

    public async Task<Result<UserDetailDto>> Handle(GetUserByIdQuery request, CancellationToken ct)
    {
        var user = await _db.Users
            .AsNoTracking()
            .Include(u => u.SystemRoles).ThenInclude(sr => sr.SystemRole)
            .Include(u => u.TenantRoles).ThenInclude(tr => tr.TenantRole)
            .Include(u => u.CompanyRoles).ThenInclude(cr => cr.CompanyRole)
            .Include(u => u.CompanyMemberships)
            .FirstOrDefaultAsync(u => u.Id == request.Id, ct);

        if (user is null)
            return Result<UserDetailDto>.NotFound($"Kullanıcı bulunamadı. (ID: {request.Id})");

        return Result<UserDetailDto>.Success(user.ToDetailDto());
    }
}
