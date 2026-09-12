using Microsoft.EntityFrameworkCore;
using PaladinHub.Areas.Admin.Models;
using PaladinHubV2.Server.Data;
using PaladinHubV2.Server.Data.Entities;

namespace PaladinHubV2.Server.Domain.Services.PageBuilder
{
	public sealed record PageBuilderCreateResult(
		ContentPage? Page,
		bool SlugConflict);

	public sealed class PageBuilderAdminService
	{
		private readonly AppDbContext _db;

		public PageBuilderAdminService(AppDbContext db)
		{
			_db = db;
		}

		public CreatePageViewModel BuildCreateModel(
			string? section)
		{
			string normalizedSection =
				NormalizeSection(section);

			return new CreatePageViewModel
			{
				Section = Capitalize(normalizedSection),
				Title = string.Empty,
				Slug = string.Empty,
				IsPublished = true,
				JsonLayout = "[]"
			};
		}

		public async Task<ContentPage?> FindAsync(
			string? section,
			string? slug,
			bool tracking = false,
			CancellationToken cancellationToken = default)
		{
			string normalizedSection =
				NormalizeSection(section);

			string normalizedSlug =
				Slugify(slug);

			IQueryable<ContentPage> query = _db.ContentPages;

			if (!tracking)
			{
				query = query.AsNoTracking();
			}

			return await query.FirstOrDefaultAsync(
				candidate =>
					candidate.Section == normalizedSection &&
					candidate.Slug == normalizedSlug,
				cancellationToken);
		}

		public async Task<PageBuilderCreateResult> CreateAsync(
			CreatePageViewModel model,
			CancellationToken cancellationToken = default)
		{
			string normalizedSection =
				NormalizeSection(model.Section);

			string? rawSlug =
				string.IsNullOrWhiteSpace(model.Slug)
					? model.Title
					: model.Slug;

			string normalizedSlug = Slugify(rawSlug);

			bool exists = await _db.ContentPages.AnyAsync(
				page =>
					page.Section == normalizedSection &&
					page.Slug == normalizedSlug,
				cancellationToken);

			if (exists)
			{
				return new PageBuilderCreateResult(
					null,
					true);
			}

			DateTime now = DateTime.UtcNow;

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
			await _db.SaveChangesAsync(cancellationToken);

			return new PageBuilderCreateResult(
				page,
				false);
		}

		public async Task<ContentPage?> UpdateAsync(
			string? section,
			string? slug,
			string? title,
			string? jsonLayout,
			CancellationToken cancellationToken = default)
		{
			ContentPage? page = await FindAsync(
				section,
				slug,
				tracking: true,
				cancellationToken);

			if (page == null)
			{
				return null;
			}

			if (!string.IsNullOrWhiteSpace(title))
			{
				page.Title = title.Trim();
			}

			if (!string.IsNullOrWhiteSpace(jsonLayout))
			{
				page.JsonLayout = jsonLayout.Trim();
			}

			page.UpdatedAt = DateTime.UtcNow;

			await _db.SaveChangesAsync(cancellationToken);
			return page;
		}

		public async Task DeleteAsync(
			string? section,
			string? slug,
			CancellationToken cancellationToken = default)
		{
			ContentPage? page = await FindAsync(
				section,
				slug,
				tracking: true,
				cancellationToken);

			if (page == null)
			{
				return;
			}

			_db.ContentPages.Remove(page);
			await _db.SaveChangesAsync(cancellationToken);
		}

		public static string NormalizeSection(string? section)
		{
			string normalized = (section ?? string.Empty)
				.Trim()
				.ToLowerInvariant();

			return normalized switch
			{
				"holy" => "holy",
				"protection" or "prot" => "protection",
				"retribution" or "retri" or "ret" =>
					"retribution",
				_ => "holy"
			};
		}

		public static string Capitalize(string value)
		{
			return string.IsNullOrWhiteSpace(value)
				? value
				: char.ToUpperInvariant(value[0]) + value[1..];
		}

		public static string Slugify(string? value)
		{
			string slug = (value ?? string.Empty)
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
	}
}
