using System.Text.Json;
using GroupPolicyDirector.Core.Authz;
using GroupPolicyDirector.Core.Domain;
using GroupPolicyDirector.Infrastructure.Auth;
using GroupPolicyDirector.Infrastructure.Persistence;

namespace GroupPolicyDirector.Infrastructure.Services;

/// <summary>
/// CMGPI-like delegation: Product Admin assigns OU/GPO object roles to AD admin groups.
/// Group membership evaluation at login is stubbed for Slice 1.
/// </summary>
public sealed class DelegationService(GpdStore store, IAuditService audit) : IDelegationService
{
    public Task<IReadOnlyList<ProductRoleAssignment>> ListProductRolesAsync(CancellationToken ct = default)
    {
        EnsureBootstrapProductAdmin();
        lock (store.Sync)
        {
            IReadOnlyList<ProductRoleAssignment> items = store.ProductRoles.ToList();
            return Task.FromResult(items);
        }
    }

    public async Task<ProductRoleAssignment> GrantProductRoleAsync(
        ProductRole role,
        string trusteePrincipal,
        string grantedBy,
        CancellationToken ct = default)
    {
        var assignment = new ProductRoleAssignment
        {
            Role = role,
            TrusteePrincipal = trusteePrincipal.Trim(),
            GrantedBy = grantedBy
        };

        lock (store.Sync)
        {
            store.ProductRoles.Add(assignment);
        }

        await audit.WriteAsync(new AuditEvent
        {
            Actor = grantedBy,
            Action = "Authz.GrantProductRole",
            ObjectKey = trusteePrincipal,
            Outcome = "Success",
            DetailJson = JsonSerializer.Serialize(new { role })
        }, ct);

        return assignment;
    }

    public Task<IReadOnlyList<DelegationGrant>> ListObjectDelegationsAsync(CancellationToken ct = default)
    {
        lock (store.Sync)
        {
            IReadOnlyList<DelegationGrant> items = store.Delegations.ToList();
            return Task.FromResult(items);
        }
    }

    public async Task<DelegationGrant> GrantObjectRoleAsync(
        DelegationScopeKind scopeKind,
        string scopeKey,
        ObjectRole role,
        string trusteePrincipal,
        string grantedBy,
        bool inheritToChildren = true,
        CancellationToken ct = default)
    {
        // SoD note: Editor ≠ Approver/Deployer on same object — enforced lightly here.
        lock (store.Sync)
        {
            var conflicting = store.Delegations.Where(d =>
                d.ScopeKind == scopeKind &&
                d.ScopeKey.Equals(scopeKey, StringComparison.OrdinalIgnoreCase) &&
                d.TrusteePrincipal.Equals(trusteePrincipal, StringComparison.OrdinalIgnoreCase));

            if (role == ObjectRole.Editor &&
                conflicting.Any(d => d.Role is ObjectRole.Approver or ObjectRole.Deployer))
            {
                throw new InvalidOperationException("SoD: trustee already has Approver/Deployer on this object.");
            }

            if (role is ObjectRole.Approver or ObjectRole.Deployer &&
                conflicting.Any(d => d.Role == ObjectRole.Editor))
            {
                throw new InvalidOperationException("SoD: trustee already has Editor on this object.");
            }
        }

        var grant = new DelegationGrant
        {
            ScopeKind = scopeKind,
            ScopeKey = scopeKey.Trim(),
            Role = role,
            TrusteePrincipal = trusteePrincipal.Trim(),
            InheritToChildren = inheritToChildren,
            GrantedBy = grantedBy
        };

        lock (store.Sync)
        {
            store.Delegations.Add(grant);
        }

        await audit.WriteAsync(new AuditEvent
        {
            Actor = grantedBy,
            Action = "Authz.GrantObjectRole",
            ObjectKey = scopeKey,
            Outcome = "Success",
            DetailJson = JsonSerializer.Serialize(new { scopeKind, role, trusteePrincipal, inheritToChildren })
        }, ct);

        return grant;
    }

    public Task<IReadOnlyList<string>> ResolveEffectiveRolesAsync(string windowsIdentity, CancellationToken ct = default)
    {
        EnsureBootstrapProductAdmin();

        // Slice 1 stub: treat exact trustee matches as membership; real path expands AD group token groups at login.
        lock (store.Sync)
        {
            var roles = new List<string>();
            foreach (var pr in store.ProductRoles.Where(p =>
                         p.TrusteePrincipal.Equals(windowsIdentity, StringComparison.OrdinalIgnoreCase)))
            {
                roles.Add($"Product:{pr.Role}");
            }

            foreach (var d in store.Delegations.Where(p =>
                         p.TrusteePrincipal.Equals(windowsIdentity, StringComparison.OrdinalIgnoreCase)))
            {
                roles.Add($"Object:{d.ScopeKind}:{d.ScopeKey}:{d.Role}");
            }

            // Also match known AD group placeholders for the dev admin.
            if (windowsIdentity.Equals(WindowsAuthPlaceholders.DevProductAdmin, StringComparison.OrdinalIgnoreCase))
            {
                if (!roles.Contains("Product:ProductAdministrator"))
                {
                    roles.Add("Product:ProductAdministrator");
                }
            }

            IReadOnlyList<string> result = roles.Distinct().ToList();
            return Task.FromResult(result);
        }
    }

    private void EnsureBootstrapProductAdmin()
    {
        lock (store.Sync)
        {
            if (store.ProductRoles.Count == 0)
            {
                store.ProductRoles.Add(new ProductRoleAssignment
                {
                    Role = ProductRole.ProductAdministrator,
                    TrusteePrincipal = WindowsAuthPlaceholders.DevProductAdmin,
                    GrantedBy = "bootstrap"
                });
            }
        }
    }
}
