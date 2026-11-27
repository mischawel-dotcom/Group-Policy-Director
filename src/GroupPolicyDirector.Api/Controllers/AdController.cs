using GroupPolicyDirector.Core.Ad;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace GroupPolicyDirector.Api.Controllers;

[ApiController]
[Route("api/ad")]
[Authorize]
public sealed class AdController(IGpoDirectoryService directory) : ControllerBase
{
    /// <summary>Domain-join / GPMC / dry-run detection for the host running GPD.</summary>
    [HttpGet("environment")]
    public async Task<IActionResult> Environment(CancellationToken ct) =>
        Ok(await directory.GetEnvironmentAsync(ct));

    public sealed record SimulateDriftRequest(string ProductionGpoId);

    /// <summary>
    /// Demo helper: mutate dry-run live fingerprint so OOB scan detects drift.
    /// Refuses to mutate when live AD mode is active.
    /// </summary>
    [HttpPost("simulate-drift")]
    public async Task<IActionResult> SimulateDrift([FromBody] SimulateDriftRequest request, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.ProductionGpoId))
        {
            return BadRequest(new { error = "ProductionGpoId is required." });
        }

        var ok = await directory.SimulateLiveDriftAsync(request.ProductionGpoId, ct);
        if (!ok)
        {
            return Conflict(new
            {
                error = "Simulate drift is only available in dry-run mode for a known controlled GPO baseline."
            });
        }

        return Ok(new { simulated = true, request.ProductionGpoId });
    }
}
