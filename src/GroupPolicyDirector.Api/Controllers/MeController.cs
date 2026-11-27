using GroupPolicyDirector.Core.Authz;
using GroupPolicyDirector.Infrastructure.Auth;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace GroupPolicyDirector.Api.Controllers;

[ApiController]
[Route("api/me")]
[Authorize]
public sealed class MeController(IDelegationService delegation, IBreakGlassService breakGlass) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> Get(CancellationToken ct)
    {
        await delegation.EnsureDemoTrusteesSeededAsync(ct);

        var identity = ResolveActor();
        var roles = await delegation.ResolveEffectiveRolesAsync(identity, ct);
        var memberships = await delegation.ResolveMembershipPrincipalsAsync(identity, ct);
        var bg = await breakGlass.GetActiveSessionAsync(identity, ct);
        return Ok(new
        {
            identity,
            authMode = User.FindFirst(WindowsAuthPlaceholders.AuthModeClaim)?.Value
                        ?? (User.Identity?.IsAuthenticated == true
                            ? WindowsAuthPlaceholders.NegotiateMode
                            : WindowsAuthPlaceholders.DevelopmentStubMode),
            effectiveRoles = roles,
            membershipPrincipals = memberships,
            isAuditor = await delegation.IsAuditorAsync(identity, ct),
            isProductAdministrator = await delegation.IsProductAdministratorAsync(identity, ct),
            breakGlassEligible = await delegation.HasBreakGlassEligibilityAsync(identity, ct),
            breakGlassActive = bg is not null,
            activeBreakGlassSession = bg,
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
