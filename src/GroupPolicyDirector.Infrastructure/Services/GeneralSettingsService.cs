using System.Text.Json;
using System.Text.RegularExpressions;
using GroupPolicyDirector.Core.Domain;
using GroupPolicyDirector.Infrastructure.Persistence;

namespace GroupPolicyDirector.Infrastructure.Services;

/// <summary>
/// General settings + Welcome Wizard state. Naming regex enforced on Create/Rename.
/// </summary>
public sealed class GeneralSettingsService(GpdStore store, IAuditService audit) : IGeneralSettingsService
{
    public GeneralSettingsView GetView()
    {
        lock (store.Sync)
        {
            return Snapshot();
        }
    }

    public GeneralSettingsView Update(GeneralSettingsUpdate update, string actor)
    {
        lock (store.Sync)
        {
            if (update.ClearGpoNamingRegex == true)
            {
                store.GpoNamingRegex = null;
            }
            else if (update.GpoNamingRegex is not null)
            {
                var pattern = update.GpoNamingRegex.Trim();
                if (pattern.Length == 0)
                {
                    store.GpoNamingRegex = null;
                }
                else
                {
                    // Validate compile-time before storing.
                    _ = new Regex(pattern, RegexOptions.CultureInvariant | RegexOptions.Compiled, TimeSpan.FromSeconds(1));
                    store.GpoNamingRegex = pattern;
                }
            }
        }

        var view = GetView();
        _ = audit.WriteAsync(new AuditEvent
        {
            Actor = actor,
            Action = "Settings.GeneralUpdate",
            ObjectKey = "General",
            Outcome = "Success",
            DetailJson = JsonSerializer.Serialize(new
            {
                namingEnabled = view.NamingEnforcementEnabled,
                regex = view.GpoNamingRegex
            })
        });
        return view;
    }

    public void EnsureNameAllowed(string displayName)
    {
        if (string.IsNullOrWhiteSpace(displayName))
        {
            throw new ArgumentException("GPO display name is required.");
        }

        string? pattern;
        lock (store.Sync)
        {
            pattern = store.GpoNamingRegex;
        }

        if (string.IsNullOrWhiteSpace(pattern))
        {
            return;
        }

        Regex regex;
        try
        {
            regex = new Regex(pattern, RegexOptions.CultureInvariant | RegexOptions.Compiled, TimeSpan.FromSeconds(1));
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"Configured GPO naming regex is invalid: {ex.Message}");
        }

        if (!regex.IsMatch(displayName.Trim()))
        {
            throw new ArgumentException(
                $"GPO name '{displayName.Trim()}' does not match naming rule '{pattern}'.");
        }
    }

    public WelcomeWizardView GetWizard(string actor, bool canManage)
    {
        lock (store.Sync)
        {
            return BuildWizard(canManage);
        }
    }

    public WelcomeWizardView CompleteWizard(string actor, bool canManage)
    {
        if (!canManage)
        {
            throw new UnauthorizedAccessException("Only Product Administrators can complete the Welcome Wizard.");
        }

        lock (store.Sync)
        {
            store.WelcomeWizardStatus = WelcomeWizardStatus.Completed;
            store.WelcomeWizardCompletedAt = DateTimeOffset.UtcNow;
            store.WelcomeWizardCompletedBy = actor;
        }

        _ = audit.WriteAsync(new AuditEvent
        {
            Actor = actor,
            Action = "Wizard.Complete",
            ObjectKey = "Welcome",
            Outcome = "Success"
        });

        return GetWizard(actor, canManage);
    }

    public WelcomeWizardView SkipWizard(string actor, bool canManage)
    {
        if (!canManage)
        {
            throw new UnauthorizedAccessException("Only Product Administrators can skip the Welcome Wizard.");
        }

        lock (store.Sync)
        {
            store.WelcomeWizardStatus = WelcomeWizardStatus.Skipped;
            store.WelcomeWizardCompletedAt = DateTimeOffset.UtcNow;
            store.WelcomeWizardCompletedBy = actor;
        }

        _ = audit.WriteAsync(new AuditEvent
        {
            Actor = actor,
            Action = "Wizard.Skip",
            ObjectKey = "Welcome",
            Outcome = "Success"
        });

        return GetWizard(actor, canManage);
    }

    private GeneralSettingsView Snapshot() =>
        new()
        {
            GpoNamingRegex = store.GpoNamingRegex,
            NamingEnforcementEnabled = !string.IsNullOrWhiteSpace(store.GpoNamingRegex),
            RequireCheckInComment = true,
            WelcomeWizardStatus = store.WelcomeWizardStatus,
            WelcomeWizardCompletedAt = store.WelcomeWizardCompletedAt,
            WelcomeWizardCompletedBy = store.WelcomeWizardCompletedBy
        };

    private WelcomeWizardView BuildWizard(bool canManage)
    {
        var hasDomain = store.Domains.Count > 0;
        var hasGpo = store.Gpos.Count > 0;
        var hasContainer = store.Containers.Count > 0;
        var hasDelegation = store.Delegations.Any(d =>
            !string.Equals(d.GrantedBy, "bootstrap", StringComparison.OrdinalIgnoreCase));
        var hasNamingOrSmtpTouched = !string.IsNullOrWhiteSpace(store.GpoNamingRegex);

        return new WelcomeWizardView
        {
            Status = store.WelcomeWizardStatus,
            CanManage = canManage,
            CompletedAt = store.WelcomeWizardCompletedAt,
            CompletedBy = store.WelcomeWizardCompletedBy,
            Steps =
            [
                new WelcomeWizardStep
                {
                    Id = "domain-gpos",
                    Title = "Domain & GPOs",
                    Description = "Register a domain and Take Control of at least one GPO (or Create a new GPO).",
                    Done = hasDomain && hasGpo,
                    Optional = false
                },
                new WelcomeWizardStep
                {
                    Id = "containers",
                    Title = "AD Containers",
                    Description = "Optional: Take Control of an OU/Domain/Site for gpLink change-control.",
                    Done = hasContainer,
                    Optional = true
                },
                new WelcomeWizardStep
                {
                    Id = "delegation",
                    Title = "Delegate Access",
                    Description = "Grant Editor/Approver/Deployer on OU or GPO scope to AD admin groups.",
                    Done = hasDelegation,
                    Optional = false
                },
                new WelcomeWizardStep
                {
                    Id = "settings",
                    Title = "Settings",
                    Description = "Configure GPO naming regex and SMTP alert recipients. Intune is deferred.",
                    Done = hasNamingOrSmtpTouched,
                    Optional = true
                }
            ]
        };
    }
}
