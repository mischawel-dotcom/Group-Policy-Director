using GroupPolicyDirector.Core.Domain;
using GroupPolicyDirector.Core.Workflow;
using GroupPolicyDirector.Infrastructure.Auth;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace GroupPolicyDirector.Api.Controllers;

[ApiController]
[Route("api/containers")]
[Authorize]
public sealed class ContainersController(IContainerWorkflowService workflow) : ControllerBase
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
        Ok(await workflow.ListAsync(ct));

    public sealed record TakeControlRequest(
        Guid DomainId,
        AdContainerKind Kind,
        string ContainerKey,
        string DisplayName);

    [HttpPost("take-control")]
    public async Task<IActionResult> TakeControl([FromBody] TakeControlRequest request, CancellationToken ct)
    {
        try
        {
            var container = await workflow.TakeControlAsync(
                request.DomainId,
                request.Kind,
                request.ContainerKey,
                request.DisplayName,
                Actor,
                ct);
            return Ok(container);
        }
        catch (Exception ex) when (ex is InvalidOperationException or ArgumentException or KeyNotFoundException or UnauthorizedAccessException)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    [HttpPost("{id:guid}/checkout")]
    public async Task<IActionResult> Checkout(Guid id, CancellationToken ct)
    {
        try
        {
            return Ok(await workflow.CheckoutAsync(id, Actor, ct));
        }
        catch (UnauthorizedAccessException ex)
        {
            return StatusCode(StatusCodes.Status403Forbidden, new { error = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            return Conflict(new { error = ex.Message });
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { error = ex.Message });
        }
    }

    [HttpGet("{id:guid}/links")]
    public async Task<IActionResult> GetLinks(Guid id, CancellationToken ct)
    {
        try
        {
            return Ok(await workflow.GetLinksAsync(id, ct));
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { error = ex.Message });
        }
    }

    [HttpPut("{id:guid}/links")]
    public async Task<IActionResult> UpdateLinks(Guid id, [FromBody] ContainerLinksState links, CancellationToken ct)
    {
        try
        {
            return Ok(await workflow.UpdateLinksAsync(id, links, Actor, ct));
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
            return Ok(await workflow.CheckInAsync(id, request.Comment, Actor, ct));
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
            return Ok(await workflow.ApproveAsync(id, Actor, request?.Comment, ct));
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
            return Ok(await workflow.RejectAsync(id, Actor, request.Comment ?? string.Empty, ct));
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
            return Ok(await workflow.DeployAsync(id, Actor, ct));
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
            return Ok(await workflow.DiffVersionsAsync(id, from, to, ct));
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { error = ex.Message });
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }
}
