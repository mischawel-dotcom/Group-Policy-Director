using GroupPolicyDirector.Core.Domain;

namespace GroupPolicyDirector.Core.Workflow;

public interface IGpoWorkflowService
{
    Task<IReadOnlyList<ControlledGpo>> ListControlledAsync(CancellationToken ct = default);
    Task<ControlledGpo> TakeControlAsync(Guid domainId, string productionGpoId, string displayName, string actor, CancellationToken ct = default);
    /// <summary>Create a new empty (or copied) GPO under control. Enforces naming regex when configured.</summary>
    Task<ControlledGpo> CreateAsync(
        Guid domainId,
        string displayName,
        string actor,
        Guid? copyFromControlledGpoId = null,
        bool checkoutAfterCreate = false,
        CancellationToken ct = default);
    /// <summary>Rename a controlled GPO. Enforces naming regex when configured.</summary>
    Task<ControlledGpo> RenameAsync(Guid controlledGpoId, string newDisplayName, string actor, CancellationToken ct = default);
    Task<ControlledGpo> CheckoutAsync(Guid controlledGpoId, string actor, bool checkoutAnyway = false, CancellationToken ct = default);
    Task<PolicyTreeNode> GetSettingsTreeAsync(Guid controlledGpoId, CancellationToken ct = default);
    Task<PolicyTreeNode> UpdateSettingsAsync(Guid controlledGpoId, IReadOnlyList<PolicySettingUpdate> updates, string actor, CancellationToken ct = default);
    Task<GpoVersion> CheckInAsync(Guid controlledGpoId, string comment, string actor, CancellationToken ct = default);
    Task<ControlledGpo> ApproveAsync(Guid controlledGpoId, string actor, string? comment = null, CancellationToken ct = default);
    Task<ControlledGpo> RejectAsync(Guid controlledGpoId, string actor, string comment, CancellationToken ct = default);
    Task<ControlledGpo> DeployAsync(Guid controlledGpoId, string actor, CancellationToken ct = default);
    Task<ControlledGpo> DeployAsync(Guid controlledGpoId, string actor, DeployOptions options, CancellationToken ct = default);
    Task EnsureCanDeployAsync(Guid controlledGpoId, string actor, CancellationToken ct = default);
    Task<IReadOnlyList<GpoVersion>> GetHistoryAsync(Guid controlledGpoId, CancellationToken ct = default);
}
