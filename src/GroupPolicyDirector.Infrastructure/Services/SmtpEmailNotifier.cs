using System.Net;
using System.Net.Mail;
using GroupPolicyDirector.Core.Notifications;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace GroupPolicyDirector.Infrastructure.Services;

/// <summary>
/// Configurable SMTP notifier. When Smtp:Host is empty, logs only (safe offline).
/// When Host is set, sends via System.Net.Mail.SmtpClient.
/// </summary>
public sealed class SmtpEmailNotifier(IConfiguration config, ILogger<SmtpEmailNotifier> logger) : IEmailNotifier
{
    public async Task SendAsync(EmailMessage message, CancellationToken ct = default)
    {
        var host = config["Smtp:Host"];
        var from = config["Smtp:From"] ?? "gpd-noreply@localhost";
        var port = config.GetValue("Smtp:Port", 25);
        var useSsl = config.GetValue("Smtp:UseSsl", false);
        var user = config["Smtp:User"];
        var password = config["Smtp:Password"];

        if (string.IsNullOrWhiteSpace(host))
        {
            logger.LogInformation(
                "SMTP not configured (Smtp:Host empty) — log-only. To={To} Subject={Subject}\n{Body}",
                string.Join(",", message.To),
                message.Subject,
                message.Body);
            return;
        }

        using var client = new SmtpClient(host, port)
        {
            EnableSsl = useSsl,
            DeliveryMethod = SmtpDeliveryMethod.Network,
            Timeout = 30_000
        };

        if (!string.IsNullOrWhiteSpace(user))
        {
            client.Credentials = new NetworkCredential(user, password ?? "");
        }
        else
        {
            client.UseDefaultCredentials = config.GetValue("Smtp:UseDefaultCredentials", false);
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
            port,
            string.Join(",", message.To),
            message.Subject);
    }
}
