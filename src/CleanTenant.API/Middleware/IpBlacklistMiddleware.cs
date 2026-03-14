using CleanTenant.Application.Common.Interfaces;
using CleanTenant.Shared.Constants;

namespace CleanTenant.API.Middleware;

/// <summary>
/// IP Kara Liste middleware'i — Pipeline'ın EN BAŞINDAKİ kontrol.
/// 
/// <para><b>NEDEN İLK MIDDLEWARE?</b></para>
/// Kara listedeki IP'lerden gelen istekler mümkün olan en erken noktada
/// reddedilmelidir. Kimlik doğrulama, yetkilendirme gibi pahalı işlemler
/// yapılmadan önce O(1) Redis lookup ile IP kontrol edilir.
/// 
/// <para><b>REDIS SET KULLANIMI:</b></para>
/// IP kara listesi Redis SET tipinde saklanır.
/// SET lookup O(1) karmaşıklığındadır — milyonlarca IP olsa bile hızlıdır.
/// <code>
/// Redis: ct:blacklist:ips = {"192.168.1.100", "10.0.0.0/8", ...}
/// Kontrol: SISMEMBER ct:blacklist:ips "192.168.1.100" → true/false
/// </code>
/// </summary>
public class IpBlacklistMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<IpBlacklistMiddleware> _logger;

    public IpBlacklistMiddleware(RequestDelegate next, ILogger<IpBlacklistMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context, ICacheService cache)
    {
        var ipAddress = context.Connection.RemoteIpAddress?.ToString();

        if (!string.IsNullOrEmpty(ipAddress))
        {
            var isBlacklisted = await cache.SetContainsAsync(
                CacheKeys.IpBlacklist, ipAddress);

            if (isBlacklisted)
            {
                _logger.LogWarning(
                    "[BLACKLIST] Kara listedeki IP'den istek engellendi: {IpAddress} | Path: {Path}",
                    ipAddress, context.Request.Path);

                context.Response.StatusCode = StatusCodes.Status403Forbidden;
                await context.Response.WriteAsJsonAsync(new
                {
                    isSuccess = false,
                    statusCode = 403,
                    message = "Erişim engellendi.",
                    timestamp = DateTime.UtcNow
                });
                return;  // Pipeline'ı durdur — sonraki middleware'lere geçme
            }
        }

        await _next(context);
    }
}
