using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PaladinHub.Areas.Admin.Models;
using PaladinHubV2.Server.Data;
using PaladinHubV2.Server.Data.Entities;

namespace PaladinHubV2.Server.API.Controllers.Content.PageBuilder
{
	[Area("Admin")]
	[ApiController]
	[Authorize(Roles = "Admin")]
	[Route("Admin/PageBuilder")]
	public sealed class PageBuilderController : ControllerBase
	{
		private readonly AppDbContext _db;

		public PageBuilderController(AppDbContext db)
		{
			_db = db;
		}

		private static string NormalizeSection(string? section)
		{
			var normalized = (section ?? string.Empty)
				.Trim()
				.ToLowerInvariant();

			return normalized switch
			{
				"holy" => "holy",
				"protection" or "prot" => "protection",
				"retribution" or "retri" or "ret" => "retribution",
				_ => "holy"
			};
		}

		private static string Capitalize(string value)
		{
			return string.IsNullOrWhiteSpace(value)
				? value
				: char.ToUpperInvariant(value[0]) + value[1..];
		}

		private static string Slugify(string? value)
		{
			var slug = (value ?? string.Empty)
				.Trim()
				.ToLowerInvariant();

			slug = new string(
				slug
					.Where(character =>
						char.IsLetterOrDigit(character) ||
						character == '-')
					.ToArray());

			slug = string.Join(
				"-",
				slug.Split(
					'-',
					StringSplitOptions.RemoveEmptyEntries));

			return string.IsNullOrWhiteSpace(slug)
				? "page"
				: slug;
		}

		[HttpGet("Create")]
		public IActionResult Create(
			[FromQuery] string? section)
		{
			var normalizedSection =
				NormalizeSection(section);

			return Ok(new CreatePageViewModel
			{
				Section = Capitalize(normalizedSection),
				Title = string.Empty,
				Slug = string.Empty,
				IsPublished = true,
				JsonLayout = "[]"
			});
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
			var normalizedSection =
				NormalizeSection(section);

			var normalizedSlug =
				Slugify(slug);

			var page = await _db.ContentPages
				.AsNoTracking()
				.FirstOrDefaultAsync(candidate =>
					candidate.Section == normalizedSection &&
					candidate.Slug == normalizedSlug);

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
				Section = Capitalize(page.Section),
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
			var normalizedSection =
				NormalizeSection(section);

			var normalizedSlug =
				Slugify(slug);

			var page = await _db.ContentPages
				.AsNoTracking()
				.FirstOrDefaultAsync(candidate =>
					candidate.Section == normalizedSection &&
					candidate.Slug == normalizedSlug);

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
				section = Capitalize(page.Section),
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
			var normalizedSection =
				NormalizeSection(request.Section);

			var normalizedSlug =
				Slugify(request.Slug);

			var page = await _db.ContentPages
				.FirstOrDefaultAsync(candidate =>
					candidate.Section == normalizedSection &&
					candidate.Slug == normalizedSlug);

			if (page == null)
			{
				return NotFound(new
				{
					message = "Page not found."
				});
			}

			if (!string.IsNullOrWhiteSpace(request.Title))
			{
				page.Title = request.Title.Trim();
			}

			if (!string.IsNullOrWhiteSpace(request.JsonLayout))
			{
				page.JsonLayout =
					request.JsonLayout.Trim();
			}

			page.UpdatedAt = DateTime.UtcNow;

			await _db.SaveChangesAsync();

			return Ok(new
			{
				id = page.Id,
				section = Capitalize(page.Section),
				title = page.Title,
				slug = page.Slug,
				isPublished = page.IsPublished,
				jsonLayout = page.JsonLayout,
				updatedAt = page.UpdatedAt,
				redirectUrl =
					$"/{Capitalize(page.Section)}/{page.Slug}"
			});
		}

		private async Task<IActionResult> CreateCore(
			CreatePageViewModel model)
		{
			if (!ModelState.IsValid)
			{
				return ValidationProblem(ModelState);
			}

			var normalizedSection =
				NormalizeSection(model.Section);

			var rawSlug =
				string.IsNullOrWhiteSpace(model.Slug)
					? model.Title
					: model.Slug;

			var normalizedSlug =
				Slugify(rawSlug);

			var exists = await _db.ContentPages
				.AnyAsync(page =>
					page.Section == normalizedSection &&
					page.Slug == normalizedSlug);

			if (exists)
			{
				return Conflict(new
				{
					message =
						"Slug is already used in this section."
				});
			}

			var now = DateTime.UtcNow;

			var page = new ContentPage
			{
				Section = normalizedSection,
				Slug = normalizedSlug,
				Title = string.IsNullOrWhiteSpace(model.Title)
					? normalizedSlug
					: model.Title.Trim(),
				IsPublished = true,
				JsonLayout =
					string.IsNullOrWhiteSpace(model.JsonLayout)
						? "[]"
						: model.JsonLayout.Trim(),
				CreatedAt = now,
				UpdatedAt = now,
				RowVersion = Array.Empty<byte>()
			};

			_db.ContentPages.Add(page);
			await _db.SaveChangesAsync();

			var redirectUrl =
				$"/{Capitalize(normalizedSection)}/{normalizedSlug}";

			return Created(redirectUrl, new
			{
				id = page.Id,
				section = Capitalize(page.Section),
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
			var normalizedSection =
				NormalizeSection(section);

			var normalizedSlug =
				Slugify(slug);

			var page = await _db.ContentPages
				.FirstOrDefaultAsync(candidate =>
					candidate.Section == normalizedSection &&
					candidate.Slug == normalizedSlug);

			if (page != null)
			{
				_db.ContentPages.Remove(page);
				await _db.SaveChangesAsync();
			}

			return NoContent();
		}

		public sealed class EditPageRequest
		{
			public string Section { get; init; } =
				string.Empty;

			public string Slug { get; init; } =
				string.Empty;

			public string? Title { get; init; }

			public string? JsonLayout { get; init; }
		}
	}
}
