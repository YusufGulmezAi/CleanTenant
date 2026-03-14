using System.Text.Json;
using FluentValidation;

namespace CleanTenant.API.Middleware;

/// <summary>
/// Global hata yönetimi middleware'i — tüm yakalanmamış exception'ları ele alır.
/// 
/// <para><b>NEDEN GLOBAL MIDDLEWARE?</b></para>
/// Her endpoint'te try-catch yazmak yerine tek bir noktada tüm hataları
/// yakalayıp standart ApiResponse formatında döneriz. Böylece:
/// <list type="bullet">
///   <item>UI tarafı her zaman aynı hata formatını parse eder</item>
///   <item>Hassas hata detayları (stack trace) production'da gizlenir</item>
///   <item>Tüm hatalar loglanır</item>
///   <item>Exception tipine göre uygun HTTP status code döner</item>
/// </list>
/// 
/// <para><b>EXCEPTION → STATUS CODE EŞLEMESİ:</b></para>
/// <code>
/// ValidationException      → 422 Unprocessable Entity
/// UnauthorizedAccessException → 401 Unauthorized
/// InvalidOperationException → 400 Bad Request
/// KeyNotFoundException     → 404 Not Found
/// Diğer tüm exception'lar → 500 Internal Server Error
/// </code>
/// </summary>
public class ExceptionHandlingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<ExceptionHandlingMiddleware> _logger;
    private readonly IHostEnvironment _env;

    public ExceptionHandlingMiddleware(
        RequestDelegate next,
        ILogger<ExceptionHandlingMiddleware> logger,
        IHostEnvironment env)
    {
        _next = next;
        _logger = logger;
        _env = env;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await _next(context);
        }
        catch (Exception ex)
        {
            await HandleExceptionAsync(context, ex);
        }
    }

    private async Task HandleExceptionAsync(HttpContext context, Exception exception)
    {
        var (statusCode, message, errors) = exception switch
        {
            ValidationException validationEx => (
                StatusCodes.Status422UnprocessableEntity,
                "Doğrulama hataları oluştu.",
                validationEx.Errors.Select(e => e.ErrorMessage).Distinct().ToList()),

            UnauthorizedAccessException => (
                StatusCodes.Status401Unauthorized,
                exception.Message.Length > 0 ? exception.Message : "Yetkiniz bulunmamaktadır.",
                (List<string>?)null),

            InvalidOperationException => (
                StatusCodes.Status400BadRequest,
                exception.Message,
                (List<string>?)null),

            KeyNotFoundException => (
                StatusCodes.Status404NotFound,
                exception.Message.Length > 0 ? exception.Message : "Kayıt bulunamadı.",
                (List<string>?)null),

            ArgumentException => (
                StatusCodes.Status400BadRequest,
                exception.Message,
                (List<string>?)null),

            _ => (
                StatusCodes.Status500InternalServerError,
                "Beklenmeyen bir hata oluştu. Lütfen daha sonra tekrar deneyiniz.",
                (List<string>?)null)
        };

        // Loglama — 5xx hatalar Error, diğerleri Warning
        if (statusCode >= 500)
        {
            _logger.LogError(exception,
                "[EXCEPTION] {StatusCode} | Path: {Path} | Message: {Message}",
                statusCode, context.Request.Path, exception.Message);
        }
        else
        {
            _logger.LogWarning(
                "[EXCEPTION] {StatusCode} | Path: {Path} | Message: {Message}",
                statusCode, context.Request.Path, exception.Message);
        }

        // Yanıt oluştur
        context.Response.StatusCode = statusCode;
        context.Response.ContentType = "application/json";

        var response = new
        {
            isSuccess = false,
            statusCode,
            message,
            errors = errors ?? (statusCode >= 500 && _env.IsDevelopment()
                ? new List<string> { exception.ToString() }  // Dev'de detaylı hata
                : null),
            timestamp = DateTime.UtcNow
        };

        var json = JsonSerializer.Serialize(response, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = _env.IsDevelopment()
        });

        await context.Response.WriteAsync(json);
    }
}
