using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PaladinHubV2.Server.Data.Entities;
using PaladinHubV2.Server.Domain.Services.PageBuilder;

namespace PaladinHubV2.Server.API.Controllers.Content.PageBuilder
{
	[ApiController]
	[Authorize(Roles = "Admin")]
	[Route("Admin/api/page-builder/pages")]
	public sealed class PageManagementController : ControllerBase
	{
		private readonly PageManagementService _pages;

		public PageManagementController(PageManagementService pages)
		{
			_pages = pages;
		}

		[HttpGet]
		[ResponseCache(
			NoStore = true,
			Location = ResponseCacheLocation.None)]
		public async Task<IActionResult> List(
			CancellationToken cancellationToken)
		{
			List<ContentPage> pages =
				await _pages.ListAsync(cancellationToken);

			return Ok(pages.Select(page => new
			{
				id = page.Id,
				section = PageManagementService.Capitalize(page.Section),
				title = page.Title,
				slug = page.Slug,
				isPublished = page.IsPublished,
				createdAt = page.CreatedAt,
				updatedAt = page.UpdatedAt,
				updatedBy = page.UpdatedBy,
				rowVersionBase64 = Convert.ToBase64String(page.RowVersion)
			}));
		}

		[HttpGet("{id:int}")]
		[ResponseCache(
			NoStore = true,
			Location = ResponseCacheLocation.None)]
		public async Task<IActionResult> Get(
			int id,
			CancellationToken cancellationToken)
		{
			ContentPage? page =
				await _pages.GetAsync(id, cancellationToken);

			return page == null
				? NotFound(new { message = "Page not found." })
				: Ok(ToResponse(page));
		}

		internal static object ToResponse(ContentPage page)
		{
			return new
			{
				id = page.Id,
				section = PageManagementService.Capitalize(page.Section),
				title = page.Title,
				slug = page.Slug,
				isPublished = page.IsPublished,
				jsonLayout = page.JsonLayout,
				createdAt = page.CreatedAt,
				updatedAt = page.UpdatedAt,
				updatedBy = page.UpdatedBy,
				rowVersionBase64 = Convert.ToBase64String(
					page.RowVersion ?? Array.Empty<byte>())
			};
		}
	}
}
