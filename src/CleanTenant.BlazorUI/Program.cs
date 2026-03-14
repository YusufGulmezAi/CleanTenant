// ============================================================================
// CleanTenant.BlazorUI — Blazor Web App Giriş Noktası
// ============================================================================
// Bu dosya Faz 6'da (MudBlazor UI) detaylı olarak yapılandırılacaktır.
// Şimdilik solution'ın sorunsuz derlenmesi için minimal yapıdadır.
// ============================================================================

var builder = WebApplication.CreateBuilder(args);
var app = builder.Build();

app.MapGet("/", () => "CleanTenant Blazor UI - Faz 6'da yapılandırılacak.");

app.Run();
