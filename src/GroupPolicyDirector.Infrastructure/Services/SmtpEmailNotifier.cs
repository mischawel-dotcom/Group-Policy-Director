using GroupPolicyDirector.Core.Notifications;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace GroupPolicyDirector.Infrastructure.Services;

/// <summary>
/// SMTP stub — logs outbound mail. Wire real SmtpClient / MailKit when SMTP settings are configured.
/// </summary>
public sealed class SmtpEmailNotifier(IConfiguration config, ILogger<SmtpEmailNotifier> logger) : IEmailNotifier
{
    public Task SendAsync(EmailMessage message, CancellationToken ct = default)
    {
        var host = config["Smtp:Host"];
        if (string.IsNullOrWhiteSpace(host))
        {
            logger.LogInformation(
                "SMTP stub (no host configured). To={To} Subject={Subject}",
                string.Join(",", message.To),
                message.Subject);
            return Task.CompletedTask;
        }

        // Placeholder for real send: Smtp:Host / Port / From / UseSsl
        logger.LogInformation(
            "SMTP would send via {Host} To={To} Subject={Subject}",
            host,
            string.Join(",", message.To),
            message.Subject);

        return Task.CompletedTask;
    }
}
