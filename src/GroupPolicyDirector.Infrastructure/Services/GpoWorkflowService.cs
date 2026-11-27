using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using GroupPolicyDirector.Core.Authz;
using GroupPolicyDirector.Core.Domain;
using GroupPolicyDirector.Core.Workflow;
using GroupPolicyDirector.Infrastructure.Persistence;

namespace GroupPolicyDirector.Infrastructure.Services;

/// <summary>
/// Slice 2 workflow: Take Control → Checkout → Edit Settings → Check-in → Approve → Deploy.
/// SoD: Editor who authored the pending version cannot Approve or Deploy.
/// AD/GPO ACL lockdown and real Temp-GPO creation remain placeholders without a domain.
/// </summary>
public sealed class GpoWorkflowService(
    GpdStore store,
    IAuditService audit,
    IOobDetectionService oob,
    IDelegationService delegation) : IGpoWorkflowService
{
    public Task<IReadOnlyList<ControlledGpo>> ListControlledAsync(CancellationToken ct = default)
    {
        lock (store.Sync)
        {
            IReadOnlyList<ControlledGpo> items = store.Gpos.ToList();
            return Task.FromResult(items);
        }
    }

    public async Task<ControlledGpo> TakeControlAsync(
        Guid domainId,
        string productionGpoId,
        string displayName,
        string actor,
        CancellationToken ct = default)
    {
        await delegation.EnsureDemoTrusteesSeededAsync(ct);

        lock (store.Sync)
        {
            if (!store.Domains.Any(d => d.Id == domainId))
            {
                throw new InvalidOperationException("Domain is not registered.");
            }

            if (store.Gpos.Any(g => g.ProductionGpoId.Equals(productionGpoId, StringComparison.OrdinalIgnoreCase)))
            {
                throw new InvalidOperationException($"GPO '{productionGpoId}' is already under control.");
            }
        }

        var tree = PolicyTreeFactory.CreateBaseline(displayName);
        var json = JsonSerializer.Serialize(tree);
        var hash = ComputeHash(json);
        var gpo = new ControlledGpo
        {
            DomainId = domainId,
            ProductionGpoId = productionGpoId,
            DisplayName = displayName,
            SettingsHash = hash,
            TakenControlBy = actor,
            Status = ControlledGpoStatus.UnderControl
        };

        lock (store.Sync)
        {
            store.Gpos.Add(gpo);
            store.SettingsTrees[gpo.Id] = tree;
            store.Versions.Add(new GpoVersion
            {
                ControlledGpoId = gpo.Id,
                VersionNumber = 1,
                Comment = "Initial controlled baseline (Take Control)",
                CreatedBy = actor,
                SettingsHash = hash,
                SettingsSnapshotJson = json,
                Provenance = "GPD",
                WorkflowState = "Deployed"
            });
        }

        await audit.WriteAsync(new AuditEvent
        {
            Actor = actor,
            Action = "Gpo.TakeControl",
            ObjectKey = productionGpoId,
            Outcome = "Success",
            DetailJson = JsonSerializer.Serialize(new { gpo.DisplayName, note = "ACL lockdown stub — domain-joined worker required" })
        }, ct);

        return gpo;
    }

    public async Task<ControlledGpo> CheckoutAsync(
        Guid controlledGpoId,
        string actor,
        bool checkoutAnyway = false,
        CancellationToken ct = default)
    {
        await delegation.EnsureDemoTrusteesSeededAsync(ct);

        ControlledGpo gpo;
        lock (store.Sync)
        {
            gpo = store.Gpos.FirstOrDefault(g => g.Id == controlledGpoId)
                  ?? throw new KeyNotFoundException("Controlled GPO not found.");

            if (gpo.Status is ControlledGpoStatus.CheckedOut)
            {
                throw new InvalidOperationException($"Already checked out by {gpo.CheckedOutBy}.");
            }

            if (gpo.Status is ControlledGpoStatus.WaitingApproval or ControlledGpoStatus.Approved)
            {
                throw new InvalidOperationException($"Cannot checkout while status is {gpo.Status}.");
            }
        }

        if (!await delegation.HasObjectRoleAsync(actor, gpo.ProductionGpoId, ObjectRole.Editor, ct) &&
            !await delegation.IsProductAdminOrBreakGlassAsync(actor, ct))
        {
            throw new UnauthorizedAccessException("Editor (or Product Admin) role required to checkout.");
        }

        var oobHit = await oob.ResolveAtCheckoutAsync(
            controlledGpoId,
            checkoutAnyway ? "CheckoutAnyway" : "PendingDecision",
            actor,
            ct);

        if (oobHit is not null && !checkoutAnyway && oobHit.Resolution == "PendingDecision")
        {
            throw new InvalidOperationException(
                "Out-of-band change detected. Review Diff, then choose Rollback to Last Controlled or Checkout anyway.");
        }

        lock (store.Sync)
        {
            if (!store.SettingsTrees.ContainsKey(gpo.Id))
            {
                store.SettingsTrees[gpo.Id] = PolicyTreeFactory.CreateBaseline(gpo.DisplayName);
            }
            else
            {
                // Working copy for temp edit (clone production tree).
                store.SettingsTrees[gpo.Id] = PolicyTreeFactory.Clone(store.SettingsTrees[gpo.Id]);
            }

            gpo.Status = ControlledGpoStatus.CheckedOut;
            gpo.CheckedOutBy = actor;
            gpo.CheckedOutAt = DateTimeOffset.UtcNow;
            gpo.TempGpoId = $"TEMP-{{{Guid.NewGuid():D}}}";
            gpo.PendingVersionId = null;
            gpo.ApprovedBy = null;
            gpo.ApprovedAt = null;
        }

        await audit.WriteAsync(new AuditEvent
        {
            Actor = actor,
            Action = "Gpo.Checkout",
            ObjectKey = gpo.ProductionGpoId,
            Outcome = "Success",
            DetailJson = JsonSerializer.Serialize(new { gpo.TempGpoId, checkoutAnyway })
        }, ct);

        return gpo;
    }

    public Task<PolicyTreeNode> GetSettingsTreeAsync(Guid controlledGpoId, CancellationToken ct = default)
    {
        lock (store.Sync)
        {
            var gpo = store.Gpos.FirstOrDefault(g => g.Id == controlledGpoId)
                      ?? throw new KeyNotFoundException("Controlled GPO not found.");

            if (!store.SettingsTrees.TryGetValue(gpo.Id, out var tree))
            {
                tree = PolicyTreeFactory.CreateBaseline(gpo.DisplayName);
                store.SettingsTrees[gpo.Id] = tree;
            }

            return Task.FromResult(PolicyTreeFactory.Clone(tree));
        }
    }

    public async Task<PolicyTreeNode> UpdateSettingsAsync(
        Guid controlledGpoId,
        IReadOnlyList<PolicySettingUpdate> updates,
        string actor,
        CancellationToken ct = default)
    {
        ControlledGpo gpo;
        lock (store.Sync)
        {
            gpo = store.Gpos.FirstOrDefault(g => g.Id == controlledGpoId)
                  ?? throw new KeyNotFoundException("Controlled GPO not found.");

            if (gpo.Status != ControlledGpoStatus.CheckedOut ||
                !string.Equals(gpo.CheckedOutBy, actor, StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException("Settings can only be edited by the user who checked out the GPO.");
            }

            if (!store.SettingsTrees.TryGetValue(gpo.Id, out var tree))
            {
                throw new InvalidOperationException("No settings tree loaded for this GPO.");
            }

            foreach (var update in updates)
            {
                if (!PolicyTreeFactory.TryUpdate(tree, update))
                {
                    throw new KeyNotFoundException($"Setting path not found: {update.Path}");
                }
            }
        }

        await audit.WriteAsync(new AuditEvent
        {
            Actor = actor,
            Action = "Gpo.UpdateSettings",
            ObjectKey = gpo.ProductionGpoId,
            Outcome = "Success",
            DetailJson = JsonSerializer.Serialize(new { count = updates.Count, paths = updates.Select(u => u.Path).ToArray() })
        }, ct);

        return await GetSettingsTreeAsync(controlledGpoId, ct);
    }

    public async Task<GpoVersion> CheckInAsync(
        Guid controlledGpoId,
        string comment,
        string actor,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(comment))
        {
            throw new ArgumentException("Check-in comment is required.", nameof(comment));
        }

        GpoVersion version;
        ControlledGpo gpo;
        lock (store.Sync)
        {
            gpo = store.Gpos.FirstOrDefault(g => g.Id == controlledGpoId)
                  ?? throw new KeyNotFoundException("Controlled GPO not found.");

            if (gpo.Status != ControlledGpoStatus.CheckedOut ||
                !string.Equals(gpo.CheckedOutBy, actor, StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException("GPO is not checked out by the current user.");
            }

            if (!store.SettingsTrees.TryGetValue(gpo.Id, out var tree))
            {
                tree = PolicyTreeFactory.CreateBaseline(gpo.DisplayName);
                store.SettingsTrees[gpo.Id] = tree;
            }

            var next = store.Versions.Count(v => v.ControlledGpoId == controlledGpoId) + 1;
            var snapshot = PolicyTreeFactory.Clone(tree);
            var json = JsonSerializer.Serialize(snapshot);
            var hash = ComputeHash(json);

            version = new GpoVersion
            {
                ControlledGpoId = controlledGpoId,
                VersionNumber = next,
                Comment = comment.Trim(),
                CreatedBy = actor,
                SettingsSnapshotJson = json,
                SettingsHash = hash,
                Provenance = "GPD",
                WorkflowState = "PendingApproval"
            };

            store.Versions.Add(version);
            gpo.SettingsHash = hash;
            gpo.Status = ControlledGpoStatus.WaitingApproval;
            gpo.PendingVersionId = version.Id;
            gpo.CheckedOutBy = null;
            gpo.CheckedOutAt = null;
            gpo.TempGpoId = null;
            gpo.ApprovedBy = null;
            gpo.ApprovedAt = null;
        }

        await audit.WriteAsync(new AuditEvent
        {
            Actor = actor,
            Action = "Gpo.CheckIn",
            ObjectKey = gpo.ProductionGpoId,
            Outcome = "Success",
            DetailJson = JsonSerializer.Serialize(new { version.VersionNumber, version.Comment, status = "WaitingApproval" })
        }, ct);

        return version;
    }

    public async Task<ControlledGpo> ApproveAsync(
        Guid controlledGpoId,
        string actor,
        string? comment = null,
        CancellationToken ct = default)
    {
        ControlledGpo gpo;
        GpoVersion version;
        lock (store.Sync)
        {
            gpo = store.Gpos.FirstOrDefault(g => g.Id == controlledGpoId)
                  ?? throw new KeyNotFoundException("Controlled GPO not found.");

            if (gpo.Status != ControlledGpoStatus.WaitingApproval || gpo.PendingVersionId is null)
            {
                throw new InvalidOperationException("GPO is not waiting for approval.");
            }

            version = store.Versions.FirstOrDefault(v => v.Id == gpo.PendingVersionId)
                      ?? throw new InvalidOperationException("Pending version not found.");
        }

        await EnsureApproverAsync(actor, gpo, version, ct);

        lock (store.Sync)
        {
            version.WorkflowState = "Approved";
            version.ReviewedBy = actor;
            version.ReviewComment = comment;
            gpo.Status = ControlledGpoStatus.Approved;
            gpo.ApprovedBy = actor;
            gpo.ApprovedAt = DateTimeOffset.UtcNow;
        }

        await audit.WriteAsync(new AuditEvent
        {
            Actor = actor,
            Action = "Gpo.Approve",
            ObjectKey = gpo.ProductionGpoId,
            Outcome = "Success",
            DetailJson = JsonSerializer.Serialize(new { version.VersionNumber, comment })
        }, ct);

        return gpo;
    }

    public async Task<ControlledGpo> RejectAsync(
        Guid controlledGpoId,
        string actor,
        string comment,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(comment))
        {
            throw new ArgumentException("Reject comment is required.", nameof(comment));
        }

        ControlledGpo gpo;
        GpoVersion version;
        lock (store.Sync)
        {
            gpo = store.Gpos.FirstOrDefault(g => g.Id == controlledGpoId)
                  ?? throw new KeyNotFoundException("Controlled GPO not found.");

            if (gpo.Status is not (ControlledGpoStatus.WaitingApproval or ControlledGpoStatus.Approved) ||
                gpo.PendingVersionId is null)
            {
                throw new InvalidOperationException("GPO has no pending version to reject.");
            }

            version = store.Versions.FirstOrDefault(v => v.Id == gpo.PendingVersionId)
                      ?? throw new InvalidOperationException("Pending version not found.");
        }

        await EnsureApproverAsync(actor, gpo, version, ct);

        lock (store.Sync)
        {
            version.WorkflowState = "Rejected";
            version.ReviewedBy = actor;
            version.ReviewComment = comment.Trim();
            gpo.Status = ControlledGpoStatus.UnderControl;
            gpo.PendingVersionId = null;
            gpo.ApprovedBy = null;
            gpo.ApprovedAt = null;
        }

        await audit.WriteAsync(new AuditEvent
        {
            Actor = actor,
            Action = "Gpo.Reject",
            ObjectKey = gpo.ProductionGpoId,
            Outcome = "Success",
            DetailJson = JsonSerializer.Serialize(new { version.VersionNumber, comment })
        }, ct);

        return gpo;
    }

    public async Task<ControlledGpo> DeployAsync(Guid controlledGpoId, string actor, CancellationToken ct = default)
    {
        ControlledGpo gpo;
        GpoVersion version;
        lock (store.Sync)
        {
            gpo = store.Gpos.FirstOrDefault(g => g.Id == controlledGpoId)
                  ?? throw new KeyNotFoundException("Controlled GPO not found.");

            if (gpo.Status != ControlledGpoStatus.Approved || gpo.PendingVersionId is null)
            {
                throw new InvalidOperationException("GPO must be Approved before Deploy.");
            }

            version = store.Versions.FirstOrDefault(v => v.Id == gpo.PendingVersionId)
                      ?? throw new InvalidOperationException("Pending version not found.");
        }

        // SoD: Editor who authored the change cannot deploy it.
        if (string.Equals(version.CreatedBy, actor, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("SoD: Editor who checked in this version cannot Deploy it.");
        }

        var isDeployer = await delegation.HasObjectRoleAsync(actor, gpo.ProductionGpoId, ObjectRole.Deployer, ct);
        var isApprover = await delegation.HasObjectRoleAsync(actor, gpo.ProductionGpoId, ObjectRole.Approver, ct);
        // Combined Approver+Deployer mode (Referenz-Tool default): Approver may also deploy if granted Deployer OR Approver-only when deployer grant exists separately.
        if (!isDeployer && !isApprover)
        {
            // Product Admin is not a Deployer by default (SoD / Referenz-Tools: admin configures, does not self-deploy without role).
            throw new UnauthorizedAccessException("Deployer (or Approver in combined mode) role required.");
        }

        if (!isDeployer && isApprover)
        {
            // Allow Approver to deploy only when no separate Deployer grant exists on OU/GPO (combined mode).
            var hasSeparateDeployer = await HasAnyDeployerGrantAsync(gpo.ProductionGpoId, ct);
            if (hasSeparateDeployer)
            {
                throw new UnauthorizedAccessException("Separate Deployer role is configured; Approver cannot Deploy.");
            }
        }

        lock (store.Sync)
        {
            // Apply approved snapshot as production tree (live deploy stub — no AD write without domain).
            if (!string.IsNullOrWhiteSpace(version.SettingsSnapshotJson))
            {
                var applied = JsonSerializer.Deserialize<PolicyTreeNode>(version.SettingsSnapshotJson);
                if (applied is not null)
                {
                    store.SettingsTrees[gpo.Id] = applied;
                }
            }

            version.WorkflowState = "Deployed";
            gpo.SettingsHash = version.SettingsHash;
            gpo.Status = ControlledGpoStatus.UnderControl;
            gpo.PendingVersionId = null;
            // Keep ApprovedBy for audit trail on object briefly — clear after deploy for next cycle.
            gpo.ApprovedBy = null;
            gpo.ApprovedAt = null;
        }

        await audit.WriteAsync(new AuditEvent
        {
            Actor = actor,
            Action = "Gpo.Deploy",
            ObjectKey = gpo.ProductionGpoId,
            Outcome = "Success",
            DetailJson = JsonSerializer.Serialize(new
            {
                version.VersionNumber,
                note = "Production apply stub — domain-joined worker writes Temp → Production"
            })
        }, ct);

        return gpo;
    }

    public Task<IReadOnlyList<GpoVersion>> GetHistoryAsync(Guid controlledGpoId, CancellationToken ct = default)
    {
        lock (store.Sync)
        {
            IReadOnlyList<GpoVersion> items = store.Versions
                .Where(v => v.ControlledGpoId == controlledGpoId)
                .OrderByDescending(v => v.VersionNumber)
                .ToList();
            return Task.FromResult(items);
        }
    }

    private async Task EnsureApproverAsync(string actor, ControlledGpo gpo, GpoVersion version, CancellationToken ct)
    {
        if (string.Equals(version.CreatedBy, actor, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("SoD: Editor who checked in this version cannot Approve/Reject it.");
        }

        var isApprover = await delegation.HasObjectRoleAsync(actor, gpo.ProductionGpoId, ObjectRole.Approver, ct);
        if (!isApprover)
        {
            throw new UnauthorizedAccessException("Approver role required.");
        }
    }

    private Task<bool> HasAnyDeployerGrantAsync(string productionGpoId, CancellationToken ct)
    {
        lock (store.Sync)
        {
            var hit = store.Delegations.Any(d =>
                d.Role == ObjectRole.Deployer &&
                (d.ScopeKind == DelegationScopeKind.Ou ||
                 (d.ScopeKind == DelegationScopeKind.Gpo &&
                  d.ScopeKey.Equals(productionGpoId, StringComparison.OrdinalIgnoreCase))));
            return Task.FromResult(hit);
        }
    }

    private static string ComputeHash(string input)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(input));
        return Convert.ToHexString(bytes);
    }
}
