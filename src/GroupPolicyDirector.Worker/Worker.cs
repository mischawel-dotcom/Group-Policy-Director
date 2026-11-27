using GroupPolicyDirector.Core.Domain;
using GroupPolicyDirector.Core.Workflow;

namespace GroupPolicyDirector.Worker;

/// <summary>
/// Background OOB scanner (mail-first) + scheduled-deploy nudge.
/// Shared SQL will make Worker ProcessDue see the same queue as the API; until then
/// the API hosted processor owns in-memory due jobs.
/// </summary>
public sealed class Worker(
    ILogger<Worker> logger,
    IOobDetectionService oob,
    IScheduledDeployService schedules,
    IConfiguration config) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation("Group Policy Director Worker started (OOB scan + scheduled deploy).");

        while (!stoppingToken.IsCancellationRequested)
        {
            var seconds = config.GetValue("Oob:ScanIntervalSeconds", 300);
            try
            {
                var hits = await oob.ScanAsync(stoppingToken);
                if (hits.Count > 0)
                {
                    logger.LogWarning("OOB scan found {Count} drift event(s).", hits.Count);
                }
                else
                {
                    logger.LogDebug("OOB scan complete — no drift.");
                }

                var due = await schedules.ProcessDueAsync(stoppingToken);
                if (due.Count > 0)
                {
                    logger.LogInformation("Worker processed {Count} scheduled deploy(s).", due.Count);
                }
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogError(ex, "Worker cycle failed.");
            }

            await Task.Delay(TimeSpan.FromSeconds(Math.Max(30, seconds)), stoppingToken);
        }
    }
}
