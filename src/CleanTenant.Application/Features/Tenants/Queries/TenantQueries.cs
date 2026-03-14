using CleanTenant.Application.Common.Behaviors;
using CleanTenant.Application.Common.Interfaces;
using CleanTenant.Application.Common.Mappings;
using CleanTenant.Application.Common.Models;
using CleanTenant.Application.Common.Rules;
using CleanTenant.Shared.Constants;
using CleanTenant.Shared.DTOs.Common;
using CleanTenant.Shared.DTOs.Tenants;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace CleanTenant.Application.Features.Tenants.Queries;

// ============================================================================
// GET ALL TENANTS (Paginated)
// ============================================================================

[RequirePermission(Permissions.Tenants.Read)]
public record GetTenantsQuery : IRequest<Result<PaginatedResult<TenantDto>>>, ICacheableQuery
{
    public int PageNumber { get; init; } = 1;
    public int PageSize { get; init; } = 20;
    public string? Search { get; init; }
    public bool? IsActive { get; init; }

    // Cache key parametrelere göre benzersiz olmalı
    public string CacheKey => $"tenants:all:{PageNumber}:{PageSize}:{Search}:{IsActive}";
    public TimeSpan? CacheDuration => TimeSpan.FromMinutes(5);
}

public class GetTenantsHandler : IRequestHandler<GetTenantsQuery, Result<PaginatedResult<TenantDto>>>
{
    private readonly IApplicationDbContext _db;

    public GetTenantsHandler(IApplicationDbContext db)
    {
        _db = db;
    }

    public async Task<Result<PaginatedResult<TenantDto>>> Handle(
        GetTenantsQuery request, CancellationToken ct)
    {
        // IQueryable oluştur — sorgu henüz çalışmadı (deferred execution)
        var query = _db.Tenants.AsNoTracking();

        // Filtreleme
        if (!string.IsNullOrWhiteSpace(request.Search))
        {
            var search = request.Search.ToLowerInvariant();
            query = query.Where(t =>
                t.Name.ToLower().Contains(search) ||
                t.Identifier.Contains(search));
        }

        if (request.IsActive.HasValue)
            query = query.Where(t => t.IsActive == request.IsActive.Value);

        // Toplam kayıt sayısı
        var totalCount = await query.CountAsync(ct);

        // Sayfalama + Projection (sadece gerekli kolonlar çekilir)
        var items = await query
            .OrderBy(t => t.Name)
            .Skip((request.PageNumber - 1) * request.PageSize)
            .Take(request.PageSize)
            .ProjectToDto()  // Custom mapping — IQueryable üzerinde SQL seviyesinde dönüşüm
            .ToListAsync(ct);

        var result = new PaginatedResult<TenantDto>(items, totalCount, request.PageNumber, request.PageSize);

        return Result<PaginatedResult<TenantDto>>.Success(result);
    }
}

// ============================================================================
// GET TENANT BY ID
// ============================================================================

[RequirePermission(Permissions.Tenants.Read)]
public record GetTenantByIdQuery(Guid Id) : IRequest<Result<TenantDetailDto>>, ICacheableQuery
{
    public string CacheKey => $"tenant:{Id}";
    public TimeSpan? CacheDuration => TimeSpan.FromMinutes(10);
}

public class GetTenantByIdHandler : IRequestHandler<GetTenantByIdQuery, Result<TenantDetailDto>>
{
    private readonly IApplicationDbContext _db;
    private readonly TenantRules _tenantRules;

    public GetTenantByIdHandler(IApplicationDbContext db, TenantRules tenantRules)
    {
        _db = db;
        _tenantRules = tenantRules;
    }

    public async Task<Result<TenantDetailDto>> Handle(GetTenantByIdQuery request, CancellationToken ct)
    {
        var detail = await _db.Tenants
            .AsNoTracking()
            .Where(t => t.Id == request.Id)
            .ProjectToDetailDto()
            .FirstOrDefaultAsync(ct);

        if (detail is null)
            return Result<TenantDetailDto>.NotFound($"Tenant bulunamadı. (ID: {request.Id})");

        return Result<TenantDetailDto>.Success(detail);
    }
}
