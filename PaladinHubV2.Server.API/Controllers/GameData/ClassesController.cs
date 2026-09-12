using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PaladinHubV2.Server.Common.Models.GameData;
using PaladinHubV2.Server.Domain.Services.GameDataAdmin;

namespace PaladinHubV2.Server.API.Controllers.GameData;

[ApiController, Authorize(Roles = "Admin"), Route("Admin/api/classes")]
public sealed class ClassesController(
	DisciplineAdminService disciplines) : ControllerBase
{
	[HttpGet]
	public async Task<IActionResult> List(CancellationToken ct)
	{
		return Ok(await disciplines.ListAsync(ct));
	}

	[HttpGet("{id:int}/history")]
	public async Task<IActionResult> History(
		int id,
		CancellationToken ct)
	{
		return Ok(await disciplines.HistoryAsync(id, ct));
	}

	[HttpPost, ValidateAntiForgeryToken]
	public async Task<IActionResult> Create(
		DisciplineRequest request,
		CancellationToken ct)
	{
		DisciplineAdminResult result = await disciplines.CreateAsync(
			request,
			Actor(),
			ct);

		if (result.Error == DisciplineAdminError.Validation)
		{
			return BadRequest(new { message = result.Message });
		}

		return Ok(result.Discipline);
	}

	[HttpPut("{id:int}"), ValidateAntiForgeryToken]
	public async Task<IActionResult> Edit(
		int id,
		DisciplineRequest request,
		CancellationToken ct)
	{
		DisciplineAdminResult result = await disciplines.UpdateAsync(
			id,
			request,
			Actor(),
			ct);

		return result.Error switch
		{
			DisciplineAdminError.None => Ok(result.Discipline),
			DisciplineAdminError.NotFound => NotFound(),
			DisciplineAdminError.Stale => Stale(),
			DisciplineAdminError.Validation =>
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
		DisciplineAdminResult result = await disciplines.DeleteAsync(
			id,
			version,
			Actor(),
			ct);

		return result.Error switch
		{
			DisciplineAdminError.None => NoContent(),
			DisciplineAdminError.NotFound => NotFound(),
			DisciplineAdminError.Stale => Stale(),
			DisciplineAdminError.InUse =>
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
		DisciplineAdminResult result = await disciplines.RestoreAsync(
			id,
			request,
			Actor(),
			ct);

		return result.Error switch
		{
			DisciplineAdminError.None => Ok(result.Discipline),
			DisciplineAdminError.NotFound or
			DisciplineAdminError.RevisionNotFound => NotFound(),
			DisciplineAdminError.Stale => Stale(),
			DisciplineAdminError.DeletedRevision =>
				BadRequest(new { message = result.Message }),
			DisciplineAdminError.Validation =>
				Conflict(new { message = result.Message }),
			_ => Conflict()
		};
	}

	private IActionResult Stale()
	{
		return Conflict(new
		{
			message =
				"This class or specialization changed in another session. Refresh before saving."
		});
	}

	private string Actor()
	{
		return User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "admin";
	}
}
