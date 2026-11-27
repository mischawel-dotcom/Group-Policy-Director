using GroupPolicyDirector.Core.Domain;
using GroupPolicyDirector.Infrastructure.Persistence;

namespace GroupPolicyDirector.Infrastructure.Services;

public sealed class DomainService(GpdStore store, IAuditService audit) : IDomainService
{
    public Task<IReadOnlyList<DomainRegistration>> ListAsync(CancellationToken ct = default)
    {
        lock (store.Sync)
        {
            IReadOnlyList<DomainRegistration> items = store.Domains.ToList();
            return Task.FromResult(items);
        }
    }

    public async Task<DomainRegistration> RegisterAsync(string dnsName, string netBiosName, string actor, CancellationToken ct = default)
    {
        var domain = new DomainRegistration
        {
            DnsName = dnsName.Trim(),
            NetBiosName = netBiosName.Trim(),
            RegisteredBy = actor
        };

        lock (store.Sync)
        {
            if (store.Domains.Any(d => d.DnsName.Equals(domain.DnsName, StringComparison.OrdinalIgnoreCase)))
            {
                throw new InvalidOperationException($"Domain '{domain.DnsName}' is already registered.");
            }

            store.Domains.Add(domain);
        }

        await audit.WriteAsync(new AuditEvent
        {
            Actor = actor,
            Action = "Domain.Register",
            ObjectKey = domain.DnsName,
            Outcome = "Success"
        }, ct);

        return domain;
    }
}
