namespace GroupPolicyDirector.Core.Domain;

public interface IGeneralSettingsService
{
    GeneralSettingsView GetView();
    GeneralSettingsView Update(GeneralSettingsUpdate update, string actor);
    /// <summary>Throws <see cref="ArgumentException"/> when naming regex is set and name does not match.</summary>
    void EnsureNameAllowed(string displayName);
    WelcomeWizardView GetWizard(string actor, bool canManage);
    WelcomeWizardView CompleteWizard(string actor, bool canManage);
    WelcomeWizardView SkipWizard(string actor, bool canManage);
}
