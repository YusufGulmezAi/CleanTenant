using CleanTenant.Application.Common.Interfaces;
using CleanTenant.Application.Common.Models;
using CleanTenant.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace CleanTenant.Application.Common.Rules;

/// <summary>
/// Hiyerarşik yetki kuralları.
/// 
/// <para><b>HİYERARŞİK YETKİ MODELİ:</b></para>
/// CleanTenant'ta üst seviye, alt seviyenin tüm yetkilerine otomatik sahiptir.
/// Hiçbir alt seviye, üst seviyeye müdahale edemez.
/// 
/// <code>
/// SuperAdmin (100)  → Tüm sisteme tam yetki
///   SystemUser (80) → Tüm tenant'larda rol bazlı yetki
///     TenantAdmin (60)  → Kendi tenant'ında tam yetki
///       TenantUser (40) → Alt şirketlerde yetkili
///         CompanyAdmin (20) → Kendi şirketinde tam yetki
///           CompanyUser (10)  → Rol bazlı yetki
///             CompanyMember (5) → Sınırlı erişim
/// </code>
/// 
/// <para><b>KORUNMA KURALI:</b></para>
/// Bir kullanıcı kendi seviyesindeki veya üstündeki kullanıcıya müdahale edemez.
/// Örnek: TenantUser, TenantAdmin'i bloke edemez veya rolünü değiştiremez.
/// </summary>
public class AuthorizationRules
{
    private readonly IApplicationDbContext _db;
    private readonly ICurrentUserService _currentUser;
    private readonly ICacheService _cache;

    public AuthorizationRules(
        IApplicationDbContext db,
        ICurrentUserService currentUser,
        ICacheService cache)
    {
        _db = db;
        _currentUser = currentUser;
        _cache = cache;
    }

    /// <summary>
    /// Mevcut kullanıcının belirli bir izne sahip olup olmadığını kontrol eder.
    /// Önce Redis cache'e bakar, yoksa veritabanından hesaplar ve cache'e yazar.
    /// </summary>
    /// <param name="permission">Kontrol edilecek izin (örn: "tenants.create")</param>
    public async Task<Result<bool>> EnsureHasPermissionAsync(string permission, CancellationToken ct)
    {
        if (!_currentUser.IsAuthenticated || _currentUser.UserId is null)
            return Result<bool>.Unauthorized();

        var userId = _currentUser.UserId.Value;

        // SuperAdmin her zaman tam yetki
        if (await IsSuperAdminAsync(userId, ct))
            return Result<bool>.Success(true);

        // Cache'ten izinleri al
        var cacheKey = Shared.Constants.CacheKeys.UserPermissions(userId);
        var cachedPermissions = await _cache.GetAsync<List<string>>(cacheKey, ct);

        if (cachedPermissions is null)
        {
            // Cache'te yoksa hesapla ve yaz
            cachedPermissions = await CalculateUserPermissionsAsync(userId, ct);
            await _cache.SetAsync(cacheKey, cachedPermissions, TimeSpan.FromMinutes(30), ct);
        }

        // İzin kontrolü: Tam eşleşme veya wildcard
        var hasPermission = cachedPermissions.Any(p =>
            p == permission ||                          // Tam eşleşme: "tenants.create"
            p == Shared.Constants.Permissions.FullAccess || // Tam yetki: "*.*"
            IsWildcardMatch(p, permission));             // Modül wildcard: "tenants.*"

        if (!hasPermission)
            return Result<bool>.Forbidden($"Bu işlem için '{permission}' izni gereklidir.");

        return Result<bool>.Success(true);
    }

    /// <summary>
    /// Mevcut kullanıcının hedef kullanıcıya müdahale edip edemeyeceğini kontrol eder.
    /// Kural: Alt seviye, üst seviyeye müdahale edemez.
    /// </summary>
    public async Task<Result<bool>> EnsureCanManageUserAsync(Guid targetUserId, CancellationToken ct)
    {
        if (!_currentUser.IsAuthenticated || _currentUser.UserId is null)
            return Result<bool>.Unauthorized();

        var currentUserId = _currentUser.UserId.Value;

        // Kendine müdahale kontrolü (bazı işlemler kısıtlı olabilir)
        if (currentUserId == targetUserId)
            return Result<bool>.Success(true);

        var currentLevel = await GetUserLevelAsync(currentUserId, ct);
        var targetLevel = await GetUserLevelAsync(targetUserId, ct);

        if (currentLevel <= targetLevel)
            return Result<bool>.Forbidden(
                "Bu kullanıcıya müdahale etme yetkiniz yoktur. " +
                "Yalnızca sizden alt seviyedeki kullanıcıları yönetebilirsiniz.");

        return Result<bool>.Success(true);
    }

    /// <summary>
    /// Mevcut kullanıcının aktif tenant bağlamında yetkili olup olmadığını kontrol eder.
    /// Context Switching header'ındaki TenantId ile karşılaştırılır.
    /// </summary>
    public async Task<Result<bool>> EnsureHasTenantAccessAsync(Guid tenantId, CancellationToken ct)
    {
        if (!_currentUser.IsAuthenticated || _currentUser.UserId is null)
            return Result<bool>.Unauthorized();

        var userId = _currentUser.UserId.Value;

        // SuperAdmin ve SystemUser tüm tenant'lara erişebilir
        if (await IsSuperAdminAsync(userId, ct) || await IsSystemUserAsync(userId, ct))
            return Result<bool>.Success(true);

        // Tenant bazlı rol kontrolü
        var hasTenantRole = await _db.UserTenantRoles
            .AnyAsync(utr => utr.UserId == userId && utr.TenantId == tenantId, ct);

        if (!hasTenantRole)
            return Result<bool>.Forbidden("Bu tenant'a erişim yetkiniz bulunmamaktadır.");

        return Result<bool>.Success(true);
    }

    /// <summary>
    /// Mevcut kullanıcının aktif şirket bağlamında yetkili olup olmadığını kontrol eder.
    /// </summary>
    public async Task<Result<bool>> EnsureHasCompanyAccessAsync(
        Guid tenantId, Guid companyId, CancellationToken ct)
    {
        if (!_currentUser.IsAuthenticated || _currentUser.UserId is null)
            return Result<bool>.Unauthorized();

        var userId = _currentUser.UserId.Value;

        // SuperAdmin, SystemUser tüm şirketlere erişir
        if (await IsSuperAdminAsync(userId, ct) || await IsSystemUserAsync(userId, ct))
            return Result<bool>.Success(true);

        // TenantAdmin ve TenantUser kendi tenant'ının tüm şirketlerine erişir
        var hasTenantRole = await _db.UserTenantRoles
            .AnyAsync(utr => utr.UserId == userId && utr.TenantId == tenantId, ct);
        if (hasTenantRole)
            return Result<bool>.Success(true);

        // Şirket bazlı rol veya üyelik kontrolü
        var hasCompanyRole = await _db.UserCompanyRoles
            .AnyAsync(ucr => ucr.UserId == userId && ucr.CompanyId == companyId, ct);

        var hasCompanyMembership = await _db.UserCompanyMemberships
            .AnyAsync(ucm => ucm.UserId == userId && ucm.CompanyId == companyId && ucm.IsActive, ct);

        if (!hasCompanyRole && !hasCompanyMembership)
            return Result<bool>.Forbidden("Bu şirkete erişim yetkiniz bulunmamaktadır.");

        return Result<bool>.Success(true);
    }

    // ========================================================================
    // YARDIMCI METODLAR (private)
    // ========================================================================

    private async Task<bool> IsSuperAdminAsync(Guid userId, CancellationToken ct)
    {
        return await _db.UserSystemRoles
            .AnyAsync(usr =>
                usr.UserId == userId &&
                usr.SystemRole.Name == Shared.Constants.SystemRoles.SuperAdmin,
                ct);
    }

    private async Task<bool> IsSystemUserAsync(Guid userId, CancellationToken ct)
    {
        return await _db.UserSystemRoles
            .AnyAsync(usr => usr.UserId == userId, ct);
    }

    /// <summary>
    /// Kullanıcının en yüksek hiyerarşi seviyesini hesaplar.
    /// Birden fazla rolü olabilir — en yüksek seviye geçerlidir.
    /// </summary>
    private async Task<UserLevel> GetUserLevelAsync(Guid userId, CancellationToken ct)
    {
        // SuperAdmin kontrolü
        var isSuperAdmin = await _db.UserSystemRoles
            .AnyAsync(usr =>
                usr.UserId == userId &&
                usr.SystemRole.Name == Shared.Constants.SystemRoles.SuperAdmin, ct);
        if (isSuperAdmin) return UserLevel.SuperAdmin;

        // System User kontrolü
        var isSystemUser = await _db.UserSystemRoles.AnyAsync(usr => usr.UserId == userId, ct);
        if (isSystemUser) return UserLevel.SystemUser;

        // Tenant kontrolü — en azından TenantUser
        var hasTenantRole = await _db.UserTenantRoles.AnyAsync(utr => utr.UserId == userId, ct);
        if (hasTenantRole) return UserLevel.TenantUser;  // Detaylı Admin/User ayrımı role bazlı yapılacak

        // Company kontrolü
        var hasCompanyRole = await _db.UserCompanyRoles.AnyAsync(ucr => ucr.UserId == userId, ct);
        if (hasCompanyRole) return UserLevel.CompanyUser;

        // Üyelik kontrolü
        var hasMembership = await _db.UserCompanyMemberships
            .AnyAsync(ucm => ucm.UserId == userId && ucm.IsActive, ct);
        if (hasMembership) return UserLevel.CompanyMember;

        return UserLevel.CompanyMember; // En düşük seviye
    }

    /// <summary>
    /// Kullanıcının tüm rollerinden toplam izin listesini hesaplar.
    /// Sistem + Tenant + Company rollerinin izinlerini birleştirir.
    /// </summary>
    private async Task<List<string>> CalculateUserPermissionsAsync(Guid userId, CancellationToken ct)
    {
        var permissions = new HashSet<string>();

        // Sistem rolleri izinleri
        var systemPermissions = await _db.UserSystemRoles
            .Where(usr => usr.UserId == userId && usr.SystemRole.IsActive)
            .Select(usr => usr.SystemRole.Permissions)
            .ToListAsync(ct);

        // Tenant rolleri izinleri (aktif tenant bağlamında)
        var tenantPermissions = await _db.UserTenantRoles
            .Where(utr => utr.UserId == userId && utr.TenantRole.IsActive)
            .Select(utr => utr.TenantRole.Permissions)
            .ToListAsync(ct);

        // Company rolleri izinleri (aktif company bağlamında)
        var companyPermissions = await _db.UserCompanyRoles
            .Where(ucr => ucr.UserId == userId && ucr.CompanyRole.IsActive)
            .Select(ucr => ucr.CompanyRole.Permissions)
            .ToListAsync(ct);

        // JSON string'lerini parse edip birleştir
        foreach (var json in systemPermissions.Concat(tenantPermissions).Concat(companyPermissions))
        {
            if (string.IsNullOrEmpty(json)) continue;
            try
            {
                var parsed = System.Text.Json.JsonSerializer.Deserialize<List<string>>(json);
                if (parsed is not null)
                    foreach (var p in parsed)
                        permissions.Add(p);
            }
            catch { /* Geçersiz JSON — loglanabilir */ }
        }

        return [.. permissions];
    }

    /// <summary>
    /// Wildcard eşleştirme: "tenants.*" izni "tenants.create", "tenants.read" vb. ile eşleşir.
    /// </summary>
    private static bool IsWildcardMatch(string pattern, string permission)
    {
        if (!pattern.EndsWith(".*")) return false;

        var patternModule = pattern[..^2]; // "tenants.*" → "tenants"
        var permissionModule = permission.Split('.')[0]; // "tenants.create" → "tenants"

        return patternModule == permissionModule;
    }
}
