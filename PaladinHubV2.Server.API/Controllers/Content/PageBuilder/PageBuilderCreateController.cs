using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PaladinHub.Areas.Admin.Models;
using PaladinHubV2.Server.Data.Entities;
using PaladinHubV2.Server.Domain.Services.PageBuilder;

namespace PaladinHubV2.Server.API.Controllers.Content.PageBuilder
{
	[Area("Admin")]
	[ApiController]
	[Authorize(Roles = "Admin")]
	[Route("Admin/PageBuilder")]
	public sealed class PageBuilderCreateController : ControllerBase
	{
		private readonly PageBuilderAdminService _pages;

		public PageBuilderCreateController(PageBuilderAdminService pages)
		{
			_pages = pages;
		}

		[HttpGet("Create")]
		public IActionResult Create([FromQuery] string? section)
		{
			return Ok(_pages.BuildCreateModel(section));
		}

		[HttpPost("~/Admin/api/pages")]
		[ValidateAntiForgeryToken]
		public Task<IActionResult> CreateApi(
			[FromBody] CreatePageViewModel model)
		{
			return CreateCore(model);
		}

		[HttpPost("Create")]
		[ValidateAntiForgeryToken]
		public Task<IActionResult> CreateLegacy(
			[FromForm] CreatePageViewModel model)
		{
			return CreateCore(model);
		}

		private async Task<IActionResult> CreateCore(
			CreatePageViewModel model)
		{
			if (!ModelState.IsValid)
			{
				return ValidationProblem(ModelState);
			}

			PageBuilderCreateResult result =
				await _pages.CreateAsync(model);

			if (result.SlugConflict)
			{
				return Conflict(new
				{
					message = "Slug is already used in this section."
				});
			}

			ContentPage page = result.Page!;
			string redirectUrl =
				$"/{PageBuilderAdminService.Capitalize(page.Section)}/{page.Slug}";

			return Created(redirectUrl, new
			{
				id = page.Id,
				section = PageBuilderAdminService.Capitalize(page.Section),
				title = page.Title,
				slug = page.Slug,
				isPublished = page.IsPublished,
				jsonLayout = page.JsonLayout,
				createdAt = page.CreatedAt,
				updatedAt = page.UpdatedAt,
				redirectUrl
			});
		}
	}
}
