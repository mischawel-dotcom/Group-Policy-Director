namespace GroupPolicyDirector.Core.Domain;

public interface IDomainService
{
    Task<IReadOnlyList<DomainRegistration>> ListAsync(CancellationToken ct = default);
    Task<DomainRegistration> RegisterAsync(string dnsName, string netBiosName, string actor, CancellationToken ct = default);
}

public interface IOobDetectionService
{
    Task<IReadOnlyList<OobDetection>> ScanAsync(CancellationToken ct = default);
    Task<IReadOnlyList<OobDetection>> ListAsync(CancellationToken ct = default);
    Task<OobDetection?> ResolveAtCheckoutAsync(Guid controlledGpoId, string resolution, string actor, CancellationToken ct = default);
}

public sealed class AuditQuery
{
    public string? ObjectKey { get; init; }
    public string? Actor { get; init; }
    public string? Action { get; init; }
    public DateTimeOffset? From { get; init; }
    public DateTimeOffset? To { get; init; }
    public int Take { get; init; } = 200;
}

public interface IAuditService
{
    Task WriteAsync(AuditEvent evt, CancellationToken ct = default);
    Task<IReadOnlyList<AuditEvent>> ListAsync(int take = 100, CancellationToken ct = default);
    Task<IReadOnlyList<AuditEvent>> QueryAsync(AuditQuery query, CancellationToken ct = default);
    Task<string> ExportCsvAsync(AuditQuery query, CancellationToken ct = default);
}
