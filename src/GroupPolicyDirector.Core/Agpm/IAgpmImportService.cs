namespace GroupPolicyDirector.Core.Agpm;

public sealed class AgpmDryRunRequest
{
    public required string ArchivePath { get; init; }
    public bool PrintOnly { get; init; } = true;
}

public sealed class AgpmDryRunConflict
{
    public required string GpoId { get; init; }
    public required string Reason { get; init; }
}

public sealed class AgpmDryRunReport
{
    public required string ArchivePath { get; init; }
    public int ArchivesScanned { get; init; }
    public int VersionsFound { get; init; }
    public int MappableGpos { get; init; }
    public IReadOnlyList<AgpmDryRunConflict> Conflicts { get; init; } = [];
    public string Notes { get; init; } = "Dry-run only — no history written.";
}

public interface IAgpmImportService
{
    /// <summary>
    /// Slice 1 stub: scan/parse AGPM history archives and report mapping conflicts without writing.
    /// </summary>
    Task<AgpmDryRunReport> DryRunAsync(AgpmDryRunRequest request, CancellationToken ct = default);
}
