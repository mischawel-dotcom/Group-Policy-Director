using GroupPolicyDirector.Core.Authz;

namespace GroupPolicyDirector.Core.Domain;

public sealed class DomainRegistration
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public required string DnsName { get; init; }
    public required string NetBiosName { get; init; }
    public DateTimeOffset RegisteredAt { get; init; } = DateTimeOffset.UtcNow;
    public string? RegisteredBy { get; init; }
    public bool IsActive { get; set; } = true;
}

public enum ControlledGpoStatus
{
    UnderControl = 1,
    CheckedOut = 2,
    WaitingApproval = 3,
    Deployed = 4
}

public sealed class ControlledGpo
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public Guid DomainId { get; init; }
    public required string ProductionGpoId { get; init; }
    public required string DisplayName { get; init; }
    public string? TempGpoId { get; set; }
    public ControlledGpoStatus Status { get; set; } = ControlledGpoStatus.UnderControl;
    public string? CheckedOutBy { get; set; }
    public DateTimeOffset? CheckedOutAt { get; set; }
    public string? SettingsHash { get; set; }
    public DateTimeOffset TakenControlAt { get; init; } = DateTimeOffset.UtcNow;
    public string? TakenControlBy { get; init; }
}

public sealed class GpoVersion
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public Guid ControlledGpoId { get; init; }
    public int VersionNumber { get; init; }
    public required string Comment { get; init; }
    public required string CreatedBy { get; init; }
    public DateTimeOffset CreatedAt { get; init; } = DateTimeOffset.UtcNow;
    public string? SettingsSnapshotJson { get; init; }
    public string? SettingsHash { get; init; }
    public string? Provenance { get; init; }
}

public sealed class DelegationGrant
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public DelegationScopeKind ScopeKind { get; init; }
    public required string ScopeKey { get; init; }
    public ObjectRole Role { get; init; }
    /// <summary>AD group (preferred) or user SID/UPN as trustee.</summary>
    public required string TrusteePrincipal { get; init; }
    public bool InheritToChildren { get; init; } = true;
    public required string GrantedBy { get; init; }
    public DateTimeOffset GrantedAt { get; init; } = DateTimeOffset.UtcNow;
}

public sealed class ProductRoleAssignment
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public ProductRole Role { get; init; }
    public required string TrusteePrincipal { get; init; }
    public required string GrantedBy { get; init; }
    public DateTimeOffset GrantedAt { get; init; } = DateTimeOffset.UtcNow;
}

public sealed class AuditEvent
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public DateTimeOffset OccurredAt { get; init; } = DateTimeOffset.UtcNow;
    public required string Actor { get; init; }
    public required string Action { get; init; }
    public string? ObjectKey { get; init; }
    public string? Outcome { get; init; }
    public string? DetailJson { get; init; }
}

public sealed class OobDetection
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public Guid ControlledGpoId { get; init; }
    public DateTimeOffset DetectedAt { get; init; } = DateTimeOffset.UtcNow;
    public required string ProductionHash { get; init; }
    public required string LastControlledHash { get; init; }
    public string? DiffSummary { get; init; }
    public bool EmailSent { get; set; }
    public string? Resolution { get; set; }
}

public sealed class PolicyTreeNode
{
    public required string Path { get; init; }
    public required string Name { get; init; }
    public string? Value { get; init; }
    public string? State { get; init; }
    public List<PolicyTreeNode> Children { get; init; } = [];
}
