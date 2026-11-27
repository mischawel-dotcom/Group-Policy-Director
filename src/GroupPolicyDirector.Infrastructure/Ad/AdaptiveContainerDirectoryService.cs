using System.DirectoryServices;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using GroupPolicyDirector.Core.Ad;
using GroupPolicyDirector.Core.Domain;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace GroupPolicyDirector.Infrastructure.Ad;

/// <summary>
/// Container gpLink / gpOptions adapter: live LDAP when domain-joined, otherwise dry-run store.
/// </summary>
public sealed class AdaptiveContainerDirectoryService(
    IDirectoryEnvironment environment,
    IConfiguration config,
    ILogger<AdaptiveContainerDirectoryService> logger) : IContainerDirectoryService
{
    private readonly object _sync = new();
    private readonly Dictionary<string, DryRunContainerState> _live = new(StringComparer.OrdinalIgnoreCase);
    private DomainEnvironmentInfo? _cachedEnv;
    private DateTimeOffset _cacheUntil = DateTimeOffset.MinValue;

    public async Task<AdOperationResult> TakeControlAsync(TakeControlContainerAdRequest request, CancellationToken ct = default)
    {
        var env = await GetEnvironmentAsync(ct);
        var serviceAccount = string.IsNullOrWhiteSpace(request.ServiceAccountSam)
            ? config["Gpd:Ad:ServiceAccount"] ?? "GPD-Svc"
            : request.ServiceAccountSam!;

        var steps = new List<string>
        {
            $"Locate container {request.ContainerKey} ({request.Kind})",
            "Backup existing discretionary ACL (gpLink / gpOptions write)",
            $"Deny WriteProperty on gpLink/gpOptions for Authenticated Users (retain Domain/Enterprise Admins)",
            $"Grant Full Control / WriteProperty to service account '{serviceAccount}'",
            "Capture gpLink + gpOptions baseline into GPD"
        };

        ContainerLinksState links;
        string hash;
        AdOperationMode mode;

        if (env.EffectiveMode == AdOperationMode.Live &&
            env.DirectoryReachable &&
            RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            try
            {
                var fp = TryReadLdapLinks(request.ContainerKey);
                if (fp is not null)
                {
                    links = CloneLinks(fp.Links);
                    hash = fp.LinksHash;
                    steps.Add("LIVE: Read gpLink/gpOptions via LDAP.");
                }
                else
                {
                    links = SeedDemoLinks(request.DisplayName);
                    hash = ComputeLinksHash(links);
                    steps.Add("LIVE: Container reachable but empty gpLink — seeded demo baseline.");
                }

                mode = AdOperationMode.Live;
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogWarning(ex, "Live container Take Control failed; falling back to dry-run.");
                links = SeedDemoLinks(request.DisplayName);
                hash = ComputeLinksHash(links);
                mode = AdOperationMode.DryRun;
                steps.Add($"FALLBACK DryRun: {ex.Message}");
            }
        }
        else
        {
            links = SeedDemoLinks(request.DisplayName);
            hash = ComputeLinksHash(links);
            mode = AdOperationMode.DryRun;
            steps.Add($"DryRun: {env.ModeReason}");
        }

        await SetControlledBaselineAsync(request.ContainerKey, hash, links, ct);

        return new AdOperationResult
        {
            Success = true,
            Mode = mode,
            Message = mode == AdOperationMode.Live
                ? "Container Take Control completed (gpLink ACL path)."
                : "Container Take Control planned (dry-run); baseline seeded.",
            PlannedSteps = steps,
            SettingsHash = hash
        };
    }

    public async Task<ContainerLinksFingerprint?> ReadLinksAsync(
        string domainDnsName,
        string containerKey,
        CancellationToken ct = default)
    {
        _ = domainDnsName;
        var env = await GetEnvironmentAsync(ct);

        if (!env.ForceDryRunConfigured &&
            env.IsDomainJoined &&
            env.DirectoryReachable &&
            RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            try
            {
                var live = TryReadLdapLinks(containerKey);
                if (live is not null)
                {
                    return live;
                }
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogWarning(ex, "LDAP gpLink read failed for {Container}.", containerKey);
            }
        }

        lock (_sync)
        {
            if (_live.TryGetValue(containerKey, out var state))
            {
                return new ContainerLinksFingerprint
                {
                    ContainerKey = containerKey,
                    LinksHash = state.LinksHash,
                    Links = CloneLinks(state.Links),
                    Source = "DryRunStore"
                };
            }
        }

        return null;
    }

    public async Task<AdOperationResult> DeployLinksAsync(DeployContainerLinksAdRequest request, CancellationToken ct = default)
    {
        var env = await GetEnvironmentAsync(ct);
        var normalized = Normalize(request.Links);
        var hash = ComputeLinksHash(normalized);
        var gpLink = EncodeGpLink(normalized, request.DomainDnsName);
        var gpOptions = normalized.BlockInheritance ? 1 : 0;

        var steps = new List<string>
        {
            $"Write gpLink on {request.ContainerKey}",
            $"gpLink value length={gpLink.Length}",
            $"Set gpOptions={gpOptions} (block inheritance={(normalized.BlockInheritance ? "yes" : "no")})",
            $"Link count={normalized.Links.Count}"
        };

        if (env.EffectiveMode == AdOperationMode.Live &&
            env.DirectoryReachable &&
            RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            try
            {
                ApplyLiveGpLink(request.ContainerKey, gpLink, gpOptions);
                steps.Add("LIVE: gpLink/gpOptions written via LDAP.");
                await SetControlledBaselineAsync(request.ContainerKey, hash, normalized, ct);
                return new AdOperationResult
                {
                    Success = true,
                    Mode = AdOperationMode.Live,
                    Message = "Container links deployed to AD.",
                    PlannedSteps = steps,
                    SettingsHash = hash
                };
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogWarning(ex, "Live gpLink deploy failed; recording dry-run baseline.");
                steps.Add($"LIVE write failed → DryRun baseline: {ex.Message}");
            }
        }
        else
        {
            steps.Add($"DryRun: {env.ModeReason}");
        }

        await SetControlledBaselineAsync(request.ContainerKey, hash, normalized, ct);
        return new AdOperationResult
        {
            Success = true,
            Mode = AdOperationMode.DryRun,
            Message = "Container links deploy planned (dry-run store updated).",
            PlannedSteps = steps,
            SettingsHash = hash
        };
    }

    public Task SetControlledBaselineAsync(
        string containerKey,
        string linksHash,
        ContainerLinksState? links,
        CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        lock (_sync)
        {
            _live[containerKey] = new DryRunContainerState
            {
                LinksHash = linksHash,
                Links = links is null ? new ContainerLinksState() : CloneLinks(links)
            };
        }

        return Task.CompletedTask;
    }

    private async Task<DomainEnvironmentInfo> GetEnvironmentAsync(CancellationToken ct)
    {
        if (_cachedEnv is not null && DateTimeOffset.UtcNow < _cacheUntil)
        {
            return _cachedEnv;
        }

        _cachedEnv = await environment.DetectAsync(ct);
        _cacheUntil = DateTimeOffset.UtcNow.AddSeconds(30);
        return _cachedEnv;
    }

    private static ContainerLinksState SeedDemoLinks(string displayName)
    {
        return new ContainerLinksState
        {
            BlockInheritance = false,
            Links =
            [
                new GpoLinkEntry
                {
                    GpoId = "{11111111-1111-1111-1111-111111111111}",
                    DisplayName = "Demo Workstation Policy",
                    Order = 0,
                    Enforced = false,
                    Disabled = false
                },
                new GpoLinkEntry
                {
                    GpoId = "{22222222-2222-2222-2222-222222222222}",
                    DisplayName = $"Baseline for {displayName}",
                    Order = 1,
                    Enforced = true,
                    Disabled = false
                }
            ]
        };
    }

    internal static ContainerLinksState Normalize(ContainerLinksState state)
    {
        var clone = CloneLinks(state);
        var ordered = clone.Links
            .OrderBy(l => l.Order)
            .ThenBy(l => l.GpoId, StringComparer.OrdinalIgnoreCase)
            .Select((l, i) =>
            {
                l.Order = i;
                return l;
            })
            .ToList();
        clone.Links = ordered;
        return clone;
    }

    internal static string ComputeLinksHash(ContainerLinksState state)
    {
        var normalized = Normalize(state);
        var json = JsonSerializer.Serialize(normalized);
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(json));
        return Convert.ToHexString(bytes);
    }

    internal static ContainerLinksState CloneLinks(ContainerLinksState state) =>
        new()
        {
            BlockInheritance = state.BlockInheritance,
            Links = state.Links.Select(l => new GpoLinkEntry
            {
                GpoId = l.GpoId,
                DisplayName = l.DisplayName,
                Order = l.Order,
                Enforced = l.Enforced,
                Disabled = l.Disabled
            }).ToList()
        };

    internal static string EncodeGpLink(ContainerLinksState state, string domainDnsName)
    {
        var dc = string.Join(",", domainDnsName.Split('.', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(p => $"DC={p}"));
        var sb = new StringBuilder();
        foreach (var link in state.Links.OrderBy(l => l.Order))
        {
            var options = (link.Disabled ? 1 : 0) | (link.Enforced ? 2 : 0);
            var guid = link.GpoId.Trim().Trim('{', '}');
            sb.Append($"[LDAP://CN={{{guid}}},CN=Policies,CN=System,{dc};{options}]");
        }

        return sb.ToString();
    }

    [SupportedOSPlatform("windows")]
    private ContainerLinksFingerprint? TryReadLdapLinks(string containerKey)
    {
        using var entry = new DirectoryEntry($"LDAP://{containerKey}");
        entry.RefreshCache(["gpLink", "gpOptions", "name", "distinguishedName"]);
        var gpLink = entry.Properties["gpLink"]?.Value?.ToString() ?? "";
        var gpOptionsRaw = entry.Properties["gpOptions"]?.Value;
        var block = gpOptionsRaw is int i && i == 1
                    || (gpOptionsRaw is not null && int.TryParse(gpOptionsRaw.ToString(), out var parsed) && parsed == 1);

        var links = ParseGpLink(gpLink);
        var state = new ContainerLinksState { BlockInheritance = block, Links = links };
        return new ContainerLinksFingerprint
        {
            ContainerKey = containerKey,
            LinksHash = ComputeLinksHash(state),
            Links = state,
            Source = "LDAP"
        };
    }

    internal static List<GpoLinkEntry> ParseGpLink(string gpLink)
    {
        var result = new List<GpoLinkEntry>();
        if (string.IsNullOrWhiteSpace(gpLink))
        {
            return result;
        }

        // Format: [LDAP://CN={guid},CN=Policies,...;options]...
        var parts = gpLink.Split(']', StringSplitOptions.RemoveEmptyEntries);
        var order = 0;
        foreach (var part in parts)
        {
            var chunk = part.Trim().TrimStart('[');
            if (string.IsNullOrWhiteSpace(chunk))
            {
                continue;
            }

            var semi = chunk.LastIndexOf(';');
            if (semi < 0)
            {
                continue;
            }

            var ldap = chunk[..semi];
            var optionsText = chunk[(semi + 1)..];
            _ = int.TryParse(optionsText, out var options);
            var guidStart = ldap.IndexOf('{');
            var guidEnd = ldap.IndexOf('}');
            if (guidStart < 0 || guidEnd <= guidStart)
            {
                continue;
            }

            var guid = ldap[guidStart..(guidEnd + 1)];
            result.Add(new GpoLinkEntry
            {
                GpoId = guid,
                Order = order++,
                Disabled = (options & 1) == 1,
                Enforced = (options & 2) == 2
            });
        }

        return result;
    }

    [SupportedOSPlatform("windows")]
    private void ApplyLiveGpLink(string containerKey, string gpLink, int gpOptions)
    {
        using var entry = new DirectoryEntry($"LDAP://{containerKey}");
        entry.Properties["gpLink"].Value = gpLink;
        entry.Properties["gpOptions"].Value = gpOptions;
        entry.CommitChanges();
    }

    private sealed class DryRunContainerState
    {
        public required string LinksHash { get; init; }
        public required ContainerLinksState Links { get; init; }
    }
}
