using System.Text.Json;
using GroupPolicyDirector.Core.Authz;
using GroupPolicyDirector.Core.Domain;
using GroupPolicyDirector.Infrastructure.Persistence;

namespace GroupPolicyDirector.Infrastructure.Services;

/// <summary>
/// Time-bound Break Glass sessions: Product Admin activates for an eligible trustee;
/// active session bypasses object SoD for emergency Checkout/Approve/Deploy.
/// </summary>
public sealed class BreakGlassService(
    GpdStore store,
    IDelegationService delegation,
    IAuditService audit) : IBreakGlassService
{
    public static readonly TimeSpan DefaultDuration = TimeSpan.FromHours(1);
    public static readonly TimeSpan MaxDuration = TimeSpan.FromHours(8);

    public Task<IReadOnlyList<BreakGlassSession>> ListSessionsAsync(CancellationToken ct = default)
    {
        lock (store.Sync)
        {
            IReadOnlyList<BreakGlassSession> items = store.BreakGlassSessions
                .OrderByDescending(s => s.ActivatedAt)
                .ToList();
            return Task.FromResult(items);
        }
    }

    public async Task<BreakGlassSession?> GetActiveSessionAsync(string windowsIdentity, CancellationToken ct = default)
    {
        var principals = await delegation.ResolveMembershipPrincipalsAsync(windowsIdentity, ct);
        lock (store.Sync)
        {
            var now = DateTimeOffset.UtcNow;
            return store.BreakGlassSessions.FirstOrDefault(s =>
                s.RevokedAt is null &&
                s.ExpiresAt > now &&
                principals.Any(p => p.Equals(s.TrusteePrincipal, StringComparison.OrdinalIgnoreCase)));
        }
    }

    public async Task<bool> HasActiveBreakGlassAsync(string windowsIdentity, CancellationToken ct = default) =>
        await GetActiveSessionAsync(windowsIdentity, ct) is not null;

    public async Task<BreakGlassSession> ActivateAsync(
        string trusteePrincipal,
        string activatedBy,
        string reason,
        TimeSpan? duration = null,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(reason) || reason.Trim().Length < 8)
        {
            throw new InvalidOperationException("Break Glass activation requires a reason (min 8 characters).");
        }

        if (!await delegation.IsProductAdministratorAsync(activatedBy, ct))
        {
            throw new InvalidOperationException("Only Product Administrator may activate Break Glass (4-eyes assignment).");
        }

        var trustee = trusteePrincipal.Trim();
        if (!await delegation.HasBreakGlassEligibilityAsync(trustee, ct))
        {
            throw new InvalidOperationException(
                "Trustee must hold ProductRole.BreakGlass (or be a member of a BreakGlass trustee group) before activation.");
        }

        var span = duration ?? DefaultDuration;
        if (span < TimeSpan.FromMinutes(1) || span > MaxDuration)
        {
            throw new InvalidOperationException($"Break Glass duration must be between 1 minute and {MaxDuration.TotalHours:0} hours.");
        }

        lock (store.Sync)
        {
            var now = DateTimeOffset.UtcNow;
            foreach (var existing in store.BreakGlassSessions.Where(s =>
                         s.RevokedAt is null &&
                         s.ExpiresAt > now &&
                         s.TrusteePrincipal.Equals(trustee, StringComparison.OrdinalIgnoreCase)))
            {
                existing.RevokedAt = now;
                existing.RevokedBy = activatedBy;
            }
        }

        var session = new BreakGlassSession
        {
            TrusteePrincipal = trustee,
            ActivatedBy = activatedBy,
            ExpiresAt = DateTimeOffset.UtcNow.Add(span),
            Reason = reason.Trim()
        };

        lock (store.Sync)
        {
            store.BreakGlassSessions.Add(session);
        }

        await audit.WriteAsync(new AuditEvent
        {
            Actor = activatedBy,
            Action = "BreakGlass.Activate",
            ObjectKey = trustee,
            Outcome = "Success",
            DetailJson = JsonSerializer.Serialize(new
            {
                session.Id,
                session.ExpiresAt,
                session.Reason,
                durationMinutes = span.TotalMinutes
            })
        }, ct);

        return session;
    }

    public async Task<bool> RevokeAsync(Guid sessionId, string revokedBy, CancellationToken ct = default)
    {
        BreakGlassSession? session;
        lock (store.Sync)
        {
            session = store.BreakGlassSessions.FirstOrDefault(s => s.Id == sessionId);
            if (session is null)
            {
                return false;
            }
        }

        var isAdmin = await delegation.IsProductAdministratorAsync(revokedBy, ct);
        var principals = await delegation.ResolveMembershipPrincipalsAsync(revokedBy, ct);
        var isHolder = principals.Any(p =>
            p.Equals(session.TrusteePrincipal, StringComparison.OrdinalIgnoreCase));

        if (!isAdmin && !isHolder)
        {
            throw new InvalidOperationException("Only Product Administrator or the Break Glass holder may revoke the session.");
        }

        lock (store.Sync)
        {
            if (session.RevokedAt is not null)
            {
                return true;
            }

            session.RevokedAt = DateTimeOffset.UtcNow;
            session.RevokedBy = revokedBy;
        }

        await audit.WriteAsync(new AuditEvent
        {
            Actor = revokedBy,
            Action = "BreakGlass.Revoke",
            ObjectKey = session.TrusteePrincipal,
            Outcome = "Success",
            DetailJson = JsonSerializer.Serialize(new { sessionId, session.Reason })
        }, ct);

        return true;
    }
}
