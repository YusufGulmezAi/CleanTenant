using CleanTenant.API.Extensions;
using CleanTenant.Application.Common.Interfaces;
using CleanTenant.Application.Features.Auth.Commands;
using CleanTenant.Application.Features.Auth.Queries;
using CleanTenant.Shared.DTOs.Auth;
using MediatR;
using Microsoft.EntityFrameworkCore;

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

        // ── E-posta Doğrulama ────────────────────────────────────────────
        group.MapPost("/send-email-verification", SendEmailVerification)
            .WithSummary("E-posta doğrulama kodu gönder")
            .RequireAuthorization();

        group.MapPost("/confirm-email", ConfirmEmail)
            .WithSummary("E-posta doğrulama kodunu onayla")
            .RequireAuthorization();

        // ── 2FA Yönetimi ───────────────────────────────────────────────────
        group.MapGet("/2fa/status", Get2FAStatus)
            .WithSummary("2FA durumunu getir")
            .RequireAuthorization();

        group.MapPost("/2fa/enable-email", Enable2FAEmail)
            .WithSummary("E-posta ile 2FA aktifleştir")
            .RequireAuthorization();

        group.MapPost("/2fa/setup-authenticator", SetupAuthenticator)
            .WithSummary("Authenticator kurulumu — QR kod + secret döner")
            .RequireAuthorization();

        group.MapPost("/2fa/verify-authenticator", VerifyAuthenticator)
            .WithSummary("Authenticator kodunu doğrula ve aktifleştir")
            .RequireAuthorization();

        group.MapPost("/2fa/disable", Disable2FA)
            .WithSummary("Tüm 2FA yöntemlerini devre dışı bırak")
            .RequireAuthorization();

        group.MapPost("/2fa/enable-sms", EnableSms)
            .WithSummary("SMS ile 2FA aktifleştir (telefon doğrulama kodu gönderir)")
            .RequireAuthorization();

        group.MapPost("/2fa/verify-sms", VerifySms)
            .WithSummary("SMS kodunu doğrula ve SMS 2FA aktifleştir")
            .RequireAuthorization();

        group.MapPost("/2fa/set-primary", SetPrimary2FA)
            .WithSummary("Primary 2FA yöntemini değiştir")
            .RequireAuthorization();

        group.MapPost("/2fa/disable-method", DisableSpecific2FA)
            .WithSummary("Belirli bir 2FA yöntemini kapat (diğerleri kalsın)")
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

    // ── 2FA Yönetim Endpoint'leri ──────────────────────────────────────

    /// <summary>GET /api/auth/2fa/status</summary>
    private static async Task<IResult> Get2FAStatus(ISender sender, CancellationToken ct)
    {
        var result = await sender.Send(new Get2FAStatusQuery(), ct);
        return result.ToApiResponse();
    }

    /// <summary>POST /api/auth/2fa/enable-email</summary>
    private static async Task<IResult> Enable2FAEmail(
        Enable2FAEmailDto dto, ISender sender, CancellationToken ct)
    {
        var result = await sender.Send(new Enable2FAEmailCommand(dto), ct);
        return result.ToApiResponse();
    }

    /// <summary>POST /api/auth/2fa/setup-authenticator</summary>
    private static async Task<IResult> SetupAuthenticator(ISender sender, CancellationToken ct)
    {
        var result = await sender.Send(new SetupAuthenticatorCommand(), ct);
        return result.ToApiResponse();
    }

    /// <summary>POST /api/auth/2fa/verify-authenticator</summary>
    private static async Task<IResult> VerifyAuthenticator(
        VerifyAuthenticatorDto dto, ISender sender, CancellationToken ct)
    {
        var result = await sender.Send(new VerifyAuthenticatorCommand(dto), ct);
        return result.ToApiResponse();
    }

    /// <summary>POST /api/auth/2fa/disable</summary>
    private static async Task<IResult> Disable2FA(
        Disable2FADto dto, ISender sender, CancellationToken ct)
    {
        var result = await sender.Send(new Disable2FACommand(dto), ct);
        return result.ToApiResponse();
    }

    // ── E-posta Doğrulama Endpoint'leri ────────────────────────────────

    /// <summary>POST /api/auth/send-email-verification</summary>
    private static async Task<IResult> SendEmailVerification(
        ICurrentUserService currentUser, IApplicationDbContext db,
        ICacheService cache, IEmailService emailService, CancellationToken ct)
    {
        if (currentUser.UserId is null)
            return Results.Json(new { isSuccess = false, message = "Yetkisiz." }, statusCode: 401);

        var user = await db.Users.FirstOrDefaultAsync(u => u.Id == currentUser.UserId, ct);
        if (user is null)
            return Results.Json(new { isSuccess = false, message = "Kullanıcı bulunamadı." }, statusCode: 404);

        if (user.EmailConfirmed)
            return Results.Json(new { isSuccess = false, message = "E-posta zaten doğrulanmış." }, statusCode: 400);

        // 6 haneli doğrulama kodu üret ve Redis'e kaydet (5dk TTL)
        var code = CleanTenant.Shared.Helpers.SecurityHelper.GenerateVerificationCode(6);
        await cache.SetAsync($"ct:email-verify:{user.Id}", code, TimeSpan.FromMinutes(5), ct);

        // Gerçek e-posta gönder (EmailLog ID döner)
        try
        {
            var emailId = await emailService.SendEmailVerificationCodeAsync(user.Email, code, ct);
            return Results.Ok(new
            {
                isSuccess = true,
                message = "Doğrulama kodu e-posta adresinize gönderildi.",
                emailId,
                devCode = code  // TODO: Production'da kaldır
            });
        }
        catch
        {
            return Results.Ok(new
            {
                isSuccess = true,
                message = $"E-posta gönderilemedi (SMTP yapılandırılmamış olabilir). DEV KODU: {code}",
                devCode = code
            });
        }
    }

    /// <summary>POST /api/auth/confirm-email</summary>
    private static async Task<IResult> ConfirmEmail(
        ConfirmEmailDto dto, ICurrentUserService currentUser,
        IApplicationDbContext db, ICacheService cache, CancellationToken ct)
    {
        if (currentUser.UserId is null)
            return Results.Json(new { isSuccess = false, message = "Yetkisiz." }, statusCode: 401);

        var user = await db.Users.FirstOrDefaultAsync(u => u.Id == currentUser.UserId, ct);
        if (user is null)
            return Results.Json(new { isSuccess = false, message = "Kullanıcı bulunamadı." }, statusCode: 404);

        // Redis'ten kodu kontrol et
        var cachedCode = await cache.GetAsync<string>($"ct:email-verify:{user.Id}", ct);
        if (cachedCode is null || cachedCode != dto.Code)
            return Results.Json(new { isSuccess = false, message = "Doğrulama kodu hatalı veya süresi dolmuş." }, statusCode: 400);

        // E-postayı doğrulanmış olarak işaretle
        user.ConfirmEmail();
        await db.SaveChangesAsync(ct);

        // Kodu Redis'ten sil
        await cache.RemoveAsync($"ct:email-verify:{user.Id}", ct);

        return Results.Ok(new { isSuccess = true, message = "E-posta başarıyla doğrulandı." });
    }

    // ── SMS + Çoklu 2FA Yönetim Endpoint'leri ──────────────────────────

    private static async Task<IResult> EnableSms(
        Enable2FASmsDto dto, ISender sender, CancellationToken ct)
    {
        var result = await sender.Send(new Enable2FASmsCommand(dto), ct);
        return result.ToApiResponse();
    }

    private static async Task<IResult> VerifySms(
        VerifySmsDto dto, ISender sender, CancellationToken ct)
    {
        var result = await sender.Send(new VerifySmsCommand(dto), ct);
        return result.ToApiResponse();
    }

    private static async Task<IResult> SetPrimary2FA(
        SetPrimary2FADto dto, ISender sender, CancellationToken ct)
    {
        var result = await sender.Send(new SetPrimary2FACommand(dto), ct);
        return result.ToApiResponse();
    }

    private static async Task<IResult> DisableSpecific2FA(
        DisableSpecific2FARequest body, ISender sender, CancellationToken ct)
    {
        var result = await sender.Send(new DisableSpecific2FACommand
        {
            Method = body.Method,
            CurrentPassword = body.CurrentPassword
        }, ct);
        return result.ToApiResponse();
    }
}

public class DisableSpecific2FARequest
{
    public string Method { get; set; } = default!;
    public string CurrentPassword { get; set; } = default!;
}
