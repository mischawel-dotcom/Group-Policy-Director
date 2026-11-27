using GroupPolicyDirector.Core.Domain;

namespace GroupPolicyDirector.Infrastructure.Persistence;

/// <summary>
/// In-memory store for Slice scaffolding. Replace with SQL persistence against database/schema.
/// </summary>
public sealed class GpdStore
{
    public List<DomainRegistration> Domains { get; } = [];
    public List<ControlledGpo> Gpos { get; } = [];
    public List<GpoVersion> Versions { get; } = [];
    public List<DelegationGrant> Delegations { get; } = [];
    public List<ProductRoleAssignment> ProductRoles { get; } = [];
    public List<AdGroupMembershipStub> GroupMemberships { get; } = [];
    public List<AuditEvent> Audit { get; } = [];
    public List<OobDetection> OobDetections { get; } = [];
    /// <summary>Working / production settings trees keyed by ControlledGpo Id.</summary>
    public Dictionary<Guid, PolicyTreeNode> SettingsTrees { get; } = new();

    public readonly object Sync = new();
}
