using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PaladinHubV2.Server.Common.Models.GameData;
using PaladinHubV2.Server.Data;
using PaladinHubV2.Server.Domain.Services.GameData;
using PaladinHubV2.Server.Domain.Services.GameDataAdmin;

namespace PaladinHubV2.Server.API.Controllers.GameData;

[ApiController, Authorize(Roles = "Admin"), Route("Admin/api/tags")]
public sealed class TagsController : ControllerBase
{
	private readonly TagAdminService _tags;

	public TagsController(
		AppDbContext db,
		GameDataAssignmentService assignments)
	{
		_tags = new TagAdminService(db, assignments);
	}

	[HttpGet]
	public async Task<IActionResult> List(CancellationToken ct)
	{
		return Ok(await _tags.ListAsync(ct));
	}

	[HttpGet("{id:int}/history")]
	public async Task<IActionResult> History(
		int id,
		CancellationToken ct)
	{
		return Ok(await _tags.HistoryAsync(id, ct));
	}

	[HttpPost, ValidateAntiForgeryToken]
	public async Task<IActionResult> Create(
		TagRequest request,
		CancellationToken ct)
	{
		TagAdminResult result = await _tags.CreateAsync(
			request,
			Actor(),
			ct);

		if (result.Error == TagAdminError.Validation)
		{
			return BadRequest(new { message = result.Message });
		}

		return Ok(result.Tag);
	}

	[HttpPut("{id:int}"), ValidateAntiForgeryToken]
	public async Task<IActionResult> Edit(
		int id,
		TagRequest request,
		CancellationToken ct)
	{
		TagAdminResult result = await _tags.UpdateAsync(
			id,
			request,
			Actor(),
			ct);

		return result.Error switch
		{
			TagAdminError.None => Ok(result.Tag),
			TagAdminError.NotFound => NotFound(),
			TagAdminError.Stale => Stale(),
			TagAdminError.Validation =>
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
		TagAdminResult result = await _tags.DeleteAsync(
			id,
			version,
			Actor(),
			ct);

		return result.Error switch
		{
			TagAdminError.None => NoContent(),
			TagAdminError.NotFound => NotFound(),
			TagAdminError.Stale => Stale(),
			TagAdminError.InUse =>
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
		TagAdminResult result = await _tags.RestoreAsync(
			id,
			request,
			Actor(),
			ct);

		return result.Error switch
		{
			TagAdminError.None => Ok(result.Tag),
			TagAdminError.NotFound or
			TagAdminError.RevisionNotFound => NotFound(),
			TagAdminError.Stale => Stale(),
			TagAdminError.DeletedRevision =>
				BadRequest(new { message = result.Message }),
			TagAdminError.Validation =>
				Conflict(new { message = result.Message }),
			_ => Conflict()
		};
	}

	private IActionResult Stale()
	{
		return Conflict(new
		{
			message =
				"This tag changed in another session. Refresh before saving."
		});
	}

	private string Actor()
	{
		return User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "admin";
	}
}
