using System.ComponentModel.DataAnnotations;

namespace PaladinHubV2.Server.Common.Models.GameData;

public sealed record CategoryRequest(
	[property: Required, MaxLength(100)] string Name,
	[property: MaxLength(2000)] string? Description,
	int? ParentId,
	int SortOrder,
	bool IsArchived,
	int Version);

public sealed record CategoryListItem(
	int Id,
	string Name,
	string Description,
	int? ParentId,
	int SortOrder,
	bool IsArchived,
	bool IsDeleted,
	int Version,
	int UsageCount,
	int ChildCount);
