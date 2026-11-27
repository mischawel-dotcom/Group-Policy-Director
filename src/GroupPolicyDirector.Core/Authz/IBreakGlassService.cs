using GroupPolicyDirector.Core.Domain;

namespace GroupPolicyDirector.Core.Authz;

public interface IBreakGlassService
{
    Task<IReadOnlyList<BreakGlassSession>> ListSessionsAsync(CancellationToken ct = default);
    Task<BreakGlassSession?> GetActiveSessionAsync(string windowsIdentity, CancellationToken ct = default);
    Task<bool> HasActiveBreakGlassAsync(string windowsIdentity, CancellationToken ct = default);
    /// <summary>
    /// Product Admin activates a time-bound session for a trustee that holds ProductRole.BreakGlass.
    /// </summary>
    Task<BreakGlassSession> ActivateAsync(
        string trusteePrincipal,
        string activatedBy,
        string reason,
        TimeSpan? duration = null,
        CancellationToken ct = default);
    Task<bool> RevokeAsync(Guid sessionId, string revokedBy, CancellationToken ct = default);
}
