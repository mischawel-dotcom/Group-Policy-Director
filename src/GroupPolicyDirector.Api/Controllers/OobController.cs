using GroupPolicyDirector.Core.Domain;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace GroupPolicyDirector.Api.Controllers;

[ApiController]
[Route("api/oob")]
[Authorize]
public sealed class OobController(IOobDetectionService oob) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> List(CancellationToken ct) =>
        Ok(await oob.ListAsync(ct));

    [HttpPost("scan")]
    public async Task<IActionResult> Scan(CancellationToken ct) =>
        Ok(await oob.ScanAsync(ct));
}
