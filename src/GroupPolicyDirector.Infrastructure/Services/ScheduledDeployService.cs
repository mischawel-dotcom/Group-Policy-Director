using System.Text.Json;
using GroupPolicyDirector.Core.Domain;
using GroupPolicyDirector.Core.Workflow;
using GroupPolicyDirector.Infrastructure.Persistence;
using Microsoft.Extensions.Logging;

namespace GroupPolicyDirector.Infrastructure.Services;

/// <summary>
/// Slice 8: Schedule / Cancel / ProcessDue for GPO and container deploys.
/// Shares <see cref="GpdStore"/> with the API hosted processor (in-memory until SQL).
/// </summary>
public sealed class ScheduledDeployService(
    GpdStore store,
    IGpoWorkflowService gpoWorkflow,
    IContainerWorkflowService containerWorkflow,
    IAuditService audit,
    ILogger<ScheduledDeployService> logger) : IScheduledDeployService
{
    public Task<IReadOnlyList<ScheduledDeploy>> ListAsync(CancellationToken ct = default)
    {
        lock (store.Sync)
        {
            IReadOnlyList<ScheduledDeploy> items = store.ScheduledDeploys
                .OrderByDescending(s => s.CreatedAt)
                .ToList();
            return Task.FromResult(items);
        }
    }

    public Task<ScheduledDeploy?> GetAsync(Guid id, CancellationToken ct = default)
    {
        lock (store.Sync)
        {
            return Task.FromResult(store.ScheduledDeploys.FirstOrDefault(s => s.Id == id));
        }
    }

    public async Task<ScheduledDeploy> ScheduleGpoAsync(
        Guid controlledGpoId,
        ScheduleDeployRequest request,
        string actor,
        CancellationToken ct = default)
    {
        ValidateScheduleRequest(request);
        await gpoWorkflow.EnsureCanDeployAsync(controlledGpoId, actor, ct);

        ScheduledDeploy job;
        lock (store.Sync)
        {
            var gpo = store.Gpos.FirstOrDefault(g => g.Id == controlledGpoId)
                      ?? throw new KeyNotFoundException("Controlled GPO not found.");

            if (gpo.Status != ControlledGpoStatus.Approved || gpo.PendingVersionId is null)
            {
                throw new InvalidOperationException("GPO must be Approved (not already Scheduled) to schedule Deploy.");
            }

            if (store.ScheduledDeploys.Any(s =>
                    s.ObjectKind == ScheduledDeployObjectKind.Gpo &&
                    s.ObjectId == controlledGpoId &&
                    s.Status is ScheduledDeployStatus.Pending or ScheduledDeployStatus.Running))
            {
                throw new InvalidOperationException("A pending schedule already exists for this GPO.");
            }

            job = new ScheduledDeploy
            {
                ObjectKind = ScheduledDeployObjectKind.Gpo,
                ObjectId = gpo.Id,
                ObjectKey = gpo.ProductionGpoId,
                DisplayName = gpo.DisplayName,
                PendingVersionId = gpo.PendingVersionId.Value,
                ScheduledFor = request.ScheduledFor.ToUniversalTime(),
                ScheduledBy = actor,
                RollbackOnFailure = request.RollbackOnFailure,
                SimulateFailure = request.SimulateFailure,
                Status = ScheduledDeployStatus.Pending
            };

            store.ScheduledDeploys.Add(job);
            gpo.Status = ControlledGpoStatus.Scheduled;
            gpo.ActiveScheduleId = job.Id;
        }

        await audit.WriteAsync(new AuditEvent
        {
            Actor = actor,
            Action = "Gpo.ScheduleDeploy",
            ObjectKey = job.ObjectKey,
            Outcome = "Success",
            DetailJson = JsonSerializer.Serialize(new
            {
                job.Id,
                job.ScheduledFor,
                job.RollbackOnFailure,
                job.SimulateFailure
            })
        }, ct);

        return job;
    }

    public async Task<ScheduledDeploy> ScheduleContainerAsync(
        Guid containerId,
        ScheduleDeployRequest request,
        string actor,
        CancellationToken ct = default)
    {
        ValidateScheduleRequest(request);
        await containerWorkflow.EnsureCanDeployAsync(containerId, actor, ct);

        ScheduledDeploy job;
        lock (store.Sync)
        {
            var container = store.Containers.FirstOrDefault(c => c.Id == containerId)
                            ?? throw new KeyNotFoundException("Controlled container not found.");

            if (container.Status != ControlledGpoStatus.Approved || container.PendingVersionId is null)
            {
                throw new InvalidOperationException("Container must be Approved (not already Scheduled) to schedule Deploy.");
            }

            if (store.ScheduledDeploys.Any(s =>
                    s.ObjectKind == ScheduledDeployObjectKind.Container &&
                    s.ObjectId == containerId &&
                    s.Status is ScheduledDeployStatus.Pending or ScheduledDeployStatus.Running))
            {
                throw new InvalidOperationException("A pending schedule already exists for this container.");
            }

            job = new ScheduledDeploy
            {
                ObjectKind = ScheduledDeployObjectKind.Container,
                ObjectId = container.Id,
                ObjectKey = container.ContainerKey,
                DisplayName = container.DisplayName,
                PendingVersionId = container.PendingVersionId.Value,
                ScheduledFor = request.ScheduledFor.ToUniversalTime(),
                ScheduledBy = actor,
                RollbackOnFailure = request.RollbackOnFailure,
                SimulateFailure = request.SimulateFailure,
                Status = ScheduledDeployStatus.Pending
            };

            store.ScheduledDeploys.Add(job);
            container.Status = ControlledGpoStatus.Scheduled;
            container.ActiveScheduleId = job.Id;
        }

        await audit.WriteAsync(new AuditEvent
        {
            Actor = actor,
            Action = "Container.ScheduleDeploy",
            ObjectKey = job.ObjectKey,
            Outcome = "Success",
            DetailJson = JsonSerializer.Serialize(new
            {
                job.Id,
                job.ScheduledFor,
                job.RollbackOnFailure,
                job.SimulateFailure
            })
        }, ct);

        return job;
    }

    public async Task<ScheduledDeploy> CancelAsync(Guid scheduleId, string actor, CancellationToken ct = default)
    {
        ScheduledDeploy job;
        lock (store.Sync)
        {
            job = store.ScheduledDeploys.FirstOrDefault(s => s.Id == scheduleId)
                  ?? throw new KeyNotFoundException("Scheduled deploy not found.");

            if (job.Status != ScheduledDeployStatus.Pending)
            {
                throw new InvalidOperationException($"Cannot cancel schedule in status {job.Status}.");
            }
        }

        if (job.ObjectKind == ScheduledDeployObjectKind.Gpo)
        {
            await gpoWorkflow.EnsureCanDeployAsync(job.ObjectId, actor, ct);
        }
        else
        {
            await containerWorkflow.EnsureCanDeployAsync(job.ObjectId, actor, ct);
        }

        lock (store.Sync)
        {
            job.Status = ScheduledDeployStatus.Cancelled;
            job.CancelledAt = DateTimeOffset.UtcNow;
            job.CancelledBy = actor;
            job.CompletedAt = DateTimeOffset.UtcNow;

            if (job.ObjectKind == ScheduledDeployObjectKind.Gpo)
            {
                var gpo = store.Gpos.FirstOrDefault(g => g.Id == job.ObjectId);
                if (gpo is not null && gpo.ActiveScheduleId == job.Id)
                {
                    gpo.Status = ControlledGpoStatus.Approved;
                    gpo.ActiveScheduleId = null;
                }
            }
            else
            {
                var container = store.Containers.FirstOrDefault(c => c.Id == job.ObjectId);
                if (container is not null && container.ActiveScheduleId == job.Id)
                {
                    container.Status = ControlledGpoStatus.Approved;
                    container.ActiveScheduleId = null;
                }
            }
        }

        await audit.WriteAsync(new AuditEvent
        {
            Actor = actor,
            Action = job.ObjectKind == ScheduledDeployObjectKind.Gpo
                ? "Gpo.CancelSchedule"
                : "Container.CancelSchedule",
            ObjectKey = job.ObjectKey,
            Outcome = "Success",
            DetailJson = JsonSerializer.Serialize(new { job.Id })
        }, ct);

        return job;
    }

    public async Task<IReadOnlyList<ScheduledDeploy>> ProcessDueAsync(CancellationToken ct = default)
    {
        List<Guid> dueIds;
        lock (store.Sync)
        {
            var now = DateTimeOffset.UtcNow;
            dueIds = store.ScheduledDeploys
                .Where(s => s.Status == ScheduledDeployStatus.Pending && s.ScheduledFor <= now)
                .OrderBy(s => s.ScheduledFor)
                .Select(s => s.Id)
                .ToList();
        }

        var processed = new List<ScheduledDeploy>();
        foreach (var id in dueIds)
        {
            processed.Add(await RunOneAsync(id, ct));
        }

        return processed;
    }

    private async Task<ScheduledDeploy> RunOneAsync(Guid scheduleId, CancellationToken ct)
    {
        ScheduledDeploy job;
        lock (store.Sync)
        {
            job = store.ScheduledDeploys.FirstOrDefault(s => s.Id == scheduleId)
                  ?? throw new KeyNotFoundException("Scheduled deploy not found.");

            if (job.Status != ScheduledDeployStatus.Pending)
            {
                return job;
            }

            job.Status = ScheduledDeployStatus.Running;
            job.StartedAt = DateTimeOffset.UtcNow;
        }

        var options = new DeployOptions
        {
            RollbackOnFailure = job.RollbackOnFailure,
            SimulateFailure = job.SimulateFailure
        };

        try
        {
            if (job.ObjectKind == ScheduledDeployObjectKind.Gpo)
            {
                await gpoWorkflow.DeployAsync(job.ObjectId, job.ScheduledBy, options, ct);
            }
            else
            {
                await containerWorkflow.DeployAsync(job.ObjectId, job.ScheduledBy, options, ct);
            }

            lock (store.Sync)
            {
                job.Status = ScheduledDeployStatus.Succeeded;
                job.CompletedAt = DateTimeOffset.UtcNow;
                job.ResultDetailJson = JsonSerializer.Serialize(new { outcome = "Succeeded" });
            }

            logger.LogInformation("Scheduled deploy {Id} succeeded for {ObjectKey}.", job.Id, job.ObjectKey);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            var rolledBack = job.RollbackOnFailure &&
                             ex.Message.Contains("rolled back", StringComparison.OrdinalIgnoreCase);

            lock (store.Sync)
            {
                job.Status = rolledBack ? ScheduledDeployStatus.RolledBack : ScheduledDeployStatus.Failed;
                job.CompletedAt = DateTimeOffset.UtcNow;
                job.ErrorMessage = ex.Message;
                job.ResultDetailJson = JsonSerializer.Serialize(new
                {
                    outcome = job.Status.ToString(),
                    error = ex.Message
                });

                // Deploy paths clear ActiveScheduleId; ensure object is not stuck in Scheduled.
                if (job.ObjectKind == ScheduledDeployObjectKind.Gpo)
                {
                    var gpo = store.Gpos.FirstOrDefault(g => g.Id == job.ObjectId);
                    if (gpo is not null && gpo.Status == ControlledGpoStatus.Scheduled)
                    {
                        gpo.Status = rolledBack ? ControlledGpoStatus.UnderControl : ControlledGpoStatus.Approved;
                        gpo.ActiveScheduleId = null;
                    }
                }
                else
                {
                    var container = store.Containers.FirstOrDefault(c => c.Id == job.ObjectId);
                    if (container is not null && container.Status == ControlledGpoStatus.Scheduled)
                    {
                        container.Status = rolledBack
                            ? ControlledGpoStatus.UnderControl
                            : ControlledGpoStatus.Approved;
                        container.ActiveScheduleId = null;
                    }
                }
            }

            logger.LogWarning(ex, "Scheduled deploy {Id} ended as {Status}.", job.Id, job.Status);

            await audit.WriteAsync(new AuditEvent
            {
                Actor = job.ScheduledBy,
                Action = "ScheduledDeploy.Process",
                ObjectKey = job.ObjectKey,
                Outcome = job.Status.ToString(),
                DetailJson = job.ResultDetailJson
            }, ct);
        }

        return job;
    }

    private static void ValidateScheduleRequest(ScheduleDeployRequest request)
    {
        if (request.ScheduledFor == default)
        {
            throw new ArgumentException("ScheduledFor is required.", nameof(request));
        }

        // Allow slight clock skew; reject schedules more than 1 minute in the past.
        if (request.ScheduledFor < DateTimeOffset.UtcNow.AddMinutes(-1))
        {
            throw new ArgumentException("ScheduledFor must be in the future.", nameof(request));
        }
    }
}
