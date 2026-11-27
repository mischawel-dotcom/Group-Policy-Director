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
}
