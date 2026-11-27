using System.Globalization;
using System.Text;
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

    public Task<IReadOnlyList<AuditEvent>> ListAsync(int take = 100, CancellationToken ct = default) =>
        QueryAsync(new AuditQuery { Take = take }, ct);

    public Task<IReadOnlyList<AuditEvent>> QueryAsync(AuditQuery query, CancellationToken ct = default)
    {
        lock (store.Sync)
        {
            IEnumerable<AuditEvent> q = store.Audit;

            if (!string.IsNullOrWhiteSpace(query.ObjectKey))
            {
                var key = query.ObjectKey.Trim();
                q = q.Where(a =>
                    a.ObjectKey is not null &&
                    a.ObjectKey.Contains(key, StringComparison.OrdinalIgnoreCase));
            }

            if (!string.IsNullOrWhiteSpace(query.Actor))
            {
                var actor = query.Actor.Trim();
                q = q.Where(a => a.Actor.Contains(actor, StringComparison.OrdinalIgnoreCase));
            }

            if (!string.IsNullOrWhiteSpace(query.Action))
            {
                var action = query.Action.Trim();
                q = q.Where(a => a.Action.Contains(action, StringComparison.OrdinalIgnoreCase));
            }

            if (query.From is not null)
            {
                q = q.Where(a => a.OccurredAt >= query.From);
            }

            if (query.To is not null)
            {
                q = q.Where(a => a.OccurredAt <= query.To);
            }

            var take = query.Take <= 0 ? 200 : Math.Min(query.Take, 5000);
            IReadOnlyList<AuditEvent> items = q
                .OrderByDescending(a => a.OccurredAt)
                .Take(take)
                .ToList();
            return Task.FromResult(items);
        }
    }

    public async Task<string> ExportCsvAsync(AuditQuery query, CancellationToken ct = default)
    {
        var items = await QueryAsync(query, ct);
        var sb = new StringBuilder();
        sb.AppendLine("OccurredAt,Actor,Action,ObjectKey,Outcome,DetailJson");
        foreach (var e in items)
        {
            sb.Append(Csv(e.OccurredAt.ToString("O", CultureInfo.InvariantCulture))).Append(',');
            sb.Append(Csv(e.Actor)).Append(',');
            sb.Append(Csv(e.Action)).Append(',');
            sb.Append(Csv(e.ObjectKey ?? "")).Append(',');
            sb.Append(Csv(e.Outcome ?? "")).Append(',');
            sb.Append(Csv(e.DetailJson ?? ""));
            sb.AppendLine();
        }

        return sb.ToString();
    }

    private static string Csv(string value)
    {
        if (value.Contains('"') || value.Contains(',') || value.Contains('\n') || value.Contains('\r'))
        {
            return $"\"{value.Replace("\"", "\"\"")}\"";
        }

        return value;
    }
}
