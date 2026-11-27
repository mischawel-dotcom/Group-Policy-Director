using GroupPolicyDirector.Core.Domain;

namespace GroupPolicyDirector.Core.Authz;

public interface IDelegationService
{
    Task<IReadOnlyList<ProductRoleAssignment>> ListProductRolesAsync(CancellationToken ct = default);
    Task<ProductRoleAssignment> GrantProductRoleAsync(ProductRole role, string trusteePrincipal, string grantedBy, CancellationToken ct = default);
    Task<IReadOnlyList<DelegationGrant>> ListObjectDelegationsAsync(CancellationToken ct = default);
    Task<DelegationGrant> GrantObjectRoleAsync(
        DelegationScopeKind scopeKind,
        string scopeKey,
        ObjectRole role,
        string trusteePrincipal,
        string grantedBy,
        bool inheritToChildren = true,
        CancellationToken ct = default);
    Task<IReadOnlyList<string>> ResolveEffectiveRolesAsync(string windowsIdentity, CancellationToken ct = default);
    /// <summary>Resolved principals for the identity (self + AD group stubs).</summary>
    Task<IReadOnlyList<string>> ResolveMembershipPrincipalsAsync(string windowsIdentity, CancellationToken ct = default);
    /// <summary>True if actor has the object role on the GPO (direct or via OU inheritance stub).</summary>
    Task<bool> HasObjectRoleAsync(string windowsIdentity, string productionGpoId, ObjectRole role, CancellationToken ct = default);
    /// <summary>True if actor is Product Administrator or Break Glass.</summary>
    Task<bool> IsProductAdminOrBreakGlassAsync(string windowsIdentity, CancellationToken ct = default);
    Task EnsureDemoTrusteesSeededAsync(CancellationToken ct = default);
}
