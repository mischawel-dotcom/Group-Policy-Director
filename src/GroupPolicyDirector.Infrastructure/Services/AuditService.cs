using GroupPolicyDirector.Core.Domain;
using GroupPolicyDirector.Infrastructure.Persistence;

namespace GroupPolicyDirector.Infrastructure.Services;

public sealed class AuditService(GpdStore store) : IAuditService
{
    public Task WriteAsync(AuditEvent evt, CancellationToken ct = default)
    {
        lock (store.Sync)
        {
            store.Audit.Add(evt);
        }

        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<AuditEvent>> ListAsync(int take = 100, CancellationToken ct = default)
    {
        lock (store.Sync)
        {
            IReadOnlyList<AuditEvent> items = store.Audit
                .OrderByDescending(a => a.OccurredAt)
                .Take(take)
                .ToList();
            return Task.FromResult(items);
        }
    }
}
