using GroupPolicyDirector.Core.Ad;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace GroupPolicyDirector.Api.Controllers;

[ApiController]
[Route("api/health")]
public sealed class HealthController(IGpoDirectoryService directory) : ControllerBase
{
    [HttpGet]
    [AllowAnonymous]
    public async Task<IActionResult> Get(CancellationToken ct)
    {
        var env = await directory.GetEnvironmentAsync(ct);
        return Ok(new
        {
            product = "Group Policy Director",
            slice = 3,
            status = "Healthy",
            utc = DateTimeOffset.UtcNow,
            ad = new
            {
                env.EffectiveMode,
                env.IsDomainJoined,
                env.DirectoryReachable,
                env.GpmcAvailable,
                env.ModeReason
            }
        });
    }
}
