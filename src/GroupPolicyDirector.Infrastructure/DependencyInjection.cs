using System.Runtime.InteropServices;
using GroupPolicyDirector.Core.Ad;
using GroupPolicyDirector.Core.Agpm;
using GroupPolicyDirector.Core.Authz;
using GroupPolicyDirector.Core.Domain;
using GroupPolicyDirector.Core.Notifications;
using GroupPolicyDirector.Core.Workflow;
using GroupPolicyDirector.Infrastructure.Ad;
using GroupPolicyDirector.Infrastructure.Persistence;
using GroupPolicyDirector.Infrastructure.Services;
using Microsoft.Extensions.DependencyInjection;

namespace GroupPolicyDirector.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddGpdInfrastructure(this IServiceCollection services)
    {
        services.AddSingleton<GpdStore>();
        services.AddSingleton<IAuditService, AuditService>();
        services.AddSingleton<IDomainService, DomainService>();
        services.AddSingleton<IDelegationService, DelegationService>();
        services.AddSingleton<IDiffService, DiffService>();
        services.AddSingleton<IGpoWorkflowService, GpoWorkflowService>();
        services.AddSingleton<IContainerWorkflowService, ContainerWorkflowService>();
        services.AddSingleton<IOobDetectionService, OobDetectionService>();
        services.AddSingleton<IAgpmImportService, AgpmImportDryRunService>();
        services.AddSingleton<ISmtpSettingsService, SmtpSettingsService>();
        services.AddSingleton<IEmailNotifier, SmtpEmailNotifier>();

        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            services.AddSingleton<IDirectoryEnvironment, WindowsDirectoryEnvironment>();
        }
        else
        {
            services.AddSingleton<IDirectoryEnvironment, OfflineDirectoryEnvironment>();
        }

        services.AddSingleton<IGpoDirectoryService, AdaptiveGpoDirectoryService>();
        services.AddSingleton<IContainerDirectoryService, AdaptiveContainerDirectoryService>();

        return services;
    }
}
