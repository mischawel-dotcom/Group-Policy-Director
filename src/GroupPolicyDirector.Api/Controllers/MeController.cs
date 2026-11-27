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
        await delegation.EnsureDemoTrusteesSeededAsync(ct);

        var identity = ResolveActor();
        var roles = await delegation.ResolveEffectiveRolesAsync(identity, ct);
        var memberships = await delegation.ResolveMembershipPrincipalsAsync(identity, ct);
        return Ok(new
        {
            identity,
            authMode = User.FindFirst(WindowsAuthPlaceholders.AuthModeClaim)?.Value
                        ?? (User.Identity?.IsAuthenticated == true
                            ? WindowsAuthPlaceholders.NegotiateMode
                            : WindowsAuthPlaceholders.DevelopmentStubMode),
            effectiveRoles = roles,
            membershipPrincipals = memberships,
            demoActors = WindowsAuthPlaceholders.DemoActors
        });
    }

    private string ResolveActor()
    {
        if (Request.Headers.TryGetValue(WindowsAuthPlaceholders.ActorHeader, out var overrideActor))
        {
            var value = overrideActor.ToString().Trim();
            if (!string.IsNullOrWhiteSpace(value) &&
                WindowsAuthPlaceholders.DemoActors.Contains(value, StringComparer.OrdinalIgnoreCase))
            {
                return value;
            }
        }

        return User.Identity?.Name ?? WindowsAuthPlaceholders.DevProductAdmin;
    }
}
