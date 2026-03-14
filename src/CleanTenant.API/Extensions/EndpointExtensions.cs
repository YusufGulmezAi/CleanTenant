using CleanTenant.API.Endpoints;

namespace CleanTenant.API.Extensions;

/// <summary>
/// Tüm Minimal API endpoint gruplarını kaydeden extension method.
/// Program.cs'de <c>app.MapCleanTenantEndpoints()</c> ile çağrılır.
/// 
/// Yeni bir endpoint grubu eklerken:
/// 1. Endpoints/ klasörüne XxxEndpoints.cs oluştur
/// 2. MapXxxEndpoints() extension method'u tanımla
/// 3. Bu dosyaya ekle
/// </summary>
public static class EndpointExtensions
{
    public static WebApplication MapCleanTenantEndpoints(this WebApplication app)
    {
        // Kimlik doğrulama: /api/auth
        app.MapAuthEndpoints();

        // Tenant yönetimi: /api/tenants
        app.MapTenantEndpoints();

        // Şirket yönetimi: /api/companies
        app.MapCompanyEndpoints();

        // Kullanıcı yönetimi: /api/users
        app.MapUserEndpoints();

        // Rol yönetimi: /api/roles
        app.MapRoleEndpoints();

        // Oturum izleme: /api/sessions
        app.MapSessionEndpoints();

        // TODO: Gelecek fazlar:
        // app.MapAuditEndpoints();     → /api/audit (audit log sorgulama)
        // app.MapBackupEndpoints();    → /api/backups (şirket bazlı yedekleme)

        return app;
    }
}
