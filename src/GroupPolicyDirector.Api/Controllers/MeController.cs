using GroupPolicyDirector.Core.Authz;
using GroupPolicyDirector.Infrastructure.Auth;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace GroupPolicyDirector.Api.Controllers;

[ApiController]
[Route("api/me")]
[Authorize]
public sealed class MeController(IDelegationService delegation) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> Get(CancellationToken ct)
    {
        var identity = User.Identity?.Name ?? WindowsAuthPlaceholders.DevProductAdmin;
        var roles = await delegation.ResolveEffectiveRolesAsync(identity, ct);
        return Ok(new
        {
            identity,
            authMode = User.FindFirst(WindowsAuthPlaceholders.AuthModeClaim)?.Value
                        ?? (User.Identity?.IsAuthenticated == true
                            ? WindowsAuthPlaceholders.NegotiateMode
                            : WindowsAuthPlaceholders.DevelopmentStubMode),
            effectiveRoles = roles
        });
    }
}
