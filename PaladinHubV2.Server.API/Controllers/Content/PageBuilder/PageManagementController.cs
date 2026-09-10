using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PaladinHubV2.Server.Data;
using PaladinHubV2.Server.Data.Entities;

namespace PaladinHubV2.Server.API.Controllers.Content.PageBuilder
{
	[ApiController]
	[Authorize(Roles = "Admin")]
	[Route("Admin/api/page-builder/pages")]
	public sealed class PageManagementController : ControllerBase
	{
		private static readonly HashSet<string> ReservedRoutes = new(
			StringComparer.OrdinalIgnoreCase)
		{
			"holy/overview",
			"holy/gear",
			"holy/talents",
			"holy/consumables",
			"holy/rotation",
			"holy/stats",
			"protection/overview",
			"protection/gear",
			"protection/talents",
			"protection/consumables",
			"protection/rotation",
			"protection/stats",
			"retribution/overview",
			"retribution/gear",
			"retribution/talents",
			"retribution/consumables",
			"retribution/rotation",
			"retribution/stats"
		};

		private readonly AppDbContext _db;

		public PageManagementController(AppDbContext db)
		{
			_db = db;
		}

		public sealed class SavePageRequest
		{
			public string? Section { get; init; }
			public string? Title { get; init; }
			public string? Slug { get; init; }
			public bool IsPublished { get; init; } = true;
		}

		[HttpGet]
		[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
		public async Task<IActionResult> List(CancellationToken ct)
		{
			var pages = await _db.ContentPages
				.AsNoTracking()
				.OrderBy(page => page.Section)
				.ThenBy(page => page.Title)
				.Select(page => new
				{
					page.Id,
					page.Section,
					page.Title,
					page.Slug,
					page.IsPublished,
					page.CreatedAt,
					page.UpdatedAt,
					page.UpdatedBy
				})
				.ToListAsync(ct);

			return Ok(pages.Select(page => new
			{
				id = page.Id,
				section = Capitalize(page.Section),
				title = page.Title,
				slug = page.Slug,
				isPublished = page.IsPublished,
				createdAt = page.CreatedAt,
				updatedAt = page.UpdatedAt,
				updatedBy = page.UpdatedBy
			}));
		}

		[HttpGet("{id:int}")]
		[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
		public async Task<IActionResult> Get(int id, CancellationToken ct)
		{
			var page = await _db.ContentPages
				.AsNoTracking()
				.FirstOrDefaultAsync(candidate => candidate.Id == id, ct);

			return page == null
				? NotFound(new { message = "Page not found." })
				: Ok(ToResponse(page));
		}

		[HttpPost]
		[ValidateAntiForgeryToken]
		public async Task<IActionResult> Create(
			[FromBody] SavePageRequest request,
			CancellationToken ct)
		{
			var validation = ValidateRequest(request);
			if (validation != null)
			{
				return BadRequest(new { message = validation });
			}

			var section = NormalizeSection(request.Section!);
			var slug = Slugify(request.Slug!);

			if (IsReserved(section, slug))
			{
				return Conflict(new
				{
					message = "This route belongs to a hardcoded page and cannot be replaced from Page Builder."
				});
			}

			var exists = await _db.ContentPages.AnyAsync(
				page => page.Section == section && page.Slug == slug,
				ct);

			if (exists)
			{
				return Conflict(new
				{
					message = "Slug is already used in this section."
				});
			}

			var now = DateTime.UtcNow;
			var page = new ContentPage
			{
				Section = section,
				Title = request.Title!.Trim(),
				Slug = slug,
				IsPublished = request.IsPublished,
				JsonLayout = "[]",
				CreatedAt = now,
				UpdatedAt = now,
				UpdatedBy = User.Identity?.Name ?? "admin",
				RowVersion = Array.Empty<byte>()
			};

			_db.ContentPages.Add(page);
			await _db.SaveChangesAsync(ct);

			return CreatedAtAction(nameof(Get), new { id = page.Id }, ToResponse(page));
		}

		[HttpPut("{id:int}")]
		[ValidateAntiForgeryToken]
		public async Task<IActionResult> Update(
			int id,
			[FromBody] SavePageRequest request,
			CancellationToken ct)
		{
			var validation = ValidateRequest(request);
			if (validation != null)
			{
				return BadRequest(new { message = validation });
			}

			var page = await _db.ContentPages
				.FirstOrDefaultAsync(candidate => candidate.Id == id, ct);

			if (page == null)
			{
				return NotFound(new { message = "Page not found." });
			}

			var section = NormalizeSection(request.Section!);
			var slug = Slugify(request.Slug!);

			if (IsReserved(section, slug))
			{
				return Conflict(new
				{
					message = "This route belongs to a hardcoded page and cannot be replaced from Page Builder."
				});
			}

			var exists = await _db.ContentPages.AnyAsync(
				candidate =>
					candidate.Id != id &&
					candidate.Section == section &&
					candidate.Slug == slug,
				ct);

			if (exists)
			{
				return Conflict(new
				{
					message = "Slug is already used in this section."
				});
			}

			page.Section = section;
			page.Title = request.Title!.Trim();
			page.Slug = slug;
			page.IsPublished = request.IsPublished;
			page.UpdatedAt = DateTime.UtcNow;
			page.UpdatedBy = User.Identity?.Name ?? "admin";

			try
			{
				await _db.SaveChangesAsync(ct);
			}
			catch (DbUpdateConcurrencyException)
			{
				return Conflict(new
				{
					message = "The page changed while you were editing it. Reload and try again."
				});
			}

			return Ok(ToResponse(page));
		}

		[HttpDelete("{id:int}")]
		[ValidateAntiForgeryToken]
		public async Task<IActionResult> Delete(int id, CancellationToken ct)
		{
			var page = await _db.ContentPages
				.FirstOrDefaultAsync(candidate => candidate.Id == id, ct);

			if (page == null)
			{
				return NotFound(new { message = "Page not found." });
			}

			_db.ContentPages.Remove(page);
			await _db.SaveChangesAsync(ct);
			return NoContent();
		}

		private static string? ValidateRequest(SavePageRequest request)
		{
			if (request == null)
			{
				return "Request body is required.";
			}

			if (!TryNormalizeSection(request.Section, out _))
			{
				return "Section must be Holy, Protection, or Retribution.";
			}

			if (string.IsNullOrWhiteSpace(request.Title))
			{
				return "Page title is required.";
			}

			if (request.Title.Trim().Length > 200)
			{
				return "Page title cannot exceed 200 characters.";
			}

			if (string.IsNullOrWhiteSpace(request.Slug))
			{
				return "Slug is required.";
			}

			if (Slugify(request.Slug).Length > 100)
			{
				return "Slug cannot exceed 100 characters.";
			}

			return null;
		}

		private static bool TryNormalizeSection(string? value, out string section)
		{
			section = (value ?? string.Empty).Trim().ToLowerInvariant() switch
			{
				"holy" => "holy",
				"protection" or "prot" => "protection",
				"retribution" or "retri" or "ret" => "retribution",
				_ => string.Empty
			};

			return section.Length > 0;
		}

		private static string NormalizeSection(string value)
		{
			TryNormalizeSection(value, out var section);
			return section;
		}

		private static string Slugify(string value)
		{
			var output = new List<char>();
			var pendingDash = false;

			foreach (var character in value.Trim().ToLowerInvariant())
			{
				if (char.IsLetterOrDigit(character))
				{
					if (pendingDash && output.Count > 0)
					{
						output.Add('-');
					}

					output.Add(character);
					pendingDash = false;
				}
				else if (character == '-' || char.IsWhiteSpace(character))
				{
					pendingDash = output.Count > 0;
				}
			}

			return new string(output.ToArray()).Trim('-');
		}

		private static bool IsReserved(string section, string slug)
			=> ReservedRoutes.Contains($"{section}/{slug}");

		private static string Capitalize(string value)
			=> string.IsNullOrWhiteSpace(value)
				? value
				: char.ToUpperInvariant(value[0]) + value[1..];

		private static object ToResponse(ContentPage page)
			=> new
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
				rowVersionBase64 = Convert.ToBase64String(page.RowVersion ?? Array.Empty<byte>())
			};
	}
}
