using GroupPolicyDirector.Core.Workflow;
using GroupPolicyDirector.Infrastructure.Auth;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace GroupPolicyDirector.Api.Controllers;

[ApiController]
[Route("api/gpos")]
[Authorize]
public sealed class GposController(IGpoWorkflowService workflow) : ControllerBase
{
    private string Actor => User.Identity?.Name ?? WindowsAuthPlaceholders.DevProductAdmin;

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
        catch (Exception ex) when (ex is InvalidOperationException or KeyNotFoundException)
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

    public sealed record CheckInRequest(string Comment);

    [HttpPost("{id:guid}/check-in")]
    public async Task<IActionResult> CheckIn(Guid id, [FromBody] CheckInRequest request, CancellationToken ct)
    {
        try
        {
            var version = await workflow.CheckInAsync(id, request.Comment, Actor, ct);
            return Ok(version);
        }
        catch (Exception ex) when (ex is InvalidOperationException or ArgumentException or KeyNotFoundException)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    [HttpGet("{id:guid}/history")]
    public async Task<IActionResult> History(Guid id, CancellationToken ct) =>
        Ok(await workflow.GetHistoryAsync(id, ct));
}
