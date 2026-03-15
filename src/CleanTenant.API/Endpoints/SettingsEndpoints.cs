using CleanTenant.API.Extensions;
using CleanTenant.Application.Features.Settings;
using CleanTenant.Domain.Settings;
using MediatR;

namespace CleanTenant.API.Endpoints;

/// <summary>
/// Sistem ayarları yönetim endpoint'leri.
/// Hiyerarşik: System → Tenant → Company override desteği.
/// </summary>
public static class SettingsEndpoints
{
    public static void MapSettingsEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/settings")
            .WithTags("Settings")
            .RequireAuthorization();

        group.MapGet("/", GetSettings)
            .WithSummary("Ayarları listele (seviye/kategori bazlı)");

        group.MapGet("/{key}", GetSettingValue)
            .WithSummary("Ayar değerini getir (hiyerarşik okuma)");

        group.MapPut("/", UpsertSetting)
            .WithSummary("Ayar oluştur veya güncelle");

        group.MapDelete("/{id:guid}", DeleteSetting)
            .WithSummary("Tenant/Company override ayarını sil");
    }

    private static async Task<IResult> GetSettings(
        ISender sender, HttpContext context,
        string? level = null, string? category = null, CancellationToken ct = default)
    {
        var tenantId = GetHeader(context, "X-Tenant-Id");
        var companyId = GetHeader(context, "X-Company-Id");

        SettingLevel? settingLevel = level?.ToLower() switch
        {
            "system" => SettingLevel.System,
            "tenant" => SettingLevel.Tenant,
            "company" => SettingLevel.Company,
            _ => null
        };

        var result = await sender.Send(new GetSettingsQuery
        {
            Level = settingLevel,
            TenantId = tenantId,
            CompanyId = companyId,
            Category = category
        }, ct);
        return result.ToApiResponse();
    }

    private static async Task<IResult> GetSettingValue(
        string key, ISender sender, HttpContext context, CancellationToken ct)
    {
        var result = await sender.Send(new GetSettingValueQuery(key)
        {
            TenantId = GetHeader(context, "X-Tenant-Id"),
            CompanyId = GetHeader(context, "X-Company-Id")
        }, ct);
        return result.ToApiResponse();
    }

    private static async Task<IResult> UpsertSetting(
        UpsertSettingRequest body, ISender sender, HttpContext context, CancellationToken ct)
    {
        var tenantId = GetHeader(context, "X-Tenant-Id");
        var companyId = GetHeader(context, "X-Company-Id");

        var level = body.Level?.ToLower() switch
        {
            "tenant" => SettingLevel.Tenant,
            "company" => SettingLevel.Company,
            _ => SettingLevel.System
        };

        var result = await sender.Send(new UpsertSettingCommand
        {
            Key = body.Key,
            Value = body.Value,
            Description = body.Description,
            Category = body.Category,
            ValueType = body.ValueType,
            Level = level,
            TenantId = level >= SettingLevel.Tenant ? tenantId : null,
            CompanyId = level >= SettingLevel.Company ? companyId : null
        }, ct);
        return result.ToApiResponse();
    }

    private static async Task<IResult> DeleteSetting(Guid id, ISender sender, CancellationToken ct)
    {
        var result = await sender.Send(new DeleteSettingCommand(id), ct);
        return result.ToApiResponse();
    }

    private static Guid? GetHeader(HttpContext c, string name) =>
        Guid.TryParse(c.Request.Headers[name].ToString(), out var id) ? id : null;
}

public class UpsertSettingRequest
{
    public string Key { get; set; } = default!;
    public string Value { get; set; } = default!;
    public string? Description { get; set; }
    public string? Category { get; set; }
    public string? ValueType { get; set; }
    public string? Level { get; set; }
}
