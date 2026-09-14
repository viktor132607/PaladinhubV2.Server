using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PaladinHubV2.Server.Data;
using PaladinHubV2.Server.Domain.Services.Roles;

namespace PaladinHubV2.Server.API.Controllers.Admin;

[ApiController]
[Authorize(Roles = "Admin")]
[AutoValidateAntiforgeryToken]
[Route("Admin/api/access-control")]
public sealed class AccessControlController : ControllerBase
{
    private readonly AccessControlAdminService _service;

    public AccessControlController(AppDbContext database)
    {
        string connectionString = database.Database.GetConnectionString()
            ?? throw new InvalidOperationException("Access-control database connection is unavailable.");
        _service = AccessControlAdminService.ForPostgres(connectionString);
    }

    [HttpGet("permissions")]
    public async Task<IActionResult> Permissions()
    {
        return Ok(await _service.GetPermissionCatalogAsync());
    }

    [HttpGet("roles")]
    public async Task<IActionResult> Roles(CancellationToken cancellationToken)
    {
        return Ok(await _service.ListRolesAsync(cancellationToken));
    }

    [HttpGet("roles/{roleId}")]
    public Task<IActionResult> Role([FromRoute] string roleId, CancellationToken cancellationToken)
    {
        return ExecuteAsync(() => _service.GetRoleAsync(roleId, cancellationToken));
    }

    [HttpPost("roles")]
    public Task<IActionResult> CreateRole([FromBody] CreateRoleRequest request, CancellationToken cancellationToken)
    {
        return ExecuteAsync(async () =>
        {
            RoleResponse created = await _service.CreateRoleAsync(request, Actor(), cancellationToken);
            return (IActionResult)CreatedAtAction(nameof(Role), new { roleId = created.Id }, created);
        });
    }

    [HttpPut("roles/{roleId}")]
    public Task<IActionResult> UpdateRole(
        [FromRoute] string roleId,
        [FromBody] UpdateRoleRequest request,
        CancellationToken cancellationToken)
    {
        return ExecuteAsync(async () =>
        {
            AccessControlActor actor = Actor();
            return await _service.UpdateRoleAsync(roleId, request, actor, cancellationToken);
        });
    }

    [HttpDelete("roles/{roleId}")]
    public Task<IActionResult> DeleteRole(
        [FromRoute] string roleId,
        [FromQuery] int version,
        CancellationToken cancellationToken)
    {
        return ExecuteAsync(async () =>
        {
            await _service.DeleteRoleAsync(roleId, version, Actor(), cancellationToken);
            return (IActionResult)NoContent();
        });
    }

    [HttpPut("roles/{roleId}/permissions")]
    public Task<IActionResult> ReplacePermissions(
        [FromRoute] string roleId,
        [FromBody] ReplaceRolePermissionsRequest request,
        CancellationToken cancellationToken)
    {
        return ExecuteAsync(async () =>
        {
            AccessControlActor actor = Actor();
            return await _service.ReplacePermissionsAsync(roleId, request, actor, cancellationToken);
        });
    }

    [HttpGet("roles/{roleId}/history")]
    public Task<IActionResult> History([FromRoute] string roleId, CancellationToken cancellationToken)
    {
        return ExecuteAsync(() => _service.GetHistoryAsync(roleId, cancellationToken));
    }

    [HttpPost("roles/{roleId}/restore")]
    public Task<IActionResult> Restore(
        [FromRoute] string roleId,
        [FromBody] RestoreRoleRevisionRequest request,
        CancellationToken cancellationToken)
    {
        return ExecuteAsync(async () =>
        {
            AccessControlActor actor = Actor();
            return await _service.RestoreRevisionAsync(roleId, request, actor, cancellationToken);
        });
    }

    [HttpGet("roles/{roleId}/users")]
    public Task<IActionResult> RoleUsers([FromRoute] string roleId, CancellationToken cancellationToken)
    {
        return ExecuteAsync(() => _service.GetRoleUsersAsync(roleId, cancellationToken));
    }

    [HttpGet("users")]
    public async Task<IActionResult> Users([FromQuery] string? search, CancellationToken cancellationToken)
    {
        return Ok(await _service.ListUsersAsync(search, cancellationToken));
    }

    [HttpPost("roles/{roleId}/users/{userId}")]
    public Task<IActionResult> AssignUser(
        [FromRoute] string roleId,
        [FromRoute] string userId,
        CancellationToken cancellationToken)
    {
        return ExecuteAsync(async () =>
        {
            AccessControlActor actor = Actor();
            await _service.AssignUserAsync(roleId, userId, actor, cancellationToken);
            return (IActionResult)NoContent();
        });
    }

    [HttpDelete("roles/{roleId}/users/{userId}")]
    public Task<IActionResult> RevokeUser(
        [FromRoute] string roleId,
        [FromRoute] string userId,
        CancellationToken cancellationToken)
    {
        return ExecuteAsync(async () =>
        {
            await _service.RevokeUserAsync(roleId, userId, Actor(), cancellationToken);
            return (IActionResult)NoContent();
        });
    }

    [HttpGet("audit")]
    public async Task<IActionResult> Audit(
        [FromQuery] string? roleId,
        [FromQuery] string? userId,
        CancellationToken cancellationToken)
    {
        return Ok(await _service.GetAuditAsync(roleId, userId, cancellationToken));
    }

    private AccessControlActor Actor()
    {
        string id = User.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? throw new AccessControlAdminException(AccessControlFailure.Conflict, "Authenticated administrator ID is unavailable.");
        string name = User.Identity?.Name ?? id;
        return new AccessControlActor(id, name);
    }

    private async Task<IActionResult> ExecuteAsync<T>(Func<Task<T>> operation)
    {
        try
        {
            T value = await operation();
            return value is IActionResult result ? result : Ok(value);
        }
        catch (AccessControlAdminException error)
        {
            return Failure(error);
        }
    }

    private IActionResult Failure(AccessControlAdminException error)
    {
        var body = new { message = error.Message };
        return error.Failure switch
        {
            AccessControlFailure.Validation => BadRequest(body),
            AccessControlFailure.NotFound => NotFound(body),
            AccessControlFailure.Conflict => Conflict(body),
            _ => StatusCode(StatusCodes.Status500InternalServerError, body)
        };
    }
}
