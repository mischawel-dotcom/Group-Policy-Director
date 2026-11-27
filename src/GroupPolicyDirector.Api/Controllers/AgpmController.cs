using GroupPolicyDirector.Core.Agpm;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace GroupPolicyDirector.Api.Controllers;

[ApiController]
[Route("api/agpm")]
[Authorize]
public sealed class AgpmController(IAgpmImportService agpm, IWebHostEnvironment env) : ControllerBase
{
    public sealed record DryRunBody(string? ArchivePath, bool PrintOnly = true);

    /// <summary>AGPM history import dry-run from a server path (directory or .zip).</summary>
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

    /// <summary>AGPM dry-run against an uploaded archive zip (fixture OK).</summary>
    [HttpPost("import/dry-run/upload")]
    [RequestSizeLimit(32 * 1024 * 1024)]
    public async Task<IActionResult> DryRunUpload(IFormFile archive, [FromForm] bool printOnly = true, CancellationToken ct = default)
    {
        if (archive is null || archive.Length == 0)
        {
            return BadRequest(new { error = "Upload a non-empty .zip archive." });
        }

        await using var ms = new MemoryStream();
        await archive.CopyToAsync(ms, ct);
        var report = await agpm.DryRunAsync(new AgpmDryRunRequest
        {
            ArchiveZipBytes = ms.ToArray(),
            ArchiveFileName = archive.FileName,
            PrintOnly = printOnly
        }, ct);
        return Ok(report);
    }

    /// <summary>Resolve the bundled sample fixture path when present under content root / repo.</summary>
    [HttpGet("fixture")]
    public IActionResult FixtureInfo()
    {
        var candidates = new[]
        {
            Path.GetFullPath(Path.Combine(env.ContentRootPath, "..", "..", "fixtures", "agpm-history-sample.zip")),
            Path.GetFullPath(Path.Combine(env.ContentRootPath, "fixtures", "agpm-history-sample.zip")),
            Path.GetFullPath(Path.Combine(Directory.GetCurrentDirectory(), "fixtures", "agpm-history-sample.zip"))
        };

        var path = candidates.FirstOrDefault(System.IO.File.Exists);
        return Ok(new
        {
            available = path is not null,
            path,
            hint = "POST /api/agpm/import/dry-run with ArchivePath, or upload the zip via /import/dry-run/upload."
        });
    }
}
