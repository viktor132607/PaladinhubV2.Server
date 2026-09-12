using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PaladinHubV2.Server.Common.Models.GameData;
using PaladinHubV2.Server.Domain.Services.GameDataAdmin;

namespace PaladinHubV2.Server.API.Controllers.GameData;

[ApiController, Authorize(Roles = "Admin"), Route("Admin/api/categories")]
public sealed class CategoriesController(
	CategoryAdminService categories) : ControllerBase
{
	[HttpGet]
	public async Task<IActionResult> List(CancellationToken ct)
	{
		return Ok(await categories.ListAsync(ct));
	}

	[HttpGet("{id:int}/history")]
	public async Task<IActionResult> History(
		int id,
		CancellationToken ct)
	{
		return Ok(await categories.HistoryAsync(id, ct));
	}

	[HttpPost, ValidateAntiForgeryToken]
	public async Task<IActionResult> Create(
		CategoryRequest request,
		CancellationToken ct)
	{
		CategoryAdminResult result = await categories.CreateAsync(
			request,
			Actor(),
			ct);

		if (result.Error == CategoryAdminError.Validation)
		{
			return BadRequest(new { message = result.Message });
		}

		return Ok(result.Category);
	}

	[HttpPut("{id:int}"), ValidateAntiForgeryToken]
	public async Task<IActionResult> Edit(
		int id,
		CategoryRequest request,
		CancellationToken ct)
	{
		CategoryAdminResult result = await categories.UpdateAsync(
			id,
			request,
			Actor(),
			ct);

		return result.Error switch
		{
			CategoryAdminError.None => Ok(result.Category),
			CategoryAdminError.NotFound => NotFound(),
			CategoryAdminError.Stale => Stale(),
			CategoryAdminError.Validation =>
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
		CategoryAdminResult result = await categories.DeleteAsync(
			id,
			version,
			Actor(),
			ct);

		return result.Error switch
		{
			CategoryAdminError.None => NoContent(),
			CategoryAdminError.NotFound => NotFound(),
			CategoryAdminError.Stale => Stale(),
			CategoryAdminError.InUse =>
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
		CategoryAdminResult result = await categories.RestoreAsync(
			id,
			request,
			Actor(),
			ct);

		return result.Error switch
		{
			CategoryAdminError.None => Ok(result.Category),
			CategoryAdminError.NotFound or
			CategoryAdminError.RevisionNotFound => NotFound(),
			CategoryAdminError.Stale => Stale(),
			CategoryAdminError.DeletedRevision =>
				BadRequest(new { message = result.Message }),
			CategoryAdminError.Validation =>
				Conflict(new { message = result.Message }),
			_ => Conflict()
		};
	}

	private IActionResult Stale()
	{
		return Conflict(new
		{
			message =
				"This category changed in another session. Refresh before saving."
		});
	}

	private string Actor()
	{
		return User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "admin";
	}
}
