using CleanTenant.API.Extensions;
using CleanTenant.Application.Features.Tenants.Commands;
using CleanTenant.Application.Features.Tenants.Queries;
using CleanTenant.Shared.DTOs.Tenants;
using MediatR;

namespace CleanTenant.API.Endpoints;

/// <summary>
/// Tenant yönetimi Minimal API endpoint'leri.
/// 
/// <para><b>METOD BAZLI YAKLAŞIM:</b></para>
/// Inline lambda yerine static method'lar kullanılır:
/// <list type="bullet">
///   <item>Okunabilirlik: Her endpoint'in ne yaptığı açıkça görülür</item>
///   <item>Test edilebilirlik: Method'lar birim test edilebilir</item>
///   <item>Separation of Concerns: Routing ve iş mantığı ayrılır</item>
/// </list>
/// </summary>
public static class TenantEndpoints
{
    /// <summary>Tenant endpoint grubunu kaydeder.</summary>
    public static void MapTenantEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/tenants")
            .WithTags("Tenants")
            .RequireAuthorization();

        group.MapGet("/", GetAll).WithSummary("Tüm tenant'ları listeler (sayfalı)");
        group.MapGet("/{id:guid}", GetById).WithSummary("Tenant detayını getirir");
        group.MapPost("/", Create).WithSummary("Yeni tenant oluşturur");
        group.MapPut("/{id:guid}", Update).WithSummary("Tenant bilgilerini günceller");
        group.MapDelete("/{id:guid}", Delete).WithSummary("Tenant'ı siler (soft delete)");
    }

    /// <summary>GET /api/tenants?pageNumber=1&amp;pageSize=20&amp;search=abc&amp;isActive=true</summary>
    private static async Task<IResult> GetAll(
        ISender sender,
        int pageNumber = 1,
        int pageSize = 20,
        string? search = null,
        bool? isActive = null,
        CancellationToken ct = default)
    {
        var query = new GetTenantsQuery
        {
            PageNumber = pageNumber,
            PageSize = pageSize,
            Search = search,
            IsActive = isActive
        };

        var result = await sender.Send(query, ct);
        return result.ToApiResponse();
    }

    /// <summary>GET /api/tenants/{id}</summary>
    private static async Task<IResult> GetById(
        Guid id, ISender sender, CancellationToken ct)
    {
        var result = await sender.Send(new GetTenantByIdQuery(id), ct);
        return result.ToApiResponse();
    }

    /// <summary>POST /api/tenants</summary>
    private static async Task<IResult> Create(
        CreateTenantDto dto, ISender sender, CancellationToken ct)
    {
        var result = await sender.Send(new CreateTenantCommand(dto), ct);
        return result.ToApiResponse();
    }

    /// <summary>PUT /api/tenants/{id}</summary>
    private static async Task<IResult> Update(
        Guid id, UpdateTenantDto dto, ISender sender, CancellationToken ct)
    {
        var result = await sender.Send(new UpdateTenantCommand(id, dto), ct);
        return result.ToApiResponse();
    }

    /// <summary>DELETE /api/tenants/{id}</summary>
    private static async Task<IResult> Delete(
        Guid id, ISender sender, CancellationToken ct)
    {
        var result = await sender.Send(new DeleteTenantCommand(id), ct);
        return result.ToApiResponse();
    }
}
