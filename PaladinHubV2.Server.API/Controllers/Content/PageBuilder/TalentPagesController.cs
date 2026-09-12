using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PaladinHubV2.Server.Data.Entities;
using PaladinHubV2.Server.Domain.Services.PageBuilder;

namespace PaladinHubV2.Server.API.Controllers.Content.PageBuilder;

[ApiController, Authorize(Roles = "Admin")]
[Route("Admin/api/talent-pages")]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public sealed class TalentPagesController(
	TalentPageAdminService talentPages) : ControllerBase
{
	[HttpGet]
	public async Task<IActionResult> List(
		CancellationToken cancellationToken)
	{
		List<ContentPage> pages =
			await talentPages.ListAsync(cancellationToken);

		return Ok(pages.Select(page => new
		{
			id = page.Id,
			title = page.Title,
			section = page.Section,
			slug = page.Slug
		}));
	}

	[HttpGet("{id:int}")]
	public async Task<IActionResult> Get(int id)
	{
		ContentPage? page = await talentPages.GetAsync(id);
		return page == null
			? NotFound()
			: Ok(Details(page));
	}

	[HttpPost]
	[ValidateAntiForgeryToken]
	public async Task<IActionResult> Create(
		SaveRequest request,
		CancellationToken cancellationToken)
	{
		TalentPageAdminResult result =
			await talentPages.CreateAsync(
				request.Title,
				request.Section,
				request.Slug,
				request.JsonLayout,
				User.Identity?.Name,
				cancellationToken);

		IActionResult? error = MapError(result);
		if (error != null)
		{
			return error;
		}

		ContentPage page = result.Page!;
		return Created(
			$"/Admin/api/talent-pages/{page.Id}",
			Details(page));
	}

	[HttpPut("{id:int}")]
	[ValidateAntiForgeryToken]
	public async Task<IActionResult> Update(
		int id,
		SaveRequest request)
	{
		TalentPageAdminResult result =
			await talentPages.UpdateAsync(
				id,
				request.JsonLayout,
				request.RowVersionBase64,
				User.Identity?.Name ?? "Admin");

		IActionResult? error = MapError(result);
		if (error != null)
		{
			return error;
		}

		return Ok(new
		{
			id,
			rowVersionBase64 =
				Convert.ToBase64String(result.RowVersion!)
		});
	}

	private IActionResult? MapError(TalentPageAdminResult result)
	{
		return result.Error switch
		{
			TalentPageAdminError.None => null,
			TalentPageAdminError.TitleRequired =>
				BadRequest(new { message = "Page title is required." }),
			TalentPageAdminError.InvalidSection =>
				BadRequest(new { message = "Invalid section." }),
			TalentPageAdminError.InvalidLayout =>
				BadRequest(new
				{
					message = "Layout validation failed",
					errors = result.ValidationErrors ?? Array.Empty<string>()
				}),
			TalentPageAdminError.InvalidSlug =>
				BadRequest(new
				{
					message = "Choose a unique slug, different from the existing guide pages."
				}),
			TalentPageAdminError.SlugExists =>
				Conflict(new { message = "Slug already exists." }),
			TalentPageAdminError.InvalidVersion =>
				BadRequest(new { message = "Invalid page version." }),
			TalentPageAdminError.VersionRequired =>
				BadRequest(new { message = "Page version is required." }),
			TalentPageAdminError.NotFound => NotFound(),
			TalentPageAdminError.ConcurrencyConflict =>
				Conflict(new
				{
					message = "The page was modified. Reload before saving."
				}),
			_ => null
		};
	}

	private static object Details(ContentPage page)
	{
		return new
		{
			id = page.Id,
			title = page.Title,
			section = page.Section,
			slug = page.Slug,
			jsonLayout = page.JsonLayout,
			rowVersionBase64 = Convert.ToBase64String(
				page.RowVersion ?? Array.Empty<byte>())
		};
	}

	public sealed class SaveRequest
	{
		[Required]
		public string JsonLayout { get; init; } = "[]";

		public string? RowVersionBase64 { get; init; }

		[StringLength(200)]
		public string? Title { get; init; }

		public string? Section { get; init; }

		[StringLength(100)]
		public string? Slug { get; init; }
	}
}
