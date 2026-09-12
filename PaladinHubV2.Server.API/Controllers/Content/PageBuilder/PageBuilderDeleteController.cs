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
	public sealed class PageBuilderDeleteController : ControllerBase
	{
		private readonly PageBuilderAdminService _pages;

		public PageBuilderDeleteController(PageBuilderAdminService pages)
		{
			_pages = pages;
		}

		[HttpGet("DeleteConfirm")]
		public async Task<IActionResult> DeleteConfirm(
			[FromQuery] string section,
			[FromQuery] string slug)
		{
			ContentPage? page = await _pages.FindAsync(section, slug);

			if (page == null)
			{
				return NotFound(new { message = "Page not found." });
			}

			return Ok(new DeletePageViewModel
			{
				Id = page.Id,
				Section = PageBuilderAdminService.Capitalize(page.Section),
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
			return DeleteCore(model.Section, model.Slug);
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
