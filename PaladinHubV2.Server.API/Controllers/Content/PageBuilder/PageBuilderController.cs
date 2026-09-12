using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PaladinHub.Areas.Admin.Models;
using PaladinHubV2.Server.Data;
using PaladinHubV2.Server.Data.Entities;
using PaladinHubV2.Server.Domain.Services.PageBuilder;

namespace PaladinHubV2.Server.API.Controllers.Content.PageBuilder
{
	[Area("Admin")]
	[ApiController]
	[Authorize(Roles = "Admin")]
	[Route("Admin/PageBuilder")]
	public sealed class PageBuilderController : ControllerBase
	{
		private readonly PageBuilderAdminService _pages;

		public PageBuilderController(AppDbContext db)
		{
			_pages = new PageBuilderAdminService(db);
		}

		[HttpGet("Create")]
		public IActionResult Create(
			[FromQuery] string? section)
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
		public Task<IActionResult> Create(
			[FromForm] CreatePageViewModel model)
		{
			return CreateCore(model);
		}

		[HttpGet("DeleteConfirm")]
		public async Task<IActionResult> DeleteConfirm(
			[FromQuery] string section,
			[FromQuery] string slug)
		{
			ContentPage? page =
				await _pages.FindAsync(section, slug);

			if (page == null)
			{
				return NotFound(new
				{
					message = "Page not found."
				});
			}

			return Ok(new DeletePageViewModel
			{
				Id = page.Id,
				Section =
					PageBuilderAdminService.Capitalize(page.Section),
				Slug = page.Slug,
				Title = page.Title,
				CreatedAt = page.CreatedAt
			});
		}

		[HttpGet("Delete")]
		public Task<IActionResult> Delete(
			[FromQuery] string section,
			[FromQuery] string slug)
		{
			return DeleteConfirm(section, slug);
		}

		[HttpDelete("~/Admin/api/pages")]
		[ValidateAntiForgeryToken]
		public Task<IActionResult> DeleteApi(
			[FromQuery] string section,
			[FromQuery] string slug)
		{
			return DeleteCore(section, slug);
		}

		[HttpPost("Delete")]
		[ValidateAntiForgeryToken]
		public Task<IActionResult> DeleteConfirmed(
			[FromForm] DeletePageViewModel model)
		{
			return DeleteCore(
				model.Section,
				model.Slug);
		}

		[HttpGet("Edit")]
		public async Task<IActionResult> Edit(
			[FromQuery] string section,
			[FromQuery] string slug)
		{
			ContentPage? page =
				await _pages.FindAsync(section, slug);

			if (page == null)
			{
				return NotFound(new
				{
					message = "Page not found."
				});
			}

			return Ok(new
			{
				id = page.Id,
				section =
					PageBuilderAdminService.Capitalize(page.Section),
				title = page.Title,
				slug = page.Slug,
				isPublished = page.IsPublished,
				jsonLayout = page.JsonLayout,
				createdAt = page.CreatedAt,
				updatedAt = page.UpdatedAt,
				updatedBy = page.UpdatedBy,
				rowVersionBase64 =
					Convert.ToBase64String(page.RowVersion)
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
				return NotFound(new
				{
					message = "Page not found."
				});
			}

			return Ok(new
			{
				id = page.Id,
				section =
					PageBuilderAdminService.Capitalize(page.Section),
				title = page.Title,
				slug = page.Slug,
				isPublished = page.IsPublished,
				jsonLayout = page.JsonLayout,
				updatedAt = page.UpdatedAt,
				redirectUrl =
					$"/{PageBuilderAdminService.Capitalize(page.Section)}/{page.Slug}"
			});
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
					message =
						"Slug is already used in this section."
				});
			}

			ContentPage page = result.Page!;

			string redirectUrl =
				$"/{PageBuilderAdminService.Capitalize(page.Section)}/{page.Slug}";

			return Created(redirectUrl, new
			{
				id = page.Id,
				section =
					PageBuilderAdminService.Capitalize(page.Section),
				title = page.Title,
				slug = page.Slug,
				isPublished = page.IsPublished,
				jsonLayout = page.JsonLayout,
				createdAt = page.CreatedAt,
				updatedAt = page.UpdatedAt,
				redirectUrl
			});
		}

		private async Task<IActionResult> DeleteCore(
			string section,
			string slug)
		{
			await _pages.DeleteAsync(section, slug);
			return NoContent();
		}
	}
}
