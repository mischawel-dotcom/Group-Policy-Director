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
    Deployed = 4,
    /// <summary>Approved, waiting for Deploy.</summary>
    Approved = 5
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
    /// <summary>Version awaiting Approve or Deploy.</summary>
    public Guid? PendingVersionId { get; set; }
    public string? ApprovedBy { get; set; }
    public DateTimeOffset? ApprovedAt { get; set; }
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
    public string? WorkflowState { get; set; } // Draft | PendingApproval | Approved | Rejected | Deployed
    public string? ReviewedBy { get; set; }
    public string? ReviewComment { get; set; }
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

/// <summary>
/// Time-bound Break Glass emergency session. Eligibility requires ProductRole.BreakGlass;
/// activation is audited and expires automatically.
/// </summary>
public sealed class BreakGlassSession
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public required string TrusteePrincipal { get; init; }
    public required string ActivatedBy { get; init; }
    public DateTimeOffset ActivatedAt { get; init; } = DateTimeOffset.UtcNow;
    public DateTimeOffset ExpiresAt { get; init; }
    public required string Reason { get; init; }
    public DateTimeOffset? RevokedAt { get; set; }
    public string? RevokedBy { get; set; }
    public bool IsActive => RevokedAt is null && DateTimeOffset.UtcNow < ExpiresAt;
}

/// <summary>
/// Stub AD group membership used when not domain-joined. Production expands Windows token groups.
/// </summary>
public sealed class AdGroupMembershipStub
{
    public required string UserPrincipal { get; init; }
    public required string GroupPrincipal { get; init; }
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
    public required string ProductionHash { get; set; }
    public required string LastControlledHash { get; set; }
    public string? DiffSummary { get; set; }
    public bool EmailSent { get; set; }
    public string? Resolution { get; set; }
}

public sealed class PolicyTreeNode
{
    public required string Path { get; init; }
    public required string Name { get; init; }
    public string? Value { get; set; }
    /// <summary>Enabled | Disabled | NotConfigured | Configured | Stub</summary>
    public string? State { get; set; }
    public bool IsEditable { get; set; }
    /// <summary>True when ADMX/GPMC provider is not available — UI shows stub hint.</summary>
    public bool IsStub { get; set; }
    public string? SettingType { get; set; } // PolicyState | Text | Number | MultiText
    public List<PolicyTreeNode> Children { get; set; } = [];
}

public sealed class PolicySettingUpdate
{
    public required string Path { get; init; }
    public string? State { get; init; }
    public string? Value { get; init; }
}

/// <summary>AD container under GPD control (OU, Domain, or Site) for gpLink change-control.</summary>
public enum AdContainerKind
{
    OrganizationalUnit = 1,
    Domain = 2,
    Site = 3
}

public sealed class ControlledContainer
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public Guid DomainId { get; init; }
    public AdContainerKind Kind { get; init; }
    /// <summary>Stable key: OU/Domain DN or Site CN=Name,CN=Sites,CN=Configuration,...</summary>
    public required string ContainerKey { get; init; }
    public required string DisplayName { get; init; }
    public ControlledGpoStatus Status { get; set; } = ControlledGpoStatus.UnderControl;
    public string? CheckedOutBy { get; set; }
    public DateTimeOffset? CheckedOutAt { get; set; }
    public string? LinksHash { get; set; }
    public DateTimeOffset TakenControlAt { get; init; } = DateTimeOffset.UtcNow;
    public string? TakenControlBy { get; init; }
    public Guid? PendingVersionId { get; set; }
    public string? ApprovedBy { get; set; }
    public DateTimeOffset? ApprovedAt { get; set; }
}

/// <summary>One GPO link on a container (gpLink entry).</summary>
public sealed class GpoLinkEntry
{
    public required string GpoId { get; set; }
    public string? DisplayName { get; set; }
    /// <summary>0 = highest precedence (leftmost / first in gpLink).</summary>
    public int Order { get; set; }
    public bool Enforced { get; set; }
    public bool Disabled { get; set; }
}

/// <summary>Editable link set for a controlled container (working copy or snapshot).</summary>
public sealed class ContainerLinksState
{
    public bool BlockInheritance { get; set; }
    public List<GpoLinkEntry> Links { get; set; } = [];
}

public sealed class ContainerVersion
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public Guid ControlledContainerId { get; init; }
    public int VersionNumber { get; init; }
    public required string Comment { get; init; }
    public required string CreatedBy { get; init; }
    public DateTimeOffset CreatedAt { get; init; } = DateTimeOffset.UtcNow;
    public string? LinksSnapshotJson { get; init; }
    public string? LinksHash { get; init; }
    public string? Provenance { get; init; }
    public string? WorkflowState { get; set; }
    public string? ReviewedBy { get; set; }
    public string? ReviewComment { get; set; }
}

/// <summary>Unified settings or links diff line for History UI.</summary>
public sealed class DiffChange
{
    public required string Path { get; init; }
    public required string Kind { get; init; } // Added | Removed | Changed
    public string? Before { get; init; }
    public string? After { get; init; }
}

public sealed class VersionDiffResult
{
    public int FromVersion { get; init; }
    public int ToVersion { get; init; }
    public required string ObjectKey { get; init; }
    public required string ObjectType { get; init; } // Gpo | Container
    public IReadOnlyList<DiffChange> Changes { get; init; } = [];
}

/// <summary>Runtime SMTP settings overlay (Slice 5 polish). Password never returned to UI.</summary>
public sealed class SmtpSettingsView
{
    public string Host { get; init; } = "";
    public int Port { get; init; } = 25;
    public string From { get; init; } = "";
    public bool UseSsl { get; init; }
    public string User { get; init; } = "";
    public bool PasswordConfigured { get; init; }
    public bool UseDefaultCredentials { get; init; }
    public bool IsConfigured { get; init; }
    public IReadOnlyList<string> AlertRecipients { get; init; } = [];
    public string Mode { get; init; } = "LogOnly"; // LogOnly | Smtp
    public bool AutoResetEnabled { get; init; } // always false in MVP
}

public sealed class SmtpSettingsUpdate
{
    public string? Host { get; init; }
    public int? Port { get; init; }
    public string? From { get; init; }
    public bool? UseSsl { get; init; }
    public string? User { get; init; }
    public string? Password { get; init; }
    public bool? UseDefaultCredentials { get; init; }
    public List<string>? AlertRecipients { get; init; }
}

/// <summary>General product settings: GPO naming regex and first-run wizard state (Slice 7).</summary>
public sealed class GeneralSettingsView
{
    /// <summary>Optional .NET regex. Empty/null disables naming enforcement.</summary>
    public string? GpoNamingRegex { get; init; }
    public bool NamingEnforcementEnabled { get; init; }
    public bool RequireCheckInComment { get; init; } = true;
    public WelcomeWizardStatus WelcomeWizardStatus { get; init; } = WelcomeWizardStatus.Pending;
    public DateTimeOffset? WelcomeWizardCompletedAt { get; init; }
    public string? WelcomeWizardCompletedBy { get; init; }
}

public sealed class GeneralSettingsUpdate
{
    public string? GpoNamingRegex { get; init; }
    /// <summary>When true, clear the naming regex (disable enforcement).</summary>
    public bool? ClearGpoNamingRegex { get; init; }
}

public enum WelcomeWizardStatus
{
    Pending = 0,
    Completed = 1,
    Skipped = 2
}

/// <summary>
/// Policy-area guard (Referenz-Tools Restricted Policies). Area-level only — not setting-level.
/// Allowed trustees ⇒ implicit deny for all other editors; check-in blocked on violation.
/// </summary>
public sealed class RestrictedPolicyArea
{
    public Guid Id { get; init; } = Guid.NewGuid();
    /// <summary>Policy tree path prefix, e.g. /Computer/Policies/Windows Settings/Security Settings</summary>
    public required string AreaPath { get; init; }
    public string? Label { get; init; }
    /// <summary>AD groups/users allowed to edit this area. Empty = deny everyone except Product Admin / Break Glass.</summary>
    public List<string> AllowedTrustees { get; set; } = [];
    public required string CreatedBy { get; init; }
    public DateTimeOffset CreatedAt { get; init; } = DateTimeOffset.UtcNow;
}

public sealed class WelcomeWizardView
{
    public WelcomeWizardStatus Status { get; init; }
    public bool IsDismissed => Status is WelcomeWizardStatus.Completed or WelcomeWizardStatus.Skipped;
    public bool CanManage { get; init; }
    public IReadOnlyList<WelcomeWizardStep> Steps { get; init; } = [];
    public DateTimeOffset? CompletedAt { get; init; }
    public string? CompletedBy { get; init; }
}

public sealed class WelcomeWizardStep
{
    public required string Id { get; init; }
    public required string Title { get; init; }
    public required string Description { get; init; }
    public bool Done { get; init; }
    public bool Optional { get; init; }
}
