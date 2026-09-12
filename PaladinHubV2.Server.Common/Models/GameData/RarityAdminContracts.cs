using System.ComponentModel.DataAnnotations;

namespace PaladinHubV2.Server.Common.Models.GameData;

public sealed record RarityRequest(
	[property: Required, MaxLength(50)] string Name,
	[property: MaxLength(2000)] string? Description,
	[property: Required, RegularExpression("^#[0-9a-fA-F]{6}$")] string Color,
	int SortOrder,
	bool IsArchived,
	int Version);

public sealed record RarityListItem(
	int Id,
	string Name,
	string Description,
	string Color,
	int? ParentId,
	int SortOrder,
	bool IsArchived,
	bool IsDeleted,
	int Version,
	int UsageCount,
	int ChildCount);
