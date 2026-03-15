namespace CleanTenant.Shared.Constants;


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
