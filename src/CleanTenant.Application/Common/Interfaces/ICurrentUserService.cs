

namespace CleanTenant.Application.Common.Interfaces;

/// <summary>
/// Mevcut oturumdaki kullanıcı bilgilerini sağlayan servis.
/// 
/// <para><b>DEPENDENCY INVERSION:</b></para>
/// Bu interface Application katmanında tanımlanır.
/// Infrastructure katmanı HTTP context'ten bilgileri okuyarak implemente eder.
/// Böylece Application katmanı HttpContext'i hiç bilmez.
/// </summary>
public interface ICurrentUserService
{
    /// <summary>Mevcut kullanıcının ID'si. Login değilse null.</summary>
    Guid? UserId { get; }

    /// <summary>Mevcut kullanıcının e-posta adresi.</summary>
    string? Email { get; }

    /// <summary>Mevcut kullanıcının IP adresi.</summary>
    string? IpAddress { get; }

    /// <summary>Mevcut kullanıcının tarayıcı bilgisi.</summary>
    string? UserAgent { get; }

    /// <summary>Aktif tenant ID (Context Switching header'ından).</summary>
    Guid? ActiveTenantId { get; }

    /// <summary>Aktif company ID (Context Switching header'ından).</summary>
    Guid? ActiveCompanyId { get; }

    /// <summary>Kullanıcı kimlik doğrulaması yapılmış mı?</summary>
    bool IsAuthenticated { get; }
}
