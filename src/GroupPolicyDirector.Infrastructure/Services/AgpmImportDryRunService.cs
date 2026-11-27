using GroupPolicyDirector.Core.Agpm;
using GroupPolicyDirector.Infrastructure.Persistence;
using Microsoft.Extensions.Logging;

namespace GroupPolicyDirector.Infrastructure.Services;

/// <summary>
/// AGPM history import dry-run: parses directory/zip archives (fixture-compatible layout) and maps to controlled GPOs.
/// Write/import remains out of scope for Slice 4.
/// </summary>
public sealed class AgpmImportDryRunService(GpdStore store, ILogger<AgpmImportDryRunService> logger) : IAgpmImportService
{
    public async Task<AgpmDryRunReport> DryRunAsync(AgpmDryRunRequest request, CancellationToken ct = default)
    {
        logger.LogInformation(
            "AGPM dry-run path={Path} zip={HasZip} file={File}",
            request.ArchivePath,
            request.ArchiveZipBytes is { Length: > 0 },
            request.ArchiveFileName);

        var parsed = await AgpmArchiveParser.ParseAsync(
            request.ArchivePath,
            request.ArchiveZipBytes,
            request.ArchiveFileName,
            ct);

        var conflicts = new List<AgpmDryRunConflict>();
        var entries = new List<AgpmArchiveGpoEntry>();
        var mappable = 0;
        var unmapped = 0;
        var versionsFound = 0;

        if (parsed.ArchivesScanned == 0 || parsed.Folders.Count == 0)
        {
            conflicts.Add(new AgpmDryRunConflict
            {
                GpoId = "(archive)",
                Reason = parsed.Notes,
                Severity = "Error"
            });
        }

        Dictionary<string, Guid> controlledByProductionId;
        lock (store.Sync)
        {
            controlledByProductionId = store.Gpos
                .GroupBy(g => Normalize(g.ProductionGpoId), StringComparer.OrdinalIgnoreCase)
                .ToDictionary(g => g.Key, g => g.First().Id, StringComparer.OrdinalIgnoreCase);

            if (store.Gpos.Count == 0 && parsed.Folders.Count > 0)
            {
                conflicts.Add(new AgpmDryRunConflict
                {
                    GpoId = "(mapping)",
                    Reason = "No GPOs under GPD control — Take Control required before import.",
                    Severity = "Warning"
                });
            }
        }

        foreach (var folder in parsed.Folders)
        {
            ct.ThrowIfCancellationRequested();
            versionsFound += folder.VersionLabels.Count;
            var key = Normalize(folder.GpoId);
            controlledByProductionId.TryGetValue(key, out var controlledId);
            var mapped = controlledId != Guid.Empty;

            if (mapped)
            {
                mappable++;
            }
            else
            {
                unmapped++;
                conflicts.Add(new AgpmDryRunConflict
                {
                    GpoId = folder.GpoId,
                    Reason = "No controlled GPO with matching production GUID — Take Control first or map manually later.",
                    Severity = "Warning"
                });
            }

            entries.Add(new AgpmArchiveGpoEntry
            {
                SourceGpoId = folder.GpoId,
                DisplayName = folder.DisplayName,
                VersionCount = folder.VersionLabels.Count,
                VersionLabels = folder.VersionLabels.ToList(),
                MappedControlledGpoId = mapped ? controlledId.ToString() : null,
                MappingStatus = mapped ? "Mappable" : "Unmapped",
                Notes = mapped
                    ? "Would import chronologically with provenance [AGPM]; re-entrant skip of already-imported versions TBD."
                    : "Unmapped — excluded from write when import lands."
            });
        }

        return new AgpmDryRunReport
        {
            ArchivePath = parsed.ArchivePath,
            ArchiveFileName = request.ArchiveFileName,
            Parser = "AgpmArchiveStubParser/v1",
            ArchivesScanned = parsed.ArchivesScanned,
            VersionsFound = versionsFound,
            MappableGpos = mappable,
            UnmappedGpos = unmapped,
            Entries = entries,
            Conflicts = conflicts,
            Notes = request.PrintOnly
                ? $"{parsed.Notes} Dry-run only (--print-only). No history written."
                : $"{parsed.Notes} Write mode not implemented in Slice 4."
        };
    }

    private static string Normalize(string id)
    {
        var trimmed = id.Trim().Trim('{', '}');
        return Guid.TryParse(trimmed, out var g) ? g.ToString("B").ToUpperInvariant() : id.Trim();
    }
}
