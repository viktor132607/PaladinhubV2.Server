using System.ComponentModel.DataAnnotations;

namespace PaladinHubV2.Server.Common.Models.GameData;

public sealed record MediaRequest(
	[property: Required, MaxLength(255)] string Name,
	[property: MaxLength(500)] string? AltText,
	[property: MaxLength(2000)] string? Description,
	bool IsArchived,
	int Version);

public sealed record MediaListItem(
	Guid Id,
	string Name,
	string AltText,
	string Description,
	bool IsArchived,
	bool IsDeleted,
	int Version,
	DateTime CreatedAtUtc,
	string ContentType,
	int Size,
	string Icon,
	int UsageCount);

public sealed record MediaPageResult(
	IReadOnlyList<MediaListItem> Media,
	int Page,
	int Pages,
	int Total);
