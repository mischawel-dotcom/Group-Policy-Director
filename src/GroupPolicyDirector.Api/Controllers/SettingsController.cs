using System.Text.Json;
using GroupPolicyDirector.Core.Domain;
using GroupPolicyDirector.Core.Notifications;
using GroupPolicyDirector.Infrastructure.Auth;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using IAuditService = GroupPolicyDirector.Core.Domain.IAuditService;

namespace GroupPolicyDirector.Api.Controllers;

[ApiController]
[Route("api/settings")]
[Authorize]
public sealed class SettingsController(
    ISmtpSettingsService smtp,
    IGeneralSettingsService general,
    IEmailNotifier email,
    IAuditService audit) : ControllerBase
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

    [HttpGet("smtp")]
    public IActionResult GetSmtp() => Ok(smtp.GetView());

    [HttpPut("smtp")]
    public IActionResult PutSmtp([FromBody] SmtpSettingsUpdate update)
    {
        var view = smtp.Update(update, Actor);
        return Ok(view);
    }

    [HttpGet("general")]
    public IActionResult GetGeneral() => Ok(general.GetView());

    [HttpPut("general")]
    public IActionResult PutGeneral([FromBody] GeneralSettingsUpdate update)
    {
        try
        {
            return Ok(general.Update(update, Actor));
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    public sealed record SmtpTestRequest(string? To);

    [HttpPost("smtp/test")]
    public async Task<IActionResult> TestSmtp([FromBody] SmtpTestRequest? request, CancellationToken ct)
    {
        var runtime = smtp.GetRuntime();
        var to = !string.IsNullOrWhiteSpace(request?.To)
            ? new[] { request!.To!.Trim() }
            : runtime.AlertRecipients.ToArray();

        if (to.Length == 0)
        {
            return BadRequest(new { error = "No test recipient. Set AlertRecipients or pass To." });
        }

        await email.SendAsync(new EmailMessage
        {
            To = to,
            Subject = "[GPD] SMTP test — Group Policy Director",
            Body =
                "This is a test message from Group Policy Director SMTP settings.\n" +
                $"Actor: {Actor}\n" +
                $"Mode: {(string.IsNullOrWhiteSpace(runtime.Host) ? "LogOnly (Host empty)" : $"Smtp {runtime.Host}:{runtime.Port}")}\n" +
                "OOB auto-reset remains disabled (MVP).\n"
        }, ct);

        await audit.WriteAsync(new AuditEvent
        {
            Actor = Actor,
            Action = "Settings.SmtpTest",
            ObjectKey = "Smtp",
            Outcome = "Success",
            DetailJson = JsonSerializer.Serialize(new
            {
                to,
                logOnly = string.IsNullOrWhiteSpace(runtime.Host),
                autoReset = false
            })
        }, ct);

        var message = string.IsNullOrWhiteSpace(runtime.Host)
            ? $"Test logged only (Smtp:Host empty). Recipients: {string.Join(", ", to)}"
            : $"Test sent via {runtime.Host}:{runtime.Port} to {string.Join(", ", to)}";

        return Ok(new
        {
            ok = true,
            message,
            mode = string.IsNullOrWhiteSpace(runtime.Host) ? "LogOnly" : "Smtp",
            autoResetEnabled = false
        });
    }
}
