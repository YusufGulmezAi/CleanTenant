using CleanTenant.Domain.Common;
using CleanTenant.Domain.Enums;
using CleanTenant.Domain.Identity;

namespace CleanTenant.Domain.Security;

/// <summary>
/// Kullanıcı erişim politikası.
/// IP whitelist ve zaman kısıtlamaları burada tanımlanır.
/// Her kullanıcı için ayrı ayrı konfigüre edilebilir.
/// 
/// <para><b>PARAMETRİK YAPILANDIRMA:</b></para>
/// Bu özellikler appsettings.json'dan global olarak aktif/pasif edilir.
/// Aktif edildiğinde kullanıcı bazında detay tanımlanır.
/// </summary>
public class UserAccessPolicy : BaseAuditableEntity
{
    private UserAccessPolicy() { }

    /// <summary>Politika sahibi kullanıcı.</summary>
    public Guid UserId { get; private set; }

    /// <summary>Politika aktif mi?</summary>
    public bool IsEnabled { get; private set; } = true;

    /// <summary>
    /// İzin verilen IP adresleri veya aralıkları (JSONB).
    /// Boş ise tüm IP'lerden erişim serbest.
    /// 
    /// Örnek JSON:
    /// <code>
    /// ["192.168.1.0/24", "10.0.0.1", "203.0.113.50"]
    /// </code>
    /// 
    /// CIDR notasyonu desteklenir (192.168.1.0/24 = 192.168.1.0 - 192.168.1.255)
    /// </summary>
    public string AllowedIpRanges { get; private set; } = "[]";

    /// <summary>
    /// İzin verilen günler (JSONB).
    /// Haftanın hangi günlerinde login olunabilir?
    /// Boş ise her gün izinli.
    /// 
    /// Örnek JSON:
    /// <code>
    /// [1, 2, 3, 4, 5]  // Pazartesi-Cuma (DayOfWeek: 1=Monday)
    /// </code>
    /// </summary>
    public string AllowedDays { get; private set; } = "[]";

    /// <summary>
    /// İzin verilen saat başlangıcı.
    /// Örnek: 08:30 → saat 08:30'dan önce login yapılamaz.
    /// null ise saat kısıtlaması yok.
    /// </summary>
    public TimeOnly? AllowedTimeStart { get; private set; }

    /// <summary>
    /// İzin verilen saat bitişi.
    /// Örnek: 18:00 → saat 18:00'den sonra login yapılamaz.
    /// null ise saat kısıtlaması yok.
    /// </summary>
    public TimeOnly? AllowedTimeEnd { get; private set; }

    // Navigation
    public ApplicationUser User { get; private set; } = default!;

    public static UserAccessPolicy Create(Guid userId)
    {
        return new UserAccessPolicy
        {
            Id = Guid.CreateVersion7(),
            UserId = userId,
            IsEnabled = true,
            CreatedAt = DateTime.UtcNow
        };
    }

    public void UpdateIpRanges(string allowedIpRangesJson)
    {
        AllowedIpRanges = allowedIpRangesJson;
    }

    public void UpdateTimeRestrictions(string allowedDaysJson, TimeOnly? start, TimeOnly? end)
    {
        AllowedDays = allowedDaysJson;
        AllowedTimeStart = start;
        AllowedTimeEnd = end;
    }
}
