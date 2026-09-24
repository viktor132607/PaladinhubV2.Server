using PaladinHubV2.Server.Data.Entities;

namespace PaladinHubV2.Server.Domain.Services.Seo;

internal static class SeoEntryMapper
{
    internal static void Apply(
        SeoEntry entry,
        SeoRequest request,
        SeoResolvedTarget target)
    {
        entry.PageId = target.PageId;
        entry.Path = target.StoredPath;
        entry.Title = (request.Title ?? string.Empty).Trim();
        entry.Description = (request.Description ?? string.Empty).Trim();
        entry.CanonicalUrl = (request.CanonicalUrl ?? string.Empty).Trim();
        entry.SocialTitle = (request.SocialTitle ?? string.Empty).Trim();
        entry.SocialDescription = (request.SocialDescription ?? string.Empty).Trim();
        entry.SocialImageMediaId = request.SocialImageMediaId;
        entry.ImageUrl = (request.ImageUrl ?? string.Empty).Trim();
        entry.Index = request.Index;
        entry.Follow = request.Follow;
    }

    internal static SeoSnapshot ToSnapshot(SeoEntry entry) =>
        new(
            entry.PageId,
            entry.Path,
            entry.Title,
            entry.Description,
            entry.CanonicalUrl,
            entry.SocialTitle,
            entry.SocialDescription,
            entry.SocialImageMediaId,
            entry.ImageUrl,
            entry.Index,
            entry.Follow,
            entry.IsArchived,
            entry.IsDeleted);

    internal static SeoRequest FromSnapshot(SeoSnapshot snapshot, int version) =>
        new(
            snapshot.PageId,
            snapshot.Path,
            snapshot.Title,
            snapshot.Description,
            snapshot.CanonicalUrl,
            snapshot.SocialTitle,
            snapshot.SocialDescription,
            snapshot.SocialImageMediaId,
            snapshot.ImageUrl,
            snapshot.Index,
            snapshot.Follow,
            version);

    internal static SeoRequest ToRequest(SeoEntry entry, int version) =>
        FromSnapshot(ToSnapshot(entry), version);

    internal static SeoAdminItem ToAdminItem(
        SeoEntry entry,
        IReadOnlyDictionary<int, ContentPage> pages)
    {
        SeoResolvedTarget? target = null;
        if (entry.PageId.HasValue &&
            pages.TryGetValue(entry.PageId.Value, out ContentPage? page) &&
            SeoRouteRegistry.TryBuildDatabasePagePath(
                page.Section,
                page.Slug,
                out string pagePath,
                out _))
        {
            target = new SeoResolvedTarget(
                page.Id,
                string.Empty,
                pagePath,
                page.Title,
                "database");
        }

        target ??= new SeoResolvedTarget(
            entry.PageId,
            entry.Path,
            entry.Path,
            entry.PageId.HasValue
                ? "Unavailable target"
                : entry.Path == "*"
                    ? "Global defaults"
                    : entry.Path,
            entry.PageId.HasValue
                ? "database"
                : entry.Path == "*"
                    ? "global"
                    : "static");

        return ToAdminItem(entry, target);
    }

    internal static SeoAdminItem ToAdminItem(
        SeoEntry entry,
        SeoResolvedTarget? target)
    {
        target ??= new SeoResolvedTarget(
            entry.PageId,
            entry.Path,
            entry.Path,
            "Unavailable target",
            entry.PageId.HasValue ? "database" : "static");

        return new SeoAdminItem(
            entry.Id,
            entry.PageId,
            entry.Path,
            target.ResolvedPath,
            target.TargetType,
            target.Label,
            entry.Title,
            entry.Description,
            entry.CanonicalUrl,
            entry.SocialTitle,
            entry.SocialDescription,
            entry.SocialImageMediaId,
            entry.ImageUrl,
            entry.Index,
            entry.Follow,
            entry.IsArchived,
            entry.IsDeleted,
            entry.Version);
    }
}
