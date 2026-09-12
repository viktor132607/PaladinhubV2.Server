using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PaladinHub.Areas.Admin.Models;
using PaladinHubV2.Server.Data.Entities;
using PaladinHubV2.Server.Domain.Services.PageBuilder;

namespace PaladinHubV2.Server.API.Controllers.Content.PageBuilder
{
	[ApiController]
	[Authorize(Roles = "Admin")]
	[Route("Admin/api/page-builder/pages")]
	public sealed class PageManagementMutationsController : ControllerBase
	{
		private readonly PageManagementService _pages;

		public PageManagementMutationsController(
			PageManagementService pages)
		{
			_pages = pages;
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
				nameof(PageManagementController.Get),
				"PageManagement",
				new { id = page.Id },
				PageManagementController.ToResponse(page));
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

			return Ok(PageManagementController.ToResponse(result.Page!));
		}

		[HttpDelete("{id:int}")]
		[ValidateAntiForgeryToken]
		public async Task<IActionResult> Delete(
			int id,
			CancellationToken cancellationToken)
		{
			bool deleted = await _pages.DeleteAsync(
				id,
				cancellationToken);

			if (!deleted)
			{
				return NotFound(new { message = "Page not found." });
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
						message = "This route belongs to a hardcoded page and cannot be replaced from Page Builder."
					}),
				PageManagementError.SlugConflict =>
					Conflict(new
					{
						message = "Slug is already used in this section."
					}),
				PageManagementError.ConcurrencyConflict =>
					Conflict(new
					{
						message = "The page changed while you were editing it. Reload and try again."
					}),
				_ => Conflict()
			};
		}
	}
}
