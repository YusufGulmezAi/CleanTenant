namespace CleanTenant.Shared.Constants;

/// <summary>
/// Sistem genelinde kullanılan sabit rol adları.
/// 
/// <para><b>NEDEN SABİT SINIF?</b></para>
/// Rol adları kodun birçok yerinde referans edilir (attribute'lar,
/// if kontrolleri, seed data). Sihirli string (magic string) kullanmak
/// yazım hatalarına ve tutarsızlıklara yol açar. Sabit sınıf ile
/// derleme zamanında hata yakalanır.
/// 
/// <code>
/// // ❌ KÖTÜ — yazım hatası runtime'da fark edilir
/// if (role == "SuperAdmn") { ... }
/// 
/// // ✅ İYİ — derleme zamanında hata verir
/// if (role == SystemRoles.SuperAdmin) { ... }
/// </code>
/// </summary>
public static class SystemRoles
{
    public const string SuperAdmin = "SuperAdmin";
    public const string SystemUser = "SystemUser";
}

/// <summary>
/// Modül bazlı izin tanımları.
/// "module.action" formatında tanımlanır.
/// 
/// <para><b>İZİN FORMATI:</b></para>
/// <code>
/// "{modül}.{aksiyon}"
/// Örnekler:
///   "tenants.create"    → Tenant oluşturma
///   "tenants.read"      → Tenant listeleme/görüntüleme
///   "users.block"       → Kullanıcı bloklama
///   "audit.read"        → Audit log okuma
/// </code>
/// 
/// <para><b>JOKER İZİN:</b></para>
/// "{modül}.*" → O modüldeki TÜM aksiyonlara izin verir.
/// "*.*" → TÜM modüllerde TÜM aksiyonlara izin verir (SuperAdmin).
/// </para>
/// </summary>
public static class Permissions
{
    // ========================================================================
    // Tenant Yönetimi
    // ========================================================================
    public static class Tenants
    {
        public const string Create = "tenants.create";
        public const string Read = "tenants.read";
        public const string Update = "tenants.update";
        public const string Delete = "tenants.delete";
        public const string ManageSettings = "tenants.settings";
        public const string All = "tenants.*";
    }

    // ========================================================================
    // Şirket Yönetimi
    // ========================================================================
    public static class Companies
    {
        public const string Create = "companies.create";
        public const string Read = "companies.read";
        public const string Update = "companies.update";
        public const string Delete = "companies.delete";
        public const string ManageSettings = "companies.settings";
        public const string Backup = "companies.backup";
        public const string All = "companies.*";
    }

    // ========================================================================
    // Kullanıcı Yönetimi
    // ========================================================================
    public static class Users
    {
        public const string Create = "users.create";
        public const string Read = "users.read";
        public const string Update = "users.update";
        public const string Delete = "users.delete";
        public const string Block = "users.block";
        public const string ForceLogout = "users.forcelogout";
        public const string ManageRoles = "users.roles";
        public const string All = "users.*";
    }

    // ========================================================================
    // Rol ve İzin Yönetimi
    // ========================================================================
    public static class Roles
    {
        public const string Create = "roles.create";
        public const string Read = "roles.read";
        public const string Update = "roles.update";
        public const string Delete = "roles.delete";
        public const string Assign = "roles.assign";
        public const string All = "roles.*";
    }

    // ========================================================================
    // Oturum Yönetimi
    // ========================================================================
    public static class Sessions
    {
        public const string Read = "sessions.read";
        public const string Revoke = "sessions.revoke";
        public const string All = "sessions.*";
    }

    // ========================================================================
    // Audit ve Log
    // ========================================================================
    public static class Audit
    {
        public const string Read = "audit.read";
        public const string Export = "audit.export";
        public const string All = "audit.*";
    }

    // ========================================================================
    // Güvenlik Yönetimi
    // ========================================================================
    public static class Security
    {
        public const string ManageIpBlacklist = "security.blacklist";
        public const string ManageAccessPolicies = "security.policies";
        public const string ManageRateLimit = "security.ratelimit";
        public const string All = "security.*";
    }

    /// <summary>Tüm izinler — sadece SuperAdmin.</summary>
    public const string FullAccess = "*.*";
}

/// <summary>
/// Redis cache key şablonları.
/// Tüm cache key'leri merkezi olarak yönetilir.
/// 
/// <para><b>KEY FORMATI:</b></para>
/// <code>
/// "ct:{kategori}:{id}"  (ct = CleanTenant prefix)
/// Örnekler:
///   "ct:session:abc123"        → Kullanıcı oturum bilgisi
///   "ct:user:abc123:roles"     → Kullanıcının tüm rolleri
///   "ct:blacklist:ips"         → IP kara listesi seti
/// </code>
/// 
/// <para><b>NEDEN PREFIX?</b></para>
/// Redis'te birden fazla uygulama aynı instance'ı paylaşabilir.
/// "ct:" prefix'i CleanTenant'ın key'lerini diğer uygulamalardan ayırır.
/// </para>
/// </summary>
public static class CacheKeys
{
    private const string Prefix = "ct";

    // Oturum
    public static string Session(Guid userId) => $"{Prefix}:session:{userId}";
    public static string SessionDevice(Guid userId) => $"{Prefix}:session:{userId}:device";

    // Kullanıcı rolleri ve izinleri
    public static string UserRoles(Guid userId) => $"{Prefix}:user:{userId}:roles";
    public static string UserPermissions(Guid userId) => $"{Prefix}:user:{userId}:permissions";
    public static string UserInfo(Guid userId) => $"{Prefix}:user:{userId}:info";

    // Kullanıcı bloklama
    public static string UserBlocked(Guid userId) => $"{Prefix}:user:{userId}:blocked";

    // IP Kara Listesi (Redis SET tipi — O(1) lookup)
    public static string IpBlacklist => $"{Prefix}:blacklist:ips";

    // Rate Limiting (Redis sliding window)
    public static string RateLimit(string endpoint, string clientId) =>
        $"{Prefix}:ratelimit:{endpoint}:{clientId}";

    // Tenant ayarları
    public static string TenantSettings(Guid tenantId) => $"{Prefix}:tenant:{tenantId}:settings";

    // Şirket ayarları
    public static string CompanySettings(Guid companyId) => $"{Prefix}:company:{companyId}:settings";
}
