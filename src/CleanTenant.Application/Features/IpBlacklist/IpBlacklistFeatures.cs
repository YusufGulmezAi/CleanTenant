using CleanTenant.Application.Common.Interfaces;
using CleanTenant.Application.Common.Models;
using CleanTenant.Domain.Security;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace CleanTenant.Application.Features.IpBlacklist;

// ============================================================================
// DTOs
// ============================================================================

public class IpBlacklistDto
{
    public Guid Id { get; set; }
    public string IpAddressOrRange { get; set; } = default!;
    public string? Reason { get; set; }
    public DateTime? ExpiresAt { get; set; }
    public bool IsActive { get; set; }
    public DateTime CreatedAt { get; set; }
    public string? CreatedBy { get; set; }
}

// ============================================================================
// GET ALL
// ============================================================================

public record GetIpBlacklistQuery(bool IncludeInactive = false) : IRequest<Result<List<IpBlacklistDto>>>;

public class GetIpBlacklistHandler : IRequestHandler<GetIpBlacklistQuery, Result<List<IpBlacklistDto>>>
{
    private readonly IApplicationDbContext _db;

    public GetIpBlacklistHandler(IApplicationDbContext db) => _db = db;

    public async Task<Result<List<IpBlacklistDto>>> Handle(GetIpBlacklistQuery request, CancellationToken ct)
    {
        var query = _db.IpBlacklists.AsNoTracking().AsQueryable();

        if (!request.IncludeInactive)
            query = query.Where(b => b.IsActive);

        var items = await query
            .OrderByDescending(b => b.CreatedAt)
            .Select(b => new IpBlacklistDto
            {
                Id = b.Id,
                IpAddressOrRange = b.IpAddressOrRange,
                Reason = b.Reason,
                ExpiresAt = b.ExpiresAt,
                IsActive = b.IsActive,
                CreatedAt = b.CreatedAt,
                CreatedBy = b.CreatedBy
            })
            .ToListAsync(ct);

        return Result<List<IpBlacklistDto>>.Success(items);
    }
}

// ============================================================================
// ADD IP TO BLACKLIST
// ============================================================================

public record AddIpBlacklistCommand : IRequest<Result<IpBlacklistDto>>
{
    public string IpAddressOrRange { get; init; } = default!;
    public string? Reason { get; init; }
    public int? ExpiresInMinutes { get; init; }
}

public class AddIpBlacklistValidator : AbstractValidator<AddIpBlacklistCommand>
{
    public AddIpBlacklistValidator()
    {
        RuleFor(x => x.IpAddressOrRange).NotEmpty().MaximumLength(50);
    }
}

public class AddIpBlacklistHandler : IRequestHandler<AddIpBlacklistCommand, Result<IpBlacklistDto>>
{
    private readonly IApplicationDbContext _db;
    private readonly ICacheService _cache;
    private readonly ICurrentUserService _currentUser;
    private readonly IAuditDbContext _auditDb;
    private readonly ILogger<AddIpBlacklistHandler> _logger;

    public AddIpBlacklistHandler(IApplicationDbContext db, ICacheService cache,
        ICurrentUserService currentUser, IAuditDbContext auditDb, ILogger<AddIpBlacklistHandler> logger)
    {
        _db = db; _cache = cache; _currentUser = currentUser; _auditDb = auditDb; _logger = logger;
    }

    public async Task<Result<IpBlacklistDto>> Handle(AddIpBlacklistCommand request, CancellationToken ct)
    {
        // Zaten var mı?
        var exists = await _db.IpBlacklists.AnyAsync(
            b => b.IpAddressOrRange == request.IpAddressOrRange && b.IsActive, ct);

        if (exists)
            return Result<IpBlacklistDto>.Failure("Bu IP zaten kara listede.", 400);

        DateTime? expiresAt = request.ExpiresInMinutes.HasValue
            ? DateTime.UtcNow.AddMinutes(request.ExpiresInMinutes.Value)
            : null;

        var entry = Domain.Security.IpBlacklist.Create(request.IpAddressOrRange, request.Reason, expiresAt);
        _db.IpBlacklists.Add(entry);
        await _db.SaveChangesAsync(ct);

        // Redis SET'e ekle (IpBlacklistMiddleware burayı kontrol ediyor)
        await _cache.SetAddAsync("ct:blacklist:ips", request.IpAddressOrRange, ct);

        // Security log
        _auditDb.SecurityLogs.Add(new SecurityLog
        {
            Id = Guid.CreateVersion7(), Timestamp = DateTime.UtcNow,
            UserId = _currentUser.UserId, UserEmail = _currentUser.Email,
            IpAddress = _currentUser.IpAddress ?? "unknown",
            EventType = "IpBlacklist", IsSuccess = true,
            Description = $"IP kara listeye eklendi: {request.IpAddressOrRange}",
            Details = System.Text.Json.JsonSerializer.Serialize(new
            {
                Ip = request.IpAddressOrRange, Reason = request.Reason,
                ExpiresAt = expiresAt
            })
        });
        await _auditDb.SaveChangesAsync(ct);

        _logger.LogWarning("[BLACKLIST] IP eklendi: {Ip}, Neden: {Reason}", request.IpAddressOrRange, request.Reason);

        return Result<IpBlacklistDto>.Created(new IpBlacklistDto
        {
            Id = entry.Id, IpAddressOrRange = entry.IpAddressOrRange,
            Reason = entry.Reason, ExpiresAt = entry.ExpiresAt,
            IsActive = true, CreatedAt = entry.CreatedAt
        });
    }
}

// ============================================================================
// REMOVE IP FROM BLACKLIST
// ============================================================================

public record RemoveIpBlacklistCommand(Guid Id) : IRequest<Result<object>>;

public class RemoveIpBlacklistHandler : IRequestHandler<RemoveIpBlacklistCommand, Result<object>>
{
    private readonly IApplicationDbContext _db;
    private readonly ICacheService _cache;
    private readonly ICurrentUserService _currentUser;
    private readonly IAuditDbContext _auditDb;
    private readonly ILogger<RemoveIpBlacklistHandler> _logger;

    public RemoveIpBlacklistHandler(IApplicationDbContext db, ICacheService cache,
        ICurrentUserService currentUser, IAuditDbContext auditDb, ILogger<RemoveIpBlacklistHandler> logger)
    {
        _db = db; _cache = cache; _currentUser = currentUser; _auditDb = auditDb; _logger = logger;
    }

    public async Task<Result<object>> Handle(RemoveIpBlacklistCommand request, CancellationToken ct)
    {
        var entry = await _db.IpBlacklists.FirstOrDefaultAsync(b => b.Id == request.Id, ct);
        if (entry is null) return Result<object>.NotFound("IP kaydı bulunamadı.");

        entry.Deactivate();
        await _db.SaveChangesAsync(ct);

        // Redis SET'ten kaldır
        await _cache.SetRemoveAsync("ct:blacklist:ips", entry.IpAddressOrRange, ct);

        // Security log
        _auditDb.SecurityLogs.Add(new SecurityLog
        {
            Id = Guid.CreateVersion7(), Timestamp = DateTime.UtcNow,
            UserId = _currentUser.UserId, UserEmail = _currentUser.Email,
            IpAddress = _currentUser.IpAddress ?? "unknown",
            EventType = "IpBlacklist", IsSuccess = true,
            Description = $"IP kara listeden kaldırıldı: {entry.IpAddressOrRange}"
        });
        await _auditDb.SaveChangesAsync(ct);

        _logger.LogWarning("[BLACKLIST] IP kaldırıldı: {Ip}", entry.IpAddressOrRange);

        return Result.Success();
    }
}

// ============================================================================
// CHECK IP
// ============================================================================

public record CheckIpBlacklistQuery(string IpAddress) : IRequest<Result<object>>;

public class CheckIpBlacklistHandler : IRequestHandler<CheckIpBlacklistQuery, Result<object>>
{
    private readonly ICacheService _cache;

    public CheckIpBlacklistHandler(ICacheService cache) => _cache = cache;

    public async Task<Result<object>> Handle(CheckIpBlacklistQuery request, CancellationToken ct)
    {
        var isBlacklisted = await _cache.SetContainsAsync("ct:blacklist:ips", request.IpAddress, ct);

        return Result<object>.Success(new
        {
            ipAddress = request.IpAddress,
            isBlacklisted
        });
    }
}
