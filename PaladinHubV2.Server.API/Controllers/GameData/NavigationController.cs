using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PaladinHubV2.Server.Common.Models.Navigation;
using PaladinHubV2.Server.Data;
using PaladinHubV2.Server.Domain.Services.Navigation;

namespace PaladinHubV2.Server.API.Controllers.GameData;

[ApiController, Authorize(Roles = "Admin"), Route("Admin/api/navigation")]
public sealed class NavigationController(AppDbContext db) : ControllerBase
{
    private readonly NavigationAdminService service = new(db);
    private string Actor => User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "admin";

    [HttpGet]
    public async Task<IActionResult> List(CancellationToken ct) => Ok(await service.List(ct));

    [AllowAnonymous, HttpGet("/api/navigation")]
    public async Task<IActionResult> Public(CancellationToken ct) => Ok(await service.Public(ct));

    [HttpGet("{id:int}/history")]
    public async Task<IActionResult> History(int id, CancellationToken ct) => Ok(await service.History(id, ct));

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(NavigationRequest request, CancellationToken ct) => Map(await service.Create(request, Actor, ct));

    [HttpPut("{id:int}"), ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(int id, NavigationRequest request, CancellationToken ct) => Map(await service.Edit(id, request, Actor, ct));

    [HttpDelete("{id:int}"), ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(int id, [FromQuery] int version, CancellationToken ct) => Map(await service.Delete(id, version, Actor, ct));

    [HttpPost("{id:int}/restore"), ValidateAntiForgeryToken]
    public async Task<IActionResult> Restore(int id, RestoreNavigationRequest request, CancellationToken ct) => Map(await service.Restore(id, request, Actor, ct));

    private IActionResult Map(NavigationResult result) => result.Error switch {
        NavigationError.Validation => BadRequest(new { message = result.Message }),
        NavigationError.Conflict => Conflict(new { message = result.Message }),
        NavigationError.NotFound => NotFound(),
        _ => result.Link is null ? NoContent() : Ok(result.Link)
    };
}
