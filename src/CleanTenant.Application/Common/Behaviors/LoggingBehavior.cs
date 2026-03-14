using System.Diagnostics;
using CleanTenant.Application.Common.Interfaces;
using MediatR;
using Microsoft.Extensions.Logging;

namespace CleanTenant.Application.Common.Behaviors;

/// <summary>
/// Loglama pipeline behavior'ı — her isteği süre, kullanıcı ve sonuç bilgisiyle loglar.
/// 
/// <para><b>NE LOGLAR?</b></para>
/// <list type="bullet">
///   <item>İstek adı (CreateTenantCommand, GetTenantsQuery vb.)</item>
///   <item>Kullanıcı bilgisi (userId, email, IP)</item>
///   <item>Aktif bağlam (tenantId, companyId)</item>
///   <item>Çalışma süresi (ms) — yavaş sorguları tespit etmek için</item>
///   <item>Başarı/başarısızlık durumu</item>
/// </list>
/// 
/// <para><b>YAPISAL LOGLAMA (Structured Logging):</b></para>
/// Serilog ile kullanıldığında log mesajları JSON formatında saklanır.
/// <code>
/// // Klasik loglama (aranması zor):
/// logger.LogInformation("CreateTenantCommand çalıştırıldı, 45ms sürdü, user: admin@test.com");
/// 
/// // Yapısal loglama (sorgulanabilir):
/// logger.LogInformation("Request {@RequestName} by {@UserId} completed in {@ElapsedMs}ms",
///     requestName, userId, elapsedMs);
/// // Seq'te sorgu: RequestName = "CreateTenantCommand" AND ElapsedMs > 1000
/// </code>
/// 
/// <para><b>PERFORMANS UYARISI:</b></para>
/// 500ms'den uzun süren istekler Warning seviyesinde loglanır.
/// Bu, performans sorunlarını erken tespit etmeyi sağlar.
/// </summary>
public class LoggingBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
    where TRequest : IRequest<TResponse>
{
    private readonly ILogger<LoggingBehavior<TRequest, TResponse>> _logger;
    private readonly ICurrentUserService _currentUser;

    /// <summary>Yavaş istek eşiği (ms). Bu süreyi aşan istekler Warning loglanır.</summary>
    private const int SlowRequestThresholdMs = 500;

    public LoggingBehavior(
        ILogger<LoggingBehavior<TRequest, TResponse>> logger,
        ICurrentUserService currentUser)
    {
        _logger = logger;
        _currentUser = currentUser;
    }

    public async Task<TResponse> Handle(
        TRequest request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken)
    {
        var requestName = typeof(TRequest).Name;
        var userId = _currentUser.UserId?.ToString() ?? "Anonymous";
        var userEmail = _currentUser.Email ?? "Anonymous";
        var ipAddress = _currentUser.IpAddress ?? "Unknown";
        var tenantId = _currentUser.ActiveTenantId?.ToString() ?? "None";
        var companyId = _currentUser.ActiveCompanyId?.ToString() ?? "None";

        // İstek başlangıç logu
        _logger.LogInformation(
            "[START] {RequestName} | User: {UserId} ({UserEmail}) | IP: {IpAddress} | Tenant: {TenantId} | Company: {CompanyId}",
            requestName, userId, userEmail, ipAddress, tenantId, companyId);

        // Süre ölçümü başlat
        var stopwatch = Stopwatch.StartNew();

        try
        {
            var response = await next();
            stopwatch.Stop();

            var elapsedMs = stopwatch.ElapsedMilliseconds;

            // Yavaş istek uyarısı
            if (elapsedMs > SlowRequestThresholdMs)
            {
                _logger.LogWarning(
                    "[SLOW] {RequestName} tamamlandı: {ElapsedMs}ms | User: {UserId} | " +
                    "⚠️ Eşik değer ({Threshold}ms) aşıldı!",
                    requestName, elapsedMs, userId, SlowRequestThresholdMs);
            }
            else
            {
                _logger.LogInformation(
                    "[END] {RequestName} tamamlandı: {ElapsedMs}ms | User: {UserId}",
                    requestName, elapsedMs, userId);
            }

            return response;
        }
        catch (Exception ex)
        {
            stopwatch.Stop();

            _logger.LogError(ex,
                "[ERROR] {RequestName} hata ile sonlandı: {ElapsedMs}ms | User: {UserId} | Hata: {ErrorMessage}",
                requestName, stopwatch.ElapsedMilliseconds, userId, ex.Message);

            throw; // Exception'ı yeniden fırlat — ExceptionHandlingMiddleware yakalayacak
        }
    }
}
