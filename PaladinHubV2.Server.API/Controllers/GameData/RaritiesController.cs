using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PaladinHubV2.Server.Common.Models.GameData;
using PaladinHubV2.Server.Data;
using PaladinHubV2.Server.Domain.Services.GameData;
using PaladinHubV2.Server.Domain.Services.GameDataAdmin;

namespace PaladinHubV2.Server.API.Controllers.GameData;

[ApiController, Authorize(Roles = "Admin"), Route("Admin/api/rarities")]
public sealed class RaritiesController : ControllerBase
{
	private readonly RarityAdminService _rarities;

	public RaritiesController(
		AppDbContext db,
		GameDataAssignmentService assignments)
	{
		_rarities = new RarityAdminService(db, assignments);
	}

	[HttpGet]
	public async Task<IActionResult> List(CancellationToken ct)
	{
		return Ok(await _rarities.ListAsync(ct));
	}

	[HttpGet("{id:int}/history")]
	public async Task<IActionResult> History(
		int id,
		CancellationToken ct)
	{
		return Ok(await _rarities.HistoryAsync(id, ct));
	}

	[HttpPost, ValidateAntiForgeryToken]
	public async Task<IActionResult> Create(
		RarityRequest request,
		CancellationToken ct)
	{
		RarityAdminResult result = await _rarities.CreateAsync(
			request,
			Actor(),
			ct);

		if (result.Error == RarityAdminError.Validation)
		{
			return BadRequest(new { message = result.Message });
		}

		return Ok(result.Rarity);
	}

	[HttpPut("{id:int}"), ValidateAntiForgeryToken]
	public async Task<IActionResult> Edit(
		int id,
		RarityRequest request,
		CancellationToken ct)
	{
		RarityAdminResult result = await _rarities.UpdateAsync(
			id,
			request,
			Actor(),
			ct);

		return result.Error switch
		{
			RarityAdminError.None => Ok(result.Rarity),
			RarityAdminError.NotFound => NotFound(),
			RarityAdminError.Stale => Stale(),
			RarityAdminError.Validation =>
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
		RarityAdminResult result = await _rarities.DeleteAsync(
			id,
			version,
			Actor(),
			ct);

		return result.Error switch
		{
			RarityAdminError.None => NoContent(),
			RarityAdminError.NotFound => NotFound(),
			RarityAdminError.Stale => Stale(),
			RarityAdminError.InUse =>
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
		RarityAdminResult result = await _rarities.RestoreAsync(
			id,
			request,
			Actor(),
			ct);

		return result.Error switch
		{
			RarityAdminError.None => Ok(result.Rarity),
			RarityAdminError.NotFound or
			RarityAdminError.RevisionNotFound => NotFound(),
			RarityAdminError.Stale => Stale(),
			RarityAdminError.DeletedRevision =>
				BadRequest(new { message = result.Message }),
			RarityAdminError.Validation =>
				Conflict(new { message = result.Message }),
			_ => Conflict()
		};
	}

	private IActionResult Stale()
	{
		return Conflict(new
		{
			message =
				"This rarity changed in another session. Refresh before saving."
		});
	}

	private string Actor()
	{
		return User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "admin";
	}
}
