using CleanTenant.Application.Common.Behaviors;
using CleanTenant.Application.Common.Interfaces;
using CleanTenant.Application.Common.Mappings;
using CleanTenant.Application.Common.Models;
using CleanTenant.Application.Common.Rules;
using CleanTenant.Domain.Tenancy;
using CleanTenant.Shared.Constants;
using CleanTenant.Shared.DTOs.Common;
using CleanTenant.Shared.DTOs.Companies;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace CleanTenant.Application.Features.Companies;

// ============================================================================
// CREATE COMPANY
// ============================================================================

[RequireTenantAccess]
[RequirePermission(Permissions.Companies.Create)]
public record CreateCompanyCommand(CreateCompanyDto Dto) : IRequest<Result<CompanyDto>>, ICacheInvalidator
{
    /// <summary>Aktif tenant ID — middleware tarafından header'dan okunur.</summary>
    public Guid TenantId { get; init; }

    public string[] CacheKeysToInvalidate => [$"tenant:{TenantId}:companies", "companies:count"];
}

public class CreateCompanyValidator : AbstractValidator<CreateCompanyCommand>
{
    public CreateCompanyValidator()
    {
        RuleFor(x => x.TenantId).NotEmpty().WithMessage("Tenant ID zorunludur.");
        RuleFor(x => x.Dto.Name).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Dto.Code).NotEmpty().MinimumLength(2).MaximumLength(50)
            .Matches("^[A-Z0-9-]+$").WithMessage("Şirket kodu sadece büyük harf, rakam ve tire içerebilir.");
    }
}

public class CreateCompanyHandler : IRequestHandler<CreateCompanyCommand, Result<CompanyDto>>
{
    private readonly IApplicationDbContext _db;
    private readonly TenantRules _tenantRules;
    private readonly CompanyRules _companyRules;

    public CreateCompanyHandler(IApplicationDbContext db, TenantRules tenantRules, CompanyRules companyRules)
    {
        _db = db;
        _tenantRules = tenantRules;
        _companyRules = companyRules;
    }

    public async Task<Result<CompanyDto>> Handle(CreateCompanyCommand request, CancellationToken ct)
    {
        // Tenant aktif mi?
        var tenantResult = await _tenantRules.GetActiveOrFailAsync(request.TenantId, ct);
        if (tenantResult.IsFailure)
            return Result<CompanyDto>.Failure(tenantResult.Error!, tenantResult.StatusCode);

        // Kod benzersiz mi (aynı tenant içinde)?
        var codeResult = await _companyRules.EnsureCodeUniqueInTenantAsync(
            request.TenantId, request.Dto.Code, ct: ct);
        if (codeResult.IsFailure)
            return Result<CompanyDto>.Failure(codeResult.Error!, codeResult.StatusCode);

        var company = Company.Create(
            request.TenantId,
            request.Dto.Name,
            request.Dto.Code,
            request.Dto.TaxNumber,
            request.Dto.TaxOffice,
            request.Dto.ContactEmail,
            request.Dto.ContactPhone,
            request.Dto.Address);

        _db.Companies.Add(company);
        await _db.SaveChangesAsync(ct);

        return Result<CompanyDto>.Created(company.ToDto());
    }
}

// ============================================================================
// UPDATE COMPANY
// ============================================================================

[RequireTenantAccess]
[RequirePermission(Permissions.Companies.Update)]
public record UpdateCompanyCommand(Guid Id, UpdateCompanyDto Dto) : IRequest<Result<CompanyDto>>, ICacheInvalidator
{
    public Guid TenantId { get; init; }
    public string[] CacheKeysToInvalidate => [$"tenant:{TenantId}:companies", $"company:{Id}"];
}

public class UpdateCompanyHandler : IRequestHandler<UpdateCompanyCommand, Result<CompanyDto>>
{
    private readonly IApplicationDbContext _db;
    private readonly CompanyRules _companyRules;

    public UpdateCompanyHandler(IApplicationDbContext db, CompanyRules companyRules)
    {
        _db = db;
        _companyRules = companyRules;
    }

    public async Task<Result<CompanyDto>> Handle(UpdateCompanyCommand request, CancellationToken ct)
    {
        var companyResult = await _companyRules.GetInTenantOrFailAsync(request.Id, request.TenantId, ct);
        if (companyResult.IsFailure)
            return Result<CompanyDto>.Failure(companyResult.Error!, companyResult.StatusCode);

        var company = companyResult.Value!;
        company.Update(
            request.Dto.Name,
            request.Dto.TaxNumber,
            request.Dto.TaxOffice,
            request.Dto.ContactEmail,
            request.Dto.ContactPhone,
            request.Dto.Address);

        await _db.SaveChangesAsync(ct);

        return Result<CompanyDto>.Success(company.ToDto());
    }
}

// ============================================================================
// DELETE COMPANY
// ============================================================================

[RequireTenantAccess]
[RequirePermission(Permissions.Companies.Delete)]
public record DeleteCompanyCommand(Guid Id) : IRequest<Result<object>>, ICacheInvalidator
{
    public Guid TenantId { get; init; }
    public string[] CacheKeysToInvalidate => [$"tenant:{TenantId}:companies", $"company:{Id}"];
}

public class DeleteCompanyHandler : IRequestHandler<DeleteCompanyCommand, Result<object>>
{
    private readonly IApplicationDbContext _db;
    private readonly CompanyRules _companyRules;

    public DeleteCompanyHandler(IApplicationDbContext db, CompanyRules companyRules)
    {
        _db = db;
        _companyRules = companyRules;
    }

    public async Task<Result<object>> Handle(DeleteCompanyCommand request, CancellationToken ct)
    {
        var companyResult = await _companyRules.GetInTenantOrFailAsync(request.Id, request.TenantId, ct);
        if (companyResult.IsFailure)
            return Result<object>.Failure(companyResult.Error!, companyResult.StatusCode);

        var depsResult = await _companyRules.EnsureNoDependenciesAsync(request.Id, ct);
        if (depsResult.IsFailure)
            return Result<object>.Failure(depsResult.Error!, depsResult.StatusCode);

        _db.Companies.Remove(companyResult.Value!);
        await _db.SaveChangesAsync(ct);

        return Result.NoContent();
    }
}

// ============================================================================
// GET COMPANIES (Paginated, Tenant-scoped)
// ============================================================================

[RequireTenantAccess]
[RequirePermission(Permissions.Companies.Read)]
public record GetCompaniesQuery : IRequest<Result<PaginatedResult<CompanyDto>>>, ICacheableQuery
{
    public Guid TenantId { get; init; }
    public int PageNumber { get; init; } = 1;
    public int PageSize { get; init; } = 20;
    public string? Search { get; init; }

    public string CacheKey => $"tenant:{TenantId}:companies:{PageNumber}:{PageSize}:{Search}";
    public TimeSpan? CacheDuration => TimeSpan.FromMinutes(5);
}

public class GetCompaniesHandler : IRequestHandler<GetCompaniesQuery, Result<PaginatedResult<CompanyDto>>>
{
    private readonly IApplicationDbContext _db;

    public GetCompaniesHandler(IApplicationDbContext db) => _db = db;

    public async Task<Result<PaginatedResult<CompanyDto>>> Handle(GetCompaniesQuery request, CancellationToken ct)
    {
        var query = _db.Companies
            .AsNoTracking()
            .Where(c => c.TenantId == request.TenantId);

        if (!string.IsNullOrWhiteSpace(request.Search))
        {
            var search = request.Search.ToLowerInvariant();
            query = query.Where(c =>
                c.Name.ToLower().Contains(search) ||
                c.Code.ToLower().Contains(search));
        }

        var totalCount = await query.CountAsync(ct);

        var items = await query
            .OrderBy(c => c.Name)
            .Skip((request.PageNumber - 1) * request.PageSize)
            .Take(request.PageSize)
            .ProjectToDto()
            .ToListAsync(ct);

        return Result<PaginatedResult<CompanyDto>>.Success(
            new PaginatedResult<CompanyDto>(items, totalCount, request.PageNumber, request.PageSize));
    }
}

// ============================================================================
// GET COMPANY BY ID
// ============================================================================

[RequireTenantAccess]
[RequirePermission(Permissions.Companies.Read)]
public record GetCompanyByIdQuery(Guid Id) : IRequest<Result<CompanyDetailDto>>, ICacheableQuery
{
    public Guid TenantId { get; init; }
    public string CacheKey => $"company:{Id}";
    public TimeSpan? CacheDuration => TimeSpan.FromMinutes(10);
}

public class GetCompanyByIdHandler : IRequestHandler<GetCompanyByIdQuery, Result<CompanyDetailDto>>
{
    private readonly IApplicationDbContext _db;

    public GetCompanyByIdHandler(IApplicationDbContext db) => _db = db;

    public async Task<Result<CompanyDetailDto>> Handle(GetCompanyByIdQuery request, CancellationToken ct)
    {
        var detail = await _db.Companies
            .AsNoTracking()
            .Where(c => c.Id == request.Id && c.TenantId == request.TenantId)
            .ProjectToDetailDto()
            .FirstOrDefaultAsync(ct);

        if (detail is null)
            return Result<CompanyDetailDto>.NotFound($"Şirket bulunamadı. (ID: {request.Id})");

        return Result<CompanyDetailDto>.Success(detail);
    }
}
