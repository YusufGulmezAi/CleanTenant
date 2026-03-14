using CleanTenant.API.Middleware;

namespace CleanTenant.API.Extensions;

/// <summary>
/// Middleware pipeline extension method'ları.
/// Program.cs'de <c>app.UseCleanTenantMiddleware()</c> ile çağrılır.
/// 
/// <para><b>MIDDLEWARE SIRASI KRİTİKTİR:</b></para>
/// <code>
/// İstek geldi
///   ↓ [1] ExceptionHandling   → Tüm hataları yakalar (en dışta olmalı)
///   ↓ [2] RequestLogging      → Her isteği loglar
///   ↓ [3] IpBlacklist         → Kara listedeki IP'leri engeller
///   ↓ [4] RateLimit           → İstek sayısını sınırlar
///   ↓ [5] Authentication      → JWT token doğrular (.NET built-in)
///   ↓ [6] SessionValidation   → Redis'ten oturum/bloke kontrolü
///   ↓ [7] Authorization       → Yetki kontrolü (.NET built-in)
///   ↓ [8] Endpoint            → Minimal API handler
/// </code>
/// 
/// Neden bu sıra?
/// <list type="bullet">
///   <item>ExceptionHandling en dışta — tüm katmanların hatalarını yakalar</item>
///   <item>IpBlacklist, Authentication'dan ÖNCE — bloke IP'ler token bile gönderemesin</item>
///   <item>RateLimit, Authentication'dan ÖNCE — brute force koruması</item>
///   <item>SessionValidation, Authentication SONRASINDA — userId gerekli</item>
/// </list>
/// </summary>
public static class MiddlewareExtensions
{
    /// <summary>
    /// CleanTenant middleware pipeline'ını yapılandırır.
    /// </summary>
    public static WebApplication UseCleanTenantMiddleware(this WebApplication app)
    {
        // [1] Global hata yönetimi — en dışta
        app.UseMiddleware<ExceptionHandlingMiddleware>();

        // [2] HTTP istek loglama
        app.UseMiddleware<RequestLoggingMiddleware>();

        // [3] IP kara liste kontrolü — authentication'dan ÖNCE
        app.UseMiddleware<IpBlacklistMiddleware>();

        // [4] Rate limiting — brute force koruması
        app.UseMiddleware<RateLimitMiddleware>();

        // [5] Authentication — .NET built-in JWT doğrulama
        app.UseAuthentication();

        // [6] Oturum doğrulama — Redis kontrolü (bloke, device, session)
        app.UseMiddleware<SessionValidationMiddleware>();

        // [7] Authorization — .NET built-in yetki kontrolü
        app.UseAuthorization();

        return app;
    }
}
