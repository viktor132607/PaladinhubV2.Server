using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PaladinHubV2.Server.Data;
using PaladinHubV2.Server.Domain.Services.GameDataAdmin;

namespace PaladinHubV2.Server.API.Controllers.GameData;

[ApiController]
[Authorize(Roles = "Admin")]
[Route("Admin/api/record-types")]
public sealed class RecordTypesController : ControllerBase
{
	public sealed record TypeRequest(
		[property: Required, MaxLength(50)] string Name);

	private readonly RecordTypeAdminService _recordTypes;

	public RecordTypesController(AppDbContext db)
	{
		_recordTypes = new RecordTypeAdminService(db);
	}

	[HttpGet]
	public async Task<IActionResult> List(
		CancellationToken cancellationToken)
	{
		return Ok(await _recordTypes.ListAsync(cancellationToken));
	}

	[HttpPost]
	[ValidateAntiForgeryToken]
	public async Task<IActionResult> Create(
		TypeRequest request,
		CancellationToken cancellationToken)
	{
		RecordTypeAdminResult result = await _recordTypes.CreateAsync(
			request.Name,
			cancellationToken);

		return result.Error switch
		{
			RecordTypeAdminError.None => Ok(new
			{
				name = result.Name,
				usageCount = result.UsageCount
			}),
			RecordTypeAdminError.Validation =>
				BadRequest(new { message = result.Message }),
			RecordTypeAdminError.Duplicate =>
				Conflict(new { message = result.Message }),
			_ => BadRequest()
		};
	}

	[HttpPut]
	[ValidateAntiForgeryToken]
	public async Task<IActionResult> Rename(
		[FromQuery] string name,
		TypeRequest request,
		CancellationToken cancellationToken)
	{
		RecordTypeAdminResult result = await _recordTypes.RenameAsync(
			name,
			request.Name,
			cancellationToken);

		return result.Error switch
		{
			RecordTypeAdminError.None => Ok(new { name = result.Name }),
			RecordTypeAdminError.Validation =>
				BadRequest(new { message = result.Message }),
			RecordTypeAdminError.NotFound =>
				NotFound(new { message = result.Message }),
			RecordTypeAdminError.Duplicate =>
				Conflict(new { message = result.Message }),
			_ => BadRequest()
		};
	}

	[HttpDelete]
	[ValidateAntiForgeryToken]
	public async Task<IActionResult> Delete(
		[FromQuery] string name,
		[FromQuery] string? replacement,
		CancellationToken cancellationToken)
	{
		RecordTypeAdminResult result = await _recordTypes.DeleteAsync(
			name,
			replacement,
			cancellationToken);

		return result.Error switch
		{
			RecordTypeAdminError.None => NoContent(),
			RecordTypeAdminError.SameReplacement or
			RecordTypeAdminError.ReplacementNotFound =>
				BadRequest(new { message = result.Message }),
			RecordTypeAdminError.NotFound =>
				NotFound(new { message = result.Message }),
			RecordTypeAdminError.InUse =>
				Conflict(new { message = result.Message }),
			_ => BadRequest()
		};
	}
}
