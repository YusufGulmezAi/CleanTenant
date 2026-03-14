using System.Diagnostics;

namespace CleanTenant.API.Middleware;

/// <summary>
/// HTTP istek loglama middleware'i — her isteğin özet bilgisini loglar.
/// Serilog RequestLogging'den farklı olarak IP, UserAgent ve tenant bilgisini ekler.
/// </summary>
public class RequestLoggingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<RequestLoggingMiddleware> _logger;

    public RequestLoggingMiddleware(RequestDelegate next, ILogger<RequestLoggingMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        var stopwatch = Stopwatch.StartNew();

        await _next(context);

        stopwatch.Stop();

        var ipAddress = context.Connection.RemoteIpAddress?.ToString() ?? "unknown";
        var tenantId = context.Request.Headers["X-Tenant-Id"].ToString();
        var userId = context.User?.FindFirst("sub")?.Value ?? "anonymous";

        _logger.LogInformation(
            "HTTP {Method} {Path} → {StatusCode} in {ElapsedMs}ms | " +
            "IP: {IpAddress} | User: {UserId} | Tenant: {TenantId}",
            context.Request.Method,
            context.Request.Path,
            context.Response.StatusCode,
            stopwatch.ElapsedMilliseconds,
            ipAddress,
            userId,
            string.IsNullOrEmpty(tenantId) ? "none" : tenantId);
    }
}
