using CleanTenant.Domain.Settings;

namespace CleanTenant.Application.Common.Interfaces;

/// <summary>
/// Hiyerarşik ayar okuma servisi.
/// DB → appsettings.json fallback zinciri ile çalışır.
/// 
/// <para><b>ÖNCELİK:</b></para>
/// Company ayarı → Tenant ayarı → System ayarı → appsettings.json
/// </summary>
public interface ISettingsService
{
    /// <summary>Ayar değerini hiyerarşik olarak okur.</summary>
    Task<string?> GetAsync(string key, Guid? tenantId = null, Guid? companyId = null, CancellationToken ct = default);

    /// <summary>int olarak okur.</summary>
    Task<int> GetIntAsync(string key, int fallback, Guid? tenantId = null, Guid? companyId = null, CancellationToken ct = default);

    /// <summary>bool olarak okur.</summary>
    Task<bool> GetBoolAsync(string key, bool fallback, Guid? tenantId = null, Guid? companyId = null, CancellationToken ct = default);
}
