using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using PaladinHubV2.Server.Data;
using PaladinHubV2.Server.Data.Entities;
using PaladinHubV2.Server.Domain.Services.GameData;

namespace PaladinHubV2.Server.Domain.Services.GameDataAdmin;

public sealed record SpellIconLibraryEntry(string Name, string Icon, string Kind);

public sealed record SpellIconLibraryPage(
	int Page,
	int Pages,
	int Total,
	IReadOnlyList<SpellIconLibraryEntry> Icons);

public enum SpellIconUploadError
{
	None,
	InvalidSize,
	InvalidType
}

public sealed record SpellIconUploadResult(
	SpellIconUploadError Error,
	string? Icon = null,
	string? Name = null);

public sealed record SpellIconImageResult(byte[] Content, string ContentType);

public sealed class SpellIconLibraryService
{
	public const int MaxUploadBytes = 5 * 1024 * 1024;

	private sealed record MediaSnapshot(
		string Name,
		string AltText,
		string Description,
		bool IsArchived,
		bool IsDeleted);

	private readonly AppDbContext _db;
	private readonly GameDataAssignmentService _assignments;

	public SpellIconLibraryService(
		AppDbContext db,
		GameDataAssignmentService assignments)
	{
		_db = db;
		_assignments = assignments;
	}

	public async Task<SpellIconLibraryPage> BrowseAsync(
		string? search,
		int page,
		int pageSize,
		CancellationToken cancellationToken)
	{
		page = Math.Max(1, page);
		pageSize = Math.Clamp(pageSize, 1, 100);

		var spells = await _db.Spells
			.AsNoTracking()
			.Where(spell => spell.Icon != null && spell.Icon != "")
			.Select(spell => new
			{
				spell.Name,
				spell.Icon,
				spell.Quality
			})
			.ToListAsync(cancellationToken);

		var uploads = await _db.SpellIcons
			.AsNoTracking()
			.Where(icon => !icon.IsArchived && !icon.IsDeleted)
			.Select(icon => new
			{
				icon.Id,
				icon.Name
			})
			.ToListAsync(cancellationToken);

		IEnumerable<SpellIconLibraryEntry> records = spells
			.Select(spell => new SpellIconLibraryEntry(
				spell.Name,
				spell.Icon!,
				spell.Quality))
			.Concat(uploads.Select(icon => new SpellIconLibraryEntry(
				icon.Name,
				$"/api/spell-icons/{icon.Id}",
				"upload")));

		var items = await _db.Items
			.AsNoTracking()
			.Select(item => new
			{
				item.Name,
				item.Icon,
				item.SecondIcon
			})
			.ToListAsync(cancellationToken);

		records = records.Concat(
			items.SelectMany(item =>
				new[] { item.Icon, item.SecondIcon }
					.Where(value => !string.IsNullOrWhiteSpace(value))
					.Select(value => new SpellIconLibraryEntry(
						item.Name,
						value!.StartsWith("/") || value.Contains("://")
							? value
							: "/images/ItemIcons/" + Uri.EscapeDataString(value),
						"item"))));

		if (!string.IsNullOrWhiteSpace(search))
		{
			string text = search.Trim();
			records = records.Where(icon =>
				icon.Name.Contains(text, StringComparison.OrdinalIgnoreCase) ||
				icon.Icon.Contains(text, StringComparison.OrdinalIgnoreCase));
		}

		List<string> hiddenIds = await _db.SpellIcons
			.Where(icon => icon.IsArchived || icon.IsDeleted)
			.Select(icon => icon.Id.ToString())
			.ToListAsync(cancellationToken);

		records = records.Where(record =>
			!hiddenIds.Any(id =>
				record.Icon.Contains(id, StringComparison.OrdinalIgnoreCase)));

		List<SpellIconLibraryEntry> all = records
			.DistinctBy(icon => icon.Icon)
			.OrderBy(icon => icon.Name)
			.ThenBy(icon => icon.Icon)
			.ToList();

		int pages = Math.Max(
			1,
			(int)Math.Ceiling(all.Count / (double)pageSize));
		page = Math.Min(page, pages);

		return new SpellIconLibraryPage(
			page,
			pages,
			all.Count,
			all.Skip((page - 1) * pageSize).Take(pageSize).ToList());
	}

	public async Task<SpellIconUploadResult> UploadAsync(
		string fileName,
		byte[] content,
		string actor,
		CancellationToken cancellationToken)
	{
		if (content.Length is <= 0 or > MaxUploadBytes)
		{
			return new SpellIconUploadResult(SpellIconUploadError.InvalidSize);
		}

		string? contentType = ImageType(content);
		if (contentType == null)
		{
			return new SpellIconUploadResult(SpellIconUploadError.InvalidType);
		}

		string name = Path.GetFileName(fileName);
		var icon = new SpellIcon
		{
			Id = Guid.NewGuid(),
			Name = name.Length > 255 ? name[..255] : name,
			ContentType = contentType,
			Content = content,
			CreatedAtUtc = DateTime.UtcNow
		};

		await using var transaction =
			await _assignments.BeginAsync(cancellationToken);

		_db.SpellIcons.Add(icon);
		RecordUploaded(icon, actor);
		await _db.SaveChangesAsync(cancellationToken);
		await transaction.CommitAsync(cancellationToken);

		return new SpellIconUploadResult(
			SpellIconUploadError.None,
			$"/api/spell-icons/{icon.Id}",
			icon.Name);
	}

	public Task<SpellIconImageResult?> GetImageAsync(
		Guid id,
		CancellationToken cancellationToken)
	{
		return _db.SpellIcons
			.AsNoTracking()
			.Where(icon => icon.Id == id)
			.Select(icon => new SpellIconImageResult(
				icon.Content,
				icon.ContentType))
			.SingleOrDefaultAsync(cancellationToken);
	}

	private void RecordUploaded(SpellIcon icon, string actor)
	{
		_db.MediaRevisions.Add(new MediaRevision
		{
			MediaId = icon.Id,
			Version = icon.Version,
			Action = "uploaded",
			Actor = actor,
			Snapshot = JsonSerializer.Serialize(
				new MediaSnapshot(
					icon.Name,
					icon.AltText,
					icon.Description,
					icon.IsArchived,
					icon.IsDeleted))
		});
	}

	private static string? ImageType(byte[] content)
	{
		ReadOnlySpan<byte> bytes = content.AsSpan();

		if (bytes.Length >= 24 &&
			bytes.StartsWith(new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 }))
		{
			return "image/png";
		}

		if (bytes.Length >= 3 &&
			bytes[0] == 255 &&
			bytes[1] == 216 &&
			bytes[2] == 255)
		{
			return "image/jpeg";
		}

		if (bytes.Length >= 10 &&
			(bytes.StartsWith("GIF87a"u8) || bytes.StartsWith("GIF89a"u8)))
		{
			return "image/gif";
		}

		if (bytes.Length >= 12 &&
			bytes.StartsWith("RIFF"u8) &&
			bytes.Slice(8, 4).SequenceEqual("WEBP"u8))
		{
			return "image/webp";
		}

		return null;
	}
}
