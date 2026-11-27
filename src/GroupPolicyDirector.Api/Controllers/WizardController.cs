using GroupPolicyDirector.Core.Authz;
using GroupPolicyDirector.Core.Domain;
using GroupPolicyDirector.Infrastructure.Auth;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace GroupPolicyDirector.Api.Controllers;

[ApiController]
[Route("api/wizard")]
[Authorize]
public sealed class WizardController(
    IGeneralSettingsService general,
    IDelegationService delegation) : ControllerBase
{
    private string Actor
    {
        get
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

    [HttpGet]
    public async Task<IActionResult> Get(CancellationToken ct)
    {
        await delegation.EnsureDemoTrusteesSeededAsync(ct);
        var canManage = await delegation.IsProductAdministratorAsync(Actor, ct);
        return Ok(general.GetWizard(Actor, canManage));
    }

    [HttpPost("complete")]
    public async Task<IActionResult> Complete(CancellationToken ct)
    {
        try
        {
            await delegation.EnsureDemoTrusteesSeededAsync(ct);
            var canManage = await delegation.IsProductAdministratorAsync(Actor, ct);
            return Ok(general.CompleteWizard(Actor, canManage));
        }
        catch (UnauthorizedAccessException ex)
        {
            return StatusCode(StatusCodes.Status403Forbidden, new { error = ex.Message });
        }
    }

    [HttpPost("skip")]
    public async Task<IActionResult> Skip(CancellationToken ct)
    {
        try
        {
            await delegation.EnsureDemoTrusteesSeededAsync(ct);
            var canManage = await delegation.IsProductAdministratorAsync(Actor, ct);
            return Ok(general.SkipWizard(Actor, canManage));
        }
        catch (UnauthorizedAccessException ex)
        {
            return StatusCode(StatusCodes.Status403Forbidden, new { error = ex.Message });
        }
    }
}
