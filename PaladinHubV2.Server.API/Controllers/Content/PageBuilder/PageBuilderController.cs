using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PaladinHub.Areas.Admin.Models;
using PaladinHub.Models.PageBuilder;
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
		private readonly IPageBuilderAdminService _pages;

		public PageBuilderController(IPageBuilderAdminService pages)
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
		public Task<IActionResult> CreateApi([FromBody] CreatePageViewModel model)
		{
			return CreateCore(model);
		}

		[HttpPost("Create")]
		[ValidateAntiForgeryToken]
		public Task<IActionResult> Create([FromForm] CreatePageViewModel model)
		{
			return CreateCore(model);
		}

		[HttpGet("DeleteConfirm")]
		public async Task<IActionResult> DeleteConfirm(
			[FromQuery] string section,
			[FromQuery] string slug,
			CancellationToken cancellationToken = default)
		{
			ContentPage? page = await _pages.GetByRouteAsync(
				section,
				slug,
				cancellationToken);

			return page == null
				? NotFound(new { message = "Page not found." })
				: Ok(_pages.BuildDeleteModel(page));
		}

		[HttpGet("Delete")]
		public Task<IActionResult> Delete(
			[FromQuery] string section,
			[FromQuery] string slug,
			CancellationToken cancellationToken = default)
		{
			return DeleteConfirm(section, slug, cancellationToken);
		}

		[HttpDelete("~/Admin/api/pages")]
		[ValidateAntiForgeryToken]
		public Task<IActionResult> DeleteApi(
			[FromQuery] string section,
			[FromQuery] string slug,
			CancellationToken cancellationToken = default)
		{
			return DeleteCore(section, slug, cancellationToken);
		}

		[HttpPost("Delete")]
		[ValidateAntiForgeryToken]
		public Task<IActionResult> DeleteConfirmed(
			[FromForm] DeletePageViewModel model,
			CancellationToken cancellationToken = default)
		{
			return DeleteCore(model.Section, model.Slug, cancellationToken);
		}

		[HttpGet("Edit")]
		public async Task<IActionResult> Edit(
			[FromQuery] string section,
			[FromQuery] string slug,
			CancellationToken cancellationToken = default)
		{
			ContentPage? page = await _pages.GetByRouteAsync(
				section,
				slug,
				cancellationToken);

			if (page == null)
			{
				return NotFound(new { message = "Page not found." });
			}

			return Ok(new
			{
				id = page.Id,
				section = _pages.DisplaySection(page.Section),
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
			[FromForm] EditPageRequest request,
			CancellationToken cancellationToken = default)
		{
			PageBuilderEditResult? result = await _pages.EditAsync(
				request,
				cancellationToken);

			if (result == null)
			{
				return NotFound(new { message = "Page not found." });
			}

			ContentPage page = result.Page;
			return Ok(new
			{
				id = page.Id,
				section = _pages.DisplaySection(page.Section),
				title = page.Title,
				slug = page.Slug,
				isPublished = page.IsPublished,
				jsonLayout = page.JsonLayout,
				updatedAt = page.UpdatedAt,
				redirectUrl = result.RedirectUrl
			});
		}

		private async Task<IActionResult> CreateCore(
			CreatePageViewModel model,
			CancellationToken cancellationToken = default)
		{
			if (!ModelState.IsValid)
			{
				return ValidationProblem(ModelState);
			}

			PageBuilderCreateResult result = await _pages.CreateAsync(
				model,
				cancellationToken);

			if (result.Conflict)
			{
				return Conflict(new
				{
					message = "Slug is already used in this section."
				});
			}

			ContentPage page = result.Page!;
			return Created(result.RedirectUrl!, new
			{
				id = page.Id,
				section = _pages.DisplaySection(page.Section),
				title = page.Title,
				slug = page.Slug,
				isPublished = page.IsPublished,
				jsonLayout = page.JsonLayout,
				createdAt = page.CreatedAt,
				updatedAt = page.UpdatedAt,
				redirectUrl = result.RedirectUrl
			});
		}

		private async Task<IActionResult> DeleteCore(
			string section,
			string slug,
			CancellationToken cancellationToken)
		{
			await _pages.DeleteAsync(section, slug, cancellationToken);
			return NoContent();
		}
	}
}
