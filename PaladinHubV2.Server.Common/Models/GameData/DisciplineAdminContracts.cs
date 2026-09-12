using System.ComponentModel.DataAnnotations;

namespace PaladinHubV2.Server.Common.Models.GameData;

public sealed record DisciplineRequest(
	[property: Required, MaxLength(100)] string Name,
	[property: MaxLength(2000)] string? Description,
	int? ParentId,
	int SortOrder,
	bool IsArchived,
	int Version);

public sealed record RevisionRestoreRequest(
	Guid RevisionId,
	int Version);

public sealed record DisciplineListItem(
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
