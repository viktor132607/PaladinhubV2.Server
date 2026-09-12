using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PaladinHub.Areas.Admin.Models;
using PaladinHubV2.Server.Data;
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

		public PageManagementController(AppDbContext db)
		{
			_pages = new PageManagementService(db);
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
				updatedBy = page.UpdatedBy
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

		[HttpPost]
		[ValidateAntiForgeryToken]
		public async Task<IActionResult> Create(
			[FromBody] SavePageRequest request,
			CancellationToken cancellationToken)
		{
			string? validation = _pages.ValidateRequest(request);

			if (validation != null)
			{
				return BadRequest(new { message = validation });
			}

			PageManagementResult result = await _pages.CreateAsync(
				request,
				User.Identity?.Name ?? "admin",
				cancellationToken);

			IActionResult? error = MapMutationError(result.Error);

			if (error != null)
			{
				return error;
			}

			ContentPage page = result.Page!;

			return CreatedAtAction(
				nameof(Get),
				new { id = page.Id },
				ToResponse(page));
		}

		[HttpPut("{id:int}")]
		[ValidateAntiForgeryToken]
		public async Task<IActionResult> Update(
			int id,
			[FromBody] SavePageRequest request,
			CancellationToken cancellationToken)
		{
			string? validation = _pages.ValidateRequest(request);

			if (validation != null)
			{
				return BadRequest(new { message = validation });
			}

			PageManagementResult result = await _pages.UpdateAsync(
				id,
				request,
				User.Identity?.Name ?? "admin",
				cancellationToken);

			IActionResult? error = MapMutationError(result.Error);

			if (error != null)
			{
				return error;
			}

			return Ok(ToResponse(result.Page!));
		}

		[HttpDelete("{id:int}")]
		[ValidateAntiForgeryToken]
		public async Task<IActionResult> Delete(
			int id,
			CancellationToken cancellationToken)
		{
			bool deleted =
				await _pages.DeleteAsync(id, cancellationToken);

			if (!deleted)
			{
				return NotFound(new
				{
					message = "Page not found."
				});
			}

			return NoContent();
		}

		private IActionResult? MapMutationError(
			PageManagementError error)
		{
			return error switch
			{
				PageManagementError.None => null,
				PageManagementError.NotFound =>
					NotFound(new { message = "Page not found." }),
				PageManagementError.ReservedRoute =>
					Conflict(new
					{
						message =
							"This route belongs to a hardcoded page and cannot be replaced from Page Builder."
					}),
				PageManagementError.SlugConflict =>
					Conflict(new
					{
						message =
							"Slug is already used in this section."
					}),
				PageManagementError.ConcurrencyConflict =>
					Conflict(new
					{
						message =
							"The page changed while you were editing it. Reload and try again."
					}),
				_ => Conflict()
			};
		}

		private static object ToResponse(ContentPage page)
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
