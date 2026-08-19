using Microsoft.EntityFrameworkCore;
using PaladinHub.Areas.Admin.Models;
using PaladinHub.Models.PageBuilder;
using PaladinHubV2.Server.Data;
using PaladinHubV2.Server.Data.Entities;

namespace PaladinHubV2.Server.Domain.Services.PageBuilder
{
	public sealed class PageBuilderAdminService : IPageBuilderAdminService
	{
		private readonly AppDbContext _db;

		public PageBuilderAdminService(AppDbContext db)
		{
			_db = db;
		}

		public CreatePageViewModel BuildCreateModel(string? section)
		{
			string normalizedSection = NormalizeSection(section);
			return new CreatePageViewModel
			{
				Section = Capitalize(normalizedSection),
				Title = string.Empty,
				Slug = string.Empty,
				IsPublished = true,
				JsonLayout = "[]"
			};
		}

		public Task<ContentPage?> GetByRouteAsync(
			string section,
			string slug,
			CancellationToken cancellationToken = default)
		{
			string normalizedSection = NormalizeSection(section);
			string normalizedSlug = Slugify(slug);

			return _db.ContentPages
				.AsNoTracking()
				.FirstOrDefaultAsync(candidate =>
					candidate.Section == normalizedSection &&
					candidate.Slug == normalizedSlug,
					cancellationToken);
		}

		public DeletePageViewModel BuildDeleteModel(ContentPage page)
		{
			return new DeletePageViewModel
			{
				Id = page.Id,
				Section = Capitalize(page.Section),
				Slug = page.Slug,
				Title = page.Title,
				CreatedAt = page.CreatedAt
			};
		}

		public async Task<PageBuilderCreateResult> CreateAsync(
			CreatePageViewModel model,
			CancellationToken cancellationToken = default)
		{
			string normalizedSection = NormalizeSection(model.Section);
			string? rawSlug = string.IsNullOrWhiteSpace(model.Slug)
				? model.Title
				: model.Slug;
			string normalizedSlug = Slugify(rawSlug);

			bool exists = await _db.ContentPages.AnyAsync(page =>
				page.Section == normalizedSection &&
				page.Slug == normalizedSlug,
				cancellationToken);

			if (exists)
			{
				return new PageBuilderCreateResult(true, null, null);
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
				JsonLayout = string.IsNullOrWhiteSpace(model.JsonLayout)
					? "[]"
					: model.JsonLayout.Trim(),
				CreatedAt = now,
				UpdatedAt = now,
				RowVersion = Array.Empty<byte>()
			};

			_db.ContentPages.Add(page);
			await _db.SaveChangesAsync(cancellationToken);

			string redirectUrl = $"/{Capitalize(normalizedSection)}/{normalizedSlug}";
			return new PageBuilderCreateResult(false, page, redirectUrl);
		}

		public async Task<PageBuilderEditResult?> EditAsync(
			EditPageRequest request,
			CancellationToken cancellationToken = default)
		{
			string normalizedSection = NormalizeSection(request.Section);
			string normalizedSlug = Slugify(request.Slug);

			ContentPage? page = await _db.ContentPages
				.FirstOrDefaultAsync(candidate =>
					candidate.Section == normalizedSection &&
					candidate.Slug == normalizedSlug,
					cancellationToken);

			if (page == null)
			{
				return null;
			}

			if (!string.IsNullOrWhiteSpace(request.Title))
			{
				page.Title = request.Title.Trim();
			}

			if (!string.IsNullOrWhiteSpace(request.JsonLayout))
			{
				page.JsonLayout = request.JsonLayout.Trim();
			}

			page.UpdatedAt = DateTime.UtcNow;
			await _db.SaveChangesAsync(cancellationToken);

			return new PageBuilderEditResult(
				page,
				$"/{Capitalize(page.Section)}/{page.Slug}");
		}

		public async Task DeleteAsync(
			string section,
			string slug,
			CancellationToken cancellationToken = default)
		{
			string normalizedSection = NormalizeSection(section);
			string normalizedSlug = Slugify(slug);

			ContentPage? page = await _db.ContentPages
				.FirstOrDefaultAsync(candidate =>
					candidate.Section == normalizedSection &&
					candidate.Slug == normalizedSlug,
					cancellationToken);

			if (page == null)
			{
				return;
			}

			_db.ContentPages.Remove(page);
			await _db.SaveChangesAsync(cancellationToken);
		}

		public string DisplaySection(string section) => Capitalize(section);

		private static string NormalizeSection(string? section)
		{
			string normalized = (section ?? string.Empty).Trim().ToLowerInvariant();
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
			string slug = (value ?? string.Empty).Trim().ToLowerInvariant();
			slug = new string(slug
				.Where(character => char.IsLetterOrDigit(character) || character == '-')
				.ToArray());
			slug = string.Join(
				"-",
				slug.Split('-', StringSplitOptions.RemoveEmptyEntries));
			return string.IsNullOrWhiteSpace(slug) ? "page" : slug;
		}
	}
}
