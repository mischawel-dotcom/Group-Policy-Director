using GroupPolicyDirector.Core.Domain;

namespace GroupPolicyDirector.Core.Notifications;

public interface ISmtpSettingsService
{
    SmtpSettingsView GetView();
    SmtpSettingsView Update(SmtpSettingsUpdate update, string actor);
    Task<(bool Ok, string Message)> SendTestAsync(string? toOverride, string actor, CancellationToken ct = default);

    /// <summary>Effective values for the notifier (including password).</summary>
    SmtpRuntimeConfig GetRuntime();
}

public sealed class SmtpRuntimeConfig
{
    public string Host { get; init; } = "";
    public int Port { get; init; } = 25;
    public string From { get; init; } = "";
    public bool UseSsl { get; init; }
    public string User { get; init; } = "";
    public string Password { get; init; } = "";
    public bool UseDefaultCredentials { get; init; }
    public IReadOnlyList<string> AlertRecipients { get; init; } = [];
}