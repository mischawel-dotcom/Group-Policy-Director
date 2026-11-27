using GroupPolicyDirector.Core.Agpm;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace GroupPolicyDirector.Api.Controllers;

[ApiController]
[Route("api/agpm")]
[Authorize]
public sealed class AgpmController(IAgpmImportService agpm) : ControllerBase
{
    public sealed record DryRunBody(string ArchivePath, bool PrintOnly = true);

    /// <summary>AGPM history import dry-run (no writes in Slice 1).</summary>
    [HttpPost("import/dry-run")]
    public async Task<IActionResult> DryRun([FromBody] DryRunBody body, CancellationToken ct)
    {
        var report = await agpm.DryRunAsync(new AgpmDryRunRequest
        {
            ArchivePath = body.ArchivePath,
            PrintOnly = body.PrintOnly
        }, ct);
        return Ok(report);
    }
}
