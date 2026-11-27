using GroupPolicyDirector.Core.Authz;
using GroupPolicyDirector.Infrastructure.Auth;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace GroupPolicyDirector.Api.Controllers;

[ApiController]
[Route("api/break-glass")]
[Authorize]
public sealed class BreakGlassController(IBreakGlassService breakGlass) : ControllerBase
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
    public async Task<IActionResult> List(CancellationToken ct) =>
        Ok(await breakGlass.ListSessionsAsync(ct));

    [HttpGet("active")]
    public async Task<IActionResult> Active(CancellationToken ct)
    {
        var session = await breakGlass.GetActiveSessionAsync(Actor, ct);
        return Ok(new
        {
            active = session is not null,
            session
        });
    }

    public sealed record ActivateRequest(string TrusteePrincipal, string Reason, int? DurationMinutes);

    [HttpPost("activate")]
    public async Task<IActionResult> Activate([FromBody] ActivateRequest request, CancellationToken ct)
    {
        try
        {
            TimeSpan? duration = request.DurationMinutes is > 0
                ? TimeSpan.FromMinutes(request.DurationMinutes.Value)
                : null;
            var session = await breakGlass.ActivateAsync(
                request.TrusteePrincipal,
                Actor,
                request.Reason,
                duration,
                ct);
            return Ok(session);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    [HttpPost("{id:guid}/revoke")]
    public async Task<IActionResult> Revoke(Guid id, CancellationToken ct)
    {
        try
        {
            var ok = await breakGlass.RevokeAsync(id, Actor, ct);
            return ok ? NoContent() : NotFound();
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }
}
