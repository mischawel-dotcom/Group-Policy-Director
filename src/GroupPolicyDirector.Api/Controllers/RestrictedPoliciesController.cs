using GroupPolicyDirector.Core.Authz;
using GroupPolicyDirector.Infrastructure.Auth;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace GroupPolicyDirector.Api.Controllers;

[ApiController]
[Route("api/restricted-policies")]
[Authorize]
public sealed class RestrictedPoliciesController(IRestrictedPolicyService restricted) : ControllerBase
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
        Ok(await restricted.ListAsync(ct));

    [HttpGet("area-hints")]
    public async Task<IActionResult> AreaHints(CancellationToken ct) =>
        Ok(await restricted.ListAreaPathHintsAsync(ct));

    public sealed record UpsertRequest(string AreaPath, List<string>? AllowedTrustees, string? Label);

    [HttpPost]
    public async Task<IActionResult> Upsert([FromBody] UpsertRequest request, CancellationToken ct)
    {
        try
        {
            var area = await restricted.UpsertAsync(
                request.AreaPath,
                request.AllowedTrustees ?? [],
                Actor,
                request.Label,
                ct);
            return Ok(area);
        }
        catch (UnauthorizedAccessException ex)
        {
            return StatusCode(StatusCodes.Status403Forbidden, new { error = ex.Message });
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        try
        {
            var ok = await restricted.DeleteAsync(id, Actor, ct);
            return ok ? NoContent() : NotFound();
        }
        catch (UnauthorizedAccessException ex)
        {
            return StatusCode(StatusCodes.Status403Forbidden, new { error = ex.Message });
        }
    }
}
