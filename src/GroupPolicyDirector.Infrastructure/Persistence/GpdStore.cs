using GroupPolicyDirector.Core.Domain;

namespace GroupPolicyDirector.Infrastructure.Persistence;

/// <summary>
/// In-memory store for Slice 1 scaffolding. Replace with SQL persistence against database/schema.
/// </summary>
public sealed class GpdStore
{
    public List<DomainRegistration> Domains { get; } = [];
    public List<ControlledGpo> Gpos { get; } = [];
    public List<GpoVersion> Versions { get; } = [];
    public List<DelegationGrant> Delegations { get; } = [];
    public List<ProductRoleAssignment> ProductRoles { get; } = [];
    public List<AuditEvent> Audit { get; } = [];
    public List<OobDetection> OobDetections { get; } = [];

    public readonly object Sync = new();
}
