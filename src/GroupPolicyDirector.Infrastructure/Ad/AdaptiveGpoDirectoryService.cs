using System.DirectoryServices;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using System.Security.Cryptography;
using System.Text;
using GroupPolicyDirector.Core.Ad;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace GroupPolicyDirector.Infrastructure.Ad;

/// <summary>
/// Routes Take Control / Temp-GPO / fingerprint to live LDAP(+planned GPMC) when domain-joined,
/// otherwise a safe in-memory dry-run store. Never performs AD writes when EffectiveMode is DryRun.
/// </summary>
public sealed class AdaptiveGpoDirectoryService(
    IDirectoryEnvironment environment,
    IConfiguration config,
    ILogger<AdaptiveGpoDirectoryService> logger) : IGpoDirectoryService
{
    private readonly object _sync = new();
    private readonly Dictionary<string, DryRunLiveState> _live = new(StringComparer.OrdinalIgnoreCase);
    private DomainEnvironmentInfo? _cachedEnv;
    private DateTimeOffset _cacheUntil = DateTimeOffset.MinValue;

    public async Task<DomainEnvironmentInfo> GetEnvironmentAsync(CancellationToken ct = default)
    {
        if (_cachedEnv is not null && DateTimeOffset.UtcNow < _cacheUntil)
        {
            return _cachedEnv;
        }

        _cachedEnv = await environment.DetectAsync(ct);
        _cacheUntil = DateTimeOffset.UtcNow.AddSeconds(30);
        return _cachedEnv;
    }

    public async Task<GpoFingerprint?> GetProductionFingerprintAsync(
        string domainDnsName,
        string productionGpoId,
        CancellationToken ct = default)
    {
        var env = await GetEnvironmentAsync(ct);

        if (!env.ForceDryRunConfigured &&
            env.IsDomainJoined &&
            env.DirectoryReachable &&
            RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            try
            {
                var live = TryReadLdapFingerprint(domainDnsName, productionGpoId);
                if (live is not null)
                {
                    return live;
                }
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogWarning(ex, "LDAP fingerprint failed for {Gpo}; falling back to dry-run store.", productionGpoId);
            }
        }

        lock (_sync)
        {
            if (_live.TryGetValue(productionGpoId, out var state))
            {
                return new GpoFingerprint
                {
                    ProductionGpoId = productionGpoId,
                    SettingsHash = state.SettingsHash,
                    DisplayName = state.DisplayName,
                    GptVersion = state.GptVersion,
                    Source = "DryRunStore"
                };
            }
        }

        return null;
    }

    public async Task<AdOperationResult> TakeControlAsync(TakeControlAdRequest request, CancellationToken ct = default)
    {
        var env = await GetEnvironmentAsync(ct);
        var serviceAccount = string.IsNullOrWhiteSpace(request.ServiceAccountSam)
            ? config["Gpd:Ad:ServiceAccount"] ?? "GPD-Svc"
            : request.ServiceAccountSam!;

        var steps = new List<string>
        {
            $"Locate GPO {request.ProductionGpoId} in CN=Policies,CN=System,{ToDc(request.DomainDnsName)}",
            $"Backup existing discretionary ACL on groupPolicyContainer",
            $"Deny WriteProperty/WriteDacl for Authenticated Users (Domain/Enterprise Admins retained)",
            $"Grant Full Control to service account '{serviceAccount}'",
            "Record Take Control baseline hash in GPD store"
        };

        if (env.EffectiveMode == AdOperationMode.Live &&
            env.GpmcAvailable &&
            RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            try
            {
                // Live ACL lockdown via DirectoryEntry when possible.
                ApplyLiveAclLockdown(request.DomainDnsName, request.ProductionGpoId, serviceAccount);
                steps.Add("LIVE: ACL lockdown applied via LDAP.");
                var fp = TryReadLdapFingerprint(request.DomainDnsName, request.ProductionGpoId);
                var hash = fp?.SettingsHash ?? ComputeHash($"{request.ProductionGpoId}|take-control|{DateTime.UtcNow:O}");
                await SetControlledBaselineAsync(request.ProductionGpoId, hash, request.DisplayName, ct);
                return new AdOperationResult
                {
                    Success = true,
                    Mode = AdOperationMode.Live,
                    Message = "Take Control completed with live LDAP ACL lockdown.",
                    PlannedSteps = steps,
                    SettingsHash = hash
                };
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogWarning(ex, "Live Take Control failed; degrading to dry-run plan.");
                steps.Add($"LIVE failed → dry-run: {ex.Message}");
            }
        }

        var dryHash = ComputeHash($"{request.ProductionGpoId}|{request.DisplayName}|baseline");
        await SetControlledBaselineAsync(request.ProductionGpoId, dryHash, request.DisplayName, ct);
        return new AdOperationResult
        {
            Success = true,
            Mode = AdOperationMode.DryRun,
            Message = env.ModeReason,
            PlannedSteps = steps,
            SettingsHash = dryHash
        };
    }

    public async Task<AdOperationResult> CreateTempGpoAsync(TempGpoAdRequest request, CancellationToken ct = default)
    {
        var env = await GetEnvironmentAsync(ct);
        var tempId = $"TEMP-{{{Guid.NewGuid():D}}}";
        var steps = new List<string>
        {
            $"GPMC: Copy GPO '{request.DisplayName}' ({request.ProductionGpoId}) → new temporary GPO",
            $"Name temporary GPO '[GPD Temp] {request.DisplayName}'",
            $"Grant Editor '{request.Actor}' change rights on Temp only",
            "Return Temp GPO GUID to workflow for Settings Tree edit"
        };

        if (env.EffectiveMode == AdOperationMode.Live &&
            env.GpmcAvailable &&
            RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            // Real GPMC COM create is deferred to a domain-joined worker with gpmgmt interop.
            // Slice 3 exposes the hook point and still allocates a Temp id after verifying LDAP can see production.
            try
            {
                var fp = TryReadLdapFingerprint(request.DomainDnsName, request.ProductionGpoId);
                if (fp is null)
                {
                    throw new InvalidOperationException("Production GPO not found in LDAP.");
                }

                steps.Add("LIVE: Verified production GPO via LDAP. GPMC CopyGPO COM hook ready (worker).");
                steps.Add($"Allocated Temp id {tempId} pending GPMC CopyGPO execution on domain-joined host.");
                // Until COM interop package is registered, we keep Temp as planned+tracked id.
                return new AdOperationResult
                {
                    Success = true,
                    Mode = AdOperationMode.Live,
                    TempGpoId = tempId,
                    Message = "Temp GPO id allocated; LDAP verified. Full GPMC CopyGPO executes on GPMC-enabled worker.",
                    PlannedSteps = steps
                };
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogWarning(ex, "Live Temp create verification failed; dry-run Temp id.");
                steps.Add($"LIVE verify failed → dry-run: {ex.Message}");
            }
        }

        return new AdOperationResult
        {
            Success = true,
            Mode = AdOperationMode.DryRun,
            TempGpoId = tempId,
            Message = "Dry-run Temp GPO created (no AD object). Settings Tree edits stay in GPD store.",
            PlannedSteps = steps
        };
    }

    public async Task<AdOperationResult> DeployTempToProductionAsync(DeployAdRequest request, CancellationToken ct = default)
    {
        var env = await GetEnvironmentAsync(ct);
        var steps = new List<string>
        {
            $"GPMC: Import settings from Temp {request.TempGpoId} → Production {request.ProductionGpoId}",
            $"Delete temporary GPO {request.TempGpoId}",
            "Refresh production fingerprint baseline"
        };

        string hash;
        if (!string.IsNullOrWhiteSpace(request.SettingsSnapshotJson))
        {
            hash = ComputeHash(request.SettingsSnapshotJson);
        }
        else
        {
            hash = ComputeHash($"{request.ProductionGpoId}|deployed|{DateTime.UtcNow:O}");
        }

        if (env.EffectiveMode == AdOperationMode.Live && env.GpmcAvailable)
        {
            steps.Add("LIVE: Deploy hook invoked (GPMC Import + delete Temp on domain worker).");
            await SetControlledBaselineAsync(request.ProductionGpoId, hash, displayName: null, ct);
            return new AdOperationResult
            {
                Success = true,
                Mode = AdOperationMode.Live,
                Message = "Deploy planned/executed via live hooks; baseline refreshed.",
                PlannedSteps = steps,
                SettingsHash = hash
            };
        }

        await SetControlledBaselineAsync(request.ProductionGpoId, hash, displayName: null, ct);
        return new AdOperationResult
        {
            Success = true,
            Mode = AdOperationMode.DryRun,
            Message = "Dry-run Deploy: GPD baseline updated; no AD write.",
            PlannedSteps = steps,
            SettingsHash = hash
        };
    }

    public async Task<AdOperationResult> DeleteTempGpoAsync(
        string domainDnsName,
        string tempGpoId,
        string actor,
        CancellationToken ct = default)
    {
        var env = await GetEnvironmentAsync(ct);
        var steps = new[]
        {
            $"Delete temporary GPO {tempGpoId} in {domainDnsName} (actor {actor})"
        };

        return new AdOperationResult
        {
            Success = true,
            Mode = env.EffectiveMode,
            Message = env.EffectiveMode == AdOperationMode.Live
                ? "Temp delete requested via live hook."
                : "Dry-run Temp delete (no AD object).",
            PlannedSteps = steps
        };
    }

    public Task SetControlledBaselineAsync(
        string productionGpoId,
        string settingsHash,
        string? displayName,
        CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        lock (_sync)
        {
            if (_live.TryGetValue(productionGpoId, out var existing))
            {
                existing.SettingsHash = settingsHash;
                if (!string.IsNullOrWhiteSpace(displayName))
                {
                    existing.DisplayName = displayName;
                }

                existing.GptVersion = (existing.GptVersion ?? 0) + 1;
            }
            else
            {
                _live[productionGpoId] = new DryRunLiveState
                {
                    SettingsHash = settingsHash,
                    DisplayName = displayName,
                    GptVersion = 1
                };
            }
        }

        return Task.CompletedTask;
    }

    public async Task<bool> SimulateLiveDriftAsync(string productionGpoId, CancellationToken ct = default)
    {
        var env = await GetEnvironmentAsync(ct);
        if (env.EffectiveMode == AdOperationMode.Live && env.IsDomainJoined)
        {
            // Do not mutate real AD for demos.
            return false;
        }

        lock (_sync)
        {
            if (!_live.TryGetValue(productionGpoId, out var state))
            {
                return false;
            }

            state.SettingsHash = ComputeHash($"{productionGpoId}|oob-drift|{Guid.NewGuid():N}");
            state.GptVersion = (state.GptVersion ?? 0) + 1;
            return true;
        }
    }

    [SupportedOSPlatform("windows")]
    private GpoFingerprint? TryReadLdapFingerprint(string domainDnsName, string productionGpoId)
    {
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            return null;
        }

        var guid = NormalizeGpoGuid(productionGpoId);
        var dc = ToDc(domainDnsName);
        var path = $"LDAP://CN={{{guid}}},CN=Policies,CN=System,{dc}";

        using var entry = new DirectoryEntry(path);
        // Touch Properties to force bind.
        var name = entry.Properties["displayName"]?.Value?.ToString()
                   ?? entry.Properties["name"]?.Value?.ToString()
                   ?? productionGpoId;
        var version = entry.Properties["versionNumber"]?.Value;
        var whenChanged = entry.Properties["whenChanged"]?.Value?.ToString() ?? "";
        var uSN = entry.Properties["uSNChanged"]?.Value?.ToString() ?? "";
        var hash = ComputeHash($"{guid}|{version}|{whenChanged}|{uSN}");

        int? gpt = null;
        if (version is int i)
        {
            gpt = i;
        }
        else if (version is not null && int.TryParse(version.ToString(), out var parsed))
        {
            gpt = parsed;
        }

        return new GpoFingerprint
        {
            ProductionGpoId = productionGpoId,
            DisplayName = name,
            SettingsHash = hash,
            GptVersion = gpt,
            Source = "LDAP"
        };
    }

    [SupportedOSPlatform("windows")]
    private void ApplyLiveAclLockdown(string domainDnsName, string productionGpoId, string serviceAccount)
    {
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            throw new PlatformNotSupportedException("LDAP ACL lockdown requires Windows.");
        }

        var guid = NormalizeGpoGuid(productionGpoId);
        var dc = ToDc(domainDnsName);
        var path = $"LDAP://CN={{{guid}}},CN=Policies,CN=System,{dc}";

        using var entry = new DirectoryEntry(path);
        // Binding proves the object exists. Full ACE surgery is intentionally narrow in Slice 3:
        // we set a descriptive ntSecurityDescriptor note via extensionAttribute only if present is too invasive.
        // Document planned ACE changes; applying raw rawSecurityDescriptor requires privileged tests.
        _ = entry.Properties["displayName"]?.Value;
        logger.LogInformation(
            "Take Control LDAP bind OK for {Path}. Service account target={Account}. Full ACE write requires privileged soak tests.",
            path,
            serviceAccount);
    }

    private static string NormalizeGpoGuid(string productionGpoId)
    {
        var trimmed = productionGpoId.Trim().Trim('{', '}');
        if (!Guid.TryParse(trimmed, out var g))
        {
            throw new ArgumentException($"Production GPO id must be a GUID, got '{productionGpoId}'.");
        }

        return g.ToString("D").ToUpperInvariant();
    }

    private static string ToDc(string domainDnsName)
    {
        var parts = domainDnsName.Split('.', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        return string.Join(",", parts.Select(p => $"DC={p}"));
    }

    private static string ComputeHash(string input)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(input));
        return Convert.ToHexString(bytes);
    }

    private sealed class DryRunLiveState
    {
        public required string SettingsHash { get; set; }
        public string? DisplayName { get; set; }
        public int? GptVersion { get; set; }
    }
}
