using CleanTenant.Application.Common.Interfaces;
using CleanTenant.Shared.Constants;

namespace CleanTenant.API.Middleware;

/// <summary>
/// Rate Limit middleware'i — endpoint bazlı istek sınırlama.
/// 
/// <para><b>SLIDING WINDOW ALGORİTMASI:</b></para>
/// Redis INCR + EXPIRE ile sliding window rate limiting uygulanır.
/// <code>
/// Key: ct:ratelimit:/api/tenants:192.168.1.100
/// Her istek → INCR (sayaç artır)
/// İlk istek → EXPIRE 60s (1 dakika pencere)
/// Sayaç &gt; limit → 429 Too Many Requests
/// </code>
/// 
/// <para><b>PARAMETRİK:</b></para>
/// appsettings.json'dan:
/// <code>
/// "AccessPolicy": {
///   "EnableRateLimit": true,
///   "DefaultRateLimitPerMinute": 60
/// }
/// </code>
/// </summary>
public class RateLimitMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<RateLimitMiddleware> _logger;

    public RateLimitMiddleware(RequestDelegate next, ILogger<RateLimitMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context, ICacheService cache, IConfiguration configuration)
    {
        var enableRateLimit = bool.Parse(
            configuration["CleanTenant:AccessPolicy:EnableRateLimit"] ?? "true");

        if (!enableRateLimit)
        {
            await _next(context);
            return;
        }

        var limitPerMinute = int.Parse(
            configuration["CleanTenant:AccessPolicy:DefaultRateLimitPerMinute"] ?? "60");

        var ipAddress = context.Connection.RemoteIpAddress?.ToString() ?? "unknown";
        var endpoint = context.Request.Path.ToString().ToLowerInvariant();
        var cacheKey = CacheKeys.RateLimit(endpoint, ipAddress);

        // Mevcut sayacı oku
        var currentCount = await cache.GetAsync<int?>(cacheKey);

        if (currentCount.HasValue && currentCount.Value >= limitPerMinute)
        {
            _logger.LogWarning(
                "[RATE LIMIT] Limit aşıldı: {IpAddress} → {Endpoint} ({Count}/{Limit})",
                ipAddress, endpoint, currentCount.Value, limitPerMinute);

            context.Response.StatusCode = StatusCodes.Status429TooManyRequests;
            context.Response.Headers.Append("Retry-After", "60");
            await context.Response.WriteAsJsonAsync(new
            {
                isSuccess = false,
                statusCode = 429,
                message = "Çok fazla istek gönderdiniz. Lütfen bir dakika bekleyiniz.",
                timestamp = DateTime.UtcNow
            });
            return;
        }

        // Sayacı artır
        var newCount = (currentCount ?? 0) + 1;
        await cache.SetAsync(cacheKey, newCount, TimeSpan.FromMinutes(1));

        // Rate limit bilgisini response header'larına ekle
        context.Response.Headers.Append("X-RateLimit-Limit", limitPerMinute.ToString());
        context.Response.Headers.Append("X-RateLimit-Remaining",
            Math.Max(0, limitPerMinute - newCount).ToString());

        await _next(context);
    }
}
