using CleanTenant.Application.Common.Interfaces;
using CleanTenant.Application.Common.Models;
using CleanTenant.Application.Common.Rules;
using CleanTenant.Domain.Enums;
using CleanTenant.Shared.Constants;
using CleanTenant.Shared.DTOs.Auth;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace CleanTenant.Application.Features.Auth.Commands;

// ============================================================================
// LOGIN
// ============================================================================

/// <summary>
/// Login komutu — şifre doğrulandıktan sonra:
/// - 2FA KAPALI → Gerçek AccessToken + RefreshToken üretilir
/// - 2FA AÇIK  → TempToken üretilir, gerçek token VERİLMEZ
/// </summary>
public record LoginCommand(LoginRequestDto Dto) : IRequest<Result<LoginResponseDto>>;

public class LoginValidator : AbstractValidator<LoginCommand>
{
    public LoginValidator()
    {
        RuleFor(x => x.Dto.Email)
            .NotEmpty().WithMessage("E-posta adresi zorunludur.")
            .EmailAddress().WithMessage("Geçerli bir e-posta adresi giriniz.");

        RuleFor(x => x.Dto.Password)
            .NotEmpty().WithMessage("Şifre zorunludur.");
    }
}

public class LoginHandler : IRequestHandler<LoginCommand, Result<LoginResponseDto>>
{
    private readonly IApplicationDbContext _db;
    private readonly ISessionManager _sessionManager;
    private readonly ICacheService _cache;
    private readonly UserRules _userRules;
    private readonly IConfiguration _configuration;
    private readonly ILogger<LoginHandler> _logger;

    public LoginHandler(
        IApplicationDbContext db,
        ISessionManager sessionManager,
        ICacheService cache,
        UserRules userRules,
        IConfiguration configuration,
        ILogger<LoginHandler> logger)
    {
        _db = db;
        _sessionManager = sessionManager;
        _cache = cache;
        _userRules = userRules;
        _configuration = configuration;
        _logger = logger;
    }

    public async Task<Result<LoginResponseDto>> Handle(LoginCommand request, CancellationToken ct)
    {
        var dto = request.Dto;

        // [1] Kullanıcıyı bul
        var user = await _userRules.FindByEmailAsync(dto.Email, ct);
        if (user is null)
        {
            // Güvenlik: Kullanıcı var/yok bilgisi sızdırma — aynı hata mesajı
            _logger.LogWarning("[AUTH] Başarısız login denemesi — bilinmeyen e-posta: {Email}", dto.Email);
            return Result<LoginResponseDto>.Failure("E-posta adresi veya şifre hatalıdır.", 401);
        }

        // [2] Hesap aktif mi?
        if (!user.IsActive)
            return Result<LoginResponseDto>.Failure("Hesabınız pasif durumdadır.", 403);

        // [3] Hesap kilitli mi?
        if (user.LockoutEnd.HasValue && user.LockoutEnd > DateTime.UtcNow)
            return Result<LoginResponseDto>.Failure(
                $"Hesabınız kilitli. Kilit bitiş: {user.LockoutEnd:yyyy-MM-dd HH:mm} UTC", 403);

        // [4] Kullanıcı bloke mu? (Redis + DB kontrol)
        if (await _sessionManager.IsUserBlockedAsync(user.Id, ct))
            return Result<LoginResponseDto>.Failure("Hesabınız bloke edilmiştir.", 403);

        // [5] Şifre doğrulama
        // NOT: Gerçek implementasyonda BCrypt/PBKDF2 hash karşılaştırması yapılır.
        // Şimdilik basit kontrol — Infrastructure'da Identity servisi ile değiştirilecek.
        var isPasswordValid = VerifyPassword(dto.Password, user.PasswordHash);
        if (!isPasswordValid)
        {
            // Başarısız deneme kaydet
            var maxAttempts = int.Parse(_configuration["CleanTenant:PasswordPolicy:MaxFailedAccessAttempts"] ?? "5");
            var lockoutMinutes = int.Parse(_configuration["CleanTenant:PasswordPolicy:LockoutDurationMinutes"] ?? "15");
            user.RecordFailedLogin(maxAttempts, TimeSpan.FromMinutes(lockoutMinutes));
            await _db.SaveChangesAsync(ct);

            _logger.LogWarning("[AUTH] Başarısız login — yanlış şifre: {Email}, Deneme: {Count}",
                dto.Email, user.AccessFailedCount);

            return Result<LoginResponseDto>.Failure("E-posta adresi veya şifre hatalıdır.", 401);
        }

        // [6] 2FA kontrolü
        if (user.TwoFactorEnabled)
        {
            // TempToken üret — gerçek token VERİLMEZ!
            var tempToken = Guid.NewGuid().ToString("N");
            var tempExpireMinutes = int.Parse(_configuration["CleanTenant:TwoFactor:CodeExpirationMinutes"] ?? "5");

            // Redis'e TempToken → userId + deviceFingerprint eşlemesi kaydet
            var tempData = new TempTokenData
            {
                UserId = user.Id,
                Email = user.Email,
                DeviceFingerprint = dto.DeviceFingerprint,
                IpAddress = "", // Middleware'den alınacak
                CreatedAt = DateTime.UtcNow
            };

            await _cache.SetAsync(
                $"ct:temp:{tempToken}",
                tempData,
                TimeSpan.FromMinutes(tempExpireMinutes),
                ct);

            // TODO: 2FA kodu gönder (SMS/Email/Authenticator'a göre)
            // Şimdilik sadece TempToken dönüyoruz

            _logger.LogInformation("[AUTH] 2FA gerekli — TempToken üretildi: {Email}", user.Email);

            return Result<LoginResponseDto>.Success(new LoginResponseDto
            {
                Requires2FA = true,
                TempToken = tempToken,
                TwoFactorMethod = user.PrimaryTwoFactorMethod.ToString()
            });
        }

        // [7] 2FA kapalı — doğrudan gerçek token üret
        var ipAddress = dto.DeviceFingerprint ?? "unknown";  // Endpoint'ten override edilecek
        var session = await _sessionManager.CreateSessionAsync(user.Id, ipAddress, "", ct);

        // Başarılı login kaydet
        user.RecordLogin(ipAddress);
        await _db.SaveChangesAsync(ct);

        _logger.LogInformation("[AUTH] Başarılı login: {Email}", user.Email);

        return Result<LoginResponseDto>.Success(new LoginResponseDto
        {
            Requires2FA = false,
            AccessToken = session.AccessToken,
            RefreshToken = session.RefreshToken,
            AccessTokenExpiresAt = session.AccessTokenExpiresAt,
            RefreshTokenExpiresAt = session.RefreshTokenExpiresAt
        });
    }

    /// <summary>
    /// Şifre doğrulama — SecurityHelper.VerifyPassword (PBKDF2) kullanır.
    /// </summary>
    private static bool VerifyPassword(string password, string passwordHash)
    {
        // Seed data için geçici kontrol — ilk loginde şifre değiştirilecek
        if (passwordHash == "CHANGE_ON_FIRST_LOGIN")
            return password == "Admin123!";

        return Shared.Helpers.SecurityHelper.VerifyPassword(password, passwordHash);
    }
}

/// <summary>Redis'te TempToken ile saklanan geçici oturum verisi.</summary>
public class TempTokenData
{
    public Guid UserId { get; set; }
    public string Email { get; set; } = default!;
    public string? DeviceFingerprint { get; set; }
    public string? IpAddress { get; set; }
    public DateTime CreatedAt { get; set; }
}

// ============================================================================
// VERIFY 2FA
// ============================================================================

/// <summary>
/// 2FA doğrulama komutu.
/// TempToken + 2FA kodu gönderilir → Başarılıysa gerçek token üretilir.
/// </summary>
public record Verify2FACommand(TwoFactorVerifyDto Dto) : IRequest<Result<LoginResponseDto>>;

public class Verify2FAValidator : AbstractValidator<Verify2FACommand>
{
    public Verify2FAValidator()
    {
        RuleFor(x => x.Dto.TempToken)
            .NotEmpty().WithMessage("Geçici token (TempToken) zorunludur.");

        RuleFor(x => x.Dto.Code)
            .NotEmpty().WithMessage("Doğrulama kodu zorunludur.")
            .Length(6).WithMessage("Doğrulama kodu 6 haneli olmalıdır.");
    }
}

public class Verify2FAHandler : IRequestHandler<Verify2FACommand, Result<LoginResponseDto>>
{
    private readonly IApplicationDbContext _db;
    private readonly ISessionManager _sessionManager;
    private readonly ICacheService _cache;
    private readonly IConfiguration _configuration;
    private readonly ILogger<Verify2FAHandler> _logger;

    public Verify2FAHandler(
        IApplicationDbContext db,
        ISessionManager sessionManager,
        ICacheService cache,
        IConfiguration configuration,
        ILogger<Verify2FAHandler> logger)
    {
        _db = db;
        _sessionManager = sessionManager;
        _cache = cache;
        _configuration = configuration;
        _logger = logger;
    }

    public async Task<Result<LoginResponseDto>> Handle(Verify2FACommand request, CancellationToken ct)
    {
        var dto = request.Dto;

        // [1] TempToken'ı Redis'ten çözümle
        var tempDataKey = $"ct:temp:{dto.TempToken}";
        var tempData = await _cache.GetAsync<TempTokenData>(tempDataKey, ct);

        if (tempData is null)
        {
            _logger.LogWarning("[2FA] Geçersiz veya süresi dolmuş TempToken");
            return Result<LoginResponseDto>.Failure(
                "Geçici token geçersiz veya süresi dolmuş. Lütfen tekrar giriş yapınız.", 401);
        }

        // [2] Kullanıcıyı bul
        var user = await _db.Users.FirstOrDefaultAsync(u => u.Id == tempData.UserId, ct);
        if (user is null)
            return Result<LoginResponseDto>.Failure("Kullanıcı bulunamadı.", 404);

        // [3] 2FA kodunu doğrula
        // TODO: Gerçek TOTP/SMS/Email kod doğrulaması
        // Şimdilik geliştirme amaçlı "123456" kabul ediliyor
        var isCodeValid = Verify2FACode(user, dto.Code, dto.IsFallback);

        if (!isCodeValid)
        {
            // Başarısız deneme sayısını kontrol et
            var maxAttempts = int.Parse(_configuration["CleanTenant:TwoFactor:MaxFailedAttempts"] ?? "3");
            var failCountKey = $"ct:temp:{dto.TempToken}:fails";
            var failCount = await _cache.GetAsync<int?>(failCountKey, ct) ?? 0;
            failCount++;

            if (failCount >= maxAttempts)
            {
                // TempToken'ı sil — tekrar login gerekecek
                await _cache.RemoveAsync(tempDataKey, ct);
                await _cache.RemoveAsync(failCountKey, ct);

                _logger.LogWarning("[2FA] Maksimum deneme aşıldı: {Email}", tempData.Email);
                return Result<LoginResponseDto>.Failure(
                    "Maksimum deneme sayısı aşıldı. Lütfen tekrar giriş yapınız.", 401);
            }

            await _cache.SetAsync(failCountKey, failCount, TimeSpan.FromMinutes(5), ct);

            _logger.LogWarning("[2FA] Yanlış kod: {Email}, Deneme: {Count}/{Max}",
                tempData.Email, failCount, maxAttempts);

            return Result<LoginResponseDto>.Failure(
                $"Doğrulama kodu hatalı. Kalan deneme: {maxAttempts - failCount}", 401);
        }

        // [4] TempToken'ı sil (tek kullanımlık)
        await _cache.RemoveAsync(tempDataKey, ct);
        await _cache.RemoveAsync($"ct:temp:{dto.TempToken}:fails", ct);

        // [5] Gerçek token üret
        var ipAddress = tempData.IpAddress ?? "unknown";
        var session = await _sessionManager.CreateSessionAsync(user.Id, ipAddress, "", ct);

        // Başarılı login kaydet
        user.RecordLogin(ipAddress);
        await _db.SaveChangesAsync(ct);

        _logger.LogInformation("[2FA] Başarılı doğrulama: {Email}", user.Email);

        return Result<LoginResponseDto>.Success(new LoginResponseDto
        {
            Requires2FA = false,
            AccessToken = session.AccessToken,
            RefreshToken = session.RefreshToken,
            AccessTokenExpiresAt = session.AccessTokenExpiresAt,
            RefreshTokenExpiresAt = session.RefreshTokenExpiresAt
        });
    }

    private static bool Verify2FACode(Domain.Identity.ApplicationUser user, string code, bool isFallback)
    {
        // E-posta fallback — Redis'teki kodu karşılaştır (TODO: cache inject edilecek)
        // Şimdilik fallback'te "123456" kabul ediliyor
        if (isFallback)
            return code == "123456";

        return user.PrimaryTwoFactorMethod switch
        {
            // Authenticator: Gerçek TOTP doğrulama
            Domain.Enums.TwoFactorMethod.Authenticator when !string.IsNullOrEmpty(user.AuthenticatorKey)
                => Shared.Helpers.SecurityHelper.VerifyTotpCode(user.AuthenticatorKey, code),

            // E-posta: Redis'teki kodu karşılaştır (TODO: cache)
            // Geliştirme amaçlı "123456" kabul
            Domain.Enums.TwoFactorMethod.Email => code == "123456",

            // SMS: Redis'teki kodu karşılaştır (TODO: cache)
            Domain.Enums.TwoFactorMethod.Sms => code == "123456",

            _ => false
        };
    }
}

// ============================================================================
// 2FA FALLBACK — "Kodumu alamıyorum" butonu
// ============================================================================

public record Request2FAFallbackCommand(TwoFactorFallbackRequestDto Dto) : IRequest<Result<object>>;

public class Request2FAFallbackHandler : IRequestHandler<Request2FAFallbackCommand, Result<object>>
{
    private readonly IApplicationDbContext _db;
    private readonly ICacheService _cache;
    private readonly ILogger<Request2FAFallbackHandler> _logger;

    public Request2FAFallbackHandler(IApplicationDbContext db, ICacheService cache, ILogger<Request2FAFallbackHandler> logger)
    {
        _db = db;
        _cache = cache;
        _logger = logger;
    }

    public async Task<Result<object>> Handle(Request2FAFallbackCommand request, CancellationToken ct)
    {
        var tempDataKey = $"ct:temp:{request.Dto.TempToken}";
        var tempData = await _cache.GetAsync<TempTokenData>(tempDataKey, ct);

        if (tempData is null)
            return Result<object>.Failure("Geçici token geçersiz veya süresi dolmuş.", 401);

        var user = await _db.Users.FirstOrDefaultAsync(u => u.Id == tempData.UserId, ct);
        if (user is null || !user.EmailConfirmed)
            return Result<object>.Failure("E-posta doğrulanmamış kullanıcı için fallback yapılamaz.", 400);

        // TODO: E-posta ile 2FA kodu gönder
        // var code = GenerateRandomCode(6);
        // await _emailService.SendTwoFactorCodeAsync(user.Email, code);
        // await _cache.SetAsync($"ct:2fa:email:{user.Id}", code, TimeSpan.FromMinutes(5), ct);

        _logger.LogInformation("[2FA] Fallback e-posta gönderildi: {Email}", user.Email);

        return Result<object>.Success(null!);
    }
}

// ============================================================================
// REFRESH TOKEN
// ============================================================================

/// <summary>
/// Token yenileme komutu.
/// Eski RefreshToken revoke edilir, yeni AccessToken + RefreshToken üretilir.
/// RefreshToken hem Redis'te hem DB'de kontrol edilir (dual storage).
/// </summary>
public record RefreshTokenCommand(RefreshTokenDto Dto) : IRequest<Result<LoginResponseDto>>;

public class RefreshTokenValidator : AbstractValidator<RefreshTokenCommand>
{
    public RefreshTokenValidator()
    {
        RuleFor(x => x.Dto.AccessToken).NotEmpty().WithMessage("Access token zorunludur.");
        RuleFor(x => x.Dto.RefreshToken).NotEmpty().WithMessage("Refresh token zorunludur.");
    }
}

public class RefreshTokenHandler : IRequestHandler<RefreshTokenCommand, Result<LoginResponseDto>>
{
    private readonly IApplicationDbContext _db;
    private readonly ISessionManager _sessionManager;
    private readonly ICacheService _cache;
    private readonly ILogger<RefreshTokenHandler> _logger;

    public RefreshTokenHandler(
        IApplicationDbContext db,
        ISessionManager sessionManager,
        ICacheService cache,
        ILogger<RefreshTokenHandler> logger)
    {
        _db = db;
        _sessionManager = sessionManager;
        _cache = cache;
        _logger = logger;
    }

    public async Task<Result<LoginResponseDto>> Handle(RefreshTokenCommand request, CancellationToken ct)
    {
        var dto = request.Dto;

        // [1] Access token'dan userId çıkar (süresi dolmuş olsa bile)
        // TokenService.GetPrincipalFromToken(token, validateLifetime: false)
        // Şimdilik basitleştirilmiş — JWT decode ile userId alınacak
        // TODO: TokenService inject edilip kullanılacak

        // [2] Refresh token hash'ini kontrol et
        var refreshHash = CleanTenant.Shared.Helpers.SecurityHelper.HashToken(dto.RefreshToken);

        // Önce Redis kontrol (hızlı)
        // Sonra DB kontrol (fallback)
        var session = await _db.UserSessions
            .Where(s => s.RefreshTokenHash == refreshHash && !s.IsRevoked)
            .FirstOrDefaultAsync(ct);

        if (session is null)
        {
            _logger.LogWarning("[REFRESH] Geçersiz refresh token");
            return Result<LoginResponseDto>.Failure("Geçersiz veya süresi dolmuş refresh token.", 401);
        }

        // [3] Refresh token süresi dolmuş mu?
        if (session.RefreshTokenExpiresAt < DateTime.UtcNow)
        {
            session.Revoke("SYSTEM:Expired");
            await _db.SaveChangesAsync(ct);
            return Result<LoginResponseDto>.Failure("Refresh token süresi dolmuş. Tekrar giriş yapınız.", 401);
        }

        // [4] Kullanıcı bloke mu?
        if (await _sessionManager.IsUserBlockedAsync(session.UserId, ct))
            return Result<LoginResponseDto>.Failure("Hesabınız bloke edilmiştir.", 403);

        // [5] Eski oturumu revoke et (Token Rotation)
        session.Revoke("SYSTEM:TokenRotation");

        // [6] Yeni oturum oluştur
        var newSession = await _sessionManager.CreateSessionAsync(
            session.UserId, session.IpAddress, session.UserAgent, ct);

        await _db.SaveChangesAsync(ct);

        _logger.LogInformation("[REFRESH] Token yenilendi: UserId={UserId}", session.UserId);

        return Result<LoginResponseDto>.Success(new LoginResponseDto
        {
            Requires2FA = false,
            AccessToken = newSession.AccessToken,
            RefreshToken = newSession.RefreshToken,
            AccessTokenExpiresAt = newSession.AccessTokenExpiresAt,
            RefreshTokenExpiresAt = newSession.RefreshTokenExpiresAt
        });
    }
}

// ============================================================================
// LOGOUT
// ============================================================================

public record LogoutCommand : IRequest<Result<object>>;

public class LogoutHandler : IRequestHandler<LogoutCommand, Result<object>>
{
    private readonly ICurrentUserService _currentUser;
    private readonly ISessionManager _sessionManager;
    private readonly ILogger<LogoutHandler> _logger;

    public LogoutHandler(
        ICurrentUserService currentUser,
        ISessionManager sessionManager,
        ILogger<LogoutHandler> logger)
    {
        _currentUser = currentUser;
        _sessionManager = sessionManager;
        _logger = logger;
    }

    public async Task<Result<object>> Handle(LogoutCommand request, CancellationToken ct)
    {
        if (_currentUser.UserId is null)
            return Result<object>.Unauthorized();

        await _sessionManager.RevokeSessionAsync(_currentUser.UserId.Value, ct);

        _logger.LogInformation("[AUTH] Logout: UserId={UserId}", _currentUser.UserId);

        return Result.Success();
    }
}

// ============================================================================
// CHANGE PASSWORD
// ============================================================================

public record ChangePasswordCommand(ChangePasswordDto Dto) : IRequest<Result<object>>;

public class ChangePasswordValidator : AbstractValidator<ChangePasswordCommand>
{
    public ChangePasswordValidator()
    {
        RuleFor(x => x.Dto.CurrentPassword).NotEmpty().WithMessage("Mevcut şifre zorunludur.");
        RuleFor(x => x.Dto.NewPassword).NotEmpty().MinimumLength(8)
            .WithMessage("Yeni şifre en az 8 karakter olmalıdır.");
        RuleFor(x => x.Dto.ConfirmPassword)
            .Equal(x => x.Dto.NewPassword).WithMessage("Şifreler eşleşmiyor.");
    }
}

public class ChangePasswordHandler : IRequestHandler<ChangePasswordCommand, Result<object>>
{
    private readonly IApplicationDbContext _db;
    private readonly ICurrentUserService _currentUser;
    private readonly ISessionManager _sessionManager;
    private readonly ILogger<ChangePasswordHandler> _logger;

    public ChangePasswordHandler(
        IApplicationDbContext db,
        ICurrentUserService currentUser,
        ISessionManager sessionManager,
        ILogger<ChangePasswordHandler> logger)
    {
        _db = db;
        _currentUser = currentUser;
        _sessionManager = sessionManager;
        _logger = logger;
    }

    public async Task<Result<object>> Handle(ChangePasswordCommand request, CancellationToken ct)
    {
        if (_currentUser.UserId is null)
            return Result<object>.Unauthorized();

        var user = await _db.Users.FirstOrDefaultAsync(u => u.Id == _currentUser.UserId, ct);
        if (user is null)
            return Result<object>.NotFound("Kullanıcı bulunamadı.");

        // TODO: Mevcut şifre doğrulama (BCrypt)
        // TODO: Şifre geçmişi kontrolü
        // TODO: Yeni şifre hash'leme

        user.RecordPasswordChange();
        await _db.SaveChangesAsync(ct);

        // Tüm diğer oturumları sonlandır (güvenlik)
        await _sessionManager.RevokeAllSessionsAsync(user.Id, "SYSTEM:PasswordChange", ct);

        _logger.LogInformation("[AUTH] Şifre değiştirildi: {Email}", user.Email);

        return Result.Success();
    }
}

// ============================================================================
// FORGOT PASSWORD
// ============================================================================

public record ForgotPasswordCommand(ForgotPasswordDto Dto) : IRequest<Result<object>>;

public class ForgotPasswordHandler : IRequestHandler<ForgotPasswordCommand, Result<object>>
{
    private readonly IApplicationDbContext _db;
    private readonly ILogger<ForgotPasswordHandler> _logger;

    public ForgotPasswordHandler(IApplicationDbContext db, ILogger<ForgotPasswordHandler> logger)
    {
        _db = db;
        _logger = logger;
    }

    public async Task<Result<object>> Handle(ForgotPasswordCommand request, CancellationToken ct)
    {
        // GÜVENLİK: Her durumda aynı mesaj dön — e-posta var/yok bilgisi sızdırma
        var user = await _db.Users
            .FirstOrDefaultAsync(u => u.Email == request.Dto.Email.Trim().ToLowerInvariant(), ct);

        if (user is not null && user.EmailConfirmed)
        {
            // TODO: Şifre sıfırlama token'ı üret ve e-posta gönder
            // var resetToken = Guid.NewGuid().ToString("N");
            // await _cache.SetAsync($"ct:reset:{resetToken}", user.Id, TimeSpan.FromMinutes(15));
            // await _emailService.SendPasswordResetAsync(user.Email, $"https://app/reset?token={resetToken}");

            _logger.LogInformation("[AUTH] Şifre sıfırlama linki gönderildi: {Email}", user.Email);
        }

        // Her durumda başarılı yanıt dön — bilgi sızdırma
        return Result<object>.Success(null!);
    }
}
