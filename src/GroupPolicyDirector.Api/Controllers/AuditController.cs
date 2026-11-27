using GroupPolicyDirector.Core.Domain;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace GroupPolicyDirector.Api.Controllers;

[ApiController]
[Route("api/audit")]
[Authorize]
public sealed class AuditController(IAuditService audit) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> List([FromQuery] int take = 100, CancellationToken ct = default) =>
        Ok(await audit.ListAsync(take, ct));
}
