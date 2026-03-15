using CleanTenant.Application.Common.Interfaces;
using CleanTenant.Application.Common.Models;
using CleanTenant.Domain.Settings;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace CleanTenant.Application.Features.Settings;

// ============================================================================
// DTOs
// ============================================================================

public class SettingDto
{
    public Guid Id { get; set; }
    public string Key { get; set; } = default!;
    public string Value { get; set; } = default!;
    public string? Description { get; set; }
    public string ValueType { get; set; } = default!;
    public string Category { get; set; } = default!;
    public string Level { get; set; } = default!;
    public Guid? TenantId { get; set; }
    public Guid? CompanyId { get; set; }
    public bool IsReadOnly { get; set; }
    public bool IsSecret { get; set; }
    public string? UpdatedBy { get; set; }
    public DateTime? UpdatedAt { get; set; }
}

// ============================================================================
// GET ALL SETTINGS (grouped by category)
// ============================================================================

public record GetSettingsQuery : IRequest<Result<List<SettingDto>>>
{
    public SettingLevel? Level { get; init; }
    public Guid? TenantId { get; init; }
    public Guid? CompanyId { get; init; }
    public string? Category { get; init; }
}

public class GetSettingsHandler : IRequestHandler<GetSettingsQuery, Result<List<SettingDto>>>
{
    private readonly IApplicationDbContext _db;

    public GetSettingsHandler(IApplicationDbContext db) => _db = db;

    public async Task<Result<List<SettingDto>>> Handle(GetSettingsQuery request, CancellationToken ct)
    {
        var query = _db.SystemSettings.AsNoTracking().AsQueryable();

        if (request.Level.HasValue)
            query = query.Where(s => s.Level == request.Level.Value);
        if (request.TenantId.HasValue)
            query = query.Where(s => s.TenantId == request.TenantId.Value);
        if (request.CompanyId.HasValue)
            query = query.Where(s => s.CompanyId == request.CompanyId.Value);
        if (!string.IsNullOrWhiteSpace(request.Category))
            query = query.Where(s => s.Category == request.Category);

        var settings = await query
            .OrderBy(s => s.Category)
            .ThenBy(s => s.Key)
            .Select(s => new SettingDto
            {
                Id = s.Id,
                Key = s.Key,
                Value = s.IsSecret ? "********" : s.Value,
                Description = s.Description,
                ValueType = s.ValueType.ToString(),
                Category = s.Category,
                Level = s.Level.ToString(),
                TenantId = s.TenantId,
                CompanyId = s.CompanyId,
                IsReadOnly = s.IsReadOnly,
                IsSecret = s.IsSecret,
                UpdatedBy = s.UpdatedBy,
                UpdatedAt = s.UpdatedAt
            })
            .ToListAsync(ct);

        return Result<List<SettingDto>>.Success(settings);
    }
}

// ============================================================================
// GET SETTING VALUE (hierarchical)
// ============================================================================

public record GetSettingValueQuery(string Key) : IRequest<Result<SettingDto>>
{
    public Guid? TenantId { get; init; }
    public Guid? CompanyId { get; init; }
}

public class GetSettingValueHandler : IRequestHandler<GetSettingValueQuery, Result<SettingDto>>
{
    private readonly IApplicationDbContext _db;
    private readonly ISettingsService _settingsService;

    public GetSettingValueHandler(IApplicationDbContext db, ISettingsService settingsService)
    {
        _db = db;
        _settingsService = settingsService;
    }

    public async Task<Result<SettingDto>> Handle(GetSettingValueQuery request, CancellationToken ct)
    {
        var value = await _settingsService.GetAsync(request.Key, request.TenantId, request.CompanyId, ct);

        if (value is null)
            return Result<SettingDto>.NotFound($"Ayar bulunamadı: {request.Key}");

        // En öncelikli ayarı bul
        var setting = await _db.SystemSettings.AsNoTracking()
            .Where(s => s.Key == request.Key)
            .OrderByDescending(s => s.Level)
            .FirstOrDefaultAsync(ct);

        return Result<SettingDto>.Success(new SettingDto
        {
            Id = setting?.Id ?? Guid.Empty,
            Key = request.Key,
            Value = setting?.IsSecret == true ? "********" : value,
            Description = setting?.Description,
            ValueType = setting?.ValueType.ToString() ?? "String",
            Category = setting?.Category ?? "Genel",
            Level = setting?.Level.ToString() ?? "System"
        });
    }
}

// ============================================================================
// UPSERT SETTING (Create or Update)
// ============================================================================

public record UpsertSettingCommand : IRequest<Result<SettingDto>>
{
    public string Key { get; init; } = default!;
    public string Value { get; init; } = default!;
    public string? Description { get; init; }
    public string? Category { get; init; }
    public string? ValueType { get; init; }
    public SettingLevel Level { get; init; }
    public Guid? TenantId { get; init; }
    public Guid? CompanyId { get; init; }
}

public class UpsertSettingValidator : AbstractValidator<UpsertSettingCommand>
{
    public UpsertSettingValidator()
    {
        RuleFor(x => x.Key).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Value).NotNull();
    }
}

public class UpsertSettingHandler : IRequestHandler<UpsertSettingCommand, Result<SettingDto>>
{
    private readonly IApplicationDbContext _db;
    private readonly ICacheService _cache;
    private readonly ICurrentUserService _currentUser;
    private readonly IAuditDbContext _auditDb;
    private readonly ILogger<UpsertSettingHandler> _logger;

    public UpsertSettingHandler(IApplicationDbContext db, ICacheService cache, ICurrentUserService currentUser, IAuditDbContext auditDb, ILogger<UpsertSettingHandler> logger)
    {
        _db = db; _cache = cache; _currentUser = currentUser; _auditDb = auditDb; _logger = logger;
    }

    public async Task<Result<SettingDto>> Handle(UpsertSettingCommand request, CancellationToken ct)
    {
        var existing = await _db.SystemSettings.FirstOrDefaultAsync(s =>
            s.Key == request.Key &&
            s.Level == request.Level &&
            s.TenantId == request.TenantId &&
            s.CompanyId == request.CompanyId, ct);

        var actor = _currentUser.UserId?.ToString() ?? "SYSTEM";

        if (existing is not null)
        {
            // Update
            if (existing.IsReadOnly)
                return Result<SettingDto>.Failure($"'{request.Key}' ayarı salt okunurdur.", 400);

            var oldValue = existing.Value;
            existing.UpdateValue(request.Value, actor);
            await _db.SaveChangesAsync(ct);

            // Cache invalidate
            await _cache.RemoveAsync($"ct:settings:{request.Level}:{request.TenantId}:{request.CompanyId}:{request.Key}", ct);

            // KVKK log
            _auditDb.SecurityLogs.Add(new CleanTenant.Application.Common.Interfaces.SecurityLog
            {
                Id = Guid.CreateVersion7(), Timestamp = DateTime.UtcNow,
                UserId = _currentUser.UserId, UserEmail = _currentUser.Email,
                IpAddress = _currentUser.IpAddress ?? "unknown",
                EventType = "SettingChanged", IsSuccess = true,
                Description = $"Ayar güncellendi: {request.Key}",
                Details = System.Text.Json.JsonSerializer.Serialize(new
                {
                    Key = request.Key, OldValue = existing.IsSecret ? "***" : oldValue,
                    NewValue = existing.IsSecret ? "***" : request.Value,
                    Level = request.Level.ToString(), TenantId = request.TenantId, CompanyId = request.CompanyId
                })
            });
            await _auditDb.SaveChangesAsync(ct);

            _logger.LogInformation("[SETTINGS] Güncellendi: {Key} = {Value} (Level: {Level})", request.Key,
                existing.IsSecret ? "***" : request.Value, request.Level);

            return Result<SettingDto>.Success(MapToDto(existing));
        }

        // Create
        var valueType = Enum.TryParse<SettingValueType>(request.ValueType, true, out var vt) ? vt : SettingValueType.String;

        var setting = SystemSetting.Create(
            request.Key, request.Value,
            request.Category ?? "Genel",
            valueType, request.Level,
            request.TenantId, request.CompanyId,
            request.Description);

        _db.SystemSettings.Add(setting);
        await _db.SaveChangesAsync(ct);

        _logger.LogInformation("[SETTINGS] Oluşturuldu: {Key} = {Value} (Level: {Level})", request.Key, request.Value, request.Level);

        return Result<SettingDto>.Created(MapToDto(setting));
    }

    private static SettingDto MapToDto(SystemSetting s) => new()
    {
        Id = s.Id, Key = s.Key, Value = s.IsSecret ? "********" : s.Value,
        Description = s.Description, ValueType = s.ValueType.ToString(),
        Category = s.Category, Level = s.Level.ToString(),
        TenantId = s.TenantId, CompanyId = s.CompanyId,
        IsReadOnly = s.IsReadOnly, IsSecret = s.IsSecret,
        UpdatedBy = s.UpdatedBy, UpdatedAt = s.UpdatedAt
    };
}

// ============================================================================
// DELETE SETTING (tenant/company override kaldırma)
// ============================================================================

public record DeleteSettingCommand(Guid Id) : IRequest<Result<object>>;

public class DeleteSettingHandler : IRequestHandler<DeleteSettingCommand, Result<object>>
{
    private readonly IApplicationDbContext _db;
    private readonly ICacheService _cache;

    public DeleteSettingHandler(IApplicationDbContext db, ICacheService cache)
    {
        _db = db; _cache = cache;
    }

    public async Task<Result<object>> Handle(DeleteSettingCommand request, CancellationToken ct)
    {
        var setting = await _db.SystemSettings.FirstOrDefaultAsync(s => s.Id == request.Id, ct);
        if (setting is null) return Result<object>.NotFound("Ayar bulunamadı.");
        if (setting.IsReadOnly) return Result<object>.Failure("Salt okunur ayar silinemez.", 400);
        if (setting.Level == SettingLevel.System)
            return Result<object>.Failure("Sistem seviyesi ayarları silinemez. Değerini değiştirebilirsiniz.", 400);

        await _cache.RemoveAsync($"ct:settings:{setting.Level}:{setting.TenantId}:{setting.CompanyId}:{setting.Key}", ct);

        _db.SystemSettings.Remove(setting);
        await _db.SaveChangesAsync(ct);

        return Result.NoContent();
    }
}

// ============================================================================
// DEFAULT SETTINGS SEED HELPER
// ============================================================================

/// <summary>Varsayılan sistem ayarlarını oluşturur (idempotent).</summary>
public static class DefaultSettingsSeeder
{
    public static async Task SeedAsync(IApplicationDbContext db, CancellationToken ct = default)
    {
        if (await db.SystemSettings.AnyAsync(ct))
            return;

        var settings = new List<SystemSetting>
        {
            // ── JWT ──
            S("Jwt.AccessTokenExpirationMinutes", "15", "JWT", SettingValueType.Int, "Access Token süresi (dakika)"),
            S("Jwt.RefreshTokenExpirationDays", "7", "JWT", SettingValueType.Int, "Refresh Token süresi (gün)"),

            // ── Oturum ──
            S("Session.EnforceSingleSession", "true", "Oturum", SettingValueType.Bool, "Tek oturum kuralı"),
            S("Session.ValidateDeviceFingerprint", "true", "Oturum", SettingValueType.Bool, "Cihaz parmak izi doğrulaması"),
            S("Session.ValidateIpAddress", "true", "Oturum", SettingValueType.Bool, "IP adresi doğrulaması"),

            // ── Şifre Politikası ──
            S("PasswordPolicy.MinimumLength", "8", "Şifre", SettingValueType.Int, "Minimum şifre uzunluğu"),
            S("PasswordPolicy.RequireUppercase", "true", "Şifre", SettingValueType.Bool, "Büyük harf zorunlu"),
            S("PasswordPolicy.RequireLowercase", "true", "Şifre", SettingValueType.Bool, "Küçük harf zorunlu"),
            S("PasswordPolicy.RequireDigit", "true", "Şifre", SettingValueType.Bool, "Rakam zorunlu"),
            S("PasswordPolicy.RequireSpecialCharacter", "true", "Şifre", SettingValueType.Bool, "Özel karakter zorunlu"),
            S("PasswordPolicy.MaxFailedAccessAttempts", "5", "Şifre", SettingValueType.Int, "Maksimum başarısız giriş denemesi"),
            S("PasswordPolicy.LockoutDurationMinutes", "15", "Şifre", SettingValueType.Int, "Hesap kilitleme süresi (dakika)"),

            // ── 2FA ──
            S("TwoFactor.CodeExpirationMinutes", "5", "2FA", SettingValueType.Int, "2FA kodu geçerlilik süresi (dakika)"),
            S("TwoFactor.MaxFailedAttempts", "3", "2FA", SettingValueType.Int, "2FA maksimum deneme sayısı"),
            S("TwoFactor.EnableEmailFallback", "true", "2FA", SettingValueType.Bool, "E-posta fallback aktif"),

            // ── Erişim Politikası ──
            S("AccessPolicy.EnableRateLimit", "true", "Erişim", SettingValueType.Bool, "Rate limit aktif"),
            S("AccessPolicy.DefaultRateLimitPerMinute", "60", "Erişim", SettingValueType.Int, "Dakikadaki maksimum istek"),
            S("AccessPolicy.EnableIpBlacklist", "true", "Erişim", SettingValueType.Bool, "IP kara liste aktif"),

            // ── E-posta ──
            S("Email.Enabled", "false", "E-posta", SettingValueType.Bool, "SMTP e-posta gönderimi aktif"),

            // ── Genel ──
            S("General.DefaultTimeZone", "Europe/Istanbul", "Genel", SettingValueType.String, "Varsayılan zaman dilimi"),
            S("General.DateFormat", "dd.MM.yyyy", "Genel", SettingValueType.String, "Tarih formatı"),
            S("General.DateTimeFormat", "dd.MM.yyyy HH:mm:ss", "Genel", SettingValueType.String, "Tarih-saat formatı"),
        };

        db.SystemSettings.AddRange(settings);
        await db.SaveChangesAsync(ct);
    }

    private static SystemSetting S(string key, string value, string category, SettingValueType type, string description)
        => SystemSetting.Create(key, value, category, type, SettingLevel.System, description: description);
}
