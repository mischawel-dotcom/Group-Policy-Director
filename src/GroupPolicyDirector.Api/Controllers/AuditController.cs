using System.Text;
using GroupPolicyDirector.Core.Authz;
using GroupPolicyDirector.Core.Domain;
using GroupPolicyDirector.Infrastructure.Auth;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace GroupPolicyDirector.Api.Controllers;

[ApiController]
[Route("api/audit")]
[Authorize]
public sealed class AuditController(IAuditService audit, IDelegationService delegation) : ControllerBase
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
    public async Task<IActionResult> List(
        [FromQuery] string? objectKey = null,
        [FromQuery] string? actor = null,
        [FromQuery] string? action = null,
        [FromQuery] DateTimeOffset? from = null,
        [FromQuery] DateTimeOffset? to = null,
        [FromQuery] int take = 200,
        CancellationToken ct = default)
    {
        if (!await CanReadAuditAsync(ct))
        {
            return Forbid();
        }

        var items = await audit.QueryAsync(new AuditQuery
        {
            ObjectKey = objectKey,
            Actor = actor,
            Action = action,
            From = from,
            To = to,
            Take = take
        }, ct);
        return Ok(items);
    }

    [HttpGet("export")]
    public async Task<IActionResult> ExportCsv(
        [FromQuery] string? objectKey = null,
        [FromQuery] string? actor = null,
        [FromQuery] string? action = null,
        [FromQuery] DateTimeOffset? from = null,
        [FromQuery] DateTimeOffset? to = null,
        [FromQuery] int take = 5000,
        CancellationToken ct = default)
    {
        if (!await CanReadAuditAsync(ct))
        {
            return Forbid();
        }

        var csv = await audit.ExportCsvAsync(new AuditQuery
        {
            ObjectKey = objectKey,
            Actor = actor,
            Action = action,
            From = from,
            To = to,
            Take = take
        }, ct);

        var bytes = Encoding.UTF8.GetBytes(csv);
        var fileName = $"gpd-audit-{DateTimeOffset.UtcNow:yyyyMMdd-HHmmss}.csv";
        return File(bytes, "text/csv; charset=utf-8", fileName);
    }

    private async Task<bool> CanReadAuditAsync(CancellationToken ct)
    {
        // Product Admin, Auditor, or active Break Glass may read the audit log.
        if (await delegation.IsProductAdministratorAsync(Actor, ct) ||
            await delegation.IsAuditorAsync(Actor, ct) ||
            await delegation.IsProductAdminOrBreakGlassAsync(Actor, ct))
        {
            return true;
        }

        // Editors/Approvers/Deployers may also read (compliance visibility for their work).
        var roles = await delegation.ResolveEffectiveRolesAsync(Actor, ct);
        return roles.Count > 0;
    }
}
