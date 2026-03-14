using CleanTenant.API.Extensions;
using CleanTenant.Application.Features.Auth.Commands;
using CleanTenant.Application.Features.Auth.Queries;
using CleanTenant.Shared.DTOs.Auth;
using MediatR;

namespace CleanTenant.API.Endpoints;

/// <summary>
/// Kimlik doğrulama Minimal API endpoint'leri.
/// 
/// <para><b>ENDPOINT AKIŞI:</b></para>
/// <code>
/// [1] POST /api/auth/login          → Şifre doğrula → 2FA açıksa TempToken dön
/// [2] POST /api/auth/verify-2fa     → TempToken + Kod → Gerçek token dön
/// [3] POST /api/auth/2fa-fallback   → TempToken → E-posta ile kod gönder
/// [4] POST /api/auth/refresh        → Eski token → Yeni token (rotation)
/// [5] POST /api/auth/logout         → Oturumu sonlandır
/// [6] POST /api/auth/change-password → Şifre değiştir + diğer oturumları kapat
/// [7] POST /api/auth/forgot-password → Şifre sıfırlama linki gönder
/// [8] GET  /api/auth/me             → Mevcut kullanıcı bilgileri
/// [9] GET  /api/auth/context        → Erişilebilir tenant/şirket listesi
/// </code>
/// </summary>
public static class AuthEndpoints
{
    public static void MapAuthEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/auth")
            .WithTags("Authentication");

        // Anonim erişim (login gerektirmez)
        group.MapPost("/login", Login)
            .WithSummary("Giriş yap — 2FA açıksa TempToken döner")
            .AllowAnonymous();

        group.MapPost("/verify-2fa", Verify2FA)
            .WithSummary("2FA doğrulama — TempToken + Kod ile gerçek token al")
            .AllowAnonymous();

        group.MapPost("/2fa-fallback", Request2FAFallback)
            .WithSummary("2FA fallback — e-posta ile doğrulama kodu iste")
            .AllowAnonymous();

        group.MapPost("/refresh", RefreshToken)
            .WithSummary("Token yenile — RefreshToken rotation ile")
            .AllowAnonymous();

        group.MapPost("/forgot-password", ForgotPassword)
            .WithSummary("Şifre sıfırlama linki gönder")
            .AllowAnonymous();

        // Kimlik doğrulaması gerekli
        group.MapPost("/logout", Logout)
            .WithSummary("Çıkış yap — oturumu sonlandır")
            .RequireAuthorization();

        group.MapPost("/change-password", ChangePassword)
            .WithSummary("Şifre değiştir — diğer oturumlar sonlandırılır")
            .RequireAuthorization();

        group.MapGet("/me", GetCurrentUser)
            .WithSummary("Mevcut kullanıcı bilgilerini getir")
            .RequireAuthorization();

        group.MapGet("/context", GetUserContext)
            .WithSummary("Kullanıcının erişebildiği tenant/şirket bağlamlarını getir")
            .RequireAuthorization();
    }

    /// <summary>POST /api/auth/login</summary>
    private static async Task<IResult> Login(
        LoginRequestDto dto, HttpContext context, ISender sender, CancellationToken ct)
    {
        // IP adresini DTO'ya ekle (endpoint seviyesinde — handler'dan bağımsız)
        dto.DeviceFingerprint ??= context.Connection.RemoteIpAddress?.ToString();

        var result = await sender.Send(new LoginCommand(dto), ct);
        return result.ToApiResponse();
    }

    /// <summary>POST /api/auth/verify-2fa</summary>
    private static async Task<IResult> Verify2FA(
        TwoFactorVerifyDto dto, ISender sender, CancellationToken ct)
    {
        var result = await sender.Send(new Verify2FACommand(dto), ct);
        return result.ToApiResponse();
    }

    /// <summary>POST /api/auth/2fa-fallback</summary>
    private static async Task<IResult> Request2FAFallback(
        TwoFactorFallbackRequestDto dto, ISender sender, CancellationToken ct)
    {
        var result = await sender.Send(new Request2FAFallbackCommand(dto), ct);
        return result.ToApiResponse();
    }

    /// <summary>POST /api/auth/refresh</summary>
    private static async Task<IResult> RefreshToken(
        RefreshTokenDto dto, ISender sender, CancellationToken ct)
    {
        var result = await sender.Send(new RefreshTokenCommand(dto), ct);
        return result.ToApiResponse();
    }

    /// <summary>POST /api/auth/logout</summary>
    private static async Task<IResult> Logout(ISender sender, CancellationToken ct)
    {
        var result = await sender.Send(new LogoutCommand(), ct);
        return result.ToApiResponse();
    }

    /// <summary>POST /api/auth/change-password</summary>
    private static async Task<IResult> ChangePassword(
        ChangePasswordDto dto, ISender sender, CancellationToken ct)
    {
        var result = await sender.Send(new ChangePasswordCommand(dto), ct);
        return result.ToApiResponse();
    }

    /// <summary>POST /api/auth/forgot-password</summary>
    private static async Task<IResult> ForgotPassword(
        ForgotPasswordDto dto, ISender sender, CancellationToken ct)
    {
        var result = await sender.Send(new ForgotPasswordCommand(dto), ct);
        return result.ToApiResponse();
    }

    /// <summary>GET /api/auth/me</summary>
    private static async Task<IResult> GetCurrentUser(ISender sender, CancellationToken ct)
    {
        var result = await sender.Send(new GetCurrentUserQuery(), ct);
        return result.ToApiResponse();
    }

    /// <summary>GET /api/auth/context</summary>
    private static async Task<IResult> GetUserContext(ISender sender, CancellationToken ct)
    {
        var result = await sender.Send(new GetUserContextQuery(), ct);
        return result.ToApiResponse();
    }
}
