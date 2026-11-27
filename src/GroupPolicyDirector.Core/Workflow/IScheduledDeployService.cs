using GroupPolicyDirector.Core.Domain;

namespace GroupPolicyDirector.Core.Workflow;

public interface IScheduledDeployService
{
    Task<IReadOnlyList<ScheduledDeploy>> ListAsync(CancellationToken ct = default);

    Task<ScheduledDeploy?> GetAsync(Guid id, CancellationToken ct = default);

    Task<ScheduledDeploy> ScheduleGpoAsync(
        Guid controlledGpoId,
        ScheduleDeployRequest request,
        string actor,
        CancellationToken ct = default);

    Task<ScheduledDeploy> ScheduleContainerAsync(
        Guid containerId,
        ScheduleDeployRequest request,
        string actor,
        CancellationToken ct = default);

    Task<ScheduledDeploy> CancelAsync(Guid scheduleId, string actor, CancellationToken ct = default);

    /// <summary>Run due Pending jobs (API hosted processor + manual /process-due).</summary>
    Task<IReadOnlyList<ScheduledDeploy>> ProcessDueAsync(CancellationToken ct = default);
}
