using Blazored.LocalStorage;
using CleanTenant.BlazorUI.Components;
using CleanTenant.BlazorUI.Services;
using Microsoft.AspNetCore.Components.Authorization;
using MudBlazor;
using MudBlazor.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

builder.Services.AddMudServices(config =>
{
    config.SnackbarConfiguration.PositionClass = Defaults.Classes.Position.BottomRight;
    config.SnackbarConfiguration.PreventDuplicates = true;
    config.SnackbarConfiguration.ShowTransitionDuration = 200;
    config.SnackbarConfiguration.HideTransitionDuration = 200;
    config.SnackbarConfiguration.SnackbarVariant = Variant.Filled;
});

builder.Services.AddBlazoredLocalStorage();
builder.Services.AddAuthorizationCore(options =>
{
	options.FallbackPolicy = null; // Authorize attribute çalýþsýn ama middleware scheme aramasýn
});

builder.Services.AddAuthentication("Cookies").AddCookie("Cookies", options =>
{
	options.LoginPath = "/login";
});

builder.Services.AddAuthorization();

builder.Services.AddCascadingAuthenticationState();
builder.Services.AddScoped<AuthenticationStateProvider, CleanTenantAuthStateProvider>();
builder.Services.AddScoped<CleanTenantAuthStateProvider>();

var apiBaseUrl = builder.Configuration["ApiSettings:BaseUrl"] ?? "http://localhost:54491";

builder.Services.AddScoped(sp => new HttpClient { BaseAddress = new Uri(apiBaseUrl) });
builder.Services.AddScoped<ApiClient>();
builder.Services.AddScoped<ThemeService>();

var app = builder.Build();

if (!app.Environment.IsDevelopment())
    app.UseHsts();

app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseAntiforgery();

app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.Run();
