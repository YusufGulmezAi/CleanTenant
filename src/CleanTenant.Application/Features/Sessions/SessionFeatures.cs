using CleanTenant.Application.Common.Behaviors;
using CleanTenant.Application.Common.Interfaces;
using CleanTenant.Application.Common.Models;
using CleanTenant.Shared.Constants;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace CleanTenant.Application.Features.Sessions;

// ============================================================================
// GET ACTIVE SESSIONS — Admin oturum izleme paneli
// ============================================================================

[RequirePermission(Permissions.Sessions.Read)]
public record GetActiveSessionsQuery : IRequest<Result<List<ActiveSessionDto>>>
{
    public Guid? FilterByUserId { get; init; }
}

public class GetActiveSessionsHandler : IRequestHandler<GetActiveSessionsQuery, Result<List<ActiveSessionDto>>>
{
    private readonly IApplicationDbContext _db;

    public GetActiveSessionsHandler(IApplicationDbContext db) => _db = db;

    public async Task<Result<List<ActiveSessionDto>>> Handle(GetActiveSessionsQuery request, CancellationToken ct)
    {
        var query = _db.UserSessions
            .AsNoTracking()
            .Where(s => !s.IsRevoked && s.AccessTokenExpiresAt > DateTime.UtcNow);

        if (request.FilterByUserId.HasValue)
            query = query.Where(s => s.UserId == request.FilterByUserId.Value);

        var sessions = await query
            .OrderByDescending(s => s.CreatedAt)
            .Select(s => new ActiveSessionDto
            {
                SessionId = s.Id,
                UserId = s.UserId,
                UserEmail = s.User.Email,
                UserFullName = s.User.FullName,
                IpAddress = s.IpAddress,
                UserAgent = s.UserAgent,
                CreatedAt = s.CreatedAt,
                AccessTokenExpiresAt = s.AccessTokenExpiresAt,
                RefreshTokenExpiresAt = s.RefreshTokenExpiresAt
            })
            .Take(100)  // Güvenlik: Maksimum 100 sonuç
            .ToListAsync(ct);

        return Result<List<ActiveSessionDto>>.Success(sessions);
    }
}

// ============================================================================
// REVOKE SESSION — Admin belirli bir oturumu sonlandırır
// ============================================================================

[RequirePermission(Permissions.Sessions.Revoke)]
public record RevokeSessionCommand(Guid UserId) : IRequest<Result<object>>;

public class RevokeSessionHandler : IRequestHandler<RevokeSessionCommand, Result<object>>
{
    private readonly ISessionManager _sessionManager;
    private readonly ICurrentUserService _currentUser;

    public RevokeSessionHandler(ISessionManager sessionManager, ICurrentUserService currentUser)
    {
        _sessionManager = sessionManager;
        _currentUser = currentUser;
    }

    public async Task<Result<object>> Handle(RevokeSessionCommand request, CancellationToken ct)
    {
        if (_currentUser.UserId is null) return Result<object>.Unauthorized();

        await _sessionManager.RevokeAllSessionsAsync(
            request.UserId, _currentUser.UserId.Value.ToString(), ct);

        return Result.Success();
    }
}

// ============================================================================
// SESSION DTO
// ============================================================================

public class ActiveSessionDto
{
    public Guid SessionId { get; set; }
    public Guid UserId { get; set; }
    public string UserEmail { get; set; } = default!;
    public string UserFullName { get; set; } = default!;
    public string IpAddress { get; set; } = default!;
    public string UserAgent { get; set; } = default!;
    public DateTime CreatedAt { get; set; }
    public DateTime AccessTokenExpiresAt { get; set; }
    public DateTime RefreshTokenExpiresAt { get; set; }
}
