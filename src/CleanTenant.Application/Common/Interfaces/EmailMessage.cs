

namespace CleanTenant.Application.Common.Interfaces;

/// <summary>
/// E-posta mesajı — CC, BCC, çoklu dosya eki desteği.
/// 
/// <code>
/// var message = new EmailMessage
/// {
///     To = ["user@test.com", "user2@test.com"],
///     Cc = ["manager@test.com"],
///     Bcc = ["archive@test.com"],
///     Subject = "Rapor",
///     HtmlBody = "&lt;h1&gt;Merhaba&lt;/h1&gt;",
///     Attachments =
///     [
///         new EmailAttachment("rapor.pdf", pdfBytes, "application/pdf"),
///         new EmailAttachment("logo.png", logoBytes, "image/png")
///     ]
/// };
/// var emailId = await emailService.SendAsync(message);
/// </code>
/// </summary>
public class EmailMessage
{
    public List<string> To { get; set; } = [];
    public List<string> Cc { get; set; } = [];
    public List<string> Bcc { get; set; } = [];
    public string Subject { get; set; } = default!;
    public string HtmlBody { get; set; } = default!;
    public string? TextBody { get; set; }
    public List<EmailAttachment> Attachments { get; set; } = [];

    /// <summary>Gönderen adı (boşsa config'den alınır).</summary>
    public string? SenderName { get; set; }

    /// <summary>Gönderen e-posta (boşsa config'den alınır).</summary>
    public string? SenderEmail { get; set; }

    /// <summary>Hangfire job olarak mı gönderilsin?</summary>
    public bool SendInBackground { get; set; }

    /// <summary>İlişkili tenant ID (tracking için).</summary>
    public Guid? TenantId { get; set; }

    /// <summary>İlişkili kullanıcı ID (tracking için).</summary>
    public Guid? UserId { get; set; }

    /// <summary>E-posta kategorisi (tracking için).</summary>
    public string? Category { get; set; }
}
