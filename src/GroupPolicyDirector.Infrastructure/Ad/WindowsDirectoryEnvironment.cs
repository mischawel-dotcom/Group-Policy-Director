using System.DirectoryServices.ActiveDirectory;
using System.Runtime.Versioning;
using GroupPolicyDirector.Core.Ad;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace GroupPolicyDirector.Infrastructure.Ad;

/// <summary>
/// Domain-join + GPMC detection for Windows hosts.
/// </summary>
[SupportedOSPlatform("windows")]
public sealed class WindowsDirectoryEnvironment(
    IConfiguration config,
    ILogger<WindowsDirectoryEnvironment> logger) : IDirectoryEnvironment
{
    public Task<DomainEnvironmentInfo> DetectAsync(CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();

        var forceDryRun = config.GetValue("Gpd:Ad:ForceDryRun", false);
        var joined = false;
        string? dnsDomain = null;
        string? netBios = null;
        var directoryReachable = false;
        string joinDetail = "Not domain-joined (WORKGROUP / standalone).";

        try
        {
            // Environment variables are a fast hint; ADSI confirms.
            var userDns = Environment.GetEnvironmentVariable("USERDNSDOMAIN");
            if (!string.IsNullOrWhiteSpace(userDns) &&
                !string.Equals(userDns, Environment.MachineName, StringComparison.OrdinalIgnoreCase))
            {
                dnsDomain = userDns;
            }

            try
            {
                using var domain = Domain.GetComputerDomain();
                joined = true;
                dnsDomain = domain.Name;
                netBios = domain.Forest?.Name; // forest DNS; NetBIOS via DomainControllers below if needed
                directoryReachable = true;
                joinDetail = $"Domain.GetComputerDomain → {domain.Name}";
            }
            catch (ActiveDirectoryObjectNotFoundException)
            {
                joined = false;
                joinDetail = "Domain.GetComputerDomain: computer is not domain-joined.";
            }
            catch (ActiveDirectoryOperationException ex)
            {
                joined = false;
                joinDetail = $"Active Directory operation failed: {ex.Message}";
                logger.LogWarning(ex, "Directory environment probe failed.");
            }
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogWarning(ex, "Unexpected domain detection failure.");
            joinDetail = ex.Message;
        }

        if (string.IsNullOrWhiteSpace(netBios))
        {
            netBios = Environment.GetEnvironmentVariable("USERDOMAIN");
        }

        var gpmc = DetectGpmc();
        AdOperationMode mode;
        string reason;
        if (forceDryRun)
        {
            mode = AdOperationMode.DryRun;
            reason = "Gpd:Ad:ForceDryRun=true — all AD/GPMC writes are simulated.";
        }
        else if (!joined || !directoryReachable)
        {
            mode = AdOperationMode.DryRun;
            reason = joinDetail + " Safe dry-run mode active (no AD writes).";
        }
        else if (!gpmc)
        {
            mode = AdOperationMode.DryRun;
            reason = "Domain-joined but GPMC not detected — Temp-GPO create/deploy stay dry-run; LDAP fingerprint may still work.";
        }
        else
        {
            mode = AdOperationMode.Live;
            reason = "Domain-joined, directory reachable, GPMC present — live LDAP/GPMC hooks enabled.";
        }

        // If joined but GPMC missing we still expose Live for fingerprint-only via LDAP when ForceDryRun is false.
        // EffectiveMode above keeps DryRun for write ops; Adaptive service may override fingerprint to Live LDAP.
        if (!forceDryRun && joined && directoryReachable && !gpmc)
        {
            // Keep DryRun for writes; fingerprint path handled separately in AdaptiveGpoDirectoryService.
            reason += " Fingerprints attempt LDAP read-only.";
        }

        return Task.FromResult(new DomainEnvironmentInfo
        {
            IsDomainJoined = joined,
            ComputerDnsDomain = dnsDomain,
            ComputerNetBiosDomain = netBios,
            DirectoryReachable = directoryReachable,
            GpmcAvailable = gpmc,
            ForceDryRunConfigured = forceDryRun,
            EffectiveMode = mode,
            ModeReason = reason
        });
    }

    private static bool DetectGpmc()
    {
        try
        {
            // GPMC registers gpmgmt.dll — presence is a sufficient Slice-3 signal.
            var system = Environment.GetFolderPath(Environment.SpecialFolder.System);
            var dll = Path.Combine(system, "gpmgmt.dll");
            if (File.Exists(dll))
            {
                return true;
            }

            // Also check Program Files\Microsoft Group Policy Management Console paths.
            var pf = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
            var alt = Path.Combine(pf, "Microsoft Group Policy Management Console");
            return Directory.Exists(alt);
        }
        catch
        {
            return false;
        }
    }
}

/// <summary>Non-Windows / fallback detector always reports DryRun.</summary>
public sealed class OfflineDirectoryEnvironment : IDirectoryEnvironment
{
    public Task<DomainEnvironmentInfo> DetectAsync(CancellationToken ct = default) =>
        Task.FromResult(new DomainEnvironmentInfo
        {
            IsDomainJoined = false,
            EffectiveMode = AdOperationMode.DryRun,
            ModeReason = "Non-Windows or unsupported host — dry-run only.",
            ForceDryRunConfigured = true
        });
}
