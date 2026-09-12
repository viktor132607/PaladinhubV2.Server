using System.ComponentModel.DataAnnotations;

namespace PaladinHubV2.Server.Common.Models.Navigation;

public sealed record NavigationRequest([property: Required, MaxLength(100)] string Name,
    [property: MaxLength(2000)] string? Description, [property: Required, MaxLength(2048)] string Href,
    [property: Required] string Location, bool OpenNewTab, int? ParentId, int SortOrder, bool IsArchived, int Version);
public sealed record RestoreNavigationRequest(Guid RevisionId, int Version);
public sealed record NavigationAdminRow(int Id, string Name, string Description, string Href, string Location,
    bool OpenNewTab, int? ParentId, int SortOrder, bool IsArchived, bool IsDeleted, int Version, int UsageCount, int ChildCount);
public sealed record PublicNavigationRow(int Id, string Name, string Href, string Location, bool OpenNewTab, int? ParentId, int SortOrder);
