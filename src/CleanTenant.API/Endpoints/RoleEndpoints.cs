using CleanTenant.API.Extensions;
using CleanTenant.Application.Features.Roles;
using MediatR;

namespace CleanTenant.API.Endpoints;

/// <summary>Rol yönetimi ve atama Minimal API endpoint'leri.</summary>
public static class RoleEndpoints
{
    public static void MapRoleEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/roles")
            .WithTags("Roles")
            .RequireAuthorization();

        // Tenant rolleri
        group.MapGet("/tenant/{tenantId:guid}", GetTenantRoles)
            .WithSummary("Tenant rollerini listele");
        group.MapPost("/tenant", CreateTenantRole)
            .WithSummary("Tenant rolü oluştur");
        group.MapPost("/tenant/assign", AssignTenantRole)
            .WithSummary("Kullanıcıya tenant rolü ata");

        // Company rolleri
        group.MapGet("/company/{companyId:guid}", GetCompanyRoles)
            .WithSummary("Şirket rollerini listele");
        group.MapPost("/company", CreateCompanyRole)
            .WithSummary("Şirket rolü oluştur");
        group.MapPost("/company/assign", AssignCompanyRole)
            .WithSummary("Kullanıcıya şirket rolü ata");
    }

    // ── Tenant Roles ───────────────────────────────────────────────────────

    private static async Task<IResult> GetTenantRoles(
        Guid tenantId, ISender sender, CancellationToken ct)
    {
        var result = await sender.Send(new GetTenantRolesQuery(tenantId), ct);
        return result.ToApiResponse();
    }

    private static async Task<IResult> CreateTenantRole(
        CreateTenantRoleRequest body, HttpContext context, ISender sender, CancellationToken ct)
    {
        var tenantId = GetTenantId(context);
        if (tenantId is null)
            return Results.BadRequest(new { message = "X-Tenant-Id header zorunludur." });

        var result = await sender.Send(new CreateTenantRoleCommand
        {
            TenantId = tenantId.Value,
            Name = body.Name,
            Description = body.Description,
            PermissionList = body.Permissions
        }, ct);
        return result.ToApiResponse();
    }

    private static async Task<IResult> AssignTenantRole(
        AssignTenantRoleRequest body, HttpContext context, ISender sender, CancellationToken ct)
    {
        var tenantId = GetTenantId(context);
        if (tenantId is null)
            return Results.BadRequest(new { message = "X-Tenant-Id header zorunludur." });

        var result = await sender.Send(new AssignTenantRoleCommand
        {
            UserId = body.UserId,
            TenantId = tenantId.Value,
            TenantRoleId = body.RoleId
        }, ct);
        return result.ToApiResponse();
    }

    // ── Company Roles ──────────────────────────────────────────────────────

    private static async Task<IResult> GetCompanyRoles(
        Guid companyId, HttpContext context, ISender sender, CancellationToken ct)
    {
        var tenantId = GetTenantId(context);
        if (tenantId is null)
            return Results.BadRequest(new { message = "X-Tenant-Id header zorunludur." });

        var result = await sender.Send(new GetCompanyRolesQuery(companyId) { TenantId = tenantId.Value }, ct);
        return result.ToApiResponse();
    }

    private static async Task<IResult> CreateCompanyRole(
        CreateCompanyRoleRequest body, HttpContext context, ISender sender, CancellationToken ct)
    {
        var tenantId = GetTenantId(context);
        var companyId = GetCompanyId(context);
        if (tenantId is null || companyId is null)
            return Results.BadRequest(new { message = "X-Tenant-Id ve X-Company-Id header zorunludur." });

        var result = await sender.Send(new CreateCompanyRoleCommand
        {
            TenantId = tenantId.Value, CompanyId = companyId.Value,
            Name = body.Name, Description = body.Description, PermissionList = body.Permissions
        }, ct);
        return result.ToApiResponse();
    }

    private static async Task<IResult> AssignCompanyRole(
        AssignCompanyRoleRequest body, HttpContext context, ISender sender, CancellationToken ct)
    {
        var companyId = GetCompanyId(context);
        if (companyId is null)
            return Results.BadRequest(new { message = "X-Company-Id header zorunludur." });

        var result = await sender.Send(new AssignCompanyRoleCommand
        {
            UserId = body.UserId, CompanyId = companyId.Value, CompanyRoleId = body.RoleId
        }, ct);
        return result.ToApiResponse();
    }

    private static Guid? GetTenantId(HttpContext c) =>
        Guid.TryParse(c.Request.Headers["X-Tenant-Id"].ToString(), out var id) ? id : null;

    private static Guid? GetCompanyId(HttpContext c) =>
        Guid.TryParse(c.Request.Headers["X-Company-Id"].ToString(), out var id) ? id : null;
}

// ── Request Bodies ─────────────────────────────────────────────────────

public class CreateTenantRoleRequest
{
    public string Name { get; set; } = default!;
    public string? Description { get; set; }
    public string Permissions { get; set; } = "[]";
}

public class CreateCompanyRoleRequest
{
    public string Name { get; set; } = default!;
    public string? Description { get; set; }
    public string Permissions { get; set; } = "[]";
}

public class AssignTenantRoleRequest
{
    public Guid UserId { get; set; }
    public Guid RoleId { get; set; }
}

public class AssignCompanyRoleRequest
{
    public Guid UserId { get; set; }
    public Guid RoleId { get; set; }
}
