using System.Text.Json;
using GroupPolicyDirector.Core.Ad;
using GroupPolicyDirector.Core.Authz;
using GroupPolicyDirector.Core.Domain;
using GroupPolicyDirector.Core.Workflow;
using GroupPolicyDirector.Infrastructure.Ad;
using GroupPolicyDirector.Infrastructure.Persistence;

namespace GroupPolicyDirector.Infrastructure.Services;

/// <summary>
/// Slice 5: AD Container (OU/Site/Domain) gpLink change-control —
/// Take Control → Checkout → Edit Links → Check-in → Approve → Deploy.
/// </summary>
public sealed class ContainerWorkflowService(
    GpdStore store,
    IAuditService audit,
    IDelegationService delegation,
    IBreakGlassService breakGlass,
    IContainerDirectoryService directory,
    IDiffService diff) : IContainerWorkflowService
{
    public Task<IReadOnlyList<ControlledContainer>> ListAsync(CancellationToken ct = default)
    {
        lock (store.Sync)
        {
            IReadOnlyList<ControlledContainer> items = store.Containers.ToList();
            return Task.FromResult(items);
        }
    }

    public async Task<ControlledContainer> TakeControlAsync(
        Guid domainId,
        AdContainerKind kind,
        string containerKey,
        string displayName,
        string actor,
        CancellationToken ct = default)
    {
        await delegation.EnsureDemoTrusteesSeededAsync(ct);
        await delegation.EnsureNotAuditorOnlyAsync(actor, ct);

        if (string.IsNullOrWhiteSpace(containerKey))
        {
            throw new ArgumentException("Container key (DN) is required.", nameof(containerKey));
        }

        DomainRegistration domain;
        lock (store.Sync)
        {
            domain = store.Domains.FirstOrDefault(d => d.Id == domainId)
                     ?? throw new InvalidOperationException("Domain is not registered.");

            if (store.Containers.Any(c => c.ContainerKey.Equals(containerKey, StringComparison.OrdinalIgnoreCase)))
            {
                throw new InvalidOperationException($"Container '{containerKey}' is already under control.");
            }
        }

        var adResult = await directory.TakeControlAsync(new TakeControlContainerAdRequest
        {
            DomainDnsName = domain.DnsName,
            ContainerKey = containerKey.Trim(),
            DisplayName = displayName,
            Actor = actor,
            Kind = kind
        }, ct);

        var fingerprint = await directory.ReadLinksAsync(domain.DnsName, containerKey.Trim(), ct);
        var links = fingerprint?.Links ?? AdaptiveContainerDirectoryService.CloneLinks(new ContainerLinksState
        {
            BlockInheritance = false,
            Links = []
        });
        links = AdaptiveContainerDirectoryService.Normalize(links);
        var hash = !string.IsNullOrWhiteSpace(adResult.SettingsHash)
            ? adResult.SettingsHash!
            : AdaptiveContainerDirectoryService.ComputeLinksHash(links);

        var container = new ControlledContainer
        {
            DomainId = domainId,
            Kind = kind,
            ContainerKey = containerKey.Trim(),
            DisplayName = displayName,
            LinksHash = hash,
            TakenControlBy = actor,
            Status = ControlledGpoStatus.UnderControl
        };

        lock (store.Sync)
        {
            store.Containers.Add(container);
            store.ContainerLinks[container.Id] = AdaptiveContainerDirectoryService.CloneLinks(links);
            store.ContainerVersions.Add(new ContainerVersion
            {
                ControlledContainerId = container.Id,
                VersionNumber = 1,
                Comment = "Initial controlled baseline (Take Control)",
                CreatedBy = actor,
                LinksHash = hash,
                LinksSnapshotJson = JsonSerializer.Serialize(links),
                Provenance = "GPD",
                WorkflowState = "Deployed"
            });
        }

        await audit.WriteAsync(new AuditEvent
        {
            Actor = actor,
            Action = "Container.TakeControl",
            ObjectKey = container.ContainerKey,
            Outcome = "Success",
            DetailJson = JsonSerializer.Serialize(new
            {
                container.DisplayName,
                kind = kind.ToString(),
                adMode = adResult.Mode.ToString(),
                adMessage = adResult.Message,
                plannedSteps = adResult.PlannedSteps,
                linkCount = links.Links.Count
            })
        }, ct);

        return container;
    }

    public async Task<ControlledContainer> CheckoutAsync(Guid containerId, string actor, CancellationToken ct = default)
    {
        await delegation.EnsureDemoTrusteesSeededAsync(ct);

        ControlledContainer container;
        lock (store.Sync)
        {
            container = store.Containers.FirstOrDefault(c => c.Id == containerId)
                        ?? throw new KeyNotFoundException("Controlled container not found.");

            if (container.Status is ControlledGpoStatus.CheckedOut)
            {
                throw new InvalidOperationException($"Already checked out by {container.CheckedOutBy}.");
            }

            if (container.Status is ControlledGpoStatus.WaitingApproval or ControlledGpoStatus.Approved or ControlledGpoStatus.Scheduled)
            {
                throw new InvalidOperationException($"Cannot checkout while status is {container.Status}.");
            }
        }

        await delegation.EnsureNotAuditorOnlyAsync(actor, ct);
        if (!await delegation.HasObjectRoleAsync(actor, container.ContainerKey, ObjectRole.Editor, ct) &&
            !await HasOuEditorAsync(actor, container, ct) &&
            !await delegation.IsProductAdministratorAsync(actor, ct))
        {
            throw new UnauthorizedAccessException("Editor (or Product Admin / active Break Glass) role required to checkout container.");
        }

        await AuditBreakGlassUseAsync(actor, "Container.Checkout", container.ContainerKey, ct);

        lock (store.Sync)
        {
            if (!store.ContainerLinks.ContainsKey(container.Id))
            {
                store.ContainerLinks[container.Id] = new ContainerLinksState();
            }
            else
            {
                store.ContainerLinks[container.Id] =
                    AdaptiveContainerDirectoryService.CloneLinks(store.ContainerLinks[container.Id]);
            }

            container.Status = ControlledGpoStatus.CheckedOut;
            container.CheckedOutBy = actor;
            container.CheckedOutAt = DateTimeOffset.UtcNow;
            container.PendingVersionId = null;
            container.ApprovedBy = null;
            container.ApprovedAt = null;
            container.ActiveScheduleId = null;
        }

        await audit.WriteAsync(new AuditEvent
        {
            Actor = actor,
            Action = "Container.Checkout",
            ObjectKey = container.ContainerKey,
            Outcome = "Success"
        }, ct);

        return container;
    }

    public Task<ContainerLinksState> GetLinksAsync(Guid containerId, CancellationToken ct = default)
    {
        lock (store.Sync)
        {
            var container = store.Containers.FirstOrDefault(c => c.Id == containerId)
                            ?? throw new KeyNotFoundException("Controlled container not found.");

            if (!store.ContainerLinks.TryGetValue(container.Id, out var links))
            {
                links = new ContainerLinksState();
                store.ContainerLinks[container.Id] = links;
            }

            return Task.FromResult(AdaptiveContainerDirectoryService.CloneLinks(links));
        }
    }

    public async Task<ContainerLinksState> UpdateLinksAsync(
        Guid containerId,
        ContainerLinksState links,
        string actor,
        CancellationToken ct = default)
    {
        await delegation.EnsureNotAuditorOnlyAsync(actor, ct);
        ControlledContainer container;
        lock (store.Sync)
        {
            container = store.Containers.FirstOrDefault(c => c.Id == containerId)
                        ?? throw new KeyNotFoundException("Controlled container not found.");

            if (container.Status != ControlledGpoStatus.CheckedOut ||
                !string.Equals(container.CheckedOutBy, actor, StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException("Links can only be edited by the user who checked out the container.");
            }

            store.ContainerLinks[container.Id] = AdaptiveContainerDirectoryService.Normalize(links);
        }

        await audit.WriteAsync(new AuditEvent
        {
            Actor = actor,
            Action = "Container.UpdateLinks",
            ObjectKey = container.ContainerKey,
            Outcome = "Success",
            DetailJson = JsonSerializer.Serialize(new
            {
                linkCount = links.Links.Count,
                blockInheritance = links.BlockInheritance
            })
        }, ct);

        return await GetLinksAsync(containerId, ct);
    }

    public async Task<ContainerVersion> CheckInAsync(
        Guid containerId,
        string comment,
        string actor,
        CancellationToken ct = default)
    {
        await delegation.EnsureNotAuditorOnlyAsync(actor, ct);
        if (string.IsNullOrWhiteSpace(comment))
        {
            throw new ArgumentException("Check-in comment is required.", nameof(comment));
        }

        ContainerVersion version;
        ControlledContainer container;
        lock (store.Sync)
        {
            container = store.Containers.FirstOrDefault(c => c.Id == containerId)
                        ?? throw new KeyNotFoundException("Controlled container not found.");

            if (container.Status != ControlledGpoStatus.CheckedOut ||
                !string.Equals(container.CheckedOutBy, actor, StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException("Container is not checked out by the current user.");
            }

            if (!store.ContainerLinks.TryGetValue(container.Id, out var links))
            {
                links = new ContainerLinksState();
                store.ContainerLinks[container.Id] = links;
            }

            links = AdaptiveContainerDirectoryService.Normalize(links);
            store.ContainerLinks[container.Id] = links;
            var next = store.ContainerVersions.Count(v => v.ControlledContainerId == containerId) + 1;
            var json = JsonSerializer.Serialize(links);
            var hash = AdaptiveContainerDirectoryService.ComputeLinksHash(links);

            version = new ContainerVersion
            {
                ControlledContainerId = containerId,
                VersionNumber = next,
                Comment = comment.Trim(),
                CreatedBy = actor,
                LinksSnapshotJson = json,
                LinksHash = hash,
                Provenance = "GPD",
                WorkflowState = "PendingApproval"
            };

            store.ContainerVersions.Add(version);
            container.LinksHash = hash;
            container.Status = ControlledGpoStatus.WaitingApproval;
            container.PendingVersionId = version.Id;
            container.CheckedOutBy = null;
            container.CheckedOutAt = null;
            container.ApprovedBy = null;
            container.ApprovedAt = null;
        }

        await audit.WriteAsync(new AuditEvent
        {
            Actor = actor,
            Action = "Container.CheckIn",
            ObjectKey = container.ContainerKey,
            Outcome = "Success",
            DetailJson = JsonSerializer.Serialize(new { version.VersionNumber, version.Comment })
        }, ct);

        return version;
    }

    public async Task<ControlledContainer> ApproveAsync(
        Guid containerId,
        string actor,
        string? comment = null,
        CancellationToken ct = default)
    {
        ControlledContainer container;
        ContainerVersion version;
        lock (store.Sync)
        {
            container = store.Containers.FirstOrDefault(c => c.Id == containerId)
                        ?? throw new KeyNotFoundException("Controlled container not found.");

            if (container.Status != ControlledGpoStatus.WaitingApproval || container.PendingVersionId is null)
            {
                throw new InvalidOperationException("Container is not waiting for approval.");
            }

            version = store.ContainerVersions.FirstOrDefault(v => v.Id == container.PendingVersionId)
                      ?? throw new InvalidOperationException("Pending version not found.");
        }

        await EnsureApproverAsync(actor, container, version, ct);

        lock (store.Sync)
        {
            version.WorkflowState = "Approved";
            version.ReviewedBy = actor;
            version.ReviewComment = comment;
            container.Status = ControlledGpoStatus.Approved;
            container.ApprovedBy = actor;
            container.ApprovedAt = DateTimeOffset.UtcNow;
        }

        await audit.WriteAsync(new AuditEvent
        {
            Actor = actor,
            Action = "Container.Approve",
            ObjectKey = container.ContainerKey,
            Outcome = "Success",
            DetailJson = JsonSerializer.Serialize(new { version.VersionNumber, comment })
        }, ct);

        return container;
    }

    public async Task<ControlledContainer> RejectAsync(
        Guid containerId,
        string actor,
        string comment,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(comment))
        {
            throw new ArgumentException("Reject comment is required.", nameof(comment));
        }

        ControlledContainer container;
        ContainerVersion version;
        lock (store.Sync)
        {
            container = store.Containers.FirstOrDefault(c => c.Id == containerId)
                        ?? throw new KeyNotFoundException("Controlled container not found.");

            if (container.Status is not (ControlledGpoStatus.WaitingApproval or ControlledGpoStatus.Approved or ControlledGpoStatus.Scheduled) ||
                container.PendingVersionId is null)
            {
                throw new InvalidOperationException("Container has no pending version to reject.");
            }

            version = store.ContainerVersions.FirstOrDefault(v => v.Id == container.PendingVersionId)
                      ?? throw new InvalidOperationException("Pending version not found.");

            if (container.ActiveScheduleId is Guid schedId)
            {
                var sched = store.ScheduledDeploys.FirstOrDefault(s => s.Id == schedId);
                if (sched is not null && sched.Status == ScheduledDeployStatus.Pending)
                {
                    sched.Status = ScheduledDeployStatus.Cancelled;
                    sched.CancelledAt = DateTimeOffset.UtcNow;
                    sched.CancelledBy = actor;
                    sched.CompletedAt = DateTimeOffset.UtcNow;
                    sched.ErrorMessage = "Cancelled by Reject";
                }
            }
        }

        await EnsureApproverAsync(actor, container, version, ct);

        lock (store.Sync)
        {
            version.WorkflowState = "Rejected";
            version.ReviewedBy = actor;
            version.ReviewComment = comment.Trim();
            container.Status = ControlledGpoStatus.UnderControl;
            container.PendingVersionId = null;
            container.ApprovedBy = null;
            container.ApprovedAt = null;
            container.ActiveScheduleId = null;
        }

        await audit.WriteAsync(new AuditEvent
        {
            Actor = actor,
            Action = "Container.Reject",
            ObjectKey = container.ContainerKey,
            Outcome = "Success",
            DetailJson = JsonSerializer.Serialize(new { version.VersionNumber, comment })
        }, ct);

        return container;
    }

    public Task<ControlledContainer> DeployAsync(Guid containerId, string actor, CancellationToken ct = default) =>
        DeployAsync(containerId, actor, new DeployOptions(), ct);

    public async Task EnsureCanDeployAsync(Guid containerId, string actor, CancellationToken ct = default)
    {
        ControlledContainer container;
        ContainerVersion version;
        lock (store.Sync)
        {
            container = store.Containers.FirstOrDefault(c => c.Id == containerId)
                        ?? throw new KeyNotFoundException("Controlled container not found.");

            if (container.Status is not (ControlledGpoStatus.Approved or ControlledGpoStatus.Scheduled) ||
                container.PendingVersionId is null)
            {
                throw new InvalidOperationException("Container must be Approved (or Scheduled) before Deploy.");
            }

            version = store.ContainerVersions.FirstOrDefault(v => v.Id == container.PendingVersionId)
                      ?? throw new InvalidOperationException("Pending version not found.");
        }

        await EnsureDeployerAsync(actor, container, version, ct);
    }

    public async Task<ControlledContainer> DeployAsync(
        Guid containerId,
        string actor,
        DeployOptions options,
        CancellationToken ct = default)
    {
        ControlledContainer container;
        ContainerVersion version;
        DomainRegistration domain;
        ContainerLinksState links;
        ContainerVersion? previousDeployed;
        lock (store.Sync)
        {
            container = store.Containers.FirstOrDefault(c => c.Id == containerId)
                        ?? throw new KeyNotFoundException("Controlled container not found.");

            domain = store.Domains.FirstOrDefault(d => d.Id == container.DomainId)
                     ?? throw new InvalidOperationException("Domain registration missing for controlled container.");

            if (container.Status is not (ControlledGpoStatus.Approved or ControlledGpoStatus.Scheduled) ||
                container.PendingVersionId is null)
            {
                throw new InvalidOperationException("Container must be Approved (or Scheduled) before Deploy.");
            }

            version = store.ContainerVersions.FirstOrDefault(v => v.Id == container.PendingVersionId)
                      ?? throw new InvalidOperationException("Pending version not found.");

            links = string.IsNullOrWhiteSpace(version.LinksSnapshotJson)
                ? new ContainerLinksState()
                : JsonSerializer.Deserialize<ContainerLinksState>(version.LinksSnapshotJson!) ?? new ContainerLinksState();

            previousDeployed = store.ContainerVersions
                .Where(v => v.ControlledContainerId == containerId &&
                            string.Equals(v.WorkflowState, "Deployed", StringComparison.OrdinalIgnoreCase))
                .OrderByDescending(v => v.VersionNumber)
                .FirstOrDefault();
        }

        await EnsureDeployerAsync(actor, container, version, ct);
        await AuditBreakGlassUseAsync(actor, "Container.Deploy", container.ContainerKey, ct);

        lock (store.Sync)
        {
            store.ContainerLinks[container.Id] = AdaptiveContainerDirectoryService.Normalize(links);
        }

        AdOperationResult deployResult;
        try
        {
            if (options.SimulateFailure)
            {
                throw new InvalidOperationException("Simulated deploy failure (Slice 8 smoke).");
            }

            deployResult = await directory.DeployLinksAsync(new DeployContainerLinksAdRequest
            {
                DomainDnsName = domain.DnsName,
                ContainerKey = container.ContainerKey,
                Actor = actor,
                Links = links
            }, ct);

            if (!deployResult.Success)
            {
                throw new InvalidOperationException(deployResult.Message ?? "AD container Deploy reported failure.");
            }
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            if (options.RollbackOnFailure)
            {
                await RollbackContainerAsync(container, previousDeployed, actor, ex.Message, ct);
                throw new InvalidOperationException(
                    $"Deploy failed and was rolled back to last controlled links: {ex.Message}", ex);
            }

            lock (store.Sync)
            {
                container.Status = ControlledGpoStatus.Approved;
                container.ActiveScheduleId = null;
                version.WorkflowState = "DeployFailed";
            }

            await audit.WriteAsync(new AuditEvent
            {
                Actor = actor,
                Action = "Container.Deploy",
                ObjectKey = container.ContainerKey,
                Outcome = "Failed",
                DetailJson = JsonSerializer.Serialize(new
                {
                    version.VersionNumber,
                    error = ex.Message,
                    rollbackOnFailure = false
                })
            }, ct);

            throw;
        }

        lock (store.Sync)
        {
            version.WorkflowState = "Deployed";
            container.LinksHash = version.LinksHash ?? deployResult.SettingsHash;
            container.Status = ControlledGpoStatus.UnderControl;
            container.PendingVersionId = null;
            container.ApprovedBy = null;
            container.ApprovedAt = null;
            container.ActiveScheduleId = null;
        }

        await audit.WriteAsync(new AuditEvent
        {
            Actor = actor,
            Action = "Container.Deploy",
            ObjectKey = container.ContainerKey,
            Outcome = "Success",
            DetailJson = JsonSerializer.Serialize(new
            {
                version.VersionNumber,
                adMode = deployResult.Mode.ToString(),
                adMessage = deployResult.Message,
                plannedSteps = deployResult.PlannedSteps,
                rollbackOnFailure = options.RollbackOnFailure
            })
        }, ct);

        return container;
    }

    private async Task RollbackContainerAsync(
        ControlledContainer container,
        ContainerVersion? previousDeployed,
        string actor,
        string failureMessage,
        CancellationToken ct)
    {
        DomainRegistration? domain;
        ContainerLinksState restoreLinks;
        string? restoreHash;
        int? restoreVersion;
        lock (store.Sync)
        {
            domain = store.Domains.FirstOrDefault(d => d.Id == container.DomainId);
            restoreLinks = string.IsNullOrWhiteSpace(previousDeployed?.LinksSnapshotJson)
                ? new ContainerLinksState()
                : JsonSerializer.Deserialize<ContainerLinksState>(previousDeployed!.LinksSnapshotJson!)
                  ?? new ContainerLinksState();
            restoreLinks = AdaptiveContainerDirectoryService.Normalize(restoreLinks);
            restoreHash = previousDeployed?.LinksHash ?? container.LinksHash;
            restoreVersion = previousDeployed?.VersionNumber;

            store.ContainerLinks[container.Id] = AdaptiveContainerDirectoryService.CloneLinks(restoreLinks);
            container.LinksHash = restoreHash;
            container.Status = ControlledGpoStatus.UnderControl;
            container.PendingVersionId = null;
            container.ApprovedBy = null;
            container.ApprovedAt = null;
            container.ActiveScheduleId = null;

            var pending = store.ContainerVersions.FirstOrDefault(v =>
                v.ControlledContainerId == container.Id &&
                string.Equals(v.WorkflowState, "Approved", StringComparison.OrdinalIgnoreCase));
            if (pending is not null)
            {
                pending.WorkflowState = "DeployFailed";
            }
        }

        if (domain is not null)
        {
            await directory.DeployLinksAsync(new DeployContainerLinksAdRequest
            {
                DomainDnsName = domain.DnsName,
                ContainerKey = container.ContainerKey,
                Actor = actor,
                Links = restoreLinks
            }, ct);
        }

        await audit.WriteAsync(new AuditEvent
        {
            Actor = actor,
            Action = "Container.DeployRollback",
            ObjectKey = container.ContainerKey,
            Outcome = "Success",
            DetailJson = JsonSerializer.Serialize(new
            {
                failureMessage,
                restoredVersion = restoreVersion,
                restoredHash = restoreHash
            })
        }, ct);
    }

    private async Task EnsureDeployerAsync(
        string actor,
        ControlledContainer container,
        ContainerVersion version,
        CancellationToken ct)
    {
        await delegation.EnsureNotAuditorOnlyAsync(actor, ct);
        var breakGlassActive = await breakGlass.HasActiveBreakGlassAsync(actor, ct);
        if (!breakGlassActive && string.Equals(version.CreatedBy, actor, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("SoD: Editor who checked in this version cannot Deploy it.");
        }

        var isDeployer = await delegation.HasObjectRoleAsync(actor, container.ContainerKey, ObjectRole.Deployer, ct)
                         || await HasOuRoleAsync(actor, container, ObjectRole.Deployer, ct);
        var isApprover = await delegation.HasObjectRoleAsync(actor, container.ContainerKey, ObjectRole.Approver, ct)
                         || await HasOuRoleAsync(actor, container, ObjectRole.Approver, ct);
        if (!isDeployer && !isApprover && !breakGlassActive)
        {
            throw new UnauthorizedAccessException("Deployer (or Approver in combined mode) role required.");
        }
    }

    public Task<IReadOnlyList<ContainerVersion>> GetHistoryAsync(Guid containerId, CancellationToken ct = default)
    {
        lock (store.Sync)
        {
            IReadOnlyList<ContainerVersion> items = store.ContainerVersions
                .Where(v => v.ControlledContainerId == containerId)
                .OrderByDescending(v => v.VersionNumber)
                .ToList();
            return Task.FromResult(items);
        }
    }

    public Task<VersionDiffResult> DiffVersionsAsync(
        Guid containerId,
        int fromVersion,
        int toVersion,
        CancellationToken ct = default)
    {
        lock (store.Sync)
        {
            var container = store.Containers.FirstOrDefault(c => c.Id == containerId)
                            ?? throw new KeyNotFoundException("Controlled container not found.");

            var from = store.ContainerVersions.FirstOrDefault(v =>
                           v.ControlledContainerId == containerId && v.VersionNumber == fromVersion)
                       ?? throw new KeyNotFoundException($"Version {fromVersion} not found.");
            var to = store.ContainerVersions.FirstOrDefault(v =>
                         v.ControlledContainerId == containerId && v.VersionNumber == toVersion)
                     ?? throw new KeyNotFoundException($"Version {toVersion} not found.");

            return Task.FromResult(diff.DiffSnapshots(
                container.ContainerKey,
                "Container",
                fromVersion,
                toVersion,
                from.LinksSnapshotJson,
                to.LinksSnapshotJson,
                linksMode: true));
        }
    }

    private async Task EnsureApproverAsync(
        string actor,
        ControlledContainer container,
        ContainerVersion version,
        CancellationToken ct)
    {
        await delegation.EnsureNotAuditorOnlyAsync(actor, ct);
        var breakGlassActive = await breakGlass.HasActiveBreakGlassAsync(actor, ct);
        if (!breakGlassActive && string.Equals(version.CreatedBy, actor, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("SoD: Editor who checked in this version cannot Approve/Reject it.");
        }

        var isApprover = await delegation.HasObjectRoleAsync(actor, container.ContainerKey, ObjectRole.Approver, ct)
                         || await HasOuRoleAsync(actor, container, ObjectRole.Approver, ct);
        if (!isApprover && !breakGlassActive)
        {
            throw new UnauthorizedAccessException("Approver role required.");
        }

        await AuditBreakGlassUseAsync(actor, "Container.ApproveOrReject", container.ContainerKey, ct);
    }

    private async Task AuditBreakGlassUseAsync(string actor, string workflowAction, string objectKey, CancellationToken ct)
    {
        var session = await breakGlass.GetActiveSessionAsync(actor, ct);
        if (session is null)
        {
            return;
        }

        await audit.WriteAsync(new AuditEvent
        {
            Actor = actor,
            Action = "BreakGlass.Use",
            ObjectKey = objectKey,
            Outcome = "Success",
            DetailJson = JsonSerializer.Serialize(new
            {
                session.Id,
                session.Reason,
                session.ExpiresAt,
                workflowAction
            })
        }, ct);
    }

    private Task<bool> HasOuEditorAsync(string actor, ControlledContainer container, CancellationToken ct) =>
        HasOuRoleAsync(actor, container, ObjectRole.Editor, ct);

    private async Task<bool> HasOuRoleAsync(
        string actor,
        ControlledContainer container,
        ObjectRole role,
        CancellationToken ct)
    {
        // OU-scoped grants apply to containers whose key matches or is under that OU DN.
        if (await delegation.HasObjectRoleAsync(actor, container.ContainerKey, role, ct))
        {
            return true;
        }

        // Also accept demo GPO-scoped editors for the seeded demo GPO when container links reference it —
        // Product Admin / Break Glass already covered. Fall back: any OU Editor grant.
        lock (store.Sync)
        {
            var principals = store.GroupMemberships
                .Where(m => m.UserPrincipal.Equals(actor, StringComparison.OrdinalIgnoreCase))
                .Select(m => m.GroupPrincipal)
                .Append(actor)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

            return store.Delegations.Any(d =>
                d.Role == role &&
                principals.Contains(d.TrusteePrincipal) &&
                (d.ScopeKind == DelegationScopeKind.Ou || d.ScopeKind == DelegationScopeKind.Container) &&
                (container.ContainerKey.EndsWith(d.ScopeKey, StringComparison.OrdinalIgnoreCase) ||
                 container.ContainerKey.Equals(d.ScopeKey, StringComparison.OrdinalIgnoreCase) ||
                 d.ScopeKey.Equals(container.ContainerKey, StringComparison.OrdinalIgnoreCase)));
        }
    }

}
