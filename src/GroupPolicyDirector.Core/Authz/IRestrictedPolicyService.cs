using GroupPolicyDirector.Core.Domain;

namespace GroupPolicyDirector.Core.Authz;

public interface IRestrictedPolicyService
{
    Task<IReadOnlyList<RestrictedPolicyArea>> ListAsync(CancellationToken ct = default);
    Task<IReadOnlyList<string>> ListAreaPathHintsAsync(CancellationToken ct = default);
    Task<RestrictedPolicyArea> UpsertAsync(
        string areaPath,
        IReadOnlyList<string> allowedTrustees,
        string actor,
        string? label = null,
        CancellationToken ct = default);
    Task<bool> DeleteAsync(Guid id, string actor, CancellationToken ct = default);
    /// <summary>True if actor may edit settings under the given policy path (area-level).</summary>
    Task<bool> CanEditPathAsync(string windowsIdentity, string settingPath, CancellationToken ct = default);
    /// <summary>
    /// Throws if any of the setting paths fall under a restricted area the actor may not edit.
    /// </summary>
    Task EnsurePathsEditableAsync(
        string windowsIdentity,
        IEnumerable<string> settingPaths,
        CancellationToken ct = default);
    /// <summary>
    /// Compare working tree to last deployed baseline; throw if restricted-area drift exists
    /// for an actor who is not allowed on those areas (check-in guard).
    /// </summary>
    Task EnsureCheckInAllowedAsync(
        string windowsIdentity,
        PolicyTreeNode? baseline,
        PolicyTreeNode working,
        CancellationToken ct = default);
}
