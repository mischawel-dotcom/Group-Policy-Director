using GroupPolicyDirector.Core.Domain;
using GroupPolicyDirector.Core.Workflow;
using GroupPolicyDirector.Infrastructure.Auth;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace GroupPolicyDirector.Api.Controllers;

[ApiController]
[Route("api/gpos")]
[Authorize]
public sealed class GposController(IGpoWorkflowService workflow, IDiffService diff) : ControllerBase
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
        Ok(await workflow.ListControlledAsync(ct));

    public sealed record TakeControlRequest(Guid DomainId, string ProductionGpoId, string DisplayName);

    [HttpPost("take-control")]
    public async Task<IActionResult> TakeControl([FromBody] TakeControlRequest request, CancellationToken ct)
    {
        try
        {
            var gpo = await workflow.TakeControlAsync(
                request.DomainId,
                request.ProductionGpoId,
                request.DisplayName,
                Actor,
                ct);
            return Ok(gpo);
        }
        catch (Exception ex) when (ex is InvalidOperationException or KeyNotFoundException or UnauthorizedAccessException)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    public sealed record CheckoutRequest(bool CheckoutAnyway = false);

    [HttpPost("{id:guid}/checkout")]
    public async Task<IActionResult> Checkout(Guid id, [FromBody] CheckoutRequest? request, CancellationToken ct)
    {
        try
        {
            var gpo = await workflow.CheckoutAsync(id, Actor, request?.CheckoutAnyway ?? false, ct);
            return Ok(gpo);
        }
        catch (UnauthorizedAccessException ex)
        {
            return StatusCode(StatusCodes.Status403Forbidden, new { error = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            return Conflict(new { error = ex.Message, oob = true });
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { error = ex.Message });
        }
    }

    [HttpGet("{id:guid}/settings-tree")]
    public async Task<IActionResult> SettingsTree(Guid id, CancellationToken ct)
    {
        try
        {
            return Ok(await workflow.GetSettingsTreeAsync(id, ct));
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { error = ex.Message });
        }
    }

    public sealed record UpdateSettingsRequest(List<PolicySettingUpdate> Updates);

    [HttpPut("{id:guid}/settings-tree")]
    public async Task<IActionResult> UpdateSettings(Guid id, [FromBody] UpdateSettingsRequest request, CancellationToken ct)
    {
        try
        {
            var tree = await workflow.UpdateSettingsAsync(id, request.Updates ?? [], Actor, ct);
            return Ok(tree);
        }
        catch (UnauthorizedAccessException ex)
        {
            return StatusCode(StatusCodes.Status403Forbidden, new { error = ex.Message });
        }
        catch (Exception ex) when (ex is InvalidOperationException or ArgumentException or KeyNotFoundException)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    public sealed record CheckInRequest(string Comment);

    [HttpPost("{id:guid}/check-in")]
    public async Task<IActionResult> CheckIn(Guid id, [FromBody] CheckInRequest request, CancellationToken ct)
    {
        try
        {
            var version = await workflow.CheckInAsync(id, request.Comment, Actor, ct);
            return Ok(version);
        }
        catch (Exception ex) when (ex is InvalidOperationException or ArgumentException or KeyNotFoundException or UnauthorizedAccessException)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    public sealed record ReviewRequest(string? Comment);

    [HttpPost("{id:guid}/approve")]
    public async Task<IActionResult> Approve(Guid id, [FromBody] ReviewRequest? request, CancellationToken ct)
    {
        try
        {
            var gpo = await workflow.ApproveAsync(id, Actor, request?.Comment, ct);
            return Ok(gpo);
        }
        catch (UnauthorizedAccessException ex)
        {
            return StatusCode(StatusCodes.Status403Forbidden, new { error = ex.Message });
        }
        catch (Exception ex) when (ex is InvalidOperationException or KeyNotFoundException)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    [HttpPost("{id:guid}/reject")]
    public async Task<IActionResult> Reject(Guid id, [FromBody] ReviewRequest request, CancellationToken ct)
    {
        try
        {
            var gpo = await workflow.RejectAsync(id, Actor, request.Comment ?? string.Empty, ct);
            return Ok(gpo);
        }
        catch (UnauthorizedAccessException ex)
        {
            return StatusCode(StatusCodes.Status403Forbidden, new { error = ex.Message });
        }
        catch (Exception ex) when (ex is InvalidOperationException or ArgumentException or KeyNotFoundException)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    [HttpPost("{id:guid}/deploy")]
    public async Task<IActionResult> Deploy(Guid id, CancellationToken ct)
    {
        try
        {
            var gpo = await workflow.DeployAsync(id, Actor, ct);
            return Ok(gpo);
        }
        catch (UnauthorizedAccessException ex)
        {
            return StatusCode(StatusCodes.Status403Forbidden, new { error = ex.Message });
        }
        catch (Exception ex) when (ex is InvalidOperationException or KeyNotFoundException)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    [HttpGet("{id:guid}/history")]
    public async Task<IActionResult> History(Guid id, CancellationToken ct) =>
        Ok(await workflow.GetHistoryAsync(id, ct));

    [HttpGet("{id:guid}/diff")]
    public async Task<IActionResult> Diff(Guid id, [FromQuery] int from, [FromQuery] int to, CancellationToken ct)
    {
        try
        {
            return Ok(await diff.DiffGpoVersionsAsync(id, from, to, ct));
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { error = ex.Message });
        }
    }
}
