using CleanTenant.Domain.Common;
using CleanTenant.Domain.Enums;
using CleanTenant.Domain.Identity;

namespace CleanTenant.Domain.Security;

/// <summary>
/// Sistem geneli IP kara listesi.
/// Bu listedeki IP'lerden HİÇBİR kullanıcı login olamaz.
/// Middleware seviyesinde ilk kontrol olarak çalışır (en yüksek öncelik).
/// </summary>
public class IpBlacklist : BaseAuditableEntity
{
    private IpBlacklist() { }

    /// <summary>
    /// Kara listeye alınan IP adresi veya CIDR aralığı.
    /// Örnek: "192.168.1.100" veya "10.0.0.0/8"
    /// </summary>
    public string IpAddressOrRange { get; private set; } = default!;

    /// <summary>Kara listeye alma nedeni.</summary>
    public string? Reason { get; private set; }

    /// <summary>
    /// Kara liste süresi dolma zamanı.
    /// null ise kalıcı kara liste.
    /// </summary>
    public DateTime? ExpiresAt { get; private set; }

    /// <summary>Kara liste aktif mi?</summary>
    public bool IsActive { get; private set; } = true;

    public static IpBlacklist Create(string ipAddressOrRange, string? reason, DateTime? expiresAt = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(ipAddressOrRange, nameof(ipAddressOrRange));
        return new IpBlacklist
        {
            Id = Guid.CreateVersion7(),
            IpAddressOrRange = ipAddressOrRange.Trim(),
            Reason = reason,
            ExpiresAt = expiresAt,
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        };
    }

    public void Deactivate() => IsActive = false;
    public void Activate() => IsActive = true;
}
