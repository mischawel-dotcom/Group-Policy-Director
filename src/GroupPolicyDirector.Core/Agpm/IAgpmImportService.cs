namespace GroupPolicyDirector.Core.Agpm;

public sealed class AgpmDryRunRequest
{
    /// <summary>Filesystem path to an AGPM history directory or .zip archive.</summary>
    public string? ArchivePath { get; init; }

    /// <summary>Optional in-memory zip bytes (fixture upload). Takes precedence when set.</summary>
    public byte[]? ArchiveZipBytes { get; init; }

    public string? ArchiveFileName { get; init; }

    public bool PrintOnly { get; init; } = true;
}

public sealed class AgpmDryRunConflict
{
    public required string GpoId { get; init; }
    public required string Reason { get; init; }
    public string? Severity { get; init; }
}

/// <summary>One GPO discovered in an AGPM-style history archive.</summary>
public sealed class AgpmArchiveGpoEntry
{
    public required string SourceGpoId { get; init; }
    public string? DisplayName { get; init; }
    public int VersionCount { get; init; }
    public IReadOnlyList<string> VersionLabels { get; init; } = [];
    public string? MappedControlledGpoId { get; init; }
    public string MappingStatus { get; init; } = "Unmapped";
    public string? Notes { get; init; }
}

public sealed class AgpmDryRunReport
{
    public required string ArchivePath { get; init; }
    public string? ArchiveFileName { get; init; }
    public string Parser { get; init; } = "AgpmArchiveStubParser";
    public int ArchivesScanned { get; init; }
    public int VersionsFound { get; init; }
    public int MappableGpos { get; init; }
    public int UnmappedGpos { get; init; }
    public IReadOnlyList<AgpmArchiveGpoEntry> Entries { get; init; } = [];
    public IReadOnlyList<AgpmDryRunConflict> Conflicts { get; init; } = [];
    public string Notes { get; init; } = "Dry-run only — no history written.";
}

public interface IAgpmImportService
{
    /// <summary>
    /// Parse AGPM-style history archives (directory or zip) and report mapping without writing history.
    /// </summary>
    Task<AgpmDryRunReport> DryRunAsync(AgpmDryRunRequest request, CancellationToken ct = default);
}
