using System.Net;
using System.Net.Mail;
using GroupPolicyDirector.Core.Notifications;
using Microsoft.Extensions.Logging;

namespace GroupPolicyDirector.Infrastructure.Services;

/// <summary>
/// Configurable SMTP notifier. Uses runtime settings overlay when present.
/// Empty Host → log-only (safe offline). No OOB auto-reset.
/// </summary>
public sealed class SmtpEmailNotifier(
    ISmtpSettingsService smtpSettings,
    ILogger<SmtpEmailNotifier> logger) : IEmailNotifier
{
    public async Task SendAsync(EmailMessage message, CancellationToken ct = default)
    {
        var cfg = smtpSettings.GetRuntime();
        var host = cfg.Host;
        var from = string.IsNullOrWhiteSpace(cfg.From) ? "gpd-noreply@localhost" : cfg.From;

        if (string.IsNullOrWhiteSpace(host))
        {
            logger.LogInformation(
                "SMTP not configured (Smtp:Host empty) — log-only. To={To} Subject={Subject}\n{Body}",
                string.Join(",", message.To),
                message.Subject,
                message.Body);
            return;
        }

        using var client = new SmtpClient(host, cfg.Port)
        {
            EnableSsl = cfg.UseSsl,
            DeliveryMethod = SmtpDeliveryMethod.Network,
            Timeout = 30_000
        };

        if (!string.IsNullOrWhiteSpace(cfg.User))
        {
            client.Credentials = new NetworkCredential(cfg.User, cfg.Password ?? "");
        }
        else
        {
            client.UseDefaultCredentials = cfg.UseDefaultCredentials;
        }

        using var mail = new MailMessage
        {
            From = new MailAddress(from),
            Subject = message.Subject,
            Body = message.Body,
            IsBodyHtml = false
        };
        foreach (var to in message.To.Where(t => !string.IsNullOrWhiteSpace(t)))
        {
            mail.To.Add(to.Trim());
        }

        if (mail.To.Count == 0)
        {
            logger.LogWarning("SMTP send skipped — no recipients. Subject={Subject}", message.Subject);
            return;
        }

        ct.ThrowIfCancellationRequested();
        await client.SendMailAsync(mail, ct);
        logger.LogInformation(
            "SMTP sent via {Host}:{Port} To={To} Subject={Subject}",
            host,
            cfg.Port,
            string.Join(",", message.To),
            message.Subject);
    }
}
