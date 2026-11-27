using System.Text.Json;
using GroupPolicyDirector.Core.Ad;
using GroupPolicyDirector.Core.Domain;
using GroupPolicyDirector.Core.Notifications;
using GroupPolicyDirector.Infrastructure.Persistence;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace GroupPolicyDirector.Infrastructure.Services;

/// <summary>
/// Out-of-band detection (mail-first): compare last controlled hash vs live production fingerprint.
/// No auto-reset in MVP / Slice 3.
/// </summary>
public sealed class OobDetectionService(
    GpdStore store,
    IAuditService audit,
    IEmailNotifier email,
    IGpoDirectoryService directory,
    IConfiguration config,
    ILogger<OobDetectionService> logger) : IOobDetectionService
{
    public async Task<IReadOnlyList<OobDetection>> ScanAsync(CancellationToken ct = default)
    {
        var found = new List<OobDetection>();
        List<(ControlledGpo Gpo, string DomainDns)> gpos;
        lock (store.Sync)
        {
            gpos = store.Gpos
                .Where(g => g.Status != ControlledGpoStatus.CheckedOut)
                .Select(g =>
                {
                    var dns = store.Domains.FirstOrDefault(d => d.Id == g.DomainId)?.DnsName ?? "";
                    return (g, dns);
                })
                .ToList();
        }

        foreach (var (gpo, domainDns) in gpos)
        {
            var fingerprint = await directory.GetProductionFingerprintAsync(domainDns, gpo.ProductionGpoId, ct);
            var productionHash = fingerprint?.SettingsHash;
            var last = gpo.SettingsHash ?? "";

            if (string.IsNullOrWhiteSpace(productionHash))
            {
                logger.LogDebug("No production fingerprint for {Gpo}; skip OOB.", gpo.DisplayName);
                continue;
            }

            if (string.Equals(productionHash, last, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            // Deduplicate: skip if unresolved detection already exists with same hashes.
            lock (store.Sync)
            {
                var duplicate = store.OobDetections.Any(o =>
                    o.ControlledGpoId == gpo.Id &&
                    (o.Resolution is null || o.Resolution == "PendingDecision") &&
                    string.Equals(o.ProductionHash, productionHash, StringComparison.OrdinalIgnoreCase) &&
                    string.Equals(o.LastControlledHash, last, StringComparison.OrdinalIgnoreCase));
                if (duplicate)
                {
                    continue;
                }
            }

            var detection = new OobDetection
            {
                ControlledGpoId = gpo.Id,
                ProductionHash = productionHash!,
                LastControlledHash = last,
                DiffSummary =
                    $"Settings hash mismatch for '{gpo.DisplayName}' " +
                    $"(live={Short(productionHash)} via {fingerprint?.Source ?? "unknown"}, " +
                    $"controlled={Short(last)}). No auto-reset."
            };

            lock (store.Sync)
            {
                store.OobDetections.Add(detection);
            }

            found.Add(detection);
            await NotifyAsync(gpo, detection, ct);
        }

        return found;
    }

    public Task<IReadOnlyList<OobDetection>> ListAsync(CancellationToken ct = default)
    {
        lock (store.Sync)
        {
            IReadOnlyList<OobDetection> items = store.OobDetections
                .OrderByDescending(o => o.DetectedAt)
                .ToList();
            return Task.FromResult(items);
        }
    }

    public async Task<OobDetection?> ResolveAtCheckoutAsync(
        Guid controlledGpoId,
        string resolution,
        string actor,
        CancellationToken ct = default)
    {
        ControlledGpo? gpo;
        string domainDns;
        lock (store.Sync)
        {
            gpo = store.Gpos.FirstOrDefault(g => g.Id == controlledGpoId);
            domainDns = gpo is null
                ? ""
                : store.Domains.FirstOrDefault(d => d.Id == gpo.DomainId)?.DnsName ?? "";
        }

        if (gpo is null)
        {
            return null;
        }

        // Fresh live compare at checkout (not only prior scan results).
        var fingerprint = await directory.GetProductionFingerprintAsync(domainDns, gpo.ProductionGpoId, ct);
        var productionHash = fingerprint?.SettingsHash;
        var last = gpo.SettingsHash ?? "";
        var drifted = !string.IsNullOrWhiteSpace(productionHash) &&
                      !string.Equals(productionHash, last, StringComparison.OrdinalIgnoreCase);

        OobDetection? hit;
        lock (store.Sync)
        {
            hit = store.OobDetections
                .Where(o => o.ControlledGpoId == controlledGpoId &&
                            (o.Resolution is null || o.Resolution == "PendingDecision"))
                .OrderByDescending(o => o.DetectedAt)
                .FirstOrDefault();

            if (drifted)
            {
                if (hit is null)
                {
                    hit = new OobDetection
                    {
                        ControlledGpoId = controlledGpoId,
                        ProductionHash = productionHash!,
                        LastControlledHash = last,
                        DiffSummary =
                            $"OOB change detected at checkout for '{gpo.DisplayName}' " +
                            $"(live via {fingerprint?.Source ?? "unknown"}).",
                        Resolution = resolution
                    };
                    store.OobDetections.Add(hit);
                }
                else
                {
                    hit.Resolution = resolution;
                    hit.ProductionHash = productionHash!;
                    hit.LastControlledHash = last;
                    hit.DiffSummary =
                        $"OOB change detected at checkout for '{gpo.DisplayName}' " +
                        $"(live via {fingerprint?.Source ?? "unknown"}).";
                }
            }
            else if (hit is not null)
            {
                // Prior unresolved detection but live now matches — clear for checkout.
                hit.Resolution = "ClearedAtCheckout";
                hit = null;
            }
        }

        if (hit is not null)
        {
            await audit.WriteAsync(new AuditEvent
            {
                Actor = actor,
                Action = "Oob.CheckoutDecision",
                ObjectKey = gpo.ProductionGpoId,
                Outcome = resolution,
                DetailJson = JsonSerializer.Serialize(new { hit.DiffSummary, hit.ProductionHash, hit.LastControlledHash })
            }, ct);

            if (resolution == "PendingDecision")
            {
                await NotifyAsync(gpo, hit, ct);
            }
        }

        return hit;
    }

    private async Task NotifyAsync(ControlledGpo gpo, OobDetection detection, CancellationToken ct)
    {
        var recipients = config.GetSection("Oob:AlertRecipients").GetChildren()
                             .Select(c => c.Value)
                             .Where(v => !string.IsNullOrWhiteSpace(v))
                             .Cast<string>()
                             .ToArray();
        if (recipients.Length == 0)
        {
            recipients = ["gpd-admins@contoso.local"];
        }

        try
        {
            await email.SendAsync(new EmailMessage
            {
                To = recipients,
                Subject = $"[GPD] Out-of-band change: {gpo.DisplayName}",
                Body =
                    $"{detection.DiffSummary}\n\n" +
                    $"Production GPO: {gpo.ProductionGpoId}\n" +
                    "Open Group Policy Director to choose Rollback to Last Controlled or Checkout anyway.\n" +
                    "Auto-reset is NOT enabled (MVP mail-first)."
            }, ct);
            detection.EmailSent = true;
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "OOB email failed for {Gpo}", gpo.DisplayName);
        }

        await audit.WriteAsync(new AuditEvent
        {
            Actor = "system:oob-scanner",
            Action = "Oob.Detected",
            ObjectKey = gpo.ProductionGpoId,
            Outcome = detection.EmailSent ? "Notified" : "DetectedNoMail",
            DetailJson = JsonSerializer.Serialize(new
            {
                detection.DiffSummary,
                detection.EmailSent,
                detection.ProductionHash,
                detection.LastControlledHash
            })
        }, ct);
    }

    private static string Short(string? hash) =>
        string.IsNullOrEmpty(hash) ? "(none)" : hash.Length <= 12 ? hash : hash[..12] + "…";
}
