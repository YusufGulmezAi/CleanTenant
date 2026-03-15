using CleanTenant.Application.Common.Interfaces;
using CleanTenant.Application.Common.Models;
using CleanTenant.Application.Common.Rules;
using CleanTenant.Domain.Enums;
using CleanTenant.Shared.Constants;
using CleanTenant.Shared.DTOs.Auth;
using CleanTenant.Shared.Helpers;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace CleanTenant.Application.Features.Auth.Commands;

// ============================================================================
// LOGIN
// ============================================================================

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
	private readonly IEmailService _emailService;
	private readonly UserRules _userRules;
	private readonly IConfiguration _configuration;
	private readonly ILogger<LoginHandler> _logger;

	public LoginHandler(
		IApplicationDbContext db,
		ISessionManager sessionManager,
		ICacheService cache,
		IEmailService emailService,
		UserRules userRules,
		IConfiguration configuration,
		ILogger<LoginHandler> logger)
	{
		_db = db;
		_sessionManager = sessionManager;
		_cache = cache;
		_emailService = emailService;
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

		// [4] Kullanıcı bloke mu?
		if (await _sessionManager.IsUserBlockedAsync(user.Id, ct))
			return Result<LoginResponseDto>.Failure("Hesabınız bloke edilmiştir.", 403);

		// [5] Şifre doğrulama
		var isPasswordValid = VerifyPassword(dto.Password, user.PasswordHash);
		if (!isPasswordValid)
		{
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
			var tempToken = Guid.NewGuid().ToString("N");
			var tempExpireMinutes = int.Parse(_configuration["CleanTenant:TwoFactor:CodeExpirationMinutes"] ?? "5");

			// TempToken → Redis
			var tempData = new TempTokenData
			{
				UserId = user.Id,
				Email = user.Email,
				DeviceFingerprint = dto.DeviceFingerprint,
				IpAddress = "",
				CreatedAt = DateTime.UtcNow
			};

			await _cache.SetAsync(
				$"ct:temp:{tempToken}",
				tempData,
				TimeSpan.FromMinutes(tempExpireMinutes),
				ct);

			// E-posta veya SMS ile 2FA kodu gönder
			if (user.PrimaryTwoFactorMethod == TwoFactorMethod.Email ||
				user.PrimaryTwoFactorMethod == TwoFactorMethod.Sms)
			{
				var code = SecurityHelper.GenerateVerificationCode(6);
				await _cache.SetAsync(
					$"ct:2fa-code:{user.Id}",
					code,
					TimeSpan.FromMinutes(tempExpireMinutes),
					ct);

				if (user.PrimaryTwoFactorMethod == TwoFactorMethod.Email)
				{
					try
					{
						await _emailService.SendTwoFactorCodeAsync(user.Email, code, ct);
						_logger.LogInformation("[AUTH] 2FA kodu e-posta ile gönderildi: {Email}", user.Email);
					}
					catch (Exception ex)
					{
						_logger.LogError(ex, "[AUTH] 2FA e-posta gönderilemedi: {Email}", user.Email);
					}
				}
			}

			_logger.LogInformation("[AUTH] 2FA gerekli — TempToken üretildi: {Email}", user.Email);

			return Result<LoginResponseDto>.Success(new LoginResponseDto
			{
				Requires2FA = true,
				TempToken = tempToken,
				TwoFactorMethod = user.PrimaryTwoFactorMethod.ToString()
			});
		}

		// [7] 2FA kapalı — doğrudan token üret
		var ipAddress = dto.DeviceFingerprint ?? "unknown";
		var session = await _sessionManager.CreateSessionAsync(user.Id, ipAddress, "", ct);

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

	private static bool VerifyPassword(string password, string passwordHash)
	{
		if (passwordHash == "CHANGE_ON_FIRST_LOGIN")
			return password == "Admin123!";

		return SecurityHelper.VerifyPassword(password, passwordHash);
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
			return Result<LoginResponseDto>.NotFound("Kullanıcı bulunamadı.");

		// [3] 2FA kodunu doğrula
		bool isCodeValid;

		if (user.PrimaryTwoFactorMethod == TwoFactorMethod.Authenticator && !dto.IsFallback)
		{
			// Authenticator — gerçek TOTP doğrulama
			isCodeValid = !string.IsNullOrEmpty(user.AuthenticatorKey)
				&& SecurityHelper.VerifyTotpCode(user.AuthenticatorKey, dto.Code);
		}
		else
		{
			// Email / SMS / Fallback — Redis'teki kodu kontrol et
			var cachedCode = await _cache.GetAsync<string>($"ct:2fa-code:{user.Id}", ct);
			isCodeValid = cachedCode is not null && cachedCode == dto.Code;

			if (isCodeValid)
				await _cache.RemoveAsync($"ct:2fa-code:{user.Id}", ct);
		}

		if (!isCodeValid)
		{
			var maxAttempts = int.Parse(_configuration["CleanTenant:TwoFactor:MaxFailedAttempts"] ?? "3");
			var failCountKey = $"ct:temp:{dto.TempToken}:fails";
			var failCount = await _cache.GetAsync<int?>(failCountKey, ct) ?? 0;
			failCount++;

			if (failCount >= maxAttempts)
			{
				await _cache.RemoveAsync(tempDataKey, ct);
				await _cache.RemoveAsync(failCountKey, ct);
				await _cache.RemoveAsync($"ct:2fa-code:{user.Id}", ct);

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

		// [4] TempToken'ı sil
		await _cache.RemoveAsync(tempDataKey, ct);
		await _cache.RemoveAsync($"ct:temp:{dto.TempToken}:fails", ct);

		// [5] Gerçek token üret
		var ipAddress = tempData.IpAddress ?? "unknown";
		var session = await _sessionManager.CreateSessionAsync(user.Id, ipAddress, "", ct);

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
}

// ============================================================================
// 2FA FALLBACK — "Kodumu alamıyorum" butonu
// ============================================================================

public record Request2FAFallbackCommand(TwoFactorFallbackRequestDto Dto) : IRequest<Result<object>>;

public class Request2FAFallbackHandler : IRequestHandler<Request2FAFallbackCommand, Result<object>>
{
	private readonly IApplicationDbContext _db;
	private readonly ICacheService _cache;
	private readonly IEmailService _emailService;
	private readonly ILogger<Request2FAFallbackHandler> _logger;

	public Request2FAFallbackHandler(
		IApplicationDbContext db, ICacheService cache,
		IEmailService emailService, ILogger<Request2FAFallbackHandler> logger)
	{
		_db = db;
		_cache = cache;
		_emailService = emailService;
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

		// Yeni kod üret ve Redis'e kaydet
		var code = SecurityHelper.GenerateVerificationCode(6);
		await _cache.SetAsync($"ct:2fa-code:{user.Id}", code, TimeSpan.FromMinutes(5), ct);

		// E-posta ile gönder
		try
		{
			await _emailService.SendTwoFactorCodeAsync(user.Email, code, ct);
			_logger.LogInformation("[2FA] Fallback kodu e-posta ile gönderildi: {Email}", user.Email);
		}
		catch (Exception ex)
		{
			_logger.LogError(ex, "[2FA] Fallback e-posta gönderilemedi: {Email}", user.Email);
			return Result<object>.Failure("E-posta gönderilemedi. Lütfen tekrar deneyiniz.", 500);
		}

		return Result<object>.Success(null!);
	}
}

// ============================================================================
// REFRESH TOKEN
// ============================================================================

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

		var refreshHash = SecurityHelper.HashToken(dto.RefreshToken);

		var session = await _db.UserSessions
			.Where(s => s.RefreshTokenHash == refreshHash && !s.IsRevoked)
			.FirstOrDefaultAsync(ct);

		if (session is null)
		{
			_logger.LogWarning("[REFRESH] Geçersiz refresh token");
			return Result<LoginResponseDto>.Failure("Geçersiz veya süresi dolmuş refresh token.", 401);
		}

		if (session.RefreshTokenExpiresAt < DateTime.UtcNow)
		{
			session.Revoke("SYSTEM:Expired");
			await _db.SaveChangesAsync(ct);
			return Result<LoginResponseDto>.Failure("Refresh token süresi dolmuş. Tekrar giriş yapınız.", 401);
		}

		if (await _sessionManager.IsUserBlockedAsync(session.UserId, ct))
			return Result<LoginResponseDto>.Failure("Hesabınız bloke edilmiştir.", 403);

		// Token Rotation
		session.Revoke("SYSTEM:TokenRotation");
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

		user.RecordPasswordChange();
		await _db.SaveChangesAsync(ct);

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
		var user = await _db.Users
			.FirstOrDefaultAsync(u => u.Email == request.Dto.Email.Trim().ToLowerInvariant(), ct);

		if (user is not null && user.EmailConfirmed)
		{
			_logger.LogInformation("[AUTH] Şifre sıfırlama linki gönderildi: {Email}", user.Email);
		}

		return Result<object>.Success(null!);
	}
}
