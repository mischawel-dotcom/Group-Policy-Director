using GroupPolicyDirector.Core.Agpm;
using GroupPolicyDirector.Infrastructure.Persistence;
using Microsoft.Extensions.Logging;

namespace GroupPolicyDirector.Infrastructure.Services;

/// <summary>
/// AGPM history import dry-run stub. No sample archive available yet — reports structure only.
/// </summary>
public sealed class AgpmImportDryRunService(GpdStore store, ILogger<AgpmImportDryRunService> logger) : IAgpmImportService
{
    public Task<AgpmDryRunReport> DryRunAsync(AgpmDryRunRequest request, CancellationToken ct = default)
    {
        logger.LogInformation("AGPM dry-run for archive path {Path}", request.ArchivePath);

        var conflicts = new List<AgpmDryRunConflict>();
        var mappable = 0;
        var versionsFound = 0;

        if (string.IsNullOrWhiteSpace(request.ArchivePath))
        {
            conflicts.Add(new AgpmDryRunConflict
            {
                GpoId = "(none)",
                Reason = "Archive path is empty."
            });
        }
        else if (!Directory.Exists(request.ArchivePath) && !File.Exists(request.ArchivePath))
        {
            // Expected until real AGPM archives are provided.
            conflicts.Add(new AgpmDryRunConflict
            {
                GpoId = "(archive)",
                Reason = $"Path not found: {request.ArchivePath}. Provide AGPM history archive for real parse."
            });
        }
        else
        {
            // Placeholder discovery: count files; real format parsing TBD when samples exist.
            var files = Directory.Exists(request.ArchivePath)
                ? Directory.EnumerateFiles(request.ArchivePath, "*", SearchOption.AllDirectories).Take(500).ToList()
                : [request.ArchivePath];

            versionsFound = files.Count;
            lock (store.Sync)
            {
                foreach (var gpo in store.Gpos)
                {
                    mappable++;
                }

                if (store.Gpos.Count == 0)
                {
                    conflicts.Add(new AgpmDryRunConflict
                    {
                        GpoId = "(mapping)",
                        Reason = "No GPOs under GPD control — Take Control required before import."
                    });
                }
            }
        }

        var report = new AgpmDryRunReport
        {
            ArchivePath = request.ArchivePath,
            ArchivesScanned = 1,
            VersionsFound = versionsFound,
            MappableGpos = mappable,
            Conflicts = conflicts,
            Notes = request.PrintOnly
                ? "Dry-run only (--print-only). No history written. Re-entrant import will skip already-imported versions later."
                : "Write mode not implemented in Slice 1."
        };

        return Task.FromResult(report);
    }
}
