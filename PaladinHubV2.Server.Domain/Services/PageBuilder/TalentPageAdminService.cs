using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using PaladinHubV2.Server.Data;
using PaladinHubV2.Server.Data.Entities;

namespace PaladinHubV2.Server.Domain.Services.PageBuilder;

public enum TalentPageAdminError
{
	None,
	TitleRequired,
	InvalidSection,
	InvalidLayout,
	InvalidSlug,
	SlugExists,
	InvalidVersion,
	VersionRequired,
	NotFound,
	ConcurrencyConflict
}

public sealed record TalentPageAdminResult(
	TalentPageAdminError Error,
	ContentPage? Page = null,
	byte[]? RowVersion = null,
	string[]? ValidationErrors = null);

public sealed class TalentPageAdminService
{
	private static readonly string[] ReservedSlugs =
	[
		"overview",
		"gear",
		"talents",
		"consumables",
		"rotation",
		"stats"
	];

	private readonly AppDbContext _db;
	private readonly IJsonLayoutValidator _validator;
	private readonly IPageService _pages;

	public TalentPageAdminService(
		AppDbContext db,
		IJsonLayoutValidator validator,
		IPageService pages)
	{
		_db = db;
		_validator = validator;
		_pages = pages;
	}

	public Task<List<ContentPage>> ListAsync(
		CancellationToken cancellationToken = default)
	{
		return _db.ContentPages
			.AsNoTracking()
			.Where(page =>
				page.JsonLayout.Contains("talenttree.dynamic"))
			.OrderBy(page => page.Section)
			.ThenBy(page => page.Title)
			.ToListAsync(cancellationToken);
	}

	public Task<ContentPage?> GetAsync(int id)
	{
		return _pages.GetByIdAsync(id);
	}

	public async Task<TalentPageAdminResult> CreateAsync(
		string? title,
		string? section,
		string? slug,
		string jsonLayout,
		string? actor,
		CancellationToken cancellationToken = default)
	{
		if (string.IsNullOrWhiteSpace(title))
		{
			return new TalentPageAdminResult(
				TalentPageAdminError.TitleRequired);
		}

		string normalizedSection =
			(section ?? string.Empty).ToLowerInvariant();

		if (normalizedSection is not
			("holy" or "protection" or "retribution"))
		{
			return new TalentPageAdminResult(
				TalentPageAdminError.InvalidSection);
		}

		TalentPageAdminResult? layoutError =
			ValidateLayout(jsonLayout);

		if (layoutError != null)
		{
			return layoutError;
		}

		string normalizedSlug = Slugify(
			string.IsNullOrWhiteSpace(slug)
				? title
				: slug);

		if (normalizedSlug.Length is 0 or > 100 ||
			ReservedSlugs.Contains(normalizedSlug))
		{
			return new TalentPageAdminResult(
				TalentPageAdminError.InvalidSlug);
		}

		bool exists = await _db.ContentPages
			.IgnoreQueryFilters()
			.AnyAsync(
				page =>
					page.Section == normalizedSection &&
					page.Slug == normalizedSlug,
				cancellationToken);

		if (exists)
		{
			return new TalentPageAdminResult(
				TalentPageAdminError.SlugExists);
		}

		DateTime now = DateTime.UtcNow;
		var page = new ContentPage
		{
			Title = title.Trim(),
			Section = normalizedSection,
			Slug = normalizedSlug,
			JsonLayout = jsonLayout,
			IsPublished = true,
			CreatedAt = now,
			UpdatedAt = now,
			UpdatedBy = actor
		};

		_db.ContentPages.Add(page);
		await _db.SaveChangesAsync(cancellationToken);

		return new TalentPageAdminResult(
			TalentPageAdminError.None,
			page);
	}

	public async Task<TalentPageAdminResult> UpdateAsync(
		int id,
		string jsonLayout,
		string? rowVersionBase64,
		string actor)
	{
		byte[] version;

		try
		{
			version = Convert.FromBase64String(
				rowVersionBase64 ?? string.Empty);
		}
		catch (FormatException)
		{
			return new TalentPageAdminResult(
				TalentPageAdminError.InvalidVersion);
		}

		if (version.Length == 0)
		{
			return new TalentPageAdminResult(
				TalentPageAdminError.VersionRequired);
		}

		if (await _pages.GetByIdAsync(id) == null)
		{
			return new TalentPageAdminResult(
				TalentPageAdminError.NotFound);
		}

		TalentPageAdminResult? layoutError =
			ValidateLayout(jsonLayout);

		if (layoutError != null)
		{
			return layoutError;
		}

		(bool saved, byte[]? nextVersion) =
			await _pages.UpdateLayoutSafeAsync(
				id,
				jsonLayout,
				version,
				actor);

		if (!saved || nextVersion == null)
		{
			return new TalentPageAdminResult(
				TalentPageAdminError.ConcurrencyConflict);
		}

		return new TalentPageAdminResult(
			TalentPageAdminError.None,
			RowVersion: nextVersion);
	}

	private TalentPageAdminResult? ValidateLayout(string jsonLayout)
	{
		try
		{
			_validator.ValidateOrThrow(jsonLayout);

			using JsonDocument document =
				JsonDocument.Parse(jsonLayout);

			var ids = new HashSet<string>();

			foreach (
				JsonElement block in
				document.RootElement.EnumerateArray())
			{
				if (block.GetProperty("type").GetString() !=
					"talenttree.dynamic")
				{
					continue;
				}

				string id = block.GetProperty("id").GetString()!;

				if (!ids.Add(id))
				{
					throw new JsonLayoutValidationException(
						["Dynamic tree IDs must be unique."]);
				}
			}

			return null;
		}
		catch (JsonLayoutValidationException exception)
		{
			return new TalentPageAdminResult(
				TalentPageAdminError.InvalidLayout,
				ValidationErrors: exception.Errors.ToArray());
		}
	}

	private static string Slugify(string value)
	{
		return Regex.Replace(
				value.Trim().ToLowerInvariant(),
				@"[^\p{L}\p{Nd}]+",
				"-")
			.Trim('-');
	}
}
