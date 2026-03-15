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
