using CleanTenant.Application.Common.Interfaces;
using CleanTenant.Infrastructure.Caching;
using CleanTenant.Infrastructure.Persistence;
using CleanTenant.Infrastructure.Persistence.Interceptors;
using CleanTenant.Infrastructure.Security;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using StackExchange.Redis;

namespace CleanTenant.Infrastructure;

/// <summary>
/// Infrastructure katmanı servis kayıtları.
/// Program.cs'de <c>builder.Services.AddInfrastructureServices(configuration)</c> ile çağrılır.
/// </summary>
public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructureServices(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        // ================================================================
        // REDIS — Dağıtık Cache
        // Singleton: Tüm istekler tek bağlantı üzerinden çalışır
        // ================================================================
        var redisConnectionString = configuration.GetConnectionString("Redis")
            ?? throw new InvalidOperationException("Redis bağlantı dizesi yapılandırılmamış.");

        services.AddSingleton<IConnectionMultiplexer>(sp =>
            ConnectionMultiplexer.Connect(redisConnectionString));

        services.AddScoped<ICacheService, RedisCacheService>();

        // ================================================================
        // INTERCEPTOR'LAR
        // ================================================================
        services.AddScoped<AuditableInterceptor>();
        services.AddScoped<SoftDeleteInterceptor>();
        services.AddScoped<AuditTrailInterceptor>();

        // ================================================================
        // ANA VERİTABANI — ApplicationDbContext
        // ================================================================
        services.AddDbContext<ApplicationDbContext>((sp, options) =>
        {
            var auditableInterceptor = sp.GetRequiredService<AuditableInterceptor>();
            var softDeleteInterceptor = sp.GetRequiredService<SoftDeleteInterceptor>();
            var auditTrailInterceptor = sp.GetRequiredService<AuditTrailInterceptor>();

            options
                .UseNpgsql(
                    configuration.GetConnectionString("MainDatabase"),
                    npgsqlOptions =>
                    {
                        npgsqlOptions.MigrationsAssembly(typeof(ApplicationDbContext).Assembly.FullName);
                        npgsqlOptions.EnableRetryOnFailure(
                            maxRetryCount: 3,
                            maxRetryDelay: TimeSpan.FromSeconds(5),
                            errorCodesToAdd: null);
                    })
                .AddInterceptors(
                    auditableInterceptor,
                    softDeleteInterceptor,
                    auditTrailInterceptor);

#if DEBUG
            options.EnableDetailedErrors();
            options.EnableSensitiveDataLogging();
#endif
        });

        services.AddScoped<IApplicationDbContext>(sp =>
            sp.GetRequiredService<ApplicationDbContext>());

        // ================================================================
        // AUDIT VERİTABANI — AuditDbContext
        // ================================================================
        services.AddDbContext<AuditDbContext>(options =>
        {
            options.UseNpgsql(
                configuration.GetConnectionString("AuditDatabase"),
                npgsqlOptions =>
                {
                    npgsqlOptions.MigrationsAssembly(typeof(AuditDbContext).Assembly.FullName);
                    npgsqlOptions.EnableRetryOnFailure(maxRetryCount: 3);
                });
        });

        services.AddScoped<IAuditDbContext>(sp =>
            sp.GetRequiredService<AuditDbContext>());

        // ================================================================
        // GÜVENLİK SERVİSLERİ
        // ================================================================
        // Token üretim/doğrulama servisi
        services.AddSingleton<TokenService>();

        // Oturum yönetimi (Redis + DB dual storage)
        services.AddScoped<ISessionManager, SessionManager>();

        // Mevcut kullanıcı bilgisi (HttpContext'ten)
        services.AddHttpContextAccessor();
        services.AddScoped<ICurrentUserService, CurrentUserService>();

        return services;
    }
}
