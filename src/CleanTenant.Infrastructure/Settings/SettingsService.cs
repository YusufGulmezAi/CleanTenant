using CleanTenant.Application.Common.Interfaces;
using CleanTenant.Domain.Settings;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

namespace CleanTenant.Infrastructure.Settings;

/// <summary>
/// Hiyerarşik ayar okuma servisi.
/// 
/// <para><b>OKUMA SIRASI:</b></para>
/// <code>
/// [1] Redis cache (ct:settings:{level}:{id}:{key}) → 5dk TTL
/// [2] DB → Company ayarı (CompanyId + Key)
/// [3] DB → Tenant ayarı (TenantId + Key, CompanyId null)
/// [4] DB → System ayarı (TenantId null, CompanyId null)
/// [5] appsettings.json → CleanTenant:{Key} (nokta → : dönüşümü)
/// </code>
/// </summary>
public class SettingsService : ISettingsService
{
    private readonly IApplicationDbContext _db;
    private readonly ICacheService _cache;
    private readonly IConfiguration _configuration;

    public SettingsService(IApplicationDbContext db, ICacheService cache, IConfiguration configuration)
    {
        _db = db;
        _cache = cache;
        _configuration = configuration;
    }

    public async Task<string?> GetAsync(string key, Guid? tenantId = null, Guid? companyId = null, CancellationToken ct = default)
    {
        // [1] Company seviyesi
        if (companyId.HasValue)
        {
            var companyValue = await GetFromCacheOrDbAsync(key, SettingLevel.Company, tenantId, companyId, ct);
            if (companyValue is not null) return companyValue;
        }

        // [2] Tenant seviyesi
        if (tenantId.HasValue)
        {
            var tenantValue = await GetFromCacheOrDbAsync(key, SettingLevel.Tenant, tenantId, null, ct);
            if (tenantValue is not null) return tenantValue;
        }

        // [3] System seviyesi
        var systemValue = await GetFromCacheOrDbAsync(key, SettingLevel.System, null, null, ct);
        if (systemValue is not null) return systemValue;

        // [4] appsettings.json fallback
        // "Jwt.AccessTokenExpirationMinutes" → "CleanTenant:Jwt:AccessTokenExpirationMinutes"
        var configKey = $"CleanTenant:{key.Replace('.', ':')}";
        return _configuration[configKey];
    }

    public async Task<int> GetIntAsync(string key, int fallback, Guid? tenantId = null, Guid? companyId = null, CancellationToken ct = default)
    {
        var value = await GetAsync(key, tenantId, companyId, ct);
        return int.TryParse(value, out var result) ? result : fallback;
    }

    public async Task<bool> GetBoolAsync(string key, bool fallback, Guid? tenantId = null, Guid? companyId = null, CancellationToken ct = default)
    {
        var value = await GetAsync(key, tenantId, companyId, ct);
        return bool.TryParse(value, out var result) ? result : fallback;
    }

    private async Task<string?> GetFromCacheOrDbAsync(
        string key, SettingLevel level, Guid? tenantId, Guid? companyId, CancellationToken ct)
    {
        var cacheKey = $"ct:settings:{level}:{tenantId}:{companyId}:{key}";

        // Redis cache
        var cached = await _cache.GetAsync<string>(cacheKey, ct);
        if (cached is not null) return cached;

        // DB
        var setting = await _db.SystemSettings
            .AsNoTracking()
            .FirstOrDefaultAsync(s =>
                s.Key == key &&
                s.Level == level &&
                s.TenantId == tenantId &&
                s.CompanyId == companyId, ct);

        if (setting is null) return null;

        // Cache'e yaz (5dk)
        await _cache.SetAsync(cacheKey, setting.Value, TimeSpan.FromMinutes(5), ct);

        return setting.Value;
    }
}
