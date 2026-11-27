using System.Text.Json;
using GroupPolicyDirector.Core.Domain;
using GroupPolicyDirector.Core.Notifications;
using Microsoft.Extensions.Configuration;

namespace GroupPolicyDirector.Infrastructure.Services;

/// <summary>
/// Runtime SMTP overlay for OOB polish. Empty Host ⇒ log-only. Auto-reset never enabled.
/// </summary>
public sealed class SmtpSettingsService(IConfiguration config, IAuditService audit) : ISmtpSettingsService
{
    private readonly object _sync = new();
    private string? _host;
    private int? _port;
    private string? _from;
    private bool? _useSsl;
    private string? _user;
    private string? _password;
    private bool? _useDefaultCredentials;
    private List<string>? _recipients;
    private bool _passwordSet;

    public SmtpSettingsView GetView()
    {
        var runtime = GetRuntime();
        return new SmtpSettingsView
        {
            Host = runtime.Host,
            Port = runtime.Port,
            From = runtime.From,
            UseSsl = runtime.UseSsl,
            User = runtime.User,
            PasswordConfigured = _passwordSet || !string.IsNullOrEmpty(config["Smtp:Password"]),
            UseDefaultCredentials = runtime.UseDefaultCredentials,
            IsConfigured = !string.IsNullOrWhiteSpace(runtime.Host),
            AlertRecipients = runtime.AlertRecipients,
            Mode = string.IsNullOrWhiteSpace(runtime.Host) ? "LogOnly" : "Smtp",
            AutoResetEnabled = false
        };
    }

    public SmtpSettingsView Update(SmtpSettingsUpdate update, string actor)
    {
        lock (_sync)
        {
            if (update.Host is not null)
            {
                _host = update.Host.Trim();
            }

            if (update.Port is not null)
            {
                _port = update.Port;
            }

            if (update.From is not null)
            {
                _from = update.From.Trim();
            }

            if (update.UseSsl is not null)
            {
                _useSsl = update.UseSsl;
            }

            if (update.User is not null)
            {
                _user = update.User.Trim();
            }

            if (update.Password is not null)
            {
                _password = update.Password;
                _passwordSet = !string.IsNullOrEmpty(update.Password);
            }

            if (update.UseDefaultCredentials is not null)
            {
                _useDefaultCredentials = update.UseDefaultCredentials;
            }

            if (update.AlertRecipients is not null)
            {
                _recipients = update.AlertRecipients
                    .Where(r => !string.IsNullOrWhiteSpace(r))
                    .Select(r => r.Trim())
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .ToList();
            }
        }

        var runtime = GetRuntime();
        _ = audit.WriteAsync(new AuditEvent
        {
            Actor = actor,
            Action = "Settings.SmtpUpdate",
            ObjectKey = "Smtp",
            Outcome = "Success",
            DetailJson = JsonSerializer.Serialize(new
            {
                host = runtime.Host,
                port = runtime.Port,
                mode = string.IsNullOrWhiteSpace(runtime.Host) ? "LogOnly" : "Smtp",
                autoReset = false
            })
        });

        return GetView();
    }

    public Task<(bool Ok, string Message)> SendTestAsync(
        string? toOverride,
        string actor,
        CancellationToken ct = default)
    {
        // Controller performs the actual send via IEmailNotifier; this validates recipients.
        ct.ThrowIfCancellationRequested();
        var runtime = GetRuntime();
        var to = !string.IsNullOrWhiteSpace(toOverride)
            ? new List<string> { toOverride.Trim() }
            : runtime.AlertRecipients.ToList();

        if (to.Count == 0)
        {
            return Task.FromResult((false, "No test recipient. Set AlertRecipients or pass to=."));
        }

        var msg = string.IsNullOrWhiteSpace(runtime.Host)
            ? $"Ready (LogOnly). Recipients: {string.Join(", ", to)}"
            : $"Ready via {runtime.Host}:{runtime.Port}. Recipients: {string.Join(", ", to)}";
        return Task.FromResult((true, msg));
    }

    public SmtpRuntimeConfig GetRuntime()
    {
        lock (_sync)
        {
            var recipients = _recipients
                             ?? config.GetSection("Oob:AlertRecipients").Get<string[]>()?.ToList()
                             ?? [];

            return new SmtpRuntimeConfig
            {
                Host = _host ?? config["Smtp:Host"] ?? "",
                Port = _port ?? config.GetValue("Smtp:Port", 25),
                From = _from ?? config["Smtp:From"] ?? "gpd-noreply@localhost",
                UseSsl = _useSsl ?? config.GetValue("Smtp:UseSsl", false),
                User = _user ?? config["Smtp:User"] ?? "",
                Password = _password ?? config["Smtp:Password"] ?? "",
                UseDefaultCredentials = _useDefaultCredentials
                                        ?? config.GetValue("Smtp:UseDefaultCredentials", false),
                AlertRecipients = recipients
            };
        }
    }
}
