namespace GroupPolicyDirector.Core.Ad;

/// <summary>
/// AD/GPMC boundary for Take Control, Temp-GPO lifecycle, and live fingerprinting.
/// Implementations must prefer DryRun when the host is not domain-joined or ForceDryRun is set.
/// </summary>
public interface IGpoDirectoryService
{
    Task<DomainEnvironmentInfo> GetEnvironmentAsync(CancellationToken ct = default);

    /// <summary>Read production GPO fingerprint (LDAP/SYSVOL when live; dry-run store offline).</summary>
    Task<GpoFingerprint?> GetProductionFingerprintAsync(
        string domainDnsName,
        string productionGpoId,
        CancellationToken ct = default);

    /// <summary>
    /// Take Control: lock discretionary edit ACLs to the GPD service account (live)
    /// or return a planned ACL step list (dry-run). Never throws solely because offline.
    /// </summary>
    Task<AdOperationResult> TakeControlAsync(TakeControlAdRequest request, CancellationToken ct = default);

    /// <summary>Create a temporary edit GPO (copy of production) or plan the create in dry-run.</summary>
    Task<AdOperationResult> CreateTempGpoAsync(TempGpoAdRequest request, CancellationToken ct = default);

    /// <summary>Copy Temp → Production and delete Temp (or plan those steps).</summary>
    Task<AdOperationResult> DeployTempToProductionAsync(DeployAdRequest request, CancellationToken ct = default);

    /// <summary>Delete a leftover Temp GPO (best-effort / planned).</summary>
    Task<AdOperationResult> DeleteTempGpoAsync(
        string domainDnsName,
        string tempGpoId,
        string actor,
        CancellationToken ct = default);

    /// <summary>
    /// Seed or refresh the dry-run "live" fingerprint after Take Control / Deploy
    /// so OOB has a baseline to compare against.
    /// </summary>
    Task SetControlledBaselineAsync(
        string productionGpoId,
        string settingsHash,
        string? displayName,
        CancellationToken ct = default);

    /// <summary>
    /// Demo/test helper: mutate the dry-run live fingerprint so OOB scan detects drift.
    /// No-op (returns false) in Live mode.
    /// </summary>
    Task<bool> SimulateLiveDriftAsync(string productionGpoId, CancellationToken ct = default);
}
