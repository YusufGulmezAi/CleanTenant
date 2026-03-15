using Hangfire;
using CleanTenant.Application.Common.Interfaces;
using CleanTenant.Domain.Email;
using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using MimeKit;

namespace CleanTenant.Infrastructure.Email;

/// <summary>
/// MailKit tabanlı SMTP e-posta servisi.
/// CC, BCC, çoklu dosya eki, PostgreSQL tracking, Hangfire arka plan desteği.
/// </summary>
public class SmtpEmailService : IEmailService
{
    private readonly IConfiguration _configuration;
    private readonly ILogger<SmtpEmailService> _logger;
    private readonly IServiceProvider _serviceProvider;

    private readonly string _smtpHost;
    private readonly int _smtpPort;
    private readonly string _smtpUser;
    private readonly string _smtpPassword;
    private readonly string _senderName;
    private readonly string _senderEmail;
    private readonly bool _useSsl;
    private readonly bool _isEnabled;

    public SmtpEmailService(
        IConfiguration configuration,
        ILogger<SmtpEmailService> logger,
        IServiceProvider serviceProvider)
    {
        _configuration = configuration;
        _logger = logger;
        _serviceProvider = serviceProvider;

		var smtp = configuration.GetSection("EmailSettings");
		_isEnabled = bool.Parse(smtp["Enabled"] ?? "true");
		_smtpHost = smtp["Host"] ?? "smtp.gmail.com";
		_smtpPort = int.Parse(smtp["Port"] ?? "587");
		_smtpUser = smtp["Username"] ?? "";
		_smtpPassword = smtp["Password"] ?? "";
		_senderName = smtp["FromName"] ?? "CleanTenant";
		_senderEmail = smtp["FromAddress"] ?? _smtpUser;
		_useSsl = bool.Parse(smtp["UseSsl"] ?? "true");
	}

    // ====================================================================
    // BASIT GÖNDERIM
    // ====================================================================

    public async Task<Guid> SendAsync(string to, string subject, string htmlBody, CancellationToken ct = default)
    {
        return await SendAsync(new EmailMessage
        {
            To = [to],
            Subject = subject,
            HtmlBody = htmlBody,
            Category = "General"
        }, ct);
    }

    // ====================================================================
    // GELİŞMİŞ GÖNDERIM — CC, BCC, Ekler, Tracking
    // ====================================================================

    public async Task<Guid> SendAsync(EmailMessage message, CancellationToken ct = default)
    {
        // EmailLog oluştur (PostgreSQL tracking)
        var emailLog = CreateEmailLog(message);

        using var scope = _serviceProvider.CreateScope();
        var auditDb = scope.ServiceProvider.GetRequiredService<IAuditDbContext>();
        auditDb.EmailLogs.Add(emailLog);
        await auditDb.SaveChangesAsync(ct);

        // Arka plan gönderimi istenmişse Hangfire'a ver
        if (message.SendInBackground)
        {
            var jobClient = scope.ServiceProvider.GetService<Hangfire.IBackgroundJobClient>();
            if (jobClient is not null)
            {
				var jobId = Hangfire.BackgroundJob.Enqueue<EmailBackgroundJob>(
                    job => job.SendEmailAsync(emailLog.Id));
				emailLog.HangfireJobId = jobId;
                await auditDb.SaveChangesAsync(ct);
                _logger.LogInformation("[EMAIL] Arka plana eklendi: {Id}, JobId: {JobId}", emailLog.Id, jobId);
            }
            else
            {
                // Hangfire yoksa senkron gönder
                await ExecuteSendAsync(emailLog, message, auditDb, ct);
            }
            return emailLog.Id;
        }

        // Senkron gönderim
        await ExecuteSendAsync(emailLog, message, auditDb, ct);
        return emailLog.Id;
    }

    public Guid EnqueueAsync(EmailMessage message)
    {
        message.SendInBackground = true;
        return SendAsync(message).GetAwaiter().GetResult();
    }

    // ====================================================================
    // ŞABLON E-POSTALAR
    // ====================================================================

    public async Task<Guid> SendTwoFactorCodeAsync(string to, string code, CancellationToken ct = default)
    {
        return await SendAsync(new EmailMessage
        {
            To = [to],
            Subject = $"CleanTenant — Doğrulama Kodunuz: {code}",
            HtmlBody = $@"
                <h2>İki Faktörlü Doğrulama</h2>
                <p>Giriş yapmak için aşağıdaki kodu kullanınız:</p>
                <div style='text-align:center; margin:30px 0;'>
                    <span style='font-size:36px; font-weight:bold; letter-spacing:8px; 
                                 background:#f0f0f0; padding:15px 30px; border-radius:8px;
                                 font-family:monospace; color:#333;'>{code}</span>
                </div>
                <p style='color:#666;'>Bu kod <strong>5 dakika</strong> geçerlidir.</p>",
            Category = "TwoFactor"
        }, ct);
    }

    public async Task<Guid> SendPasswordResetAsync(string to, string resetLink, CancellationToken ct = default)
    {
        return await SendAsync(new EmailMessage
        {
            To = [to],
            Subject = "CleanTenant — Şifre Sıfırlama",
            HtmlBody = $@"
                <h2>Şifre Sıfırlama</h2>
                <p>Şifrenizi sıfırlamak için butona tıklayınız:</p>
                <div style='text-align:center; margin:30px 0;'>
                    <a href='{resetLink}' style='background:#4CAF50; color:white; padding:14px 28px; 
                       text-decoration:none; border-radius:6px; font-size:16px; display:inline-block;'>Şifremi Sıfırla</a>
                </div>
                <p style='color:#666;'>Bu link <strong>15 dakika</strong> geçerlidir.</p>",
            Category = "PasswordReset"
        }, ct);
    }

    public async Task<Guid> SendEmailVerificationCodeAsync(string to, string code, CancellationToken ct = default)
    {
        return await SendAsync(new EmailMessage
        {
            To = [to],
            Subject = $"CleanTenant — E-posta Doğrulama: {code}",
            HtmlBody = $@"
                <h2>E-posta Doğrulama</h2>
                <p>Doğrulama kodunuz:</p>
                <div style='text-align:center; margin:30px 0;'>
                    <span style='font-size:36px; font-weight:bold; letter-spacing:8px; 
                                 background:#e3f2fd; padding:15px 30px; border-radius:8px;
                                 font-family:monospace; color:#1565C0;'>{code}</span>
                </div>
                <p style='color:#666;'>Bu kod <strong>5 dakika</strong> geçerlidir.</p>",
            Category = "Verification"
        }, ct);
    }

    public async Task<Guid> SendWelcomeAsync(string to, string fullName, string tempPassword, CancellationToken ct = default)
    {
        return await SendAsync(new EmailMessage
        {
            To = [to],
            Subject = "CleanTenant — Hesabınız Oluşturuldu",
            HtmlBody = $@"
                <h2>Hoş Geldiniz, {fullName}!</h2>
                <p>CleanTenant platformunda hesabınız oluşturulmuştur.</p>
                <div style='background:#f5f5f5; padding:20px; border-radius:8px; margin:20px 0;'>
                    <p><strong>E-posta:</strong> {to}</p>
                    <p><strong>Geçici Şifre:</strong> <code style='font-size:16px;'>{tempPassword}</code></p>
                </div>
                <p style='color:#d32f2f;'><strong>Önemli:</strong> İlk girişte şifrenizi değiştirmeniz gerekmektedir.</p>",
            Category = "Welcome"
        }, ct);
    }

    // ====================================================================
    // SMTP GÖNDERIM MOTORU (Hangfire job'ından da çağrılır)
    // ====================================================================

    public async Task ExecuteSendAsync(
        EmailLog emailLog, EmailMessage message, IAuditDbContext auditDb, CancellationToken ct = default)
    {
        emailLog.MarkSending();
        await auditDb.SaveChangesAsync(ct);

        if (!_isEnabled)
        {
            _logger.LogWarning("[EMAIL] SMTP devre dışı. Id: {Id}, To: {To}", emailLog.Id, emailLog.To);
            emailLog.MarkFailed("SMTP devre dışı (Email:Enabled = false)");
            await auditDb.SaveChangesAsync(ct);
            return;
        }

        try
        {
            var mimeMessage = BuildMimeMessage(message);

            using var smtp = new SmtpClient();
            var secureSocket = _smtpPort switch
            {
                465 => SecureSocketOptions.SslOnConnect,
                587 => SecureSocketOptions.StartTls,
                _ => _useSsl ? SecureSocketOptions.Auto : SecureSocketOptions.None
            };

            await smtp.ConnectAsync(_smtpHost, _smtpPort, secureSocket, ct);

            if (!string.IsNullOrEmpty(_smtpUser) && !string.IsNullOrEmpty(_smtpPassword))
                await smtp.AuthenticateAsync(_smtpUser, _smtpPassword, ct);

            await smtp.SendAsync(mimeMessage, ct);
            await smtp.DisconnectAsync(true, ct);

            emailLog.MarkSent();
            await auditDb.SaveChangesAsync(ct);

            _logger.LogInformation("[EMAIL] Gönderildi: {Id}, To: {To}", emailLog.Id, emailLog.To);
        }
        catch (Exception ex)
        {
            emailLog.MarkFailed(ex.Message);
            await auditDb.SaveChangesAsync(ct);
            _logger.LogError(ex, "[EMAIL] Gönderilemedi: {Id}, To: {To}", emailLog.Id, emailLog.To);
            throw; // Hangfire retry yapabilsin
        }
    }

    // ====================================================================
    // MIME MESSAGE OLUŞTURMA (CC, BCC, Ekler)
    // ====================================================================

    private MimeMessage BuildMimeMessage(EmailMessage message)
    {
        var mimeMessage = new MimeMessage();

        mimeMessage.From.Add(new MailboxAddress(
            message.SenderName ?? _senderName,
            message.SenderEmail ?? _senderEmail));

        foreach (var to in message.To)
            mimeMessage.To.Add(MailboxAddress.Parse(to));

        foreach (var cc in message.Cc)
            mimeMessage.Cc.Add(MailboxAddress.Parse(cc));

        foreach (var bcc in message.Bcc)
            mimeMessage.Bcc.Add(MailboxAddress.Parse(bcc));

        mimeMessage.Subject = message.Subject;

        var builder = new BodyBuilder
        {
            HtmlBody = WrapInTemplate(message.HtmlBody, message.Subject),
            TextBody = message.TextBody ?? StripHtml(message.HtmlBody)
        };

        // Çoklu dosya eki
        foreach (var attachment in message.Attachments)
        {
            builder.Attachments.Add(
                attachment.FileName,
                attachment.Content,
                ContentType.Parse(attachment.ContentType));
        }

        mimeMessage.Body = builder.ToMessageBody();
        return mimeMessage;
    }

    // ====================================================================
    // YARDIMCI
    // ====================================================================

    private static EmailLog CreateEmailLog(EmailMessage message)
    {
        return EmailLog.Create(
            to: string.Join(", ", message.To),
            subject: message.Subject,
            htmlBody: message.HtmlBody,
            cc: message.Cc.Count > 0 ? string.Join(", ", message.Cc) : null,
            bcc: message.Bcc.Count > 0 ? string.Join(", ", message.Bcc) : null,
            attachmentNames: message.Attachments.Count > 0
                ? string.Join(", ", message.Attachments.Select(a => a.FileName)) : null,
            attachmentSize: message.Attachments.Sum(a => (long)a.Content.Length),
            tenantId: message.TenantId,
            userId: message.UserId,
            category: message.Category);
    }

    private static string WrapInTemplate(string bodyContent, string title) => $@"
<!DOCTYPE html><html><head><meta charset='utf-8'></head>
<body style='margin:0; padding:0; font-family:-apple-system,BlinkMacSystemFont,""Segoe UI"",Roboto,Arial,sans-serif; background:#f5f5f5;'>
<table width='100%' cellpadding='0' cellspacing='0' style='background:#f5f5f5; padding:40px 0;'>
<tr><td align='center'>
<table width='600' cellpadding='0' cellspacing='0' style='background:#fff; border-radius:12px; overflow:hidden; box-shadow:0 2px 8px rgba(0,0,0,0.1);'>
<tr><td style='background:linear-gradient(135deg,#1a237e,#283593); padding:30px; text-align:center;'>
<h1 style='color:#fff; margin:0; font-size:24px;'>🏗️ CleanTenant</h1></td></tr>
<tr><td style='padding:40px 30px;'>{bodyContent}</td></tr>
<tr><td style='background:#fafafa; padding:20px 30px; text-align:center; border-top:1px solid #eee;'>
<p style='color:#999; font-size:12px; margin:0;'>Bu e-posta CleanTenant tarafından otomatik gönderilmiştir.</p>
</td></tr></table></td></tr></table></body></html>";

    private static string StripHtml(string html) =>
        System.Text.RegularExpressions.Regex.Replace(html, "<[^>]*>", "").Trim();
}
