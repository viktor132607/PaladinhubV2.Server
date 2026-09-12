using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PaladinHubV2.Server.Common.Models.GameData;
using PaladinHubV2.Server.Data;
using PaladinHubV2.Server.Domain.Services.GameData;
using PaladinHubV2.Server.Domain.Services.GameDataAdmin;

namespace PaladinHubV2.Server.API.Controllers.GameData;

[ApiController, Authorize(Roles = "Admin"), Route("Admin/api/patches")]
public sealed class PatchesController : ControllerBase
{
	private readonly PatchAdminService _patches;

	public PatchesController(
		AppDbContext db,
		GameDataAssignmentService assignments)
	{
		_patches = new PatchAdminService(db, assignments);
	}

	[HttpGet]
	public async Task<IActionResult> List(CancellationToken ct)
	{
		return Ok(await _patches.ListAsync(ct));
	}

	[HttpGet("{id:int}/history")]
	public async Task<IActionResult> History(
		int id,
		CancellationToken ct)
	{
		return Ok(await _patches.HistoryAsync(id, ct));
	}

	[HttpPost, ValidateAntiForgeryToken]
	public async Task<IActionResult> Create(
		PatchRequest request,
		CancellationToken ct)
	{
		PatchAdminResult result = await _patches.CreateAsync(
			request,
			Actor(),
			ct);

		if (result.Error == PatchAdminError.Validation)
		{
			return BadRequest(new { message = result.Message });
		}

		return Ok(result.Patch);
	}

	[HttpPut("{id:int}"), ValidateAntiForgeryToken]
	public async Task<IActionResult> Edit(
		int id,
		PatchRequest request,
		CancellationToken ct)
	{
		PatchAdminResult result = await _patches.UpdateAsync(
			id,
			request,
			Actor(),
			ct);

		return result.Error switch
		{
			PatchAdminError.None => Ok(result.Patch),
			PatchAdminError.NotFound => NotFound(),
			PatchAdminError.Stale => Stale(),
			PatchAdminError.Validation =>
				BadRequest(new { message = result.Message }),
			_ => Conflict()
		};
	}

	[HttpDelete("{id:int}"), ValidateAntiForgeryToken]
	public async Task<IActionResult> Delete(
		int id,
		[FromQuery] int version,
		CancellationToken ct)
	{
		PatchAdminResult result = await _patches.DeleteAsync(
			id,
			version,
			Actor(),
			ct);

		return result.Error switch
		{
			PatchAdminError.None => NoContent(),
			PatchAdminError.NotFound => NotFound(),
			PatchAdminError.Stale => Stale(),
			PatchAdminError.InUse =>
				Conflict(new { message = result.Message }),
			_ => Conflict()
		};
	}

	[HttpPost("{id:int}/restore"), ValidateAntiForgeryToken]
	public async Task<IActionResult> Restore(
		int id,
		RevisionRestoreRequest request,
		CancellationToken ct)
	{
		PatchAdminResult result = await _patches.RestoreAsync(
			id,
			request,
			Actor(),
			ct);

		return result.Error switch
		{
			PatchAdminError.None => Ok(result.Patch),
			PatchAdminError.NotFound or
			PatchAdminError.RevisionNotFound => NotFound(),
			PatchAdminError.Stale => Stale(),
			PatchAdminError.DeletedRevision =>
				BadRequest(new { message = result.Message }),
			PatchAdminError.Validation =>
				Conflict(new { message = result.Message }),
			_ => Conflict()
		};
	}

	private IActionResult Stale()
	{
		return Conflict(new
		{
			message =
				"This patch changed in another session. Refresh before saving."
		});
	}

	private string Actor()
	{
		return User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "admin";
	}
}
