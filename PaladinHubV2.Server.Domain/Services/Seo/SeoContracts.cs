namespace PaladinHubV2.Server.Domain.Services.Seo;

public sealed record SeoRequest(
    int? PageId,
    string? Path,
    string? Title,
    string? Description,
    string? CanonicalUrl,
    string? SocialTitle,
    string? SocialDescription,
    Guid? SocialImageMediaId,
    string? ImageUrl,
    bool? Index,
    bool? Follow,
    int Version);

public sealed record SeoResult(
    int Status,
    string? Message = null,
    SeoAdminItem? Entry = null);

public sealed record SeoAdminItem(
    Guid Id,
    int? PageId,
    string Path,
    string ResolvedPath,
    string TargetType,
    string TargetLabel,
    string Title,
    string Description,
    string CanonicalUrl,
    string SocialTitle,
    string SocialDescription,
    Guid? SocialImageMediaId,
    string ImageUrl,
    bool? Index,
    bool? Follow,
    bool IsArchived,
    bool IsDeleted,
    int Version);

public sealed record SeoHistoryItem(
    Guid Id,
    int Version,
    string Action,
    string Actor,
    DateTime CreatedAtUtc);

public sealed record SeoTargetPage(
    int Id,
    string Title,
    string Path,
    bool IsPublished);

public sealed record SeoPublicEntry(
    Guid Id,
    int Version,
    int? PageId,
    string Path,
    string Title,
    string Description,
    string CanonicalUrl,
    string SocialTitle,
    string SocialDescription,
    string ImageUrl,
    bool? Index,
    bool? Follow);

public sealed record SeoPublicPage(
    int Id,
    string Title,
    string Path);

public sealed record SeoPublicSnapshot(
    string SiteUrl,
    string RegistryVersion,
    string SnapshotVersion,
    DateTime GeneratedAtUtc,
    IReadOnlyList<string> StaticRoutes,
    IReadOnlyList<SeoPublicPage> Pages,
    IReadOnlyList<SeoPublicEntry> Entries);

public sealed record SeoResolvedTarget(
    int? PageId,
    string StoredPath,
    string ResolvedPath,
    string Label,
    string TargetType);

public sealed record SeoResolvedTargetResult(
    SeoResolvedTarget? Target,
    int Status,
    string? Error);

internal sealed record SeoSnapshot(
    int? PageId,
    string Path,
    string Title,
    string Description,
    string CanonicalUrl,
    string SocialTitle,
    string SocialDescription,
    Guid? SocialImageMediaId,
    string ImageUrl,
    bool? Index,
    bool? Follow,
    bool IsArchived,
    bool IsDeleted);
