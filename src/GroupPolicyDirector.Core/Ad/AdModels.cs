namespace GroupPolicyDirector.Core.Ad;

public enum AdOperationMode
{
    /// <summary>Machine is domain-joined and live LDAP/GPMC hooks are used.</summary>
    Live = 1,
    /// <summary>Safe offline path: plan operations, no AD writes.</summary>
    DryRun = 2
}

public sealed class DomainEnvironmentInfo
{
    public bool IsDomainJoined { get; init; }
    public string? ComputerDnsDomain { get; init; }
    public string? ComputerNetBiosDomain { get; init; }
    public bool DirectoryReachable { get; init; }
    public bool GpmcAvailable { get; init; }
    public bool ForceDryRunConfigured { get; init; }
    public AdOperationMode EffectiveMode { get; init; }
    public required string ModeReason { get; init; }
    public DateTimeOffset DetectedAt { get; init; } = DateTimeOffset.UtcNow;
}

/// <summary>Fingerprint of a live (or simulated-live) production GPO for OOB compare.</summary>
public sealed class GpoFingerprint
{
    public required string ProductionGpoId { get; init; }
    public required string SettingsHash { get; init; }
    public string? DisplayName { get; init; }
    public int? GptVersion { get; init; }
    public string? Source { get; init; } // LDAP | Sysvol | DryRunStore
    public DateTimeOffset CapturedAt { get; init; } = DateTimeOffset.UtcNow;
}

public sealed class AdOperationResult
{
    public bool Success { get; init; }
    public AdOperationMode Mode { get; init; }
    public string? TempGpoId { get; init; }
    public string? Message { get; init; }
    public IReadOnlyList<string> PlannedSteps { get; init; } = [];
    public string? SettingsHash { get; init; }
}

public sealed class TakeControlAdRequest
{
    public required string DomainDnsName { get; init; }
    public required string ProductionGpoId { get; init; }
    public required string DisplayName { get; init; }
    public required string Actor { get; init; }
    public string? ServiceAccountSam { get; init; }
}

public sealed class TempGpoAdRequest
{
    public required string DomainDnsName { get; init; }
    public required string ProductionGpoId { get; init; }
    public required string DisplayName { get; init; }
    public required string Actor { get; init; }
}

public sealed class DeployAdRequest
{
    public required string DomainDnsName { get; init; }
    public required string ProductionGpoId { get; init; }
    public required string TempGpoId { get; init; }
    public required string Actor { get; init; }
    public string? SettingsSnapshotJson { get; init; }
}

public sealed class TakeControlContainerAdRequest
{
    public required string DomainDnsName { get; init; }
    public required string ContainerKey { get; init; }
    public required string DisplayName { get; init; }
    public required string Actor { get; init; }
    public Domain.AdContainerKind Kind { get; init; }
    public string? ServiceAccountSam { get; init; }
}

public sealed class DeployContainerLinksAdRequest
{
    public required string DomainDnsName { get; init; }
    public required string ContainerKey { get; init; }
    public required string Actor { get; init; }
    public required Domain.ContainerLinksState Links { get; init; }
}

/// <summary>Live or dry-run snapshot of a container's gpLink / gpOptions.</summary>
public sealed class ContainerLinksFingerprint
{
    public required string ContainerKey { get; init; }
    public required string LinksHash { get; init; }
    public required Domain.ContainerLinksState Links { get; init; }
    public string? Source { get; init; }
    public DateTimeOffset CapturedAt { get; init; } = DateTimeOffset.UtcNow;
}
