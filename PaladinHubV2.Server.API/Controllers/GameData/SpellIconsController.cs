using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PaladinHubV2.Server.Data;
using PaladinHubV2.Server.Domain.Services.GameData;
using PaladinHubV2.Server.Domain.Services.GameDataAdmin;

namespace PaladinHubV2.Server.API.Controllers.GameData;

[ApiController]
[Authorize(Roles = "Admin")]
[Route("Admin/api/spells/icons")]
public sealed class SpellIconsController : ControllerBase
{
	private readonly SpellIconLibraryService _icons;

	public SpellIconsController(
		AppDbContext db,
		GameDataAssignmentService assignments)
	{
		_icons = new SpellIconLibraryService(db, assignments);
	}

	[HttpGet]
	public async Task<IActionResult> Browse(
		string? search,
		int page = 1,
		int pageSize = 64,
		CancellationToken cancellationToken = default)
	{
		SpellIconLibraryPage result = await _icons.BrowseAsync(
			search,
			page,
			pageSize,
			cancellationToken);

		return Ok(new
		{
			page = result.Page,
			pages = result.Pages,
			total = result.Total,
			icons = result.Icons
		});
	}

	[HttpPost]
	[ValidateAntiForgeryToken]
	[RequestSizeLimit(SpellIconLibraryService.MaxUploadBytes + 65536)]
	[RequestFormLimits(
		MultipartBodyLengthLimit = SpellIconLibraryService.MaxUploadBytes + 65536)]
	public async Task<IActionResult> Upload(
		[FromForm] IFormFile file,
		CancellationToken cancellationToken)
	{
		if (file.Length is <= 0 or > SpellIconLibraryService.MaxUploadBytes)
		{
			return BadRequest(new
			{
				message = "Choose a PNG, JPEG, GIF or WebP image up to 5 MB."
			});
		}

		await using var buffer = new MemoryStream();
		await file.CopyToAsync(buffer, cancellationToken);

		SpellIconUploadResult result = await _icons.UploadAsync(
			file.FileName,
			buffer.ToArray(),
			Actor(),
			cancellationToken);

		return result.Error switch
		{
			SpellIconUploadError.None => Ok(new
			{
				icon = result.Icon,
				name = result.Name
			}),
			SpellIconUploadError.InvalidSize => BadRequest(new
			{
				message = "Choose a PNG, JPEG, GIF or WebP image up to 5 MB."
			}),
			SpellIconUploadError.InvalidType => BadRequest(new
			{
				message = "Only PNG, JPEG, GIF and WebP images are supported."
			}),
			_ => BadRequest()
		};
	}

	[AllowAnonymous]
	[HttpGet("/api/spell-icons/{id:guid}")]
	public async Task<IActionResult> Image(
		Guid id,
		CancellationToken cancellationToken)
	{
		SpellIconImageResult? image =
			await _icons.GetImageAsync(id, cancellationToken);

		if (image == null)
		{
			return NotFound();
		}

		Response.Headers["X-Content-Type-Options"] = "nosniff";
		Response.Headers.CacheControl = "public,max-age=31536000,immutable";
		return File(image.Content, image.ContentType);
	}

	private string Actor()
	{
		return User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "admin";
	}
}
