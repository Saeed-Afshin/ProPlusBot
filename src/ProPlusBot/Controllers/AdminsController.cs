using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ProPlusBot.Auth;
using ProPlusBot.Models;
using ProPlusBot.Services;

namespace ProPlusBot.Controllers;

[ApiController]
[Route("api/admins")]
[Authorize(Policy = AuthConstants.Scheme)]
public class AdminsController(AdminManagementService adminService) : ControllerBase
{
    [HttpGet]
    [ProducesResponseType(typeof(List<AdminUserDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<List<AdminUserDto>>> List(CancellationToken ct)
    {
        if (!User.CanAccessAdminPanel())
            return Forbid();
        return Ok(await adminService.ListAsync(ct));
    }

    [HttpPost]
    [ProducesResponseType(typeof(AdminUserDto), StatusCodes.Status201Created)]
    public async Task<ActionResult<AdminUserDto>> Create([FromBody] CreateAdminRequest request, CancellationToken ct)
    {
        var role = User.GetAdminRole();
        if (role is null || !User.CanAccessAdminPanel())
            return Forbid();

        try
        {
            var created = await adminService.CreateAsync(request, role.Value, ct);
            return CreatedAtAction(nameof(List), created);
        }
        catch (UnauthorizedAccessException ex)
        {
            return Forbid(ex.Message);
        }
        catch (InvalidOperationException ex)
        {
            return Conflict(new { error = ex.Message });
        }
    }

    [HttpDelete("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> Deactivate(Guid id, CancellationToken ct)
    {
        var role = User.GetAdminRole();
        if (role is null || !User.CanAccessAdminPanel())
            return Forbid();

        try
        {
            var ok = await adminService.DeactivateAsync(id, role.Value, ct);
            return ok ? NoContent() : NotFound();
        }
        catch (UnauthorizedAccessException ex)
        {
            return Forbid(ex.Message);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }
}
