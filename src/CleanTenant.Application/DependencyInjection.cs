using CleanTenant.Application.Common.Behaviors;
using CleanTenant.Application.Common.Rules;
using FluentValidation;
using MediatR;
using Microsoft.Extensions.DependencyInjection;

namespace CleanTenant.Application;

/// <summary>
/// Application katmanı servis kayıtları.
/// Program.cs'de <c>builder.Services.AddApplicationServices()</c> ile çağrılır.
/// 
/// <para><b>PIPELINE BEHAVIOR SIRASI ÖNEMLİDİR:</b></para>
/// MediatR pipeline'ında behavior'lar kayıt sırasına göre çalışır:
/// <code>
/// İstek → [Validation] → [Logging] → [Authorization] → [Caching] → Handler
///                                                     ↓
///                                           [CacheInvalidation] (Command sonrası)
/// </code>
/// 
/// <list type="number">
///   <item><b>Validation:</b> İlk çalışır — geçersiz veri handler'a ulaşmaz</item>
///   <item><b>Logging:</b> Her isteği loglar (valid/invalid farketmez süre ölçer)</item>
///   <item><b>Authorization:</b> Kullanıcı yetkisi kontrol edilir</item>
///   <item><b>Caching:</b> Query cache'ten dönebilir (handler çalışmaz)</item>
///   <item><b>CacheInvalidation:</b> Command başarılıysa ilgili cache'leri siler</item>
/// </list>
/// </summary>
public static class DependencyInjection
{
    public static IServiceCollection AddApplicationServices(this IServiceCollection services)
    {
        var assembly = typeof(DependencyInjection).Assembly;

        // ================================================================
        // MediatR + Pipeline Behaviors
        // ================================================================
        services.AddMediatR(cfg =>
        {
            cfg.RegisterServicesFromAssembly(assembly);

            // Pipeline behavior kayıt sırası = çalışma sırası
            cfg.AddBehavior(typeof(IPipelineBehavior<,>), typeof(ValidationBehavior<,>));
            cfg.AddBehavior(typeof(IPipelineBehavior<,>), typeof(LoggingBehavior<,>));
            cfg.AddBehavior(typeof(IPipelineBehavior<,>), typeof(AuthorizationBehavior<,>));
            cfg.AddBehavior(typeof(IPipelineBehavior<,>), typeof(CachingBehavior<,>));
            cfg.AddBehavior(typeof(IPipelineBehavior<,>), typeof(CacheInvalidationBehavior<,>));
        });

        // ================================================================
        // FluentValidation
        // ================================================================
        services.AddValidatorsFromAssembly(assembly);

        // ================================================================
        // Business Rules
        // ================================================================
        services.AddScoped<TenantRules>();
        services.AddScoped<CompanyRules>();
        services.AddScoped<UserRules>();
        services.AddScoped<AuthorizationRules>();

        return services;
    }
}
