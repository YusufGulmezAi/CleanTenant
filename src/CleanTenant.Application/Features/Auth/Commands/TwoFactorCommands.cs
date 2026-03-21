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
// GET 2FA STATUS (çoklu yöntem)
// ============================================================================

public record Get2FAStatusQuery : IRequest<Result<TwoFactorStatusDto>>;

public class Get2FAStatusHandler : IRequestHandler<Get2FAStatusQuery, Result<TwoFactorStatusDto>>
{
    private readonly IApplicationDbContext _db;
    private readonly ICurrentUserService _currentUser;

    public Get2FAStatusHandler(IApplicationDbContext db, ICurrentUserService currentUser)
    { _db = db; _currentUser = currentUser; }

    public async Task<Result<TwoFactorStatusDto>> Handle(Get2FAStatusQuery request, CancellationToken ct)
    {
        if (_currentUser.UserId is null) return Result<TwoFactorStatusDto>.Unauthorized();

        var user = await _db.Users.FirstOrDefaultAsync(u => u.Id == _currentUser.UserId, ct);
        if (user is null) return Result<TwoFactorStatusDto>.NotFound("Kullanıcı bulunamadı.");

        var methods = user.GetEnabledMethodsList();

        return Result<TwoFactorStatusDto>.Success(new TwoFactorStatusDto
        {
            IsEnabled = user.TwoFactorEnabled,
            PrimaryMethod = user.PrimaryTwoFactorMethod.ToString(),
            EnabledMethods = methods,
            EmailEnabled = methods.Contains("Email"),
            SmsEnabled = methods.Contains("Sms"),
            AuthenticatorEnabled = methods.Contains("Authenticator"),
            EmailConfirmed = user.EmailConfirmed,
            PhoneConfirmed = user.PhoneNumberConfirmed,
            HasPhoneNumber = !string.IsNullOrEmpty(user.PhoneNumber)
        });
    }
}

// ============================================================================
// ENABLE 2FA — EMAIL
// ============================================================================

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
    { _db = db; _currentUser = currentUser; _logger = logger; }

    public async Task<Result<object>> Handle(Enable2FAEmailCommand request, CancellationToken ct)
    {
        if (_currentUser.UserId is null) return Result<object>.Unauthorized();
        var user = await _db.Users.FirstOrDefaultAsync(u => u.Id == _currentUser.UserId, ct);
        if (user is null) return Result<object>.NotFound("Kullanıcı bulunamadı.");

        if (!VerifyPassword(request.Dto.CurrentPassword, user.PasswordHash))
            return Result<object>.Failure("Şifre hatalıdır.", 401);

        if (!user.EmailConfirmed)
            return Result<object>.Failure("E-posta adresiniz doğrulanmamış.", 400);

        if (user.IsTwoFactorMethodEnabled(TwoFactorMethod.Email))
            return Result<object>.Failure("E-posta 2FA zaten aktif.", 400);

        user.EnableTwoFactor(TwoFactorMethod.Email);
        await _db.SaveChangesAsync(ct);

        _logger.LogInformation("[2FA] E-posta 2FA aktifleştirildi: {Email}", user.Email);
        return Result.Success();
    }

    private static bool VerifyPassword(string password, string hash)
    {
        if (hash == "CHANGE_ON_FIRST_LOGIN") return password == "Admin123!";
        return SecurityHelper.VerifyPassword(password, hash);
    }
}

// ============================================================================
// ENABLE 2FA — SMS
// ============================================================================

public record Enable2FASmsCommand(Enable2FASmsDto Dto) : IRequest<Result<object>>;

public class Enable2FASmsValidator : AbstractValidator<Enable2FASmsCommand>
{
    public Enable2FASmsValidator()
    {
        RuleFor(x => x.Dto.CurrentPassword).NotEmpty().WithMessage("Şifre zorunludur.");
        RuleFor(x => x.Dto.PhoneNumber).NotEmpty().WithMessage("Telefon numarası zorunludur.")
            .Matches(@"^\+?[0-9]{10,15}$").WithMessage("Geçerli bir telefon numarası giriniz.");
    }
}

public class Enable2FASmsHandler : IRequestHandler<Enable2FASmsCommand, Result<object>>
{
    private readonly IApplicationDbContext _db;
    private readonly ICacheService _cache;
    private readonly ICurrentUserService _currentUser;
    private readonly ILogger<Enable2FASmsHandler> _logger;

    public Enable2FASmsHandler(IApplicationDbContext db, ICacheService cache, ICurrentUserService currentUser, ILogger<Enable2FASmsHandler> logger)
    { _db = db; _cache = cache; _currentUser = currentUser; _logger = logger; }

    public async Task<Result<object>> Handle(Enable2FASmsCommand request, CancellationToken ct)
    {
        if (_currentUser.UserId is null) return Result<object>.Unauthorized();
        var user = await _db.Users.FirstOrDefaultAsync(u => u.Id == _currentUser.UserId, ct);
        if (user is null) return Result<object>.NotFound("Kullanıcı bulunamadı.");

        if (!VerifyPassword(request.Dto.CurrentPassword, user.PasswordHash))
            return Result<object>.Failure("Şifre hatalıdır.", 401);

        if (!user.EmailConfirmed)
            return Result<object>.Failure("Önce e-posta doğrulaması yapınız.", 400);

        // Telefon numarasını kaydet
        user.UpdatePhoneNumber(request.Dto.PhoneNumber);

        // SMS doğrulama kodu gönder (Redis'e kaydet)
        var code = SecurityHelper.GenerateVerificationCode(6);
        await _cache.SetAsync($"ct:sms-verify:{user.Id}", code, TimeSpan.FromMinutes(5), ct);

        // TODO: Gerçek SMS gönderimi (ISmsProvider)
        // await _smsProvider.SendAsync(request.Dto.PhoneNumber, $"CleanTenant doğrulama kodu: {code}");

        _logger.LogInformation("[2FA] SMS doğrulama kodu gönderildi: {Phone} (DEV: {Code})", request.Dto.PhoneNumber, code);

        return Result<object>.Success(new
        {
            message = "SMS doğrulama kodu gönderildi. Kodu /api/auth/2fa/verify-sms ile doğrulayınız.",
            devCode = code // TODO: Production'da kaldır
        });
    }

    private static bool VerifyPassword(string password, string hash)
    {
        if (hash == "CHANGE_ON_FIRST_LOGIN") return password == "Admin123!";
        return SecurityHelper.VerifyPassword(password, hash);
    }
}

// ============================================================================
// VERIFY SMS — Telefon doğrulama + SMS 2FA aktifleştirme
// ============================================================================

public record VerifySmsCommand(VerifySmsDto Dto) : IRequest<Result<object>>;

public class VerifySmsHandler : IRequestHandler<VerifySmsCommand, Result<object>>
{
    private readonly IApplicationDbContext _db;
    private readonly ICacheService _cache;
    private readonly ICurrentUserService _currentUser;
    private readonly ILogger<VerifySmsHandler> _logger;

    public VerifySmsHandler(IApplicationDbContext db, ICacheService cache, ICurrentUserService currentUser, ILogger<VerifySmsHandler> logger)
    { _db = db; _cache = cache; _currentUser = currentUser; _logger = logger; }

    public async Task<Result<object>> Handle(VerifySmsCommand request, CancellationToken ct)
    {
        if (_currentUser.UserId is null) return Result<object>.Unauthorized();
        var user = await _db.Users.FirstOrDefaultAsync(u => u.Id == _currentUser.UserId, ct);
        if (user is null) return Result<object>.NotFound("Kullanıcı bulunamadı.");

        var cachedCode = await _cache.GetAsync<string>($"ct:sms-verify:{user.Id}", ct);
        if (cachedCode is null || cachedCode != request.Dto.Code)
            return Result<object>.Failure("Doğrulama kodu hatalı veya süresi dolmuş.", 400);

        // Telefon doğrulandı
        user.ConfirmPhoneNumber();

        // SMS 2FA aktifleştir
        user.EnableTwoFactor(TwoFactorMethod.Sms);
        await _db.SaveChangesAsync(ct);

        await _cache.RemoveAsync($"ct:sms-verify:{user.Id}", ct);

        _logger.LogInformation("[2FA] SMS 2FA aktifleştirildi: {Email}, Phone: {Phone}", user.Email, user.PhoneNumber);
        return Result.Success();
    }
}

// ============================================================================
// SETUP AUTHENTICATOR — QR Kod + Secret
// ============================================================================

public record SetupAuthenticatorCommand : IRequest<Result<SetupAuthenticatorResponseDto>>;

public class SetupAuthenticatorHandler : IRequestHandler<SetupAuthenticatorCommand, Result<SetupAuthenticatorResponseDto>>
{
    private readonly IApplicationDbContext _db;
    private readonly ICurrentUserService _currentUser;
    private readonly ITotpService _totp;

    public SetupAuthenticatorHandler(IApplicationDbContext db, ICurrentUserService currentUser, ITotpService totp)
    { _db = db; _currentUser = currentUser; _totp = totp; }

    public async Task<Result<SetupAuthenticatorResponseDto>> Handle(SetupAuthenticatorCommand request, CancellationToken ct)
    {
        if (_currentUser.UserId is null) return Result<SetupAuthenticatorResponseDto>.Unauthorized();
        var user = await _db.Users.FirstOrDefaultAsync(u => u.Id == _currentUser.UserId, ct);
        if (user is null) return Result<SetupAuthenticatorResponseDto>.NotFound("Kullanıcı bulunamadı.");

        if (!user.EmailConfirmed)
            return Result<SetupAuthenticatorResponseDto>.Failure("Önce e-posta doğrulaması yapınız.", 400);

        var secretKey = _totp.GenerateSecret();
        var qrCodeBytes = _totp.GenerateQrCode(secretKey, user.Email);
        var recoveryCodes = _totp.GenerateRecoveryCodes();

        var qrCodeUri = $"otpauth://totp/{Uri.EscapeDataString("CleanTenant")}" +
                        $":{Uri.EscapeDataString(user.Email)}" +
                        $"?secret={secretKey}&issuer={Uri.EscapeDataString("CleanTenant")}" +
                        "&digits=6&period=30&algorithm=SHA1";

        return Result<SetupAuthenticatorResponseDto>.Success(new SetupAuthenticatorResponseDto
        {
            SecretKey = secretKey,
            QrCodeUri = qrCodeUri,
            QrCodeImage = Convert.ToBase64String(qrCodeBytes),
            RecoveryCodes = recoveryCodes
        });
    }
}

// ============================================================================
// VERIFY AUTHENTICATOR — Kodu doğrula ve aktifleştir
// ============================================================================

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
    private readonly ITotpService _totp;
    private readonly ILogger<VerifyAuthenticatorHandler> _logger;

    public VerifyAuthenticatorHandler(IApplicationDbContext db, ICurrentUserService currentUser, ITotpService totp, ILogger<VerifyAuthenticatorHandler> logger)
    { _db = db; _currentUser = currentUser; _totp = totp; _logger = logger; }

    public async Task<Result<object>> Handle(VerifyAuthenticatorCommand request, CancellationToken ct)
    {
        if (_currentUser.UserId is null) return Result<object>.Unauthorized();
        var user = await _db.Users.FirstOrDefaultAsync(u => u.Id == _currentUser.UserId, ct);
        if (user is null) return Result<object>.NotFound("Kullanıcı bulunamadı.");

        if (!_totp.Verify(request.Dto.SecretKey, request.Dto.Code))
            return Result<object>.Failure("Doğrulama kodu hatalı.", 400);

        user.EnableTwoFactor(TwoFactorMethod.Authenticator, request.Dto.SecretKey);
        await _db.SaveChangesAsync(ct);

        _logger.LogInformation("[2FA] Authenticator aktifleştirildi: {Email}", user.Email);
        return Result.Success();
    }
}

// ============================================================================
// DISABLE SPECIFIC 2FA METHOD
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
    { _db = db; _currentUser = currentUser; _logger = logger; }

    public async Task<Result<object>> Handle(Disable2FACommand request, CancellationToken ct)
    {
        if (_currentUser.UserId is null) return Result<object>.Unauthorized();
        var user = await _db.Users.FirstOrDefaultAsync(u => u.Id == _currentUser.UserId, ct);
        if (user is null) return Result<object>.NotFound("Kullanıcı bulunamadı.");

        if (!user.TwoFactorEnabled)
            return Result<object>.Failure("2FA zaten devre dışı.", 400);

        if (!VerifyPassword(request.Dto.CurrentPassword, user.PasswordHash))
            return Result<object>.Failure("Şifre hatalıdır.", 401);

        // Tüm 2FA yöntemlerini kapat
        user.DisableTwoFactor();
        await _db.SaveChangesAsync(ct);

        _logger.LogInformation("[2FA] Tüm 2FA devre dışı bırakıldı: {Email}", user.Email);
        return Result.Success();
    }

    private static bool VerifyPassword(string password, string hash)
    {
        if (hash == "CHANGE_ON_FIRST_LOGIN") return password == "Admin123!";
        return SecurityHelper.VerifyPassword(password, hash);
    }
}

// ============================================================================
// DISABLE SPECIFIC METHOD (tek yöntem kapat, diğerleri kalsın)
// ============================================================================

public record DisableSpecific2FACommand : IRequest<Result<object>>
{
    public string Method { get; init; } = default!;
    public string CurrentPassword { get; init; } = default!;
}

public class DisableSpecific2FAHandler : IRequestHandler<DisableSpecific2FACommand, Result<object>>
{
    private readonly IApplicationDbContext _db;
    private readonly ICurrentUserService _currentUser;
    private readonly ILogger<DisableSpecific2FAHandler> _logger;

    public DisableSpecific2FAHandler(IApplicationDbContext db, ICurrentUserService currentUser, ILogger<DisableSpecific2FAHandler> logger)
    { _db = db; _currentUser = currentUser; _logger = logger; }

    public async Task<Result<object>> Handle(DisableSpecific2FACommand request, CancellationToken ct)
    {
        if (_currentUser.UserId is null) return Result<object>.Unauthorized();
        var user = await _db.Users.FirstOrDefaultAsync(u => u.Id == _currentUser.UserId, ct);
        if (user is null) return Result<object>.NotFound("Kullanıcı bulunamadı.");

        if (!VerifyPassword(request.CurrentPassword, user.PasswordHash))
            return Result<object>.Failure("Şifre hatalıdır.", 401);

        if (!Enum.TryParse<TwoFactorMethod>(request.Method, true, out var method))
            return Result<object>.Failure("Geçersiz yöntem. Email, Sms veya Authenticator olabilir.", 400);

        if (!user.IsTwoFactorMethodEnabled(method))
            return Result<object>.Failure($"{method} yöntemi zaten devre dışı.", 400);

        user.DisableTwoFactorMethod(method);
        await _db.SaveChangesAsync(ct);

        _logger.LogInformation("[2FA] {Method} devre dışı bırakıldı: {Email}", method, user.Email);
        return Result.Success();
    }

    private static bool VerifyPassword(string password, string hash)
    {
        if (hash == "CHANGE_ON_FIRST_LOGIN") return password == "Admin123!";
        return SecurityHelper.VerifyPassword(password, hash);
    }
}

// ============================================================================
// SET PRIMARY 2FA METHOD
// ============================================================================

public record SetPrimary2FACommand(SetPrimary2FADto Dto) : IRequest<Result<object>>;

public class SetPrimary2FAHandler : IRequestHandler<SetPrimary2FACommand, Result<object>>
{
    private readonly IApplicationDbContext _db;
    private readonly ICurrentUserService _currentUser;
    private readonly ILogger<SetPrimary2FAHandler> _logger;

    public SetPrimary2FAHandler(IApplicationDbContext db, ICurrentUserService currentUser, ILogger<SetPrimary2FAHandler> logger)
    { _db = db; _currentUser = currentUser; _logger = logger; }

    public async Task<Result<object>> Handle(SetPrimary2FACommand request, CancellationToken ct)
    {
        if (_currentUser.UserId is null) return Result<object>.Unauthorized();
        var user = await _db.Users.FirstOrDefaultAsync(u => u.Id == _currentUser.UserId, ct);
        if (user is null) return Result<object>.NotFound("Kullanıcı bulunamadı.");

        if (!Enum.TryParse<TwoFactorMethod>(request.Dto.Method, true, out var method))
            return Result<object>.Failure("Geçersiz yöntem.", 400);

        user.SetPrimaryTwoFactorMethod(method);
        await _db.SaveChangesAsync(ct);

        _logger.LogInformation("[2FA] Primary yöntem değiştirildi: {Method} — {Email}", method, user.Email);
        return Result.Success();
    }
}
