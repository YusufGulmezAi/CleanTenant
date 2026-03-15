using CleanTenant.Application.Common.Interfaces;
using CleanTenant.Application.Common.Models;
using CleanTenant.Domain.Security;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace CleanTenant.Application.Features.AccessPolicies;

// ============================================================================
// DTOs
// ============================================================================

public class AccessPolicyDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = default!;
    public string? Description { get; set; }
    public string Level { get; set; } = default!;
    public Guid? TenantId { get; set; }
    public Guid? CompanyId { get; set; }
    public bool IsDefault { get; set; }
    public bool IsActive { get; set; }
    public bool DenyAllIps { get; set; }
    public string AllowedIpRanges { get; set; } = "[]";
    public bool DenyAllTimes { get; set; }
    public string AllowedDays { get; set; } = "[]";
    public string? AllowedTimeStart { get; set; }
    public string? AllowedTimeEnd { get; set; }
    public int AssignedUserCount { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class UserPolicyDto
{
    public Guid UserId { get; set; }
    public string UserEmail { get; set; } = default!;
    public Guid PolicyId { get; set; }
    public string PolicyName { get; set; } = default!;
    public bool IsDefault { get; set; }
    public string AssignedBy { get; set; } = default!;
    public DateTime AssignedAt { get; set; }
}

// ============================================================================
// GET POLICIES BY LEVEL
// ============================================================================

public record GetAccessPoliciesQuery : IRequest<Result<List<AccessPolicyDto>>>
{
    public PolicyLevel? Level { get; init; }
    public Guid? TenantId { get; init; }
    public Guid? CompanyId { get; init; }
}

public class GetAccessPoliciesHandler : IRequestHandler<GetAccessPoliciesQuery, Result<List<AccessPolicyDto>>>
{
    private readonly IApplicationDbContext _db;

    public GetAccessPoliciesHandler(IApplicationDbContext db) => _db = db;

    public async Task<Result<List<AccessPolicyDto>>> Handle(GetAccessPoliciesQuery request, CancellationToken ct)
    {
        var query = _db.AccessPolicies.AsNoTracking().AsQueryable();

        if (request.Level.HasValue)
            query = query.Where(p => p.Level == request.Level.Value);
        if (request.TenantId.HasValue)
            query = query.Where(p => p.TenantId == request.TenantId.Value);
        if (request.CompanyId.HasValue)
            query = query.Where(p => p.CompanyId == request.CompanyId.Value);

        var policies = await query
            .OrderByDescending(p => p.IsDefault)
            .ThenBy(p => p.Name)
            .Select(p => new AccessPolicyDto
            {
                Id = p.Id,
                Name = p.Name,
                Description = p.Description,
                Level = p.Level.ToString(),
                TenantId = p.TenantId,
                CompanyId = p.CompanyId,
                IsDefault = p.IsDefault,
                IsActive = p.IsActive,
                DenyAllIps = p.DenyAllIps,
                AllowedIpRanges = p.AllowedIpRanges,
                DenyAllTimes = p.DenyAllTimes,
                AllowedDays = p.AllowedDays,
                AllowedTimeStart = p.AllowedTimeStart.HasValue ? p.AllowedTimeStart.Value.ToString("HH:mm") : null,
                AllowedTimeEnd = p.AllowedTimeEnd.HasValue ? p.AllowedTimeEnd.Value.ToString("HH:mm") : null,
                AssignedUserCount = p.Assignments.Count,
                CreatedAt = p.CreatedAt
            })
            .ToListAsync(ct);

        return Result<List<AccessPolicyDto>>.Success(policies);
    }
}

// ============================================================================
// CREATE POLICY
// ============================================================================

public record CreateAccessPolicyCommand : IRequest<Result<AccessPolicyDto>>
{
    public string Name { get; init; } = default!;
    public string? Description { get; init; }
    public PolicyLevel Level { get; init; }
    public Guid? TenantId { get; init; }
    public Guid? CompanyId { get; init; }
    public bool DenyAllIps { get; init; }
    public string AllowedIpRanges { get; init; } = "[]";
    public bool DenyAllTimes { get; init; }
    public string AllowedDays { get; init; } = "[]";
    public string? AllowedTimeStart { get; init; }
    public string? AllowedTimeEnd { get; init; }
}

public class CreateAccessPolicyValidator : AbstractValidator<CreateAccessPolicyCommand>
{
    public CreateAccessPolicyValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(200);
    }
}

public class CreateAccessPolicyHandler : IRequestHandler<CreateAccessPolicyCommand, Result<AccessPolicyDto>>
{
    private readonly IApplicationDbContext _db;
    private readonly ICurrentUserService _currentUser;
    private readonly IAuditDbContext _auditDb;
    private readonly ILogger<CreateAccessPolicyHandler> _logger;

    public CreateAccessPolicyHandler(IApplicationDbContext db, ICurrentUserService currentUser, IAuditDbContext auditDb, ILogger<CreateAccessPolicyHandler> logger)
    {
        _db = db; _currentUser = currentUser; _auditDb = auditDb; _logger = logger;
    }

    public async Task<Result<AccessPolicyDto>> Handle(CreateAccessPolicyCommand request, CancellationToken ct)
    {
        var policy = AccessPolicy.CreateCustom(
            request.Name, request.Level,
            request.TenantId, request.CompanyId,
            request.Description, _currentUser.UserId?.ToString());

        // IP kuralları
        if (!request.DenyAllIps)
            policy.UpdateIpRules(false, request.AllowedIpRanges, _currentUser.UserId?.ToString() ?? "SYSTEM");

        // Zaman kuralları
        if (!request.DenyAllTimes)
        {
            TimeOnly? start = request.AllowedTimeStart != null ? TimeOnly.Parse(request.AllowedTimeStart) : null;
            TimeOnly? end = request.AllowedTimeEnd != null ? TimeOnly.Parse(request.AllowedTimeEnd) : null;
            policy.UpdateTimeRules(false, request.AllowedDays, start, end, _currentUser.UserId?.ToString() ?? "SYSTEM");
        }

        _db.AccessPolicies.Add(policy);
        await _db.SaveChangesAsync(ct);

        // Cross-level loglama
        await LogPolicyActionAsync("PolicyCreated", policy, null, ct);

        _logger.LogInformation("[POLICY] Oluşturuldu: {Name}, Level: {Level}", policy.Name, policy.Level);

        return Result<AccessPolicyDto>.Created(MapToDto(policy));
    }

    private async Task LogPolicyActionAsync(string action, AccessPolicy policy, Guid? targetUserId, CancellationToken ct)
    {
        _auditDb.SecurityLogs.Add(new CleanTenant.Application.Common.Interfaces.SecurityLog
        {
            Id = Guid.CreateVersion7(),
            Timestamp = DateTime.UtcNow,
            UserId = _currentUser.UserId,
            UserEmail = _currentUser.Email,
            IpAddress = _currentUser.IpAddress ?? "unknown",
            UserAgent = _currentUser.UserAgent,
            EventType = "AccessPolicy",
            IsSuccess = true,
            Description = $"{action}: {policy.Name} (Level: {policy.Level})",
            Details = System.Text.Json.JsonSerializer.Serialize(new
            {
                PolicyId = policy.Id,
                PolicyName = policy.Name,
                Level = policy.Level.ToString(),
                TenantId = policy.TenantId,
                CompanyId = policy.CompanyId,
                TargetUserId = targetUserId,
                Action = action
            })
        });
        await _auditDb.SaveChangesAsync(ct);
    }

    private static AccessPolicyDto MapToDto(AccessPolicy p) => new()
    {
        Id = p.Id, Name = p.Name, Description = p.Description,
        Level = p.Level.ToString(), TenantId = p.TenantId, CompanyId = p.CompanyId,
        IsDefault = p.IsDefault, IsActive = p.IsActive,
        DenyAllIps = p.DenyAllIps, AllowedIpRanges = p.AllowedIpRanges,
        DenyAllTimes = p.DenyAllTimes, AllowedDays = p.AllowedDays,
        AllowedTimeStart = p.AllowedTimeStart?.ToString("HH:mm"),
        AllowedTimeEnd = p.AllowedTimeEnd?.ToString("HH:mm"),
        CreatedAt = p.CreatedAt
    };
}

// ============================================================================
// UPDATE POLICY
// ============================================================================

public record UpdateAccessPolicyCommand : IRequest<Result<AccessPolicyDto>>
{
    public Guid Id { get; init; }
    public string? Name { get; init; }
    public string? Description { get; init; }
    public bool DenyAllIps { get; init; }
    public string AllowedIpRanges { get; init; } = "[]";
    public bool DenyAllTimes { get; init; }
    public string AllowedDays { get; init; } = "[]";
    public string? AllowedTimeStart { get; init; }
    public string? AllowedTimeEnd { get; init; }
}

public class UpdateAccessPolicyHandler : IRequestHandler<UpdateAccessPolicyCommand, Result<AccessPolicyDto>>
{
    private readonly IApplicationDbContext _db;
    private readonly ICurrentUserService _currentUser;
    private readonly IAuditDbContext _auditDb;
    private readonly ILogger<UpdateAccessPolicyHandler> _logger;

    public UpdateAccessPolicyHandler(IApplicationDbContext db, ICurrentUserService currentUser, IAuditDbContext auditDb, ILogger<UpdateAccessPolicyHandler> logger)
    { _db = db; _currentUser = currentUser; _auditDb = auditDb; _logger = logger; }

    public async Task<Result<AccessPolicyDto>> Handle(UpdateAccessPolicyCommand request, CancellationToken ct)
    {
        var policy = await _db.AccessPolicies.FirstOrDefaultAsync(p => p.Id == request.Id, ct);
        if (policy is null) return Result<AccessPolicyDto>.NotFound("Politika bulunamadı.");

        var actor = _currentUser.UserId?.ToString() ?? "SYSTEM";

        // İsim/açıklama (default'ta değiştirilemez)
        if (!policy.IsDefault && !string.IsNullOrWhiteSpace(request.Name))
            policy.UpdateInfo(request.Name, request.Description, actor);

        // IP kuralları
        policy.UpdateIpRules(request.DenyAllIps, request.AllowedIpRanges, actor);

        // Zaman kuralları
        TimeOnly? start = request.AllowedTimeStart != null ? TimeOnly.Parse(request.AllowedTimeStart) : null;
        TimeOnly? end = request.AllowedTimeEnd != null ? TimeOnly.Parse(request.AllowedTimeEnd) : null;
        policy.UpdateTimeRules(request.DenyAllTimes, request.AllowedDays, start, end, actor);

        await _db.SaveChangesAsync(ct);

        // KVKK log
        _auditDb.SecurityLogs.Add(new CleanTenant.Application.Common.Interfaces.SecurityLog
        {
            Id = Guid.CreateVersion7(), Timestamp = DateTime.UtcNow,
            UserId = _currentUser.UserId, UserEmail = _currentUser.Email,
            IpAddress = _currentUser.IpAddress ?? "unknown",
            EventType = "AccessPolicy", IsSuccess = true,
            Description = $"PolicyUpdated: {policy.Name} (Level: {policy.Level})"
        });
        await _auditDb.SaveChangesAsync(ct);

        return Result<AccessPolicyDto>.Success(new AccessPolicyDto
        {
            Id = policy.Id, Name = policy.Name, Description = policy.Description,
            Level = policy.Level.ToString(), IsDefault = policy.IsDefault, IsActive = policy.IsActive,
            DenyAllIps = policy.DenyAllIps, AllowedIpRanges = policy.AllowedIpRanges,
            DenyAllTimes = policy.DenyAllTimes, AllowedDays = policy.AllowedDays,
            AllowedTimeStart = policy.AllowedTimeStart?.ToString("HH:mm"),
            AllowedTimeEnd = policy.AllowedTimeEnd?.ToString("HH:mm"),
            CreatedAt = policy.CreatedAt
        });
    }
}

// ============================================================================
// DELETE POLICY (default silinemez → kullanıcılar default'a düşer)
// ============================================================================

public record DeleteAccessPolicyCommand(Guid Id) : IRequest<Result<object>>;

public class DeleteAccessPolicyHandler : IRequestHandler<DeleteAccessPolicyCommand, Result<object>>
{
    private readonly IApplicationDbContext _db;
    private readonly ICurrentUserService _currentUser;
    private readonly IAuditDbContext _auditDb;
    private readonly ILogger<DeleteAccessPolicyHandler> _logger;

    public DeleteAccessPolicyHandler(IApplicationDbContext db, ICurrentUserService currentUser, IAuditDbContext auditDb, ILogger<DeleteAccessPolicyHandler> logger)
    { _db = db; _currentUser = currentUser; _auditDb = auditDb; _logger = logger; }

    public async Task<Result<object>> Handle(DeleteAccessPolicyCommand request, CancellationToken ct)
    {
        var policy = await _db.AccessPolicies
            .Include(p => p.Assignments)
            .FirstOrDefaultAsync(p => p.Id == request.Id, ct);

        if (policy is null) return Result<object>.NotFound("Politika bulunamadı.");
        if (policy.IsDefault) return Result<object>.Failure("Default politika silinemez.", 400);

        // Bu politikaya atanmış kullanıcıları default'a düşür
        if (policy.Assignments.Count > 0)
        {
            var defaultPolicy = await _db.AccessPolicies
                .FirstOrDefaultAsync(p => p.Level == policy.Level
                    && p.TenantId == policy.TenantId
                    && p.CompanyId == policy.CompanyId
                    && p.IsDefault, ct);

            if (defaultPolicy is not null)
            {
                foreach (var assignment in policy.Assignments)
                {
                    assignment.AccessPolicyId = defaultPolicy.Id;
                    assignment.AssignedBy = "SYSTEM:PolicyDeleted";
                    assignment.AssignedAt = DateTime.UtcNow;
                }
                _logger.LogWarning("[POLICY] Silinen politika kullanıcıları default'a düşürüldü: {Count} kullanıcı", policy.Assignments.Count);
            }
        }

        _db.AccessPolicies.Remove(policy);
        await _db.SaveChangesAsync(ct);

        // KVKK log
        _auditDb.SecurityLogs.Add(new CleanTenant.Application.Common.Interfaces.SecurityLog
        {
            Id = Guid.CreateVersion7(), Timestamp = DateTime.UtcNow,
            UserId = _currentUser.UserId, UserEmail = _currentUser.Email,
            IpAddress = _currentUser.IpAddress ?? "unknown",
            EventType = "AccessPolicy", IsSuccess = true,
            Description = $"PolicyDeleted: {policy.Name}, {policy.Assignments.Count} kullanıcı default'a düşürüldü"
        });
        await _auditDb.SaveChangesAsync(ct);

        return Result.NoContent();
    }
}

// ============================================================================
// ASSIGN POLICY TO USER
// ============================================================================

public record AssignPolicyCommand : IRequest<Result<object>>
{
    public Guid PolicyId { get; init; }
    public Guid UserId { get; init; }
}

public class AssignPolicyHandler : IRequestHandler<AssignPolicyCommand, Result<object>>
{
    private readonly IApplicationDbContext _db;
    private readonly ICurrentUserService _currentUser;
    private readonly IAuditDbContext _auditDb;
    private readonly ILogger<AssignPolicyHandler> _logger;

    public AssignPolicyHandler(IApplicationDbContext db, ICurrentUserService currentUser, IAuditDbContext auditDb, ILogger<AssignPolicyHandler> logger)
    { _db = db; _currentUser = currentUser; _auditDb = auditDb; _logger = logger; }

    public async Task<Result<object>> Handle(AssignPolicyCommand request, CancellationToken ct)
    {
        var policy = await _db.AccessPolicies.FirstOrDefaultAsync(p => p.Id == request.PolicyId, ct);
        if (policy is null) return Result<object>.NotFound("Politika bulunamadı.");

        var userExists = await _db.Users.AnyAsync(u => u.Id == request.UserId, ct);
        if (!userExists) return Result<object>.NotFound("Kullanıcı bulunamadı.");

        // Mevcut atamayı kaldır
        var existingAssignment = await _db.UserPolicyAssignments
            .FirstOrDefaultAsync(a => a.UserId == request.UserId, ct);

        var previousPolicyName = "Yok";
        if (existingAssignment is not null)
        {
            var prevPolicy = await _db.AccessPolicies.FirstOrDefaultAsync(p => p.Id == existingAssignment.AccessPolicyId, ct);
            previousPolicyName = prevPolicy?.Name ?? "Bilinmeyen";
            _db.UserPolicyAssignments.Remove(existingAssignment);
        }

        // Yeni atama
        _db.UserPolicyAssignments.Add(new UserPolicyAssignment
        {
            Id = Guid.CreateVersion7(),
            UserId = request.UserId,
            AccessPolicyId = request.PolicyId,
            AssignedBy = _currentUser.UserId?.ToString() ?? "SYSTEM",
            AssignedAt = DateTime.UtcNow
        });

        await _db.SaveChangesAsync(ct);

        // KVKK — cross-level detaylı log
        _auditDb.SecurityLogs.Add(new CleanTenant.Application.Common.Interfaces.SecurityLog
        {
            Id = Guid.CreateVersion7(), Timestamp = DateTime.UtcNow,
            UserId = _currentUser.UserId, UserEmail = _currentUser.Email,
            IpAddress = _currentUser.IpAddress ?? "unknown",
            UserAgent = _currentUser.UserAgent,
            EventType = "CrossLevelPolicyChange", IsSuccess = true,
            Description = $"PolicyAssigned: {policy.Name} → User:{request.UserId}",
            Details = System.Text.Json.JsonSerializer.Serialize(new
            {
                Action = "PolicyAssigned",
                TargetUserId = request.UserId,
                NewPolicyId = policy.Id,
                NewPolicyName = policy.Name,
                PreviousPolicyName = previousPolicyName,
                PolicyLevel = policy.Level.ToString(),
                TenantId = policy.TenantId,
                CompanyId = policy.CompanyId
            })
        });
        await _auditDb.SaveChangesAsync(ct);

        _logger.LogInformation("[POLICY] Atandı: Policy={PolicyName} → User={UserId}", policy.Name, request.UserId);
        return Result.Success();
    }
}

// ============================================================================
// UNASSIGN POLICY (→ default'a düş)
// ============================================================================

public record UnassignPolicyCommand(Guid UserId) : IRequest<Result<object>>
{
    public PolicyLevel Level { get; init; }
    public Guid? TenantId { get; init; }
    public Guid? CompanyId { get; init; }
}

public class UnassignPolicyHandler : IRequestHandler<UnassignPolicyCommand, Result<object>>
{
    private readonly IApplicationDbContext _db;
    private readonly ICurrentUserService _currentUser;
    private readonly IAuditDbContext _auditDb;

    public UnassignPolicyHandler(IApplicationDbContext db, ICurrentUserService currentUser, IAuditDbContext auditDb)
    { _db = db; _currentUser = currentUser; _auditDb = auditDb; }

    public async Task<Result<object>> Handle(UnassignPolicyCommand request, CancellationToken ct)
    {
        var assignment = await _db.UserPolicyAssignments
            .Include(a => a.AccessPolicy)
            .FirstOrDefaultAsync(a => a.UserId == request.UserId, ct);

        if (assignment is null) return Result<object>.Failure("Kullanıcıya atanmış politika bulunamadı.", 404);

        var removedPolicyName = assignment.AccessPolicy.Name;

        // Default politikayı bul
        var defaultPolicy = await _db.AccessPolicies
            .FirstOrDefaultAsync(p => p.Level == request.Level
                && p.TenantId == request.TenantId
                && p.CompanyId == request.CompanyId
                && p.IsDefault, ct);

        if (defaultPolicy is not null)
        {
            assignment.AccessPolicyId = defaultPolicy.Id;
            assignment.AssignedBy = "SYSTEM:PolicyUnassigned";
            assignment.AssignedAt = DateTime.UtcNow;
        }
        else
        {
            _db.UserPolicyAssignments.Remove(assignment);
        }

        await _db.SaveChangesAsync(ct);

        // KVKK log
        _auditDb.SecurityLogs.Add(new CleanTenant.Application.Common.Interfaces.SecurityLog
        {
            Id = Guid.CreateVersion7(), Timestamp = DateTime.UtcNow,
            UserId = _currentUser.UserId, UserEmail = _currentUser.Email,
            IpAddress = _currentUser.IpAddress ?? "unknown",
            EventType = "CrossLevelPolicyChange", IsSuccess = true,
            Description = $"PolicyUnassigned: {removedPolicyName} → User:{request.UserId} → Default'a düşürüldü"
        });
        await _auditDb.SaveChangesAsync(ct);

        return Result.Success();
    }
}

// ============================================================================
// GET USER'S ACTIVE POLICY
// ============================================================================

public record GetUserPolicyQuery(Guid UserId) : IRequest<Result<UserPolicyDto>>;

public class GetUserPolicyHandler : IRequestHandler<GetUserPolicyQuery, Result<UserPolicyDto>>
{
    private readonly IApplicationDbContext _db;

    public GetUserPolicyHandler(IApplicationDbContext db) => _db = db;

    public async Task<Result<UserPolicyDto>> Handle(GetUserPolicyQuery request, CancellationToken ct)
    {
        var assignment = await _db.UserPolicyAssignments
            .AsNoTracking()
            .Include(a => a.AccessPolicy)
            .Include(a => a.User)
            .FirstOrDefaultAsync(a => a.UserId == request.UserId, ct);

        if (assignment is null)
            return Result<UserPolicyDto>.NotFound("Kullanıcıya atanmış politika bulunamadı. GİRİŞ YASAK.");

        return Result<UserPolicyDto>.Success(new UserPolicyDto
        {
            UserId = assignment.UserId,
            UserEmail = assignment.User.Email,
            PolicyId = assignment.AccessPolicyId,
            PolicyName = assignment.AccessPolicy.Name,
            IsDefault = assignment.AccessPolicy.IsDefault,
            AssignedBy = assignment.AssignedBy,
            AssignedAt = assignment.AssignedAt
        });
    }
}
