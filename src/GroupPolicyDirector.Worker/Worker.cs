using GroupPolicyDirector.Core.Domain;

namespace GroupPolicyDirector.Worker;

/// <summary>
/// Background OOB scanner (mail-first). Interval from Oob:ScanIntervalSeconds.
/// </summary>
public sealed class Worker(ILogger<Worker> logger, IOobDetectionService oob, IConfiguration config) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation("Group Policy Director Worker started (OOB baseline-vs-live scan).");

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
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogError(ex, "OOB scan failed.");
            }

            await Task.Delay(TimeSpan.FromSeconds(Math.Max(30, seconds)), stoppingToken);
        }
    }
}
