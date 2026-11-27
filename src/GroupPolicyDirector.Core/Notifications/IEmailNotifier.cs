namespace GroupPolicyDirector.Core.Notifications;

public sealed class EmailMessage
{
    public required IReadOnlyList<string> To { get; init; }
    public required string Subject { get; init; }
    public required string Body { get; init; }
}

public interface IEmailNotifier
{
    /// <summary>SMTP stub for Slice 1 — logs intent; real SMTP wired in later.</summary>
    Task SendAsync(EmailMessage message, CancellationToken ct = default);
}
