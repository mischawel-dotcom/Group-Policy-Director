using System.Text.Json;
using GroupPolicyDirector.Core.Authz;
using GroupPolicyDirector.Core.Domain;
using GroupPolicyDirector.Infrastructure.Persistence;

namespace GroupPolicyDirector.Infrastructure.Services;

/// <summary>
/// Area-level Restricted Policies (Referenz-Tools parity). Allowed trustees ⇒ implicit deny for others.
/// </summary>
public sealed class RestrictedPolicyService(
    GpdStore store,
    IDelegationService delegation,
    IAuditService audit) : IRestrictedPolicyService
{
    private static readonly string[] AreaHints =
    [
        "/Computer/Policies/Windows Settings/Security Settings",
        "/Computer/Policies/Administrative Templates/Windows Components/Windows Update",
        "/Computer/Policies/Administrative Templates/Windows Components/Windows Defender Antivirus",
        "/Computer/Policies/Administrative Templates/System",
        "/Computer/Preferences",
        "/User/Policies/Administrative Templates",
        "/User/Preferences"
    ];

    public Task<IReadOnlyList<RestrictedPolicyArea>> ListAsync(CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        lock (store.Sync)
        {
            IReadOnlyList<RestrictedPolicyArea> items = store.RestrictedPolicies
                .OrderBy(r => r.AreaPath, StringComparer.OrdinalIgnoreCase)
                .ToList();
            return Task.FromResult(items);
        }
    }

    public Task<IReadOnlyList<string>> ListAreaPathHintsAsync(CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        IReadOnlyList<string> hints = AreaHints;
        return Task.FromResult(hints);
    }

    public async Task<RestrictedPolicyArea> UpsertAsync(
        string areaPath,
        IReadOnlyList<string> allowedTrustees,
        string actor,
        string? label = null,
        CancellationToken ct = default)
    {
        await delegation.EnsureNotAuditorOnlyAsync(actor, ct);
        if (!await delegation.IsProductAdministratorAsync(actor, ct))
        {
            throw new UnauthorizedAccessException("Only Product Administrators can manage Restricted Policies.");
        }

        if (string.IsNullOrWhiteSpace(areaPath) || !areaPath.Trim().StartsWith('/'))
        {
            throw new ArgumentException("AreaPath must be a policy tree path starting with '/'.");
        }

        var normalized = NormalizePath(areaPath);
        var trustees = (allowedTrustees ?? [])
            .Where(t => !string.IsNullOrWhiteSpace(t))
            .Select(t => t.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        RestrictedPolicyArea area;
        lock (store.Sync)
        {
            var existing = store.RestrictedPolicies.FirstOrDefault(r =>
                r.AreaPath.Equals(normalized, StringComparison.OrdinalIgnoreCase));
            if (existing is not null)
            {
                existing.AllowedTrustees = trustees;
                if (!string.IsNullOrWhiteSpace(label))
                {
                    // Label is init-only on entity — replace entry.
                    store.RestrictedPolicies.Remove(existing);
                    area = new RestrictedPolicyArea
                    {
                        Id = existing.Id,
                        AreaPath = normalized,
                        Label = label.Trim(),
                        AllowedTrustees = trustees,
                        CreatedBy = existing.CreatedBy,
                        CreatedAt = existing.CreatedAt
                    };
                    store.RestrictedPolicies.Add(area);
                }
                else
                {
                    area = existing;
                }
            }
            else
            {
                area = new RestrictedPolicyArea
                {
                    AreaPath = normalized,
                    Label = string.IsNullOrWhiteSpace(label) ? null : label.Trim(),
                    AllowedTrustees = trustees,
                    CreatedBy = actor
                };
                store.RestrictedPolicies.Add(area);
            }
        }

        await audit.WriteAsync(new AuditEvent
        {
            Actor = actor,
            Action = "RestrictedPolicy.Upsert",
            ObjectKey = normalized,
            Outcome = "Success",
            DetailJson = JsonSerializer.Serialize(new { area.Id, trustees })
        }, ct);

        return area;
    }

    public async Task<bool> DeleteAsync(Guid id, string actor, CancellationToken ct = default)
    {
        await delegation.EnsureNotAuditorOnlyAsync(actor, ct);
        if (!await delegation.IsProductAdministratorAsync(actor, ct))
        {
            throw new UnauthorizedAccessException("Only Product Administrators can manage Restricted Policies.");
        }

        RestrictedPolicyArea? removed;
        lock (store.Sync)
        {
            removed = store.RestrictedPolicies.FirstOrDefault(r => r.Id == id);
            if (removed is not null)
            {
                store.RestrictedPolicies.Remove(removed);
            }
        }

        if (removed is null)
        {
            return false;
        }

        await audit.WriteAsync(new AuditEvent
        {
            Actor = actor,
            Action = "RestrictedPolicy.Delete",
            ObjectKey = removed.AreaPath,
            Outcome = "Success",
            DetailJson = JsonSerializer.Serialize(new { removed.Id })
        }, ct);

        return true;
    }

    public async Task<bool> CanEditPathAsync(string windowsIdentity, string settingPath, CancellationToken ct = default)
    {
        RestrictedPolicyArea? matching;
        lock (store.Sync)
        {
            matching = FindMatchingArea(settingPath);
            if (matching is null)
            {
                return true; // not restricted
            }
        }

        // Product Admin always; active Break Glass session bypasses area guards.
        if (await delegation.IsProductAdminOrBreakGlassAsync(windowsIdentity, ct))
        {
            return true;
        }

        var principals = await delegation.ResolveMembershipPrincipalsAsync(windowsIdentity, ct);
        return matching.AllowedTrustees.Any(t =>
            principals.Any(p => p.Equals(t, StringComparison.OrdinalIgnoreCase)));
    }

    public async Task EnsurePathsEditableAsync(
        string windowsIdentity,
        IEnumerable<string> settingPaths,
        CancellationToken ct = default)
    {
        var blocked = new List<string>();
        foreach (var path in settingPaths.Distinct(StringComparer.OrdinalIgnoreCase))
        {
            if (!await CanEditPathAsync(windowsIdentity, path, ct))
            {
                blocked.Add(path);
            }
        }

        if (blocked.Count > 0)
        {
            var areas = blocked
                .Select(p =>
                {
                    lock (store.Sync)
                    {
                        return FindMatchingArea(p)?.AreaPath ?? p;
                    }
                })
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray();

            throw new UnauthorizedAccessException(
                "Restricted Policy area denied for this editor. Areas: " + string.Join(", ", areas));
        }
    }

    public async Task EnsureCheckInAllowedAsync(
        string windowsIdentity,
        PolicyTreeNode? baseline,
        PolicyTreeNode working,
        CancellationToken ct = default)
    {
        if (baseline is null)
        {
            return;
        }

        var changed = PolicyTreeFactory.CollectChangedPaths(baseline, working);
        if (changed.Count == 0)
        {
            return;
        }

        await EnsurePathsEditableAsync(windowsIdentity, changed, ct);
    }

    private RestrictedPolicyArea? FindMatchingArea(string settingPath)
    {
        var path = NormalizePath(settingPath);
        return store.RestrictedPolicies
            .Where(r => path.Equals(r.AreaPath, StringComparison.OrdinalIgnoreCase) ||
                        path.StartsWith(r.AreaPath.TrimEnd('/') + "/", StringComparison.OrdinalIgnoreCase))
            .OrderByDescending(r => r.AreaPath.Length)
            .FirstOrDefault();
    }

    private static string NormalizePath(string path) =>
        path.Trim().TrimEnd('/');
}
