using CleanTenant.Domain.Identity;
using CleanTenant.Infrastructure.Persistence;
using CleanTenant.Shared.Constants;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace CleanTenant.Infrastructure.Persistence.Seeds;

/// <summary>
/// Veritabanı ilk oluşturulduğunda varsayılan verileri ekler.
/// 
/// <para><b>NE OLUŞTURUR?</b></para>
/// <list type="bullet">
///   <item>SuperAdmin sistem rolü (yerleşik, silinemez)</item>
///   <item>SystemUser sistem rolü (yerleşik, silinemez)</item>
///   <item>Varsayılan SuperAdmin kullanıcısı (ilk erişim için)</item>
/// </list>
/// 
/// <para><b>İDEMPOTENT:</b></para>
/// Birden fazla çalıştırılabilir — var olan verileri tekrar eklemez.
/// Her çalıştırmada "bu veri var mı?" kontrolü yapar.
/// </summary>
public static class DefaultDataSeeder
{
    public static async Task SeedAsync(IServiceProvider serviceProvider)
    {
        using var scope = serviceProvider.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var logger = scope.ServiceProvider.GetRequiredService<ILogger<ApplicationDbContext>>();

        try
        {
            // Migration'ları uygula (yoksa oluştur)
            await context.Database.MigrateAsync();
            logger.LogInformation("Ana veritabanı migration'ları uygulandı.");

            // Audit DB migration
            var auditContext = scope.ServiceProvider.GetRequiredService<AuditDbContext>();
            await auditContext.Database.MigrateAsync();
            logger.LogInformation("Audit veritabanı migration'ları uygulandı.");

            // Seed: Sistem rolleri
            await SeedSystemRolesAsync(context, logger);

            // Seed: SuperAdmin kullanıcısı
            await SeedSuperAdminAsync(context, logger);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Veritabanı seed işlemi sırasında hata oluştu.");
            throw;
        }
    }

    private static async Task SeedSystemRolesAsync(
        ApplicationDbContext context, ILogger logger)
    {
        // SuperAdmin rolü
        if (!await context.SystemRoles.AnyAsync(r => r.Name == SystemRoles.SuperAdmin))
        {
            var superAdminRole = SystemRole.Create(
                SystemRoles.SuperAdmin,
                "Tüm sisteme sınırsız erişim sağlayan en üst düzey rol.",
                $"[\"{Permissions.FullAccess}\"]",
                isSystem: true);

            context.SystemRoles.Add(superAdminRole);
            logger.LogInformation("SuperAdmin rolü oluşturuldu.");
        }

        // SystemUser rolü
        if (!await context.SystemRoles.AnyAsync(r => r.Name == SystemRoles.SystemUser))
        {
            var systemUserRole = SystemRole.Create(
                SystemRoles.SystemUser,
                "Tüm tenant'larda rol bazlı yetki sağlayan sistem kullanıcısı rolü.",
                "[]",  // İzinler admin tarafından atanacak
                isSystem: true);

            context.SystemRoles.Add(systemUserRole);
            logger.LogInformation("SystemUser rolü oluşturuldu.");
        }

        await context.SaveChangesAsync();
    }

    private static async Task SeedSuperAdminAsync(
        ApplicationDbContext context, ILogger logger)
    {
        const string superAdminEmail = "admin@cleantenant.com";

        if (await context.Users.AnyAsync(u => u.Email == superAdminEmail))
            return;

        // SuperAdmin kullanıcısı oluştur
        var adminUser = ApplicationUser.Create(superAdminEmail, "System Administrator");
        adminUser.ConfirmEmail(); // E-posta otomatik doğrulanmış

        // NOT: Şifre hash'i Infrastructure katmanında Identity servisi ile atanacak.
        // Seed'de geçici bir hash atıyoruz — ilk loginde değiştirilmesi zorunlu olacak.
        adminUser.PasswordHash = "CHANGE_ON_FIRST_LOGIN";

        context.Users.Add(adminUser);
        await context.SaveChangesAsync();

        // SuperAdmin rolünü ata
        var superAdminRole = await context.SystemRoles
            .FirstAsync(r => r.Name == SystemRoles.SuperAdmin);

        var roleAssignment = new UserSystemRole
        {
            Id = Guid.CreateVersion7(),
            UserId = adminUser.Id,
            SystemRoleId = superAdminRole.Id,
            AssignedBy = "SYSTEM",
            AssignedAt = DateTime.UtcNow
        };

        context.UserSystemRoles.Add(roleAssignment);
        await context.SaveChangesAsync();

        logger.LogInformation(
            "SuperAdmin kullanıcısı oluşturuldu: {Email}. " +
            "ÖNEMLİ: İlk loginde şifre değiştirilmelidir!",
            superAdminEmail);
    }
}
