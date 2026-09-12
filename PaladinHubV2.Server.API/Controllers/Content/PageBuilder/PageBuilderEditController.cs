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
	public sealed class PageBuilderEditController : ControllerBase
	{
		private readonly PageBuilderAdminService _pages;

		public PageBuilderEditController(PageBuilderAdminService pages)
		{
			_pages = pages;
		}

		[HttpGet("Edit")]
		public async Task<IActionResult> Edit(
			[FromQuery] string section,
			[FromQuery] string slug)
		{
			ContentPage? page = await _pages.FindAsync(section, slug);

			if (page == null)
			{
				return NotFound(new { message = "Page not found." });
			}

			return Ok(new
			{
				id = page.Id,
				section = PageBuilderAdminService.Capitalize(page.Section),
				title = page.Title,
				slug = page.Slug,
				isPublished = page.IsPublished,
				jsonLayout = page.JsonLayout,
				createdAt = page.CreatedAt,
				updatedAt = page.UpdatedAt,
				updatedBy = page.UpdatedBy,
				rowVersionBase64 = Convert.ToBase64String(page.RowVersion)
			});
		}

		[HttpPost("Edit")]
		[ValidateAntiForgeryToken]
		public async Task<IActionResult> EditPost(
			[FromForm] EditPageRequest request)
		{
			ContentPage? page = await _pages.UpdateAsync(
				request.Section,
				request.Slug,
				request.Title,
				request.JsonLayout);

			if (page == null)
			{
				return NotFound(new { message = "Page not found." });
			}

			return Ok(new
			{
				id = page.Id,
				section = PageBuilderAdminService.Capitalize(page.Section),
				title = page.Title,
				slug = page.Slug,
				isPublished = page.IsPublished,
				jsonLayout = page.JsonLayout,
				updatedAt = page.UpdatedAt,
				redirectUrl =
					$"/{PageBuilderAdminService.Capitalize(page.Section)}/{page.Slug}"
			});
		}
	}
}
