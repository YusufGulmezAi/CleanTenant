using CleanTenant.Application.Common.Behaviors;
using CleanTenant.Application.Common.Interfaces;
using CleanTenant.Application.Common.Mappings;
using CleanTenant.Application.Common.Models;
using CleanTenant.Application.Common.Rules;
using CleanTenant.Domain.Identity;
using CleanTenant.Shared.Constants;
using CleanTenant.Shared.DTOs.Users;
using CleanTenant.Shared.Helpers;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace CleanTenant.Application.Features.Users.Commands;

// ============================================================================
// CREATE USER (E-posta ile arama → varsa rol ekle, yoksa yeni oluştur)
// ============================================================================

/// <summary>
/// Kullanıcı oluşturma/ekleme komutu.
/// 
/// <para><b>ÇAPRAZ KİMLİK AKIŞI:</b></para>
/// <code>
/// [1] E-posta adresini gir
/// [2] Sistemde aynı e-posta var mı?
///     → VARSA:  Mevcut kullanıcıya yeni rol/üyelik ata
///     → YOKSA:  Yeni kullanıcı oluştur + rol ata + davet e-postası gönder
/// </code>
/// </summary>
[RequirePermission(Permissions.Users.Create)]
public record CreateUserCommand(CreateUserDto Dto) : IRequest<Result<UserDto>>, ICacheInvalidator
{
    public Guid? TenantId { get; init; }
    public Guid? CompanyId { get; init; }
    public string? RoleId { get; init; }
    public string[] CacheKeysToInvalidate => ["users:all"];
}

public class CreateUserValidator : AbstractValidator<CreateUserCommand>
{
    public CreateUserValidator()
    {
        RuleFor(x => x.Dto.Email).NotEmpty().EmailAddress();
        RuleFor(x => x.Dto.FullName).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Dto.Password)
            .MinimumLength(8).When(x => !string.IsNullOrEmpty(x.Dto.Password));
    }
}

public class CreateUserHandler : IRequestHandler<CreateUserCommand, Result<UserDto>>
{
    private readonly IApplicationDbContext _db;
    private readonly UserRules _userRules;
    private readonly ICurrentUserService _currentUser;
    private readonly ILogger<CreateUserHandler> _logger;

    public CreateUserHandler(
        IApplicationDbContext db, UserRules userRules,
        ICurrentUserService currentUser, ILogger<CreateUserHandler> logger)
    {
        _db = db;
        _userRules = userRules;
        _currentUser = currentUser;
        _logger = logger;
    }

    public async Task<Result<UserDto>> Handle(CreateUserCommand request, CancellationToken ct)
    {
        var dto = request.Dto;

        // E-posta ile mevcut kullanıcı ara
        var existingUser = await _userRules.FindByEmailAsync(dto.Email, ct);

        if (existingUser is not null)
        {
            // Mevcut kullanıcıya rol/üyelik ekle
            _logger.LogInformation(
                "[USER] Mevcut kullanıcıya rol ekleniyor: {Email}", dto.Email);

            // TODO: TenantId/CompanyId ve RoleId'ye göre uygun pivot tabloya ekleme
            return Result<UserDto>.Success(existingUser.ToDto());
        }

        // Yeni kullanıcı oluştur
        var uniqueResult = await _userRules.EnsureEmailUniqueAsync(dto.Email, ct: ct);
        if (uniqueResult.IsFailure)
            return Result<UserDto>.Failure(uniqueResult.Error!, uniqueResult.StatusCode);

        var user = ApplicationUser.Create(dto.Email, dto.FullName, dto.PhoneNumber);

        // Şifre hash'le
        if (!string.IsNullOrEmpty(dto.Password))
            user.PasswordHash = SecurityHelper.HashPassword(dto.Password);
        else
            user.PasswordHash = "CHANGE_ON_FIRST_LOGIN";

        _db.Users.Add(user);
        await _db.SaveChangesAsync(ct);

        _logger.LogInformation("[USER] Yeni kullanıcı oluşturuldu: {Email}", dto.Email);

        return Result<UserDto>.Created(user.ToDto());
    }
}

// ============================================================================
// UPDATE USER PROFILE
// ============================================================================

[RequirePermission(Permissions.Users.Update)]
public record UpdateUserCommand(Guid Id, UpdateUserProfileDto Dto) : IRequest<Result<UserDto>>, ICacheInvalidator
{
    public string[] CacheKeysToInvalidate => ["users:all", $"user:{Id}"];
}

public class UpdateUserHandler : IRequestHandler<UpdateUserCommand, Result<UserDto>>
{
    private readonly IApplicationDbContext _db;
    private readonly UserRules _userRules;

    public UpdateUserHandler(IApplicationDbContext db, UserRules userRules)
    {
        _db = db;
        _userRules = userRules;
    }

    public async Task<Result<UserDto>> Handle(UpdateUserCommand request, CancellationToken ct)
    {
        var userResult = await _userRules.GetActiveOrFailAsync(request.Id, ct);
        if (userResult.IsFailure)
            return Result<UserDto>.Failure(userResult.Error!, userResult.StatusCode);

        var user = userResult.Value!;
        user.UpdateProfile(request.Dto.FullName, request.Dto.PhoneNumber, request.Dto.AvatarUrl);
        user.UpdatePreferences(request.Dto.PreferredLanguage, request.Dto.TimeZone);

        await _db.SaveChangesAsync(ct);
        return Result<UserDto>.Success(user.ToDto());
    }
}

// ============================================================================
// BLOCK USER
// ============================================================================

[RequirePermission(Permissions.Users.Block)]
public record BlockUserCommand : IRequest<Result<object>>
{
    public Guid UserId { get; init; }
    public string? Reason { get; init; }
    public DateTime? ExpiresAt { get; init; }
}

public class BlockUserHandler : IRequestHandler<BlockUserCommand, Result<object>>
{
    private readonly ISessionManager _sessionManager;
    private readonly ICurrentUserService _currentUser;
    private readonly AuthorizationRules _authRules;
    private readonly ILogger<BlockUserHandler> _logger;

    public BlockUserHandler(
        ISessionManager sessionManager, ICurrentUserService currentUser,
        AuthorizationRules authRules, ILogger<BlockUserHandler> logger)
    {
        _sessionManager = sessionManager;
        _currentUser = currentUser;
        _authRules = authRules;
        _logger = logger;
    }

    public async Task<Result<object>> Handle(BlockUserCommand request, CancellationToken ct)
    {
        if (_currentUser.UserId is null)
            return Result<object>.Unauthorized();

        // Hiyerarşi kontrolü: Alt seviye üst seviyeyi bloke edemez
        var canManage = await _authRules.EnsureCanManageUserAsync(request.UserId, ct);
        if (canManage.IsFailure)
            return Result<object>.Forbidden(canManage.Error!);

        await _sessionManager.BlockUserAsync(
            request.UserId,
            _currentUser.UserId.Value.ToString(),
            request.Reason,
            request.ExpiresAt,
            ct);

        _logger.LogWarning("[USER] Kullanıcı bloke edildi: {UserId}, Neden: {Reason}",
            request.UserId, request.Reason);

        return Result.Success();
    }
}

// ============================================================================
// FORCE LOGOUT
// ============================================================================

[RequirePermission(Permissions.Users.ForceLogout)]
public record ForceLogoutCommand(Guid UserId) : IRequest<Result<object>>;

public class ForceLogoutHandler : IRequestHandler<ForceLogoutCommand, Result<object>>
{
    private readonly ISessionManager _sessionManager;
    private readonly ICurrentUserService _currentUser;
    private readonly AuthorizationRules _authRules;
    private readonly ILogger<ForceLogoutHandler> _logger;

    public ForceLogoutHandler(
        ISessionManager sessionManager, ICurrentUserService currentUser,
        AuthorizationRules authRules, ILogger<ForceLogoutHandler> logger)
    {
        _sessionManager = sessionManager;
        _currentUser = currentUser;
        _authRules = authRules;
        _logger = logger;
    }

    public async Task<Result<object>> Handle(ForceLogoutCommand request, CancellationToken ct)
    {
        if (_currentUser.UserId is null)
            return Result<object>.Unauthorized();

        var canManage = await _authRules.EnsureCanManageUserAsync(request.UserId, ct);
        if (canManage.IsFailure)
            return Result<object>.Forbidden(canManage.Error!);

        await _sessionManager.RevokeAllSessionsAsync(
            request.UserId, _currentUser.UserId.Value.ToString(), ct);

        _logger.LogWarning("[USER] Force logout: {UserId}", request.UserId);
        return Result.Success();
    }
}

// ============================================================================
// DELETE USER (Soft Delete)
// ============================================================================

[RequirePermission(Permissions.Users.Delete)]
public record DeleteUserCommand(Guid Id) : IRequest<Result<object>>, ICacheInvalidator
{
    public string[] CacheKeysToInvalidate => ["users:all", $"user:{Id}"];
}

public class DeleteUserHandler : IRequestHandler<DeleteUserCommand, Result<object>>
{
    private readonly IApplicationDbContext _db;
    private readonly UserRules _userRules;
    private readonly AuthorizationRules _authRules;
    private readonly ISessionManager _sessionManager;

    public DeleteUserHandler(
        IApplicationDbContext db, UserRules userRules,
        AuthorizationRules authRules, ISessionManager sessionManager)
    {
        _db = db;
        _userRules = userRules;
        _authRules = authRules;
        _sessionManager = sessionManager;
    }

    public async Task<Result<object>> Handle(DeleteUserCommand request, CancellationToken ct)
    {
        var userResult = await _userRules.GetOrFailAsync(request.Id, ct);
        if (userResult.IsFailure)
            return Result<object>.Failure(userResult.Error!, userResult.StatusCode);

        var canManage = await _authRules.EnsureCanManageUserAsync(request.Id, ct);
        if (canManage.IsFailure)
            return Result<object>.Forbidden(canManage.Error!);

        // Tüm oturumları sonlandır
        await _sessionManager.RevokeAllSessionsAsync(request.Id, "SYSTEM:UserDeleted", ct);

        _db.Users.Remove(userResult.Value!);
        await _db.SaveChangesAsync(ct);

        return Result.NoContent();
    }
}
