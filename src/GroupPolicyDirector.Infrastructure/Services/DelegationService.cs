using System.Text.Json;
using GroupPolicyDirector.Core.Authz;
using GroupPolicyDirector.Core.Domain;
using GroupPolicyDirector.Infrastructure.Auth;
using GroupPolicyDirector.Infrastructure.Persistence;

namespace GroupPolicyDirector.Infrastructure.Services;

/// <summary>
/// Referenz-Tools-like delegation: Product Admin assigns OU/GPO object roles to AD admin groups.
/// Group membership evaluation at login uses stubs when not domain-joined.
/// </summary>
public sealed class DelegationService(GpdStore store, IAuditService audit) : IDelegationService
{
    public Task EnsureDemoTrusteesSeededAsync(CancellationToken ct = default)
    {
        EnsureBootstrap();
        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<ProductRoleAssignment>> ListProductRolesAsync(CancellationToken ct = default)
    {
        EnsureBootstrap();
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
        EnsureBootstrap();
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
        EnsureBootstrap();
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
        EnsureBootstrap();

        // SoD: Editor ≠ Approver/Deployer on same object for the same trustee.
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

    public async Task<IReadOnlyList<string>> ResolveMembershipPrincipalsAsync(string windowsIdentity, CancellationToken ct = default)
    {
        EnsureBootstrap();
        await Task.CompletedTask;
        lock (store.Sync)
        {
            var principals = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { windowsIdentity };
            foreach (var m in store.GroupMemberships.Where(g =>
                         g.UserPrincipal.Equals(windowsIdentity, StringComparison.OrdinalIgnoreCase)))
            {
                principals.Add(m.GroupPrincipal);
            }

            return principals.ToList();
        }
    }

    public async Task<IReadOnlyList<string>> ResolveEffectiveRolesAsync(string windowsIdentity, CancellationToken ct = default)
    {
        EnsureBootstrap();
        var principals = await ResolveMembershipPrincipalsAsync(windowsIdentity, ct);

        lock (store.Sync)
        {
            var roles = new List<string>();
            foreach (var pr in store.ProductRoles.Where(p =>
                         principals.Any(x => x.Equals(p.TrusteePrincipal, StringComparison.OrdinalIgnoreCase))))
            {
                roles.Add($"Product:{pr.Role}");
            }

            foreach (var d in store.Delegations.Where(p =>
                         principals.Any(x => x.Equals(p.TrusteePrincipal, StringComparison.OrdinalIgnoreCase))))
            {
                roles.Add($"Object:{d.ScopeKind}:{d.ScopeKey}:{d.Role}");
            }

            IReadOnlyList<string> result = roles.Distinct().ToList();
            return result;
        }
    }

    public async Task<bool> IsProductAdminOrBreakGlassAsync(string windowsIdentity, CancellationToken ct = default)
    {
        var roles = await ResolveEffectiveRolesAsync(windowsIdentity, ct);
        return roles.Any(r =>
            r.Equals("Product:ProductAdministrator", StringComparison.OrdinalIgnoreCase) ||
            r.Equals("Product:BreakGlass", StringComparison.OrdinalIgnoreCase));
    }

    public async Task<bool> HasObjectRoleAsync(
        string windowsIdentity,
        string productionGpoId,
        ObjectRole role,
        CancellationToken ct = default)
    {
        EnsureBootstrap();
        if (await IsProductAdminOrBreakGlassAsync(windowsIdentity, ct) && role == ObjectRole.Editor)
        {
            // Product Admin may edit for bootstrap demos, but SoD still blocks self-approve/deploy.
            return true;
        }

        var principals = await ResolveMembershipPrincipalsAsync(windowsIdentity, ct);
        lock (store.Sync)
        {
            return store.Delegations.Any(d =>
                principals.Any(p => p.Equals(d.TrusteePrincipal, StringComparison.OrdinalIgnoreCase)) &&
                d.Role == role &&
                (d.ScopeKind == DelegationScopeKind.Gpo &&
                 d.ScopeKey.Equals(productionGpoId, StringComparison.OrdinalIgnoreCase)
                 || d.ScopeKind == DelegationScopeKind.Ou)); // OU inheritance stub: any OU grant applies
        }
    }

    private void EnsureBootstrap()
    {
        lock (store.Sync)
        {
            if (store.ProductRoles.Count == 0)
            {
                store.ProductRoles.Add(new ProductRoleAssignment
                {
                    Role = ProductRole.ProductAdministrator,
                    TrusteePrincipal = WindowsAuthPlaceholders.ProductAdminsGroup,
                    GrantedBy = "bootstrap"
                });
            }

            if (store.GroupMemberships.Count == 0)
            {
                store.GroupMemberships.AddRange(
                [
                    new AdGroupMembershipStub
                    {
                        UserPrincipal = WindowsAuthPlaceholders.DevProductAdmin,
                        GroupPrincipal = WindowsAuthPlaceholders.ProductAdminsGroup
                    },
                    new AdGroupMembershipStub
                    {
                        UserPrincipal = WindowsAuthPlaceholders.DevEditor,
                        GroupPrincipal = WindowsAuthPlaceholders.EditorsGroup
                    },
                    new AdGroupMembershipStub
                    {
                        UserPrincipal = WindowsAuthPlaceholders.DevApprover,
                        GroupPrincipal = WindowsAuthPlaceholders.ApproversGroup
                    },
                    new AdGroupMembershipStub
                    {
                        UserPrincipal = WindowsAuthPlaceholders.DevDeployer,
                        GroupPrincipal = WindowsAuthPlaceholders.DeployersGroup
                    }
                ]);
            }

            if (store.Delegations.Count == 0)
            {
                // Demo OU scope: AD admin-role groups as trustees (Referenz-Tools-like).
                store.Delegations.AddRange(
                [
                    new DelegationGrant
                    {
                        ScopeKind = DelegationScopeKind.Ou,
                        ScopeKey = "OU=Workstations,DC=contoso,DC=local",
                        Role = ObjectRole.Editor,
                        TrusteePrincipal = WindowsAuthPlaceholders.EditorsGroup,
                        GrantedBy = "bootstrap"
                    },
                    new DelegationGrant
                    {
                        ScopeKind = DelegationScopeKind.Ou,
                        ScopeKey = "OU=Workstations,DC=contoso,DC=local",
                        Role = ObjectRole.Approver,
                        TrusteePrincipal = WindowsAuthPlaceholders.ApproversGroup,
                        GrantedBy = "bootstrap"
                    },
                    new DelegationGrant
                    {
                        ScopeKind = DelegationScopeKind.Ou,
                        ScopeKey = "OU=Workstations,DC=contoso,DC=local",
                        Role = ObjectRole.Deployer,
                        TrusteePrincipal = WindowsAuthPlaceholders.DeployersGroup,
                        GrantedBy = "bootstrap"
                    }
                ]);
            }
        }
    }
}
