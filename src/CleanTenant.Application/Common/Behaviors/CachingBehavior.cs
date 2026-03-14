using CleanTenant.Application.Common.Interfaces;
using MediatR;
using Microsoft.Extensions.Logging;

namespace CleanTenant.Application.Common.Behaviors;

/// <summary>
/// Cache marker interface'i.
/// Bu interface'i implemente eden Query'ler otomatik olarak cache'lenir.
/// 
/// <para><b>KULLANIM:</b></para>
/// <code>
/// // Query tanımı — ICacheableQuery ile işaretle:
/// public record GetTenantsQuery : IRequest&lt;Result&lt;List&lt;TenantDto&gt;&gt;&gt;, ICacheableQuery
/// {
///     public string CacheKey => "tenants:all";
///     public TimeSpan? CacheDuration => TimeSpan.FromMinutes(10);
/// }
/// 
/// // Sonuç: İlk çağrıda DB'den çeker, cache'e yazar.
/// // Sonraki çağrılarda cache'ten döner (10 dakika boyunca).
/// // Tenant oluşturulduğunda/güncellendiğinde cache invalidate edilir.
/// </code>
/// 
/// <para><b>NEDEN SADECE QUERY'LER?</b></para>
/// Command'lar (Create, Update, Delete) veri değiştirir — cache'lenmez.
/// Query'ler (Get, List, Search) veri okur — cache'lenebilir.
/// Bu, CQRS pattern'ının doğal bir uzantısıdır.
/// </summary>
public interface ICacheableQuery
{
    /// <summary>
    /// Redis cache key'i.
    /// Benzersiz olmalıdır. Parametreli sorgularda parametre key'e dahil edilir.
    /// 
    /// <code>
    /// // Parametresiz: "tenants:all"
    /// // Parametreli: $"tenant:{TenantId}:companies"
    /// </code>
    /// </summary>
    string CacheKey { get; }

    /// <summary>
    /// Cache süresi. null ise varsayılan süre (30 dakika) uygulanır.
    /// Sık değişen veriler için kısa, nadir değişenler için uzun tutulur.
    /// </summary>
    TimeSpan? CacheDuration => null;
}

/// <summary>
/// Cache invalidation marker interface'i.
/// Bu interface'i implemente eden Command'lar, belirtilen cache key'lerini siler.
/// 
/// <para><b>KULLANIM:</b></para>
/// <code>
/// public record CreateTenantCommand(...) : IRequest&lt;Result&lt;TenantDto&gt;&gt;, ICacheInvalidator
/// {
///     public string[] CacheKeysToInvalidate => ["tenants:all", "tenants:count"];
/// }
/// // Tenant oluşturulunca "tenants:all" ve "tenants:count" cache'leri silinir.
/// </code>
/// </summary>
public interface ICacheInvalidator
{
    /// <summary>Silinecek cache key'leri.</summary>
    string[] CacheKeysToInvalidate { get; }
}

/// <summary>
/// Caching pipeline behavior'ı — ICacheableQuery ile işaretli sorguları cache'ler.
/// 
/// <para><b>AKIŞ:</b></para>
/// <code>
/// Query geldi → ICacheableQuery mi? 
///   HAYIR → Direkt handler'a geç
///   EVET → Cache'te var mı?
///     VAR → Cache'ten dön (handler çalışmaz!)
///     YOK → Handler çalışsın → Sonucu cache'e yaz → Dön
/// </code>
/// </summary>
public class CachingBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
    where TRequest : IRequest<TResponse>
{
    private readonly ICacheService _cache;
    private readonly ILogger<CachingBehavior<TRequest, TResponse>> _logger;

    /// <summary>Varsayılan cache süresi — ICacheableQuery.CacheDuration null ise kullanılır.</summary>
    private static readonly TimeSpan DefaultCacheDuration = TimeSpan.FromMinutes(30);

    public CachingBehavior(ICacheService cache, ILogger<CachingBehavior<TRequest, TResponse>> logger)
    {
        _cache = cache;
        _logger = logger;
    }

    public async Task<TResponse> Handle(
        TRequest request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken)
    {
        // Sadece ICacheableQuery için cache uygula
        if (request is not ICacheableQuery cacheableQuery)
            return await next();

        var cacheKey = cacheableQuery.CacheKey;

        // Cache'te var mı?
        var cachedResult = await _cache.GetAsync<TResponse>(cacheKey, cancellationToken);

        if (cachedResult is not null)
        {
            _logger.LogDebug(
                "[CACHE HIT] {RequestName} → Key: {CacheKey}",
                typeof(TRequest).Name, cacheKey);

            return cachedResult;
        }

        // Cache'te yok → Handler'ı çalıştır
        _logger.LogDebug(
            "[CACHE MISS] {RequestName} → Key: {CacheKey} — DB'den yükleniyor",
            typeof(TRequest).Name, cacheKey);

        var response = await next();

        // Sonucu cache'e yaz
        var duration = cacheableQuery.CacheDuration ?? DefaultCacheDuration;
        await _cache.SetAsync(cacheKey, response, duration, cancellationToken);

        _logger.LogDebug(
            "[CACHE SET] {RequestName} → Key: {CacheKey} | TTL: {CacheDuration}",
            typeof(TRequest).Name, cacheKey, duration);

        return response;
    }
}

/// <summary>
/// Cache invalidation pipeline behavior'ı — ICacheInvalidator ile işaretli
/// Command'lar başarılı olduğunda belirtilen cache key'lerini siler.
/// 
/// <para><b>AKIŞ:</b></para>
/// <code>
/// Command geldi → ICacheInvalidator mi?
///   HAYIR → Direkt handler'a geç
///   EVET → Handler çalışsın → Başarılı mı?
///     EVET → Belirtilen cache key'lerini sil
///     HAYIR → Cache'e dokunma
/// </code>
/// </summary>
public class CacheInvalidationBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
    where TRequest : IRequest<TResponse>
{
    private readonly ICacheService _cache;
    private readonly ILogger<CacheInvalidationBehavior<TRequest, TResponse>> _logger;

    public CacheInvalidationBehavior(
        ICacheService cache,
        ILogger<CacheInvalidationBehavior<TRequest, TResponse>> logger)
    {
        _cache = cache;
        _logger = logger;
    }

    public async Task<TResponse> Handle(
        TRequest request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken)
    {
        // Sadece ICacheInvalidator için çalış
        if (request is not ICacheInvalidator invalidator)
            return await next();

        var response = await next();

        // Başarılı sonuç kontrolü — Result<T> ise IsSuccess kontrol et
        if (IsSuccessResult(response))
        {
            foreach (var key in invalidator.CacheKeysToInvalidate)
            {
                await _cache.RemoveAsync(key, cancellationToken);

                _logger.LogDebug(
                    "[CACHE INVALIDATED] {RequestName} → Key: {CacheKey}",
                    typeof(TRequest).Name, key);
            }
        }

        return response;
    }

    /// <summary>
    /// Response'un başarılı bir Result olup olmadığını kontrol eder.
    /// Result<T> değilse her zaman true döner (cache her türlü invalidate olur).
    /// </summary>
    private static bool IsSuccessResult(TResponse response)
    {
        if (response is null) return false;

        // Result<T>.IsSuccess property'sini kontrol et
        var isSuccessProperty = response.GetType().GetProperty("IsSuccess");
        if (isSuccessProperty is not null)
        {
            return (bool)(isSuccessProperty.GetValue(response) ?? false);
        }

        // Result<T> değilse true kabul et
        return true;
    }
}
