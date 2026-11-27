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
    public List<ControlledContainer> Containers { get; } = [];
    public List<ContainerVersion> ContainerVersions { get; } = [];
    public List<DelegationGrant> Delegations { get; } = [];
    public List<ProductRoleAssignment> ProductRoles { get; } = [];
    public List<BreakGlassSession> BreakGlassSessions { get; } = [];
    public List<AdGroupMembershipStub> GroupMemberships { get; } = [];
    public List<AuditEvent> Audit { get; } = [];
    public List<OobDetection> OobDetections { get; } = [];
    public List<RestrictedPolicyArea> RestrictedPolicies { get; } = [];
    /// <summary>Working / production settings trees keyed by ControlledGpo Id.</summary>
    public Dictionary<Guid, PolicyTreeNode> SettingsTrees { get; } = new();
    /// <summary>Working link sets keyed by ControlledContainer Id.</summary>
    public Dictionary<Guid, ContainerLinksState> ContainerLinks { get; } = new();

    /// <summary>General settings (naming regex + welcome wizard) — mutated under Sync.</summary>
    public string? GpoNamingRegex { get; set; }
    public WelcomeWizardStatus WelcomeWizardStatus { get; set; } = WelcomeWizardStatus.Pending;
    public DateTimeOffset? WelcomeWizardCompletedAt { get; set; }
    public string? WelcomeWizardCompletedBy { get; set; }

    public readonly object Sync = new();
}
