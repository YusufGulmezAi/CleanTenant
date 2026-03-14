using CleanTenant.Application.Common.Interfaces;
using CleanTenant.Domain.Enums;
using CleanTenant.Domain.Security;
using CleanTenant.Shared.Constants;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace CleanTenant.Infrastructure.Security;

/// <summary>
/// Oturum yönetimi servisi — Redis + DB dual storage.
/// 
/// <para><b>DUAL STORAGE STRATEJİSİ:</b></para>
/// <code>
/// Redis (Hızlı erişim)              DB (Kalıcılık + Audit)
/// ─────────────────────             ───────────────────────
/// session:{userId} → JSON           UserSessions tablosu
/// user:{userId}:refresh → hash      RefreshToken hash
/// user:{userId}:blocked → flag      UserBlocks tablosu
/// ct:temp:{token} → userId          (TempToken sadece Redis'te)
/// </code>
/// 
/// <para><b>CACHE SİLİNDİĞİNDE NE OLUR?</b></para>
/// <list type="bullet">
///   <item>RefreshToken cache'te yoksa → DB'den kontrol edilir → Varsa cache'e geri yazılır</item>
///   <item>Admin force logout yaparsa → Hem cache hem DB'den silinir → Logout kesinleşir</item>
///   <item>Redis tamamen çökerse → DB fallback devreye girer (graceful degradation)</item>
/// </list>
/// </summary>
public class SessionManager : ISessionManager
{
    private readonly ICacheService _cache;
    private readonly IApplicationDbContext _db;
    private readonly TokenService _tokenService;
    private readonly ILogger<SessionManager> _logger;
    private readonly bool _enforceSingleSession;

    public SessionManager(
        ICacheService cache,
        IApplicationDbContext db,
        TokenService tokenService,
        IConfiguration configuration,
        ILogger<SessionManager> logger)
    {
        _cache = cache;
        _db = db;
        _tokenService = tokenService;
        _logger = logger;
        _enforceSingleSession = bool.Parse(
            configuration["CleanTenant:Session:EnforceSingleSession"] ?? "true");
    }

    /// <inheritdoc />
    public async Task<SessionInfo> CreateSessionAsync(
        Guid userId, string ipAddress, string userAgent, CancellationToken ct = default)
    {
        // Tek oturum kuralı — önceki oturumları sonlandır
        if (_enforceSingleSession)
        {
            await RevokeAllSessionsAsync(userId, "SYSTEM:SingleSession", ct);
        }

        // Token üret
        var accessToken = _tokenService.GenerateAccessToken(userId, "");
        var refreshToken = _tokenService.GenerateRefreshToken();
        var deviceHash = DeviceFingerprintService.GenerateHash(ipAddress, userAgent);

        // DB'ye kaydet
        var session = UserSession.Create(
            userId,
            TokenService.HashToken(accessToken.Token),
            TokenService.HashToken(refreshToken.Token),
            ipAddress,
            userAgent,
            deviceHash,
            accessToken.ExpiresAt,
            refreshToken.ExpiresAt);

        _db.UserSessions.Add(session);
        await _db.SaveChangesAsync(ct);

        // Redis'e kaydet — oturum bilgisi
        var sessionData = new CachedSession
        {
            SessionId = session.Id,
            UserId = userId,
            DeviceHash = deviceHash,
            IpAddress = ipAddress,
            AccessTokenExpiresAt = accessToken.ExpiresAt,
            RefreshTokenExpiresAt = refreshToken.ExpiresAt
        };

        await _cache.SetAsync(
            CacheKeys.Session(userId),
            sessionData,
            TimeSpan.FromDays(7),
            ct);

        // Redis'e RefreshToken hash'ini kaydet (dual storage)
        await _cache.SetAsync(
            $"{CacheKeys.Session(userId)}:refresh",
            TokenService.HashToken(refreshToken.Token),
            TimeSpan.FromDays(7),
            ct);

        // Redis'e device hash kaydet
        await _cache.SetAsync(
            CacheKeys.SessionDevice(userId),
            deviceHash,
            TimeSpan.FromDays(7),
            ct);

        _logger.LogInformation(
            "Oturum oluşturuldu: UserId={UserId}, IP={IpAddress}, SessionId={SessionId}",
            userId, ipAddress, session.Id);

        return new SessionInfo(
            accessToken.Token,
            refreshToken.Token,
            accessToken.ExpiresAt,
            refreshToken.ExpiresAt);
    }

    /// <inheritdoc />
    public async Task<bool> ValidateSessionAsync(
        Guid userId, string tokenHash, string deviceHash, CancellationToken ct = default)
    {
        // 1. Kullanıcı bloke mi?
        if (await IsUserBlockedAsync(userId, ct))
            return false;

        // 2. Redis'ten oturum bilgisi al
        var session = await _cache.GetAsync<CachedSession>(CacheKeys.Session(userId), ct);

        if (session is null)
        {
            // Cache'te yok — DB'den kontrol et
            var dbSession = await _db.UserSessions
                .Where(s => s.UserId == userId && !s.IsRevoked)
                .OrderByDescending(s => s.CreatedAt)
                .FirstOrDefaultAsync(ct);

            if (dbSession is null || !dbSession.IsValid())
                return false;

            // DB'den bulundu — cache'e geri yaz
            session = new CachedSession
            {
                SessionId = dbSession.Id,
                UserId = userId,
                DeviceHash = dbSession.DeviceHash,
                IpAddress = dbSession.IpAddress,
                AccessTokenExpiresAt = dbSession.AccessTokenExpiresAt,
                RefreshTokenExpiresAt = dbSession.RefreshTokenExpiresAt
            };

            await _cache.SetAsync(CacheKeys.Session(userId), session, TimeSpan.FromHours(1), ct);
        }

        // 3. Device fingerprint eşleşme kontrolü
        if (session.DeviceHash != deviceHash)
        {
            _logger.LogWarning(
                "Device fingerprint uyumsuz! UserId={UserId}, Beklenen={Expected}, Gelen={Actual}",
                userId, session.DeviceHash, deviceHash);
            return false;
        }

        return true;
    }

    /// <inheritdoc />
    public async Task RevokeSessionAsync(Guid userId, CancellationToken ct = default)
    {
        // Redis'ten sil
        await _cache.RemoveAsync(CacheKeys.Session(userId), ct);
        await _cache.RemoveAsync($"{CacheKeys.Session(userId)}:refresh", ct);
        await _cache.RemoveAsync(CacheKeys.SessionDevice(userId), ct);

        // DB'de revoke et
        var sessions = await _db.UserSessions
            .Where(s => s.UserId == userId && !s.IsRevoked)
            .ToListAsync(ct);

        foreach (var session in sessions)
        {
            session.Revoke();
        }

        await _db.SaveChangesAsync(ct);

        _logger.LogInformation("Oturum sonlandırıldı: UserId={UserId}", userId);
    }

    /// <inheritdoc />
    public async Task RevokeAllSessionsAsync(Guid userId, string? revokedBy = null, CancellationToken ct = default)
    {
        // Redis'ten sil
        await _cache.RemoveAsync(CacheKeys.Session(userId), ct);
        await _cache.RemoveAsync($"{CacheKeys.Session(userId)}:refresh", ct);
        await _cache.RemoveAsync(CacheKeys.SessionDevice(userId), ct);
        await _cache.RemoveAsync(CacheKeys.UserRoles(userId), ct);
        await _cache.RemoveAsync(CacheKeys.UserPermissions(userId), ct);

        // DB'de tüm aktif oturumları revoke et
        var sessions = await _db.UserSessions
            .Where(s => s.UserId == userId && !s.IsRevoked)
            .ToListAsync(ct);

        foreach (var session in sessions)
        {
            session.Revoke(revokedBy);
        }

        if (sessions.Count > 0)
            await _db.SaveChangesAsync(ct);

        _logger.LogInformation(
            "Tüm oturumlar sonlandırıldı: UserId={UserId}, RevokedBy={RevokedBy}, Count={Count}",
            userId, revokedBy, sessions.Count);
    }

    /// <inheritdoc />
    public async Task BlockUserAsync(
        Guid userId, string blockedBy, string? reason,
        DateTime? expiresAt = null, CancellationToken ct = default)
    {
        var blockType = expiresAt.HasValue ? BlockType.Temporary : BlockType.Permanent;

        // DB'ye blok kaydı
        var block = UserBlock.Create(userId, blockType, blockedBy, reason, expiresAt);
        _db.UserBlocks.Add(block);
        await _db.SaveChangesAsync(ct);

        // Redis'e blok flag'i yaz
        var ttl = expiresAt.HasValue
            ? expiresAt.Value - DateTime.UtcNow
            : TimeSpan.FromDays(365);

        await _cache.SetAsync(CacheKeys.UserBlocked(userId), true, ttl, ct);

        // Tüm oturumları sonlandır
        await RevokeAllSessionsAsync(userId, blockedBy, ct);

        _logger.LogWarning(
            "Kullanıcı bloke edildi: UserId={UserId}, BlockedBy={BlockedBy}, Type={BlockType}, Reason={Reason}",
            userId, blockedBy, blockType, reason);
    }

    /// <inheritdoc />
    public async Task<bool> IsUserBlockedAsync(Guid userId, CancellationToken ct = default)
    {
        // Önce Redis kontrol (hızlı)
        var isBlocked = await _cache.GetAsync<bool?>(CacheKeys.UserBlocked(userId), ct);

        if (isBlocked == true)
            return true;

        // Redis'te yoksa DB kontrol
        var activeBlock = await _db.UserBlocks
            .Where(b =>
                b.UserId == userId &&
                !b.IsLifted &&
                (b.ExpiresAt == null || b.ExpiresAt > DateTime.UtcNow))
            .FirstOrDefaultAsync(ct);

        if (activeBlock is not null)
        {
            // DB'de aktif blok var — Redis'e yaz
            var ttl = activeBlock.ExpiresAt.HasValue
                ? activeBlock.ExpiresAt.Value - DateTime.UtcNow
                : TimeSpan.FromDays(365);

            await _cache.SetAsync(CacheKeys.UserBlocked(userId), true, ttl, ct);
            return true;
        }

        return false;
    }
}

/// <summary>Redis'te saklanan oturum bilgisi.</summary>
public class CachedSession
{
    public Guid SessionId { get; set; }
    public Guid UserId { get; set; }
    public string DeviceHash { get; set; } = default!;
    public string IpAddress { get; set; } = default!;
    public DateTime AccessTokenExpiresAt { get; set; }
    public DateTime RefreshTokenExpiresAt { get; set; }
}
