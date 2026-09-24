using Microsoft.EntityFrameworkCore;
using PaladinHubV2.Server.Data;
using PaladinHubV2.Server.Data.Entities;

namespace PaladinHubV2.Server.Domain.Services.Seo;

public interface ISeoPublicSnapshotService
{
    Task<SeoPublicSnapshot> BuildAsync(
        string? siteUrl,
        string apiOrigin,
        CancellationToken ct);
}

public sealed class SeoPublicSnapshotService : ISeoPublicSnapshotService
{
    private readonly AppDbContext _db;

    public SeoPublicSnapshotService(AppDbContext db)
    {
        _db = db;
    }

    public async Task<SeoPublicSnapshot> BuildAsync(
        string? siteUrl,
        string apiOrigin,
        CancellationToken ct)
    {
        string normalizedSiteUrl = SeoUrlPolicy.NormalizeOrigin(siteUrl);
        string normalizedApiOrigin = SeoUrlPolicy.NormalizeOrigin(apiOrigin);

        List<ContentPage> pages = await _db.ContentPages
            .IgnoreQueryFilters()
            .AsNoTracking()
            .Where(page =>
                page.IsPublished &&
                !page.IsDeleted &&
                !page.IsArchived)
            .OrderBy(page => page.Section)
            .ThenBy(page => page.Slug)
            .ToListAsync(ct);

        Dictionary<int, ContentPage> pageMap = pages.ToDictionary(page => page.Id);

        List<SeoEntry> candidates = await _db.Set<SeoEntry>()
            .AsNoTracking()
            .Where(entry => !entry.IsDeleted && !entry.IsArchived)
            .OrderBy(entry => entry.Path)
            .ThenBy(entry => entry.Id)
            .ToListAsync(ct);

        Guid[] mediaIds = candidates
            .Where(entry => entry.SocialImageMediaId.HasValue)
            .Select(entry => entry.SocialImageMediaId!.Value)
            .Distinct()
            .ToArray();

        HashSet<Guid> activeMedia = mediaIds.Length == 0
            ? []
            : (await _db.SpellIcons
                    .AsNoTracking()
                    .Where(media =>
                        mediaIds.Contains(media.Id) &&
                        !media.IsDeleted &&
                        !media.IsArchived)
                    .Select(media => media.Id)
                    .ToListAsync(ct))
                .ToHashSet();

        List<SeoPublicEntry> publicEntries = [];
        foreach (SeoEntry entry in candidates)
        {
            string? resolvedPath = SeoTargetResolver.ResolvePublicPath(
                entry,
                pageMap);
            if (resolvedPath is null)
            {
                continue;
            }

            string imageUrl = entry.ImageUrl;
            if (entry.SocialImageMediaId.HasValue)
            {
                if (!activeMedia.Contains(entry.SocialImageMediaId.Value))
                {
                    continue;
                }

                imageUrl = SeoUrlPolicy.CombineOriginAndPath(
                    normalizedApiOrigin,
                    $"/api/spell-icons/{entry.SocialImageMediaId.Value}");
            }

            string canonical = entry.CanonicalUrl;
            if (string.IsNullOrWhiteSpace(canonical) && resolvedPath != "*")
            {
                canonical = SeoUrlPolicy.CombineOriginAndPath(
                    normalizedSiteUrl,
                    resolvedPath);
            }

            publicEntries.Add(new SeoPublicEntry(
                entry.Id,
                entry.Version,
                entry.PageId,
                resolvedPath,
                entry.Title,
                entry.Description,
                canonical,
                entry.SocialTitle,
                entry.SocialDescription,
                imageUrl,
                entry.Index,
                entry.Follow));
        }

        List<SeoPublicPage> publicPages = [];
        foreach (ContentPage page in pages)
        {
            if (!SeoRouteRegistry.TryBuildDatabasePagePath(
                    page.Section,
                    page.Slug,
                    out string path,
                    out _))
            {
                continue;
            }

            if (SeoRouteRegistry.StaticSeoTargets.Any(route =>
                    route.Route.Equals(path, StringComparison.OrdinalIgnoreCase)))
            {
                continue;
            }

            publicPages.Add(new SeoPublicPage(page.Id, page.Title, path));
        }

        List<string> resourceRoutes = [];
        await AddProductResourcesAsync(
            publicEntries,
            resourceRoutes,
            ct);
        await AddDiscussionResourcesAsync(
            publicEntries,
            resourceRoutes,
            ct);

        string[] staticRoutes = SeoRouteRegistry.StaticSeoTargets
            .Select(route => route.Route)
            .Concat(resourceRoutes)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        string snapshotVersion = SeoUrlPolicy.BuildSnapshotVersion(
            publicEntries,
            publicPages,
            staticRoutes,
            normalizedSiteUrl,
            apiOrigin);

        return new SeoPublicSnapshot(
            normalizedSiteUrl,
            SeoRouteRegistry.Version,
            snapshotVersion,
            DateTime.UtcNow,
            staticRoutes,
            publicPages,
            publicEntries);
    }

    private async Task AddProductResourcesAsync(
        List<SeoPublicEntry> publicEntries,
        List<string> resourceRoutes,
        CancellationToken ct)
    {
        List<Product> products = await _db.Products
            .AsNoTracking()
            .Include(product => product.Images)
            .OrderBy(product => product.Id)
            .ToListAsync(ct);

        foreach (Product product in products)
        {
            if (!Guid.TryParse(product.Id, out _))
            {
                continue;
            }

            string path = "/products/" + product.Id;
            resourceRoutes.Add(path);
            resourceRoutes.Add("/Products/Details/" + product.Id);

            string image = product.Images
                .OrderBy(image => image.Id == product.ThumbnailImageId ? 0 : 1)
                .ThenBy(image => image.SortOrder)
                .Select(image => image.Url)
                .FirstOrDefault() ?? string.Empty;

            AddResourceEntry(
                publicEntries,
                path,
                product.Name,
                product.Description ?? string.Empty,
                image);
        }
    }

    private async Task AddDiscussionResourcesAsync(
        List<SeoPublicEntry> publicEntries,
        List<string> resourceRoutes,
        CancellationToken ct)
    {
        List<DiscussionPost> discussions = await _db.DiscussionPosts
            .AsNoTracking()
            .OrderBy(post => post.Id)
            .ToListAsync(ct);

        foreach (DiscussionPost post in discussions)
        {
            string path = "/Discussions/Details/" + post.Id;
            resourceRoutes.Add(path);
            AddResourceEntry(
                publicEntries,
                path,
                post.Title,
                post.Content,
                string.Empty);
        }
    }

    private static void AddResourceEntry(
        List<SeoPublicEntry> publicEntries,
        string path,
        string title,
        string description,
        string image)
    {
        if (publicEntries.Any(entry =>
                entry.Path.Equals(path, StringComparison.OrdinalIgnoreCase)))
        {
            return;
        }

        string plain = System.Net.WebUtility.HtmlDecode(
            System.Text.RegularExpressions.Regex.Replace(
                description,
                "<[^>]*>",
                " ",
                System.Text.RegularExpressions.RegexOptions.None,
                TimeSpan.FromSeconds(1)));

        if (plain.Length > 500)
        {
            plain = plain[..500];
        }

        if (!SeoUrlPolicy.IsSafeAbsoluteHttpUrl(image))
        {
            image = string.Empty;
        }

        publicEntries.Add(new SeoPublicEntry(
            SeoUrlPolicy.ResourceId(path),
            1,
            null,
            path,
            title,
            plain,
            string.Empty,
            string.Empty,
            string.Empty,
            image,
            null,
            null));
    }
}
