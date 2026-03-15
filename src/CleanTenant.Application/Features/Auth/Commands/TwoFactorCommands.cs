using CleanTenant.Application.Common.Interfaces;
using CleanTenant.Application.Common.Models;
using CleanTenant.Domain.Enums;
using CleanTenant.Shared.DTOs.Auth;
using CleanTenant.Shared.Helpers;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace CleanTenant.Application.Features.Auth.Commands;

// ============================================================================
// GET 2FA STATUS
// ============================================================================

public record Get2FAStatusQuery : IRequest<Result<TwoFactorStatusDto>>;

public class Get2FAStatusHandler : IRequestHandler<Get2FAStatusQuery, Result<TwoFactorStatusDto>>
{
    private readonly IApplicationDbContext _db;
    private readonly ICurrentUserService _currentUser;

    public Get2FAStatusHandler(IApplicationDbContext db, ICurrentUserService currentUser)
    {
        _db = db;
        _currentUser = currentUser;
    }

    public async Task<Result<TwoFactorStatusDto>> Handle(Get2FAStatusQuery request, CancellationToken ct)
    {
        if (_currentUser.UserId is null)
            return Result<TwoFactorStatusDto>.Unauthorized();

        var user = await _db.Users.FirstOrDefaultAsync(u => u.Id == _currentUser.UserId, ct);
        if (user is null)
            return Result<TwoFactorStatusDto>.NotFound("Kullanıcı bulunamadı.");

        return Result<TwoFactorStatusDto>.Success(new TwoFactorStatusDto
        {
            IsEnabled = user.TwoFactorEnabled,
            Method = user.PrimaryTwoFactorMethod.ToString(),
            HasAuthenticator = !string.IsNullOrEmpty(user.AuthenticatorKey),
            EmailConfirmed = user.EmailConfirmed,
            PhoneConfirmed = user.PhoneNumberConfirmed
        });
    }
}

// ============================================================================
// ENABLE 2FA — EMAIL
// ============================================================================

/// <summary>
/// E-posta ile 2FA aktifleştir.
/// Kullanıcının e-postası doğrulanmış olmalı.
/// Şifre ile onay gerekli — yanlışlıkla aktifleştirme engellensin.
/// </summary>
public record Enable2FAEmailCommand(Enable2FAEmailDto Dto) : IRequest<Result<object>>;

public class Enable2FAEmailValidator : AbstractValidator<Enable2FAEmailCommand>
{
    public Enable2FAEmailValidator()
    {
        RuleFor(x => x.Dto.CurrentPassword).NotEmpty().WithMessage("Şifre zorunludur.");
    }
}

public class Enable2FAEmailHandler : IRequestHandler<Enable2FAEmailCommand, Result<object>>
{
    private readonly IApplicationDbContext _db;
    private readonly ICurrentUserService _currentUser;
    private readonly ILogger<Enable2FAEmailHandler> _logger;

    public Enable2FAEmailHandler(IApplicationDbContext db, ICurrentUserService currentUser, ILogger<Enable2FAEmailHandler> logger)
    {
        _db = db;
        _currentUser = currentUser;
        _logger = logger;
    }

    public async Task<Result<object>> Handle(Enable2FAEmailCommand request, CancellationToken ct)
    {
        if (_currentUser.UserId is null)
            return Result<object>.Unauthorized();

        var user = await _db.Users.FirstOrDefaultAsync(u => u.Id == _currentUser.UserId, ct);
        if (user is null)
            return Result<object>.NotFound("Kullanıcı bulunamadı.");

        // Şifre doğrulama
        if (!VerifyPassword(request.Dto.CurrentPassword, user.PasswordHash))
            return Result<object>.Failure("Şifre hatalıdır.", 401);

        // E-posta doğrulanmış mı?
        if (!user.EmailConfirmed)
            return Result<object>.Failure("E-posta adresiniz doğrulanmamış. Önce e-posta doğrulaması yapınız.", 400);

        // 2FA zaten aktif mi?
        if (user.TwoFactorEnabled)
            return Result<object>.Failure($"2FA zaten aktif. Mevcut metod: {user.PrimaryTwoFactorMethod}", 400);

        user.EnableTwoFactor(TwoFactorMethod.Email);
        await _db.SaveChangesAsync(ct);

        _logger.LogInformation("[2FA] E-posta ile 2FA aktifleştirildi: {Email}", user.Email);

        return Result.Success();
    }

    private static bool VerifyPassword(string password, string hash)
    {
        if (hash == "CHANGE_ON_FIRST_LOGIN") return password == "Admin123!";
        return SecurityHelper.VerifyPassword(password, hash);
    }
}

// ============================================================================
// SETUP AUTHENTICATOR — QR Kod + Secret döner
// ============================================================================

/// <summary>
/// Authenticator kurulumu başlat.
/// Secret key + QR kod URI döner.
/// Kullanıcı Authenticator uygulamasına tarar.
/// Henüz aktifleştirmez — verify-authenticator ile tamamlanır.
/// </summary>
public record SetupAuthenticatorCommand : IRequest<Result<SetupAuthenticatorResponseDto>>;

public class SetupAuthenticatorHandler : IRequestHandler<SetupAuthenticatorCommand, Result<SetupAuthenticatorResponseDto>>
{
    private readonly IApplicationDbContext _db;
    private readonly ICurrentUserService _currentUser;

    public SetupAuthenticatorHandler(IApplicationDbContext db, ICurrentUserService currentUser)
    {
        _db = db;
        _currentUser = currentUser;
    }

    public async Task<Result<SetupAuthenticatorResponseDto>> Handle(SetupAuthenticatorCommand request, CancellationToken ct)
    {
        if (_currentUser.UserId is null)
            return Result<SetupAuthenticatorResponseDto>.Unauthorized();

        var user = await _db.Users.FirstOrDefaultAsync(u => u.Id == _currentUser.UserId, ct);
        if (user is null)
            return Result<SetupAuthenticatorResponseDto>.NotFound("Kullanıcı bulunamadı.");

        if (!user.EmailConfirmed)
            return Result<SetupAuthenticatorResponseDto>.Failure("Önce e-posta doğrulaması yapınız.", 400);

        // Yeni secret key üret
        var secretKey = SecurityHelper.GenerateAuthenticatorKey();
        var qrCodeUri = SecurityHelper.GenerateAuthenticatorUri(user.Email, secretKey);

		return Result<SetupAuthenticatorResponseDto>.Success(new SetupAuthenticatorResponseDto
		{
			SecretKey = secretKey,
			QrCodeUri = qrCodeUri,
			QrCodeImageUrl = $"https://api.qrserver.com/v1/create-qr-code/?size=300x300&data={Uri.EscapeDataString(qrCodeUri)}"
		});
	}
}

// ============================================================================
// VERIFY AUTHENTICATOR — Kodu doğrula ve aktifleştir
// ============================================================================

/// <summary>
/// Authenticator kurulumunu tamamla.
/// Kullanıcı Authenticator'dan aldığı 6 haneli kodu gönderir.
/// Doğruysa 2FA aktifleşir + secret key DB'ye kaydedilir.
/// </summary>
public record VerifyAuthenticatorCommand(VerifyAuthenticatorDto Dto) : IRequest<Result<object>>;

public class VerifyAuthenticatorValidator : AbstractValidator<VerifyAuthenticatorCommand>
{
    public VerifyAuthenticatorValidator()
    {
        RuleFor(x => x.Dto.SecretKey).NotEmpty().WithMessage("Secret key zorunludur.");
        RuleFor(x => x.Dto.Code).NotEmpty().Length(6).WithMessage("Kod 6 haneli olmalıdır.");
    }
}

public class VerifyAuthenticatorHandler : IRequestHandler<VerifyAuthenticatorCommand, Result<object>>
{
    private readonly IApplicationDbContext _db;
    private readonly ICurrentUserService _currentUser;
    private readonly ILogger<VerifyAuthenticatorHandler> _logger;

    public VerifyAuthenticatorHandler(IApplicationDbContext db, ICurrentUserService currentUser, ILogger<VerifyAuthenticatorHandler> logger)
    {
        _db = db;
        _currentUser = currentUser;
        _logger = logger;
    }

    public async Task<Result<object>> Handle(VerifyAuthenticatorCommand request, CancellationToken ct)
    {
        if (_currentUser.UserId is null)
            return Result<object>.Unauthorized();

        var user = await _db.Users.FirstOrDefaultAsync(u => u.Id == _currentUser.UserId, ct);
        if (user is null)
            return Result<object>.NotFound("Kullanıcı bulunamadı.");

        // TOTP kodu doğrula
        var isValid = SecurityHelper.VerifyTotpCode(request.Dto.SecretKey, request.Dto.Code);
        if (!isValid)
            return Result<object>.Failure("Doğrulama kodu hatalı. Authenticator uygulamanızdaki kodu tekrar deneyin.", 400);

        // Başarılı — 2FA aktifleştir ve secret key'i kaydet
        user.EnableTwoFactor(TwoFactorMethod.Authenticator, request.Dto.SecretKey);
        await _db.SaveChangesAsync(ct);

        _logger.LogInformation("[2FA] Authenticator ile 2FA aktifleştirildi: {Email}", user.Email);

        return Result.Success();
    }
}

// ============================================================================
// DISABLE 2FA
// ============================================================================

public record Disable2FACommand(Disable2FADto Dto) : IRequest<Result<object>>;

public class Disable2FAValidator : AbstractValidator<Disable2FACommand>
{
    public Disable2FAValidator()
    {
        RuleFor(x => x.Dto.CurrentPassword).NotEmpty().WithMessage("Şifre zorunludur.");
    }
}

public class Disable2FAHandler : IRequestHandler<Disable2FACommand, Result<object>>
{
    private readonly IApplicationDbContext _db;
    private readonly ICurrentUserService _currentUser;
    private readonly ILogger<Disable2FAHandler> _logger;

    public Disable2FAHandler(IApplicationDbContext db, ICurrentUserService currentUser, ILogger<Disable2FAHandler> logger)
    {
        _db = db;
        _currentUser = currentUser;
        _logger = logger;
    }

    public async Task<Result<object>> Handle(Disable2FACommand request, CancellationToken ct)
    {
        if (_currentUser.UserId is null)
            return Result<object>.Unauthorized();

        var user = await _db.Users.FirstOrDefaultAsync(u => u.Id == _currentUser.UserId, ct);
        if (user is null)
            return Result<object>.NotFound("Kullanıcı bulunamadı.");

        if (!user.TwoFactorEnabled)
            return Result<object>.Failure("2FA zaten devre dışı.", 400);

        // Şifre doğrulama
        if (!VerifyPassword(request.Dto.CurrentPassword, user.PasswordHash))
            return Result<object>.Failure("Şifre hatalıdır.", 401);

        user.DisableTwoFactor();
        await _db.SaveChangesAsync(ct);

        _logger.LogInformation("[2FA] 2FA devre dışı bırakıldı: {Email}", user.Email);

        return Result.Success();
    }

    private static bool VerifyPassword(string password, string hash)
    {
        if (hash == "CHANGE_ON_FIRST_LOGIN") return password == "Admin123!";
        return SecurityHelper.VerifyPassword(password, hash);
    }
}
