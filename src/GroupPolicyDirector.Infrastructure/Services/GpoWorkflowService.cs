using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using GroupPolicyDirector.Core.Domain;
using GroupPolicyDirector.Core.Workflow;
using GroupPolicyDirector.Infrastructure.Persistence;

namespace GroupPolicyDirector.Infrastructure.Services;

/// <summary>
/// Slice 1 workflow stub: Take Control, Checkout, read-only settings tree, Check-in, History.
/// AD/GPO ACL lockdown and real Temp-GPO creation are placeholders for domain-joined deploy.
/// </summary>
public sealed class GpoWorkflowService(GpdStore store, IAuditService audit, IOobDetectionService oob) : IGpoWorkflowService
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

        var hash = ComputeHash($"{productionGpoId}|{displayName}|baseline");
        var gpo = new ControlledGpo
        {
            DomainId = domainId,
            ProductionGpoId = productionGpoId,
            DisplayName = displayName,
            SettingsHash = hash,
            TakenControlBy = actor,
            Status = ControlledGpoStatus.UnderControl
        };

        // Placeholder: production ACL lockdown via service account (Take Control).
        lock (store.Sync)
        {
            store.Gpos.Add(gpo);
            store.Versions.Add(new GpoVersion
            {
                ControlledGpoId = gpo.Id,
                VersionNumber = 1,
                Comment = "Initial controlled baseline (Take Control)",
                CreatedBy = actor,
                SettingsHash = hash,
                SettingsSnapshotJson = JsonSerializer.Serialize(BuildSampleTree(displayName)),
                Provenance = "GPD"
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
        ControlledGpo gpo;
        lock (store.Sync)
        {
            gpo = store.Gpos.FirstOrDefault(g => g.Id == controlledGpoId)
                  ?? throw new KeyNotFoundException("Controlled GPO not found.");

            if (gpo.Status == ControlledGpoStatus.CheckedOut)
            {
                throw new InvalidOperationException($"Already checked out by {gpo.CheckedOutBy}.");
            }
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
            gpo.Status = ControlledGpoStatus.CheckedOut;
            gpo.CheckedOutBy = actor;
            gpo.CheckedOutAt = DateTimeOffset.UtcNow;
            gpo.TempGpoId = $"TEMP-{{{Guid.NewGuid():D}}}";
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
        ControlledGpo gpo;
        lock (store.Sync)
        {
            gpo = store.Gpos.FirstOrDefault(g => g.Id == controlledGpoId)
                  ?? throw new KeyNotFoundException("Controlled GPO not found.");
        }

        // Slice 1: read-only sample tree. Real path: read Temp/Production via GPMC APIs / Registry.pol.
        return Task.FromResult(BuildSampleTree(gpo.DisplayName));
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

            var next = store.Versions.Count(v => v.ControlledGpoId == controlledGpoId) + 1;
            var snapshot = BuildSampleTree(gpo.DisplayName);
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
                Provenance = "GPD"
            };

            store.Versions.Add(version);
            gpo.SettingsHash = hash;
            gpo.Status = ControlledGpoStatus.UnderControl;
            gpo.CheckedOutBy = null;
            gpo.CheckedOutAt = null;
            gpo.TempGpoId = null;
        }

        await audit.WriteAsync(new AuditEvent
        {
            Actor = actor,
            Action = "Gpo.CheckIn",
            ObjectKey = gpo.ProductionGpoId,
            Outcome = "Success",
            DetailJson = JsonSerializer.Serialize(new { version.VersionNumber, version.Comment })
        }, ct);

        return version;
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

    private static PolicyTreeNode BuildSampleTree(string displayName) =>
        new()
        {
            Path = "/",
            Name = displayName,
            Children =
            [
                new PolicyTreeNode
                {
                    Path = "/Computer",
                    Name = "Computer Configuration",
                    Children =
                    [
                        new PolicyTreeNode
                        {
                            Path = "/Computer/Policies",
                            Name = "Policies",
                            Children =
                            [
                                new PolicyTreeNode
                                {
                                    Path = "/Computer/Policies/Windows Settings",
                                    Name = "Windows Settings",
                                    Children =
                                    [
                                        new PolicyTreeNode
                                        {
                                            Path = "/Computer/Policies/Windows Settings/Security Settings",
                                            Name = "Security Settings",
                                            State = "Configured",
                                            Value = "(sample — read-only stub)"
                                        }
                                    ]
                                }
                            ]
                        },
                        new PolicyTreeNode
                        {
                            Path = "/Computer/Preferences",
                            Name = "Preferences",
                            Children = []
                        }
                    ]
                },
                new PolicyTreeNode
                {
                    Path = "/User",
                    Name = "User Configuration",
                    Children =
                    [
                        new PolicyTreeNode
                        {
                            Path = "/User/Policies",
                            Name = "Policies",
                            Children = []
                        }
                    ]
                }
            ]
        };

    private static string ComputeHash(string input)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(input));
        return Convert.ToHexString(bytes);
    }
}
