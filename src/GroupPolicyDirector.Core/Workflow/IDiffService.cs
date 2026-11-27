using GroupPolicyDirector.Core.Domain;

namespace GroupPolicyDirector.Core.Workflow;

public interface IDiffService
{
    Task<VersionDiffResult> DiffGpoVersionsAsync(
        Guid controlledGpoId,
        int fromVersion,
        int toVersion,
        CancellationToken ct = default);

    VersionDiffResult DiffSnapshots(
        string objectKey,
        string objectType,
        int fromVersion,
        int toVersion,
        string? beforeJson,
        string? afterJson,
        bool linksMode);
}
