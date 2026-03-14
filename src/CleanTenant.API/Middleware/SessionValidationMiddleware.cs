using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using CleanTenant.Application.Common.Interfaces;
using CleanTenant.Infrastructure.Security;
using CleanTenant.Shared.Constants;

namespace CleanTenant.API.Middleware;

/// <summary>
/// Oturum doğrulama middleware'i — her kimlik doğrulamalı istekte çalışır.
/// 
/// <para><b>JWT DOĞRULAMASI YETERLİ DEĞİL:</b></para>
/// JWT token geçerli olsa bile oturum geçersiz olabilir:
/// <list type="bullet">
///   <item>Kullanıcı bloke edilmiş olabilir</item>
///   <item>Admin force logout yapmış olabilir</item>
///   <item>Token başka bir cihazda kullanılıyor olabilir</item>
///   <item>Tek oturum kuralı ihlal ediliyor olabilir</item>
/// </list>
/// 
/// Bu middleware JWT doğrulaması SONRASINDA çalışır ve Redis'ten
/// ek güvenlik kontrollerini yapar.
/// </summary>
public class SessionValidationMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<SessionValidationMiddleware> _logger;

    public SessionValidationMiddleware(RequestDelegate next, ILogger<SessionValidationMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(
        HttpContext context,
        ISessionManager sessionManager,
        IConfiguration configuration)
    {
        // Sadece kimlik doğrulaması yapılmış istekler için çalış
        if (context.User.Identity?.IsAuthenticated != true)
        {
            await _next(context);
            return;
        }

        // userId çıkar
        var userIdClaim = context.User.FindFirstValue(JwtRegisteredClaimNames.Sub)
                          ?? context.User.FindFirstValue(ClaimTypes.NameIdentifier);

        if (!Guid.TryParse(userIdClaim, out var userId))
        {
            await _next(context);
            return;
        }

        // Kullanıcı bloke mi?
        var isBlocked = await sessionManager.IsUserBlockedAsync(userId);
        if (isBlocked)
        {
            _logger.LogWarning("[SESSION] Bloke kullanıcı engellendi: {UserId}", userId);

            context.Response.StatusCode = StatusCodes.Status403Forbidden;
            await context.Response.WriteAsJsonAsync(new
            {
                isSuccess = false,
                statusCode = 403,
                message = "Hesabınız bloke edilmiştir. Yöneticinizle iletişime geçiniz.",
                timestamp = DateTime.UtcNow
            });
            return;
        }

        // Device fingerprint doğrulama
        var validateDevice = bool.Parse(
            configuration["CleanTenant:Session:ValidateDeviceFingerprint"] ?? "true");
        var validateIp = bool.Parse(
            configuration["CleanTenant:Session:ValidateIpAddress"] ?? "true");

        if (validateDevice || validateIp)
        {
            var ipAddress = context.Connection.RemoteIpAddress?.ToString() ?? "";
            var userAgent = context.Request.Headers["User-Agent"].ToString();

            var deviceHash = validateIp
                ? DeviceFingerprintService.GenerateHash(ipAddress, userAgent)
                : DeviceFingerprintService.GenerateHashWithoutIp(userAgent);

            var tokenString = context.Request.Headers.Authorization
                .ToString().Replace("Bearer ", "");
            var tokenHash = TokenService.HashToken(tokenString);

            var isValid = await sessionManager.ValidateSessionAsync(userId, tokenHash, deviceHash);

            if (!isValid)
            {
                _logger.LogWarning(
                    "[SESSION] Geçersiz oturum: UserId={UserId}, IP={IpAddress}",
                    userId, ipAddress);

                context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                await context.Response.WriteAsJsonAsync(new
                {
                    isSuccess = false,
                    statusCode = 401,
                    message = "Oturumunuz sonlanmıştır. Lütfen tekrar giriş yapınız.",
                    timestamp = DateTime.UtcNow
                });
                return;
            }
        }

        await _next(context);
    }
}
