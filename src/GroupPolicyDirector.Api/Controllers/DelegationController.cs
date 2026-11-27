using GroupPolicyDirector.Core.Authz;
using GroupPolicyDirector.Infrastructure.Auth;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace GroupPolicyDirector.Api.Controllers;

[ApiController]
[Route("api/delegation")]
[Authorize]
public sealed class DelegationController(IDelegationService delegation) : ControllerBase
{
    private string Actor => User.Identity?.Name ?? WindowsAuthPlaceholders.DevProductAdmin;

    [HttpGet("product-roles")]
    public async Task<IActionResult> ListProductRoles(CancellationToken ct) =>
        Ok(await delegation.ListProductRolesAsync(ct));

    public sealed record GrantProductRoleRequest(ProductRole Role, string TrusteePrincipal);

    [HttpPost("product-roles")]
    public async Task<IActionResult> GrantProductRole([FromBody] GrantProductRoleRequest request, CancellationToken ct)
    {
        try
        {
            var result = await delegation.GrantProductRoleAsync(request.Role, request.TrusteePrincipal, Actor, ct);
            return Ok(result);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    [HttpDelete("product-roles/{id:guid}")]
    public async Task<IActionResult> RevokeProductRole(Guid id, CancellationToken ct)
    {
        try
        {
            var ok = await delegation.RevokeProductRoleAsync(id, Actor, ct);
            return ok ? NoContent() : NotFound();
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    [HttpGet("object-roles")]
    public async Task<IActionResult> ListObjectRoles(CancellationToken ct) =>
        Ok(await delegation.ListObjectDelegationsAsync(ct));

    public sealed record GrantObjectRoleRequest(
        DelegationScopeKind ScopeKind,
        string ScopeKey,
        ObjectRole Role,
        string TrusteePrincipal,
        bool InheritToChildren = true);

    [HttpPost("object-roles")]
    public async Task<IActionResult> GrantObjectRole([FromBody] GrantObjectRoleRequest request, CancellationToken ct)
    {
        try
        {
            var result = await delegation.GrantObjectRoleAsync(
                request.ScopeKind,
                request.ScopeKey,
                request.Role,
                request.TrusteePrincipal,
                Actor,
                request.InheritToChildren,
                ct);
            return Ok(result);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    [HttpDelete("object-roles/{id:guid}")]
    public async Task<IActionResult> RevokeObjectRole(Guid id, CancellationToken ct)
    {
        try
        {
            var ok = await delegation.RevokeObjectRoleAsync(id, Actor, ct);
            return ok ? NoContent() : NotFound();
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    [HttpGet("suggested-trustees")]
    public async Task<IActionResult> SuggestedTrustees(CancellationToken ct) =>
        Ok(await delegation.ListSuggestedTrusteesAsync(ct));

    [HttpGet("scope-hints")]
    public async Task<IActionResult> ScopeHints(CancellationToken ct) =>
        Ok(await delegation.ListScopeHintsAsync(ct));
}
