using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PaladinHubV2.Server.Common.Models.GameData;
using PaladinHubV2.Server.Data;
using PaladinHubV2.Server.Domain.Services.GameData;
using PaladinHubV2.Server.Domain.Services.GameDataAdmin;

namespace PaladinHubV2.Server.API.Controllers.GameData;

[ApiController, Authorize(Roles = "Admin"), Route("Admin/api/media")]
public sealed class MediaController : ControllerBase
{
	private readonly MediaAdminService _media;

	public MediaController(
		AppDbContext db,
		GameDataAssignmentService assignments)
	{
		_media = new MediaAdminService(db, assignments);
	}

	[HttpGet]
	public async Task<IActionResult> List(
		string? search,
		string status = "active",
		int page = 1,
		int pageSize = 32,
		CancellationToken ct = default)
	{
		MediaPageResult result = await _media.ListAsync(
			search,
			status,
			page,
			pageSize,
			ct);

		return Ok(new
		{
			media = result.Media,
			page = result.Page,
			pages = result.Pages,
			total = result.Total
		});
	}

	[HttpGet("{id:guid}/history")]
	public async Task<IActionResult> History(
		Guid id,
		CancellationToken ct)
	{
		return Ok(await _media.HistoryAsync(id, ct));
	}

	[HttpPut("{id:guid}"), ValidateAntiForgeryToken]
	public async Task<IActionResult> Edit(
		Guid id,
		MediaRequest request,
		CancellationToken ct)
	{
		MediaAdminResult result = await _media.UpdateAsync(
			id,
			request,
			Actor(),
			ct);

		return result.Error switch
		{
			MediaAdminError.None => Ok(new { Id = result.Id }),
			MediaAdminError.Validation =>
				BadRequest(new { message = result.Message }),
			MediaAdminError.NotFound => NotFound(),
			MediaAdminError.Stale => Stale(),
			_ => Conflict()
		};
	}

	[HttpDelete("{id:guid}"), ValidateAntiForgeryToken]
	public async Task<IActionResult> Delete(
		Guid id,
		int version,
		CancellationToken ct)
	{
		MediaAdminResult result = await _media.DeleteAsync(
			id,
			version,
			Actor(),
			ct);

		return result.Error switch
		{
			MediaAdminError.None => NoContent(),
			MediaAdminError.NotFound => NotFound(),
			MediaAdminError.Stale => Stale(),
			MediaAdminError.InUse =>
				Conflict(new { message = result.Message }),
			_ => Conflict()
		};
	}

	[HttpPost("{id:guid}/restore"), ValidateAntiForgeryToken]
	public async Task<IActionResult> Restore(
		Guid id,
		RevisionRestoreRequest request,
		CancellationToken ct)
	{
		MediaAdminResult result = await _media.RestoreAsync(
			id,
			request,
			Actor(),
			ct);

		return result.Error switch
		{
			MediaAdminError.None => Ok(new { Id = result.Id }),
			MediaAdminError.NotFound or
			MediaAdminError.RevisionNotFound => NotFound(),
			MediaAdminError.Stale => Stale(),
			MediaAdminError.DeletedRevision =>
				BadRequest(new { message = result.Message }),
			_ => Conflict()
		};
	}

	private IActionResult Stale()
	{
		return Conflict(new
		{
			message =
				"This image changed in another session. Refresh before saving."
		});
	}

	private string Actor()
	{
		return User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "admin";
	}
}
