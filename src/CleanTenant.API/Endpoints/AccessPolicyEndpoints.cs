using CleanTenant.API.Extensions;
using CleanTenant.Application.Features.AccessPolicies;
using CleanTenant.Domain.Security;
using MediatR;

namespace CleanTenant.API.Endpoints;

/// <summary>
/// Erişim politikası yönetim endpoint'leri.
/// Hiyerarşik: System → Tenant → Company seviyelerinde IP/Zaman kısıtlama.
/// </summary>
public static class AccessPolicyEndpoints
{
    public static void MapAccessPolicyEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/access-policies")
            .WithTags("Access Policies")
            .RequireAuthorization();

        group.MapGet("/", GetPolicies)
            .WithSummary("Politikaları listele (seviye/tenant/company bazlı)");

        group.MapPost("/", CreatePolicy)
            .WithSummary("Yeni politika oluştur");

        group.MapPut("/{id:guid}", UpdatePolicy)
            .WithSummary("Politika güncelle (default'ın kuralları gevşetilemez)");

        group.MapDelete("/{id:guid}", DeletePolicy)
            .WithSummary("Politika sil (default silinemez, kullanıcılar default'a düşer)");

        group.MapPost("/{policyId:guid}/assign/{userId:guid}", AssignPolicy)
            .WithSummary("Kullanıcıya politika ata");

        group.MapDelete("/unassign/{userId:guid}", UnassignPolicy)
            .WithSummary("Kullanıcının politikasını kaldır (default'a düşer)");

        group.MapGet("/user/{userId:guid}", GetUserPolicy)
            .WithSummary("Kullanıcının aktif politikasını getir");
    }

    private static async Task<IResult> GetPolicies(
        ISender sender, HttpContext context,
        string? level = null, CancellationToken ct = default)
    {
        var tenantId = GetHeader(context, "X-Tenant-Id");
        var companyId = GetHeader(context, "X-Company-Id");

        PolicyLevel? policyLevel = level?.ToLower() switch
        {
            "system" => PolicyLevel.System,
            "tenant" => PolicyLevel.Tenant,
            "company" => PolicyLevel.Company,
            _ => null
        };

        var result = await sender.Send(new GetAccessPoliciesQuery
        {
            Level = policyLevel,
            TenantId = tenantId,
            CompanyId = companyId
        }, ct);
        return result.ToApiResponse();
    }

    private static async Task<IResult> CreatePolicy(
        CreatePolicyRequest body, HttpContext context, ISender sender, CancellationToken ct)
    {
        var tenantId = GetHeader(context, "X-Tenant-Id");
        var companyId = GetHeader(context, "X-Company-Id");

        var level = body.Level.ToLower() switch
        {
            "system" => PolicyLevel.System,
            "tenant" => PolicyLevel.Tenant,
            "company" => PolicyLevel.Company,
            _ => PolicyLevel.System
        };

        var result = await sender.Send(new CreateAccessPolicyCommand
        {
            Name = body.Name,
            Description = body.Description,
            Level = level,
            TenantId = level == PolicyLevel.Tenant || level == PolicyLevel.Company ? tenantId : null,
            CompanyId = level == PolicyLevel.Company ? companyId : null,
            DenyAllIps = body.DenyAllIps,
            AllowedIpRanges = body.AllowedIpRanges,
            DenyAllTimes = body.DenyAllTimes,
            AllowedDays = body.AllowedDays,
            AllowedTimeStart = body.AllowedTimeStart,
            AllowedTimeEnd = body.AllowedTimeEnd
        }, ct);
        return result.ToApiResponse();
    }

    private static async Task<IResult> UpdatePolicy(
        Guid id, UpdatePolicyRequest body, ISender sender, CancellationToken ct)
    {
        var result = await sender.Send(new UpdateAccessPolicyCommand
        {
            Id = id,
            Name = body.Name,
            Description = body.Description,
            DenyAllIps = body.DenyAllIps,
            AllowedIpRanges = body.AllowedIpRanges,
            DenyAllTimes = body.DenyAllTimes,
            AllowedDays = body.AllowedDays,
            AllowedTimeStart = body.AllowedTimeStart,
            AllowedTimeEnd = body.AllowedTimeEnd
        }, ct);
        return result.ToApiResponse();
    }

    private static async Task<IResult> DeletePolicy(Guid id, ISender sender, CancellationToken ct)
    {
        var result = await sender.Send(new DeleteAccessPolicyCommand(id), ct);
        return result.ToApiResponse();
    }

    private static async Task<IResult> AssignPolicy(
        Guid policyId, Guid userId, ISender sender, CancellationToken ct)
    {
        var result = await sender.Send(new AssignPolicyCommand
        {
            PolicyId = policyId,
            UserId = userId
        }, ct);
        return result.ToApiResponse();
    }

    private static async Task<IResult> UnassignPolicy(
        Guid userId, HttpContext context, ISender sender, CancellationToken ct)
    {
        var tenantId = GetHeader(context, "X-Tenant-Id");
        var companyId = GetHeader(context, "X-Company-Id");

        var level = companyId.HasValue ? PolicyLevel.Company
            : tenantId.HasValue ? PolicyLevel.Tenant
            : PolicyLevel.System;

        var result = await sender.Send(new UnassignPolicyCommand(userId)
        {
            Level = level,
            TenantId = tenantId,
            CompanyId = companyId
        }, ct);
        return result.ToApiResponse();
    }

    private static async Task<IResult> GetUserPolicy(
        Guid userId, ISender sender, CancellationToken ct)
    {
        var result = await sender.Send(new GetUserPolicyQuery(userId), ct);
        return result.ToApiResponse();
    }

    private static Guid? GetHeader(HttpContext c, string name) =>
        Guid.TryParse(c.Request.Headers[name].ToString(), out var id) ? id : null;
}

// ── Request Bodies ─────────────────────────────────────────────────────

public class CreatePolicyRequest
{
    public string Name { get; set; } = default!;
    public string? Description { get; set; }
    public string Level { get; set; } = "System";
    public bool DenyAllIps { get; set; }
    public string AllowedIpRanges { get; set; } = "[]";
    public bool DenyAllTimes { get; set; }
    public string AllowedDays { get; set; } = "[]";
    public string? AllowedTimeStart { get; set; }
    public string? AllowedTimeEnd { get; set; }
}

public class UpdatePolicyRequest
{
    public string? Name { get; set; }
    public string? Description { get; set; }
    public bool DenyAllIps { get; set; }
    public string AllowedIpRanges { get; set; } = "[]";
    public bool DenyAllTimes { get; set; }
    public string AllowedDays { get; set; } = "[]";
    public string? AllowedTimeStart { get; set; }
    public string? AllowedTimeEnd { get; set; }
}
