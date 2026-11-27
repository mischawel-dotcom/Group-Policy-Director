using GroupPolicyDirector.Core.Domain;

namespace GroupPolicyDirector.Core.Workflow;

public interface IContainerWorkflowService
{
    Task<IReadOnlyList<ControlledContainer>> ListAsync(CancellationToken ct = default);

    Task<ControlledContainer> TakeControlAsync(
        Guid domainId,
        AdContainerKind kind,
        string containerKey,
        string displayName,
        string actor,
        CancellationToken ct = default);

    Task<ControlledContainer> CheckoutAsync(Guid containerId, string actor, CancellationToken ct = default);

    Task<ContainerLinksState> GetLinksAsync(Guid containerId, CancellationToken ct = default);

    Task<ContainerLinksState> UpdateLinksAsync(
        Guid containerId,
        ContainerLinksState links,
        string actor,
        CancellationToken ct = default);

    Task<ContainerVersion> CheckInAsync(Guid containerId, string comment, string actor, CancellationToken ct = default);

    Task<ControlledContainer> ApproveAsync(Guid containerId, string actor, string? comment = null, CancellationToken ct = default);

    Task<ControlledContainer> RejectAsync(Guid containerId, string actor, string comment, CancellationToken ct = default);

    Task<ControlledContainer> DeployAsync(Guid containerId, string actor, CancellationToken ct = default);

    Task<IReadOnlyList<ContainerVersion>> GetHistoryAsync(Guid containerId, CancellationToken ct = default);

    Task<VersionDiffResult> DiffVersionsAsync(Guid containerId, int fromVersion, int toVersion, CancellationToken ct = default);
}
