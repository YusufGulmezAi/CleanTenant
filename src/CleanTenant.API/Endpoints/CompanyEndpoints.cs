using CleanTenant.API.Extensions;
using CleanTenant.Application.Features.Companies;
using CleanTenant.Shared.DTOs.Companies;
using MediatR;

namespace CleanTenant.API.Endpoints;

/// <summary>
/// Şirket yönetimi Minimal API endpoint'leri.
/// Tüm endpoint'ler tenant bağlamında çalışır (X-Tenant-Id header zorunlu).
/// </summary>
public static class CompanyEndpoints
{
    public static void MapCompanyEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/companies")
            .WithTags("Companies")
            .RequireAuthorization();

        group.MapGet("/", GetAll).WithSummary("Aktif tenant'ın şirketlerini listeler");
        group.MapGet("/{id:guid}", GetById).WithSummary("Şirket detayını getirir");
        group.MapPost("/", Create).WithSummary("Yeni şirket oluşturur");
        group.MapPut("/{id:guid}", Update).WithSummary("Şirket bilgilerini günceller");
        group.MapDelete("/{id:guid}", Delete).WithSummary("Şirketi siler (soft delete)");
    }

    private static async Task<IResult> GetAll(
        HttpContext context, ISender sender,
        int pageNumber = 1, int pageSize = 20, string? search = null,
        CancellationToken ct = default)
    {
        var tenantId = GetTenantId(context);
        if (tenantId is null)
            return Results.BadRequest(new { message = "X-Tenant-Id header zorunludur." });

        var query = new GetCompaniesQuery
        {
            TenantId = tenantId.Value,
            PageNumber = pageNumber,
            PageSize = pageSize,
            Search = search
        };

        var result = await sender.Send(query, ct);
        return result.ToApiResponse();
    }

    private static async Task<IResult> GetById(
        Guid id, HttpContext context, ISender sender, CancellationToken ct)
    {
        var tenantId = GetTenantId(context);
        if (tenantId is null)
            return Results.BadRequest(new { message = "X-Tenant-Id header zorunludur." });

        var query = new GetCompanyByIdQuery(id) { TenantId = tenantId.Value };
        var result = await sender.Send(query, ct);
        return result.ToApiResponse();
    }

    private static async Task<IResult> Create(
        CreateCompanyDto dto, HttpContext context, ISender sender, CancellationToken ct)
    {
        var tenantId = GetTenantId(context);
        if (tenantId is null)
            return Results.BadRequest(new { message = "X-Tenant-Id header zorunludur." });

        var command = new CreateCompanyCommand(dto) { TenantId = tenantId.Value };
        var result = await sender.Send(command, ct);
        return result.ToApiResponse();
    }

    private static async Task<IResult> Update(
        Guid id, UpdateCompanyDto dto, HttpContext context, ISender sender, CancellationToken ct)
    {
        var tenantId = GetTenantId(context);
        if (tenantId is null)
            return Results.BadRequest(new { message = "X-Tenant-Id header zorunludur." });

        var command = new UpdateCompanyCommand(id, dto) { TenantId = tenantId.Value };
        var result = await sender.Send(command, ct);
        return result.ToApiResponse();
    }

    private static async Task<IResult> Delete(
        Guid id, HttpContext context, ISender sender, CancellationToken ct)
    {
        var tenantId = GetTenantId(context);
        if (tenantId is null)
            return Results.BadRequest(new { message = "X-Tenant-Id header zorunludur." });

        var command = new DeleteCompanyCommand(id) { TenantId = tenantId.Value };
        var result = await sender.Send(command, ct);
        return result.ToApiResponse();
    }

    /// <summary>X-Tenant-Id header'ından TenantId çıkarır.</summary>
    private static Guid? GetTenantId(HttpContext context)
    {
        var header = context.Request.Headers["X-Tenant-Id"].ToString();
        return Guid.TryParse(header, out var id) ? id : null;
    }
}
