using Microsoft.EntityFrameworkCore;
using PaladinHub.Areas.Admin.Models;
using PaladinHubV2.Server.Data;
using PaladinHubV2.Server.Data.Entities;

namespace PaladinHubV2.Server.Domain.Services.PageBuilder
{
	public enum PageManagementError
	{
		None = 0,
		NotFound,
		ReservedRoute,
		SlugConflict,
		ConcurrencyConflict
	}

	public sealed record PageManagementResult(
		PageManagementError Error,
		ContentPage? Page = null);

	public sealed class PageManagementService
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

		public PageManagementService(AppDbContext db)
		{
			_db = db;
		}

		public Task<List<ContentPage>> ListAsync(
			CancellationToken cancellationToken)
		{
			return _db.ContentPages
				.AsNoTracking()
				.OrderBy(page => page.Section)
				.ThenBy(page => page.Title)
				.ToListAsync(cancellationToken);
		}

		public Task<ContentPage?> GetAsync(
			int id,
			CancellationToken cancellationToken)
		{
			return _db.ContentPages
				.AsNoTracking()
				.FirstOrDefaultAsync(
					candidate => candidate.Id == id,
					cancellationToken);
		}

		public string? ValidateRequest(SavePageRequest? request)
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

		public async Task<PageManagementResult> CreateAsync(
			SavePageRequest request,
			string updatedBy,
			CancellationToken cancellationToken)
		{
			string section = NormalizeSection(request.Section!);
			string slug = Slugify(request.Slug!);

			if (IsReserved(section, slug))
			{
				return new PageManagementResult(
					PageManagementError.ReservedRoute);
			}

			bool exists = await _db.ContentPages.AnyAsync(
				page =>
					page.Section == section &&
					page.Slug == slug,
				cancellationToken);

			if (exists)
			{
				return new PageManagementResult(
					PageManagementError.SlugConflict);
			}

			DateTime now = DateTime.UtcNow;

			var page = new ContentPage
			{
				Section = section,
				Title = request.Title!.Trim(),
				Slug = slug,
				IsPublished = request.IsPublished,
				JsonLayout = "[]",
				CreatedAt = now,
				UpdatedAt = now,
				UpdatedBy = updatedBy,
				RowVersion = Array.Empty<byte>()
			};

			_db.ContentPages.Add(page);
			await _db.SaveChangesAsync(cancellationToken);

			return new PageManagementResult(
				PageManagementError.None,
				page);
		}

		public async Task<PageManagementResult> UpdateAsync(
			int id,
			SavePageRequest request,
			string updatedBy,
			CancellationToken cancellationToken)
		{
			ContentPage? page = await _db.ContentPages
				.FirstOrDefaultAsync(
					candidate => candidate.Id == id,
					cancellationToken);

			if (page == null)
			{
				return new PageManagementResult(
					PageManagementError.NotFound);
			}

			string section = NormalizeSection(request.Section!);
			string slug = Slugify(request.Slug!);

			if (IsReserved(section, slug))
			{
				return new PageManagementResult(
					PageManagementError.ReservedRoute);
			}

			bool exists = await _db.ContentPages.AnyAsync(
				candidate =>
					candidate.Id != id &&
					candidate.Section == section &&
					candidate.Slug == slug,
				cancellationToken);

			if (exists)
			{
				return new PageManagementResult(
					PageManagementError.SlugConflict);
			}

			page.Section = section;
			page.Title = request.Title!.Trim();
			page.Slug = slug;
			page.IsPublished = request.IsPublished;
			page.UpdatedAt = DateTime.UtcNow;
			page.UpdatedBy = updatedBy;

			try
			{
				await _db.SaveChangesAsync(cancellationToken);
			}
			catch (DbUpdateConcurrencyException)
			{
				return new PageManagementResult(
					PageManagementError.ConcurrencyConflict);
			}

			return new PageManagementResult(
				PageManagementError.None,
				page);
		}

		public async Task<bool> DeleteAsync(
			int id,
			CancellationToken cancellationToken)
		{
			ContentPage? page = await _db.ContentPages
				.FirstOrDefaultAsync(
					candidate => candidate.Id == id,
					cancellationToken);

			if (page == null)
			{
				return false;
			}

			_db.ContentPages.Remove(page);
			await _db.SaveChangesAsync(cancellationToken);
			return true;
		}

		public static string Capitalize(string value)
		{
			return string.IsNullOrWhiteSpace(value)
				? value
				: char.ToUpperInvariant(value[0]) + value[1..];
		}

		private static bool TryNormalizeSection(
			string? value,
			out string section)
		{
			section = (value ?? string.Empty)
				.Trim()
				.ToLowerInvariant() switch
			{
				"holy" => "holy",
				"protection" or "prot" => "protection",
				"retribution" or "retri" or "ret" =>
					"retribution",
				_ => string.Empty
			};

			return section.Length > 0;
		}

		private static string NormalizeSection(string value)
		{
			TryNormalizeSection(value, out string section);
			return section;
		}

		private static string Slugify(string value)
		{
			var output = new List<char>();
			bool pendingDash = false;

			foreach (char character in value.Trim().ToLowerInvariant())
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
				else if (character == '-' ||
					char.IsWhiteSpace(character))
				{
					pendingDash = output.Count > 0;
				}
			}

			return new string(output.ToArray()).Trim('-');
		}

		private static bool IsReserved(
			string section,
			string slug)
		{
			return ReservedRoutes.Contains(
				$"{section}/{slug}");
		}
	}
}
