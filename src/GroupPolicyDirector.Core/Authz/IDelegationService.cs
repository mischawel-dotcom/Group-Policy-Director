using GroupPolicyDirector.Core.Domain;

namespace GroupPolicyDirector.Core.Authz;

public interface IDelegationService
{
    Task<IReadOnlyList<ProductRoleAssignment>> ListProductRolesAsync(CancellationToken ct = default);
    Task<ProductRoleAssignment> GrantProductRoleAsync(ProductRole role, string trusteePrincipal, string grantedBy, CancellationToken ct = default);
    Task<bool> RevokeProductRoleAsync(Guid assignmentId, string revokedBy, CancellationToken ct = default);
    Task<IReadOnlyList<DelegationGrant>> ListObjectDelegationsAsync(CancellationToken ct = default);
    Task<DelegationGrant> GrantObjectRoleAsync(
        DelegationScopeKind scopeKind,
        string scopeKey,
        ObjectRole role,
        string trusteePrincipal,
        string grantedBy,
        bool inheritToChildren = true,
        CancellationToken ct = default);
    Task<bool> RevokeObjectRoleAsync(Guid grantId, string revokedBy, CancellationToken ct = default);
    /// <summary>Suggested AD admin-role groups for trustees (dev stubs + known GPD groups).</summary>
    Task<IReadOnlyList<SuggestedTrustee>> ListSuggestedTrusteesAsync(CancellationToken ct = default);
    /// <summary>Known OU / container scopes for the admin UI (stub catalog).</summary>
    Task<IReadOnlyList<DelegationScopeHint>> ListScopeHintsAsync(CancellationToken ct = default);
    Task<IReadOnlyList<string>> ResolveEffectiveRolesAsync(string windowsIdentity, CancellationToken ct = default);
    /// <summary>Resolved principals for the identity (self + AD group stubs).</summary>
    Task<IReadOnlyList<string>> ResolveMembershipPrincipalsAsync(string windowsIdentity, CancellationToken ct = default);
    /// <summary>True if actor has the object role on the GPO (direct or via OU inheritance stub).</summary>
    Task<bool> HasObjectRoleAsync(string windowsIdentity, string productionGpoId, ObjectRole role, CancellationToken ct = default);
    /// <summary>True if actor holds Product Administrator.</summary>
    Task<bool> IsProductAdministratorAsync(string windowsIdentity, CancellationToken ct = default);
    /// <summary>True if actor holds the Auditor product role (read-only).</summary>
    Task<bool> IsAuditorAsync(string windowsIdentity, CancellationToken ct = default);
    /// <summary>True if actor holds ProductRole.BreakGlass (eligibility — not necessarily an active session).</summary>
    Task<bool> HasBreakGlassEligibilityAsync(string windowsIdentity, CancellationToken ct = default);
    /// <summary>
    /// Legacy helper: Product Administrator, or an <em>active</em> Break Glass session.
    /// Permanent BreakGlass product role alone does not grant admin/delegation powers.
    /// </summary>
    Task<bool> IsProductAdminOrBreakGlassAsync(string windowsIdentity, CancellationToken ct = default);
    /// <summary>Deny mutating operations when the actor is Auditor-only (no write roles).</summary>
    Task EnsureNotAuditorOnlyAsync(string windowsIdentity, CancellationToken ct = default);
    Task EnsureDemoTrusteesSeededAsync(CancellationToken ct = default);
}

public sealed class SuggestedTrustee
{
    public required string Principal { get; init; }
    public required string Kind { get; init; } // Group | User
    public string? Description { get; init; }
}

public sealed class DelegationScopeHint
{
    public required DelegationScopeKind ScopeKind { get; init; }
    public required string ScopeKey { get; init; }
    public string? Label { get; init; }
}
