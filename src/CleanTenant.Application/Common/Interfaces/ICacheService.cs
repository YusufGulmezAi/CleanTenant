

namespace CleanTenant.Application.Common.Interfaces;

/// <summary>
/// Redis cache servis sözleşmesi.
/// Tüm cache operasyonları bu interface üzerinden yapılır.
/// </summary>
public interface ICacheService
{
    /// <summary>Cache'ten değer okur. Yoksa null döner.</summary>
    Task<T?> GetAsync<T>(string key, CancellationToken ct = default);

    /// <summary>Cache'e değer yazar. TTL ile süre belirtilir.</summary>
    Task SetAsync<T>(string key, T value, TimeSpan? expiration = null, CancellationToken ct = default);

    /// <summary>Cache'ten değer siler.</summary>
    Task RemoveAsync(string key, CancellationToken ct = default);

    /// <summary>Belirli bir pattern'a uyan tüm key'leri siler.</summary>
    Task RemoveByPrefixAsync(string prefix, CancellationToken ct = default);

    /// <summary>Key var mı kontrol eder.</summary>
    Task<bool> ExistsAsync(string key, CancellationToken ct = default);

    /// <summary>
    /// Redis SET'e eleman ekler (IP blacklist için).
    /// SET tipi: Eleman benzersizliği garantiler, O(1) lookup.
    /// </summary>
    Task SetAddAsync(string key, string value, CancellationToken ct = default);

    /// <summary>Redis SET'ten eleman siler.</summary>
    Task SetRemoveAsync(string key, string value, CancellationToken ct = default);

    /// <summary>Değer SET'te var mı? (O(1) kontrol)</summary>
    Task<bool> SetContainsAsync(string key, string value, CancellationToken ct = default);
}
