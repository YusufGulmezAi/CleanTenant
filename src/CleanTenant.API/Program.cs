// ============================================================================
// CleanTenant.API — Minimal API Giriş Noktası
// ============================================================================
// Clean Code: Program.cs sadece extension method çağrılarından oluşur.
// ============================================================================

using CleanTenant.API.Extensions;
using CleanTenant.Application;
using CleanTenant.Infrastructure;
using CleanTenant.Infrastructure.Persistence.Seeds;
using Scalar.AspNetCore;

// Npgsql: UTC DateTime zorunluluğu — legacy davranışı kapat
AppContext.SetSwitch("Npgsql.EnableLegacyTimestampBehavior", false);

var builder = WebApplication.CreateBuilder(args);

// ── .env dosyası desteği (opsiyonel — Docker dışında çalışırken) ───────
// Docker ortamında .env docker-compose tarafından yüklenir.
// Lokal geliştirmede appsettings.Development.json yeterlidir.
var envFile = Path.Combine(Directory.GetCurrentDirectory(), "..", "..", ".env");
if (File.Exists(envFile))
{
    foreach (var line in File.ReadAllLines(envFile))
    {
        if (string.IsNullOrWhiteSpace(line) || line.TrimStart().StartsWith('#')) continue;
        var parts = line.Split('=', 2);
        if (parts.Length == 2)
            Environment.SetEnvironmentVariable(parts[0].Trim(), parts[1].Trim());
    }
}

// ── Katman bazlı servis kayıtları ──────────────────────────────────────
builder.Services
    .AddApplicationServices()
    .AddInfrastructureServices(builder.Configuration);

// JSON: UTC DateTime'ları ISO 8601 formatında döner
builder.Services.ConfigureHttpJsonOptions(options =>
{
    options.SerializerOptions.PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase;
});

// Authentication & Authorization
builder.Services.AddAuthentication();
builder.Services.AddAuthorization();

// OpenAPI (Scalar)
builder.Services.AddOpenApi();

var app = builder.Build();

// ── Middleware pipeline (sıra kritik!) ──────────────────────────────────
app.UseCleanTenantMiddleware();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.MapScalarApiReference();
}

// ── Endpoint'ler ───────────────────────────────────────────────────────
app.MapGet("/", () => "CleanTenant API is running.");
app.MapGet("/health", () => Results.Ok(new
{
    Status = "Healthy",
    Environment = app.Environment.EnvironmentName,
    Timestamp = DateTime.UtcNow
}));

// CleanTenant Minimal API endpoint grupları
app.MapCleanTenantEndpoints();

// ── Veritabanı migration & seed ────────────────────────────────────────
await DefaultDataSeeder.SeedAsync(app.Services);

app.Run();

public partial class Program { }
