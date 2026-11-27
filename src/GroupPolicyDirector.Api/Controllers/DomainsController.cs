using GroupPolicyDirector.Core.Domain;
using GroupPolicyDirector.Infrastructure.Auth;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace GroupPolicyDirector.Api.Controllers;

[ApiController]
[Route("api/domains")]
[Authorize]
public sealed class DomainsController(IDomainService domains) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> List(CancellationToken ct) =>
        Ok(await domains.ListAsync(ct));

    public sealed record RegisterDomainRequest(string DnsName, string NetBiosName);

    [HttpPost]
    public async Task<IActionResult> Register([FromBody] RegisterDomainRequest request, CancellationToken ct)
    {
        var actor = User.Identity?.Name ?? WindowsAuthPlaceholders.DevProductAdmin;
        var domain = await domains.RegisterAsync(request.DnsName, request.NetBiosName, actor, ct);
        return CreatedAtAction(nameof(List), new { id = domain.Id }, domain);
    }
}
