using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace GroupPolicyDirector.Api.Controllers;

[ApiController]
[Route("api/health")]
public sealed class HealthController : ControllerBase
{
    [HttpGet]
    [AllowAnonymous]
    public IActionResult Get() =>
        Ok(new
        {
            product = "Group Policy Director",
            slice = 1,
            status = "Healthy",
            utc = DateTimeOffset.UtcNow
        });
}
