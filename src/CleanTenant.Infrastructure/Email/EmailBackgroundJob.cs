using CleanTenant.Application.Common.Interfaces;
using CleanTenant.Domain.Email;
using Hangfire;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using System.Text.Json;

namespace CleanTenant.Infrastructure.Email;

/// <summary>
/// Hangfire arka plan görevi — e-postaları kuyruktan alıp gönderir.
/// 
/// <para><b>ÇALIŞMA AKIŞI:</b></para>
/// <code>
/// [1] SmtpEmailService.SendAsync() → EmailLog DB'ye yazılır (Queued)
/// [2] Hangfire.Enqueue(job => job.SendEmailAsync(emailLogId))
/// [3] Hangfire worker EmailLog'u DB'den okur
/// [4] SmtpEmailService.ExecuteSendAsync() çağrılır
/// [5] Başarılıysa → EmailLog.Status = Sent
///     Başarısızsa → Hangfire retry (varsayılan 3 deneme)
/// </code>
/// 
/// <para><b>RETRY POLİTİKASI:</b></para>
/// [AutomaticRetry(Attempts = 3)] — 3 başarısız deneme sonrası job "Failed" olur.
/// Hangfire Dashboard'dan (localhost:port/hangfire) tüm job'lar izlenebilir.
/// </summary>
public class EmailBackgroundJob
{
    private readonly IAuditDbContext _auditDb;
    private readonly SmtpEmailService _emailService;
    private readonly ILogger<EmailBackgroundJob> _logger;

    public EmailBackgroundJob(
        IAuditDbContext auditDb,
        SmtpEmailService emailService,
        ILogger<EmailBackgroundJob> logger)
    {
        _auditDb = auditDb;
        _emailService = emailService;
        _logger = logger;
    }

    /// <summary>
    /// EmailLog ID'si ile e-postayı gönderir.
    /// Hangfire tarafından çağrılır — serializable parametre (Guid) gerekli.
    /// </summary>
    [AutomaticRetry(Attempts = 3, DelaysInSeconds = [30, 120, 300])]
    [Queue("email")]
    public async Task SendEmailAsync(Guid emailLogId)
    {
        _logger.LogInformation("[EMAIL-JOB] İşleniyor: {EmailId}", emailLogId);

        var emailLog = await _auditDb.EmailLogs
            .FirstOrDefaultAsync(e => e.Id == emailLogId);

        if (emailLog is null)
        {
            _logger.LogWarning("[EMAIL-JOB] EmailLog bulunamadı: {Id}", emailLogId);
            return;
        }

        if (emailLog.Status == EmailStatus.Sent)
        {
            _logger.LogInformation("[EMAIL-JOB] Zaten gönderilmiş: {Id}", emailLogId);
            return;
        }

        // EmailLog'dan EmailMessage'ı yeniden oluştur
        var message = new EmailMessage
        {
            To = emailLog.To.Split(", ", StringSplitOptions.RemoveEmptyEntries).ToList(),
            Cc = emailLog.Cc?.Split(", ", StringSplitOptions.RemoveEmptyEntries).ToList() ?? [],
            Bcc = emailLog.Bcc?.Split(", ", StringSplitOptions.RemoveEmptyEntries).ToList() ?? [],
            Subject = emailLog.Subject,
            HtmlBody = emailLog.HtmlBody ?? "",
            TenantId = emailLog.TenantId,
            UserId = emailLog.UserId,
            Category = emailLog.Category
            // Not: Attachments arka planda yeniden yüklenemez
            // Attachment'lı mailler SendInBackground = false ile gönderilmeli
        };

        await _emailService.ExecuteSendAsync(emailLog, message, _auditDb);
    }
}
