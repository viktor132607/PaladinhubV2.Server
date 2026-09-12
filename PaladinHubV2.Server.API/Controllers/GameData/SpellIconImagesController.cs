using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PaladinHubV2.Server.Data;
using PaladinHubV2.Server.Domain.Services.GameData;
using PaladinHubV2.Server.Domain.Services.GameDataAdmin;

namespace PaladinHubV2.Server.API.Controllers.GameData;

[ApiController]
[AllowAnonymous]
[Route("api/spell-icons")]
public sealed class SpellIconImagesController : ControllerBase
{
	private readonly SpellIconLibraryService _icons;

	public SpellIconImagesController(
		AppDbContext db,
		GameDataAssignmentService assignments)
	{
		_icons = new SpellIconLibraryService(db, assignments);
	}

	[HttpGet("{id:guid}")]
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
}
