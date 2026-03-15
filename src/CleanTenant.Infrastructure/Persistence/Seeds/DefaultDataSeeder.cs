using CleanTenant.Domain.Identity;
using CleanTenant.Domain.Security;
using CleanTenant.Infrastructure.Persistence;
using CleanTenant.Shared.Constants;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace CleanTenant.Infrastructure.Persistence.Seeds;

/// <summary>
/// Veritabanı ilk oluşturulduğunda varsayılan verileri ekler.
/// 
/// <para><b>OLUŞTURMA SIRASI (bağımlılık sırasına göre):</b></para>
/// <list type="number">
///   <item>Sistem rolleri (SuperAdmin, SystemUser)</item>
///   <item>System Default Access Policy (silinemez)</item>
///   <item>SuperAdmin kullanıcısı + Tam Erişim politikası</item>
/// </list>
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
            await context.Database.MigrateAsync();
            logger.LogInformation("Ana veritabanı migration'ları uygulandı.");

            var auditContext = scope.ServiceProvider.GetRequiredService<AuditDbContext>();
            await auditContext.Database.MigrateAsync();
            logger.LogInformation("Audit veritabanı migration'ları uygulandı.");

            // Sıra kritik — bağımlılık sırasına göre
            await SeedSystemRolesAsync(context, logger);
            await SeedSystemDefaultPolicyAsync(context, logger);
            await SeedSuperAdminAsync(context, logger);

            // Seed: Varsayılan sistem ayarları
            await Application.Features.Settings.DefaultSettingsSeeder.SeedAsync(context);
            logger.LogInformation("Varsayılan sistem ayarları oluşturuldu.");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Veritabanı seed işlemi sırasında hata oluştu.");
            throw;
        }
    }

    /// <summary>Sistem rollerini oluşturur.</summary>
    private static async Task SeedSystemRolesAsync(ApplicationDbContext context, ILogger logger)
    {
        if (!await context.SystemRoles.AnyAsync(r => r.Name == SystemRoles.SuperAdmin))
        {
            context.SystemRoles.Add(SystemRole.Create(
                SystemRoles.SuperAdmin,
                "Tüm sisteme sınırsız erişim sağlayan en üst düzey rol.",
                $"[\"{Permissions.FullAccess}\"]",
                isSystem: true));
            logger.LogInformation("SuperAdmin rolü oluşturuldu.");
        }

        if (!await context.SystemRoles.AnyAsync(r => r.Name == SystemRoles.SystemUser))
        {
            context.SystemRoles.Add(SystemRole.Create(
                SystemRoles.SystemUser,
                "Tüm tenant'larda rol bazlı yetki sağlayan sistem kullanıcısı rolü.",
                "[]",
                isSystem: true));
            logger.LogInformation("SystemUser rolü oluşturuldu.");
        }

        await context.SaveChangesAsync();
    }

    /// <summary>
    /// System seviyesi Default Access Policy oluşturur.
    /// Default = "Hiçbir IP'den, hiçbir zaman giremez" (silinemez).
    /// </summary>
    private static async Task SeedSystemDefaultPolicyAsync(ApplicationDbContext context, ILogger logger)
    {
        if (await context.AccessPolicies.AnyAsync(p => p.Level == PolicyLevel.System && p.IsDefault))
            return;

        var defaultPolicy = AccessPolicy.CreateDefault(PolicyLevel.System, createdBy: "SYSTEM");
        context.AccessPolicies.Add(defaultPolicy);
        await context.SaveChangesAsync();

        logger.LogInformation("System Default Access Policy oluşturuldu (tüm erişim reddedilir).");
    }

    /// <summary>
    /// SuperAdmin kullanıcısı + Tam Erişim politikası oluşturur.
    /// </summary>
    private static async Task SeedSuperAdminAsync(ApplicationDbContext context, ILogger logger)
    {
        const string superAdminEmail = "admin@cleantenant.com";

        if (await context.Users.AnyAsync(u => u.Email == superAdminEmail))
            return;

        // [1] SuperAdmin kullanıcısı oluştur
        var adminUser = ApplicationUser.Create(superAdminEmail, "System Administrator");
        adminUser.ConfirmEmail();
        adminUser.PasswordHash = "CHANGE_ON_FIRST_LOGIN";
        adminUser.EnableTwoFactor(Domain.Enums.TwoFactorMethod.Email);

        context.Users.Add(adminUser);
        await context.SaveChangesAsync();

        // [2] SuperAdmin rolünü ata
        var superAdminRole = await context.SystemRoles.FirstAsync(r => r.Name == SystemRoles.SuperAdmin);
        context.UserSystemRoles.Add(new UserSystemRole
        {
            Id = Guid.CreateVersion7(),
            UserId = adminUser.Id,
            SystemRoleId = superAdminRole.Id,
            AssignedBy = "SYSTEM",
            AssignedAt = DateTime.UtcNow
        });

        // [3] Tam Erişim politikası oluştur (tüm IP, tüm gün, tüm saat)
        var fullAccessPolicy = AccessPolicy.CreateFullAccess(PolicyLevel.System, createdBy: "SYSTEM");
        context.AccessPolicies.Add(fullAccessPolicy);
        await context.SaveChangesAsync();

        // [4] SuperAdmin'e tam erişim politikası ata
        context.UserPolicyAssignments.Add(new UserPolicyAssignment
        {
            Id = Guid.CreateVersion7(),
            UserId = adminUser.Id,
            AccessPolicyId = fullAccessPolicy.Id,
            AssignedBy = "SYSTEM",
            AssignedAt = DateTime.UtcNow
        });

        await context.SaveChangesAsync();

        logger.LogInformation(
            "SuperAdmin oluşturuldu: {Email} + Tam Erişim politikası atandı. İlk loginde şifre değiştirilmelidir!",
            superAdminEmail);
    }
}
