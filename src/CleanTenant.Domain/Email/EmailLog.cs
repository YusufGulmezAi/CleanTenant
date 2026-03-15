using CleanTenant.Domain.Common;

namespace CleanTenant.Domain.Email;

/// <summary>
/// E-posta gönderim kaydı — her gönderilen e-posta PostgreSQL'de izlenir.
/// 
/// <para><b>NEDEN TAKİP?</b></para>
/// <list type="bullet">
///   <item>Hangi e-posta kime, ne zaman gönderildi?</item>
///   <item>Gönderim başarılı mı, başarısız mı?</item>
///   <item>Kaç deneme yapıldı? (Hangfire retry)</item>
///   <item>Hata mesajı neydi?</item>
///   <item>Tenant/Kullanıcı bazlı raporlama</item>
/// </list>
/// </summary>
public class EmailLog : BaseEntity
{
    // ── Alıcılar ────────────────────────────────────────────────────────
    /// <summary>Ana alıcılar (virgülle ayrılmış).</summary>
    public string To { get; set; } = default!;

    /// <summary>CC alıcıları (virgülle ayrılmış). Boş olabilir.</summary>
    public string? Cc { get; set; }

    /// <summary>BCC alıcıları (virgülle ayrılmış). Boş olabilir.</summary>
    public string? Bcc { get; set; }

    // ── İçerik ──────────────────────────────────────────────────────────
    public string Subject { get; set; } = default!;
    public string? HtmlBody { get; set; }

    /// <summary>Ek dosya adları (virgülle ayrılmış).</summary>
    public string? AttachmentNames { get; set; }

    /// <summary>Toplam ek dosya boyutu (byte).</summary>
    public long AttachmentTotalSize { get; set; }

    // ── Gönderim Durumu ─────────────────────────────────────────────────
    /// <summary>Gönderim durumu: Queued, Sending, Sent, Failed, Cancelled.</summary>
    public EmailStatus Status { get; set; } = EmailStatus.Queued;

    /// <summary>Gönderim zamanı (başarılı ise).</summary>
    public DateTime? SentAt { get; set; }

    /// <summary>Hata mesajı (başarısızsa).</summary>
    public string? ErrorMessage { get; set; }

    /// <summary>Deneme sayısı (Hangfire retry).</summary>
    public int AttemptCount { get; set; }

    /// <summary>Son deneme zamanı.</summary>
    public DateTime? LastAttemptAt { get; set; }

    // ── İlişki / Takip ──────────────────────────────────────────────────
    /// <summary>İlişkili tenant (raporlama için).</summary>
    public Guid? TenantId { get; set; }

    /// <summary>Gönderen kullanıcı.</summary>
    public Guid? UserId { get; set; }

    /// <summary>Kategori: Verification, TwoFactor, PasswordReset, Welcome, Custom.</summary>
    public string? Category { get; set; }

    /// <summary>Hangfire Job ID (arka plan görevi takibi).</summary>
    public string? HangfireJobId { get; set; }

    /// <summary>Oluşturulma zamanı.</summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    // ── Factory Method ──────────────────────────────────────────────────

    public static EmailLog Create(
        string to, string subject, string? htmlBody = null,
        string? cc = null, string? bcc = null,
        string? attachmentNames = null, long attachmentSize = 0,
        Guid? tenantId = null, Guid? userId = null, string? category = null)
    {
        return new EmailLog
        {
            Id = Guid.CreateVersion7(),
            To = to,
            Cc = cc,
            Bcc = bcc,
            Subject = subject,
            HtmlBody = htmlBody,
            AttachmentNames = attachmentNames,
            AttachmentTotalSize = attachmentSize,
            Status = EmailStatus.Queued,
            TenantId = tenantId,
            UserId = userId,
            Category = category,
            CreatedAt = DateTime.UtcNow
        };
    }

    public void MarkSending()
    {
        Status = EmailStatus.Sending;
        AttemptCount++;
        LastAttemptAt = DateTime.UtcNow;
    }

    public void MarkSent()
    {
        Status = EmailStatus.Sent;
        SentAt = DateTime.UtcNow;
    }

    public void MarkFailed(string errorMessage)
    {
        Status = EmailStatus.Failed;
        ErrorMessage = errorMessage;
    }
}

public enum EmailStatus
{
    Queued = 0,
    Sending = 1,
    Sent = 2,
    Failed = 3,
    Cancelled = 4
}
