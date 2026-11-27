namespace GroupPolicyDirector.Core.Ad;

/// <summary>
/// Detects whether this host can safely talk to AD/GPMC.
/// Non-domain-joined machines always report DryRun-capable offline mode.
/// </summary>
public interface IDirectoryEnvironment
{
    Task<DomainEnvironmentInfo> DetectAsync(CancellationToken ct = default);
}
