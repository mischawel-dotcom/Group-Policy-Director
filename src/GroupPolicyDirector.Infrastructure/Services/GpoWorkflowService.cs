using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using GroupPolicyDirector.Core.Ad;
using GroupPolicyDirector.Core.Authz;
using GroupPolicyDirector.Core.Domain;
using GroupPolicyDirector.Core.Workflow;
using GroupPolicyDirector.Infrastructure.Persistence;

namespace GroupPolicyDirector.Infrastructure.Services;

/// <summary>
/// Slice 3 workflow: Take Control → Checkout (Temp GPO) → Edit → Check-in → Approve → Deploy.
/// AD/GPMC via <see cref="IGpoDirectoryService"/> (live LDAP when domain-joined, otherwise dry-run).
/// SoD: Editor who authored the pending version cannot Approve or Deploy.
/// </summary>
public sealed class GpoWorkflowService(
    GpdStore store,
    IAuditService audit,
    IOobDetectionService oob,
    IDelegationService delegation,
    IGpoDirectoryService directory) : IGpoWorkflowService
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

        DomainRegistration domain;
        lock (store.Sync)
        {
            domain = store.Domains.FirstOrDefault(d => d.Id == domainId)
                     ?? throw new InvalidOperationException("Domain is not registered.");

            if (store.Gpos.Any(g => g.ProductionGpoId.Equals(productionGpoId, StringComparison.OrdinalIgnoreCase)))
            {
                throw new InvalidOperationException($"GPO '{productionGpoId}' is already under control.");
            }
        }

        var adResult = await directory.TakeControlAsync(new TakeControlAdRequest
        {
            DomainDnsName = domain.DnsName,
            ProductionGpoId = productionGpoId,
            DisplayName = displayName,
            Actor = actor
        }, ct);

        var tree = PolicyTreeFactory.CreateBaseline(displayName);
        var json = JsonSerializer.Serialize(tree);
        // Prefer AD-reported baseline hash when present; otherwise hash of settings tree.
        var hash = !string.IsNullOrWhiteSpace(adResult.SettingsHash)
            ? adResult.SettingsHash!
            : ComputeHash(json);

        // Align dry-run live fingerprint with the controlled tree hash used by OOB
        // when AD did not supply an independent production hash.
        if (adResult.Mode == AdOperationMode.DryRun)
        {
            var treeHash = ComputeHash(json);
            await directory.SetControlledBaselineAsync(productionGpoId, treeHash, displayName, ct);
            hash = treeHash;
        }

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
            DetailJson = JsonSerializer.Serialize(new
            {
                gpo.DisplayName,
                adMode = adResult.Mode.ToString(),
                adMessage = adResult.Message,
                plannedSteps = adResult.PlannedSteps
            })
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
        DomainRegistration domain;
        lock (store.Sync)
        {
            gpo = store.Gpos.FirstOrDefault(g => g.Id == controlledGpoId)
                  ?? throw new KeyNotFoundException("Controlled GPO not found.");

            domain = store.Domains.FirstOrDefault(d => d.Id == gpo.DomainId)
                     ?? throw new InvalidOperationException("Domain registration missing for controlled GPO.");

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

        var tempResult = await directory.CreateTempGpoAsync(new TempGpoAdRequest
        {
            DomainDnsName = domain.DnsName,
            ProductionGpoId = gpo.ProductionGpoId,
            DisplayName = gpo.DisplayName,
            Actor = actor
        }, ct);

        lock (store.Sync)
        {
            if (!store.SettingsTrees.ContainsKey(gpo.Id))
            {
                store.SettingsTrees[gpo.Id] = PolicyTreeFactory.CreateBaseline(gpo.DisplayName);
            }
            else
            {
                store.SettingsTrees[gpo.Id] = PolicyTreeFactory.Clone(store.SettingsTrees[gpo.Id]);
            }

            gpo.Status = ControlledGpoStatus.CheckedOut;
            gpo.CheckedOutBy = actor;
            gpo.CheckedOutAt = DateTimeOffset.UtcNow;
            gpo.TempGpoId = tempResult.TempGpoId ?? $"TEMP-{{{Guid.NewGuid():D}}}";
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
            DetailJson = JsonSerializer.Serialize(new
            {
                gpo.TempGpoId,
                checkoutAnyway,
                adMode = tempResult.Mode.ToString(),
                adMessage = tempResult.Message,
                plannedSteps = tempResult.PlannedSteps
            })
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
        string? tempId;
        DomainRegistration? domain;
        lock (store.Sync)
        {
            gpo = store.Gpos.FirstOrDefault(g => g.Id == controlledGpoId)
                  ?? throw new KeyNotFoundException("Controlled GPO not found.");

            domain = store.Domains.FirstOrDefault(d => d.Id == gpo.DomainId);
            tempId = gpo.TempGpoId;

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
            // Temp GPO retained until Deploy (or cleaned on Reject).
            gpo.ApprovedBy = null;
            gpo.ApprovedAt = null;
        }

        await audit.WriteAsync(new AuditEvent
        {
            Actor = actor,
            Action = "Gpo.CheckIn",
            ObjectKey = gpo.ProductionGpoId,
            Outcome = "Success",
            DetailJson = JsonSerializer.Serialize(new
            {
                version.VersionNumber,
                version.Comment,
                status = "WaitingApproval",
                tempGpoId = tempId,
                domain = domain?.DnsName
            })
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
        string? tempId;
        DomainRegistration? domain;
        lock (store.Sync)
        {
            gpo = store.Gpos.FirstOrDefault(g => g.Id == controlledGpoId)
                  ?? throw new KeyNotFoundException("Controlled GPO not found.");

            domain = store.Domains.FirstOrDefault(d => d.Id == gpo.DomainId);
            tempId = gpo.TempGpoId;

            if (gpo.Status is not (ControlledGpoStatus.WaitingApproval or ControlledGpoStatus.Approved) ||
                gpo.PendingVersionId is null)
            {
                throw new InvalidOperationException("GPO has no pending version to reject.");
            }

            version = store.Versions.FirstOrDefault(v => v.Id == gpo.PendingVersionId)
                      ?? throw new InvalidOperationException("Pending version not found.");
        }

        await EnsureApproverAsync(actor, gpo, version, ct);

        if (!string.IsNullOrWhiteSpace(tempId) && domain is not null)
        {
            await directory.DeleteTempGpoAsync(domain.DnsName, tempId, actor, ct);
        }

        lock (store.Sync)
        {
            version.WorkflowState = "Rejected";
            version.ReviewedBy = actor;
            version.ReviewComment = comment.Trim();
            gpo.Status = ControlledGpoStatus.UnderControl;
            gpo.PendingVersionId = null;
            gpo.TempGpoId = null;
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
        DomainRegistration domain;
        string? tempId;
        lock (store.Sync)
        {
            gpo = store.Gpos.FirstOrDefault(g => g.Id == controlledGpoId)
                  ?? throw new KeyNotFoundException("Controlled GPO not found.");

            domain = store.Domains.FirstOrDefault(d => d.Id == gpo.DomainId)
                     ?? throw new InvalidOperationException("Domain registration missing for controlled GPO.");

            tempId = gpo.TempGpoId;

            if (gpo.Status != ControlledGpoStatus.Approved || gpo.PendingVersionId is null)
            {
                throw new InvalidOperationException("GPO must be Approved before Deploy.");
            }

            version = store.Versions.FirstOrDefault(v => v.Id == gpo.PendingVersionId)
                      ?? throw new InvalidOperationException("Pending version not found.");
        }

        if (string.Equals(version.CreatedBy, actor, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("SoD: Editor who checked in this version cannot Deploy it.");
        }

        var isDeployer = await delegation.HasObjectRoleAsync(actor, gpo.ProductionGpoId, ObjectRole.Deployer, ct);
        var isApprover = await delegation.HasObjectRoleAsync(actor, gpo.ProductionGpoId, ObjectRole.Approver, ct);
        if (!isDeployer && !isApprover)
        {
            throw new UnauthorizedAccessException("Deployer (or Approver in combined mode) role required.");
        }

        if (!isDeployer && isApprover)
        {
            var hasSeparateDeployer = await HasAnyDeployerGrantAsync(gpo.ProductionGpoId, ct);
            if (hasSeparateDeployer)
            {
                throw new UnauthorizedAccessException("Separate Deployer role is configured; Approver cannot Deploy.");
            }
        }

        var deployResult = await directory.DeployTempToProductionAsync(new DeployAdRequest
        {
            DomainDnsName = domain.DnsName,
            ProductionGpoId = gpo.ProductionGpoId,
            TempGpoId = tempId ?? "(none)",
            Actor = actor,
            SettingsSnapshotJson = version.SettingsSnapshotJson
        }, ct);

        lock (store.Sync)
        {
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
            gpo.TempGpoId = null;
            gpo.ApprovedBy = null;
            gpo.ApprovedAt = null;
        }

        // Keep dry-run live fingerprint aligned with deployed controlled hash for OOB.
        if (!string.IsNullOrWhiteSpace(version.SettingsHash))
        {
            await directory.SetControlledBaselineAsync(
                gpo.ProductionGpoId,
                version.SettingsHash!,
                gpo.DisplayName,
                ct);
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
                adMode = deployResult.Mode.ToString(),
                adMessage = deployResult.Message,
                plannedSteps = deployResult.PlannedSteps
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
