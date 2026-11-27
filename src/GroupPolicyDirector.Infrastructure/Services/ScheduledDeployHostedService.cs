using GroupPolicyDirector.Core.Workflow;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace GroupPolicyDirector.Infrastructure.Services;

/// <summary>
/// Polls due scheduled deploys in the same process as the shared <c>GpdStore</c>
/// (API). Worker also hosts this for when SQL persistence is shared later.
/// </summary>
public sealed class ScheduledDeployHostedService(
    IServiceProvider services,
    IConfiguration config,
    ILogger<ScheduledDeployHostedService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation("Scheduled deploy processor started.");

        while (!stoppingToken.IsCancellationRequested)
        {
            var seconds = config.GetValue("ScheduledDeploy:PollIntervalSeconds", 10);
            try
            {
                using var scope = services.CreateScope();
                // Singletons today; scope still keeps the pattern ready for scoped SQL.
                var queue = scope.ServiceProvider.GetRequiredService<IScheduledDeployService>();
                var done = await queue.ProcessDueAsync(stoppingToken);
                if (done.Count > 0)
                {
                    logger.LogInformation("Processed {Count} due scheduled deploy(s).", done.Count);
                }
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogError(ex, "Scheduled deploy processor failed.");
            }

            await Task.Delay(TimeSpan.FromSeconds(Math.Max(2, seconds)), stoppingToken);
        }
    }
}
