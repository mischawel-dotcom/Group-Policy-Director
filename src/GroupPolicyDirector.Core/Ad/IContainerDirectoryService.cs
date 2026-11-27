namespace GroupPolicyDirector.Core.Ad;

/// <summary>
/// AD boundary for container gpLink / gpOptions Take Control and Deploy.
/// DryRun when not domain-joined; Live reads/writes LDAP attributes when available.
/// </summary>
public interface IContainerDirectoryService
{
    Task<AdOperationResult> TakeControlAsync(TakeControlContainerAdRequest request, CancellationToken ct = default);

    Task<ContainerLinksFingerprint?> ReadLinksAsync(
        string domainDnsName,
        string containerKey,
        CancellationToken ct = default);

    Task<AdOperationResult> DeployLinksAsync(DeployContainerLinksAdRequest request, CancellationToken ct = default);

    Task SetControlledBaselineAsync(
        string containerKey,
        string linksHash,
        Domain.ContainerLinksState? links,
        CancellationToken ct = default);
}
