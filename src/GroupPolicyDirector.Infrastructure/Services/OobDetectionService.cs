using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using GroupPolicyDirector.Core.Domain;
using GroupPolicyDirector.Core.Notifications;
using GroupPolicyDirector.Infrastructure.Persistence;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace GroupPolicyDirector.Infrastructure.Services;

/// <summary>
/// Out-of-band detection (mail-first). No auto-reset in Slice 1 / MVP.
/// </summary>
public sealed class OobDetectionService(
    GpdStore store,
    IAuditService audit,
    IEmailNotifier email,
    IConfiguration config,
    ILogger<OobDetectionService> logger) : IOobDetectionService
{
    public async Task<IReadOnlyList<OobDetection>> ScanAsync(CancellationToken ct = default)
    {
        var found = new List<OobDetection>();
        List<ControlledGpo> gpos;
        lock (store.Sync)
        {
            gpos = store.Gpos.Where(g => g.Status != ControlledGpoStatus.CheckedOut).ToList();
        }

        foreach (var gpo in gpos)
        {
            // Stub: simulate production hash drift for GPOs tagged with "OOB-DEMO" in the name.
            var productionHash = gpo.DisplayName.Contains("OOB-DEMO", StringComparison.OrdinalIgnoreCase)
                ? ComputeHash($"{gpo.ProductionGpoId}|drift|{DateTime.UtcNow:yyyyMMddHH}")
                : gpo.SettingsHash ?? "";

            var last = gpo.SettingsHash ?? "";
            if (string.Equals(productionHash, last, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var detection = new OobDetection
            {
                ControlledGpoId = gpo.Id,
                ProductionHash = productionHash,
                LastControlledHash = last,
                DiffSummary = $"Settings hash mismatch for '{gpo.DisplayName}' (stub diff)."
            };

            lock (store.Sync)
            {
                store.OobDetections.Add(detection);
            }

            found.Add(detection);

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
                    Body = $"{detection.DiffSummary}\nOpen Group Policy Director to choose Rollback or Checkout anyway."
                }, ct);
                detection.EmailSent = true;
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "OOB email stub failed for {Gpo}", gpo.DisplayName);
            }

            await audit.WriteAsync(new AuditEvent
            {
                Actor = "system:oob-scanner",
                Action = "Oob.Detected",
                ObjectKey = gpo.ProductionGpoId,
                Outcome = detection.EmailSent ? "Notified" : "DetectedNoMail",
                DetailJson = JsonSerializer.Serialize(new { detection.DiffSummary, detection.EmailSent })
            }, ct);
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
        // Light check: if last scan found unresolved OOB for this GPO, surface it.
        OobDetection? hit;
        lock (store.Sync)
        {
            hit = store.OobDetections
                .Where(o => o.ControlledGpoId == controlledGpoId &&
                            (o.Resolution is null || o.Resolution == "PendingDecision"))
                .OrderByDescending(o => o.DetectedAt)
                .FirstOrDefault();

            if (hit is null)
            {
                // Also detect live drift on checkout (same stub rule).
                var gpo = store.Gpos.FirstOrDefault(g => g.Id == controlledGpoId);
                if (gpo is not null &&
                    gpo.DisplayName.Contains("OOB-DEMO", StringComparison.OrdinalIgnoreCase))
                {
                    hit = new OobDetection
                    {
                        ControlledGpoId = controlledGpoId,
                        ProductionHash = ComputeHash($"{gpo.ProductionGpoId}|drift-checkout"),
                        LastControlledHash = gpo.SettingsHash ?? "",
                        DiffSummary = $"OOB change detected at checkout for '{gpo.DisplayName}'.",
                        Resolution = resolution
                    };
                    store.OobDetections.Add(hit);
                }
            }
            else
            {
                hit.Resolution = resolution;
            }
        }

        if (hit is not null)
        {
            await audit.WriteAsync(new AuditEvent
            {
                Actor = actor,
                Action = "Oob.CheckoutDecision",
                ObjectKey = controlledGpoId.ToString(),
                Outcome = resolution,
                DetailJson = JsonSerializer.Serialize(new { hit.DiffSummary })
            }, ct);
        }

        return hit;
    }

    private static string ComputeHash(string input)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(input));
        return Convert.ToHexString(bytes);
    }
}
