namespace GroupPolicyDirector.Core.Notifications;

public sealed class EmailMessage
{
    public required IReadOnlyList<string> To { get; init; }
    public required string Subject { get; init; }
    public required string Body { get; init; }
}

public interface IEmailNotifier
{
    /// <summary>Sends via configurable SMTP when Smtp:Host is set; otherwise logs only.</summary>
    Task SendAsync(EmailMessage message, CancellationToken ct = default);
}
