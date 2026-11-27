using System.Text.Json;
using GroupPolicyDirector.Core.Authz;
using GroupPolicyDirector.Core.Domain;
using GroupPolicyDirector.Infrastructure.Auth;
using GroupPolicyDirector.Infrastructure.Persistence;

namespace GroupPolicyDirector.Infrastructure.Services;

/// <summary>
/// Referenz-Tools-like delegation: Product Admin assigns OU/GPO object roles to AD admin groups.
/// Group membership evaluation at login uses stubs when not domain-joined.
/// Active Break Glass sessions are read from the store (time-bound SoD bypass).
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
        await EnsureNotAuditorOnlyAsync(grantedBy, ct);
        if (!await IsProductAdministratorAsync(grantedBy, ct))
        {
            throw new InvalidOperationException("Only Product Administrator may grant product roles.");
        }

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

    public async Task<bool> RevokeProductRoleAsync(Guid assignmentId, string revokedBy, CancellationToken ct = default)
    {
        EnsureBootstrap();
        await EnsureNotAuditorOnlyAsync(revokedBy, ct);
        if (!await IsProductAdministratorAsync(revokedBy, ct))
        {
            throw new InvalidOperationException("Only Product Administrator may revoke product roles.");
        }

        ProductRoleAssignment? removed;
        lock (store.Sync)
        {
            removed = store.ProductRoles.FirstOrDefault(p => p.Id == assignmentId);
            if (removed is null)
            {
                return false;
            }

            store.ProductRoles.Remove(removed);
        }

        await audit.WriteAsync(new AuditEvent
        {
            Actor = revokedBy,
            Action = "Authz.RevokeProductRole",
            ObjectKey = removed.TrusteePrincipal,
            Outcome = "Success",
            DetailJson = JsonSerializer.Serialize(new { removed.Role, assignmentId })
        }, ct);

        return true;
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
        await EnsureNotAuditorOnlyAsync(grantedBy, ct);
        if (!await IsProductAdministratorAsync(grantedBy, ct))
        {
            throw new InvalidOperationException("Only Product Administrator may grant OU/GPO object roles.");
        }

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

    public async Task<bool> RevokeObjectRoleAsync(Guid grantId, string revokedBy, CancellationToken ct = default)
    {
        EnsureBootstrap();
        await EnsureNotAuditorOnlyAsync(revokedBy, ct);
        if (!await IsProductAdministratorAsync(revokedBy, ct))
        {
            throw new InvalidOperationException("Only Product Administrator may revoke OU/GPO object roles.");
        }

        DelegationGrant? removed;
        lock (store.Sync)
        {
            removed = store.Delegations.FirstOrDefault(d => d.Id == grantId);
            if (removed is null)
            {
                return false;
            }

            store.Delegations.Remove(removed);
        }

        await audit.WriteAsync(new AuditEvent
        {
            Actor = revokedBy,
            Action = "Authz.RevokeObjectRole",
            ObjectKey = removed.ScopeKey,
            Outcome = "Success",
            DetailJson = JsonSerializer.Serialize(new
            {
                removed.ScopeKind,
                removed.Role,
                removed.TrusteePrincipal,
                grantId
            })
        }, ct);

        return true;
    }

    public Task<IReadOnlyList<SuggestedTrustee>> ListSuggestedTrusteesAsync(CancellationToken ct = default)
    {
        EnsureBootstrap();
        IReadOnlyList<SuggestedTrustee> items =
        [
            new SuggestedTrustee
            {
                Principal = WindowsAuthPlaceholders.ProductAdminsGroup,
                Kind = "Group",
                Description = "Product Administrators (AD admin-role group)"
            },
            new SuggestedTrustee
            {
                Principal = WindowsAuthPlaceholders.EditorsGroup,
                Kind = "Group",
                Description = "Editors — Checkout/Edit/Check-in"
            },
            new SuggestedTrustee
            {
                Principal = WindowsAuthPlaceholders.ApproversGroup,
                Kind = "Group",
                Description = "Approvers — Approve/Reject"
            },
            new SuggestedTrustee
            {
                Principal = WindowsAuthPlaceholders.DeployersGroup,
                Kind = "Group",
                Description = "Deployers — Deploy to production"
            },
            new SuggestedTrustee
            {
                Principal = WindowsAuthPlaceholders.BreakGlassGroup,
                Kind = "Group",
                Description = "Break Glass — time-bound emergency SoD bypass (activation required)"
            },
            new SuggestedTrustee
            {
                Principal = WindowsAuthPlaceholders.AuditorsGroup,
                Kind = "Group",
                Description = "Auditors — read-only objects / diffs / audit"
            },
            new SuggestedTrustee
            {
                Principal = @"CONTOSO\Domain Admins",
                Kind = "Group",
                Description = "Example existing AD admin-role group (optional trustee)"
            }
        ];
        return Task.FromResult(items);
    }

    public Task<IReadOnlyList<DelegationScopeHint>> ListScopeHintsAsync(CancellationToken ct = default)
    {
        EnsureBootstrap();
        lock (store.Sync)
        {
            var hints = new List<DelegationScopeHint>
            {
                new()
                {
                    ScopeKind = DelegationScopeKind.Ou,
                    ScopeKey = "OU=Workstations,DC=contoso,DC=local",
                    Label = "Workstations OU"
                },
                new()
                {
                    ScopeKind = DelegationScopeKind.Ou,
                    ScopeKey = "OU=Servers,DC=contoso,DC=local",
                    Label = "Servers OU"
                }
            };

            foreach (var gpo in store.Gpos)
            {
                hints.Add(new DelegationScopeHint
                {
                    ScopeKind = DelegationScopeKind.Gpo,
                    ScopeKey = gpo.ProductionGpoId,
                    Label = gpo.DisplayName
                });
            }

            foreach (var container in store.Containers)
            {
                hints.Add(new DelegationScopeHint
                {
                    ScopeKind = DelegationScopeKind.Container,
                    ScopeKey = container.ContainerKey,
                    Label = $"{container.Kind}: {container.DisplayName}"
                });
            }

            if (store.Containers.Count == 0)
            {
                hints.Add(new DelegationScopeHint
                {
                    ScopeKind = DelegationScopeKind.Container,
                    ScopeKey = "OU=Workstations,DC=contoso,DC=local",
                    Label = "Demo OU container (gpLink)"
                });
                hints.Add(new DelegationScopeHint
                {
                    ScopeKind = DelegationScopeKind.Container,
                    ScopeKey = "DC=contoso,DC=local",
                    Label = "Demo Domain container (gpLink)"
                });
                hints.Add(new DelegationScopeHint
                {
                    ScopeKind = DelegationScopeKind.Container,
                    ScopeKey = "CN=Default-First-Site-Name,CN=Sites,CN=Configuration,DC=contoso,DC=local",
                    Label = "Demo Site container (gpLink)"
                });
            }

            IReadOnlyList<DelegationScopeHint> result = hints;
            return Task.FromResult(result);
        }
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

    public async Task<bool> IsProductAdministratorAsync(string windowsIdentity, CancellationToken ct = default)
    {
        var roles = await ResolveEffectiveRolesAsync(windowsIdentity, ct);
        return roles.Any(r => r.Equals("Product:ProductAdministrator", StringComparison.OrdinalIgnoreCase));
    }

    public async Task<bool> IsAuditorAsync(string windowsIdentity, CancellationToken ct = default)
    {
        var roles = await ResolveEffectiveRolesAsync(windowsIdentity, ct);
        return roles.Any(r => r.Equals("Product:Auditor", StringComparison.OrdinalIgnoreCase));
    }

    public async Task<bool> CanCreateGpoAsync(string windowsIdentity, CancellationToken ct = default)
    {
        if (await IsProductAdministratorAsync(windowsIdentity, ct))
        {
            return true;
        }

        var roles = await ResolveEffectiveRolesAsync(windowsIdentity, ct);
        return roles.Any(r => r.Equals("Product:GpoCreator", StringComparison.OrdinalIgnoreCase));
    }

    public async Task<bool> HasBreakGlassEligibilityAsync(string windowsIdentity, CancellationToken ct = default)
    {
        var roles = await ResolveEffectiveRolesAsync(windowsIdentity, ct);
        return roles.Any(r => r.Equals("Product:BreakGlass", StringComparison.OrdinalIgnoreCase));
    }

    public async Task<bool> IsProductAdminOrBreakGlassAsync(string windowsIdentity, CancellationToken ct = default)
    {
        if (await IsProductAdministratorAsync(windowsIdentity, ct))
        {
            return true;
        }

        return await HasActiveBreakGlassSessionAsync(windowsIdentity, ct);
    }

    public async Task EnsureNotAuditorOnlyAsync(string windowsIdentity, CancellationToken ct = default)
    {
        if (!await IsAuditorAsync(windowsIdentity, ct))
        {
            return;
        }

        if (await IsProductAdministratorAsync(windowsIdentity, ct) ||
            await HasActiveBreakGlassSessionAsync(windowsIdentity, ct))
        {
            return;
        }

        // Auditor with any object role may still perform that role's mutations;
        // Auditor-only (product role alone) is read-only.
        var roles = await ResolveEffectiveRolesAsync(windowsIdentity, ct);
        if (roles.Any(r => r.StartsWith("Object:", StringComparison.OrdinalIgnoreCase)))
        {
            return;
        }

        throw new UnauthorizedAccessException("Auditor is read-only across objects, diffs, and audit.");
    }

    public async Task<bool> HasObjectRoleAsync(
        string windowsIdentity,
        string productionGpoId,
        ObjectRole role,
        CancellationToken ct = default)
    {
        EnsureBootstrap();

        if (await HasActiveBreakGlassSessionAsync(windowsIdentity, ct))
        {
            // Active Break Glass bypasses all object-role checks (emergency SoD bypass).
            return true;
        }

        if (await IsProductAdministratorAsync(windowsIdentity, ct) && role == ObjectRole.Editor)
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
                 || d.ScopeKind == DelegationScopeKind.Container &&
                 d.ScopeKey.Equals(productionGpoId, StringComparison.OrdinalIgnoreCase)
                 || d.ScopeKind == DelegationScopeKind.Ou)); // OU inheritance stub: any OU grant applies
        }
    }

    private async Task<bool> HasActiveBreakGlassSessionAsync(string windowsIdentity, CancellationToken ct)
    {
        var principals = await ResolveMembershipPrincipalsAsync(windowsIdentity, ct);
        lock (store.Sync)
        {
            var now = DateTimeOffset.UtcNow;
            return store.BreakGlassSessions.Any(s =>
                s.RevokedAt is null &&
                s.ExpiresAt > now &&
                principals.Any(p => p.Equals(s.TrusteePrincipal, StringComparison.OrdinalIgnoreCase)));
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
                store.ProductRoles.Add(new ProductRoleAssignment
                {
                    Role = ProductRole.BreakGlass,
                    TrusteePrincipal = WindowsAuthPlaceholders.BreakGlassGroup,
                    GrantedBy = "bootstrap"
                });
                store.ProductRoles.Add(new ProductRoleAssignment
                {
                    Role = ProductRole.Auditor,
                    TrusteePrincipal = WindowsAuthPlaceholders.AuditorsGroup,
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
                    },
                    new AdGroupMembershipStub
                    {
                        UserPrincipal = WindowsAuthPlaceholders.DevBreakGlass,
                        GroupPrincipal = WindowsAuthPlaceholders.BreakGlassGroup
                    },
                    new AdGroupMembershipStub
                    {
                        UserPrincipal = WindowsAuthPlaceholders.DevAuditor,
                        GroupPrincipal = WindowsAuthPlaceholders.AuditorsGroup
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
