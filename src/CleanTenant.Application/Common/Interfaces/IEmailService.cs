

namespace CleanTenant.Application.Common.Interfaces;

/// <summary>
/// E-posta gönderici servis sözleşmesi.
/// CC, BCC, çoklu dosya eki, Hangfire background job desteği.
/// </summary>
public interface IEmailService
{
    /// <summary>Basit e-posta gönderir.</summary>
    Task<Guid> SendAsync(string to, string subject, string htmlBody, CancellationToken ct = default);

    /// <summary>Gelişmiş e-posta gönderir (CC, BCC, ekler).</summary>
    Task<Guid> SendAsync(EmailMessage message, CancellationToken ct = default);

    /// <summary>Hangfire ile arka planda gönderir — anında dönüş yapar.</summary>
    Guid EnqueueAsync(EmailMessage message);

    /// <summary>2FA doğrulama kodu gönderir.</summary>
    Task<Guid> SendTwoFactorCodeAsync(string to, string code, CancellationToken ct = default);

    /// <summary>Şifre sıfırlama linki gönderir.</summary>
    Task<Guid> SendPasswordResetAsync(string to, string resetLink, CancellationToken ct = default);

    /// <summary>E-posta doğrulama kodu gönderir.</summary>
    Task<Guid> SendEmailVerificationCodeAsync(string to, string code, CancellationToken ct = default);

    /// <summary>Hoş geldiniz e-postası gönderir.</summary>
    Task<Guid> SendWelcomeAsync(string to, string fullName, string tempPassword, CancellationToken ct = default);
}
