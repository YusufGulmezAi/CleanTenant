using CleanTenant.Application.Common.Behaviors;
using CleanTenant.Application.Common.Interfaces;
using CleanTenant.Application.Common.Mappings;
using CleanTenant.Application.Common.Models;
using CleanTenant.Application.Common.Rules;
using CleanTenant.Domain.Tenancy;
using CleanTenant.Shared.Constants;
using CleanTenant.Shared.DTOs.Tenants;
using FluentValidation;
using MediatR;

namespace CleanTenant.Application.Features.Tenants.Commands;

// ============================================================================
// CREATE TENANT
// ============================================================================

/// <summary>Yeni tenant oluşturma komutu.</summary>
[RequirePermission(Permissions.Tenants.Create)]
public record CreateTenantCommand(CreateTenantDto Dto) : IRequest<Result<TenantDto>>, ICacheInvalidator
{
    public string[] CacheKeysToInvalidate => ["tenants:all", "tenants:count"];
}

/// <summary>CreateTenantCommand doğrulama kuralları.</summary>
public class CreateTenantValidator : AbstractValidator<CreateTenantCommand>
{
    public CreateTenantValidator()
    {
        RuleFor(x => x.Dto.Name)
            .NotEmpty().WithMessage("Tenant adı zorunludur.")
            .MaximumLength(200).WithMessage("Tenant adı en fazla 200 karakter olabilir.");

        RuleFor(x => x.Dto.Identifier)
            .NotEmpty().WithMessage("Tanımlayıcı (identifier) zorunludur.")
            .MinimumLength(3).WithMessage("Tanımlayıcı en az 3 karakter olmalıdır.")
            .MaximumLength(100).WithMessage("Tanımlayıcı en fazla 100 karakter olabilir.")
            .Matches("^[a-z0-9-]+$").WithMessage("Tanımlayıcı sadece küçük harf, rakam ve tire içerebilir.");

        RuleFor(x => x.Dto.ContactEmail)
            .EmailAddress().When(x => !string.IsNullOrEmpty(x.Dto.ContactEmail))
            .WithMessage("Geçerli bir e-posta adresi giriniz.");
    }
}

/// <summary>
/// CreateTenantCommand handler'ı.
/// Business Rules ile kontrol, Entity factory method ile oluşturma, Mapping ile DTO dönüşümü.
/// Handler SADECE orkestrasyon yapar — iş kuralı ve mapping dışarıda.
/// </summary>
public class CreateTenantHandler : IRequestHandler<CreateTenantCommand, Result<TenantDto>>
{
    private readonly IApplicationDbContext _db;
    private readonly TenantRules _tenantRules;

    public CreateTenantHandler(IApplicationDbContext db, TenantRules tenantRules)
    {
        _db = db;
        _tenantRules = tenantRules;
    }

    public async Task<Result<TenantDto>> Handle(CreateTenantCommand request, CancellationToken ct)
    {
        // İş kuralı: Identifier benzersiz mi?
        var uniqueResult = await _tenantRules.EnsureIdentifierUniqueAsync(request.Dto.Identifier, ct: ct);
        if (uniqueResult.IsFailure)
            return Result<TenantDto>.Failure(uniqueResult.Error!, uniqueResult.StatusCode);

        // Entity oluştur (Factory Method — domain event tetiklenir)
        var tenant = Tenant.Create(
            request.Dto.Name,
            request.Dto.Identifier,
            request.Dto.TaxNumber,
            request.Dto.ContactEmail,
            request.Dto.ContactPhone);

        // Veritabanına kaydet
        _db.Tenants.Add(tenant);
        await _db.SaveChangesAsync(ct);

        // DTO'ya dönüştür ve dön
        return Result<TenantDto>.Created(tenant.ToDto());
    }
}

// ============================================================================
// UPDATE TENANT
// ============================================================================

[RequirePermission(Permissions.Tenants.Update)]
public record UpdateTenantCommand(Guid Id, UpdateTenantDto Dto) : IRequest<Result<TenantDto>>, ICacheInvalidator
{
    public string[] CacheKeysToInvalidate => ["tenants:all", $"tenant:{Id}"];
}

public class UpdateTenantValidator : AbstractValidator<UpdateTenantCommand>
{
    public UpdateTenantValidator()
    {
        RuleFor(x => x.Id).NotEmpty().WithMessage("Tenant ID zorunludur.");
        RuleFor(x => x.Dto.Name).NotEmpty().MaximumLength(200);
    }
}

public class UpdateTenantHandler : IRequestHandler<UpdateTenantCommand, Result<TenantDto>>
{
    private readonly IApplicationDbContext _db;
    private readonly TenantRules _tenantRules;

    public UpdateTenantHandler(IApplicationDbContext db, TenantRules tenantRules)
    {
        _db = db;
        _tenantRules = tenantRules;
    }

    public async Task<Result<TenantDto>> Handle(UpdateTenantCommand request, CancellationToken ct)
    {
        // İş kuralı: Tenant var mı ve aktif mi?
        var tenantResult = await _tenantRules.GetActiveOrFailAsync(request.Id, ct);
        if (tenantResult.IsFailure)
            return Result<TenantDto>.Failure(tenantResult.Error!, tenantResult.StatusCode);

        var tenant = tenantResult.Value!;

        // Domain method ile güncelle
        tenant.Update(
            request.Dto.Name,
            request.Dto.TaxNumber,
            request.Dto.ContactEmail,
            request.Dto.ContactPhone);

        await _db.SaveChangesAsync(ct);

        return Result<TenantDto>.Success(tenant.ToDto());
    }
}

// ============================================================================
// DELETE TENANT (Soft Delete)
// ============================================================================

[RequirePermission(Permissions.Tenants.Delete)]
public record DeleteTenantCommand(Guid Id) : IRequest<Result<object>>, ICacheInvalidator
{
    public string[] CacheKeysToInvalidate => ["tenants:all", $"tenant:{Id}"];
}

public class DeleteTenantHandler : IRequestHandler<DeleteTenantCommand, Result<object>>
{
    private readonly IApplicationDbContext _db;
    private readonly TenantRules _tenantRules;

    public DeleteTenantHandler(IApplicationDbContext db, TenantRules tenantRules)
    {
        _db = db;
        _tenantRules = tenantRules;
    }

    public async Task<Result<object>> Handle(DeleteTenantCommand request, CancellationToken ct)
    {
        var tenantResult = await _tenantRules.GetOrFailAsync(request.Id, ct);
        if (tenantResult.IsFailure)
            return Result<object>.Failure(tenantResult.Error!, tenantResult.StatusCode);

        // İş kuralı: Alt şirketler var mı?
        var depsResult = await _tenantRules.EnsureNoDependenciesAsync(request.Id, ct);
        if (depsResult.IsFailure)
            return Result<object>.Failure(depsResult.Error!, depsResult.StatusCode);

        // Soft delete — SoftDeleteInterceptor otomatik IsDeleted = true yapar
        _db.Tenants.Remove(tenantResult.Value!);
        await _db.SaveChangesAsync(ct);

        return Result.NoContent();
    }
}
