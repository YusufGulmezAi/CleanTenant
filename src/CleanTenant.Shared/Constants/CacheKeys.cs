namespace CleanTenant.Shared.Constants;


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
